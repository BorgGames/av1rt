// Port of libaom av1/encoder/encodemb.h
global using AV1_XFORM_QUANT = int;
global using OPT_TYPE = int;
global using TRELLIS_OPT_TYPE = int;
global using static Av1rt.Aom.encodemb;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class encodemb
{
    // enum AV1_XFORM_QUANT
    internal const int AV1_XFORM_QUANT_FP = 0;
    internal const int AV1_XFORM_QUANT_B = 1;
    internal const int AV1_XFORM_QUANT_DC = 2;
    internal const int AV1_XFORM_QUANT_SKIP_QUANT = 3;
    internal const int AV1_XFORM_QUANT_TYPES = 4;
    // enum OPT_TYPE
    internal const int NONE_OPT = 0; // No optimization.
    internal const int TRELLIS_OPT = 1; // Trellis optimization. See `av1_optimize_b()`.
    internal const int DROPOUT_OPT = 2; // Dropout optimization. See `av1_dropout_qcoeff()`.
    internal const int TRELLIS_DROPOUT_OPT = 3; // Perform dropout after trellis optimization.
    // enum TRELLIS_OPT_TYPE
    internal const int NO_TRELLIS_OPT = 0; // No trellis optimization
    internal const int FULL_TRELLIS_OPT = 1; // Trellis optimization in all stages
    internal const int FINAL_PASS_TRELLIS_OPT = 2; // Trellis optimization in only the final encode pass
    internal const int NO_ESTIMATE_YRD_TRELLIS_OPT = 3; // Disable trellis in estimate_yrd_for_sb

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_setup_xform(AV1_COMMON cm, MACROBLOCK x,
                                         TX_SIZE tx_size, TX_TYPE tx_type,
                                         ref TxfmParam txfm_param)
    {
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mbmi = xd.mi[0];

        txfm_param.tx_type = tx_type;
        txfm_param.tx_size = tx_size;
        txfm_param.lossless = xd.lossless[mbmi.segment_id];
        // C: tx_set_type is only computed when NDEBUG is not defined (assert bookkeeping); the
        // oracle is a release build.
        _ = cm;

        txfm_param.bd = xd.bd;
        // [subset] is_cur_buf_hbd(xd) is always 0 (no high bitdepth).
        txfm_param.is_hbd = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_setup_quant(TX_SIZE tx_size, int use_optimize_b,
                                         int xform_quant_idx, int use_quant_b_adapt,
                                         ref QUANT_PARAM qparam)
    {
        qparam.log_scale = av1_get_tx_scale(tx_size);
        qparam.tx_size = tx_size;

        qparam.use_quant_b_adapt = use_quant_b_adapt;
        qparam.use_optimize_b = use_optimize_b;
        qparam.xform_quant_idx = xform_quant_idx;

        qparam.qmatrix = Ptr<qm_val_t>.Null;
        qparam.iqmatrix = Ptr<qm_val_t>.Null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_setup_qmatrix(CommonQuantParams quant_params,
                                           MACROBLOCKD xd, int plane,
                                           TX_SIZE tx_size, TX_TYPE tx_type,
                                           ref QUANT_PARAM qparam)
    {
        _ = quant_params;
        if (tx_type < IDTX)
        {
            var pd = xd.plane[plane];
            int seg_id = xd.mi[0].segment_id;
            qparam.qmatrix = pd.seg_qmatrix[seg_id][tx_size];
            qparam.iqmatrix = pd.seg_iqmatrix[seg_id][tx_size];
        }
        else
        {
            qparam.qmatrix = Ptr<qm_val_t>.Null;
            qparam.iqmatrix = Ptr<qm_val_t>.Null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_xform(MACROBLOCK x, int plane, int block, int blk_row,
                                   int blk_col, BLOCK_SIZE plane_bsize,
                                   ref TxfmParam txfm_param)
    {
        macroblock_plane p = x.plane[plane];
        int block_offset = BLOCK_OFFSET(block);
        Ptr<int> coeff = p.coeff + block_offset;
        int diff_stride = block_size_wide[plane_bsize];

        int src_offset = (blk_row * diff_stride + blk_col);
        Ptr<short> src_diff = p.src_diff + (src_offset << MI_SIZE_LOG2);

        av1_fwd_txfm(src_diff, coeff, diff_stride, ref txfm_param);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_quant(MACROBLOCK x, int plane, int block,
                                   ref TxfmParam txfm_param, in QUANT_PARAM qparam)
    {
        macroblock_plane p = x.plane[plane];
        var scan_order =
            get_scan(txfm_param.tx_size, txfm_param.tx_type);
        int block_offset = BLOCK_OFFSET(block);
        Ptr<int> coeff = p.coeff + block_offset;
        Ptr<int> qcoeff = p.qcoeff + block_offset;
        Ptr<int> dqcoeff = p.dqcoeff + block_offset;
        Ptr<ushort> eob = p.eobs + block;

        if (qparam.xform_quant_idx != AV1_XFORM_QUANT_SKIP_QUANT)
        {
            int n_coeffs = av1_get_max_eob(txfm_param.tx_size);
            if (x.seg_skip_block == 0)
            {
                if (qparam.xform_quant_idx == AV1_XFORM_QUANT_FP)
                {
                    // [subset] av1_quantize_fp_facade: the encode path always uses
                    // AV1_XFORM_QUANT_B (USE_B_QUANT_NO_TRELLIS, trellis off).
                    throw new NotSupportedException("av1_quant: AV1_XFORM_QUANT_FP");
                }
                else if (qparam.xform_quant_idx == AV1_XFORM_QUANT_B)
                {
                    av1_quantize_b_facade(coeff, n_coeffs, p, qcoeff, dqcoeff, eob,
                                          scan_order, qparam);
                }
                else
                {
                    // [subset] av1_quantize_dc_facade: AV1_XFORM_QUANT_DC is never used.
                    throw new NotSupportedException("av1_quant: AV1_XFORM_QUANT_DC");
                }
                // [subset] high bitdepth facades.
            }
            else
            {
                av1_quantize_skip(n_coeffs, qcoeff, dqcoeff, eob);
            }
        }
        if (qparam.use_optimize_b != 0)
        {
            p.txb_entropy_ctx[block] = 0;
        }
        else
        {
            p.txb_entropy_ctx[block] =
                av1_get_txb_entropy_context(qcoeff, scan_order, eob.Value);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_xform_quant(MACROBLOCK x, int plane, int block,
                                         int blk_row, int blk_col,
                                         BLOCK_SIZE plane_bsize,
                                         ref TxfmParam txfm_param,
                                         in QUANT_PARAM qparam)
    {
        av1_xform(x, plane, block, blk_row, blk_col, plane_bsize, ref txfm_param);
        av1_quant(x, plane, block, ref txfm_param, qparam);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_subtract_block(MACROBLOCK x, int rows, int cols,
                                            Ptr<short> diff, int diff_stride,
                                            Ptr<byte> src8, int src_stride,
                                            Ptr<byte> pred8,
                                            int pred_stride, int plane,
                                            BLOCK_SIZE plane_bsize, int blk_col,
                                            int blk_row, TX_TYPE tx_type,
                                            bool do_border_pad)
    {
        Debug.Assert(rows >= 4 && cols >= 4);
        // [subset] aom_highbd_subtract_block (high bitdepth).
        aom_subtract_block(rows, cols, diff, diff_stride, src8, src_stride, pred8,
                           pred_stride);
        if (do_border_pad)
        {
            // [subset] av1_pad_block_border: cpi->do_border_pad is only set for GOOD mode with
            // DELTA_Q_OBJECTIVE.
            throw new NotSupportedException("av1_subtract_block: do_border_pad");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_subtract_txb(MACROBLOCK x, int plane,
                                          BLOCK_SIZE plane_bsize, int blk_col,
                                          int blk_row, TX_SIZE tx_size,
                                          TX_TYPE tx_type, bool do_border_pad)
    {
        macroblock_plane p = x.plane[plane];
        var pd = x.e_mbd.plane[plane];
        int diff_stride = block_size_wide[plane_bsize];
        int src_stride = p.src.stride;
        int dst_stride = pd.dst.stride;
        int tx1d_width = tx_size_wide[tx_size];
        int tx1d_height = tx_size_high[tx_size];
        Ptr<byte> dst = pd.dst.buf + ((blk_row * dst_stride + blk_col) << MI_SIZE_LOG2);
        Ptr<byte> src = p.src.buf + ((blk_row * src_stride + blk_col) << MI_SIZE_LOG2);
        Ptr<short> src_diff =
            p.src_diff + ((blk_row * diff_stride + blk_col) << MI_SIZE_LOG2);
        av1_subtract_block(x, tx1d_height, tx1d_width, src_diff, diff_stride, src,
                           src_stride, dst, dst_stride, plane, plane_bsize, blk_col,
                           blk_row, tx_type, do_border_pad);
    }

    // [subset] av1_subtract_plane: only used by the rd path.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_set_txb_context(MACROBLOCK x, int plane, int block,
                                             TX_SIZE tx_size, Ptr<ENTROPY_CONTEXT> a,
                                             Ptr<ENTROPY_CONTEXT> l)
    {
        byte ctx = x.plane[plane].txb_entropy_ctx[block];
        memset(a, unchecked((ENTROPY_CONTEXT)ctx), tx_size_wide_unit[tx_size]);
        memset(l, unchecked((ENTROPY_CONTEXT)ctx), tx_size_high_unit[tx_size]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_trellis_used(TRELLIS_OPT_TYPE optimize_b,
                                        RUN_TYPE dry_run)
    {
        if (optimize_b == NO_TRELLIS_OPT) return 0;
        if (optimize_b == FINAL_PASS_TRELLIS_OPT && dry_run != OUTPUT_ENABLED)
            return 0;
        return 1;
    }

    // Scaling terms (precision of 12 bits) to perform tx-size specific
    // normalization that is used in DCT_DCT forward transform.
    // For transform blocks of 1:2 and 2:1       - sqrt(2) normalization is used
    // For transform blocks of 1:4 and 4:1       - factor of 2 is used
    // For transform blocks TX_8x8 and below     - an additional factor of 2 is used
    // For transform blocks max(width,height)=64 - currently not supported
    internal static readonly ushort[] dc_coeff_scale = {
        1024, 2048, 4096, 4096, 0, 1448, 1448, 2896, 2896, 2896, 2896, 0, 0, 2048, 2048, 4096,
        4096, 0, 0,
    };
}

// Class: a local of av1_encode_sb()/encode_block_inter(), reached through encode_b_args.
internal sealed partial class optimize_ctx
{
    internal readonly ENTROPY_CONTEXT[][] ta = EncArrays.New2<ENTROPY_CONTEXT>(MAX_MB_PLANE, MAX_MIB_SIZE);
    internal readonly ENTROPY_CONTEXT[][] tl = EncArrays.New2<ENTROPY_CONTEXT>(MAX_MB_PLANE, MAX_MIB_SIZE);
}

// Class: passed to the transform-block visitors as `void *arg`.
internal sealed partial class encode_b_args
{
    internal AV1_COMP cpi;
    internal MACROBLOCK x;
    internal optimize_ctx ctx;
    internal Ptr<ENTROPY_CONTEXT> ta;
    internal Ptr<ENTROPY_CONTEXT> tl;
    internal RUN_TYPE dry_run;
    internal TRELLIS_OPT_TYPE enable_optimize_b;
}
