namespace Bonk;
using System;
using System.Runtime.CompilerServices;

internal unsafe struct Huffman
{
    private const int SymbolCount = 16;

    internal readonly byte[] tree;
    private readonly int maxBitSize;
    internal fixed byte symbols[SymbolCount];

    private Huffman(uint treeId)
    {
        tree = Trees[treeId];
        maxBitSize = MaxBitSizes[treeId];
        for (byte i = 0; i < SymbolCount; i++)
            symbols[i] = i;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read(ref BitStream bitStream)
    {
        var code = tree[bitStream.Peek(maxBitSize)];
        bitStream.Read(code >> 4);
        return symbols[code & 0xF];
    }

    public static Huffman ReadTree(ref BitStream bitStream)
    {
        var treeId = bitStream.Read(4);
        var huffman = new Huffman(treeId);
        var symbols = huffman.symbols;
        if (treeId == 0)
            return huffman; // symbols are already ordered

        if (bitStream.Read(1) != 0)
        {
            // first symbols are given, the rest is ordered
            int symbolsGiven = 0;
            int firstCount = (int)bitStream.Read(3);
            int i = 0;
            for (; i <= firstCount; i++)
            {
                symbols[i] = (byte)bitStream.Read(4);
                symbolsGiven |= 1 << symbols[i];
            }
            for (int j = 0; j < SymbolCount; j++)
            {
                if ((symbolsGiven & (1 << j)) == 0)
                    symbols[i++] = (byte)j;
            }
            return huffman;
        }

        // otherwise pairs, quadruplets, octets are shuffled, Span magic is sadly not possible due to CS8352
        uint shuffleDepth = bitStream.Read(2);
        byte* to = symbols, from = stackalloc byte[SymbolCount];
        for (int i = 0; i <= shuffleDepth; i++)
        {
            var tmp = from; // not tuples with pointers either
            from = to;
            to = tmp;

            int mergeSize = 1 << i;
            for (int j = 0; j < SymbolCount; j += mergeSize * 2)
                Merge(ref bitStream, to + j, from + j, from + j + mergeSize, mergeSize);
        }
        if (to != symbols)
            Buffer.MemoryCopy(to, symbols, SymbolCount, SymbolCount);
        return huffman;
    }

    private static void Merge(ref BitStream bitStream, byte* dst, byte* src1, byte* src2, int size1)
    {
        int size2 = size1;
        while(size1 > 0 && size2 > 0)
        {
            if (bitStream.Read(1) == 0)
            {
                *dst++ = *src1++;
                size1--;
            }
            else
            {
                *dst++ = *src2++;
                size2--;
            }
        }
        while (size1 --> 0)
            *dst++ = *src1++;
        while (size2 --> 0)
            *dst++ = *src2++;
    }

    private static readonly int[] MaxBitSizes = new[]
    {
        4, 5, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, 6, 7, 7, 7
    };

    // The lookup tables for the static trees
    // High nibble is code size, low nibble is symbol index

    private static readonly byte[][] Trees = new[]
    {
        new byte[]
        {
            0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
            0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F
        },
        new byte[]
        {
            0x10, 0x41, 0x10, 0x52, 0x10, 0x53, 0x10, 0x54, 0x10, 0x55,
            0x10, 0x56, 0x10, 0x57, 0x10, 0x58, 0x10, 0x41, 0x10, 0x59,
            0x10, 0x5A, 0x10, 0x5B, 0x10, 0x5C, 0x10, 0x5D, 0x10, 0x5E,
            0x10, 0x5F
        },
        new byte[]
        {
            0x20, 0x42, 0x21, 0x58, 0x20, 0x54, 0x21, 0x5C, 0x20, 0x43,
            0x21, 0x5A, 0x20, 0x56, 0x21, 0x5E, 0x20, 0x42, 0x21, 0x59,
            0x20, 0x55, 0x21, 0x5D, 0x20, 0x43, 0x21, 0x5B, 0x20, 0x57,
            0x21, 0x5F
        },
        new byte[]
        {
            0x20, 0x43, 0x31, 0x58, 0x20, 0x45, 0x32, 0x5C, 0x20, 0x44,
            0x31, 0x5A, 0x20, 0x56, 0x32, 0x5E, 0x20, 0x43, 0x31, 0x59,
            0x20, 0x45, 0x32, 0x5D, 0x20, 0x44, 0x31, 0x5B, 0x20, 0x57,
            0x32, 0x5F
        },
        new byte[]
        {
            0x30, 0x44, 0x32, 0x58, 0x31, 0x46, 0x33, 0x5C, 0x30, 0x45,
            0x32, 0x5A, 0x31, 0x47, 0x33, 0x5E, 0x30, 0x44, 0x32, 0x59,
            0x31, 0x46, 0x33, 0x5D, 0x30, 0x45, 0x32, 0x5B, 0x31, 0x47,
            0x33, 0x5F
        },
        new byte[]
        {
            0x30, 0x46, 0x42, 0x4A, 0x31, 0x48, 0x44, 0x5C, 0x30, 0x47,
            0x43, 0x4B, 0x31, 0x49, 0x45, 0x5E, 0x30, 0x46, 0x42, 0x4A,
            0x31, 0x48, 0x44, 0x5D, 0x30, 0x47, 0x43, 0x4B, 0x31, 0x49,
            0x45, 0x5F
        },
        new byte[]
        {
             0x20, 0x45, 0x41, 0x49, 0x20, 0x47, 0x43, 0x5C, 0x20, 0x46,
            0x42, 0x5A, 0x20, 0x48, 0x44, 0x5E, 0x20, 0x45, 0x41, 0x49,
            0x20, 0x47, 0x43, 0x5D, 0x20, 0x46, 0x42, 0x5B, 0x20, 0x48,
            0x44, 0x5F
        },
        new byte[]
        {
            0x10, 0x31, 0x10, 0x53, 0x10, 0x32, 0x10, 0x68, 0x10, 0x31,
            0x10, 0x55, 0x10, 0x32, 0x10, 0x6C, 0x10, 0x31, 0x10, 0x54,
            0x10, 0x32, 0x10, 0x6A, 0x10, 0x31, 0x10, 0x66, 0x10, 0x32,
            0x10, 0x6E, 0x10, 0x31, 0x10, 0x53, 0x10, 0x32, 0x10, 0x69,
            0x10, 0x31, 0x10, 0x55, 0x10, 0x32, 0x10, 0x6D, 0x10, 0x31,
            0x10, 0x54, 0x10, 0x32, 0x10, 0x6B, 0x10, 0x31, 0x10, 0x67,
            0x10, 0x32, 0x10, 0x6F
        },
        new byte[]
        {
            0x10, 0x21, 0x10, 0x52, 0x10, 0x21, 0x10, 0x68, 0x10, 0x21,
            0x10, 0x64, 0x10, 0x21, 0x10, 0x6C, 0x10, 0x21, 0x10, 0x53,
            0x10, 0x21, 0x10, 0x6A, 0x10, 0x21, 0x10, 0x66, 0x10, 0x21,
            0x10, 0x6E, 0x10, 0x21, 0x10, 0x52, 0x10, 0x21, 0x10, 0x69,
            0x10, 0x21, 0x10, 0x65, 0x10, 0x21, 0x10, 0x6D, 0x10, 0x21,
            0x10, 0x53, 0x10, 0x21, 0x10, 0x6B, 0x10, 0x21, 0x10, 0x67,
            0x10, 0x21, 0x10, 0x6F
        },
        new byte[]
        {
            0x10, 0x31, 0x10, 0x54, 0x10, 0x42, 0x10, 0x68, 0x10, 0x31,
            0x10, 0x56, 0x10, 0x43, 0x10, 0x6C, 0x10, 0x31, 0x10, 0x55,
            0x10, 0x42, 0x10, 0x6A, 0x10, 0x31, 0x10, 0x57, 0x10, 0x43,
            0x10, 0x6E, 0x10, 0x31, 0x10, 0x54, 0x10, 0x42, 0x10, 0x69,
            0x10, 0x31, 0x10, 0x56, 0x10, 0x43, 0x10, 0x6D, 0x10, 0x31,
            0x10, 0x55, 0x10, 0x42, 0x10, 0x6B, 0x10, 0x31, 0x10, 0x57,
            0x10, 0x43, 0x10, 0x6F
        },
        new byte[]
        {
            0x20, 0x32, 0x21, 0x55, 0x20, 0x43, 0x21, 0x59, 0x20, 0x32,
            0x21, 0x57, 0x20, 0x44, 0x21, 0x6C, 0x20, 0x32, 0x21, 0x56,
            0x20, 0x43, 0x21, 0x6A, 0x20, 0x32, 0x21, 0x58, 0x20, 0x44,
            0x21, 0x6E, 0x20, 0x32, 0x21, 0x55, 0x20, 0x43, 0x21, 0x59,
            0x20, 0x32, 0x21, 0x57, 0x20, 0x44, 0x21, 0x6D, 0x20, 0x32,
            0x21, 0x56, 0x20, 0x43, 0x21, 0x6B, 0x20, 0x32, 0x21, 0x58,
            0x20, 0x44, 0x21, 0x6F
        },
        new byte[]
        {
            0x10, 0x41, 0x10, 0x55, 0x10, 0x43, 0x10, 0x59, 0x10, 0x42,
            0x10, 0x57, 0x10, 0x44, 0x10, 0x6C, 0x10, 0x41, 0x10, 0x56,
            0x10, 0x43, 0x10, 0x6A, 0x10, 0x42, 0x10, 0x58, 0x10, 0x44,
            0x10, 0x6E, 0x10, 0x41, 0x10, 0x55, 0x10, 0x43, 0x10, 0x59,
            0x10, 0x42, 0x10, 0x57, 0x10, 0x44, 0x10, 0x6D, 0x10, 0x41,
            0x10, 0x56, 0x10, 0x43, 0x10, 0x6B, 0x10, 0x42, 0x10, 0x58,
            0x10, 0x44, 0x10, 0x6F
        },
        new byte[]
        {
            0x20, 0x22, 0x21, 0x53, 0x20, 0x22, 0x21, 0x68, 0x20, 0x22,
            0x21, 0x55, 0x20, 0x22, 0x21, 0x6C, 0x20, 0x22, 0x21, 0x54,
            0x20, 0x22, 0x21, 0x6A, 0x20, 0x22, 0x21, 0x66, 0x20, 0x22,
            0x21, 0x6E, 0x20, 0x22, 0x21, 0x53, 0x20, 0x22, 0x21, 0x69,
            0x20, 0x22, 0x21, 0x55, 0x20, 0x22, 0x21, 0x6D, 0x20, 0x22,
            0x21, 0x54, 0x20, 0x22, 0x21, 0x6B, 0x20, 0x22, 0x21, 0x67,
            0x20, 0x22, 0x21, 0x6F
        },
        new byte[]
        {
            0x10, 0x31, 0x10, 0x33, 0x10, 0x32, 0x10, 0x64, 0x10, 0x31,
            0x10, 0x33, 0x10, 0x32, 0x10, 0x78, 0x10, 0x31, 0x10, 0x33,
            0x10, 0x32, 0x10, 0x66, 0x10, 0x31, 0x10, 0x33, 0x10, 0x32,
            0x10, 0x7C, 0x10, 0x31, 0x10, 0x33, 0x10, 0x32, 0x10, 0x65,
            0x10, 0x31, 0x10, 0x33, 0x10, 0x32, 0x10, 0x7A, 0x10, 0x31,
            0x10, 0x33, 0x10, 0x32, 0x10, 0x67, 0x10, 0x31, 0x10, 0x33,
            0x10, 0x32, 0x10, 0x7E, 0x10, 0x31, 0x10, 0x33, 0x10, 0x32,
            0x10, 0x64, 0x10, 0x31, 0x10, 0x33, 0x10, 0x32, 0x10, 0x79,
            0x10, 0x31, 0x10, 0x33, 0x10, 0x32, 0x10, 0x66, 0x10, 0x31,
            0x10, 0x33, 0x10, 0x32, 0x10, 0x7D, 0x10, 0x31, 0x10, 0x33,
            0x10, 0x32, 0x10, 0x65, 0x10, 0x31, 0x10, 0x33, 0x10, 0x32,
            0x10, 0x7B, 0x10, 0x31, 0x10, 0x33, 0x10, 0x32, 0x10, 0x67,
            0x10, 0x31, 0x10, 0x33, 0x10, 0x32, 0x10, 0x7F
        },
        new byte[]
        {
            0x10, 0x31, 0x10, 0x33, 0x10, 0x32, 0x10, 0x54, 0x10, 0x31,
            0x10, 0x33, 0x10, 0x32, 0x10, 0x78, 0x10, 0x31, 0x10, 0x33,
            0x10, 0x32, 0x10, 0x65, 0x10, 0x31, 0x10, 0x33, 0x10, 0x32,
            0x10, 0x7C, 0x10, 0x31, 0x10, 0x33, 0x10, 0x32, 0x10, 0x54,
            0x10, 0x31, 0x10, 0x33, 0x10, 0x32, 0x10, 0x7A, 0x10, 0x31,
            0x10, 0x33, 0x10, 0x32, 0x10, 0x76, 0x10, 0x31, 0x10, 0x33,
            0x10, 0x32, 0x10, 0x7E, 0x10, 0x31, 0x10, 0x33, 0x10, 0x32,
            0x10, 0x54, 0x10, 0x31, 0x10, 0x33, 0x10, 0x32, 0x10, 0x79,
            0x10, 0x31, 0x10, 0x33, 0x10, 0x32, 0x10, 0x65, 0x10, 0x31,
            0x10, 0x33, 0x10, 0x32, 0x10, 0x7D, 0x10, 0x31, 0x10, 0x33,
            0x10, 0x32, 0x10, 0x54, 0x10, 0x31, 0x10, 0x33, 0x10, 0x32,
            0x10, 0x7B, 0x10, 0x31, 0x10, 0x33, 0x10, 0x32, 0x10, 0x77,
            0x10, 0x31, 0x10, 0x33, 0x10, 0x32, 0x10, 0x7F
        },
        new byte[]
        {
            0x20, 0x32, 0x21, 0x34, 0x20, 0x33, 0x21, 0x65, 0x20, 0x32,
            0x21, 0x34, 0x20, 0x33, 0x21, 0x69, 0x20, 0x32, 0x21, 0x34,
            0x20, 0x33, 0x21, 0x67, 0x20, 0x32, 0x21, 0x34, 0x20, 0x33,
            0x21, 0x7C, 0x20, 0x32, 0x21, 0x34, 0x20, 0x33, 0x21, 0x66,
            0x20, 0x32, 0x21, 0x34, 0x20, 0x33, 0x21, 0x7A, 0x20, 0x32,
            0x21, 0x34, 0x20, 0x33, 0x21, 0x68, 0x20, 0x32, 0x21, 0x34,
            0x20, 0x33, 0x21, 0x7E, 0x20, 0x32, 0x21, 0x34, 0x20, 0x33,
            0x21, 0x65, 0x20, 0x32, 0x21, 0x34, 0x20, 0x33, 0x21, 0x69,
            0x20, 0x32, 0x21, 0x34, 0x20, 0x33, 0x21, 0x67, 0x20, 0x32,
            0x21, 0x34, 0x20, 0x33, 0x21, 0x7D, 0x20, 0x32, 0x21, 0x34,
            0x20, 0x33, 0x21, 0x66, 0x20, 0x32, 0x21, 0x34, 0x20, 0x33,
            0x21, 0x7B, 0x20, 0x32, 0x21, 0x34, 0x20, 0x33, 0x21, 0x68,
            0x20, 0x32, 0x21, 0x34, 0x20, 0x33, 0x21, 0x7F
        }
    };
}
