namespace Bonk.Test;
using System;
using NUnit.Framework;

public class BitStreamTest
{

    [Test]
    public void CanReadCorrectly()
    {
        var data = new byte[] {
            0x07, 0x00, 0x86, 0x88, 0x00, 0x00, 0xBD, 0xFF
        };
        var bitStream = new BitStream(data.AsSpan());
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
}
