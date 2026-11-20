// Port of libaom av1/encoder/superres_scale.c

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

// [subset] Frame resizing and super-resolution are off in the subset (resize_mode RESIZE_NONE,
// superres_mode AOM_SUPERRES_NONE): the energy analysis (analyze_hor_freq,
// get_energy_by_q2_thresh, get_superres_denom_*) and av1_superres_post_encode never run and
// are not ported.
internal static partial class superres_scale
{
    private static byte calculate_next_resize_scale(AV1_COMP cpi)
    {
        ref ResizeCfg resize_cfg = ref cpi.oxcf.resize_cfg;
        if (is_stat_generation_stage(cpi) != 0) return SCALE_NUMERATOR;
        byte new_denom = SCALE_NUMERATOR;

        if (cpi.common.seq_params.reduced_still_picture_hdr != 0) return SCALE_NUMERATOR;
        switch (resize_cfg.resize_mode)
        {
            case RESIZE_NONE: new_denom = SCALE_NUMERATOR; break;
            case RESIZE_FIXED:
                if (cpi.common.current_frame.frame_type == KEY_FRAME)
                    new_denom = resize_cfg.resize_kf_scale_denominator;
                else
                    new_denom = resize_cfg.resize_scale_denominator;
                break;
            // [subset] RESIZE_RANDOM (lcg_rand16 on a function-local seed): test-only mode.
            default:
                throw new NotSupportedException("resize mode not supported");
        }
        return new_denom;
    }

    internal static int av1_superres_in_recode_allowed(AV1_COMP cpi)
    {
        AV1EncoderConfig oxcf = cpi.oxcf;
        // Empirically found to not be beneficial for image coding.
        return B(oxcf.superres_cfg.superres_mode == AOM_SUPERRES_AUTO &&
                 cpi.sf.hl_sf.superres_auto_search_type != SUPERRES_AUTO_SOLO &&
                 cpi.rc.frames_to_key > 1);
    }

    private static byte calculate_next_superres_scale(AV1_COMP cpi)
    {
        AV1EncoderConfig oxcf = cpi.oxcf;
        ref SuperResCfg superres_cfg = ref oxcf.superres_cfg;

        if (is_stat_generation_stage(cpi) != 0) return SCALE_NUMERATOR;
        byte new_denom = SCALE_NUMERATOR;

        // Make sure that superres mode of the frame is consistent with the
        // sequence-level flag.
        Debug.Assert(superres_cfg.superres_mode == AOM_SUPERRES_NONE ||
                     cpi.common.seq_params.enable_superres != 0);
        Debug.Assert(cpi.common.seq_params.enable_superres != 0 ||
                     superres_cfg.superres_mode == AOM_SUPERRES_NONE);
        // Make sure that superres mode for current encoding is consistent with user
        // provided superres mode.
        Debug.Assert(superres_cfg.superres_mode == AOM_SUPERRES_AUTO ||
                     cpi.superres_mode == superres_cfg.superres_mode);

        // Note: we must look at the current superres_mode to be tried in 'cpi' here,
        // not the user given mode in 'oxcf'.
        switch (cpi.superres_mode)
        {
            case AOM_SUPERRES_NONE: new_denom = SCALE_NUMERATOR; break;
            case AOM_SUPERRES_FIXED:
                if (cpi.common.current_frame.frame_type == KEY_FRAME)
                    new_denom = superres_cfg.superres_kf_scale_denominator;
                else
                    new_denom = superres_cfg.superres_scale_denominator;
                break;
            // [subset] AOM_SUPERRES_RANDOM / QTHRESH / AUTO: super-resolution is disabled in
            // the subset (enable_superres is 0 for realtime with superres_mode NONE).
            default:
                throw new NotSupportedException("superres mode not supported");
        }
        return new_denom;
    }

    private static int dimension_is_ok(int orig_dim, int resized_dim, int denom)
    {
        return B(resized_dim * SCALE_NUMERATOR >= orig_dim * denom / 2);
    }

    private static int dimensions_are_ok(int owidth, int oheight, ref size_params_type rsz)
    {
        // Only need to check the width, as scaling is horizontal only.
        return dimension_is_ok(owidth, rsz.resize_width, rsz.superres_denom);
    }

