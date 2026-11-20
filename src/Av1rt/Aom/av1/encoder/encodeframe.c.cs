// Port of libaom av1/encoder/encodeframe.c
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Av1rt.Aom;

internal static partial class encodeframe
{
    // This is used as a reference when computing the source variance for the
    //  purposes of activity masking.
    // Eventually this should be replaced by custom no-reference routines,
    //  which will be faster.
    private static readonly byte[] AV1_VAR_OFFS = new_filled_var_offs();

    private static byte[] new_filled_var_offs()
    {
        byte[] a = new byte[MAX_SB_SIZE];
        Array.Fill(a, (byte)128);
        return a;
    }

    // [subset] AV1_HIGH_VAR_OFFS_8/10/12: CONFIG_AV1_HIGHBITDEPTH == 0.

    // For the given bit depth, returns a constant array used to assist the
    // calculation of source block variance, which will then be used to decide
    // adaptive quantizers.
    private static Ptr<byte> get_var_offs(int use_hbd, int bd)
    {
        // [subset] CONFIG_AV1_HIGHBITDEPTH == 0.
        Debug.Assert(use_hbd == 0);
        Debug.Assert(bd == 8);
        return AV1_VAR_OFFS;
    }

    internal static void av1_init_rtc_counters(MACROBLOCK x)
    {
        av1_init_cyclic_refresh_counters(x);
        x.cnt_zeromv = 0;
        x.sb_col_scroll = 0;
        x.sb_row_scroll = 0;
    }

    internal static void av1_accumulate_rtc_counters(AV1_COMP cpi, MACROBLOCK x)
    {
        if (cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ)
            av1_accumulate_cyclic_refresh_counters(cpi.cyclic_refresh, x);
        cpi.rc.cnt_zeromv += x.cnt_zeromv;
        cpi.rc.num_col_blscroll_last_tl0 += x.sb_col_scroll;
        cpi.rc.num_row_blscroll_last_tl0 += x.sb_row_scroll;
    }

    internal static uint av1_get_perpixel_variance(AV1_COMP cpi,
                                                   MACROBLOCKD xd,
                                                   in buf_2d @ref,
                                                   BLOCK_SIZE bsize, int plane,
                                                   int use_hbd)
    {
        int subsampling_x = xd.plane[plane].subsampling_x;
        int subsampling_y = xd.plane[plane].subsampling_y;
        BLOCK_SIZE plane_bsize =
            get_plane_block_size(bsize, subsampling_x, subsampling_y);
        uint sse = 0;
        uint var = cpi.ppi.fn_ptr[plane_bsize].vf(
            @ref.buf, @ref.stride, get_var_offs(use_hbd, xd.bd), 0, ref sse);
        return ROUND_POWER_OF_TWO(var, num_pels_log2_lookup[plane_bsize]);
    }

    internal static uint av1_get_perpixel_variance_facade(AV1_COMP cpi,
                                                          MACROBLOCKD xd,
                                                          in buf_2d @ref,
                                                          BLOCK_SIZE bsize, int plane)
    {
        int use_hbd = is_cur_buf_hbd(xd);
        return av1_get_perpixel_variance(cpi, xd, in @ref, bsize, plane, use_hbd);
    }

    internal static void av1_setup_src_planes(MACROBLOCK x, YV12_BUFFER_CONFIG src,
                                              int mi_row, int mi_col, int num_planes,
                                              BLOCK_SIZE bsize)
    {
        // Set current frame pointer.
        x.e_mbd.cur_buf = src;

        // We use AOMMIN(num_planes, MAX_MB_PLANE) instead of num_planes to quiet
        // the static analysis warnings.
        for (int i = 0; i < AOMMIN(num_planes, MAX_MB_PLANE); i++)
        {
            int is_uv = B(i > 0);
            setup_pred_plane(
                ref x.plane[i].src, bsize, src.buffers[i], src.crop_widths[is_uv],
                src.crop_heights[is_uv], src.strides[is_uv], mi_row, mi_col, null,
                x.e_mbd.plane[i].subsampling_x, x.e_mbd.plane[i].subsampling_y);
        }
    }

    // [subset] setup_delta_q, av1_get_q_for_deltaq_objective callers, init_ref_frame_space,
    // adjust_rdmult_tpl_model, get_estimated_pred, perform_one_partition_pass,
    // sb_qp_sweep*, encode_rd_sb and init_encode_rd_sb: !CONFIG_REALTIME_ONLY or the rd
    // (non nonrd_pick_mode) superblock path, never executed in the subset.

    /*!\brief Encode a superblock (minimal RD search involved)
     *
     * Encodes the superblock by a pre-determined partition pattern, only minor
     * rd-based searches are allowed to adjust the initial pattern. It is only used
     * by realtime encoding.
     */
    private static void encode_nonrd_sb(AV1_COMP cpi, ThreadData td,
                                        TileDataEnc tile_data, ref Ptr<TokenExtra> tp,
                                        int mi_row, int mi_col,
                                        int seg_skip)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCK x = td.mb;
        SPEED_FEATURES sf = cpi.sf;
        ref TileInfo tile_info = ref tile_data.tile_info;
        Ptr<MB_MODE_INFO> mi = new Ptr<MB_MODE_INFO>(
            cm.mi_params.mi_grid_base, get_mi_grid_idx(cm.mi_params, mi_row, mi_col));
        BLOCK_SIZE sb_size = cm.seq_params.sb_size;
        PC_TREE pc_root = td.pc_root;

        // [subset] setup_delta_q_nonrd (delta_q_present_flag): !CONFIG_REALTIME_ONLY.
        // [subset] CONFIG_RT_ML_PARTITIONING: not configured.
        // Set the partition
        if (sf.part_sf.partition_search_type == FIXED_PARTITION || seg_skip != 0 ||
            (sf.rt_sf.use_fast_fixed_part != 0 && x.sb_force_fixed_part == 1 &&
             (frame_is_intra_only(cm) == 0 &&
              (cpi.ppi.use_svc == 0 ||
               cpi.svc.layer_context[cpi.svc.temporal_layer_id].is_key_frame == 0))))
        {
            // set a fixed-size partition
            av1_set_offsets(cpi, in tile_info, x, mi_row, mi_col, sb_size);
            BLOCK_SIZE bsize_select = sf.part_sf.fixed_partition_size;
            if (sf.rt_sf.use_fast_fixed_part != 0 &&
                x.content_state_sb.source_sad_nonrd < kLowSad)
            {
                bsize_select = cm.seq_params.sb_size;
            }
            if (cpi.sf.rt_sf.skip_encoding_non_reference_slide_change != 0 &&
                cpi.rc.high_source_sad != 0 && cpi.ppi.rtc_ref.non_reference_frame != 0)
            {
                bsize_select = cm.seq_params.sb_size;
                x.force_zeromv_skip_for_sb = 1;
            }
            BLOCK_SIZE bsize = seg_skip != 0 ? sb_size : bsize_select;
            if (x.content_state_sb.source_sad_nonrd > kZeroSad)
                x.force_color_check_block_level = 1;
            av1_set_fixed_partitioning(cpi, in tile_info, mi, mi_row, mi_col, bsize);
        }
        else if (sf.part_sf.partition_search_type == VAR_BASED_PARTITION)
        {
            // set a variance-based partition
            av1_set_offsets(cpi, in tile_info, x, mi_row, mi_col, sb_size);
            av1_choose_var_based_partitioning(cpi, in tile_info, td, x, mi_row, mi_col);
        }
        Debug.Assert(sf.part_sf.partition_search_type == FIXED_PARTITION || seg_skip != 0 ||
                     sf.part_sf.partition_search_type == VAR_BASED_PARTITION);
        set_cb_offsets(td.mb.cb_offset, 0, 0);

        // Initialize the flag to skip cdef to 1.
        if (sf.rt_sf.skip_cdef_sb != 0)
        {
            int block64_in_sb = (sb_size == BLOCK_128X128) ? 2 : 1;
            // If 128x128 block is used, we need to set the flag for all 4 64x64 sub
            // "blocks".
            for (int r = 0; r < block64_in_sb; ++r)
            {
                int mi_row_sub = mi_row + r * MI_SIZE_64X64;
                if (mi_row_sub >= cm.mi_params.mi_rows) continue;
                for (int c = 0; c < block64_in_sb; ++c)
                {
                    int mi_col_sub = mi_col + c * MI_SIZE_64X64;
                    if (mi_col_sub >= cm.mi_params.mi_cols) continue;
                    int alloc_mi_idx =
                        get_alloc_mi_idx(cm.mi_params, mi_row_sub, mi_col_sub);
                    cm.mi_params.mi_alloc[alloc_mi_idx].cdef_strength = 1;
                }
            }
        }

