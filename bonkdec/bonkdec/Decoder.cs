namespace Bonk;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static Bonk.Utils;
using System.Collections;

/// <summary>How much validation is performed during decoding</summary>
public enum ValidationMode
{
    /// <summary>No explicit validation is performed, bad/unsupported input can cause exceptions and crashes</summary>
    Minimal,

    /// <summary>Input is validated based on the supported feature set. Unexpected input due to unsupported features can cause exceptions.</summary>
    /// <remarks>This is the default mode</remarks>
    Supported,

    /// <summary>Input is validated to check for unsupported features, which will cause exceptions.</summary>
    Pedantic
}

public readonly struct AudioTrackInfo
{
    public readonly uint ID;
    public readonly ushort Frequency;
    public readonly ushort ChannelCount;
    public readonly AudioTrackFlags Flags;
    public readonly bool IsEnabled;

    internal AudioTrackInfo(uint id, ushort frequency, AudioTrackFlags flags, bool isEnabled = false)
    {
        ID = id;
        Frequency = frequency;
        ChannelCount = 2;
        Flags = flags;
        IsEnabled = isEnabled;
    }
}

public unsafe class Decoder : IDisposable, IEnumerator<Frame>
{
    private readonly ValidationMode validationMode;
    private readonly Stream source;
    private readonly ContainerHeader container;
    internal readonly AudioTrackInfo[] audioTracks;
    internal readonly AudioDecoder?[] audioDecoders;
    private readonly PlaneDecoder? alphaDecoder;
    private readonly PlaneDecoder yDecoder;
    private readonly PlaneDecoder? cbDecoder;
    private readonly PlaneDecoder? crDecoder;
    private readonly uint[] frameOffsets;
    private readonly bool[] isKeyFrame;
    private readonly byte[] frameBuffer;
    private readonly MemoryStream frameStream;
    private readonly Frame frame;
    private int currentFrameI = -1;
    private bool shouldDisposeStream;

    public uint FrameCount => container.FrameCount;
    public uint FrameWidth => container.Width;
    public uint FrameHeight => container.Height;
    public VideoCodecRevision VideoCodecRevision => container.VideoCodecRevision;
    public VideoFlags VideoFlags => container.VideoFlags;
    public VideoScalingMode VideoScalingMode => container.VideoScalingMode;
    public uint FpsDividend => container.FpsDividend;
    public uint FpsDivider => container.FpsDivider;
    public IReadOnlyList<AudioTrackInfo> AudioTracks => audioTracks;

    public Frame Current => frame;
    object IEnumerator.Current => Current;

    public Decoder(Stream source, bool shouldDisposeStream = true, ValidationMode validationMode = ValidationMode.Supported)
    {
        this.validationMode = validationMode;
        this.source = source;
        this.shouldDisposeStream = shouldDisposeStream;

        Read(source, ref container, 1);
        if (!BitConverter.IsLittleEndian)
            container.SwapByteOrder();
        container.Validate(validationMode);

        var audioHeaders1 = new AudioTrackHeader1[container.AudioTrackCount];
        Read(source, ref audioHeaders1[0], audioHeaders1.Length);
        if (!BitConverter.IsLittleEndian)
        {
            foreach (ref var audioHeader in audioHeaders1.AsSpan())
                audioHeader.SwapByteOrder();
        }
        // TODO: Consider validation of first audio track header

        var audioHeaders2 = new AudioTrackHeader2[container.AudioTrackCount];
        Read(source, ref audioHeaders2[0], audioHeaders2.Length);
        if (!BitConverter.IsLittleEndian)
        {
            foreach (ref var audioHeader in audioHeaders2.AsSpan())
                audioHeader.SwapByteOrder();
        }
        if (validationMode != ValidationMode.Minimal)
        {
            foreach (ref readonly var audioHeader in audioHeaders2.AsSpan())
                audioHeader.Validate(validationMode);
        }

        var audioTrackIDs = new uint[container.AudioTrackCount];
        Read(source, ref audioTrackIDs[0], audioTrackIDs.Length);
        if (!BitConverter.IsLittleEndian)
        {
            foreach (ref var audioTrackID in audioTrackIDs.AsSpan())
                audioTrackID = Swap(audioTrackID);
        }
        if (validationMode != ValidationMode.Minimal)
        {
            if (audioTrackIDs.Distinct().Count() != audioTrackIDs.Length)
                throw new BinkDecoderException("Invalid duplicated audio track IDs");
        }

        audioDecoders = new AudioDecoder[audioTrackIDs.Length];
        audioTracks = audioTrackIDs
            .Select((id, i) => new AudioTrackInfo(id, audioHeaders2[i].Frequency, audioHeaders2[i].Flags))
            .ToArray();

        frameOffsets = new uint[container.FrameCount + 1];
        Read(source, ref frameOffsets[0], frameOffsets.Length);
        if (!BitConverter.IsLittleEndian)
        {
            foreach (ref var frameOffset in frameOffsets.AsSpan())
                frameOffset = Swap(frameOffset);
        }
        isKeyFrame = frameOffsets.Select(f => (f & 1) != 0).ToArray();
        foreach (ref var frameOffset in frameOffsets.AsSpan())
            frameOffset &= ~1u;
        ValidateFrameOffsets(validationMode, frameOffsets, container.MaxFrameSize);

        frameBuffer = new byte[container.MaxFrameSize];
        frameStream = new MemoryStream(frameBuffer, writable: false);
        frame = new Frame(this);

        yDecoder = new PlaneDecoder(container, subSampled: false);
        if (container.VideoFlags.HasFlag(VideoFlags.Alpha))
            alphaDecoder = new PlaneDecoder(container, subSampled: false);
        if (!container.VideoFlags.HasFlag(VideoFlags.Grayscale))
        {
            cbDecoder = new PlaneDecoder(container, subSampled: true);
            crDecoder = new PlaneDecoder(container, subSampled: true);
        }
    }

