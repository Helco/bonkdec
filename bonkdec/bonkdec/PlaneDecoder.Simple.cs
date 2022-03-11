namespace Bonk;
using System;
using System.Runtime.CompilerServices;

internal unsafe partial class PlaneDecoder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeSkipBlock(byte* sourcePtr, byte* targetPtr)
    {
        for (int i = 0; i < BlockSize; i++)
        {
            *((ulong*)targetPtr) = *((ulong*)sourcePtr);
            targetPtr += width;
            sourcePtr += width;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeMotionBlock(byte* sourcePtr, byte* targetPtr)
    {
        // This will cause unaligned access, maybe optimized versions should be used instead
        sourcePtr = unchecked(sourcePtr + bundleXMotion.Next() + bundleYMotion.Next() * width);
        DecodeSkipBlock(sourcePtr, targetPtr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeFillBlock(byte* targetPtr)
    {
        // TODO: Check fill block in the case of signed colors
        ulong value = (ulong)bundleColors.Next();
        value |= (value << 8);
        value |= (value << 16);
        value |= (value << 32);
        for (int i = 0; i < BlockSize; i++)
        {
            *((ulong*)targetPtr) = value;
            targetPtr += width;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeRawBlock(byte* targetPtr)
    {
        for (int i = 0; i < BlockSize; i++)
        {
            bundleColors.Next(BlockSize).CopyTo(new Span<byte>(targetPtr, BlockSize));
            targetPtr += width;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodePatternFill(byte* targetPtr)
    {
        uint color1 = (uint)bundleColors.Next();
        color1 |= color1 << 8;
        color1 |= color1 << 16;

        uint color2 = (uint)bundleColors.Next();
        color2 |= color2 << 8;
        color2 |= color2 << 16;

        for (int i = 0; i < BlockSize; i++)
        {
            var pattern = bundlePattern.Next();
            ((uint*)targetPtr)[0] = (color1 & Patterns[pattern & 0xF]) | (color2 & ~Patterns[pattern & 0xF]);
            ((uint*)targetPtr)[1] = (color1 & Patterns[pattern >> 4]) | (color2 & ~Patterns[pattern >> 4]);
            targetPtr += width;
        }
    }

    private static readonly uint[] Patterns = new uint[]
    {
        // basically 0-16 in binary with whole bytes as bits
        0xFFFFFFFF, 0xFFFFFF00, 0xFFFF00FF, 0xFFFF0000,
        0xFF00FFFF, 0xFF00FF00, 0xFF0000FF, 0xFF000000,
        0x00FFFFFF, 0x00FFFF00, 0x00FF00FF, 0x00FF0000,
        0x0000FFFF, 0x0000FF00, 0x000000FF, 0x00000000,
    };
}
