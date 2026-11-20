// Port of libaom av1/common/filter.h

// <gen:aliases>
global using InterpFilter = int;
global using SUBPEL_SEARCH_TYPE = int;
global using INTERP_EVAL_PLANE = int;
global using INTERP_PRED_TYPE = int;
// </gen:aliases>

global using static Av1rt.Aom.filter;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Av1rt.Aom;

// Pack two InterpFilter's into a uint32_t: since there are at most 10 filters,
// we can use 16 bits for each and have more than enough space. This reduces
// argument passing and unifies the operation of setting a (pair of) filters.
internal partial struct InterpFilters
{
    public ushort y_filter;
    public ushort x_filter;
}

[StructLayout(LayoutKind.Explicit)]
internal partial struct int_interpfilters
{
    [FieldOffset(0)] public uint as_int;
    [FieldOffset(0)] public InterpFilters as_filters;
}

// [port] Class: the instances are the immutable tables below, always referenced through
// `const InterpFilterParams *` and never copied in scope.
internal sealed partial class InterpFilterParams
{
    public Ptr<short> filter_ptr;
    public ushort taps;
    public InterpFilter interp_filter;

    public InterpFilterParams(Ptr<short> filter_ptr, ushort taps, InterpFilter interp_filter)
    {
        this.filter_ptr = filter_ptr;
        this.taps = taps;
        this.interp_filter = interp_filter;
    }
}

