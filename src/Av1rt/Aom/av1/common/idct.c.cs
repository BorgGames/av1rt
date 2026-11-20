// Port of libaom av1/common/idct.c
//
// [port] With CONFIG_AV1_HIGHBITDEPTH=0 the C build still reconstructs through the high
// bitdepth 2-D inverse transforms: av1_inv_txfm_add_c copies the 8-bit block into a uint16_t
// buffer, passes it as CONVERT_TO_BYTEPTR(tmp) and the highbd_* helpers turn it back with
// CONVERT_TO_SHORTPTR. That pointer tagging cannot be expressed safely, so the highbd helpers
// below take the uint16_t buffer directly as Ptr<ushort>.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class idct
{
    // NOTE: The implementation of all inverses need to be aware of the fact
    // that input and output could be the same buffer.

    // [subset] av1_highbd_iwht4x4_add: lossless only (see highbd_inv_txfm_add_4x4_c).

    private static void highbd_inv_txfm_add_4x4_c(Ptr<tran_low_t> input, Ptr<ushort> dest,
                                                  int stride, in TxfmParam txfm_param)
    {
        Debug.Assert(av1_ext_tx_used[txfm_param.tx_set_type][txfm_param.tx_type] != 0);
        int eob = txfm_param.eob;
        int bd = txfm_param.bd;
        int lossless = txfm_param.lossless;
        Ptr<int> src = cast_to_int32(input);
        TX_TYPE tx_type = txfm_param.tx_type;
        if (lossless != 0)
        {
            // [subset] av1_highbd_iwht4x4_add: lossless needs qindex 0, which the subset never
            // reaches (rc_min_quantizer 10; cyclic refresh deltas stay above 0).
            _ = eob;
            throw new NotSupportedException("lossless (WHT) inverse transform");
        }

        av1_inv_txfm2d_add_4x4_c(src, dest, stride, tx_type, bd);
    }

    // [port] 4x8, 8x16 and 16x32 never ran in coverage, but like their executed 8x4 / 16x8 /
    // 32x16 counterparts they follow from forced partitions at frame edges (data dependent).
    private static void highbd_inv_txfm_add_4x8_c(Ptr<tran_low_t> input, Ptr<ushort> dest,
                                                  int stride, in TxfmParam txfm_param)
    {
        Debug.Assert(av1_ext_tx_used[txfm_param.tx_set_type][txfm_param.tx_type] != 0);
        Ptr<int> src = cast_to_int32(input);
        av1_inv_txfm2d_add_4x8_c(src, dest, stride,
                                 txfm_param.tx_type, txfm_param.bd);
    }

    private static void highbd_inv_txfm_add_8x4_c(Ptr<tran_low_t> input, Ptr<ushort> dest,
                                                  int stride, in TxfmParam txfm_param)
    {
        Debug.Assert(av1_ext_tx_used[txfm_param.tx_set_type][txfm_param.tx_type] != 0);
        Ptr<int> src = cast_to_int32(input);
        av1_inv_txfm2d_add_8x4_c(src, dest, stride,
                                 txfm_param.tx_type, txfm_param.bd);
    }

    private static void highbd_inv_txfm_add_16x32_c(Ptr<tran_low_t> input, Ptr<ushort> dest,
                                                    int stride,
                                                    in TxfmParam txfm_param)
    {
        Ptr<int> src = cast_to_int32(input);
        av1_inv_txfm2d_add_16x32_c(src, dest, stride,
                                   txfm_param.tx_type, txfm_param.bd);
    }

    private static void highbd_inv_txfm_add_32x16_c(Ptr<tran_low_t> input, Ptr<ushort> dest,
                                                    int stride,
                                                    in TxfmParam txfm_param)
    {
        Ptr<int> src = cast_to_int32(input);
        av1_inv_txfm2d_add_32x16_c(src, dest, stride,
                                   txfm_param.tx_type, txfm_param.bd);
    }

    // [subset] highbd_inv_txfm_add_16x4_c / 4x16 / 32x8 / 8x32: 1:4 transforms only come from
    // PARTITION_HORZ_4/VERT_4 (no rectangular partitions in the subset).
    // [subset] highbd_inv_txfm_add_32x64_c / 64x32 / 16x64 / 64x16 / 64x64: TX64 is disabled.

    private static void highbd_inv_txfm_add_8x8_c(Ptr<tran_low_t> input, Ptr<ushort> dest,
                                                  int stride, in TxfmParam txfm_param)
    {
        int bd = txfm_param.bd;
        TX_TYPE tx_type = txfm_param.tx_type;
        Ptr<int> src = cast_to_int32(input);

        av1_inv_txfm2d_add_8x8_c(src, dest, stride, tx_type, bd);
    }

    private static void highbd_inv_txfm_add_16x16_c(Ptr<tran_low_t> input, Ptr<ushort> dest,
                                                    int stride,
                                                    in TxfmParam txfm_param)
    {
        int bd = txfm_param.bd;
        TX_TYPE tx_type = txfm_param.tx_type;
        Ptr<int> src = cast_to_int32(input);

        av1_inv_txfm2d_add_16x16_c(src, dest, stride, tx_type,
                                   bd);
    }

    private static void highbd_inv_txfm_add_8x16_c(Ptr<tran_low_t> input, Ptr<ushort> dest,
                                                   int stride,
                                                   in TxfmParam txfm_param)
    {
        Ptr<int> src = cast_to_int32(input);
        av1_inv_txfm2d_add_8x16_c(src, dest, stride,
                                  txfm_param.tx_type, txfm_param.bd);
    }

    private static void highbd_inv_txfm_add_16x8_c(Ptr<tran_low_t> input, Ptr<ushort> dest,
                                                   int stride,
                                                   in TxfmParam txfm_param)
    {
        Ptr<int> src = cast_to_int32(input);
        av1_inv_txfm2d_add_16x8_c(src, dest, stride,
                                  txfm_param.tx_type, txfm_param.bd);
    }

    private static void highbd_inv_txfm_add_32x32_c(Ptr<tran_low_t> input, Ptr<ushort> dest,
                                                    int stride,
                                                    in TxfmParam txfm_param)
    {
        int bd = txfm_param.bd;
        TX_TYPE tx_type = txfm_param.tx_type;
        Ptr<int> src = cast_to_int32(input);

        av1_inv_txfm2d_add_32x32_c(src, dest, stride, tx_type,
                                   bd);
    }

    // PORT-NOTE: C takes `uint8_t *dest` holding CONVERT_TO_BYTEPTR(uint16_t buffer); here it
    // is the uint16_t buffer itself (see the note at the top of the file).
    internal static void av1_highbd_inv_txfm_add_c(Ptr<tran_low_t> input, Ptr<ushort> dest,
                                                   int stride, in TxfmParam txfm_param)
    {
        Debug.Assert(av1_ext_tx_used[txfm_param.tx_set_type][txfm_param.tx_type] != 0);
        TX_SIZE tx_size = txfm_param.tx_size;
        switch (tx_size)
        {
            case TX_32X32:
                highbd_inv_txfm_add_32x32_c(input, dest, stride, in txfm_param);
                break;
            case TX_16X16:
                highbd_inv_txfm_add_16x16_c(input, dest, stride, in txfm_param);
                break;
            case TX_8X8:
                highbd_inv_txfm_add_8x8_c(input, dest, stride, in txfm_param);
                break;
            case TX_4X8:
                highbd_inv_txfm_add_4x8_c(input, dest, stride, in txfm_param);
                break;
            case TX_8X4:
                highbd_inv_txfm_add_8x4_c(input, dest, stride, in txfm_param);
                break;
            case TX_8X16:
                highbd_inv_txfm_add_8x16_c(input, dest, stride, in txfm_param);
                break;
            case TX_16X8:
                highbd_inv_txfm_add_16x8_c(input, dest, stride, in txfm_param);
                break;
            case TX_16X32:
                highbd_inv_txfm_add_16x32_c(input, dest, stride, in txfm_param);
                break;
            case TX_32X16:
                highbd_inv_txfm_add_32x16_c(input, dest, stride, in txfm_param);
                break;
            case TX_64X64:
            case TX_32X64:
            case TX_64X32:
            case TX_16X64:
            case TX_64X16:
                // [subset] TX64 is disabled.
                throw new NotSupportedException("64-point inverse transform (TX64)");
            case TX_4X4:
                // this is like av1_short_idct4x4 but has a special case around eob<=1
                // which is significant (not just an optimization) for the lossless
                // case.
                highbd_inv_txfm_add_4x4_c(input, dest, stride, in txfm_param);
                break;
            case TX_16X4:
            case TX_4X16:
            case TX_8X32:
            case TX_32X8:
                // [subset] 1:4 transforms (no PARTITION_HORZ_4/VERT_4 in the subset).
                throw new NotSupportedException("1:4 inverse transform");
            default: Debug.Assert(false, "Invalid transform size"); break;
        }
    }

    // av1_rtcd.h dispatched name.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_highbd_inv_txfm_add(Ptr<tran_low_t> input, Ptr<ushort> dest,
                                                 int stride, in TxfmParam txfm_param) =>
        av1_highbd_inv_txfm_add_c(input, dest, stride, in txfm_param);

    // C: DECLARE_ALIGNED(32, uint16_t, tmp[MAX_TX_SQUARE]) local of av1_inv_txfm_add_c (leaf).
    [ThreadStatic] private static ushort[] s_tmp;

    internal static void av1_inv_txfm_add_c(Ptr<tran_low_t> dqcoeff, Ptr<byte> dst, int stride,
                                            in TxfmParam txfm_param)
    {
        TX_SIZE tx_size = txfm_param.tx_size;
        ushort[] tmp = s_tmp ??= new ushort[MAX_TX_SQUARE];
        int tmp_stride = MAX_TX_SIZE;
        int w = tx_size_wide[tx_size];
        int h = tx_size_high[tx_size];
        for (int r = 0; r < h; ++r)
        {
            for (int c = 0; c < w; ++c)
            {
                tmp[r * tmp_stride + c] = dst[r * stride + c];
            }
        }

        av1_highbd_inv_txfm_add(dqcoeff, tmp, tmp_stride,
                                in txfm_param);

        for (int r = 0; r < h; ++r)
        {
            for (int c = 0; c < w; ++c)
            {
                dst[r * stride + c] = (byte)tmp[r * tmp_stride + c];
            }
        }
    }

    // av1_rtcd.h dispatched name.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_inv_txfm_add(Ptr<tran_low_t> dqcoeff, Ptr<byte> dst, int stride,
                                          in TxfmParam txfm_param)
    {
        if (Simd.UseAvx2) av1_inv_txfm_add_avx2(dqcoeff, dst, stride, in txfm_param);
        else av1_inv_txfm_add_c(dqcoeff, dst, stride, in txfm_param);
    }
}
