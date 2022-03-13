namespace Bonk;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

unsafe partial class PlaneDecoder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeResidueBlock(ref BitStream bitStream, byte* sourcePtr, byte* targetPtr)
    {
        sourcePtr = unchecked(sourcePtr + bundleXMotion.Next() + bundleYMotion.Next() * width);
        sbyte* residue = stackalloc sbyte[BlockSize * BlockSize];
        DecodeResidue(ref bitStream, residue);

        var residueScanOrder = ResidueScanOrder.AsSpan();
        for (int i = 0; i < BlockSize; i++) 
        {
            for (int j = 0; j < BlockSize; j++)
                targetPtr[j] = unchecked((byte)((sbyte)sourcePtr[j] + residue[residueScanOrder[j]]));
            sourcePtr += width;
            targetPtr += width;
            residueScanOrder = residueScanOrder.Slice(BlockSize);
        }
    }

    private void DecodeResidue(ref BitStream bitStream, sbyte* residue)
    {
        Span<byte> residueIndices = stackalloc byte[BlockSize * BlockSize];
        Span<(byte residueI, byte mode)> ops = stackalloc (byte, byte)[BlockSize * BlockSize * 2];
        int curOp, startOp = ops.Length / 2, nextOp = startOp + 4;
        ops[startOp + 0] = (4, 0);
        ops[startOp + 1] = (24, 0);
        ops[startOp + 2] = (44, 0);
        ops[startOp + 3] = (0, 2);

        var maskCount = (int)bitStream.Read(7);
        var bitCount = (int)bitStream.Read(3) + 1;
        var mask = 1 << (bitCount - 1);
        int residueCount = 0;
        for (int i = 0; i < bitCount; i++, mask >>= 1)
        {
            for (int j = 0; j < residueCount; j++)
            {
                if (bitStream.Read(1) == 0)
                    continue;
                var curResidue = residue + residueIndices[j];
                *curResidue += (sbyte)(*curResidue < 0 ? -mask : mask);
                if (maskCount-- == 0)
                    return;
            }

            curOp = startOp;
            while(curOp < nextOp)
            {
                if (ops[curOp] == (0, 0) || bitStream.Read(1) == 0)
                {
                    curOp++;
                    continue;
                }

                var (curResidueI, curMode) = ops[curOp];
                switch(curMode)
                {
                    case 1:
                        ops[curOp] = (curResidueI, 2);
                        ops[nextOp++] = ((byte)(curResidueI + 4), 2);
                        ops[nextOp++] = ((byte)(curResidueI + 8), 2);
                        ops[nextOp++] = ((byte)(curResidueI + 12), 2);
                        break;

                    case 0:
                        ops[curOp] = ((byte)(curResidueI + 4), 1);
                        for (int j = 0; j < 4; j++, curResidueI++)
                        {
                            if (bitStream.Read(1) == 1)
                            {
                                ops[--startOp] = (curResidueI, 3);
                                continue;
                            }

                            residueIndices[residueCount++] = curResidueI;
                            residue[curResidueI] = (sbyte)(bitStream.Read(1) == 0 ? mask : -mask);
                            if (maskCount-- == 0)
                                return;
                        }
                        break;

                    case 2:
                        ops[curOp++] = (0, 0);
                        for (int j = 0; j < 4; j++, curResidueI++)
                        {
                            if (bitStream.Read(1) == 1)
                            {
                                ops[--startOp] = (curResidueI, 3);
                                continue;
                            }

                            residueIndices[residueCount++] = curResidueI;
                            residue[curResidueI] = (sbyte)(bitStream.Read(1) == 0 ? mask : -mask);
                            if (maskCount-- == 0)
                                return;
                        }
                        break;

                    case 3:
                        ops[curOp++] = (0, 0);
                        residueIndices[residueCount++] = curResidueI;
                        residue[curResidueI] = (sbyte)(bitStream.Read(1) == 0 ? mask : -mask);
                        if (maskCount-- == 0)
                            return;
                        break;

                    default: throw new BinkDecoderException($"Unexpected residue op mode: {curMode}");
                }
            }
        }
    }

    private static readonly byte[] ResidueScanOrder = DCTScanOrder
        .SelectMany(i => new[] { (byte)(2 * i), (byte)(2 * i + 1) })
        .ToArray();
}