    private static int validate_size_scales(RESIZE_MODE resize_mode,
                                            aom_superres_mode superres_mode, int owidth,
                                            int oheight, ref size_params_type rsz)
    {
        if (dimensions_are_ok(owidth, oheight, ref rsz) != 0)
        {  // Nothing to do.
            return 1;
        }

        // Calculate current resize scale.
        int resize_denom =
            Math.Max(DIVIDE_AND_ROUND(owidth * SCALE_NUMERATOR, rsz.resize_width),
                     DIVIDE_AND_ROUND(oheight * SCALE_NUMERATOR, rsz.resize_height));

        if (resize_mode != RESIZE_RANDOM && superres_mode == AOM_SUPERRES_RANDOM)
        {
            // Alter superres scale as needed to enforce conformity.
            rsz.superres_denom =
                (byte)((2 * SCALE_NUMERATOR * SCALE_NUMERATOR) / resize_denom);
            if (dimensions_are_ok(owidth, oheight, ref rsz) == 0)
            {
                if (rsz.superres_denom > SCALE_NUMERATOR) --rsz.superres_denom;
            }
        }
        else if (resize_mode == RESIZE_RANDOM &&
                 superres_mode != AOM_SUPERRES_RANDOM)
        {
            // Alter resize scale as needed to enforce conformity.
            resize_denom =
                (2 * SCALE_NUMERATOR * SCALE_NUMERATOR) / rsz.superres_denom;
            rsz.resize_width = owidth;
            rsz.resize_height = oheight;
            av1_calculate_scaled_size(ref rsz.resize_width, ref rsz.resize_height,
                                      resize_denom);
            if (dimensions_are_ok(owidth, oheight, ref rsz) == 0)
            {
                if (resize_denom > SCALE_NUMERATOR)
                {
                    --resize_denom;
                    rsz.resize_width = owidth;
                    rsz.resize_height = oheight;
                    av1_calculate_scaled_size(ref rsz.resize_width, ref rsz.resize_height,
                                              resize_denom);
                }
            }
        }
        else if (resize_mode == RESIZE_RANDOM &&
                 superres_mode == AOM_SUPERRES_RANDOM)
        {
            // Alter both resize and superres scales as needed to enforce conformity.
            do
            {
                if (resize_denom > rsz.superres_denom)
                    --resize_denom;
                else
                    --rsz.superres_denom;
                rsz.resize_width = owidth;
                rsz.resize_height = oheight;
                av1_calculate_scaled_size(ref rsz.resize_width, ref rsz.resize_height,
                                          resize_denom);
            } while (dimensions_are_ok(owidth, oheight, ref rsz) == 0 &&
                     (resize_denom > SCALE_NUMERATOR ||
                      rsz.superres_denom > SCALE_NUMERATOR));
        }
        else
        {  // We are allowed to alter neither resize scale nor superres
           // scale.
            return 0;
        }
        return dimensions_are_ok(owidth, oheight, ref rsz);
    }

    // Calculates resize and superres params for next frame
    private static size_params_type calculate_next_size_params(AV1_COMP cpi)
    {
        AV1EncoderConfig oxcf = cpi.oxcf;
        ref ResizePendingParams resize_pending_params = ref cpi.resize_pending_params;
        ref FrameDimensionCfg frm_dim_cfg = ref oxcf.frm_dim_cfg;
        size_params_type rsz = new size_params_type
        {
            resize_width = frm_dim_cfg.width,
            resize_height = frm_dim_cfg.height,
            superres_denom = SCALE_NUMERATOR,
        };
        int resize_denom = SCALE_NUMERATOR;
        // [subset] use_svc branch (spatial layers keep the layer's own size): no SVC.
        Debug.Assert(cpi.ppi.use_svc == 0);
        if (is_stat_generation_stage(cpi) != 0) return rsz;
        if (resize_pending_params.width != 0 && resize_pending_params.height != 0)
        {
            rsz.resize_width = resize_pending_params.width;
            rsz.resize_height = resize_pending_params.height;
            resize_pending_params.width = resize_pending_params.height = 0;
            if (oxcf.superres_cfg.superres_mode == AOM_SUPERRES_NONE) return rsz;
        }
        else
        {
            resize_denom = calculate_next_resize_scale(cpi);
            rsz.resize_width = frm_dim_cfg.width;
            rsz.resize_height = frm_dim_cfg.height;
            av1_calculate_scaled_size(ref rsz.resize_width, ref rsz.resize_height,
                                      resize_denom);
        }
        rsz.superres_denom = calculate_next_superres_scale(cpi);
        if (validate_size_scales(oxcf.resize_cfg.resize_mode, cpi.superres_mode,
                                 frm_dim_cfg.width, frm_dim_cfg.height, ref rsz) == 0)
            Debug.Assert(false, "Invalid scale parameters");
        return rsz;
    }

    private static void setup_frame_size_from_params(AV1_COMP cpi, in size_params_type rsz)
    {
        int encode_width = rsz.resize_width;
        int encode_height = rsz.resize_height;

        AV1_COMMON cm = cpi.common;
        cm.superres_upscaled_width = encode_width;
        cm.superres_upscaled_height = encode_height;
        cm.superres_scale_denominator = rsz.superres_denom;
        av1_calculate_scaled_superres_size(ref encode_width, ref encode_height,
                                           rsz.superres_denom);
        av1_set_frame_size(cpi, encode_width, encode_height);
    }

    internal static void av1_setup_frame_size(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        // Reset superres params from previous frame.
        cm.superres_scale_denominator = SCALE_NUMERATOR;
        size_params_type rsz = calculate_next_size_params(cpi);
        setup_frame_size_from_params(cpi, in rsz);

        Debug.Assert(av1_is_min_tile_width_satisfied(cm) != 0);
    }
}
