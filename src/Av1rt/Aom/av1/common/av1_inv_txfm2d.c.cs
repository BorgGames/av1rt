// Port of libaom av1/common/av1_inv_txfm2d.c

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class av1_inv_txfm2d
{
    // [subset] av1_highbd_iwht4x4_16_add_c / av1_highbd_iwht4x4_1_add_c: lossless only
    // (qindex 0), which the subset never reaches (rc_min_quantizer 10).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TxfmFunc inv_txfm_type_to_func(TXFM_TYPE txfm_type)
    {
        switch (txfm_type)
        {
            case TXFM_TYPE_DCT4: return av1_idct4;
            case TXFM_TYPE_DCT8: return av1_idct8;
            case TXFM_TYPE_DCT16: return av1_idct16;
            case TXFM_TYPE_DCT32: return av1_idct32;
            // [subset] TXFM_TYPE_DCT64 (av1_idct64): TX64 is disabled.
            case TXFM_TYPE_DCT64: throw new NotSupportedException("av1_idct64 (TX64)");
            case TXFM_TYPE_ADST4: return av1_iadst4;
            case TXFM_TYPE_ADST8: return av1_iadst8;
            case TXFM_TYPE_ADST16: return av1_iadst16;
            case TXFM_TYPE_IDENTITY4: return av1_iidentity4_c;
            case TXFM_TYPE_IDENTITY8: return av1_iidentity8_c;
            case TXFM_TYPE_IDENTITY16: return av1_iidentity16_c;
            case TXFM_TYPE_IDENTITY32: return av1_iidentity32_c;
            default: Debug.Assert(false); return null;
        }
    }

    private static readonly sbyte[] iadst4_range = { 0, 1, 0, 0, 0, 0, 0 };

    internal static void av1_get_inv_txfm_cfg(TX_TYPE tx_type, TX_SIZE tx_size,
                                              ref TXFM_2D_FLIP_CFG cfg)
    {
        cfg.tx_size = tx_size;
        cfg.stage_range_col = default;
        cfg.stage_range_row = default;
        set_flip_cfg(tx_type, ref cfg);
        TX_TYPE_1D tx_type_1d_col = vtx_tab[tx_type];
        TX_TYPE_1D tx_type_1d_row = htx_tab[tx_type];
        cfg.shift = av1_inv_txfm_shift_ls[tx_size];
        int txw_idx = get_txw_idx(tx_size);
        int txh_idx = get_txh_idx(tx_size);
        cfg.cos_bit_col = INV_COS_BIT;
        cfg.cos_bit_row = INV_COS_BIT;
        cfg.txfm_type_col = av1_txfm_type_ls[txh_idx][tx_type_1d_col];
        if (cfg.txfm_type_col == TXFM_TYPE_ADST4)
        {
            iadst4_range.AsSpan().CopyTo(cfg.stage_range_col);
        }
        cfg.txfm_type_row = av1_txfm_type_ls[txw_idx][tx_type_1d_row];
        if (cfg.txfm_type_row == TXFM_TYPE_ADST4)
        {
            iadst4_range.AsSpan().CopyTo(cfg.stage_range_row);
        }
        cfg.stage_num_col = av1_txfm_stage_num_list[cfg.txfm_type_col];
        cfg.stage_num_row = av1_txfm_stage_num_list[cfg.txfm_type_row];
    }

    internal static void av1_gen_inv_stage_range(Ptr<sbyte> stage_range_col,
                                                 Ptr<sbyte> stage_range_row,
                                                 in TXFM_2D_FLIP_CFG cfg, TX_SIZE tx_size,
                                                 int bd)
    {
        int fwd_shift = inv_start_range[tx_size];
        Ptr<sbyte> shift = cfg.shift;
        sbyte opt_range_row, opt_range_col;
        if (bd == 8)
        {
            opt_range_row = 16;
            opt_range_col = 16;
        }
        else if (bd == 10)
        {
            opt_range_row = 18;
            opt_range_col = 16;
        }
        else
        {
            Debug.Assert(bd == 12);
            opt_range_row = 20;
            opt_range_col = 18;
        }
        // i < MAX_TXFM_STAGE_NUM will mute above array bounds warning
        for (int i = 0; i < cfg.stage_num_row && i < MAX_TXFM_STAGE_NUM; ++i)
        {
            int real_range_row = cfg.stage_range_row[i] + fwd_shift + bd + 1;
            _ = real_range_row;
            if (cfg.txfm_type_row == TXFM_TYPE_ADST4 && i == 1)
            {
                // the adst4 may use 1 extra bit on top of opt_range_row at stage 1
                // so opt_range_row >= real_range_row will not hold
                stage_range_row[i] = opt_range_row;
            }
            else
            {
                Debug.Assert(opt_range_row >= real_range_row);
                stage_range_row[i] = opt_range_row;
            }
        }
        // i < MAX_TXFM_STAGE_NUM will mute above array bounds warning
        for (int i = 0; i < cfg.stage_num_col && i < MAX_TXFM_STAGE_NUM; ++i)
        {
            int real_range_col =
                cfg.stage_range_col[i] + fwd_shift + shift[0] + bd + 1;
            _ = real_range_col;
            if (cfg.txfm_type_col == TXFM_TYPE_ADST4 && i == 1)
            {
                // the adst4 may use 1 extra bit on top of opt_range_col at stage 1
                // so opt_range_col >= real_range_col will not hold
                stage_range_col[i] = opt_range_col;
            }
            else
            {
                Debug.Assert(opt_range_col >= real_range_col);
                stage_range_col[i] = opt_range_col;
            }
        }
    }

    // C: int8_t stage_range_row/col[MAX_TXFM_STAGE_NUM] locals of inv_txfm2d_add_c (leaf).
    [ThreadStatic] private static sbyte[] s_stage_range_row;
    [ThreadStatic] private static sbyte[] s_stage_range_col;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void inv_txfm2d_add_c(Ptr<int> input, Ptr<ushort> output, int stride,
                                         ref TXFM_2D_FLIP_CFG cfg, Ptr<int> txfm_buf,
                                         TX_SIZE tx_size, int bd)
    {
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
        sbyte[] stage_range_row = s_stage_range_row ??= new sbyte[MAX_TXFM_STAGE_NUM];
        sbyte[] stage_range_col = s_stage_range_col ??= new sbyte[MAX_TXFM_STAGE_NUM];
        Debug.Assert(cfg.stage_num_row <= MAX_TXFM_STAGE_NUM);
        Debug.Assert(cfg.stage_num_col <= MAX_TXFM_STAGE_NUM);
        av1_gen_inv_stage_range(stage_range_col, stage_range_row, in cfg, tx_size, bd);

        sbyte cos_bit_col = cfg.cos_bit_col;
        sbyte cos_bit_row = cfg.cos_bit_row;
        TxfmFunc txfm_func_col = inv_txfm_type_to_func(cfg.txfm_type_col);
        TxfmFunc txfm_func_row = inv_txfm_type_to_func(cfg.txfm_type_row);

        // txfm_buf's length is  txfm_size_row * txfm_size_col + 2 *
        // AOMMAX(txfm_size_row, txfm_size_col)
        // it is used for intermediate data buffering
        int buf_offset = AOMMAX(txfm_size_row, txfm_size_col);
        Ptr<int> temp_in = txfm_buf;
        Ptr<int> temp_out = temp_in + buf_offset;
        Ptr<int> buf = temp_out + buf_offset;
        Ptr<int> buf_ptr = buf;
        int c, r;

        // Rows
        for (r = 0; r < txfm_size_row; ++r)
        {
            if (Math.Abs(rect_type) == 1)
            {
                for (c = 0; c < txfm_size_col; ++c)
                {
                    temp_in[c] = round_shift(
                        (long)input[c * txfm_size_row + r] * NewInvSqrt2, NewSqrt2Bits);
                }
                clamp_buf(temp_in, txfm_size_col, (sbyte)(bd + 8));
                txfm_func_row(temp_in, buf_ptr, cos_bit_row, stage_range_row);
            }
            else
            {
                for (c = 0; c < txfm_size_col; ++c)
                {
                    temp_in[c] = input[c * txfm_size_row + r];
                }
                clamp_buf(temp_in, txfm_size_col, (sbyte)(bd + 8));
                txfm_func_row(temp_in, buf_ptr, cos_bit_row, stage_range_row);
            }
            av1_round_shift_array(buf_ptr, txfm_size_col, -shift[0]);
            buf_ptr += txfm_size_col;
        }

        // Columns
        for (c = 0; c < txfm_size_col; ++c)
        {
            if (cfg.lr_flip == 0)
            {
                for (r = 0; r < txfm_size_row; ++r)
                    temp_in[r] = buf[r * txfm_size_col + c];
            }
            else
            {
                // flip left right
                for (r = 0; r < txfm_size_row; ++r)
                    temp_in[r] = buf[r * txfm_size_col + (txfm_size_col - c - 1)];
            }
            clamp_buf(temp_in, txfm_size_row, (sbyte)AOMMAX(bd + 6, 16));
            txfm_func_col(temp_in, temp_out, cos_bit_col, stage_range_col);
            av1_round_shift_array(temp_out, txfm_size_row, -shift[1]);
            if (cfg.ud_flip == 0)
            {
                for (r = 0; r < txfm_size_row; ++r)
                {
                    output[r * stride + c] =
                        highbd_clip_pixel_add(output[r * stride + c], temp_out[r], bd);
                }
            }
            else
            {
                // flip upside down
                for (r = 0; r < txfm_size_row; ++r)
                {
                    output[r * stride + c] = highbd_clip_pixel_add(
                        output[r * stride + c], temp_out[txfm_size_row - r - 1], bd);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void inv_txfm2d_add_facade(Ptr<int> input, Ptr<ushort> output,
                                              int stride, Ptr<int> txfm_buf,
                                              TX_TYPE tx_type, TX_SIZE tx_size,
                                              int bd)
    {
        TXFM_2D_FLIP_CFG cfg = default;
        av1_get_inv_txfm_cfg(tx_type, tx_size, ref cfg);
        // Forward shift sum uses larger square size, to be consistent with what
        // av1_gen_inv_stage_range() does for inverse shifts.
        inv_txfm2d_add_c(input, output, stride, ref cfg, txfm_buf, tx_size, bd);
    }

    // [port] C: each av1_inv_txfm2d_add_WxH_c declares its own
    // DECLARE_ALIGNED(32, int, txfm_buf[W * H + 2 * max(W, H)]) on the stack. These are leaf
    // functions whose buffer is fully written before it is read, so one per-thread buffer of
    // the largest (32x32) size serves all of them.
    [ThreadStatic] private static int[] s_txfm_buf;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int[] get_txfm_buf() => s_txfm_buf ??= new int[32 * 32 + 32 + 32];

    // [port] 4x8, 8x16 and 16x32 never ran in coverage, but like their executed 8x4 / 16x8 /
    // 32x16 counterparts they follow from forced partitions at frame edges (data dependent).
    internal static void av1_inv_txfm2d_add_4x8_c(Ptr<int> input, Ptr<ushort> output,
                                                  int stride, TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [4 * 8 + 8 + 8]
        inv_txfm2d_add_facade(input, output, stride, txfm_buf, tx_type, TX_4X8, bd);
    }

    internal static void av1_inv_txfm2d_add_8x4_c(Ptr<int> input, Ptr<ushort> output,
                                                  int stride, TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [8 * 4 + 8 + 8]
        inv_txfm2d_add_facade(input, output, stride, txfm_buf, tx_type, TX_8X4, bd);
    }

    internal static void av1_inv_txfm2d_add_8x16_c(Ptr<int> input, Ptr<ushort> output,
                                                   int stride, TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [8 * 16 + 16 + 16]
        inv_txfm2d_add_facade(input, output, stride, txfm_buf, tx_type, TX_8X16, bd);
    }

    internal static void av1_inv_txfm2d_add_16x8_c(Ptr<int> input, Ptr<ushort> output,
                                                   int stride, TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [16 * 8 + 16 + 16]
        inv_txfm2d_add_facade(input, output, stride, txfm_buf, tx_type, TX_16X8, bd);
    }

    internal static void av1_inv_txfm2d_add_16x32_c(Ptr<int> input, Ptr<ushort> output,
                                                    int stride, TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [16 * 32 + 32 + 32]
        inv_txfm2d_add_facade(input, output, stride, txfm_buf, tx_type, TX_16X32, bd);
    }

    internal static void av1_inv_txfm2d_add_32x16_c(Ptr<int> input, Ptr<ushort> output,
                                                    int stride, TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [32 * 16 + 32 + 32]
        inv_txfm2d_add_facade(input, output, stride, txfm_buf, tx_type, TX_32X16, bd);
    }

    internal static void av1_inv_txfm2d_add_4x4_c(Ptr<int> input, Ptr<ushort> output,
                                                  int stride, TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [4 * 4 + 4 + 4]
        inv_txfm2d_add_facade(input, output, stride, txfm_buf, tx_type, TX_4X4, bd);
    }

    internal static void av1_inv_txfm2d_add_8x8_c(Ptr<int> input, Ptr<ushort> output,
                                                  int stride, TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [8 * 8 + 8 + 8]
        inv_txfm2d_add_facade(input, output, stride, txfm_buf, tx_type, TX_8X8, bd);
    }

    internal static void av1_inv_txfm2d_add_16x16_c(Ptr<int> input, Ptr<ushort> output,
                                                    int stride, TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [16 * 16 + 16 + 16]
        inv_txfm2d_add_facade(input, output, stride, txfm_buf, tx_type, TX_16X16, bd);
    }

    internal static void av1_inv_txfm2d_add_32x32_c(Ptr<int> input, Ptr<ushort> output,
                                                    int stride, TX_TYPE tx_type, int bd)
    {
        int[] txfm_buf = get_txfm_buf();  // [32 * 32 + 32 + 32]
        inv_txfm2d_add_facade(input, output, stride, txfm_buf, tx_type, TX_32X32, bd);
    }

    // [subset] av1_inv_txfm2d_add_64x64_c / 64x32 / 32x64 / 16x64 / 64x16: TX64 is disabled.
    // [subset] av1_inv_txfm2d_add_4x16_c / 16x4 / 8x32 / 32x8: 1:4 transforms only come from
    // PARTITION_HORZ_4/VERT_4 (no rectangular partitions in the subset); never executed.
}
