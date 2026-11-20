// Port of libaom av1/encoder/allintra_vis.h
global using static Av1rt.Aom.allintra_vis;

namespace Av1rt.Aom;

// All-intra perceptual analysis is out of the subset (allintra mode only).
internal static partial class allintra_vis
{
    internal const int MB_WIENER_MT_UNIT_SIZE = BLOCK_64X64;

    // av1_dealloc_mb_wiener_var_pred_buf (allintra_vis.c) runs on teardown; it
    // is stubbed in _stubs/p1b1.cs.
}
