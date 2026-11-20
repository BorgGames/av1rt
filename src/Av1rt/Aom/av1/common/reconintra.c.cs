// Port of libaom av1/common/reconintra.c
// [subset] Only DC_PRED, V_PRED, H_PRED and SMOOTH_PRED are ever selected in the realtime
// subset (no angle delta, no filter intra, no palette, no CfL, intra edge filter off), so the
// directional (zone 1/2/3), filter-intra, intra-edge-filter/upsample and palette paths, and
// the has_top_right / has_bottom_left availability tables they need, are not ported; reaching
// them throws NotSupportedException.

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal delegate void intra_pred_fn(Ptr<byte> dst, int stride, Ptr<byte> above,
                                     Ptr<byte> left);

internal static partial class reconintra
{
    // enum { NEED_LEFT, NEED_ABOVE, NEED_ABOVERIGHT, NEED_ABOVELEFT, NEED_BOTTOMLEFT }
    private const int NEED_LEFT = 1 << 1;
    private const int NEED_ABOVE = 1 << 2;
    private const int NEED_ABOVERIGHT = 1 << 3;
    private const int NEED_ABOVELEFT = 1 << 4;
    private const int NEED_BOTTOMLEFT = 1 << 5;

    private const int INTRA_EDGE_FILT = 3;
    private const int INTRA_EDGE_TAPS = 5;
    private const int MAX_UPSAMPLE_SZ = 16;
    private const int NUM_INTRA_NEIGHBOUR_PIXELS = (MAX_TX_SIZE * 2 + 32);

    private static readonly byte[] extend_modes = {
        NEED_ABOVE | NEED_LEFT,                   // DC
        NEED_ABOVE,                               // V
        NEED_LEFT,                                // H
        NEED_ABOVE | NEED_ABOVERIGHT,             // D45
        NEED_LEFT | NEED_ABOVE | NEED_ABOVELEFT,  // D135
        NEED_LEFT | NEED_ABOVE | NEED_ABOVELEFT,  // D113
        NEED_LEFT | NEED_ABOVE | NEED_ABOVELEFT,  // D157
        NEED_LEFT | NEED_BOTTOMLEFT,              // D203
        NEED_ABOVE | NEED_ABOVERIGHT,             // D67
        NEED_LEFT | NEED_ABOVE,                   // SMOOTH
        NEED_LEFT | NEED_ABOVE,                   // SMOOTH_V
        NEED_LEFT | NEED_ABOVE,                   // SMOOTH_H
        NEED_LEFT | NEED_ABOVE | NEED_ABOVELEFT,  // PAETH
    };

    // [subset] has_tr_*, has_tr_tables, has_tr_vert_*, get_has_tr_table, has_top_right,
    // has_bl_*, has_bl_tables, has_bl_vert_*, get_has_bl_table, has_bottom_left: only needed
    // for directional modes with p_angle < 90 or > 180 (never selected).

    private static readonly intra_pred_fn[][] pred = new_jagged<intra_pred_fn>(INTRA_MODES, TX_SIZES_ALL);
    private static readonly intra_pred_fn[][][] dc_pred = new_jagged<intra_pred_fn>(2, 2, TX_SIZES_ALL);

