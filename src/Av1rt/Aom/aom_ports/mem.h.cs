// Port of libaom aom_ports/mem.h

global using static Av1rt.Aom.mem;

using System.Numerics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] DECLARE_ALIGNED, AOM_HAS_BUILTIN and __builtin_prefetch have no C# counterpart (alignment
// is irrelevant for managed arrays). CONVERT_TO_SHORTPTR / CONVERT_TO_BYTEPTR are high bitdepth
// only and compiled out. The UENUM1BYTE family only picks the C storage size of an enum; every
// enum is an `int` alias in the port (PORTING 3.2).
//
// The macros are type generic in C. They are ported as overloads for int, uint, long and ulong so
// that C's integer promotions carry over: byte/short/ushort arguments bind to the int overload,
// as C promotes them to int, and uint arguments stay unsigned.
internal static partial class mem
{
    /* Shift down with rounding for use when n >= 0. Usually value >= 0, but the
     * macro can be used with a negative value if the direction of rounding is
     * acceptable.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ROUND_POWER_OF_TWO(int value, int n) => (value + ((1 << n) >> 1)) >> n;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ROUND_POWER_OF_TWO(uint value, int n) => (value + (uint)((1 << n) >> 1)) >> n;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long ROUND_POWER_OF_TWO(long value, int n) => (value + ((1 << n) >> 1)) >> n;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong ROUND_POWER_OF_TWO(ulong value, int n) => (value + (ulong)((1 << n) >> 1)) >> n;

    /* Shift down with rounding for signed integers, for use when n >= 0 */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ROUND_POWER_OF_TWO_SIGNED(int value, int n) =>
        (value < 0) ? -ROUND_POWER_OF_TWO(-value, n) : ROUND_POWER_OF_TWO(value, n);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long ROUND_POWER_OF_TWO_SIGNED(long value, int n) =>
        (value < 0) ? -ROUND_POWER_OF_TWO(-value, n) : ROUND_POWER_OF_TWO(value, n);

    /* Shift down with rounding for use when n >= 0 (64-bit value). Usually
     * value >= 0, but the macro can be used with a negative value if the
     * direction of rounding is acceptable.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long ROUND_POWER_OF_TWO_64(long value, int n) => (value + ((1L << n) >> 1)) >> n;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong ROUND_POWER_OF_TWO_64(ulong value, int n) => (value + (ulong)((1L << n) >> 1)) >> n;

    /* Shift down with rounding for signed integers, for use when n >= 0 (64-bit
     * value)
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long ROUND_POWER_OF_TWO_SIGNED_64(long value, int n) =>
        (value < 0) ? -ROUND_POWER_OF_TWO_64(-value, n) : ROUND_POWER_OF_TWO_64(value, n);

    /* Shift down with ceil() for use when n >= 0 and value >= 0.*/
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CEIL_POWER_OF_TWO(int value, int n) => (value + (1 << n) - 1) >> n;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint CEIL_POWER_OF_TWO(uint value, int n) => (value + (uint)(1 << n) - 1) >> n;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long CEIL_POWER_OF_TWO(long value, int n) => (value + (1 << n) - 1) >> n;

    /* shift right or left depending on sign of n */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RIGHT_SIGNED_SHIFT(int value, int n) => n < 0 ? (value << (-n)) : (value >> n);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long RIGHT_SIGNED_SHIFT(long value, int n) => n < 0 ? (value << (-n)) : (value >> n);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ALIGN_POWER_OF_TWO(int value, int n) => (value + ((1 << n) - 1)) & ~((1 << n) - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ALIGN_POWER_OF_TWO(uint value, int n) =>
        (value + (uint)((1 << n) - 1)) & (uint)~((1 << n) - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long ALIGN_POWER_OF_TWO(long value, int n) => (value + ((1 << n) - 1)) & ~((1 << n) - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong ALIGN_POWER_OF_TWO(ulong value, int n) =>
        (value + (ulong)((1 << n) - 1)) & (ulong)~((1 << n) - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ALIGN_POWER_OF_TWO_UNSIGNED(uint value, int n) =>
        (value + ((1u << n) - 1)) & ~((1u << n) - 1);

    // [port] DIVIDE_AND_ROUND mixes the types of x and y as C's usual arithmetic conversions do.
    // A generic keeps that for same-signedness mixes (int with long infers long) and refuses to
    // compile signed/unsigned mixes, which C would evaluate in unsigned: cast those explicitly.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T DIVIDE_AND_ROUND<T>(T x, T y) where T : IBinaryInteger<T> => (x + (y >> 1)) / y;

    /* This macro requires d > 0. */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T DIVIDE_AND_ROUND_SIGNED<T>(T n, T d) where T : IBinaryInteger<T>
    {
        T two = T.One + T.One;
        return (n < T.Zero) ? ((n - d / two) / d) : ((n + d / two) / d);
    }
}
