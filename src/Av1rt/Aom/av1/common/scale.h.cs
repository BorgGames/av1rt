// Port of libaom av1/common/scale.h

global using static Av1rt.Aom.scale;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// PORT-NOTE: a class, not a struct (PORTING 3.3 lists it as a struct example). C keeps
// `const struct scale_factors *` in fields (xd->block_ref_scale_factors[],
// InterPredParams.scale_factors) pointing at cm->ref_scale_factors[i] or &cm->sf_identity,
// and fills them through pointers (av1_setup_scale_factors_for_frame). A class keeps those
// pointer semantics; nothing in scope copies a scale_factors by value.
internal sealed partial class scale_factors
{
    public int x_scale_fp;  // horizontal fixed point scale factor
    public int y_scale_fp;  // vertical fixed point scale factor
    public int x_step_q4;
    public int y_step_q4;
}

internal static partial class scale
{
    // <gen:consts>
    internal const int SCALE_NUMERATOR = 8;
    internal const int REF_SCALE_SHIFT = 14;
    internal const int REF_NO_SCALE = (1 << REF_SCALE_SHIFT);
    internal const int REF_INVALID_SCALE = -1;
    // </gen:consts>

    // Note: Expect val to be in q4 precision
    // [port] Never executed in the subset (references are never scaled); kept because
    // scaled_buffer_offset/init_subpel_params call it on the data-dependent scaled path.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_scaled_x(int val, scale_factors sf)
    {
        int off =
            (sf.x_scale_fp - (1 << REF_SCALE_SHIFT)) * (1 << (SUBPEL_BITS - 1));
        long tval = (long)val * sf.x_scale_fp + off;
        return (int)ROUND_POWER_OF_TWO_SIGNED_64(tval,
                                                 REF_SCALE_SHIFT - SCALE_EXTRA_BITS);
    }

    // Note: Expect val to be in q4 precision
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_scaled_y(int val, scale_factors sf)
    {
        int off =
            (sf.y_scale_fp - (1 << REF_SCALE_SHIFT)) * (1 << (SUBPEL_BITS - 1));
        long tval = (long)val * sf.y_scale_fp + off;
        return (int)ROUND_POWER_OF_TWO_SIGNED_64(tval,
                                                 REF_SCALE_SHIFT - SCALE_EXTRA_BITS);
    }

    // Note: Expect val to be in q4 precision
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_unscaled_value(int val, scale_factors sf)
    {
        return val * (1 << SCALE_EXTRA_BITS);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_is_valid_scale(scale_factors sf)
    {
        Debug.Assert(sf != null);
        return B(sf.x_scale_fp != REF_INVALID_SCALE &&
                 sf.y_scale_fp != REF_INVALID_SCALE);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_is_scaled(scale_factors sf)
    {
        Debug.Assert(sf != null);
        return B(av1_is_valid_scale(sf) != 0 &&
                 (sf.x_scale_fp != REF_NO_SCALE || sf.y_scale_fp != REF_NO_SCALE));
    }

    // See AV1 spec, Section 6.8.6. Frame size with refs semantics.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int valid_ref_frame_size(int ref_width, int ref_height,
                                             int this_width, int this_height)
    {
        return B(2 * this_width >= ref_width && 2 * this_height >= ref_height &&
                 this_width <= 16 * ref_width && this_height <= 16 * ref_height);
    }
}
