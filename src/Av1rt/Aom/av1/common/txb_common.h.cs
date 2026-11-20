// Port of libaom av1/common/txb_common.h

// [port] typedef aom_cdf_prob (*base_cdf_arr)[CDF_SIZE(4)]; and
// typedef aom_cdf_prob (*br_cdf_arr)[CDF_SIZE(BR_CDF_SIZE)]; point at rows of a FRAME_CONTEXT
// CDF table (e.g. coeff_base_cdf[txs_ctx][plane_type]), which are ushort[][] in the port.
global using base_cdf_arr = ushort[][];
global using br_cdf_arr = ushort[][];

global using static Av1rt.Aom.txb_common;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal partial struct TXB_CTX
{
    public int txb_skip_ctx;
    public int dc_sign_ctx;
}

internal static partial class txb_common
{
    // <gen:consts>
    internal const int NZ_MAP_CTX_0 = SIG_COEF_CONTEXTS_2D;
    internal const int NZ_MAP_CTX_5 = (NZ_MAP_CTX_0 + 5);
    internal const int NZ_MAP_CTX_10 = (NZ_MAP_CTX_0 + 10);
    internal const int MAX_TX_SIZE_UNIT = 16;

    // int base_level_count_to_index[13]
    internal static readonly int[] base_level_count_to_index = {
        0, 0, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3
    };

    // TX_CLASS tx_type_to_class[TX_TYPES]
    internal static readonly TX_CLASS[] tx_type_to_class = {
        TX_CLASS_2D, TX_CLASS_2D, TX_CLASS_2D, TX_CLASS_2D, TX_CLASS_2D, TX_CLASS_2D, TX_CLASS_2D,
        TX_CLASS_2D, TX_CLASS_2D, TX_CLASS_2D, TX_CLASS_VERT, TX_CLASS_HORIZ, TX_CLASS_VERT,
        TX_CLASS_HORIZ, TX_CLASS_VERT, TX_CLASS_HORIZ
    };

    // uint8_t clip_max3[256]
    private static readonly byte[] clip_max3 = {
        0, 1, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3
    };

    // int nz_map_ctx_offset_1d[32]
    private static readonly int[] nz_map_ctx_offset_1d = {
        NZ_MAP_CTX_0, NZ_MAP_CTX_5, NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10,
        NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10,
        NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10,
        NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10,
        NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10, NZ_MAP_CTX_10,
        NZ_MAP_CTX_10, NZ_MAP_CTX_10
    };

    // int8_t kTxbCtxSigns[3]
    private static readonly sbyte[] kTxbCtxSigns = {
        0, -1, 1
    };

    // int8_t kTxbCtxDcSignContexts[4 * MAX_TX_SIZE_UNIT + 1]
    private static readonly sbyte[] kTxbCtxDcSignContexts = {
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 0, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2
    };

    // uint8_t kTxbCtxSkipContexts[5][5]
    private static readonly byte[][] kTxbCtxSkipContexts = {
        new byte[] { 1, 2, 2, 2, 3 },
        new byte[] { 2, 4, 4, 4, 5 },
        new byte[] { 2, 4, 4, 4, 5 },
        new byte[] { 2, 4, 4, 4, 5 },
        new byte[] { 3, 5, 5, 5, 6 }
    };
    // </gen:consts>

