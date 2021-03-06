namespace Bonk;
using System;

public class Frame
{
    private readonly Decoder parent;

    internal Frame(Decoder parent) => this.parent = parent;

    // TODO Frame should support multiple audio tracks
    public ReadOnlySpan<short> AudioSamples => parent.audioDecoders[0]!.Samples;

    public ReadOnlySpan<byte> YPlane => parent.yDecoder.Target;
    public ReadOnlySpan<byte> VPlane => parent.uDecoder?.Target ?? ReadOnlySpan<byte>.Empty;
    public ReadOnlySpan<byte> UPlane => parent.vDecoder?.Target ?? ReadOnlySpan<byte>.Empty;
    public ReadOnlySpan<byte> APlane => parent.alphaDecoder?.Target ?? ReadOnlySpan<byte>.Empty;
}
