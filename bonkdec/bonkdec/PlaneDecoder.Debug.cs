namespace Bonk;
using System;
using System.Runtime.CompilerServices;

unsafe partial class PlaneDecoder
{
#if BONKDEC_RENDER_BLOCKS
    private void RenderDebugBlock(int blockType, byte* targetPtr, int patternPitch = 1)
    {
        // just custom patterns for the pattern block, reversed bits for easier plotting

        uint color2 = 0xffff_ffff, color1 = 0;
        int offset = 0;
        if (patternPitch == 2 && blockType % 2 == 1)
        {
            blockType--;
            offset++;
        }
        for (int i = 0; i < BlockSize; i++)
        {
            var pattern = ReverseBits(DebugPatterns[blockType * BlockSize + i * patternPitch + offset]);
            ((uint*)targetPtr)[0] = (color1 & Patterns[pattern & 0xF]) | (color2 & ~Patterns[pattern & 0xF]);
            ((uint*)targetPtr)[1] = (color1 & Patterns[pattern >> 4]) | (color2 & ~Patterns[pattern >> 4]);
            targetPtr += width;
        }
    }

    public byte ReverseBits(byte n)
    {
        n = (byte)((n >> 1) & 0x55 | (n << 1) & 0xaa);
        n = (byte)((n >> 2) & 0x33 | (n << 2) & 0xcc);
        n = (byte)((n >> 4) & 0x0f | (n << 4) & 0xf0);
        return n;
    }

    private void RenderScaledDebugBlock(int blockType, byte* targetPtr)
    {
        blockType = blockType switch
        {
            3 => 0,
            5 => 1,
            6 => 2,
            8 => 3,
            9 => 4,
            _ => throw new BinkDecoderException("Unexpected block sub type")
        };

        RenderDebugBlock(10 + blockType * 4, targetPtr, 2);
        RenderDebugBlock(10 + blockType * 4 + 1, targetPtr + BlockSize, 2);
        RenderDebugBlock(10 + blockType * 4 + 2, targetPtr + BlockSize * width, 2);
        RenderDebugBlock(10 + blockType * 4 + 3, targetPtr + BlockSize + BlockSize * width, 2);
    }

