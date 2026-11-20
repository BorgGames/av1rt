// Port of libaom av1/encoder/bitstream.c
//
// CONFIG_REALTIME_ONLY is 0 in the oracle build, but the loop-restoration writers it guards
// are out of the subset (enable_restoration is 0). CONFIG_RD_DEBUG, CONFIG_ENTROPY_STATS,
// ENC_MISMATCH_DEBUG and CONFIG_BITSTREAM_DEBUG are 0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class bitstream
{
    private const int ENC_MISMATCH_DEBUG = 0;
    private const int SETUP_TIME_OH_CONST = 5;     // Setup time overhead constant per worker
    private const int JOB_DISP_TIME_OH_CONST = 1;  // Job dispatch time overhead per tile

    // [subset] write_uniform: only used by pack_map_tokens (palette).

    // [subset] loop_restoration_write_sb_coeffs: loop restoration is off in the subset.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_intra_y_mode_kf(FRAME_CONTEXT frame_ctx,
                                              MB_MODE_INFO mi,
                                              MB_MODE_INFO above_mi,
                                              MB_MODE_INFO left_mi,
                                              PREDICTION_MODE mode, aom_writer w)
    {
        Debug.Assert(is_intrabc_block(mi) == 0);
        aom_write_symbol(w, mode, get_y_mode_cdf(frame_ctx, above_mi, left_mi),
                         INTRA_MODES);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_inter_mode(aom_writer w, PREDICTION_MODE mode,
                                         FRAME_CONTEXT ec_ctx,
                                         short mode_ctx)
    {
        short newmv_ctx = (short)(mode_ctx & NEWMV_CTX_MASK);

        aom_write_symbol(w, B(mode != NEWMV), ec_ctx.newmv_cdf[newmv_ctx], 2);

        if (mode != NEWMV)
        {
            short zeromv_ctx =
                (short)((mode_ctx >> GLOBALMV_OFFSET) & GLOBALMV_CTX_MASK);
            aom_write_symbol(w, B(mode != GLOBALMV), ec_ctx.zeromv_cdf[zeromv_ctx], 2);

            if (mode != GLOBALMV)
            {
                short refmv_ctx = (short)((mode_ctx >> REFMV_OFFSET) & REFMV_CTX_MASK);
                aom_write_symbol(w, B(mode != NEARESTMV), ec_ctx.refmv_cdf[refmv_ctx], 2);
            }
        }
    }

    // [port] av1_drl_ctx() takes `const uint16_t *`; MB_MODE_INFO_EXT_FRAME::weight is an inline
    // array, so it is copied into a per-thread scratch array first.
    [ThreadStatic] private static ushort[] drl_weight_scratch;

    private static Ptr<ushort> ext_frame_weight(in MB_MODE_INFO_EXT_FRAME mbmi_ext_frame)
    {
        ushort[] wgt = drl_weight_scratch ??= new ushort[USABLE_REF_MV_STACK_SIZE];
        for (int i = 0; i < USABLE_REF_MV_STACK_SIZE; i++) wgt[i] = mbmi_ext_frame.weight[i];
        return wgt;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_drl_idx(FRAME_CONTEXT ec_ctx,
                                      MB_MODE_INFO mbmi,
                                      in MB_MODE_INFO_EXT_FRAME mbmi_ext_frame,
                                      aom_writer w)
    {
        Debug.Assert(mbmi.ref_mv_idx < 3);

        bool new_mv = mbmi.mode == NEWMV || mbmi.mode == NEW_NEWMV;
        if (new_mv)
        {
            int idx;
            for (idx = 0; idx < 2; ++idx)
            {
                if (mbmi_ext_frame.ref_mv_count > idx + 1)
                {
                    byte drl_ctx = av1_drl_ctx(ext_frame_weight(in mbmi_ext_frame), idx);

                    aom_write_symbol(w, B(mbmi.ref_mv_idx != idx), ec_ctx.drl_cdf[drl_ctx],
                                     2);
                    if (mbmi.ref_mv_idx == idx) return;
                }
            }
            return;
        }

        if (have_nearmv_in_inter_mode(mbmi.mode) != 0)
        {
            int idx;
            // TODO(jingning): Temporary solution to compensate the NEARESTMV offset.
            for (idx = 1; idx < 3; ++idx)
            {
                if (mbmi_ext_frame.ref_mv_count > idx + 1)
                {
                    byte drl_ctx = av1_drl_ctx(ext_frame_weight(in mbmi_ext_frame), idx);
                    aom_write_symbol(w, B(mbmi.ref_mv_idx != (idx - 1)),
                                     ec_ctx.drl_cdf[drl_ctx], 2);
                    if (mbmi.ref_mv_idx == (idx - 1)) return;
                }
            }
            return;
        }
    }

    // [subset] write_inter_compound_mode: compound prediction is off in the subset.

    private static void write_tx_size_vartx(MACROBLOCKD xd,
                                            MB_MODE_INFO mbmi,
                                            TX_SIZE tx_size, int depth, int blk_row,
                                            int blk_col, aom_writer w)
    {
        FRAME_CONTEXT ec_ctx = xd.tile_ctx;
        int max_blocks_high = max_block_high(xd, mbmi.bsize, 0);
        int max_blocks_wide = max_block_wide(xd, mbmi.bsize, 0);

        if (blk_row >= max_blocks_high || blk_col >= max_blocks_wide) return;

        if (depth == MAX_VARTX_DEPTH)
        {
            txfm_partition_update(xd.above_txfm_context + blk_col,
                                  xd.left_txfm_context + blk_row, tx_size, tx_size);
            return;
        }

        int ctx = txfm_partition_context(xd.above_txfm_context + blk_col,
                                         xd.left_txfm_context + blk_row,
                                         mbmi.bsize, tx_size);
        int txb_size_index =
            av1_get_txb_size_index(mbmi.bsize, blk_row, blk_col);
        bool write_txfm_partition =
            tx_size == mbmi.inter_tx_size[txb_size_index];
        if (write_txfm_partition)
        {
            aom_write_symbol(w, 0, ec_ctx.txfm_partition_cdf[ctx], 2);

            txfm_partition_update(xd.above_txfm_context + blk_col,
                                  xd.left_txfm_context + blk_row, tx_size, tx_size);
            // TODO(yuec): set correct txfm partition update for qttx
        }
        else
        {
            TX_SIZE sub_txs = sub_tx_size_map[tx_size];
            int bsw = tx_size_wide_unit[sub_txs];
            int bsh = tx_size_high_unit[sub_txs];

            aom_write_symbol(w, 1, ec_ctx.txfm_partition_cdf[ctx], 2);

            if (sub_txs == TX_4X4)
            {
                txfm_partition_update(xd.above_txfm_context + blk_col,
                                      xd.left_txfm_context + blk_row, sub_txs, tx_size);
                return;
            }

            Debug.Assert(bsw > 0 && bsh > 0);
            for (int row = 0; row < tx_size_high_unit[tx_size]; row += bsh)
            {
                int offsetr = blk_row + row;
                for (int col = 0; col < tx_size_wide_unit[tx_size]; col += bsw)
                {
                    int offsetc = blk_col + col;
                    write_tx_size_vartx(xd, mbmi, sub_txs, depth + 1, offsetr, offsetc, w);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_selected_tx_size(MACROBLOCKD xd,
                                               aom_writer w)
    {
        MB_MODE_INFO mbmi = xd.mi[0];
        BLOCK_SIZE bsize = mbmi.bsize;
        FRAME_CONTEXT ec_ctx = xd.tile_ctx;
        if (block_signals_txsize(bsize) != 0)
        {
            TX_SIZE tx_size = mbmi.tx_size;
            int tx_size_ctx = get_tx_size_context(xd);
            int depth = tx_size_to_depth(tx_size, bsize);
            int max_depths = bsize_to_max_depth(bsize);
            int tx_size_cat = bsize_to_tx_size_cat(bsize);

            Debug.Assert(depth >= 0 && depth <= max_depths);
            Debug.Assert(is_inter_block(mbmi) == 0);

            aom_write_symbol(w, depth, ec_ctx.tx_size_cdf[tx_size_cat][tx_size_ctx],
                             max_depths + 1);
        }
    }

    private static int write_skip(AV1_COMMON cm, MACROBLOCKD xd,
                                  byte segment_id, MB_MODE_INFO mi,
                                  aom_writer w)
    {
        if (segfeature_active(cm.seg, segment_id, SEG_LVL_SKIP) != 0)
        {
            return 1;
        }
        else
        {
            int skip_txfm = mi.skip_txfm;
            int ctx = av1_get_skip_txfm_context(xd);
            FRAME_CONTEXT ec_ctx = xd.tile_ctx;
            aom_write_symbol(w, skip_txfm, ec_ctx.skip_txfm_cdfs[ctx], 2);
            return skip_txfm;
        }
    }

    private static int write_skip_mode(AV1_COMMON cm, MACROBLOCKD xd,
                                       byte segment_id, MB_MODE_INFO mi,
                                       aom_writer w)
    {
        if (cm.current_frame.skip_mode_info.skip_mode_flag == 0) return 0;
        // [subset] skip_mode_flag can only be set when skip_mode_allowed, which needs
        // enable_order_hint (off in the subset). The rest of the function (and
        // av1_get_skip_mode_context) is not ported.
        throw new NotSupportedException("[subset] write_skip_mode: skip mode needs order hints");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_is_inter(AV1_COMMON cm, MACROBLOCKD xd,
                                       byte segment_id, aom_writer w,
                                       int is_inter)
    {
        if (segfeature_active(cm.seg, segment_id, SEG_LVL_REF_FRAME) == 0)
        {
            if (segfeature_active(cm.seg, segment_id, SEG_LVL_GLOBALMV) != 0)
            {
                Debug.Assert(is_inter != 0);
                return;
            }
            int ctx = av1_get_intra_inter_context(xd);
            FRAME_CONTEXT ec_ctx = xd.tile_ctx;
            aom_write_symbol(w, is_inter, ec_ctx.intra_inter_cdf[ctx], 2);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_motion_mode(AV1_COMMON cm, MACROBLOCKD xd,
                                          MB_MODE_INFO mbmi, aom_writer w)
    {
        if (cm.features.switchable_motion_mode)
        {
            // [subset] motion_mode_allowed: switchable_motion_mode needs OBMC or warped
            // motion, both off in the subset.
            throw new NotSupportedException("[subset] write_motion_mode: switchable_motion_mode");
        }
        MOTION_MODE last_motion_mode_allowed = SIMPLE_TRANSLATION;
        Debug.Assert(mbmi.motion_mode <= last_motion_mode_allowed);
        switch (last_motion_mode_allowed)
        {
            case SIMPLE_TRANSLATION: break;
            case OBMC_CAUSAL:
                aom_write_symbol(w, B(mbmi.motion_mode == OBMC_CAUSAL),
                                 xd.tile_ctx.obmc_cdf[mbmi.bsize], 2);
                break;
            default:
                aom_write_symbol(w, mbmi.motion_mode,
                                 xd.tile_ctx.motion_mode_cdf[mbmi.bsize],
                                 MOTION_MODES);
                break;
        }
    }

    // [subset] write_delta_qindex, write_delta_lflevel: delta q / delta lf are off
    // (deltaq_mode 0), see write_delta_q_params.

    // [subset] pack_map_tokens: palette is off in the subset.

    private static void pack_txb_tokens(
        aom_writer w, AV1_COMMON cm, MACROBLOCK x, ref Ptr<TokenExtra> tp,
        Ptr<TokenExtra> tok_end, MACROBLOCKD xd, MB_MODE_INFO mbmi,
        int plane, BLOCK_SIZE plane_bsize, aom_bit_depth_t bit_depth, int block,
        int blk_row, int blk_col, TX_SIZE tx_size, ref TOKEN_STATS token_stats)
    {
        int max_blocks_high = max_block_high(xd, plane_bsize, plane);
        int max_blocks_wide = max_block_wide(xd, plane_bsize, plane);

        if (blk_row >= max_blocks_high || blk_col >= max_blocks_wide) return;

        macroblockd_plane pd = xd.plane[plane];
        TX_SIZE plane_tx_size =
            plane != 0 ? av1_get_max_uv_txsize(mbmi.bsize, pd.subsampling_x,
                                               pd.subsampling_y)
                       : mbmi.inter_tx_size[av1_get_txb_size_index(plane_bsize, blk_row,
                                                                   blk_col)];

        if (tx_size == plane_tx_size || plane != 0)
        {
            av1_write_coeffs_txb(cm, x, w, blk_row, blk_col, plane, block, tx_size);
        }
        else
        {
            TX_SIZE sub_txs = sub_tx_size_map[tx_size];
            int bsw = tx_size_wide_unit[sub_txs];
            int bsh = tx_size_high_unit[sub_txs];
            int step = bsh * bsw;
            int row_end =
                AOMMIN(tx_size_high_unit[tx_size], max_blocks_high - blk_row);
            int col_end =
                AOMMIN(tx_size_wide_unit[tx_size], max_blocks_wide - blk_col);

            Debug.Assert(bsw > 0 && bsh > 0);

            for (int r = 0; r < row_end; r += bsh)
            {
                int offsetr = blk_row + r;
                for (int c = 0; c < col_end; c += bsw)
                {
                    int offsetc = blk_col + c;
                    pack_txb_tokens(w, cm, x, ref tp, tok_end, xd, mbmi, plane, plane_bsize,
                                    bit_depth, block, offsetr, offsetc, sub_txs,
                                    ref token_stats);
                    block += step;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_spatial_segment_id(
        CommonModeInfoParams mi_params, Ptr<byte> segment_ids,
        BLOCK_SIZE bsize, int mi_row, int mi_col, byte segment_id)
    {
        int mi_offset = mi_row * mi_params.mi_cols + mi_col;
        int bw = mi_size_wide[bsize];
        int bh = mi_size_high[bsize];
        int xmis = AOMMIN(mi_params.mi_cols - mi_col, bw);
        int ymis = AOMMIN(mi_params.mi_rows - mi_row, bh);

        int mi_stride = mi_params.mi_cols;

        set_segment_id(segment_ids, mi_offset, xmis, ymis, mi_stride, segment_id);
    }

    internal static int av1_neg_interleave(int x, int @ref, int max)
    {
        Debug.Assert(x < max);
        int diff = x - @ref;
        if (@ref == 0) return x;
        if (@ref >= (max - 1)) return -x + max - 1;
        if (2 * @ref < max)
        {
            if (Math.Abs(diff) <= @ref)
            {
                if (diff > 0)
                    return (diff << 1) - 1;
                else
                    return ((-diff) << 1);
            }
            return x;
        }
        else
        {
            if (Math.Abs(diff) < (max - @ref))
            {
                if (diff > 0)
                    return (diff << 1) - 1;
                else
                    return ((-diff) << 1);
            }
            return (max - x) - 1;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_segment_id(AV1_COMP cpi, MACROBLOCKD xd,
                                         MB_MODE_INFO mbmi,
                                         aom_writer w,
                                         segmentation seg,
                                         segmentation_probs segp,
                                         int skip_txfm)
    {
        if (seg.enabled == 0 || seg.update_map == 0) return;

        AV1_COMMON cm = cpi.common;
        int cdf_num = 0;
        byte pred = av1_get_spatial_seg_pred(
            cm, xd, ref cdf_num, cpi.cyclic_refresh.skip_over4x4);
        int mi_row = xd.mi_row;
        int mi_col = xd.mi_col;

        if (skip_txfm != 0)
        {
            // Still need to transmit tx size for intra blocks even if skip_txfm is
            // true. Changing segment_id may make the tx size become invalid, e.g
            // changing from lossless to lossy.
            Debug.Assert(is_inter_block(mbmi) != 0 || !cpi.enc_seg.has_lossless_segment);

            set_spatial_segment_id(cm.mi_params, cm.cur_frame.seg_map, mbmi.bsize,
                                   mi_row, mi_col, pred);
            set_spatial_segment_id(cm.mi_params, cpi.enc_seg.map, mbmi.bsize,
                                   mi_row, mi_col, pred);
            /* mbmi is read only but we need to update segment_id */
            mbmi.segment_id = pred;
            return;
        }

        int coded_id =
            av1_neg_interleave(mbmi.segment_id, pred, seg.last_active_segid + 1);
        Ptr<aom_cdf_prob> pred_cdf = segp.spatial_pred_seg_cdf[cdf_num];
        aom_write_symbol(w, coded_id, pred_cdf, MAX_SEGMENTS);
        set_spatial_segment_id(cm.mi_params, cm.cur_frame.seg_map, mbmi.bsize,
                               mi_row, mi_col, mbmi.segment_id);
    }

    // #define WRITE_REF_BIT(bname, pname) \
    //   aom_write_symbol(w, bname, av1_get_pred_cdf_##pname(xd), 2)

    // This function encodes the reference frame
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_ref_frames(AV1_COMMON cm, MACROBLOCKD xd,
                                         aom_writer w)
    {
        MB_MODE_INFO mbmi = xd.mi[0];
        int is_compound = has_second_ref(mbmi);
        byte segment_id = mbmi.segment_id;

        // If segment level coding of this signal is disabled...
        // or the segment allows multiple reference frame options
        if (segfeature_active(cm.seg, segment_id, SEG_LVL_REF_FRAME) != 0)
        {
            Debug.Assert(is_compound == 0);
            Debug.Assert(mbmi.ref_frame[0] ==
                         get_segdata(cm.seg, segment_id, SEG_LVL_REF_FRAME));
        }
        else if (segfeature_active(cm.seg, segment_id, SEG_LVL_SKIP) != 0 ||
                 segfeature_active(cm.seg, segment_id, SEG_LVL_GLOBALMV) != 0)
        {
            Debug.Assert(is_compound == 0);
            Debug.Assert(mbmi.ref_frame[0] == LAST_FRAME);
        }
        else
        {
            // does the feature use compound prediction or not
            // (if not specified at the frame/segment level)
            if (cm.current_frame.reference_mode == REFERENCE_MODE_SELECT)
            {
                if (is_comp_ref_allowed(mbmi.bsize) != 0)
                    // [subset] av1_get_reference_mode_cdf: compound reference is off
                    // (reference_mode is SINGLE_REFERENCE in the subset).
                    throw new NotSupportedException(
                        "[subset] write_ref_frames: REFERENCE_MODE_SELECT");
            }
            else
            {
                Debug.Assert((is_compound == 0) ==
                             (cm.current_frame.reference_mode == SINGLE_REFERENCE));
            }

            if (is_compound != 0)
            {
                // [subset] compound reference frames (comp_ref_type, uni/bidir comp refs):
                // compound prediction is off in the subset.
                throw new NotSupportedException("[subset] write_ref_frames: compound");
            }
            else
            {
                int bit0 = B(mbmi.ref_frame[0] <= ALTREF_FRAME &&
                             mbmi.ref_frame[0] >= BWDREF_FRAME);
                aom_write_symbol(w, bit0, av1_get_pred_cdf_single_ref_p1(xd), 2);

                if (bit0 != 0)
                {
                    // [subset] BWDREF/ALTREF2/ALTREF single references (single_ref_p2,
                    // single_ref_p6): the realtime reference set never uses them (coverage 0;
                    // their pred_common contexts are not ported).
                    throw new NotSupportedException(
                        "[subset] write_ref_frames: backward single reference");
                }
                else
                {
                    int bit2 = B(mbmi.ref_frame[0] == LAST3_FRAME ||
                                 mbmi.ref_frame[0] == GOLDEN_FRAME);
                    aom_write_symbol(w, bit2, av1_get_pred_cdf_single_ref_p3(xd), 2);

                    if (bit2 == 0)
                    {
                        int bit3 = B(mbmi.ref_frame[0] != LAST_FRAME);
                        aom_write_symbol(w, bit3, av1_get_pred_cdf_single_ref_p4(xd), 2);
                    }
                    else
                    {
                        int bit4 = B(mbmi.ref_frame[0] != LAST3_FRAME);
                        aom_write_symbol(w, bit4, av1_get_pred_cdf_single_ref_p5(xd), 2);
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_filter_intra_mode_info(AV1_COMMON cm,
                                                     MACROBLOCKD xd,
                                                     MB_MODE_INFO mbmi,
                                                     aom_writer w)
    {
        if (av1_filter_intra_allowed(cm, mbmi) != 0)
        {
            aom_write_symbol(w, mbmi.filter_intra_mode_info.use_filter_intra,
                             xd.tile_ctx.filter_intra_cdfs[mbmi.bsize], 2);
            if (mbmi.filter_intra_mode_info.use_filter_intra != 0)
            {
                FILTER_INTRA_MODE mode =
                    mbmi.filter_intra_mode_info.filter_intra_mode;
                aom_write_symbol(w, mode, xd.tile_ctx.filter_intra_mode_cdf,
                                 FILTER_INTRA_MODES);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_angle_delta(aom_writer w, int angle_delta,
                                          Ptr<aom_cdf_prob> cdf)
    {
        aom_write_symbol(w, angle_delta + MAX_ANGLE_DELTA, cdf,
                         2 * MAX_ANGLE_DELTA + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_mb_interp_filter(AV1_COMMON cm, ThreadData td,
                                               aom_writer w)
    {
        MACROBLOCKD xd = td.mb.e_mbd;
        MB_MODE_INFO mbmi = xd.mi[0];
        FRAME_CONTEXT ec_ctx = xd.tile_ctx;

        if (av1_is_interp_needed(xd) == 0)
        {
            int_interpfilters filters = av1_broadcast_interp_filter(
                av1_unswitchable_filter(cm.features.interp_filter));
            Debug.Assert(mbmi.interp_filters.as_int == filters.as_int);
            return;
        }
        if (cm.features.interp_filter == SWITCHABLE)
        {
            int dir;
            for (dir = 0; dir < 2; ++dir)
            {
                int ctx = av1_get_pred_context_switchable_interp(xd, dir);
                InterpFilter filter =
                    av1_extract_interp_filter(mbmi.interp_filters, dir);
                aom_write_symbol(w, filter, ec_ctx.switchable_interp_cdf[ctx],
                                 SWITCHABLE_FILTERS);
                ++td.interp_filter_selected[filter];
                if (cm.seq_params.enable_dual_filter == 0) return;
            }
        }
    }

    // [subset] delta_encode_palette_colors, write_palette_colors_y, write_palette_colors_uv,
    // write_palette_mode_info: palette is off in the subset.

    internal static void av1_write_tx_type(AV1_COMMON cm, MACROBLOCKD xd,
                                           TX_TYPE tx_type, TX_SIZE tx_size, aom_writer w)
    {
        MB_MODE_INFO mbmi = xd.mi[0];
        FeatureFlags features = cm.features;
        int is_inter = is_inter_block(mbmi);
        if (get_ext_tx_types(tx_size, is_inter, B(features.reduced_tx_set_used)) > 1 &&
            ((cm.seg.enabled == 0 && cm.quant_params.base_qindex > 0) ||
             (cm.seg.enabled != 0 && xd.qindex[mbmi.segment_id] > 0)) &&
            mbmi.skip_txfm == 0 &&
            segfeature_active(cm.seg, mbmi.segment_id, SEG_LVL_SKIP) == 0)
        {
            FRAME_CONTEXT ec_ctx = xd.tile_ctx;
            TX_SIZE square_tx_size = txsize_sqr_map[tx_size];
            TxSetType tx_set_type = av1_get_ext_tx_set_type(
                tx_size, is_inter, B(features.reduced_tx_set_used));
            int eset =
                get_ext_tx_set(tx_size, is_inter, B(features.reduced_tx_set_used));
            // eset == 0 should correspond to a set with only DCT_DCT and there
            // is no need to send the tx_type
            Debug.Assert(eset > 0);
            Debug.Assert(av1_ext_tx_used[tx_set_type][tx_type] != 0);
            if (is_inter != 0)
            {
                aom_write_symbol(w, av1_ext_tx_ind[tx_set_type][tx_type],
                                 ec_ctx.inter_ext_tx_cdf[eset][square_tx_size],
                                 av1_num_ext_tx_set[tx_set_type]);
            }
            else
            {
                PREDICTION_MODE intra_dir;
                if (mbmi.filter_intra_mode_info.use_filter_intra != 0)
                    intra_dir =
                        fimode_to_intradir[mbmi.filter_intra_mode_info.filter_intra_mode];
                else
                    intra_dir = mbmi.mode;
                aom_write_symbol(
                    w, av1_ext_tx_ind[tx_set_type][tx_type],
                    ec_ctx.intra_ext_tx_cdf[eset][square_tx_size][intra_dir],
                    av1_num_ext_tx_set[tx_set_type]);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_intra_y_mode_nonkf(FRAME_CONTEXT frame_ctx,
                                                 BLOCK_SIZE bsize,
                                                 PREDICTION_MODE mode,
                                                 aom_writer w)
    {
        aom_write_symbol(w, mode, frame_ctx.y_mode_cdf[size_group_lookup[bsize]],
                         INTRA_MODES);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_intra_uv_mode(FRAME_CONTEXT frame_ctx,
                                            UV_PREDICTION_MODE uv_mode,
                                            PREDICTION_MODE y_mode,
                                            CFL_ALLOWED_TYPE cfl_allowed,
                                            aom_writer w)
    {
        aom_write_symbol(w, uv_mode, frame_ctx.uv_mode_cdf[cfl_allowed][y_mode],
                         UV_INTRA_MODES - B(cfl_allowed == 0));
    }

    // [subset] write_cfl_alphas: CfL is off in the subset.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_cdef(AV1_COMMON cm, MACROBLOCKD xd,
                                   aom_writer w, int skip)
    {
        if (cm.features.coded_lossless || cm.features.allow_intrabc) return;

        // At the start of a superblock, mark that we haven't yet written CDEF
        // strengths for any of the CDEF units contained in this superblock.
        int sb_mask = (cm.seq_params.mib_size - 1);
        int mi_row_in_sb = (xd.mi_row & sb_mask);
        int mi_col_in_sb = (xd.mi_col & sb_mask);
        if (mi_row_in_sb == 0 && mi_col_in_sb == 0)
        {
            xd.cdef_transmitted[0] = xd.cdef_transmitted[1] =
                xd.cdef_transmitted[2] = xd.cdef_transmitted[3] = false;
        }

        // CDEF unit size is 64x64 irrespective of the superblock size.
        const int cdef_size = 1 << (6 - MI_SIZE_LOG2);

        // Find index of this CDEF unit in this superblock.
        const int index_mask = cdef_size;
        int cdef_unit_row_in_sb = B((xd.mi_row & index_mask) != 0);
        int cdef_unit_col_in_sb = B((xd.mi_col & index_mask) != 0);
        int index = (cm.seq_params.sb_size == BLOCK_128X128)
                        ? cdef_unit_col_in_sb + 2 * cdef_unit_row_in_sb
                        : 0;

        // Write CDEF strength to the first non-skip coding block in this CDEF unit.
        if (!xd.cdef_transmitted[index] && skip == 0)
        {
            // CDEF strength for this CDEF unit needs to be stored in the MB_MODE_INFO
            // of the 1st block in this CDEF unit.
            const int first_block_mask = ~(cdef_size - 1);
            CommonModeInfoParams mi_params = cm.mi_params;
            int grid_idx =
                get_mi_grid_idx(mi_params, xd.mi_row & first_block_mask,
                                xd.mi_col & first_block_mask);
            MB_MODE_INFO mbmi = mi_params.mi_grid_base[grid_idx];
            aom_write_literal(w, mbmi.cdef_strength, cm.cdef_info.cdef_bits);
            xd.cdef_transmitted[index] = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_inter_segment_id(AV1_COMP cpi, MACROBLOCKD xd,
                                               aom_writer w,
                                               segmentation seg,
                                               segmentation_probs segp,
                                               int skip, int preskip)
    {
        MB_MODE_INFO mbmi = xd.mi[0];
        AV1_COMMON cm = cpi.common;
        int mi_row = xd.mi_row;
        int mi_col = xd.mi_col;

        if (seg.update_map != 0)
        {
            if (preskip != 0)
            {
                if (seg.segid_preskip == 0) return;
            }
            else
            {
                if (seg.segid_preskip != 0) return;
                if (skip != 0)
                {
                    write_segment_id(cpi, xd, mbmi, w, seg, segp, 1);
                    if (seg.temporal_update != 0) mbmi.seg_id_predicted = 0;
                    return;
                }
            }
            if (seg.temporal_update != 0)
            {
                int pred_flag = mbmi.seg_id_predicted;
                Ptr<aom_cdf_prob> pred_cdf = av1_get_pred_cdf_seg_id(segp, xd);
                aom_write_symbol(w, pred_flag, pred_cdf, 2);
                if (pred_flag == 0)
                {
                    write_segment_id(cpi, xd, mbmi, w, seg, segp, 0);
                }
                if (pred_flag != 0)
                {
                    set_spatial_segment_id(cm.mi_params, cm.cur_frame.seg_map,
                                           mbmi.bsize, mi_row, mi_col, mbmi.segment_id);
                }
            }
            else
            {
                write_segment_id(cpi, xd, mbmi, w, seg, segp, 0);
            }
        }
    }

    // If delta q is present, writes delta_q index.
    // Also writes delta_q loop filter levels, if present.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_delta_q_params(AV1_COMMON cm,
                                             MACROBLOCKD xd, int skip,
                                             aom_writer w)
    {
        if (cm.delta_q_info.delta_q_present_flag != 0)
        {
            // [subset] delta q / delta lf signaling: deltaq_mode is 0 in the subset, so
            // delta_q_present_flag is never set.
            throw new NotSupportedException("[subset] write_delta_q_params: delta q present");
        }
    }

    private static void write_intra_prediction_modes(AV1_COMMON cm,
                                                     MACROBLOCKD xd,
                                                     int is_keyframe,
                                                     aom_writer w)
    {
        FRAME_CONTEXT ec_ctx = xd.tile_ctx;
        MB_MODE_INFO mbmi = xd.mi[0];
        PREDICTION_MODE mode = mbmi.mode;
        BLOCK_SIZE bsize = mbmi.bsize;

        // Y mode.
        if (is_keyframe != 0)
        {
            MB_MODE_INFO above_mi = xd.above_mbmi;
            MB_MODE_INFO left_mi = xd.left_mbmi;
            write_intra_y_mode_kf(ec_ctx, mbmi, above_mi, left_mi, mode, w);
        }
        else
        {
            write_intra_y_mode_nonkf(ec_ctx, bsize, mode, w);
        }

        // Y angle delta.
        int use_angle_delta = av1_use_angle_delta(bsize);
        if (use_angle_delta != 0 && av1_is_directional_mode(mode) != 0)
        {
            write_angle_delta(w, mbmi.angle_delta[PLANE_TYPE_Y],
                              ec_ctx.angle_delta_cdf[mode - V_PRED]);
        }

        // UV mode and UV angle delta.
        if (cm.seq_params.monochrome == 0 && xd.is_chroma_ref)
        {
            UV_PREDICTION_MODE uv_mode = mbmi.uv_mode;
            write_intra_uv_mode(ec_ctx, uv_mode, mode, is_cfl_allowed(xd), w);
            if (uv_mode == UV_CFL_PRED)
                // [subset] write_cfl_alphas: CfL is off in the subset.
                throw new NotSupportedException("[subset] write_cfl_alphas");
            PREDICTION_MODE intra_mode = get_uv_mode(uv_mode);
            if (use_angle_delta != 0 && av1_is_directional_mode(intra_mode) != 0)
            {
                write_angle_delta(w, mbmi.angle_delta[PLANE_TYPE_UV],
                                  ec_ctx.angle_delta_cdf[intra_mode - V_PRED]);
            }
        }

        // Palette.
        if (av1_allow_palette(B(cm.features.allow_screen_content_tools), bsize) != 0)
        {
            // [subset] write_palette_mode_info: screen content tools / palette are off.
            throw new NotSupportedException("[subset] write_palette_mode_info");
        }

        // Filter intra.
        write_filter_intra_mode_info(cm, xd, mbmi, w);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short mode_context_analyzer(
        short mode_context, Ptr<MV_REFERENCE_FRAME> rf)
    {
        if (rf[1] <= INTRA_FRAME) return mode_context;

        short newmv_ctx = (short)(mode_context & NEWMV_CTX_MASK);
        short refmv_ctx = (short)((mode_context >> REFMV_OFFSET) & REFMV_CTX_MASK);

        short comp_ctx = (short)compound_mode_ctx_map[refmv_ctx >> 1][AOMMIN(
            (int)newmv_ctx, COMP_NEWMV_CTXS - 1)];
        return comp_ctx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int_mv get_ref_mv_from_stack(
        int ref_idx, Ptr<MV_REFERENCE_FRAME> ref_frame, int ref_mv_idx,
        in MB_MODE_INFO_EXT_FRAME mbmi_ext_frame)
    {
        sbyte ref_frame_type = av1_ref_frame_type(ref_frame);
        // C: const CANDIDATE_MV *curr_ref_mv_stack = mbmi_ext_frame->ref_mv_stack;

        if (ref_frame[1] > INTRA_FRAME)
        {
            Debug.Assert(ref_idx == 0 || ref_idx == 1);
            return ref_idx != 0 ? mbmi_ext_frame.ref_mv_stack[ref_mv_idx].comp_mv
                                : mbmi_ext_frame.ref_mv_stack[ref_mv_idx].this_mv;
        }

        Debug.Assert(ref_idx == 0);
        return ref_mv_idx < mbmi_ext_frame.ref_mv_count
                   ? mbmi_ext_frame.ref_mv_stack[ref_mv_idx].this_mv
                   : mbmi_ext_frame.global_mvs[ref_frame_type];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int_mv get_ref_mv(MACROBLOCK x, int ref_idx)
    {
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mbmi = xd.mi[0];
        int ref_mv_idx = mbmi.ref_mv_idx;
        if (mbmi.mode == NEAR_NEWMV || mbmi.mode == NEW_NEARMV)
        {
            Debug.Assert(has_second_ref(mbmi) != 0);
            ref_mv_idx += 1;
        }
        return get_ref_mv_from_stack(ref_idx, mbmi.ref_frame, ref_mv_idx,
                                     in x.mbmi_ext_frame.Value);
    }

    private static void pack_inter_mode_mvs(AV1_COMP cpi, ThreadData td,
                                            aom_writer w)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCK x = td.mb;
        MACROBLOCKD xd = x.e_mbd;
        FRAME_CONTEXT ec_ctx = xd.tile_ctx;
        segmentation seg = cm.seg;
        segmentation_probs segp = ec_ctx.seg;
        MB_MODE_INFO mbmi = xd.mi[0];
        ref MB_MODE_INFO_EXT_FRAME mbmi_ext_frame = ref x.mbmi_ext_frame.Value;
        PREDICTION_MODE mode = mbmi.mode;
        byte segment_id = mbmi.segment_id;
        BLOCK_SIZE bsize = mbmi.bsize;
        int allow_hp = B(cm.features.allow_high_precision_mv);
        int is_inter = is_inter_block(mbmi);
        int is_compound = has_second_ref(mbmi);
        int @ref;

        write_inter_segment_id(cpi, xd, w, seg, segp, 0, 1);

        write_skip_mode(cm, xd, segment_id, mbmi, w);

        Debug.Assert(mbmi.skip_mode == 0 || mbmi.skip_txfm != 0);
        int skip =
            mbmi.skip_mode != 0 ? 1 : write_skip(cm, xd, segment_id, mbmi, w);

        write_inter_segment_id(cpi, xd, w, seg, segp, skip, 0);

        write_cdef(cm, xd, w, skip);

        write_delta_q_params(cm, xd, skip, w);

        if (mbmi.skip_mode == 0) write_is_inter(cm, xd, mbmi.segment_id, w, is_inter);

        if (mbmi.skip_mode != 0) return;

        if (is_inter == 0)
        {
            write_intra_prediction_modes(cm, xd, 0, w);
        }
        else
        {
            short mode_ctx;

            av1_collect_neighbors_ref_counts(xd);

            write_ref_frames(cm, xd, w);

            mode_ctx =
                mode_context_analyzer(mbmi_ext_frame.mode_context, mbmi.ref_frame);

            // If segment skip is not enabled code the mode.
            if (segfeature_active(seg, segment_id, SEG_LVL_SKIP) == 0)
            {
                if (is_inter_compound_mode(mode) != 0)
                    // [subset] write_inter_compound_mode: compound prediction is off.
                    throw new NotSupportedException("[subset] write_inter_compound_mode");
                else if (is_inter_singleref_mode(mode) != 0)
                    write_inter_mode(w, mode, ec_ctx, mode_ctx);

                if (mode == NEWMV || mode == NEW_NEWMV || have_nearmv_in_inter_mode(mode) != 0)
                    write_drl_idx(ec_ctx, mbmi, in mbmi_ext_frame, w);
                else
                    Debug.Assert(mbmi.ref_mv_idx == 0);
            }

            if (mode == NEWMV || mode == NEW_NEWMV)
            {
                for (@ref = 0; @ref < 1 + is_compound; ++@ref)
                {
                    nmv_context nmvc = ec_ctx.nmvc;
                    int_mv ref_mv = get_ref_mv(x, @ref);
                    av1_encode_mv(cpi, w, td, in mbmi.mv[@ref].as_mv, in ref_mv.as_mv, nmvc,
                                  allow_hp);
                }
            }
            else if (mode == NEAREST_NEWMV || mode == NEAR_NEWMV)
            {
                nmv_context nmvc = ec_ctx.nmvc;
                int_mv ref_mv = get_ref_mv(x, 1);
                av1_encode_mv(cpi, w, td, in mbmi.mv[1].as_mv, in ref_mv.as_mv, nmvc,
                              allow_hp);
            }
            else if (mode == NEW_NEARESTMV || mode == NEW_NEARMV)
            {
                nmv_context nmvc = ec_ctx.nmvc;
                int_mv ref_mv = get_ref_mv(x, 0);
                av1_encode_mv(cpi, w, td, in mbmi.mv[0].as_mv, in ref_mv.as_mv, nmvc,
                              allow_hp);
            }

            if (cpi.common.current_frame.reference_mode != COMPOUND_REFERENCE &&
                cpi.common.seq_params.enable_interintra_compound != 0 &&
                is_interintra_allowed(mbmi) != 0)
            {
                // [subset] inter-intra compound: enable_interintra_compound is 0.
                throw new NotSupportedException("[subset] pack_inter_mode_mvs: interintra");
            }

            if (mbmi.ref_frame[1] != INTRA_FRAME) write_motion_mode(cm, xd, mbmi, w);

            // First write idx to indicate current compound inter prediction mode group
            // Group A (0): dist_wtd_comp, compound_average
            // Group B (1): interintra, compound_diffwtd, wedge
            if (has_second_ref(mbmi) != 0)
            {
                // [subset] compound group / index / masked compound type: compound
                // prediction is off in the subset.
                throw new NotSupportedException("[subset] pack_inter_mode_mvs: compound");
            }
            write_mb_interp_filter(cm, td, w);
        }
    }

    // [subset] write_intrabc_info: intra block copy is off in the subset.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_mb_modes_kf(
        AV1_COMP cpi, MACROBLOCKD xd,
        Ptr<MB_MODE_INFO_EXT_FRAME> mbmi_ext_frame, aom_writer w)
    {
        AV1_COMMON cm = cpi.common;
        FRAME_CONTEXT ec_ctx = xd.tile_ctx;
        segmentation seg = cm.seg;
        segmentation_probs segp = ec_ctx.seg;
        MB_MODE_INFO mbmi = xd.mi[0];

        if (seg.segid_preskip != 0 && seg.update_map != 0)
            write_segment_id(cpi, xd, mbmi, w, seg, segp, 0);

        int skip = write_skip(cm, xd, mbmi.segment_id, mbmi, w);

        if (seg.segid_preskip == 0 && seg.update_map != 0)
            write_segment_id(cpi, xd, mbmi, w, seg, segp, skip);

        write_cdef(cm, xd, w, skip);

        write_delta_q_params(cm, xd, skip, w);

        if (av1_allow_intrabc(cm) != 0)
        {
            // [subset] write_intrabc_info: intra block copy is off in the subset.
            throw new NotSupportedException("[subset] write_intrabc_info");
        }

        write_intra_prediction_modes(cm, xd, 1, w);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_mbmi_b(AV1_COMP cpi, ThreadData td,
                                     aom_writer w)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = td.mb.e_mbd;
        MB_MODE_INFO m = xd.mi[0];

        if (frame_is_intra_only(cm) != 0)
        {
            write_mb_modes_kf(cpi, xd, td.mb.mbmi_ext_frame, w);
        }
        else
        {
            // has_subpel_mv_component needs the ref frame buffers set up to look
            // up if they are scaled. has_subpel_mv_component is in turn needed by
            // write_switchable_interp_filter, which is called by pack_inter_mode_mvs.
            set_ref_ptrs(cm, xd, m.ref_frame[0], m.ref_frame[1]);

            pack_inter_mode_mvs(cpi, td, w);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_inter_txb_coeff(
        AV1_COMMON cm, MACROBLOCK x, MB_MODE_INFO mbmi,
        aom_writer w, ref Ptr<TokenExtra> tok, Ptr<TokenExtra> tok_end,
        ref TOKEN_STATS token_stats, int row, int col, ref int block,
        int plane)
    {
        MACROBLOCKD xd = x.e_mbd;
        macroblockd_plane pd = xd.plane[plane];
        BLOCK_SIZE bsize = mbmi.bsize;
        Debug.Assert(bsize < BLOCK_SIZES_ALL);
        int ss_x = pd.subsampling_x;
        int ss_y = pd.subsampling_y;
        BLOCK_SIZE plane_bsize = get_plane_block_size(bsize, ss_x, ss_y);
        Debug.Assert(plane_bsize < BLOCK_SIZES_ALL);
        TX_SIZE max_tx_size = get_vartx_max_txsize(xd, plane_bsize, plane);
        int step =
            tx_size_wide_unit[max_tx_size] * tx_size_high_unit[max_tx_size];
        int bkw = tx_size_wide_unit[max_tx_size];
        int bkh = tx_size_high_unit[max_tx_size];
        BLOCK_SIZE max_unit_bsize =
            get_plane_block_size(BLOCK_64X64, ss_x, ss_y);
        int num_4x4_w = mi_size_wide[plane_bsize];
        int num_4x4_h = mi_size_high[plane_bsize];
        int mu_blocks_wide = mi_size_wide[max_unit_bsize];
        int mu_blocks_high = mi_size_high[max_unit_bsize];
        int unit_height = AOMMIN(mu_blocks_high + (row >> ss_y), num_4x4_h);
        int unit_width = AOMMIN(mu_blocks_wide + (col >> ss_x), num_4x4_w);
        for (int blk_row = row >> ss_y; blk_row < unit_height; blk_row += bkh)
        {
            for (int blk_col = col >> ss_x; blk_col < unit_width; blk_col += bkw)
            {
                pack_txb_tokens(w, cm, x, ref tok, tok_end, xd, mbmi, plane, plane_bsize,
                                cm.seq_params.bit_depth, block, blk_row, blk_col,
                                max_tx_size, ref token_stats);
                block += step;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_tokens_b(AV1_COMP cpi, MACROBLOCK x,
                                       aom_writer w, ref Ptr<TokenExtra> tok,
                                       Ptr<TokenExtra> tok_end)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mbmi = xd.mi[0];
        BLOCK_SIZE bsize = mbmi.bsize;

        Debug.Assert(mbmi.skip_txfm == 0);

        int is_inter = is_inter_block(mbmi);
        if (is_inter == 0)
        {
            av1_write_intra_coeffs_mb(cm, x, w, bsize);
        }
        else
        {
            Span<int> block = stackalloc int[MAX_MB_PLANE];
            block.Clear();
            Debug.Assert(bsize == get_plane_block_size(bsize, xd.plane[0].subsampling_x,
                                                       xd.plane[0].subsampling_y));
            int num_4x4_w = mi_size_wide[bsize];
            int num_4x4_h = mi_size_high[bsize];
            TOKEN_STATS token_stats = default;
            init_token_stats(ref token_stats);

            const BLOCK_SIZE max_unit_bsize = BLOCK_64X64;
            Debug.Assert(max_unit_bsize == get_plane_block_size(BLOCK_64X64,
                                                                xd.plane[0].subsampling_x,
                                                                xd.plane[0].subsampling_y));
            int mu_blocks_wide = mi_size_wide[max_unit_bsize];
            int mu_blocks_high = mi_size_high[max_unit_bsize];
            mu_blocks_wide = AOMMIN(num_4x4_w, mu_blocks_wide);
            mu_blocks_high = AOMMIN(num_4x4_h, mu_blocks_high);

            int num_planes = av1_num_planes(cm);
            for (int row = 0; row < num_4x4_h; row += mu_blocks_high)
            {
                for (int col = 0; col < num_4x4_w; col += mu_blocks_wide)
                {
                    for (int plane = 0; plane < num_planes; ++plane)
                    {
                        if (plane != 0 && !xd.is_chroma_ref) break;
                        write_inter_txb_coeff(cm, x, mbmi, w, ref tok, tok_end, ref token_stats,
                                              row, col, ref block[plane], plane);
                    }
                }
            }
        }
    }

    private static void write_modes_b(AV1_COMP cpi, ThreadData td,
                                      in TileInfo tile, aom_writer w,
                                      ref Ptr<TokenExtra> tok,
                                      Ptr<TokenExtra> tok_end, int mi_row,
                                      int mi_col)
    {
        AV1_COMMON cm = cpi.common;
        CommonModeInfoParams mi_params = cm.mi_params;
        MACROBLOCKD xd = td.mb.e_mbd;
        // C: FRAME_CONTEXT *tile_ctx = xd->tile_ctx; only read by the palette path.
        int grid_idx = mi_row * mi_params.mi_stride + mi_col;
        xd.mi = new Ptr<MB_MODE_INFO>(mi_params.mi_grid_base, grid_idx);
        td.mb.mbmi_ext_frame =
            new Ptr<MB_MODE_INFO_EXT_FRAME>(cpi.mbmi_ext_info.frame_base,
                get_mi_ext_idx(mi_row, mi_col, cm.mi_params.mi_alloc_bsize,
                               cpi.mbmi_ext_info.stride));
        xd.tx_type_map = mi_params.tx_type_map + grid_idx;
        xd.tx_type_map_stride = mi_params.mi_stride;

        MB_MODE_INFO mbmi = xd.mi[0];
        BLOCK_SIZE bsize = mbmi.bsize;
        Debug.Assert(bsize <= cm.seq_params.sb_size ||
                     (bsize >= BLOCK_SIZES && bsize < BLOCK_SIZES_ALL));

        int bh = mi_size_high[bsize];
        int bw = mi_size_wide[bsize];
        set_mi_row_col(xd, in tile, mi_row, bh, mi_col, bw, mi_params.mi_rows,
                       mi_params.mi_cols);

        xd.above_txfm_context = cm.above_contexts.txfm[tile.tile_row] + mi_col;
        xd.left_txfm_context =
            new Ptr<TXFM_CONTEXT>(xd.left_txfm_context_buffer, (mi_row & MAX_MIB_MASK));

        write_mbmi_b(cpi, td, w);

        for (int plane = 0; plane < AOMMIN(2, av1_num_planes(cm)); ++plane)
        {
            byte palette_size_plane =
                mbmi.palette_mode_info.palette_size[plane];
            Debug.Assert(mbmi.skip_mode == 0 || palette_size_plane == 0);
            if (palette_size_plane > 0)
            {
                // [subset] pack_map_tokens: palette is off in the subset.
                throw new NotSupportedException("[subset] write_modes_b: palette tokens");
            }
        }

        int is_inter_tx = is_inter_block(mbmi);
        int skip_txfm = mbmi.skip_txfm;
        byte segment_id = mbmi.segment_id;
        if (cm.features.tx_mode == TX_MODE_SELECT && block_signals_txsize(bsize) != 0 &&
            !(is_inter_tx != 0 && skip_txfm != 0) && xd.lossless[segment_id] == 0)
        {
            if (is_inter_tx != 0)
            {  // This implies skip flag is 0.
                TX_SIZE max_tx_size = get_vartx_max_txsize(xd, bsize, 0);
                int txbh = tx_size_high_unit[max_tx_size];
                int txbw = tx_size_wide_unit[max_tx_size];
                int width = mi_size_wide[bsize];
                int height = mi_size_high[bsize];
                for (int idy = 0; idy < height; idy += txbh)
                {
                    for (int idx = 0; idx < width; idx += txbw)
                    {
                        write_tx_size_vartx(xd, mbmi, max_tx_size, 0, idy, idx, w);
                    }
                }
            }
            else
            {
                write_selected_tx_size(xd, w);
                set_txfm_ctxs(mbmi.tx_size, xd.width, xd.height, 0, xd);
            }
        }
        else
        {
            set_txfm_ctxs(mbmi.tx_size, xd.width, xd.height,
                          B(skip_txfm != 0 && is_inter_tx != 0), xd);
        }

        if (mbmi.skip_txfm == 0)
        {
            int start = aom_tell_size(w);

            write_tokens_b(cpi, td.mb, w, ref tok, tok_end);

            int end = aom_tell_size(w);
            td.coefficient_size += end - start;
        }
    }

    // [port] C `aom_cdf_prob cdf[2]` locals of write_partition: per-thread scratch.
    [ThreadStatic] private static aom_cdf_prob[] write_partition_cdf;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_partition(AV1_COMMON cm,
                                        MACROBLOCKD xd, int hbs,
                                        int mi_row, int mi_col, PARTITION_TYPE p,
                                        BLOCK_SIZE bsize, aom_writer w)
    {
        bool is_partition_point = bsize >= BLOCK_8X8;

        if (!is_partition_point) return;

        bool has_rows = (mi_row + hbs) < cm.mi_params.mi_rows;
        bool has_cols = (mi_col + hbs) < cm.mi_params.mi_cols;
        int ctx = partition_plane_context(xd, mi_row, mi_col, bsize);
        FRAME_CONTEXT ec_ctx = xd.tile_ctx;

        if (!has_rows && !has_cols)
        {
            Debug.Assert(p == PARTITION_SPLIT);
            return;
        }

        if (has_rows && has_cols)
        {
            aom_write_symbol(w, p, ec_ctx.partition_cdf[ctx],
                             partition_cdf_length(bsize));
        }
        else if (!has_rows && has_cols)
        {
            Debug.Assert(p == PARTITION_SPLIT || p == PARTITION_HORZ);
            Debug.Assert(bsize > BLOCK_8X8);
            aom_cdf_prob[] cdf = write_partition_cdf ??= new aom_cdf_prob[2];
            partition_gather_vert_alike(cdf, ec_ctx.partition_cdf[ctx], bsize);
            aom_write_cdf(w, B(p == PARTITION_SPLIT), cdf, 2);
        }
        else
        {
            Debug.Assert(has_rows && !has_cols);
            Debug.Assert(p == PARTITION_SPLIT || p == PARTITION_VERT);
            Debug.Assert(bsize > BLOCK_8X8);
            aom_cdf_prob[] cdf = write_partition_cdf ??= new aom_cdf_prob[2];
            partition_gather_horz_alike(cdf, ec_ctx.partition_cdf[ctx], bsize);
            aom_write_cdf(w, B(p == PARTITION_SPLIT), cdf, 2);
        }
    }

    private static void write_modes_sb(AV1_COMP cpi, ThreadData td,
                                       in TileInfo tile,
                                       aom_writer w, ref Ptr<TokenExtra> tok,
                                       Ptr<TokenExtra> tok_end, int mi_row,
                                       int mi_col, BLOCK_SIZE bsize)
    {
        AV1_COMMON cm = cpi.common;
        CommonModeInfoParams mi_params = cm.mi_params;
        MACROBLOCKD xd = td.mb.e_mbd;
        Debug.Assert(bsize < BLOCK_SIZES_ALL);
        int hbs = mi_size_wide[bsize] / 2;
        int quarter_step = mi_size_wide[bsize] / 4;
        int i;
        PARTITION_TYPE partition = get_partition(cm, mi_row, mi_col, bsize);
        BLOCK_SIZE subsize = get_partition_subsize(bsize, partition);

        if (mi_row >= mi_params.mi_rows || mi_col >= mi_params.mi_cols) return;

        // [subset] (!CONFIG_REALTIME_ONLY) loop restoration coefficients: every plane has
        // frame_restoration_type RESTORE_NONE (enable_restoration is 0), so the loop over the
        // planes does nothing.
        Debug.Assert(cm.seq_params.enable_restoration == 0);

        write_partition(cm, xd, hbs, mi_row, mi_col, partition, bsize, w);
        switch (partition)
        {
            case PARTITION_NONE:
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row, mi_col);
                break;
            case PARTITION_HORZ:
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row, mi_col);
                if (mi_row + hbs < mi_params.mi_rows)
                    write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row + hbs, mi_col);
                break;
            case PARTITION_VERT:
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row, mi_col);
                if (mi_col + hbs < mi_params.mi_cols)
                    write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row, mi_col + hbs);
                break;
            case PARTITION_SPLIT:
                write_modes_sb(cpi, td, in tile, w, ref tok, tok_end, mi_row, mi_col, subsize);
                write_modes_sb(cpi, td, in tile, w, ref tok, tok_end, mi_row, mi_col + hbs,
                               subsize);
                write_modes_sb(cpi, td, in tile, w, ref tok, tok_end, mi_row + hbs, mi_col,
                               subsize);
                write_modes_sb(cpi, td, in tile, w, ref tok, tok_end, mi_row + hbs, mi_col + hbs,
                               subsize);
                break;
            case PARTITION_HORZ_A:
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row, mi_col);
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row, mi_col + hbs);
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row + hbs, mi_col);
                break;
            case PARTITION_HORZ_B:
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row, mi_col);
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row + hbs, mi_col);
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row + hbs, mi_col + hbs);
                break;
            case PARTITION_VERT_A:
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row, mi_col);
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row + hbs, mi_col);
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row, mi_col + hbs);
                break;
            case PARTITION_VERT_B:
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row, mi_col);
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row, mi_col + hbs);
                write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row + hbs, mi_col + hbs);
                break;
            case PARTITION_HORZ_4:
                for (i = 0; i < 4; ++i)
                {
                    int this_mi_row = mi_row + i * quarter_step;
                    if (i > 0 && this_mi_row >= mi_params.mi_rows) break;

                    write_modes_b(cpi, td, in tile, w, ref tok, tok_end, this_mi_row, mi_col);
                }
                break;
            case PARTITION_VERT_4:
                for (i = 0; i < 4; ++i)
                {
                    int this_mi_col = mi_col + i * quarter_step;
                    if (i > 0 && this_mi_col >= mi_params.mi_cols) break;

                    write_modes_b(cpi, td, in tile, w, ref tok, tok_end, mi_row, this_mi_col);
                }
                break;
            default: Debug.Assert(false); break;
        }

        // update partition context
        update_ext_partition_context(xd, mi_row, mi_col, subsize, bsize, partition);
    }

    // Populate token pointers appropriately based on token_info.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void get_token_pointers(TokenInfo token_info,
                                           int tile_row, int tile_col,
                                           int sb_row_in_tile,
                                           ref Ptr<TokenExtra> tok,
                                           ref Ptr<TokenExtra> tok_end)
    {
        if (!is_token_info_allocated(token_info))
        {
            tok = Ptr<TokenExtra>.Null;
            tok_end = Ptr<TokenExtra>.Null;
            return;
        }
        tok = token_info.tplist[tile_row][tile_col][sb_row_in_tile].start;
        tok_end =
            tok + (int)token_info.tplist[tile_row][tile_col][sb_row_in_tile].count;
    }

    private static void write_modes(AV1_COMP cpi, ThreadData td,
                                    in TileInfo tile, aom_writer w,
                                    int tile_row, int tile_col)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = td.mb.e_mbd;
        int mi_row_start = tile.mi_row_start;
        int mi_row_end = tile.mi_row_end;
        int mi_col_start = tile.mi_col_start;
        int mi_col_end = tile.mi_col_end;
        int num_planes = av1_num_planes(cm);

        av1_zero_above_context(cm, xd, mi_col_start, mi_col_end, tile.tile_row);
        av1_init_above_context(cm.above_contexts, num_planes, tile.tile_row, xd);

        if (cpi.common.delta_q_info.delta_q_present_flag != 0)
        {
            // [subset] delta q (deltaq_mode 0): xd->current_base_qindex reset and
            // av1_reset_loop_filter_delta are not ported.
            throw new NotSupportedException("[subset] write_modes: delta q present");
        }

        for (int mi_row = mi_row_start; mi_row < mi_row_end;
             mi_row += cm.seq_params.mib_size)
        {
            int sb_row_in_tile =
                (mi_row - tile.mi_row_start) >> cm.seq_params.mib_size_log2;
            TokenInfo token_info = cpi.token_info;
            Ptr<TokenExtra> tok = default;
            Ptr<TokenExtra> tok_end = default;
            get_token_pointers(token_info, tile_row, tile_col, sb_row_in_tile, ref tok,
                               ref tok_end);

            av1_zero_left_context(xd);

            for (int mi_col = mi_col_start; mi_col < mi_col_end;
                 mi_col += cm.seq_params.mib_size)
            {
                td.mb.cb_coef_buff = av1_get_cb_coeff_buffer(cpi, mi_row, mi_col);
                write_modes_sb(cpi, td, in tile, w, ref tok, tok_end, mi_row, mi_col,
                               cm.seq_params.sb_size);
            }
            Debug.Assert(tok == tok_end);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void encode_restoration_mode(AV1_COMMON cm,
                                                ref aom_write_bit_buffer wb)
    {
        Debug.Assert(!cm.features.all_lossless);
        if (cm.seq_params.enable_restoration == 0) return;
        if (cm.features.allow_intrabc) return;
        // [subset] restoration modes: loop restoration is off in the subset.
        throw new NotSupportedException("[subset] encode_restoration_mode");
    }

    // [subset] write_wiener_filter, write_sgrproj_filter, loop_restoration_write_sb_coeffs:
    // loop restoration is off in the subset.

    // [port] C `int8_t last_ref_deltas[REF_FRAMES]` / `last_mode_deltas[MAX_MODE_LF_DELTAS]`
    // locals of is_mode_ref_delta_meaningful and encode_loopfilter: per-thread scratch (both
    // are fully written before they are read).
    [ThreadStatic] private static sbyte[] lf_last_ref_deltas;
    [ThreadStatic] private static sbyte[] lf_last_mode_deltas;

    // Only write out the ref delta section if any of the elements
    // will signal a delta.
    private static bool is_mode_ref_delta_meaningful(AV1_COMMON cm)
    {
        loopfilter lf = cm.lf;
        if (lf.mode_ref_delta_update == 0)
        {
            return false;
        }
        RefCntBuffer buf = get_primary_ref_frame_buf(cm);
        sbyte[] last_ref_deltas = lf_last_ref_deltas ??= new sbyte[REF_FRAMES];
        sbyte[] last_mode_deltas = lf_last_mode_deltas ??= new sbyte[MAX_MODE_LF_DELTAS];
        if (buf == null)
        {
            av1_set_default_ref_deltas(last_ref_deltas);
            av1_set_default_mode_deltas(last_mode_deltas);
        }
        else
        {
            memcpy<sbyte>(last_ref_deltas, buf.ref_deltas, REF_FRAMES);
            memcpy<sbyte>(last_mode_deltas, buf.mode_deltas, MAX_MODE_LF_DELTAS);
        }
        for (int i = 0; i < REF_FRAMES; i++)
        {
            if (lf.ref_deltas[i] != last_ref_deltas[i])
            {
                return true;
            }
        }
        for (int i = 0; i < MAX_MODE_LF_DELTAS; i++)
        {
            if (lf.mode_deltas[i] != last_mode_deltas[i])
            {
                return true;
            }
        }
        return false;
    }

    private static void encode_loopfilter(AV1_COMMON cm,
                                          ref aom_write_bit_buffer wb)
    {
        Debug.Assert(!cm.features.coded_lossless);
        if (cm.features.allow_intrabc) return;
        int num_planes = av1_num_planes(cm);
        loopfilter lf = cm.lf;

        // Encode the loop filter level and type
        aom_wb_write_literal(ref wb, lf.filter_level[0], 6);
        aom_wb_write_literal(ref wb, lf.filter_level[1], 6);
        if (num_planes > 1)
        {
            if (lf.filter_level[0] != 0 || lf.filter_level[1] != 0)
            {
                aom_wb_write_literal(ref wb, lf.filter_level_u, 6);
                aom_wb_write_literal(ref wb, lf.filter_level_v, 6);
            }
        }
        aom_wb_write_literal(ref wb, lf.sharpness_level, 3);

        aom_wb_write_bit(ref wb, lf.mode_ref_delta_enabled);

        if (lf.mode_ref_delta_enabled != 0)
        {
            // Check if the loop filter deltas have changed from the previous frame.
            // If mode_ref_delta_update_decision is 1, signal the update and write out
            // the actual delta values. Otherwise, signal 0 and skip writing delta
            // values.
            int mode_ref_delta_update = B(is_mode_ref_delta_meaningful(cm));
            aom_wb_write_bit(ref wb, mode_ref_delta_update);
            if (mode_ref_delta_update == 0)
            {
                return;
            }

            RefCntBuffer buf = get_primary_ref_frame_buf(cm);
            sbyte[] last_ref_deltas = lf_last_ref_deltas ??= new sbyte[REF_FRAMES];
            sbyte[] last_mode_deltas = lf_last_mode_deltas ??= new sbyte[MAX_MODE_LF_DELTAS];
            if (buf == null)
            {
                av1_set_default_ref_deltas(last_ref_deltas);
                av1_set_default_mode_deltas(last_mode_deltas);
            }
            else
            {
                memcpy<sbyte>(last_ref_deltas, buf.ref_deltas, REF_FRAMES);
                memcpy<sbyte>(last_mode_deltas, buf.mode_deltas, MAX_MODE_LF_DELTAS);
            }
            for (int i = 0; i < REF_FRAMES; i++)
            {
                int delta = lf.ref_deltas[i];
                int changed = B(delta != last_ref_deltas[i]);
                aom_wb_write_bit(ref wb, changed);
                if (changed != 0) aom_wb_write_inv_signed_literal(ref wb, delta, 6);
            }
            for (int i = 0; i < MAX_MODE_LF_DELTAS; i++)
            {
                int delta = lf.mode_deltas[i];
                int changed = B(delta != last_mode_deltas[i]);
                aom_wb_write_bit(ref wb, changed);
                if (changed != 0) aom_wb_write_inv_signed_literal(ref wb, delta, 6);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void encode_cdef(AV1_COMMON cm,
                                    ref aom_write_bit_buffer wb)
    {
        Debug.Assert(!cm.features.coded_lossless);
        if (cm.seq_params.enable_cdef == 0) return;
        if (cm.features.allow_intrabc) return;
        int num_planes = av1_num_planes(cm);
        int i;
        aom_wb_write_literal(ref wb, cm.cdef_info.cdef_damping - 3, 2);
        aom_wb_write_literal(ref wb, cm.cdef_info.cdef_bits, 2);
        for (i = 0; i < cm.cdef_info.nb_cdef_strengths; i++)
        {
            aom_wb_write_literal(ref wb, cm.cdef_info.cdef_strengths[i],
                                 CDEF_STRENGTH_BITS);
            if (num_planes > 1)
                aom_wb_write_literal(ref wb, cm.cdef_info.cdef_uv_strengths[i],
                                     CDEF_STRENGTH_BITS);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_delta_q(ref aom_write_bit_buffer wb, int delta_q)
    {
        if (delta_q != 0)
        {
            aom_wb_write_bit(ref wb, 1);
            aom_wb_write_inv_signed_literal(ref wb, delta_q, 6);
        }
        else
        {
            aom_wb_write_bit(ref wb, 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void encode_quantization(
        CommonQuantParams quant_params, int num_planes,
        bool separate_uv_delta_q, ref aom_write_bit_buffer wb)
    {
        aom_wb_write_literal(ref wb, quant_params.base_qindex, QINDEX_BITS);
        write_delta_q(ref wb, quant_params.y_dc_delta_q);
        if (num_planes > 1)
        {
            int diff_uv_delta =
                B((quant_params.u_dc_delta_q != quant_params.v_dc_delta_q) ||
                  (quant_params.u_ac_delta_q != quant_params.v_ac_delta_q));
            if (separate_uv_delta_q) aom_wb_write_bit(ref wb, diff_uv_delta);
            write_delta_q(ref wb, quant_params.u_dc_delta_q);
            write_delta_q(ref wb, quant_params.u_ac_delta_q);
            if (diff_uv_delta != 0)
            {
                write_delta_q(ref wb, quant_params.v_dc_delta_q);
                write_delta_q(ref wb, quant_params.v_ac_delta_q);
            }
        }
        aom_wb_write_bit(ref wb, B(quant_params.using_qmatrix));
        if (quant_params.using_qmatrix)
        {
            aom_wb_write_literal(ref wb, quant_params.qmatrix_level_y, QM_LEVEL_BITS);
            aom_wb_write_literal(ref wb, quant_params.qmatrix_level_u, QM_LEVEL_BITS);
            if (!separate_uv_delta_q)
                Debug.Assert(quant_params.qmatrix_level_u == quant_params.qmatrix_level_v);
            else
                aom_wb_write_literal(ref wb, quant_params.qmatrix_level_v, QM_LEVEL_BITS);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void encode_segmentation(AV1_COMMON cm,
                                            ref aom_write_bit_buffer wb)
    {
        int i, j;
        segmentation seg = cm.seg;

        aom_wb_write_bit(ref wb, seg.enabled);
        if (seg.enabled == 0) return;

        // Write update flags
        if (cm.features.primary_ref_frame != PRIMARY_REF_NONE)
        {
            aom_wb_write_bit(ref wb, seg.update_map);
            if (seg.update_map != 0) aom_wb_write_bit(ref wb, seg.temporal_update);
            aom_wb_write_bit(ref wb, seg.update_data);
        }

        // Segmentation data
        if (seg.update_data != 0)
        {
            for (i = 0; i < MAX_SEGMENTS; i++)
            {
                for (j = 0; j < SEG_LVL_MAX; j++)
                {
                    int active = segfeature_active(seg, (byte)i, j);
                    aom_wb_write_bit(ref wb, active);
                    if (active != 0)
                    {
                        int data_max = av1_seg_feature_data_max(j);
                        int data_min = -data_max;
                        int ubits = get_unsigned_bits((uint)data_max);
                        int data = clamp(get_segdata(seg, i, j), data_min, data_max);

                        if (av1_is_segfeature_signed(j) != 0)
                        {
                            aom_wb_write_inv_signed_literal(ref wb, data, ubits);
                        }
                        else
                        {
                            aom_wb_write_literal(ref wb, data, ubits);
                        }
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_frame_interp_filter(InterpFilter filter,
                                                  ref aom_write_bit_buffer wb)
    {
        aom_wb_write_bit(ref wb, B(filter == SWITCHABLE));
        if (filter != SWITCHABLE)
            aom_wb_write_literal(ref wb, filter, LOG_SWITCHABLE_FILTERS);
    }

    // Same function as write_uniform but writing to uncompresses header wb
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void wb_write_uniform(ref aom_write_bit_buffer wb, int n,
                                         int v)
    {
        int l = get_unsigned_bits((uint)n);
        int m = (1 << l) - n;
        if (l == 0) return;
        if (v < m)
        {
            aom_wb_write_literal(ref wb, v, l - 1);
        }
        else
        {
            aom_wb_write_literal(ref wb, m + ((v - m) >> 1), l - 1);
            aom_wb_write_literal(ref wb, (v - m) & 1, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_tile_info_max_tile(AV1_COMMON cm,
                                                 ref aom_write_bit_buffer wb)
    {
        int width_sb =
            CEIL_POWER_OF_TWO(cm.mi_params.mi_cols, cm.seq_params.mib_size_log2);
        int height_sb =
            CEIL_POWER_OF_TWO(cm.mi_params.mi_rows, cm.seq_params.mib_size_log2);
        int size_sb, i;
        CommonTileParams tiles = cm.tiles;

        aom_wb_write_bit(ref wb, tiles.uniform_spacing);

        if (tiles.uniform_spacing != 0)
        {
            int ones = tiles.log2_cols - tiles.min_log2_cols;
            while (ones-- != 0)
            {
                aom_wb_write_bit(ref wb, 1);
            }
            if (tiles.log2_cols < tiles.max_log2_cols)
            {
                aom_wb_write_bit(ref wb, 0);
            }

            // rows
            ones = tiles.log2_rows - tiles.min_log2_rows;
            while (ones-- != 0)
            {
                aom_wb_write_bit(ref wb, 1);
            }
            if (tiles.log2_rows < tiles.max_log2_rows)
            {
                aom_wb_write_bit(ref wb, 0);
            }
        }
        else
        {
            // Explicit tiles with configurable tile widths and heights
            // columns
            for (i = 0; i < tiles.cols; i++)
            {
                size_sb = tiles.col_start_sb[i + 1] - tiles.col_start_sb[i];
                wb_write_uniform(ref wb, AOMMIN(width_sb, tiles.max_width_sb), size_sb - 1);
                width_sb -= size_sb;
            }
            Debug.Assert(width_sb == 0);

            // rows
            for (i = 0; i < tiles.rows; i++)
            {
                size_sb = tiles.row_start_sb[i + 1] - tiles.row_start_sb[i];
                wb_write_uniform(ref wb, AOMMIN(height_sb, tiles.max_height_sb),
                                 size_sb - 1);
                height_sb -= size_sb;
            }
            Debug.Assert(height_sb == 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_tile_info(AV1_COMMON cm,
                                        ref aom_write_bit_buffer saved_wb,
                                        ref aom_write_bit_buffer wb)
    {
        write_tile_info_max_tile(cm, ref wb);

        saved_wb = wb;
        if (cm.tiles.rows * cm.tiles.cols > 1)
        {
            // tile id used for cdf update
            aom_wb_write_literal(ref wb, 0, cm.tiles.log2_cols + cm.tiles.log2_rows);
            // Number of bytes in tile size - 1
            aom_wb_write_literal(ref wb, 3, 2);
        }
    }

    // [subset] write_ext_tile_info, find_identical_tile: large-scale tile coding only.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_render_size(AV1_COMMON cm,
                                          ref aom_write_bit_buffer wb)
    {
        int scaling_active = av1_resize_scaled(cm);
        aom_wb_write_bit(ref wb, scaling_active);
        if (scaling_active != 0)
        {
            aom_wb_write_literal(ref wb, cm.render_width - 1, 16);
            aom_wb_write_literal(ref wb, cm.render_height - 1, 16);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_superres_scale(AV1_COMMON cm,
                                             ref aom_write_bit_buffer wb)
    {
        SequenceHeader seq_params = cm.seq_params;
        if (seq_params.enable_superres == 0)
        {
            Debug.Assert(cm.superres_scale_denominator == SCALE_NUMERATOR);
            return;
        }

        // First bit is whether to to scale or not
        if (cm.superres_scale_denominator == SCALE_NUMERATOR)
        {
            aom_wb_write_bit(ref wb, 0);  // no scaling
        }
        else
        {
            aom_wb_write_bit(ref wb, 1);  // scaling, write scale factor
            Debug.Assert(cm.superres_scale_denominator >= SUPERRES_SCALE_DENOMINATOR_MIN);
            Debug.Assert(cm.superres_scale_denominator <
                         SUPERRES_SCALE_DENOMINATOR_MIN + (1 << SUPERRES_SCALE_BITS));
            aom_wb_write_literal(
                ref wb, cm.superres_scale_denominator - SUPERRES_SCALE_DENOMINATOR_MIN,
                SUPERRES_SCALE_BITS);
        }
    }

    private static void write_frame_size(AV1_COMMON cm,
                                         int frame_size_override,
                                         ref aom_write_bit_buffer wb)
    {
        int coded_width = cm.superres_upscaled_width - 1;
        int coded_height = cm.superres_upscaled_height - 1;
        SequenceHeader seq_params = cm.seq_params;

        if (frame_size_override != 0)
        {
            int num_bits_width = seq_params.num_bits_width;
            int num_bits_height = seq_params.num_bits_height;
            aom_wb_write_literal(ref wb, coded_width, num_bits_width);
            aom_wb_write_literal(ref wb, coded_height, num_bits_height);
        }
        else
        {
            if (cm.superres_upscaled_width != seq_params.max_frame_width ||
                cm.superres_upscaled_height != seq_params.max_frame_height)
            {
                throw new AomException(AOM_CODEC_UNSUP_BITSTREAM,
                                       "Frame dimensions are different from the maximum " +
                                       "values when frame_size_override_flag is 0");
            }
        }

        write_superres_scale(cm, ref wb);
        write_render_size(cm, ref wb);
    }

    private static void write_frame_size_with_refs(AV1_COMMON cm,
                                                   ref aom_write_bit_buffer wb)
    {
        int found = 0;

        MV_REFERENCE_FRAME ref_frame;
        for (ref_frame = LAST_FRAME; ref_frame <= ALTREF_FRAME; ++ref_frame)
        {
            YV12_BUFFER_CONFIG cfg = get_ref_frame_yv12_buf(cm, ref_frame);

            if (cfg != null)
            {
                found = B(cm.superres_upscaled_width == cfg.y_crop_width &&
                          cm.superres_upscaled_height == cfg.y_crop_height);
                found &= B(cm.render_width == cfg.render_width &&
                           cm.render_height == cfg.render_height);
            }
            aom_wb_write_bit(ref wb, found);
            if (found != 0)
            {
                write_superres_scale(cm, ref wb);
                break;
            }
        }

        if (found == 0)
        {
            int frame_size_override = 1;  // Always equal to 1 in this function
            write_frame_size(cm, frame_size_override, ref wb);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_profile(BITSTREAM_PROFILE profile,
                                      ref aom_write_bit_buffer wb)
    {
        Debug.Assert(profile >= PROFILE_0 && profile < MAX_PROFILES);
        aom_wb_write_literal(ref wb, profile, PROFILE_BITS);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_bitdepth(SequenceHeader seq_params,
                                       ref aom_write_bit_buffer wb)
    {
        // Profile 0/1: [0] for 8 bit, [1]  10-bit
        // Profile   2: [0] for 8 bit, [10] 10-bit, [11] - 12-bit
        aom_wb_write_bit(ref wb, seq_params.bit_depth == AOM_BITS_8 ? 0 : 1);
        if (seq_params.profile == PROFILE_2 && seq_params.bit_depth != AOM_BITS_8)
        {
            aom_wb_write_bit(ref wb, seq_params.bit_depth == AOM_BITS_10 ? 0 : 1);
        }
    }

    private static void write_color_config(SequenceHeader seq_params,
                                           ref aom_write_bit_buffer wb)
    {
        write_bitdepth(seq_params, ref wb);
        int is_monochrome = seq_params.monochrome;
        // monochrome bit
        if (seq_params.profile != PROFILE_1)
            aom_wb_write_bit(ref wb, is_monochrome);
        else
            Debug.Assert(is_monochrome == 0);
        if (seq_params.color_primaries == AOM_CICP_CP_UNSPECIFIED &&
            seq_params.transfer_characteristics == AOM_CICP_TC_UNSPECIFIED &&
            seq_params.matrix_coefficients == AOM_CICP_MC_UNSPECIFIED)
        {
            aom_wb_write_bit(ref wb, 0);  // No color description present
        }
        else
        {
            aom_wb_write_bit(ref wb, 1);  // Color description present
            aom_wb_write_literal(ref wb, seq_params.color_primaries, 8);
            aom_wb_write_literal(ref wb, seq_params.transfer_characteristics, 8);
            aom_wb_write_literal(ref wb, seq_params.matrix_coefficients, 8);
        }
        if (is_monochrome != 0)
        {
            // 0: [16, 235] (i.e. xvYCC), 1: [0, 255]
            aom_wb_write_bit(ref wb, seq_params.color_range);
            return;
        }
        if (seq_params.color_primaries == AOM_CICP_CP_BT_709 &&
            seq_params.transfer_characteristics == AOM_CICP_TC_SRGB &&
            seq_params.matrix_coefficients == AOM_CICP_MC_IDENTITY)
        {
            Debug.Assert(seq_params.subsampling_x == 0 && seq_params.subsampling_y == 0);
            Debug.Assert(seq_params.profile == PROFILE_1 ||
                         (seq_params.profile == PROFILE_2 &&
                          seq_params.bit_depth == AOM_BITS_12));
        }
        else
        {
            // 0: [16, 235] (i.e. xvYCC), 1: [0, 255]
            aom_wb_write_bit(ref wb, seq_params.color_range);
            if (seq_params.profile == PROFILE_0)
            {
                // 420 only
                Debug.Assert(seq_params.subsampling_x == 1 && seq_params.subsampling_y == 1);
            }
            else if (seq_params.profile == PROFILE_1)
            {
                // 444 only
                Debug.Assert(seq_params.subsampling_x == 0 && seq_params.subsampling_y == 0);
            }
            else if (seq_params.profile == PROFILE_2)
            {
                if (seq_params.bit_depth == AOM_BITS_12)
                {
                    // 420, 444 or 422
                    aom_wb_write_bit(ref wb, seq_params.subsampling_x);
                    if (seq_params.subsampling_x == 0)
                    {
                        Debug.Assert(seq_params.subsampling_y == 0,
                                     "4:4:0 subsampling not allowed in AV1");
                    }
                    else
                    {
                        aom_wb_write_bit(ref wb, seq_params.subsampling_y);
                    }
                }
                else
                {
                    // 422 only
                    Debug.Assert(seq_params.subsampling_x == 1 &&
                                 seq_params.subsampling_y == 0);
                }
            }
            if (seq_params.matrix_coefficients == AOM_CICP_MC_IDENTITY)
            {
                Debug.Assert(seq_params.subsampling_x == 0 && seq_params.subsampling_y == 0);
            }
            if (seq_params.subsampling_x == 1 && seq_params.subsampling_y == 1)
            {
                aom_wb_write_literal(ref wb, seq_params.chroma_sample_position, 2);
            }
        }
        aom_wb_write_bit(ref wb, seq_params.separate_uv_delta_q);
    }

    // [subset] write_timing_info_header, write_decoder_model_info,
    // write_dec_model_op_parameters, write_tu_pts_info: timing info and the decoder model
    // are not signaled (timing_info_present and decoder_model_info_present_flag are 0).

    // [subset] write_film_grain_params: film grain is out of the subset.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_sb_size(SequenceHeader seq_params,
                                      ref aom_write_bit_buffer wb)
    {
        Debug.Assert(seq_params.mib_size == mi_size_wide[seq_params.sb_size]);
        Debug.Assert(seq_params.mib_size == 1 << seq_params.mib_size_log2);
        Debug.Assert(seq_params.sb_size == BLOCK_128X128 ||
                     seq_params.sb_size == BLOCK_64X64);
        aom_wb_write_bit(ref wb, seq_params.sb_size == BLOCK_128X128 ? 1 : 0);
    }

    private static void write_sequence_header(SequenceHeader seq_params,
                                              ref aom_write_bit_buffer wb)
    {
        aom_wb_write_literal(ref wb, seq_params.num_bits_width - 1, 4);
        aom_wb_write_literal(ref wb, seq_params.num_bits_height - 1, 4);
        aom_wb_write_literal(ref wb, seq_params.max_frame_width - 1,
                             seq_params.num_bits_width);
        aom_wb_write_literal(ref wb, seq_params.max_frame_height - 1,
                             seq_params.num_bits_height);

        if (seq_params.reduced_still_picture_hdr == 0)
        {
            aom_wb_write_bit(ref wb, seq_params.frame_id_numbers_present_flag);
            if (seq_params.frame_id_numbers_present_flag != 0)
            {
                // We must always have delta_frame_id_length < frame_id_length,
                // in order for a frame to be referenced with a unique delta.
                // Avoid wasting bits by using a coding that enforces this restriction.
                aom_wb_write_literal(ref wb, seq_params.delta_frame_id_length - 2, 4);
                aom_wb_write_literal(
                    ref wb,
                    seq_params.frame_id_length - seq_params.delta_frame_id_length - 1,
                    3);
            }
        }

        write_sb_size(seq_params, ref wb);

        aom_wb_write_bit(ref wb, seq_params.enable_filter_intra);
        aom_wb_write_bit(ref wb, seq_params.enable_intra_edge_filter);

        if (seq_params.reduced_still_picture_hdr == 0)
        {
            aom_wb_write_bit(ref wb, seq_params.enable_interintra_compound);
            aom_wb_write_bit(ref wb, seq_params.enable_masked_compound);
            aom_wb_write_bit(ref wb, seq_params.enable_warped_motion);
            aom_wb_write_bit(ref wb, seq_params.enable_dual_filter);

            aom_wb_write_bit(ref wb, seq_params.order_hint_info.enable_order_hint);

            if (seq_params.order_hint_info.enable_order_hint != 0)
            {
                aom_wb_write_bit(ref wb, seq_params.order_hint_info.enable_dist_wtd_comp);
                aom_wb_write_bit(ref wb, seq_params.order_hint_info.enable_ref_frame_mvs);
            }
            if (seq_params.force_screen_content_tools == 2)
            {
                aom_wb_write_bit(ref wb, 1);
            }
            else
            {
                aom_wb_write_bit(ref wb, 0);
                aom_wb_write_bit(ref wb, seq_params.force_screen_content_tools);
            }
            if (seq_params.force_screen_content_tools > 0)
            {
                if (seq_params.force_integer_mv == 2)
                {
                    aom_wb_write_bit(ref wb, 1);
                }
                else
                {
                    aom_wb_write_bit(ref wb, 0);
                    aom_wb_write_bit(ref wb, seq_params.force_integer_mv);
                }
            }
            else
            {
                Debug.Assert(seq_params.force_integer_mv == 2);
            }
            if (seq_params.order_hint_info.enable_order_hint != 0)
                aom_wb_write_literal(
                    ref wb, seq_params.order_hint_info.order_hint_bits_minus_1, 3);
        }

        aom_wb_write_bit(ref wb, seq_params.enable_superres);
        aom_wb_write_bit(ref wb, seq_params.enable_cdef);
        aom_wb_write_bit(ref wb, seq_params.enable_restoration);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_global_motion_params(
        in WarpedMotionParams @params, in WarpedMotionParams ref_params,
        ref aom_write_bit_buffer wb, int allow_hp)
    {
        TransformationType type = @params.wmtype;

        // As a workaround for an AV1 spec bug, we avoid choosing TRANSLATION
        // type models. Check here that we don't accidentally pick one somehow.
        // See comments in gm_get_motion_vector() for details on the bug we're
        // working around here
        Debug.Assert(type != TRANSLATION);

        aom_wb_write_bit(ref wb, B(type != IDENTITY));
        if (type != IDENTITY)
        {
            // [subset] non-identity global motion (type bits and the
            // aom_wb_write_signed_primitive_refsubexpfin parameters): global motion is
            // off in the subset.
            throw new NotSupportedException("[subset] write_global_motion_params: non-identity");
        }
    }

    private static void write_global_motion(AV1_COMP cpi,
                                            ref aom_write_bit_buffer wb)
    {
        AV1_COMMON cm = cpi.common;
        int frame;
        for (frame = LAST_FRAME; frame <= ALTREF_FRAME; ++frame)
        {
            if (cm.prev_frame != null)
                write_global_motion_params(in cm.global_motion[frame],
                                           in cm.prev_frame.global_motion[frame], ref wb,
                                           B(cm.features.allow_high_precision_mv));
            else
                write_global_motion_params(in cm.global_motion[frame],
                                           in default_warp_params, ref wb,
                                           B(cm.features.allow_high_precision_mv));
            // TODO(sarahparker, debargha): The logic in the commented out code below
            // does not work currently and causes mismatches when resize is on.
            // Fix it before turning the optimization back on.
        }
    }

    // [subset] check_frame_refs_short_signaling: only called when
    // frame_refs_short_signaling is 1, which needs enable_order_hint (no order hint in the
    // subset); av1_set_frame_refs is not ported.

    // New function based on HLS R18
    // PORT-NOTE: `struct aom_write_bit_buffer *saved_wb` points to av1_pack_bitstream()'s
    // local (see PackBSParams): Ptr<aom_write_bit_buffer>.
    private static void write_uncompressed_header_obu(
        AV1_COMP cpi, MACROBLOCKD xd, Ptr<aom_write_bit_buffer> saved_wb,
        ref aom_write_bit_buffer wb)
    {
        AV1_COMMON cm = cpi.common;
        SequenceHeader seq_params = cm.seq_params;
        CommonQuantParams quant_params = cm.quant_params;
        CurrentFrame current_frame = cm.current_frame;
        FeatureFlags features = cm.features;

        if (!cpi.sf.rt_sf.enable_ref_short_signaling ||
            seq_params.order_hint_info.enable_order_hint == 0 ||
            seq_params.order_hint_info.enable_ref_frame_mvs != 0)
        {
            current_frame.frame_refs_short_signaling = 0;
        }
        else
        {
            current_frame.frame_refs_short_signaling = 1;
        }

        if (seq_params.still_picture != 0)
        {
            Debug.Assert(cm.show_existing_frame == 0);
            Debug.Assert(cm.show_frame == 1);
            Debug.Assert(current_frame.frame_type == KEY_FRAME);
        }
        if (seq_params.reduced_still_picture_hdr == 0)
        {
            if (encode_show_existing_frame(cm) != 0)
            {
                aom_wb_write_bit(ref wb, 1);  // show_existing_frame
                aom_wb_write_literal(ref wb, cpi.existing_fb_idx_to_show, 3);

                if (seq_params.decoder_model_info_present_flag != 0 &&
                    seq_params.timing_info.equal_picture_interval == 0)
                {
                    // [subset] write_tu_pts_info: the decoder model is not signaled.
                    throw new NotSupportedException("[subset] write_tu_pts_info");
                }
                if (seq_params.frame_id_numbers_present_flag != 0)
                {
                    int frame_id_len = seq_params.frame_id_length;
                    int display_frame_id = cm.ref_frame_id[cpi.existing_fb_idx_to_show];
                    aom_wb_write_literal(ref wb, display_frame_id, frame_id_len);
                }
                return;
            }
            else
            {
                aom_wb_write_bit(ref wb, 0);  // show_existing_frame
            }

            aom_wb_write_literal(ref wb, current_frame.frame_type, 2);

            aom_wb_write_bit(ref wb, cm.show_frame);
            if (cm.show_frame != 0)
            {
                if (seq_params.decoder_model_info_present_flag != 0 &&
                    seq_params.timing_info.equal_picture_interval == 0)
                    // [subset] write_tu_pts_info: the decoder model is not signaled.
                    throw new NotSupportedException("[subset] write_tu_pts_info");
            }
            else
            {
                aom_wb_write_bit(ref wb, cm.showable_frame);
            }
            if (frame_is_sframe(cm) != 0)
            {
                Debug.Assert(features.error_resilient_mode);
            }
            else if (!(current_frame.frame_type == KEY_FRAME && cm.show_frame != 0))
            {
                aom_wb_write_bit(ref wb, B(features.error_resilient_mode));
            }
        }
        aom_wb_write_bit(ref wb, B(features.disable_cdf_update));

        if (seq_params.force_screen_content_tools == 2)
        {
            aom_wb_write_bit(ref wb, B(features.allow_screen_content_tools));
        }
        else
        {
            Debug.Assert(B(features.allow_screen_content_tools) ==
                         seq_params.force_screen_content_tools);
        }

        if (features.allow_screen_content_tools)
        {
            if (seq_params.force_integer_mv == 2)
            {
                aom_wb_write_bit(ref wb, B(features.cur_frame_force_integer_mv));
            }
            else
            {
                Debug.Assert(B(features.cur_frame_force_integer_mv) ==
                             seq_params.force_integer_mv);
            }
        }
        else
        {
            Debug.Assert(!features.cur_frame_force_integer_mv);
        }

        int frame_size_override_flag = 0;

        if (seq_params.reduced_still_picture_hdr == 0)
        {
            if (seq_params.frame_id_numbers_present_flag != 0)
            {
                int frame_id_len = seq_params.frame_id_length;
                aom_wb_write_literal(ref wb, cm.current_frame_id, frame_id_len);
            }

            if (cm.superres_upscaled_width > seq_params.max_frame_width ||
                cm.superres_upscaled_height > seq_params.max_frame_height)
            {
                throw new AomException(AOM_CODEC_UNSUP_BITSTREAM,
                                       "Frame dimensions are larger than the maximum values");
            }

            frame_size_override_flag =
                frame_is_sframe(cm) != 0
                    ? 1
                    : B(cm.superres_upscaled_width != seq_params.max_frame_width ||
                        cm.superres_upscaled_height != seq_params.max_frame_height);
            if (frame_is_sframe(cm) == 0) aom_wb_write_bit(ref wb, frame_size_override_flag);

            if (seq_params.order_hint_info.enable_order_hint != 0)
                aom_wb_write_literal(
                    ref wb, (int)current_frame.order_hint,
                    seq_params.order_hint_info.order_hint_bits_minus_1 + 1);

            if (!features.error_resilient_mode && frame_is_intra_only(cm) == 0)
            {
                aom_wb_write_literal(ref wb, features.primary_ref_frame, PRIMARY_REF_BITS);
            }
        }

        if (seq_params.decoder_model_info_present_flag != 0)
        {
            // [subset] buffer_removal_time_present / buffer_removal_times: the decoder
            // model is not signaled (aom_wb_write_unsigned_literal is not ported).
            throw new NotSupportedException("[subset] decoder model buffer removal times");
        }

        // Shown keyframes and switch-frames automatically refreshes all reference
        // frames.  For all other frame types, we need to write refresh_frame_flags.
        if ((current_frame.frame_type == KEY_FRAME && cm.show_frame == 0) ||
            current_frame.frame_type == INTER_FRAME ||
            current_frame.frame_type == INTRA_ONLY_FRAME)
            aom_wb_write_literal(ref wb, current_frame.refresh_frame_flags, REF_FRAMES);

        if (frame_is_intra_only(cm) == 0 || current_frame.refresh_frame_flags != 0xff)
        {
            // Write all ref frame order hints if error_resilient_mode == 1
            if (features.error_resilient_mode &&
                seq_params.order_hint_info.enable_order_hint != 0)
            {
                for (int ref_idx = 0; ref_idx < REF_FRAMES; ref_idx++)
                {
                    aom_wb_write_literal(
                        ref wb, (int)cm.ref_frame_map[ref_idx].order_hint,
                        seq_params.order_hint_info.order_hint_bits_minus_1 + 1);
                }
            }
        }

        if (frame_is_intra_only(cm) != 0)
        {
            write_frame_size(cm, frame_size_override_flag, ref wb);
            Debug.Assert(av1_superres_scaled(cm) == 0 || !features.allow_intrabc);
            if (features.allow_screen_content_tools && av1_superres_scaled(cm) == 0)
                aom_wb_write_bit(ref wb, B(features.allow_intrabc));
        }
        else if (current_frame.frame_type == INTER_FRAME || frame_is_sframe(cm) != 0)
        {
            MV_REFERENCE_FRAME ref_frame;

            // NOTE: Error resilient mode turns off frame_refs_short_signaling
            //       automatically.
            // FRAME_REFS_SHORT_SIGNALING is 0.

            if (current_frame.frame_refs_short_signaling != 0)
            {
                //    In rtc case when cpi->sf.rt_sf.enable_ref_short_signaling is true,
                //    we turn on frame_refs_short_signaling when the current frame and
                //    golden frame are in the same order_hint group, and their relative
                //    distance is <= 64 (in order to be decodable).

                // [subset] check_frame_refs_short_signaling: needs order hints.
                throw new NotSupportedException("[subset] check_frame_refs_short_signaling");
            }

            if (seq_params.order_hint_info.enable_order_hint != 0)
                aom_wb_write_bit(ref wb, current_frame.frame_refs_short_signaling);

            if (current_frame.frame_refs_short_signaling != 0)
            {
                int lst_ref = get_ref_frame_map_idx(cm, LAST_FRAME);
                aom_wb_write_literal(ref wb, lst_ref, REF_FRAMES_LOG2);

                int gld_ref = get_ref_frame_map_idx(cm, GOLDEN_FRAME);
                aom_wb_write_literal(ref wb, gld_ref, REF_FRAMES_LOG2);
            }
            for (ref_frame = LAST_FRAME; ref_frame <= ALTREF_FRAME; ++ref_frame)
            {
                Debug.Assert(get_ref_frame_map_idx(cm, ref_frame) != INVALID_IDX);
                if (current_frame.frame_refs_short_signaling == 0)
                {
                    aom_wb_write_literal(ref wb, get_ref_frame_map_idx(cm, ref_frame),
                                         REF_FRAMES_LOG2);
                }
                if (seq_params.frame_id_numbers_present_flag != 0)
                {
                    int i = get_ref_frame_map_idx(cm, ref_frame);
                    int frame_id_len = seq_params.frame_id_length;
                    int diff_len = seq_params.delta_frame_id_length;
                    int delta_frame_id_minus_1 =
                        ((cm.current_frame_id - cm.ref_frame_id[i] +
                          (1 << frame_id_len)) %
                         (1 << frame_id_len)) -
                        1;
                    if (delta_frame_id_minus_1 < 0 ||
                        delta_frame_id_minus_1 >= (1 << diff_len))
                    {
                        throw new AomException(AOM_CODEC_ERROR,
                                               "Invalid delta_frame_id_minus_1");
                    }
                    aom_wb_write_literal(ref wb, delta_frame_id_minus_1, diff_len);
                }
            }

            if (!features.error_resilient_mode && frame_size_override_flag != 0)
            {
                write_frame_size_with_refs(cm, ref wb);
            }
            else
            {
                write_frame_size(cm, frame_size_override_flag, ref wb);
            }

            if (!features.cur_frame_force_integer_mv)
                aom_wb_write_bit(ref wb, B(features.allow_high_precision_mv));
            write_frame_interp_filter(features.interp_filter, ref wb);
            aom_wb_write_bit(ref wb, B(features.switchable_motion_mode));
            if (frame_might_allow_ref_frame_mvs(cm) != 0)
            {
                aom_wb_write_bit(ref wb, B(features.allow_ref_frame_mvs));
            }
            else
            {
                Debug.Assert(!features.allow_ref_frame_mvs);
            }
        }

        int might_bwd_adapt = B(seq_params.reduced_still_picture_hdr == 0 &&
                                !features.disable_cdf_update);
        if (cm.tiles.large_scale != 0)
            Debug.Assert(features.refresh_frame_context == REFRESH_FRAME_CONTEXT_DISABLED);

        if (might_bwd_adapt != 0)
        {
            aom_wb_write_bit(
                ref wb, B(features.refresh_frame_context == REFRESH_FRAME_CONTEXT_DISABLED));
        }

        write_tile_info(cm, ref saved_wb.Value, ref wb);
        encode_quantization(quant_params, av1_num_planes(cm),
                            cm.seq_params.separate_uv_delta_q != 0, ref wb);
        encode_segmentation(cm, ref wb);

        ref DeltaQInfo delta_q_info = ref cm.delta_q_info;
        if (delta_q_info.delta_q_present_flag != 0) Debug.Assert(quant_params.base_qindex > 0);
        if (quant_params.base_qindex > 0)
        {
            aom_wb_write_bit(ref wb, delta_q_info.delta_q_present_flag);
            if (delta_q_info.delta_q_present_flag != 0)
            {
                // [subset] delta q / delta lf signaling: deltaq_mode is 0 in the subset
                // (av1_reset_loop_filter_delta is not ported).
                throw new NotSupportedException("[subset] write_uncompressed_header_obu: delta q");
            }
        }

        if (features.all_lossless)
        {
            Debug.Assert(av1_superres_scaled(cm) == 0);
        }
        else
        {
            if (!features.coded_lossless)
            {
                encode_loopfilter(cm, ref wb);
                encode_cdef(cm, ref wb);
            }
            encode_restoration_mode(cm, ref wb);
        }

        // Write TX mode
        if (features.coded_lossless)
            Debug.Assert(features.tx_mode == ONLY_4X4);
        else
            aom_wb_write_bit(ref wb, B(features.tx_mode == TX_MODE_SELECT));

        if (frame_is_intra_only(cm) == 0)
        {
            int use_hybrid_pred =
                B(current_frame.reference_mode == REFERENCE_MODE_SELECT);

            aom_wb_write_bit(ref wb, use_hybrid_pred);
        }

        if (current_frame.skip_mode_info.skip_mode_allowed != 0)
            aom_wb_write_bit(ref wb, current_frame.skip_mode_info.skip_mode_flag);

        if (frame_might_allow_warped_motion(cm) != 0)
            aom_wb_write_bit(ref wb, B(features.allow_warped_motion));
        else
            Debug.Assert(!features.allow_warped_motion);

        aom_wb_write_bit(ref wb, B(features.reduced_tx_set_used));

        if (frame_is_intra_only(cm) == 0) write_global_motion(cpi, ref wb);

        if (seq_params.film_grain_params_present != 0 &&
            (cm.show_frame != 0 || cm.showable_frame != 0))
            // [subset] write_film_grain_params: film grain is out of the subset.
            throw new NotSupportedException("[subset] write_film_grain_params");

        if (cm.tiles.large_scale != 0)
            // [subset] write_ext_tile_info: large-scale tile coding only.
            throw new NotSupportedException("[subset] write_ext_tile_info");
    }

    private static int choose_size_bytes(uint size, int spare_msbs)
    {
        // Choose the number of bytes required to represent size, without
        // using the 'spare_msbs' number of most significant bits.

        // Make sure we will fit in 4 bytes to start with..
        if (spare_msbs > 0 && size >> (32 - spare_msbs) != 0) return -1;

        // Normalise to 32 bits
        size <<= spare_msbs;

        if (size >> 24 != 0)
            return 4;
        else if (size >> 16 != 0)
            return 3;
        else if (size >> 8 != 0)
            return 2;
        else
            return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void mem_put_varsize(Ptr<byte> dst, int sz, int val)
    {
        switch (sz)
        {
            case 1: dst[0] = (byte)(val & 0xff); break;
            case 2: mem_put_le16(dst, val); break;
            case 3: mem_put_le24(dst, val); break;
            case 4: mem_put_le32(dst, val); break;
            default: Debug.Assert(false, "Invalid size"); break;
        }
    }

    private static int remux_tiles(CommonTileParams tiles, Ptr<byte> dst,
                                   uint data_size, uint max_tile_size,
                                   uint max_tile_col_size,
                                   ref int tile_size_bytes,
                                   ref int tile_col_size_bytes)
    {
        // Choose the tile size bytes (tsb) and tile column size bytes (tcsb)
        int tsb;
        int tcsb;

        if (tiles.large_scale != 0)
        {
            // [subset] large-scale tile coding: the tile copy mode / tile column headers
            // are not ported.
            throw new NotSupportedException("[subset] remux_tiles: large_scale");
        }
        else
        {
            tsb = choose_size_bytes(max_tile_size, 0);
            tcsb = 4;  // This is ignored
            _ = max_tile_col_size;
        }

        Debug.Assert(tsb > 0);
        Debug.Assert(tcsb > 0);

        tile_size_bytes = tsb;
        tile_col_size_bytes = tcsb;
        if (tsb == 4 && tcsb == 4) return (int)data_size;

        uint wpos = 0;
        uint rpos = 0;

        // [subset] (tiles->large_scale) branch: see above.
        int n_tiles = tiles.cols * tiles.rows;
        int n;

        for (n = 0; n < n_tiles; n++)
        {
            int tile_size;

            if (n == n_tiles - 1)
            {
                tile_size = (int)(data_size - rpos);
            }
            else
            {
                tile_size = (int)mem_get_le32(dst + (int)rpos);
                rpos += 4;
                mem_put_varsize(dst + (int)wpos, tsb, tile_size);
                tile_size += AV1_MIN_TILE_SIZE_BYTES;
                wpos += (uint)tsb;
            }

            memmove(dst + (int)wpos, dst + (int)rpos, tile_size);

            rpos += (uint)tile_size;
            wpos += (uint)tile_size;
        }

        Debug.Assert(rpos > wpos);
        Debug.Assert(rpos == data_size);

        return (int)wpos;
    }

    internal static uint av1_write_obu_header(AV1LevelParams level_params,
                                              ref int frame_header_count, OBU_TYPE obu_type,
                                              bool has_nonzero_operating_point_idc,
                                              bool is_layer_specific_obu, int obu_extension,
                                              Ptr<byte> dst)
    {
        Debug.Assert(has_nonzero_operating_point_idc || obu_extension == 0);

        if (level_params.keep_level_stats != 0 &&
            (obu_type == OBU_FRAME || obu_type == OBU_FRAME_HEADER))
            ++frame_header_count;

        uint size = 0;

        // The AV1 spec draft version (as of git commit 5e04f)
        // has the following requirements on the OBU extension header:
        //
        // 6.4.1. General sequence header OBU semantics:
        //   If operating_point_idc[ op ] is not equal to 0 for any value of op from 0
        //   to operating_points_cnt_minus_1, it is a requirement of bitstream
        //   conformance that obu_extension_flag is equal to 1 for all layer-specific
        //   OBUs in the coded video sequence.
        //   (...)
        //   It is a requirement of bitstream conformance that if OperatingPointIdc
        //   is equal to 0, then obu_extension_flag is equal to 0 for all OBUs that
        //   follow this sequence header until the next sequence header.
        //
        // Set obu_extension_flag to satisfy these requirements.
        int obu_extension_flag =
            B(has_nonzero_operating_point_idc && is_layer_specific_obu);
        const int obu_has_size_field = 1;

        dst[0] = (byte)(((int)obu_type << 3) | (obu_extension_flag << 2) |
                        (obu_has_size_field << 1));
        size++;

        if (obu_extension_flag != 0)
        {
            dst[1] = (byte)(obu_extension & 0xFF);
            size++;
        }

        return size;
    }

    internal static int av1_write_uleb_obu_size(int obu_payload_size, Ptr<byte> dest,
                                                int dest_size)
    {
        int coded_obu_size = 0;

        if (aom_uleb_encode((ulong)obu_payload_size, dest_size, dest, ref coded_obu_size) !=
            0)
        {
            return AOM_CODEC_ERROR;
        }
        if (coded_obu_size != dest_size)
        {
            return AOM_CODEC_ERROR;
        }

        return AOM_CODEC_OK;
    }

    // Deprecated. Use av1_write_uleb_obu_size() instead.
    private static int av1_write_uleb_obu_size_unsafe(int obu_payload_size,
                                                      Ptr<byte> dest)
    {
        int coded_obu_size = 0;

        if (aom_uleb_encode((ulong)obu_payload_size, sizeof(uint), dest,
                            ref coded_obu_size) != 0)
        {
            return AOM_CODEC_ERROR;
        }

        return AOM_CODEC_OK;
    }

    // Returns 0 on failure.
    private static int obu_memmove(int obu_header_size, int obu_payload_size,
                                   Ptr<byte> data, int data_size)
    {
        int length_field_size = aom_uleb_size_in_bytes((ulong)obu_payload_size);
        int move_dst_offset = obu_header_size + length_field_size;
        int move_src_offset = obu_header_size;
        int move_size = obu_payload_size;
        if (move_size > data_size || move_src_offset > data_size - move_size)
        {
            Debug.Assert(false, "obu_memmove: output buffer overflow");
            return 0;
        }
        if (move_dst_offset > data_size - move_size)
        {
            // Buffer full.
            return 0;
        }
        memmove(data + move_dst_offset, data + move_src_offset, move_size);
        return length_field_size;
    }

    // Deprecated. Use obu_memmove() instead.
    private static int obu_memmove_unsafe(int obu_header_size,
                                          int obu_payload_size, Ptr<byte> data)
    {
        int length_field_size = aom_uleb_size_in_bytes((ulong)obu_payload_size);
        int move_dst_offset = obu_header_size + length_field_size;
        int move_src_offset = obu_header_size;
        int move_size = obu_payload_size;
        memmove(data + move_dst_offset, data + move_src_offset, move_size);
        return length_field_size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void add_trailing_bits(ref aom_write_bit_buffer wb)
    {
        if (aom_wb_is_byte_aligned(in wb) != 0)
        {
            aom_wb_write_literal(ref wb, 0x80, 8);
        }
        else
        {
            // assumes that the other bits are already 0s
            aom_wb_write_bit(ref wb, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_bitstream_level(AV1_LEVEL seq_level_idx,
                                              ref aom_write_bit_buffer wb)
    {
        Debug.Assert(is_valid_seq_level_idx(seq_level_idx) != 0);
        aom_wb_write_literal(ref wb, seq_level_idx, LEVEL_BITS);
    }

    internal static uint av1_write_sequence_header_obu(SequenceHeader seq_params,
                                                       Ptr<byte> dst, int dst_size)
    {
        // TODO: bug 42302568 - Use dst_size.
        _ = dst_size;
        aom_write_bit_buffer wb = new(dst, 0);
        uint size = 0;

        write_profile(seq_params.profile, ref wb);

        // Still picture or not
        aom_wb_write_bit(ref wb, seq_params.still_picture);
        Debug.Assert(seq_params.still_picture != 0 ||
                     seq_params.reduced_still_picture_hdr == 0);
        // whether to use reduced still picture header
        aom_wb_write_bit(ref wb, seq_params.reduced_still_picture_hdr);

        if (seq_params.reduced_still_picture_hdr != 0)
        {
            Debug.Assert(seq_params.timing_info_present == 0);
            Debug.Assert(seq_params.decoder_model_info_present_flag == 0);
            Debug.Assert(seq_params.display_model_info_present_flag == 0);
            write_bitstream_level(seq_params.seq_level_idx[0], ref wb);
        }
        else
        {
            aom_wb_write_bit(
                ref wb, seq_params.timing_info_present);  // timing info present flag

            if (seq_params.timing_info_present != 0)
            {
                // [subset] write_timing_info_header, write_decoder_model_info: timing
                // info is not signaled.
                throw new NotSupportedException("[subset] write_timing_info_header");
            }
            aom_wb_write_bit(ref wb, seq_params.display_model_info_present_flag);
            aom_wb_write_literal(ref wb, seq_params.operating_points_cnt_minus_1,
                                 OP_POINTS_CNT_MINUS_1_BITS);
            int i;
            for (i = 0; i < seq_params.operating_points_cnt_minus_1 + 1; i++)
            {
                aom_wb_write_literal(ref wb, seq_params.operating_point_idc[i],
                                     OP_POINTS_IDC_BITS);
                write_bitstream_level(seq_params.seq_level_idx[i], ref wb);
                if (seq_params.seq_level_idx[i] >= SEQ_LEVEL_4_0)
                    aom_wb_write_bit(ref wb, seq_params.tier[i]);
                if (seq_params.decoder_model_info_present_flag != 0)
                {
                    // [subset] decoder_model_param_present_flag,
                    // write_dec_model_op_parameters: the decoder model is not signaled.
                    throw new NotSupportedException("[subset] write_dec_model_op_parameters");
                }
                if (seq_params.display_model_info_present_flag != 0)
                {
                    aom_wb_write_bit(
                        ref wb, seq_params.op_params[i].display_model_param_present_flag);
                    if (seq_params.op_params[i].display_model_param_present_flag != 0)
                    {
                        Debug.Assert(seq_params.op_params[i].initial_display_delay >= 1);
                        Debug.Assert(seq_params.op_params[i].initial_display_delay <= 10);
                        aom_wb_write_literal(
                            ref wb, seq_params.op_params[i].initial_display_delay - 1, 4);
                    }
                }
            }
        }
        write_sequence_header(seq_params, ref wb);

        write_color_config(seq_params, ref wb);

        aom_wb_write_bit(ref wb, seq_params.film_grain_params_present);

        add_trailing_bits(ref wb);

        size = aom_wb_bytes_written(in wb);
        return size;
    }

    private static uint write_frame_header_obu(AV1_COMP cpi, MACROBLOCKD xd,
                                               Ptr<aom_write_bit_buffer> saved_wb,
                                               Ptr<byte> dst,
                                               int append_trailing_bits)
    {
        aom_write_bit_buffer wb = new(dst, 0);
        write_uncompressed_header_obu(cpi, xd, saved_wb, ref wb);
        if (append_trailing_bits != 0) add_trailing_bits(ref wb);
        return aom_wb_bytes_written(in wb);
    }

    private static uint write_tile_group_header(Ptr<byte> dst, int start_tile,
                                                int end_tile, int tiles_log2,
                                                int tile_start_and_end_present_flag)
    {
        aom_write_bit_buffer wb = new(dst, 0);
        uint size = 0;

        if (tiles_log2 == 0) return size;

        aom_wb_write_bit(ref wb, tile_start_and_end_present_flag);

        if (tile_start_and_end_present_flag != 0)
        {
            aom_wb_write_literal(ref wb, start_tile, tiles_log2);
            aom_wb_write_literal(ref wb, end_tile, tiles_log2);
        }

        size = aom_wb_bytes_written(in wb);
        return size;
    }

    // [subset] LargeTileFrameOBU, init_large_scale_tile_obu_header,
    // write_large_scale_tile_obu_size, write_large_scale_tile_obu,
    // pack_large_scale_tiles_in_tg_obus: large-scale tile coding only.

    // Writes obu, tile group and uncompressed headers to bitstream.
    internal static void av1_write_obu_tg_tile_headers(AV1_COMP cpi, MACROBLOCKD xd,
                                                       PackBSParams pack_bs_params,
                                                       int tile_idx)
    {
        AV1_COMMON cm = cpi.common;
        CommonTileParams tiles = cm.tiles;
        ref int curr_tg_hdr_size = ref pack_bs_params.curr_tg_hdr_size;
        int tg_size =
            (tiles.rows * tiles.cols + cpi.num_tg - 1) / cpi.num_tg;

        // Write Tile group, frame and OBU header
        // A new tile group begins at this tile.  Write the obu header and
        // tile group header
        OBU_TYPE obu_type = (cpi.num_tg == 1) ? OBU_FRAME : OBU_TILE_GROUP;
        curr_tg_hdr_size = (int)av1_write_obu_header(
            cpi.ppi.level_params, ref cpi.frame_header_count, obu_type,
            cm.seq_params.has_nonzero_operating_point_idc,
            /*is_layer_specific_obu=*/true, pack_bs_params.obu_extn_header,
            pack_bs_params.tile_data_curr);
        pack_bs_params.obu_header_size = (uint)curr_tg_hdr_size;

        if (cpi.num_tg == 1)
            curr_tg_hdr_size += (int)write_frame_header_obu(
                cpi, xd, pack_bs_params.saved_wb,
                pack_bs_params.tile_data_curr + curr_tg_hdr_size, 0);
        curr_tg_hdr_size += (int)write_tile_group_header(
            pack_bs_params.tile_data_curr + curr_tg_hdr_size, tile_idx,
            AOMMIN(tile_idx + tg_size - 1, tiles.cols * tiles.rows - 1),
            (tiles.log2_rows + tiles.log2_cols), B(cpi.num_tg > 1));
        pack_bs_params.total_size.Value += (uint)curr_tg_hdr_size;
    }

    // [port] C `aom_writer mode_bc` stack local: per-thread scratch object (the pack
    // bitstream workers each run on their own thread and av1_pack_tile_info does not
    // re-enter itself).
    [ThreadStatic] private static aom_writer pti_mode_bc;

    // Pack tile data in the bitstream with tile_group, frame
    // and OBU header.
    internal static void av1_pack_tile_info(AV1_COMP cpi, ThreadData td,
                                            PackBSParams pack_bs_params)
    {
        aom_writer mode_bc = pti_mode_bc ??= new aom_writer();
        AV1_COMMON cm = cpi.common;
        int tile_row = pack_bs_params.tile_row;
        int tile_col = pack_bs_params.tile_col;
        Ptr<uint> total_size = pack_bs_params.total_size;
        TileInfo tile_info = default;
        av1_tile_set_col(ref tile_info, cm, tile_col);
        av1_tile_set_row(ref tile_info, cm, tile_row);
        mode_bc.allow_update_cdf = 1;
        mode_bc.allow_update_cdf =
            (byte)B(mode_bc.allow_update_cdf != 0 && !cm.features.disable_cdf_update);

        uint tile_size;

        int num_planes = av1_num_planes(cm);
        av1_reset_loop_restoration(td.mb.e_mbd, num_planes);

        pack_bs_params.buf.data = pack_bs_params.dst + (int)total_size.Value;

        // The last tile of the tile group does not have a tile_size_minus_1 header.
        if (pack_bs_params.is_last_tile_in_tg == 0) total_size.Value += 4;

        // Pack tile data
        if ((uint)pack_bs_params.tile_buf_size <= total_size.Value)
        {
            throw new AomException(AOM_CODEC_ERROR, "Error writing modes");
        }
        aom_start_encode(mode_bc, pack_bs_params.dst + (int)total_size.Value,
                         pack_bs_params.tile_buf_size - (int)total_size.Value);
        write_modes(cpi, td, in tile_info, mode_bc, tile_row, tile_col);
        if (aom_stop_encode(mode_bc) < 0)
        {
            throw new AomException(AOM_CODEC_ERROR, "Error writing modes");
        }
        tile_size = mode_bc.pos;
        Debug.Assert(tile_size >= AV1_MIN_TILE_SIZE_BYTES);

        pack_bs_params.buf.size = (int)tile_size;

        // Write tile size
        if (pack_bs_params.is_last_tile_in_tg == 0)
        {
            // size of this tile minus 1
            mem_put_le32(pack_bs_params.buf.data, (int)(tile_size - AV1_MIN_TILE_SIZE_BYTES));
        }
    }

    internal static void av1_write_last_tile_info(
        AV1_COMP cpi, in FrameHeaderInfo fh_info,
        Ptr<aom_write_bit_buffer> saved_wb, ref int curr_tg_data_size,
        Ptr<byte> curr_tg_start, ref uint total_size,
        ref Ptr<byte> tile_data_start, ref int largest_tile_id,
        ref int is_first_tg, uint obu_header_size, byte obu_extn_header)
    {
        // write current tile group size
        int obu_payload_size = curr_tg_data_size - (int)obu_header_size;
        int length_field_size =
            obu_memmove_unsafe((int)obu_header_size, obu_payload_size, curr_tg_start);
        if (av1_write_uleb_obu_size_unsafe(
                obu_payload_size, curr_tg_start + (int)obu_header_size) != AOM_CODEC_OK)
        {
            throw new AomException(AOM_CODEC_ERROR,
                                   "av1_write_last_tile_info: output buffer full");
        }
        curr_tg_data_size += length_field_size;
        total_size += (uint)length_field_size;
        tile_data_start += length_field_size;
        if (cpi.num_tg == 1)
        {
            // if this tg is combined with the frame header then update saved
            // frame header base offset according to length field size
            saved_wb.Value.bit_buffer += length_field_size;
        }

        if (is_first_tg == 0 && cpi.common.features.error_resilient_mode)
        {
            // Make room for a duplicate Frame Header OBU.
            memmove(curr_tg_start + fh_info.total_length, curr_tg_start,
                    curr_tg_data_size);

            // Insert a copy of the Frame Header OBU.
            memcpy(curr_tg_start, fh_info.frame_header, fh_info.total_length);

            // Force context update tile to be the first tile in error
            // resilient mode as the duplicate frame headers will have
            // context_update_tile_id set to 0
            largest_tile_id = 0;

            // Rewrite the OBU header to change the OBU type to Redundant Frame
            // Header.
            av1_write_obu_header(
                cpi.ppi.level_params, ref cpi.frame_header_count,
                OBU_REDUNDANT_FRAME_HEADER,
                cpi.common.seq_params.has_nonzero_operating_point_idc,
                /*is_layer_specific_obu=*/true, obu_extn_header,
                curr_tg_start + fh_info.obu_header_byte_offset);

            curr_tg_data_size += fh_info.total_length;
            total_size += (uint)fh_info.total_length;
        }
        is_first_tg = 0;
    }

    internal static void av1_reset_pack_bs_thread_data(ThreadData td)
    {
        td.coefficient_size = 0;
        td.max_mv_magnitude = 0;
        av1_zero(td.interp_filter_selected);
    }

    internal static void av1_accumulate_pack_bs_thread_data(AV1_COMP cpi,
                                                            ThreadData td)
    {
        int do_max_mv_magnitude_update = 1;
        cpi.rc.coefficient_size += td.coefficient_size;

        // Disable max_mv_magnitude update for parallel frames based on update flag.
        if (!cpi.do_frame_data_update) do_max_mv_magnitude_update = 0;

        if (cpi.sf.mv_sf.auto_mv_step_size != 0 && do_max_mv_magnitude_update != 0)
            cpi.mv_search_params.max_mv_magnitude =
                AOMMAX(cpi.mv_search_params.max_mv_magnitude, td.max_mv_magnitude);

        for (InterpFilter filter = EIGHTTAP_REGULAR; filter < SWITCHABLE; filter++)
            cpi.common.cur_frame.interp_filter_selected[filter] +=
                td.interp_filter_selected[filter];
    }

    // [port] C `PackBSParams pack_bs_params` stack local of write_tile_obu: per-thread
    // scratch object (every field read later is set before use, as in C).
    [ThreadStatic] private static PackBSParams wto_pack_bs_params;

    // Store information related to each default tile in the OBU header.
    // PORT-NOTE: `uint32_t *total_size` points to pack_tiles_in_tg_obus()'s local and is
    // stored in PackBSParams.total_size, so it is a Ptr<uint>.
    private static void write_tile_obu(
        AV1_COMP cpi, Ptr<byte> dst, int dst_size,
        Ptr<uint> total_size, Ptr<aom_write_bit_buffer> saved_wb,
        byte obu_extn_header, in FrameHeaderInfo fh_info,
        ref int largest_tile_id, ref uint max_tile_size,
        ref uint obu_header_size, ref Ptr<byte> tile_data_start)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = cpi.td.mb.e_mbd;
        CommonTileParams tiles = cm.tiles;
        int tile_cols = tiles.cols;
        int tile_rows = tiles.rows;
        // Fixed size tile groups for the moment
        int num_tg_hdrs = cpi.num_tg;
        int tg_size = (tile_rows * tile_cols + num_tg_hdrs - 1) / num_tg_hdrs;
        int tile_count = 0;
        int curr_tg_data_size = 0;
        Ptr<byte> tile_data_curr = dst;
        int new_tg = 1;
        int is_first_tg = 1;

        av1_reset_pack_bs_thread_data(cpi.td);
        for (int tile_row = 0; tile_row < tile_rows; tile_row++)
        {
            for (int tile_col = 0; tile_col < tile_cols; tile_col++)
            {
                int tile_idx = tile_row * tile_cols + tile_col;
                TileDataEnc this_tile = cpi.tile_data[tile_idx];

                int is_last_tile_in_tg = 0;
                if (new_tg != 0)
                {
                    tile_data_curr = dst + (int)total_size.Value;
                    tile_count = 0;
                }
                tile_count++;

                if (tile_count == tg_size || tile_idx == (tile_cols * tile_rows - 1))
                    is_last_tile_in_tg = 1;

                xd.tile_ctx = this_tile.tctx;

                // PackBSParams stores all parameters required to pack tile and header
                // info.
                PackBSParams pack_bs_params = wto_pack_bs_params ??= new PackBSParams();
                pack_bs_params.dst = dst;
                pack_bs_params.tile_buf_size = dst_size;
                pack_bs_params.curr_tg_hdr_size = 0;
                pack_bs_params.is_last_tile_in_tg = is_last_tile_in_tg;
                pack_bs_params.new_tg = new_tg;
                pack_bs_params.obu_extn_header = obu_extn_header;
                pack_bs_params.obu_header_size = 0;
                pack_bs_params.saved_wb = saved_wb;
                pack_bs_params.tile_col = tile_col;
                pack_bs_params.tile_row = tile_row;
                pack_bs_params.tile_data_curr = tile_data_curr;
                pack_bs_params.total_size = total_size;

                if (new_tg != 0)
                    av1_write_obu_tg_tile_headers(cpi, xd, pack_bs_params, tile_idx);

                av1_pack_tile_info(cpi, cpi.td, pack_bs_params);

                if (new_tg != 0)
                {
                    curr_tg_data_size = pack_bs_params.curr_tg_hdr_size;
                    tile_data_start += pack_bs_params.curr_tg_hdr_size;
                    obu_header_size = pack_bs_params.obu_header_size;
                    new_tg = 0;
                }
                if (is_last_tile_in_tg != 0) new_tg = 1;

                curr_tg_data_size +=
                    (pack_bs_params.buf.size + (is_last_tile_in_tg != 0 ? 0 : 4));

                if ((uint)pack_bs_params.buf.size > max_tile_size)
                {
                    largest_tile_id = tile_idx;
                    max_tile_size = (uint)pack_bs_params.buf.size;
                }

                if (is_last_tile_in_tg != 0)
                    av1_write_last_tile_info(cpi, in fh_info, saved_wb, ref curr_tg_data_size,
                                             tile_data_curr, ref total_size.Value,
                                             ref tile_data_start, ref largest_tile_id,
                                             ref is_first_tg, obu_header_size, obu_extn_header);
                total_size.Value += (uint)pack_bs_params.buf.size;
            }
        }
        av1_accumulate_pack_bs_thread_data(cpi, cpi.td);
    }

    // Write total buffer size and related information into the OBU header for
    // default tile case.
    private static void write_tile_obu_size(AV1_COMP cpi, Ptr<byte> dst,
                                            int dst_size,
                                            Ptr<aom_write_bit_buffer> saved_wb,
                                            int largest_tile_id, ref uint total_size,
                                            uint max_tile_size,
                                            uint obu_header_size,
                                            Ptr<byte> tile_data_start)
    {
        // TODO: bug 42302568 - Use dst_size.
        _ = dst_size;
        CommonTileParams tiles = cpi.common.tiles;

        // Fill in context_update_tile_id indicating the tile to use for the
        // cdf update. The encoder currently sets it to the largest tile
        // (but is up to the encoder)
        aom_wb_overwrite_literal(ref saved_wb.Value, largest_tile_id,
                                 (tiles.log2_cols + tiles.log2_rows));
        // If more than one tile group. tile_size_bytes takes the default value 4
        // and does not need to be set. For a single tile group it is set in the
        // section below.
        if (cpi.num_tg != 1) return;
        int tile_size_bytes = 4, unused = 0;
        uint tile_data_offset = (uint)(tile_data_start - dst);
        uint tile_data_size = total_size - tile_data_offset;

        total_size = (uint)remux_tiles(tiles, tile_data_start, tile_data_size,
                                       max_tile_size, 0, ref tile_size_bytes, ref unused);
        total_size += tile_data_offset;
        Debug.Assert(tile_size_bytes >= 1 && tile_size_bytes <= 4);

        aom_wb_overwrite_literal(ref saved_wb.Value, tile_size_bytes - 1, 2);

        // Update the OBU length if remux_tiles() reduced the size.
        ulong payload_size = 0;
        int length_field_size = 0;
        int res =
            aom_uleb_decode(dst + (int)obu_header_size, (int)(total_size - obu_header_size),
                            ref payload_size, ref length_field_size);
        Debug.Assert(res == 0);

        ulong new_payload_size =
            (ulong)total_size - obu_header_size - (ulong)length_field_size;
        if (new_payload_size != payload_size)
        {
            int new_length_field_size = 0;
            res = aom_uleb_encode(new_payload_size, length_field_size,
                                  dst + (int)obu_header_size, ref new_length_field_size);
            Debug.Assert(res == 0);
            if (new_length_field_size < length_field_size)
            {
                int src_offset = (int)obu_header_size + length_field_size;
                int dst_offset = (int)obu_header_size + new_length_field_size;
                memmove(dst + dst_offset, dst + src_offset, (int)payload_size);
                total_size -= (uint)(length_field_size - new_length_field_size);
            }
        }
    }

    // As per the experiments, single-thread bitstream packing is better for
    // frames with a smaller bitstream size. This behavior is due to setup time
    // overhead of multithread function would be more than that of time required
    // to pack the smaller bitstream of such frames. This function computes the
    // number of required number of workers based on setup time overhead and job
    // dispatch time overhead for given tiles and available workers.
    private static int calc_pack_bs_mt_workers(TileDataEnc[] tile_data, int num_tiles,
                                               int avail_workers, bool pack_bs_mt_enabled)
    {
        if (!pack_bs_mt_enabled) return 1;

        ulong frame_abs_sum_level = 0;

        for (int idx = 0; idx < num_tiles; idx++)
            frame_abs_sum_level += tile_data[idx].abs_sum_level;

        int ideal_num_workers = 1;
        float job_disp_time_const = (float)num_tiles * JOB_DISP_TIME_OH_CONST;
        float max_sum = 0.0f;

        for (int num_workers = avail_workers; num_workers > 1; num_workers--)
        {
            float fas_per_worker_const =
                ((float)(num_workers - 1) / num_workers) * frame_abs_sum_level;
            float setup_time_const = (float)num_workers * SETUP_TIME_OH_CONST;
            float this_sum = fas_per_worker_const - setup_time_const -
                             job_disp_time_const / num_workers;

            if (this_sum > max_sum)
            {
                max_sum = this_sum;
                ideal_num_workers = num_workers;
            }
        }
        return ideal_num_workers;
    }

    // [av1rt] The number of workers pack_tiles_in_tg_obus will use for this frame (the tiles'
    // abs_sum_level are final once the tiles are encoded).
    internal static int av1rt_pack_bs_num_workers(AV1_COMP cpi)
    {
        CommonTileParams tiles = cpi.common.tiles;
        return calc_pack_bs_mt_workers(cpi.tile_data, tiles.rows * tiles.cols,
                                       cpi.mt_info.num_mod_workers[MOD_PACK_BS],
                                       cpi.mt_info.pack_bs_mt_enabled);
    }

    // [port] C `uint32_t total_size` local of pack_tiles_in_tg_obus, whose address is
    // stored in PackBSParams: per-thread 1-element scratch array.
    [ThreadStatic] private static uint[] ptig_total_size;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint pack_tiles_in_tg_obus(
        AV1_COMP cpi, Ptr<byte> dst, int dst_size,
        Ptr<aom_write_bit_buffer> saved_wb, byte obu_extension_header,
        in FrameHeaderInfo fh_info, ref int largest_tile_id)
    {
        CommonTileParams tiles = cpi.common.tiles;
        uint[] total_size = ptig_total_size ??= new uint[1];
        total_size[0] = 0;
        uint max_tile_size = 0;
        uint obu_header_size = 0;
        Ptr<byte> tile_data_start = dst;
        int tile_cols = tiles.cols;
        int tile_rows = tiles.rows;
        int num_tiles = tile_rows * tile_cols;

        int num_workers = calc_pack_bs_mt_workers(
            cpi.tile_data, num_tiles, cpi.mt_info.num_mod_workers[MOD_PACK_BS],
            cpi.mt_info.pack_bs_mt_enabled);

        if (num_workers > 1)
        {
            av1_write_tile_obu_mt(cpi, dst, dst_size, ref total_size[0], saved_wb,
                                  obu_extension_header, in fh_info, ref largest_tile_id,
                                  ref max_tile_size, ref obu_header_size, ref tile_data_start,
                                  num_workers);
        }
        else
        {
            write_tile_obu(cpi, dst, dst_size, total_size, saved_wb,
                           obu_extension_header, in fh_info, ref largest_tile_id,
                           ref max_tile_size, ref obu_header_size, ref tile_data_start);
        }

        if (num_tiles > 1)
            write_tile_obu_size(cpi, dst, dst_size, saved_wb, largest_tile_id,
                                ref total_size[0], max_tile_size, obu_header_size,
                                tile_data_start);
        return total_size[0];
    }

    private static uint write_tiles_in_tg_obus(AV1_COMP cpi, Ptr<byte> dst,
                                               int dst_size,
                                               Ptr<aom_write_bit_buffer> saved_wb,
                                               byte obu_extension_header,
                                               in FrameHeaderInfo fh_info,
                                               ref int largest_tile_id)
    {
        AV1_COMMON cm = cpi.common;
        CommonTileParams tiles = cm.tiles;
        largest_tile_id = 0;

        // Select the coding strategy (temporal or spatial)
        if (cm.seg.enabled != 0 && cm.seg.update_map != 0)
        {
            if (cm.features.primary_ref_frame == PRIMARY_REF_NONE)
            {
                cm.seg.temporal_update = 0;
            }
            else
            {
                cm.seg.temporal_update = 1;
                if (cpi.td.rd_counts.seg_tmp_pred_cost[0] <
                    cpi.td.rd_counts.seg_tmp_pred_cost[1])
                    cm.seg.temporal_update = 0;
            }
        }

        if (tiles.large_scale != 0)
            // [subset] pack_large_scale_tiles_in_tg_obus: large-scale tile coding only.
            throw new NotSupportedException("[subset] pack_large_scale_tiles_in_tg_obus");

        return pack_tiles_in_tg_obus(cpi, dst, dst_size, saved_wb,
                                     obu_extension_header, in fh_info, ref largest_tile_id);
    }

    // Returns the number of bytes written on success. Returns 0 on failure.
    private static int av1_write_metadata_obu(aom_metadata_t metadata,
                                              Ptr<byte> dst, int dst_size)
    {
        int coded_metadata_size = 0;
        ulong metadata_type = (ulong)metadata.type;
        if (aom_uleb_encode(metadata_type, dst_size, dst, ref coded_metadata_size) !=
            0)
        {
            return 0;
        }
        if (coded_metadata_size + metadata.sz + 1 > dst_size)
        {
            return 0;
        }
        memcpy(dst + coded_metadata_size, metadata.payload, metadata.sz);
        // Add trailing bits.
        dst[coded_metadata_size + metadata.sz] = 0x80;
        return coded_metadata_size + metadata.sz + 1;
    }

    private const int AOM_MIF_LAYER_SPECIFIC = 0x10;       // aom/internal/aom_image_internal.h
    private const int AOM_MIF_INSERT_LOCATION_MASK = 0x0f; // aom/internal/aom_image_internal.h

    private static int av1_write_metadata_array(AV1_COMP cpi, Ptr<byte> dst,
                                                int dst_size)
    {
        if (cpi.source == null) return 0;
        AV1_COMMON cm = cpi.common;
        aom_metadata_array_t arr = cpi.source.metadata;
        if (arr == null) return 0;
        int obu_header_size = 0;
        int obu_payload_size = 0;
        int total_bytes_written = 0;
        int length_field_size = 0;
        for (int i = 0; i < arr.sz; i++)
        {
            aom_metadata_t current_metadata = arr.metadata_array[i];
            if (current_metadata != null && !current_metadata.payload.IsNull)
            {
                int metadata_insert_location =
                    current_metadata.insert_flag & AOM_MIF_INSERT_LOCATION_MASK;
                if ((cm.current_frame.frame_type == KEY_FRAME &&
                     metadata_insert_location == AOM_MIF_KEY_FRAME) ||
                    (cm.current_frame.frame_type != KEY_FRAME &&
                     metadata_insert_location == AOM_MIF_NON_KEY_FRAME) ||
                    metadata_insert_location == AOM_MIF_ANY_FRAME)
                {
                    // OBU header is either one or two bytes.
                    if (dst_size < 2)
                    {
                        throw new AomException(AOM_CODEC_ERROR,
                                               "av1_write_metadata_array: output buffer full");
                    }
                    bool is_layer_specific_obu =
                        (current_metadata.insert_flag & AOM_MIF_LAYER_SPECIFIC) != 0;
                    obu_header_size = (int)av1_write_obu_header(
                        cpi.ppi.level_params, ref cpi.frame_header_count, OBU_METADATA,
                        cm.seq_params.has_nonzero_operating_point_idc,
                        is_layer_specific_obu, 0, dst);
                    Debug.Assert(obu_header_size <= 2);
                    obu_payload_size =
                        av1_write_metadata_obu(current_metadata, dst + obu_header_size,
                                               dst_size - obu_header_size);
                    if (obu_payload_size == 0)
                    {
                        throw new AomException(AOM_CODEC_ERROR,
                                               "av1_write_metadata_array: output buffer full");
                    }
                    length_field_size =
                        obu_memmove(obu_header_size, obu_payload_size, dst, dst_size);
                    if (length_field_size == 0)
                    {
                        throw new AomException(AOM_CODEC_ERROR,
                                               "av1_write_metadata_array: output buffer full");
                    }
                    if (av1_write_uleb_obu_size(obu_payload_size, dst + obu_header_size,
                                                length_field_size) == AOM_CODEC_OK)
                    {
                        int obu_size =
                            obu_header_size + length_field_size + obu_payload_size;
                        dst += obu_size;
                        dst_size -= obu_size;
                        total_bytes_written += obu_size;
                    }
                    else
                    {
                        throw new AomException(AOM_CODEC_ERROR,
                                               "av1_write_metadata_array: output buffer full");
                    }
                }
            }
        }
        return total_bytes_written;
    }

    // [port] C `struct aom_write_bit_buffer saved_wb` local of av1_pack_bitstream, whose
    // address is handed down (and stored in PackBSParams): per-thread 1-element scratch.
    [ThreadStatic] private static aom_write_bit_buffer[] pb_saved_wb;

    internal static int av1_pack_bitstream(AV1_COMP cpi, Ptr<byte> dst, int dst_size,
                                           ref int size, ref int largest_tile_id)
    {
        Ptr<byte> data = dst;
        int data_size = dst_size;
        AV1_COMMON cm = cpi.common;
        AV1LevelParams level_params = cpi.ppi.level_params;
        uint obu_header_size = 0;
        uint obu_payload_size = 0;
        FrameHeaderInfo fh_info = default;
        byte obu_extension_header =
            (byte)(cm.temporal_layer_id << 5 | cm.spatial_layer_id << 3 | 0);

        // If no non-zero delta_q has been used, reset delta_q_present_flag
        if (cm.delta_q_info.delta_q_present_flag != 0 && cpi.deltaq_used == 0)
        {
            cm.delta_q_info.delta_q_present_flag = 0;
        }

        // CONFIG_BITSTREAM_DEBUG is 0.

        cpi.frame_header_count = 0;

        // The TD is now written outside the frame encode loop

        // write sequence header obu at each key frame or intra_only frame,
        // preceded by 4-byte size
        if (cm.current_frame.frame_type == INTRA_ONLY_FRAME ||
            cm.current_frame.frame_type == KEY_FRAME)
        {
            // OBU header is either one or two bytes.
            if (data_size < 2)
            {
                return AOM_CODEC_ERROR;
            }
            obu_header_size = av1_write_obu_header(
                level_params, ref cpi.frame_header_count, OBU_SEQUENCE_HEADER,
                cm.seq_params.has_nonzero_operating_point_idc,
                /*is_layer_specific_obu=*/false, 0, data);
            Debug.Assert(obu_header_size <= 2);
            obu_payload_size = av1_write_sequence_header_obu(
                cm.seq_params, data + (int)obu_header_size, data_size - (int)obu_header_size);
            int length_field_size =
                obu_memmove((int)obu_header_size, (int)obu_payload_size, data, data_size);
            if (length_field_size == 0)
            {
                return AOM_CODEC_ERROR;
            }
            if (av1_write_uleb_obu_size((int)obu_payload_size, data + (int)obu_header_size,
                                        length_field_size) != AOM_CODEC_OK)
            {
                return AOM_CODEC_ERROR;
            }

            int bytes_written =
                (int)obu_header_size + length_field_size + (int)obu_payload_size;
            data += bytes_written;
            data_size -= bytes_written;
        }

        // write metadata obus before the frame obu that has the show_frame flag set
        if (cm.show_frame != 0)
        {
            int bytes_written = av1_write_metadata_array(cpi, data, data_size);
            data += bytes_written;
            data_size -= bytes_written;
        }

        int write_frame_header =
            B(cpi.num_tg > 1 || encode_show_existing_frame(cm) != 0);
        aom_write_bit_buffer[] saved_wb_buf = pb_saved_wb ??= new aom_write_bit_buffer[1];
        saved_wb_buf[0] = new aom_write_bit_buffer(Ptr<byte>.Null, 0);
        Ptr<aom_write_bit_buffer> saved_wb = saved_wb_buf;
        int length_field = 0;
        if (write_frame_header != 0)
        {
            // Write Frame Header OBU.
            fh_info.frame_header = data;
            // OBU header is either one or two bytes.
            if (data_size < 2)
            {
                return AOM_CODEC_ERROR;
            }
            obu_header_size = av1_write_obu_header(
                level_params, ref cpi.frame_header_count, OBU_FRAME_HEADER,
                cm.seq_params.has_nonzero_operating_point_idc,
                /*is_layer_specific_obu=*/true, obu_extension_header, data);
            // TODO: bug 42302568 - Pass data_size - obu_header_size to
            // write_frame_header_obu().
            obu_payload_size = write_frame_header_obu(cpi, cpi.td.mb.e_mbd, saved_wb,
                                                      data + (int)obu_header_size, 1);

            length_field =
                obu_memmove((int)obu_header_size, (int)obu_payload_size, data, data_size);
            if (length_field == 0)
            {
                return AOM_CODEC_ERROR;
            }
            if (av1_write_uleb_obu_size((int)obu_payload_size, data + (int)obu_header_size,
                                        length_field) != AOM_CODEC_OK)
            {
                return AOM_CODEC_ERROR;
            }

            fh_info.obu_header_byte_offset = 0;
            fh_info.total_length = (int)obu_header_size + length_field + (int)obu_payload_size;
            // Make sure it is safe to cast fh_info.total_length to uint32_t.
            // [port] total_length is an int here, so the UINT32_MAX check cannot fire.
            data += fh_info.total_length;
            data_size -= fh_info.total_length;
        }

        if (encode_show_existing_frame(cm) == 0)
        {
            // Since length_field is determined adaptively after frame header
            // encoding, saved_wb must be adjusted accordingly.
            if (!saved_wb.Value.bit_buffer.IsNull)
            {
                saved_wb.Value.bit_buffer += length_field;
            }

            //  Each tile group obu will be preceded by 4-byte size of the tile group
            //  obu
            int bytes_written =
                (int)write_tiles_in_tg_obus(cpi, data, data_size, saved_wb,
                                            obu_extension_header, in fh_info,
                                            ref largest_tile_id);
            data += bytes_written;
            data_size -= bytes_written;
        }
        size = data - dst;
        _ = data_size;
        return AOM_CODEC_OK;
    }
}
