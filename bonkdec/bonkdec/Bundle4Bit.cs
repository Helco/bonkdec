namespace Bonk;
using System;

using static System.Math;

internal static class Bundle
{
    public static int GetMaxLengthInBits(int minValueCount, uint width, int addBlockLinesInBuffer)
    {
        var size = minValueCount + addBlockLinesInBuffer * (width / 8);
        return (int)Ceiling(Log(size, 2)); // fine for the small numbers we work with
    }
}

internal class Bundle4Bit
{
    private readonly byte[] buffer; // mind the isSigned flag
    private readonly int maxLengthInBits;
    private readonly bool isSigned;
    private uint offset, length;
    private Huffman huffman;

    public bool IsDone => offset > length;

    public Bundle4Bit(int minValueCount, uint width, int addBlockLinesInBuffer, bool isSigned)
    {
        maxLengthInBits = Bundle.GetMaxLengthInBits(minValueCount, width, addBlockLinesInBuffer);
        buffer = new byte[1 << maxLengthInBits];
        this.isSigned = isSigned;
    }

    public void Dump(System.IO.BinaryWriter bw)
    {
        bw.Write(length);
        bw.Write(maxLengthInBits);
        bw.Write(buffer, 0, (int)length);
    }

    public void Reset(ref BitStream bitStream)
    {
        offset = length = 0;
        huffman = Huffman.ReadTree(ref bitStream);
    }

    public int Next() => offset < length
        ? isSigned ? unchecked((sbyte)buffer[offset++]) : buffer[offset++]
        : throw new BinkDecoderException("Bundle is empty or done");

    private static readonly int[] RLELengths = new[] { 4, 8, 12, 32 };
    public void FillRLE(ref BitStream bitStream)
    {
        if (offset != length)
            return;
        length = bitStream.Read(maxLengthInBits);
        if (length == 0)
        {
            offset = 1;
            return;
        }
        offset = 0;

        if (bitStream.Read(1) == 1)
        {
            // memset mode
            Array.Fill(buffer, (byte)bitStream.Read(4), 0, (int)length);
            return;
        }

        byte lastValue = 0;
        for (int i = 0; i < length; ) // no increment
        {
            byte value = (byte)huffman.Read(ref bitStream);
            if (value < 12)
            {
                // direct value
                buffer[i++] = lastValue = value;
                continue;
            }

            // RLE - duplicate last value 
            int rleLength = RLELengths[value - 12];
            Array.Fill(buffer, lastValue, i, rleLength);
            i += rleLength;
        }
    }

    public void FillPairs(ref BitStream bitStream)
    {
        if (offset != length)
            return;
        length = bitStream.Read(maxLengthInBits);
        if (length == 0)
        {
            offset = 1;
            return;
        }
        offset = 0;

        for (int i = 0; i < length; i++)
        {
            var lowNibble = huffman.Read(ref bitStream);
            var highNibble = huffman.Read(ref bitStream);
            buffer[i] = (byte)(lowNibble | (highNibble << 4));
        }
    }
}
