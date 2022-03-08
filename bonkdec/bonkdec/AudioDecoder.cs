namespace Bonk;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using static System.Math;

internal class AudioDecoder
{
    private static readonly int[] CriticalFrequencies = new int[]
    {
        0, 100, 200, 300, 400, 510, 630, 770, 920, 1080, 1270, 1480, 1720, 2000, 2320,
        2700, 3150, 3700, 4400, 5300, 6400, 7700, 9500, 12000, 15500,
    };

    private static readonly int[] RunLengths = new int[]
    {
        2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14, 15, 16, 32, 64
    };

    private readonly int sampleRate;
    private readonly int samplesPerFrame;
    private readonly float dctScale;
    private readonly int[] quantizerBands;
    private readonly float[] quantizers;
    private readonly short[] window;
    private readonly short[] quantCoeffs; // reused for samples before windowing
    private readonly float[] coeffs;
    private readonly int[] ip;
    private readonly float[] w;

    private short[] samples = new short[0];
    private bool isFirstDecode = true;
    private int curSampleCount = 0;

    private int SamplesPerWindow => samplesPerFrame / 16;
    private int SamplesPerBlock => samplesPerFrame - SamplesPerWindow;
    internal ReadOnlySpan<short> Samples => samples.AsSpan(0, curSampleCount);

    internal AudioDecoder(in AudioTrackInfo trackInfo)
    {
        sampleRate = trackInfo.ChannelCount * trackInfo.Frequency;
        samplesPerFrame = trackInfo.ChannelCount * (trackInfo.Frequency switch
        {
            _ when trackInfo.Frequency >= 44100 => 2048,
            _ when trackInfo.Frequency >= 22050 => 1024,
            _ => 512
        });
        dctScale = (float)(2f / Sqrt(samplesPerFrame));

        var halfSampleRate = (sampleRate + 1) / 2;
        quantizerBands = CriticalFrequencies
            .TakeWhile(f => f < halfSampleRate)
            .Select(critFreq => Max(1, samplesPerFrame / 2 * critFreq / halfSampleRate))
            .Append(samplesPerFrame / 2)
            .ToArray();

        quantizers = new float[quantizerBands.Length - 1];
        window = new short[SamplesPerWindow];
        quantCoeffs = new short[samplesPerFrame];
        coeffs = new float[samplesPerFrame];
        ip = new int[samplesPerFrame];
        w = new float[samplesPerFrame / 2];
    }

    internal void ClearSamples() => curSampleCount = 0;

    internal void Decode(uint sampleByteSize, ReadOnlySpan<byte> buffer)
    {
        int samplesLeft = curSampleCount = (int)sampleByteSize / 2;
        if (curSampleCount > samples.Length)
            samples = new short[(curSampleCount + samplesPerFrame - 1) / samplesPerFrame * samplesPerFrame];

        var nextSamples = samples.AsSpan();
        var bitStream = new BitStream(buffer);
        while (samplesLeft > 0)
        {
            bitStream.AlignToWord();
            coeffs[0] = bitStream.ReadFloat29();
            coeffs[1] = bitStream.ReadFloat29();

            ReadQuantizers(ref bitStream);
            UnpackQuantizedCoefficients(ref bitStream);
            DequantizeCoefficients();
            InverseDiscreteCosineTransform();
            ApplyWindowing(nextSamples);

            int samplesToUse = Min(samplesLeft, SamplesPerBlock);
            samplesLeft -= samplesToUse;
            nextSamples = nextSamples.Slice(samplesToUse);
        }
    }

    private void ReadQuantizers(ref BitStream bitStream)
    {
        for (int i = 0; i < quantizers.Length; i++)
        {
            var exp = bitStream.Read(8);
            quantizers[i] = (float)Pow(10, exp * 0.066399999);
        }
    }

    private void UnpackQuantizedCoefficients(ref BitStream bitStream)
    {
        // Run-length encoded with each run defining (unsigned) bit size per coefficient

        int startCoeffI = 2;
        while (startCoeffI < samplesPerFrame)
        {
            int endCoeffI;
            if (bitStream.Read(1) == 0)
                endCoeffI = startCoeffI + 8;
            else
                endCoeffI = startCoeffI + 8 * RunLengths[bitStream.Read(4)];
            endCoeffI = Min(samplesPerFrame, endCoeffI);

            var coeffBitSize = bitStream.Read(4);
            if (coeffBitSize == 0)
            {
                Array.Fill<short>(quantCoeffs, 0, startCoeffI, endCoeffI - startCoeffI);
                startCoeffI = endCoeffI;
                continue;
            }

            for (; startCoeffI < endCoeffI; startCoeffI++)
            {
                quantCoeffs[startCoeffI] = (short)bitStream.Read((int)coeffBitSize);
                if (quantCoeffs[startCoeffI] != 0 && bitStream.Read(1) != 0)
                    quantCoeffs[startCoeffI] = (short)-quantCoeffs[startCoeffI];
            }
        }
    }

    private void DequantizeCoefficients()
    {
        int coeffI = 2;
        for (int bandI = 0; bandI < quantizerBands.Length - 1; bandI++)
        {
            var bandSize = quantizerBands[bandI + 1] - quantizerBands[bandI];
            for (int i = 0; i < bandSize; i++)
            {
                coeffs[coeffI] = quantizers[bandI] * quantCoeffs[coeffI++];
                coeffs[coeffI] = quantizers[bandI] * quantCoeffs[coeffI++];
            }
        }
    }

    private void InverseDiscreteCosineTransform()
    {
        RDFT.rdft(samplesPerFrame, -1, coeffs, ip, w);
        for (int i = 0; i < coeffs.Length; i++)
            quantCoeffs[i] = (short)Clamp(dctScale * coeffs[i], short.MinValue, short.MaxValue);
    }

    private void ApplyWindowing(Span<short> output)
    {
        if (isFirstDecode)
        {
            isFirstDecode = false;
            quantCoeffs
                .AsSpan(0, SamplesPerBlock)
                .CopyTo(output);
        }
        else
        {
            for (int i = 0; i < SamplesPerWindow; i++)
                output[i] = (short)((quantCoeffs[i] * i + window[i] * (SamplesPerWindow - i)) / SamplesPerWindow);
            quantCoeffs
                .AsSpan(SamplesPerWindow, SamplesPerBlock - SamplesPerWindow)
                .CopyTo(output.Slice(SamplesPerWindow));
        }

        quantCoeffs.AsSpan(SamplesPerBlock).CopyTo(window);        
    }
}
