// Port of libaom av1/encoder/encodeframe_utils.c
using System;
using System.Diagnostics;

using System.Runtime.Intrinsics;

namespace Av1rt.Aom;

internal static partial class encodeframe_utils
{
    // [subset] av1_set_ssim_rdmult, av1_get_cb_rdmult, av1_get_hier_tpl_rdmult:
    // !CONFIG_REALTIME_ONLY / tuning modes that the subset never uses.

    private static void update_filter_type_count(FRAME_COUNTS counts,
                                                 MACROBLOCKD xd,
                                                 MB_MODE_INFO mbmi)
    {
        int dir;
        for (dir = 0; dir < 2; ++dir)
        {
            int ctx = av1_get_pred_context_switchable_interp(xd, dir);
            InterpFilter filter = av1_extract_interp_filter(mbmi.interp_filters, dir);

            // Only allow the 3 valid SWITCHABLE_FILTERS.
            Debug.Assert(filter < SWITCHABLE_FILTERS);
            ++counts.switchable_interp[ctx][filter];
        }
    }

    // This function will copy the best reference mode information from
    // MB_MODE_INFO_EXT_FRAME to MB_MODE_INFO_EXT.
    private static void copy_mbmi_ext_frame_to_mbmi_ext(
        MB_MODE_INFO_EXT mbmi_ext,
        in MB_MODE_INFO_EXT_FRAME mbmi_ext_best, byte ref_frame_type)
    {
        // memcpy of sizeof(mbmi_ext->ref_mv_stack[USABLE_REF_MV_STACK_SIZE]), i.e. one row
        // of USABLE_REF_MV_STACK_SIZE entries.
        CANDIDATE_MV[] ref_mv_stack = mbmi_ext.ref_mv_stack[ref_frame_type];
        for (int i = 0; i < USABLE_REF_MV_STACK_SIZE; i++)
            ref_mv_stack[i] = mbmi_ext_best.ref_mv_stack[i];
        ushort[] weight = mbmi_ext.weight[ref_frame_type];
        for (int i = 0; i < USABLE_REF_MV_STACK_SIZE; i++)
            weight[i] = mbmi_ext_best.weight[i];
        mbmi_ext.mode_context[ref_frame_type] = mbmi_ext_best.mode_context;
        mbmi_ext.ref_mv_count[ref_frame_type] = mbmi_ext_best.ref_mv_count;
        for (int i = 0; i < REF_FRAMES; i++)
            mbmi_ext.global_mvs[i] = mbmi_ext_best.global_mvs[i];
    }

