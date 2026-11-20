// Port of libaom aom_dsp/blend.h

global using static Av1rt.Aom.blend;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// Various blending functions and macros.
// See also the aom_blend_* functions in aom_dsp_rtcd.h
internal static partial class blend
{
    // Alpha blending with alpha values from the range [0, 64], where 64
    // means use the first input and 0 means use the second input.

    internal const int AOM_BLEND_A64_ROUND_BITS = 6;
    internal const int AOM_BLEND_A64_MAX_ALPHA = (1 << AOM_BLEND_A64_ROUND_BITS);  // 64

    // [port] The arguments are int, or 8/16-bit pixels that C promotes to int.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AOM_BLEND_A64(int a, int v0, int v1) =>
        ROUND_POWER_OF_TWO((a) * (v0) + (AOM_BLEND_A64_MAX_ALPHA - (a)) * (v1), AOM_BLEND_A64_ROUND_BITS);

    // Alpha blending with alpha values from the range [0, 256], where 256
    // means use the first input and 0 means use the second input.
    internal const int AOM_BLEND_A256_ROUND_BITS = 8;
    internal const int AOM_BLEND_A256_MAX_ALPHA = (1 << AOM_BLEND_A256_ROUND_BITS);  // 256

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AOM_BLEND_A256(int a, int v0, int v1) =>
        ROUND_POWER_OF_TWO((a) * (v0) + (AOM_BLEND_A256_MAX_ALPHA - (a)) * (v1), AOM_BLEND_A256_ROUND_BITS);

    // Blending by averaging.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AOM_BLEND_AVG(int v0, int v1) => ROUND_POWER_OF_TWO((v0) + (v1), 1);

    internal const int DIFF_FACTOR_LOG2 = 4;
    internal const int DIFF_FACTOR = (1 << DIFF_FACTOR_LOG2);
}
