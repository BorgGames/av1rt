// Port of libaom av1/encoder/av1_fwd_txfm2d.c
global using static Av1rt.Aom.av1_fwd_txfm2d;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class av1_fwd_txfm2d
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TxfmFunc fwd_txfm_type_to_func(TXFM_TYPE txfm_type)
    {
        switch (txfm_type)
        {
            case TXFM_TYPE_DCT4: return av1_fdct4;
            case TXFM_TYPE_DCT8: return av1_fdct8;
            case TXFM_TYPE_DCT16: return av1_fdct16;
            case TXFM_TYPE_DCT32: return av1_fdct32;
            // [subset] TXFM_TYPE_DCT64 (av1_fdct64): TX64 is disabled.
            case TXFM_TYPE_DCT64: throw new NotSupportedException("av1_fdct64 (TX64)");
            case TXFM_TYPE_ADST4: return av1_fadst4;
            case TXFM_TYPE_ADST8: return av1_fadst8;
            case TXFM_TYPE_ADST16: return av1_fadst16;
            case TXFM_TYPE_IDENTITY4: return av1_fidentity4_c;
            case TXFM_TYPE_IDENTITY8: return av1_fidentity8_c;
            case TXFM_TYPE_IDENTITY16: return av1_fidentity16_c;
            case TXFM_TYPE_IDENTITY32: return av1_fidentity32_c;
            default: Debug.Assert(false); return null;
        }
    }

    internal static void av1_gen_fwd_stage_range(Ptr<sbyte> stage_range_col,
                                                 Ptr<sbyte> stage_range_row,
                                                 in TXFM_2D_FLIP_CFG cfg, int bd)
    {
        // Take the shift from the larger dimension in the rectangular case.
        Ptr<sbyte> shift = cfg.shift;
        // i < MAX_TXFM_STAGE_NUM will mute above array bounds warning
        for (int i = 0; i < cfg.stage_num_col && i < MAX_TXFM_STAGE_NUM; ++i)
        {
            stage_range_col[i] = (sbyte)(cfg.stage_range_col[i] + shift[0] + bd + 1);
        }

        // i < MAX_TXFM_STAGE_NUM will mute above array bounds warning
        for (int i = 0; i < cfg.stage_num_row && i < MAX_TXFM_STAGE_NUM; ++i)
        {
            stage_range_row[i] =
                (sbyte)(cfg.stage_range_row[i] + shift[0] + shift[1] + bd + 1);
        }
    }

    // C: int8_t stage_range_col/row[MAX_TXFM_STAGE_NUM] and
    // DECLARE_ALIGNED(16, int32_t, row_buffer[MAX_TX_SIZE]) locals of fwd_txfm2d_c (leaf).
    [ThreadStatic] private static sbyte[] s_stage_range_col;
    [ThreadStatic] private static sbyte[] s_stage_range_row;
    [ThreadStatic] private static int[] s_row_buffer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fwd_txfm2d_c(Ptr<short> input, Ptr<int> output, int stride,
                                     in TXFM_2D_FLIP_CFG cfg, Ptr<int> buf, int bd)
    {
        int c, r;
        // Note when assigning txfm_size_col, we use the txfm_size from the
        // row configuration and vice versa. This is intentionally done to
        // accurately perform rectangular transforms. When the transform is
        // rectangular, the number of columns will be the same as the
        // txfm_size stored in the row cfg struct. It will make no difference
        // for square transforms.
        int txfm_size_col = tx_size_wide[cfg.tx_size];
        int txfm_size_row = tx_size_high[cfg.tx_size];
        // Take the shift from the larger dimension in the rectangular case.
        Ptr<sbyte> shift = cfg.shift;
        int rect_type = get_rect_tx_log_ratio(txfm_size_col, txfm_size_row);
        sbyte[] stage_range_col = s_stage_range_col ??= new sbyte[MAX_TXFM_STAGE_NUM];
        sbyte[] stage_range_row = s_stage_range_row ??= new sbyte[MAX_TXFM_STAGE_NUM];
        Debug.Assert(cfg.stage_num_col <= MAX_TXFM_STAGE_NUM);
        Debug.Assert(cfg.stage_num_row <= MAX_TXFM_STAGE_NUM);
        av1_gen_fwd_stage_range(stage_range_col, stage_range_row, in cfg, bd);

        sbyte cos_bit_col = cfg.cos_bit_col;
        sbyte cos_bit_row = cfg.cos_bit_row;
        TxfmFunc txfm_func_col = fwd_txfm_type_to_func(cfg.txfm_type_col);
        TxfmFunc txfm_func_row = fwd_txfm_type_to_func(cfg.txfm_type_row);

        // use output buffer as temp buffer
        Ptr<int> temp_in = output;
        Ptr<int> temp_out = output + txfm_size_row;

        // Columns
        for (c = 0; c < txfm_size_col; ++c)
        {
            if (cfg.ud_flip == 0)
            {
                for (r = 0; r < txfm_size_row; ++r) temp_in[r] = input[r * stride + c];
            }
            else
            {
                for (r = 0; r < txfm_size_row; ++r)
                    // flip upside down
                    temp_in[r] = input[(txfm_size_row - r - 1) * stride + c];
            }
            av1_round_shift_array(temp_in, txfm_size_row, -shift[0]);
            txfm_func_col(temp_in, temp_out, cos_bit_col, stage_range_col);
            av1_round_shift_array(temp_out, txfm_size_row, -shift[1]);
            if (cfg.lr_flip == 0)
            {
                for (r = 0; r < txfm_size_row; ++r)
                    buf[r * txfm_size_col + c] = temp_out[r];
            }
            else
            {
                for (r = 0; r < txfm_size_row; ++r)
                    // flip from left to right
                    buf[r * txfm_size_col + (txfm_size_col - c - 1)] = temp_out[r];
            }
        }

        int[] row_buffer = s_row_buffer ??= new int[MAX_TX_SIZE];

        // Rows
        for (r = 0; r < txfm_size_row; ++r)
        {
            txfm_func_row(buf + r * txfm_size_col, row_buffer, cos_bit_row,
                          stage_range_row);
            av1_round_shift_array(row_buffer, txfm_size_col, -shift[2]);
            if (Math.Abs(rect_type) == 1)
            {
                // Multiply everything by Sqrt2 if the transform is rectangular and the
                // size difference is a factor of 2.
                for (c = 0; c < txfm_size_col; ++c)
                {
                    row_buffer[c] =
                        round_shift((long)row_buffer[c] * NewSqrt2, NewSqrt2Bits);
                }
            }
            for (c = 0; c < txfm_size_col; ++c)
            {
                output[c * txfm_size_row + r] = row_buffer[c];
            }
        }
    }

    // [port] C: each av1_fwd_txfm2d_WxH_c declares its own int32_t txfm_buf[W * H] on the
    // stack. These are leaf functions whose buffer is fully written before it is read, so one
    // per-thread buffer of the largest (32x32) size serves all of them.
    [ThreadStatic] private static int[] s_txfm_buf;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int[] get_txfm_buf() => s_txfm_buf ??= new int[32 * 32];

    // [port] 4x8, 8x16 and 16x32 never ran in coverage, but like their executed 8x4 / 16x8 /
    // 32x16 counterparts they follow from forced partitions at frame edges (data dependent).
    internal static void av1_fwd_txfm2d_4x8_c(Ptr<short> input, Ptr<int> output, int stride,
                                              TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [4 * 8]
        TXFM_2D_FLIP_CFG cfg = default;
        av1_get_fwd_txfm_cfg(tx_type, TX_4X8, ref cfg);
        fwd_txfm2d_c(input, output, stride, in cfg, txfm_buf, bd);
    }

    internal static void av1_fwd_txfm2d_8x4_c(Ptr<short> input, Ptr<int> output, int stride,
                                              TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [8 * 4]
        TXFM_2D_FLIP_CFG cfg = default;
        av1_get_fwd_txfm_cfg(tx_type, TX_8X4, ref cfg);
        fwd_txfm2d_c(input, output, stride, in cfg, txfm_buf, bd);
    }

    internal static void av1_fwd_txfm2d_8x16_c(Ptr<short> input, Ptr<int> output, int stride,
                                               TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [8 * 16]
        TXFM_2D_FLIP_CFG cfg = default;
        av1_get_fwd_txfm_cfg(tx_type, TX_8X16, ref cfg);
        fwd_txfm2d_c(input, output, stride, in cfg, txfm_buf, bd);
    }

    internal static void av1_fwd_txfm2d_16x8_c(Ptr<short> input, Ptr<int> output, int stride,
                                               TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [16 * 8]
        TXFM_2D_FLIP_CFG cfg = default;
        av1_get_fwd_txfm_cfg(tx_type, TX_16X8, ref cfg);
        fwd_txfm2d_c(input, output, stride, in cfg, txfm_buf, bd);
    }

    internal static void av1_fwd_txfm2d_16x32_c(Ptr<short> input, Ptr<int> output, int stride,
                                                TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [16 * 32]
        TXFM_2D_FLIP_CFG cfg = default;
        av1_get_fwd_txfm_cfg(tx_type, TX_16X32, ref cfg);
        fwd_txfm2d_c(input, output, stride, in cfg, txfm_buf, bd);
    }

    internal static void av1_fwd_txfm2d_32x16_c(Ptr<short> input, Ptr<int> output, int stride,
                                                TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [32 * 16]
        TXFM_2D_FLIP_CFG cfg = default;
        av1_get_fwd_txfm_cfg(tx_type, TX_32X16, ref cfg);
        fwd_txfm2d_c(input, output, stride, in cfg, txfm_buf, bd);
    }

    // [subset] av1_fwd_txfm2d_4x16_c / 8x32_c / 32x8_c / 16x64_c / 64x16_c are compiled out
    // (!CONFIG_REALTIME_ONLY). av1_fwd_txfm2d_16x4_c is compiled but unreachable: the realtime
    // build of av1_highbd_fwd_txfm has no TX_16X4 case.

    internal static void av1_fwd_txfm2d_4x4_c(Ptr<short> input, Ptr<int> output, int stride,
                                              TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [4 * 4]
        TXFM_2D_FLIP_CFG cfg = default;
        av1_get_fwd_txfm_cfg(tx_type, TX_4X4, ref cfg);
        fwd_txfm2d_c(input, output, stride, in cfg, txfm_buf, bd);
    }

    internal static void av1_fwd_txfm2d_8x8_c(Ptr<short> input, Ptr<int> output, int stride,
                                              TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [8 * 8]
        TXFM_2D_FLIP_CFG cfg = default;
        av1_get_fwd_txfm_cfg(tx_type, TX_8X8, ref cfg);
        fwd_txfm2d_c(input, output, stride, in cfg, txfm_buf, bd);
    }

    internal static void av1_fwd_txfm2d_16x16_c(Ptr<short> input, Ptr<int> output, int stride,
                                                TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [16 * 16]
        TXFM_2D_FLIP_CFG cfg = default;
        av1_get_fwd_txfm_cfg(tx_type, TX_16X16, ref cfg);
        fwd_txfm2d_c(input, output, stride, in cfg, txfm_buf, bd);
    }

    internal static void av1_fwd_txfm2d_32x32_c(Ptr<short> input, Ptr<int> output, int stride,
                                                TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [32 * 32]
        TXFM_2D_FLIP_CFG cfg = default;
        av1_get_fwd_txfm_cfg(tx_type, TX_32X32, ref cfg);
        fwd_txfm2d_c(input, output, stride, in cfg, txfm_buf, bd);
    }

    // [subset] av1_fwd_txfm2d_64x64_c / 32x64_c / 64x32_c: TX64 is disabled.

    // av1_rtcd.h dispatched names.
    internal static void av1_fwd_txfm2d_4x8(Ptr<short> input, Ptr<int> output, int stride,
                                            TX_TYPE tx_type, int bd) =>
        av1_fwd_txfm2d_4x8_c(input, output, stride, tx_type, bd);
    internal static void av1_fwd_txfm2d_8x4(Ptr<short> input, Ptr<int> output, int stride,
                                            TX_TYPE tx_type, int bd) =>
        av1_fwd_txfm2d_8x4_c(input, output, stride, tx_type, bd);
    internal static void av1_fwd_txfm2d_8x16(Ptr<short> input, Ptr<int> output, int stride,
                                             TX_TYPE tx_type, int bd) =>
        av1_fwd_txfm2d_8x16_c(input, output, stride, tx_type, bd);
    internal static void av1_fwd_txfm2d_16x8(Ptr<short> input, Ptr<int> output, int stride,
                                             TX_TYPE tx_type, int bd) =>
        av1_fwd_txfm2d_16x8_c(input, output, stride, tx_type, bd);
    internal static void av1_fwd_txfm2d_16x32(Ptr<short> input, Ptr<int> output, int stride,
                                              TX_TYPE tx_type, int bd) =>
        av1_fwd_txfm2d_16x32_c(input, output, stride, tx_type, bd);
    internal static void av1_fwd_txfm2d_32x16(Ptr<short> input, Ptr<int> output, int stride,
                                              TX_TYPE tx_type, int bd) =>
        av1_fwd_txfm2d_32x16_c(input, output, stride, tx_type, bd);
    internal static void av1_fwd_txfm2d_4x4(Ptr<short> input, Ptr<int> output, int stride,
                                            TX_TYPE tx_type, int bd) =>
        av1_fwd_txfm2d_4x4_c(input, output, stride, tx_type, bd);
    internal static void av1_fwd_txfm2d_8x8(Ptr<short> input, Ptr<int> output, int stride,
                                            TX_TYPE tx_type, int bd) =>
        av1_fwd_txfm2d_8x8_c(input, output, stride, tx_type, bd);
    internal static void av1_fwd_txfm2d_16x16(Ptr<short> input, Ptr<int> output, int stride,
                                              TX_TYPE tx_type, int bd) =>
        av1_fwd_txfm2d_16x16_c(input, output, stride, tx_type, bd);
    internal static void av1_fwd_txfm2d_32x32(Ptr<short> input, Ptr<int> output, int stride,
                                              TX_TYPE tx_type, int bd) =>
        av1_fwd_txfm2d_32x32_c(input, output, stride, tx_type, bd);

    private static readonly sbyte[] fdct4_range_mult2 = { 0, 2, 3, 3 };
    private static readonly sbyte[] fdct8_range_mult2 = { 0, 2, 4, 5, 5, 5 };
    private static readonly sbyte[] fdct16_range_mult2 = { 0, 2, 4, 6, 7, 7, 7, 7 };
    private static readonly sbyte[] fdct32_range_mult2 = { 0, 2, 4, 6, 8, 9, 9, 9, 9, 9 };
    private static readonly sbyte[] fdct64_range_mult2 = { 0,  2,  4,  6,  8,  10,
                                                           11, 11, 11, 11, 11, 11 };

    private static readonly sbyte[] fadst4_range_mult2 = { 0, 2, 4, 3, 3, 3, 3 };
    private static readonly sbyte[] fadst8_range_mult2 = { 0, 0, 1, 3, 3, 5, 5, 5 };
    private static readonly sbyte[] fadst16_range_mult2 = { 0, 0, 1, 3, 3, 5, 5, 7, 7, 7 };

    private static readonly sbyte[] fidtx4_range_mult2 = { 1 };
    private static readonly sbyte[] fidtx8_range_mult2 = { 2 };
    private static readonly sbyte[] fidtx16_range_mult2 = { 3 };
    private static readonly sbyte[] fidtx32_range_mult2 = { 4 };

    private static readonly sbyte[][] fwd_txfm_range_mult2_list = {
        fdct4_range_mult2,  fdct8_range_mult2,   fdct16_range_mult2,
        fdct32_range_mult2, fdct64_range_mult2,  fadst4_range_mult2,
        fadst8_range_mult2, fadst16_range_mult2, fidtx4_range_mult2,
        fidtx8_range_mult2, fidtx16_range_mult2, fidtx32_range_mult2
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_fwd_txfm_non_scale_range(ref TXFM_2D_FLIP_CFG cfg)
    {
        cfg.stage_range_col = default;
        cfg.stage_range_row = default;

        sbyte[] range_mult2_col =
            fwd_txfm_range_mult2_list[cfg.txfm_type_col];
        int stage_num_col = cfg.stage_num_col;
        // i < MAX_TXFM_STAGE_NUM will quiet -Wstringop-overflow.
        for (int i = 0; i < stage_num_col && i < MAX_TXFM_STAGE_NUM; ++i)
            cfg.stage_range_col[i] = (sbyte)((range_mult2_col[i] + 1) >> 1);

        sbyte[] range_mult2_row =
            fwd_txfm_range_mult2_list[cfg.txfm_type_row];
        int stage_num_row = cfg.stage_num_row;
        // i < MAX_TXFM_STAGE_NUM will quiet -Wstringop-overflow.
        for (int i = 0; i < stage_num_row && i < MAX_TXFM_STAGE_NUM; ++i)
        {
            cfg.stage_range_row[i] =
                (sbyte)((range_mult2_col[stage_num_col - 1] + range_mult2_row[i] + 1) >> 1);
        }
    }

    internal static void av1_get_fwd_txfm_cfg(TX_TYPE tx_type, TX_SIZE tx_size,
                                              ref TXFM_2D_FLIP_CFG cfg)
    {
        cfg.tx_size = tx_size;
        set_flip_cfg(tx_type, ref cfg);
        TX_TYPE_1D tx_type_1d_col = vtx_tab[tx_type];
        TX_TYPE_1D tx_type_1d_row = htx_tab[tx_type];
        int txw_idx = get_txw_idx(tx_size);
        int txh_idx = get_txh_idx(tx_size);
        cfg.shift = av1_fwd_txfm_shift_ls[tx_size];
        cfg.cos_bit_col = av1_fwd_cos_bit_col[txw_idx][txh_idx];
        cfg.cos_bit_row = av1_fwd_cos_bit_row[txw_idx][txh_idx];
        cfg.txfm_type_col = av1_txfm_type_ls[txh_idx][tx_type_1d_col];
        Debug.Assert(cfg.txfm_type_col != TXFM_TYPE_INVALID);
        cfg.txfm_type_row = av1_txfm_type_ls[txw_idx][tx_type_1d_row];
        Debug.Assert(cfg.txfm_type_row != TXFM_TYPE_INVALID);
        cfg.stage_num_col = av1_txfm_stage_num_list[cfg.txfm_type_col];
        cfg.stage_num_row = av1_txfm_stage_num_list[cfg.txfm_type_row];
        set_fwd_txfm_non_scale_range(ref cfg);
    }
}
