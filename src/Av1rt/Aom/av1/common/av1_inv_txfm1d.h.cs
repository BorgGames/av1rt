// Port of libaom av1/common/av1_inv_txfm1d.h

global using static Av1rt.Aom.av1_inv_txfm1d;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class av1_inv_txfm1d
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int clamp_value(int value, sbyte bit)
    {
        if (bit <= 0) return value;  // Do nothing for invalid clamp bit.
        long max_value = (1L << (bit - 1)) - 1;
        long min_value = -(1L << (bit - 1));
        return (int)clamp64(value, min_value, max_value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void clamp_buf(Ptr<int> buf, int size, sbyte bit)
    {
        for (int i = 0; i < size; ++i) buf[i] = clamp_value(buf[i], bit);
    }
}
