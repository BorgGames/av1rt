// Port of libaom aom_dsp/odintrin.h

global using static Av1rt.Aom.odintrin;
global using od_coeff = int;

using System.Numerics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] OD_DIVU_SMALL / OD_DIVU need OD_DIVU_SMALL_CONSTS from odintrin.c and are not used by
// the encoder; OD_SIGNMASK / OD_FLIPSIGNI are unused. OD_WARN_UNUSED_RESULT, OD_ARG_NONNULL and
// OD_GNUC_PREREQ are compiler attributes and have no counterpart.
internal static partial class odintrin
{
    internal const int OD_DIVU_DMAX = 1024;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T OD_MINI<T>(T x, T y) where T : INumber<T> => AOMMIN(x, y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T OD_MAXI<T>(T x, T y) where T : INumber<T> => AOMMAX(x, y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T OD_CLAMPI<T>(T min, T val, T max) where T : INumber<T> => OD_MAXI(min, OD_MINI(val, max));

    /*Integer logarithm (base 2) of a nonzero unsigned 32-bit integer.
      OD_ILOG_NZ(x) = (int)floor(log2(x)) + 1.*/
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int OD_ILOG_NZ(uint x) => 1 + get_msb(x);
}
