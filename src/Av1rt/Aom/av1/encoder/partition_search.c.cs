// Port of libaom av1/encoder/partition_search.c (non-RD / real-time parts)
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class partition_search
{
    // [subset] av1_reset_part_sf, av1_reset_sf_for_ext_part (CONFIG_PARTITION_SEARCH_ORDER) and
    // collect_tpl_stats_sb (!CONFIG_REALTIME_ONLY) are compiled out.

    private static void update_txfm_count(MACROBLOCK x, MACROBLOCKD xd,
                                          FRAME_COUNTS counts, TX_SIZE tx_size, int depth,
                                          int blk_row, int blk_col,
                                          byte allow_update_cdf)
    {
        MB_MODE_INFO mbmi = xd.mi[0];
        BLOCK_SIZE bsize = mbmi.bsize;
        int max_blocks_high = max_block_high(xd, bsize, 0);
        int max_blocks_wide = max_block_wide(xd, bsize, 0);
        int ctx = txfm_partition_context(xd.above_txfm_context + blk_col,
                                         xd.left_txfm_context + blk_row, mbmi.bsize,
                                         tx_size);
        int txb_size_index = av1_get_txb_size_index(bsize, blk_row, blk_col);
        TX_SIZE plane_tx_size = mbmi.inter_tx_size[txb_size_index];

        if (blk_row >= max_blocks_high || blk_col >= max_blocks_wide) return;
        Debug.Assert(tx_size > TX_4X4);

        if (depth == MAX_VARTX_DEPTH)
        {
            // Don't add to counts in this case
            mbmi.tx_size = tx_size;
            txfm_partition_update(xd.above_txfm_context + blk_col,
                                  xd.left_txfm_context + blk_row, tx_size, tx_size);
            return;
        }

        if (tx_size == plane_tx_size)
        {
            // [subset] CONFIG_ENTROPY_STATS: ++counts->txfm_partition[ctx][0]
            if (allow_update_cdf != 0)
                update_cdf(xd.tile_ctx.txfm_partition_cdf[ctx], 0, 2);
            mbmi.tx_size = tx_size;
            txfm_partition_update(xd.above_txfm_context + blk_col,
                                  xd.left_txfm_context + blk_row, tx_size, tx_size);
        }
        else
        {
            TX_SIZE sub_txs = sub_tx_size_map[tx_size];
            int bsw = tx_size_wide_unit[sub_txs];
            int bsh = tx_size_high_unit[sub_txs];

            // [subset] CONFIG_ENTROPY_STATS: ++counts->txfm_partition[ctx][1]
            if (allow_update_cdf != 0)
                update_cdf(xd.tile_ctx.txfm_partition_cdf[ctx], 1, 2);
            ++x.txfm_search_info.txb_split_count;

            if (sub_txs == TX_4X4)
            {
                mbmi.inter_tx_size[txb_size_index] = TX_4X4;
                mbmi.tx_size = TX_4X4;
                txfm_partition_update(xd.above_txfm_context + blk_col,
                                      xd.left_txfm_context + blk_row, TX_4X4, tx_size);
                return;
            }

            for (int row = 0; row < tx_size_high_unit[tx_size]; row += bsh)
            {
                for (int col = 0; col < tx_size_wide_unit[tx_size]; col += bsw)
                {
                    int offsetr = row;
                    int offsetc = col;

                    update_txfm_count(x, xd, counts, sub_txs, depth + 1, blk_row + offsetr,
                                      blk_col + offsetc, allow_update_cdf);
                }
            }
        }
    }

    private static void tx_partition_count_update(AV1_COMMON cm, MACROBLOCK x,
                                                  BLOCK_SIZE plane_bsize,
                                                  FRAME_COUNTS td_counts,
                                                  byte allow_update_cdf)
    {
        MACROBLOCKD xd = x.e_mbd;
        int mi_width = mi_size_wide[plane_bsize];
        int mi_height = mi_size_high[plane_bsize];
        TX_SIZE max_tx_size = get_vartx_max_txsize(xd, plane_bsize, 0);
        int bh = tx_size_high_unit[max_tx_size];
        int bw = tx_size_wide_unit[max_tx_size];

        xd.above_txfm_context =
            cm.above_contexts.txfm[xd.tile.tile_row] + xd.mi_col;
        xd.left_txfm_context =
            new Ptr<TXFM_CONTEXT>(xd.left_txfm_context_buffer, xd.mi_row & MAX_MIB_MASK);

        for (int idy = 0; idy < mi_height; idy += bh)
        {
            for (int idx = 0; idx < mi_width; idx += bw)
            {
                update_txfm_count(x, xd, td_counts, max_tx_size, 0, idy, idx,
                                  allow_update_cdf);
            }
        }
    }

    // [subset] set_txfm_context / tx_partition_set_contexts: only reached from
    // encode_superblock() with dry_run != 0; the non-RD path always encodes with
    // dry_run == OUTPUT_ENABLED (0).

    private static void update_zeromv_cnt(AV1_COMP cpi,
                                          MB_MODE_INFO mi, int mi_row,
                                          int mi_col, BLOCK_SIZE bsize)
    {
        if (mi.ref_frame[0] != LAST_FRAME || is_inter_block(mi) == 0 ||
            mi.segment_id > CR_SEGMENT_ID_BOOST2)
        {
            return;
        }
        AV1_COMMON cm = cpi.common;
        MV mv = mi.mv[0].as_mv;
        int bw = mi_size_wide[bsize] >> 1;
        int bh = mi_size_high[bsize] >> 1;
        int xmis = AOMMIN((cm.mi_params.mi_cols - mi_col) >> 1, bw);
        int ymis = AOMMIN((cm.mi_params.mi_rows - mi_row) >> 1, bh);
        int block_index =
            (mi_row >> 1) * (cm.mi_params.mi_cols >> 1) + (mi_col >> 1);
        for (int y = 0; y < ymis; y++)
        {
            for (int x = 0; x < xmis; x++)
            {
                // consec_zero_mv is in the scale of 8x8 blocks
                int map_offset = block_index + y * (cm.mi_params.mi_cols >> 1) + x;
                if (Math.Abs((int)mv.row) < 10 && Math.Abs((int)mv.col) < 10)
                {
                    if (cpi.consec_zero_mv[map_offset] < 255)
                        cpi.consec_zero_mv[map_offset]++;
                }
                else
                {
                    cpi.consec_zero_mv[map_offset] = 0;
                }
            }
        }
    }

    // Returns true when the pixels of this block already match its prediction,
    // because its whole skip superblock was copied from the reference up front.
    private static bool is_prediction_copied(MACROBLOCK x, MB_MODE_INFO mbmi,
                                             int seg_skip)
    {
        if (seg_skip == 0 || x.sb_copied_ref == NONE_FRAME) return false;

        return has_second_ref(mbmi) == 0 && is_interintra_pred(mbmi) == 0 &&
               mbmi.ref_frame[0] == x.sb_copied_ref &&
               mbmi.motion_mode == SIMPLE_TRANSLATION && mbmi.mv[0].as_int == 0;
    }

    private static void encode_superblock(AV1_COMP cpi, TileDataEnc tile_data,
                                          ThreadData td, ref Ptr<TokenExtra> t, RUN_TYPE dry_run,
                                          BLOCK_SIZE bsize, Ptr<int> rate)
    {
        AV1_COMMON cm = cpi.common;
        int num_planes = av1_num_planes(cm);
        MACROBLOCK x = td.mb;
        MACROBLOCKD xd = x.e_mbd;
        Ptr<MB_MODE_INFO> mi_4x4 = xd.mi;
        MB_MODE_INFO mbmi = mi_4x4[0];
        int seg_skip =
            segfeature_active(cm.seg, mbmi.segment_id, SEG_LVL_SKIP);
        int mis = cm.mi_params.mi_stride;
        int mi_width = mi_size_wide[bsize];
        int mi_height = mi_size_high[bsize];
        int is_inter = is_inter_block(mbmi);

        // Initialize tx_mode and tx_size_search_method
        TxfmSearchParams txfm_params = x.txfm_search_params;
        set_tx_size_search_method(
            cm, cpi.winner_mode_params, txfm_params,
            cpi.sf.winner_mode_sf.enable_winner_mode_for_tx_size_srch, 1);

        int mi_row = xd.mi_row;
        int mi_col = xd.mi_col;
        if (is_inter == 0)
        {
            xd.cfl.store_y = store_cfl_required(cm, xd);
            for (int plane = 0; plane < num_planes; ++plane)
            {
                av1_encode_intra_block_plane(cpi, x, bsize, plane, dry_run,
                                             cpi.optimize_seg_arr[mbmi.segment_id]);
            }

            // If there is at least one lossless segment, force the skip for intra
            // block to be 0, in order to avoid the segment_id to be changed by in
            // write_segment_id().
            if (cpi.common.seg.segid_preskip == 0 && cpi.common.seg.update_map != 0 &&
                cpi.enc_seg.has_lossless_segment)
                mbmi.skip_txfm = 0;

            xd.cfl.store_y = 0;
            if (av1_allow_palette(B(cm.features.allow_screen_content_tools), bsize) != 0)
            {
                // Gather the stats to determine whether to use screen content tools in
                // function av1_determine_sc_tools_with_encoding().
                if (mbmi.palette_mode_info.palette_size[0] > 0 &&
                    dry_run == OUTPUT_ENABLED)
                    x.palette_pixels += (block_size_wide[bsize] * block_size_high[bsize]);
                for (int plane = 0; plane < AOMMIN(2, num_planes); ++plane)
                {
                    if (mbmi.palette_mode_info.palette_size[plane] > 0)
                    {
                        // [subset] palette coding: allow_screen_content_tools is only set
                        // for screen content, which is out of the subset
                        // (av1_tokenize_color_map / av1_cost_color_map are not ported).
                        throw new NotSupportedException("encode_superblock: palette");
                    }
                }
            }

            av1_update_intra_mb_txb_context(cpi, td, dry_run, bsize,
                                            tile_data.allow_update_cdf);
        }
        else
        {
            int @ref;
            int is_compound = has_second_ref(mbmi);

            set_ref_ptrs(cm, xd, mbmi.ref_frame[0], mbmi.ref_frame[1]);
            for (@ref = 0; @ref < 1 + is_compound; ++@ref)
            {
                YV12_BUFFER_CONFIG cfg =
                    get_ref_frame_yv12_buf(cm, mbmi.ref_frame[@ref]);
                Debug.Assert(is_intrabc_block(mbmi) != 0 || cfg != null);
                av1_setup_pre_planes(xd, @ref, cfg, mi_row, mi_col,
                                     xd.block_ref_scale_factors[@ref], num_planes);
            }
            // Predicted sample of inter mode (for Luma plane) cannot be reused if
            // nonrd_check_partition_split speed feature is enabled, Since in such cases
            // the buffer may not contain the predicted sample of best mode. Skipped
            // segment blocks are picked without building a prediction.
            int start_plane =
                (x.reuse_inter_pred && (cpi.sf.rt_sf.nonrd_check_partition_split == 0) &&
                 seg_skip == 0 && cm.seq_params.bit_depth == AOM_BITS_8)
                    ? 1
                    : 0;
            if (!is_prediction_copied(x, mbmi, seg_skip))
            {
                av1_enc_build_inter_predictor(cm, xd, mi_row, mi_col, Ptr<BUFFER_SET>.Null, bsize,
                                              start_plane, av1_num_planes(cm) - 1);
            }
            if (mbmi.motion_mode == OBMC_CAUSAL)
            {
                // [subset] OBMC is disabled in the real-time subset
                // (av1_build_obmc_inter_predictors_sb is not ported).
                throw new NotSupportedException("encode_superblock: OBMC_CAUSAL");
            }

            // [subset] CONFIG_MISMATCH_DEBUG is off.

            av1_encode_sb(cpi, x, bsize, dry_run);
            av1_tokenize_sb_vartx(cpi, td, dry_run, bsize, rate,
                                  tile_data.allow_update_cdf);
        }

        if (dry_run == 0)
        {
            if (av1_allow_intrabc(cm) != 0 && is_intrabc_block(mbmi) != 0) td.intrabc_used = 1;
            if (txfm_params.tx_mode_search_type == TX_MODE_SELECT &&
                xd.lossless[mbmi.segment_id] == 0 && mbmi.bsize > BLOCK_4X4 &&
                !(is_inter != 0 && (mbmi.skip_txfm != 0 || seg_skip != 0)))
            {
                if (is_inter != 0)
                {
                    tx_partition_count_update(cm, x, bsize, td.counts,
                                              tile_data.allow_update_cdf);
                }
                else
                {
                    if (mbmi.tx_size != max_txsize_rect_lookup[bsize])
                        ++x.txfm_search_info.txb_split_count;
                    if (block_signals_txsize(bsize) != 0)
                    {
                        int tx_size_ctx = get_tx_size_context(xd);
                        int tx_size_cat = bsize_to_tx_size_cat(bsize);
                        int depth = tx_size_to_depth(mbmi.tx_size, bsize);
                        int max_depths = bsize_to_max_depth(bsize);

                        if (tile_data.allow_update_cdf != 0)
                            update_cdf(xd.tile_ctx.tx_size_cdf[tx_size_cat][tx_size_ctx],
                                       depth, max_depths + 1);
                        // [subset] CONFIG_ENTROPY_STATS: ++td->counts->intra_tx_size[...]
                    }
                }
            }
            else
            {
                int i, j;
                TX_SIZE intra_tx_size;
                // The new intra coding scheme requires no change of transform size
                if (is_inter != 0)
                {
                    if (xd.lossless[mbmi.segment_id] != 0)
                    {
                        intra_tx_size = TX_4X4;
                    }
                    else
                    {
                        intra_tx_size =
                            tx_size_from_tx_mode(bsize, txfm_params.tx_mode_search_type);
                    }
                }
                else
                {
                    intra_tx_size = mbmi.tx_size;
                }

                int cols = AOMMIN(cm.mi_params.mi_cols - mi_col, mi_width);
                int rows = AOMMIN(cm.mi_params.mi_rows - mi_row, mi_height);
                for (j = 0; j < rows; j++)
                {
                    for (i = 0; i < cols; i++) mi_4x4[mis * j + i].tx_size = intra_tx_size;
                }

                if (intra_tx_size != max_txsize_rect_lookup[bsize])
                    ++x.txfm_search_info.txb_split_count;
            }
        }

        if (txfm_params.tx_mode_search_type == TX_MODE_SELECT &&
            block_signals_txsize(mbmi.bsize) != 0 && is_inter != 0 &&
            !(mbmi.skip_txfm != 0 || seg_skip != 0) && xd.lossless[mbmi.segment_id] == 0)
        {
            if (dry_run != 0)
            {
                // [subset] tx_partition_set_contexts(): dry_run is always 0 in the
                // non-RD path.
                throw new NotSupportedException("encode_superblock: dry_run");
            }
        }
        else
        {
            TX_SIZE tx_size = mbmi.tx_size;
            // The new intra coding scheme requires no change of transform size
            if (is_inter != 0)
            {
                if (xd.lossless[mbmi.segment_id] != 0)
                {
                    tx_size = TX_4X4;
                }
                else
                {
                    tx_size = tx_size_from_tx_mode(bsize, txfm_params.tx_mode_search_type);
                }
            }
            else
            {
                tx_size = (bsize > BLOCK_4X4) ? tx_size : TX_4X4;
            }
            mbmi.tx_size = tx_size;
            set_txfm_ctxs(tx_size, xd.width, xd.height,
                          B((mbmi.skip_txfm != 0 || seg_skip != 0) && is_inter_block(mbmi) != 0),
                          xd);
        }

        // [subset] cfl_store_block for inter blocks is #if !CONFIG_REALTIME_ONLY.
        if (dry_run == 0)
        {
            if (cpi.oxcf.pass == AOM_RC_ONE_PASS && cpi.svc.temporal_layer_id == 0 &&
                cpi.sf.rt_sf.use_temporal_noise_estimate != 0 &&
                (cpi.ppi.use_svc == 0 ||
                 (cpi.ppi.use_svc != 0 &&
                  cpi.svc.layer_context[cpi.svc.temporal_layer_id].is_key_frame == 0 &&
                  cpi.svc.spatial_layer_id == cpi.svc.number_spatial_layers - 1)))
                update_zeromv_cnt(cpi, mbmi, mi_row, mi_col, bsize);
        }
    }

    private static void setup_block_rdmult(AV1_COMP cpi, MACROBLOCK x,
                                           int mi_row, int mi_col, BLOCK_SIZE bsize,
                                           AQ_MODE aq_mode, MB_MODE_INFO mbmi)
    {
        x.rdmult = cpi.rd.RDMULT;

        if (aq_mode != NO_AQ)
        {
            Debug.Assert(mbmi != null);
            if (aq_mode == VARIANCE_AQ)
            {
                // [subset] VARIANCE_AQ is not used in the real-time subset.
                throw new NotSupportedException("setup_block_rdmult: VARIANCE_AQ");
            }
            else if (aq_mode == COMPLEXITY_AQ)
            {
                // [subset] COMPLEXITY_AQ is not used in the real-time subset.
                throw new NotSupportedException("setup_block_rdmult: COMPLEXITY_AQ");
            }
            else if (aq_mode == CYCLIC_REFRESH_AQ)
            {
                // If segment is boosted, use rdmult for that segment.
                if (cyclic_refresh_segment_id_boosted(mbmi.segment_id) != 0)
                    x.rdmult = av1_cyclic_refresh_get_rdmult(cpi.cyclic_refresh);
            }
        }

        // [subset] cb_delta_rdmult_enabled (av1_get_cb_rdmult) is #if !CONFIG_REALTIME_ONLY.

        if (cpi.oxcf.tune_cfg.tuning == AOM_TUNE_SSIM ||
            cpi.oxcf.tune_cfg.tuning == AOM_TUNE_IQ ||
            cpi.oxcf.tune_cfg.tuning == AOM_TUNE_SSIMULACRA2)
        {
            // [subset] av1_set_ssim_rdmult: the subset uses the default PSNR tuning.
            throw new NotSupportedException("setup_block_rdmult: SSIM tuning");
        }
        // [subset] CONFIG_SALIENCY_MAP, CONFIG_TUNE_VMAF, CONFIG_TUNE_BUTTERAUGLI are off.
        if (cpi.oxcf.mode == ALLINTRA)
        {
            x.rdmult = (int)(((long)x.rdmult * x.intra_sb_rdmult_modifier) >> 7);
        }

        // Check to make sure that the adjustments above have not caused the
        // rd multiplier to be truncated to 0.
        x.rdmult = (x.rdmult > 0) ? x.rdmult : 1;
    }

    internal static void av1_set_offsets_without_segment_id(AV1_COMP cpi,
                                                           in TileInfo tile,
                                                           MACROBLOCK x, int mi_row,
                                                           int mi_col, BLOCK_SIZE bsize)
    {
        AV1_COMMON cm = cpi.common;
        int num_planes = av1_num_planes(cm);
        MACROBLOCKD xd = x.e_mbd;
        Debug.Assert(bsize < BLOCK_SIZES_ALL);
        int mi_width = mi_size_wide[bsize];
        int mi_height = mi_size_high[bsize];

        set_mode_info_offsets(cpi.common.mi_params, cpi.mbmi_ext_info, x, xd,
                              mi_row, mi_col);

        set_entropy_context(xd, mi_row, mi_col, num_planes);
        xd.above_txfm_context = cm.above_contexts.txfm[tile.tile_row] + mi_col;
        xd.left_txfm_context =
            new Ptr<TXFM_CONTEXT>(xd.left_txfm_context_buffer, mi_row & MAX_MIB_MASK);

        // Set up destination pointers.
        av1_setup_dst_planes(xd.plane, bsize, cm.cur_frame.buf, mi_row, mi_col, 0,
                             num_planes);

        // Set up limit values for MV components.
        // Mv beyond the range do not produce new/different prediction block.
        av1_set_mv_limits(cm.mi_params, ref x.mv_limits, mi_row, mi_col, mi_height,
                          mi_width, cpi.oxcf.border_in_pixels);

        set_plane_n4(xd, mi_width, mi_height, num_planes);

        // Set up distance of MB to edge of frame in 1/8th pel units.
        Debug.Assert((mi_col & (mi_width - 1)) == 0 && (mi_row & (mi_height - 1)) == 0);
        set_mi_row_col(xd, in tile, mi_row, mi_height, mi_col, mi_width,
                       cm.mi_params.mi_rows, cm.mi_params.mi_cols);

        set_pixels_to_frame_edge(x, mi_width, mi_height, mi_col, mi_row,
                                 cm.mi_params.mi_cols, cm.mi_params.mi_rows,
                                 cm.width, cm.height, cpi.do_border_pad);

        // Set up source buffers.
        av1_setup_src_planes(x, cpi.source, mi_row, mi_col, num_planes, bsize);

        // required by av1_append_sub8x8_mvs_for_idx() and av1_find_best_ref_mvs()
        xd.tile = tile;
    }

    internal static void av1_set_offsets(AV1_COMP cpi, in TileInfo tile,
                                         MACROBLOCK x, int mi_row, int mi_col,
                                         BLOCK_SIZE bsize)
    {
        AV1_COMMON cm = cpi.common;
        segmentation seg = cm.seg;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mbmi;

        av1_set_offsets_without_segment_id(cpi, in tile, x, mi_row, mi_col, bsize);

        // Setup segment ID.
        mbmi = xd.mi[0];
        mbmi.segment_id = 0;
        if (seg.enabled != 0)
        {
            if (seg.enabled != 0 && cpi.vaq_refresh == 0)
            {
                Ptr<byte> map =
                    seg.update_map != 0 ? cpi.enc_seg.map : cm.last_frame_seg_map;
                mbmi.segment_id =
                    !map.IsNull ? get_segment_id(cm.mi_params, map, bsize, mi_row, mi_col) : (byte)0;
            }
            av1_init_plane_quantizers(cpi, x, mbmi.segment_id, 0);
        }
        // C: #ifndef NDEBUG
        x.last_set_offsets_loc.mi_row = mi_row;
        x.last_set_offsets_loc.mi_col = mi_col;
        x.last_set_offsets_loc.bsize = bsize;
    }

    private static readonly uint[] var_thresh = { 0, 101, 201 };

    /*!\brief Hybrid intra mode search.
     *
     * This is top level function for mode search for intra frames in non-RD
     * optimized case. Depending on speed feature and block size it calls
     * either non-RD or RD optimized intra mode search.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void hybrid_intra_mode_search(AV1_COMP cpi, MACROBLOCK x,
                                                 ref RD_STATS rd_cost, BLOCK_SIZE bsize,
                                                 PICK_MODE_CONTEXT ctx)
    {
        int use_rdopt = 0;
        int hybrid_intra_pickmode = cpi.sf.rt_sf.hybrid_intra_pickmode;
        // Use rd pick for intra mode search based on block size and variance.
        if (hybrid_intra_pickmode != 0 && bsize < BLOCK_16X16)
        {
            Debug.Assert(hybrid_intra_pickmode <= 3);
            if (x.source_variance >= var_thresh[hybrid_intra_pickmode - 1])
                use_rdopt = 1;
        }

        if (use_rdopt != 0)
        {
            // [subset] av1_rd_pick_intra_mode_sb: hybrid_intra_pickmode is 0 at speed 11
            // for non-screen content (speed_features.c), so the RD intra search is never used.
            throw new NotSupportedException("hybrid_intra_mode_search: av1_rd_pick_intra_mode_sb");
        }
        else
            av1_nonrd_pick_intra_mode(cpi, x, ref rd_cost, bsize, ctx);
    }

    // For real time/allintra row-mt enabled multi-threaded encoding with cost
    // update frequency set to COST_UPD_TILE/COST_UPD_OFF, tile ctxt is not updated
    // at superblock level. Thus, it is not required for the encoding of top-right
    // superblock be complete for updating tile ctxt. However, when encoding a block
    // whose right edge is also the superblock edge, intra and inter mode evaluation
    // (ref mv list population) require the encoding of the top-right superblock to
    // be complete. So, here, we delay the waiting of threads until the need for the
    // data from the top-right superblock region.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void wait_for_top_right_sb(AV1EncRowMultiThreadInfo enc_row_mt,
                                              AV1EncRowMultiThreadSync row_mt_sync,
                                              in TileInfo tile_info,
                                              BLOCK_SIZE sb_size,
                                              int sb_mi_size_log2, BLOCK_SIZE bsize,
                                              int mi_row, int mi_col)
    {
        int sb_size_in_mi = mi_size_wide[sb_size];
        int bw_in_mi = mi_size_wide[bsize];
        int blk_row_in_sb = mi_row & (sb_size_in_mi - 1);
        int blk_col_in_sb = mi_col & (sb_size_in_mi - 1);
        bool top_right_block_in_sb =
            (blk_row_in_sb == 0) && (blk_col_in_sb + bw_in_mi >= sb_size_in_mi);

        // Don't wait if the block is the not the top-right block in the superblock.
        if (!top_right_block_in_sb) return;

        // Wait for the top-right superblock to finish encoding.
        int sb_row_in_tile =
            (mi_row - tile_info.mi_row_start) >> sb_mi_size_log2;
        int sb_col_in_tile =
            (mi_col - tile_info.mi_col_start) >> sb_mi_size_log2;

        enc_row_mt.sync_read_ptr(row_mt_sync, sb_row_in_tile, sb_col_in_tile);
    }

    // [subset] pick_sb_modes, update_stats, encode_b, encode_sb,
    // is_adjust_var_based_part_enabled, av1_rd_use_partition: RD partition search,
    // not used with use_nonrd_pick_mode (and update_stats only runs with
    // allow_update_cdf, which is 0 in the real-time subset).

    private static void encode_b_nonrd(AV1_COMP cpi, TileDataEnc tile_data,
                                       ThreadData td, ref Ptr<TokenExtra> tp, int mi_row,
                                       int mi_col, RUN_TYPE dry_run, BLOCK_SIZE bsize,
                                       PARTITION_TYPE partition,
                                       PICK_MODE_CONTEXT ctx, Ptr<int> rate)
    {
        // [subset] CONFIG_COLLECT_COMPONENT_TIMING is off.
        AV1_COMMON cm = cpi.common;
        ref TileInfo tile = ref tile_data.tile_info;
        MACROBLOCK x = td.mb;
        MACROBLOCKD xd = x.e_mbd;
        av1_set_offsets_without_segment_id(cpi, in tile, x, mi_row, mi_col, bsize);
        int origin_mult = x.rdmult;
        setup_block_rdmult(cpi, x, mi_row, mi_col, bsize, NO_AQ, null);
        MB_MODE_INFO mbmi = xd.mi[0];
        mbmi.partition = partition;
        av1_update_state(cpi, td, ctx, mi_row, mi_col, bsize, dry_run);
        int subsampling_x = cpi.common.seq_params.subsampling_x;
        int subsampling_y = cpi.common.seq_params.subsampling_y;
        if (dry_run == 0)
        {
            set_cb_offsets(x.mbmi_ext_frame.Value.cb_offset, x.cb_offset[PLANE_TYPE_Y],
                           x.cb_offset[PLANE_TYPE_UV]);
            Debug.Assert(x.cb_offset[PLANE_TYPE_Y] <
                         (1 << num_pels_log2_lookup[cpi.common.seq_params.sb_size]));
            Debug.Assert(x.cb_offset[PLANE_TYPE_UV] <
                         ((1 << num_pels_log2_lookup[cpi.common.seq_params.sb_size]) >>
                          (subsampling_x + subsampling_y)));
        }

        if (is_inter_block(xd.mi[0]) == 0) xd.mi[0].skip_txfm = 0;
        encode_superblock(cpi, tile_data, td, ref tp, dry_run, bsize, rate);
        if (dry_run == 0)
        {
            update_cb_offsets(x, bsize, subsampling_x, subsampling_y);
            if (has_second_ref(mbmi) != 0)
            {
                if (mbmi.compound_idx == 0 ||
                    mbmi.interinter_comp.type == COMPOUND_AVERAGE)
                    mbmi.comp_group_idx = 0;
                else
                    mbmi.comp_group_idx = 1;
                mbmi.compound_idx = 1;
            }
            RD_COUNTS rdc = td.rd_counts;
            if (mbmi.skip_mode != 0)
            {
                Debug.Assert(frame_is_intra_only(cm) == 0);
                rdc.skip_mode_used_flag = 1;
                if (cm.current_frame.reference_mode == REFERENCE_MODE_SELECT &&
                    has_second_ref(mbmi) != 0)
                {
                    rdc.compound_ref_used_flag = 1;
                }
                set_ref_ptrs(cm, xd, mbmi.ref_frame[0], mbmi.ref_frame[1]);
            }
            else
            {
                int seg_ref_active =
                    segfeature_active(cm.seg, mbmi.segment_id, SEG_LVL_REF_FRAME);
                if (seg_ref_active == 0)
                {
                    // If the segment reference feature is enabled we have only a single
                    // reference frame allowed for the segment so exclude it from
                    // the reference frame counts used to work out probabilities.
                    if (is_inter_block(mbmi) != 0)
                    {
                        av1_collect_neighbors_ref_counts(xd);
                        if (cm.current_frame.reference_mode == REFERENCE_MODE_SELECT &&
                            has_second_ref(mbmi) != 0)
                        {
                            // This flag is also updated for 4x4 blocks
                            rdc.compound_ref_used_flag = 1;
                        }
                        set_ref_ptrs(cm, xd, mbmi.ref_frame[0], mbmi.ref_frame[1]);
                    }
                }
            }
            if (cpi.oxcf.algo_cfg.loopfilter_control == LOOPFILTER_SELECTIVELY &&
                (mbmi.mode == NEWMV || mbmi.mode < INTRA_MODE_END))
            {
                int blocks = mi_size_high[bsize] * mi_size_wide[bsize];
                rdc.newmv_or_intra_blocks += blocks;
            }
            if (tile_data.allow_update_cdf != 0)
            {
                // [subset] update_stats(): allow_update_cdf is 0 in the real-time subset
                // (delay_wait_for_top_right_sb() is true with cost updates off).
                throw new NotSupportedException("encode_b_nonrd: update_stats");
            }
        }
        if ((cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ ||
             cpi.active_map.enabled != 0 || cpi.roi.enabled != 0) &&
            mbmi.skip_txfm != 0 && cpi.rc.rtc_external_ratectrl == 0 && cm.seg.enabled != 0)
            av1_cyclic_reset_segment_skip(cpi, x, mi_row, mi_col, bsize, dry_run);
        // TODO(Ravi/Remya): Move this copy function to a better logical place
        // This function will copy the best mode information from block
        // level (x->mbmi_ext) to frame level (cpi->mbmi_ext_info.frame_base). This
        // frame level buffer (cpi->mbmi_ext_info.frame_base) will be used during
        // bitstream preparation.
        av1_copy_mbmi_ext_to_mbmi_ext_frame(ref x.mbmi_ext_frame.Value, x.mbmi_ext,
                                            (byte)av1_ref_frame_type(xd.mi[0].ref_frame));
        x.rdmult = origin_mult;
    }

    // [port] scratch for get_force_zeromv_skip_flag_for_blk(): C `struct buf_2d
    // yv12_mb[MAX_MB_PLANE]` and `unsigned int thresh_exit_part[MAX_MB_PLANE]`.
    [ThreadStatic] private static buf_2d[] s_zeromv_yv12_mb;

    private static int get_force_zeromv_skip_flag_for_blk(AV1_COMP cpi,
                                                          MACROBLOCK x,
                                                          BLOCK_SIZE bsize)
    {
        // Force zero MV skip based on SB level decision
        if (x.force_zeromv_skip_for_sb < 2) return x.force_zeromv_skip_for_sb;

        // For blocks of size equal to superblock size, the decision would have been
        // already done at superblock level. Hence zeromv-skip decision is skipped.
        AV1_COMMON cm = cpi.common;
        if (bsize == cm.seq_params.sb_size) return 0;

        int num_planes = av1_num_planes(cm);
        MACROBLOCKD xd = x.e_mbd;
        uint thresh_exit_part_y =
            cpi.zeromv_skip_thresh_exit_part[bsize];
        uint thresh_exit_part_uv =
            (uint)CALC_CHROMA_THRESH_FOR_ZEROMV_SKIP(thresh_exit_part_y);
        // C: const unsigned int thresh_exit_part[MAX_MB_PLANE] =
        //        { thresh_exit_part_y, thresh_exit_part_uv, thresh_exit_part_uv };
        YV12_BUFFER_CONFIG yv12 = get_ref_frame_yv12_buf(cm, LAST_FRAME);
        scale_factors sf =
            get_ref_scale_factors_const(cm, LAST_FRAME);

        buf_2d[] yv12_mb = s_zeromv_yv12_mb ??= new buf_2d[MAX_MB_PLANE];
        av1_setup_pred_block(xd, yv12_mb, yv12, sf, sf, num_planes);

        for (int plane = 0; plane < num_planes; ++plane)
        {
            macroblock_plane p = x.plane[plane];
            macroblockd_plane pd = xd.plane[plane];
            BLOCK_SIZE bs =
                get_plane_block_size(bsize, pd.subsampling_x, pd.subsampling_y);
            uint plane_sad = cpi.ppi.fn_ptr[bs].sdf(
                p.src.buf, p.src.stride, yv12_mb[plane].buf, yv12_mb[plane].stride);
            Debug.Assert(plane < MAX_MB_PLANE);
            uint thresh_exit_part = plane == 0 ? thresh_exit_part_y : thresh_exit_part_uv;
            if (plane_sad >= thresh_exit_part) return 0;
        }
        return 1;
    }

    /*!\brief Top level function to pick block mode for non-RD optimized case
     *
     * Searches prediction modes, transform, and coefficient coding modes for an
     * individual coding block. This function is the top-level function that is
     * used for non-RD optimized mode search (controlled by
     * \c cpi->sf.rt_sf.use_nonrd_pick_mode). Depending on frame type it calls
     * inter/skip/hybrid-intra mode search functions
     */
    private static void pick_sb_modes_nonrd(AV1_COMP cpi, TileDataEnc tile_data,
                                            MACROBLOCK x, int mi_row, int mi_col,
                                            ref RD_STATS rd_cost, BLOCK_SIZE bsize,
                                            PICK_MODE_CONTEXT ctx)
    {
        // For nonrd mode, av1_set_offsets is already called at the superblock level
        // in encode_nonrd_sb when we determine the partitioning.
        if (bsize != cpi.common.seq_params.sb_size ||
            cpi.sf.rt_sf.nonrd_check_partition_split == 1)
        {
            av1_set_offsets(cpi, in tile_data.tile_info, x, mi_row, mi_col, bsize);
        }
        Debug.Assert(x.last_set_offsets_loc.mi_row == mi_row &&
                     x.last_set_offsets_loc.mi_col == mi_col &&
                     x.last_set_offsets_loc.bsize == bsize);
        AV1_COMMON cm = cpi.common;
        int num_planes = av1_num_planes(cm);
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mbmi = xd.mi[0];
        macroblock_plane[] p = x.plane;
        macroblockd_plane[] pd = xd.plane;
        AQ_MODE aq_mode = cpi.oxcf.q_cfg.aq_mode;
        TxfmSearchInfo txfm_info = x.txfm_search_info;
        int i;
        int seg_skip =
            segfeature_active(cm.seg, mbmi.segment_id, SEG_LVL_SKIP);

        // This is only needed for real time/allintra row-mt enabled multi-threaded
        // encoding with cost update frequency set to COST_UPD_TILE/COST_UPD_OFF.
        wait_for_top_right_sb(cpi.mt_info.enc_row_mt, tile_data.row_mt_sync,
                              in tile_data.tile_info, cm.seq_params.sb_size,
                              cm.seq_params.mib_size_log2, bsize, mi_row, mi_col);

        // Sets up the tx_type_map buffer in MACROBLOCKD.
        xd.tx_type_map = txfm_info.tx_type_map_;
        xd.tx_type_map_stride = mi_size_wide[bsize];
        for (i = 0; i < num_planes; ++i)
        {
            p[i].coeff = ctx.coeff[i];
            p[i].qcoeff = ctx.qcoeff[i];
            p[i].dqcoeff = ctx.dqcoeff[i];
            p[i].eobs = ctx.eobs[i];
            p[i].txb_entropy_ctx = ctx.txb_entropy_ctx[i];
        }
        for (i = 0; i < 2; ++i) pd[i].color_index_map = ctx.color_index_map[i];

        if (seg_skip == 0)
        {
            x.force_zeromv_skip_for_blk =
                get_force_zeromv_skip_flag_for_blk(cpi, x, bsize);

            // Source variance may be already compute at superblock level, so no need
            // to recompute, unless bsize < sb_size or source_variance is not yet set.
            if (x.force_zeromv_skip_for_blk == 0 &&
                (x.source_variance == uint.MaxValue || bsize < cm.seq_params.sb_size))
                x.source_variance = av1_get_perpixel_variance_facade(
                    cpi, xd, in x.plane[0].src, bsize, AOM_PLANE_Y);
        }

        // Save rdmult before it might be changed, so it can be restored later.
        int orig_rdmult = x.rdmult;
        setup_block_rdmult(cpi, x, mi_row, mi_col, bsize, aq_mode, mbmi);
        if (cpi.roi.enabled != 0 && cpi.roi.delta_qp_enabled != 0 && mbmi.segment_id != 0)
            x.rdmult = cpi.roi.rdmult_delta_qp;
        // Set error per bit for current rdmult
        av1_set_error_per_bit(ref x.errorperbit, x.rdmult);
        // Find best coding mode & reconstruct the MB so it is available
        // as a predictor for MBs that follow in the SB
        if (frame_is_intra_only(cm) != 0)
        {
            hybrid_intra_mode_search(cpi, x, ref rd_cost, bsize, ctx);
        }
        else
        {
            if (seg_skip != 0)
            {
                // [subset] av1_nonrd_pick_inter_mode_sb_seg_skip: SEG_LVL_SKIP is never
                // enabled in the real-time subset.
                throw new NotSupportedException(
                    "pick_sb_modes_nonrd: av1_nonrd_pick_inter_mode_sb_seg_skip");
            }
            else
            {
                av1_nonrd_pick_inter_mode_sb(cpi, tile_data, x, ref rd_cost, bsize, ctx);
            }
        }
        if (cpi.sf.rt_sf.skip_cdef_sb != 0)
        {
            // cdef_strength is initialized to 1 which means skip_cdef, and is updated
            // here. Check to see is skipping cdef is allowed. Never skip on slide/scene
            // change, near a key frame, or when color sensitivity is set. Always allow
            // cdef_skip for seg_skip = 1.
            bool allow_cdef_skipping =
                seg_skip != 0 ||
                (cpi.rc.frames_since_key > 10 && cpi.rc.high_source_sad == 0 &&
                 !(x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] != 0 ||
                   x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] != 0));

            // Find the corresponding 64x64 block. It'll be the 128x128 block if that's
            // the block size.
            int mi_row_sb = mi_row - mi_row % MI_SIZE_64X64;
            int mi_col_sb = mi_col - mi_col % MI_SIZE_64X64;
            MB_MODE_INFO mi_sb0 =
                cm.mi_params.mi_grid_base[get_mi_grid_idx(cm.mi_params, mi_row_sb, mi_col_sb)];
            bool is_720p_or_larger = AOMMIN(cm.width, cm.height) >= 720;
            uint thresh_spatial_var =
                (cpi.oxcf.speed >= 11 && !is_720p_or_larger &&
                 cpi.oxcf.tune_cfg.content != AOM_CONTENT_SCREEN)
                    ? 400u
                    : uint.MaxValue;
            // For skip_cdef_sb = 1: do not skip if allow_cdef_skipping is false or
            // intra or new mv is picked, with possible conidition on spatial variance.
            // For skip_cdef_sb >= 2: more aggressive mode to always skip unless
            // allow_cdef_skipping is false and source_variance is non-zero.
            if (cpi.sf.rt_sf.skip_cdef_sb >= 2)
            {
                mi_sb0.cdef_strength = (sbyte)B(
                    mi_sb0.cdef_strength != 0 &&
                    (allow_cdef_skipping || x.source_variance == 0));
            }
            else
            {
                mi_sb0.cdef_strength = (sbyte)B(
                    mi_sb0.cdef_strength != 0 && allow_cdef_skipping &&
                    !(x.source_variance < thresh_spatial_var &&
                      (mbmi.mode < INTRA_MODES || mbmi.mode == NEWMV)));
            }
            // Store in the pickmode context.
            ctx.mic.cdef_strength = mi_sb0.cdef_strength;
        }
        x.rdmult = orig_rdmult;
        ctx.rd_stats.rate = rd_cost.rate;
        ctx.rd_stats.dist = rd_cost.dist;
        ctx.rd_stats.rdcost = rd_cost.rdcost;
    }

    // [subset] try_split_partition / calc_do_split_flag: nonrd_check_partition_split is 0
    // in the real-time subset. try_merge (and av1_is_leaf_split_partition):
    // nonrd_check_partition_merge_mode is 0 at speed >= 8.

    // [port] scratch for direct_partition_merging(): C `int_mv frame_mv[MB_MODE_COUNT]
    // [REF_FRAMES]` and `struct buf_2d yv12_mb[REF_FRAMES][MAX_MB_PLANE]`.
    [ThreadStatic] private static int_mv[][] s_dpm_frame_mv;
    [ThreadStatic] private static buf_2d[][] s_dpm_yv12_mb;

    // Evaluate if the sub-partitions can be merged directly into a large partition
    // without calculating the RD cost.
    private static void direct_partition_merging(AV1_COMP cpi, ThreadData td,
                                                 TileDataEnc tile_data, Ptr<MB_MODE_INFO> mib,
                                                 int mi_row, int mi_col, BLOCK_SIZE bsize)
    {
        AV1_COMMON cm = cpi.common;
        CommonModeInfoParams mi_params = cm.mi_params;
        ref TileInfo tile_info = ref tile_data.tile_info;
        MACROBLOCK x = td.mb;
        MACROBLOCKD xd = x.e_mbd;
        int bs = mi_size_wide[bsize];
        int hbs = bs / 2;
        PARTITION_TYPE partition =
            (bsize >= BLOCK_8X8) ? get_partition(cm, mi_row, mi_col, bsize)
                                 : PARTITION_NONE;
        BLOCK_SIZE subsize = get_partition_subsize(bsize, partition);

        Ptr<MB_MODE_INFO> b0 = mib;
        Ptr<MB_MODE_INFO> b1 = mib + hbs;
        Ptr<MB_MODE_INFO> b2 = mib + hbs * mi_params.mi_stride;
        Ptr<MB_MODE_INFO> b3 = mib + hbs * mi_params.mi_stride + hbs;

        // Check if the following conditions are met. This can be updated
        // later with more support added.
        bool further_split = b0[0].bsize < subsize || b1[0].bsize < subsize ||
                             b2[0].bsize < subsize || b3[0].bsize < subsize;
        if (further_split) return;

        bool no_skip = b0[0].skip_txfm == 0 || b1[0].skip_txfm == 0 ||
                       b2[0].skip_txfm == 0 || b3[0].skip_txfm == 0;
        if (no_skip) return;

        bool compound = (b0[0].ref_frame[1] != b1[0].ref_frame[1] ||
                         b0[0].ref_frame[1] != b2[0].ref_frame[1] ||
                         b0[0].ref_frame[1] != b3[0].ref_frame[1] ||
                         b0[0].ref_frame[1] > NONE_FRAME);
        if (compound) return;

        // Intra modes aren't considered here.
        bool different_ref = (b0[0].ref_frame[0] != b1[0].ref_frame[0] ||
                              b0[0].ref_frame[0] != b2[0].ref_frame[0] ||
                              b0[0].ref_frame[0] != b3[0].ref_frame[0] ||
                              b0[0].ref_frame[0] <= INTRA_FRAME);
        if (different_ref) return;

        bool different_mode =
            (b0[0].mode != b1[0].mode || b0[0].mode != b2[0].mode ||
             b0[0].mode != b3[0].mode);
        if (different_mode) return;

        bool unsupported_mode =
            (b0[0].mode != NEARESTMV && b0[0].mode != GLOBALMV);
        if (unsupported_mode) return;

        bool different_mv = (b0[0].mv[0].as_int != b1[0].mv[0].as_int ||
                             b0[0].mv[0].as_int != b2[0].mv[0].as_int ||
                             b0[0].mv[0].as_int != b3[0].mv[0].as_int);
        if (different_mv) return;

        bool unsupported_motion_mode =
            (b0[0].motion_mode != b1[0].motion_mode ||
             b0[0].motion_mode != b2[0].motion_mode ||
             b0[0].motion_mode != b3[0].motion_mode ||
             b0[0].motion_mode != SIMPLE_TRANSLATION);
        if (unsupported_motion_mode) return;

        bool diffent_filter =
            (b0[0].interp_filters.as_int != b1[0].interp_filters.as_int ||
             b0[0].interp_filters.as_int != b2[0].interp_filters.as_int ||
             b0[0].interp_filters.as_int != b3[0].interp_filters.as_int);
        if (diffent_filter) return;

        bool different_seg = (b0[0].segment_id != b1[0].segment_id ||
                              b0[0].segment_id != b2[0].segment_id ||
                              b0[0].segment_id != b3[0].segment_id);
        if (different_seg) return;

        // Evaluate the ref_mv.
        Ptr<MB_MODE_INFO> this_mi = mib;
        BLOCK_SIZE orig_bsize = this_mi[0].bsize;
        PARTITION_TYPE orig_partition = this_mi[0].partition;
        byte orig_num_proj_ref = this_mi[0].num_proj_ref;

        this_mi[0].bsize = bsize;
        this_mi[0].partition = PARTITION_NONE;
        this_mi[0].skip_txfm = 1;

        // TODO(yunqing): functions called below can be optimized by
        // removing unrelated operations.
        av1_set_offsets_without_segment_id(cpi, in tile_data.tile_info, x, mi_row,
                                           mi_col, bsize);

        MV_REFERENCE_FRAME ref_frame = this_mi[0].ref_frame[0];
        int_mv[][] frame_mv = s_dpm_frame_mv ??= new_jagged<int_mv>(MB_MODE_COUNT, REF_FRAMES);
        buf_2d[][] yv12_mb = s_dpm_yv12_mb ??= new_jagged<buf_2d>(REF_FRAMES, MAX_MB_PLANE);
        int force_skip_low_temp_var = 0;
        int skip_pred_mv = 0;
        bool use_scaled_ref = false; // C: uninitialized; written by find_predictors()

        for (int i = 0; i < MB_MODE_COUNT; ++i)
        {
            for (int j = 0; j < REF_FRAMES; ++j)
            {
                frame_mv[i][j].as_int = INVALID_MV;
            }
        }
        av1_copy(x.color_sensitivity, x.color_sensitivity_sb);
        skip_pred_mv = B(x.nonrd_prune_ref_frame_search > 2 &&
                         x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] != 2 &&
                         x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] != 2);

        find_predictors(cpi, x, ref_frame, frame_mv, yv12_mb, bsize,
                        force_skip_low_temp_var, skip_pred_mv, ref use_scaled_ref);

        int continue_merging = 1;
        if (frame_mv[NEARESTMV][ref_frame].as_mv.row != b0[0].mv[0].as_mv.row ||
            frame_mv[NEARESTMV][ref_frame].as_mv.col != b0[0].mv[0].as_mv.col)
            continue_merging = 0;

        if (continue_merging == 0)
        {
            this_mi[0].bsize = orig_bsize;
            this_mi[0].partition = orig_partition;

            // TODO(yunqing): Store the results and restore here instead of
            // calling find_predictors() again.
            av1_set_offsets_without_segment_id(cpi, in tile_data.tile_info, x, mi_row,
                                               mi_col, this_mi[0].bsize);
            find_predictors(cpi, x, ref_frame, frame_mv, yv12_mb, this_mi[0].bsize,
                            force_skip_low_temp_var, skip_pred_mv, ref use_scaled_ref);
            this_mi[0].num_proj_ref = orig_num_proj_ref;
        }
        else
        {
            scale_factors sf = get_ref_scale_factors(cm, ref_frame);
            int is_scaled = av1_is_scaled(sf);
            bool is_y_subpel_mv = (Math.Abs((int)this_mi[0].mv[0].as_mv.row) % 8) != 0 ||
                                  (Math.Abs((int)this_mi[0].mv[0].as_mv.col) % 8) != 0;
            bool is_uv_subpel_mv = (Math.Abs((int)this_mi[0].mv[0].as_mv.row) % 16) != 0 ||
                                   (Math.Abs((int)this_mi[0].mv[0].as_mv.col) % 16) != 0;

            if (cpi.ppi.use_svc != 0 || is_scaled != 0 || is_y_subpel_mv || is_uv_subpel_mv)
            {
                int num_planes_ = av1_num_planes(cm);
                set_ref_ptrs(cm, xd, ref_frame, this_mi[0].ref_frame[1]);
                YV12_BUFFER_CONFIG cfg = get_ref_frame_yv12_buf(cm, ref_frame);
                av1_setup_pre_planes(xd, 0, cfg, mi_row, mi_col,
                                     xd.block_ref_scale_factors[0], num_planes_);

                if (cpi.ppi.use_svc == 0 && is_scaled == 0 && !is_y_subpel_mv)
                {
                    Debug.Assert(is_uv_subpel_mv);
                    av1_enc_build_inter_predictor(cm, xd, mi_row, mi_col, Ptr<BUFFER_SET>.Null,
                                                  bsize, 1, num_planes_ - 1);
                }
                else
                {
                    av1_enc_build_inter_predictor(cm, xd, mi_row, mi_col, Ptr<BUFFER_SET>.Null,
                                                  bsize, 0, num_planes_ - 1);
                }
            }

            // Copy out mbmi_ext information.
            MB_MODE_INFO_EXT mbmi_ext = x.mbmi_ext;
            av1_copy_mbmi_ext_to_mbmi_ext_frame(
                ref x.mbmi_ext_frame.Value, mbmi_ext,
                (byte)av1_ref_frame_type(this_mi[0].ref_frame));

            BLOCK_SIZE this_subsize =
                get_partition_subsize(bsize, this_mi[0].partition);
            // Update partition contexts.
            update_ext_partition_context(xd, mi_row, mi_col, this_subsize, bsize,
                                         this_mi[0].partition);

            int num_planes = av1_num_planes(cm);
            av1_reset_entropy_context(xd, bsize, num_planes);

            // Note: use x->txfm_search_params.tx_mode_search_type instead of
            // cm->features.tx_mode here.
            TX_SIZE tx_size =
                tx_size_from_tx_mode(bsize, x.txfm_search_params.tx_mode_search_type);
            if (xd.lossless[this_mi[0].segment_id] != 0) tx_size = TX_4X4;
            this_mi[0].tx_size = tx_size;
            Array.Fill(this_mi[0].inter_tx_size, this_mi[0].tx_size);

            // Update txfm contexts.
            xd.above_txfm_context =
                cm.above_contexts.txfm[tile_info.tile_row] + mi_col;
            xd.left_txfm_context =
                new Ptr<TXFM_CONTEXT>(xd.left_txfm_context_buffer, mi_row & MAX_MIB_MASK);
            set_txfm_ctxs(this_mi[0].tx_size, xd.width, xd.height,
                          B(this_mi[0].skip_txfm != 0 && is_inter_block(this_mi[0]) != 0), xd);

            // Update mi for this partition block.
            for (int y = 0; y < bs; y++)
            {
                for (int x_idx = 0; x_idx < bs; x_idx++)
                {
                    this_mi[x_idx + y * mi_params.mi_stride] = this_mi[0];
                }
            }

            this_mi[0].num_proj_ref = 0;
            if (warped_motion_update_num_proj_ref(cpi, this_mi[0]) != 0)
            {
                // [subset] av1_findSamples: warped motion is disabled in the real-time
                // subset (allow_warped_motion == 0).
                throw new NotSupportedException("direct_partition_merging: av1_findSamples");
            }
        }
    }

    /*!\brief AV1 block partition application (minimal RD search).
     *
     * Encode the block by applying pre-calculated partition patterns that are
     * represented by coding block sizes stored in the mbmi array. The only
     * partition adjustment allowed is merging leaf split nodes if it leads to a
     * lower rd cost. The partition types are limited to a basic set: none, horz,
     * vert, and split. This function is only used in the real-time mode.
     */
    internal static void av1_nonrd_use_partition(AV1_COMP cpi, ThreadData td,
                                                 TileDataEnc tile_data, Ptr<MB_MODE_INFO> mib,
                                                 ref Ptr<TokenExtra> tp, int mi_row, int mi_col,
                                                 BLOCK_SIZE bsize, PC_TREE pc_tree)
    {
        AV1_COMMON cm = cpi.common;
        CommonModeInfoParams mi_params = cm.mi_params;
        ref TileInfo tile_info = ref tile_data.tile_info;
        MACROBLOCK x = td.mb;
        MACROBLOCKD xd = x.e_mbd;
        ModeCosts mode_costs = x.mode_costs;
        // Only square blocks from 8x8 to 128x128 are supported
        Debug.Assert(bsize >= BLOCK_8X8 && bsize <= BLOCK_128X128);
        int bs = mi_size_wide[bsize];
        int hbs = bs / 2;
        PARTITION_TYPE partition = (bsize >= BLOCK_8X8)
                                       ? get_partition(cm, mi_row, mi_col, bsize)
                                       : PARTITION_NONE;
        BLOCK_SIZE subsize = get_partition_subsize(bsize, partition);
        Debug.Assert(subsize <= BLOCK_LARGEST);
        int pl = (bsize >= BLOCK_8X8)
                     ? partition_plane_context(xd, mi_row, mi_col, bsize)
                     : 0;

        RD_STATS dummy_cost = default;
        av1_invalid_rd_stats(ref dummy_cost);

        if (mi_row >= mi_params.mi_rows || mi_col >= mi_params.mi_cols) return;

        Debug.Assert(mi_size_wide[bsize] == mi_size_high[bsize]);

        xd.above_txfm_context =
            cm.above_contexts.txfm[tile_info.tile_row] + mi_col;
        xd.left_txfm_context =
            new Ptr<TXFM_CONTEXT>(xd.left_txfm_context_buffer, mi_row & MAX_MIB_MASK);

        // Initialize default mode evaluation params
        set_mode_eval_params(cpi, x, DEFAULT_EVAL);

        x.reuse_inter_pred = cpi.sf.rt_sf.reuse_inter_pred_nonrd != 0;

        int change_none_to_split = 0;
        if (partition == PARTITION_NONE &&
            cpi.sf.rt_sf.nonrd_check_partition_split == 1)
        {
            // [subset] try_split_partition: nonrd_check_partition_split is 0 in the
            // real-time subset.
            throw new NotSupportedException("av1_nonrd_use_partition: try_split_partition");
        }

        pc_tree.partitioning = partition;

        switch (partition)
        {
            case PARTITION_NONE:
                if (pc_tree.none == null)
                {
                    pc_tree.none = av1_alloc_pmc(cpi, bsize, td.shared_coeff_buf);
                    if (pc_tree.none == null)
                        throw new AomException(AOM_CODEC_MEM_ERROR,
                                               "Failed to allocate PICK_MODE_CONTEXT");
                }
                else
                {
                    av1_reset_pmc(pc_tree.none);
                }
                pick_sb_modes_nonrd(cpi, tile_data, x, mi_row, mi_col, ref dummy_cost, bsize,
                                    pc_tree.none);
                encode_b_nonrd(cpi, tile_data, td, ref tp, mi_row, mi_col, 0, bsize,
                               partition, pc_tree.none, Ptr<int>.Null);
                break;
            case PARTITION_VERT:
                for (int i = 0; i < SUB_PARTITIONS_RECT; ++i)
                {
                    if (pc_tree.vertical[i] == null)
                    {
                        pc_tree.vertical[i] =
                            av1_alloc_pmc(cpi, subsize, td.shared_coeff_buf);
                        if (pc_tree.vertical[i] == null)
                            throw new AomException(AOM_CODEC_MEM_ERROR,
                                                   "Failed to allocate PICK_MODE_CONTEXT");
                    }
                    else
                    {
                        av1_reset_pmc(pc_tree.vertical[i]);
                    }
                }
                pick_sb_modes_nonrd(cpi, tile_data, x, mi_row, mi_col, ref dummy_cost,
                                    subsize, pc_tree.vertical[0]);
                encode_b_nonrd(cpi, tile_data, td, ref tp, mi_row, mi_col, 0, subsize,
                               PARTITION_VERT, pc_tree.vertical[0], Ptr<int>.Null);
                if (mi_col + hbs < mi_params.mi_cols && bsize > BLOCK_8X8)
                {
                    pick_sb_modes_nonrd(cpi, tile_data, x, mi_row, mi_col + hbs,
                                        ref dummy_cost, subsize, pc_tree.vertical[1]);
                    encode_b_nonrd(cpi, tile_data, td, ref tp, mi_row, mi_col + hbs, 0, subsize,
                                   PARTITION_VERT, pc_tree.vertical[1], Ptr<int>.Null);
                }
                break;
            case PARTITION_HORZ:
                for (int i = 0; i < SUB_PARTITIONS_RECT; ++i)
                {
                    if (pc_tree.horizontal[i] == null)
                    {
                        pc_tree.horizontal[i] =
                            av1_alloc_pmc(cpi, subsize, td.shared_coeff_buf);
                        if (pc_tree.horizontal[i] == null)
                            throw new AomException(AOM_CODEC_MEM_ERROR,
                                                   "Failed to allocate PICK_MODE_CONTEXT");
                    }
                    else
                    {
                        av1_reset_pmc(pc_tree.horizontal[i]);
                    }
                }
                pick_sb_modes_nonrd(cpi, tile_data, x, mi_row, mi_col, ref dummy_cost,
                                    subsize, pc_tree.horizontal[0]);
                encode_b_nonrd(cpi, tile_data, td, ref tp, mi_row, mi_col, 0, subsize,
                               PARTITION_HORZ, pc_tree.horizontal[0], Ptr<int>.Null);

                if (mi_row + hbs < mi_params.mi_rows && bsize > BLOCK_8X8)
                {
                    pick_sb_modes_nonrd(cpi, tile_data, x, mi_row + hbs, mi_col,
                                        ref dummy_cost, subsize, pc_tree.horizontal[1]);
                    encode_b_nonrd(cpi, tile_data, td, ref tp, mi_row + hbs, mi_col, 0, subsize,
                                   PARTITION_HORZ, pc_tree.horizontal[1], Ptr<int>.Null);
                }
                break;
            case PARTITION_SPLIT:
                for (int i = 0; i < SUB_PARTITIONS_SPLIT; ++i)
                {
                    if (pc_tree.split[i] == null)
                    {
                        pc_tree.split[i] = av1_alloc_pc_tree_node(subsize);
                        if (pc_tree.split[i] == null)
                            throw new AomException(AOM_CODEC_MEM_ERROR,
                                                   "Failed to allocate PC_TREE");
                    }
                    pc_tree.split[i].index = i;
                }
                if (cpi.sf.rt_sf.nonrd_check_partition_merge_mode != 0 &&
                    av1_is_leaf_split_partition(cm, mi_row, mi_col, bsize) &&
                    frame_is_intra_only(cm) == 0 && bsize <= BLOCK_64X64)
                {
                    // [subset] try_merge: nonrd_check_partition_merge_mode is 0 at
                    // speed >= 8.
                    throw new NotSupportedException("av1_nonrd_use_partition: try_merge");
                }
                else
                {
                    for (int i = 0; i < SUB_PARTITIONS_SPLIT; i++)
                    {
                        int x_idx = (i & 1) * hbs;
                        int y_idx = (i >> 1) * hbs;
                        int jj = i >> 1, ii = i & 0x01;
                        if ((mi_row + y_idx >= mi_params.mi_rows) ||
                            (mi_col + x_idx >= mi_params.mi_cols))
                            continue;
                        av1_nonrd_use_partition(
                            cpi, td, tile_data,
                            mib + jj * hbs * mi_params.mi_stride + ii * hbs, ref tp,
                            mi_row + y_idx, mi_col + x_idx, subsize, pc_tree.split[i]);
                    }

                    if (change_none_to_split == 0)
                    {
                        // Note: Palette, cfl are not supported.
                        if (frame_is_intra_only(cm) == 0 && tile_data.allow_update_cdf == 0 &&
                            cpi.sf.rt_sf.partition_direct_merging != 0 &&
                            mode_costs.partition_cost[pl][PARTITION_NONE] <
                                mode_costs.partition_cost[pl][PARTITION_SPLIT] &&
                            (mi_row + bs <= mi_params.mi_rows) &&
                            (mi_col + bs <= mi_params.mi_cols))
                        {
                            direct_partition_merging(cpi, td, tile_data, mib, mi_row, mi_col,
                                                     bsize);
                        }
                    }
                }
                break;
            case PARTITION_VERT_A:
            case PARTITION_VERT_B:
            case PARTITION_HORZ_A:
            case PARTITION_HORZ_B:
            case PARTITION_HORZ_4:
            case PARTITION_VERT_4:
                Debug.Assert(false, "Cannot handle extended partition types");
                goto default;
            default: Debug.Assert(false); break;
        }
    }

    // [port] av1_is_leaf_split_partition (partition_strategy.c) is only evaluated after
    // nonrd_check_partition_merge_mode, which is 0 in the subset; the short-circuit means it
    // is never called. This placeholder keeps the C condition intact.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool av1_is_leaf_split_partition(AV1_COMMON cm, int mi_row, int mi_col,
                                                    BLOCK_SIZE bsize) =>
        throw new NotSupportedException("av1_is_leaf_split_partition");

    // [subset] the remaining functions of partition_search.c (rd_try_subblock onwards: the
    // RD partition search, av1_rd_pick_partition, ML partition, ...) are
    // #if !CONFIG_REALTIME_ONLY or only used by the RD path.
}
