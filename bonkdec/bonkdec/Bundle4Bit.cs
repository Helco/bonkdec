namespace Bonk;
using System;

internal class Bundle4Bit : Bundle
{
    private readonly byte[] buffer; // mind the isSigned flag
    private readonly bool isSigned;
    private Huffman huffman;

    public Bundle4Bit(int minValueCount, uint width, int addBlockLinesInBuffer, bool isSigned)
        : base(minValueCount, width, addBlockLinesInBuffer)
    {
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

    private void Clear()
    {
#if DEBUG
        Array.Fill(buffer, (byte)0xB0); // canary value for debugging
#endif
    }

    public int Next() => offset < length
        ? isSigned ? unchecked((sbyte)buffer[offset++]) : buffer[offset++]
        : throw new BinkDecoderException("Bundle is empty or done");

    private static readonly int[] RLELengths = new[] { 4, 8, 12, 32 };
    public void FillRLE(ref BitStream bitStream)
    {
        if (!ReadLength(ref bitStream))
            return;
        Clear();

        if (bitStream.Read(1) == 1)
        {
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
        if (!ReadLength(ref bitStream))
            return;
        Clear();

        for (int i = 0; i < length; i++)
        {
            var lowNibble = huffman.Read(ref bitStream);
            var highNibble = huffman.Read(ref bitStream);
            buffer[i] = (byte)(lowNibble | (highNibble << 4));
        }
    }

    public void FillSimple(ref BitStream bitStream)
    {
        if (isSigned)
            FillSigned(ref bitStream);
        else
            FillUnsigned(ref bitStream);
    }

    private void FillUnsigned(ref BitStream bitStream)
    {
        if (!ReadLength(ref bitStream))
            return;
        Clear();

        if (bitStream.Read(1) == 1)
        {
            Array.Fill(buffer, (byte)bitStream.Read(4), 0, (int)length);
            return;
        }

        for (int i = 0; i < length; i++)
            buffer[i] = huffman.Read(ref bitStream);
    }

    private void FillSigned(ref BitStream bitStream)
    {
        if (!ReadLength(ref bitStream))
            return;
        Clear();

        if (bitStream.Read(1) == 1)
        {
            var unsigned = (byte)bitStream.Read(4);
            Array.Fill(buffer, ReadSign(ref bitStream, unsigned), 0, (int)length);
            return;
        }

        for (int i = 0; i < length; i++)
        {
            var unsigned = huffman.Read(ref bitStream);
            buffer[i] = ReadSign(ref bitStream, unsigned);
        }
    }

    private static byte ReadSign(ref BitStream bitStream, byte value) => value == 0 || bitStream.Read(1) == 0
        ? value
        : unchecked((byte)(-(sbyte)value));
}
