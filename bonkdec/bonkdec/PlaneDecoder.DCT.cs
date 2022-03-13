namespace Bonk;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

unsafe partial class PlaneDecoder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeIntraBlock(ref BitStream bitStream, byte* targetPtr)
    {
        short* values = stackalloc short[BlockSize * BlockSize];
        DecodeDCTValues(ref bitStream, bundleDCIntra, values, IntraQuantizers);

        for (int i = 0; i < BlockSize; i++)
        {
            for (int j = 0; j < BlockSize; j++)
                targetPtr[j] = (byte)*values++;
            targetPtr += width;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeScaledIntraBlock(ref BitStream bitStream, byte* targetPtr)
    {
        short* values = stackalloc short[BlockSize * BlockSize];
        DecodeDCTValues(ref bitStream, bundleDCIntra, values, IntraQuantizers);

        for (int i = 0; i < BlockSize; i++)
        {
            for (int j = 0; j < 2 * BlockSize; j += 2)
                targetPtr[j] = targetPtr[j + 1] = targetPtr[width + j] = targetPtr[width + j + 1] = (byte)*values++;
            targetPtr += 2 * width;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeInterBlock(ref BitStream bitStream, byte* sourcePtr, byte* targetPtr)
    {
        sourcePtr = unchecked(sourcePtr + bundleXMotion.Next() + bundleYMotion.Next() * width);
        short* values = stackalloc short[BlockSize * BlockSize];
        DecodeDCTValues(ref bitStream, bundleDCInter, values, InterQuantizers);

        for (int i = 0; i < BlockSize; i++)
        {
            for (int j = 0; j < BlockSize; j++)
                targetPtr[j] = (byte)(sourcePtr[j] + unchecked((sbyte)values[j]));
            sourcePtr += width;
            targetPtr += width;
            values += 8;
        }
    }

    private short* DecodeDCTValues(ref BitStream bitStream, Bundle16Bit dcBundle, short* values, ReadOnlySpan<int> allQuantizers)
    {
        short* quantCoeffs = stackalloc short[BlockSize * BlockSize];
        quantCoeffs[0] = unchecked(dcBundle.Next());
        DecodeDCTCoefficients(ref bitStream, quantCoeffs);
        uint quantizerI = bitStream.Read(4);
        var quantizers = allQuantizers.Slice(BlockSize * BlockSize * (int)quantizerI, BlockSize * BlockSize);

        IDCT8x8.idct(quantCoeffs, values, quantizers);
        return values;
    }

    private void DecodeDCTCoefficients(ref BitStream bitStream, short* quantCoeffsOut)
    {
        Span<short> quantCoeffs = stackalloc short[BlockSize * BlockSize];
        Span<(byte coeffI, byte mode)> ops = stackalloc (byte, byte)[BlockSize * BlockSize * 2];
        int curOp, startOp = BlockSize * BlockSize, nextOp = startOp + 6;
        ops[startOp + 0] = (4, 0);
        ops[startOp + 1] = (24, 0);
        ops[startOp + 2] = (44, 0);
        ops[startOp + 3] = (1, 3);
        ops[startOp + 4] = (2, 3);
        ops[startOp + 5] = (3, 3);

        int maxBitCount = (int)bitStream.Read(4);
        uint mask = 1u << (maxBitCount - 1);
        for (int bitCount = maxBitCount - 1; bitCount >= 0; bitCount--, mask >>= 1)
        {
            curOp = startOp;
            while(curOp < nextOp)
            {
                if (ops[curOp] == (0, 0) || bitStream.Read(1) == 0)
                {
                    curOp++;
                    continue;
                }

                var curMode = ops[curOp].mode;
                switch (curMode)
                {
                    case 1:
                        byte curCoeffI = ops[curOp].coeffI;
                        ops[curOp] = (curCoeffI, 2);
                        ops[nextOp++] = ((byte)(curCoeffI + 4), 2);
                        ops[nextOp++] = ((byte)(curCoeffI + 8), 2);
                        ops[nextOp++] = ((byte)(curCoeffI + 12), 2);
                        break;

                    case 0:
                        curCoeffI = ops[curOp].coeffI;
                        ops[curOp] = ((byte)(curCoeffI + 4), 1);
                        for (int j = 0; j < 4; j++, curCoeffI++)
                        {
                            if (bitStream.Read(1) == 1)
                            {
                                ops[--startOp] = (curCoeffI, 3);
                                continue;
                            }

                            quantCoeffs[curCoeffI] = ReadCoefficient(ref bitStream, mask, bitCount);
                        }
                        break;
                        
                    case 2:
                        curCoeffI = ops[curOp].coeffI;
                        ops[curOp++] = (0, 0);
                        for (int j = 0; j < 4; j++, curCoeffI++)
                        {
                            if (bitStream.Read(1) == 1)
                            {
                                ops[--startOp] = (curCoeffI, 3);
                                continue;
                            }

                            quantCoeffs[curCoeffI] = ReadCoefficient(ref bitStream, mask, bitCount);
                        }
                        break;

                    case 3:
                        quantCoeffs[ops[curOp].coeffI] = ReadCoefficient(ref bitStream, mask, bitCount);
                        ops[curOp++] = (0, 0);
                        break;

                    default: throw new BinkDecoderException($"Unexpected DCT coefficient op mode: {ops[curOp].mode}");
                }
            }
        }

        quantCoeffs[0] = quantCoeffsOut[0];
        var coeffPairsIn = MemoryMarshal.Cast<short, uint>(quantCoeffs);
        var coeffPairsOut = new Span<uint>(quantCoeffsOut, coeffPairsIn.Length);
        for (int i = 0; i < DCTScanOrder.Length; i++)
            coeffPairsOut[i] = coeffPairsIn[DCTScanOrder[i]];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short ReadCoefficient(ref BitStream bitStream, uint mask, int bitCount)
    {
        short coeffValue = (short)(bitCount == 0 ? 1 : mask | bitStream.Read(bitCount));
        if (bitStream.Read(1) == 1)
            coeffValue *= -1;
        return coeffValue;
    }

    private static readonly byte[] DCTScanOrder = new byte[]
    {
        0, 2, 4, 6,
        1, 3, 5, 7,
        12, 22, 8, 10,
        13, 23, 9, 11,
        14, 16, 24, 26,
        15, 17, 25, 27,
        18, 20, 28, 30,
        19, 21, 29, 31
    };
}