    private static void init_intra_predictors_internal()
    {
        Debug.Assert(mode_to_angle_map.Length == INTRA_MODES);

        // [port] The INIT_ALL_SIZES(p, type) macro expansions (CONFIG_AV1_DECODER is set in the
        // oracle build, so the full rectangular list is used).
        // INIT_ALL_SIZES(pred[V_PRED], v)
        pred[V_PRED][TX_4X4] = aom_v_predictor_4x4;
        pred[V_PRED][TX_8X8] = aom_v_predictor_8x8;
        pred[V_PRED][TX_16X16] = aom_v_predictor_16x16;
        pred[V_PRED][TX_32X32] = aom_v_predictor_32x32;
        pred[V_PRED][TX_64X64] = aom_v_predictor_64x64;
        pred[V_PRED][TX_4X8] = aom_v_predictor_4x8;
        pred[V_PRED][TX_8X4] = aom_v_predictor_8x4;
        pred[V_PRED][TX_8X16] = aom_v_predictor_8x16;
        pred[V_PRED][TX_16X8] = aom_v_predictor_16x8;
        pred[V_PRED][TX_16X32] = aom_v_predictor_16x32;
        pred[V_PRED][TX_32X16] = aom_v_predictor_32x16;
        pred[V_PRED][TX_32X64] = aom_v_predictor_32x64;
        pred[V_PRED][TX_64X32] = aom_v_predictor_64x32;
        pred[V_PRED][TX_4X16] = aom_v_predictor_4x16;
        pred[V_PRED][TX_16X4] = aom_v_predictor_16x4;
        pred[V_PRED][TX_8X32] = aom_v_predictor_8x32;
        pred[V_PRED][TX_32X8] = aom_v_predictor_32x8;
        pred[V_PRED][TX_16X64] = aom_v_predictor_16x64;
        pred[V_PRED][TX_64X16] = aom_v_predictor_64x16;
        // INIT_ALL_SIZES(pred[H_PRED], h)
        pred[H_PRED][TX_4X4] = aom_h_predictor_4x4;
        pred[H_PRED][TX_8X8] = aom_h_predictor_8x8;
        pred[H_PRED][TX_16X16] = aom_h_predictor_16x16;
        pred[H_PRED][TX_32X32] = aom_h_predictor_32x32;
        pred[H_PRED][TX_64X64] = aom_h_predictor_64x64;
        pred[H_PRED][TX_4X8] = aom_h_predictor_4x8;
        pred[H_PRED][TX_8X4] = aom_h_predictor_8x4;
        pred[H_PRED][TX_8X16] = aom_h_predictor_8x16;
        pred[H_PRED][TX_16X8] = aom_h_predictor_16x8;
        pred[H_PRED][TX_16X32] = aom_h_predictor_16x32;
        pred[H_PRED][TX_32X16] = aom_h_predictor_32x16;
        pred[H_PRED][TX_32X64] = aom_h_predictor_32x64;
        pred[H_PRED][TX_64X32] = aom_h_predictor_64x32;
        pred[H_PRED][TX_4X16] = aom_h_predictor_4x16;
        pred[H_PRED][TX_16X4] = aom_h_predictor_16x4;
        pred[H_PRED][TX_8X32] = aom_h_predictor_8x32;
        pred[H_PRED][TX_32X8] = aom_h_predictor_32x8;
        pred[H_PRED][TX_16X64] = aom_h_predictor_16x64;
        pred[H_PRED][TX_64X16] = aom_h_predictor_64x16;
        // INIT_ALL_SIZES(pred[SMOOTH_PRED], smooth)
        pred[SMOOTH_PRED][TX_4X4] = aom_smooth_predictor_4x4;
        pred[SMOOTH_PRED][TX_8X8] = aom_smooth_predictor_8x8;
        pred[SMOOTH_PRED][TX_16X16] = aom_smooth_predictor_16x16;
        pred[SMOOTH_PRED][TX_32X32] = aom_smooth_predictor_32x32;
        pred[SMOOTH_PRED][TX_64X64] = aom_smooth_predictor_64x64;
        pred[SMOOTH_PRED][TX_4X8] = aom_smooth_predictor_4x8;
        pred[SMOOTH_PRED][TX_8X4] = aom_smooth_predictor_8x4;
        pred[SMOOTH_PRED][TX_8X16] = aom_smooth_predictor_8x16;
        pred[SMOOTH_PRED][TX_16X8] = aom_smooth_predictor_16x8;
        pred[SMOOTH_PRED][TX_16X32] = aom_smooth_predictor_16x32;
        pred[SMOOTH_PRED][TX_32X16] = aom_smooth_predictor_32x16;
        pred[SMOOTH_PRED][TX_32X64] = aom_smooth_predictor_32x64;
        pred[SMOOTH_PRED][TX_64X32] = aom_smooth_predictor_64x32;
        pred[SMOOTH_PRED][TX_4X16] = aom_smooth_predictor_4x16;
        pred[SMOOTH_PRED][TX_16X4] = aom_smooth_predictor_16x4;
        pred[SMOOTH_PRED][TX_8X32] = aom_smooth_predictor_8x32;
        pred[SMOOTH_PRED][TX_32X8] = aom_smooth_predictor_32x8;
        pred[SMOOTH_PRED][TX_16X64] = aom_smooth_predictor_16x64;
        pred[SMOOTH_PRED][TX_64X16] = aom_smooth_predictor_64x16;
        // INIT_ALL_SIZES(dc_pred[0][0], dc_128)
        dc_pred[0][0][TX_4X4] = aom_dc_128_predictor_4x4;
        dc_pred[0][0][TX_8X8] = aom_dc_128_predictor_8x8;
        dc_pred[0][0][TX_16X16] = aom_dc_128_predictor_16x16;
        dc_pred[0][0][TX_32X32] = aom_dc_128_predictor_32x32;
        dc_pred[0][0][TX_64X64] = aom_dc_128_predictor_64x64;
        dc_pred[0][0][TX_4X8] = aom_dc_128_predictor_4x8;
        dc_pred[0][0][TX_8X4] = aom_dc_128_predictor_8x4;
        dc_pred[0][0][TX_8X16] = aom_dc_128_predictor_8x16;
        dc_pred[0][0][TX_16X8] = aom_dc_128_predictor_16x8;
        dc_pred[0][0][TX_16X32] = aom_dc_128_predictor_16x32;
        dc_pred[0][0][TX_32X16] = aom_dc_128_predictor_32x16;
        dc_pred[0][0][TX_32X64] = aom_dc_128_predictor_32x64;
        dc_pred[0][0][TX_64X32] = aom_dc_128_predictor_64x32;
        dc_pred[0][0][TX_4X16] = aom_dc_128_predictor_4x16;
        dc_pred[0][0][TX_16X4] = aom_dc_128_predictor_16x4;
        dc_pred[0][0][TX_8X32] = aom_dc_128_predictor_8x32;
        dc_pred[0][0][TX_32X8] = aom_dc_128_predictor_32x8;
        dc_pred[0][0][TX_16X64] = aom_dc_128_predictor_16x64;
        dc_pred[0][0][TX_64X16] = aom_dc_128_predictor_64x16;
        // INIT_ALL_SIZES(dc_pred[0][1], dc_top)
        dc_pred[0][1][TX_4X4] = aom_dc_top_predictor_4x4;
        dc_pred[0][1][TX_8X8] = aom_dc_top_predictor_8x8;
        dc_pred[0][1][TX_16X16] = aom_dc_top_predictor_16x16;
        dc_pred[0][1][TX_32X32] = aom_dc_top_predictor_32x32;
        dc_pred[0][1][TX_64X64] = aom_dc_top_predictor_64x64;
        dc_pred[0][1][TX_4X8] = aom_dc_top_predictor_4x8;
        dc_pred[0][1][TX_8X4] = aom_dc_top_predictor_8x4;
        dc_pred[0][1][TX_8X16] = aom_dc_top_predictor_8x16;
        dc_pred[0][1][TX_16X8] = aom_dc_top_predictor_16x8;
        dc_pred[0][1][TX_16X32] = aom_dc_top_predictor_16x32;
        dc_pred[0][1][TX_32X16] = aom_dc_top_predictor_32x16;
        dc_pred[0][1][TX_32X64] = aom_dc_top_predictor_32x64;
        dc_pred[0][1][TX_64X32] = aom_dc_top_predictor_64x32;
        dc_pred[0][1][TX_4X16] = aom_dc_top_predictor_4x16;
        dc_pred[0][1][TX_16X4] = aom_dc_top_predictor_16x4;
        dc_pred[0][1][TX_8X32] = aom_dc_top_predictor_8x32;
        dc_pred[0][1][TX_32X8] = aom_dc_top_predictor_32x8;
        dc_pred[0][1][TX_16X64] = aom_dc_top_predictor_16x64;
        dc_pred[0][1][TX_64X16] = aom_dc_top_predictor_64x16;
        // INIT_ALL_SIZES(dc_pred[1][0], dc_left)
        dc_pred[1][0][TX_4X4] = aom_dc_left_predictor_4x4;
        dc_pred[1][0][TX_8X8] = aom_dc_left_predictor_8x8;
        dc_pred[1][0][TX_16X16] = aom_dc_left_predictor_16x16;
        dc_pred[1][0][TX_32X32] = aom_dc_left_predictor_32x32;
        dc_pred[1][0][TX_64X64] = aom_dc_left_predictor_64x64;
        dc_pred[1][0][TX_4X8] = aom_dc_left_predictor_4x8;
        dc_pred[1][0][TX_8X4] = aom_dc_left_predictor_8x4;
        dc_pred[1][0][TX_8X16] = aom_dc_left_predictor_8x16;
        dc_pred[1][0][TX_16X8] = aom_dc_left_predictor_16x8;
        dc_pred[1][0][TX_16X32] = aom_dc_left_predictor_16x32;
        dc_pred[1][0][TX_32X16] = aom_dc_left_predictor_32x16;
        dc_pred[1][0][TX_32X64] = aom_dc_left_predictor_32x64;
        dc_pred[1][0][TX_64X32] = aom_dc_left_predictor_64x32;
        dc_pred[1][0][TX_4X16] = aom_dc_left_predictor_4x16;
        dc_pred[1][0][TX_16X4] = aom_dc_left_predictor_16x4;
        dc_pred[1][0][TX_8X32] = aom_dc_left_predictor_8x32;
        dc_pred[1][0][TX_32X8] = aom_dc_left_predictor_32x8;
        dc_pred[1][0][TX_16X64] = aom_dc_left_predictor_16x64;
        dc_pred[1][0][TX_64X16] = aom_dc_left_predictor_64x16;
        // INIT_ALL_SIZES(dc_pred[1][1], dc)
        dc_pred[1][1][TX_4X4] = aom_dc_predictor_4x4;
        dc_pred[1][1][TX_8X8] = aom_dc_predictor_8x8;
        dc_pred[1][1][TX_16X16] = aom_dc_predictor_16x16;
        dc_pred[1][1][TX_32X32] = aom_dc_predictor_32x32;
        dc_pred[1][1][TX_64X64] = aom_dc_predictor_64x64;
        dc_pred[1][1][TX_4X8] = aom_dc_predictor_4x8;
        dc_pred[1][1][TX_8X4] = aom_dc_predictor_8x4;
        dc_pred[1][1][TX_8X16] = aom_dc_predictor_8x16;
        dc_pred[1][1][TX_16X8] = aom_dc_predictor_16x8;
        dc_pred[1][1][TX_16X32] = aom_dc_predictor_16x32;
        dc_pred[1][1][TX_32X16] = aom_dc_predictor_32x16;
        dc_pred[1][1][TX_32X64] = aom_dc_predictor_32x64;
        dc_pred[1][1][TX_64X32] = aom_dc_predictor_64x32;
        dc_pred[1][1][TX_4X16] = aom_dc_predictor_4x16;
        dc_pred[1][1][TX_16X4] = aom_dc_predictor_16x4;
        dc_pred[1][1][TX_8X32] = aom_dc_predictor_8x32;
        dc_pred[1][1][TX_32X8] = aom_dc_predictor_32x8;
        dc_pred[1][1][TX_16X64] = aom_dc_predictor_16x64;
        dc_pred[1][1][TX_64X16] = aom_dc_predictor_64x16;
        // [subset] INIT_ALL_SIZES(pred[PAETH_PRED], paeth), (pred[SMOOTH_V_PRED], smooth_v),
        // (pred[SMOOTH_H_PRED], smooth_h): those modes are never selected; the entries stay
        // null.
    }

