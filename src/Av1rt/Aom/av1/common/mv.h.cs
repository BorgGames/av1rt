// Port of libaom av1/common/mv.h

global using static Av1rt.Aom.mv;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Av1rt.Aom;

// The motion vector in units of full pixel
internal partial struct FULLPEL_MV
{
    public short row;
    public short col;

    public FULLPEL_MV(short row, short col)
    {
        this.row = row;
        this.col = col;
    }
}

// The motion vector in units of 1/8-pel
internal partial struct MV
{
    public short row;
    public short col;

    public MV(short row, short col)
    {
        this.row = row;
        this.col = col;
    }
}

/* facilitates faster equality tests and copies */
[StructLayout(LayoutKind.Explicit)]
internal partial struct int_mv
{
    [FieldOffset(0)] public uint as_int;
    [FieldOffset(0)] public MV as_mv;
    [FieldOffset(0)] public FULLPEL_MV as_fullmv;
}

internal partial struct MV32
{
    public int row;
    public int col;
}

// The mv limit for fullpel mvs
internal partial struct FullMvLimits
{
    public int col_min;
    public int col_max;
    public int row_min;
    public int row_max;
}

// The mv limit for subpel mvs
internal partial struct SubpelMvLimits
{
    public int col_min;
    public int col_max;
    public int row_min;
    public int row_max;
}

internal partial struct WarpTypesAllowed
{
    public int global_warp_allowed;
    public int local_warp_allowed;
}

// The order of values in the wmmat matrix below is best described
// by the affine transformation:
//      [x'     (m2 m3 m0   [x
//  z .  y'  =   m4 m5 m1 *  y
//       1]       0  0 1)    1]
internal partial struct WarpedMotionParams
{
    public Arr6<int> wmmat; // [MAX_PARAMDIM]
    public short alpha, beta, gamma, delta;
    public TransformationType wmtype;
    public sbyte invalid;
}

internal partial struct CANDIDATE_MV
{
    public int_mv this_mv;
    public int_mv comp_mv;
}

internal static partial class mv
{
    // <gen:consts>
    internal const uint INVALID_MV = 0x80008000;
    internal const int INVALID_MV_ROW_COL = -32768;
    internal const int WARPEDMODEL_PREC_BITS = 16;
    internal const int WARPEDMODEL_TRANS_CLAMP = (128 << WARPEDMODEL_PREC_BITS);
    internal const int WARPEDMODEL_NONDIAGAFFINE_CLAMP = (1 << (WARPEDMODEL_PREC_BITS - 3));
    internal const int WARPEDPIXEL_PREC_BITS = 6;
    internal const int WARPEDPIXEL_PREC_SHIFTS = (1 << WARPEDPIXEL_PREC_BITS);
    internal const int WARP_PARAM_REDUCE_BITS = 6;
    internal const int WARPEDDIFF_PREC_BITS = (WARPEDMODEL_PREC_BITS - WARPEDPIXEL_PREC_BITS);
    internal const int SUBEXPFIN_K = 3;
    internal const int GM_TRANS_PREC_BITS = 6;
    internal const int GM_ABS_TRANS_BITS = 12;
    internal const int GM_ABS_TRANS_ONLY_BITS = (GM_ABS_TRANS_BITS - GM_TRANS_PREC_BITS + 3);
    internal const int GM_TRANS_PREC_DIFF = (WARPEDMODEL_PREC_BITS - GM_TRANS_PREC_BITS);
    internal const int GM_TRANS_ONLY_PREC_DIFF = (WARPEDMODEL_PREC_BITS - 3);
    internal const int GM_TRANS_DECODE_FACTOR = (1 << GM_TRANS_PREC_DIFF);
    internal const int GM_TRANS_ONLY_DECODE_FACTOR = (1 << GM_TRANS_ONLY_PREC_DIFF);
    internal const int GM_ALPHA_PREC_BITS = 15;
    internal const int GM_ABS_ALPHA_BITS = 12;
    internal const int GM_ALPHA_PREC_DIFF = (WARPEDMODEL_PREC_BITS - GM_ALPHA_PREC_BITS);
    internal const int GM_ALPHA_DECODE_FACTOR = (1 << GM_ALPHA_PREC_DIFF);
    internal const int GM_TRANS_MAX = (1 << GM_ABS_TRANS_BITS);
    internal const int GM_ALPHA_MAX = (1 << GM_ABS_ALPHA_BITS);
    internal const int GM_TRANS_MIN = -GM_TRANS_MAX;
    internal const int GM_ALPHA_MIN = -GM_ALPHA_MAX;
    // </gen:consts>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GET_MV_RAWPEL(int x) => (x + 3 + B(x >= 0)) >> 3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GET_MV_SUBPEL(int x) => x * 8;

