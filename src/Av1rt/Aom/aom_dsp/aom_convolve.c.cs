// Port of libaom aom_dsp/aom_convolve.c
// [subset] horz_scalar_product, vert_scalar_product, convolve_horiz, convolve_vert,
// get_filter_base, get_filter_offset, aom_convolve8_horiz_c, aom_convolve8_vert_c and
// aom_scaled_2d_c never execute in the realtime subset (they serve frame scaling); only
// aom_convolve_copy_c runs.

global using static Av1rt.Aom.aom_convolve;

namespace Av1rt.Aom;

internal static partial class aom_convolve
{
    internal static void aom_convolve_copy_c(Ptr<byte> src, int src_stride, Ptr<byte> dst,
                                             int dst_stride, int w, int h)
    {
        for (int r = h; r > 0; --r)
        {
            memcpy(dst, src, w);
            src += src_stride;
            dst += dst_stride;
        }
    }

    internal static void aom_convolve_copy(Ptr<byte> src, int src_stride, Ptr<byte> dst,
                                           int dst_stride, int w, int h) =>
        aom_convolve_copy_c(src, src_stride, dst, dst_stride, w, h);
}