    private static readonly Action s_init_intra_predictors_internal = init_intra_predictors_internal;

    // [subset] av1_dr_prediction_z1_c, av1_dr_prediction_z2_c, av1_dr_prediction_z3_c,
    // dr_predictor, av1_filter_intra_taps, av1_filter_intra_predictor_c, is_smooth,
    // get_intra_edge_filter_type, intra_edge_filter_strength, av1_filter_intra_edge_c,
    // filter_intra_edge_corner, av1_upsample_intra_edge_c: never executed (see file comment).

    private static void copy_above_row_lbd(Ptr<byte> above_row,
                                           Ptr<byte> above_ref,
                                           int n_copy)
    {
        // [port] C branches on n_copy (4/8/16/32/64/128/other) only to give the compiler fixed
        // memcpy sizes; every branch copies n_copy bytes.
        memcpy(above_row, above_ref, n_copy);
    }

    [ThreadStatic] private static byte[] s_dir_left_data;
    [ThreadStatic] private static byte[] s_dir_above_data;

    private static void build_directional_and_filter_intra_predictors(
        Ptr<byte> @ref, int ref_stride, Ptr<byte> dst, int dst_stride,
        PREDICTION_MODE mode, int p_angle, FILTER_INTRA_MODE filter_intra_mode,
        TX_SIZE tx_size, int disable_edge_filter, int n_top_px, int n_topright_px,
        int n_left_px, int n_bottomleft_px, int intra_edge_filter_type)
    {
        int i;
        Ptr<byte> above_ref = @ref - ref_stride;
        Ptr<byte> left_ref = @ref - 1;
        int txwpx = tx_size_wide[tx_size];
        int txhpx = tx_size_high[tx_size];
        int is_dr_mode = av1_is_directional_mode(mode);
        int use_filter_intra = B(filter_intra_mode != FILTER_INTRA_MODES);
        Debug.Assert(use_filter_intra != 0 || is_dr_mode != 0);
        int need_above = B(use_filter_intra != 0 || (p_angle < 180));
        int need_left = B(use_filter_intra != 0 || (p_angle > 90));
        const int need_above_left = 1;

        Debug.Assert(n_top_px >= 0);
        Debug.Assert(n_topright_px >= -1);
        Debug.Assert(n_left_px >= 0);
        Debug.Assert(n_bottomleft_px >= -1);

        if ((need_above == 0 && n_left_px == 0) || (need_left == 0 && n_top_px == 0))
        {
            int val;
            if (need_left != 0)
            {
                val = (n_top_px > 0) ? above_ref[0] : 129;
            }
            else
            {
                val = (n_left_px > 0) ? left_ref[0] : 127;
            }
            for (i = 0; i < txhpx; ++i)
            {
                memset(dst, (byte)val, txwpx);
                dst += dst_stride;
            }
            return;
        }

        // [port] DECLARE_ALIGNED(16, uint8_t, left_data / above_data[NUM_INTRA_NEIGHBOUR_PIXELS]):
        // per-thread scratch (leaf function).
        byte[] left_data = s_dir_left_data ??= new byte[NUM_INTRA_NEIGHBOUR_PIXELS];
        byte[] above_data = s_dir_above_data ??= new byte[NUM_INTRA_NEIGHBOUR_PIXELS];
        Ptr<byte> above_row = new Ptr<byte>(above_data, 16);
        Ptr<byte> left_col = new Ptr<byte>(left_data, 16);

        // NEED_LEFT
        if (need_left != 0)
        {
            int num_left_pixels_needed =
                txhpx + (n_bottomleft_px >= 0 ? txwpx : 0);
            if (n_left_px > 0)
            {
                int n_left_copy =
                    n_left_px + (n_bottomleft_px > 0 ? n_bottomleft_px : 0);
                Debug.Assert(n_left_copy > 0);
                for (i = 0; i < n_left_copy; i++) left_col[i] = left_ref[i * ref_stride];
                if (i < num_left_pixels_needed)
                    memset(left_col + i, left_col[i - 1], num_left_pixels_needed - i);
            }
            else
            {
                byte val = (n_top_px > 0) ? above_ref[0] : (byte)129;
                memset(left_col, val, num_left_pixels_needed);
            }
        }

        // NEED_ABOVE
        if (need_above != 0)
        {
            int num_top_pixels_needed = txwpx + (n_topright_px >= 0 ? txhpx : 0);
            if (n_top_px > 0)
            {
                int n_top_copy = n_top_px + (n_topright_px > 0 ? n_topright_px : 0);
                copy_above_row_lbd(above_row, above_ref, n_top_copy);
                if (n_top_copy < num_top_pixels_needed)
                    memset(above_row + n_top_copy, above_row[n_top_copy - 1],
                           num_top_pixels_needed - n_top_copy);
            }
            else
            {
                byte val = (n_left_px > 0) ? left_ref[0] : (byte)127;
                memset(above_row, val, num_top_pixels_needed);
            }
        }

        // Pure vertical (90 deg) and horizontal (180 deg) directional predictors do
        // not require the top-left corner pixel or intra edge filtering.
        if (use_filter_intra == 0)
        {
            if (p_angle == 90)
            {
                pred[V_PRED][tx_size](dst, dst_stride, above_row, left_col);
                return;
            }
            if (p_angle == 180)
            {
                pred[H_PRED][tx_size](dst, dst_stride, above_row, left_col);
                return;
            }
        }

        // [subset] Everything below (top-left corner set-up, filter intra, intra edge filter /
        // upsampling and dr_predictor) only runs for filter intra or directional modes other
        // than V_PRED / H_PRED, which are never selected.
        _ = need_above_left;
        throw new NotSupportedException(
            "build_directional_and_filter_intra_predictors: filter intra / oblique directional mode");
    }

