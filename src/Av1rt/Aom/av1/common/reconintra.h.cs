// Port of libaom av1/common/reconintra.h

global using static Av1rt.Aom.reconintra;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class reconintra
{
    // <gen:consts>
    internal const int FILTER_INTRA_SCALE_BITS = 4;

    // PREDICTION_MODE interintra_to_intra_mode[INTERINTRA_MODES]
    internal static readonly PREDICTION_MODE[] interintra_to_intra_mode = {
        DC_PRED, V_PRED, H_PRED, SMOOTH_PRED
    };

    // INTERINTRA_MODE intra_to_interintra_mode[INTRA_MODES]
    internal static readonly INTERINTRA_MODE[] intra_to_interintra_mode = {
        II_DC_PRED, II_V_PRED, II_H_PRED, II_V_PRED, II_SMOOTH_PRED, II_V_PRED, II_H_PRED,
        II_H_PRED, II_V_PRED, II_SMOOTH_PRED, II_SMOOTH_PRED, 0, 0
    };

    // int16_t dr_intra_derivative[90]
    internal static readonly short[] dr_intra_derivative = {
        0, 0, 0, 1023, 0, 0, 547, 0, 0, 372, 0, 0, 0, 0, 273, 0, 0, 215, 0, 0, 178, 0, 0, 151, 0,
        0, 132, 0, 0, 116, 0, 0, 102, 0, 0, 0, 90, 0, 0, 80, 0, 0, 71, 0, 0, 64, 0, 0, 57, 0, 0,
        51, 0, 0, 45, 0, 0, 0, 40, 0, 0, 35, 0, 0, 31, 0, 0, 27, 0, 0, 23, 0, 0, 19, 0, 0, 15, 0,
        0, 0, 0, 11, 0, 0, 7, 0, 0, 3, 0, 0
    };
    // </gen:consts>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_is_directional_mode(PREDICTION_MODE mode)
    {
        return B(mode >= V_PRED && mode <= D67_PRED);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_use_angle_delta(BLOCK_SIZE bsize)
    {
        return B(bsize >= BLOCK_8X8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_allow_intrabc(AV1_COMMON cm)
    {
        return B(frame_is_intra_only(cm) != 0 && cm.features.allow_screen_content_tools &&
                 cm.features.allow_intrabc);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_filter_intra_allowed_bsize(AV1_COMMON cm,
                                                       BLOCK_SIZE bs)
    {
        if (cm.seq_params.enable_filter_intra == 0 || bs == BLOCK_INVALID) return 0;

        return B(block_size_wide[bs] <= 32 && block_size_high[bs] <= 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_filter_intra_allowed(AV1_COMMON cm,
                                                 MB_MODE_INFO mbmi)
    {
        return B(mbmi.mode == DC_PRED &&
                 mbmi.palette_mode_info.palette_size[0] == 0 &&
                 av1_filter_intra_allowed_bsize(cm, mbmi.bsize) != 0);
    }

    // av1_filter_intra_taps[FILTER_INTRA_MODES][8][8] is defined in reconintra.c (not part
    // of p1a).
}
