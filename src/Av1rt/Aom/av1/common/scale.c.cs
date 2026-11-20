// Port of libaom av1/common/scale.c

namespace Av1rt.Aom;

internal static partial class scale
{
    private static int get_fixed_point_scale_factor(int other_size, int this_size)
    {
        // Calculate scaling factor once for each reference frame
        // and use fixed point scaling factors in decoding and encoding routines.
        // Hardware implementations can calculate scale factor in device driver
        // and use multiplication and shifting on hardware instead of division.
        return ((other_size << REF_SCALE_SHIFT) + this_size / 2) / this_size;
    }

    // Given the fixed point scale, calculate coarse point scale.
    private static int fixed_point_scale_to_coarse_point_scale(int scale_fp)
    {
        return ROUND_POWER_OF_TWO(scale_fp, REF_SCALE_SHIFT - SCALE_SUBPEL_BITS);
    }

    // [subset] av1_scale_mv: never executed (references are never scaled in the subset).

    internal static void av1_setup_scale_factors_for_frame(scale_factors sf, int other_w,
                                                           int other_h, int this_w, int this_h)
    {
        if (valid_ref_frame_size(other_w, other_h, this_w, this_h) == 0)
        {
            sf.x_scale_fp = REF_INVALID_SCALE;
            sf.y_scale_fp = REF_INVALID_SCALE;
            return;
        }

        sf.x_scale_fp = get_fixed_point_scale_factor(other_w, this_w);
        sf.y_scale_fp = get_fixed_point_scale_factor(other_h, this_h);

        sf.x_step_q4 = fixed_point_scale_to_coarse_point_scale(sf.x_scale_fp);
        sf.y_step_q4 = fixed_point_scale_to_coarse_point_scale(sf.y_scale_fp);
    }
}
