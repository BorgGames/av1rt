// Port of libaom av1/encoder/encodetxb.c
//
// CONFIG_ENTROPY_STATS is 0: the counts-only code under it is not ported.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class encodetxb
{
    internal static void av1_alloc_txb_buf(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        CoeffBufferPool coeff_buf_pool = cpi.coeff_buffer_pool;
        int mi_rows = cm.mi_params.mi_rows;
        int mi_cols = cm.mi_params.mi_cols;
        if (cpi.oxcf.frm_dim_cfg.forced_max_frame_width != 0)
        {
            mi_cols = size_in_mi(cpi.oxcf.frm_dim_cfg.forced_max_frame_width);
        }
        if (cpi.oxcf.frm_dim_cfg.forced_max_frame_height != 0)
        {
            mi_rows = size_in_mi(cpi.oxcf.frm_dim_cfg.forced_max_frame_height);
        }
        int num_sb_rows =
            CEIL_POWER_OF_TWO(mi_rows, cm.seq_params.mib_size_log2);
        int num_sb_cols =
            CEIL_POWER_OF_TWO(mi_cols, cm.seq_params.mib_size_log2);
        int size = num_sb_rows * num_sb_cols;
        int num_planes = av1_num_planes(cm);
        int subsampling_x = cm.seq_params.subsampling_x;
        int subsampling_y = cm.seq_params.subsampling_y;
        int luma_max_sb_square =
            1 << num_pels_log2_lookup[cm.seq_params.sb_size];
        int chroma_max_sb_square =
            luma_max_sb_square >> (subsampling_x + subsampling_y);
        int total_max_sb_square =
            (luma_max_sb_square + (num_planes - 1) * chroma_max_sb_square);
        // PORT-NOTE: C checks the size_t products against SIZE_MAX; a managed array is limited
        // to int.MaxValue elements, so the bound is checked against that instead (same error).
        if ((long)size * total_max_sb_square > int.MaxValue)
        {
            throw new AomException(AOM_CODEC_ERROR,
                                   "A multiplication would overflow size_t");
        }
        int num_tcoeffs = size * total_max_sb_square;
        const int txb_unit_size = TX_SIZE_W_MIN * TX_SIZE_H_MIN;

        av1_free_txb_buf(cpi);
        // TODO(jingning): This should be further reduced.
        cpi.coeff_buffer_base = new CB_COEFF_BUFFER[size];
        for (int i = 0; i < size; i++) cpi.coeff_buffer_base[i] = new CB_COEFF_BUFFER();
        cpi.coeff_buffer_stride = num_sb_cols;
        coeff_buf_pool.tcoeff = new tran_low_t[num_tcoeffs];
        coeff_buf_pool.eobs = new ushort[num_tcoeffs / txb_unit_size];
        coeff_buf_pool.entropy_ctx = new byte[num_tcoeffs / txb_unit_size];

        Ptr<tran_low_t> tcoeff_ptr = coeff_buf_pool.tcoeff;
        Ptr<ushort> eob_ptr = coeff_buf_pool.eobs;
        Ptr<byte> entropy_ctx_ptr = coeff_buf_pool.entropy_ctx;
        for (int i = 0; i < size; i++)
        {
            for (int plane = 0; plane < num_planes; plane++)
            {
                int max_sb_square =
                    (plane == AOM_PLANE_Y) ? luma_max_sb_square : chroma_max_sb_square;
                cpi.coeff_buffer_base[i].tcoeff[plane] = tcoeff_ptr;
                cpi.coeff_buffer_base[i].eobs[plane] = eob_ptr;
                cpi.coeff_buffer_base[i].entropy_ctx[plane] = entropy_ctx_ptr;
                tcoeff_ptr += max_sb_square;
                eob_ptr += max_sb_square / txb_unit_size;
                entropy_ctx_ptr += max_sb_square / txb_unit_size;
            }
        }
    }

    internal static void av1_free_txb_buf(AV1_COMP cpi)
    {
        CoeffBufferPool coeff_buf_pool = cpi.coeff_buffer_pool;
        cpi.coeff_buffer_base = null;
        cpi.coeff_buffer_stride = 0;
        coeff_buf_pool.tcoeff = Ptr<tran_low_t>.Null;
        coeff_buf_pool.eobs = Ptr<ushort>.Null;
        coeff_buf_pool.entropy_ctx = Ptr<byte>.Null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_golomb(aom_writer w, ref od_ec_enc_state s,
                                     int level)
    {
        int x = level + 1;
        int i = x;
        int length = 0;

        while (i != 0)
        {
            i >>= 1;
            ++length;
        }
        Debug.Assert(length > 0);

        for (i = 0; i < length - 1; ++i) aom_write_bit_state(w, ref s, 0);

        for (i = length - 1; i >= 0; --i) aom_write_bit_state(w, ref s, (x >> i) & 0x01);
    }

    // Writes the base range symbols of a level above NUM_BASE_LEVELS.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void write_coeff_br(aom_writer w, ref od_ec_enc_state s,
                                       Ptr<aom_cdf_prob> cdf, int level)
    {
        int base_range = level - 1 - NUM_BASE_LEVELS;
        for (int idx = 0; idx < COEFF_BASE_RANGE; idx += BR_CDF_SIZE - 1)
        {
            int k = AOMMIN(base_range - idx, BR_CDF_SIZE - 1);
            aom_write_symbol_state(w, ref s, k, cdf, BR_CDF_SIZE);
            if (k < BR_CDF_SIZE - 1) break;
        }
    }

    internal static int av1_get_eob_pos_token(int eob, ref int extra)
    {
        if (eob < 3)
        {
            extra = 0;
            return eob;
        }
        // For eob >= 3, eob groups double in width starting at 3, so token index is
        // floor(log2(eob - 1)) + 2 and extra is the offset within that group.
        int d = eob - 1;
        int k = get_msb(d);
        extra = d - (1 << k);
        return k + 2;
    }

    // [subset] update_eob_context: only reached with allow_update_cdf (see
    // av1_update_and_record_txb_context below).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int get_nz_map_ctx(Ptr<byte> levels,
                                      int coeff_idx, int bhl,
                                      int width, int scan_idx,
                                      int is_eob, TX_SIZE tx_size,
                                      TX_CLASS tx_class)
    {
        if (is_eob != 0) return get_lower_levels_ctx_eob(bhl, width, scan_idx);
        int stats =
            get_nz_mag(levels + get_padded_idx(coeff_idx, bhl), bhl, tx_class);
        return get_nz_map_ctx_from_stats(stats, coeff_idx, bhl, tx_size, tx_class);
    }

    internal static void av1_txb_init_levels_c(Ptr<tran_low_t> coeff, int width,
                                               int height, Ptr<byte> levels)
    {
        int stride = height + TX_PAD_HOR;
        Ptr<byte> ls = levels;

        memset<byte>(levels + stride * width, 0,
                     (TX_PAD_BOTTOM * stride + TX_PAD_END));

        int k = 0;
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                ls[k++] = (byte)clamp(Math.Abs(coeff[i * height + j]), 0, sbyte.MaxValue /* INT8_MAX */);
            }
            for (int j = 0; j < TX_PAD_HOR; j++)
            {
                ls[k++] = 0;
            }
        }
    }

    // RTCD: av1_txb_init_levels is av1_txb_init_levels_c in the pure-C oracle build.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_txb_init_levels(Ptr<tran_low_t> coeff, int width,
                                             int height, Ptr<byte> levels)
    {
        if (Simd.UseAvx2) av1_txb_init_levels_avx2(coeff, width, height, levels);
        else av1_txb_init_levels_c(coeff, width, height, levels);
    }

    internal static void av1_get_nz_map_contexts_c(Ptr<byte> levels,
                                                   Ptr<short> scan, int eob,
                                                   TX_SIZE tx_size, TX_CLASS tx_class,
                                                   Ptr<sbyte> coeff_contexts)
    {
        int bhl = get_txb_bhl(tx_size);
        int width = get_txb_wide(tx_size);
        for (int i = 0; i < eob; ++i)
        {
            int pos = scan[i];
            coeff_contexts[pos] = (sbyte)get_nz_map_ctx(levels, pos, bhl, width, i,
                                                        B(i == eob - 1), tx_size, tx_class);
        }
    }

    // RTCD: av1_get_nz_map_contexts is av1_get_nz_map_contexts_c in the pure-C oracle build.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_get_nz_map_contexts(Ptr<byte> levels,
                                                 Ptr<short> scan, int eob,
                                                 TX_SIZE tx_size, TX_CLASS tx_class,
                                                 Ptr<sbyte> coeff_contexts)
    {
        if (Simd.UseAvx2) av1_get_nz_map_contexts_sse2(levels, scan, eob, tx_size, tx_class, coeff_contexts);
        else av1_get_nz_map_contexts_c(levels, scan, eob, tx_size, tx_class, coeff_contexts);
    }

    // [port] C stack arrays of av1_write_coeffs_txb (uint8_t levels_buf[TX_PAD_2D],
    // int8_t coeff_contexts[MAX_TX_SQUARE], tran_low_t nz_coeffs[32 * 32]): per-thread scratch.
    // The function is a leaf (av1_write_tx_type does not re-enter it).
    [ThreadStatic] private static byte[] wct_levels_buf;
    [ThreadStatic] private static sbyte[] wct_coeff_contexts;
    [ThreadStatic] private static tran_low_t[] wct_nz_coeffs;

    internal static void av1_write_coeffs_txb(AV1_COMMON cm, MACROBLOCK x,
                                              aom_writer w, int blk_row, int blk_col, int plane,
                                              int block, TX_SIZE tx_size)
    {
        MACROBLOCKD xd = x.e_mbd;
        CB_COEFF_BUFFER cb_coef_buff = x.cb_coef_buff;
        PLANE_TYPE plane_type = get_plane_type(plane);
        int txb_offset = x.mbmi_ext_frame.Value.cb_offset[plane_type] /
                         (TX_SIZE_W_MIN * TX_SIZE_H_MIN);
        Ptr<ushort> eob_txb = cb_coef_buff.eobs[plane] + txb_offset;
        ushort eob = eob_txb[block];
        Ptr<byte> entropy_ctx = cb_coef_buff.entropy_ctx[plane] + txb_offset;
        int txb_skip_ctx = entropy_ctx[block] & TXB_SKIP_CTX_MASK;
        TX_SIZE txs_ctx = get_txsize_entropy_ctx(tx_size);
        FRAME_CONTEXT ec_ctx = xd.tile_ctx;
        aom_write_symbol(w, B(eob == 0), ec_ctx.txb_skip_cdf[txs_ctx][txb_skip_ctx], 2);
        if (eob == 0) return;

        TX_TYPE tx_type =
            av1_get_tx_type(xd, plane_type, blk_row, blk_col, tx_size,
                            B(cm.features.reduced_tx_set_used));
        // Only y plane's tx_type is transmitted
        if (plane == 0)
        {
            av1_write_tx_type(cm, xd, tx_type, tx_size, w);
        }

        int eob_extra = 0;
        int eob_pt = av1_get_eob_pos_token(eob, ref eob_extra);
        int eob_multi_size = txsize_log2_minus4[tx_size];
        TX_CLASS tx_class = tx_type_to_class[tx_type];
        int eob_multi_ctx = (tx_class == TX_CLASS_2D) ? 0 : 1;

        int width = get_txb_wide(tx_size);
        int height = get_txb_high(tx_size);
        byte[] levels_buf = wct_levels_buf ??= new byte[TX_PAD_2D];
        Ptr<byte> levels = set_levels(levels_buf, height);
        Ptr<tran_low_t> tcoeff_txb =
            cb_coef_buff.tcoeff[plane] + x.mbmi_ext_frame.Value.cb_offset[plane_type];
        Ptr<tran_low_t> tcoeff = tcoeff_txb + BLOCK_OFFSET(block);
        SCAN_ORDER scan_order = get_scan(tx_size, tx_type);
        Ptr<short> scan = scan_order.scan;
        sbyte[] coeff_contexts = wct_coeff_contexts ??= new sbyte[MAX_TX_SQUARE];
        if (eob > 1)
        {
            av1_txb_init_levels(tcoeff, width, height, levels);
            av1_get_nz_map_contexts(levels, scan, eob, tx_size, tx_class,
                                    coeff_contexts);
        }
        else
        {
            coeff_contexts[0] = 0;
        }

        // The rest of the block only writes symbols, so keep the range coder state
        // in registers.
        od_ec_enc_state st = od_ec_enc_get_state(w.ec);
        Ptr<aom_cdf_prob> eob_cdf;
        int eob_nsymbs;
        switch (eob_multi_size)
        {
            case 0:
                eob_cdf = ec_ctx.eob_flag_cdf16[plane_type][eob_multi_ctx];
                eob_nsymbs = 5;
                break;
            case 1:
                eob_cdf = ec_ctx.eob_flag_cdf32[plane_type][eob_multi_ctx];
                eob_nsymbs = 6;
                break;
            case 2:
                eob_cdf = ec_ctx.eob_flag_cdf64[plane_type][eob_multi_ctx];
                eob_nsymbs = 7;
                break;
            case 3:
                eob_cdf = ec_ctx.eob_flag_cdf128[plane_type][eob_multi_ctx];
                eob_nsymbs = 8;
                break;
            case 4:
                eob_cdf = ec_ctx.eob_flag_cdf256[plane_type][eob_multi_ctx];
                eob_nsymbs = 9;
                break;
            case 5:
                eob_cdf = ec_ctx.eob_flag_cdf512[plane_type][eob_multi_ctx];
                eob_nsymbs = 10;
                break;
            default:
                eob_cdf = ec_ctx.eob_flag_cdf1024[plane_type][eob_multi_ctx];
                eob_nsymbs = 11;
                break;
        }
        aom_write_symbol_state(w, ref st, eob_pt - 1, eob_cdf, eob_nsymbs);

        int eob_offset_bits = av1_eob_offset_bits[eob_pt];
        if (eob_offset_bits > 0)
        {
            int eob_ctx = eob_pt - 3;
            int eob_shift = eob_offset_bits - 1;
            int bit = (eob_extra & (1 << eob_shift)) != 0 ? 1 : 0;
            aom_write_symbol_state(
                w, ref st, bit, ec_ctx.eob_extra_cdf[txs_ctx][plane_type][eob_ctx], 2);
            for (int i2 = 1; i2 < eob_offset_bits; i2++)
            {
                eob_shift = eob_offset_bits - 1 - i2;
                bit = (eob_extra & (1 << eob_shift)) != 0 ? 1 : 0;
                aom_write_bit_state(w, ref st, bit);
            }
        }

        int bhl = get_txb_bhl(tx_size);
        aom_cdf_prob[][] base_cdf =
            ec_ctx.coeff_base_cdf[txs_ctx][plane_type];
        aom_cdf_prob[][] br_cdf =
            ec_ctx.coeff_br_cdf[AOMMIN(txs_ctx, TX_32X32)][plane_type];
        // The nonzero coefficients from c = eob - 1 down, for the sign loop.
        tran_low_t[] nz_coeffs = wct_nz_coeffs ??= new tran_low_t[32 * 32];
        Debug.Assert(eob <= av1_get_max_eob(tx_size));

        int num_nz = 0;
        {
            // The last coefficient is never 0 and has its own cdf.
            int pos = scan[eob - 1];
            tran_low_t v = tcoeff[pos];
            tran_low_t level = Math.Abs(v);
            nz_coeffs[num_nz++] = v;
            aom_write_symbol_state(
                w, ref st, AOMMIN(level, 3) - 1,
                ec_ctx.coeff_base_eob_cdf[txs_ctx][plane_type][coeff_contexts[pos]],
                3);
            if (level > NUM_BASE_LEVELS)
            {
                write_coeff_br(w, ref st, br_cdf[get_br_ctx_eob(pos, bhl, tx_class)], level);
            }
        }
        for (int c = eob - 2; c >= 0; --c)
        {
            int pos = scan[c];
            tran_low_t v = tcoeff[pos];
            tran_low_t level = Math.Abs(v);
            nz_coeffs[num_nz] = v;
            num_nz += B(v != 0);
            aom_write_symbol_state(w, ref st, AOMMIN(level, 3),
                                   base_cdf[coeff_contexts[pos]], 4);
            if (level > NUM_BASE_LEVELS)
            {
                write_coeff_br(w, ref st, br_cdf[get_br_ctx(levels, pos, bhl, tx_class)],
                               level);
            }
        }

        // Code all signs in the transform block, starting with the sign of DC (if
        // applicable). nz_coeffs holds them in the reverse order.
        int i = num_nz - 1;
        if (tcoeff[scan[0]] != 0)
        {
            tran_low_t v = nz_coeffs[i--];
            tran_low_t level = Math.Abs(v);
            int dc_sign_ctx =
                (entropy_ctx[block] >> DC_SIGN_CTX_SHIFT) & DC_SIGN_CTX_MASK;
            aom_write_symbol_state(w, ref st, B(v < 0),
                                   ec_ctx.dc_sign_cdf[plane_type][dc_sign_ctx], 2);
            if (level > COEFF_BASE_RANGE + NUM_BASE_LEVELS)
                write_golomb(w, ref st, level - COEFF_BASE_RANGE - 1 - NUM_BASE_LEVELS);
        }
        for (; i >= 0; --i)
        {
            tran_low_t v = nz_coeffs[i];
            tran_low_t level = Math.Abs(v);
            aom_write_bit_state(w, ref st, B(v < 0));
            if (level > COEFF_BASE_RANGE + NUM_BASE_LEVELS)
                write_golomb(w, ref st, level - COEFF_BASE_RANGE - 1 - NUM_BASE_LEVELS);
        }
        od_ec_enc_set_state(w.ec, in st);
    }

    internal static void av1_write_intra_coeffs_mb(AV1_COMMON cm, MACROBLOCK x,
                                                   aom_writer w, BLOCK_SIZE bsize)
    {
        MACROBLOCKD xd = x.e_mbd;
        int num_planes = av1_num_planes(cm);
        Span<int> block = stackalloc int[MAX_MB_PLANE];
        block.Clear();
        int row, col;
        Debug.Assert(bsize == get_plane_block_size(bsize, xd.plane[0].subsampling_x,
                                                   xd.plane[0].subsampling_y));
        int max_blocks_wide = max_block_wide(xd, bsize, 0);
        int max_blocks_high = max_block_high(xd, bsize, 0);
        const BLOCK_SIZE max_unit_bsize = BLOCK_64X64;
        int mu_blocks_wide = mi_size_wide[max_unit_bsize];
        int mu_blocks_high = mi_size_high[max_unit_bsize];
        mu_blocks_wide = AOMMIN(max_blocks_wide, mu_blocks_wide);
        mu_blocks_high = AOMMIN(max_blocks_high, mu_blocks_high);

        for (row = 0; row < max_blocks_high; row += mu_blocks_high)
        {
            for (col = 0; col < max_blocks_wide; col += mu_blocks_wide)
            {
                for (int plane = 0; plane < num_planes; ++plane)
                {
                    if (plane != 0 && !xd.is_chroma_ref) break;
                    TX_SIZE tx_size = av1_get_tx_size(plane, xd);
                    int stepr = tx_size_high_unit[tx_size];
                    int stepc = tx_size_wide_unit[tx_size];
                    int step = stepr * stepc;
                    macroblockd_plane pd = xd.plane[plane];
                    int unit_height = ROUND_POWER_OF_TWO(
                        AOMMIN(mu_blocks_high + row, max_blocks_high), pd.subsampling_y);
                    int unit_width = ROUND_POWER_OF_TWO(
                        AOMMIN(mu_blocks_wide + col, max_blocks_wide), pd.subsampling_x);
                    for (int blk_row = row >> pd.subsampling_y; blk_row < unit_height;
                         blk_row += stepr)
                    {
                        for (int blk_col = col >> pd.subsampling_x; blk_col < unit_width;
                             blk_col += stepc)
                        {
                            av1_write_coeffs_txb(cm, x, w, blk_row, blk_col, plane,
                                                 block[plane], tx_size);
                            block[plane] += step;
                        }
                    }
                }
            }
        }
    }

    // [subset] update_tx_type_count, update_and_record_txb_context_output and
    // av1_update_and_record_txb_context: the CDF-updating visitor is only chosen when
    // tile_data->allow_update_cdf is set, which the realtime configuration never does
    // (cdf_update_mode / delay_wait_for_top_right_sb; coverage 0).

    private static void record_txb_context_output(int plane, int block, int blk_row,
                                                  int blk_col, BLOCK_SIZE plane_bsize,
                                                  TX_SIZE tx_size,
                                                  tokenize_b_args args)
    {
        AV1_COMP cpi = args.cpi;
        AV1_COMMON cm = cpi.common;
        ThreadData td = args.td;
        MACROBLOCK x = td.mb;
        MACROBLOCKD xd = x.e_mbd;
        macroblock_plane p = x.plane[plane];
        macroblockd_plane pd = xd.plane[plane];
        int eob = p.eobs[block];
        PLANE_TYPE plane_type = pd.plane_type;
        MB_MODE_INFO mbmi = xd.mi[0];
        TXB_CTX txb_ctx = default;
        get_txb_ctx(plane_bsize, tx_size, plane, pd.above_entropy_context + blk_col,
                    pd.left_entropy_context + blk_row, ref txb_ctx);
        // C: txsize_ctx, bhl, width, height and cdf_idx are only read under
        // CONFIG_ENTROPY_STATS.

        CB_COEFF_BUFFER cb_coef_buff = x.cb_coef_buff;
        int txb_offset = x.mbmi_ext_frame.Value.cb_offset[plane_type] /
                         (TX_SIZE_W_MIN * TX_SIZE_H_MIN);
        Ptr<ushort> eob_txb = cb_coef_buff.eobs[plane] + txb_offset;
        Ptr<byte> entropy_ctx = cb_coef_buff.entropy_ctx[plane] + txb_offset;
        entropy_ctx[block] = (byte)txb_ctx.txb_skip_ctx;
        eob_txb[block] = (ushort)eob;

        if (eob == 0)
        {
            av1_set_entropy_contexts(xd, pd, plane, plane_bsize, tx_size, 0, blk_col,
                                     blk_row);
            return;
        }
        int block_offset = BLOCK_OFFSET(block);
        Ptr<tran_low_t> qcoeff = p.qcoeff + block_offset;
        TX_TYPE tx_type =
            av1_get_tx_type(xd, plane_type, blk_row, blk_col, tx_size,
                            B(cm.features.reduced_tx_set_used));
        SCAN_ORDER scan_order = get_scan(tx_size, tx_type);
        int segment_id = mbmi.segment_id;
        int seg_eob = av1_get_tx_eob(cpi.common.seg, segment_id, tx_size);
        Ptr<tran_low_t> tcoeff_txb =
            cb_coef_buff.tcoeff[plane] + x.mbmi_ext_frame.Value.cb_offset[plane_type];
        Ptr<tran_low_t> tcoeff = tcoeff_txb + block_offset;
        if (eob == 1)
        {
            tcoeff[0] = qcoeff[0];
        }
        else
        {
            memcpy(tcoeff, qcoeff, seg_eob);
        }

        bool do_coeff_scan = cpi.mt_info.pack_bs_mt_enabled;
        Ptr<short> scan = scan_order.scan;

        // record tx type usage
        td.rd_counts.tx_type_used[tx_size][tx_type]++;

        // [port] C sums |qcoeff| over the scan (c = eob - 1 down to 0) into abs_sum_level, then
        // av1_get_txb_entropy_context sums it again from c = 0, stopping once the sum reaches
        // COEFF_CONTEXT_MASK. With the full sum at hand, the clamped total is that context level.
        long abs_sum = -1;
        if (do_coeff_scan)
        {
            /* abs_sum_level is needed to decide the job scheduling order of
             * pack bitstream multi-threading. This data is not needed if
             * multi-threading is disabled. */
            ReadOnlySpan<short> sc = scan.AsSpan(0, eob);
            ReadOnlySpan<tran_low_t> qc = qcoeff.AsSpan(0, av1_get_max_eob(tx_size));
            long sum = 0;
            for (int c = 0; c < sc.Length; c++)
            {
                int v = qc[sc[c]];
                int m = v >> 31;
                sum += (v ^ m) - m;
            }
            td.abs_sum_level += (ulong)sum;
            abs_sum = sum;
        }
        // Update the context needed to code the DC sign (if applicable)
        if (tcoeff[0] != 0)
        {
            int dc_sign_ctx = txb_ctx.dc_sign_ctx;
            entropy_ctx[block] = (byte)(entropy_ctx[block] | (dc_sign_ctx << DC_SIGN_CTX_SHIFT));
        }
        byte cul_level;
        if (abs_sum >= 0)
        {
            int level = (int)AOMMIN(abs_sum, COEFF_CONTEXT_MASK);
            set_dc_sign(ref level, tcoeff[0]);
            cul_level = (byte)level;
        }
        else
        {
            cul_level = av1_get_txb_entropy_context(tcoeff, scan_order, eob);
        }
        av1_set_entropy_contexts(xd, pd, plane, plane_bsize, tx_size, cul_level,
                                 blk_col, blk_row);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void record_txb_context_dry_run(
        int plane, int block, int blk_row, int blk_col, BLOCK_SIZE plane_bsize,
        TX_SIZE tx_size, tokenize_b_args args)
    {
        Debug.Assert(args.dry_run != DRY_RUN_COSTCOEFFS);
        MACROBLOCK x = args.td.mb;
        MACROBLOCKD xd = x.e_mbd;
        macroblock_plane p = x.plane[plane];
        macroblockd_plane pd = xd.plane[plane];
        int eob = p.eobs[block];
        byte cul_level = 0;
        if (eob > 0)
        {
            AV1_COMMON cm = args.cpi.common;
            TX_TYPE tx_type =
                av1_get_tx_type(xd, pd.plane_type, blk_row, blk_col, tx_size,
                                B(cm.features.reduced_tx_set_used));
            SCAN_ORDER scan_order = get_scan(tx_size, tx_type);
            Ptr<tran_low_t> tcoeff = p.qcoeff + BLOCK_OFFSET(block);
            cul_level = av1_get_txb_entropy_context(tcoeff, scan_order, eob);
        }
        av1_set_entropy_contexts(xd, pd, plane, plane_bsize, tx_size, cul_level,
                                 blk_col, blk_row);
    }

    internal static void av1_record_txb_context(int plane, int block, int blk_row, int blk_col,
                                                BLOCK_SIZE plane_bsize, TX_SIZE tx_size,
                                                object arg)
    {
        tokenize_b_args args = (tokenize_b_args)arg;
        if (args.dry_run == OUTPUT_ENABLED)
        {
            record_txb_context_output(plane, block, blk_row, blk_col, plane_bsize,
                                      tx_size, args);
            return;
        }
        record_txb_context_dry_run(plane, block, blk_row, blk_col, plane_bsize,
                                   tx_size, args);
    }

    // [port] C `struct tokenize_b_args arg` stack local: per-thread scratch object (the
    // function does not re-enter itself).
    [ThreadStatic] private static tokenize_b_args uimtc_arg;

    internal static void av1_update_intra_mb_txb_context(AV1_COMP cpi, ThreadData td,
                                                         RUN_TYPE dry_run, BLOCK_SIZE bsize,
                                                         byte allow_update_cdf)
    {
        AV1_COMMON cm = cpi.common;
        int num_planes = av1_num_planes(cm);
        MACROBLOCK x = td.mb;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mbmi = xd.mi[0];
        tokenize_b_args arg = uimtc_arg ??= new tokenize_b_args();
        arg.cpi = cpi;
        arg.td = td;
        arg.this_rate = 0;
        arg.allow_update_cdf = allow_update_cdf;
        arg.dry_run = dry_run;
        if (mbmi.skip_txfm != 0)
        {
            av1_reset_entropy_context(xd, bsize, num_planes);
            return;
        }
        if (allow_update_cdf != 0)
        {
            // [subset] av1_update_and_record_txb_context: see above.
            throw new NotSupportedException(
                "[subset] av1_update_and_record_txb_context (allow_update_cdf)");
        }
        foreach_transformed_block_visitor visit = av1_record_txb_context;

        for (int plane = 0; plane < num_planes; ++plane)
        {
            if (plane != 0 && !xd.is_chroma_ref) break;
            macroblockd_plane pd = xd.plane[plane];
            int ss_x = pd.subsampling_x;
            int ss_y = pd.subsampling_y;
            BLOCK_SIZE plane_bsize = get_plane_block_size(bsize, ss_x, ss_y);
            av1_foreach_transformed_block_in_plane(xd, plane_bsize, plane, visit, arg);
        }
    }

    internal static CB_COEFF_BUFFER av1_get_cb_coeff_buffer(AV1_COMP cpi, int mi_row,
                                                            int mi_col)
    {
        AV1_COMMON cm = cpi.common;
        int mib_size_log2 = cm.seq_params.mib_size_log2;
        int offset = (mi_row >> mib_size_log2) * cpi.coeff_buffer_stride +
                     (mi_col >> mib_size_log2);
        return cpi.coeff_buffer_base[offset];
    }
}
