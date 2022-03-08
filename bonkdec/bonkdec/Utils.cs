#if DEBUG // only during development
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("bonk-decoder")]
#endif
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("bonkdec.test")]

namespace Bonk;
using System;
using System.Runtime.CompilerServices;

internal static unsafe class Utils
{
    public static ushort Swap(ushort value) => unchecked((ushort)((value << 8) | (value >> 8)));

    // from https://stackoverflow.com/questions/19560436/bitwise-endian-swap-for-various-types
    public static uint Swap(uint value)
    {
        unchecked
        {
            value = (value >> 16) | (value << 16);
            return ((value & 0xFF00FF00) >> 8) | ((value & 0x00FF00FF) << 8);
        }
    }

    public static void SwapIfNecessary(ref ushort value) =>
        value = BitConverter.IsLittleEndian ? value : Swap(value);
    public static void SwapIfNecessary(ref uint value) =>
        value = BitConverter.IsLittleEndian ? value : Swap(value);

    public static Span<T> AsSpan<T>(ref T value, int count = 1) where T : unmanaged
    {
        var pointer = Unsafe.AsPointer(ref value);
        return new Span<T>(pointer, count);
    }

    public static Span<byte> AsByteSpan<T>(ref T value, int count = 1) where T : unmanaged
    {
        var pointer = Unsafe.AsPointer(ref value);
        return new Span<byte>((byte*)pointer, sizeof(T) * count);
    }
}
