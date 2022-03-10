namespace Bonk;

using static System.Math;

internal abstract class Bundle
{
    protected readonly int maxLengthInBits;
    protected uint offset, length;

    public bool IsDone => offset > length;

    protected Bundle(int minValueCount, uint width, int addBlockLinesInBuffer)
    {
        var size = minValueCount + addBlockLinesInBuffer * (width / 8);
        maxLengthInBits = (int)Ceiling(Log(size, 2)); // fine for the small numbers we work with
    }

    protected bool ReadLength(ref BitStream bitStream)
    {
        if (offset != length)
            return false;
        length = bitStream.Read(maxLengthInBits);
        if (length == 0)
        {
            offset = 1;
            return false;
        }
        offset = 0;
        return true;
    }
}
