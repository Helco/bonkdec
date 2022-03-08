namespace Bonk;
using System;

public class Frame
{
    private readonly Decoder parent;

    internal Frame(Decoder parent) => this.parent = parent;

    // TODO Frame should support multiple audio tracks
    public ReadOnlySpan<short> AudioSamples => parent.audioDecoders[0]!.Samples;
}