    // MARK_MV_INVALID(mv): ((int_mv *)(mv))->as_int = INVALID_MV
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MARK_MV_INVALID(ref int_mv mv) => mv.as_int = INVALID_MV;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MARK_MV_INVALID(ref MV mv)
    {
        mv.row = INVALID_MV_ROW_COL;
        mv.col = INVALID_MV_ROW_COL;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CHECK_MV_EQUAL(MV x, MV y) => (x.row == y.row) && (x.col == y.col);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CHECK_MV_EQUAL(FULLPEL_MV x, FULLPEL_MV y) => (x.row == y.row) && (x.col == y.col);

    internal static readonly MV kZeroMv = new(0, 0);
    internal static readonly FULLPEL_MV kZeroFullMv = new(0, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FULLPEL_MV get_fullmv_from_mv(in MV subpel_mv)
    {
        FULLPEL_MV full_mv = new((short)GET_MV_RAWPEL(subpel_mv.row),
                                 (short)GET_MV_RAWPEL(subpel_mv.col));
        return full_mv;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MV get_mv_from_fullmv(in FULLPEL_MV full_mv)
    {
        MV subpel_mv = new((short)GET_MV_SUBPEL(full_mv.row),
                           (short)GET_MV_SUBPEL(full_mv.col));
        return subpel_mv;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void convert_fullmv_to_mv(ref int_mv mv)
    {
        mv.as_mv = get_mv_from_fullmv(in mv.as_fullmv);
    }

    internal static readonly WarpedMotionParams default_warp_params = make_default_warp_params();

    private static WarpedMotionParams make_default_warp_params()
    {
        WarpedMotionParams p = default;
        p.wmmat[0] = 0;
        p.wmmat[1] = 0;
        p.wmmat[2] = (1 << WARPEDMODEL_PREC_BITS);
        p.wmmat[3] = 0;
        p.wmmat[4] = 0;
        p.wmmat[5] = (1 << WARPEDMODEL_PREC_BITS);
        p.alpha = 0;
        p.beta = 0;
        p.gamma = 0;
        p.delta = 0;
        p.wmtype = IDENTITY;
        p.invalid = 0;
        return p;
    }

    // Convert a global motion vector into a motion vector at the centre of the
    // given block.
    //
    // The resulting motion vector will have three fractional bits of precision. If
    // allow_hp is zero, the bottom bit will always be zero. If CONFIG_AMVR and
    // is_integer is true, the bottom three bits will be zero (so the motion vector
    // represents an integer)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int_mv gm_get_motion_vector(in WarpedMotionParams gm,
                                                int allow_hp, BLOCK_SIZE bsize,
                                                int mi_col, int mi_row,
                                                int is_integer)
    {
        int_mv res = default;

        if (gm.wmtype == IDENTITY)
        {
            res.as_int = 0;
            return res;
        }

        // [subset] Global motion is disabled, so every frame's global motion is IDENTITY.
        // The TRANSLATION/ROTZOOM/AFFINE paths (block_center_x/y, convert_to_trans_prec)
        // never run and are not ported.
        throw new NotSupportedException("gm_get_motion_vector: non-identity global motion");
    }

    // [port] Never executed (integer MVs are not used in the realtime subset); ported because
    // lower_mv_precision (mvref_common.h) calls it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void integer_mv_precision(ref MV mv)
    {
        int mod = (mv.row % 8);
        if (mod != 0)
        {
            mv.row -= (short)mod;
            if (Math.Abs(mod) > 4)
            {
                if (mod > 0)
                {
                    mv.row += 8;
                }
                else
                {
                    mv.row -= 8;
                }
            }
        }

        mod = (mv.col % 8);
        if (mod != 0)
        {
            mv.col -= (short)mod;
            if (Math.Abs(mod) > 4)
            {
                if (mod > 0)
                {
                    mv.col += 8;
                }
                else
                {
                    mv.col -= 8;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void clamp_mv(ref MV mv, in SubpelMvLimits mv_limits)
    {
        mv.col = (short)clamp(mv.col, mv_limits.col_min, mv_limits.col_max);
        mv.row = (short)clamp(mv.row, mv_limits.row_min, mv_limits.row_max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void clamp_fullmv(ref FULLPEL_MV mv, in FullMvLimits mv_limits)
    {
        mv.col = (short)clamp(mv.col, mv_limits.col_min, mv_limits.col_max);
        mv.row = (short)clamp(mv.row, mv_limits.row_min, mv_limits.row_max);
    }
}
