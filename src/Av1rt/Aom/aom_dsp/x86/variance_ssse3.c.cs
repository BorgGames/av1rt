// Port of libaom aom_dsp/x86/variance_ssse3.c

global using static Av1rt.Aom.variance_ssse3;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] The FN macros are one function each (FN_VAR / FN_AVG_VAR) with the macro parameters as
// arguments; the cast_prod / cast pair of every instantiation is spelled out in its wrapper.
// Only the CONFIG_REALTIME_ONLY FNS(ssse3) instantiations the RTCD picks on an AVX2 machine are
// written: sub_pixel_variance{8x16,8x8,8x4,4x8,4x4} and
// sub_pixel_avg_variance{16x32,16x16,16x8,8x16,8x8,8x4,4x8,4x4}; the wider ones use AVX2.
// The asm helpers are in subpel_variance_ssse3.asm.cs (without the unused NULL arguments).
internal static class variance_ssse3
{
    // FN(w, h, wf, wlog2, hlog2, opt, cast_prod, cast) for aom_sub_pixel_variance: returns se
    // and sets sse; the wrapper applies `sse - (unsigned int)(cast_prod(cast se * se) >> bits)`.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FN_VAR(int w, int h, int wf, Ptr<byte> src, int src_stride,
                              int x_offset, int y_offset, Ptr<byte> dst, int dst_stride,
                              ref uint sse_ptr)
    {
        /*Avoid overflow in helper by capping height.*/
        int hf = Math.Min(h, 64);
        uint sse = 0;
        int se = 0;
        for (int i = 0; i < (w / wf); ++i)
        {
            Ptr<byte> src_ptr = src;
            Ptr<byte> dst_ptr = dst;
            for (int j = 0; j < (h / hf); ++j)
            {
                uint sse2 = 0;
                int se2 = wf == 8
                    ? aom_sub_pixel_variance8xh_ssse3(src_ptr, src_stride, x_offset, y_offset,
                                                      dst_ptr, dst_stride, hf, ref sse2)
                    : aom_sub_pixel_variance4xh_ssse3(src_ptr, src_stride, x_offset, y_offset,
                                                      dst_ptr, dst_stride, hf, ref sse2);
                dst_ptr += hf * dst_stride;
                src_ptr += hf * src_stride;
                se += se2;
                sse += sse2;
            }
            src += wf;
            dst += wf;
        }
        sse_ptr = sse;
        return se;
    }

    // FN(8, 16, 8, 3, 4, opt, (int32_t), (int32_t)) ... FN(4, 4, 4, 2, 2, opt, (int32_t), (int32_t))
    internal static uint aom_sub_pixel_variance8x16_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                          Ptr<byte> dst, int dst_stride, ref uint sse_ptr)
    {
        int se = FN_VAR(8, 16, 8, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr);
        return sse_ptr - (uint)((int)(se * se) >> (3 + 4));
    }

    internal static uint aom_sub_pixel_variance8x8_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                         Ptr<byte> dst, int dst_stride, ref uint sse_ptr)
    {
        int se = FN_VAR(8, 8, 8, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr);
        return sse_ptr - (uint)((int)(se * se) >> (3 + 3));
    }

    internal static uint aom_sub_pixel_variance8x4_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                         Ptr<byte> dst, int dst_stride, ref uint sse_ptr)
    {
        int se = FN_VAR(8, 4, 8, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr);
        return sse_ptr - (uint)((int)(se * se) >> (3 + 2));
    }

    internal static uint aom_sub_pixel_variance4x8_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                         Ptr<byte> dst, int dst_stride, ref uint sse_ptr)
    {
        int se = FN_VAR(4, 8, 4, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr);
        return sse_ptr - (uint)((int)(se * se) >> (2 + 3));
    }

    internal static uint aom_sub_pixel_variance4x4_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                         Ptr<byte> dst, int dst_stride, ref uint sse_ptr)
    {
        int se = FN_VAR(4, 4, 4, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr);
        return sse_ptr - (uint)((int)(se * se) >> (2 + 2));
    }

    // FN(w, h, wf, wlog2, hlog2, opt, cast_prod, cast) for aom_sub_pixel_avg_variance (second
    // FN macro): sec is a packed w x h block, passed to the helper with stride w.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FN_AVG_VAR(int w, int h, int wf, Ptr<byte> src, int src_stride,
                                  int x_offset, int y_offset, Ptr<byte> dst, int dst_stride,
                                  ref uint sse_ptr, Ptr<byte> sec)
    {
        /*Avoid overflow in helper by capping height.*/
        int hf = Math.Min(h, 64);
        uint sse = 0;
        int se = 0;
        for (int i = 0; i < (w / wf); ++i)
        {
            Ptr<byte> src_ptr = src;
            Ptr<byte> dst_ptr = dst;
            Ptr<byte> sec_ptr = sec;
            for (int j = 0; j < (h / hf); ++j)
            {
                uint sse2 = 0;
                int se2 = wf == 16
                    ? aom_sub_pixel_avg_variance16xh_ssse3(src_ptr, src_stride, x_offset, y_offset,
                                                           dst_ptr, dst_stride, sec_ptr, w, hf, ref sse2)
                    : wf == 8
                    ? aom_sub_pixel_avg_variance8xh_ssse3(src_ptr, src_stride, x_offset, y_offset,
                                                          dst_ptr, dst_stride, sec_ptr, w, hf, ref sse2)
                    : aom_sub_pixel_avg_variance4xh_ssse3(src_ptr, src_stride, x_offset, y_offset,
                                                          dst_ptr, dst_stride, sec_ptr, w, hf, ref sse2);
                dst_ptr += hf * dst_stride;
                src_ptr += hf * src_stride;
                sec_ptr += hf * w;
                se += se2;
                sse += sse2;
            }
            src += wf;
            dst += wf;
            sec += wf;
        }
        sse_ptr = sse;
        return se;
    }

    // FN(16, 32, 16, 4, 5, opt, (int64_t), (int64_t))
    internal static uint aom_sub_pixel_avg_variance16x32_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                               Ptr<byte> dst, int dst_stride, ref uint sse_ptr, Ptr<byte> sec)
    {
        int se = FN_AVG_VAR(16, 32, 16, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr, sec);
        return sse_ptr - (uint)((long)((long)se * se) >> (4 + 5));
    }

    // FN(16, 16, 16, 4, 4, opt, (uint32_t), (int64_t))
    internal static uint aom_sub_pixel_avg_variance16x16_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                               Ptr<byte> dst, int dst_stride, ref uint sse_ptr, Ptr<byte> sec)
    {
        int se = FN_AVG_VAR(16, 16, 16, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr, sec);
        return sse_ptr - (uint)((uint)((long)se * se) >> (4 + 4));
    }

    // FN(16, 8, 16, 4, 3, opt, (uint32_t), (int32_t)) ... FN(4, 4, 4, 2, 2, opt, (uint32_t), (int32_t))
    internal static uint aom_sub_pixel_avg_variance16x8_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                              Ptr<byte> dst, int dst_stride, ref uint sse_ptr, Ptr<byte> sec)
    {
        int se = FN_AVG_VAR(16, 8, 16, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr, sec);
        return sse_ptr - (uint)((uint)(se * se) >> (4 + 3));
    }

    internal static uint aom_sub_pixel_avg_variance8x16_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                              Ptr<byte> dst, int dst_stride, ref uint sse_ptr, Ptr<byte> sec)
    {
        int se = FN_AVG_VAR(8, 16, 8, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr, sec);
        return sse_ptr - (uint)((uint)(se * se) >> (3 + 4));
    }

    internal static uint aom_sub_pixel_avg_variance8x8_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                             Ptr<byte> dst, int dst_stride, ref uint sse_ptr, Ptr<byte> sec)
    {
        int se = FN_AVG_VAR(8, 8, 8, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr, sec);
        return sse_ptr - (uint)((uint)(se * se) >> (3 + 3));
    }

    internal static uint aom_sub_pixel_avg_variance8x4_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                             Ptr<byte> dst, int dst_stride, ref uint sse_ptr, Ptr<byte> sec)
    {
        int se = FN_AVG_VAR(8, 4, 8, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr, sec);
        return sse_ptr - (uint)((uint)(se * se) >> (3 + 2));
    }

    internal static uint aom_sub_pixel_avg_variance4x8_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                             Ptr<byte> dst, int dst_stride, ref uint sse_ptr, Ptr<byte> sec)
    {
        int se = FN_AVG_VAR(4, 8, 4, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr, sec);
        return sse_ptr - (uint)((uint)(se * se) >> (2 + 3));
    }

    internal static uint aom_sub_pixel_avg_variance4x4_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                             Ptr<byte> dst, int dst_stride, ref uint sse_ptr, Ptr<byte> sec)
    {
        int se = FN_AVG_VAR(4, 4, 4, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr, sec);
        return sse_ptr - (uint)((uint)(se * se) >> (2 + 2));
    }
}