internal static partial class filter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static InterpFilter av1_extract_interp_filter(int_interpfilters filters,
                                                           int dir)
    {
        return (InterpFilter)((dir != 0) ? filters.as_filters.x_filter
                                         : filters.as_filters.y_filter);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int_interpfilters av1_broadcast_interp_filter(
        InterpFilter filter)
    {
        int_interpfilters filters = default;
        filters.as_filters.x_filter = (ushort)filter;
        filters.as_filters.y_filter = (ushort)filter;
        return filters;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static InterpFilter av1_unswitchable_filter(InterpFilter filter)
    {
        return filter == SWITCHABLE ? EIGHTTAP_REGULAR : filter;
    }

    // [port] The InterpKernel tables (int16_t[SUBPEL_SHIFTS][SUBPEL_TAPS]) and
    // av1_sub_pel_filters_12sharp are flattened to short[] so that filter_ptr can point into
    // them, exactly like the C casts `(const int16_t *)av1_sub_pel_filters_8`.
    // <gen:consts>
    internal const int MAX_FILTER_TAP = 12;
    internal const int LOG_SWITCHABLE_FILTERS = 2;
    internal const int SWITCHABLE_FILTER_CONTEXTS = ((SWITCHABLE_FILTERS + 1) * 4);
    internal const int INTER_FILTER_COMP_OFFSET = (SWITCHABLE_FILTERS + 1);
    internal const int INTER_FILTER_DIR_OFFSET = ((SWITCHABLE_FILTERS + 1) * 2);
    internal const int ALLOW_ALL_INTERP_FILT_MASK = (0x01ff);

    // InterpFilter
    internal const int EIGHTTAP_REGULAR = 0;
    internal const int EIGHTTAP_SMOOTH = 1;
    internal const int MULTITAP_SHARP = 2;
    internal const int BILINEAR = 3;
    internal const int MULTITAP_SHARP2 = 4;
    internal const int INTERP_FILTERS_ALL = 5;
    internal const int SWITCHABLE_FILTERS = BILINEAR;
    internal const int SWITCHABLE = SWITCHABLE_FILTERS + 1;
    internal const int EXTRA_FILTERS = INTERP_FILTERS_ALL - SWITCHABLE_FILTERS;
    internal const int INTERP_INVALID = 0xff;

    // SUBPEL_SEARCH_TYPE
    internal const int USE_2_TAPS_ORIG = 0;
    internal const int USE_2_TAPS = 1;
    internal const int USE_4_TAPS = 2;
    internal const int USE_8_TAPS = 3;

    // INTERP_EVAL_PLANE
    internal const int INTERP_EVAL_LUMA_EVAL_CHROMA = 0;
    internal const int INTERP_SKIP_LUMA_EVAL_CHROMA = 1;
    internal const int INTERP_EVAL_LUMA_SKIP_CHROMA = 2;
    internal const int INTERP_SKIP_LUMA_SKIP_CHROMA = 3;

    // INTERP_PRED_TYPE
    internal const int INTERP_HORZ_NEQ_VERT_NEQ = 0;
    internal const int INTERP_HORZ_EQ_VERT_NEQ = 1;
    internal const int INTERP_HORZ_NEQ_VERT_EQ = 2;
    internal const int INTERP_HORZ_EQ_VERT_EQ = 3;
    internal const int INTERP_PRED_TYPE_ALL = 4;

    // InterpKernel av1_bilinear_filters[SUBPEL_SHIFTS][SUBPEL_TAPS]
    internal static readonly short[] av1_bilinear_filters = {
        0, 0, 0, 128, 0, 0, 0, 0,
        0, 0, 0, 120, 8, 0, 0, 0,
        0, 0, 0, 112, 16, 0, 0, 0,
        0, 0, 0, 104, 24, 0, 0, 0,
        0, 0, 0, 96, 32, 0, 0, 0,
        0, 0, 0, 88, 40, 0, 0, 0,
        0, 0, 0, 80, 48, 0, 0, 0,
        0, 0, 0, 72, 56, 0, 0, 0,
        0, 0, 0, 64, 64, 0, 0, 0,
        0, 0, 0, 56, 72, 0, 0, 0,
        0, 0, 0, 48, 80, 0, 0, 0,
        0, 0, 0, 40, 88, 0, 0, 0,
        0, 0, 0, 32, 96, 0, 0, 0,
        0, 0, 0, 24, 104, 0, 0, 0,
        0, 0, 0, 16, 112, 0, 0, 0,
        0, 0, 0, 8, 120, 0, 0, 0
    };

    // InterpKernel av1_sub_pel_filters_8[SUBPEL_SHIFTS][SUBPEL_TAPS]
    internal static readonly short[] av1_sub_pel_filters_8 = {
        0, 0, 0, 128, 0, 0, 0, 0,
        0, 2, -6, 126, 8, -2, 0, 0,
        0, 2, -10, 122, 18, -4, 0, 0,
        0, 2, -12, 116, 28, -8, 2, 0,
        0, 2, -14, 110, 38, -10, 2, 0,
        0, 2, -14, 102, 48, -12, 2, 0,
        0, 2, -16, 94, 58, -12, 2, 0,
        0, 2, -14, 84, 66, -12, 2, 0,
        0, 2, -14, 76, 76, -14, 2, 0,
        0, 2, -12, 66, 84, -14, 2, 0,
        0, 2, -12, 58, 94, -16, 2, 0,
        0, 2, -12, 48, 102, -14, 2, 0,
        0, 2, -10, 38, 110, -14, 2, 0,
        0, 2, -8, 28, 116, -12, 2, 0,
        0, 0, -4, 18, 122, -10, 2, 0,
        0, 0, -2, 8, 126, -6, 2, 0
    };

    // InterpKernel av1_sub_pel_filters_8sharp[SUBPEL_SHIFTS][SUBPEL_TAPS]
    internal static readonly short[] av1_sub_pel_filters_8sharp = {
        0, 0, 0, 128, 0, 0, 0, 0,
        -2, 2, -6, 126, 8, -2, 2, 0,
        -2, 6, -12, 124, 16, -6, 4, -2,
        -2, 8, -18, 120, 26, -10, 6, -2,
        -4, 10, -22, 116, 38, -14, 6, -2,
        -4, 10, -22, 108, 48, -18, 8, -2,
        -4, 10, -24, 100, 60, -20, 8, -2,
        -4, 10, -24, 90, 70, -22, 10, -2,
        -4, 12, -24, 80, 80, -24, 12, -4,
        -2, 10, -22, 70, 90, -24, 10, -4,
        -2, 8, -20, 60, 100, -24, 10, -4,
        -2, 8, -18, 48, 108, -22, 10, -4,
        -2, 6, -14, 38, 116, -22, 10, -4,
        -2, 6, -10, 26, 120, -18, 8, -2,
        -2, 4, -6, 16, 124, -12, 6, -2,
        0, 2, -2, 8, 126, -6, 2, -2
    };

    // InterpKernel av1_sub_pel_filters_8smooth[SUBPEL_SHIFTS][SUBPEL_TAPS]
    internal static readonly short[] av1_sub_pel_filters_8smooth = {
        0, 0, 0, 128, 0, 0, 0, 0,
        0, 2, 28, 62, 34, 2, 0, 0,
        0, 0, 26, 62, 36, 4, 0, 0,
        0, 0, 22, 62, 40, 4, 0, 0,
        0, 0, 20, 60, 42, 6, 0, 0,
        0, 0, 18, 58, 44, 8, 0, 0,
        0, 0, 16, 56, 46, 10, 0, 0,
        0, -2, 16, 54, 48, 12, 0, 0,
        0, -2, 14, 52, 52, 14, -2, 0,
        0, 0, 12, 48, 54, 16, -2, 0,
        0, 0, 10, 46, 56, 16, 0, 0,
        0, 0, 8, 44, 58, 18, 0, 0,
        0, 0, 6, 42, 60, 20, 0, 0,
        0, 0, 4, 40, 62, 22, 0, 0,
        0, 0, 4, 36, 62, 26, 0, 0,
        0, 0, 2, 34, 62, 28, 2, 0
    };

    // int16_t av1_sub_pel_filters_12sharp[SUBPEL_SHIFTS][12]
    internal static readonly short[] av1_sub_pel_filters_12sharp = {
        0, 0, 0, 0, 0, 128, 0, 0, 0, 0, 0, 0,
        0, 1, -2, 3, -7, 127, 8, -4, 2, -1, 1, 0,
        -1, 2, -3, 6, -13, 124, 18, -8, 4, -2, 2, -1,
        -1, 3, -4, 8, -18, 120, 28, -12, 7, -4, 2, -1,
        -1, 3, -6, 10, -21, 115, 38, -15, 8, -5, 3, -1,
        -2, 4, -6, 12, -24, 108, 49, -18, 10, -6, 3, -2,
        -2, 4, -7, 13, -25, 100, 60, -21, 11, -7, 4, -2,
        -2, 4, -7, 13, -26, 91, 71, -24, 13, -7, 4, -2,
        -2, 4, -7, 13, -25, 81, 81, -25, 13, -7, 4, -2,
        -2, 4, -7, 13, -24, 71, 91, -26, 13, -7, 4, -2,
        -2, 4, -7, 11, -21, 60, 100, -25, 13, -7, 4, -2,
        -2, 3, -6, 10, -18, 49, 108, -24, 12, -6, 4, -2,
        -1, 3, -5, 8, -15, 38, 115, -21, 10, -6, 3, -1,
        -1, 2, -4, 7, -12, 28, 120, -18, 8, -4, 3, -1,
        -1, 2, -2, 4, -8, 18, 124, -13, 6, -3, 2, -1,
        0, 1, -1, 2, -4, 8, 127, -7, 3, -2, 1, 0
    };

    // int16_t av1_intrabc_bilinear_filter[2 * SUBPEL_SHIFTS]
    internal static readonly short[] av1_intrabc_bilinear_filter = {
        128, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 64, 64, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0
    };

    // InterpKernel av1_sub_pel_filters_4[SUBPEL_SHIFTS][SUBPEL_TAPS]
    internal static readonly short[] av1_sub_pel_filters_4 = {
        0, 0, 0, 128, 0, 0, 0, 0,
        0, 0, -4, 126, 8, -2, 0, 0,
        0, 0, -8, 122, 18, -4, 0, 0,
        0, 0, -10, 116, 28, -6, 0, 0,
        0, 0, -12, 110, 38, -8, 0, 0,
        0, 0, -12, 102, 48, -10, 0, 0,
        0, 0, -14, 94, 58, -10, 0, 0,
        0, 0, -12, 84, 66, -10, 0, 0,
        0, 0, -12, 76, 76, -12, 0, 0,
        0, 0, -10, 66, 84, -12, 0, 0,
        0, 0, -10, 58, 94, -14, 0, 0,
        0, 0, -10, 48, 102, -12, 0, 0,
        0, 0, -8, 38, 110, -12, 0, 0,
        0, 0, -6, 28, 116, -10, 0, 0,
        0, 0, -4, 18, 122, -8, 0, 0,
        0, 0, -2, 8, 126, -4, 0, 0
    };

    // InterpKernel av1_sub_pel_filters_4smooth[SUBPEL_SHIFTS][SUBPEL_TAPS]
    internal static readonly short[] av1_sub_pel_filters_4smooth = {
        0, 0, 0, 128, 0, 0, 0, 0,
        0, 0, 30, 62, 34, 2, 0, 0,
        0, 0, 26, 62, 36, 4, 0, 0,
        0, 0, 22, 62, 40, 4, 0, 0,
        0, 0, 20, 60, 42, 6, 0, 0,
        0, 0, 18, 58, 44, 8, 0, 0,
        0, 0, 16, 56, 46, 10, 0, 0,
        0, 0, 14, 54, 48, 12, 0, 0,
        0, 0, 12, 52, 52, 12, 0, 0,
        0, 0, 12, 48, 54, 14, 0, 0,
        0, 0, 10, 46, 56, 16, 0, 0,
        0, 0, 8, 44, 58, 18, 0, 0,
        0, 0, 6, 42, 60, 20, 0, 0,
        0, 0, 4, 40, 62, 22, 0, 0,
        0, 0, 4, 36, 62, 26, 0, 0,
        0, 0, 2, 34, 62, 30, 0, 0
    };

    // uint16_t av1_interp_dual_filt_mask[INTERP_PRED_TYPE_ALL - 2][SWITCHABLE_FILTERS]
    internal static readonly ushort[][] av1_interp_dual_filt_mask = {
        new ushort[] {
            (1 << REG_REG) | (1 << SMOOTH_REG) | (1 << SHARP_REG),
            (1 << REG_SMOOTH) | (1 << SMOOTH_SMOOTH) | (1 << SHARP_SMOOTH),
            (1 << REG_SHARP) | (1 << SMOOTH_SHARP) | (1 << SHARP_SHARP)
        },
        new ushort[] {
            (1 << REG_REG) | (1 << REG_SMOOTH) | (1 << REG_SHARP),
            (1 << SMOOTH_REG) | (1 << SMOOTH_SMOOTH) | (1 << SMOOTH_SHARP),
            (1 << SHARP_REG) | (1 << SHARP_SMOOTH) | (1 << SHARP_SHARP)
        }
    };
    // </gen:consts>

    internal static readonly InterpFilterParams[] av1_interp_filter_params_list = {
        new(av1_sub_pel_filters_8, SUBPEL_TAPS, EIGHTTAP_REGULAR),
        new(av1_sub_pel_filters_8smooth, SUBPEL_TAPS,
            EIGHTTAP_SMOOTH),
        new(av1_sub_pel_filters_8sharp, SUBPEL_TAPS,
            MULTITAP_SHARP),
        new(av1_bilinear_filters, SUBPEL_TAPS, BILINEAR),

        // The following filters are for encoder only, and now they are used in
        // temporal filtering. The predictor block size >= 16 in temporal filter.
        new(av1_sub_pel_filters_12sharp, 12, MULTITAP_SHARP2),
    };

    internal static readonly InterpFilterParams av1_intrabc_filter_params =
        new(av1_intrabc_bilinear_filter, 2, BILINEAR);

    // For w<=4, MULTITAP_SHARP is the same as EIGHTTAP_REGULAR
    internal static readonly InterpFilterParams[] av1_interp_4tap = {
        new(av1_sub_pel_filters_4, SUBPEL_TAPS, EIGHTTAP_REGULAR),
        new(av1_sub_pel_filters_4smooth, SUBPEL_TAPS,
            EIGHTTAP_SMOOTH),
        new(av1_sub_pel_filters_4, SUBPEL_TAPS, EIGHTTAP_REGULAR),
        new(av1_bilinear_filters, SUBPEL_TAPS, BILINEAR),
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static InterpFilterParams
    av1_get_interp_filter_params_with_block_size(InterpFilter interp_filter,
                                                 int w)
    {
        if (w <= 4 && interp_filter != MULTITAP_SHARP2)
            return av1_interp_4tap[interp_filter];
        return av1_interp_filter_params_list[interp_filter];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<short> av1_get_interp_filter_subpel_kernel(
        InterpFilterParams filter_params, int subpel)
    {
        return filter_params.filter_ptr + filter_params.taps * subpel;
    }

    // [port] Used by the SIMD convolutions only (s6).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_filter_tap(InterpFilterParams filter_params,
                                       int subpel_qn)
    {
        Ptr<short> filter = av1_get_interp_filter_subpel_kernel(
            filter_params, subpel_qn & SUBPEL_MASK);
        if (filter_params.taps == 12)
        {
            return 12;
        }
        if (filter[0] != 0 || filter[7] != 0)
        {
            return 8;
        }
        if (filter[1] != 0 || filter[6] != 0)
        {
            return 6;
        }
        if (filter[2] != 0 || filter[5] != 0)
        {
            return 4;
        }
        return 2;
    }
}
