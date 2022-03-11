namespace Bonk;
using System;
using System.Runtime.CompilerServices;

unsafe partial class PlaneDecoder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeScaledFillBlock(byte* targetPtr)
    {
        ulong value = (ulong)bundleColors.Next();
        value |= (value << 8);
        value |= (value << 16);
        value |= (value << 32);
        for (int i = 0; i < 2 * BlockSize; i++)
        {
            ((ulong*)targetPtr)[0] = value;
            ((ulong*)targetPtr)[1] = value;
            targetPtr += width;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeScaledRawBlock(byte* targetPtr)
    {
        for (int i = 0; i < BlockSize; i++)
        {
            var row = bundleColors.Next(BlockSize);
            var value1 = row[0] | ((ulong)row[1] << 16) | ((ulong)row[2] << 32) | ((ulong)row[3] << 48);
            var value2 = row[4] | ((ulong)row[5] << 16) | ((ulong)row[6] << 32) | ((ulong)row[7] << 48);
            value1 |= value1 << 8;
            value2 |= value2 << 8;

            ((ulong*)targetPtr)[0] = value1;
            ((ulong*)targetPtr)[1] = value2;
            targetPtr += width;
            ((ulong*)targetPtr)[0] = value1;
            ((ulong*)targetPtr)[1] = value2;
            targetPtr += width;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeScaledPatternFill(byte* targetPtr)
    {
        ulong color1 = (uint)bundleColors.Next();
        color1 |= color1 << 8;
        color1 |= color1 << 16;
        color1 |= color1 << 32;

        ulong color2 = (uint)bundleColors.Next();
        color2 |= color2 << 8;
        color2 |= color2 << 16;
        color2 |= color2 << 32;

        for (int i = 0; i < BlockSize; i++)
        {
            var pattern = bundlePattern.Next();
            ulong value1 = (color1 & ScaledPatterns[pattern & 0xF]) | (color2 & ~ScaledPatterns[pattern & 0xF]);
            ulong value2 = (color1 & ScaledPatterns[pattern >> 4]) | (color2 & ~ScaledPatterns[pattern >> 4]);

            ((ulong*)targetPtr)[0] = value1;
            ((ulong*)targetPtr)[1] = value2;
            targetPtr += width;
            ((ulong*)targetPtr)[0] = value1;
            ((ulong*)targetPtr)[1] = value2;
            targetPtr += width;
        }
    }

    private static readonly ulong[] ScaledPatterns = new ulong[]
    {
        0xFFFFFFFFFFFFFFFF, 0xFFFFFFFFFFFF0000,
        0xFFFFFFFF0000FFFF, 0xFFFFFFFF00000000,
        0xFFFF0000FFFFFFFF, 0xFFFF0000FFFF0000,
        0xFFFF00000000FFFF, 0xFFFF000000000000,
        0x0000FFFFFFFFFFFF, 0x0000FFFFFFFF0000,
        0x0000FFFF0000FFFF, 0x0000FFFF00000000,
        0x00000000FFFFFFFF, 0x00000000FFFF0000,
        0x000000000000FFFF, 0x0000000000000000
    };
}
