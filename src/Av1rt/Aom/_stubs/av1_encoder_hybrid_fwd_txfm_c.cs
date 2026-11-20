// Stubs for av1/encoder/hybrid_fwd_txfm.c: delete this file when av1/encoder/hybrid_fwd_txfm.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_hybrid_fwd_txfm_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_hybrid_fwd_txfm_c
{
    // ---- av1/encoder/hybrid_fwd_txfm.c --------------------------------------------------
    // av1_rtcd.h dispatched name of av1_lowbd_fwd_txfm_c.
    internal static void av1_lowbd_fwd_txfm(Ptr<short> src_diff, Ptr<int> coeff,
                                            int diff_stride, ref TxfmParam txfm_param) =>
        throw new NotImplementedException();
    internal static void av1_highbd_fwd_txfm(Ptr<short> src_diff, Ptr<int> coeff,
                                             int diff_stride, ref TxfmParam txfm_param) =>
        throw new NotImplementedException();
}
