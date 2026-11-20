// Port of libaom aom_dsp/sse.c

global using static Av1rt.Aom.sse;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

/*
 * Sum the square of the difference between every corresponding element of the
 * buffers.
 */
// [subset] aom_highbd_sse_c: CONFIG_AV1_HIGHBITDEPTH == 0.
internal static partial class sse
{
    internal static long aom_sse_c(Ptr<byte> a, int a_stride, Ptr<byte> b,
                                   int b_stride, int width, int height)
    {
        int y, x;
        long sse = 0;

        for (y = 0; y < height; y++)
        {
            for (x = 0; x < width; x++)
            {
                int diff = Math.Abs(a[x] - b[x]);
                sse += diff * diff;
            }

            a += a_stride;
            b += b_stride;
        }
        return sse;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long aom_sse(Ptr<byte> a, int a_stride, Ptr<byte> b, int b_stride, int width,
                                 int height) =>
        aom_sse_c(a, a_stride, b, b_stride, width, height);
}
