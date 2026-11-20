// Port of libaom av1/common/warped_motion.h
// [subset] Warped and global motion are disabled; only the declarations are ported.

// <gen:aliases>
global using WarpedFilterCoeff = sbyte;
// </gen:aliases>

global using static Av1rt.Aom.warped_motion;

namespace Av1rt.Aom;

internal static partial class warped_motion
{
    // <gen:consts>
    internal const int LEAST_SQUARES_SAMPLES_MAX_BITS = 3;
    internal const int LEAST_SQUARES_SAMPLES_MAX = (1 << LEAST_SQUARES_SAMPLES_MAX_BITS);
    internal const int SAMPLES_ARRAY_SIZE = (LEAST_SQUARES_SAMPLES_MAX * 2);
    internal const int WARPED_MOTION_DEBUG = 0;
    internal const int DEFAULT_WMTYPE = AFFINE;
    internal const int WARP_ERROR_BLOCK_LOG = 5;
    internal const int WARP_ERROR_BLOCK = (1 << WARP_ERROR_BLOCK_LOG);

    // uint8_t warp_pad_left[14][16]
    internal static readonly byte[][] warp_pad_left = {
        new byte[] { 1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
        new byte[] { 2, 2, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
        new byte[] { 3, 3, 3, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
        new byte[] { 4, 4, 4, 4, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
        new byte[] { 5, 5, 5, 5, 5, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
        new byte[] { 6, 6, 6, 6, 6, 6, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
        new byte[] { 7, 7, 7, 7, 7, 7, 7, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
        new byte[] { 8, 8, 8, 8, 8, 8, 8, 8, 8, 9, 10, 11, 12, 13, 14, 15 },
        new byte[] { 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 10, 11, 12, 13, 14, 15 },
        new byte[] { 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 11, 12, 13, 14, 15 },
        new byte[] { 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 12, 13, 14, 15 },
        new byte[] { 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 13, 14, 15 },
        new byte[] { 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 14, 15 },
        new byte[] { 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 15 }
    };

    // uint8_t warp_pad_right[14][16]
    internal static readonly byte[][] warp_pad_right = {
        new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 14 },
        new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 13, 13 },
        new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 12, 12, 12 },
        new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 11, 11, 11, 11 },
        new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10, 10, 10, 10 },
        new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 9, 9, 9, 9, 9, 9 },
        new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 8, 8, 8, 8, 8, 8, 8 },
        new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7 },
        new byte[] { 0, 1, 2, 3, 4, 5, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6 },
        new byte[] { 0, 1, 2, 3, 4, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 },
        new byte[] { 0, 1, 2, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        new byte[] { 0, 1, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 },
        new byte[] { 0, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 },
        new byte[] { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }
    };
    // </gen:consts>

    // av1_warped_filter[WARPEDPIXEL_PREC_SHIFTS * 3 + 1][8] is defined in warped_motion.c
    // (not part of p1a).
}
