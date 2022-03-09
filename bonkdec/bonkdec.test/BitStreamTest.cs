namespace Bonk.Test;
using System;
using NUnit.Framework;

public class BitStreamTest
{
    private static readonly byte[] TestData = new byte[] {
        0x07, 0x00, 0x86, 0x88, 0x00, 0x00, 0xBD, 0xFF
    };

    [Test]
    public void CanReadCorrectly()
    {
        var bitStream = new BitStream(TestData.AsSpan());
        Assert.AreEqual(bitStream.Read(5), 0b00111);
        Assert.AreEqual(bitStream.Read(23), 0b10001000011000000000000);
        Assert.AreEqual(bitStream.Read(1), 0);
        Assert.AreEqual(bitStream.Read(5), 0b00100);
        Assert.AreEqual(bitStream.Read(23), 0b11011110100000000000000);
        Assert.AreEqual(bitStream.Read(1), 1);
        Assert.AreEqual(bitStream.Read(6), 0b111111);
        Assert.True(bitStream.AtEnd);

        bitStream.AlignToWord(); // should work, just no reading anymore
    }

    [Test]
    public void CanPeekCorrectly()
    {
        var bitStream = new BitStream(TestData.AsSpan());
        Assert.AreEqual(bitStream.Peek(5), bitStream.Read(5));
        Assert.AreEqual(bitStream.Peek(23), bitStream.Read(23));
        Assert.AreEqual(bitStream.Peek(1), bitStream.Read(1));
        Assert.AreEqual(bitStream.Peek(5), bitStream.Read(5));
        Assert.AreEqual(bitStream.Peek(13), bitStream.Read(13));
        Assert.AreEqual(bitStream.Peek(10), bitStream.Read(10));
        Assert.AreEqual(bitStream.Peek(7), bitStream.Read(7));
    }
}