        // [subset] CONFIG_COLLECT_COMPONENT_TIMING: not configured.
        av1_nonrd_use_partition(cpi, td, tile_data, mi, ref tp, mi_row, mi_col, sb_size,
                                pc_root);
    }

    // Check if the cost update of symbols mode, coeff and dv are tile or off.
    private static int is_mode_coeff_dv_upd_freq_tile_or_off(AV1_COMP cpi)
    {
        INTER_MODE_SPEED_FEATURES inter_sf = cpi.sf.inter_sf;

        return B(inter_sf.coeff_cost_upd_level <= INTERNAL_COST_UPD_TILE &&
                 inter_sf.mode_cost_upd_level <= INTERNAL_COST_UPD_TILE &&
                 cpi.sf.intra_sf.dv_cost_upd_level <= INTERNAL_COST_UPD_TILE);
    }

    // When row-mt is enabled and cost update frequencies are set to off/tile,
    // processing of current SB can start even before processing of top-right SB
    // is finished. This function checks if it is sufficient to wait for top SB
    // to finish processing before current SB starts processing.
    private static int delay_wait_for_top_right_sb(AV1_COMP cpi)
    {
        MODE mode = cpi.oxcf.mode;
        if (mode == GOOD) return 0;

        if (mode == ALLINTRA)
            return is_mode_coeff_dv_upd_freq_tile_or_off(cpi);
        else if (mode == REALTIME)
            return B(is_mode_coeff_dv_upd_freq_tile_or_off(cpi) != 0 &&
                     cpi.sf.inter_sf.mv_cost_upd_level <= INTERNAL_COST_UPD_TILE);
        else
            return 0;
    }

    /*!\brief Calculate source SAD at superblock level using 64x64 block source SAD
     */
    private static ulong get_sb_source_sad(AV1_COMP cpi, int mi_row,
                                           int mi_col)
    {
        if (cpi.src_sad_blk_64x64.IsNull) return ulong.MaxValue;

        AV1_COMMON cm = cpi.common;
        int blk_64x64_in_mis = (cm.seq_params.sb_size == BLOCK_128X128)
                                   ? (cm.seq_params.mib_size >> 1)
                                   : cm.seq_params.mib_size;
        int num_blk_64x64_cols =
            (cm.mi_params.mi_cols + blk_64x64_in_mis - 1) / blk_64x64_in_mis;
        int num_blk_64x64_rows =
            (cm.mi_params.mi_rows + blk_64x64_in_mis - 1) / blk_64x64_in_mis;
        int blk_64x64_col_index = mi_col / blk_64x64_in_mis;
        int blk_64x64_row_index = mi_row / blk_64x64_in_mis;
        ulong curr_sb_sad = ulong.MaxValue;
        // Avoid the border as sad_blk_64x64 may not be set for the border
        // in the scene detection.
        if ((blk_64x64_row_index >= num_blk_64x64_rows - 1) ||
            (blk_64x64_col_index >= num_blk_64x64_cols - 1))
        {
            return curr_sb_sad;
        }
        Ptr<ulong> src_sad_blk_64x64_data =
            cpi.src_sad_blk_64x64 + (blk_64x64_col_index +
                                     blk_64x64_row_index * num_blk_64x64_cols);
        if (cm.seq_params.sb_size == BLOCK_128X128)
        {
            // Calculate SB source SAD by accumulating source SAD of 64x64 blocks in the
            // superblock
            curr_sb_sad = unchecked(src_sad_blk_64x64_data[0] + src_sad_blk_64x64_data[1] +
                                    src_sad_blk_64x64_data[num_blk_64x64_cols] +
                                    src_sad_blk_64x64_data[num_blk_64x64_cols + 1]);
        }
        else if (cm.seq_params.sb_size == BLOCK_64X64)
        {
            curr_sb_sad = src_sad_blk_64x64_data[0];
        }
        return curr_sb_sad;
    }

    /*!\brief Determine whether grading content can be skipped based on sad stat
     */
    private static bool is_calc_src_content_needed(AV1_COMP cpi,
                                                   MACROBLOCK x, int mi_row,
                                                   int mi_col)
    {
        if (cpi.svc.spatial_layer_id < cpi.svc.number_spatial_layers - 1)
            return true;
        ulong curr_sb_sad = get_sb_source_sad(cpi, mi_row, mi_col);
        if (curr_sb_sad == ulong.MaxValue) return true;
        if (curr_sb_sad == 0)
        {
            x.content_state_sb.source_sad_nonrd = kZeroSad;
            return false;
        }
        AV1_COMMON cm = cpi.common;
        bool do_calc_src_content = true;

        if (cpi.oxcf.speed < 9) return do_calc_src_content;

        // TODO(yunqing): Tune/validate the thresholds for 128x128 SB size.
        if (AOMMIN(cm.width, cm.height) < 360)
        {
            // Derive Average 64x64 block source SAD from SB source SAD
            ulong avg_64x64_blk_sad =
                (cm.seq_params.sb_size == BLOCK_128X128) ? ((curr_sb_sad + 2) >> 2)
                                                         : curr_sb_sad;

            // The threshold is determined based on kLowSad and kHighSad threshold and
            // test results.
            ulong thresh_low = 15000;
            ulong thresh_high = 40000;

            if (cpi.sf.rt_sf.increase_source_sad_thresh != 0)
            {
                thresh_low = thresh_low << 1;
                thresh_high = thresh_high << 1;
            }

            if (avg_64x64_blk_sad > thresh_low && avg_64x64_blk_sad < thresh_high)
            {
                do_calc_src_content = false;
                // Note: set x->content_state_sb.source_sad_rd as well if this is extended
                // to RTC rd path.
                x.content_state_sb.source_sad_nonrd = kMedSad;
            }
        }

        return do_calc_src_content;
    }

    /*!\brief Determine whether grading content is needed based on sf and frame stat
     */
    // TODO(any): consolidate sfs to make interface cleaner
    private static void grade_source_content_sb(AV1_COMP cpi, MACROBLOCK x,
                                                TileDataEnc tile_data, int mi_row,
                                                int mi_col)
    {
        AV1_COMMON cm = cpi.common;
        if (cm.current_frame.frame_type == KEY_FRAME ||
            (cpi.ppi.use_svc != 0 &&
             cpi.svc.layer_context[cpi.svc.temporal_layer_id].is_key_frame != 0))
        {
            Debug.Assert(x.content_state_sb.source_sad_nonrd == kMedSad);
            Debug.Assert(x.content_state_sb.source_sad_rd == kMedSad);
            return;
        }
        bool calc_src_content = false;

        if (cpi.sf.rt_sf.source_metrics_sb_nonrd != 0)
        {
            // The frame average can round down to 0 for a small change, so check the
            // largest block sad too.
            if (cpi.sf.rt_sf.check_scene_detection == 0 || cpi.rc.frame_source_sad > 0 ||
                cpi.rc.max_block_source_sad > 0)
            {
                calc_src_content = is_calc_src_content_needed(cpi, x, mi_row, mi_col);
            }
            else
            {
                x.content_state_sb.source_sad_nonrd = kZeroSad;
            }
        }
        else if ((cpi.sf.rt_sf.var_part_based_on_qidx >= 1) &&
                 (cm.width * cm.height <= 352 * 288))
        {
            if (cpi.rc.frame_source_sad > 0)
                calc_src_content = true;
            else
                x.content_state_sb.source_sad_rd = kZeroSad;
        }
        if (calc_src_content)
            av1_source_content_sb(cpi, x, tile_data, mi_row, mi_col);
    }

    private static byte get_sb_segment_id(AV1_COMP cpi, int mi_row, int mi_col)
    {
        AV1_COMMON cm = cpi.common;
        segmentation seg = cm.seg;
        Ptr<byte> map =
            seg.update_map != 0 ? cpi.enc_seg.map : cm.last_frame_seg_map;
        if (map.IsNull) return 0;
        return get_segment_id(cm.mi_params, map, cm.seq_params.sb_size, mi_row,
                              mi_col);
    }

    // Returns true when skip segment superblocks of this frame may be copied from
    // their reference before they are coded.
    private static bool can_copy_skip_sbs(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        if (cm.seg.enabled == 0 || frame_is_intra_only(cm) != 0 ||
            cpi.sf.rt_sf.use_nonrd_pick_mode == 0)
            return false;

        // Segmentation without a skip segment, like cyclic refresh, has nothing to
        // copy, so this saves the segment lookups of the whole frame.
        for (int i = 0; i < MAX_SEGMENTS; ++i)
        {
            if (segfeature_active(cm.seg, (byte)i, SEG_LVL_SKIP) != 0) return true;
        }
        return false;
    }

    // [subset] get_skip_sb_copy_ref, copy_sb_span: only reached when a segment has
    // SEG_LVL_SKIP active (active maps / ROI skip), never in the subset.

    // Gets the segment id of each superblock of a tile row. The map of a
    // superblock only changes while it is coded, so the ids hold for the row.
    private static void get_sb_segment_ids_of_row(AV1_COMP cpi,
                                                  in TileInfo tile_info, int mi_row,
                                                  Ptr<byte> segment_ids)
    {
        int mib_size = cpi.common.seq_params.mib_size;
        for (int mi_col = tile_info.mi_col_start; mi_col < tile_info.mi_col_end;
             mi_col += mib_size)
        {
            segment_ids.Value = get_sb_segment_id(cpi, mi_row, mi_col);
            segment_ids++;
        }
    }

    // Fills the skip segment superblocks of a row from their reference before the
    // row is coded. Wide row copies are much faster than copying every 64x64 block
    // plane by plane, so encode_superblock() then skips their prediction.
    private static void copy_skip_sbs_of_row(AV1_COMP cpi, in TileInfo tile_info,
                                             int mi_row, Ptr<byte> segment_ids)
    {
        if (!can_copy_skip_sbs(cpi)) return;

        // [subset] the span copy loop (get_skip_sb_copy_ref / copy_sb_span): SEG_LVL_SKIP is
        // never active in the subset.
        throw new NotSupportedException("skip segment superblock copies are not in the subset");
    }

    // C: uint8_t sb_segment_ids[MAX_TILE_WIDTH >> MIN_SB_SIZE_LOG2] on the stack of
    // encode_sb_row (leaf scratch, PORTING.md §6).
    [ThreadStatic] private static byte[] s_sb_segment_ids;

    /*!\brief Encode a superblock row by breaking it into superblocks
     *
     * Do partition and mode search for an sb row: one row of superblocks filling up
     * the width of the current tile.
     */
    private static void encode_sb_row(AV1_COMP cpi, ThreadData td,
                                      TileDataEnc tile_data, int mi_row,
                                      ref Ptr<TokenExtra> tp)
    {
        AV1_COMMON cm = cpi.common;
        ref TileInfo tile_info = ref tile_data.tile_info;
        MultiThreadInfo mt_info = cpi.mt_info;
        AV1EncRowMultiThreadInfo enc_row_mt = mt_info.enc_row_mt;
        AV1EncRowMultiThreadSync row_mt_sync = tile_data.row_mt_sync;
        bool row_mt_enabled = mt_info.row_mt_enabled;
        MACROBLOCK x = td.mb;
        MACROBLOCKD xd = x.e_mbd;
        int sb_cols_in_tile = av1_get_sb_cols_in_tile(cm, in tile_info);
        BLOCK_SIZE sb_size = cm.seq_params.sb_size;
        int mib_size = cm.seq_params.mib_size;
        int mib_size_log2 = cm.seq_params.mib_size_log2;
        int sb_row = (mi_row - tile_info.mi_row_start) >> mib_size_log2;
        int use_nonrd_mode = cpi.sf.rt_sf.use_nonrd_pick_mode;

        // [subset] CONFIG_COLLECT_COMPONENT_TIMING: not configured.

        // Initialize the left context for the new SB row
        av1_zero_left_context(xd);

        // Reset delta for quantizer and loof filters at the beginning of every tile
        if (mi_row == tile_info.mi_row_start || row_mt_enabled)
        {
            if (cpi.cb_delta_rdmult_enabled != 0)
                xd.current_base_qindex = cm.quant_params.base_qindex;
            if (cm.delta_q_info.delta_lf_present_flag != 0)
            {
                // [subset] av1_reset_loop_filter_delta: delta lf needs deltaq_mode != 0.
                throw new NotSupportedException("delta lf is not in the subset");
            }
        }

        reset_thresh_freq_fact(x);

        segmentation seg = cm.seg;
        byte[] sb_segment_ids = s_sb_segment_ids ??= new byte[MAX_TILE_WIDTH >> MIN_SB_SIZE_LOG2];
        sb_segment_ids.AsSpan().Clear();
        Debug.Assert(sb_cols_in_tile <= sb_segment_ids.Length);
        if (seg.enabled != 0)
        {
            get_sb_segment_ids_of_row(cpi, in tile_info, mi_row, sb_segment_ids);
            copy_skip_sbs_of_row(cpi, in tile_info, mi_row, sb_segment_ids);
        }

        // Code each SB in the row
        for (int mi_col = tile_info.mi_col_start, sb_col_in_tile = 0;
             mi_col < tile_info.mi_col_end; mi_col += mib_size, sb_col_in_tile++)
        {
            // In realtime/allintra mode and when frequency of cost updates is off/tile,
            // wait for the top superblock to finish encoding. Otherwise, wait for the
            // top-right superblock to finish encoding.
            enc_row_mt.sync_read_ptr(
                row_mt_sync, sb_row, sb_col_in_tile - delay_wait_for_top_right_sb(cpi));

            // CONFIG_MULTITHREAD
            if (row_mt_enabled)
            {
                pthread_mutex_lock(enc_row_mt.mutex_);
                bool row_mt_exit = enc_row_mt.row_mt_exit;
                pthread_mutex_unlock(enc_row_mt.mutex_);
                // Exit in case any worker has encountered an error.
                if (row_mt_exit) return;
            }

            int update_cdf = B(tile_data.allow_update_cdf != 0 && row_mt_enabled);
            if (update_cdf != 0 && (tile_info.mi_row_start != mi_row))
            {
                // [subset] restoring / averaging the row CDF context (x->row_ctx,
                // av1_avg_cdf_symbols): allow_update_cdf is 0 in the subset (see
                // av1_init_tile_data, delay_wait_for_top_right_sb() is 1).
                throw new NotSupportedException("row-mt cdf update is not in the subset");
            }

            // Update the rate cost tables for some symbols
            av1_set_cost_upd_freq(cpi, td, in tile_info, mi_row, mi_col);

            // Reset color coding related parameters
            av1_zero(x.color_sensitivity_sb);
            av1_zero(x.color_sensitivity_sb_g);
            av1_zero(x.color_sensitivity_sb_alt);
            av1_zero(x.color_sensitivity);
            x.content_state_sb.source_sad_nonrd = kMedSad;
            x.content_state_sb.source_sad_rd = kMedSad;
            x.content_state_sb.lighting_change = 0;
            x.content_state_sb.low_sumdiff = 0;
            x.force_zeromv_skip_for_sb = 0;
            x.sb_me_block = 0;
            x.sb_me_partition = 0;
            x.sb_me_mv.as_int = 0;
            x.sb_force_fixed_part = 1;
            // Superblocks coded without the variance partitioning must not use the
            // low variance flags of an earlier superblock.
            av1_zero(x.part_search_info.variance_low);
            x.color_palette_thresh = 64;
            x.force_color_check_block_level = 0;
            x.nonrd_prune_ref_frame_search =
                cpi.sf.rt_sf.nonrd_prune_ref_frame_search;

            if (cpi.oxcf.mode == ALLINTRA)
            {
                x.intra_sb_rdmult_modifier = 128;
            }

            xd.cur_frame_force_integer_mv = B(cm.features.cur_frame_force_integer_mv);
            x.source_variance = uint.MaxValue;
            td.mb.cb_coef_buff = av1_get_cb_coeff_buffer(cpi, mi_row, mi_col);

            // Get segment id and skip flag
            int seg_skip = 0;
            x.sb_copied_ref = NONE_FRAME;
            if (seg.enabled != 0)
            {
                byte segment_id = sb_segment_ids[sb_col_in_tile];
                seg_skip = segfeature_active(seg, segment_id, SEG_LVL_SKIP);
                if (seg_skip != 0 && frame_is_intra_only(cm) == 0 && use_nonrd_mode != 0)
                {
                    // [subset] x->sb_copied_ref = get_skip_sb_copy_ref(cm, segment_id):
                    // SEG_LVL_SKIP is never active in the subset.
                    throw new NotSupportedException("skip segments are not in the subset");
                }
            }

            produce_gradients_for_sb(cpi, x, sb_size, mi_row, mi_col);

            init_src_var_info_of_4x4_sub_blocks(cpi, x.src_var_info_of_4x4_sub_blocks,
                                                sb_size);

            // Grade the temporal variation of the sb, the grade will be used to decide
            // fast mode search strategy for coding blocks
            if (seg_skip == 0) grade_source_content_sb(cpi, x, tile_data, mi_row, mi_col);

            // encode the superblock
            if (use_nonrd_mode != 0)
            {
                encode_nonrd_sb(cpi, td, tile_data, ref tp, mi_row, mi_col, seg_skip);
            }
            else
            {
                // [subset] encode_rd_sb: use_nonrd_pick_mode is 1 at speed 11.
                throw new NotSupportedException("encode_rd_sb is not in the subset");
            }

            // Update the top-right context in row_mt coding
            if (update_cdf != 0 && (tile_info.mi_row_end > (mi_row + mib_size)))
            {
                // [subset] x->row_ctx[] = *xd->tile_ctx: allow_update_cdf is 0 (see above).
                throw new NotSupportedException("row-mt cdf update is not in the subset");
            }
            enc_row_mt.sync_write_ptr(row_mt_sync, sb_row, sb_col_in_tile,
                                      sb_cols_in_tile);
        }

        // [subset] CONFIG_COLLECT_COMPONENT_TIMING: not configured.
    }

    private static void init_encode_frame_mb_context(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        int num_planes = av1_num_planes(cm);
        MACROBLOCK x = cpi.td.mb;
        MACROBLOCKD xd = x.e_mbd;

        // Copy data over into macro block data structures.
        av1_setup_src_planes(x, cpi.source, 0, 0, num_planes,
                             cm.seq_params.sb_size);

        av1_setup_block_planes(xd, cm.seq_params.subsampling_x,
                               cm.seq_params.subsampling_y, num_planes);
    }

    internal static void av1_alloc_tile_data(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        int tile_cols = cm.tiles.cols;
        int tile_rows = cm.tiles.rows;

        av1_row_mt_mem_dealloc(cpi);

        cpi.tile_data = null;  // aom_free
        cpi.allocated_tiles = 0;

        // PORT-NOTE: aom_memalign leaves the TileDataEnc memory uninitialised; every field
        // that C reads is written (here and in av1_init_tile_data) before it is read.
        cpi.tile_data = new_array<TileDataEnc>(tile_cols * tile_rows);

        cpi.allocated_tiles = tile_cols * tile_rows;
        for (int tile_row = 0; tile_row < tile_rows; ++tile_row)
        {
            for (int tile_col = 0; tile_col < tile_cols; ++tile_col)
            {
                int tile_index = tile_row * tile_cols + tile_col;
                TileDataEnc this_tile = cpi.tile_data[tile_index];
                this_tile.row_mt_sync.Clear();
                this_tile.row_ctx = null;
            }
        }
    }

    internal static void av1_init_tile_data(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        int tile_cols = cm.tiles.cols;
        int tile_rows = cm.tiles.rows;
        int tile_col, tile_row;
        TokenInfo token_info = cpi.token_info;

        if (is_stat_generation_stage(cpi) == 0 &&
            cm.features.allow_screen_content_tools)
        {
            // [subset] palette token buffers (get_token_alloc / alloc_token_info):
            // allow_screen_content_tools is 0 for non-screen realtime content.
            throw new NotSupportedException("screen content tools are not in the subset");
        }

        for (tile_row = 0; tile_row < tile_rows; ++tile_row)
        {
            for (tile_col = 0; tile_col < tile_cols; ++tile_col)
            {
                TileDataEnc tile_data =
                    cpi.tile_data[tile_row * tile_cols + tile_col];
                ref TileInfo tile_info = ref tile_data.tile_info;
                av1_tile_init(ref tile_info, cm, tile_row, tile_col);
                tile_data.firstpass_top_mv = kZeroMv;
                tile_data.abs_sum_level = 0;

                if (is_token_info_allocated(token_info))
                {
                    // [subset] tile_tok / tplist setup: tokens are only allocated with
                    // allow_screen_content_tools (see above).
                    throw new NotSupportedException("palette tokens are not in the subset");
                }
                tile_data.allow_update_cdf = (byte)B(cm.tiles.large_scale == 0);
                tile_data.allow_update_cdf = (byte)B(tile_data.allow_update_cdf != 0 &&
                                                     !cm.features.disable_cdf_update &&
                                                     delay_wait_for_top_right_sb(cpi) == 0);
                tile_data.tctx.CopyFrom(cm.fc);
            }
        }
    }

    // Populate the start palette token info prior to encoding an SB row.
    private static void get_token_start(AV1_COMP cpi, in TileInfo tile_info,
                                        int tile_row, int tile_col, int mi_row,
                                        ref Ptr<TokenExtra> tp)
    {
        TokenInfo token_info = cpi.token_info;
        if (!is_token_info_allocated(token_info)) return;

        // [subset] get_start_tok / tplist start: palette tokens are never allocated in the
        // subset.
        throw new NotSupportedException("palette tokens are not in the subset");
    }

    // Populate the token count after encoding an SB row.
    private static void populate_token_count(AV1_COMP cpi,
                                             in TileInfo tile_info, int tile_row,
                                             int tile_col, int mi_row,
                                             Ptr<TokenExtra> tok)
    {
        TokenInfo token_info = cpi.token_info;
        if (!is_token_info_allocated(token_info)) return;

        // [subset] tplist count: palette tokens are never allocated in the subset.
        throw new NotSupportedException("palette tokens are not in the subset");
    }

    /*!\brief Encode a superblock row
     */
    internal static void av1_encode_sb_row(AV1_COMP cpi, ThreadData td, int tile_row,
                                           int tile_col, int mi_row)
    {
        AV1_COMMON cm = cpi.common;
        int tile_cols = cm.tiles.cols;
        TileDataEnc this_tile = cpi.tile_data[tile_row * tile_cols + tile_col];
        ref TileInfo tile_info = ref this_tile.tile_info;
        Ptr<TokenExtra> tok = Ptr<TokenExtra>.Null;

        get_token_start(cpi, in tile_info, tile_row, tile_col, mi_row, ref tok);

        encode_sb_row(cpi, td, this_tile, mi_row, ref tok);

        populate_token_count(cpi, in tile_info, tile_row, tile_col, mi_row, tok);
    }

    /*!\brief Encode a tile
     */
    internal static void av1_encode_tile(AV1_COMP cpi, ThreadData td, int tile_row,
                                         int tile_col)
    {
        AV1_COMMON cm = cpi.common;
        TileDataEnc this_tile =
            cpi.tile_data[tile_row * cm.tiles.cols + tile_col];
        ref TileInfo tile_info = ref this_tile.tile_info;

        if (cpi.sf.rt_sf.use_nonrd_pick_mode == 0) av1_inter_mode_data_init(this_tile);

        av1_zero_above_context(cm, td.mb.e_mbd, tile_info.mi_col_start,
                               tile_info.mi_col_end, tile_row);
        av1_init_above_context(cm.above_contexts, av1_num_planes(cm), tile_row,
                               td.mb.e_mbd);

        // [subset] cfl_init: !CONFIG_REALTIME_ONLY.

        // [subset] av1_crc32c_calculator_init(&mb_rd_record->crc_calculator): the
        // mb_rd_record is dropped from MACROBLOCK (td->mb.txfm_search_info.mb_rd_record is
        // always NULL with use_nonrd_pick_mode).

        for (int mi_row = tile_info.mi_row_start; mi_row < tile_info.mi_row_end;
             mi_row += cm.seq_params.mib_size)
        {
            av1_encode_sb_row(cpi, td, tile_row, tile_col, mi_row);
        }
        this_tile.abs_sum_level = td.abs_sum_level;
    }

    /*!\brief Break one frame into tiles and encode the tiles
     */
    private static void encode_tiles(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        int tile_cols = cm.tiles.cols;
        int tile_rows = cm.tiles.rows;
        int tile_col, tile_row;

        MACROBLOCK mb = cpi.td.mb;
        Debug.Assert(cpi.tile_data != null || cpi.allocated_tiles == 0);
        if (cpi.allocated_tiles != tile_cols * tile_rows) av1_alloc_tile_data(cpi);

        av1_init_tile_data(cpi);
        av1_alloc_mb_data(cpi, mb);

        for (tile_row = 0; tile_row < tile_rows; ++tile_row)
        {
            for (tile_col = 0; tile_col < tile_cols; ++tile_col)
            {
                TileDataEnc this_tile =
                    cpi.tile_data[tile_row * cm.tiles.cols + tile_col];
                cpi.td.intrabc_used = 0;
                cpi.td.deltaq_used = 0;
                cpi.td.abs_sum_level = 0;
                cpi.td.mb.e_mbd.tile_ctx = this_tile.tctx;
                cpi.td.mb.tile_pb_ctx = this_tile.tctx;
                av1_init_rtc_counters(cpi.td.mb);
                cpi.td.mb.palette_pixels = 0;
                av1_encode_tile(cpi, cpi.td, tile_row, tile_col);
                if (frame_is_intra_only(cpi.common) == 0)
                    av1_accumulate_rtc_counters(cpi, cpi.td.mb);
                cpi.palette_pixel_num += cpi.td.mb.palette_pixels;
                cpi.intrabc_used |= cpi.td.intrabc_used;
                cpi.deltaq_used |= cpi.td.deltaq_used;
            }
        }

        av1_dealloc_mb_data(mb, av1_num_planes(cm));
    }

    // Set the relative distance of a reference frame w.r.t. current frame
    private static void set_rel_frame_dist(
        AV1_COMMON cm, RefFrameDistanceInfo ref_frame_dist_info,
        int ref_frame_flags)
    {
        MV_REFERENCE_FRAME ref_frame;
        int min_past_dist = int.MaxValue, min_future_dist = int.MaxValue;
        ref_frame_dist_info.nearest_past_ref = (sbyte)NONE_FRAME;
        ref_frame_dist_info.nearest_future_ref = (sbyte)NONE_FRAME;
        for (ref_frame = LAST_FRAME; ref_frame <= ALTREF_FRAME; ++ref_frame)
        {
            ref_frame_dist_info.ref_relative_dist[ref_frame - LAST_FRAME] = 0;
            if ((ref_frame_flags & av1_ref_frame_flag_list[ref_frame]) != 0)
            {
                // C: the unsigned int order hints convert to av1_encoder_get_relative_dist's
                // int parameters.
                int dist = av1_encoder_get_relative_dist(
                    (int)cm.cur_frame.ref_display_order_hint[ref_frame - LAST_FRAME],
                    (int)cm.current_frame.display_order_hint);
                ref_frame_dist_info.ref_relative_dist[ref_frame - LAST_FRAME] = dist;
                // Get the nearest ref_frame in the past
                if (Math.Abs(dist) < min_past_dist && dist < 0)
                {
                    ref_frame_dist_info.nearest_past_ref = (sbyte)ref_frame;
                    min_past_dist = Math.Abs(dist);
                }
                // Get the nearest ref_frame in the future
                if (dist < min_future_dist && dist > 0)
                {
                    ref_frame_dist_info.nearest_future_ref = (sbyte)ref_frame;
                    min_future_dist = dist;
                }
            }
        }
    }

    private static int refs_are_one_sided(AV1_COMMON cm)
    {
        Debug.Assert(frame_is_intra_only(cm) == 0);

        int one_sided_refs = 1;
        int cur_display_order_hint = (int)cm.current_frame.display_order_hint;
        for (int @ref = LAST_FRAME; @ref <= ALTREF_FRAME; ++@ref)
        {
            RefCntBuffer buf = get_ref_frame_buf(cm, @ref);
            if (buf == null) continue;
            if (av1_encoder_get_relative_dist((int)buf.display_order_hint,
                                              cur_display_order_hint) > 0)
            {
                one_sided_refs = 0;  // bwd reference
                break;
            }
        }
        return one_sided_refs;
    }

    // [subset] get_skip_mode_ref_offsets: only reached when skip_mode_allowed, which needs
    // order hints (off in the subset).

    private static int check_skip_mode_enabled(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;

        av1_setup_skip_mode_allowed(cm);
        if (cm.current_frame.skip_mode_info.skip_mode_allowed == 0) return 0;

        // [subset] the reference pair distance checks: skip_mode_allowed needs
        // enable_order_hint, which is off in the subset.
        throw new NotSupportedException("skip mode is not in the subset");
    }

    private static void set_default_interp_skip_flags(
        AV1_COMMON cm, InterpSearchFlags interp_search_flags)
    {
        int num_planes = av1_num_planes(cm);
        interp_search_flags.default_interp_skip_flags =
            (num_planes == 1) ? INTERP_SKIP_LUMA_EVAL_CHROMA
                              : INTERP_SKIP_LUMA_SKIP_CHROMA;
    }

    private struct RefScoreData
    {
        // Scoring function for usefulness of references (the lower score, the more
        // useful)
        public int score;
        // Index in the reference buffer
        public int index;
    }

    // Comparison function to sort reference frames in ascending score order.
    private static int compare_score_data_asc(RefScoreData ra, RefScoreData rb)
    {
        int score_diff = ra.score - rb.score;
        if (score_diff != 0) return score_diff;

        return ra.index - rb.index;
    }

    // PORT-NOTE: qsort -> Span.Sort. The comparator is a total order (index breaks every
    // tie and the indices are distinct), so the sorted order is unique.
    private static readonly Comparison<RefScoreData> compare_score_data_asc_fn =
        compare_score_data_asc;

    private static readonly int[] num_single_ref_to_keep_lookup = { INTER_REFS_PER_FRAME, 5,
                                                                    3, 0, 0 };
    private static readonly int[] num_comp_ref_to_keep_lookup = { INTER_REFS_PER_FRAME, 3,
                                                                  0, 0 };

    // Determines whether a given reference frame is "good" based on temporal
    // distance and base_qindex. The "good" reference frames are not allowed to be
    // pruned by the speed feature "prune_single_ref" and "prune_comp_ref_frames"
    // at block level.
    private static void setup_keep_ref_frame_mask(AV1_COMP cpi)
    {
        int prune_single_ref = cpi.sf.inter_sf.prune_single_ref;
        int prune_comp_ref_frames = cpi.sf.inter_sf.prune_comp_ref_frames;
        AV1_COMMON cm = cpi.common;
        cpi.keep_single_ref_frame_mask = 0;
        cpi.keep_comp_ref_frame_mask = 0;
        if (frame_is_intra_only(cm) != 0) return;

        Span<RefScoreData> ref_score_data = stackalloc RefScoreData[INTER_REFS_PER_FRAME];
        for (int i = 0; i < INTER_REFS_PER_FRAME; ++i)
        {
            ref_score_data[i].score = int.MaxValue;
            ref_score_data[i].index = i;
        }

        // Calculate score for each reference frame based on relative distance to
        // the current frame and its base_qindex. A lower score means that the
        // reference is potentially more useful.
        for (MV_REFERENCE_FRAME ref_frame = LAST_FRAME; ref_frame <= ALTREF_FRAME;
             ++ref_frame)
        {
            if ((cpi.ref_frame_flags & av1_ref_frame_flag_list[ref_frame]) != 0)
            {
                RefFrameDistanceInfo ref_frame_dist_info =
                    cpi.ref_frame_dist_info;
                RefCntBuffer buf = get_ref_frame_buf(cm, ref_frame);
                ref_score_data[ref_frame - LAST_FRAME].score =
                    Math.Abs(ref_frame_dist_info.ref_relative_dist[ref_frame - LAST_FRAME]) +
                    buf.base_qindex;
            }
        }

        ref_score_data.Sort(compare_score_data_asc_fn);

        // Decide the number of reference frames for which pruning via the speed
        // feature prune_single_ref is disallowed.
        // prune_single_ref = 0 => None of the 7 reference frames are pruned.
        // prune_single_ref = 1 => The best 5 reference frames are not pruned.
        // prune_single_ref = 2 => The best 3 reference frames are not pruned.
        // prune_single_ref = 3, 4 => All the 7 references are allowed to be pruned.
        Debug.Assert(prune_single_ref >= 0 && prune_single_ref <= 4);
        int num_single_ref_to_keep =
            num_single_ref_to_keep_lookup[prune_single_ref];
        for (int i = 0; i < num_single_ref_to_keep; ++i)
        {
            int idx = ref_score_data[i].index;
            cpi.keep_single_ref_frame_mask |= 1 << idx;
        }

        // Decide the number of reference frame pairs for which pruning via the speed
        // feature "prune_comp_ref_frames" is disallowed.
        // prune_comp_ref_frames = 0    => None of the allowed reference frame pairs
        //                                 are pruned.
        // prune_comp_ref_frames = 1    => The best 3 reference frame pairs are not
        //                                 allowed to be pruned, i.e, reference frame
        //                                 pairs with rank (1, 2), (1, 3), (2, 3) are
        //                                 not  pruned.
        // prune_comp_ref_frames = 2, 3 => All the reference frame pairs are allowed
        //                                 to be pruned.
        Debug.Assert(prune_comp_ref_frames >= 0 && prune_comp_ref_frames <= 3);
        int num_comp_ref_to_keep =
            num_comp_ref_to_keep_lookup[prune_comp_ref_frames];
        for (int i = 0; i < num_comp_ref_to_keep; ++i)
        {
            int idx = ref_score_data[i].index;
            cpi.keep_comp_ref_frame_mask |= 1 << idx;
        }
    }

    private static void setup_prune_ref_frame_mask(AV1_COMP cpi)
    {
        if ((!cpi.oxcf.ref_frm_cfg.enable_onesided_comp ||
             cpi.sf.inter_sf.disable_onesided_comp != 0) &&
            cpi.all_one_sided_refs != 0)
        {
            // Disable all compound references
            cpi.prune_ref_frame_mask = (1 << MODE_CTX_REF_FRAMES) - (1 << REF_FRAMES);
        }
        else if (cpi.sf.rt_sf.use_nonrd_pick_mode == 0 &&
                 cpi.sf.inter_sf.selective_ref_frame >= 1)
        {
            // [subset] selective_ref_frame pruning of compound pairs: rd mode search only.
            throw new NotSupportedException("rd selective_ref_frame pruning is not in the subset");
        }
    }

    // [subset] allow_deltaq_mode, enable_delta_q: deltaq_mode is 0.

    private static int disable_deltaq_for_intl_arfs(AV1_COMP cpi)
    {
        if (cpi.oxcf.mode == GOOD && is_stat_consumption_stage_twopass(cpi) != 0 &&
            cpi.oxcf.q_cfg.deltaq_mode == DELTA_Q_OBJECTIVE &&
            cpi.oxcf.algo_cfg.enable_tpl_model && cpi.oxcf.q_cfg.aq_mode == NO_AQ &&
            cpi.common.seg.enabled == 0 && cpi.roi.enabled == 0 && cpi.oxcf.sb_qp_sweep == 0 &&
            cpi.use_ducky_encode == 0)
        {
            return 1;
        }
        return 0;
    }

    private static int enable_delta_rdmult(AV1_COMP cpi)
    {
        if (disable_deltaq_for_intl_arfs(cpi) == 0)
            return cpi.common.delta_q_info.delta_q_present_flag;

        GF_GROUP gf_group = cpi.ppi.gf_group;
        return B(gf_group.update_type[cpi.gf_frame_index] != LF_UPDATE);
    }

    private const uint FORCE_ZMV_SKIP_128X128_BLK_DIFF = 10000;
    private const int FORCE_ZMV_SKIP_MAX_PER_PIXEL_DIFF = 4;

    // Populates block level thresholds for force zeromv-skip decision
    private static void populate_thresh_to_force_zeromv_skip(AV1_COMP cpi)
    {
        if (cpi.sf.rt_sf.part_early_exit_zeromv == 0) return;

        // Threshold for forcing zeromv-skip decision is as below:
        // For 128x128 blocks, threshold is 10000 and per pixel threshold is 0.6103.
        // For 64x64 blocks, threshold is 5000 and per pixel threshold is 1.221
        // allowing slightly higher error for smaller blocks.
        // Per Pixel Threshold of 64x64 block        Area of 64x64 block         1  1
        // ------------------------------------=sqrt(---------------------)=sqrt(-)=-
        // Per Pixel Threshold of 128x128 block      Area of 128x128 block       4  2
        // Thus, per pixel thresholds for blocks of size 32x32, 16x16,...  can be
        // chosen as 2.442, 4.884,.... As the per pixel error tends to be higher for
        // small blocks, the same is clipped to 4.
        uint thresh_exit_128x128_part = FORCE_ZMV_SKIP_128X128_BLK_DIFF;
        int num_128x128_pix =
            block_size_wide[BLOCK_128X128] * block_size_high[BLOCK_128X128];

        for (BLOCK_SIZE bsize = BLOCK_4X4; bsize < BLOCK_SIZES_ALL; bsize++)
        {
            int num_block_pix = block_size_wide[bsize] * block_size_high[bsize];

            // Calculate the threshold for zeromv-skip decision based on area of the
            // partition
            uint thresh_exit_part_blk =
                (uint)(thresh_exit_128x128_part *
                           Math.Sqrt((double)num_block_pix / num_128x128_pix) +
                       0.5);
            thresh_exit_part_blk = AOMMIN(
                thresh_exit_part_blk,
                (uint)(FORCE_ZMV_SKIP_MAX_PER_PIXEL_DIFF * num_block_pix));
            cpi.zeromv_skip_thresh_exit_part[bsize] = thresh_exit_part_blk;
        }
    }

    // [subset] free_block_hash_buffers, get_intrabc_hash_stride, get_intrabc_hash_region,
    // set_intrabc_hash_table_grid, get_max_intrabc_hash_size, has_intrabc_hash_table_4x4,
    // av1_build_intrabc_hash_table, build_intrabc_hash_tables: intra block copy is off.
    // [subset] aom_get_variance_boost_delta_q_res: deltaq_mode is 0.

    private static readonly enc_row_mt_sync_read_t av1_row_mt_sync_read_dummy_fn =
        av1_row_mt_sync_read_dummy;
    private static readonly enc_row_mt_sync_write_t av1_row_mt_sync_write_dummy_fn =
        av1_row_mt_sync_write_dummy;
    private static readonly enc_row_mt_sync_read_t av1_row_mt_sync_read_fn =
        av1_row_mt_sync_read;
    private static readonly enc_row_mt_sync_write_t av1_row_mt_sync_write_fn =
        av1_row_mt_sync_write;

    /*!\brief Encoder setup(only for the current frame), encoding, and recontruction
     * for a single frame
     */
    private static void encode_frame_internal(AV1_COMP cpi)
    {
        ThreadData td = cpi.td;
        MACROBLOCK x = td.mb;
        AV1_COMMON cm = cpi.common;
        CommonModeInfoParams mi_params = cm.mi_params;
        FeatureFlags features = cm.features;
        MACROBLOCKD xd = x.e_mbd;
        RD_COUNTS rdc = cpi.td.rd_counts;
        // [subset] CONFIG_FPMT_TEST temp_frame_probs: not configured.
        MultiThreadInfo mt_info = cpi.mt_info;
        AV1EncRowMultiThreadInfo enc_row_mt = mt_info.enc_row_mt;
        AV1EncoderConfig oxcf = cpi.oxcf;
        DELTAQ_MODE deltaq_mode = oxcf.q_cfg.deltaq_mode;
        int i;

        if (cpi.sf.rt_sf.use_nonrd_pick_mode == 0)
        {
            // [subset] mi_params->setup_mi(mi_params): use_nonrd_pick_mode is 1.
            throw new NotSupportedException("setup_mi for the rd path is not in the subset");
        }

        set_mi_offsets(mi_params, xd, 0, 0);

        td.counts.Clear();
        av1_zero(rdc.tx_type_used);
        av1_zero(rdc.obmc_used);
        av1_zero(rdc.warped_used);
        av1_zero(rdc.seg_tmp_pred_cost);

        // Reset the flag.
        cpi.intrabc_used = 0;
        // Need to disable intrabc when superres is selected
        if (av1_superres_scaled(cm) != 0)
        {
            features.allow_intrabc = false;
        }

        features.allow_intrabc &= (oxcf.kf_cfg.enable_intrabc);

        if (features.allow_warped_motion &&
            cpi.sf.inter_sf.prune_warped_prob_thresh > 0)
        {
            // [subset] warped motion probability pruning: warped motion is off.
            throw new NotSupportedException("warped motion is not in the subset");
        }

        if (is_stat_generation_stage(cpi) == 0 && av1_use_hash_me(cpi) != 0 &&
            (cpi.sf.rt_sf.use_nonrd_pick_mode == 0 || cpi.sf.rt_sf.rt_use_intrabc != 0))
        {
            // [subset] build_intrabc_hash_tables: intra block copy is off.
            throw new NotSupportedException("intra block copy hash tables are not in the subset");
        }

        CommonQuantParams quant_params = cm.quant_params;
        for (i = 0; i < MAX_SEGMENTS; ++i)
        {
            int qindex =
                cm.seg.enabled != 0 ? av1_get_qindex(cm.seg, i, quant_params.base_qindex)
                                    : quant_params.base_qindex;
            xd.lossless[i] =
                B(qindex == 0 && quant_params.y_dc_delta_q == 0 &&
                  quant_params.u_dc_delta_q == 0 && quant_params.u_ac_delta_q == 0 &&
                  quant_params.v_dc_delta_q == 0 && quant_params.v_ac_delta_q == 0);
            if (xd.lossless[i] != 0) cpi.enc_seg.has_lossless_segment = true;
            xd.qindex[i] = qindex;
            if (xd.lossless[i] != 0)
            {
                cpi.optimize_seg_arr[i] = NO_TRELLIS_OPT;
            }
            else
            {
                cpi.optimize_seg_arr[i] = cpi.sf.rd_sf.optimize_coefficients;
            }
        }
        features.coded_lossless = is_coded_lossless(cm, xd) != 0;
        features.all_lossless = features.coded_lossless && av1_superres_scaled(cm) == 0;

        // Fix delta q resolution for the moment

        cm.delta_q_info.delta_q_res = 0;
        if (cpi.use_ducky_encode != 0)
        {
            // [subset] DEFAULT_DELTA_Q_RES_DUCKY_ENCODE: ducky encode is never used.
            throw new NotSupportedException("ducky encode is not in the subset");
        }
        else if (cpi.oxcf.q_cfg.aq_mode != CYCLIC_REFRESH_AQ &&
                 cpi.roi.enabled == 0)
        {
            // [subset] Only deltaq_mode == NO_DELTA_Q (the subset's setting) is ported: the
            // delta_q_res choices and the objective-delta-q eligibility checks are dropped.
            if (deltaq_mode != NO_DELTA_Q)
                throw new NotSupportedException("delta q is not in the subset");
            // Set delta_q_present_flag before it is used for the first time
            cm.delta_q_info.delta_lf_res = DEFAULT_DELTA_LF_RES;
            cm.delta_q_info.delta_q_present_flag = B(deltaq_mode != NO_DELTA_Q);

            // Reset delta_q_used flag
            cpi.deltaq_used = 0;

            cm.delta_q_info.delta_lf_present_flag =
                B(cm.delta_q_info.delta_q_present_flag != 0 &&
                  oxcf.tool_cfg.enable_deltalf_mode);
            cm.delta_q_info.delta_lf_multi = DEFAULT_DELTA_LF_MULTI;

            // update delta_q_present_flag and delta_lf_present_flag based on
            // base_qindex
            cm.delta_q_info.delta_q_present_flag &= B(quant_params.base_qindex > 0);
            cm.delta_q_info.delta_lf_present_flag &= B(quant_params.base_qindex > 0);
        }
        else if (cpi.cyclic_refresh.apply_cyclic_refresh != 0 ||
                 cpi.svc.number_temporal_layers == 1)
        {
            cpi.cyclic_refresh.actual_num_seg1_blocks = 0;
            cpi.cyclic_refresh.actual_num_seg2_blocks = 0;
        }
        cpi.rc.cnt_zeromv = 0;
        cpi.cb_delta_rdmult_enabled = enable_delta_rdmult(cpi);

        av1_frame_init_quantizer(cpi);
        init_encode_frame_mb_context(cpi);
        set_default_interp_skip_flags(cm, cpi.interp_search_flags);

        // Like the decoder, only use the previous segment map if it has the size of
        // this frame.
        if (cm.prev_frame != null && cm.prev_frame.seg.enabled != 0 &&
            cpi.svc.number_spatial_layers == 1 &&
            cm.prev_frame.mi_rows == cm.mi_params.mi_rows &&
            cm.prev_frame.mi_cols == cm.mi_params.mi_cols)
            cm.last_frame_seg_map = cm.prev_frame.seg_map;
        else
            cm.last_frame_seg_map = Ptr<byte>.Null;
        if (features.allow_intrabc || features.coded_lossless)
        {
            av1_set_default_ref_deltas(cm.lf.ref_deltas);
            av1_set_default_mode_deltas(cm.lf.mode_deltas);
        }
        else if (cm.prev_frame != null)
        {
            memcpy<sbyte>(cm.lf.ref_deltas, cm.prev_frame.ref_deltas, REF_FRAMES);
            memcpy<sbyte>(cm.lf.mode_deltas, cm.prev_frame.mode_deltas, MAX_MODE_LF_DELTAS);
        }
        cm.lf.mode_ref_delta_enabled = (byte)oxcf.algo_cfg.mode_ref_delta_enabled;
        memcpy<sbyte>(cm.cur_frame.ref_deltas, cm.lf.ref_deltas, REF_FRAMES);
        memcpy<sbyte>(cm.cur_frame.mode_deltas, cm.lf.mode_deltas, MAX_MODE_LF_DELTAS);

        cpi.all_one_sided_refs =
            frame_is_intra_only(cm) != 0 ? 0 : refs_are_one_sided(cm);

        cpi.prune_ref_frame_mask = 0;
        // Figure out which ref frames can be skipped at frame level.
        setup_prune_ref_frame_mask(cpi);
        // Disable certain reference frame pruning based on temporal distance and
        // quality of that reference frame.
        setup_keep_ref_frame_mask(cpi);

        x.txfm_search_info.txb_split_count = 0;
        // [subset] CONFIG_SPEED_STATS: not configured.

        // [subset] av1_compute_global_motion_facade: !CONFIG_REALTIME_ONLY.

        av1_calculate_ref_frame_side(cm);

        features.allow_ref_frame_mvs &= !(cpi.sf.hl_sf.ref_frame_mvs_lvl == 2);
        if (features.allow_ref_frame_mvs)
        {
            // [subset] av1_setup_motion_field: ref frame MVs need order hints (off).
            throw new NotSupportedException("ref frame mvs are not in the subset");
        }
        // [subset] check_to_disable_ref_frame_mvs: !CONFIG_REALTIME_ONLY.

        cm.current_frame.skip_mode_info.skip_mode_flag =
            check_skip_mode_enabled(cpi);

        // Initialization of skip mode cost depends on the value of
        // 'skip_mode_flag'. This initialization happens in the function
        // av1_fill_mode_rates(), which is in turn called in
        // av1_initialize_rd_consts(). Thus, av1_initialize_rd_consts()
        // has to be called after 'skip_mode_flag' is initialized.
        av1_initialize_rd_consts(cpi);
        av1_set_sad_per_bit(cpi, ref x.sadperbit, quant_params.base_qindex);
        populate_thresh_to_force_zeromv_skip(cpi);

        enc_row_mt.sync_read_ptr = av1_row_mt_sync_read_dummy_fn;
        enc_row_mt.sync_write_ptr = av1_row_mt_sync_write_dummy_fn;
        mt_info.row_mt_enabled = false;
        mt_info.pack_bs_mt_enabled = AOMMIN(mt_info.num_mod_workers[MOD_PACK_BS],
                                            cm.tiles.cols * cm.tiles.rows) > 1;

        if (oxcf.row_mt && (mt_info.num_mod_workers[MOD_ENC] > 1))
        {
            mt_info.row_mt_enabled = true;
            enc_row_mt.sync_read_ptr = av1_row_mt_sync_read_fn;
            enc_row_mt.sync_write_ptr = av1_row_mt_sync_write_fn;
            av1_encode_tiles_row_mt(cpi);
        }
        else
        {
            int num_workers = AOMMIN(mt_info.num_mod_workers[MOD_ENC],
                                     cm.tiles.cols * cm.tiles.rows);
            if (num_workers > 1)
            {
                // [subset] av1_encode_tiles_mt: tile multi-threading without row mt (the
                // subset always enables row_mt).
                throw new NotSupportedException("tile mt without row mt is not in the subset");
            }
            else
            {
                // Preallocate the pc_tree for realtime coding to reduce the cost of
                // memory allocation.
                int use_nonrd_mode = cpi.sf.rt_sf.use_nonrd_pick_mode;
                if (use_nonrd_mode != 0)
                {
                    td.pc_root = av1_alloc_pc_tree_node(cm.seq_params.sb_size);
                    if (td.pc_root == null)
                        throw new AomException(AOM_CODEC_MEM_ERROR, "Failed to allocate PC_TREE");
                }
                else
                {
                    td.pc_root = null;
                }

                encode_tiles(cpi);
                av1_free_pc_tree_recursive(td.pc_root, av1_num_planes(cm), 0, 0,
                                           cpi.sf.part_sf.partition_search_type);
                td.pc_root = null;
            }
        }

        // If intrabc is allowed but never selected, reset the allow_intrabc flag.
        if (features.allow_intrabc && cpi.intrabc_used == 0)
        {
            features.allow_intrabc = false;
        }
        if (features.allow_intrabc)
        {
            cm.delta_q_info.delta_lf_present_flag = 0;
        }

        if (cm.delta_q_info.delta_q_present_flag != 0 && cpi.deltaq_used == 0)
        {
            cm.delta_q_info.delta_q_present_flag = 0;
        }

        // Set the transform size appropriately before bitstream creation
        MODE_EVAL_TYPE eval_type =
            cpi.sf.winner_mode_sf.enable_winner_mode_for_tx_size_srch != 0
                ? WINNER_MODE_EVAL
                : DEFAULT_EVAL;
        TX_SIZE_SEARCH_METHOD tx_search_type =
            cpi.winner_mode_params.tx_size_search_methods[eval_type];
        Debug.Assert(oxcf.txfm_cfg.enable_tx64 || tx_search_type != USE_LARGESTALL);
        features.tx_mode = select_tx_mode(cm, tx_search_type);

        // Retain the frame level probability update conditions for parallel frames.
        // These conditions will be consumed during postencode stage to update the
        // probability.
        if (cpi.ppi.gf_group.frame_parallel_level[cpi.gf_frame_index] > 0)
        {
            // [subset] do_update_frame_probs_*: frame parallel encoding (FPMT) is off.
            throw new NotSupportedException("frame parallel encoding is not in the subset");
        }

        if (cpi.sf.tx_sf.tx_type_search.prune_tx_type_using_stats != 0 ||
            ((cpi.sf.tx_sf.tx_type_search.fast_inter_tx_type_prob_thresh !=
              int.MaxValue) &&
             (cpi.sf.tx_sf.tx_type_search.fast_inter_tx_type_prob_thresh != 0)))
        {
            // [subset] tx_type_probs update: the tx type probability speed features are
            // off for the rt presets.
            throw new NotSupportedException("tx type probability updates are not in the subset");
        }

        if (cm.seg.enabled != 0)
        {
            cm.seg.temporal_update = 1;
            if (rdc.seg_tmp_pred_cost[0] < rdc.seg_tmp_pred_cost[1])
                cm.seg.temporal_update = 0;
        }

        if (cpi.sf.inter_sf.prune_obmc_prob_thresh > 0 &&
            cpi.sf.inter_sf.prune_obmc_prob_thresh < int.MaxValue)
        {
            // [subset] obmc_probs update: OBMC is off.
            throw new NotSupportedException("obmc probability updates are not in the subset");
        }

        if (features.allow_warped_motion &&
            cpi.sf.inter_sf.prune_warped_prob_thresh > 0)
        {
            // [subset] warped_probs update: warped motion is off.
            throw new NotSupportedException("warped probability updates are not in the subset");
        }

        if (cm.current_frame.frame_type != KEY_FRAME &&
            cpi.sf.interp_sf.adaptive_interp_filter_search == 2 &&
            features.interp_filter == SWITCHABLE)
        {
            // [subset] switchable_interp_probs update: adaptive_interp_filter_search is
            // never 2 for the rt presets.
            throw new NotSupportedException("interp filter probability updates are not in the subset");
        }
    }

    /*!\brief Setup reference frame buffers and encode a frame
     */
    internal static void av1_encode_frame(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        CurrentFrame current_frame = cm.current_frame;
        FeatureFlags features = cm.features;
        RD_COUNTS rdc = cpi.td.rd_counts;
        AV1EncoderConfig oxcf = cpi.oxcf;
        // Indicates whether or not to use a default reduced set for ext-tx
        // rather than the potential full set of 16 transforms
        features.reduced_tx_set_used = oxcf.txfm_cfg.reduced_tx_type_set;

        // Make sure segment_id is no larger than last_active_segid.
        if (cm.seg.enabled != 0 && cm.seg.update_map != 0)
        {
            int mi_rows = cm.mi_params.mi_rows;
            int mi_cols = cm.mi_params.mi_cols;
            int last_active_segid = cm.seg.last_active_segid;
            // [port] C clamps row by row (map += mi_cols); the rows are contiguous, so this is
            // one vectorized min over mi_rows * mi_cols bytes (518K at 4K, on the serial path).
            Span<byte> map = cpi.enc_seg.map.AsSpan(0, mi_rows * mi_cols);
            Span<Vector256<byte>> vmap = MemoryMarshal.Cast<byte, Vector256<byte>>(map);
            Vector256<byte> vlim = Vector256.Create((byte)AOMMIN(last_active_segid, byte.MaxValue));
            for (int i = 0; i < vmap.Length; i++) vmap[i] = Vector256.Min(vmap[i], vlim);
            for (int i = vmap.Length * 32; i < map.Length; i++)
                map[i] = (byte)AOMMIN(map[i], last_active_segid);
        }

        av1_setup_frame_buf_refs(cm);
        enforce_max_ref_frames(cpi, ref cpi.ref_frame_flags,
                               cm.cur_frame.ref_display_order_hint,
                               cm.current_frame.display_order_hint);
        set_rel_frame_dist(cpi.common, cpi.ref_frame_dist_info,
                           cpi.ref_frame_flags);
        av1_setup_frame_sign_bias(cm);

        // [subset] !NDEBUG && !CONFIG_REALTIME_ONLY pyramid asserts.
        // [subset] CONFIG_MISMATCH_DEBUG: not configured.

        rdc.newmv_or_intra_blocks = 0;
        cpi.palette_pixel_num = 0;

        if (cpi.sf.hl_sf.frame_parameter_update != 0 ||
            cpi.sf.rt_sf.use_comp_ref_nonrd != 0)
        {
            if (frame_is_intra_only(cm) != 0)
                current_frame.reference_mode = SINGLE_REFERENCE;
            else
                current_frame.reference_mode = REFERENCE_MODE_SELECT;

            features.interp_filter = SWITCHABLE;
            if (cm.tiles.large_scale != 0) features.interp_filter = EIGHTTAP_REGULAR;

            features.switchable_motion_mode = is_switchable_motion_mode_allowed(
                features.allow_warped_motion, oxcf.motion_mode_cfg.enable_obmc);

            rdc.compound_ref_used_flag = 0;
            rdc.skip_mode_used_flag = 0;

            encode_frame_internal(cpi);

            if (current_frame.reference_mode == REFERENCE_MODE_SELECT)
            {
                // Use a flag that includes 4x4 blocks
                if (rdc.compound_ref_used_flag == 0)
                {
                    current_frame.reference_mode = SINGLE_REFERENCE;
                    // [subset] CONFIG_ENTROPY_STATS: not configured.
                }
            }
            // Re-check on the skip mode status as reference mode may have been
            // changed.
            ref SkipModeInfo skip_mode_info = ref current_frame.skip_mode_info;
            if (frame_is_intra_only(cm) != 0 ||
                current_frame.reference_mode == SINGLE_REFERENCE)
            {
                skip_mode_info.skip_mode_allowed = 0;
                skip_mode_info.skip_mode_flag = 0;
            }
            if (skip_mode_info.skip_mode_flag != 0 && rdc.skip_mode_used_flag == 0)
                skip_mode_info.skip_mode_flag = 0;

            if (cm.tiles.large_scale == 0)
            {
                if (features.tx_mode == TX_MODE_SELECT &&
                    cpi.td.mb.txfm_search_info.txb_split_count == 0)
                    features.tx_mode = TX_MODE_LARGEST;
            }
        }
        else
        {
            // This is needed if real-time speed setting is changed on the fly
            // from one using compound prediction to one using single reference.
            if (current_frame.reference_mode == REFERENCE_MODE_SELECT)
                current_frame.reference_mode = SINGLE_REFERENCE;
            encode_frame_internal(cpi);
        }
    }
}