    // [port] av1_eob_group_start, av1_eob_offset_bits, av1_nz_map_ctx_offset and
    // av1_txb_padded_idx are defined in txb_common.c (stubbed until it is ported).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_txb_bhl(TX_SIZE tx_size)
    {
        tx_size = av1_get_adjusted_tx_size(tx_size);
        return tx_size_high_log2[tx_size];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_txb_wide(TX_SIZE tx_size)
    {
        tx_size = av1_get_adjusted_tx_size(tx_size);
        return tx_size_wide[tx_size];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_txb_high(TX_SIZE tx_size)
    {
        tx_size = av1_get_adjusted_tx_size(tx_size);
        return tx_size_high[tx_size];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<byte> set_levels(Ptr<byte> levels_buf, int height)
    {
        return levels_buf + TX_PAD_TOP * (height + TX_PAD_HOR);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_padded_idx(int idx, int bhl)
    {
        return av1_txb_padded_idx[bhl - MIN_TX_SIZE_LOG2][idx];
    }

    // [subset] get_br_ctx_2d: never executed.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_br_ctx_eob(int c,  // raster order
                                       int bhl,
                                       TX_CLASS tx_class)
    {
        if (c == 0) return 0;
        int col = c >> bhl;
        int row = c - (col << bhl);
        if ((tx_class == TX_CLASS_2D && ((row | col) < 2)) ||
            (tx_class == TX_CLASS_HORIZ && col == 0) ||
            (tx_class == TX_CLASS_VERT && row == 0))
            return 7;
        return 14;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_br_ctx(Ptr<byte> levels,
                                   int c,  // raster order
                                   int bhl, TX_CLASS tx_class)
    {
        int col = c >> bhl;
        int row = c - (col << bhl);
        int stride = (1 << bhl) + TX_PAD_HOR;
        Ptr<byte> lv = levels + c + (col << TX_PAD_HOR_LOG2);
        Ptr<byte> lvs = lv + stride;
        int mag = lv[1] + lvs[0];
        switch (tx_class)
        {
            case TX_CLASS_2D:
                mag += lvs[1];
                mag = AOMMIN((mag + 1) >> 1, 6);
                if (c == 0) return mag;
                if ((row | col) < 2) return mag + 7;
                break;
            case TX_CLASS_HORIZ:
                mag += lvs[stride];
                mag = AOMMIN((mag + 1) >> 1, 6);
                if (c == 0) return mag;
                if (col == 0) return mag + 7;
                break;
            case TX_CLASS_VERT:
                mag += lv[2];
                mag = AOMMIN((mag + 1) >> 1, 6);
                if (c == 0) return mag;
                if (row == 0) return mag + 7;
                break;
            default: break;
        }

        return mag + 14;
    }

    // [subset] get_br_ctx_simple: never executed.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_nz_mag_stride(Ptr<byte> levels,
                                          int stride,
                                          TX_CLASS tx_class)
    {
        int mag;

        // Note: AOMMIN(level, 3) is useless for decoder since level < 3.
        Ptr<byte> lvs = levels + stride;
        mag = clip_max3[lvs[0]];      // { 1, 0 }
        mag += clip_max3[levels[1]];  // { 0, 1 }

        if (tx_class == TX_CLASS_2D)
        {
            mag += clip_max3[lvs[1]];       // { 1, 1 }
            mag += clip_max3[lvs[stride]];  // { 2, 0 }
            mag += clip_max3[levels[2]];    // { 0, 2 }
        }
        else if (tx_class == TX_CLASS_VERT)
        {
            mag += clip_max3[levels[2]];  // { 0, 2 }
            mag += clip_max3[levels[3]];  // { 0, 3 }
            mag += clip_max3[levels[4]];  // { 0, 4 }
        }
        else
        {
            Ptr<byte> lvs2 = lvs + stride;
            mag += clip_max3[lvs2[0]];           // { 2, 0 }
            mag += clip_max3[lvs2[stride]];      // { 3, 0 }
            mag += clip_max3[lvs2[stride * 2]];  // { 4, 0 }
        }

        return mag;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_nz_mag(Ptr<byte> levels,
                                   int bhl, TX_CLASS tx_class)
    {
        int stride = (1 << bhl) + TX_PAD_HOR;
        return get_nz_mag_stride(levels, stride, tx_class);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_nz_map_ctx_from_stats(
        int stats,
        int coeff_idx,  // raster order
        int bhl, TX_SIZE tx_size, TX_CLASS tx_class)
    {
        // tx_class == 0(TX_CLASS_2D)
        if ((tx_class | coeff_idx) == 0) return 0;
        int ctx = (stats + 1) >> 1;
        ctx = AOMMIN(ctx, 4);
        switch (tx_class)
        {
            case TX_CLASS_2D:
                {
                    // This is the algorithm to generate av1_nz_map_ctx_offset[][]
                    //   const int width = tx_size_wide[tx_size];
                    //   const int height = tx_size_high[tx_size];
                    //   if (width < height) {
                    //     if (row < 2) return 11 + ctx;
                    //   } else if (width > height) {
                    //     if (col < 2) return 16 + ctx;
                    //   }
                    //   if (row + col < 2) return ctx + 1;
                    //   if (row + col < 4) return 5 + ctx + 1;
                    //   return 21 + ctx;
                    return ctx + av1_nz_map_ctx_offset[tx_size][coeff_idx];
                }
            case TX_CLASS_HORIZ:
                {
                    int col = coeff_idx >> bhl;
                    return ctx + nz_map_ctx_offset_1d[col];
                }
            case TX_CLASS_VERT:
                {
                    int col = coeff_idx >> bhl;
                    int row = coeff_idx - (col << bhl);
                    return ctx + nz_map_ctx_offset_1d[row];
                }
            default: break;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_lower_levels_ctx_eob(int bhl, int width, int scan_idx)
    {
        if (scan_idx == 0) return 0;
        if (scan_idx <= ((width << bhl) >> 3)) return 1;
        if (scan_idx <= ((width << bhl) >> 2)) return 2;
        return 3;
    }

    // [subset] get_lower_levels_ctx_2d, get_lower_levels_ctx, get_lower_levels_ctx_fast,
    // get_lower_levels_ctx_simple_fast, get_lower_levels_ctx_simple,
    // get_lower_levels_ctx_general: never executed.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_dc_sign(ref int cul_level, int dc_val)
    {
        cul_level += (B(dc_val < 0) + (B(dc_val > 0) << 1)) << COEFF_CONTEXT_BITS;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte av1_get_txb_entropy_context(Ptr<tran_low_t> qcoeff,
                                                     SCAN_ORDER scan_order,
                                                     int eob)
    {
        if (eob == 0) return 0;
        int dc_val = qcoeff[0];
        int cul_level = Math.Abs(dc_val);
        if (cul_level < COEFF_CONTEXT_MASK && eob > 1)
        {
            Ptr<short> scan = scan_order.scan;
            int c = 1;
            for (; c + 3 < eob; c += 4)
            {
                cul_level += Math.Abs(qcoeff[scan[c]]) + Math.Abs(qcoeff[scan[c + 1]]) +
                             Math.Abs(qcoeff[scan[c + 2]]) + Math.Abs(qcoeff[scan[c + 3]]);
                if (cul_level >= COEFF_CONTEXT_MASK) break;
            }
            if (cul_level < COEFF_CONTEXT_MASK)
            {
                for (; c < eob; ++c)
                {
                    cul_level += Math.Abs(qcoeff[scan[c]]);
                }
            }
        }
        cul_level = AOMMIN(COEFF_CONTEXT_MASK, cul_level);
        set_dc_sign(ref cul_level, dc_val);
        return (byte)cul_level;
    }

    // PORT-NOTE: ENTROPY_CONTEXT is a char typedef in C and an int alias here (PORTING 3.2).
    // The values written into the contexts (cul_level with dc sign, at most
    // COEFF_CONTEXT_MASK + (2 << COEFF_CONTEXT_BITS) = 23) fit in a signed char, so reading
    // them back gives the same results as C.
    internal static void get_txb_ctx_general(BLOCK_SIZE plane_bsize,
                                             TX_SIZE tx_size, int plane,
                                             Ptr<ENTROPY_CONTEXT> a,
                                             Ptr<ENTROPY_CONTEXT> l,
                                             ref TXB_CTX txb_ctx)
    {
        int txb_w_unit = tx_size_wide_unit[tx_size];
        int txb_h_unit = tx_size_high_unit[tx_size];
        int dc_sign = 0;
        int k = 0;

        do
        {
            uint sign = (uint)((byte)a[k]) >> COEFF_CONTEXT_BITS;
            Debug.Assert(sign <= 2);
            dc_sign += kTxbCtxSigns[sign];
        } while (++k < txb_w_unit);

        k = 0;
        do
        {
            uint sign = (uint)((byte)l[k]) >> COEFF_CONTEXT_BITS;
            Debug.Assert(sign <= 2);
            dc_sign += kTxbCtxSigns[sign];
        } while (++k < txb_h_unit);

        txb_ctx.dc_sign_ctx = kTxbCtxDcSignContexts[dc_sign + 2 * MAX_TX_SIZE_UNIT];

        if (plane == 0)
        {
            if (plane_bsize == txsize_to_bsize[tx_size])
            {
                txb_ctx.txb_skip_ctx = 0;
            }
            else
            {
                // This is the algorithm to generate table skip_contexts[top][left].
                //    const int max = AOMMIN(top | left, 4);
                //    const int min = AOMMIN(AOMMIN(top, left), 4);
                //    if (!max)
                //      txb_skip_ctx = 1;
                //    else if (!min)
                //      txb_skip_ctx = 2 + (max > 3);
                //    else if (max <= 3)
                //      txb_skip_ctx = 4;
                //    else if (min <= 3)
                //      txb_skip_ctx = 5;
                //    else
                //      txb_skip_ctx = 6;
                int top = 0;
                int left = 0;

                k = 0;
                do
                {
                    top |= a[k];
                } while (++k < txb_w_unit);
                top &= COEFF_CONTEXT_MASK;
                top = AOMMIN(top, 4);

                k = 0;
                do
                {
                    left |= l[k];
                } while (++k < txb_h_unit);
                left &= COEFF_CONTEXT_MASK;
                left = AOMMIN(left, 4);

                txb_ctx.txb_skip_ctx = kTxbCtxSkipContexts[top][left];
            }
        }
        else
        {
            int ctx_base = get_entropy_context(tx_size, a, l);
            int ctx_offset = (num_pels_log2_lookup[plane_bsize] >
                              num_pels_log2_lookup[txsize_to_bsize[tx_size]])
                                 ? 10
                                 : 7;
            txb_ctx.txb_skip_ctx = ctx_base + ctx_offset;
        }
    }

    // [port] SPECIALIZE_GET_TXB_CTX(w, h) generates get_txb_ctx_<w>x<h>: the body of
    // get_txb_ctx_general with tx_size fixed to TX_<w>X<h>. The macro body is this method;
    // each specialization passes its constant tx_size.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SPECIALIZE_GET_TXB_CTX(TX_SIZE tx_size,
                                               BLOCK_SIZE plane_bsize, int plane,
                                               Ptr<ENTROPY_CONTEXT> a, Ptr<ENTROPY_CONTEXT> l,
                                               ref TXB_CTX txb_ctx)
    {
        int txb_w_unit = tx_size_wide_unit[tx_size];
        int txb_h_unit = tx_size_high_unit[tx_size];
        int dc_sign = 0;
        int k = 0;

        do
        {
            uint sign = (uint)((byte)a[k]) >> COEFF_CONTEXT_BITS;
            Debug.Assert(sign <= 2);
            dc_sign += kTxbCtxSigns[sign];
        } while (++k < txb_w_unit);

        k = 0;
        do
        {
            uint sign = (uint)((byte)l[k]) >> COEFF_CONTEXT_BITS;
            Debug.Assert(sign <= 2);
            dc_sign += kTxbCtxSigns[sign];
        } while (++k < txb_h_unit);

        txb_ctx.dc_sign_ctx =
            kTxbCtxDcSignContexts[dc_sign + 2 * MAX_TX_SIZE_UNIT];

        if (plane == 0)
        {
            if (plane_bsize == txsize_to_bsize[tx_size])
            {
                txb_ctx.txb_skip_ctx = 0;
            }
            else
            {
                int top = 0;
                int left = 0;

                k = 0;
                do
                {
                    top |= a[k];
                } while (++k < txb_w_unit);
                top &= COEFF_CONTEXT_MASK;
                top = AOMMIN(top, 4);

                k = 0;
                do
                {
                    left |= l[k];
                } while (++k < txb_h_unit);
                left &= COEFF_CONTEXT_MASK;
                left = AOMMIN(left, 4);

                txb_ctx.txb_skip_ctx = kTxbCtxSkipContexts[top][left];
            }
        }
        else
        {
            int ctx_base = get_entropy_context(tx_size, a, l);
            int ctx_offset = (num_pels_log2_lookup[plane_bsize] >
                              num_pels_log2_lookup[txsize_to_bsize[tx_size]])
                                 ? 10
                                 : 7;
            txb_ctx.txb_skip_ctx = ctx_base + ctx_offset;
        }
    }

    internal static void get_txb_ctx_4x4(BLOCK_SIZE plane_bsize, int plane,
                                         Ptr<ENTROPY_CONTEXT> a, Ptr<ENTROPY_CONTEXT> l,
                                         ref TXB_CTX txb_ctx) =>
        SPECIALIZE_GET_TXB_CTX(TX_4X4, plane_bsize, plane, a, l, ref txb_ctx);

    internal static void get_txb_ctx_8x8(BLOCK_SIZE plane_bsize, int plane,
                                         Ptr<ENTROPY_CONTEXT> a, Ptr<ENTROPY_CONTEXT> l,
                                         ref TXB_CTX txb_ctx) =>
        SPECIALIZE_GET_TXB_CTX(TX_8X8, plane_bsize, plane, a, l, ref txb_ctx);

    internal static void get_txb_ctx_16x16(BLOCK_SIZE plane_bsize, int plane,
                                           Ptr<ENTROPY_CONTEXT> a, Ptr<ENTROPY_CONTEXT> l,
                                           ref TXB_CTX txb_ctx) =>
        SPECIALIZE_GET_TXB_CTX(TX_16X16, plane_bsize, plane, a, l, ref txb_ctx);

    internal static void get_txb_ctx_32x32(BLOCK_SIZE plane_bsize, int plane,
                                           Ptr<ENTROPY_CONTEXT> a, Ptr<ENTROPY_CONTEXT> l,
                                           ref TXB_CTX txb_ctx) =>
        SPECIALIZE_GET_TXB_CTX(TX_32X32, plane_bsize, plane, a, l, ref txb_ctx);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void get_txb_ctx(BLOCK_SIZE plane_bsize,
                                     TX_SIZE tx_size, int plane,
                                     Ptr<ENTROPY_CONTEXT> a,
                                     Ptr<ENTROPY_CONTEXT> l,
                                     ref TXB_CTX txb_ctx)
    {
        switch (tx_size)
        {
            case TX_4X4: get_txb_ctx_4x4(plane_bsize, plane, a, l, ref txb_ctx); break;
            case TX_8X8: get_txb_ctx_8x8(plane_bsize, plane, a, l, ref txb_ctx); break;
            case TX_16X16: get_txb_ctx_16x16(plane_bsize, plane, a, l, ref txb_ctx); break;
            case TX_32X32: get_txb_ctx_32x32(plane_bsize, plane, a, l, ref txb_ctx); break;
            default:
                get_txb_ctx_general(plane_bsize, tx_size, plane, a, l, ref txb_ctx);
                break;
        }
    }
}
