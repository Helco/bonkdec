namespace Bonk;
using System;
using System.Runtime.InteropServices;
using static Bonk.Utils;

public enum VideoCodecRevision : byte
{
    b = 0x62,
    d = 0x64,
    f = 0x66,
    g = 0x67,
    h = 0x68,
    i = 0x69
}

[Flags]
public enum VideoFlags : uint
{
    Grayscale = (1 << 17),
    Alpha = (1 << 20),
    ScalingMask = (7 << 28),

    SupportedMask = Grayscale | Alpha | ScalingMask
}

public enum VideoScalingMode
{
    None = 0,
    HeightDoubled = 1 << 28,
    HeightInterlaced,
    WidthDoubled,
    BothDoubled,
    BothInterlaced,
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct ContainerHeader
{
    public fixed byte Signature[3];
    public VideoCodecRevision VideoCodecRevision;
    public uint FileSize;
    public uint FrameCount;
    public uint MaxFrameSize;
    public uint FrameCountAgain;
    public uint Width;
    public uint Height;
    public uint FpsDividend;
    public uint FpsDivider;
    public VideoFlags VideoFlags;
    public uint AudioTrackCount;

    public VideoScalingMode VideoScalingMode => (VideoScalingMode)(VideoFlags & VideoFlags.SupportedMask);

    internal void SwapByteOrder()
    {
        FileSize = Swap(FileSize);
        FrameCount = Swap(FrameCount);
        MaxFrameSize = Swap(MaxFrameSize);
        FrameCountAgain = Swap(FrameCountAgain);
        Width = Swap(Width);
        Height = Swap(Height);
        FpsDividend = Swap(FpsDividend);
        FpsDivider = Swap(FpsDivider);
        VideoFlags = (VideoFlags)Swap((uint)VideoFlags);
        AudioTrackCount = Swap(AudioTrackCount);
    }

    private const uint MaxSize = 1 << 16 - 1;
    private const uint MaxAudioTracks = 256;
    internal void Validate(ValidationMode mode)
    {
        if (mode == ValidationMode.Minimal)
            return;
        if (Signature[0] != 'B' || Signature[1] != 'I' || Signature[2] != 'K')
            throw new BinkDecoderException("Invalid bink signature");
        if (!Enum.IsDefined(typeof(VideoCodecRevision), (byte)VideoCodecRevision))
            throw new BinkDecoderException("Unsupported video codec revision");
        if (Width == 0 || Width > MaxSize || Height == 0 || Height > MaxSize)
            throw new BinkDecoderException("Invalid frame width/height");
        if (VideoScalingMode != 0 && (VideoScalingMode < VideoScalingMode.HeightDoubled || VideoScalingMode > VideoScalingMode.BothInterlaced))
            throw new BinkDecoderException("Unsupported video scaling mode");

        if (mode != ValidationMode.Pedantic)
            return;
        if (FrameCount != FrameCountAgain)
            throw new BinkDecoderException("Duplicated frame count fields do not match");
        if (FpsDividend == 0 || FpsDivider == 0)
            throw new BinkDecoderException("Invalid frame per second dividend/divider");
        if (AudioTrackCount > MaxAudioTracks)
            throw new BinkDecoderException("Invalid number of audio tracks");
        if ((VideoFlags & ~VideoFlags.SupportedMask) != 0)
            throw new BinkDecoderException("Unsupported video flags");
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct AudioTrackHeader1
{
    public ushort Unknown;
    public ushort AudioChannels;

    internal void SwapByteOrder()
    {
        Unknown = Swap(Unknown);
        AudioChannels = Swap(AudioChannels);
    }
}

[Flags]
public enum AudioTrackFlags : ushort
{
    DCT = (1 << 12),
    Stereo = (1 << 13),
    Unknown14 = (1 << 14),
    Unknown15 = (1 << 15),

    SupportedMask = DCT | Stereo | Unknown14 | Unknown15
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct AudioTrackHeader2
{
    public ushort Frequency;
    public AudioTrackFlags Flags;

    internal void SwapByteOrder()
    {
        Frequency = Swap(Frequency);
        Flags = (AudioTrackFlags)Swap((ushort)Flags);
    }

    internal void Validate(ValidationMode mode)
    {
        if (mode == ValidationMode.Minimal)
            return;

        if (Frequency == 0)
            throw new BinkDecoderException("Invalid sample rate");
        if (Flags.HasFlag(AudioTrackFlags.DCT))
            throw new BinkDecoderException("Unsupported DCT audio codec");
        if (!Flags.HasFlag(AudioTrackFlags.Stereo))
            throw new BinkDecoderException("Unsupported mono audio codec");

        if (mode != ValidationMode.Pedantic)
            return;
        if ((Flags & ~AudioTrackFlags.SupportedMask) != 0)
            throw new BinkDecoderException("Unsupported audio flags");
    }
}
