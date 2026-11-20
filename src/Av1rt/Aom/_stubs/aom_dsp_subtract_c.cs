// Stubs for aom_dsp/subtract.c: delete this file when aom_dsp/subtract.c is ported.
global using static Av1rt.Aom.stubs_aom_dsp_subtract_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_aom_dsp_subtract_c
{
    // ---- aom_dsp/subtract.c (aom_dsp_rtcd.h dispatched name of aom_subtract_block_c) ----
    internal static void aom_subtract_block(int rows, int cols, Ptr<short> diff_ptr,
                                            int diff_stride, Ptr<byte> src_ptr,
                                            int src_stride, Ptr<byte> pred_ptr,
                                            int pred_stride) =>
        throw new NotImplementedException();
}
