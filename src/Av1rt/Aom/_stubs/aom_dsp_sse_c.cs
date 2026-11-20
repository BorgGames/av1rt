// Stubs for aom_dsp/sse.c: delete this file when aom_dsp/sse.c is ported.
global using static Av1rt.Aom.stubs_aom_dsp_sse_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_aom_dsp_sse_c
{
    // aom_dsp/sse.c: int64_t aom_sse_c(...), dispatched as aom_sse (aom_dsp_rtcd.h).
    // Used by e4 (pickcdef.c).
    internal static long aom_sse(Ptr<byte> a, int a_stride, Ptr<byte> b, int b_stride,
                                 int width, int height) =>
        throw new NotImplementedException();
}
