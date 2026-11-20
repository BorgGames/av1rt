// Port of libaom av1/encoder/nonrd_opt.h
global using static Av1rt.Aom.nonrd_opt;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class nonrd_opt
{
    internal const int RTC_INTER_MODES = (4);
    internal const int RTC_INTRA_MODES = (4);
    internal const int RTC_MODES = (RTC_INTER_MODES > RTC_INTRA_MODES ? RTC_INTER_MODES : RTC_INTRA_MODES); // AOMMAX(RTC_INTER_MODES, RTC_INTRA_MODES)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long CALC_BIASED_RDCOST(long rdcost) => (7 * (rdcost) >> 3);

    internal const int NUM_COMP_INTER_MODES_RT = (6);
    internal const int NUM_COMP_INTER_MODES_RT_FULL = (10);
    internal const int NUM_INTER_MODES = 12;
    internal const int NUM_INTER_MODES_FULL = 28;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CAP_TX_SIZE_FOR_BSIZE_GT32(TX_MODE tx_mode_search_type, BLOCK_SIZE bsize) =>
        (((tx_mode_search_type) != ONLY_4X4 && (bsize) > BLOCK_32X32) ? true : false);

    internal const int TX_SIZE_FOR_BSIZE_GT32 = (TX_16X16);
    internal const int FILTER_SEARCH_SIZE = 2;
    // [subset] MOTION_MODE_SEARCH_SIZE: !CONFIG_REALTIME_ONLY.

    // [subset] `extern int g_pick_inter_mode_cnt;` is declared but never defined/used.

    internal static readonly byte[] b_width_log2_lookup = { 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5 };
    internal static readonly byte[] b_height_log2_lookup = { 0, 1, 0, 1, 2, 1, 2, 3, 2, 3, 4, 3, 4, 5, 4, 5 };

    internal static readonly PREDICTION_MODE[] intra_mode_list = { DC_PRED, V_PRED, H_PRED, SMOOTH_PRED };

    internal static readonly PREDICTION_MODE[] inter_mode_list = { NEARESTMV, NEARMV, GLOBALMV, NEWMV };

    internal static readonly THR_MODES[][] mode_idx = {
        new THR_MODES[] { THR_DC, THR_V_PRED, THR_H_PRED, THR_SMOOTH },
        new THR_MODES[] { THR_NEARESTMV, THR_NEARMV, THR_GLOBALMV, THR_NEWMV },
        new THR_MODES[] { THR_NEARESTL2, THR_NEARL2, THR_GLOBALL2, THR_NEWL2 },
        new THR_MODES[] { THR_NEARESTL3, THR_NEARL3, THR_GLOBALL3, THR_NEWL3 },
        new THR_MODES[] { THR_NEARESTG, THR_NEARG, THR_GLOBALG, THR_NEWG },
        new THR_MODES[] { THR_NEARESTB, THR_NEARB, THR_GLOBALB, THR_NEWB },
        new THR_MODES[] { THR_NEARESTA2, THR_NEARA2, THR_GLOBALA2, THR_NEWA2 },
        new THR_MODES[] { THR_NEARESTA, THR_NEARA, THR_GLOBALA, THR_NEWA },
    };

    // GLOBALMV in the set below is in fact ZEROMV as we don't do global ME in RT
    // mode
    internal static readonly REF_MODE[] ref_mode_set = {
        new REF_MODE { ref_frame = LAST_FRAME, pred_mode = NEARESTMV },
        new REF_MODE { ref_frame = LAST_FRAME, pred_mode = NEARMV },
        new REF_MODE { ref_frame = LAST_FRAME, pred_mode = GLOBALMV },
        new REF_MODE { ref_frame = LAST_FRAME, pred_mode = NEWMV },
        new REF_MODE { ref_frame = GOLDEN_FRAME, pred_mode = NEARESTMV },
        new REF_MODE { ref_frame = GOLDEN_FRAME, pred_mode = NEARMV },
        new REF_MODE { ref_frame = GOLDEN_FRAME, pred_mode = GLOBALMV },
        new REF_MODE { ref_frame = GOLDEN_FRAME, pred_mode = NEWMV },
        new REF_MODE { ref_frame = ALTREF_FRAME, pred_mode = NEARESTMV },
        new REF_MODE { ref_frame = ALTREF_FRAME, pred_mode = NEARMV },
        new REF_MODE { ref_frame = ALTREF_FRAME, pred_mode = GLOBALMV },
        new REF_MODE { ref_frame = ALTREF_FRAME, pred_mode = NEWMV },
    };

    internal static readonly REF_MODE[] ref_mode_set_full = {
        new REF_MODE { ref_frame = LAST_FRAME, pred_mode = NEARESTMV },
        new REF_MODE { ref_frame = LAST_FRAME, pred_mode = NEARMV },
        new REF_MODE { ref_frame = LAST_FRAME, pred_mode = GLOBALMV },
        new REF_MODE { ref_frame = LAST_FRAME, pred_mode = NEWMV },
        new REF_MODE { ref_frame = GOLDEN_FRAME, pred_mode = NEARESTMV },
        new REF_MODE { ref_frame = GOLDEN_FRAME, pred_mode = NEARMV },
        new REF_MODE { ref_frame = GOLDEN_FRAME, pred_mode = GLOBALMV },
        new REF_MODE { ref_frame = GOLDEN_FRAME, pred_mode = NEWMV },
        new REF_MODE { ref_frame = ALTREF_FRAME, pred_mode = NEARESTMV },
        new REF_MODE { ref_frame = ALTREF_FRAME, pred_mode = NEARMV },
        new REF_MODE { ref_frame = ALTREF_FRAME, pred_mode = GLOBALMV },
        new REF_MODE { ref_frame = ALTREF_FRAME, pred_mode = NEWMV },
        new REF_MODE { ref_frame = LAST2_FRAME, pred_mode = NEARESTMV },
        new REF_MODE { ref_frame = LAST2_FRAME, pred_mode = NEARMV },
        new REF_MODE { ref_frame = LAST2_FRAME, pred_mode = GLOBALMV },
        new REF_MODE { ref_frame = LAST2_FRAME, pred_mode = NEWMV },
        new REF_MODE { ref_frame = LAST3_FRAME, pred_mode = NEARESTMV },
        new REF_MODE { ref_frame = LAST3_FRAME, pred_mode = NEARMV },
        new REF_MODE { ref_frame = LAST3_FRAME, pred_mode = GLOBALMV },
        new REF_MODE { ref_frame = LAST3_FRAME, pred_mode = NEWMV },
        new REF_MODE { ref_frame = BWDREF_FRAME, pred_mode = NEARESTMV },
        new REF_MODE { ref_frame = BWDREF_FRAME, pred_mode = NEARMV },
        new REF_MODE { ref_frame = BWDREF_FRAME, pred_mode = GLOBALMV },
        new REF_MODE { ref_frame = BWDREF_FRAME, pred_mode = NEWMV },
        new REF_MODE { ref_frame = ALTREF2_FRAME, pred_mode = NEARESTMV },
        new REF_MODE { ref_frame = ALTREF2_FRAME, pred_mode = NEARMV },
        new REF_MODE { ref_frame = ALTREF2_FRAME, pred_mode = GLOBALMV },
        new REF_MODE { ref_frame = ALTREF2_FRAME, pred_mode = NEWMV },
    };

    internal static readonly COMP_REF_MODE[] comp_ref_mode_set = {
        new COMP_REF_MODE(LAST_FRAME, GOLDEN_FRAME, GLOBAL_GLOBALMV),
        new COMP_REF_MODE(LAST_FRAME, GOLDEN_FRAME, NEAREST_NEARESTMV),
        new COMP_REF_MODE(LAST_FRAME, LAST2_FRAME, GLOBAL_GLOBALMV),
        new COMP_REF_MODE(LAST_FRAME, LAST2_FRAME, NEAREST_NEARESTMV),
        new COMP_REF_MODE(LAST_FRAME, ALTREF_FRAME, GLOBAL_GLOBALMV),
        new COMP_REF_MODE(LAST_FRAME, ALTREF_FRAME, NEAREST_NEARESTMV),
    };

    internal static readonly COMP_REF_MODE[] comp_ref_mode_set_full = {
        new COMP_REF_MODE(LAST_FRAME, GOLDEN_FRAME, GLOBAL_GLOBALMV),
        new COMP_REF_MODE(LAST_FRAME, GOLDEN_FRAME, NEAREST_NEARESTMV),
        new COMP_REF_MODE(LAST_FRAME, LAST2_FRAME, GLOBAL_GLOBALMV),
        new COMP_REF_MODE(LAST_FRAME, LAST2_FRAME, NEAREST_NEARESTMV),
        new COMP_REF_MODE(LAST_FRAME, ALTREF_FRAME, GLOBAL_GLOBALMV),
        new COMP_REF_MODE(LAST_FRAME, ALTREF_FRAME, NEAREST_NEARESTMV),
        new COMP_REF_MODE(LAST_FRAME, BWDREF_FRAME, GLOBAL_GLOBALMV),
        new COMP_REF_MODE(LAST_FRAME, BWDREF_FRAME, NEAREST_NEARESTMV),
        new COMP_REF_MODE(LAST_FRAME, ALTREF2_FRAME, GLOBAL_GLOBALMV),
        new COMP_REF_MODE(LAST_FRAME, ALTREF2_FRAME, NEAREST_NEARESTMV),
    };

    // C uses designated initializers `[i].as_filters = { y_filter, x_filter }`; IF() builds
    // the same union value (InterpFilters is { y_filter, x_filter }).
    internal static readonly int_interpfilters[] filters_ref_set = {
        IF(EIGHTTAP_REGULAR, EIGHTTAP_REGULAR), // [0]
        IF(EIGHTTAP_SMOOTH, EIGHTTAP_SMOOTH),   // [1]
        IF(EIGHTTAP_REGULAR, EIGHTTAP_SMOOTH),  // [2]
        IF(EIGHTTAP_SMOOTH, EIGHTTAP_REGULAR),  // [3]
        IF(MULTITAP_SHARP, MULTITAP_SHARP),     // [4]
        IF(EIGHTTAP_REGULAR, MULTITAP_SHARP),   // [5]
        IF(MULTITAP_SHARP, EIGHTTAP_REGULAR),   // [6]
        IF(EIGHTTAP_SMOOTH, MULTITAP_SHARP),    // [7]
        IF(MULTITAP_SHARP, EIGHTTAP_SMOOTH),    // [8]
    };

    private static int_interpfilters IF(InterpFilter y_filter, InterpFilter x_filter)
    {
        int_interpfilters f = default;
        f.as_filters.y_filter = (ushort)y_filter;
        f.as_filters.x_filter = (ushort)x_filter;
        return f;
    }

    // anonymous enum
    //  INTER_ALL = (1 << NEARESTMV) | (1 << NEARMV) | (1 << NEWMV),
    internal const int INTER_NEAREST = (1 << NEARESTMV);
    internal const int INTER_NEAREST_NEW = (1 << NEARESTMV) | (1 << NEWMV);
    internal const int INTER_NEAREST_NEAR = (1 << NEARESTMV) | (1 << NEARMV);
    internal const int INTER_NEAR_NEW = (1 << NEARMV) | (1 << NEWMV);

    // The original scan order (default_scan_8x8) is modified according to the extra
    // transpose in hadamard c implementation, i.e., aom_hadamard_lp_8x8_c and
    // aom_hadamard_8x8_c.
    internal static readonly short[] default_scan_8x8_transpose = {
        0, 8, 1, 2, 9, 16, 24, 17, 10, 3, 4, 11, 18, 25, 32, 40, 33, 26, 19, 12, 5, 6, 13, 20, 27,
        34, 41, 48, 56, 49, 42, 35, 28, 21, 14, 7, 15, 22, 29, 36, 43, 50, 57, 58, 51, 44, 37, 30,
        23, 31, 38, 45, 52, 59, 60, 53, 46, 39, 47, 54, 61, 62, 55, 63,
    };

    // The original scan order (av1_default_iscan_8x8) is modified to match
    // hadamard AVX2 implementation, i.e., aom_hadamard_lp_8x8_avx2 and
    // aom_hadamard_8x8_avx2. Since hadamard AVX2 implementation will modify the
    // order of coefficients, such that the normal scan order is no longer
    // guaranteed to scan low coefficients first, therefore we modify the scan order
    // accordingly.
    // Note that this one has to be used together with default_scan_8x8_transpose.
    internal static readonly short[] av1_default_iscan_8x8_transpose = {
        0, 2, 3, 9, 10, 20, 21, 35, 1, 4, 8, 11, 19, 22, 34, 36, 5, 7, 12, 18, 23, 33, 37, 48, 6,
        13, 17, 24, 32, 38, 47, 49, 14, 16, 25, 31, 39, 46, 50, 57, 15, 26, 30, 40, 45, 51, 56, 58,
        27, 29, 41, 44, 52, 55, 59, 62, 28, 42, 43, 53, 54, 60, 61, 63,
    };

    // The original scan order (default_scan_16x16) is modified according to the
    // extra transpose in hadamard c implementation in lp case, i.e.,
    // aom_hadamard_lp_16x16_c.
    internal static readonly short[] default_scan_lp_16x16_transpose = {
        0, 8, 2, 4, 10, 16, 24, 18, 12, 6, 64, 14, 20, 26, 32, 40, 34, 28, 22, 72, 66, 68, 74, 80,
        30, 36, 42, 48, 56, 50, 44, 38, 88, 82, 76, 70, 128, 78, 84, 90, 96, 46, 52, 58, 1, 9, 3,
        60, 54, 104, 98, 92, 86, 136, 130, 132, 138, 144, 94, 100, 106, 112, 62, 5, 11, 17, 25, 19,
        13, 7, 120, 114, 108, 102, 152, 146, 140, 134, 192, 142, 148, 154, 160, 110, 116, 122, 65,
        15, 21, 27, 33, 41, 35, 29, 23, 73, 67, 124, 118, 168, 162, 156, 150, 200, 194, 196, 202,
        208, 158, 164, 170, 176, 126, 69, 75, 81, 31, 37, 43, 49, 57, 51, 45, 39, 89, 83, 77, 71,
        184, 178, 172, 166, 216, 210, 204, 198, 206, 212, 218, 224, 174, 180, 186, 129, 79, 85, 91,
        97, 47, 53, 59, 61, 55, 105, 99, 93, 87, 137, 131, 188, 182, 232, 226, 220, 214, 222, 228,
        234, 240, 190, 133, 139, 145, 95, 101, 107, 113, 63, 121, 115, 109, 103, 153, 147, 141,
        135, 248, 242, 236, 230, 238, 244, 250, 193, 143, 149, 155, 161, 111, 117, 123, 125, 119,
        169, 163, 157, 151, 201, 195, 252, 246, 254, 197, 203, 209, 159, 165, 171, 177, 127, 185,
        179, 173, 167, 217, 211, 205, 199, 207, 213, 219, 225, 175, 181, 187, 189, 183, 233, 227,
        221, 215, 223, 229, 235, 241, 191, 249, 243, 237, 231, 239, 245, 251, 253, 247, 255,
    };

    // [subset] default_scan_fp_16x16_transpose: CONFIG_AV1_HIGHBITDEPTH.

    // The original scan order (av1_default_iscan_16x16) is modified to match
    // hadamard AVX2 implementation, i.e., aom_hadamard_lp_16x16_avx2.
    // Since hadamard AVX2 implementation will modify the order of coefficients,
    // such that the normal scan order is no longer guaranteed to scan low
    // coefficients first, therefore we modify the scan order accordingly. Note that
    // this one has to be used together with default_scan_lp_16x16_transpose.
    internal static readonly short[] av1_default_iscan_lp_16x16_transpose = {
        0, 44, 2, 46, 3, 63, 9, 69, 1, 45, 4, 64, 8, 68, 11, 87, 5, 65, 7, 67, 12, 88, 18, 94, 6,
        66, 13, 89, 17, 93, 24, 116, 14, 90, 16, 92, 25, 117, 31, 123, 15, 91, 26, 118, 30, 122,
        41, 148, 27, 119, 29, 121, 42, 149, 48, 152, 28, 120, 43, 150, 47, 151, 62, 177, 10, 86,
        20, 96, 21, 113, 35, 127, 19, 95, 22, 114, 34, 126, 37, 144, 23, 115, 33, 125, 38, 145, 52,
        156, 32, 124, 39, 146, 51, 155, 58, 173, 40, 147, 50, 154, 59, 174, 73, 181, 49, 153, 60,
        175, 72, 180, 83, 198, 61, 176, 71, 179, 84, 199, 98, 202, 70, 178, 85, 200, 97, 201, 112,
        219, 36, 143, 54, 158, 55, 170, 77, 185, 53, 157, 56, 171, 76, 184, 79, 194, 57, 172, 75,
        183, 80, 195, 102, 206, 74, 182, 81, 196, 101, 205, 108, 215, 82, 197, 100, 204, 109, 216,
        131, 223, 99, 203, 110, 217, 130, 222, 140, 232, 111, 218, 129, 221, 141, 233, 160, 236,
        128, 220, 142, 234, 159, 235, 169, 245, 78, 193, 104, 208, 105, 212, 135, 227, 103, 207,
        106, 213, 134, 226, 136, 228, 107, 214, 133, 225, 137, 229, 164, 240, 132, 224, 138, 230,
        163, 239, 165, 241, 139, 231, 162, 238, 166, 242, 189, 249, 161, 237, 167, 243, 188, 248,
        190, 250, 168, 244, 187, 247, 191, 251, 210, 254, 186, 246, 192, 252, 209, 253, 211, 255,
    };

    // [subset] av1_default_iscan_fp_16x16_transpose: CONFIG_AV1_HIGHBITDEPTH.

    // For entropy coding, IDTX shares the scan orders of the other 2D-transforms,
    // but the fastest way to calculate the IDTX transform (i.e. no transposes)
    // results in coefficients that are a transposition of the entropy coding
    // versions. These tables are used as substitute for the scan order for the
    // faster version of IDTX.

    // Must be used together with av1_fast_idtx_iscan_4x4
    internal static readonly short[] av1_fast_idtx_scan_4x4 = { 0, 1, 4, 8, 5, 2, 3, 6, 9, 12, 13, 10, 7, 11, 14, 15 };

    // Must be used together with av1_fast_idtx_scan_4x4
    internal static readonly short[] av1_fast_idtx_iscan_4x4 = { 0, 1, 5, 6, 2, 4, 7, 12, 3, 8, 11, 13, 9, 10, 14, 15 };

    internal static readonly SCAN_ORDER av1_fast_idtx_scan_order_4x4 =
        new(av1_fast_idtx_scan_4x4, av1_fast_idtx_iscan_4x4);

    // Must be used together with av1_fast_idtx_iscan_8x8
    internal static readonly short[] av1_fast_idtx_scan_8x8 = {
        0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26, 33, 40, 48, 41, 34, 27,
        20, 13, 6, 7, 14, 21, 28, 35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63,
    };

    // Must be used together with av1_fast_idtx_scan_8x8
    internal static readonly short[] av1_fast_idtx_iscan_8x8 = {
        0, 1, 5, 6, 14, 15, 27, 28, 2, 4, 7, 13, 16, 26, 29, 42, 3, 8, 12, 17, 25, 30, 41, 43, 9,
        11, 18, 24, 31, 40, 44, 53, 10, 19, 23, 32, 39, 45, 52, 54, 20, 22, 33, 38, 46, 51, 55, 60,
        21, 34, 37, 47, 50, 56, 59, 61, 35, 36, 48, 49, 57, 58, 62, 63,
    };

    internal static readonly SCAN_ORDER av1_fast_idtx_scan_order_8x8 =
        new(av1_fast_idtx_scan_8x8, av1_fast_idtx_iscan_8x8);

    // Must be used together with av1_fast_idtx_iscan_16x16
    internal static readonly short[] av1_fast_idtx_scan_16x16 = {
        0, 1, 16, 32, 17, 2, 3, 18, 33, 48, 64, 49, 34, 19, 4, 5, 20, 35, 50, 65, 80, 96, 81, 66,
        51, 36, 21, 6, 7, 22, 37, 52, 67, 82, 97, 112, 128, 113, 98, 83, 68, 53, 38, 23, 8, 9, 24,
        39, 54, 69, 84, 99, 114, 129, 144, 160, 145, 130, 115, 100, 85, 70, 55, 40, 25, 10, 11, 26,
        41, 56, 71, 86, 101, 116, 131, 146, 161, 176, 192, 177, 162, 147, 132, 117, 102, 87, 72,
        57, 42, 27, 12, 13, 28, 43, 58, 73, 88, 103, 118, 133, 148, 163, 178, 193, 208, 224, 209,
        194, 179, 164, 149, 134, 119, 104, 89, 74, 59, 44, 29, 14, 15, 30, 45, 60, 75, 90, 105,
        120, 135, 150, 165, 180, 195, 210, 225, 240, 241, 226, 211, 196, 181, 166, 151, 136, 121,
        106, 91, 76, 61, 46, 31, 47, 62, 77, 92, 107, 122, 137, 152, 167, 182, 197, 212, 227, 242,
        243, 228, 213, 198, 183, 168, 153, 138, 123, 108, 93, 78, 63, 79, 94, 109, 124, 139, 154,
        169, 184, 199, 214, 229, 244, 245, 230, 215, 200, 185, 170, 155, 140, 125, 110, 95, 111,
        126, 141, 156, 171, 186, 201, 216, 231, 246, 247, 232, 217, 202, 187, 172, 157, 142, 127,
        143, 158, 173, 188, 203, 218, 233, 248, 249, 234, 219, 204, 189, 174, 159, 175, 190, 205,
        220, 235, 250, 251, 236, 221, 206, 191, 207, 222, 237, 252, 253, 238, 223, 239, 254, 255,
    };

    // Must be used together with av1_fast_idtx_scan_16x16
    internal static readonly short[] av1_fast_idtx_iscan_16x16 = {
        0, 1, 5, 6, 14, 15, 27, 28, 44, 45, 65, 66, 90, 91, 119, 120, 2, 4, 7, 13, 16, 26, 29, 43,
        46, 64, 67, 89, 92, 118, 121, 150, 3, 8, 12, 17, 25, 30, 42, 47, 63, 68, 88, 93, 117, 122,
        149, 151, 9, 11, 18, 24, 31, 41, 48, 62, 69, 87, 94, 116, 123, 148, 152, 177, 10, 19, 23,
        32, 40, 49, 61, 70, 86, 95, 115, 124, 147, 153, 176, 178, 20, 22, 33, 39, 50, 60, 71, 85,
        96, 114, 125, 146, 154, 175, 179, 200, 21, 34, 38, 51, 59, 72, 84, 97, 113, 126, 145, 155,
        174, 180, 199, 201, 35, 37, 52, 58, 73, 83, 98, 112, 127, 144, 156, 173, 181, 198, 202,
        219, 36, 53, 57, 74, 82, 99, 111, 128, 143, 157, 172, 182, 197, 203, 218, 220, 54, 56, 75,
        81, 100, 110, 129, 142, 158, 171, 183, 196, 204, 217, 221, 234, 55, 76, 80, 101, 109, 130,
        141, 159, 170, 184, 195, 205, 216, 222, 233, 235, 77, 79, 102, 108, 131, 140, 160, 169,
        185, 194, 206, 215, 223, 232, 236, 245, 78, 103, 107, 132, 139, 161, 168, 186, 193, 207,
        214, 224, 231, 237, 244, 246, 104, 106, 133, 138, 162, 167, 187, 192, 208, 213, 225, 230,
        238, 243, 247, 252, 105, 134, 137, 163, 166, 188, 191, 209, 212, 226, 229, 239, 242, 248,
        251, 253, 135, 136, 164, 165, 189, 190, 210, 211, 227, 228, 240, 241, 249, 250, 254, 255,
    };

    // Indicates the blocks for which RD model should be based on special logic
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_model_rd_flag(AV1_COMP cpi, MACROBLOCKD xd,
                                          BLOCK_SIZE bsize)
    {
        AV1_COMMON cm = cpi.common;
        int large_block = B(bsize >= BLOCK_32X32);
        // Only enable for low bitdepth to mitigate issue: b/303023614.
        return B(cpi.oxcf.rc_cfg.mode == AOM_CBR && large_block != 0 &&
                 cyclic_refresh_segment_id_boosted(xd.mi[0].segment_id) == 0 &&
                 cm.quant_params.base_qindex != 0 && !cpi.oxcf.use_highbitdepth);
    }

    // Finds predicted motion vectors for a block.
    //
    // Finds predicted motion vectors for a block from a certain reference frame.
    // First, it fills reference MV stack, then picks the test from the stack and
    // predicts the final MV for a block for each mode.
    // cpi                      Top-level encoder structure
    // x                        Pointer to structure holding all the
    //                          data for the current macroblock
    // ref_frame                Reference frame for which to find
    //                          ref MVs
    // frame_mv                 [out] Predicted MVs for a block
    // yv12_mb                  Buffer to hold predicted block
    // bsize                    Current block size
    // force_skip_low_temp_var  Flag indicating possible mode search
    //                          prune for low temporal variance block
    // skip_pred_mv             Flag indicating to skip av1_mv_pred
    // use_scaled_ref_frame     [out] Flag to indicate if scaled reference
    //                          frame is used.
    //
    // Nothing is returned. Instead, predicted MVs are placed into
    // frame_mv array, and use_scaled_ref_frame is set.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void find_predictors(
        AV1_COMP cpi, MACROBLOCK x, MV_REFERENCE_FRAME ref_frame,
        int_mv[][] frame_mv,
        buf_2d[][] yv12_mb, BLOCK_SIZE bsize,
        int force_skip_low_temp_var, int skip_pred_mv, ref bool use_scaled_ref_frame)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mbmi = xd.mi[0];
        MB_MODE_INFO_EXT mbmi_ext = x.mbmi_ext;
        YV12_BUFFER_CONFIG @ref = get_ref_frame_yv12_buf(cm, ref_frame);
        bool ref_is_scaled =
            @ref.y_crop_height != cm.height || @ref.y_crop_width != cm.width;
        YV12_BUFFER_CONFIG scaled_ref =
            av1_get_scaled_ref_frame(cpi, ref_frame);
        YV12_BUFFER_CONFIG yv12 =
            ref_is_scaled && scaled_ref != null ? scaled_ref : @ref;
        int num_planes = av1_num_planes(cm);
        x.pred_mv_sad[ref_frame] = int.MaxValue;
        x.pred_mv0_sad[ref_frame] = int.MaxValue;
        x.pred_mv1_sad[ref_frame] = int.MaxValue;
        frame_mv[NEWMV][ref_frame].as_int = INVALID_MV;
        // TODO(kyslov) this needs various further optimizations. to be continued..
        Debug.Assert(yv12 != null);
        if (yv12 != null)
        {
            scale_factors sf =
                scaled_ref != null ? null : get_ref_scale_factors(cm, ref_frame);
            av1_setup_pred_block(xd, yv12_mb[ref_frame], yv12, sf, sf, num_planes);
            av1_find_mv_refs(cm, xd, mbmi, ref_frame, mbmi_ext.ref_mv_count,
                             xd.ref_mv_stack, xd.weight, null, mbmi_ext.global_mvs,
                             mbmi_ext.mode_context);
            // TODO(Ravi): Populate mbmi_ext->ref_mv_stack[ref_frame][4] and
            // mbmi_ext->weight[ref_frame][4] inside av1_find_mv_refs.
            av1_copy_usable_ref_mv_stack_and_weight(xd, mbmi_ext, ref_frame);
            av1_find_best_ref_mvs_from_stack(
                B(cm.features.allow_high_precision_mv), mbmi_ext, ref_frame,
                ref frame_mv[NEARESTMV][ref_frame], ref frame_mv[NEARMV][ref_frame], 0);
            frame_mv[GLOBALMV][ref_frame] = mbmi_ext.global_mvs[ref_frame];
            // Early exit for non-LAST frame if force_skip_low_temp_var is set.
            if (is_one_pass_rt_lag_params(cpi) == 0 && !ref_is_scaled &&
                bsize >= BLOCK_8X8 && skip_pred_mv == 0 &&
                !(force_skip_low_temp_var != 0 && ref_frame != LAST_FRAME))
            {
                av1_mv_pred(cpi, x, yv12_mb[ref_frame][0].buf, yv12.y_stride, ref_frame,
                            bsize);
            }
        }
        if (cm.features.switchable_motion_mode)
        {
            // [subset] av1_count_overlappable_neighbors (OBMC / warped motion): never
            // reached in the subset (switchable_motion_mode stays 0 for RT nonrd).
            throw new NotSupportedException("find_predictors: switchable_motion_mode");
        }
        mbmi.num_proj_ref = 1;
        use_scaled_ref_frame = ref_is_scaled && scaled_ref != null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void init_mbmi_nonrd(MB_MODE_INFO mbmi,
                                         PREDICTION_MODE pred_mode,
                                         MV_REFERENCE_FRAME ref_frame0,
                                         MV_REFERENCE_FRAME ref_frame1,
                                         AV1_COMMON cm)
    {
        ref PALETTE_MODE_INFO pmi = ref mbmi.palette_mode_info;
        mbmi.ref_mv_idx = 0;
        mbmi.mode = (PREDICTION_MODE)pred_mode;
        mbmi.uv_mode = UV_DC_PRED;
        mbmi.ref_frame[0] = ref_frame0;
        mbmi.ref_frame[1] = ref_frame1;
        pmi.palette_size[PLANE_TYPE_Y] = 0;
        pmi.palette_size[PLANE_TYPE_UV] = 0;
        mbmi.use_intrabc = 0;
        mbmi.filter_intra_mode_info.use_filter_intra = 0;
        mbmi.mv[0].as_int = mbmi.mv[1].as_int = 0;
        mbmi.motion_mode = SIMPLE_TRANSLATION;
        mbmi.num_proj_ref = 1;
        mbmi.interintra_mode = 0;
        set_default_interp_filters(mbmi, cm.features.interp_filter);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void init_estimate_block_intra_args(
        estimate_block_intra_args args, AV1_COMP cpi, MACROBLOCK x)
    {
        args.cpi = cpi;
        args.x = x;
        args.mode = DC_PRED;
        args.skippable = 1;
        args.rdc = Ptr<RD_STATS>.Null;
        args.best_sad = uint.MaxValue;
        args.prune_mode_based_on_sad = false;
        args.prune_palette_sad = false;
    }

    // PORT-NOTE: C takes `PRED_BUFFER *p` (an array of PRED_BUFFER); PRED_BUFFER is a class
    // here, so the array of references is passed.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_pred_buffer(PRED_BUFFER[] p, int len)
    {
        for (int buf_idx = 0; buf_idx < len; buf_idx++)
        {
            if (p[buf_idx].in_use == 0)
            {
                p[buf_idx].in_use = 1;
                return buf_idx;
            }
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool prune_palette_testing_inter(AV1_COMP cpi,
                                                     uint source_variance)
    {
        return (
            cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN &&
            cpi.oxcf.speed >= 11 && cpi.rc.high_source_sad != 0 &&
            ((cpi.sf.rt_sf.prune_palette_search_nonrd > 2) ||
             (cpi.sf.rt_sf.rc_compute_spatial_var_sc_kf != 0 &&
              cpi.rc.frame_spatial_variance < 1200 &&
              cpi.rc.perc_spatial_flat_blocks < 5 &&
              cpi.rc.percent_blocks_with_motion > 98 && source_variance < 4000)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void free_pred_buffer(PRED_BUFFER p)
    {
        if (p != null) p.in_use = 0;
    }

    // [subset] the CONFIG_INTERNAL_STATS variant (extra mode_index parameter).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void store_coding_context_nonrd(MACROBLOCK x,
                                                    PICK_MODE_CONTEXT ctx)
    {
        MACROBLOCKD xd = x.e_mbd;
        TxfmSearchInfo txfm_info = x.txfm_search_info;

        // Take a snapshot of the coding context so it can be
        // restored if we decide to encode this way
        ctx.rd_stats.skip_txfm = txfm_info.skip_txfm;

        ctx.skippable = txfm_info.skip_txfm;
        ctx.mic.CopyFrom(xd.mi[0]);
        ctx.skippable = txfm_info.skip_txfm;
        // C: the int8_t result of av1_ref_frame_type converts to the uint8_t parameter.
        av1_copy_mbmi_ext_to_mbmi_ext_frame(ref ctx.mbmi_ext_best, x.mbmi_ext,
                                            (byte)av1_ref_frame_type(xd.mi[0].ref_frame));
    }
}

// Class: elements of the tmp_buffer[4] array whose addresses are kept and compared
// (best_pickmode->best_pred, this_mode_pred) and set to NULL.
internal sealed partial class PRED_BUFFER
{
    internal Ptr<byte> data;
    internal int stride;
    internal int in_use;
}

// Class: embedded in InterModeSearchStateNonrd and only used through pointers; never copied
// as a whole.
internal sealed partial class BEST_PICKMODE
{
    internal PRED_BUFFER best_pred;
    internal PREDICTION_MODE best_mode;
    internal TX_SIZE best_tx_size;
    internal TX_TYPE tx_type;
    internal MV_REFERENCE_FRAME best_ref_frame;
    internal MV_REFERENCE_FRAME best_second_ref_frame;
    internal byte best_mode_skip_txfm;
    internal byte best_mode_initial_skip_flag;
    internal int_interpfilters best_pred_filter;
    internal MOTION_MODE best_motion_mode;
    internal WarpedMotionParams wm_params;
    internal int num_proj_ref;
    internal PALETTE_MODE_INFO pmi;
    internal long best_sse;
}

internal partial struct REF_MODE
{
    internal MV_REFERENCE_FRAME ref_frame;
    internal PREDICTION_MODE pred_mode;
}

internal partial struct COMP_REF_MODE
{
    internal Arr2<MV_REFERENCE_FRAME> ref_frame;
    internal PREDICTION_MODE pred_mode;

    // `{ { ref_frame0, ref_frame1 }, pred_mode }` initializer of the tables above.
    internal COMP_REF_MODE(MV_REFERENCE_FRAME ref_frame0, MV_REFERENCE_FRAME ref_frame1,
                           PREDICTION_MODE pred_mode)
    {
        ref_frame = default;
        ref_frame[0] = ref_frame0;
        ref_frame[1] = ref_frame1;
        this.pred_mode = pred_mode;
    }
}

// Class: passed to av1_estimate_block_intra() as the visitor's `void *arg`.
// PORT-NOTE: `RD_STATS *rdc` points to a caller local; it is a Ptr<RD_STATS>, so the caller
// keeps that RD_STATS in a 1-element array (`args.rdc = new Ptr<RD_STATS>(this_rdc_buf)`)
// and reads it back from there.
internal sealed partial class estimate_block_intra_args
{
    internal AV1_COMP cpi;
    internal MACROBLOCK x;
    internal PREDICTION_MODE mode;
    internal int skippable;
    internal Ptr<RD_STATS> rdc;
    internal uint best_sad;
    internal bool prune_mode_based_on_sad;
    internal bool prune_palette_sad;
}

// Structure to store parameters and statistics used in non-rd inter mode
// evaluation.
// Class: a large local of av1_nonrd_pick_inter_mode_sb() passed by pointer; never copied.
internal sealed partial class InterModeSearchStateNonrd
{
    // Structure to hold best inter mode data
    internal readonly BEST_PICKMODE best_pickmode = new();
    // Structure to RD cost of current mode
    internal RD_STATS this_rdc;
    // Pointer to the RD Cost for the best mode found so far
    internal RD_STATS best_rdc;
    // Distortion of chroma planes for all modes and reference frames
    internal readonly long[][] uv_dist = EncArrays.New2<long>(RTC_INTER_MODES, REF_FRAMES);
    // Buffer to hold predicted block for all reference frames and planes
    internal readonly buf_2d[][] yv12_mb = EncArrays.New2<buf_2d>(REF_FRAMES, MAX_MB_PLANE);
    // Array to hold variance of all modes and reference frames
    internal readonly uint[][] vars = EncArrays.New2<uint>(RTC_INTER_MODES, REF_FRAMES);
    // Array to hold ref cost of single reference mode for all ref frames
    internal readonly uint[] ref_costs_single = new uint[REF_FRAMES];
    // Array to hold motion vector for all modes and reference frames
    internal readonly int_mv[][] frame_mv = EncArrays.New2<int_mv>(MB_MODE_COUNT, REF_FRAMES);
    // Array to hold best mv for all modes and reference frames
    internal readonly int_mv[][] frame_mv_best = EncArrays.New2<int_mv>(MB_MODE_COUNT, REF_FRAMES);
    // Array to hold inter mode cost of single ref mode for all ref frames
    internal readonly int[][] single_inter_mode_costs = EncArrays.New2<int>(RTC_INTER_MODES, REF_FRAMES);
    // Array to hold use reference frame mask for each reference frame
    internal readonly int[] use_ref_frame_mask = new int[REF_FRAMES];
    // Array to hold flags of evaluated modes for each reference frame
    internal readonly byte[][] mode_checked = EncArrays.New2<byte>(MB_MODE_COUNT, REF_FRAMES);
    // Array to hold flag indicating if scaled reference frame is used.
    internal readonly bool[] use_scaled_ref_frame = new bool[REF_FRAMES];
}
