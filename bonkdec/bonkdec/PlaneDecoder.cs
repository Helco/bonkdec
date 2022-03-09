namespace Bonk;
using System;
using System.IO;
using System.Linq;

internal unsafe class PlaneDecoder
{
    private const int BlockSize = 8;

    private readonly uint width, height; // aligned to blocks and subsampled
    private readonly byte[][] sampleBuffers;
    private readonly Huffman[] huffColorsHigh = new Huffman[16];
    private Huffman huffBlockType;
    private Huffman huffSubBlockType;
    private Huffman huffColorsLow;
    private Huffman huffPattern;
    private Huffman huffXMotion;
    private Huffman huffYMotion;
    private Huffman huffPatternColors;

    private int curBuffer = 0;
    private byte[] Target => sampleBuffers[curBuffer];
    private byte[] Source => sampleBuffers[curBuffer ^ 1];

    private static readonly BinaryWriter bw = new BinaryWriter(new FileStream("myvid.bin", FileMode.Create, FileAccess.Write));

    public PlaneDecoder(in ContainerHeader header, bool subSampled)
    {
        static uint AdaptSize(uint value, bool subSampled)
        {
            value = subSampled ? (value + 1) / 2 : value;
            return (value + 7u) & ~7u;
        }

        width = AdaptSize(header.Width, subSampled);
        height = AdaptSize(header.Height, subSampled);
        sampleBuffers = new[]
        {
            new byte[width * height],
            new byte[width * height]
        };
    }

    public ReadOnlySpan<byte> Decode(ReadOnlySpan<byte> buffer)
    {
        var bitStream = new BitStream(buffer);
        huffBlockType = Huffman.ReadTree(ref bitStream);
        huffSubBlockType = Huffman.ReadTree(ref bitStream);
        for (int i = 0; i < huffColorsHigh.Length; i++)
            huffColorsHigh[i] = Huffman.ReadTree(ref bitStream);
        huffColorsLow = Huffman.ReadTree(ref bitStream);
        huffPattern = Huffman.ReadTree(ref bitStream);
        huffXMotion = Huffman.ReadTree(ref bitStream);
        huffYMotion = Huffman.ReadTree(ref bitStream);
        huffPatternColors = Huffman.ReadTree(ref bitStream);

        void DumpTree(Huffman t)
        {
            bw.Write(t.tree, 0, 16);
            bw.Write(new ReadOnlySpan<byte>(t.symbols, 16));
            bw.Flush();
        }
        DumpTree(huffBlockType);
        DumpTree(huffSubBlockType);
        Array.ForEach(huffColorsHigh, DumpTree);
        DumpTree(huffColorsLow);
        DumpTree(huffPattern);
        DumpTree(huffXMotion);
        DumpTree(huffYMotion);
        DumpTree(huffPatternColors);

        bitStream.AlignToWord();
        return buffer.Slice(bitStream.currentOffset * 4);
    }
}
