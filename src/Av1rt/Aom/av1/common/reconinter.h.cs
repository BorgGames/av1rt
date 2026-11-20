// Port of libaom av1/common/reconinter.h

// <gen:aliases>
global using WedgeDirectionType = int;
global using InterPredMode = int;
global using InterCompMode = int;
// </gen:aliases>
// [port] typedef uint32_t wedge_masks_type[MAX_WEDGE_TYPES];
global using wedge_masks_type = uint[];

global using static Av1rt.Aom.reconinter;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal partial struct wedge_code_type
{
    public WedgeDirectionType direction;
    public int x_offset;
    public int y_offset;
}

internal partial struct wedge_params_type
{
    public int wedge_types;
    public Ptr<wedge_code_type> codebook;
    public Ptr<byte> signflip;
    // [port] const wedge_masks_type *masks: C points it at wedge_masks[bsize], a
    // wedge_masks_type[2] row (indexed masks[flipsign][wedge_index]); the port keeps that row.
    public wedge_masks_type[] masks;
}

internal partial struct SubpelParams
{
    public int xs;
    public int ys;
    public int subpel_x;
    public int subpel_y;
    public int pos_x;
    public int pos_y;
}

internal partial struct build_prediction_ctxt
{
    public AV1_COMMON cm;
    public Ptr<Ptr<byte>> tmp_buf;
    public Ptr<int> tmp_width;
    public Ptr<int> tmp_height;
    public Ptr<int> tmp_stride;
    public int mb_to_far_edge;
    public object dcb;  // Decoder-only coding block.
}

// [port] A struct, not a class: C declares it as a local in the per-block prediction paths and
// passes its address, so callers hold it on the stack and pass `ref` (no per-call allocation).
internal partial struct InterPredParams
{
    public InterPredMode mode;
    public InterCompMode comp_mode;
    public WarpedMotionParams warp_params;
    public ConvolveParams conv_params;
    // PORT-NOTE: const InterpFilterParams *interp_filter_params[2] is an Arr2 of class
    // references; functions taking `const InterpFilterParams *[2]` take ReadOnlySpan /
    // Span<InterpFilterParams> (the inline array converts implicitly).
    public Arr2<InterpFilterParams> interp_filter_params;
    public int block_width;
    public int block_height;
    public int pix_row;
    public int pix_col;
    public buf_2d ref_frame_buf;
    public int subsampling_x;
    public int subsampling_y;
    public scale_factors scale_factors;
    public int bit_depth;
    public int use_hbd_buf;
    public INTERINTER_COMPOUND_DATA mask_comp;
    public BLOCK_SIZE sb_type;
    public int is_intrabc;
    public int top;
    public int left;
}

internal static partial class reconinter
{
    // <gen:consts>
    internal const int MAX_WEDGE_TYPES = 16;
    internal const int MAX_WEDGE_SIZE_LOG2 = 5;
    internal const int MAX_WEDGE_SIZE = (1 << MAX_WEDGE_SIZE_LOG2);
    internal const int MAX_WEDGE_SQUARE = (MAX_WEDGE_SIZE * MAX_WEDGE_SIZE);
    internal const int WEDGE_WEIGHT_BITS = 6;
    internal const int WEDGE_NONE = -1;
    internal const int MASK_MASTER_SIZE = ((MAX_WEDGE_SIZE) << 1);
    internal const int MASK_MASTER_STRIDE = (MASK_MASTER_SIZE);

    // WedgeDirectionType
    internal const int WEDGE_HORIZONTAL = 0;
    internal const int WEDGE_VERTICAL = 1;
    internal const int WEDGE_OBLIQUE27 = 2;
    internal const int WEDGE_OBLIQUE63 = 3;
    internal const int WEDGE_OBLIQUE117 = 4;
    internal const int WEDGE_OBLIQUE153 = 5;
    internal const int WEDGE_DIRECTIONS = 6;

    // InterPredMode
    internal const int TRANSLATION_PRED = 0;
    internal const int WARP_PRED = 1;

    // InterCompMode
    internal const int UNIFORM_SINGLE = 0;
    internal const int UNIFORM_COMP = 1;
    internal const int MASK_COMP = 2;
    // </gen:consts>

    // Work out how many pixels off the edge of a reference frame we're allowed
    // to go when forming an inter prediction.
    // The outermost row/col of each referernce frame is extended by
    // (AOM_BORDER_IN_PIXELS >> subsampling) pixels, but we need to keep
    // at least AOM_INTERP_EXTEND pixels within that to account for filtering.
    //
    // We have to break this up into two macros to keep both clang-format and
    // tools/lint-hunks.py happy.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AOM_LEFT_TOP_MARGIN_PX(int subsampling) =>
        ((AOM_BORDER_IN_PIXELS >> subsampling) - AOM_INTERP_EXTEND);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AOM_LEFT_TOP_MARGIN_SCALED(int subsampling) =>
        (AOM_LEFT_TOP_MARGIN_PX(subsampling) << SCALE_SUBPEL_BITS);

