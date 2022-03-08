namespace Bonk;
using System;
using System.Diagnostics;
using System.Runtime.Serialization;

[Serializable]
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public sealed class BinkDecoderException : Exception
{
    public BinkDecoderException()
    {
    }

    public BinkDecoderException(string message) : base(message)
    {
    }

    public BinkDecoderException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    public BinkDecoderException(string message, Exception innerException) : base(message, innerException)
    {
    }

    private string GetDebuggerDisplay()
    {
        return ToString();
    }
}
