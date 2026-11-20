// Port of libaom av1/encoder/picklpf.c
//
// [subset] Only the LPF_PICK_FROM_Q path is ported: speed 11 (RT) sets
// lpf_sf.lpf_pick = LPF_PICK_FROM_Q. yv12_copy_plane, try_filter_frame and search_filter_level
// (the full/sub-image filter-level searches) are never executed.

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class picklpf
{
    private static int get_max_filter_level(AV1_COMP cpi)
    {
        if (is_stat_consumption_stage_twopass(cpi) != 0)
        {
            return cpi.ppi.twopass.section_intra_rating > 8 ? MAX_LOOP_FILTER * 3 / 4
                                                            : MAX_LOOP_FILTER;
        }
        else
        {
            return MAX_LOOP_FILTER;
        }
    }

    internal static void av1_pick_filter_level(YV12_BUFFER_CONFIG sd, AV1_COMP cpi,
                                               LPF_PICK_METHOD method)
    {
        AV1_COMMON cm = cpi.common;
        SequenceHeader seq_params = cm.seq_params;
        loopfilter lf = cm.lf;
        int disable_filter_rt_screen = 0;
        _ = sd;

        // Enable loop filter sharpness only for all-intra encoding mode,
        // or tune IQ or SSIMULACRA2. This is because:
        // - All-intra: frames do not have to serve as references to others
        // - Tune IQ/SSIMULACRA2: enabling loop filter sharpness has been found to
        //   be beneficial for sharpness perception
        if (cpi.oxcf.mode == ALLINTRA || cpi.oxcf.tune_cfg.tuning == AOM_TUNE_IQ ||
            cpi.oxcf.tune_cfg.tuning == AOM_TUNE_SSIMULACRA2)
        {
            lf.sharpness_level = cpi.oxcf.algo_cfg.sharpness;
        }
        else
        {
            lf.sharpness_level = 0;
        }

        if (cpi.oxcf.algo_cfg.enable_adaptive_sharpness)
        {
            // Loop filter sharpness levels are highly nonlinear. Visually, lf sharpness
            // 1 is closer to 7 than it is to 0, so in practice adaptive sharpness is
            // written to pick levels 0, 1 and 7 to keep it simple.
            int max_lf_sharpness;

            if (cm.quant_params.base_qindex <= 112)
            {
                max_lf_sharpness = 7;
            }
            else if (cm.quant_params.base_qindex <= 160)
            {
                max_lf_sharpness = 1;
            }
            else
            {
                max_lf_sharpness = 0;
            }

            lf.sharpness_level = AOMMIN(lf.sharpness_level, max_lf_sharpness);
        }

        if (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN &&
            cpi.sf.rt_sf.skip_lf_screen != 0)
        {
            // [subset] Screen content (active maps / cyclic-refresh skip of the loop filter):
            // tune_content is default in the subset; never executed.
            throw new NotSupportedException("av1_pick_filter_level: screen content skip_lf_screen");
        }

        if (disable_filter_rt_screen != 0 ||
            cpi.oxcf.algo_cfg.loopfilter_control == LOOPFILTER_NONE ||
            (cpi.oxcf.algo_cfg.loopfilter_control == LOOPFILTER_REFERENCE &&
             cpi.ppi.rtc_ref.non_reference_frame != 0))
        {
            lf.filter_level[0] = 0;
            lf.filter_level[1] = 0;
            return;
        }

        if (method == LPF_PICK_MINIMAL_LPF)
        {
            lf.filter_level[0] = 0;
            lf.filter_level[1] = 0;
        }
        else if (method >= LPF_PICK_FROM_Q)
        {
            const int min_filter_level = 0;
            int max_filter_level = get_max_filter_level(cpi);
            int q = av1_ac_quant_QTX(cm.quant_params.base_qindex, 0,
                                     seq_params.bit_depth);
            // based on tests result for rtc test set
            // 0.04590 boosted or 0.02295 non-booseted in 18-bit fixed point
            const int strength_boost_q_treshold = 0;
            int inter_frame_multiplier =
                (q > strength_boost_q_treshold ||
                 (cpi.sf.rt_sf.use_nonrd_pick_mode != 0 &&
                  cpi.common.width * cpi.common.height > 352 * 288))
                    ? 12034
                    : 6017;
            // Increase strength on base TL0 for temporal layers, for low-resoln,
            // based on frame source_sad.
            if (cpi.svc.number_temporal_layers > 1 &&
                cpi.svc.temporal_layer_id == 0 &&
                cpi.common.width * cpi.common.height <= 352 * 288 &&
                cpi.sf.rt_sf.use_nonrd_pick_mode != 0)
            {
                if (cpi.rc.frame_source_sad > 100000)
                    inter_frame_multiplier = inter_frame_multiplier << 1;
                else if (cpi.rc.frame_source_sad > 50000)
                    inter_frame_multiplier = 3 * (inter_frame_multiplier >> 1);
            }
            else if (cpi.sf.rt_sf.use_fast_fixed_part != 0)
            {
                inter_frame_multiplier = inter_frame_multiplier << 1;
            }
            // These values were determined by linear fitting the result of the
            // searched level for 8 bit depth:
            // Keyframes: filt_guess = q * 0.06699 - 1.60817
            // Other frames: filt_guess = q * inter_frame_multiplier + 2.48225
            //
            // 10 bit depth:
            // filt_guess = q * 0.01976 + 3.87252
            //
            // 12 bit depth:
            // filt_guess = q * 0.00494 + 3.87252
            int filt_guess;
            switch (seq_params.bit_depth)
            {
                case AOM_BITS_8:
                    filt_guess =
                        (cm.current_frame.frame_type == KEY_FRAME)
                            ? ROUND_POWER_OF_TWO(q * 17563 - 421574, 18)
                            : ROUND_POWER_OF_TWO(q * inter_frame_multiplier + 650707, 18);
                    break;
                // [subset] AOM_BITS_10 / AOM_BITS_12: 8-bit only.
                default:
                    throw new NotSupportedException("av1_pick_filter_level: bit_depth " + seq_params.bit_depth);
            }
            // [subset] `if (bit_depth != AOM_BITS_8 && KEY_FRAME) filt_guess -= 4;`: 8-bit only.

            // TODO(chengchen): retrain the model for Y, U, V filter levels
            lf.filter_level[0] = clamp(filt_guess, min_filter_level, max_filter_level);
            lf.filter_level[1] = clamp(filt_guess, min_filter_level, max_filter_level);
            lf.filter_level_u = clamp(filt_guess, min_filter_level, max_filter_level);
            lf.filter_level_v = clamp(filt_guess, min_filter_level, max_filter_level);
            if (cpi.oxcf.algo_cfg.loopfilter_control == LOOPFILTER_SELECTIVELY &&
                frame_is_intra_only(cm) == 0 && cpi.rc.high_source_sad == 0)
            {
                // [subset] LOOPFILTER_SELECTIVELY: loopfilter_control is LOOPFILTER_ALL in the
                // subset; never executed.
                throw new NotSupportedException("av1_pick_filter_level: LOOPFILTER_SELECTIVELY");
            }
        }
        else
        {
            // [subset] search_filter_level (LPF_PICK_FROM_FULL_IMAGE[_NON_DUAL] /
            // LPF_PICK_FROM_SUBIMAGE): speed 11 always uses LPF_PICK_FROM_Q; never executed.
            throw new NotSupportedException("av1_pick_filter_level: lpf search method " + method);
        }
    }
}
