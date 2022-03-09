namespace Bonk;
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

internal unsafe ref struct BitStream
{
    private readonly ReadOnlySpan<uint> buffer;
    public int currentOffset;
    public uint currentWord;
    public int bitsLeft;

    public bool AtEnd => currentOffset + 1 >= buffer.Length && bitsLeft == 0;

    public BitStream(ReadOnlySpan<byte> buffer)
    {
        this.buffer = MemoryMarshal.Cast<byte, uint>(buffer);
        currentOffset = 0;
        currentWord = this.buffer[0];
        bitsLeft = 32;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Read(int bitCount)
    {
        if (bitCount < 1 || bitCount > 32)
            throw new ArgumentOutOfRangeException(nameof(bitCount), "Bit count can only range from 1 to 32 (inclusive)");

        uint result;
        if (bitsLeft > bitCount)
        {
            result = currentWord & ((1u << bitCount) - 1);
            currentWord >>= bitCount;
            bitsLeft -= bitCount;
            return result;
        }

        if (currentOffset + 1 >= buffer.Length)
        {
            if (bitsLeft == bitCount)
            {
                bitsLeft = 0;
                return currentWord;
            }
            throw new System.IO.EndOfStreamException("End of bitstream");
        }

        result = currentWord;
        currentWord = buffer[++currentOffset];
        result = ((currentWord << bitsLeft) | result) & ((1u << bitCount) - 1);
        currentWord >>= (bitCount - bitsLeft);
        bitsLeft = 32 - (bitCount - bitsLeft);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Peek(int bitCount)
    {
        if (bitCount < 1 || bitCount > 32)
            throw new ArgumentOutOfRangeException(nameof(bitCount), "Bit count can only range from 1 to 32 (inclusive");

        uint mask = (1u << bitCount) - 1;
        if (bitCount <= bitsLeft)
            return currentWord & mask;

        if (currentOffset + 1 >= buffer.Length)
            throw new System.IO.EndOfStreamException("End of bitstream");
        uint result = currentWord | (buffer[currentOffset + 1] << bitsLeft);
        return result & mask;
    }

    public void AlignToWord()
    {
        if (bitsLeft % 32 == 0)
            return;
        if (currentOffset + 1 < buffer.Length)
        {
            currentWord = buffer[++currentOffset];
            bitsLeft = 32;
        }
        else
            bitsLeft = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloat29()
    {
        var value = (float)(Math.Pow(2, (int)Read(5) - 22) * Read(23));
        return Read(1) == 0 ? value : -value;
    }
}