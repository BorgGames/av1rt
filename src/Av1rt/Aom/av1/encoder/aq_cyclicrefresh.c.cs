// Port of libaom av1/encoder/aq_cyclicrefresh.c
//
// Subset: one-pass realtime CBR, aq_mode 3, speed 11 (skip_over4x4 = 1), no SVC, no
// screen content, no ROI / active maps. See ratectrl.c.cs for the rate control side.

using System;
using System.Diagnostics;
using static System.Math;

namespace Av1rt.Aom;

internal static partial class aq_cyclicrefresh
{
    internal static CYCLIC_REFRESH av1_cyclic_refresh_alloc(int mi_rows, int mi_cols)
    {
        CYCLIC_REFRESH cr = new CYCLIC_REFRESH();

        cr.map = new sbyte[mi_rows * mi_cols];
        cr.counter_encode_maxq_scene_change = 0;
        cr.percent_refresh_adjustment = 5;
        cr.rate_ratio_qdelta_adjustment = 0.25;
        // [port] aom_calloc failure path: managed allocation throws instead.
        return cr;
    }

    internal static void av1_cyclic_refresh_free(CYCLIC_REFRESH cr)
    {
        if (cr != null)
        {
            // [port] aom_free(cr->map); aom_free(cr): left to the GC.
            cr.map = Ptr<sbyte>.Null;
        }
    }

    // Check if this coding block, of size bsize, should be considered for refresh
    // (lower-qp coding). Decision can be based on various factors, such as
    // size of the coding block (i.e., below min_block size rejected), coding
    // mode, and rate/distortion.
    private static int candidate_refresh_aq(CYCLIC_REFRESH cr,
                                            MB_MODE_INFO mbmi, long rate,
                                            long dist, BLOCK_SIZE bsize,
                                            int noise_level)
    {
        MV mv = mbmi.mv[0].as_mv;
        int is_compound = has_second_ref(mbmi);
        // Reject the block for lower-qp coding for non-compound mode if
        // projected distortion is above the threshold, and any of the following
        // is true:
        // 1) mode uses large mv
        // 2) mode is an intra-mode
        // Otherwise accept for refresh.
        if (is_compound == 0 && dist > cr.thresh_dist_sb &&
            (mv.row > cr.motion_thresh || mv.row < -cr.motion_thresh ||
             mv.col > cr.motion_thresh || mv.col < -cr.motion_thresh ||
             is_inter_block(mbmi) == 0))
            return CR_SEGMENT_ID_BASE;
        else if ((is_compound != 0 && noise_level < kMedium) ||
                 (bsize >= BLOCK_16X16 && rate < cr.thresh_rate_sb &&
                  is_inter_block(mbmi) != 0 && mbmi.mv[0].as_int == 0 &&
                  cr.rate_boost_fac > 10))
            // More aggressive delta-q for bigger blocks with zero motion.
            return CR_SEGMENT_ID_BOOST2;
        else
            return CR_SEGMENT_ID_BOOST1;
    }

    // Compute delta-q for the segment.
    private static int compute_deltaq(AV1_COMP cpi, int q, double rate_factor)
    {
        CYCLIC_REFRESH cr = cpi.cyclic_refresh;
        int deltaq = av1_compute_qdelta_by_rate(
            cpi, cpi.common.current_frame.frame_type, q, rate_factor);
        if ((-deltaq) > cr.max_qdelta_perc * q / 100)
        {
            deltaq = -cr.max_qdelta_perc * q / 100;
        }
        return deltaq;
    }

