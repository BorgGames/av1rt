// Port of libaom av1/common/restoration.h
// [subset] Loop restoration is disabled; the types are ported because AV1_COMMON / AV1_COMP
// hold them, plus the two default setters that run at setup.

global using static Av1rt.Aom.restoration;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal partial struct sgr_params_type
{
    public Arr2<int> r;  // radii
    public Arr2<int> s;  // sgr parameters for r[0] and r[1], based on GenSgrprojVtable()
}

internal partial struct RestorationUnitInfo
{
    public RestorationType restoration_type;
    public WienerInfo wiener_info;
    public SgrprojInfo sgrproj_info;
}

// A restoration line buffer needs space for two lines plus a horizontal filter
// margin of RESTORATION_EXTRA_HORZ on each side.
internal sealed partial class RestorationLineBuffers
{
    // Temporary buffers to save/restore 3 lines above/below the restoration
    // stripe.
    public readonly ushort[][] tmp_save_above = new_jagged<ushort>(RESTORATION_BORDER, RESTORATION_LINEBUFFER_WIDTH);
    public readonly ushort[][] tmp_save_below = new_jagged<ushort>(RESTORATION_BORDER, RESTORATION_LINEBUFFER_WIDTH);
}

internal partial struct RestorationStripeBoundaries
{
    public Ptr<byte> stripe_boundary_above;
    public Ptr<byte> stripe_boundary_below;
    public int stripe_boundary_stride;
    public int stripe_boundary_size;
}

internal sealed partial class RestorationInfo
{
    public RestorationType frame_restoration_type;
    public int restoration_unit_size;
    public int num_rest_units;
    public int vert_units;
    public int horz_units;
    public Ptr<RestorationUnitInfo> unit_info;
    public RestorationStripeBoundaries boundaries;
    public int optimized_lr;
}

internal partial struct RestorationTileLimits
{
    public int h_start, h_end, v_start, v_end;
}

internal delegate void rest_unit_visitor_t(in RestorationTileLimits limits,
                                           Ptr<int> tmpbuf,
                                           RestorationLineBuffers rlbs,
                                           aom_internal_error_info error_info);

internal partial struct FilterFrameCtxt
{
    public RestorationInfo rsi;
    public int ss_x, ss_y;
    public int plane_w, plane_h;
    public int highbd, bit_depth;
    public Ptr<byte> data8, dst8;
    public int data_stride, dst_stride;
}

internal sealed partial class AV1LrStruct
{
    public rest_unit_visitor_t on_rest_unit;
    public readonly FilterFrameCtxt[] ctxt = new FilterFrameCtxt[MAX_MB_PLANE];
    public YV12_BUFFER_CONFIG frame;
    public YV12_BUFFER_CONFIG dst;
}

internal delegate void sync_read_fn_t(object lr_sync, int r, int c, int plane);
internal delegate void sync_write_fn_t(object lr_sync, int r, int c,
                                       int sb_cols, int plane);

