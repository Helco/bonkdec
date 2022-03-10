namespace Bonk;
using System;

internal class Bundle8Bit : Bundle
{
    private readonly byte[] buffer; // mind the isSigned flag
    private readonly bool isSigned;
    private readonly Huffman[] highNibbles = new Huffman[16];
    private Huffman lowNibbles;
    private uint lastTreeI;

    public Bundle8Bit(int minValueCount, uint width, int addBlockLinesInBuffer, bool isSigned)
        : base(minValueCount, width, addBlockLinesInBuffer)
    {
        buffer = new byte[1 << maxLengthInBits];
        this.isSigned = isSigned;
        if (isSigned)
            throw new NotSupportedException("Signed 8Bit bundles are not supported"); // TODO: Support signed 8-bit bundles
    }

    public void Dump(System.IO.BinaryWriter bw)
    {
        bw.Write(length);
        bw.Write(maxLengthInBits);
        bw.Write(buffer, 0, (int)length);
    }

    public void Reset(ref BitStream bitStream)
    {
        offset = length = lastTreeI = 0;
        foreach (ref var highNibble in highNibbles.AsSpan())
            highNibble = Huffman.ReadTree(ref bitStream);
        lowNibbles = Huffman.ReadTree(ref bitStream);
    }

    public int Next() => offset < length
        ? isSigned ? unchecked((sbyte)buffer[offset++]) : buffer[offset++]
        : throw new BinkDecoderException("Bundle is empty or done");

    public void Fill(ref BitStream bitStream)
    {
        if (!ReadLength(ref bitStream))
            return;

        var isMemset = bitStream.Read(1) != 0;
        for (int i = 0; i < (isMemset ? 1 : length); i++)
        {
            var highNibble = lastTreeI = highNibbles[lastTreeI].Read(ref bitStream);
            var lowNibble = lowNibbles.Read(ref bitStream);
            buffer[i] = (byte)(lowNibble | (highNibble << 4));
        }

        if (isMemset)
            Array.Fill(buffer, buffer[0], 0, (int)length);
    }
}