    [ThreadStatic] private static byte[] s_nondir_left_data;
    [ThreadStatic] private static byte[] s_nondir_above_data;

    // This function generates the pred data of a given block for non-directional
    // intra prediction modes (i.e., DC, SMOOTH, SMOOTH_H, SMOOTH_V and PAETH).
    private static void build_non_directional_intra_predictors(
        Ptr<byte> @ref, int ref_stride, Ptr<byte> dst, int dst_stride,
        PREDICTION_MODE mode, TX_SIZE tx_size, int n_top_px, int n_left_px)
    {
        Ptr<byte> above_ref = @ref - ref_stride;
        Ptr<byte> left_ref = @ref - 1;
        int txwpx = tx_size_wide[tx_size];
        int txhpx = tx_size_high[tx_size];
        int need_left = extend_modes[mode] & NEED_LEFT;
        int need_above = extend_modes[mode] & NEED_ABOVE;
        int need_above_left = extend_modes[mode] & NEED_ABOVELEFT;
        int i = 0;
        Debug.Assert(n_top_px >= 0);
        Debug.Assert(n_left_px >= 0);
        Debug.Assert(mode == DC_PRED || mode == SMOOTH_PRED || mode == SMOOTH_V_PRED ||
                     mode == SMOOTH_H_PRED || mode == PAETH_PRED);

        if ((need_above == 0 && n_left_px == 0) || (need_left == 0 && n_top_px == 0))
        {
            int val = 0;
            if (need_left != 0)
            {
                val = (n_top_px > 0) ? above_ref[0] : 129;
            }
            else
            {
                val = (n_left_px > 0) ? left_ref[0] : 127;
            }
            for (i = 0; i < txhpx; ++i)
            {
                memset(dst, (byte)val, txwpx);
                dst += dst_stride;
            }
            return;
        }

        // [port] DECLARE_ALIGNED(16, uint8_t, left_data / above_data[NUM_INTRA_NEIGHBOUR_PIXELS]):
        // per-thread scratch (leaf function).
        byte[] left_data = s_nondir_left_data ??= new byte[NUM_INTRA_NEIGHBOUR_PIXELS];
        byte[] above_data = s_nondir_above_data ??= new byte[NUM_INTRA_NEIGHBOUR_PIXELS];
        Ptr<byte> above_row = new Ptr<byte>(above_data, 16);
        Ptr<byte> left_col = new Ptr<byte>(left_data, 16);

        if (need_left != 0)
        {
            if (n_left_px > 0)
            {
                for (i = 0; i < n_left_px; i++) left_col[i] = left_ref[i * ref_stride];
                if (i < txhpx) memset(left_col + i, left_col[i - 1], txhpx - i);
            }
            else
            {
                byte val = (n_top_px > 0) ? above_ref[0] : (byte)129;
                memset(left_col, val, txhpx);
            }
        }

        if (need_above != 0)
        {
            if (n_top_px == txwpx)
            {
                copy_above_row_lbd(above_row, above_ref, txwpx);
            }
            else if (n_top_px > 0)
            {
                copy_above_row_lbd(above_row, above_ref, n_top_px);
                memset(above_row + n_top_px, above_row[n_top_px - 1], txwpx - n_top_px);
            }
            else
            {
                byte val = (n_left_px > 0) ? left_ref[0] : (byte)127;
                memset(above_row, val, txwpx);
            }
        }

        if (need_above_left != 0)
        {
            // aom_paeth_predictor_{4x4,8x4}_ssse3 load 8 bytes from left and above
            // before shuffling with a runtime mask; zero bytes 4..7 to avoid
            // uninitialized reads under Valgrind/MemorySanitizer (see aomedia:2752).
            if (txhpx == 4) memset(left_col + 4, (byte)0, 4);
            if (txwpx == 4) memset(above_row + 4, (byte)0, 4);
            if (n_top_px > 0 && n_left_px > 0)
            {
                above_row[-1] = above_ref[-1];
            }
            else if (n_top_px > 0)
            {
                above_row[-1] = above_ref[0];
            }
            else if (n_left_px > 0)
            {
                above_row[-1] = left_ref[0];
            }
            else
            {
                above_row[-1] = 128;
            }
            left_col[-1] = above_row[-1];
        }

        if (mode == DC_PRED)
        {
            dc_pred[B(n_left_px > 0)][B(n_top_px > 0)][tx_size](dst, dst_stride, above_row,
                                                                 left_col);
        }
        else
        {
            pred[mode][tx_size](dst, dst_stride, above_row, left_col);
        }
    }