    internal static void av1_update_state(AV1_COMP cpi, ThreadData td,
                                          PICK_MODE_CONTEXT ctx, int mi_row,
                                          int mi_col, BLOCK_SIZE bsize, RUN_TYPE dry_run)
    {
        int i, x_idx, y;
        AV1_COMMON cm = cpi.common;
        CommonModeInfoParams mi_params = cm.mi_params;
        int num_planes = av1_num_planes(cm);
        MACROBLOCK x = td.mb;
        MACROBLOCKD xd = x.e_mbd;
        macroblock_plane[] p = x.plane;
        macroblockd_plane[] pd = xd.plane;
        // PORT-NOTE: C forms &ctx->mic before the NULL check; the port checks ctx first so
        // the check itself cannot fault.
        if (ctx == null)
        {
            throw new AomException(AOM_CODEC_ERROR,
                $"ctx is NULL in av1_update_state: {cm.current_frame.frame_type} {cm.width} " +
                $"{cm.height} {mi_col} {mi_row} {bsize} \n");
        }
        MB_MODE_INFO mi = ctx.mic;
        if (mi == null)
        {
            throw new AomException(AOM_CODEC_ERROR,
                $"mi is NULL in av1_update_state: {cm.current_frame.frame_type} {cm.width} " +
                $"{cm.height} {mi_col} {mi_row} {bsize} \n");
        }
        MB_MODE_INFO mi_addr = xd.mi[0];
        segmentation seg = cm.seg;
        Debug.Assert(bsize < BLOCK_SIZES_ALL);
        Debug.Assert(mi.bsize == bsize);
        int bw = mi_size_wide[bsize];
        int bh = mi_size_high[bsize];
        int mis = mi_params.mi_stride;
        int mi_width = mi_size_wide[bsize];
        int mi_height = mi_size_high[bsize];
        TxfmSearchInfo txfm_info = x.txfm_search_info;

        mi_addr.CopyFrom(mi);
        copy_mbmi_ext_frame_to_mbmi_ext(x.mbmi_ext, in ctx.mbmi_ext_best,
                                        (byte)av1_ref_frame_type(ctx.mic.ref_frame));

        txfm_info.skip_txfm = ctx.rd_stats.skip_txfm;

        xd.tx_type_map = ctx.tx_type_map;
        xd.tx_type_map_stride = mi_size_wide[bsize];
        // If not dry_run, copy the transform type data into the frame level buffer.
        // Encoder will fetch tx types when writing bitstream.
        if (dry_run == 0)
        {
            int grid_idx = get_mi_grid_idx(mi_params, mi_row, mi_col);
            Ptr<byte> tx_type_map = mi_params.tx_type_map + grid_idx;
            int mi_stride = mi_params.mi_stride;
            for (int blk_row = 0; blk_row < bh; ++blk_row)
            {
                av1_copy_array(tx_type_map + blk_row * mi_stride,
                               xd.tx_type_map + blk_row * xd.tx_type_map_stride, bw);
            }
            xd.tx_type_map = tx_type_map;
            xd.tx_type_map_stride = mi_stride;
        }

        // If segmentation in use
        if (seg.enabled != 0)
        {
            // For in frame complexity AQ or ROI copy the segment id from the
            // segment map.
            if (cpi.oxcf.q_cfg.aq_mode == COMPLEXITY_AQ || cpi.roi.enabled != 0)
            {
                // [subset] COMPLEXITY_AQ and ROI maps: aq_mode is CYCLIC_REFRESH_AQ and no ROI
                // map is set in the subset.
                throw new NotSupportedException("complexity AQ / ROI segment maps are not in the subset");
            }
            // Else for cyclic refresh mode update the segment map, set the segment id
            // and then update the quantizer.
            if (cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ && cpi.roi.enabled == 0 &&
                mi_addr.segment_id != AM_SEGMENT_ID_INACTIVE &&
                cpi.rc.rtc_external_ratectrl == 0)
            {
                av1_cyclic_refresh_update_segment(cpi, x, mi_row, mi_col, bsize,
                                                  ctx.rd_stats.rate, ctx.rd_stats.dist,
                                                  txfm_info.skip_txfm, dry_run);
            }
            if (mi_addr.uv_mode == UV_CFL_PRED && is_cfl_allowed(xd) == 0)
                mi_addr.uv_mode = UV_DC_PRED;

            if (dry_run == 0 && mi_addr.skip_txfm == 0)
            {
                int cdf_num = 0;
                byte spatial_pred = av1_get_spatial_seg_pred(
                    cm, xd, ref cdf_num, cpi.cyclic_refresh.skip_over4x4);
                byte coded_id = (byte)av1_neg_interleave(
                    mi_addr.segment_id, spatial_pred, seg.last_active_segid + 1);
                long spatial_cost = x.mode_costs.spatial_pred_cost[cdf_num][coded_id];
                // PORT-NOTE: seg_tmp_pred_cost is int64_t in C (ulong in RD_COUNTS); the
                // costs are non-negative so the sums are identical.
                td.rd_counts.seg_tmp_pred_cost[0] += (ulong)spatial_cost;

                int pred_segment_id =
                    !cm.last_frame_seg_map.IsNull
                        ? get_segment_id(mi_params, cm.last_frame_seg_map, bsize, mi_row,
                                         mi_col)
                        : 0;
                int use_tmp_pred = B(pred_segment_id == mi_addr.segment_id);
                byte tmp_pred_ctx = av1_get_pred_context_seg_id(xd);
                td.rd_counts.seg_tmp_pred_cost[1] +=
                    (ulong)(long)x.mode_costs.tmp_pred_cost[tmp_pred_ctx][use_tmp_pred];
                if (use_tmp_pred == 0)
                {
                    td.rd_counts.seg_tmp_pred_cost[1] += (ulong)spatial_cost;
                }
            }
        }

        // Count zero motion vector.
        if (dry_run == 0 && frame_is_intra_only(cm) == 0)
        {
            MV mv = mi.mv[0].as_mv;
            if (is_inter_block(mi) != 0 && mi.ref_frame[0] == LAST_FRAME &&
                Math.Abs((int)mv.row) < 8 && Math.Abs((int)mv.col) < 8)
            {
                int ymis = AOMMIN(cm.mi_params.mi_rows - mi_row, bh);
                // Accumulate low_content_frame.
                for (int mi_y = 0; mi_y < ymis; mi_y += 2) x.cnt_zeromv += bw << 1;
            }
        }

        for (i = 0; i < num_planes; ++i)
        {
            p[i].coeff = ctx.coeff[i];
            p[i].qcoeff = ctx.qcoeff[i];
            p[i].dqcoeff = ctx.dqcoeff[i];
            p[i].eobs = ctx.eobs[i];
            p[i].txb_entropy_ctx = ctx.txb_entropy_ctx[i];
        }
        for (i = 0; i < 2; ++i) pd[i].color_index_map = ctx.color_index_map[i];
        // Restore the coding context of the MB to that that was in place
        // when the mode was picked for it

        int cols =
            AOMMIN((xd.mb_to_right_edge >> (3 + MI_SIZE_LOG2)) + mi_width, mi_width);
        int rows = AOMMIN(
            (xd.mb_to_bottom_edge >> (3 + MI_SIZE_LOG2)) + mi_height, mi_height);
        for (y = 0; y < rows; y++)
        {
            for (x_idx = 0; x_idx < cols; x_idx++) xd.mi[x_idx + y * mis] = mi_addr;
        }

        if (cpi.oxcf.q_cfg.aq_mode != 0 ||
            (cpi.roi.enabled != 0 && cpi.roi.delta_qp_enabled != 0))
            av1_init_plane_quantizers(cpi, x, mi_addr.segment_id, 0);

        if (dry_run != 0) return;

        // [subset] CONFIG_INTERNAL_STATS mode_chosen_counts: not configured.
        if (frame_is_intra_only(cm) == 0)
        {
            if (is_inter_block(mi) != 0 && cm.features.interp_filter == SWITCHABLE)
            {
                // When the frame interp filter is SWITCHABLE, several cases that always
                // use the default type (EIGHTTAP_REGULAR) are described in
                // av1_is_interp_needed(). Here, we should keep the counts for all
                // applicable blocks, so the frame filter resetting decision in
                // fix_interp_filter() is made correctly.
                update_filter_type_count(td.counts, xd, mi_addr);
            }
        }

        int x_mis = AOMMIN(bw, mi_params.mi_cols - mi_col);
        int y_mis = AOMMIN(bh, mi_params.mi_rows - mi_row);
        if (cm.seq_params.order_hint_info.enable_ref_frame_mvs != 0)
        {
            // [subset] av1_copy_frame_mvs(cm, mi, mi_row, mi_col, x_mis, y_mis): ref frame
            // MVs are off (no order hint) in the subset.
            throw new NotSupportedException(
                $"av1_copy_frame_mvs ({x_mis}x{y_mis}) is not in the subset");
        }
    }