internal static partial class restoration
{
    // <gen:consts>
    internal const int AOM_RESTORATION_FRAME_BORDER = 32;
    internal const int RESTORATION_PROC_UNIT_SIZE = 64;
    internal const int RESTORATION_UNIT_OFFSET = 8;
    internal const int SGRPROJ_BORDER_VERT = 3;
    internal const int SGRPROJ_BORDER_HORZ = 3;
    internal const int WIENER_BORDER_VERT = 2;
    internal const int WIENER_HALFWIN = 3;
    internal const int WIENER_BORDER_HORZ = (WIENER_HALFWIN);
    internal const int RESTORATION_BORDER_VERT = (SGRPROJ_BORDER_VERT);
    internal const int RESTORATION_BORDER_HORZ = (SGRPROJ_BORDER_HORZ);
    internal const int RESTORATION_BORDER = 3;
    internal const int RESTORATION_CTX_VERT = 2;
    internal const int RESTORATION_EXTRA_HORZ = 4;
    internal const int RESTORATION_PADDING = 20;
    internal const int RESTORATION_PROC_UNIT_PELS = ((RESTORATION_PROC_UNIT_SIZE + RESTORATION_BORDER_HORZ * 2 + RESTORATION_PADDING) * (RESTORATION_PROC_UNIT_SIZE + RESTORATION_BORDER_VERT * 2 + RESTORATION_PADDING));
    internal const int RESTORATION_UNITSIZE_MAX = 256;
    internal const int RESTORATION_UNITPELS_HORZ_MAX = (RESTORATION_UNITSIZE_MAX * 3 / 2 + 2 * RESTORATION_BORDER_HORZ + 16);
    internal const int RESTORATION_UNITPELS_VERT_MAX = ((RESTORATION_UNITSIZE_MAX * 3 / 2 + 2 * RESTORATION_BORDER_VERT + RESTORATION_UNIT_OFFSET));
    internal const int RESTORATION_UNITPELS_MAX = (RESTORATION_UNITPELS_HORZ_MAX * RESTORATION_UNITPELS_VERT_MAX);
    internal const int SGRPROJ_EXTBUF_SIZE = (0);
    internal const int SGRPROJ_PARAMS_BITS = 4;
    internal const int SGRPROJ_PARAMS = (1 << SGRPROJ_PARAMS_BITS);
    internal const int SGRPROJ_PRJ_BITS = 7;
    internal const int SGRPROJ_RST_BITS = 4;
    internal const int SGRPROJ_SGR_BITS = 8;
    internal const int SGRPROJ_SGR = (1 << SGRPROJ_SGR_BITS);
    internal const int SGRPROJ_PRJ_MIN0 = (-(1 << SGRPROJ_PRJ_BITS) * 3 / 4);
    internal const int SGRPROJ_PRJ_MAX0 = (SGRPROJ_PRJ_MIN0 + (1 << SGRPROJ_PRJ_BITS) - 1);
    internal const int SGRPROJ_PRJ_MIN1 = (-(1 << SGRPROJ_PRJ_BITS) / 4);
    internal const int SGRPROJ_PRJ_MAX1 = (SGRPROJ_PRJ_MIN1 + (1 << SGRPROJ_PRJ_BITS) - 1);
    internal const int SGRPROJ_PRJ_SUBEXP_K = 4;
    internal const int SGRPROJ_BITS = (SGRPROJ_PRJ_BITS * 2 + SGRPROJ_PARAMS_BITS);
    internal const int MAX_RADIUS = 2;
    internal const int MAX_NELEM = ((2 * MAX_RADIUS + 1) * (2 * MAX_RADIUS + 1));
    internal const int SGRPROJ_MTABLE_BITS = 20;
    internal const int SGRPROJ_RECIP_BITS = 12;
    internal const int WIENER_HALFWIN1 = (WIENER_HALFWIN + 1);
    internal const int WIENER_WIN = (2 * WIENER_HALFWIN + 1);
    internal const int WIENER_WIN2 = ((WIENER_WIN) * (WIENER_WIN));
    internal const int WIENER_TMPBUF_SIZE = (0);
    internal const int WIENER_EXTBUF_SIZE = (0);
    internal const int WIENER_WIN_CHROMA = (WIENER_WIN - 2);
    internal const int WIENER_WIN_REDUCED = (WIENER_WIN - 2);
    internal const int WIENER_WIN2_CHROMA = ((WIENER_WIN_CHROMA) * (WIENER_WIN_CHROMA));
    internal const int WIENER_STATS_DOWNSAMPLE_FACTOR = 4;
    internal const int WIENER_FILT_PREC_BITS = 7;
    internal const int WIENER_FILT_STEP = (1 << WIENER_FILT_PREC_BITS);
    internal const int WIENER_FILT_TAP0_MIDV = (3);
    internal const int WIENER_FILT_TAP1_MIDV = (-7);
    internal const int WIENER_FILT_TAP2_MIDV = (15);
    internal const int WIENER_FILT_TAP3_MIDV = (WIENER_FILT_STEP - 2 * (WIENER_FILT_TAP0_MIDV + WIENER_FILT_TAP1_MIDV + WIENER_FILT_TAP2_MIDV));
    internal const int WIENER_FILT_TAP0_BITS = 4;
    internal const int WIENER_FILT_TAP1_BITS = 5;
    internal const int WIENER_FILT_TAP2_BITS = 6;
    internal const int WIENER_FILT_BITS = ((WIENER_FILT_TAP0_BITS + WIENER_FILT_TAP1_BITS + WIENER_FILT_TAP2_BITS) * 2);
    internal const int WIENER_FILT_TAP0_MINV = (WIENER_FILT_TAP0_MIDV - (1 << WIENER_FILT_TAP0_BITS) / 2);
    internal const int WIENER_FILT_TAP1_MINV = (WIENER_FILT_TAP1_MIDV - (1 << WIENER_FILT_TAP1_BITS) / 2);
    internal const int WIENER_FILT_TAP2_MINV = (WIENER_FILT_TAP2_MIDV - (1 << WIENER_FILT_TAP2_BITS) / 2);
    internal const int WIENER_FILT_TAP0_MAXV = (WIENER_FILT_TAP0_MIDV - 1 + (1 << WIENER_FILT_TAP0_BITS) / 2);
    internal const int WIENER_FILT_TAP1_MAXV = (WIENER_FILT_TAP1_MIDV - 1 + (1 << WIENER_FILT_TAP1_BITS) / 2);
    internal const int WIENER_FILT_TAP2_MAXV = (WIENER_FILT_TAP2_MIDV - 1 + (1 << WIENER_FILT_TAP2_BITS) / 2);
    internal const int WIENER_FILT_TAP0_SUBEXP_K = 1;
    internal const int WIENER_FILT_TAP1_SUBEXP_K = 2;
    internal const int WIENER_FILT_TAP2_SUBEXP_K = 3;
    internal const int RESTORATION_EXTBUF_SIZE = (WIENER_EXTBUF_SIZE);
    internal const int RESTORATION_LINEBUFFER_WIDTH = (RESTORATION_UNITSIZE_MAX * 3 / 2 + 2 * RESTORATION_EXTRA_HORZ);
    // </gen:consts>

