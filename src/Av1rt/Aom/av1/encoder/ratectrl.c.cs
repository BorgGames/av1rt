// Port of libaom av1/encoder/ratectrl.c
//
// Subset: one-pass realtime CBR, lag 0, no SVC, aq_mode 3 (cyclic refresh), speed 11,
// tune_content default (cpi->is_screen_content_type is never set in REALTIME unless the
// content is tuned as screen), 8-bit. Two-pass / VBR / CQ / Q-mode / LAP / TPL / SVC /
// dynamic-resize code is dropped with [subset] comments; data-dependent branches (scene
// change, overshoot, frame drop, buffer level adaptation) are ported.

using System;
using System.Diagnostics;
using static System.Math;

namespace Av1rt.Aom;

internal static partial class ratectrl
{
    // #define USE_UNRESTRICTED_Q_IN_CQ_MODE 0

    private const int RES_NUM = 2;
    private const int MODE_NUM = 2;

    // Max rate target for 1080P and below encodes under normal circumstances
    // (1920 * 1080 / (16 * 16)) * MAX_MB_RATE bits per MB
    private const int MAX_MB_RATE = 250;
    private const int MAXRATE_1080P = 2025000;

    private const double MIN_BPB_FACTOR = 0.005;
    private const int MAX_BPB_FACTOR = 50;

    private const int FRAME_OVERHEAD_BITS = 200;

    // Tables relating active max Q to active min Q
    // [subset] Only rtc_minq_8 is read in the subset (rc_pick_q_and_bounds_no_stats_cbr,
    // 8-bit). kf_low/high_motion_minq_*, arfgf_low/high_motion_minq_*, inter_minq_* are only
    // read by two-pass / VBR / Q-mode code, by forced key frames with frame_number > 0
    // (impossible: av1_encode() resets frame_number for REFBUF_RESET key frames) and by the
    // gf_cbr_boost_pct path (0 in the subset); the 10/12-bit tables are never read.
    private static readonly int[] rtc_minq_8 = new int[QINDEX_RANGE];

    // [subset] gf_high_1 .. kf_low_rtc are only used by get_kf_active_quality /
    // get_gf_active_quality (see above).

    // How many times less pixels there are to encode given the current scaling.
    // Temporary replacement for rcf_mult and rate_thresh_mult.
    private static double resize_rate_factor(in FrameDimensionCfg frm_dim_cfg,
                                             int width, int height)
    {
        return (double)(frm_dim_cfg.width * frm_dim_cfg.height) / (width * height);
    }

    // Functions to compute the active minq lookup table entries based on a
    // formulaic approach to facilitate easier adjustment of the Q tables.
    // The formulae were derived from computing a 3rd order polynomial best
    // fit to the original data (after plotting real maxq vs minq (not q index))
    private static int get_minq_index(double maxq, double x3, double x2, double x1,
                                      aom_bit_depth_t bit_depth)
    {
        double minqtarget = AOMMIN(((x3 * maxq + x2) * maxq + x1) * maxq, maxq);

        // Special case handling to deal with the step from q2.0
        // down to lossless mode represented by q 1.0.
        if (minqtarget <= 2.0) return 0;

        return av1_find_qindex(minqtarget, bit_depth, 0, QINDEX_RANGE - 1);
    }

    // [subset] x1[MODE_NUM][RES_NUM][5]: coefficients of the kf/arfgf/inter tables only.

    // [subset] Builds only the rtc table (see rtc_minq_8). C loops over MODE_NUM x RES_NUM and
    // rewrites rtc[i] with the same value each time; one pass gives the same table.
    private static void init_minq_luts(int[] rtc, aom_bit_depth_t bit_depth)
    {
        int i;
        for (i = 0; i < QINDEX_RANGE; i++)
        {
            double maxq = av1_convert_qindex_to_q(i, bit_depth);
            rtc[i] = get_minq_index(maxq, 0.00000271, -0.00113, 0.70, bit_depth);
        }
    }

    private static void rc_init_minq_luts()
    {
        init_minq_luts(rtc_minq_8, AOM_BITS_8);
        // [subset] 10/12-bit tables are never read (8-bit only).
    }

    private static readonly object s_minq_luts_lock = new();
    private static bool s_minq_luts_done;

    // aom_once(rc_init_minq_luts)
    internal static void av1_rc_init_minq_luts()
    {
        lock (s_minq_luts_lock)
        {
            if (s_minq_luts_done) return;
            rc_init_minq_luts();
            s_minq_luts_done = true;
        }
    }

    // These functions use formulaic calculations to make playing with the
    // quantizer tables easier. If necessary they can be replaced by lookup
    // tables if and when things settle down in the experimental bitstream
    internal static double av1_convert_qindex_to_q(int qindex, aom_bit_depth_t bit_depth)
    {
        // Convert the index to a real Q value (scaled down to match old Q values)
        switch (bit_depth)
        {
            case AOM_BITS_8: return av1_ac_quant_QTX(qindex, 0, bit_depth) / 4.0;
            case AOM_BITS_10: return av1_ac_quant_QTX(qindex, 0, bit_depth) / 16.0;
            case AOM_BITS_12: return av1_ac_quant_QTX(qindex, 0, bit_depth) / 64.0;
            default:
                Debug.Assert(false, "bit_depth should be AOM_BITS_8, AOM_BITS_10 or AOM_BITS_12");
                return -1.0;
        }
    }

