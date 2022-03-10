namespace Bonk;
using System;

internal class Bundle16Bit : Bundle
{
    private readonly short[] buffer;
    private readonly int startBits;
    private readonly bool isSigned;

    public Bundle16Bit(int minValueCount, uint width, int addBlockLinesInBuffer, int startBits, bool isSigned)
        : base(minValueCount, width, addBlockLinesInBuffer)
    {
        buffer = new short[1 << maxLengthInBits];
        this.startBits = startBits;
        this.isSigned = isSigned;
    }

    public void Dump(System.IO.BinaryWriter bw)
    {
        bw.Write(length * 2); // dump in bytes not values
        bw.Write(maxLengthInBits);
        for (int i = 0; i < length; i++)
            bw.Write(buffer[i]);
    }

    public void Reset()
    {
        offset = length = 0;
    }

    public short Next() => offset < length
        ? buffer[offset++]
        : throw new BinkDecoderException("Bundle is empty or done");

    public void Fill(ref BitStream bitStream)
    {
        if (!ReadLength(ref bitStream))
            return;

        int i = 0, lastValue = isSigned
            ? ReadValue(ref bitStream, startBits - 1)
            : (int)bitStream.Read(startBits);
        buffer[i++] = (short)lastValue;
        while (i < length)
        {
            int runEnd = Math.Min((int)length, i + 8);
            var runBits = (int)bitStream.Read(4);
            if (runBits == 0)
            {
                Array.Fill(buffer, unchecked((short)lastValue), i, runEnd - i);
                i = runEnd;
                continue;
            }

            for (; i < runEnd; i++)
            {
                lastValue += ReadValue(ref bitStream, runBits);
                buffer[i] = unchecked((short)lastValue);
            }
        }
    }

    private static int ReadValue(ref BitStream bitStream, int bits)
    {
        var value = (int)bitStream.Read(bits);
        return value == 0 || bitStream.Read(1) == 0 ? value : -value;
    }
}