    internal static int av1_cyclic_refresh_estimate_bits_at_q(AV1_COMP cpi,
                                                              double correction_factor)
    {
        AV1_COMMON cm = cpi.common;
        int base_qindex = cm.quant_params.base_qindex;
        CYCLIC_REFRESH cr = cpi.cyclic_refresh;
        int mbs = cm.mi_params.MBs;
        int num4x4bl = mbs << 4;
        // Weight for non-base segments: use actual number of blocks refreshed in
        // previous/just encoded frame. Note number of blocks here is in 4x4 units.
        double weight_segment1 = (double)cr.actual_num_seg1_blocks / num4x4bl;
        double weight_segment2 = (double)cr.actual_num_seg2_blocks / num4x4bl;
        if (cpi.rc.rtc_external_ratectrl != 0)
        {
            weight_segment1 = (double)(cr.percent_refresh * cm.mi_params.mi_rows *
                                       cm.mi_params.mi_cols / 100) /
                              num4x4bl;
            weight_segment2 = 0;
        }
        // Take segment weighted average for estimated bits.
        int estimated_bits = (int)Round(
            (1.0 - weight_segment1 - weight_segment2) *
                av1_estimate_bits_at_q(cpi, base_qindex, correction_factor) +
            weight_segment1 *
                av1_estimate_bits_at_q(cpi, base_qindex + cr.qindex_delta[1],
                                       correction_factor) +
            weight_segment2 *
                av1_estimate_bits_at_q(cpi, base_qindex + cr.qindex_delta[2],
                                       correction_factor),
            MidpointRounding.AwayFromZero);
        return estimated_bits;
    }

    internal static int av1_cyclic_refresh_rc_bits_per_mb(AV1_COMP cpi, int i,
                                                          double correction_factor)
    {
        AV1_COMMON cm = cpi.common;
        CYCLIC_REFRESH cr = cpi.cyclic_refresh;
        int bits_per_mb;
        int num4x4bl = cm.mi_params.MBs << 4;
        // Weight for segment prior to encoding: take the average of the target
        // number for the frame to be encoded and the actual from the previous frame.
        double weight_segment =
            (double)((cr.target_num_seg_blocks + cr.actual_num_seg1_blocks +
                      cr.actual_num_seg2_blocks) >>
                     1) /
            num4x4bl;
        if (cpi.rc.rtc_external_ratectrl != 0)
        {
            weight_segment = (double)((cr.target_num_seg_blocks +
                                       cr.percent_refresh * cm.mi_params.mi_rows *
                                           cm.mi_params.mi_cols / 100) >>
                                      1) /
                             num4x4bl;
        }
        // Compute delta-q corresponding to qindex i.
        int deltaq = compute_deltaq(cpi, i, cr.rate_ratio_qdelta);
        int accurate_estimate = cpi.sf.hl_sf.accurate_bit_estimate;
        // Take segment weighted average for bits per mb.
        bits_per_mb = (int)Round(
            (1.0 - weight_segment) *
                av1_rc_bits_per_mb(cpi, cm.current_frame.frame_type, i,
                                   correction_factor, accurate_estimate) +
            weight_segment * av1_rc_bits_per_mb(cpi, cm.current_frame.frame_type,
                                                i + deltaq, correction_factor,
                                                accurate_estimate),
            MidpointRounding.AwayFromZero);
        return bits_per_mb;
    }

    internal static void av1_cyclic_reset_segment_skip(AV1_COMP cpi, MACROBLOCK x,
                                                       int mi_row, int mi_col, BLOCK_SIZE bsize,
                                                       RUN_TYPE dry_run)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mbmi = xd.mi[0];
        int prev_segment_id = mbmi.segment_id;
        CYCLIC_REFRESH cr = cpi.cyclic_refresh;
        int bw = mi_size_wide[bsize];
        int bh = mi_size_high[bsize];
        int xmis = AOMMIN(cm.mi_params.mi_cols - mi_col, bw);
        int ymis = AOMMIN(cm.mi_params.mi_rows - mi_row, bh);

        Debug.Assert(cm.seg.enabled != 0);