    // PORT-NOTE: a size in bytes, as in C (rst_tmpbuf is allocated with
    // aom_memalign(16, RESTORATION_TMPBUF_SIZE) and used as int32_t *).
    internal const int SGRPROJ_TMPBUF_SIZE = (RESTORATION_UNITPELS_MAX * 2 * sizeof(int));
    internal const int RESTORATION_TMPBUF_SIZE = (SGRPROJ_TMPBUF_SIZE);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CLIP(int x, int lo, int hi) => ((x) < (lo) ? (lo) : (x) > (hi) ? (hi) : (x));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RINT(double x) => ((x) < 0 ? (int)((x) - 0.5) : (int)((x) + 0.5));

    // av1_sgr_params, sgrproj_mtable, av1_x_by_xplus1 and av1_one_by_x are defined in
    // restoration.c (not part of p1a).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_default_sgrproj(ref SgrprojInfo sgrproj_info)
    {
        sgrproj_info.xqd[0] = (SGRPROJ_PRJ_MIN0 + SGRPROJ_PRJ_MAX0) / 2;
        sgrproj_info.xqd[1] = (SGRPROJ_PRJ_MIN1 + SGRPROJ_PRJ_MAX1) / 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_default_wiener(ref WienerInfo wiener_info)
    {
        wiener_info.vfilter[0] = wiener_info.hfilter[0] = WIENER_FILT_TAP0_MIDV;
        wiener_info.vfilter[1] = wiener_info.hfilter[1] = WIENER_FILT_TAP1_MIDV;
        wiener_info.vfilter[2] = wiener_info.hfilter[2] = WIENER_FILT_TAP2_MIDV;
        wiener_info.vfilter[WIENER_HALFWIN] = wiener_info.hfilter[WIENER_HALFWIN] =
            -2 *
            (WIENER_FILT_TAP2_MIDV + WIENER_FILT_TAP1_MIDV + WIENER_FILT_TAP0_MIDV);
        wiener_info.vfilter[4] = wiener_info.hfilter[4] = WIENER_FILT_TAP2_MIDV;
        wiener_info.vfilter[5] = wiener_info.hfilter[5] = WIENER_FILT_TAP1_MIDV;
        wiener_info.vfilter[6] = wiener_info.hfilter[6] = WIENER_FILT_TAP0_MIDV;
    }
}
