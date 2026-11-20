// Port of libaom av1/common/convolve.h

// <gen:aliases>
global using CONV_BUF_TYPE = ushort;
// </gen:aliases>

global using static Av1rt.Aom.convolve;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal partial struct ConvolveParams
{
    public int do_average;
    public Ptr<CONV_BUF_TYPE> dst;
    public int dst_stride;
    public int round_0;
    public int round_1;
    public int plane;
    public int is_compound;
    public int use_dist_wtd_comp_avg;
    public int fwd_offset;
    public int bck_offset;
}

internal partial struct WienerConvolveParams
{
    public int round_0;
    public int round_1;
}

internal delegate void aom_convolve_fn_t(Ptr<byte> src, int src_stride,
                                         Ptr<byte> dst, int dst_stride, int w, int h,
                                         InterpFilterParams filter_params_x,
                                         InterpFilterParams filter_params_y,
                                         int subpel_x_qn, int subpel_y_qn,
                                         ref ConvolveParams conv_params);

// [subset] aom_highbd_convolve_fn_t: high bitdepth only.

internal static partial class convolve
{
    // <gen:consts>
    internal const int ROUND0_BITS = 3;
    internal const int COMPOUND_ROUND1_BITS = 7;
    internal const int WIENER_ROUND0_BITS = 3;
    // </gen:consts>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WIENER_CLAMP_LIMIT(int r0, int bd) => 1 << ((bd) + 1 + FILTER_BITS - r0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ConvolveParams get_conv_params_no_round(int cmp_index, int plane,
                                                            Ptr<CONV_BUF_TYPE> dst,
                                                            int dst_stride,
                                                            int is_compound, int bd)
    {
        ConvolveParams conv_params = default;
        Debug.Assert(IMPLIES(cmp_index != 0, is_compound != 0));

        conv_params.is_compound = is_compound;
        conv_params.use_dist_wtd_comp_avg = 0;
        conv_params.round_0 = ROUND0_BITS;
        conv_params.round_1 = is_compound != 0 ? COMPOUND_ROUND1_BITS
                                               : 2 * FILTER_BITS - conv_params.round_0;
        // TODO(yunqing): The following dst should only be valid while
        // is_compound = 1;
        conv_params.dst = dst;
        conv_params.dst_stride = dst_stride;
        conv_params.plane = plane;

        // By default, set do average to 1 if this is the second single prediction
        // in a compound mode.
        conv_params.do_average = cmp_index;
        return conv_params;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ConvolveParams get_conv_params(int do_average, int plane,
                                                   int bd)
    {
        return get_conv_params_no_round(do_average, plane, Ptr<CONV_BUF_TYPE>.Null, 0, 0, bd);
    }
}
