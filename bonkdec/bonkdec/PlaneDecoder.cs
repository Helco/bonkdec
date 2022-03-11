namespace Bonk;
using System;
using System.IO;
using System.Runtime.CompilerServices;

internal unsafe partial class PlaneDecoder
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
    internal byte[] Target => sampleBuffers[curBuffer];
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

        curBuffer ^= 1;
        Array.Fill(Target, (byte)0xFF);
        var blockRowOffset = BlockSize * width - width;
        fixed (byte* sourceBuf = Source)
        fixed (byte* targetBuf = Target)
        {
            byte* sourcePtr = sourceBuf, targetPtr = targetBuf;
            for (int y = 0; y < height; y += BlockSize)
            {
                var prevOffset = bitStream.currentOffset;
                var prevBits = bitStream.bitsLeft;
                bundleBlockType.FillRLE(ref bitStream);
                bundleSubBlockType.FillRLE(ref bitStream);
                bundleColors.Fill(ref bitStream);
                bundlePattern.FillPairs(ref bitStream);
                bundleXMotion.FillSimple(ref bitStream);
                bundleYMotion.FillSimple(ref bitStream);
                bundleDCIntra.Fill(ref bitStream);
                bundleDCInter.Fill(ref bitStream);
                bundlePatternLengths.FillSimple(ref bitStream);
                if (y != 0 && (prevOffset != bitStream.currentOffset || prevBits != bitStream.bitsLeft))
                {
                    Console.WriteLine($"{y}");
                    break;
                }

                for (int x = 0; x < width; x += BlockSize, sourcePtr += BlockSize, targetPtr += BlockSize)
                    DecodeBlock(ref sourcePtr, ref targetPtr, ref x, y);

                sourcePtr += blockRowOffset;
                targetPtr += blockRowOffset;
            }
        }

        bitStream.AlignToWord();
        return buffer.Slice(bitStream.currentOffset * 4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeBlock(ref byte* sourcePtr, ref byte* targetPtr, ref int x, int y)
    {
        var blockType = bundleBlockType.Next();
        if (blockType > 2 && blockType < 6 || blockType == 7)
            Console.WriteLine(blockType);
        switch (blockType)
        {
            case 0: DecodeSkipBlock(sourcePtr, targetPtr);  break;
            case 1:
                if ((y & BlockSize) == 0) // odd rows skip scaled blocks to not override content from the even rows
                    DecodeScaledBlock(ref sourcePtr, ref targetPtr);
                x += BlockSize;
                sourcePtr += BlockSize;
                targetPtr += BlockSize;
                break;
            case 2: DecodeMotionBlock(sourcePtr, targetPtr); break;
            case 3: break; // TODO: Implement run fill blocks
            case 4: break; // TODO: Implement motion residue blocks
            case 5: break; // TODO: Implement intra blocks
            case 6: DecodeFillBlock(targetPtr); break;
            case 7: break; // TODO: Implement inter blocks
            case 8: DecodePatternFill(targetPtr); break;
            case 9: DecodeRawBlock(targetPtr); break;
            default: throw new BinkDecoderException($"Unsupported block type {blockType}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeScaledBlock(ref byte* sourcePtr, ref byte* targetPtr)
    {
        var blockType = bundleSubBlockType.Next();
        if (blockType < 6)
            Console.WriteLine($"S{blockType}");
        switch (blockType)
        {
            case 3: break; // TODO: Implement scaled run fill blocks
            case 5: break; // TODO: Implement scaled intra blocks
            case 6: DecodeScaledFillBlock(targetPtr); break;
            case 8: DecodeScaledPatternFill(targetPtr); break;
            case 9: DecodeScaledRawBlock(targetPtr); break;
            default: throw new BinkDecoderException($"Unsupport block sub type {blockType}");
        }
    }
}
