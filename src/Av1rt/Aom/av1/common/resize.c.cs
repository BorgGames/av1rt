// Port of libaom av1/common/resize.c

using System;

namespace Av1rt.Aom;

// [subset] Frame resize and superres are off in the subset (resize_mode 0, superres_mode 0), so
// the scalers never run: the filters, interpolate*, down2_*, resize_multistep, av1_resize_*,
// upscale_normative_*, av1_resize_and_extend_frame*, av1_upscale_normative_rows,
// copy_buffer_config and av1_superres_upscale are not ported. Only the size helpers and the
// "is scaling required" check of av1_realloc_and_scale_if_required execute.
internal static partial class resize
{
    internal static YV12_BUFFER_CONFIG av1_realloc_and_scale_if_required(
        AV1_COMMON cm, YV12_BUFFER_CONFIG unscaled, YV12_BUFFER_CONFIG scaled,
        InterpFilter filter, int phase, bool use_optimized_scaler,
        bool for_psnr, int border_in_pixels, bool alloc_pyramid)
    {
        // If scaling is performed for the sole purpose of calculating PSNR, then our
        // target dimensions are superres upscaled width/height. Otherwise our target
        // dimensions are coded width/height.
        int scaled_width = for_psnr ? cm.superres_upscaled_width : cm.width;
        int scaled_height =
            for_psnr ? cm.superres_upscaled_height : cm.height;
        bool scaling_required = (scaled_width != unscaled.y_crop_width) ||
                                (scaled_height != unscaled.y_crop_height);

        if (scaling_required)
        {
            // [subset] Scaling a frame (aom_realloc_frame_buffer of the scaled buffer,
            // av1_resize_and_extend_frame / av1_resize_and_extend_frame_nonnormative and the
            // metadata copy) only happens with resize or superres, both off in the subset.
            throw new NotSupportedException(
                "av1_realloc_and_scale_if_required: frame scaling (resize/superres) is not ported");
        }
        return unscaled;
    }

    // Calculates the scaled dimension given the original dimension and the scale
    // denominator.
    private static void calculate_scaled_size_helper(ref int dim, int denom)
    {
        if (denom != SCALE_NUMERATOR)
        {
            // We need to ensure the constraint in "Appendix A" of the spec:
            // * FrameWidth is greater than or equal to 16
            // * FrameHeight is greater than or equal to 16
            // For this, we clamp the downscaled dimension to at least 16. One
            // exception: if original dimension itself was < 16, then we keep the
            // downscaled dimension to be same as the original, to ensure that resizing
            // is valid.
            int min_dim = AOMMIN(16, dim);
            // Use this version if we need *dim to be even
            // *width = (*width * SCALE_NUMERATOR + denom) / (2 * denom);
            // *width <<= 1;
            dim = (dim * SCALE_NUMERATOR + denom / 2) / (denom);
            dim = AOMMAX(dim, min_dim);
        }
    }

    internal static void av1_calculate_scaled_size(ref int width, ref int height, int resize_denom)
    {
        calculate_scaled_size_helper(ref width, resize_denom);
        calculate_scaled_size_helper(ref height, resize_denom);
    }

    internal static void av1_calculate_scaled_superres_size(ref int width, ref int height,
                                                            int superres_denom)
    {
        calculate_scaled_size_helper(ref width, superres_denom);
    }
}
