// Port of libaom av1/encoder/hybrid_fwd_txfm.c
//
// [port] With CONFIG_AV1_HIGHBITDEPTH=0 the C build still runs the forward transform through
// the highbd_fwd_txfm_* helpers (av1_lowbd_fwd_txfm_c forwards to av1_highbd_fwd_txfm); they
// are ported as executed.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class hybrid_fwd_txfm
{
    // [subset] av1_fwht4x4_c: lossless only (qindex 0), never reached in the subset.

    private static void highbd_fwd_txfm_4x4(Ptr<short> src_diff, Ptr<tran_low_t> coeff,
                                            int diff_stride, ref TxfmParam txfm_param)
    {
        Ptr<int> dst_coeff = coeff;
        TX_TYPE tx_type = txfm_param.tx_type;
        int bd = txfm_param.bd;
        if (txfm_param.lossless != 0)
        {
            // [subset] av1_fwht4x4: lossless needs qindex 0, which the subset never reaches
            // (rc_min_quantizer 10; cyclic refresh deltas stay above 0).
            throw new NotSupportedException("lossless (WHT) forward transform");
        }
        av1_fwd_txfm2d_4x4(src_diff, dst_coeff, diff_stride, tx_type, bd);
    }

    // [port] 4x8, 8x16 and 16x32 never ran in coverage, but like their executed 8x4 / 16x8 /
    // 32x16 counterparts they follow from forced partitions at frame edges (data dependent).
    private static void highbd_fwd_txfm_4x8(Ptr<short> src_diff, Ptr<tran_low_t> coeff,
                                            int diff_stride, ref TxfmParam txfm_param)
    {
        Ptr<int> dst_coeff = coeff;
        av1_fwd_txfm2d_4x8(src_diff, dst_coeff, diff_stride, txfm_param.tx_type,
                           txfm_param.bd);
    }

    private static void highbd_fwd_txfm_8x4(Ptr<short> src_diff, Ptr<tran_low_t> coeff,
                                            int diff_stride, ref TxfmParam txfm_param)
    {
        Ptr<int> dst_coeff = coeff;
        av1_fwd_txfm2d_8x4(src_diff, dst_coeff, diff_stride, txfm_param.tx_type,
                           txfm_param.bd);
    }

    private static void highbd_fwd_txfm_8x16(Ptr<short> src_diff, Ptr<tran_low_t> coeff,
                                             int diff_stride, ref TxfmParam txfm_param)
    {
        Ptr<int> dst_coeff = coeff;
        TX_TYPE tx_type = txfm_param.tx_type;
        int bd = txfm_param.bd;
        av1_fwd_txfm2d_8x16(src_diff, dst_coeff, diff_stride, tx_type, bd);
    }

    private static void highbd_fwd_txfm_16x8(Ptr<short> src_diff, Ptr<tran_low_t> coeff,
                                             int diff_stride, ref TxfmParam txfm_param)
    {
        Ptr<int> dst_coeff = coeff;
        TX_TYPE tx_type = txfm_param.tx_type;
        int bd = txfm_param.bd;
        av1_fwd_txfm2d_16x8(src_diff, dst_coeff, diff_stride, tx_type, bd);
    }

    private static void highbd_fwd_txfm_16x32(Ptr<short> src_diff, Ptr<tran_low_t> coeff,
                                              int diff_stride, ref TxfmParam txfm_param)
    {
        Ptr<int> dst_coeff = coeff;
        av1_fwd_txfm2d_16x32(src_diff, dst_coeff, diff_stride, txfm_param.tx_type,
                             txfm_param.bd);
    }

    private static void highbd_fwd_txfm_32x16(Ptr<short> src_diff, Ptr<tran_low_t> coeff,
                                              int diff_stride, ref TxfmParam txfm_param)
    {
        Ptr<int> dst_coeff = coeff;
        av1_fwd_txfm2d_32x16(src_diff, dst_coeff, diff_stride, txfm_param.tx_type,
                             txfm_param.bd);
    }

    // [subset] highbd_fwd_txfm_16x4 / 4x16 / 32x8 / 8x32 / 16x64 / 64x16 are compiled out
    // (!CONFIG_REALTIME_ONLY).

    private static void highbd_fwd_txfm_8x8(Ptr<short> src_diff, Ptr<tran_low_t> coeff,
                                            int diff_stride, ref TxfmParam txfm_param)
    {
        Ptr<int> dst_coeff = coeff;
        TX_TYPE tx_type = txfm_param.tx_type;
        int bd = txfm_param.bd;
        av1_fwd_txfm2d_8x8(src_diff, dst_coeff, diff_stride, tx_type, bd);
    }

    private static void highbd_fwd_txfm_16x16(Ptr<short> src_diff, Ptr<tran_low_t> coeff,
                                              int diff_stride, ref TxfmParam txfm_param)
    {
        Ptr<int> dst_coeff = coeff;
        TX_TYPE tx_type = txfm_param.tx_type;
        int bd = txfm_param.bd;
        av1_fwd_txfm2d_16x16(src_diff, dst_coeff, diff_stride, tx_type, bd);
    }

    private static void highbd_fwd_txfm_32x32(Ptr<short> src_diff, Ptr<tran_low_t> coeff,
                                              int diff_stride, ref TxfmParam txfm_param)
    {
        Ptr<int> dst_coeff = coeff;
        TX_TYPE tx_type = txfm_param.tx_type;
        int bd = txfm_param.bd;
        av1_fwd_txfm2d_32x32(src_diff, dst_coeff, diff_stride, tx_type, bd);
    }

    // [subset] highbd_fwd_txfm_32x64 / 64x32 / 64x64: TX64 is disabled.

    internal static void av1_lowbd_fwd_txfm_c(Ptr<short> src_diff, Ptr<tran_low_t> coeff,
                                              int diff_stride, ref TxfmParam txfm_param)
    {
        av1_highbd_fwd_txfm(src_diff, coeff, diff_stride, ref txfm_param);
    }

    // av1_rtcd.h dispatched name.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_lowbd_fwd_txfm(Ptr<short> src_diff, Ptr<tran_low_t> coeff,
                                            int diff_stride, ref TxfmParam txfm_param) =>
        av1_lowbd_fwd_txfm_c(src_diff, coeff, diff_stride, ref txfm_param);

    internal static void av1_highbd_fwd_txfm(Ptr<short> src_diff, Ptr<tran_low_t> coeff,
                                             int diff_stride, ref TxfmParam txfm_param)
    {
        Debug.Assert(av1_ext_tx_used[txfm_param.tx_set_type][txfm_param.tx_type] != 0);
        TX_SIZE tx_size = txfm_param.tx_size;
        switch (tx_size)
        {
            case TX_64X64:
            case TX_32X64:
            case TX_64X32:
                // [subset] TX64 is disabled.
                throw new NotSupportedException("64-point forward transform (TX64)");

            case TX_32X32:
                highbd_fwd_txfm_32x32(src_diff, coeff, diff_stride, ref txfm_param);
                break;
            case TX_16X16:
                highbd_fwd_txfm_16x16(src_diff, coeff, diff_stride, ref txfm_param);
                break;
            case TX_8X8:
                highbd_fwd_txfm_8x8(src_diff, coeff, diff_stride, ref txfm_param);
                break;
            case TX_4X8:
                highbd_fwd_txfm_4x8(src_diff, coeff, diff_stride, ref txfm_param);
                break;
            case TX_8X4:
                highbd_fwd_txfm_8x4(src_diff, coeff, diff_stride, ref txfm_param);
                break;
            case TX_8X16:
                highbd_fwd_txfm_8x16(src_diff, coeff, diff_stride, ref txfm_param);
                break;
            case TX_16X8:
                highbd_fwd_txfm_16x8(src_diff, coeff, diff_stride, ref txfm_param);
                break;
            case TX_16X32:
                highbd_fwd_txfm_16x32(src_diff, coeff, diff_stride, ref txfm_param);
                break;
            case TX_32X16:
                highbd_fwd_txfm_32x16(src_diff, coeff, diff_stride, ref txfm_param);
                break;
            case TX_4X4:
                highbd_fwd_txfm_4x4(src_diff, coeff, diff_stride, ref txfm_param);
                break;
            // [subset] TX_4X16 / TX_16X4 / TX_8X32 / TX_32X8 / TX_16X64 / TX_64X16 cases are
            // compiled out (!CONFIG_REALTIME_ONLY): they reach the default (assert only).
            default: Debug.Assert(false); break;
        }
    }

    // [subset] highbd_wht_fwd_txfm (high bitdepth), wht_fwd_txfm and av1_quick_txfm: never
    // executed (the nonrd path calls aom_hadamard_* directly).
}
