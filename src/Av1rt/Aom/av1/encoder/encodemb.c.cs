// Port of libaom av1/encoder/encodemb.c

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class encodemb
{
    // [subset] avg_wxh_block_c, avg_wxh_block_horiz_c, avg_wxh_block_vert_c,
    // fill_residue_outside_frame and av1_pad_block_border: only used when
    // cpi->do_border_pad is set (GOOD mode with DELTA_Q_OBJECTIVE); never executed.
    // [subset] DROPOUT_* parameters, av1_dropout_qcoeff and av1_dropout_qcoeff_num: dropout
    // optimization is never enabled in the realtime path; never executed.
    // [subset] av1_xform_dc_only: only used by the RD tx search; never executed.

    private static void encode_block(int plane, int block, int blk_row, int blk_col,
                                     BLOCK_SIZE plane_bsize, TX_SIZE tx_size, object arg,
                                     RUN_TYPE dry_run)
    {
        _ = dry_run;
        encode_b_args args = (encode_b_args)arg;
        AV1_COMP cpi = args.cpi;
        AV1_COMMON cm = cpi.common;
        MACROBLOCK x = args.x;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mbmi = xd.mi[0];
        macroblock_plane p = x.plane[plane];
        macroblockd_plane pd = xd.plane[plane];
        Ptr<tran_low_t> dqcoeff = p.dqcoeff + BLOCK_OFFSET(block);
        Ptr<byte> dst;
        Ptr<ENTROPY_CONTEXT> a, l;

        dst = pd.dst.buf + ((blk_row * pd.dst.stride + blk_col) << MI_SIZE_LOG2);

        a = args.ta + blk_col;
        l = args.tl + blk_row;

        TX_TYPE tx_type = DCT_DCT;
        if (mbmi.skip_mode == 0)
        {
            tx_type = av1_get_tx_type(xd, pd.plane_type, blk_row, blk_col, tx_size,
                                      B(cm.features.reduced_tx_set_used));

            av1_subtract_txb(x, plane, plane_bsize, blk_col, blk_row, tx_size, tx_type,
                             cpi.do_border_pad);
            TxfmParam txfm_param = default;
            QUANT_PARAM quant_param = default;
            int use_trellis = is_trellis_used(args.enable_optimize_b, dry_run);
            int quant_idx;
            if (use_trellis != 0)
                quant_idx = AV1_XFORM_QUANT_FP;
            else
                quant_idx =
                    USE_B_QUANT_NO_TRELLIS != 0 ? AV1_XFORM_QUANT_B : AV1_XFORM_QUANT_FP;
            av1_setup_xform(cm, x, tx_size, tx_type, ref txfm_param);
            av1_setup_quant(tx_size, use_trellis, quant_idx,
                            cpi.oxcf.q_cfg.quant_b_adapt, ref quant_param);
            av1_setup_qmatrix(cm.quant_params, xd, plane, tx_size, tx_type,
                              ref quant_param);
            av1_xform_quant(x, plane, block, blk_row, blk_col, plane_bsize, ref txfm_param,
                            in quant_param);
            if (use_trellis != 0)
            {
                // [subset] get_txb_ctx + av1_optimize_b: trellis optimization is disabled
                // (optimize_seg_arr[] is NO_TRELLIS_OPT in the realtime path).
                throw new NotSupportedException("av1_optimize_b (trellis)");
            }
        }
        else
        {
            p.eobs[block] = 0;
            p.txb_entropy_ctx[block] = 0;
        }

        av1_set_txb_context(x, plane, block, tx_size, a, l);

        if (p.eobs[block] != 0)
        {
            // As long as any YUV plane has non-zero quantized transform coefficients,
            // mbmi->skip_txfm flag is set to 0.
            mbmi.skip_txfm = 0;
            av1_inverse_transform_block(xd, dqcoeff, plane, tx_type, tx_size, dst,
                                        pd.dst.stride, p.eobs[block],
                                        B(cm.features.reduced_tx_set_used));
        }
        else
        {
            // Only when YUV planes all have zero quantized transform coefficients,
            // mbmi->skip_txfm flag is set to 1.
            mbmi.skip_txfm &= 1;
        }

        // TODO(debargha, jingning): Temporarily disable txk_type check for eob=0
        // case. It is possible that certain collision in hash index would cause
        // the assertion failure. To further optimize the rate-distortion
        // performance, we need to re-visit this part and enable this assert
        // again.
        if (p.eobs[block] == 0 && plane == 0)
        {
            update_txk_array(xd, blk_row, blk_col, tx_size, DCT_DCT);
        }
    }

    private static void encode_block_inter(int plane, int block, int blk_row, int blk_col,
                                           BLOCK_SIZE plane_bsize, TX_SIZE tx_size,
                                           object arg, RUN_TYPE dry_run)
    {
        encode_b_args args = (encode_b_args)arg;
        MACROBLOCK x = args.x;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mbmi = xd.mi[0];
        macroblockd_plane pd = xd.plane[plane];
        int max_blocks_high = max_block_high(xd, plane_bsize, plane);
        int max_blocks_wide = max_block_wide(xd, plane_bsize, plane);

        if (blk_row >= max_blocks_high || blk_col >= max_blocks_wide) return;

        TX_SIZE plane_tx_size =
            plane != 0 ? av1_get_max_uv_txsize(mbmi.bsize, pd.subsampling_x,
                                               pd.subsampling_y)
                       : mbmi.inter_tx_size[av1_get_txb_size_index(plane_bsize, blk_row,
                                                                   blk_col)];
        if (plane == 0)
        {
            Debug.Assert(tx_size_wide[tx_size] >= tx_size_wide[plane_tx_size] &&
                         tx_size_high[tx_size] >= tx_size_high[plane_tx_size]);
        }

        if (tx_size == plane_tx_size || plane != 0)
        {
            encode_block(plane, block, blk_row, blk_col, plane_bsize, tx_size, arg,
                         dry_run);
        }
        else
        {
            Debug.Assert(tx_size < TX_SIZES_ALL);
            TX_SIZE sub_txs = sub_tx_size_map[tx_size];
            Debug.Assert(!(tx_size <= TX_4X4) || sub_txs == tx_size);
            Debug.Assert(!(tx_size > TX_4X4) || sub_txs < tx_size);
            // This is the square transform block partition entry point.
            int bsw = tx_size_wide_unit[sub_txs];
            int bsh = tx_size_high_unit[sub_txs];
            int step = bsh * bsw;
            int row_end =
                AOMMIN(tx_size_high_unit[tx_size], max_blocks_high - blk_row);
            int col_end =
                AOMMIN(tx_size_wide_unit[tx_size], max_blocks_wide - blk_col);
            Debug.Assert(bsw > 0 && bsh > 0);

            for (int row = 0; row < row_end; row += bsh)
            {
                int offsetr = blk_row + row;
                for (int col = 0; col < col_end; col += bsw)
                {
                    int offsetc = blk_col + col;

                    encode_block_inter(plane, block, offsetr, offsetc, plane_bsize, sub_txs,
                                       arg, dry_run);
                    block += step;
                }
            }
        }
    }

    internal static void av1_foreach_transformed_block_in_plane(
        MACROBLOCKD xd, BLOCK_SIZE plane_bsize, int plane,
        foreach_transformed_block_visitor visit, object arg)
    {
        macroblockd_plane pd = xd.plane[plane];
        // block and transform sizes, in number of 4x4 blocks log 2 ("*_b")
        // 4x4=0, 8x8=2, 16x16=4, 32x32=6, 64x64=8
        // transform size varies per plane, look it up in a common way.
        TX_SIZE tx_size = av1_get_tx_size(plane, xd);
        BLOCK_SIZE tx_bsize = txsize_to_bsize[tx_size];
        // Call visit() directly with zero offsets if the current block size is the
        // same as the transform block size.
        if (plane_bsize == tx_bsize)
        {
            visit(plane, 0, 0, 0, plane_bsize, tx_size, arg);
            return;
        }
        int txw_unit = tx_size_wide_unit[tx_size];
        int txh_unit = tx_size_high_unit[tx_size];
        int step = txw_unit * txh_unit;

        // If mb_to_right_edge is < 0 we are in a situation in which
        // the current block size extends into the UMV and we won't
        // visit the sub blocks that are wholly within the UMV.
        int max_blocks_wide = max_block_wide(xd, plane_bsize, plane);
        int max_blocks_high = max_block_high(xd, plane_bsize, plane);
        BLOCK_SIZE max_unit_bsize =
            get_plane_block_size(BLOCK_64X64, pd.subsampling_x, pd.subsampling_y);
        int mu_blocks_wide =
            AOMMIN(mi_size_wide[max_unit_bsize], max_blocks_wide);
        int mu_blocks_high =
            AOMMIN(mi_size_high[max_unit_bsize], max_blocks_high);

        // Keep track of the row and column of the blocks we use so that we know
        // if we are in the unrestricted motion border.
        int i = 0;
        for (int r = 0; r < max_blocks_high; r += mu_blocks_high)
        {
            int unit_height = AOMMIN(mu_blocks_high + r, max_blocks_high);
            // Skip visiting the sub blocks that are wholly within the UMV.
            for (int c = 0; c < max_blocks_wide; c += mu_blocks_wide)
            {
                int unit_width = AOMMIN(mu_blocks_wide + c, max_blocks_wide);
                for (int blk_row = r; blk_row < unit_height; blk_row += txh_unit)
                {
                    for (int blk_col = c; blk_col < unit_width; blk_col += txw_unit)
                    {
                        visit(plane, i, blk_row, blk_col, plane_bsize, tx_size, arg);
                        i += step;
                    }
                }
            }
        }
        // Check if visit() is invoked at least once.
        Debug.Assert(i >= 1);
    }

    // [subset] encode_block_pass1_args, encode_block_pass1 and av1_encode_sby_pass1: first
    // pass only (one-pass realtime); never executed.

    // [port] C: `struct optimize_ctx ctx` and `struct encode_b_args arg` are stack locals of
    // av1_encode_sb (not re-entered while live); ctx.ta/tl are filled by
    // av1_get_entropy_contexts before use, so per-thread instances are reused.
    [ThreadStatic] private static optimize_ctx s_encode_sb_ctx;
    [ThreadStatic] private static encode_b_args s_encode_sb_arg;

    internal static void av1_encode_sb(AV1_COMP cpi, MACROBLOCK x, BLOCK_SIZE bsize,
                                       RUN_TYPE dry_run)
    {
        Debug.Assert(bsize < BLOCK_SIZES_ALL);
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mbmi = xd.mi[0];
        // In the current encoder implementation, for inter blocks,
        // only when YUV planes all have zero quantized transform coefficients,
        // mbmi->skip_txfm flag is set to 1.
        // For intra blocks, this flag is set to 0 since skipped blocks are so rare
        // that transmitting skip_txfm = 1 is very expensive.
        // mbmi->skip_txfm is init to 1, and will be modified in encode_block() based
        // on transform, quantization, and (if exists) trellis optimization.
        mbmi.skip_txfm = 1;
        if (x.txfm_search_info.skip_txfm != 0) return;

        optimize_ctx ctx = s_encode_sb_ctx ??= new optimize_ctx();
        encode_b_args arg = s_encode_sb_arg ??= new encode_b_args();
        arg.cpi = cpi;
        arg.x = x;
        arg.ctx = ctx;
        arg.ta = Ptr<ENTROPY_CONTEXT>.Null;
        arg.tl = Ptr<ENTROPY_CONTEXT>.Null;
        arg.dry_run = dry_run;
        arg.enable_optimize_b = cpi.optimize_seg_arr[mbmi.segment_id];
        AV1_COMMON cm = cpi.common;
        int num_planes = av1_num_planes(cm);
        for (int plane = 0; plane < num_planes; ++plane)
        {
            macroblockd_plane pd = xd.plane[plane];
            int subsampling_x = pd.subsampling_x;
            int subsampling_y = pd.subsampling_y;
            if (plane != 0 && !xd.is_chroma_ref) break;
            BLOCK_SIZE plane_bsize =
                get_plane_block_size(bsize, subsampling_x, subsampling_y);
            Debug.Assert(plane_bsize < BLOCK_SIZES_ALL);
            int mi_width = mi_size_wide[plane_bsize];
            int mi_height = mi_size_high[plane_bsize];
            TX_SIZE max_tx_size = get_vartx_max_txsize(xd, plane_bsize, plane);
            BLOCK_SIZE txb_size = txsize_to_bsize[max_tx_size];
            int bw = mi_size_wide[txb_size];
            int bh = mi_size_high[txb_size];
            int block = 0;
            int step =
                tx_size_wide_unit[max_tx_size] * tx_size_high_unit[max_tx_size];
            av1_get_entropy_contexts(plane_bsize, pd, ctx.ta[plane], ctx.tl[plane]);
            arg.ta = ctx.ta[plane];
            arg.tl = ctx.tl[plane];
            BLOCK_SIZE max_unit_bsize =
                get_plane_block_size(BLOCK_64X64, subsampling_x, subsampling_y);
            int mu_blocks_wide = mi_size_wide[max_unit_bsize];
            int mu_blocks_high = mi_size_high[max_unit_bsize];
            mu_blocks_wide = AOMMIN(mi_width, mu_blocks_wide);
            mu_blocks_high = AOMMIN(mi_height, mu_blocks_high);

            for (int idy = 0; idy < mi_height; idy += mu_blocks_high)
            {
                for (int idx = 0; idx < mi_width; idx += mu_blocks_wide)
                {
                    int blk_row, blk_col;
                    int unit_height = AOMMIN(mu_blocks_high + idy, mi_height);
                    int unit_width = AOMMIN(mu_blocks_wide + idx, mi_width);
                    for (blk_row = idy; blk_row < unit_height; blk_row += bh)
                    {
                        for (blk_col = idx; blk_col < unit_width; blk_col += bw)
                        {
                            encode_block_inter(plane, block, blk_row, blk_col, plane_bsize,
                                               max_tx_size, arg, dry_run);
                            block += step;
                        }
                    }
                }
            }
        }
    }

    private static void encode_block_intra(int plane, int block, int blk_row, int blk_col,
                                           BLOCK_SIZE plane_bsize, TX_SIZE tx_size,
                                           object arg)
    {
        encode_b_args args = (encode_b_args)arg;
        AV1_COMP cpi = args.cpi;
        AV1_COMMON cm = cpi.common;
        MACROBLOCK x = args.x;
        MACROBLOCKD xd = x.e_mbd;
        macroblock_plane p = x.plane[plane];
        macroblockd_plane pd = xd.plane[plane];
        Ptr<tran_low_t> dqcoeff = p.dqcoeff + BLOCK_OFFSET(block);
        PLANE_TYPE plane_type = get_plane_type(plane);
        Ptr<ushort> eob = p.eobs + block;
        int dst_stride = pd.dst.stride;
        Ptr<byte> dst = pd.dst.buf + ((blk_row * dst_stride + blk_col) << MI_SIZE_LOG2);

        av1_predict_intra_block_facade(cm, xd, plane, blk_col, blk_row, tx_size);

        TX_TYPE tx_type = DCT_DCT;
        if (xd.mi[0].skip_txfm != 0)
        {
            eob.Value = 0;
            p.txb_entropy_ctx[block] = 0;
        }
        else
        {
            Ptr<ENTROPY_CONTEXT> a = args.ta + blk_col;
            Ptr<ENTROPY_CONTEXT> l = args.tl + blk_row;
            _ = a;
            _ = l;
            tx_type = av1_get_tx_type(xd, plane_type, blk_row, blk_col, tx_size,
                                      B(cm.features.reduced_tx_set_used));
            TX_TYPE primary_tx_type = is_stat_generation_stage(cpi) != 0 ? DCT_DCT : tx_type;
            av1_subtract_txb(x, plane, plane_bsize, blk_col, blk_row, tx_size,
                             primary_tx_type, cpi.do_border_pad);
            TxfmParam txfm_param = default;
            QUANT_PARAM quant_param = default;
            int use_trellis =
                is_trellis_used(args.enable_optimize_b, args.dry_run);
            int quant_idx;
            if (use_trellis != 0)
                quant_idx = AV1_XFORM_QUANT_FP;
            else
                quant_idx =
                    USE_B_QUANT_NO_TRELLIS != 0 ? AV1_XFORM_QUANT_B : AV1_XFORM_QUANT_FP;

            av1_setup_xform(cm, x, tx_size, tx_type, ref txfm_param);
            av1_setup_quant(tx_size, use_trellis, quant_idx,
                            cpi.oxcf.q_cfg.quant_b_adapt, ref quant_param);
            av1_setup_qmatrix(cm.quant_params, xd, plane, tx_size, tx_type,
                              ref quant_param);

            av1_xform_quant(x, plane, block, blk_row, blk_col, plane_bsize, ref txfm_param,
                            in quant_param);
            if (use_trellis != 0)
            {
                // [subset] get_txb_ctx(a, l) + av1_optimize_b: trellis optimization is
                // disabled (optimize_seg_arr[] is NO_TRELLIS_OPT in the realtime path).
                throw new NotSupportedException("av1_optimize_b (trellis)");
            }
        }

        if (eob.Value != 0)
        {
            av1_inverse_transform_block(xd, dqcoeff, plane, tx_type, tx_size, dst,
                                        dst_stride, eob.Value,
                                        B(cm.features.reduced_tx_set_used));
        }

        // TODO(jingning): Temporarily disable txk_type check for eob=0 case.
        // It is possible that certain collision in hash index would cause
        // the assertion failure. To further optimize the rate-distortion
        // performance, we need to re-visit this part and enable this assert
        // again.
        if (eob.Value == 0 && plane == 0)
        {
            update_txk_array(xd, blk_row, blk_col, tx_size, DCT_DCT);
        }

        // [subset] cfl_store_tx: compiled out (!CONFIG_REALTIME_ONLY).
    }

    private static void encode_block_intra_and_set_context(int plane, int block,
                                                           int blk_row, int blk_col,
                                                           BLOCK_SIZE plane_bsize,
                                                           TX_SIZE tx_size, object arg)
    {
        encode_block_intra(plane, block, blk_row, blk_col, plane_bsize, tx_size, arg);

        encode_b_args args = (encode_b_args)arg;
        MACROBLOCK x = args.x;
        Ptr<ENTROPY_CONTEXT> a = args.ta + blk_col;
        Ptr<ENTROPY_CONTEXT> l = args.tl + blk_row;
        av1_set_txb_context(x, plane, block, tx_size, a, l);
    }

    // [port] C: `ENTROPY_CONTEXT ta/tl[MAX_MIB_SIZE] = { 0 }` and `struct encode_b_args arg`
    // are stack locals of av1_encode_intra_block_plane (not re-entered while live);
    // per-thread instances are reused and ta/tl re-zeroed on each call.
    [ThreadStatic] private static ENTROPY_CONTEXT[] s_intra_ta;
    [ThreadStatic] private static ENTROPY_CONTEXT[] s_intra_tl;
    [ThreadStatic] private static encode_b_args s_intra_arg;

    internal static void av1_encode_intra_block_plane(AV1_COMP cpi, MACROBLOCK x,
                                                      BLOCK_SIZE bsize, int plane,
                                                      RUN_TYPE dry_run,
                                                      TRELLIS_OPT_TYPE enable_optimize_b)
    {
        Debug.Assert(bsize < BLOCK_SIZES_ALL);
        MACROBLOCKD xd = x.e_mbd;
        if (plane != 0 && !xd.is_chroma_ref) return;

        macroblockd_plane pd = xd.plane[plane];
        int ss_x = pd.subsampling_x;
        int ss_y = pd.subsampling_y;
        ENTROPY_CONTEXT[] ta = s_intra_ta ??= new ENTROPY_CONTEXT[MAX_MIB_SIZE];
        ENTROPY_CONTEXT[] tl = s_intra_tl ??= new ENTROPY_CONTEXT[MAX_MIB_SIZE];
        ta.AsSpan().Clear();
        tl.AsSpan().Clear();
        encode_b_args arg = s_intra_arg ??= new encode_b_args();
        arg.cpi = cpi;
        arg.x = x;
        arg.ctx = null;
        arg.ta = ta;
        arg.tl = tl;
        arg.dry_run = dry_run;
        arg.enable_optimize_b = enable_optimize_b;
        BLOCK_SIZE plane_bsize = get_plane_block_size(bsize, ss_x, ss_y);
        if (enable_optimize_b != 0)
        {
            av1_get_entropy_contexts(plane_bsize, pd, ta, tl);
        }
        av1_foreach_transformed_block_in_plane(
            xd, plane_bsize, plane, encode_block_intra_and_set_context, arg);
    }
}