    // [subset] av1_update_inter_mode_stats, update_palette_cdf, sum_intra_stats,
    // av1_restore_context, av1_save_context: only used by the rd partition search.

    private static void set_partial_sb_partition(AV1_COMMON cm,
                                                 Ptr<MB_MODE_INFO> mi, int bh_in, int bw_in,
                                                 int mi_rows_remaining,
                                                 int mi_cols_remaining, BLOCK_SIZE bsize,
                                                 Ptr<MB_MODE_INFO> mib)
    {
        int bh = bh_in;
        int r, c;
        for (r = 0; r < cm.seq_params.mib_size; r += bh)
        {
            int bw = bw_in;
            for (c = 0; c < cm.seq_params.mib_size; c += bw)
            {
                int grid_index = get_mi_grid_idx(cm.mi_params, r, c);
                int mi_index = get_alloc_mi_idx(cm.mi_params, r, c);
                mib[grid_index] = mi[mi_index];
                mib[grid_index].bsize = find_partition_size(
                    bsize, mi_rows_remaining - r, mi_cols_remaining - c, ref bh, ref bw);
            }
        }
    }

    // This function attempts to set all mode info entries in a given superblock
    // to the same block partition size.
    // However, at the bottom and right borders of the image the requested size
    // may not be allowed in which case this code attempts to choose the largest
    // allowable partition.
    internal static void av1_set_fixed_partitioning(AV1_COMP cpi, in TileInfo tile,
                                                    Ptr<MB_MODE_INFO> mib, int mi_row, int mi_col,
                                                    BLOCK_SIZE bsize)
    {
        AV1_COMMON cm = cpi.common;
        CommonModeInfoParams mi_params = cm.mi_params;
        int mi_rows_remaining = tile.mi_row_end - mi_row;
        int mi_cols_remaining = tile.mi_col_end - mi_col;
        Ptr<MB_MODE_INFO> mi_upper_left =
            new Ptr<MB_MODE_INFO>(mi_params.mi_alloc, get_alloc_mi_idx(mi_params, mi_row, mi_col));
        int bh = mi_size_high[bsize];
        int bw = mi_size_wide[bsize];

        Debug.Assert(bsize >= mi_params.mi_alloc_bsize,
                     "Attempted to use bsize < mi_params->mi_alloc_bsize");
        Debug.Assert((mi_rows_remaining > 0) && (mi_cols_remaining > 0));

        // Apply the requested partition size to the SB if it is all "in image"
        if ((mi_cols_remaining >= cm.seq_params.mib_size) &&
            (mi_rows_remaining >= cm.seq_params.mib_size))
        {
            for (int block_row = 0; block_row < cm.seq_params.mib_size;
                 block_row += bh)
            {
                for (int block_col = 0; block_col < cm.seq_params.mib_size;
                     block_col += bw)
                {
                    int grid_index = get_mi_grid_idx(mi_params, block_row, block_col);
                    int mi_index = get_alloc_mi_idx(mi_params, block_row, block_col);
                    mib[grid_index] = mi_upper_left[mi_index];
                    mib[grid_index].bsize = bsize;
                }
            }
        }
        else
        {
            // Else this is a partial SB.
            set_partial_sb_partition(cm, mi_upper_left, bh, bw, mi_rows_remaining,
                                     mi_cols_remaining, bsize, mib);
        }
    }

