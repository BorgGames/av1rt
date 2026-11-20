// Port of libaom av1/encoder/txb_rdopt.h
global using static Av1rt.Aom.txb_rdopt;

namespace Av1rt.Aom;

internal static partial class txb_rdopt
{
    // [subset] av1_optimize_b (trellis coefficient optimization): trellis is off in the
    // subset (encode path uses NO_TRELLIS_OPT).
}
