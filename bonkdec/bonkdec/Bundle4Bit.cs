namespace Bonk;
using System;

using static System.Math;

internal class Bundle4Bit
{
    private readonly sbyte[] buffer; // sbyte for signed 4bit bundles
    private readonly int maxLengthInBits;
    private uint offset, length;
    private Huffman huffman;

    private bool IsDone => offset > length;

    public Bundle4Bit(int minValueCount, uint width, int addBlockLinesInBuffer)
    {
        var size = minValueCount + addBlockLinesInBuffer * (width / 8);
        maxLengthInBits = (int)Ceiling(Log(size, 2)); // fine for the small numbers we work with
        buffer = new sbyte[1 << maxLengthInBits];
    }

    public void Dump(System.IO.BinaryWriter bw)
    {
        bw.Write(length);
        bw.Write(maxLengthInBits);
        bw.Write(System.Runtime.InteropServices.MemoryMarshal.Cast<sbyte, byte>(buffer).Slice(0, (int)length));
    }

    public void Reset(ref BitStream bitStream)
    {
        offset = length = 0;
        huffman = Huffman.ReadTree(ref bitStream);
    }

    public sbyte Next() => offset < length
        ? buffer[offset++]
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

        if (bitStream.Read(1) == 1)
        {
            // memset mode
            Array.Fill(buffer, (sbyte)bitStream.Read(4), 0, (int)length);
            return;
        }

        sbyte lastValue = 0;
        for (int i = 0; i < length; ) // no increment
        {
            sbyte value = (sbyte)huffman.Read(ref bitStream);
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
}