    // [subset] av1_is_leaf_split_partition, av1_reset_simple_motion_tree_partition,
    // av1_update_picked_ref_frames_mask, av1_check_ml_part_*, av1_get_max_min_partition_*,
    // av1_get_rdmult_delta, av1_get_q_for_deltaq_objective, av1_get_q_for_hdr,
    // av1_reset_mbmi, av1_backup_sb_state, av1_restore_sb_state, av1_set_cost_upd_freq's
    // skip_cost_update / skip_mv_cost_update / skip_dv_cost_update (see below),
    // av1_avg_cdf_symbols and avg_* helpers: rd partition search, deltaq or allow_update_cdf
    // paths that never run in the subset.

    // Check neighbor blocks' motion information.
    private static int check_neighbor_blocks(Ptr<MB_MODE_INFO> mi, int mi_stride,
                                             in TileInfo tile_info, int mi_row,
                                             int mi_col)
    {
        int is_above_low_motion = 1;
        int is_left_low_motion = 1;
        const int thr = 24;

        // Check above block.
        if (mi_row > tile_info.mi_row_start)
        {
            MB_MODE_INFO above_mbmi = mi[-mi_stride];
            int_mv above_mv = above_mbmi.mv[0];
            if (above_mbmi.mode >= INTRA_MODE_END &&
                (Math.Abs((int)above_mv.as_mv.row) > thr || Math.Abs((int)above_mv.as_mv.col) > thr))
                is_above_low_motion = 0;
        }

        // Check left block.
        if (mi_col > tile_info.mi_col_start)
        {
            MB_MODE_INFO left_mbmi = mi[-1];
            int_mv left_mv = left_mbmi.mv[0];
            if (left_mbmi.mode >= INTRA_MODE_END &&
                (Math.Abs((int)left_mv.as_mv.row) > thr || Math.Abs((int)left_mv.as_mv.col) > thr))
                is_left_low_motion = 0;
        }

        return B(is_above_low_motion != 0 && is_left_low_motion != 0);
    }

