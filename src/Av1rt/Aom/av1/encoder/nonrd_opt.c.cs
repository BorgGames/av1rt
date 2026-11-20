// Port of libaom av1/encoder/nonrd_opt.c

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class nonrd_opt
{
    // PORT-NOTE: `NONE` in C (best_pickmode->best_second_ref_frame = NONE) is not NONE_FRAME but
    // the ACTIVATION enumerator `enum { NONE, RELU, SOFTSIGN, SIGMOID }` from config/av1_rtcd.h,
    // i.e. 0 (== INTRA_FRAME). Kept as such for bit-exactness.
    private const int NONE = 0;

    // [subset] static const SCAN_ORDER av1_fast_idtx_scan_order_16x16: only used by
    // av1_block_yrd_idtx (identity transform search, use_idtx_nonrd = 0 outside screen content).

    // [port] DECLARE_BLOCK_YRD_BUFFERS() (dqcoeff_buf / qcoeff_buf / coeff_buf, tran_low_t[16 * 16]
    // on the stack, used through int16_t views in low bitdepth) and uint16_t eob[1] are the
    // per-thread NONRD_SCRATCH.yrd_* arrays (MACROBLOCK.nonrd_scratch, _fields/f2.cs).
    // DECLARE_BLOCK_YRD_VARS() / DECLARE_LOOP_VARS_BLOCK_YRD() are expanded inline.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void update_yrd_loop_vars(
        MACROBLOCK x, ref int skippable, int step, int ncoeffs,
        Ptr<short> low_coeff, Ptr<short> low_qcoeff,
        Ptr<short> low_dqcoeff, ref RD_STATS this_rdc, ref int eob_cost,
        int tx_blk_id)
    {
        int is_txfm_skip = B(ncoeffs == 0);
        skippable &= is_txfm_skip;
        x.txfm_search_info.blk_skip[tx_blk_id] = (byte)is_txfm_skip;
        eob_cost += get_msb(ncoeffs + 1);
        if (ncoeffs == 1)
            this_rdc.rate += Math.Abs((int)low_qcoeff[0]);
        else if (ncoeffs > 1)
            this_rdc.rate += aom_satd_lp(low_qcoeff, step << 4);

        this_rdc.dist += av1_block_error_lp(low_coeff, low_dqcoeff, step << 4) >> 2;
    }

    // PORT-NOTE: C writes the int16_t Hadamard coefficients into macroblock_plane::coeff
    // reinterpreted as int16_t (`(int16_t *)p->coeff + BLOCK_OFFSET(block)`), and av1_block_yrd()
    // reads them back through the same view. The int16_t view only lives between these two
    // functions (p->coeff is rewritten by the transform before it is next read as tran_low_t), so
    // the port keeps it in the per-thread NONRD_SCRATCH.coeff_lp buffer instead.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void aom_process_hadamard_lp_8x16(MACROBLOCK x,
                                                     int max_blocks_high,
                                                     int max_blocks_wide,
                                                     int num_4x4_w, int step,
                                                     int block_step)
    {
        macroblock_plane p = x.plane[AOM_PLANE_Y];
        int bw = 4 * num_4x4_w;
        int num_4x4 = AOMMIN(num_4x4_w, max_blocks_wide);
        int block = 0;
        Ptr<short> coeff_lp = x.nonrd_scratch.coeff_lp;

        for (int r = 0; r < max_blocks_high; r += block_step)
        {
            for (int c = 0; c < num_4x4; c += 2 * block_step)
            {
                Ptr<short> src_diff = p.src_diff + ((r * bw + c) << 2);
                Ptr<short> low_coeff = coeff_lp + BLOCK_OFFSET(block);
                aom_hadamard_lp_8x8_dual(src_diff, bw, low_coeff);
                block += 2 * step;
            }
        }
    }

    // [subset] DECLARE_BLOCK_YRD_HBD_VARS / update_yrd_loop_vars_hbd: CONFIG_AV1_HIGHBITDEPTH == 0.

    // Calculates RD Cost using Hadamard transform.
    //
    // Calculates RD Cost using Hadamard transform. For low bit depth this function
    // uses low-precision set of functions (16-bit) and 32 bit for high bit depth
    // x              Pointer to structure holding all the data for the current macroblock
    // this_rdc       Pointer to calculated RD Cost
    // skippable      Pointer to a flag indicating possible tx skip
    // bsize          Current block size
    // tx_size        Transform size
    //
    // Nothing is returned. Instead, calculated RD cost is placed to this_rdc.
    // skippable flag is set if there is no non-zero quantized coefficients for
    // Hadamard transform
    internal static void av1_block_yrd(MACROBLOCK x, ref RD_STATS this_rdc, ref int skippable,
                                       BLOCK_SIZE bsize, TX_SIZE tx_size)
    {
        MACROBLOCKD xd = x.e_mbd;
        macroblockd_plane pd = xd.plane[AOM_PLANE_Y];
        macroblock_plane p = x.plane[AOM_PLANE_Y];
        Debug.Assert(bsize < BLOCK_SIZES_ALL);
        int num_4x4_w = mi_size_wide[bsize];
        int num_4x4_h = mi_size_high[bsize];
        int step = 1 << (tx_size << 1);
        int block_step = (1 << tx_size);
        int row_step = step * num_4x4_w >> tx_size;
        int block = 0;
        int max_blocks_wide =
            num_4x4_w + (xd.mb_to_right_edge >= 0 ? 0 : xd.mb_to_right_edge >> 5);
        int max_blocks_high =
            num_4x4_h + (xd.mb_to_bottom_edge >= 0 ? 0 : xd.mb_to_bottom_edge >> 5);
        int eob_cost = 0;
        int bw = 4 * num_4x4_w;
        int bh = 4 * num_4x4_h;
        int num_blk_skip_w = num_4x4_w;

        aom_subtract_block(bh, bw, p.src_diff, bw, p.src.buf, p.src.stride,
                           pd.dst.buf, pd.dst.stride);

        // Keep the intermediate value on the stack here. Writing directly to
        // skippable causes speed regression due to load-and-store issues in
        // update_yrd_loop_vars.
        int temp_skippable = 1;
        this_rdc.dist = 0;
        this_rdc.rate = 0;
        // For block sizes 8x16 or above, Hadamard txfm of two adjacent 8x8 blocks
        // can be done per function call. Hence the call of Hadamard txfm is
        // abstracted here for the specified cases.
        bool is_tx_8x8_dual_applicable =
            (tx_size == TX_8X8 && block_size_wide[bsize] >= 16 &&
             block_size_high[bsize] >= 8);

        if (is_tx_8x8_dual_applicable)
        {
            aom_process_hadamard_lp_8x16(x, max_blocks_high, max_blocks_wide, num_4x4_w,
                                         step, block_step);
        }

        SCAN_ORDER scan_order = av1_scan_orders[tx_size][DCT_DCT];
        // DECLARE_BLOCK_YRD_BUFFERS()
        NONRD_SCRATCH scratch = x.nonrd_scratch;
        Ptr<ushort> eob = scratch.yrd_eob;
        // DECLARE_BLOCK_YRD_VARS()
        // When is_tx_8x8_dual_applicable is true, we compute the txfm for the
        // entire bsize and write macroblock_plane::coeff. So low_coeff is kept
        // as a non-const so we can reassign it to macroblock_plane::coeff.
        Ptr<short> low_coeff = scratch.yrd_coeff_buf;
        Ptr<short> low_qcoeff = scratch.yrd_qcoeff_buf;
        Ptr<short> low_dqcoeff = scratch.yrd_dqcoeff_buf;
        int diff_stride = bw;

        // Keep track of the row and column of the blocks we use so that we know
        // if we are in the unrestricted motion border.
        for (int r = 0; r < max_blocks_high; r += block_step)
        {
            for (int c = 0, s = 0; c < max_blocks_wide; c += block_step, s += step)
            {
                // DECLARE_LOOP_VARS_BLOCK_YRD()
                Ptr<short> src_diff = p.src_diff + ((r * diff_stride + c) << 2);

                switch (tx_size)
                {
                    case TX_16X16:
                        aom_hadamard_lp_16x16(src_diff, diff_stride, low_coeff);
                        av1_quantize_lp(low_coeff, 16 * 16, p.round_fp_QTX, p.quant_fp_QTX,
                                        low_qcoeff, low_dqcoeff, p.dequant_QTX, eob,
                                        default_scan_lp_16x16_transpose,
                                        av1_default_iscan_lp_16x16_transpose);
                        break;
                    case TX_8X8:
                        if (is_tx_8x8_dual_applicable)
                        {
                            // The coeffs are pre-computed for the whole block, so re-assign
                            // low_coeff to the appropriate location.
                            int block_offset = BLOCK_OFFSET(block + s);
                            low_coeff = (Ptr<short>)scratch.coeff_lp + block_offset;
                        }
                        else
                        {
                            aom_hadamard_lp_8x8(src_diff, diff_stride, low_coeff);
                        }
                        av1_quantize_lp(low_coeff, 8 * 8, p.round_fp_QTX, p.quant_fp_QTX,
                                        low_qcoeff, low_dqcoeff, p.dequant_QTX, eob,
                                        default_scan_8x8_transpose,
                                        av1_default_iscan_8x8_transpose);
                        break;
                    default:
                        // [subset] aom_fdct4x4_lp (tx_size == TX_4X4): never executed (the
                        // nonrd path never picks TX_4X4 here) and aom_fdct4x4_lp is not ported.
                        _ = scan_order;
                        throw new NotSupportedException("av1_block_yrd: TX_4X4");
                }
                Debug.Assert(eob[0] <= 1024);
                update_yrd_loop_vars(x, ref temp_skippable, step, eob[0], low_coeff,
                                     low_qcoeff, low_dqcoeff, ref this_rdc, ref eob_cost,
                                     r * num_blk_skip_w + c);
            }
            block += row_step;
        }

        this_rdc.skip_txfm = (byte)(skippable = temp_skippable);
        if (this_rdc.sse < long.MaxValue)
        {
            this_rdc.sse = (this_rdc.sse << 6) >> 2;
            if (temp_skippable != 0)
            {
                this_rdc.dist = 0;
                this_rdc.dist = this_rdc.sse;
                return;
            }
        }

        // If skippable is set, rate gets clobbered later.
        // Clamp to INT_MAX / 2 to avoid the INT_MAX invalid-rate sentinel and leave
        // headroom for mode, MV, and chroma rate costs added by callers.
        long block_rate =
            ((long)this_rdc.rate << (2 + AV1_PROB_COST_SHIFT)) +
            ((long)eob_cost << AV1_PROB_COST_SHIFT);
        this_rdc.rate = (int)AOMMIN(block_rate, int.MaxValue / 2);
    }

    // [subset] scale_square_buf_vals, av1_block_yrd_idtx: identity transform search
    // (rt_sf.use_idtx_nonrd) is only enabled for screen content.

    internal static long av1_model_rd_for_sb_uv(AV1_COMP cpi, BLOCK_SIZE plane_bsize,
                                                MACROBLOCK x, MACROBLOCKD xd,
                                                ref RD_STATS this_rdc, int start_plane,
                                                int stop_plane)
    {
        // Note our transform coeffs are 8 times an orthogonal transform.
        // Hence quantizer step is also 8 times. To get effective quantizer
        // we need to divide by 8 before sending to modeling function.
        uint sse = 0;
        int rate = 0;
        long dist = 0;
        int plane;
        long tot_sse = 0;

        this_rdc.rate = 0;
        this_rdc.dist = 0;
        this_rdc.skip_txfm = 0;

        for (plane = start_plane; plane <= stop_plane; ++plane)
        {
            macroblock_plane p = x.plane[plane];
            macroblockd_plane pd = xd.plane[plane];
            uint dc_quant = (uint)p.dequant_QTX[0];
            uint ac_quant = (uint)p.dequant_QTX[1];
            BLOCK_SIZE bs = plane_bsize;
            uint var;
            if (x.color_sensitivity[COLOR_SENS_IDX(plane)] == 0) continue;

            var = cpi.ppi.fn_ptr[bs].vf(p.src.buf, p.src.stride, pd.dst.buf,
                                        pd.dst.stride, ref sse);
            Debug.Assert(sse >= var);
            tot_sse += sse;

            av1_model_rd_from_var_lapndz(sse - var, num_pels_log2_lookup[bs],
                                         dc_quant >> 3, ref rate, ref dist);

            this_rdc.rate += rate >> 1;
            this_rdc.dist += dist << 3;

            av1_model_rd_from_var_lapndz(var, num_pels_log2_lookup[bs], ac_quant >> 3,
                                         ref rate, ref dist);

            this_rdc.rate += rate;
            this_rdc.dist += dist << 4;
        }

        if (this_rdc.rate == 0)
        {
            this_rdc.skip_txfm = 1;
        }

        if (RDCOST(x.rdmult, this_rdc.rate, this_rdc.dist) >=
            RDCOST(x.rdmult, 0, tot_sse << 4))
        {
            this_rdc.rate = 0;
            this_rdc.dist = tot_sse << 4;
            this_rdc.skip_txfm = 1;
        }

        return tot_sse;
    }

    private static void compute_intra_yprediction(AV1_COMMON cm,
                                                  PREDICTION_MODE mode, BLOCK_SIZE bsize,
                                                  MACROBLOCK x, MACROBLOCKD xd)
    {
        SequenceHeader seq_params = cm.seq_params;
        macroblockd_plane pd = xd.plane[AOM_PLANE_Y];
        macroblock_plane p = x.plane[AOM_PLANE_Y];
        Ptr<byte> src_buf_base = p.src.buf;
        Ptr<byte> dst_buf_base = pd.dst.buf;
        int src_stride = p.src.stride;
        int dst_stride = pd.dst.stride;
        int plane = 0;
        int row, col;
        // block and transform sizes, in number of 4x4 blocks log 2 ("*_b")
        // 4x4=0, 8x8=2, 16x16=4, 32x32=6, 64x64=8
        // transform size varies per plane, look it up in a common way.
        TX_SIZE tx_size = max_txsize_lookup[bsize];
        BLOCK_SIZE plane_bsize =
            get_plane_block_size(bsize, pd.subsampling_x, pd.subsampling_y);
        // If mb_to_right_edge is < 0 we are in a situation in which
        // the current block size extends into the UMV and we won't
        // visit the sub blocks that are wholly within the UMV.
        int max_blocks_wide = max_block_wide(xd, plane_bsize, plane);
        int max_blocks_high = max_block_high(xd, plane_bsize, plane);
        // Keep track of the row and column of the blocks we use so that we know
        // if we are in the unrestricted motion border.
        for (row = 0; row < max_blocks_high; row += (1 << tx_size))
        {
            // Skip visiting the sub blocks that are wholly within the UMV.
            for (col = 0; col < max_blocks_wide; col += (1 << tx_size))
            {
                p.src.buf = src_buf_base + (int)(4 * (row * (long)src_stride + col));
                pd.dst.buf = dst_buf_base + (int)(4 * (row * (long)dst_stride + col));
                av1_predict_intra_block(
                    xd, seq_params.sb_size, seq_params.enable_intra_edge_filter,
                    block_size_wide[bsize], block_size_high[bsize], tx_size, mode, 0, 0,
                    FILTER_INTRA_MODES, pd.dst.buf, dst_stride, pd.dst.buf, dst_stride,
                    0, 0, plane);
            }
        }
        p.src.buf = src_buf_base;
        pd.dst.buf = dst_buf_base;
    }

    // Checks whether Intra mode needs to be pruned based on
    // 'intra_y_mode_bsize_mask_nrd' and 'prune_hv_pred_modes_using_blksad'
    // speed features.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool is_prune_intra_mode(
        AV1_COMP cpi, int mode_index, int force_intra_check, BLOCK_SIZE bsize,
        byte segment_id, SOURCE_SAD source_sad_nonrd,
        byte[] color_sensitivity /* [MAX_MB_PLANE - 1] */)
    {
        PREDICTION_MODE this_mode = intra_mode_list[mode_index];
        if (mode_index > 2 || force_intra_check == 0)
        {
            if (((1 << this_mode) & cpi.sf.rt_sf.intra_y_mode_bsize_mask_nrd[bsize]) == 0)
                return true;

            if (this_mode == DC_PRED) return false;

            if (!cpi.sf.rt_sf.prune_hv_pred_modes_using_src_sad) return false;

            bool has_color_sensitivity =
                color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] != 0 &&
                color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] != 0;
            if (has_color_sensitivity &&
                (cpi.rc.frame_source_sad > 1.1 * cpi.rc.avg_source_sad ||
                 cyclic_refresh_segment_id_boosted(segment_id) != 0 ||
                 source_sad_nonrd > kMedSad))
                return false;

            return true;
        }
        return false;
    }

    // Estimation of RD cost of an intra mode for Non-RD optimized case.
    //
    // Calculates RD Cost for an intra mode for a single TX block using Hadamard
    // transform.
    // plane          Color plane
    // block          Index of a TX block in a prediction block
    // row            Row of a current TX block
    // col            Column of a current TX block
    // plane_bsize    Block size of a current prediction block
    // tx_size        Transform size
    // arg            Pointer to a structure that holds parameters
    //                for intra mode search
    //
    // Nothing is returned. Instead, best mode and RD Cost of the best mode
    // are set in args->rdc and args->mode
    internal static void av1_estimate_block_intra(int plane, int block, int row, int col,
                                                  BLOCK_SIZE plane_bsize, TX_SIZE tx_size,
                                                  object arg)
    {
        estimate_block_intra_args args = (estimate_block_intra_args)arg;
        AV1_COMP cpi = args.cpi;
        AV1_COMMON cm = cpi.common;
        MACROBLOCK x = args.x;
        MACROBLOCKD xd = x.e_mbd;
        macroblock_plane p = x.plane[plane];
        macroblockd_plane pd = xd.plane[plane];
        BLOCK_SIZE bsize_tx = txsize_to_bsize[tx_size];
        Ptr<byte> src_buf_base = p.src.buf;
        Ptr<byte> dst_buf_base = pd.dst.buf;
        long src_stride = p.src.stride;
        long dst_stride = pd.dst.stride;

        _ = block;

        av1_predict_intra_block_facade(cm, xd, plane, col, row, tx_size);

        if (args.prune_mode_based_on_sad || args.prune_palette_sad)
        {
            uint this_sad = cpi.ppi.fn_ptr[plane_bsize].sdf(
                p.src.buf, p.src.stride, pd.dst.buf, pd.dst.stride);
            uint sad_threshold =
                args.best_sad != uint.MaxValue ? args.best_sad + (args.best_sad >> 4)
                                               : uint.MaxValue;
            // Skip the evaluation of current mode if its SAD is more than a threshold.
            if (args.prune_mode_based_on_sad && this_sad > sad_threshold)
            {
                // For the current mode, set rate and distortion to maximum possible
                // values and return.
                // Note: args->rdc->rate is checked in av1_nonrd_pick_intra_mode() to skip
                // the evaluation of the current mode.
                args.rdc.Value.rate = int.MaxValue;
                args.rdc.Value.dist = long.MaxValue;
                return;
            }
            if (this_sad < args.best_sad)
            {
                args.best_sad = this_sad;
            }
        }

        RD_STATS this_rdc = default;
        av1_invalid_rd_stats(ref this_rdc);

        p.src.buf = src_buf_base + (int)(4 * (row * src_stride + col));
        pd.dst.buf = dst_buf_base + (int)(4 * (row * dst_stride + col));

        if (plane == 0)
        {
            av1_block_yrd(x, ref this_rdc, ref args.skippable, bsize_tx,
                          AOMMIN(tx_size, TX_16X16));
        }
        else
        {
            av1_model_rd_for_sb_uv(cpi, bsize_tx, x, xd, ref this_rdc, plane, plane);
        }

        p.src.buf = src_buf_base;
        pd.dst.buf = dst_buf_base;
        Debug.Assert(args.rdc.Value.rate != int.MaxValue && args.rdc.Value.dist != long.MaxValue);
        args.rdc.Value.rate =
            (int)AOMMIN((long)args.rdc.Value.rate + this_rdc.rate, int.MaxValue / 2);
        args.rdc.Value.dist += this_rdc.dist;
    }

    // [port] Cached delegate for av1_estimate_block_intra (avoids a delegate allocation per call
    // of av1_foreach_transformed_block_in_plane).
    private static readonly foreach_transformed_block_visitor s_estimate_block_intra =
        av1_estimate_block_intra;

    // Estimates best intra mode for inter mode search
    //
    // Using heuristics based on best inter mode, block size, and other decides
    // whether to check intra modes. If so, estimates and selects best intra mode
    // from the reduced set of intra modes (max 4 intra modes checked)
    //
    // cpi                      Top-level encoder structure
    // x                        Pointer to structure holding all the
    //                          data for the current macroblock
    // bsize                    Current block size
    // best_early_term          Flag, indicating that TX for the
    //                          best inter mode was skipped
    // ref_cost_intra           Cost of signalling intra mode
    // reuse_prediction         Flag, indicating prediction re-use
    // orig_dst                 Original destination buffer
    // tmp_buffers              Pointer to a temporary buffers for
    //                          prediction re-use
    // this_mode_pred           [out] Pointer to store prediction buffer
    //                          for prediction re-use
    // best_rdc                 Pointer to RD cost for the best
    //                          selected intra mode
    // best_pickmode            Pointer to a structure containing
    //                          best mode picked so far
    // ctx                      Pointer to structure holding coding
    //                          contexts and modes for the block
    //
    // Nothing is returned. Instead, calculated RD cost is placed to
    // best_rdc and best selected mode is placed to best_pickmode
    // PORT-NOTE: `PRED_BUFFER *tmp_buffers` is the caller's PRED_BUFFER[4] array;
    // `PRED_BUFFER **this_mode_pred` is `ref PRED_BUFFER`.
    internal static void av1_estimate_intra_mode(AV1_COMP cpi, MACROBLOCK x, BLOCK_SIZE bsize,
                                                 int best_early_term, uint ref_cost_intra,
                                                 int reuse_prediction, in buf_2d orig_dst,
                                                 PRED_BUFFER[] tmp_buffers,
                                                 ref PRED_BUFFER this_mode_pred,
                                                 ref RD_STATS best_rdc,
                                                 BEST_PICKMODE best_pickmode,
                                                 ref uint best_sad_norm)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mi = xd.mi[0];
        TxfmSearchParams txfm_params = x.txfm_search_params;
        byte segment_id = mi.segment_id;
        int[] rd_threshes = cpi.rd.threshes[segment_id][bsize];
        int[] rd_thresh_freq_fact = x.thresh_freq_fact[bsize];
        bool is_screen_content =
            cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN;
        macroblockd_plane pd = xd.plane[AOM_PLANE_Y];
        REAL_TIME_SPEED_FEATURES rt_sf = cpi.sf.rt_sf;

        CommonQuantParams quant_params = cm.quant_params;

        // [port] `RD_STATS this_rdc;` lives in a per-thread 1-element array because
        // args.rdc points at it (estimate_block_intra_args.rdc is a Ptr<RD_STATS>).
        RD_STATS[] this_rdc_buf = x.nonrd_scratch.intra_this_rdc;
        ref RD_STATS this_rdc = ref this_rdc_buf[0];

        int intra_cost_penalty = av1_get_intra_cost_penalty(
            quant_params.base_qindex, quant_params.y_dc_delta_q,
            cm.seq_params.bit_depth);
        long inter_mode_thresh =
            RDCOST(x.rdmult, ref_cost_intra + (uint)intra_cost_penalty, 0);
        int perform_intra_pred = rt_sf.check_intra_pred_nonrd;
        int force_intra_check = 0;
        // For spatial enhancement layer: turn off intra prediction if the
        // previous spatial layer as golden ref is not chosen as best reference.
        // only do this for temporal enhancement layer and on non-key frames.
        if (cpi.svc.spatial_layer_id > 0 &&
            best_pickmode.best_ref_frame != GOLDEN_FRAME &&
            cpi.svc.temporal_layer_id > 0 &&
            cpi.svc.layer_context[cpi.svc.temporal_layer_id].is_key_frame == 0)
            perform_intra_pred = 0;

        int do_early_exit_rdthresh = 1;

        uint spatial_var_thresh = 50;
        int motion_thresh = 32;
        // Adjust thresholds to make intra mode likely tested if the other
        // references (golden, alt) are skipped/not checked. For now always
        // adjust for svc mode.
        if (cpi.ppi.use_svc != 0 || (rt_sf.use_nonrd_altref_frame == 0 &&
                                     rt_sf.nonrd_prune_ref_frame_search > 0))
        {
            spatial_var_thresh = 150;
            motion_thresh = 0;
        }

        // Some adjustments to checking intra mode based on source variance.
        if (x.source_variance < spatial_var_thresh)
        {
            // If the best inter mode is large motion or non-LAST ref reduce intra cost
            // penalty, so intra mode is more likely tested.
            if (best_rdc.rdcost != long.MaxValue &&
                (best_pickmode.best_ref_frame != LAST_FRAME ||
                 Math.Abs((int)mi.mv[0].as_mv.row) >= motion_thresh ||
                 Math.Abs((int)mi.mv[0].as_mv.col) >= motion_thresh))
            {
                intra_cost_penalty = intra_cost_penalty >> 2;
                inter_mode_thresh =
                    RDCOST(x.rdmult, ref_cost_intra + (uint)intra_cost_penalty, 0);
                do_early_exit_rdthresh = 0;
            }
            if ((x.source_variance < AOMMAX(50u, (spatial_var_thresh >> 1)) &&
                 x.content_state_sb.source_sad_nonrd >= kHighSad) ||
                (is_screen_content && x.source_variance < 50 &&
                 ((bsize >= BLOCK_32X32 &&
                   x.content_state_sb.source_sad_nonrd != kZeroSad) ||
                  x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] == 1 ||
                  x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] == 1)))
                force_intra_check = 1;
            // For big blocks worth checking intra (since only DC will be checked),
            // even if best_early_term is set.
            if (bsize >= BLOCK_32X32) best_early_term = 0;
        }
        else if (rt_sf.source_metrics_sb_nonrd != 0 &&
                 x.content_state_sb.source_sad_nonrd <= kLowSad)
        {
            perform_intra_pred = 0;
        }

        if (best_rdc.skip_txfm != 0 && best_pickmode.best_mode_initial_skip_flag != 0)
        {
            if (rt_sf.skip_intra_pred == 1 && best_pickmode.best_mode != NEWMV)
                perform_intra_pred = 0;
            else if (rt_sf.skip_intra_pred == 2)
                perform_intra_pred = 0;
        }

        if (!(best_rdc.rdcost == long.MaxValue || force_intra_check != 0 ||
              (perform_intra_pred != 0 && best_early_term == 0 &&
               bsize <= cpi.sf.part_sf.max_intra_bsize)))
        {
            return;
        }

        // Early exit based on RD cost calculated using known rate. When
        // is_screen_content is true, more bias is given to intra modes. Hence,
        // considered conservative threshold in early exit for the same.
        long known_rd = is_screen_content
                            ? CALC_BIASED_RDCOST(inter_mode_thresh)
                            : inter_mode_thresh;
        if (known_rd > best_rdc.rdcost) return;

        estimate_block_intra_args args = x.nonrd_scratch.intra_args;
        init_estimate_block_intra_args(args, cpi, x);
        if (prune_palette_testing_inter(cpi, x.source_variance))
            args.prune_palette_sad = true;
        TX_SIZE intra_tx_size = AOMMIN(
            AOMMIN(max_txsize_lookup[bsize],
                   tx_mode_to_biggest_tx_size[txfm_params.tx_mode_search_type]),
            TX_16X16);
        if (is_screen_content && cpi.rc.high_source_sad != 0 &&
            x.source_variance > spatial_var_thresh && bsize <= BLOCK_16X16)
            intra_tx_size = TX_4X4;

        PRED_BUFFER best_pred = best_pickmode.best_pred;
        if (reuse_prediction != 0 && best_pred != null)
        {
            int bh = block_size_high[bsize];
            int bw = block_size_wide[bsize];
            if (best_pred.data == orig_dst.buf)
            {
                this_mode_pred = tmp_buffers[get_pred_buffer(tmp_buffers, 3)];
                aom_convolve_copy(best_pred.data, best_pred.stride,
                                  this_mode_pred.data, this_mode_pred.stride, bw,
                                  bh);
                best_pickmode.best_pred = this_mode_pred;
            }
        }
        pd.dst = orig_dst;

        for (int midx = 0; midx < RTC_INTRA_MODES; ++midx)
        {
            PREDICTION_MODE this_mode = intra_mode_list[midx];
            THR_MODES mode_index = mode_idx[INTRA_FRAME][mode_offset(this_mode)];
            long mode_rd_thresh = rd_threshes[mode_index];

            if (is_prune_intra_mode(cpi, midx, force_intra_check, bsize, segment_id,
                                    x.content_state_sb.source_sad_nonrd,
                                    x.color_sensitivity))
                continue;

            if (is_screen_content && rt_sf.source_metrics_sb_nonrd != 0)
            {
                // For spatially flat blocks with zero motion only check
                // DC mode.
                if (x.content_state_sb.source_sad_nonrd == kZeroSad &&
                    x.source_variance == 0 && this_mode != DC_PRED)
                    continue;
                // Only test Intra for big blocks if spatial_variance is small.
                else if (bsize > BLOCK_32X32 && x.source_variance > 50)
                    continue;
            }

            if (rd_less_than_thresh(best_rdc.rdcost, mode_rd_thresh,
                                    rd_thresh_freq_fact[mode_index]) != 0 &&
                (do_early_exit_rdthresh != 0 || this_mode == SMOOTH_PRED))
            {
                continue;
            }
            BLOCK_SIZE uv_bsize =
                get_plane_block_size(bsize, xd.plane[AOM_PLANE_U].subsampling_x,
                                     xd.plane[AOM_PLANE_U].subsampling_y);

            mi.mode = this_mode;
            mi.ref_frame[0] = INTRA_FRAME;
            mi.ref_frame[1] = NONE_FRAME;

            av1_invalid_rd_stats(ref this_rdc);
            args.mode = this_mode;
            args.skippable = 1;
            args.rdc = this_rdc_buf;
            mi.tx_size = intra_tx_size;
            compute_intra_yprediction(cm, this_mode, bsize, x, xd);
            // Look into selecting tx_size here, based on prediction residual.
            av1_block_yrd(x, ref this_rdc, ref args.skippable, bsize, mi.tx_size);
            // TODO(kyslov@) Need to account for skippable
            if (x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] != 0)
            {
                av1_foreach_transformed_block_in_plane(xd, uv_bsize, AOM_PLANE_U,
                                                       s_estimate_block_intra, args);
            }
            if (x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] != 0)
            {
                av1_foreach_transformed_block_in_plane(xd, uv_bsize, AOM_PLANE_V,
                                                       s_estimate_block_intra, args);
            }

            int mode_cost = 0;
            if (av1_is_directional_mode(this_mode) != 0 && av1_use_angle_delta(bsize) != 0)
            {
                mode_cost +=
                    x.mode_costs.angle_delta_cost[this_mode - V_PRED]
                                                 [MAX_ANGLE_DELTA +
                                                  mi.angle_delta[PLANE_TYPE_Y]];
            }
            if (this_mode == DC_PRED && av1_filter_intra_allowed_bsize(cm, bsize) != 0)
            {
                mode_cost += x.mode_costs.filter_intra_cost[bsize][0];
            }
            this_rdc.rate += (int)ref_cost_intra;
            this_rdc.rate += intra_cost_penalty;
            this_rdc.rate += mode_cost;
            this_rdc.rdcost = RDCOST(x.rdmult, this_rdc.rate, this_rdc.dist);

            if (is_screen_content && rt_sf.source_metrics_sb_nonrd != 0)
            {
                // For blocks with low spatial variance and color sad,
                // favor the intra-modes, only on scene/slide change.
                if (cpi.rc.high_source_sad != 0 && x.source_variance < 800 &&
                    (x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] != 0 ||
                     x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] != 0))
                    this_rdc.rdcost = CALC_BIASED_RDCOST(this_rdc.rdcost);
                // Otherwise bias against intra for blocks with zero
                // motion and no color, on non-scene/slide changes.
                else if (cpi.rc.high_source_sad == 0 && x.source_variance > 0 &&
                         x.content_state_sb.source_sad_nonrd == kZeroSad &&
                         x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] == 0 &&
                         x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] == 0)
                    this_rdc.rdcost = (3 * this_rdc.rdcost) >> 1;
            }

            if (this_rdc.rdcost < best_rdc.rdcost)
            {
                best_rdc = this_rdc;
                best_pickmode.best_mode = this_mode;
                best_pickmode.best_tx_size = mi.tx_size;
                best_pickmode.best_ref_frame = INTRA_FRAME;
                best_pickmode.best_second_ref_frame = NONE;
                best_pickmode.best_mode_skip_txfm = this_rdc.skip_txfm;
                mi.uv_mode = this_mode;
                mi.mv[0].as_int = INVALID_MV;
                mi.mv[1].as_int = INVALID_MV;
            }
        }
        mi.tx_size = best_pickmode.best_tx_size;

        best_sad_norm = args.best_sad >>
                        (b_width_log2_lookup[bsize] + b_height_log2_lookup[bsize]);
    }
}