    public void ToggleAudioTrackByIndex(int index, bool enable)
    {
        if (index < 0 || index >= audioTracks.Length)
            throw new IndexOutOfRangeException($"Audio track index out of range: {index} (total: {audioTracks.Length}");
        if (enable)
            audioDecoders[index] ??= new AudioDecoder(audioTracks[index]);
        else
            audioDecoders[index] = null;
    }

    public void ToggleAllAudioTracks(bool enable)
    {
        for (int i = 0; i < audioTracks.Length; i++)
            ToggleAudioTrackByIndex(i, enable);
    }

    // TODO: Add ToggleAudioTrackByID method

    public void Reset()
    {
        currentFrameI = -1;
        source.Seek(frameOffsets[0], SeekOrigin.Begin);
    }

    public bool MoveNext()
    {
        currentFrameI++;
        if (currentFrameI >= container.FrameCount)
            return false;
        var frameSize = frameOffsets[currentFrameI + 1] - frameOffsets[currentFrameI];
        source.Flush();
        source.Position = frameOffsets[currentFrameI]; // TODO: Seeking through source should be optional
        if (source.Read(frameBuffer, 0, (int)frameSize) != frameSize)
            throw new EndOfStreamException("End of stream during reading of next frame");
        frameStream.Position = 0;

        for (int i = 0; i < AudioTracks.Count; i++)
        {
            if (audioDecoders[i] != null)
                audioDecoders[i]!.ClearSamples();
        }

        for (int i = 0; i < AudioTracks.Count; i++)
        {
            uint audioPacketSize = 0;
            Read(frameStream, ref audioPacketSize, 1);
            SwapIfNecessary(ref audioPacketSize);
            if (audioPacketSize == 0)
                continue;
            if (validationMode != ValidationMode.Minimal && frameStream.Length - frameStream.Position < audioPacketSize)
                throw new EndOfStreamException("End of stream during reading of audio packet");

            uint sampleCount = 0;
            Read(frameStream, ref sampleCount, 1);
            SwapIfNecessary(ref sampleCount);
            if (sampleCount == 0)
                continue;

            if (audioDecoders[i] != null)
                audioDecoders[i]!.Decode(sampleCount, new ReadOnlySpan<byte>(frameBuffer, (int)frameStream.Position, (int)audioPacketSize));
            frameStream.Position += audioPacketSize - 4;
        }

        if (alphaDecoder != null)
        {
            uint alphaPlaneSize = 0;
            Read(frameStream, ref alphaPlaneSize, 1);
            SwapIfNecessary(ref alphaPlaneSize);
            alphaDecoder.Decode(new ReadOnlySpan<byte>(frameBuffer, (int)frameStream.Position, (int)alphaPlaneSize));
            frameStream.Position += alphaPlaneSize - 4;
        }
        // TODO: Add old/new frame format switches in frame decoding

        uint yPlaneSize = 0;
        Read(frameStream, ref yPlaneSize, 1);
        SwapIfNecessary(ref yPlaneSize);
        var planeBuffer = new ReadOnlySpan<byte>(frameBuffer, (int)frameStream.Position, (int)yPlaneSize);
        planeBuffer = yDecoder.Decode(planeBuffer);
        frameStream.Position += yPlaneSize - 4;

        if (cbDecoder != null && crDecoder != null && false)
        {
            // TODO: Check order of color planes
            planeBuffer = cbDecoder.Decode(planeBuffer);
            crDecoder.Decode(planeBuffer);
        }

        return true;
    }

    private void Read<T>(Stream stream, ref T first, int count) where T : unmanaged
    {
        int bytesRead = stream.Read(AsByteSpan(ref first, count));
        if (validationMode != ValidationMode.Minimal && bytesRead != count * sizeof(T))
            throw new EndOfStreamException("End of stream during reading of Bink container header");
    }

    protected virtual void Dispose(bool disposing)
    {
        if (shouldDisposeStream && disposing)
            source.Dispose();
        shouldDisposeStream = false;
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private static void ValidateFrameOffsets(ValidationMode validationMode, uint[] frameOffsets, uint maxFrameSize)
    {
        if (validationMode == ValidationMode.Minimal)
            return;
        if (frameOffsets[0] == 0)
            throw new BinkDecoderException("Invalid first frame offset");
        if (maxFrameSize > int.MaxValue)
            throw new BinkDecoderException("Too large max frame size");

        for (int i = 0; i < frameOffsets.Length - 1; i++)
        {
            if (frameOffsets[i] >= frameOffsets[i + 1])
                throw new BinkDecoderException("Invalid frame offsets");
            if (frameOffsets[i + 1] - frameOffsets[i] > maxFrameSize)
                throw new BinkDecoderException("Invalid frame size");
        }
    }
}
