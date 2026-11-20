// Stubs for av1/common/idct.c: delete this file when av1/common/idct.c is ported.
global using static Av1rt.Aom.stubs_av1_common_idct_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_common_idct_c
{
    // av1/common/idct.c (av1_inv_txfm_add_c via av1_rtcd.h; no SIMD)
    internal static void av1_inv_txfm_add(Ptr<tran_low_t> dqcoeff, Ptr<byte> dst, int stride,
                                          in TxfmParam txfm_param) =>
        throw new NotImplementedException();
}
