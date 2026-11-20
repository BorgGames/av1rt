// Port of libaom av1/common/idct.h

global using static Av1rt.Aom.idct;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal delegate void transform_1d(Ptr<int> input, Ptr<int> output);

internal partial struct transform_2d
{
    public transform_1d cols, rows;  // vertical and horizontal
}

internal static partial class idct
{
    // <gen:consts>
    internal const int MAX_TX_SCALE = 1;

    // uint8_t tx_scale[TX_SIZES_ALL]
    private static readonly byte[] tx_scale = {
        0, 0, 0, 1, 2, 0, 0, 0, 0, 1, 1, 2, 2, 0, 0, 0, 0, 1, 1
    };
    // </gen:consts>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_get_tx_scale(TX_SIZE tx_size)
    {
        // (pels > 256) + (pels > 1024), where pels = tx_size_2d[tx_size].
        Debug.Assert(tx_size < TX_SIZES_ALL);
        return tx_scale[tx_size];
    }

    // PORT-NOTE: assumes p1c ports TxfmParam (aom_dsp/txfm_common.h) as a struct, so the
    // C `const TxfmParam *` parameter of av1_inv_txfm_add is `in TxfmParam`.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_inverse_transform_block(MACROBLOCKD xd,
                                                     Ptr<int> dqcoeff,
                                                     int plane, TX_TYPE tx_type,
                                                     TX_SIZE tx_size, Ptr<byte> dst,
                                                     int stride, int eob,
                                                     int reduced_tx_set)
    {
        if (eob == 0) return;

        Debug.Assert(eob <= av1_get_max_eob(tx_size));

        TxfmParam txfm_param = new TxfmParam();
        txfm_param.tx_type = tx_type;
        txfm_param.tx_size = tx_size;
        txfm_param.eob = eob;
        txfm_param.lossless = xd.lossless[xd.mi[0].segment_id];
        txfm_param.bd = xd.bd;
        txfm_param.is_hbd = is_cur_buf_hbd(xd);
        txfm_param.tx_set_type = av1_get_ext_tx_set_type(
            txfm_param.tx_size, is_inter_block(xd.mi[0]), reduced_tx_set);
        Debug.Assert(av1_ext_tx_used[txfm_param.tx_set_type][txfm_param.tx_type] != 0);

        // [subset] is_hbd is always 0: av1_highbd_inv_txfm_add is not ported.
        av1_inv_txfm_add(dqcoeff, dst, stride, in txfm_param);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<int> cast_to_int32(Ptr<int> input)
    {
        return input;
    }
}
