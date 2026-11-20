// Port of libaom aom_dsp/aom_dsp_common.h

global using static Av1rt.Aom.aom_dsp_common;
global using qm_val_t = byte;
// Note:
// tran_low_t  is the datatype used for final transform coefficients.
// tran_high_t is the datatype used for intermediate transform stages.
global using tran_high_t = long;
global using tran_low_t = int;

using System.Numerics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] AOM_FORCE_INLINE / AOM_NOINLINE map to MethodImplOptions.AggressiveInlining /
// NoInlining at the definition. LIKELY / UNLIKELY are branch hints and are dropped (the
// condition is kept). NELEMENTS(x) is x.Length on a C# array.
internal static partial class aom_dsp_common
{
    internal const double PI = 3.141592653589793238462643383279502884;

    // [port] AOMMIN / AOMMAX / AOMSIGN are type generic in C. The C# generics infer one type for
    // both arguments: int with long gives long, int with double gives double, short with int
    // gives int, as C's conversions do. A signed/unsigned mix (int with uint, long with ulong) does
    // not compile: C would compare in unsigned there, so the caller must cast as C converts.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T AOMMIN<T>(T x, T y) where T : INumber<T> => (x < y) ? x : y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T AOMMAX<T>(T x, T y) where T : INumber<T> => (x > y) ? x : y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AOMSIGN<T>(T x) where T : INumber<T> => x < T.Zero ? -1 : 0;

    //  Logical 'a implies b' (or 'a -> b')
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IMPLIES(bool a, bool b) => !a || b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IS_POWER_OF_TWO<T>(T x) where T : IBinaryInteger<T> => (x & (x - T.One)) == T.Zero;

    /* Left shifting a negative value became undefined behavior in C99 (downgraded
       from merely implementation-defined in C89). This should still compile to the
       correct thing on any two's-complement machine, but avoid ubsan warnings.*/
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AOM_SIGNED_SHL(int x, int shift) => x * (1 << shift);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long AOM_SIGNED_SHL(long x, int shift) => x * (1L << shift);

    internal const int AOM_QM_BITS = 5;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte clip_pixel(int val) => (byte)((val > 255) ? 255 : (val < 0) ? 0 : val);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int clamp(int value, int low, int high) =>
        value < low ? low : (value > high ? high : value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long clamp64(long value, long low, long high) =>
        value < low ? low : (value > high ? high : value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double fclamp(double value, double low, double high) =>
        value < low ? low : (value > high ? high : value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ushort clip_pixel_highbd(int val, int bd)
    {
        switch (bd)
        {
            case 8:
            default: return (ushort)clamp(val, 0, 255);
            case 10: return (ushort)clamp(val, 0, 1023);
            case 12: return (ushort)clamp(val, 0, 4095);
        }
    }

    // [subset] negative_to_zero never runs in the subset and is not ported.

    // Returns the saturating cast of a double value to int.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int saturate_cast_double_to_int(double d)
    {
        if (d > int.MaxValue) return int.MaxValue;
        return (int)d;
    }
}