    // C stack arrays of fast_detect_non_zero_motion (leaf scratch, PORTING.md §6).
    [ThreadStatic] private static Ptr<byte>[] s_search_pos;
    [ThreadStatic] private static uint[] s_sad_arr;

    // Check this block's motion in a fast way.
    private static int fast_detect_non_zero_motion(AV1_COMP cpi, Ptr<byte> src_y,
                                                   int src_ystride,
                                                   Ptr<byte> last_src_y,
                                                   int last_src_ystride, int mi_row,
                                                   int mi_col)
    {
        AV1_COMMON cm = cpi.common;
        BLOCK_SIZE bsize = cm.seq_params.sb_size;
        uint blk_sad = int.MaxValue;
        if (!cpi.src_sad_blk_64x64.IsNull)
        {
            int sb_size_by_mb = (bsize == BLOCK_128X128)
                                    ? (cm.seq_params.mib_size >> 1)
                                    : cm.seq_params.mib_size;
            int sb_cols =
                (cm.mi_params.mi_cols + sb_size_by_mb - 1) / sb_size_by_mb;
            int sbi_col = mi_col / sb_size_by_mb;
            int sbi_row = mi_row / sb_size_by_mb;
            blk_sad = (uint)cpi.src_sad_blk_64x64[sbi_col + sbi_row * sb_cols];
        }
        else
        {
            blk_sad = cpi.ppi.fn_ptr[bsize].sdf(src_y, src_ystride, last_src_y,
                                                last_src_ystride);
        }

        // Search 4 1-away points.
        Ptr<byte>[] search_pos = s_search_pos ??= new Ptr<byte>[4];
        search_pos[0] = last_src_y - last_src_ystride;
        search_pos[1] = last_src_y - 1;
        search_pos[2] = last_src_y + 1;
        search_pos[3] = last_src_y + last_src_ystride;
        uint[] sad_arr = s_sad_arr ??= new uint[4];
        cpi.ppi.fn_ptr[bsize].sdx4df(src_y, src_ystride, search_pos,
                                     last_src_ystride, sad_arr);

        // C: unsigned int arithmetic (wraps).
        blk_sad = unchecked(blk_sad * 5) >> 3;
        return B(blk_sad < sad_arr[0] && blk_sad < sad_arr[1] &&
                 blk_sad < sad_arr[2] && blk_sad < sad_arr[3]);
    }