    // Not executed in the subset (only allintra_vis.c calls it); tiny and part of the
    // public rate-control API, so it is ported (and tested) with the other q helpers.
    internal static int av1_convert_q_to_qindex(double q, aom_bit_depth_t bit_depth)
    {
        int low = MINQ;
        int high = MAXQ;

        // Find the first qindex that matches or exceeds q via binary search, as
        // av1_convert_qindex_to_q() is monotonically increasing with respect to
        // increasing qindex.
        while (low < high)
        {
            int mid = (low + high) >> 1;
            if (av1_convert_qindex_to_q(mid, bit_depth) < q)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    // Gets the appropriate bpmb enumerator based on the frame and content type
    private static int get_bpmb_enumerator(FRAME_TYPE frame_type,
                                           int is_screen_content_type)
    {
        int enumerator;

        if (is_screen_content_type != 0)
        {
            enumerator = (frame_type == KEY_FRAME) ? 1000000 : 750000;
        }
        else
        {
            enumerator = (frame_type == KEY_FRAME) ? 2000000 : 1500000;
        }

        return enumerator;
    }

    // [subset] get_init_ratio / adjust_rtc_keyframe: only reached with
    // sf.hl_sf.accurate_bit_estimate (speed 7 only) or sf.rt_sf.rc_adjust_keyframe (never set).

    internal static int av1_rc_bits_per_mb(AV1_COMP cpi, FRAME_TYPE frame_type, int qindex,
                                           double correction_factor, int accurate_estimate)
    {
        AV1_COMMON cm = cpi.common;
        int is_screen_content_type = cpi.is_screen_content_type;
        aom_bit_depth_t bit_depth = cm.seq_params.bit_depth;
        double q = av1_convert_qindex_to_q(qindex, bit_depth);
        int enumerator = get_bpmb_enumerator(frame_type, is_screen_content_type);

        Debug.Assert(correction_factor <= MAX_BPB_FACTOR &&
                     correction_factor >= MIN_BPB_FACTOR);

        if (cpi.oxcf.rc_cfg.mode == AOM_CBR && frame_type != KEY_FRAME &&
            accurate_estimate != 0 && cpi.rec_sse != ulong.MaxValue)
        {
            // [subset] sse based enumerator: sf.hl_sf.accurate_bit_estimate is 0 at speed 11.
            throw new NotSupportedException("accurate_bit_estimate is out of the subset");
        }
        else if (cpi.oxcf.rc_cfg.mode == AOM_CBR && frame_type == KEY_FRAME &&
                 cpi.sf.rt_sf.rc_adjust_keyframe != 0 && bit_depth == 8 &&
                 cpi.oxcf.rc_cfg.max_intra_bitrate_pct > 0 &&
                 cpi.svc.spatial_layer_id == 0)
        {
            // [subset] adjust_rtc_keyframe: sf.rt_sf.rc_adjust_keyframe is never set.
            throw new NotSupportedException("rc_adjust_keyframe is out of the subset");
        }
        // q based adjustment to baseline enumerator
        return (int)(enumerator * correction_factor / q);
    }

    internal static int av1_estimate_bits_at_q(AV1_COMP cpi, int q,
                                               double correction_factor)
    {
        AV1_COMMON cm = cpi.common;
        FRAME_TYPE frame_type = cm.current_frame.frame_type;
        int mbs = cm.mi_params.MBs;
        int bpm =
            (int)(av1_rc_bits_per_mb(cpi, frame_type, q, correction_factor,
                                     cpi.sf.hl_sf.accurate_bit_estimate));
        return AOMMAX(FRAME_OVERHEAD_BITS,
                      (int)((ulong)bpm * (ulong)mbs) >> BPER_MB_NORMBITS);
    }

    // [subset] clamp_pframe_target_size: only called by the one-pass VBR target size.

    private static int clamp_iframe_target_size(AV1_COMP cpi, long target)
    {
        RATE_CONTROL rc = cpi.rc;
        ref readonly RateControlCfg rc_cfg = ref cpi.oxcf.rc_cfg;
        if (rc_cfg.max_intra_bitrate_pct != 0)
        {
            long max_rate =
                (long)rc.avg_frame_bandwidth * rc_cfg.max_intra_bitrate_pct / 100;
            target = AOMMIN(target, max_rate);
        }
        if (target > rc.max_frame_bandwidth) target = rc.max_frame_bandwidth;
        return (int)target;
    }

    // [subset] update_layer_buffer_level: SVC only.

    // Update the buffer level: leaky bucket model.
    private static void update_buffer_level(AV1_COMP cpi, int encoded_frame_size)
    {
        AV1_COMMON cm = cpi.common;
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;

        // Non-viewable frames are a special case and are treated as pure overhead.
        if (cm.show_frame == 0)
            p_rc.bits_off_target -= encoded_frame_size;
        else
            p_rc.bits_off_target += rc.avg_frame_bandwidth - encoded_frame_size;

        // Clip the buffer level to the maximum specified buffer size.
        p_rc.bits_off_target =
            AOMMIN(p_rc.bits_off_target, p_rc.maximum_buffer_size);
        // For screen-content mode: don't let buffer level go below threshold,
        // given here as -rc->maximum_ buffer_size, to allow buffer to come back
        // up sooner after slide change with big overshoot.
        if (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN)
            p_rc.bits_off_target =
                AOMMAX(p_rc.bits_off_target, -p_rc.maximum_buffer_size);
        p_rc.buffer_level = p_rc.bits_off_target;

        if (cpi.ppi.use_svc != 0)
        {
            // [subset] update_layer_buffer_level: SVC is out of the subset.
            throw new NotSupportedException("SVC is out of the subset");
        }

        // [subset] CONFIG_FPMT_TEST == 0: temp_buffer_level update dropped.
    }

    internal static int av1_rc_get_default_min_gf_interval(int width, int height,
                                                           double framerate)
    {
        // Assume we do not need any constraint lower than 4K 20 fps
        const double factor_safe = 3840 * 2160 * 20.0;
        double factor = (double)width * height * framerate;
        int default_interval =
            clamp((int)(framerate * 0.125), MIN_GF_INTERVAL, MAX_GF_INTERVAL);

        if (factor <= factor_safe)
            return default_interval;
        else
            return AOMMAX(default_interval,
                          (int)(MIN_GF_INTERVAL * factor / factor_safe + 0.5));
        // Note this logic makes:
        // 4K24: 5
        // 4K30: 6
        // 4K60: 12
    }

    // Note get_default_max_gf_interval() requires the min_gf_interval to
    // be passed in to ensure that the max_gf_interval returned is at least as big
    // as that.
    private static int get_default_max_gf_interval(double framerate, int min_gf_interval)
    {
        int interval = AOMMIN(MAX_GF_INTERVAL, (int)(framerate * 0.75));
        interval += (interval & 0x01);  // Round to even value
        interval = AOMMAX(MAX_GF_INTERVAL, interval);
        return AOMMAX(interval, min_gf_interval);
    }

    internal static void av1_primary_rc_init(AV1EncoderConfig oxcf,
                                             PRIMARY_RATE_CONTROL p_rc)
    {
        ref readonly RateControlCfg rc_cfg = ref oxcf.rc_cfg;

        int worst_allowed_q = rc_cfg.worst_allowed_q;

        int min_gf_interval = oxcf.gf_cfg.min_gf_interval;
        int max_gf_interval = oxcf.gf_cfg.max_gf_interval;
        if (min_gf_interval == 0)
            min_gf_interval = av1_rc_get_default_min_gf_interval(
                oxcf.frm_dim_cfg.width, oxcf.frm_dim_cfg.height,
                oxcf.input_cfg.init_framerate);
        if (max_gf_interval == 0)
            max_gf_interval = get_default_max_gf_interval(
                oxcf.input_cfg.init_framerate, min_gf_interval);
        p_rc.baseline_gf_interval = (min_gf_interval + max_gf_interval) / 2;
        p_rc.this_key_frame_forced = 0;
        p_rc.next_key_frame_forced = 0;
        p_rc.ni_frames = 0;

        p_rc.tot_q = 0.0;
        p_rc.total_actual_bits = 0;
        p_rc.total_target_bits = 0;
        p_rc.buffer_level = p_rc.starting_buffer_level;

        if (oxcf.target_seq_level_idx[0] < SEQ_LEVELS)
        {
            worst_allowed_q = 255;
        }
        if (oxcf.pass == AOM_RC_ONE_PASS && rc_cfg.mode == AOM_CBR)
        {
            p_rc.avg_frame_qindex[KEY_FRAME] = worst_allowed_q;
            p_rc.avg_frame_qindex[INTER_FRAME] = worst_allowed_q;
        }
        else
        {
            p_rc.avg_frame_qindex[KEY_FRAME] =
                (worst_allowed_q + rc_cfg.best_allowed_q) / 2;
            p_rc.avg_frame_qindex[INTER_FRAME] =
                (worst_allowed_q + rc_cfg.best_allowed_q) / 2;
        }
        p_rc.avg_q = av1_convert_qindex_to_q(rc_cfg.worst_allowed_q,
                                             oxcf.tool_cfg.bit_depth);
        p_rc.last_q[KEY_FRAME] = rc_cfg.best_allowed_q;
        p_rc.last_q[INTER_FRAME] = rc_cfg.worst_allowed_q;

        for (int i = 0; i < RATE_FACTOR_LEVELS; ++i)
        {
            p_rc.rate_correction_factors[i] = 0.7;
        }
        p_rc.rate_correction_factors[KF_STD] = 1.0;
        p_rc.bits_off_target = p_rc.starting_buffer_level;

        double bits_per_frame =
            oxcf.rc_cfg.target_bandwidth / oxcf.input_cfg.init_framerate;
        p_rc.rolling_target_bits =
            AOMMAX(1, bits_per_frame > int.MaxValue ? int.MaxValue : (int)bits_per_frame);
        p_rc.rolling_actual_bits = p_rc.rolling_target_bits;
    }

    internal static void av1_rc_init(AV1EncoderConfig oxcf, RATE_CONTROL rc)
    {
        ref readonly RateControlCfg rc_cfg = ref oxcf.rc_cfg;

        rc.frames_since_key = 8;  // Sensible default for first frame.
        rc.frames_to_fwd_kf = oxcf.kf_cfg.fwd_kf_dist;

        rc.frames_till_gf_update_due = 0;
        rc.ni_av_qi = rc_cfg.worst_allowed_q;
        rc.ni_tot_qi = 0;

        rc.min_gf_interval = oxcf.gf_cfg.min_gf_interval;
        rc.max_gf_interval = oxcf.gf_cfg.max_gf_interval;
        if (rc.min_gf_interval == 0)
            rc.min_gf_interval = av1_rc_get_default_min_gf_interval(
                oxcf.frm_dim_cfg.width, oxcf.frm_dim_cfg.height,
                oxcf.input_cfg.init_framerate);
        if (rc.max_gf_interval == 0)
            rc.max_gf_interval = get_default_max_gf_interval(
                oxcf.input_cfg.init_framerate, rc.min_gf_interval);
        rc.avg_frame_low_motion = 0;

        rc.resize_state = ORIG;
        rc.resize_avg_qp = 0;
        rc.resize_buffer_underflow = 0;
        rc.resize_count = 0;
        rc.rtc_external_ratectrl = 0;
        rc.frame_level_fast_extra_bits = 0;
        rc.use_external_qp_one_pass = 0;
        rc.percent_blocks_inactive = 0;
        rc.force_max_q = 0;
        rc.postencode_drop = 0;
        rc.frames_since_scene_change = 0;
        rc.last_frame_low_source_sad = 0;
    }

    private static bool check_buffer_below_thresh(AV1_COMP cpi, long buffer_level,
                                                  int drop_mark)
    {
        if (cpi.ppi.use_svc == 0 || cpi.svc.number_spatial_layers == 1 ||
            cpi.svc.framedrop_mode == AOM_LAYER_DROP)
        {
            return (buffer_level <= drop_mark);
        }
        else
        {
            // [subset] AOM_FULL_SUPERFRAME_DROP over spatial layers: SVC only.
            throw new NotSupportedException("SVC is out of the subset");
        }
    }

    internal static int av1_rc_drop_frame(AV1_COMP cpi)
    {
        AV1EncoderConfig oxcf = cpi.oxcf;
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        // [subset] CONFIG_FPMT_TEST == 0.
        long buffer_level = p_rc.buffer_level;
        // Never drop on key frame, or for frame whose base layer is key.
        // If drop_count_consec hits or exceeds max_consec_drop then don't drop.
        if (cpi.common.current_frame.frame_type == KEY_FRAME ||
            (cpi.ppi.use_svc != 0 &&
             cpi.svc.layer_context[cpi.svc.temporal_layer_id].is_key_frame != 0) ||
            oxcf.rc_cfg.drop_frames_water_mark == 0 ||
            (rc.max_consec_drop > 0 &&
             rc.drop_count_consec >= rc.max_consec_drop))
        {
            return 0;
        }
        else
        {
            SVC svc = cpi.svc;
            // In the full_superframe framedrop mode for svc, if the previous spatial
            // layer was dropped, drop the current spatial layer.
            if (cpi.ppi.use_svc != 0 && svc.spatial_layer_id > 0 &&
                svc.drop_spatial_layer[svc.spatial_layer_id - 1] &&
                svc.framedrop_mode == AOM_FULL_SUPERFRAME_DROP)
                return 1;
            // -1 is passed here for drop_mark since we are checking if
            // buffer goes below 0 (<= -1).
            if (check_buffer_below_thresh(cpi, buffer_level, -1))
            {
                // Always drop if buffer is below 0.
                rc.drop_count_consec++;
                return 1;
            }
            else
            {
                // If buffer is below drop_mark, for now just drop every other frame
                // (starting with the next frame) until it increases back over drop_mark.
                int drop_mark = (int)(oxcf.rc_cfg.drop_frames_water_mark *
                                      p_rc.optimal_buffer_level / 100);
                bool buffer_below_thresh =
                    check_buffer_below_thresh(cpi, buffer_level, drop_mark);
                if (!buffer_below_thresh && rc.decimation_factor > 0)
                {
                    --rc.decimation_factor;
                }
                else if (buffer_below_thresh && rc.decimation_factor == 0)
                {
                    rc.decimation_factor = 1;
                }
                if (rc.decimation_factor > 0)
                {
                    if (rc.decimation_count > 0)
                    {
                        --rc.decimation_count;
                        rc.drop_count_consec++;
                        return 1;
                    }
                    else
                    {
                        rc.decimation_count = rc.decimation_factor;
                        return 0;
                    }
                }
                else
                {
                    rc.decimation_count = 0;
                    return 0;
                }
            }
        }
    }

    private static int adjust_q_cbr(AV1_COMP cpi, int q, int active_worst_quality,
                                    int width, int height)
    {
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        AV1_COMMON cm = cpi.common;
        SVC svc = cpi.svc;
        ref readonly RefreshFrameInfo refresh_frame = ref cpi.refresh_frame;
        // Flag to indicate previous frame has overshoot, and buffer level
        // for current frame is low (less than ~half of optimal). For such
        // (inter) frames, if the source_sad is non-zero, relax the max_delta_up
        // and clamp applied below.
        bool overshoot_buffer_low =
            cpi.rc.rc_1_frame == -1 && rc.frame_source_sad > 1000 &&
            p_rc.buffer_level < (p_rc.optimal_buffer_level >> 1) &&
            rc.frames_since_key > 4;
        int max_delta_down;
        int max_delta_up = overshoot_buffer_low ? 120 : 20;
        int change_avg_frame_bandwidth =
            B(Abs(rc.avg_frame_bandwidth - rc.prev_avg_frame_bandwidth) >
              0.1 * (rc.avg_frame_bandwidth));

        // Set the maximum adjustment down for Q for this frame.
        if (cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ &&
            cpi.cyclic_refresh.apply_cyclic_refresh != 0)
        {
            // For static screen type content limit the Q drop till the start of the
            // next refresh cycle.
            if (cpi.is_screen_content_type != 0 &&
                (cpi.cyclic_refresh.sb_index > cpi.cyclic_refresh.last_sb_index))
            {
                max_delta_down = clamp(rc.q_1_frame / 32, 1, 8);
            }
            else
            {
                max_delta_down = clamp(rc.q_1_frame / 8, 1, 16);
            }
            if (cpi.ppi.use_svc == 0 && cpi.is_screen_content_type != 0)
            {
                // Link max_delta_up to max_delta_down and buffer status.
                if (p_rc.buffer_level > p_rc.optimal_buffer_level)
                {
                    max_delta_up = AOMMAX(4, max_delta_down);
                }
                else if (!overshoot_buffer_low)
                {
                    max_delta_up = AOMMAX(8, max_delta_down);
                }
            }
        }
        else
        {
            max_delta_down = cpi.is_screen_content_type != 0
                                 ? clamp(rc.q_1_frame / 16, 1, 8)
                                 : clamp(rc.q_1_frame / 8, 1, 16);
        }
        // For screen static content with stable buffer level: relax the
        // limit on max_delta_down and apply bias qp, based on buffer fullness.
        // Only for high speeds levels for now to avoid bdrate regression.
        if (cpi.sf.rt_sf.rc_faster_convergence_static == 1 &&
            cpi.sf.rt_sf.check_scene_detection != 0 && rc.frame_source_sad == 0 &&
            rc.static_since_last_scene_change != 0 &&
            p_rc.buffer_level > (p_rc.optimal_buffer_level >> 1) &&
            cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ &&
            cpi.cyclic_refresh.counter_encode_maxq_scene_change > 4)
        {
            int qp_delta = 32;
            int qp_bias = 16;
            if (p_rc.buffer_level > p_rc.optimal_buffer_level)
            {
                qp_delta = 60;
                qp_bias = 32;
            }
            if (cpi.rc.rc_1_frame == 1) q = q - qp_bias;
            max_delta_down = AOMMAX(max_delta_down, qp_delta);
            max_delta_up = AOMMIN(max_delta_up, 4);
        }

        // If resolution changes or avg_frame_bandwidth significantly changed,
        // then set this flag to indicate change in target bits per macroblock.
        int change_target_bits_mb =
            B(cm.prev_frame != null &&
              (width != cm.prev_frame.width || height != cm.prev_frame.height ||
               change_avg_frame_bandwidth != 0));
        // Apply some control/clamp to QP under certain conditions.
        // Delay the use of the clamping for svc until after num_temporal_layers,
        // to make they have been set for each temporal layer.
        // Check for rc->q_1/2_frame > 0 in case they have not been set due to
        // dropped frames.
        if (frame_is_intra_only(cm) == 0 && rc.frames_since_key > 1 &&
            rc.q_1_frame > 0 && rc.q_2_frame > 0 &&
            (cpi.ppi.use_svc == 0 ||
             svc.current_superframe > (uint)svc.number_temporal_layers) &&
            change_target_bits_mb == 0 && cpi.rc.rtc_external_ratectrl == 0 &&
            (cpi.oxcf.rc_cfg.gf_cbr_boost_pct == 0 ||
             !(refresh_frame.alt_ref_frame || refresh_frame.golden_frame)))
        {
            // If in the previous two frames we have seen both overshoot and undershoot
            // clamp Q between the two.
            if (rc.rc_1_frame * rc.rc_2_frame == -1 &&
                rc.q_1_frame != rc.q_2_frame && !overshoot_buffer_low)
            {
                int qclamp = clamp(q, AOMMIN(rc.q_1_frame, rc.q_2_frame),
                                   AOMMAX(rc.q_1_frame, rc.q_2_frame));
                // If the previous frame had overshoot and the current q needs to
                // increase above the clamped value, reduce the clamp for faster reaction
                // to overshoot.
                if (cpi.rc.rc_1_frame == -1 && q > qclamp && rc.frames_since_key > 10)
                    q = (q + qclamp) >> 1;
                else
                    q = qclamp;
            }
            // Adjust Q base on source content change from scene detection.
            if (cpi.sf.rt_sf.check_scene_detection != 0 && rc.prev_avg_source_sad > 0 &&
                rc.frames_since_key > 10 && rc.frame_source_sad > 0 &&
                cpi.rc.rtc_external_ratectrl == 0)
            {
                int bit_depth = cm.seq_params.bit_depth;
                double delta =
                    (double)rc.avg_source_sad / (double)rc.prev_avg_source_sad - 1.0;
                // Push Q downwards if content change is decreasing and buffer level
                // is stable (at least 1/4-optimal level), so not overshooting. Do so
                // only for high Q to avoid excess overshoot.
                // Else reduce decrease in Q from previous frame if content change is
                // increasing and buffer is below max (so not undershooting).
                if (delta < 0.0 &&
                    p_rc.buffer_level > (p_rc.optimal_buffer_level >> 2) &&
                    q > (rc.worst_quality >> 1))
                {
                    double q_adj_factor = 1.0 + 0.5 * Tanh(4.0 * delta);
                    double q_val = av1_convert_qindex_to_q(q, bit_depth);
                    q += av1_compute_qdelta(rc, q_val, q_val * q_adj_factor, bit_depth);
                }
                else if (rc.q_1_frame - q > 0 && delta > 0.1 &&
                         p_rc.buffer_level < AOMMIN(p_rc.maximum_buffer_size,
                                                    p_rc.optimal_buffer_level << 1))
                {
                    q = (3 * q + rc.q_1_frame) >> 2;
                }
            }
            // Limit the decrease in Q from previous frame.
            if (rc.q_1_frame - q > max_delta_down) q = rc.q_1_frame - max_delta_down;
            // Limit the increase in Q from previous frame.
            else if (q - rc.q_1_frame > max_delta_up)
                q = rc.q_1_frame + max_delta_up;
        }
        // Adjustment for temporal layers.
        if (svc.number_temporal_layers > 1 && svc.spatial_layer_id == 0 &&
            change_target_bits_mb == 0 && cpi.rc.rtc_external_ratectrl == 0 &&
            cpi.oxcf.resize_cfg.resize_mode != RESIZE_DYNAMIC)
        {
            // [subset] temporal layers (SVC) are out of the subset.
            throw new NotSupportedException("SVC is out of the subset");
        }
        // For non-svc (single layer): if resolution has increased push q closer
        // to the active_worst to avoid excess overshoot.
        if (cpi.ppi.use_svc == 0 && cm.prev_frame != null &&
            (width * height > 1.5 * cm.prev_frame.width * cm.prev_frame.height))
            q = (q + active_worst_quality) >> 1;
        // For single layer RPS: Bias Q based on distance of closest reference.
        if (cpi.ppi.rtc_ref.bias_recovery_frame)
        {
            // [subset] av1_svc_get_min_ref_dist: bias_recovery_frame needs
            // set_ref_frame_config (AV1E_SET_SVC_REF_FRAME_CONFIG), never used.
            throw new NotSupportedException("set_ref_frame_config is out of the subset");
        }
        return clamp(q, cpi.rc.best_quality, cpi.rc.worst_quality);
    }

    // [subset] rate_factor_levels[] / get_rate_factor_level: two-pass / LAP only
    // (is_stat_consumption_stage).

    /// <summary>Gets a rate vs Q correction factor</summary>
    /// <remarks>
    /// This function returns the current value of a correction factor used to
    /// dynamically adjust the relationship between Q and the expected number
    /// of bits for the frame.
    /// </remarks>
    private static double get_rate_correction_factor(AV1_COMP cpi, int width,
                                                     int height)
    {
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        ref readonly RefreshFrameInfo refresh_frame = ref cpi.refresh_frame;
        double rcf;
        double rate_correction_factors_kfstd;
        double rate_correction_factors_gfarfstd;
        double rate_correction_factors_internormal;

        rate_correction_factors_kfstd =
            (cpi.ppi.gf_group.frame_parallel_level[cpi.gf_frame_index] > 0)
                ? rc.frame_level_rate_correction_factors[KF_STD]
                : p_rc.rate_correction_factors[KF_STD];
        rate_correction_factors_gfarfstd =
            (cpi.ppi.gf_group.frame_parallel_level[cpi.gf_frame_index] > 0)
                ? rc.frame_level_rate_correction_factors[GF_ARF_STD]
                : p_rc.rate_correction_factors[GF_ARF_STD];
        rate_correction_factors_internormal =
            (cpi.ppi.gf_group.frame_parallel_level[cpi.gf_frame_index] > 0)
                ? rc.frame_level_rate_correction_factors[INTER_NORMAL]
                : p_rc.rate_correction_factors[INTER_NORMAL];

        if (cpi.common.current_frame.frame_type == KEY_FRAME)
        {
            rcf = rate_correction_factors_kfstd;
        }
        else if (is_stat_consumption_stage(cpi) != 0)
        {
            // [subset] get_rate_factor_level: two-pass / LAP only.
            throw new NotSupportedException("two-pass is out of the subset");
        }
        else
        {
            if ((refresh_frame.alt_ref_frame || refresh_frame.golden_frame) &&
                rc.is_src_frame_alt_ref == 0 && cpi.ppi.use_svc == 0 &&
                (cpi.oxcf.rc_cfg.mode != AOM_CBR ||
                 cpi.oxcf.rc_cfg.gf_cbr_boost_pct > 20))
                rcf = rate_correction_factors_gfarfstd;
            else
                rcf = rate_correction_factors_internormal;
        }
        rcf *= resize_rate_factor(in cpi.oxcf.frm_dim_cfg, width, height);
        return fclamp(rcf, MIN_BPB_FACTOR, MAX_BPB_FACTOR);
    }

    /// <summary>Sets a rate vs Q correction factor</summary>
    /// <remarks>
    /// This function updates the current value of a correction factor used to
    /// dynamically adjust the relationship between Q and the expected number
    /// of bits for the frame. Updates the rate correction factor for the
    /// current frame type in cpi->rc.
    /// </remarks>
    private static void set_rate_correction_factor(AV1_COMP cpi, int is_encode_stage,
                                                   double factor, int width, int height)
    {
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        ref readonly RefreshFrameInfo refresh_frame = ref cpi.refresh_frame;
        int update_default_rcf = 1;
        // Normalize RCF to account for the size-dependent scaling factor.
        factor /= resize_rate_factor(in cpi.oxcf.frm_dim_cfg, width, height);

        factor = fclamp(factor, MIN_BPB_FACTOR, MAX_BPB_FACTOR);

        if (cpi.common.current_frame.frame_type == KEY_FRAME)
        {
            p_rc.rate_correction_factors[KF_STD] = factor;
        }
        else if (is_stat_consumption_stage(cpi) != 0)
        {
            // [subset] get_rate_factor_level: two-pass / LAP only.
            throw new NotSupportedException("two-pass is out of the subset");
        }
        else
        {
            if ((refresh_frame.alt_ref_frame || refresh_frame.golden_frame) &&
                rc.is_src_frame_alt_ref == 0 && cpi.ppi.use_svc == 0 &&
                (cpi.oxcf.rc_cfg.mode != AOM_CBR ||
                 cpi.oxcf.rc_cfg.gf_cbr_boost_pct > 20))
            {
                p_rc.rate_correction_factors[GF_ARF_STD] = factor;
            }
            else
            {
                if (is_encode_stage != 0 &&
                    cpi.ppi.gf_group.frame_parallel_level[cpi.gf_frame_index] > 0)
                {
                    rc.frame_level_rate_correction_factors[INTER_NORMAL] = factor;
                    update_default_rcf = 0;
                }
                if (update_default_rcf != 0)
                    p_rc.rate_correction_factors[INTER_NORMAL] = factor;
            }
        }
    }

    internal static void av1_rc_update_rate_correction_factors(AV1_COMP cpi, int is_encode_stage,
                                                               int width, int height)
    {
        AV1_COMMON cm = cpi.common;
        double correction_factor = 1.0;
        double rate_correction_factor =
            get_rate_correction_factor(cpi, width, height);
        double adjustment_limit;
        int projected_size_based_on_q = 0;
        int cyclic_refresh_active =
            B(cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ && cpi.common.seg.enabled != 0);

        // Do not update the rate factors for arf overlay frames.
        if (cpi.rc.is_src_frame_alt_ref != 0) return;

        // Don't update rate correction factors here on scene changes as
        // it is already reset in av1_encodedframe_overshoot_cbr(),
        // but reset variables related to previous frame q and size.
        // Note that the counter of frames since the last scene change
        // is only valid when cyclic refresh mode is enabled and that
        // this break out only applies to scene changes that are not
        // recorded as INTRA only key frames.
        // Note that av1_encodedframe_overshoot_cbr() is only entered
        // if cpi->sf.rt_sf.overshoot_detection_cbr == FAST_DETECTION_MAXQ
        // and cpi->rc.high_source_sad = 1.
        if ((cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ) &&
            (cpi.sf.rt_sf.overshoot_detection_cbr == FAST_DETECTION_MAXQ) &&
            cpi.rc.high_source_sad != 0 &&
            (cpi.cyclic_refresh.counter_encode_maxq_scene_change == 0) &&
            frame_is_intra_only(cm) == 0 && cpi.ppi.use_svc == 0)
        {
            cpi.rc.q_2_frame = cm.quant_params.base_qindex;
            cpi.rc.q_1_frame = cm.quant_params.base_qindex;
            cpi.rc.rc_2_frame = 0;
            cpi.rc.rc_1_frame = 0;
            return;
        }

        // Clear down mmx registers to allow floating point in what follows

        // Work out how big we would have expected the frame to be at this Q given
        // the current correction factor.
        // Stay in double to avoid int overflow when values are large
        if (cyclic_refresh_active != 0)
        {
            projected_size_based_on_q =
                av1_cyclic_refresh_estimate_bits_at_q(cpi, rate_correction_factor);
        }
        else
        {
            projected_size_based_on_q = av1_estimate_bits_at_q(
                cpi, cm.quant_params.base_qindex, rate_correction_factor);
        }
        // Work out a size correction factor.
        if (projected_size_based_on_q > FRAME_OVERHEAD_BITS)
            correction_factor = (double)cpi.rc.projected_frame_size /
                                (double)projected_size_based_on_q;

        // Clamp correction factor to prevent anything too extreme
        correction_factor = AOMMAX(correction_factor, 0.25);

        cpi.rc.q_2_frame = cpi.rc.q_1_frame;
        cpi.rc.q_1_frame = cm.quant_params.base_qindex;
        cpi.rc.rc_2_frame = cpi.rc.rc_1_frame;
        if (correction_factor > 1.1)
            cpi.rc.rc_1_frame = -1;
        else if (correction_factor < 0.9)
            cpi.rc.rc_1_frame = 1;
        else
            cpi.rc.rc_1_frame = 0;

        // Decide how heavily to dampen the adjustment
        if (correction_factor > 0.0)
        {
            if (cpi.is_screen_content_type != 0)
            {
                adjustment_limit =
                    0.25 + 0.5 * AOMMIN(0.5, Abs(Log10(correction_factor)));
            }
            else
            {
                adjustment_limit =
                    0.25 + 0.75 * AOMMIN(0.5, Abs(Log10(correction_factor)));
            }
        }
        else
        {
            adjustment_limit = 0.75;
        }

        // Adjustment to delta Q and number of blocks updated in cyclic refresh
        // based on over or under shoot of target in current frame.
        if (cyclic_refresh_active != 0 && cpi.rc.this_frame_target > 0)
        {
            CYCLIC_REFRESH cr = cpi.cyclic_refresh;
            if (correction_factor > 1.25)
            {
                cr.percent_refresh_adjustment =
                    AOMMAX(cr.percent_refresh_adjustment - 1, -5);
                cr.rate_ratio_qdelta_adjustment =
                    AOMMAX(cr.rate_ratio_qdelta_adjustment - 0.05, -0.0);
            }
            else if (correction_factor < 0.5)
            {
                cr.percent_refresh_adjustment =
                    AOMMIN(cr.percent_refresh_adjustment + 1, 5);
                cr.rate_ratio_qdelta_adjustment =
                    AOMMIN(cr.rate_ratio_qdelta_adjustment + 0.05, 0.25);
            }
        }

        if (correction_factor > 1.01)
        {
            // We are not already at the worst allowable quality
            correction_factor = (1.0 + ((correction_factor - 1.0) * adjustment_limit));
            rate_correction_factor = rate_correction_factor * correction_factor;
            // Keep rate_correction_factor within limits
            if (rate_correction_factor > MAX_BPB_FACTOR)
                rate_correction_factor = MAX_BPB_FACTOR;
        }
        else if (correction_factor < 0.99)
        {
            // We are not already at the best allowable quality
            correction_factor = 1.0 / correction_factor;
            correction_factor = (1.0 + ((correction_factor - 1.0) * adjustment_limit));
            correction_factor = 1.0 / correction_factor;

            rate_correction_factor = rate_correction_factor * correction_factor;

            // Keep rate_correction_factor within limits
            if (rate_correction_factor < MIN_BPB_FACTOR)
                rate_correction_factor = MIN_BPB_FACTOR;
        }

        set_rate_correction_factor(cpi, is_encode_stage, rate_correction_factor,
                                   width, height);
    }

    // Calculate rate for the given 'q'.
    private static int get_bits_per_mb(AV1_COMP cpi, int use_cyclic_refresh,
                                       double correction_factor, int q)
    {
        AV1_COMMON cm = cpi.common;
        return use_cyclic_refresh != 0
                   ? av1_cyclic_refresh_rc_bits_per_mb(cpi, q, correction_factor)
                   : av1_rc_bits_per_mb(cpi, cm.current_frame.frame_type, q,
                                        correction_factor,
                                        cpi.sf.hl_sf.accurate_bit_estimate);
    }

    /// <summary>
    /// Searches for a Q index value predicted to give an average macro
    /// block rate closest to the target value.
    /// </summary>
    /// <remarks>
    /// Similar to find_qindex_by_rate() function, but returns a q index with a
    /// rate just above or below the desired rate, depending on which of the two
    /// rates is closer to the desired rate.
    /// Also, respects the selected aq_mode when computing the rate.
    /// </remarks>
    private static int find_closest_qindex_by_rate(int desired_bits_per_mb,
                                                   AV1_COMP cpi,
                                                   double correction_factor,
                                                   int best_qindex, int worst_qindex)
    {
        int use_cyclic_refresh = B(cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ &&
                                   cpi.cyclic_refresh.apply_cyclic_refresh != 0);

        // Find 'qindex' based on 'desired_bits_per_mb'.
        Debug.Assert(best_qindex <= worst_qindex);
        int low = best_qindex;
        int high = worst_qindex;
        while (low < high)
        {
            int mid = (low + high) >> 1;
            int mid_bits_per_mb =
                get_bits_per_mb(cpi, use_cyclic_refresh, correction_factor, mid);
            if (mid_bits_per_mb > desired_bits_per_mb)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        Debug.Assert(low == high);

        // Calculate rate difference of this q index from the desired rate.
        int curr_q = low;
        int curr_bits_per_mb =
            get_bits_per_mb(cpi, use_cyclic_refresh, correction_factor, curr_q);
        int curr_bit_diff = (curr_bits_per_mb <= desired_bits_per_mb)
                                ? desired_bits_per_mb - curr_bits_per_mb
                                : int.MaxValue;
        Debug.Assert((curr_bit_diff != int.MaxValue && curr_bit_diff >= 0) ||
                     curr_q == worst_qindex);

        // Calculate rate difference for previous q index too.
        int prev_q = curr_q - 1;
        int prev_bit_diff;
        if (curr_bit_diff == int.MaxValue || curr_q == best_qindex)
        {
            prev_bit_diff = int.MaxValue;
        }
        else
        {
            int prev_bits_per_mb =
                get_bits_per_mb(cpi, use_cyclic_refresh, correction_factor, prev_q);
            Debug.Assert(prev_bits_per_mb > desired_bits_per_mb);
            prev_bit_diff = prev_bits_per_mb - desired_bits_per_mb;
        }

        // Pick one of the two q indices, depending on which one has rate closer to
        // the desired rate.
        return (curr_bit_diff <= prev_bit_diff) ? curr_q : prev_q;
    }

    internal static int av1_rc_regulate_q(AV1_COMP cpi, int target_bits_per_frame,
                                          int active_best_quality, int active_worst_quality,
                                          int width, int height)
    {
        int MBs = av1_get_MBs(width, height);
        double correction_factor =
            get_rate_correction_factor(cpi, width, height);
        int target_bits_per_mb =
            (int)(((ulong)target_bits_per_frame << BPER_MB_NORMBITS) / (ulong)MBs);

        int q =
            find_closest_qindex_by_rate(target_bits_per_mb, cpi, correction_factor,
                                        active_best_quality, active_worst_quality);
        if (cpi.oxcf.rc_cfg.mode == AOM_CBR && has_no_stats_stage(cpi) != 0)
            return adjust_q_cbr(cpi, q, active_worst_quality, width, height);

        return q;
    }

    // [subset] get_active_quality, get_kf_active_quality, get_gf_active_quality_no_rc,
    // get_gf_active_quality, get_gf_high_motion_quality, calc_active_worst_quality_no_stats_vbr:
    // VBR / two-pass / gf_cbr_boost_pct / forced key frames with frame_number > 0 only.

    // Adjust active_worst_quality level based on buffer level.
    private static int calc_active_worst_quality_no_stats_cbr(AV1_COMP cpi)
    {
        // Adjust active_worst_quality: If buffer is above the optimal/target level,
        // bring active_worst_quality down depending on fullness of buffer.
        // If buffer is below the optimal level, let the active_worst_quality go from
        // ambient Q (at buffer = optimal level) to worst_quality level
        // (at buffer = critical level).
        AV1_COMMON cm = cpi.common;
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        SVC svc = cpi.svc;
        uint num_frames_weight_key = (uint)(5 * cpi.svc.number_temporal_layers);
        // Buffer level below which we push active_worst to worst_quality.
        long critical_level = p_rc.optimal_buffer_level >> 3;
        long buff_lvl_step = 0;
        int adjustment = 0;
        int active_worst_quality;
        int ambient_qp;
        if (frame_is_intra_only(cm) != 0)
        {
            // Allow for active_worst_quality to go lower than rc->worst_quality (max)
            // under certain conditions: that the frame spatial variance is below
            // threshold and the buffer is full/stable. Also check the encoded vs target
            // size for the last keyframe.
            if (cpi.sf.rt_sf.rc_compute_spatial_var_sc_kf != 0 &&
                svc.number_spatial_layers == 1 && rc.frame_spatial_variance < 1000 &&
                p_rc.buffer_level > p_rc.optimal_buffer_level &&
                p_rc.optimal_buffer_level > (rc.avg_frame_bandwidth << 3) &&
                rc.last_encoded_size_keyframe < (rc.last_target_size_keyframe << 3))
            {
                if (p_rc.buffer_level > ((9 * p_rc.optimal_buffer_level) >> 3))
                    ambient_qp = p_rc.avg_frame_qindex[INTER_FRAME];
                else
                    ambient_qp =
                        (rc.worst_quality + p_rc.avg_frame_qindex[INTER_FRAME]) >> 1;
                return AOMMIN(rc.worst_quality, AOMMAX(ambient_qp, rc.best_quality));
            }
            else
            {
                return rc.worst_quality;
            }
        }
        // For ambient_qp we use minimum of avg_frame_qindex[KEY_FRAME/INTER_FRAME]
        // for the first few frames following key frame. These are both initialized
        // to worst_quality and updated with (3/4, 1/4) average in postencode_update.
        // So for first few frames following key, the qp of that key frame is weighted
        // into the active_worst_quality setting. For SVC the key frame should
        // correspond to layer (0, 0), so use that for layer context.
        int avg_qindex_key = p_rc.avg_frame_qindex[KEY_FRAME];
        if (svc.number_temporal_layers > 1)
        {
            // [subset] layer (0, 0) context: temporal layers are out of the subset.
            throw new NotSupportedException("SVC is out of the subset");
        }
        if (svc.temporal_layer_id > 0 &&
            rc.frames_since_key < 2 * svc.number_temporal_layers)
        {
            ambient_qp = avg_qindex_key;
        }
        else
        {
            ambient_qp =
                (cm.current_frame.frame_number < num_frames_weight_key)
                    ? AOMMIN(p_rc.avg_frame_qindex[INTER_FRAME], avg_qindex_key)
                    : p_rc.avg_frame_qindex[INTER_FRAME];
        }
        ambient_qp = AOMMIN(rc.worst_quality, ambient_qp);

        if (p_rc.buffer_level > p_rc.optimal_buffer_level)
        {
            // Adjust down.
            int max_adjustment_down;  // Maximum adjustment down for Q

            if (cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ && cpi.ppi.use_svc == 0 &&
                (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN))
            {
                active_worst_quality = AOMMIN(rc.worst_quality, ambient_qp);
                max_adjustment_down = AOMMIN(4, active_worst_quality / 16);
            }
            else
            {
                active_worst_quality = AOMMIN(rc.worst_quality, ambient_qp * 5 / 4);
                max_adjustment_down = active_worst_quality / 3;
            }

            if (max_adjustment_down != 0)
            {
                buff_lvl_step =
                    ((p_rc.maximum_buffer_size - p_rc.optimal_buffer_level) /
                     max_adjustment_down);
                if (buff_lvl_step != 0)
                    adjustment = (int)((p_rc.buffer_level - p_rc.optimal_buffer_level) /
                                       buff_lvl_step);
                active_worst_quality -= adjustment;
            }
        }
        else if (p_rc.buffer_level > critical_level)
        {
            // Adjust up from ambient Q.
            active_worst_quality = AOMMIN(rc.worst_quality, ambient_qp);
            if (critical_level != 0)
            {
                buff_lvl_step = (p_rc.optimal_buffer_level - critical_level);
                if (buff_lvl_step != 0)
                {
                    adjustment = (int)((rc.worst_quality - ambient_qp) *
                                       (p_rc.optimal_buffer_level - p_rc.buffer_level) /
                                       buff_lvl_step);
                }
                active_worst_quality += adjustment;
            }
        }
        else
        {
            // Set to worst_quality if buffer is below critical level.
            active_worst_quality = rc.worst_quality;
        }
        return active_worst_quality;
    }

    // Calculate the active_best_quality level.
    private static int calc_active_best_quality_no_stats_cbr(AV1_COMP cpi,
                                                             int active_worst_quality,
                                                             int width, int height)
    {
        AV1_COMMON cm = cpi.common;
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        ref readonly RefreshFrameInfo refresh_frame = ref cpi.refresh_frame;
        CurrentFrame current_frame = cm.current_frame;
        int[] rtc_minq;
        int bit_depth = cm.seq_params.bit_depth;
        int active_best_quality = rc.best_quality;
        // ASSIGN_MINQ_TABLE(bit_depth, rtc_minq)
        switch (bit_depth)
        {
            case AOM_BITS_8: rtc_minq = rtc_minq_8; break;
            default:
                // [subset] 10/12-bit tables: 8-bit only.
                throw new NotSupportedException("high bitdepth is out of the subset");
        }

        // [subset] is_608p_or_larger / is_480p_or_larger / res_idx / rtc_mode only feed
        // get_kf_active_quality / get_gf_active_quality (dropped, see below).

        if (frame_is_intra_only(cm) != 0)
        {
            // Handle the special case for key frames forced when we have reached
            // the maximum key frame interval. Here force the Q to a range
            // based on the ambient Q to reduce the risk of popping.
            if (p_rc.this_key_frame_forced != 0)
            {
                int qindex = p_rc.last_boosted_qindex;
                double last_boosted_q = av1_convert_qindex_to_q(qindex, bit_depth);
                int delta_qindex = av1_compute_qdelta(rc, last_boosted_q,
                                                      (last_boosted_q * 0.75), bit_depth);
                active_best_quality = AOMMAX(qindex + delta_qindex, rc.best_quality);
            }
            else if (current_frame.frame_number > 0)
            {
                // [subset] not first frame of one pass and kf_boost is set:
                // get_kf_active_quality. Unreachable: av1_encode() resets frame_number to
                // 0 for every key frame (refbuf_state REFBUF_RESET, set by
                // av1_get_one_pass_rt_params), and INTRA_ONLY frames need
                // set_ref_frame_config.
                throw new NotSupportedException("intra frame with frame_number > 0");
            }
        }
        else if (rc.is_src_frame_alt_ref == 0 && cpi.ppi.use_svc == 0 &&
                 cpi.oxcf.rc_cfg.gf_cbr_boost_pct != 0 &&
                 (refresh_frame.golden_frame || refresh_frame.alt_ref_frame))
        {
            // [subset] get_gf_active_quality: gf_cbr_boost_pct is 0 in the subset.
            throw new NotSupportedException("gf_cbr_boost_pct is out of the subset");
        }
        else
        {
            // Use the lower of active_worst_quality and recent/average Q.
            FRAME_TYPE frame_type =
                (current_frame.frame_number > 1) ? INTER_FRAME : KEY_FRAME;
            if (p_rc.avg_frame_qindex[frame_type] < active_worst_quality)
                active_best_quality = rtc_minq[p_rc.avg_frame_qindex[frame_type]];
            else
                active_best_quality = rtc_minq[active_worst_quality];
        }
        return active_best_quality;
    }

    // [subset] get_q_passive_strategy: RT_PASSIVE_STRATEGY == 0.

    /// <summary>Picks q and q bounds given CBR rate control parameters in cpi->rc.</summary>
    /// <remarks>
    /// Handles the special case when using:
    /// - Constant bit-rate mode: cpi->oxcf.rc_cfg.mode == AOM_CBR, and
    /// - 1-pass encoding without LAP (look-ahead processing), so 1st pass stats are
    /// NOT available.
    /// </remarks>
    private static int rc_pick_q_and_bounds_no_stats_cbr(AV1_COMP cpi, int width,
                                                         int height, ref int bottom_index,
                                                         ref int top_index)
    {
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        CurrentFrame current_frame = cpi.common.current_frame;
        int q;
        int active_worst_quality = calc_active_worst_quality_no_stats_cbr(cpi);
        int active_best_quality = calc_active_best_quality_no_stats_cbr(
            cpi, active_worst_quality, width, height);
        Debug.Assert(has_no_stats_stage(cpi) != 0);
        Debug.Assert(cpi.oxcf.rc_cfg.mode == AOM_CBR);

        // Clip the active best and worst quality values to limits
        active_best_quality =
            clamp(active_best_quality, rc.best_quality, rc.worst_quality);
        active_worst_quality =
            clamp(active_worst_quality, active_best_quality, rc.worst_quality);

        top_index = active_worst_quality;
        bottom_index = active_best_quality;

        // Limit Q range for the adaptive loop.
        if (current_frame.frame_type == KEY_FRAME && p_rc.this_key_frame_forced == 0 &&
            current_frame.frame_number != 0)
        {
            int qdelta = 0;
            qdelta = av1_compute_qdelta_by_rate(cpi, current_frame.frame_type,
                                                active_worst_quality, 2.0);
            top_index = active_worst_quality + qdelta;
            top_index = AOMMAX(top_index, bottom_index);
        }

        q = av1_rc_regulate_q(cpi, rc.this_frame_target, active_best_quality,
                              active_worst_quality, width, height);
        // [subset] RT_PASSIVE_STRATEGY == 0.
        if (q > top_index)
        {
            // Special case when we are targeting the max allowed rate
            if (rc.this_frame_target >= rc.max_frame_bandwidth)
                top_index = q;
            else
                q = top_index;
        }

        Debug.Assert(top_index <= rc.worst_quality && top_index >= rc.best_quality);
        Debug.Assert(bottom_index <= rc.worst_quality &&
                     bottom_index >= rc.best_quality);
        Debug.Assert(q <= rc.worst_quality && q >= rc.best_quality);
        return q;
    }

    // [subset] gf_group_pyramid_level, get_active_cq_level, rc_pick_q_and_bounds_no_stats,
    // arf_layer_deltas / frame_type_qdelta, get_intra_q_and_bounds,
    // adjust_active_best_and_worst_quality, get_q, get_active_best_quality,
    // rc_pick_q_and_bounds_q_mode, rc_pick_q_and_bounds: VBR / CQ / Q / two-pass only.
    // [subset] rc_compute_variance_onepass_rt: sf.hl_sf.accurate_bit_estimate is 0 at speed 11.

    internal static int av1_rc_pick_q_and_bounds(AV1_COMP cpi, int width, int height, int gf_index,
                                                 ref int bottom_index, ref int top_index)
    {
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        int q;
        // TODO(sarahparker) merge no-stats vbr and altref q computation
        // with rc_pick_q_and_bounds().
        GF_GROUP gf_group = cpi.ppi.gf_group;
        if ((cpi.oxcf.rc_cfg.mode != AOM_Q ||
             gf_group.update_type[gf_index] == ARF_UPDATE) &&
            has_no_stats_stage(cpi) != 0)
        {
            if (cpi.oxcf.rc_cfg.mode == AOM_CBR)
            {
                // TODO(yunqing): the results could be used for encoder optimization.
                cpi.rec_sse = ulong.MaxValue;
                if (cpi.sf.hl_sf.accurate_bit_estimate != 0 &&
                    cpi.common.current_frame.frame_type != KEY_FRAME)
                {
                    // [subset] rc_compute_variance_onepass_rt: accurate_bit_estimate is 0
                    // at speed 11.
                    throw new NotSupportedException("accurate_bit_estimate is out of the subset");
                }

                q = rc_pick_q_and_bounds_no_stats_cbr(cpi, width, height, ref bottom_index,
                                                      ref top_index);
                // preserve copy of active worst quality selected.
                cpi.rc.active_worst_quality = top_index;
            }
            else
            {
                // [subset] rc_pick_q_and_bounds_no_stats: VBR / CQ only.
                throw new NotSupportedException("VBR/CQ is out of the subset");
            }
        }
        else
        {
            // [subset] rc_pick_q_and_bounds: Q mode / two-pass only.
            throw new NotSupportedException("Q mode / two-pass is out of the subset");
        }
        if (gf_group.update_type[gf_index] == ARF_UPDATE) p_rc.arf_q = q;

        return q;
    }

    // [subset] av1_rc_compute_frame_size_bounds: recode loop only (never in RT).

    internal static void av1_rc_set_frame_target(AV1_COMP cpi, int target, int width, int height)
    {
        AV1_COMMON cm = cpi.common;
        RATE_CONTROL rc = cpi.rc;

        rc.this_frame_target = target;

        // Modify frame size target when down-scaled.
        if (av1_frame_scaled(cm) != 0 && cpi.oxcf.rc_cfg.mode != AOM_CBR)
        {
            rc.this_frame_target = saturate_cast_double_to_int(
                rc.this_frame_target *
                resize_rate_factor(in cpi.oxcf.frm_dim_cfg, width, height));
        }

        // Target rate per SB64 (including partial SB64s.
        long sb64_target_rate =
            ((long)rc.this_frame_target << 12) / (width * height);
        rc.sb64_target_rate = (int)AOMMIN(sb64_target_rate, int.MaxValue);
    }

    // [subset] update_alt_ref_frame_stats: only with alt-ref enabled (lag_in_frames > 0).

    private static void update_golden_frame_stats(AV1_COMP cpi)
    {
        RATE_CONTROL rc = cpi.rc;

        // Update the Golden frame usage counts.
        if (cpi.refresh_frame.golden_frame || rc.is_src_frame_alt_ref != 0)
        {
            rc.frames_since_golden = 0;
        }
        else if (cpi.common.show_frame != 0)
        {
            rc.frames_since_golden++;
        }
    }

    internal static void av1_rc_postencode_update(AV1_COMP cpi, ulong bytes_used)
    {
        AV1_COMMON cm = cpi.common;
        CurrentFrame current_frame = cm.current_frame;
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        GF_GROUP gf_group = cpi.ppi.gf_group;
        ref readonly RefreshFrameInfo refresh_frame = ref cpi.refresh_frame;

        int is_intrnl_arf =
            B(gf_group.update_type[cpi.gf_frame_index] == INTNL_ARF_UPDATE);

        int qindex = cm.quant_params.base_qindex;

        // [subset] RT_PASSIVE_STRATEGY == 0: q_history not updated.

        // Update rate control heuristics
        rc.projected_frame_size = (int)(bytes_used << 3);

        // Post encode loop adjustment of Q prediction.
        av1_rc_update_rate_correction_factors(cpi, 0, cm.width, cm.height);

        // Update bit estimation ratio.
        if (cpi.oxcf.rc_cfg.mode == AOM_CBR &&
            cm.current_frame.frame_type != KEY_FRAME &&
            cpi.sf.hl_sf.accurate_bit_estimate != 0)
        {
            // [subset] bit_est_ratio: sf.hl_sf.accurate_bit_estimate is 0 at speed 11.
            throw new NotSupportedException("accurate_bit_estimate is out of the subset");
        }

        // Keep a record of last Q and ambient average Q.
        if (current_frame.frame_type == KEY_FRAME)
        {
            p_rc.last_q[KEY_FRAME] = qindex;
            p_rc.avg_frame_qindex[KEY_FRAME] =
                ROUND_POWER_OF_TWO(3 * p_rc.avg_frame_qindex[KEY_FRAME] + qindex, 2);
            if (cpi.svc.spatial_layer_id == 0)
            {
                rc.last_encoded_size_keyframe = rc.projected_frame_size;
                rc.last_target_size_keyframe = rc.this_frame_target;
            }
        }
        else
        {
            if ((cpi.ppi.use_svc != 0 && cpi.oxcf.rc_cfg.mode == AOM_CBR) ||
                cpi.rc.rtc_external_ratectrl != 0 ||
                (rc.is_src_frame_alt_ref == 0 &&
                 !(refresh_frame.golden_frame || is_intrnl_arf != 0 ||
                   refresh_frame.alt_ref_frame)))
            {
                p_rc.last_q[INTER_FRAME] = qindex;
                p_rc.avg_frame_qindex[INTER_FRAME] = ROUND_POWER_OF_TWO(
                    3 * p_rc.avg_frame_qindex[INTER_FRAME] + qindex, 2);
                p_rc.ni_frames++;
                p_rc.tot_q += av1_convert_qindex_to_q(qindex, cm.seq_params.bit_depth);
                p_rc.avg_q = p_rc.tot_q / p_rc.ni_frames;
                // Calculate the average Q for normal inter frames (not key or GFU
                // frames).
                rc.ni_tot_qi += qindex;
                rc.ni_av_qi = rc.ni_tot_qi / p_rc.ni_frames;
            }
        }
        // Keep record of last boosted (KF/GF/ARF) Q value.
        // If the current frame is coded at a lower Q then we also update it.
        // If all mbs in this group are skipped only update if the Q value is
        // better than that already stored.
        // This is used to help set quality in forced key frames to reduce popping
        if ((qindex < p_rc.last_boosted_qindex) ||
            (current_frame.frame_type == KEY_FRAME) ||
            (p_rc.constrained_gf_group == 0 &&
             (refresh_frame.alt_ref_frame || is_intrnl_arf != 0 ||
              (refresh_frame.golden_frame && rc.is_src_frame_alt_ref == 0))))
        {
            p_rc.last_boosted_qindex = qindex;
        }
        if (current_frame.frame_type == KEY_FRAME) p_rc.last_kf_qindex = qindex;

        update_buffer_level(cpi, rc.projected_frame_size);
        rc.prev_avg_frame_bandwidth = rc.avg_frame_bandwidth;

        // Rolling monitors of whether we are over or underspending used to help
        // regulate min and Max Q in two pass.
        if (av1_frame_scaled(cm) != 0)
            rc.this_frame_target = saturate_cast_double_to_int(
                rc.this_frame_target /
                resize_rate_factor(in cpi.oxcf.frm_dim_cfg, cm.width, cm.height));
        if (current_frame.frame_type != KEY_FRAME)
        {
            p_rc.rolling_target_bits = (int)ROUND_POWER_OF_TWO_64(
                (long)p_rc.rolling_target_bits * 3 + rc.this_frame_target, 2);
            p_rc.rolling_actual_bits = (int)ROUND_POWER_OF_TWO_64(
                (long)p_rc.rolling_actual_bits * 3 + rc.projected_frame_size, 2);
        }

        // Actual bits spent
        p_rc.total_actual_bits += rc.projected_frame_size;
        p_rc.total_target_bits += cm.show_frame != 0 ? rc.avg_frame_bandwidth : 0;

        if (is_altref_enabled(cpi.oxcf.gf_cfg.lag_in_frames,
                              cpi.oxcf.gf_cfg.enable_auto_arf) != 0 &&
            refresh_frame.alt_ref_frame &&
            (current_frame.frame_type != KEY_FRAME && frame_is_sframe(cm) == 0))
        {
            // Update the alternate reference frame stats as appropriate.
            // [subset] update_alt_ref_frame_stats: alt-ref needs lag_in_frames > 0.
            throw new NotSupportedException("alt-ref is out of the subset");
        }
        else
        {
            // Update the Golden frame stats as appropriate.
            update_golden_frame_stats(cpi);
        }

        // [subset] CONFIG_FPMT_TEST == 0.
        if (current_frame.frame_type == KEY_FRAME)
        {
            rc.frames_since_key = 0;
            rc.frames_since_scene_change = 0;
        }
        if (cpi.refresh_frame.golden_frame)
            rc.frame_num_last_gf_refresh = (int)current_frame.frame_number;

        if (rc.frame_source_sad < 10000)
            rc.last_frame_low_source_sad = rc.frame_number_encoded;

        rc.prev_coded_width = cm.width;
        rc.prev_coded_height = cm.height;
        rc.frame_number_encoded++;
        rc.prev_frame_is_dropped = 0;
        rc.drop_count_consec = 0;
    }

    // Not executed by the coverage runs, but reached whenever a frame is dropped
    // (av1_rc_drop_frame / av1_postencode_drop_cbr, data dependent).
    internal static void av1_rc_postencode_update_drop_frame(AV1_COMP cpi)
    {
        // Update buffer level with zero size, update frame counters, and return.
        update_buffer_level(cpi, 0);
        cpi.rc.rc_2_frame = 0;
        cpi.rc.rc_1_frame = 0;
        cpi.rc.prev_avg_frame_bandwidth = cpi.rc.avg_frame_bandwidth;
        cpi.rc.prev_coded_width = cpi.common.width;
        cpi.rc.prev_coded_height = cpi.common.height;
        cpi.rc.prev_frame_is_dropped = 1;
        // On a scene/slide change for dropped frame: reset the avg_source_sad to 0,
        // otherwise the avg_source_sad can get too large and subsequent frames
        // may miss the scene/slide detection.
        if (cpi.rc.high_source_sad != 0) cpi.rc.avg_source_sad = 0;
        if (cpi.ppi.use_svc != 0 && cpi.svc.number_spatial_layers > 1)
        {
            cpi.svc.last_layer_dropped[cpi.svc.spatial_layer_id] = true;
            cpi.svc.drop_spatial_layer[cpi.svc.spatial_layer_id] = true;
        }
        if (cpi.svc.spatial_layer_id == cpi.svc.number_spatial_layers - 1)
        {
            cpi.svc.prev_number_spatial_layers = cpi.svc.number_spatial_layers;
        }
        cpi.svc.prev_number_temporal_layers = cpi.svc.number_temporal_layers;
    }

    internal static int av1_find_qindex(double desired_q, aom_bit_depth_t bit_depth,
                                        int best_qindex, int worst_qindex)
    {
        Debug.Assert(best_qindex <= worst_qindex);
        int low = best_qindex;
        int high = worst_qindex;
        while (low < high)
        {
            int mid = (low + high) >> 1;
            double mid_q = av1_convert_qindex_to_q(mid, bit_depth);
            if (mid_q < desired_q)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        Debug.Assert(low == high);
        Debug.Assert(av1_convert_qindex_to_q(low, bit_depth) >= desired_q ||
                     low == worst_qindex);
        return low;
    }

    internal static int av1_compute_qdelta(RATE_CONTROL rc, double qstart, double qtarget,
                                           aom_bit_depth_t bit_depth)
    {
        int start_index =
            av1_find_qindex(qstart, bit_depth, rc.best_quality, rc.worst_quality);
        int target_index =
            av1_find_qindex(qtarget, bit_depth, rc.best_quality, rc.worst_quality);
        return target_index - start_index;
    }

    // Find q_index for the desired_bits_per_mb, within [best_qindex, worst_qindex],
    // assuming 'correction_factor' is 1.0.
    // To be precise, 'q_index' is the smallest integer, for which the corresponding
    // bits per mb <= desired_bits_per_mb.
    // If no such q index is found, returns 'worst_qindex'.
    private static int find_qindex_by_rate(AV1_COMP cpi,
                                           int desired_bits_per_mb, FRAME_TYPE frame_type,
                                           int best_qindex, int worst_qindex)
    {
        Debug.Assert(best_qindex <= worst_qindex);
        int low = best_qindex;
        int high = worst_qindex;
        while (low < high)
        {
            int mid = (low + high) >> 1;
            int mid_bits_per_mb =
                av1_rc_bits_per_mb(cpi, frame_type, mid, 1.0, 0);
            if (mid_bits_per_mb > desired_bits_per_mb)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        Debug.Assert(low == high);
        Debug.Assert(av1_rc_bits_per_mb(cpi, frame_type, low, 1.0, 0) <=
                         desired_bits_per_mb ||
                     low == worst_qindex);
        return low;
    }

    internal static int av1_compute_qdelta_by_rate(AV1_COMP cpi, FRAME_TYPE frame_type,
                                                   int qindex, double rate_target_ratio)
    {
        RATE_CONTROL rc = cpi.rc;

        // Look up the current projected bits per block for the base index
        int base_bits_per_mb =
            av1_rc_bits_per_mb(cpi, frame_type, qindex, 1.0, 0);

        // Find the target bits per mb based on the base value and given ratio.
        int target_bits_per_mb = (int)(rate_target_ratio * base_bits_per_mb);

        int target_index = find_qindex_by_rate(
            cpi, target_bits_per_mb, frame_type, rc.best_quality, rc.worst_quality);
        return target_index - qindex;
    }

    private static void set_gf_interval_range(AV1_COMP cpi, RATE_CONTROL rc)
    {
        AV1EncoderConfig oxcf = cpi.oxcf;
        // Set Maximum gf/arf interval
        rc.max_gf_interval = oxcf.gf_cfg.max_gf_interval;
        rc.min_gf_interval = oxcf.gf_cfg.min_gf_interval;
        if (rc.min_gf_interval == 0)
            rc.min_gf_interval = av1_rc_get_default_min_gf_interval(
                oxcf.frm_dim_cfg.width, oxcf.frm_dim_cfg.height, cpi.framerate);
        if (rc.max_gf_interval == 0)
            rc.max_gf_interval =
                get_default_max_gf_interval(cpi.framerate, rc.min_gf_interval);
        /*
         * Extended max interval for genuinely static scenes like slide shows.
         * The no.of.stats available in the case of LAP is limited,
         * hence setting to max_gf_interval.
         */
        if (cpi.ppi.lap_enabled != 0)
            rc.static_scene_max_gf_interval = rc.max_gf_interval + 1;
        else
            rc.static_scene_max_gf_interval = MAX_STATIC_GF_GROUP_LENGTH;

        if (rc.max_gf_interval > rc.static_scene_max_gf_interval)
            rc.max_gf_interval = rc.static_scene_max_gf_interval;

        // Clamp min to max
        rc.min_gf_interval = AOMMIN(rc.min_gf_interval, rc.max_gf_interval);
    }

    internal static void av1_rc_update_framerate(AV1_COMP cpi, int width, int height)
    {
        AV1EncoderConfig oxcf = cpi.oxcf;
        RATE_CONTROL rc = cpi.rc;
        int MBs = av1_get_MBs(width, height);

        rc.avg_frame_bandwidth = saturate_cast_double_to_int(
            Round(oxcf.rc_cfg.target_bandwidth / cpi.framerate, MidpointRounding.AwayFromZero));

        long vbr_min_bits =
            (long)rc.avg_frame_bandwidth * oxcf.rc_cfg.vbrmin_section / 100;
        vbr_min_bits = AOMMIN(vbr_min_bits, int.MaxValue);

        rc.min_frame_bandwidth = AOMMAX((int)vbr_min_bits, FRAME_OVERHEAD_BITS);

        // A maximum bitrate for a frame is defined.
        // The baseline for this aligns with HW implementations that
        // can support decode of 1080P content up to a bitrate of MAX_MB_RATE bits
        // per 16x16 MB (averaged over a frame). However this limit is extended if
        // a very high rate is given on the command line or the rate cannot
        // be achieved because of a user specified max q (e.g. when the user
        // specifies lossless encode.
        long vbr_max_bits =
            (long)rc.avg_frame_bandwidth * oxcf.rc_cfg.vbrmax_section / 100;
        vbr_max_bits = AOMMIN(vbr_max_bits, int.MaxValue);

        rc.max_frame_bandwidth =
            AOMMAX(AOMMAX((MBs * MAX_MB_RATE), MAXRATE_1080P), (int)vbr_max_bits);

        set_gf_interval_range(cpi, rc);
    }

    // [subset] VBR_PCT_ADJUSTMENT_LIMIT / vbr_rate_correction / av1_set_target_rate /
    // av1_calc_pframe_target_size_one_pass_vbr / av1_calc_iframe_target_size_one_pass_vbr:
    // VBR / two-pass only.

    internal static int av1_calc_pframe_target_size_one_pass_cbr(
        AV1_COMP cpi, FRAME_UPDATE_TYPE frame_update_type)
    {
        AV1EncoderConfig oxcf = cpi.oxcf;
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        ref readonly RateControlCfg rc_cfg = ref oxcf.rc_cfg;
        ref readonly RefreshFrameInfo refresh_frame = ref cpi.refresh_frame;
        long diff = p_rc.optimal_buffer_level - p_rc.buffer_level;
        // For refresh alt or golden: keep diff negative to force setting
        // higher target bandwidth on these frames.
        // Only realtime mode with lookahead.
        if (is_one_pass_rt_lag_params(cpi) != 0 &&
            (refresh_frame.alt_ref_frame || refresh_frame.golden_frame))
            diff = AOMMIN(diff, -p_rc.optimal_buffer_level);
        long one_pct_bits = 1 + p_rc.optimal_buffer_level / 100;
        int min_frame_target =
            AOMMAX(rc.avg_frame_bandwidth >> 4, FRAME_OVERHEAD_BITS);
        long target;

        if (rc_cfg.gf_cbr_boost_pct != 0)
        {
            int af_ratio_pct = (int)rc_cfg.gf_cbr_boost_pct + 100;
            if (frame_update_type == GF_UPDATE || frame_update_type == OVERLAY_UPDATE)
            {
                target = ((long)rc.avg_frame_bandwidth * p_rc.baseline_gf_interval *
                          af_ratio_pct) /
                         (p_rc.baseline_gf_interval * 100 + af_ratio_pct - 100);
            }
            else
            {
                target = ((long)rc.avg_frame_bandwidth * p_rc.baseline_gf_interval *
                          100) /
                         (p_rc.baseline_gf_interval * 100 + af_ratio_pct - 100);
            }
        }
        else
        {
            target = rc.avg_frame_bandwidth;
        }
        if (cpi.ppi.use_svc != 0)
        {
            // [subset] layer average frame size: SVC is out of the subset.
            throw new NotSupportedException("SVC is out of the subset");
        }
        if (diff > 0)
        {
            // Lower the target bandwidth for this frame.
            int pct_low =
                (int)AOMMIN(diff / one_pct_bits, rc_cfg.under_shoot_pct);
            target -= (target * pct_low) / 200;
        }
        else if (diff < 0)
        {
            // Increase the target bandwidth for this frame.
            int pct_high =
                (int)AOMMIN(-diff / one_pct_bits, rc_cfg.over_shoot_pct);
            target += (target * pct_high) / 200;
        }
        if (rc_cfg.max_inter_bitrate_pct != 0)
        {
            long max_rate =
                (long)rc.avg_frame_bandwidth * rc_cfg.max_inter_bitrate_pct / 100;
            target = AOMMIN(target, max_rate);
        }
        if (target > int.MaxValue) target = int.MaxValue;
        return AOMMAX(min_frame_target, (int)target);
    }

    internal static int av1_calc_iframe_target_size_one_pass_cbr(AV1_COMP cpi)
    {
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        long target;
        if (cpi.common.current_frame.frame_number == 0)
        {
            target = ((p_rc.starting_buffer_level / 2) > int.MaxValue)
                         ? int.MaxValue
                         : (int)(p_rc.starting_buffer_level / 2);
            if (cpi.svc.number_temporal_layers > 1 && target < (int.MaxValue >> 2))
            {
                target = target << AOMMIN(2, (cpi.svc.number_temporal_layers - 1));
            }
        }
        else
        {
            int kf_boost = 32;
            double framerate = cpi.framerate;

            kf_boost = AOMMAX(kf_boost, (int)Round(2 * framerate - 16, MidpointRounding.AwayFromZero));
            if (rc.frames_since_key < framerate / 2)
            {
                kf_boost = (int)(kf_boost * rc.frames_since_key / (framerate / 2));
            }
            target = ((long)(16 + kf_boost) * rc.avg_frame_bandwidth) >> 4;
        }
        return clamp_iframe_target_size(cpi, target);
    }

    private static readonly int[] gf_length_mult = { 8, 4 };

    private static void set_golden_update(AV1_COMP cpi)
    {
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        int divisor = 10;
        if (cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ)
            divisor = cpi.cyclic_refresh.percent_refresh;

        // Set minimum gf_interval for GF update to a multiple of the refresh period,
        // with some max limit. Depending on past encoding stats, GF flag may be
        // reset and update may not occur until next baseline_gf_interval.
        if (divisor > 0)
            p_rc.baseline_gf_interval =
                AOMMIN(gf_length_mult[cpi.sf.rt_sf.gf_length_lvl] * (100 / divisor),
                       MAX_GF_INTERVAL_RT);
        else
            p_rc.baseline_gf_interval = FIXED_GF_INTERVAL_RT;
        if (rc.avg_frame_low_motion != 0 && rc.avg_frame_low_motion < 40)
            p_rc.baseline_gf_interval = 16;
    }

    private static void set_baseline_gf_interval(AV1_COMP cpi, FRAME_TYPE frame_type)
    {
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        GF_GROUP gf_group = cpi.ppi.gf_group;

        set_golden_update(cpi);

        if (p_rc.baseline_gf_interval > rc.frames_to_key &&
            cpi.oxcf.kf_cfg.auto_key)
            p_rc.baseline_gf_interval = rc.frames_to_key;
        p_rc.gfu_boost = DEFAULT_GF_BOOST_RT;
        p_rc.constrained_gf_group =
            (p_rc.baseline_gf_interval >= rc.frames_to_key &&
             cpi.oxcf.kf_cfg.auto_key)
                ? 1
                : 0;
        rc.frames_till_gf_update_due = p_rc.baseline_gf_interval;
        cpi.gf_frame_index = 0;
        // SVC does not use GF as periodic boost.
        // TODO(marpan): Find better way to disable this for SVC.
        if (cpi.ppi.use_svc != 0)
        {
            // [subset] per-layer GF settings: SVC is out of the subset.
            throw new NotSupportedException("SVC is out of the subset");
        }
        gf_group.size = p_rc.baseline_gf_interval;
        gf_group.update_type[0] = (frame_type == KEY_FRAME) ? KF_UPDATE : GF_UPDATE;
        gf_group.refbuf_state[cpi.gf_frame_index] =
            (frame_type == KEY_FRAME) ? REFBUF_RESET : REFBUF_UPDATE;
    }

    internal static void av1_adjust_gf_refresh_qp_one_pass_rt(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        RATE_CONTROL rc = cpi.rc;
        RTC_REF rtc_ref = cpi.ppi.rtc_ref;
        int resize_pending = is_frame_resize_pending(cpi);
        if (resize_pending == 0 && rc.high_source_sad == 0)
        {
            // Check if we should disable GF refresh (if period is up),
            // or force a GF refresh update (if we are at least halfway through
            // period) based on QP. Look into add info on segment deltaq.
            PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
            int avg_qp = p_rc.avg_frame_qindex[INTER_FRAME];
            int allow_gf_update =
                B(rc.frames_till_gf_update_due <= (p_rc.baseline_gf_interval - 10));
            int gf_update_changed = 0;
            int thresh = 87;
            // C: (unsigned int frame_number - int) is computed in unsigned.
            if ((cm.current_frame.frame_number - (uint)cpi.rc.frame_num_last_gf_refresh) <
                    FIXED_GF_INTERVAL_RT &&
                rc.frames_till_gf_update_due == 1 &&
                cm.quant_params.base_qindex > avg_qp)
            {
                // Disable GF refresh since QP is above the running average QP.
                rtc_ref.refresh[rtc_ref.gld_idx_1layer] = 0;
                gf_update_changed = 1;
                cpi.refresh_frame.golden_frame = false;
            }
            else if (allow_gf_update != 0 &&
                     ((cm.quant_params.base_qindex < thresh * avg_qp / 100) ||
                      (rc.avg_frame_low_motion != 0 && rc.avg_frame_low_motion < 20)))
            {
                // Force refresh since QP is well below average QP or this is a high
                // motion frame.
                rtc_ref.refresh[rtc_ref.gld_idx_1layer] = 1;
                gf_update_changed = 1;
                cpi.refresh_frame.golden_frame = true;
            }
            if (gf_update_changed != 0)
            {
                set_baseline_gf_interval(cpi, INTER_FRAME);
                int refresh_mask = 0;
                for (uint i = 0; i < INTER_REFS_PER_FRAME; i++)
                {
                    int ref_frame_map_idx = rtc_ref.ref_idx[i];
                    refresh_mask |= rtc_ref.refresh[ref_frame_map_idx]
                                    << ref_frame_map_idx;
                }
                cm.current_frame.refresh_frame_flags = refresh_mask;
            }
        }
    }

    private static readonly ulong[][] th_frame_sad = {
        new ulong[] { 18000, 18000, 18000 },  // HDRES CPU 9
        new ulong[] { 25000, 25000, 25000 },  // MIDRES CPU 9
        new ulong[] { 40000, 30000, 20000 },  // HDRES CPU 10
        new ulong[] { 30000, 25000, 20000 }   // MIDRES CPU 10
    };

    /// <summary>Setup the reference prediction structure for 1 pass real-time</summary>
    /// <remarks>
    /// Set the reference prediction structure for 1 layer.
    /// Current structure is to use 3 references (LAST, GOLDEN, ALTREF),
    /// where ALT_REF always behind current by lag_alt frames, and GOLDEN is
    /// either updated on LAST with period baseline_gf_interval (fixed slot)
    /// or always behind current by lag_gld (gld_fixed_slot = 0, lag_gld &lt;= 7).
    /// The settings for the prediction structure are set in cpi->ext_flags; and the
    /// buffer slot index (for each of 7 references) and refresh flags (for each of the
    /// 8 slots) are set in cpi->ppi->rtc_ref.ref_idx[] and rtc_ref.refresh[].
    /// </remarks>
    internal static void av1_set_rtc_reference_structure_one_layer(AV1_COMP cpi, int gf_update)
    {
        AV1_COMMON cm = cpi.common;
        ExternalFlags ext_flags = cpi.ext_flags;
        RATE_CONTROL rc = cpi.rc;
        ref ExtRefreshFrameFlagsInfo ext_refresh_frame_flags =
            ref ext_flags.refresh_frame;
        RTC_REF rtc_ref = cpi.ppi.rtc_ref;
        uint frame_number = (cpi.oxcf.rc_cfg.drop_frames_water_mark != 0)
                                ? rc.frame_number_encoded
                                : cm.current_frame.frame_number;
        uint lag_alt = 4;
        int last_idx = 0;
        int last_idx_refresh = 0;
        int gld_idx = 0;
        int alt_ref_idx = 0;
        int last2_idx = 0;
        ext_refresh_frame_flags.update_pending = true;
        ext_flags.ref_frame_flags = 0;
        ext_refresh_frame_flags.last_frame = true;
        ext_refresh_frame_flags.golden_frame = false;
        ext_refresh_frame_flags.alt_ref_frame = false;
        // Decide altref lag adaptively for rt
        if (cpi.sf.rt_sf.sad_based_adp_altref_lag != 0)
        {
            lag_alt = 6;
            int th_idx = cpi.sf.rt_sf.sad_based_adp_altref_lag - 1;
            Debug.Assert(th_idx < 4);
            if (rc.avg_source_sad > th_frame_sad[th_idx][0])
                lag_alt = 3;
            else if (rc.avg_source_sad > th_frame_sad[th_idx][1])
                lag_alt = 4;
            else if (rc.avg_source_sad > th_frame_sad[th_idx][2])
                lag_alt = 5;
        }
        // This defines the reference structure for 1 layer (non-svc) RTC encoding.
        // To avoid the internal/default reference structure for non-realtime
        // overwriting this behavior, we use the "svc" ref parameters from the
        // external control SET_SVC_REF_FRAME_CONFIG.
        // TODO(marpan): rename that control and the related internal parameters
        // to rtc_ref.
        for (int i = 0; i < INTER_REFS_PER_FRAME; ++i) rtc_ref.ref_idx[i] = 7;
        for (int i = 0; i < REF_FRAMES; ++i) rtc_ref.refresh[i] = 0;
        // Set the reference frame flags.
        ext_flags.ref_frame_flags ^= AOM_LAST_FLAG;
        if (cpi.sf.rt_sf.force_only_last_ref == 0)
        {
            ext_flags.ref_frame_flags ^= AOM_ALT_FLAG;
            ext_flags.ref_frame_flags ^= AOM_GOLD_FLAG;
            if (cpi.sf.rt_sf.ref_frame_comp_nonrd[1] != 0)
                ext_flags.ref_frame_flags ^= AOM_LAST2_FLAG;
        }
        const int sh = 6;
        // Moving index slot for last: 0 - (sh - 1).
        if (frame_number > 1) last_idx = (int)((frame_number - 1) % sh);
        // Moving index for refresh of last: one ahead for next frame.
        last_idx_refresh = (int)(frame_number % sh);
        gld_idx = 6;

        // Moving index for alt_ref, lag behind LAST by lag_alt frames.
        if (frame_number > lag_alt) alt_ref_idx = (int)((frame_number - lag_alt) % sh);
        if (cpi.sf.rt_sf.ref_frame_comp_nonrd[1] != 0)
        {
            // Moving index for LAST2, lag behind LAST by 2 frames.
            if (frame_number > 2) last2_idx = (int)((frame_number - 2) % sh);
        }
        rtc_ref.ref_idx[0] = last_idx;          // LAST
        rtc_ref.ref_idx[1] = last_idx_refresh;  // LAST2 (for refresh of last).
        if (cpi.sf.rt_sf.ref_frame_comp_nonrd[1] != 0)
        {
            rtc_ref.ref_idx[1] = last2_idx;         // LAST2
            rtc_ref.ref_idx[2] = last_idx_refresh;  // LAST3 (for refresh of last).
        }
        rtc_ref.ref_idx[3] = gld_idx;      // GOLDEN
        rtc_ref.ref_idx[6] = alt_ref_idx;  // ALT_REF
        // Refresh this slot, which will become LAST on next frame.
        rtc_ref.refresh[last_idx_refresh] = 1;
        // Update GOLDEN on period for fixed slot case.
        if (gf_update != 0 && cm.current_frame.frame_type != KEY_FRAME)
        {
            ext_refresh_frame_flags.golden_frame = true;
            rtc_ref.refresh[gld_idx] = 1;
        }
        rtc_ref.gld_idx_1layer = gld_idx;
        // Set the flag to reduce the number of reference frame buffers used.
        // This assumes that slot 7 is never used.
        cpi.rt_reduce_num_ref_buffers = 1;
        cpi.rt_reduce_num_ref_buffers &= B(rtc_ref.ref_idx[0] < 7);
        cpi.rt_reduce_num_ref_buffers &= B(rtc_ref.ref_idx[1] < 7);
        cpi.rt_reduce_num_ref_buffers &= B(rtc_ref.ref_idx[3] < 7);
        cpi.rt_reduce_num_ref_buffers &= B(rtc_ref.ref_idx[6] < 7);
        if (cpi.sf.rt_sf.ref_frame_comp_nonrd[1] != 0)
            cpi.rt_reduce_num_ref_buffers &= B(rtc_ref.ref_idx[2] < 7);
    }

    // [subset] set_block_is_active: only with active maps (AOME_SET_ACTIVEMAP), never set.
    // [subset] estimate_scroll_motion: screen content only (high_motion_content_screen_rtc).

    internal static void av1_rc_scene_detection_onepass_rt(AV1_COMP cpi,
                                                           EncodeFrameInput frame_input)
    {
        AV1_COMMON cm = cpi.common;
        RATE_CONTROL rc = cpi.rc;
        YV12_BUFFER_CONFIG unscaled_src = frame_input.source;
        YV12_BUFFER_CONFIG unscaled_last_src = frame_input.last_source;
        Ptr<byte> src_y;
        int src_ystride;
        int src_width;
        int src_height;
        Ptr<byte> last_src_y;
        int last_src_ystride;
        int last_src_width;
        int last_src_height;
        int width = cm.width;
        int height = cm.height;
        if (cpi.svc.number_spatial_layers > 1)
        {
            width = cpi.oxcf.frm_dim_cfg.width;
            height = cpi.oxcf.frm_dim_cfg.height;
        }
        // Set src_sad_blk_64x64 to NULL also for number_spatial layers > 1, as
        // it is never allocated for number_spatial_layers > 1 (see the condition
        // under which we allocate cpi->src_sad_blk_64x64 later in this function).
        // This is guard against the case where the number_spatial_layers
        // is changed dynamically without re-alloc of encoder.
        if (width != cm.render_width || height != cm.render_height ||
            cpi.svc.number_spatial_layers > 1 || unscaled_src == null ||
            unscaled_last_src == null || is_one_pass_rt_lag_params(cpi) != 0)
        {
            cpi.src_sad_blk_64x64 = Ptr<ulong>.Null;
            cpi.src_sad_blk_alloc_size = 0;
        }
        if (unscaled_src == null || unscaled_last_src == null) return;
        src_y = unscaled_src.y_buffer;
        src_ystride = unscaled_src.y_stride;
        src_width = unscaled_src.y_width;
        src_height = unscaled_src.y_height;
        last_src_y = unscaled_last_src.y_buffer;
        last_src_ystride = unscaled_last_src.y_stride;
        last_src_width = unscaled_last_src.y_width;
        last_src_height = unscaled_last_src.y_height;
        if (src_width != last_src_width || src_height != last_src_height)
        {
            cpi.src_sad_blk_64x64 = Ptr<ulong>.Null;
            cpi.src_sad_blk_alloc_size = 0;
            return;
        }
        rc.high_source_sad = 0;
        rc.percent_blocks_with_motion = 0;
        rc.max_block_source_sad = 0;
        rc.prev_avg_source_sad = rc.avg_source_sad;
        int num_mi_cols = cm.mi_params.mi_cols;
        int num_mi_rows = cm.mi_params.mi_rows;
        if (cpi.svc.number_spatial_layers > 1)
        {
            num_mi_cols = size_in_mi(src_width);
            num_mi_rows = size_in_mi(src_height);
        }
        int num_zero_temp_sad = 0;
        uint min_thresh =
            (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN) ? 8000u : 10000u;
        if (cpi.sf.rt_sf.higher_thresh_scene_detection != 0)
        {
            min_thresh = cm.width * cm.height <= 320 * 240 && cpi.framerate < 10.0
                             ? 50000u
                             : 100000u;
        }
        const BLOCK_SIZE bsize = BLOCK_64X64;
        // Loop over sub-sample of frame, compute average sad over 64x64 blocks.
        ulong avg_sad = 0;
        ulong tmp_sad = 0;
        int num_samples = 0;
        int thresh =
            ((cm.width * cm.height <= 320 * 240 && cpi.framerate < 10.0) ||
             (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN))
                ? 5
                : 6;
        // SAD is computed on 64x64 blocks
        int sb_size_by_mb = (cm.seq_params.sb_size == BLOCK_128X128)
                                ? (cm.seq_params.mib_size >> 1)
                                : cm.seq_params.mib_size;
        int sb_cols = (num_mi_cols + sb_size_by_mb - 1) / sb_size_by_mb;
        int sb_rows = (num_mi_rows + sb_size_by_mb - 1) / sb_size_by_mb;
        // [subset] check_light_change is the constant 0: sum_sq_thresh,
        // num_low_var_high_sumdiff and the variance based light change test are dropped.
        int light_change = 0;
        // TODO(marpan): There seems some difference along the bottom border when
        // using the source_last_tl0 for last_source (used for temporal layers or
        // when previous frame is dropped).
        // Remove this border parameter when issue is resolved: difference is that
        // non-zero sad exists along bottom border even though source is static.
        int border =
            B(rc.prev_frame_is_dropped != 0 || cpi.svc.number_temporal_layers > 1);
        // Store blkwise SAD for later use. Disable for spatial layers for now.
        if (width == cm.render_width && height == cm.render_height &&
            cpi.svc.number_spatial_layers == 1 && is_one_pass_rt_lag_params(cpi) == 0)
        {
            if (cpi.src_sad_blk_alloc_size != sb_cols * sb_rows)
            {
                cpi.src_sad_blk_64x64 = new ulong[sb_cols * sb_rows];
                cpi.src_sad_blk_alloc_size = sb_cols * sb_rows;
            }
        }
        if (cpi.active_map.enabled != 0 && rc.percent_blocks_inactive > 0)
        {
            // [subset] set_block_is_active: active maps are never set.
            throw new NotSupportedException("active maps are out of the subset");
        }
        // Avoid bottom and right border.
        // [port] The per-block SADs are independent and only summed, counted and maxed, so
        // the block rows are spread over the encoder's thread budget (bit-exact with the C
        // loop; the frame-wide pass is memory bound and on the critical path).
        int scan_rows = sb_rows - border;
        aom_sad_fn_t sdf = cpi.ppi.fn_ptr[bsize].sdf;
        Ptr<ulong> blk_sad = cpi.src_sad_blk_64x64;
        Ptr<byte> src_y0 = src_y, last_src_y0 = last_src_y;
        void scan_row(int sbi_row, ref ulong row_sad, ref int row_zero, ref ulong row_max)
        {
            Ptr<byte> s = src_y0 + (sbi_row << 6) * src_ystride;
            Ptr<byte> l = last_src_y0 + (sbi_row << 6) * last_src_ystride;
            for (int sbi_col = 0; sbi_col < sb_cols; ++sbi_col)
            {
                ulong sad = sdf(s, src_ystride, l, last_src_ystride);
                if (!blk_sad.IsNull) blk_sad[sbi_col + sbi_row * sb_cols] = sad;
                row_sad += sad;
                if (sad == 0) row_zero++;
                if (sad > row_max) row_max = sad;
                s += 64;
                l += 64;
            }
        }
        int scan_threads = AOMMIN(cpi.oxcf.max_threads, scan_rows >> 2);
        if (scan_threads > 1)
        {
            var row_sads = new ulong[scan_rows];
            var row_zeros = new int[scan_rows];
            var row_maxes = new ulong[scan_rows];
            System.Threading.Tasks.Parallel.For(0, scan_rows,
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = scan_threads },
                r => scan_row(r, ref row_sads[r], ref row_zeros[r], ref row_maxes[r]));
            for (int r = 0; r < scan_rows; ++r)
            {
                avg_sad += row_sads[r];
                num_zero_temp_sad += row_zeros[r];
                if (row_maxes[r] > rc.max_block_source_sad) rc.max_block_source_sad = row_maxes[r];
            }
        }
        else
        {
            ulong row_max = rc.max_block_source_sad;
            for (int r = 0; r < scan_rows; ++r) scan_row(r, ref avg_sad, ref num_zero_temp_sad, ref row_max);
            rc.max_block_source_sad = row_max;
        }
        num_samples = scan_rows * sb_cols;
        _ = tmp_sad;
        if (num_samples > 0) avg_sad = avg_sad / (ulong)num_samples;
        // Set high_source_sad flag if we detect very high increase in avg_sad
        // between current and previous frame value(s). Use minimum threshold
        // for cases where there is small change from content that is completely
        // static.
        int thresh_zero_sad_samples =
            avg_sad > 8 * min_thresh ? 3 * (num_samples >> 2) : num_samples >> 1;
        if (light_change == 0 &&
            avg_sad >
                AOMMAX(min_thresh, (uint)(rc.avg_source_sad * (ulong)thresh)) &&
            rc.frames_since_key > 1 + cpi.svc.number_spatial_layers &&
            num_zero_temp_sad < thresh_zero_sad_samples)
            rc.high_source_sad = 1;
        else
            rc.high_source_sad = 0;
        rc.avg_source_sad = (3 * rc.avg_source_sad + avg_sad) >> 2;
        rc.frame_source_sad = avg_sad;
        if (num_samples > 0)
            rc.percent_blocks_with_motion =
                ((num_samples - num_zero_temp_sad) * 100) / num_samples;
        if (rc.frame_source_sad > 0) rc.static_since_last_scene_change = 0;
        if (rc.high_source_sad != 0)
        {
            cpi.rc.frames_since_scene_change = 0;
            rc.static_since_last_scene_change = 1;
        }
        // Update the high_motion_content_screen_rtc flag on TL0. Avoid the update
        // if too many consecutive frame drops occurred.
        // The threshold_high_motion is kept to a large value, to account for
        // mis-detection for scroll for scaled input (where scroll motion can be
        // subpel and not detected below). The threshold may be improved when better
        // scroll detection (for subpel) is added.
        if (cpi.svc.temporal_layer_id == 0 && rc.drop_count_consec < 3)
        {
            cpi.rc.high_motion_content_screen_rtc = 0;
            if (cpi.oxcf.speed >= 11 && is_one_pass_rt_lag_params(cpi) == 0 &&
                cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN)
            {
                // [subset] screen-content scroll / high motion detection
                // (estimate_scroll_motion): tune_content screen is out of the subset.
                throw new NotSupportedException("screen content is out of the subset");
            }
            // Pass the flag value to all layer frames.
            if (cpi.svc.number_spatial_layers > 1 ||
                cpi.svc.number_temporal_layers > 1)
            {
                // [subset] layer contexts: SVC is out of the subset.
                throw new NotSupportedException("SVC is out of the subset");
            }
        }
        // Scene detection is only on base SLO, and using full/original resolution.
        // Pass the state to the upper spatial layers.
        if (cpi.svc.number_spatial_layers > 1)
        {
            // [subset] layer contexts: SVC is out of the subset.
            throw new NotSupportedException("SVC is out of the subset");
        }
    }

    // [subset] AV1_VAR_OFFS / rc_spatial_act_onepass_rt: only with
    // sf.rt_sf.rc_compute_spatial_var_sc_kf (screen content).

    /// <summary>Set the GF baseline interval for 1 pass real-time mode.</summary>
    /// <returns>Return GF update flag, and update the cpi->rc with
    /// the next GF interval settings.</returns>
    private static int set_gf_interval_update_onepass_rt(AV1_COMP cpi,
                                                         FRAME_TYPE frame_type)
    {
        RATE_CONTROL rc = cpi.rc;
        int gf_update = 0;
        int resize_pending = is_frame_resize_pending(cpi);
        // GF update based on frames_till_gf_update_due, also
        // force update on resize pending frame or for scene change.
        if ((resize_pending != 0 || rc.high_source_sad != 0 ||
             rc.frames_till_gf_update_due == 0) &&
            cpi.svc.temporal_layer_id == 0 && cpi.svc.spatial_layer_id == 0)
        {
            set_baseline_gf_interval(cpi, frame_type);
            gf_update = 1;
        }
        return gf_update;
    }

    // [subset] resize_reset_rc / dynamic_resize_one_pass_cbr: resize (RESIZE_DYNAMIC or an
    // external resize request) is out of the subset.

    private static int set_key_frame(AV1_COMP cpi, uint frame_flags)
    {
        RATE_CONTROL rc = cpi.rc;
        AV1_COMMON cm = cpi.common;

        // Very first frame has to be key frame.
        if (cm.current_frame.frame_number == 0) return 1;
        // Set key frame if forced by frame flags.
        if ((frame_flags & FRAMEFLAGS_KEY) != 0) return 1;
        if (cpi.ppi.use_svc == 0)
        {
            // Non-SVC
            if (cpi.oxcf.kf_cfg.auto_key && rc.frames_to_key == 0) return 1;
        }
        else
        {
            // [subset] SVC key frame period: SVC is out of the subset.
            throw new NotSupportedException("SVC is out of the subset");
        }

        return 0;
    }

    // Set to true if this frame is a recovery frame, for 1 layer RPS,
    // and whether we should apply some boost (QP, adjust speed features, etc).
    // Recovery frame here means frame whose closest reference is x frames away,
    // where x = 4.
    // TODO(marpan): Consider adding on/off flag to SVC_REF_FRAME_CONFIG to
    // allow more control for applications.
    private static bool set_flag_rps_bias_recovery_frame(AV1_COMP cpi)
    {
        if (cpi.ppi.rtc_ref.set_ref_frame_config != 0 &&
            cpi.svc.number_temporal_layers == 1 &&
            cpi.svc.number_spatial_layers == 1 &&
            cpi.ppi.rtc_ref.reference_was_previous_frame)
        {
            // [subset] av1_svc_get_min_ref_dist: set_ref_frame_config
            // (AV1E_SET_SVC_REF_FRAME_CONFIG) is never used.
            throw new NotSupportedException("set_ref_frame_config is out of the subset");
        }
        return false;
    }

    internal static void av1_get_one_pass_rt_params(AV1_COMP cpi, ref FRAME_TYPE frame_type,
                                                    EncodeFrameInput frame_input,
                                                    uint frame_flags)
    {
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        AV1_COMMON cm = cpi.common;
        GF_GROUP gf_group = cpi.ppi.gf_group;
        SVC svc = cpi.svc;
        int target;
        if (cpi.oxcf.rc_cfg.max_consec_drop_ms > 0)
        {
            double framerate =
                cpi.framerate > 1 ? Round(cpi.framerate, MidpointRounding.AwayFromZero)
                                  : cpi.framerate;
            rc.max_consec_drop = saturate_cast_double_to_int(
                Ceiling(cpi.oxcf.rc_cfg.max_consec_drop_ms * framerate / 1000));
        }
        if (cpi.ppi.use_svc != 0)
        {
            // [subset] av1_update_temporal_layer_framerate / av1_restore_layer_context:
            // SVC is out of the subset.
            throw new NotSupportedException("SVC is out of the subset");
        }
        cpi.ppi.rtc_ref.bias_recovery_frame = set_flag_rps_bias_recovery_frame(cpi);
        // Set frame type.
        if (set_key_frame(cpi, frame_flags) != 0)
        {
            frame_type = KEY_FRAME;
            p_rc.this_key_frame_forced =
                B(cm.current_frame.frame_number != 0 && rc.frames_to_key == 0);
            rc.frames_to_key = cpi.oxcf.kf_cfg.key_freq_max;
            p_rc.kf_boost = DEFAULT_KF_BOOST_RT;
            gf_group.update_type[cpi.gf_frame_index] = KF_UPDATE;
            gf_group.frame_type[cpi.gf_frame_index] = KEY_FRAME;
            gf_group.refbuf_state[cpi.gf_frame_index] = REFBUF_RESET;
            // [subset] SVC layer reset (use_svc == 0, checked above).
            rc.frame_number_encoded = 0;
            cpi.ppi.rtc_ref.non_reference_frame = 0;
            rc.static_since_last_scene_change = 0;
        }
        else
        {
            frame_type = INTER_FRAME;
            gf_group.update_type[cpi.gf_frame_index] = LF_UPDATE;
            gf_group.frame_type[cpi.gf_frame_index] = INTER_FRAME;
            gf_group.refbuf_state[cpi.gf_frame_index] = REFBUF_UPDATE;
            // [subset] SVC layer key frame flag (use_svc == 0, checked above).
            // If the user is setting the reference structure with
            // set_ref_frame_config and did not set any references, set the
            // frame type to Intra-only.
            if (cpi.ppi.rtc_ref.set_ref_frame_config != 0)
            {
                int no_references_set = 1;
                for (int i = 0; i < INTER_REFS_PER_FRAME; i++)
                {
                    if (cpi.ppi.rtc_ref.reference[i] != 0)
                    {
                        no_references_set = 0;
                        break;
                    }
                }

                // Set to intra_only_frame if no references are set.
                // The stream can start decoding on INTRA_ONLY_FRAME so long as the
                // layer with the intra_only_frame doesn't signal a reference to a slot
                // that hasn't been set yet.
                if (no_references_set != 0) frame_type = INTRA_ONLY_FRAME;
            }
        }
        if (cpi.active_map.enabled != 0 && cpi.rc.percent_blocks_inactive == 100)
        {
            rc.frame_source_sad = 0;
            rc.avg_source_sad = (3 * rc.avg_source_sad + rc.frame_source_sad) >> 2;
            rc.percent_blocks_with_motion = 0;
            rc.max_block_source_sad = 0;
            rc.high_source_sad = 0;
        }
        else if (cpi.sf.rt_sf.check_scene_detection != 0 &&
                 svc.spatial_layer_id == 0)
        {
            if (rc.prev_coded_width == cm.width &&
                rc.prev_coded_height == cm.height)
            {
                av1_rc_scene_detection_onepass_rt(cpi, frame_input);
            }
            else
            {
                cpi.src_sad_blk_64x64 = Ptr<ulong>.Null;
                cpi.src_sad_blk_alloc_size = 0;
            }
        }
        if (cpi.sf.rt_sf.rc_compute_spatial_var_sc_kf != 0 &&
            (frame_type == KEY_FRAME || rc.high_source_sad != 0) &&
            svc.spatial_layer_id == 0 && cm.seq_params.use_highbitdepth == 0 &&
            cpi.oxcf.rc_cfg.max_intra_bitrate_pct > 0)
        {
            // [subset] rc_spatial_act_onepass_rt: rc_compute_spatial_var_sc_kf is only set
            // for screen content.
            throw new NotSupportedException("screen content is out of the subset");
        }
        // Check for dynamic resize, for single spatial layer for now.
        // For temporal layers only check on base temporal layer.
        if (cpi.oxcf.resize_cfg.resize_mode == RESIZE_DYNAMIC)
        {
            // [subset] dynamic_resize_one_pass_cbr: resize is out of the subset.
            throw new NotSupportedException("dynamic resize is out of the subset");
        }
        else if (is_frame_resize_pending(cpi) != 0)
        {
            // [subset] resize_reset_rc: resize is out of the subset.
            throw new NotSupportedException("resize is out of the subset");
        }
        // Set the GF interval and update flag.
        if (rc.rtc_external_ratectrl == 0)
            set_gf_interval_update_onepass_rt(cpi, frame_type);
        // Set target size.
        if (cpi.oxcf.rc_cfg.mode == AOM_CBR)
        {
            if (frame_type == KEY_FRAME || frame_type == INTRA_ONLY_FRAME)
            {
                target = av1_calc_iframe_target_size_one_pass_cbr(cpi);
            }
            else
            {
                target = av1_calc_pframe_target_size_one_pass_cbr(
                    cpi, gf_group.update_type[cpi.gf_frame_index]);
            }
        }
        else
        {
            // [subset] one-pass VBR target sizes.
            throw new NotSupportedException("VBR is out of the subset");
        }
        if (cpi.oxcf.rc_cfg.mode == AOM_Q)
            rc.active_worst_quality = cpi.oxcf.rc_cfg.cq_level;

        av1_rc_set_frame_target(cpi, target, cm.width, cm.height);
        rc.base_frame_target = target;
        cm.current_frame.frame_type = frame_type;
        // [subset] KSVC fixed mode (use_svc == 0, checked above).
    }

    // [subset] CHECK_INTER_LAYER_PRED: spatial layers only.

    // Not executed by the coverage runs, but entered on every scene change
    // (high_source_sad) with sf.rt_sf.overshoot_detection_cbr == FAST_DETECTION_MAXQ,
    // which is the speed 11 setting: data dependent.
    internal static int av1_encodedframe_overshoot_cbr(AV1_COMP cpi, ref int q)
    {
        AV1_COMMON cm = cpi.common;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        double rate_correction_factor =
            cpi.ppi.p_rc.rate_correction_factors[INTER_NORMAL];
        int target_size = cpi.rc.avg_frame_bandwidth;
        double new_correction_factor;
        int target_bits_per_mb;
        double q2;
        int enumerator;
        int is_screen_content = B(cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN);
        cpi.cyclic_refresh.counter_encode_maxq_scene_change = 0;
        if (cpi.svc.spatial_layer_id > 0)
        {
            // [subset] inter-layer prediction check (CHECK_INTER_LAYER_PRED): spatial
            // layers are out of the subset.
            throw new NotSupportedException("SVC is out of the subset");
        }
        // If inter-layer prediction is on: we expect to pull up the quality from
        // the lower spatial layer, so we can use a lower q.
        // [subset] (spatial_layer_id > 0 && inter_layer_pred_on) is impossible here.
        {
            // For easy scene changes used lower QP, otherwise set max-q.
            // If rt_sf->compute_spatial_var_sc is enabled relax the max-q
            // condition based on frame spatial variance.
            if (cpi.sf.rt_sf.rc_compute_spatial_var_sc_kf != 0)
            {
                if (cpi.rc.frame_spatial_variance < 100)
                {
                    q = (cpi.rc.worst_quality + q) >> 1;
                }
                else if (cpi.rc.frame_spatial_variance < 400 ||
                         (cpi.rc.frame_source_sad < 80000 &&
                          cpi.rc.frame_spatial_variance < 1000))
                {
                    q = (3 * cpi.rc.worst_quality + q) >> 2;
                }
                else
                {
                    q = cpi.rc.worst_quality;
                }
            }
            else
            {
                // Set a larger QP.
                const ulong sad_thr = 64 * 64 * 32;
                if (cm.width * cm.height >= 1280 * 720 &&
                    (p_rc.buffer_level > (p_rc.optimal_buffer_level) >> 1) &&
                    cpi.rc.avg_source_sad < sad_thr)
                {
                    q = (q + cpi.rc.worst_quality) >> 1;
                }
                else
                {
                    q = (3 * cpi.rc.worst_quality + q) >> 2;
                }
                // If we arrive here for screen content: use the max-q set by the user.
                if (is_screen_content != 0) q = cpi.rc.worst_quality;
            }
        }
        // Adjust avg_frame_qindex, buffer_level, and rate correction factors, as
        // these parameters will affect QP selection for subsequent frames. If they
        // have settled down to a very different (low QP) state, then not adjusting
        // them may cause next frame to select low QP and overshoot again.
        p_rc.avg_frame_qindex[INTER_FRAME] = q;
        p_rc.buffer_level = p_rc.optimal_buffer_level;
        p_rc.bits_off_target = p_rc.optimal_buffer_level;
        // Reset rate under/over-shoot flags.
        cpi.rc.rc_1_frame = 0;
        cpi.rc.rc_2_frame = 0;
        // Adjust rate correction factor.
        target_bits_per_mb =
            (int)(((ulong)target_size << BPER_MB_NORMBITS) / (ulong)cm.mi_params.MBs);
        // Reset rate correction factor: for now base it on target_bits_per_mb
        // and qp (==max_QP). This comes from the inverse computation of
        // av1_rc_bits_per_mb().
        q2 = av1_convert_qindex_to_q(q, cm.seq_params.bit_depth);
        enumerator = get_bpmb_enumerator(INTER_NORMAL, is_screen_content);
        new_correction_factor = (double)target_bits_per_mb * q2 / enumerator;
        if (new_correction_factor > rate_correction_factor)
        {
            rate_correction_factor =
                (new_correction_factor + rate_correction_factor) / 2.0;
            if (rate_correction_factor > MAX_BPB_FACTOR)
                rate_correction_factor = MAX_BPB_FACTOR;
            cpi.ppi.p_rc.rate_correction_factors[INTER_NORMAL] =
                rate_correction_factor;
        }
        // For temporal layers: reset the rate control parameters across all
        // temporal layers. Only do it for spatial enhancement layers when
        // inter_layer_pred_on is not set (off).
        if (cpi.svc.number_temporal_layers > 1)
        {
            // [subset] per-layer reset: SVC is out of the subset.
            throw new NotSupportedException("SVC is out of the subset");
        }
        return 1;
    }

    // C: size_t *size. The frame size in bytes (an int here, see PORTING.md 3.1).
    internal static int av1_postencode_drop_cbr(AV1_COMP cpi, ref int size)
    {
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        ulong frame_size = (ulong)size << 3;
        long new_buffer_level =
            p_rc.buffer_level + cpi.rc.avg_frame_bandwidth - (long)frame_size;
        // Drop if new buffer level (given the encoded frame size) goes below a
        // threshold and encoded frame size is much larger than per-frame-bandwidth.
        // If the frame is already labelled as scene change (high_source_sad = 1)
        // or the QP is close to max, then no need to drop.
        int qp_thresh = 3 * (cpi.rc.worst_quality >> 2);
        long buffer_thresh = p_rc.optimal_buffer_level >> 2;
        // C: 8 * (unsigned int)avg_frame_bandwidth is computed (and wraps) in 32 bits.
        if (cpi.rc.high_source_sad == 0 && new_buffer_level < buffer_thresh &&
            frame_size > (ulong)(8 * (uint)cpi.rc.avg_frame_bandwidth) &&
            cpi.common.quant_params.base_qindex < qp_thresh)
        {
            size = 0;
            cpi.is_dropped_frame = true;
            restore_all_coding_context(cpi);
            av1_rc_postencode_update_drop_frame(cpi);
            // Force max_q on next fame. Reset some RC parameters.
            cpi.rc.force_max_q = 1;
            p_rc.avg_frame_qindex[INTER_FRAME] = cpi.rc.worst_quality;
            p_rc.buffer_level = p_rc.optimal_buffer_level;
            p_rc.bits_off_target = p_rc.optimal_buffer_level;
            cpi.rc.rc_1_frame = 0;
            cpi.rc.rc_2_frame = 0;
            if (cpi.svc.number_spatial_layers > 1 ||
                cpi.svc.number_temporal_layers > 1)
            {
                // [subset] per-layer reset: SVC is out of the subset.
                throw new NotSupportedException("SVC is out of the subset");
            }
            return 1;
        }
        return 0;
    }
}
