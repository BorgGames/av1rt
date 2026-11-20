// Stubs for av1/common/convolve.c: delete this file when av1/common/convolve.c is ported.
global using static Av1rt.Aom.stubs_av1_common_convolve_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_common_convolve_c
{
    // av1/common/convolve.c
    // PORT-NOTE: `const InterpFilterParams *interp_filters[2]` is passed as a span (see
    // InterPredParams.interp_filter_params in reconinter.h.cs).
    internal static void av1_convolve_2d_facade(Ptr<byte> src, int src_stride, Ptr<byte> dst,
                                                int dst_stride, int w, int h,
                                                ReadOnlySpan<InterpFilterParams> interp_filters,
                                                int subpel_x_qn, int x_step_q4,
                                                int subpel_y_qn, int y_step_q4, int scaled,
                                                ref ConvolveParams conv_params) =>
        throw new NotImplementedException();
}