    // [subset] av1_highbd_filter_intra_edge_c, highbd_* predictors: high bitdepth is compiled
    // out. scale_chroma_bsize (directional modes only) and predict_palette_block (palette) are
    // never executed.

    private static void predict_intra_block_internal(
        MACROBLOCKD xd, BLOCK_SIZE sb_size, int enable_intra_edge_filter,
        int wpx, int hpx, TX_SIZE tx_size, PREDICTION_MODE mode, int angle_delta,
        int use_palette, FILTER_INTRA_MODE filter_intra_mode, Ptr<byte> @ref,
        int ref_stride, Ptr<byte> dst, int dst_stride, int col_off, int row_off,
        int plane)
    {
        MB_MODE_INFO mbmi = xd.mi[0];
        int txwpx = tx_size_wide[tx_size];
        int txhpx = tx_size_high[tx_size];
        int x = col_off << MI_SIZE_LOG2;
        int y = row_off << MI_SIZE_LOG2;
        int is_hbd = is_cur_buf_hbd(xd);

        Debug.Assert(mode < INTRA_MODES);

        if (use_palette != 0)
        {
            // [subset] predict_palette_block: palette is disabled.
            throw new NotSupportedException("predict_intra_block_internal: palette");
        }

        macroblockd_plane pd = xd.plane[plane];
        int ss_x = pd.subsampling_x;
        int ss_y = pd.subsampling_y;
        int have_top =
            B(row_off != 0 || (ss_y != 0 ? xd.chroma_up_available : xd.up_available));
        int have_left =
            B(col_off != 0 || (ss_x != 0 ? xd.chroma_left_available : xd.left_available));

        // Distance between the right edge of this prediction block to
        // the frame right edge
        int xr = (xd.mb_to_right_edge >> (3 + ss_x)) + wpx - x - txwpx;
        // Distance between the bottom edge of this prediction block to
        // the frame bottom edge
        int yd = (xd.mb_to_bottom_edge >> (3 + ss_y)) + hpx - y - txhpx;
        int use_filter_intra = B(filter_intra_mode != FILTER_INTRA_MODES);
        int is_dr_mode = av1_is_directional_mode(mode);

        int n_top_px = have_top != 0 ? clamp(xr + txwpx, 0, txwpx) : 0;
        int n_left_px = have_left != 0 ? clamp(yd + txhpx, 0, txhpx) : 0;
        if (use_filter_intra == 0 && is_dr_mode == 0)
        {
            build_non_directional_intra_predictors(@ref, ref_stride, dst, dst_stride,
                                                   mode, tx_size, n_top_px, n_left_px);
            return;
        }

        int p_angle = 0;
        int n_topright_px = -1;
        int n_bottomleft_px = -1;

        if (is_dr_mode != 0)
        {
            p_angle = mode_to_angle_map[mode] + angle_delta;
            if (p_angle < 90)
            {
                // [subset] Top-right availability (has_top_right, scale_chroma_bsize): only for
                // zone 1/2 directional modes, never selected.
                throw new NotSupportedException("predict_intra_block_internal: p_angle < 90");
            }
            else if (p_angle > 180)
            {
                // [subset] Bottom-left availability (has_bottom_left): only for zone 3
                // directional modes, never selected.
                throw new NotSupportedException("predict_intra_block_internal: p_angle > 180");
            }
        }

        int disable_edge_filter = B(enable_intra_edge_filter == 0);
        int intra_edge_filter_type = 0;
        if (disable_edge_filter == 0 && use_filter_intra == 0 && p_angle != 90 &&
            p_angle != 180)
        {
            // [subset] get_intra_edge_filter_type: only for oblique directional modes, never
            // selected (and the intra edge filter is off in the subset).
            throw new NotSupportedException("predict_intra_block_internal: intra edge filter");
        }
        build_directional_and_filter_intra_predictors(
            @ref, ref_stride, dst, dst_stride, mode, p_angle, filter_intra_mode,
            tx_size, disable_edge_filter, n_top_px, n_topright_px, n_left_px,
            n_bottomleft_px, intra_edge_filter_type);
    }