    // Grade the temporal variation of the source by comparing the current sb and
    // its collocated block in the last frame.
    internal static void av1_source_content_sb(AV1_COMP cpi, MACROBLOCK x, TileDataEnc tile_data,
                                               int mi_row, int mi_col)
    {
        if (cpi.last_source.y_width != cpi.source.y_width ||
            cpi.last_source.y_height != cpi.source.y_height)
            return;
        // [subset] CONFIG_AV1_HIGHBITDEPTH YV12_FLAG_HIGHBITDEPTH check: no high bitdepth.

        uint tmp_sse = 0;
        uint tmp_variance;
        BLOCK_SIZE bsize = cpi.common.seq_params.sb_size;
        Ptr<byte> src_y = cpi.source.y_buffer;
        int src_ystride = cpi.source.y_stride;
        int src_offset = src_ystride * (mi_row << 2) + (mi_col << 2);
        Ptr<byte> last_src_y = cpi.last_source.y_buffer;
        int last_src_ystride = cpi.last_source.y_stride;
        int last_src_offset = last_src_ystride * (mi_row << 2) + (mi_col << 2);
        ulong avg_source_sse_threshold_verylow = 10000;     // ~1.5*1.5*(64*64)
        ulong avg_source_sse_threshold_low0 = 100000;       // ~5*5*(64*64)
        ulong avg_source_sse_threshold_low1 = 36000;        // ~3*3*(64*64)

        ulong avg_source_sse_threshold_high = 1000000;  // ~15*15*(64*64)
        if (cpi.sf.rt_sf.increase_source_sad_thresh != 0)
        {
            avg_source_sse_threshold_high = avg_source_sse_threshold_high << 1;
            avg_source_sse_threshold_low0 = avg_source_sse_threshold_low0 << 1;
            avg_source_sse_threshold_verylow = avg_source_sse_threshold_verylow << 1;
        }
        ulong sum_sq_thresh = 10000;  // sum = sqrt(thresh / 64*64)) ~1.5
        src_y += src_offset;
        last_src_y += last_src_offset;
        tmp_variance = cpi.ppi.fn_ptr[bsize].vf(src_y, src_ystride, last_src_y,
                                                last_src_ystride, ref tmp_sse);
        // rd thresholds
        if (tmp_sse < avg_source_sse_threshold_low1)
            x.content_state_sb.source_sad_rd = kLowSad;

        // nonrd thresholds
        if (tmp_sse == 0)
        {
            x.content_state_sb.source_sad_nonrd = kZeroSad;
            return;
        }
        if (tmp_sse < avg_source_sse_threshold_verylow)
            x.content_state_sb.source_sad_nonrd = kVeryLowSad;
        else if (tmp_sse < avg_source_sse_threshold_low0)
            x.content_state_sb.source_sad_nonrd = kLowSad;
        else if (tmp_sse > avg_source_sse_threshold_high)
            x.content_state_sb.source_sad_nonrd = kHighSad;

        // Detect large lighting change.
        // Note: tmp_sse - tmp_variance = ((sum * sum) >> 12)
        // C: (tmp_sse - tmp_variance) is unsigned int arithmetic, then compared as uint64_t.
        if (tmp_variance < (tmp_sse >> 1) && unchecked(tmp_sse - tmp_variance) > sum_sq_thresh)
            x.content_state_sb.lighting_change = 1;
        if (unchecked(tmp_sse - tmp_variance) < (sum_sq_thresh >> 1))
            x.content_state_sb.low_sumdiff = 1;

        if (tmp_sse > ((avg_source_sse_threshold_high * 7) >> 3) &&
            x.content_state_sb.lighting_change == 0 && x.content_state_sb.low_sumdiff == 0)
            x.sb_force_fixed_part = 0;

        if (cpi.sf.rt_sf.use_rtc_tf == 0 || cpi.rc.high_source_sad != 0 ||
            cpi.rc.frame_source_sad > 20000 || cpi.svc.number_spatial_layers > 1)
            return;

        // In-place temporal filter. If psnr calculation is enabled, we store the
        // source for that.
        AV1_COMMON cm = cpi.common;
        // Calculate n*mean^2
        uint nmean2 = unchecked(tmp_sse - tmp_variance);
        int ac_q_step = av1_ac_quant_QTX(cm.quant_params.base_qindex, 0,
                                         cm.seq_params.bit_depth);
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        int avg_q_step = av1_ac_quant_QTX(p_rc.avg_frame_qindex[INTER_FRAME],
                                          0, cm.seq_params.bit_depth);

        uint threshold = (cpi.sf.rt_sf.use_rtc_tf == 1)
                             ? unchecked((uint)(clamp(avg_q_step, 250, 1000) * ac_q_step))
                             : unchecked((uint)(250 * ac_q_step));

        // TODO(yunqing): use a weighted sum instead of averaging in filtering.
        if (tmp_variance <= threshold && nmean2 <= 15)
        {
            // Check neighbor blocks. If neighbor blocks aren't low-motion blocks,
            // skip temporal filtering for this block.
            Ptr<MB_MODE_INFO> mi = new Ptr<MB_MODE_INFO>(
                cm.mi_params.mi_grid_base, get_mi_grid_idx(cm.mi_params, mi_row, mi_col));
            ref TileInfo tile_info = ref tile_data.tile_info;
            int is_neighbor_blocks_low_motion = check_neighbor_blocks(
                mi, cm.mi_params.mi_stride, in tile_info, mi_row, mi_col);
            if (is_neighbor_blocks_low_motion == 0) return;

            // Only consider 64x64 SB for now. Need to extend to 128x128 for large SB
            // size.
            // Test several nearby points. If non-zero mv exists, don't do temporal
            // filtering.
            int is_this_blk_low_motion = fast_detect_non_zero_motion(
                cpi, src_y, src_ystride, last_src_y, last_src_ystride, mi_row, mi_col);

            if (is_this_blk_low_motion == 0) return;

            int shift_x0 = 0, shift_x1 = cpi.source.subsampling_x;
            int shift_y0 = 0, shift_y1 = cpi.source.subsampling_y;
            byte h = block_size_high[bsize];
            byte w = block_size_wide[bsize];

            for (int plane = 0; plane < av1_num_planes(cm); ++plane)
            {
                int shift_x = plane != 0 ? shift_x1 : shift_x0;
                int shift_y = plane != 0 ? shift_y1 : shift_y0;
                Ptr<byte> src = cpi.source.buffers[plane];
                int src_stride = cpi.source.strides[plane != 0 ? 1 : 0];
                Ptr<byte> last_src = cpi.last_source.buffers[plane];
                int last_src_stride = cpi.last_source.strides[plane != 0 ? 1 : 0];
                src += src_stride * (mi_row << (2 - shift_y)) +
                       (mi_col << (2 - shift_x));
                last_src += last_src_stride * (mi_row << (2 - shift_y)) +
                            (mi_col << (2 - shift_x));

                for (int i = 0; i < (h >> shift_y); ++i)
                {
                    // [port] C: for (j) src[j] = (last_src[j] + src[j]) >> 1; vectorised with
                    // the exact floor average (a & b) + ((a ^ b) >> 1).
                    int bw = w >> shift_x;
                    Span<byte> s = src.AsSpan(bw);
                    ReadOnlySpan<byte> l = last_src.AsSpan(bw);
                    int j = 0;
                    for (; j + 32 <= bw; j += 32)
                    {
                        Vector256<byte> a = Vector256.Create<byte>(s.Slice(j, 32));
                        Vector256<byte> b = Vector256.Create<byte>(l.Slice(j, 32));
                        ((a & b) + Vector256.ShiftRightLogical(a ^ b, 1)).CopyTo(s.Slice(j, 32));
                    }
                    for (; j < bw; ++j) s[j] = (byte)((l[j] + s[j]) >> 1);
                    src += src_stride;
                    last_src += last_src_stride;
                }
            }
        }
    }

