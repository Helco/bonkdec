namespace Bonk;
using System;
using System.IO;
using System.Linq;

internal unsafe class PlaneDecoder
{
    private const int BlockSize = 8;
    private const int MinValueCount = 512;

    private readonly uint width, height; // aligned to blocks and subsampled
    private readonly byte[][] sampleBuffers;
    private readonly Bundle4Bit bundleBlockType;
    private readonly Bundle4Bit bundleSubBlockType;
    private readonly Bundle4Bit bundlePattern;
    private readonly Bundle4Bit bundleXMotion;
    private readonly Bundle4Bit bundleYMotion;
    private readonly Bundle4Bit bundlePatternLengths;
    private readonly Bundle8Bit bundleColors;
    private readonly Bundle16Bit bundleDCIntra;
    private readonly Bundle16Bit bundleDCInter;

    private int curBuffer = 0;
    private byte[] Target => sampleBuffers[curBuffer];
    private byte[] Source => sampleBuffers[curBuffer ^ 1];

    private static readonly BinaryWriter bw = new BinaryWriter(new FileStream("myvid.bin", FileMode.Create, FileAccess.Write));

    public PlaneDecoder(in ContainerHeader header, bool subSampled)
    {
        static uint AdaptSize(uint value, bool subSampled)
        {
            value = subSampled ? (value + 1) / 2 : value;
            return (value + 7u) & ~7u;
        }

        var areColorsSigned = false; // TODO: Add actual 8Bit bundle old format switch

        width = AdaptSize(header.Width, subSampled);
        height = AdaptSize(header.Height, subSampled);
        sampleBuffers = new[]
        {
            new byte[width * height],
            new byte[width * height]
        };
        bundleBlockType      = new Bundle4Bit(MinValueCount, width, addBlockLinesInBuffer: 1, isSigned: false);
        bundleSubBlockType   = new Bundle4Bit(MinValueCount, width / 2, addBlockLinesInBuffer: 1, isSigned: false);
        bundlePattern        = new Bundle4Bit(MinValueCount, width, addBlockLinesInBuffer: 8, isSigned: false);
        bundleXMotion        = new Bundle4Bit(MinValueCount, width, addBlockLinesInBuffer: 1, isSigned: true);
        bundleYMotion        = new Bundle4Bit(MinValueCount, width, addBlockLinesInBuffer: 1, isSigned: true);
        bundlePatternLengths = new Bundle4Bit(MinValueCount, width, addBlockLinesInBuffer: 48, isSigned: false);
        bundleColors         = new Bundle8Bit(MinValueCount, width, addBlockLinesInBuffer: 64, areColorsSigned);
        bundleDCIntra        = new Bundle16Bit(MinValueCount, width, addBlockLinesInBuffer: 1, startBits: 11, isSigned: false);
        bundleDCInter        = new Bundle16Bit(MinValueCount, width, addBlockLinesInBuffer: 1, startBits: 11, isSigned: true);
    }

    public ReadOnlySpan<byte> Decode(ReadOnlySpan<byte> buffer)
    {
        var bitStream = new BitStream(buffer);
        bundleBlockType.Reset(ref bitStream);
        bundleSubBlockType.Reset(ref bitStream);
        bundleColors.Reset(ref bitStream);
        bundlePattern.Reset(ref bitStream);
        bundleXMotion.Reset(ref bitStream);
        bundleYMotion.Reset(ref bitStream);
        bundlePatternLengths.Reset(ref bitStream);
        bundleDCIntra.Reset();
        bundleDCInter.Reset();

        bundleBlockType.FillRLE(ref bitStream);
        bundleSubBlockType.FillRLE(ref bitStream);
        bundleColors.Fill(ref bitStream);
        bundlePattern.FillPairs(ref bitStream);
        bundleXMotion.FillSimple(ref bitStream);
        bundleYMotion.FillSimple(ref bitStream);
        bundleDCIntra.Fill(ref bitStream);
        bundleDCInter.Fill(ref bitStream);
        //bundlePatternLengths.FillSimple(ref bitStream);

        bundleDCIntra.Dump(bw);
        bundleDCInter.Dump(bw);
        bw.Flush();

        bitStream.AlignToWord();
        return buffer.Slice(bitStream.currentOffset * 4);
    }
}
