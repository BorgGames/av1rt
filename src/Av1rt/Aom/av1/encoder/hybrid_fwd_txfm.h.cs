// Port of libaom av1/encoder/hybrid_fwd_txfm.h
global using static Av1rt.Aom.hybrid_fwd_txfm;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class hybrid_fwd_txfm
{
    // C: if (LIKELY(txfm_param->bd == 8)) lowbd else highbd. Both are kept: the oracle
    // coverage shows the else branch running (av1_lowbd_fwd_txfm_c itself forwards to
    // av1_highbd_fwd_txfm, so both compute the same transform for 8-bit input).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_fwd_txfm(Ptr<short> src_diff, Ptr<int> coeff,
                                      int diff_stride, ref TxfmParam txfm_param)
    {
        if (txfm_param.bd == 8)
            av1_lowbd_fwd_txfm(src_diff, coeff, diff_stride, ref txfm_param);
        else
            av1_highbd_fwd_txfm(src_diff, coeff, diff_stride, ref txfm_param);
    }
}
