// Port of libaom av1/encoder/rdopt.h
global using static Av1rt.Aom.rdopt;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class rdopt
{
    internal const int COMP_TYPE_RD_THRESH_SCALE = 11;
    internal const int COMP_TYPE_RD_THRESH_SHIFT = 4;
    internal const int MAX_WINNER_MOTION_MODES = 10;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int coded_to_superres_mi(int mi_col, int denom)
    {
        return (mi_col * denom + SCALE_NUMERATOR / 2) / SCALE_NUMERATOR;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_encoder_get_relative_dist(int a, int b)
    {
        Debug.Assert(a >= 0 && b >= 0);
        return (a - b);
    }

    // [subset] av1_get_sb_mi_size, prune_ref, has_closest_ref_frames, has_best_pred_mv_sad,
    // prune_ref_by_selective_ref_frame: only used by the rd mode search (rdopt.c).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_copy_mbmi_ext_to_mbmi_ext_frame(
        ref MB_MODE_INFO_EXT_FRAME mbmi_ext_best,
        MB_MODE_INFO_EXT mbmi_ext, byte ref_frame_type)
    {
        mbmi_ext.ref_mv_stack[ref_frame_type].AsSpan(0, USABLE_REF_MV_STACK_SIZE)
            .CopyTo(mbmi_ext_best.ref_mv_stack);
        mbmi_ext.weight[ref_frame_type].AsSpan(0, USABLE_REF_MV_STACK_SIZE)
            .CopyTo(mbmi_ext_best.weight);
        mbmi_ext_best.mode_context = mbmi_ext.mode_context[ref_frame_type];
        mbmi_ext_best.ref_mv_count = mbmi_ext.ref_mv_count[ref_frame_type];
        mbmi_ext.global_mvs.AsSpan(0, REF_FRAMES).CopyTo(mbmi_ext_best.global_mvs);
    }
}