    private static readonly byte[] DebugPatterns = new byte[]
    {
        0b1111_1111, // Skip block
        0b1100_0011, // a diagonal cross
        0b1010_0101,
        0b1001_1001,
        0b1001_1001,
        0b1010_0101,
        0b1100_0011,
        0b1111_1111,

        0b1111_1111, // base block, never drawn as it is
        0b1000_0001, // sitting in the scaled block entry
        0b1000_0001,
        0b1000_0001,
        0b1000_0001,
        0b1000_0001,
        0b1000_0001,
        0b1111_1111,

        0b1111_1111, // Motion block
        0b1001_0001, // an arrow pointing right
        0b1000_1001,
        0b1111_1101,
        0b1111_1101,
        0b1000_1001,
        0b1001_0001,
        0b1111_1111,

        0b1111_1111, // Run Fill
        0b1011_1001, // the letter "R"
        0b1010_0101,
        0b1010_0101,
        0b1010_1001,
        0b1011_0001,
        0b1010_1001,
        0b1111_1111,

        0b1111_1111, // Motion+Residue
        0b1001_1001, // an arrow pointing down
        0b1001_1001,
        0b1101_1011,
        0b1011_1101,
        0b1001_1001,
        0b1000_0001,
        0b1111_1111,

        0b1111_1111, // Intra DCT
        0b1000_0001, // the letter "c"
        0b1011_1001,
        0b1010_0001,
        0b1010_0001,
        0b1011_1001,
        0b1000_0001,
        0b1111_1111,

        0b1111_1111, // Fill
        0b1000_0001, // a filled square
        0b1011_1101,
        0b1011_1101,
        0b1011_1101,
        0b1011_1101,
        0b1000_0001,
        0b1111_1111,

        0b1111_1111, // Inter DCT
        0b1000_0001, // the letter "c" flipped horizontally
        0b1001_1101,
        0b1000_0101,
        0b1000_0101,
        0b1001_1101,
        0b1000_0001,
        0b1111_1111,

        0b1111_1111, // Pattern fill
        0b1000_0001, // a checkerboard pattern
        0b1010_1001,
        0b1001_0101,
        0b1010_1001,
        0b1001_0101,
        0b1000_0001,
        0b1111_1111,

        0b1111_1111, // Raw data
        0b1100_0011, // an empty square
        0b1011_1101,
        0b1010_0101,
        0b1010_0101,
        0b1011_1101,
        0b1100_0011,
        0b1111_1111,

        // scaled blocks

        0b1111_1111, 0b1111_1111, // base block
        0b1000_0000, 0b0000_0001, //
        0b1000_0000, 0b0000_0001,
        0b1000_0000, 0b0000_0001,
        0b1000_0000, 0b0000_0001,
        0b1000_0000, 0b0000_0001,
        0b1000_0000, 0b0000_0001,
        0b1000_0000, 0b0000_0001,

        0b1000_0000, 0b0000_0001,
        0b1000_0000, 0b0000_0001,
        0b1000_0000, 0b0000_0001,
        0b1000_0000, 0b0000_0001,
        0b1000_0000, 0b0000_0001,
        0b1000_0000, 0b0000_0001,
        0b1000_0000, 0b0000_0001,
        0b1111_1111, 0b1111_1111,

        0b1111_1111, 0b1111_1111, // run fill block
        0b1000_0000, 0b0000_0001, // the letter R
        0b1001_1111, 0b1111_0001,
        0b1001_1000, 0b0001_1001,
        0b1001_1000, 0b0001_1001,
        0b1001_1000, 0b0001_1001,
        0b1001_1000, 0b0001_1001,
        0b1001_1111, 0b1111_0001,

        0b1001_1111, 0b1110_0001,
        0b1001_1100, 0b0000_0001,
        0b1001_1110, 0b0000_0001,
        0b1001_1011, 0b0000_0001,
        0b1001_1001, 0b1000_0001,
        0b1001_1000, 0b1100_0001,
        0b1000_0000, 0b0110_0001,
        0b1111_1111, 0b1111_1111,

        0b1111_1111, 0b1111_1111, // intra block
        0b1000_0000, 0b0000_0001, // the letter C
        0b1001_1111, 0b1111_0001,
        0b1001_1111, 0b1111_0001,
        0b1001_1000, 0b0000_0001,
        0b1001_1000, 0b0000_0001,
        0b1001_1000, 0b0000_0001,
        0b1001_1000, 0b0000_0001,

        0b1001_1000, 0b0000_0001,
        0b1001_1000, 0b0000_0001,
        0b1001_1000, 0b0000_0001,
        0b1001_1000, 0b0000_0001,
        0b1001_1111, 0b1111_0001,
        0b1001_1111, 0b1111_0001,
        0b1000_0000, 0b0000_0001,
        0b1111_1111, 0b1111_1111,

        0b1111_1111, 0b1111_1111, // fill block
        0b1000_0000, 0b0000_0001, // a filled square
        0b1000_0000, 0b0000_0001,
        0b1001_1111, 0b1111_1001,
        0b1001_1111, 0b1111_1001,
        0b1001_1111, 0b1111_1001,
        0b1001_1111, 0b1111_1001,
        0b1001_1111, 0b1111_1001,

        0b1001_1111, 0b1111_1001,
        0b1001_1111, 0b1111_1001,
        0b1001_1111, 0b1111_1001,
        0b1001_1111, 0b1111_1001,
        0b1001_1111, 0b1111_1001,
        0b1000_0000, 0b0000_0001,
        0b1000_0000, 0b0000_0001,
        0b1111_1111, 0b1111_1111,

        0b1111_1111, 0b1111_1111, // raw block
        0b1110_0000, 0b0000_0111, // an empty square
        0b1111_0000, 0b0000_1111,
        0b1011_1111, 0b1111_1101,
        0b1001_1111, 0b1111_1001,
        0b1001_1000, 0b0001_1001,
        0b1001_1000, 0b0001_1001,
        0b1001_1000, 0b0001_1001,

        0b1001_1000, 0b0001_1001,
        0b1001_1000, 0b0001_1001,
        0b1001_1000, 0b0001_1001,
        0b1001_1111, 0b1111_1001,
        0b1011_1111, 0b1111_1101,
        0b1111_0000, 0b0000_1111,
        0b1110_0000, 0b0000_0111,
        0b1111_1111, 0b1111_1111,
    };
#endif
}