    internal static void av1_predict_intra_block(MACROBLOCKD xd, BLOCK_SIZE sb_size,
                                                 int enable_intra_edge_filter, int wpx, int hpx,
                                                 TX_SIZE tx_size, PREDICTION_MODE mode,
                                                 int angle_delta, int use_palette,
                                                 FILTER_INTRA_MODE filter_intra_mode,
                                                 Ptr<byte> @ref, int ref_stride, Ptr<byte> dst,
                                                 int dst_stride, int col_off, int row_off,
                                                 int plane)
    {
        predict_intra_block_internal(xd, sb_size, enable_intra_edge_filter, wpx, hpx,
                                     tx_size, mode, angle_delta, use_palette,
                                     filter_intra_mode, @ref, ref_stride, dst,
                                     dst_stride, col_off, row_off, plane);
    }

    internal static void av1_predict_intra_block_facade(AV1_COMMON cm, MACROBLOCKD xd,
                                                        int plane, int blk_col, int blk_row,
                                                        TX_SIZE tx_size)
    {
        MB_MODE_INFO mbmi = xd.mi[0];
        macroblockd_plane pd = xd.plane[plane];
        int dst_stride = pd.dst.stride;
        Ptr<byte> dst = pd.dst.buf + ((blk_row * dst_stride + blk_col) << MI_SIZE_LOG2);
        PREDICTION_MODE mode =
            (plane == AOM_PLANE_Y) ? mbmi.mode : get_uv_mode(mbmi.uv_mode);
        int use_palette = B(mbmi.palette_mode_info.palette_size[B(plane != 0)] > 0);
        FILTER_INTRA_MODE filter_intra_mode =
            (plane == AOM_PLANE_Y && mbmi.filter_intra_mode_info.use_filter_intra != 0)
                ? mbmi.filter_intra_mode_info.filter_intra_mode
                : FILTER_INTRA_MODES;
        int angle_delta = mbmi.angle_delta[B(plane != AOM_PLANE_Y)] * ANGLE_STEP;
        SequenceHeader seq_params = cm.seq_params;

        // !CONFIG_REALTIME_ONLY || CONFIG_AV1_DECODER (the oracle build has the decoder)
        if (plane != AOM_PLANE_Y && mbmi.uv_mode == UV_CFL_PRED)
        {
            // [subset] CfL prediction (cfl_store_dc_pred / cfl_load_dc_pred /
            // av1_cfl_predict_block): CfL is disabled in the subset.
            throw new NotSupportedException("av1_predict_intra_block_facade: CfL");
        }
        predict_intra_block_internal(
            xd, seq_params.sb_size, seq_params.enable_intra_edge_filter, pd.width,
            pd.height, tx_size, mode, angle_delta, use_palette, filter_intra_mode,
            dst, dst_stride, dst, dst_stride, blk_col, blk_row, plane);
    }

    internal static void av1_init_intra_predictors()
    {
        aom_once(s_init_intra_predictors_internal);
    }
}
