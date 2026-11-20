// Port of libaom av1/common/resize.h

global using static Av1rt.Aom.resize;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class resize
{
    // Filters for factor of 2 downsampling.
    // <gen:consts>
    internal const int UPSCALE_NORMATIVE_TAPS = 8;

    // int16_t av1_down2_symeven_half_filter[]
    internal static readonly short[] av1_down2_symeven_half_filter = {
        56, 12, -3, -1
    };

    // int16_t av1_down2_symodd_half_filter[]
    internal static readonly short[] av1_down2_symodd_half_filter = {
        64, 35, 0, -3
    };
    // </gen:consts>

    // Returns 1 if a superres upscaled frame is scaled and 0 otherwise.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_superres_scaled(AV1_COMMON cm)
    {
        // Note: for some corner cases (e.g. cm->width of 1), there may be no scaling
        // required even though cm->superres_scale_denominator != SCALE_NUMERATOR.
        // So, the following check is more accurate.
        return B(cm.width != cm.superres_upscaled_width);
    }

    // [subset] av1_has_optimized_scaler: resize only, never executed.
    // av1_resize_filter_normative is defined in resize.c (not part of p1a).
}