    // [subset] av1_reset_mbmi .. av1_get_rdmult_delta etc.: see the note above.
    // [subset] skip_cost_update, skip_mv_cost_update, skip_dv_cost_update: only reached for
    // the SBROW_SET / SBROW / SB cost update levels (the subset uses TILE / OFF).

    // Update the rate costs of some symbols according to the frequency directed
    // by speed features
    internal static void av1_set_cost_upd_freq(AV1_COMP cpi, ThreadData td,
                                               in TileInfo tile_info, int mi_row,
                                               int mi_col)
    {
        AV1_COMMON cm = cpi.common;

        if (cm.features.disable_cdf_update)
        {
            return;
        }

        switch (cpi.sf.inter_sf.coeff_cost_upd_level)
        {
            case INTERNAL_COST_UPD_OFF:
            case INTERNAL_COST_UPD_TILE:  // Tile level
                break;
            case INTERNAL_COST_UPD_SBROW_SET:  // SB row set level in tile
            case INTERNAL_COST_UPD_SBROW:      // SB row level in tile
            case INTERNAL_COST_UPD_SB:         // SB level
                // [subset] skip_cost_update / av1_fill_coeff_costs.
                throw new NotSupportedException("coeff cost update below tile level is not in the subset");
            default: Debug.Assert(false); break;
        }

        switch (cpi.sf.inter_sf.mode_cost_upd_level)
        {
            case INTERNAL_COST_UPD_OFF:
            case INTERNAL_COST_UPD_TILE:  // Tile level
                break;
            case INTERNAL_COST_UPD_SBROW_SET:  // SB row set level in tile
            case INTERNAL_COST_UPD_SBROW:      // SB row level in tile
            case INTERNAL_COST_UPD_SB:         // SB level
                // [subset] skip_cost_update / av1_fill_mode_rates.
                throw new NotSupportedException("mode cost update below tile level is not in the subset");
            default: Debug.Assert(false); break;
        }

        switch (cpi.sf.inter_sf.mv_cost_upd_level)
        {
            case INTERNAL_COST_UPD_OFF:
            case INTERNAL_COST_UPD_TILE:  // Tile level
                break;
            case INTERNAL_COST_UPD_SBROW_SET:  // SB row set level in tile
            case INTERNAL_COST_UPD_SBROW:      // SB row level in tile
            case INTERNAL_COST_UPD_SB:         // SB level
                // [subset] skip_mv_cost_update / av1_fill_mv_costs.
                throw new NotSupportedException("mv cost update below tile level is not in the subset");
            default: Debug.Assert(false); break;
        }

        switch (cpi.sf.intra_sf.dv_cost_upd_level)
        {
            case INTERNAL_COST_UPD_OFF:
            case INTERNAL_COST_UPD_TILE:  // Tile level
                break;
            case INTERNAL_COST_UPD_SBROW_SET:  // SB row set level in tile
            case INTERNAL_COST_UPD_SBROW:      // SB row level in tile
            case INTERNAL_COST_UPD_SB:         // SB level
                // [subset] skip_dv_cost_update / av1_fill_dv_costs.
                throw new NotSupportedException("dv cost update below tile level is not in the subset");
            default: Debug.Assert(false); break;
        }
    }

    internal static void av1_dealloc_src_diff_buf(MACROBLOCK mb, int num_planes)
    {
        for (int plane = 0; plane < num_planes; ++plane)
        {
            mb.plane[plane].src_diff = Ptr<short>.Null;
        }
    }

    internal static void av1_alloc_src_diff_buf(AV1_COMMON cm, MACROBLOCK mb)
    {
        int num_planes = av1_num_planes(cm);
        for (int plane = 0; plane < num_planes; ++plane)
        {
            Debug.Assert(mb.plane[plane].src_diff.IsNull);
        }
        for (int plane = 0; plane < num_planes; ++plane)
        {
            int subsampling_xy =
                plane != 0 ? cm.seq_params.subsampling_x + cm.seq_params.subsampling_y
                           : 0;
            int sb_size = MAX_SB_SQUARE >> subsampling_xy;
            mb.plane[plane].src_diff = new short[sb_size];
        }
    }
}
