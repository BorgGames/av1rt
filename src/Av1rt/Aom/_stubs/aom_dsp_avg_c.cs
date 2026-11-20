// Stubs for aom_dsp/avg.c: delete this file when aom_dsp/avg.c is ported.
global using static Av1rt.Aom.stubs_aom_dsp_avg_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_aom_dsp_avg_c
{
    // ---- aom_dsp/avg.c (aom_dsp_rtcd.h dispatched names of the _c functions) ----------
    internal static void aom_int_pro_row(Ptr<short> hbuf, Ptr<byte> @ref, int ref_stride,
                                         int width, int height, int norm_factor) =>
        throw new NotImplementedException();

    internal static void aom_int_pro_col(Ptr<short> vbuf, Ptr<byte> @ref, int ref_stride,
                                         int width, int height, int norm_factor) =>
        throw new NotImplementedException();

    internal static int aom_vector_var(Ptr<short> @ref, Ptr<short> src, int bwl) =>
        throw new NotImplementedException();
}
