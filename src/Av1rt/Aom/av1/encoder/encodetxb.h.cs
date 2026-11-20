// Port of libaom av1/encoder/encodetxb.h
global using static Av1rt.Aom.encodetxb;

namespace Av1rt.Aom;

internal static partial class encodetxb
{
    internal const int TXB_SKIP_CTX_MASK = 15;
    internal const int DC_SIGN_CTX_SHIFT = 4;
    internal const int DC_SIGN_CTX_MASK = 3;

    // C: av1_get_txb_entropy_context() is only forward-declared here; it is defined in
    // av1/common/txb_common.h.
    // [subset] av1_cost_skip_txb: only used by the rd transform search.

    // C: used by the trellis optimization (txb_rdopt.c), which is off in the subset; kept
    // because the tables are trivially cheap.
    internal static readonly int[][] plane_rd_mult_chroma = {
        new int[] { 17, 13 },
        new int[] { 16, 10 },
    };

    internal static readonly int[][] plane_rd_mult = {
        new int[] { 17, 20 },
        new int[] { 16, 20 },
    };
}
