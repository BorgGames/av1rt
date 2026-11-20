// Port of libaom aom_dsp/variance.h

global using static Av1rt.Aom.variance_h;

namespace Av1rt.Aom;

// [port] Function pointer typedefs become delegates of the same name (PORTING 3.4). Pointer
// conventions for every implementation and caller:
//   const uint8_t *            -> Ptr<byte>
//   const int32_t *            -> Ptr<int>
//   unsigned int *sse          -> ref uint sse (always written by the callee)
//   const uint8_t *const b_array[] -> Ptr<Ptr<byte>> (callers pass a Ptr<byte>[] of 3 or 4
//                                     reference pointers, converted implicitly)
//   unsigned int *sad_array    -> Ptr<uint> (callers pass a uint[], converted implicitly)
// [subset] aom_dist_wtd_sad_avg_fn_t and aom_dist_wtd_subp_avg_variance_fn_t are referenced
// nowhere in the encoder and are not ported. The functions declared here
// (aom_sse_odd_size, the highbd bilinear passes) belong to variance.c.

internal delegate uint aom_sad_fn_t(Ptr<byte> a, int a_stride, Ptr<byte> b, int b_stride);

internal delegate uint aom_sad_avg_fn_t(Ptr<byte> a, int a_stride, Ptr<byte> b, int b_stride,
                                        Ptr<byte> second_pred);

internal delegate void aom_copy32xn_fn_t(Ptr<byte> a, int a_stride, Ptr<byte> b, int b_stride, int n);

internal delegate void aom_sad_multi_d_fn_t(Ptr<byte> a, int a_stride, Ptr<Ptr<byte>> b_array,
                                            int b_stride, Ptr<uint> sad_array);

internal delegate uint aom_variance_fn_t(Ptr<byte> a, int a_stride, Ptr<byte> b, int b_stride,
                                         ref uint sse);

internal delegate uint aom_subpixvariance_fn_t(Ptr<byte> a, int a_stride, int xoffset, int yoffset,
                                               Ptr<byte> b, int b_stride, ref uint sse);

internal delegate uint aom_subp_avg_variance_fn_t(Ptr<byte> a, int a_stride, int xoffset, int yoffset,
                                                  Ptr<byte> b, int b_stride, ref uint sse,
                                                  Ptr<byte> second_pred);

internal delegate uint aom_masked_sad_fn_t(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred, Ptr<byte> msk, int msk_stride,
                                           int invert_mask);

internal delegate uint aom_masked_subpixvariance_fn_t(Ptr<byte> src, int src_stride, int xoffset,
                                                      int yoffset, Ptr<byte> @ref, int ref_stride,
                                                      Ptr<byte> second_pred, Ptr<byte> msk,
                                                      int msk_stride, int invert_mask, ref uint sse);

internal delegate uint aom_obmc_sad_fn_t(Ptr<byte> pred, int pred_stride, Ptr<int> wsrc, Ptr<int> msk);

internal delegate uint aom_obmc_variance_fn_t(Ptr<byte> pred, int pred_stride, Ptr<int> wsrc,
                                              Ptr<int> msk, ref uint sse);

internal delegate uint aom_obmc_subpixvariance_fn_t(Ptr<byte> pred, int pred_stride, int xoffset,
                                                    int yoffset, Ptr<int> wsrc, Ptr<int> msk,
                                                    ref uint sse);

// [port] A class: the encoder keeps one per block size (AV1_PRIMARY.fn_ptr[BLOCK_SIZES_ALL]) and
// hands out pointers to them (`const aom_variance_fn_ptr_t *vfp`); it is never copied by value.
// The owner of the array must allocate each element.
internal sealed partial class aom_variance_fn_ptr_t
{
    public aom_sad_fn_t sdf;
    // Same as normal sad, but downsample the rows by a factor of 2.
    public aom_sad_fn_t sdsf;
    public aom_sad_avg_fn_t sdaf;
    public aom_variance_fn_t vf;
    public aom_subpixvariance_fn_t svf;
    public aom_subp_avg_variance_fn_t svaf;
    public aom_sad_multi_d_fn_t sdx4df;
    public aom_sad_multi_d_fn_t sdx3df;
    // Same as sadx4, but downsample the rows by a factor of 2.
    public aom_sad_multi_d_fn_t sdsx4df;
    public aom_masked_sad_fn_t msdf;
    public aom_masked_subpixvariance_fn_t msvf;
    public aom_obmc_sad_fn_t osdf;
    public aom_obmc_variance_fn_t ovf;
    public aom_obmc_subpixvariance_fn_t osvf;
}

// [port] The class is named variance_h because variance.c defines a function named variance.
internal static partial class variance_h
{
    // [port] variance.h repeats `#define FILTER_BITS 7` from aom_filter.h; the one constant
    // lives in aom_filter.h.cs.
    internal const int FILTER_WEIGHT = 128;
}