    // Initialize sub-pel params required for inter prediction.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void init_subpel_params(in MV src_mv,
                                            ref InterPredParams inter_pred_params,
                                            ref SubpelParams subpel_params, int width,
                                            int height)
    {
        scale_factors sf = inter_pred_params.scale_factors;
        int ssx = inter_pred_params.subsampling_x;
        int ssy = inter_pred_params.subsampling_y;
        int orig_pos_y = inter_pred_params.pix_row << SUBPEL_BITS;
        orig_pos_y += src_mv.row * (1 << (1 - ssy));
        int orig_pos_x = inter_pred_params.pix_col << SUBPEL_BITS;
        orig_pos_x += src_mv.col * (1 << (1 - ssx));
        int is_scaled = av1_is_scaled(sf);
        int pos_x, pos_y;
        if (is_scaled == 0)
        {
            pos_y = av1_unscaled_value(orig_pos_y, sf);
            pos_x = av1_unscaled_value(orig_pos_x, sf);
        }
        else
        {
            pos_y = av1_scaled_y(orig_pos_y, sf);
            pos_x = av1_scaled_x(orig_pos_x, sf);
        }

        pos_x += SCALE_EXTRA_OFF;
        pos_y += SCALE_EXTRA_OFF;

        int bottom = (height + AOM_INTERP_EXTEND) << SCALE_SUBPEL_BITS;
        int right = (width + AOM_INTERP_EXTEND) << SCALE_SUBPEL_BITS;
        pos_y = clamp(pos_y, inter_pred_params.top, bottom);
        pos_x = clamp(pos_x, inter_pred_params.left, right);

        subpel_params.pos_x = pos_x;
        subpel_params.pos_y = pos_y;
        subpel_params.subpel_x = pos_x & SCALE_SUBPEL_MASK;
        subpel_params.subpel_y = pos_y & SCALE_SUBPEL_MASK;
        subpel_params.xs = sf.x_step_q4;
        subpel_params.ys = sf.y_step_q4;
    }

    // Initialize interp filter required for inter prediction.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void init_interp_filter_params(
        Span<InterpFilterParams> interp_filter_params,
        in InterpFilters filter, int block_width, int block_height,
        int is_intrabc)
    {
        if (is_intrabc != 0)
        {
            interp_filter_params[0] = av1_intrabc_filter_params;
            interp_filter_params[1] = av1_intrabc_filter_params;
        }
        else
        {
            interp_filter_params[0] = av1_get_interp_filter_params_with_block_size(
                (InterpFilter)filter.x_filter, block_width);
            interp_filter_params[1] = av1_get_interp_filter_params_with_block_size(
                (InterpFilter)filter.y_filter, block_height);
        }
    }

    // Initialize parameters required for inter prediction.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void init_inter_mode_params(
        in MV src_mv, ref InterPredParams inter_pred_params,
        ref SubpelParams subpel_params, scale_factors sf, int width,
        int height)
    {
        inter_pred_params.scale_factors = sf;
        init_subpel_params(in src_mv, ref inter_pred_params, ref subpel_params, width, height);
    }

    // Initialize parameters required for inter prediction.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void init_inter_block_params(ref InterPredParams inter_pred_params,
                                                 int block_width, int block_height,
                                                 int pix_row, int pix_col,
                                                 int subsampling_x, int subsampling_y,
                                                 int bit_depth, int use_hbd_buf,
                                                 int is_intrabc)
    {
        inter_pred_params.block_width = block_width;
        inter_pred_params.block_height = block_height;
        inter_pred_params.pix_row = pix_row;
        inter_pred_params.pix_col = pix_col;
        inter_pred_params.subsampling_x = subsampling_x;
        inter_pred_params.subsampling_y = subsampling_y;
        inter_pred_params.bit_depth = bit_depth;
        inter_pred_params.use_hbd_buf = use_hbd_buf;
        inter_pred_params.is_intrabc = is_intrabc;
        inter_pred_params.mode = TRANSLATION_PRED;
        inter_pred_params.comp_mode = UNIFORM_SINGLE;
        inter_pred_params.top = -AOM_LEFT_TOP_MARGIN_SCALED(subsampling_y);
        inter_pred_params.left = -AOM_LEFT_TOP_MARGIN_SCALED(subsampling_x);
    }

    // Initialize params required for inter prediction.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_init_inter_params(
        ref InterPredParams inter_pred_params, int block_width, int block_height,
        int pix_row, int pix_col, int subsampling_x, int subsampling_y,
        int bit_depth, int use_hbd_buf, int is_intrabc,
        scale_factors sf, in buf_2d ref_buf,
        int_interpfilters interp_filters)
    {
        init_inter_block_params(ref inter_pred_params, block_width, block_height, pix_row,
                                pix_col, subsampling_x, subsampling_y, bit_depth,
                                use_hbd_buf, is_intrabc);
        init_interp_filter_params(inter_pred_params.interp_filter_params,
                                  in interp_filters.as_filters, block_width,
                                  block_height, is_intrabc);
        inter_pred_params.scale_factors = sf;
        inter_pred_params.ref_frame_buf = ref_buf;
    }

    // [subset] av1_init_comp_mode: compound prediction is disabled (never executed).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int has_scale(int xs, int ys)
    {
        return B(xs != SCALE_SUBPEL_SHIFTS || ys != SCALE_SUBPEL_SHIFTS);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void revert_scale_extra_bits(ref SubpelParams sp)
    {
        sp.subpel_x >>= SCALE_EXTRA_BITS;
        sp.subpel_y >>= SCALE_EXTRA_BITS;
        sp.xs >>= SCALE_EXTRA_BITS;
        sp.ys >>= SCALE_EXTRA_BITS;
        Debug.Assert(sp.subpel_x < SUBPEL_SHIFTS);
        Debug.Assert(sp.subpel_y < SUBPEL_SHIFTS);
        Debug.Assert(sp.xs <= SUBPEL_SHIFTS);
        Debug.Assert(sp.ys <= SUBPEL_SHIFTS);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void inter_predictor(
        Ptr<byte> src, int src_stride, Ptr<byte> dst, int dst_stride,
        in SubpelParams subpel_params, int w, int h,
        ref ConvolveParams conv_params, ReadOnlySpan<InterpFilterParams> interp_filters)
    {
        Debug.Assert(conv_params.do_average == 0 || conv_params.do_average == 1);
        int is_scaled = has_scale(subpel_params.xs, subpel_params.ys);
        if (is_scaled != 0)
        {
            av1_convolve_2d_facade(src, src_stride, dst, dst_stride, w, h,
                                   interp_filters, subpel_params.subpel_x,
                                   subpel_params.xs, subpel_params.subpel_y,
                                   subpel_params.ys, 1, ref conv_params);
        }
        else
        {
            SubpelParams sp = subpel_params;
            revert_scale_extra_bits(ref sp);
            av1_convolve_2d_facade(src, src_stride, dst, dst_stride, w, h,
                                   interp_filters, sp.subpel_x, sp.xs, sp.subpel_y,
                                   sp.ys, 0, ref conv_params);
        }
    }

    // [subset] highbd_inter_predictor: high bitdepth is compiled out.
    // [subset] is_interinter_compound_used, is_any_masked_compound_used,
    // get_wedge_types_lookup, clamp_mv_to_umv_border_sb: never executed (compound and
    // OBMC/warp paths are disabled in the realtime subset).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_is_wedge_used(BLOCK_SIZE sb_type)
    {
        return B(av1_wedge_params_lookup[sb_type].wedge_types > 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long scaled_buffer_offset(int x_offset, int y_offset,
                                              int stride,
                                              scale_factors sf)
    {
        int x, y;
        if (sf == null)
        {
            x = x_offset;
            y = y_offset;
        }
        else if (av1_is_scaled(sf) != 0)
        {
            x = av1_scaled_x(x_offset, sf) >> SCALE_EXTRA_BITS;
            y = av1_scaled_y(y_offset, sf) >> SCALE_EXTRA_BITS;
        }
        else
        {
            x = av1_unscaled_value(x_offset, sf) >> SCALE_EXTRA_BITS;
            y = av1_unscaled_value(y_offset, sf) >> SCALE_EXTRA_BITS;
        }
        return (long)y * stride + x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void setup_pred_plane(ref buf_2d dst, BLOCK_SIZE bsize,
                                          Ptr<byte> src, int width, int height,
                                          int stride, int mi_row, int mi_col,
                                          scale_factors scale,
                                          int subsampling_x, int subsampling_y)
    {
        // Offset the buffer pointer
        if (subsampling_y != 0 && (mi_row & 0x01) != 0 && (mi_size_high[bsize] == 1))
            mi_row -= 1;
        if (subsampling_x != 0 && (mi_col & 0x01) != 0 && (mi_size_wide[bsize] == 1))
            mi_col -= 1;

        int x = (MI_SIZE * mi_col) >> subsampling_x;
        int y = (MI_SIZE * mi_row) >> subsampling_y;
        dst.buf = src + (int)scaled_buffer_offset(x, y, stride, scale);
        dst.buf0 = src;
        dst.width = width;
        dst.height = height;
        dst.stride = stride;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_default_interp_filters(
        MB_MODE_INFO mbmi, InterpFilter frame_interp_filter)
    {
        mbmi.interp_filters =
            av1_broadcast_interp_filter(av1_unswitchable_filter(frame_interp_filter));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_is_interp_needed(MACROBLOCKD xd)
    {
        MB_MODE_INFO mbmi = xd.mi[0];
        if (mbmi.skip_mode != 0) return 0;
        if (mbmi.motion_mode == WARPED_CAUSAL) return 0;
        if (is_nontrans_global_motion(xd, xd.mi[0]) != 0) return 0;
        return 1;
    }
}