        if (cr.skip_over4x4 == 0 && cm.seg.update_map != 0 && cm.seg.segid_preskip == 0)
        {
            // [subset] av1_get_spatial_seg_pred re-prediction: skip_over4x4 is 1 for
            // speed > 9 (no ROI), so this is never reached at speed 11.
            throw new NotSupportedException("skip_over4x4 == 0 is out of the subset");
        }
        if (dry_run == 0)
        {
            if (cyclic_refresh_segment_id(prev_segment_id) == CR_SEGMENT_ID_BOOST1)
                x.actual_num_seg1_blocks -= xmis * ymis;
            else if (cyclic_refresh_segment_id(prev_segment_id) == CR_SEGMENT_ID_BOOST2)
                x.actual_num_seg2_blocks -= xmis * ymis;
        }
    }

    internal static void av1_cyclic_refresh_update_segment(AV1_COMP cpi, MACROBLOCK x,
                                                           int mi_row, int mi_col,
                                                           BLOCK_SIZE bsize,
                                                           long rate, long dist, int skip,
                                                           RUN_TYPE dry_run)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mbmi = xd.mi[0];
        CYCLIC_REFRESH cr = cpi.cyclic_refresh;
        int bw = mi_size_wide[bsize];
        int bh = mi_size_high[bsize];
        int xmis = AOMMIN(cm.mi_params.mi_cols - mi_col, bw);
        int ymis = AOMMIN(cm.mi_params.mi_rows - mi_row, bh);
        int block_index = mi_row * cm.mi_params.mi_cols + mi_col;
        int noise_level = 0;
        if (cpi.noise_estimate.enabled != 0) noise_level = cpi.noise_estimate.level;
        int refresh_this_block =
            candidate_refresh_aq(cr, mbmi, rate, dist, bsize, noise_level);
        int sh = cpi.cyclic_refresh.skip_over4x4 != 0 ? 2 : 1;
        // Default is to not update the refresh map.
        int new_map_value = cr.map[block_index];

        // If this block is labeled for refresh, check if we should reset the
        // segment_id.
        if (cyclic_refresh_segment_id_boosted(mbmi.segment_id) != 0)
        {
            mbmi.segment_id = (byte)refresh_this_block;
            // Reset segment_id if will be skipped.
            if (skip != 0) mbmi.segment_id = CR_SEGMENT_ID_BASE;
        }
        byte segment_id = mbmi.segment_id;

        // Update the cyclic refresh map, to be used for setting segmentation map
        // for the next frame. If the block  will be refreshed this frame, mark it
        // as clean. The magnitude of the -ve influences how long before we consider
        // it for refresh again.
        if (cyclic_refresh_segment_id_boosted(segment_id) != 0)
        {
            new_map_value = -cr.time_for_refresh;
        }
        else if (refresh_this_block != 0)
        {
            // Else if it is accepted as candidate for refresh, and has not already
            // been refreshed (marked as 1) then mark it as a candidate for cleanup
            // for future time (marked as 0), otherwise don't update it.
            if (cr.map[block_index] == 1) new_map_value = 0;
        }
        else
        {
            // Leave it marked as block that is not candidate for refresh.
            new_map_value = 1;
        }

        // Update entries in the cyclic refresh map with new_map_value, and
        // copy mbmi->segment_id into global segmentation map.
        int mi_stride = cm.mi_params.mi_cols;
        for (int mi_y = 0; mi_y < ymis; mi_y += sh)
        {
            int map_offset = block_index + mi_y * mi_stride;
            memset(cr.map + map_offset, unchecked((sbyte)new_map_value), xmis);
            memset(cpi.enc_seg.map + map_offset, segment_id, xmis);
            memset(cm.cur_frame.seg_map + map_offset, segment_id, xmis);
        }

        // Accumulate cyclic refresh update counters.
        if (dry_run == 0)
        {
            if (cyclic_refresh_segment_id(segment_id) == CR_SEGMENT_ID_BOOST1)
                x.actual_num_seg1_blocks += xmis * ymis;
            else if (cyclic_refresh_segment_id(segment_id) == CR_SEGMENT_ID_BOOST2)
                x.actual_num_seg2_blocks += xmis * ymis;
        }
    }

    // Initializes counters used for cyclic refresh.
    internal static void av1_init_cyclic_refresh_counters(MACROBLOCK x)
    {
        x.actual_num_seg1_blocks = 0;
        x.actual_num_seg2_blocks = 0;
    }

    // Accumulate cyclic refresh counters.
    internal static void av1_accumulate_cyclic_refresh_counters(
        CYCLIC_REFRESH cyclic_refresh, MACROBLOCK x)
    {
        cyclic_refresh.actual_num_seg1_blocks += x.actual_num_seg1_blocks;
        cyclic_refresh.actual_num_seg2_blocks += x.actual_num_seg2_blocks;
    }

    // [subset] av1_cyclic_refresh_set_golden_update: no caller in libaom.

    // Update the segmentation map, and related quantities: cyclic refresh map,
    // refresh sb_index, and target number of blocks to be refreshed.
    // The map is set to either 0/CR_SEGMENT_ID_BASE (no refresh) or to
    // 1/CR_SEGMENT_ID_BOOST1 (refresh) for each superblock.
    // Blocks labeled as BOOST1 may later get set to BOOST2 (during the
    // encoding of the superblock).
    private static void cyclic_refresh_update_map(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        CommonModeInfoParams mi_params = cm.mi_params;
        CYCLIC_REFRESH cr = cpi.cyclic_refresh;
        Ptr<byte> seg_map = cpi.enc_seg.map;
        Ptr<byte> active_map_4x4 = cpi.active_map.map;
        int i, block_count, bl_index, sb_rows, sb_cols, sbs_in_frame;
        int xmis, ymis, x, y;
        ulong sb_sad = 0;
        ulong thresh_sad_low = 0;
        ulong thresh_sad = long.MaxValue;
        int mi_rows = mi_params.mi_rows, mi_cols = mi_params.mi_cols;
        int mi_stride = mi_cols;
        // Don't set seg_map to 0 if active_maps is enabled. Active_maps will set
        // seg_map to either 7 or 0 (AM_SEGMENT_ID_INACTIVE/ACTIVE), and cyclic
        // refresh set below (segment 1 or 2) will only be set for ACTIVE blocks.
        if (cpi.active_map.enabled == 0)
        {
            memset(seg_map, (byte)CR_SEGMENT_ID_BASE, mi_rows * mi_cols);
        }
        sb_cols = (mi_cols + cm.seq_params.mib_size - 1) / cm.seq_params.mib_size;
        sb_rows = (mi_rows + cm.seq_params.mib_size - 1) / cm.seq_params.mib_size;
        sbs_in_frame = sb_cols * sb_rows;
        // Number of target blocks to get the q delta (segment 1).
        block_count = cr.percent_refresh * mi_rows * mi_cols / 100;
        // Set the segmentation map: cycle through the superblocks, starting at
        // cr->mb_index, and stopping when either block_count blocks have been found
        // to be refreshed, or we have passed through whole frame.
        if (cr.sb_index >= sbs_in_frame) cr.sb_index = 0;
        Debug.Assert(cr.sb_index < sbs_in_frame);
        i = cr.sb_index;
        cr.last_sb_index = cr.sb_index;
        cr.target_num_seg_blocks = 0;
        do
        {
            int sum_map = 0;
            // Get the mi_row/mi_col corresponding to superblock index i.
            int sb_row_index = (i / sb_cols);
            int sb_col_index = i - sb_row_index * sb_cols;
            int mi_row = sb_row_index * cm.seq_params.mib_size;
            int mi_col = sb_col_index * cm.seq_params.mib_size;
            Debug.Assert(mi_row >= 0 && mi_row < mi_rows);
            Debug.Assert(mi_col >= 0 && mi_col < mi_cols);
            bl_index = mi_row * mi_stride + mi_col;
            // Loop through all MI blocks in superblock and update map.
            xmis = AOMMIN(mi_cols - mi_col, cm.seq_params.mib_size);
            ymis = AOMMIN(mi_rows - mi_row, cm.seq_params.mib_size);
            if (cr.use_block_sad_scene_det != 0 && cpi.rc.frames_since_key > 30 &&
                cr.counter_encode_maxq_scene_change > 30 &&
                !cpi.src_sad_blk_64x64.IsNull &&
                cpi.svc.spatial_layer_id == cpi.svc.number_spatial_layers - 1)
            {
                sb_sad = cpi.src_sad_blk_64x64[sb_col_index + sb_cols * sb_row_index];
                int scale = (cm.width * cm.height < 640 * 360) ? 6 : 8;
                int scale_low = 2;
                thresh_sad = (ulong)(scale * 64 * 64);
                thresh_sad_low = (ulong)(scale_low * 64 * 64);
                // For temporal layers: the base temporal layer (temporal_layer_id = 0)
                // has larger frame separation (2 or 4 frames apart), so use larger sad
                // thresholds to compensate for larger frame sad. The larger thresholds
                // also increase the amount of refresh, which is needed for the base
                // temporal layer.
                if (cpi.svc.number_temporal_layers > 1 &&
                    cpi.svc.temporal_layer_id == 0)
                {
                    // [subset] temporal layers (SVC) are out of the subset.
                    throw new NotSupportedException("SVC is out of the subset");
                }
            }
            // cr_map only needed at 8x8 blocks.
            for (y = 0; y < ymis; y += 2)
            {
                for (x = 0; x < xmis; x += 2)
                {
                    int bl_index2 = bl_index + y * mi_stride + x;
                    // If the block is as a candidate for clean up then mark it
                    // for possible boost/refresh (segment 1). The segment id may get
                    // reset to 0 later if block gets coded anything other than low motion.
                    // If the block_sad (sb_sad) is very low label it for refresh anyway.
                    // If active_maps is enabled, only allow for setting on ACTIVE blocks.
                    if ((cr.map[bl_index2] == 0 || sb_sad < thresh_sad_low) &&
                        (cpi.active_map.enabled == 0 ||
                         active_map_4x4[bl_index2] == AM_SEGMENT_ID_ACTIVE))
                    {
                        sum_map += 4;
                    }
                    else if (cr.map[bl_index2] < 0)
                    {
                        cr.map[bl_index2]++;
                    }
                }
            }
            // Enforce constant segment over superblock.
            // If segment is at least half of superblock, set to 1.
            // Enforce that block sad (sb_sad) is not too high.
            if (sum_map >= (xmis * ymis) >> 1 && sb_sad < thresh_sad)
            {
                set_segment_id(seg_map, bl_index, xmis, ymis, mi_stride,
                               (byte)CR_SEGMENT_ID_BOOST1);
                cr.target_num_seg_blocks += xmis * ymis;
            }
            i++;
            if (i == sbs_in_frame)
            {
                i = 0;
            }
        } while (cr.target_num_seg_blocks < block_count && i != cr.sb_index);
        cr.sb_index = i;
        if (cr.target_num_seg_blocks == 0)
        {
            // Disable segmentation, seg_map is already set to 0 above.
            // Don't disable if active_map is being used.
            if (cpi.active_map.enabled == 0) av1_disable_segmentation(cm.seg);
        }
    }

    private static int is_scene_change_detected(AV1_COMP cpi)
    {
        return cpi.rc.high_source_sad;
    }

    // Set cyclic refresh parameters.
    internal static void av1_cyclic_refresh_update_parameters(AV1_COMP cpi)
    {
        // TODO(marpan): Parameters need to be tuned.
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        AV1_COMMON cm = cpi.common;
        CYCLIC_REFRESH cr = cpi.cyclic_refresh;
        SVC svc = cpi.svc;
        int qp_thresh = AOMMAX(16, rc.best_quality + 4);
        const int qp_max_thresh = 118 * MAXQ >> 7;
        int scene_change_detected = is_scene_change_detected(cpi);
        int is_screen_content =
            B(cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN);

        // A scene change or key frame marks the start of a cyclic refresh cycle.
        int frames_since_scene_change =
            (cpi.ppi.use_svc != 0 || is_screen_content == 0)
                ? cpi.rc.frames_since_key
                : AOMMIN(cpi.rc.frames_since_key,
                         cr.counter_encode_maxq_scene_change);

        // Cases to reset the cyclic refresh adjustment parameters.
        if (frame_is_intra_only(cm) != 0 || scene_change_detected != 0 ||
            cpi.ppi.rtc_ref.bias_recovery_frame)
        {
            // Reset adaptive elements for intra only frames and scene changes.
            cr.percent_refresh_adjustment = 5;
            cr.rate_ratio_qdelta_adjustment = 0.25;
        }

        // Although this segment feature for RTC is only used for
        // blocks >= 8X8, for more efficient coding of the seg map
        // cur_frame->seg_map needs to set at 4x4 along with the
        // function av1_cyclic_reset_segment_skip(). Skipping over
        // 4x4 will therefore have small bdrate loss (~0.2%), so
        // we use it only for speed > 9 for now.
        cr.skip_over4x4 = (cpi.oxcf.speed > 9 && cpi.roi.enabled == 0) ? 1 : 0;

        // should we enable cyclic refresh on this frame.
        cr.apply_cyclic_refresh = 1;
        if (frame_is_intra_only(cm) != 0 || is_lossless_requested(in cpi.oxcf.rc_cfg) != 0 ||
            (is_one_pass_rt_lag_params(cpi) != 0 &&
             (cpi.refresh_frame.alt_ref_frame || cpi.rc.is_src_frame_alt_ref != 0)) ||
            cpi.roi.enabled != 0 || cpi.rc.high_motion_content_screen_rtc != 0 ||
            scene_change_detected != 0 || svc.temporal_layer_id > 0 ||
            svc.prev_number_spatial_layers != svc.number_spatial_layers ||
            p_rc.avg_frame_qindex[INTER_FRAME] < qp_thresh ||
            (svc.number_spatial_layers > 1 &&
             svc.layer_context[svc.temporal_layer_id].is_key_frame != 0) ||
            (frames_since_scene_change > 20 &&
             p_rc.avg_frame_qindex[INTER_FRAME] > qp_max_thresh) ||
            (rc.avg_frame_low_motion != 0 && rc.avg_frame_low_motion < 30 &&
             frames_since_scene_change > 40) ||
            cpi.ppi.rtc_ref.bias_recovery_frame)
        {
            cr.apply_cyclic_refresh = 0;
            return;
        }

        // Increase the amount of refresh for #temporal_layers > 2
        if (svc.number_temporal_layers > 2)
            cr.percent_refresh = 15;
        else
            cr.percent_refresh = 10 + cr.percent_refresh_adjustment;

        if (cpi.active_map.enabled != 0)
        {
            // Scale down the percent_refresh to target the active blocks only.
            cr.percent_refresh =
                cr.percent_refresh * (100 - cpi.rc.percent_blocks_inactive) / 100;
            if (cr.percent_refresh == 0)
            {
                cr.apply_cyclic_refresh = 0;
            }
        }

        cr.max_qdelta_perc = 60;
        cr.time_for_refresh = 0;
        cr.use_block_sad_scene_det =
            (cpi.oxcf.tune_cfg.content != AOM_CONTENT_SCREEN &&
             cm.seq_params.sb_size == BLOCK_64X64)
                ? 1
                : 0;
        cr.motion_thresh = 32;
        cr.rate_boost_fac =
            (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN) ? 10 : 15;

        // Use larger delta-qp (increase rate_ratio_qdelta) for first few
        // refresh cycles after a key frame (svc) or scene change (non svc).
        // For non svc screen content, after a scene change gradually reduce
        // this boost and supress it further if either of the previous two
        // frames overshot.
        if (cr.percent_refresh > 0)
        {
            if (cpi.ppi.use_svc != 0 || is_screen_content == 0)
            {
                if (frames_since_scene_change <
                    ((4 * svc.number_temporal_layers) * (100 / cr.percent_refresh)))
                {
                    cr.rate_ratio_qdelta = 3.0 + cr.rate_ratio_qdelta_adjustment;
                }
                else
                {
                    cr.rate_ratio_qdelta = 2.25 + cr.rate_ratio_qdelta_adjustment;
                }
            }
            else
            {
                double distance_from_sc_factor =
                    AOMMIN(0.75, (int)(frames_since_scene_change / 10) * 0.1);
                cr.rate_ratio_qdelta =
                    3.0 + cr.rate_ratio_qdelta_adjustment - distance_from_sc_factor;
                if ((frames_since_scene_change < 10) &&
                    ((cpi.rc.rc_1_frame < 0) || (cpi.rc.rc_2_frame < 0)))
                {
                    cr.rate_ratio_qdelta -= 0.25;
                }
            }
        }
        else
        {
            cr.rate_ratio_qdelta = 2.25 + cr.rate_ratio_qdelta_adjustment;
        }
        // Adjust some parameters for low resolutions.
        if (cm.width * cm.height <= 352 * 288)
        {
            if (cpi.svc.number_temporal_layers > 1)
            {
                cr.motion_thresh = 32;
                cr.rate_boost_fac = 13;
            }
            else
            {
                if (rc.avg_frame_bandwidth < 3000)
                {
                    cr.motion_thresh = 16;
                    cr.rate_boost_fac = 13;
                }
                else
                {
                    cr.max_qdelta_perc = 50;
                    cr.rate_ratio_qdelta = AOMMAX(cr.rate_ratio_qdelta, 2.0);
                }
            }
        }
        if (cpi.oxcf.rc_cfg.mode == AOM_VBR)
        {
            // To be adjusted for VBR mode, e.g., based on gf period and boost.
            // For now use smaller qp-delta (than CBR), no second boosted seg, and
            // turn-off (no refresh) on golden refresh (since it's already boosted).
            cr.percent_refresh = 10;
            cr.rate_ratio_qdelta = 1.5;
            cr.rate_boost_fac = 10;
            if (cpi.refresh_frame.golden_frame)
            {
                cr.percent_refresh = 0;
                cr.rate_ratio_qdelta = 1.0;
            }
        }
        if (rc.rtc_external_ratectrl != 0)
        {
            cr.actual_num_seg1_blocks = cr.percent_refresh * cm.mi_params.mi_rows *
                                        cm.mi_params.mi_cols / 100;
            cr.actual_num_seg2_blocks = 0;
        }
    }

    // Not executed by the coverage runs (needs a frame size change), but tiny.
    private static void cyclic_refresh_reset_resize(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        CYCLIC_REFRESH cr = cpi.cyclic_refresh;
        memset(cr.map, (sbyte)0, cm.mi_params.mi_rows * cm.mi_params.mi_cols);
        cr.sb_index = 0;
        cr.last_sb_index = 0;
        cpi.refresh_frame.golden_frame = true;
        cr.apply_cyclic_refresh = 0;
        cr.counter_encode_maxq_scene_change = 0;
        cr.percent_refresh_adjustment = 5;
        cr.rate_ratio_qdelta_adjustment = 0.25;
    }

    // Setup cyclic background refresh: set delta q and segmentation map.
    internal static void av1_cyclic_refresh_setup(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        RATE_CONTROL rc = cpi.rc;
        CYCLIC_REFRESH cr = cpi.cyclic_refresh;
        segmentation seg = cm.seg;
        int scene_change_detected = is_scene_change_detected(cpi);
        GF_GROUP gf_group = cpi.ppi.gf_group;
        int boost_index = AOMMIN(15, (cpi.ppi.p_rc.gfu_boost / 100));
        int layer_depth = AOMMIN(gf_group.layer_depth[cpi.gf_frame_index], 6);
        FRAME_TYPE frame_type = cm.current_frame.frame_type;

        // Set resolution_change flag: for single spatial layers only.
        int resolution_change = B(cpi.rc.rtc_external_ratectrl == 0 &&
                                  cm.prev_frame != null &&
                                  cpi.svc.number_spatial_layers == 1 &&
                                  (cm.width != cm.prev_frame.width ||
                                   cm.height != cm.prev_frame.height));

        if (resolution_change != 0 && cpi.svc.temporal_layer_id == 0)
            cyclic_refresh_reset_resize(cpi);
        if (cr.apply_cyclic_refresh == 0)
        {
            // Don't disable and set seg_map to 0 if active_maps is enabled, unless
            // whole frame is set as inactive (since we only apply cyclic_refresh to
            // active blocks).
            if (cpi.active_map.enabled == 0 || cpi.rc.percent_blocks_inactive == 100)
            {
                Ptr<byte> seg_map = cpi.enc_seg.map;
                memset(seg_map, (byte)0, cm.mi_params.mi_rows * cm.mi_params.mi_cols);
                av1_disable_segmentation(cm.seg);
            }
            else if (segfeature_active(seg, CR_SEGMENT_ID_BOOST1, SEG_LVL_ALT_Q) != 0 ||
                     segfeature_active(seg, CR_SEGMENT_ID_BOOST2, SEG_LVL_ALT_Q) != 0 ||
                     (cm.prev_frame != null &&
                      (segfeature_active(cm.prev_frame.seg, CR_SEGMENT_ID_BOOST1,
                                         SEG_LVL_ALT_Q) != 0 ||
                       segfeature_active(cm.prev_frame.seg, CR_SEGMENT_ID_BOOST2,
                                         SEG_LVL_ALT_Q) != 0)))
            {
                av1_disable_segfeature(seg, CR_SEGMENT_ID_BOOST1, SEG_LVL_ALT_Q);
                av1_disable_segfeature(seg, CR_SEGMENT_ID_BOOST2, SEG_LVL_ALT_Q);
                seg.update_data = 1;
            }
            if (frame_is_intra_only(cm) != 0 || scene_change_detected != 0 ||
                cpi.ppi.rtc_ref.bias_recovery_frame)
            {
                cr.sb_index = 0;
                cr.last_sb_index = 0;
                cr.counter_encode_maxq_scene_change = 0;
                cr.actual_num_seg1_blocks = 0;
                cr.actual_num_seg2_blocks = 0;
            }
            return;
        }
        else
        {
            cr.counter_encode_maxq_scene_change++;
            double q = av1_convert_qindex_to_q(cm.quant_params.base_qindex,
                                               cm.seq_params.bit_depth);
            // Set rate threshold to some multiple (set to 2 for now) of the target
            // rate (target is given by sb64_target_rate and scaled by 256).
            cr.thresh_rate_sb = ((long)(rc.sb64_target_rate) << 8) << 2;
            // Distortion threshold, quadratic in Q, scale factor to be adjusted.
            // q will not exceed 457, so (q * q) is within 32bit; see:
            // av1_convert_qindex_to_q(), av1_ac_quant(), ac_qlookup*[].
            cr.thresh_dist_sb = ((long)(q * q)) << 2;
            // For low-resoln or lower speeds, the rate/dist thresholds need to be
            // tuned/updated.
            if (cpi.oxcf.speed <= 7 || (cm.width * cm.height < 640 * 360))
            {
                cr.thresh_dist_sb = 0;
                cr.thresh_rate_sb = long.MaxValue;
            }
            // Set up segmentation.
            av1_enable_segmentation(cm.seg);
            if (cpi.active_map.enabled == 0)
            {
                // Clear down the segment map, only if active_maps is not enabled.
                av1_clearall_segfeatures(seg);
            }

            // Note: setting temporal_update has no effect, as the seg-map coding method
            // (temporal or spatial) is determined in
            // av1_choose_segmap_coding_method(),
            // based on the coding cost of each method. For error_resilient mode on the
            // last_frame_seg_map is set to 0, so if temporal coding is used, it is
            // relative to 0 previous map.
            // seg->temporal_update = 0;

            // Segment BASE "Q" feature is disabled so it defaults to the baseline Q.
            av1_disable_segfeature(seg, CR_SEGMENT_ID_BASE, SEG_LVL_ALT_Q);
            // Use segment BOOST1 for in-frame Q adjustment.
            av1_enable_segfeature(seg, CR_SEGMENT_ID_BOOST1, SEG_LVL_ALT_Q);
            // Use segment BOOST2 for more aggressive in-frame Q adjustment.
            av1_enable_segfeature(seg, CR_SEGMENT_ID_BOOST2, SEG_LVL_ALT_Q);

            // Set the q delta for segment BOOST1.
            CommonQuantParams quant_params = cm.quant_params;
            int qindex_delta =
                compute_deltaq(cpi, quant_params.base_qindex, cr.rate_ratio_qdelta);
            cr.qindex_delta[1] = qindex_delta;

            // Compute rd-mult for segment BOOST1.
            int qindex2 = clamp(
                quant_params.base_qindex + quant_params.y_dc_delta_q + qindex_delta,
                0, MAXQ);
            cr.rdmult = av1_compute_rd_mult(
                qindex2, cm.seq_params.bit_depth,
                cpi.ppi.gf_group.update_type[cpi.gf_frame_index], layer_depth,
                boost_index, frame_type, cpi.oxcf.q_cfg.use_fixed_qp_offsets,
                is_stat_consumption_stage(cpi), cpi.oxcf.tune_cfg.tuning,
                cpi.oxcf.mode);

            av1_set_segdata(seg, CR_SEGMENT_ID_BOOST1, SEG_LVL_ALT_Q, qindex_delta);

            // Set a more aggressive (higher) q delta for segment BOOST2.
            qindex_delta = compute_deltaq(
                cpi, quant_params.base_qindex,
                AOMMIN(CR_MAX_RATE_TARGET_RATIO,
                       0.1 * cr.rate_boost_fac * cr.rate_ratio_qdelta));
            cr.qindex_delta[2] = qindex_delta;
            av1_set_segdata(seg, CR_SEGMENT_ID_BOOST2, SEG_LVL_ALT_Q, qindex_delta);

            // Update the segmentation and refresh map.
            cyclic_refresh_update_map(cpi);
        }
    }

    internal static int av1_cyclic_refresh_get_rdmult(CYCLIC_REFRESH cr)
    {
        return cr.rdmult;
    }

    // [subset] av1_cyclic_refresh_disable_lf: only called with sf.rt_sf.skip_lf_screen
    // (screen content).
}
