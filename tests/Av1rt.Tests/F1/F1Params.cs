using System;
using Av1rt.Aom;
using Xunit;

namespace Av1rt.Tests.F1;

/// <summary>
/// Flat int parameter arrays for the sequence / frame header tests. The layouts mirror the
/// SEQ_* / FH_* enums of tools/oracle/shim/shim_f1.c (same order); the shim builds its
/// SequenceHeader / AV1_COMP from them and so does <see cref="BuildSeq"/> / <see cref="BuildCpi"/>.
/// </summary>
internal static class F1Params
{
    private const int REF_FRAMES = 8, INTER_REFS_PER_FRAME = 7, MAX_MODE_LF_DELTAS = 2;
    private const int CDEF_MAX_STRENGTHS = 16, MAX_SEGMENTS = 8, SEG_LVL_MAX = 8;

    // ---- SEQ_* ------------------------------------------------------------------------------
    public const int SEQ_PROFILE = 0;
    public const int SEQ_STILL_PICTURE = 1;
    public const int SEQ_REDUCED_STILL_PICTURE_HDR = 2;
    public const int SEQ_OPERATING_POINTS_CNT_MINUS_1 = 3;
    public const int SEQ_HAS_NONZERO_OPERATING_POINT_IDC = 4;
    public const int SEQ_DISPLAY_MODEL_INFO_PRESENT_FLAG = 5;
    public const int SEQ_DISPLAY_MODEL_PARAM_PRESENT_FLAG = 6;
    public const int SEQ_INITIAL_DISPLAY_DELAY = 7;
    public const int SEQ_NUM_BITS_WIDTH = 8;
    public const int SEQ_NUM_BITS_HEIGHT = 9;
    public const int SEQ_MAX_FRAME_WIDTH = 10;
    public const int SEQ_MAX_FRAME_HEIGHT = 11;
    public const int SEQ_FRAME_ID_NUMBERS_PRESENT_FLAG = 12;
    public const int SEQ_DELTA_FRAME_ID_LENGTH = 13;
    public const int SEQ_FRAME_ID_LENGTH = 14;
    public const int SEQ_SB_SIZE = 15;
    public const int SEQ_ENABLE_FILTER_INTRA = 16;
    public const int SEQ_ENABLE_INTRA_EDGE_FILTER = 17;
    public const int SEQ_ENABLE_INTERINTRA_COMPOUND = 18;
    public const int SEQ_ENABLE_MASKED_COMPOUND = 19;
    public const int SEQ_ENABLE_WARPED_MOTION = 20;
    public const int SEQ_ENABLE_DUAL_FILTER = 21;
    public const int SEQ_ENABLE_ORDER_HINT = 22;
    public const int SEQ_ENABLE_DIST_WTD_COMP = 23;
    public const int SEQ_ENABLE_REF_FRAME_MVS = 24;
    public const int SEQ_ORDER_HINT_BITS_MINUS_1 = 25;
    public const int SEQ_FORCE_SCREEN_CONTENT_TOOLS = 26;
    public const int SEQ_FORCE_INTEGER_MV = 27;
    public const int SEQ_ENABLE_SUPERRES = 28;
    public const int SEQ_ENABLE_CDEF = 29;
    public const int SEQ_ENABLE_RESTORATION = 30;
    public const int SEQ_BIT_DEPTH = 31;
    public const int SEQ_MONOCHROME = 32;
    public const int SEQ_COLOR_PRIMARIES = 33;
    public const int SEQ_TRANSFER_CHARACTERISTICS = 34;
    public const int SEQ_MATRIX_COEFFICIENTS = 35;
    public const int SEQ_COLOR_RANGE = 36;
    public const int SEQ_SUBSAMPLING_X = 37;
    public const int SEQ_SUBSAMPLING_Y = 38;
    public const int SEQ_CHROMA_SAMPLE_POSITION = 39;
    public const int SEQ_SEPARATE_UV_DELTA_Q = 40;
    public const int SEQ_FILM_GRAIN_PARAMS_PRESENT = 41;
    public const int SEQ_OP_BASE = 42;  // 4 x { operating_point_idc, seq_level_idx, tier }
    public const int SEQ_COUNT = SEQ_OP_BASE + 12;

    // ---- FH_* -------------------------------------------------------------------------------
    public const int FH_FRAME_TYPE = 0;
    public const int FH_SHOW_FRAME = 1;
    public const int FH_SHOWABLE_FRAME = 2;
    public const int FH_SHOW_EXISTING_FRAME = 3;
    public const int FH_EXISTING_FB_IDX_TO_SHOW = 4;
    public const int FH_ERROR_RESILIENT_MODE = 5;
    public const int FH_DISABLE_CDF_UPDATE = 6;
    public const int FH_ALLOW_SCREEN_CONTENT_TOOLS = 7;
    public const int FH_CUR_FRAME_FORCE_INTEGER_MV = 8;
    public const int FH_CURRENT_FRAME_ID = 9;
    public const int FH_UPSCALED_WIDTH = 10;
    public const int FH_UPSCALED_HEIGHT = 11;
    public const int FH_RENDER_WIDTH = 12;
    public const int FH_RENDER_HEIGHT = 13;
    public const int FH_ORDER_HINT = 14;
    public const int FH_PRIMARY_REF_FRAME = 15;
    public const int FH_REFRESH_FRAME_FLAGS = 16;
    public const int FH_ALLOW_INTRABC = 17;
    public const int FH_ALLOW_HIGH_PRECISION_MV = 18;
    public const int FH_INTERP_FILTER = 19;
    public const int FH_SWITCHABLE_MOTION_MODE = 20;
    public const int FH_ALLOW_REF_FRAME_MVS = 21;
    public const int FH_REFRESH_FRAME_CONTEXT = 22;
    public const int FH_BASE_QINDEX = 23;
    public const int FH_Y_DC_DELTA_Q = 24;
    public const int FH_U_DC_DELTA_Q = 25;
    public const int FH_U_AC_DELTA_Q = 26;
    public const int FH_V_DC_DELTA_Q = 27;
    public const int FH_V_AC_DELTA_Q = 28;
    public const int FH_USING_QMATRIX = 29;
    public const int FH_QM_Y = 30;
    public const int FH_QM_U = 31;
    public const int FH_QM_V = 32;
    public const int FH_SEG_ENABLED = 33;
    public const int FH_SEG_UPDATE_MAP = 34;
    public const int FH_SEG_TEMPORAL_UPDATE = 35;
    public const int FH_SEG_UPDATE_DATA = 36;
    public const int FH_FILTER_LEVEL_0 = 37;
    public const int FH_FILTER_LEVEL_1 = 38;
    public const int FH_FILTER_LEVEL_U = 39;
    public const int FH_FILTER_LEVEL_V = 40;
    public const int FH_SHARPNESS = 41;
    public const int FH_MODE_REF_DELTA_ENABLED = 42;
    public const int FH_MODE_REF_DELTA_UPDATE = 43;
    public const int FH_CDEF_DAMPING = 44;
    public const int FH_CDEF_BITS = 45;
    public const int FH_NB_CDEF_STRENGTHS = 46;
    public const int FH_CODED_LOSSLESS = 47;
    public const int FH_ALL_LOSSLESS = 48;
    public const int FH_TX_MODE = 49;
    public const int FH_REFERENCE_MODE = 50;
    public const int FH_SKIP_MODE_ALLOWED = 51;
    public const int FH_SKIP_MODE_FLAG = 52;
    public const int FH_ALLOW_WARPED_MOTION = 53;
    public const int FH_REDUCED_TX_SET_USED = 54;
    public const int FH_TILE_UNIFORM = 55;
    public const int FH_TILE_LOG2_COLS = 56;
    public const int FH_TILE_LOG2_ROWS = 57;
    public const int FH_TILE_MIN_LOG2_COLS = 58;
    public const int FH_TILE_MAX_LOG2_COLS = 59;
    public const int FH_TILE_MIN_LOG2_ROWS = 60;
    public const int FH_TILE_MAX_LOG2_ROWS = 61;
    public const int FH_TILE_COLS = 62;
    public const int FH_TILE_ROWS = 63;
    public const int FH_TILE_MAX_WIDTH_SB = 64;
    public const int FH_TILE_MAX_HEIGHT_SB = 65;
    public const int FH_MI_COLS = 66;
    public const int FH_MI_ROWS = 67;
    public const int FH_NUM_TG = 68;
    public const int FH_TILE_IDX = 69;
    public const int FH_OBU_EXTN_HEADER = 70;
    public const int FH_KEEP_LEVEL_STATS = 71;
    public const int FH_ENABLE_REF_SHORT_SIGNALING = 72;
    public const int FH_SUPERRES_SCALE_DENOMINATOR = 73;
    public const int FH_HAS_PREV_FRAME = 74;
    public const int FH_REMAPPED_REF_IDX = FH_HAS_PREV_FRAME + 1;
    public const int FH_REF_FRAME_ID = FH_REMAPPED_REF_IDX + INTER_REFS_PER_FRAME;
    public const int FH_LF_REF_DELTAS = FH_REF_FRAME_ID + REF_FRAMES;
    public const int FH_LF_MODE_DELTAS = FH_LF_REF_DELTAS + REF_FRAMES;
    public const int FH_CDEF_STRENGTHS = FH_LF_MODE_DELTAS + MAX_MODE_LF_DELTAS;
    public const int FH_CDEF_UV_STRENGTHS = FH_CDEF_STRENGTHS + CDEF_MAX_STRENGTHS;
    public const int FH_SEG_FEATURE_MASK = FH_CDEF_UV_STRENGTHS + CDEF_MAX_STRENGTHS;
    public const int FH_SEG_FEATURE_DATA = FH_SEG_FEATURE_MASK + MAX_SEGMENTS;
    public const int FH_REF_BUFS = FH_SEG_FEATURE_DATA + MAX_SEGMENTS * SEG_LVL_MAX;
    public const int FH_REF_BUF_STRIDE = 5 + REF_FRAMES + MAX_MODE_LF_DELTAS;
    public const int FH_COL_START_SB = FH_REF_BUFS + REF_FRAMES * FH_REF_BUF_STRIDE;
    public const int FH_ROW_START_SB = FH_COL_START_SB + 17;
    public const int FH_COUNT = FH_ROW_START_SB + 17;

    public static void CheckLayouts()
    {
        Assert.Equal(SEQ_COUNT, F1Shim.f1_seq_count());
        Assert.Equal(FH_COUNT, F1Shim.f1_fh_count());
    }

    public static SequenceHeader BuildSeq(int[] p)
    {
        var s = new SequenceHeader();
        s.profile = p[SEQ_PROFILE];
        s.still_picture = (byte)p[SEQ_STILL_PICTURE];
        s.reduced_still_picture_hdr = (byte)p[SEQ_REDUCED_STILL_PICTURE_HDR];
        s.operating_points_cnt_minus_1 = p[SEQ_OPERATING_POINTS_CNT_MINUS_1];
        s.has_nonzero_operating_point_idc = p[SEQ_HAS_NONZERO_OPERATING_POINT_IDC] != 0;
        s.display_model_info_present_flag = (byte)p[SEQ_DISPLAY_MODEL_INFO_PRESENT_FLAG];
        for (int i = 0; i < 4; ++i)
        {
            s.operating_point_idc[i] = p[SEQ_OP_BASE + 3 * i];
            s.seq_level_idx[i] = p[SEQ_OP_BASE + 3 * i + 1];
            s.tier[i] = (byte)p[SEQ_OP_BASE + 3 * i + 2];
            s.op_params[i].display_model_param_present_flag = p[SEQ_DISPLAY_MODEL_PARAM_PRESENT_FLAG];
            s.op_params[i].initial_display_delay = p[SEQ_INITIAL_DISPLAY_DELAY];
        }
        s.num_bits_width = p[SEQ_NUM_BITS_WIDTH];
        s.num_bits_height = p[SEQ_NUM_BITS_HEIGHT];
        s.max_frame_width = p[SEQ_MAX_FRAME_WIDTH];
        s.max_frame_height = p[SEQ_MAX_FRAME_HEIGHT];
        s.frame_id_numbers_present_flag = (byte)p[SEQ_FRAME_ID_NUMBERS_PRESENT_FLAG];
        s.delta_frame_id_length = p[SEQ_DELTA_FRAME_ID_LENGTH];
        s.frame_id_length = p[SEQ_FRAME_ID_LENGTH];
        s.sb_size = p[SEQ_SB_SIZE];
        s.mib_size = p[SEQ_SB_SIZE] == 15 /* BLOCK_128X128 */ ? 32 : 16;
        s.mib_size_log2 = p[SEQ_SB_SIZE] == 15 ? 5 : 4;
        s.enable_filter_intra = (byte)p[SEQ_ENABLE_FILTER_INTRA];
        s.enable_intra_edge_filter = (byte)p[SEQ_ENABLE_INTRA_EDGE_FILTER];
        s.enable_interintra_compound = (byte)p[SEQ_ENABLE_INTERINTRA_COMPOUND];
        s.enable_masked_compound = (byte)p[SEQ_ENABLE_MASKED_COMPOUND];
        s.enable_warped_motion = (byte)p[SEQ_ENABLE_WARPED_MOTION];
        s.enable_dual_filter = (byte)p[SEQ_ENABLE_DUAL_FILTER];
        s.order_hint_info.enable_order_hint = p[SEQ_ENABLE_ORDER_HINT];
        s.order_hint_info.enable_dist_wtd_comp = p[SEQ_ENABLE_DIST_WTD_COMP];
        s.order_hint_info.enable_ref_frame_mvs = p[SEQ_ENABLE_REF_FRAME_MVS];
        s.order_hint_info.order_hint_bits_minus_1 = p[SEQ_ORDER_HINT_BITS_MINUS_1];
        s.force_screen_content_tools = (byte)p[SEQ_FORCE_SCREEN_CONTENT_TOOLS];
        s.force_integer_mv = (byte)p[SEQ_FORCE_INTEGER_MV];
        s.enable_superres = (byte)p[SEQ_ENABLE_SUPERRES];
        s.enable_cdef = (byte)p[SEQ_ENABLE_CDEF];
        s.enable_restoration = (byte)p[SEQ_ENABLE_RESTORATION];
        s.bit_depth = p[SEQ_BIT_DEPTH];
        s.use_highbitdepth = (byte)(p[SEQ_BIT_DEPTH] > 8 ? 1 : 0);
        s.monochrome = (byte)p[SEQ_MONOCHROME];
        s.color_primaries = p[SEQ_COLOR_PRIMARIES];
        s.transfer_characteristics = p[SEQ_TRANSFER_CHARACTERISTICS];
        s.matrix_coefficients = p[SEQ_MATRIX_COEFFICIENTS];
        s.color_range = p[SEQ_COLOR_RANGE];
        s.subsampling_x = p[SEQ_SUBSAMPLING_X];
        s.subsampling_y = p[SEQ_SUBSAMPLING_Y];
        s.chroma_sample_position = p[SEQ_CHROMA_SAMPLE_POSITION];
        s.separate_uv_delta_q = (byte)p[SEQ_SEPARATE_UV_DELTA_Q];
        s.film_grain_params_present = (byte)p[SEQ_FILM_GRAIN_PARAMS_PRESENT];
        return s;
    }

    /// <summary>The AV1_COMP of the shim's f1_write_obu_tg_tile_headers, in C#.</summary>
    public static AV1_COMP BuildCpi(int[] seq, int[] fh)
    {
        var cpi = new AV1_COMP();
        cpi.ppi = new AV1_PRIMARY();
        AV1_COMMON cm = cpi.common;
        cm.seq_params = BuildSeq(seq);
        cpi.ppi.level_params.keep_level_stats = (uint)fh[FH_KEEP_LEVEL_STATS];
        cpi.sf.rt_sf.enable_ref_short_signaling = fh[FH_ENABLE_REF_SHORT_SIGNALING] != 0;
        cpi.num_tg = fh[FH_NUM_TG];
        cpi.existing_fb_idx_to_show = fh[FH_EXISTING_FB_IDX_TO_SHOW];

        cm.current_frame.frame_type = fh[FH_FRAME_TYPE];
        cm.show_frame = fh[FH_SHOW_FRAME];
        cm.showable_frame = fh[FH_SHOWABLE_FRAME];
        cm.show_existing_frame = fh[FH_SHOW_EXISTING_FRAME];
        FeatureFlags f = cm.features;
        f.error_resilient_mode = fh[FH_ERROR_RESILIENT_MODE] != 0;
        f.disable_cdf_update = fh[FH_DISABLE_CDF_UPDATE] != 0;
        f.allow_screen_content_tools = fh[FH_ALLOW_SCREEN_CONTENT_TOOLS] != 0;
        f.cur_frame_force_integer_mv = fh[FH_CUR_FRAME_FORCE_INTEGER_MV] != 0;
        f.primary_ref_frame = fh[FH_PRIMARY_REF_FRAME];
        f.allow_intrabc = fh[FH_ALLOW_INTRABC] != 0;
        f.allow_high_precision_mv = fh[FH_ALLOW_HIGH_PRECISION_MV] != 0;
        f.interp_filter = fh[FH_INTERP_FILTER];
        f.switchable_motion_mode = fh[FH_SWITCHABLE_MOTION_MODE] != 0;
        f.allow_ref_frame_mvs = fh[FH_ALLOW_REF_FRAME_MVS] != 0;
        f.refresh_frame_context = fh[FH_REFRESH_FRAME_CONTEXT];
        f.coded_lossless = fh[FH_CODED_LOSSLESS] != 0;
        f.all_lossless = fh[FH_ALL_LOSSLESS] != 0;
        f.tx_mode = fh[FH_TX_MODE];
        f.allow_warped_motion = fh[FH_ALLOW_WARPED_MOTION] != 0;
        f.reduced_tx_set_used = fh[FH_REDUCED_TX_SET_USED] != 0;
        cm.current_frame_id = fh[FH_CURRENT_FRAME_ID];
        cm.width = cm.superres_upscaled_width = fh[FH_UPSCALED_WIDTH];
        cm.height = cm.superres_upscaled_height = fh[FH_UPSCALED_HEIGHT];
        cm.render_width = fh[FH_RENDER_WIDTH];
        cm.render_height = fh[FH_RENDER_HEIGHT];
        cm.superres_scale_denominator = (byte)fh[FH_SUPERRES_SCALE_DENOMINATOR];
        cm.current_frame.order_hint = (uint)fh[FH_ORDER_HINT];
        cm.current_frame.refresh_frame_flags = fh[FH_REFRESH_FRAME_FLAGS];
        cm.current_frame.reference_mode = fh[FH_REFERENCE_MODE];
        cm.current_frame.skip_mode_info.skip_mode_allowed = fh[FH_SKIP_MODE_ALLOWED];
        cm.current_frame.skip_mode_info.skip_mode_flag = fh[FH_SKIP_MODE_FLAG];
        cm.mi_params.mi_cols = fh[FH_MI_COLS];
        cm.mi_params.mi_rows = fh[FH_MI_ROWS];

        CommonQuantParams q = cm.quant_params;
        q.base_qindex = fh[FH_BASE_QINDEX];
        q.y_dc_delta_q = fh[FH_Y_DC_DELTA_Q];
        q.u_dc_delta_q = fh[FH_U_DC_DELTA_Q];
        q.u_ac_delta_q = fh[FH_U_AC_DELTA_Q];
        q.v_dc_delta_q = fh[FH_V_DC_DELTA_Q];
        q.v_ac_delta_q = fh[FH_V_AC_DELTA_Q];
        q.using_qmatrix = fh[FH_USING_QMATRIX] != 0;
        q.qmatrix_level_y = fh[FH_QM_Y];
        q.qmatrix_level_u = fh[FH_QM_U];
        q.qmatrix_level_v = fh[FH_QM_V];

        segmentation seg = cm.seg;
        seg.enabled = (byte)fh[FH_SEG_ENABLED];
        seg.update_map = (byte)fh[FH_SEG_UPDATE_MAP];
        seg.temporal_update = (byte)fh[FH_SEG_TEMPORAL_UPDATE];
        seg.update_data = (byte)fh[FH_SEG_UPDATE_DATA];
        for (int i = 0; i < MAX_SEGMENTS; ++i)
        {
            seg.feature_mask[i] = (uint)fh[FH_SEG_FEATURE_MASK + i];
            for (int j = 0; j < SEG_LVL_MAX; ++j)
                seg.feature_data[i][j] = (short)fh[FH_SEG_FEATURE_DATA + i * SEG_LVL_MAX + j];
        }

        loopfilter lf = cm.lf;
        lf.filter_level[0] = fh[FH_FILTER_LEVEL_0];
        lf.filter_level[1] = fh[FH_FILTER_LEVEL_1];
        lf.filter_level_u = fh[FH_FILTER_LEVEL_U];
        lf.filter_level_v = fh[FH_FILTER_LEVEL_V];
        lf.sharpness_level = fh[FH_SHARPNESS];
        lf.mode_ref_delta_enabled = (byte)fh[FH_MODE_REF_DELTA_ENABLED];
        lf.mode_ref_delta_update = (byte)fh[FH_MODE_REF_DELTA_UPDATE];
        for (int i = 0; i < REF_FRAMES; ++i) lf.ref_deltas[i] = (sbyte)fh[FH_LF_REF_DELTAS + i];
        for (int i = 0; i < MAX_MODE_LF_DELTAS; ++i) lf.mode_deltas[i] = (sbyte)fh[FH_LF_MODE_DELTAS + i];

        CdefInfo cdef = cm.cdef_info;
        cdef.cdef_damping = fh[FH_CDEF_DAMPING];
        cdef.cdef_bits = fh[FH_CDEF_BITS];
        cdef.nb_cdef_strengths = fh[FH_NB_CDEF_STRENGTHS];
        for (int i = 0; i < CDEF_MAX_STRENGTHS; ++i)
        {
            cdef.cdef_strengths[i] = fh[FH_CDEF_STRENGTHS + i];
            cdef.cdef_uv_strengths[i] = fh[FH_CDEF_UV_STRENGTHS + i];
        }

        CommonTileParams t = cm.tiles;
        t.uniform_spacing = fh[FH_TILE_UNIFORM];
        t.log2_cols = fh[FH_TILE_LOG2_COLS];
        t.log2_rows = fh[FH_TILE_LOG2_ROWS];
        t.min_log2_cols = fh[FH_TILE_MIN_LOG2_COLS];
        t.max_log2_cols = fh[FH_TILE_MAX_LOG2_COLS];
        t.min_log2_rows = fh[FH_TILE_MIN_LOG2_ROWS];
        t.max_log2_rows = fh[FH_TILE_MAX_LOG2_ROWS];
        t.cols = fh[FH_TILE_COLS];
        t.rows = fh[FH_TILE_ROWS];
        t.max_width_sb = fh[FH_TILE_MAX_WIDTH_SB];
        t.max_height_sb = fh[FH_TILE_MAX_HEIGHT_SB];
        for (int i = 0; i < 17; ++i)
        {
            t.col_start_sb[i] = fh[FH_COL_START_SB + i];
            t.row_start_sb[i] = fh[FH_ROW_START_SB + i];
        }

        for (int i = 0; i < INTER_REFS_PER_FRAME; ++i) cm.remapped_ref_idx[i] = fh[FH_REMAPPED_REF_IDX + i];
        for (int i = 0; i < REF_FRAMES; ++i)
        {
            cm.ref_frame_id[i] = fh[FH_REF_FRAME_ID + i];
            int b = FH_REF_BUFS + i * FH_REF_BUF_STRIDE;
            var buf = new RefCntBuffer();
            buf.buf.y_crop_width = fh[b];
            buf.buf.y_crop_height = fh[b + 1];
            buf.buf.render_width = fh[b + 2];
            buf.buf.render_height = fh[b + 3];
            buf.order_hint = (uint)fh[b + 4];
            for (int j = 0; j < REF_FRAMES; ++j) buf.ref_deltas[j] = (sbyte)fh[b + 5 + j];
            for (int j = 0; j < MAX_MODE_LF_DELTAS; ++j) buf.mode_deltas[j] = (sbyte)fh[b + 5 + REF_FRAMES + j];
            cm.ref_frame_map[i] = buf;
        }
        // cm->global_motion[] / prev_frame->global_motion[] stay IDENTITY (wmtype 0).
        if (fh[FH_HAS_PREV_FRAME] != 0) cm.prev_frame = new RefCntBuffer();
        return cpi;
    }

    // ---- random parameters --------------------------------------------------------------

    private static readonly int[] ValidLevels = BuildValidLevels();

    private static int[] BuildValidLevels()
    {
        var list = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 32; i++)
            if (av1_common_int.is_valid_seq_level_idx(i) != 0) list.Add(i);
        return list.ToArray();
    }

    private static int NumBits(int v) => v > 1 ? 32 - System.Numerics.BitOperations.LeadingZeroCount((uint)(v - 1)) : 1;

    private static int Bit(Random rng) => rng.Next(2);

    /// <summary>A random, self-consistent sequence header (forFrame: one the frame header test
    /// can use: no still picture unless requested, no film grain, no loop restoration).</summary>
    public static int[] RandomSeq(Random rng, int width, int height, int sbSize, bool forFrame,
                                  bool stillPicture = false)
    {
        var p = new int[SEQ_COUNT];
        int profile = rng.Next(3);
        int bd, ssx, ssy, mono;
        switch (profile)
        {
            case 0:
                bd = rng.Next(2) == 0 ? 8 : 10;
                ssx = ssy = 1;
                mono = rng.Next(4) == 0 ? 1 : 0;
                break;
            case 1:
                bd = rng.Next(2) == 0 ? 8 : 10;
                ssx = ssy = 0;
                mono = 0;
                break;
            default:
                bd = new[] { 8, 10, 12 }[rng.Next(3)];
                if (bd == 12)
                {
                    int k = rng.Next(3);
                    ssx = k == 2 ? 0 : 1;
                    ssy = k == 0 ? 1 : 0;
                }
                else
                {
                    ssx = 1;
                    ssy = 0;
                }
                mono = rng.Next(4) == 0 ? 1 : 0;
                break;
        }
        if (mono != 0) ssx = ssy = 1;
        p[SEQ_PROFILE] = profile;
        p[SEQ_BIT_DEPTH] = bd;
        p[SEQ_SUBSAMPLING_X] = ssx;
        p[SEQ_SUBSAMPLING_Y] = ssy;
        p[SEQ_MONOCHROME] = mono;
        if (rng.Next(2) == 0)
        {
            p[SEQ_COLOR_PRIMARIES] = p[SEQ_TRANSFER_CHARACTERISTICS] = p[SEQ_MATRIX_COEFFICIENTS] = 2;
        }
        else
        {
            bool srgbOk = ssx == 0 && ssy == 0 && (profile == 1 || (profile == 2 && bd == 12));
            if (srgbOk && rng.Next(3) == 0)
            {
                p[SEQ_COLOR_PRIMARIES] = 1;
                p[SEQ_TRANSFER_CHARACTERISTICS] = 13;
                p[SEQ_MATRIX_COEFFICIENTS] = 0;
            }
            else
            {
                p[SEQ_COLOR_PRIMARIES] = rng.Next(1, 23);
                p[SEQ_TRANSFER_CHARACTERISTICS] = rng.Next(1, 19);
                p[SEQ_MATRIX_COEFFICIENTS] = (ssx == 0 && ssy == 0) ? rng.Next(0, 15) : rng.Next(1, 15);
                if (p[SEQ_COLOR_PRIMARIES] == 1 && p[SEQ_TRANSFER_CHARACTERISTICS] == 13 &&
                    p[SEQ_MATRIX_COEFFICIENTS] == 0 && !srgbOk)
                    p[SEQ_TRANSFER_CHARACTERISTICS] = 1;
            }
        }
        p[SEQ_COLOR_RANGE] = Bit(rng);
        p[SEQ_CHROMA_SAMPLE_POSITION] = rng.Next(3);
        p[SEQ_SEPARATE_UV_DELTA_Q] = Bit(rng);
        p[SEQ_FILM_GRAIN_PARAMS_PRESENT] = forFrame ? 0 : Bit(rng);

        bool still = forFrame ? stillPicture : rng.Next(4) == 0;
        p[SEQ_STILL_PICTURE] = still ? 1 : 0;
        p[SEQ_REDUCED_STILL_PICTURE_HDR] = still && (forFrame || Bit(rng) != 0) ? 1 : 0;
        bool reduced = p[SEQ_REDUCED_STILL_PICTURE_HDR] != 0;

        int ops = reduced ? 0 : rng.Next(4);
        p[SEQ_OPERATING_POINTS_CNT_MINUS_1] = ops;
        bool nonzero = false;
        for (int i = 0; i < 4; i++)
        {
            int idc = 0;
            if (i <= ops && !reduced && (ops > 0 || rng.Next(2) == 0))
                idc = rng.Next(1, 1 << 12);
            p[SEQ_OP_BASE + 3 * i] = idc;
            p[SEQ_OP_BASE + 3 * i + 1] = ValidLevels[rng.Next(ValidLevels.Length)];
            p[SEQ_OP_BASE + 3 * i + 2] = Bit(rng);
            if (i <= ops && idc != 0) nonzero = true;
        }
        p[SEQ_HAS_NONZERO_OPERATING_POINT_IDC] = nonzero ? 1 : 0;
        p[SEQ_DISPLAY_MODEL_INFO_PRESENT_FLAG] = reduced ? 0 : Bit(rng);
        p[SEQ_DISPLAY_MODEL_PARAM_PRESENT_FLAG] = Bit(rng);
        p[SEQ_INITIAL_DISPLAY_DELAY] = rng.Next(1, 11);

        p[SEQ_MAX_FRAME_WIDTH] = width;
        p[SEQ_MAX_FRAME_HEIGHT] = height;
        p[SEQ_NUM_BITS_WIDTH] = NumBits(width);
        p[SEQ_NUM_BITS_HEIGHT] = NumBits(height);
        p[SEQ_FRAME_ID_NUMBERS_PRESENT_FLAG] = reduced ? 0 : Bit(rng);
        int delta = rng.Next(2, 15);
        p[SEQ_DELTA_FRAME_ID_LENGTH] = delta;
        p[SEQ_FRAME_ID_LENGTH] = delta + 1 + rng.Next(0, Math.Min(7, 16 - delta - 1) + 1);
        p[SEQ_SB_SIZE] = sbSize;
        p[SEQ_ENABLE_FILTER_INTRA] = Bit(rng);
        p[SEQ_ENABLE_INTRA_EDGE_FILTER] = Bit(rng);
        p[SEQ_ENABLE_INTERINTRA_COMPOUND] = Bit(rng);
        p[SEQ_ENABLE_MASKED_COMPOUND] = Bit(rng);
        p[SEQ_ENABLE_WARPED_MOTION] = Bit(rng);
        p[SEQ_ENABLE_DUAL_FILTER] = Bit(rng);
        p[SEQ_ENABLE_ORDER_HINT] = Bit(rng);
        p[SEQ_ENABLE_DIST_WTD_COMP] = Bit(rng);
        p[SEQ_ENABLE_REF_FRAME_MVS] = Bit(rng);
        p[SEQ_ORDER_HINT_BITS_MINUS_1] = rng.Next(8);
        int fsct = rng.Next(3);
        p[SEQ_FORCE_SCREEN_CONTENT_TOOLS] = fsct;
        p[SEQ_FORCE_INTEGER_MV] = fsct > 0 ? rng.Next(3) : 2;
        p[SEQ_ENABLE_SUPERRES] = Bit(rng);
        p[SEQ_ENABLE_CDEF] = Bit(rng);
        p[SEQ_ENABLE_RESTORATION] = forFrame ? 0 : Bit(rng);
        return p;
    }

    private static int TileLog2(int blkSize, int target)
    {
        int k;
        for (k = 0; (blkSize << k) < target; k++) { }
        return k;
    }

    private static int RandomDelta(Random rng) => rng.Next(2) == 0 ? 0 : rng.Next(-64, 64);

    /// <summary>Random frame parameters consistent with the sequence parameters s.</summary>
    public static int[] RandomFrame(Random rng, int[] s)
    {
        var p = new int[FH_COUNT];
        bool reduced = s[SEQ_REDUCED_STILL_PICTURE_HDR] != 0;
        int maxW = s[SEQ_MAX_FRAME_WIDTH], maxH = s[SEQ_MAX_FRAME_HEIGHT];

        int frameType;
        if (s[SEQ_STILL_PICTURE] != 0) frameType = 0;
        else
        {
            int r = rng.Next(10);
            frameType = r < 2 ? 0 : r < 8 ? 1 : r < 9 ? 2 : 3;
        }
        bool intraOnly = frameType == 0 || frameType == 2;
        p[FH_FRAME_TYPE] = frameType;
        p[FH_SHOW_FRAME] = s[SEQ_STILL_PICTURE] != 0 ? 1 : (rng.Next(5) != 0 ? 1 : 0);
        p[FH_SHOWABLE_FRAME] = Bit(rng);
        p[FH_SHOW_EXISTING_FRAME] = s[SEQ_STILL_PICTURE] == 0 && rng.Next(20) == 0 ? 1 : 0;
        p[FH_EXISTING_FB_IDX_TO_SHOW] = rng.Next(8);
        bool errRes = frameType == 3 || rng.Next(4) == 0;
        p[FH_ERROR_RESILIENT_MODE] = errRes ? 1 : 0;
        p[FH_DISABLE_CDF_UPDATE] = Bit(rng);
        int fsct = s[SEQ_FORCE_SCREEN_CONTENT_TOOLS];
        int allowSct = fsct == 2 ? Bit(rng) : fsct;
        p[FH_ALLOW_SCREEN_CONTENT_TOOLS] = allowSct;
        int fim = s[SEQ_FORCE_INTEGER_MV];
        p[FH_CUR_FRAME_FORCE_INTEGER_MV] = allowSct != 0 ? (fim == 2 ? Bit(rng) : fim) : 0;
        int idLen = s[SEQ_FRAME_ID_LENGTH], diffLen = s[SEQ_DELTA_FRAME_ID_LENGTH];
        int curId = rng.Next(1 << idLen);
        p[FH_CURRENT_FRAME_ID] = curId;

        int w = maxW, h = maxH;
        if (!reduced && (frameType == 3 || rng.Next(5) < 2))
        {
            w = rng.Next(1, maxW + 1);
            h = rng.Next(1, maxH + 1);
        }
        p[FH_UPSCALED_WIDTH] = w;
        p[FH_UPSCALED_HEIGHT] = h;
        bool sameRender = rng.Next(10) < 7;
        p[FH_RENDER_WIDTH] = sameRender ? w : rng.Next(1, 65537);
        p[FH_RENDER_HEIGHT] = sameRender ? h : rng.Next(1, 65537);
        p[FH_SUPERRES_SCALE_DENOMINATOR] =
            s[SEQ_ENABLE_SUPERRES] != 0 && rng.Next(2) == 0 ? rng.Next(9, 17) : 8;
        int ohBits = s[SEQ_ORDER_HINT_BITS_MINUS_1] + 1;
        p[FH_ORDER_HINT] = rng.Next(1 << ohBits);
        p[FH_PRIMARY_REF_FRAME] = rng.Next(8);
        p[FH_REFRESH_FRAME_FLAGS] = frameType == 0 && p[FH_SHOW_FRAME] != 0 ? 0xff : rng.Next(256);
        p[FH_ALLOW_INTRABC] = intraOnly && allowSct != 0 ? Bit(rng) : 0;
        p[FH_ALLOW_HIGH_PRECISION_MV] = Bit(rng);
        p[FH_INTERP_FILTER] = rng.Next(5);
        p[FH_SWITCHABLE_MOTION_MODE] = Bit(rng);
        bool mightRefMvs = !errRes && s[SEQ_ENABLE_REF_FRAME_MVS] != 0 &&
                           s[SEQ_ENABLE_ORDER_HINT] != 0 && !intraOnly;
        p[FH_ALLOW_REF_FRAME_MVS] = mightRefMvs ? Bit(rng) : 0;
        p[FH_REFRESH_FRAME_CONTEXT] = Bit(rng);

        bool coded_lossless = rng.Next(10) == 0;
        p[FH_BASE_QINDEX] = coded_lossless ? 0 : rng.Next(256);
        p[FH_Y_DC_DELTA_Q] = RandomDelta(rng);
        p[FH_U_DC_DELTA_Q] = RandomDelta(rng);
        p[FH_U_AC_DELTA_Q] = RandomDelta(rng);
        bool sep = s[SEQ_SEPARATE_UV_DELTA_Q] != 0;
        p[FH_V_DC_DELTA_Q] = sep && rng.Next(2) == 0 ? RandomDelta(rng) : p[FH_U_DC_DELTA_Q];
        p[FH_V_AC_DELTA_Q] = sep && rng.Next(2) == 0 ? RandomDelta(rng) : p[FH_U_AC_DELTA_Q];
        p[FH_USING_QMATRIX] = Bit(rng);
        p[FH_QM_Y] = rng.Next(16);
        p[FH_QM_U] = rng.Next(16);
        p[FH_QM_V] = sep ? rng.Next(16) : p[FH_QM_U];

        p[FH_SEG_ENABLED] = Bit(rng);
        p[FH_SEG_UPDATE_MAP] = Bit(rng);
        p[FH_SEG_TEMPORAL_UPDATE] = Bit(rng);
        p[FH_SEG_UPDATE_DATA] = Bit(rng);
        int[] featureMax = { 255, 63, 63, 63, 63, 7, 0, 0 };
        for (int i = 0; i < MAX_SEGMENTS; i++)
        {
            p[FH_SEG_FEATURE_MASK + i] = rng.Next(256);
            for (int j = 0; j < SEG_LVL_MAX; j++)
                p[FH_SEG_FEATURE_DATA + i * SEG_LVL_MAX + j] =
                    j < 5 ? rng.Next(-featureMax[j] - 4, featureMax[j] + 5) : rng.Next(0, featureMax[j] + 1);
        }

        p[FH_FILTER_LEVEL_0] = rng.Next(4) == 0 ? 0 : rng.Next(64);
        p[FH_FILTER_LEVEL_1] = rng.Next(4) == 0 ? 0 : rng.Next(64);
        p[FH_FILTER_LEVEL_U] = rng.Next(64);
        p[FH_FILTER_LEVEL_V] = rng.Next(64);
        p[FH_SHARPNESS] = rng.Next(8);
        p[FH_MODE_REF_DELTA_ENABLED] = Bit(rng);
        p[FH_MODE_REF_DELTA_UPDATE] = Bit(rng);
        sbyte[] defRef = { 1, 0, 0, 0, -1, 0, -1, -1 };
        bool defaults = rng.Next(3) == 0;
        for (int i = 0; i < REF_FRAMES; i++)
            p[FH_LF_REF_DELTAS + i] = defaults ? defRef[i] : rng.Next(-63, 64);
        for (int i = 0; i < MAX_MODE_LF_DELTAS; i++)
            p[FH_LF_MODE_DELTAS + i] = defaults ? 0 : rng.Next(-63, 64);

        p[FH_CDEF_DAMPING] = rng.Next(3, 7);
        p[FH_CDEF_BITS] = rng.Next(4);
        p[FH_NB_CDEF_STRENGTHS] = 1 << p[FH_CDEF_BITS];
        for (int i = 0; i < CDEF_MAX_STRENGTHS; i++)
        {
            p[FH_CDEF_STRENGTHS + i] = rng.Next(64);
            p[FH_CDEF_UV_STRENGTHS + i] = rng.Next(64);
        }

        p[FH_CODED_LOSSLESS] = coded_lossless ? 1 : 0;
        p[FH_ALL_LOSSLESS] = coded_lossless && rng.Next(2) == 0 ? 1 : 0;
        p[FH_TX_MODE] = coded_lossless ? 0 : rng.Next(1, 3);
        p[FH_REFERENCE_MODE] = intraOnly ? 0 : (rng.Next(2) == 0 ? 0 : 2);
        p[FH_SKIP_MODE_ALLOWED] = intraOnly ? 0 : Bit(rng);
        p[FH_SKIP_MODE_FLAG] = Bit(rng);
        bool mightWarp = !errRes && !intraOnly && s[SEQ_ENABLE_WARPED_MOTION] != 0;
        p[FH_ALLOW_WARPED_MOTION] = mightWarp ? Bit(rng) : 0;
        p[FH_REDUCED_TX_SET_USED] = Bit(rng);

        // Tiles over the (upscaled) frame.
        int miCols = ((w + 7) & ~7) >> 2, miRows = ((h + 7) & ~7) >> 2;
        p[FH_MI_COLS] = miCols;
        p[FH_MI_ROWS] = miRows;
        int mibLog2 = s[SEQ_SB_SIZE] == 15 ? 5 : 4;
        int sbCols = (miCols + (1 << mibLog2) - 1) >> mibLog2;
        int sbRows = (miRows + (1 << mibLog2) - 1) >> mibLog2;
        bool uniform = rng.Next(3) != 0;
        p[FH_TILE_UNIFORM] = uniform ? 1 : 0;
        int cols, rows;
        if (uniform)
        {
            int minC = rng.Next(3), maxC = minC + rng.Next(5);
            int minR = rng.Next(3), maxR = minR + rng.Next(5);
            p[FH_TILE_MIN_LOG2_COLS] = minC;
            p[FH_TILE_MAX_LOG2_COLS] = maxC;
            p[FH_TILE_MIN_LOG2_ROWS] = minR;
            p[FH_TILE_MAX_LOG2_ROWS] = maxR;
            p[FH_TILE_LOG2_COLS] = rng.Next(minC, maxC + 1);
            p[FH_TILE_LOG2_ROWS] = rng.Next(minR, maxR + 1);
            cols = 1 << p[FH_TILE_LOG2_COLS];
            rows = 1 << p[FH_TILE_LOG2_ROWS];
        }
        else
        {
            cols = rng.Next(1, Math.Min(16, sbCols) + 1);
            rows = rng.Next(1, Math.Min(16, sbRows) + 1);
            p[FH_TILE_MAX_WIDTH_SB] = Partition(rng, sbCols, cols, p, FH_COL_START_SB);
            p[FH_TILE_MAX_HEIGHT_SB] = Partition(rng, sbRows, rows, p, FH_ROW_START_SB);
            p[FH_TILE_LOG2_COLS] = TileLog2(1, cols);
            p[FH_TILE_LOG2_ROWS] = TileLog2(1, rows);
        }
        p[FH_TILE_COLS] = cols;
        p[FH_TILE_ROWS] = rows;
        p[FH_NUM_TG] = rng.Next(5) == 0 ? rng.Next(2, 4) : 1;
        p[FH_TILE_IDX] = rng.Next(2) == 0 ? 0 : rng.Next(cols * rows);
        p[FH_OBU_EXTN_HEADER] = s[SEQ_HAS_NONZERO_OPERATING_POINT_IDC] != 0 ? rng.Next(256) : 0;
        p[FH_KEEP_LEVEL_STATS] = Bit(rng);
        // frame_refs_short_signaling = 1 reaches check_frame_refs_short_signaling (order hint,
        // not ported) on inter frames: only allow it where it is not reached.
        bool shortSig = s[SEQ_ENABLE_ORDER_HINT] != 0 && s[SEQ_ENABLE_REF_FRAME_MVS] == 0;
        p[FH_ENABLE_REF_SHORT_SIGNALING] = shortSig && !intraOnly ? 0 : Bit(rng);
        p[FH_HAS_PREV_FRAME] = Bit(rng);

        for (int i = 0; i < INTER_REFS_PER_FRAME; i++) p[FH_REMAPPED_REF_IDX + i] = rng.Next(8);
        for (int i = 0; i < REF_FRAMES; i++)
        {
            int d = rng.Next(1 << diffLen);
            p[FH_REF_FRAME_ID + i] = ((curId - 1 - d) % (1 << idLen) + (1 << idLen)) % (1 << idLen);
            int b = FH_REF_BUFS + i * FH_REF_BUF_STRIDE;
            bool sameSize = rng.Next(3) == 0;
            p[b] = sameSize ? w : rng.Next(1, maxW + 1);
            p[b + 1] = sameSize ? h : rng.Next(1, maxH + 1);
            bool sameR = rng.Next(2) == 0;
            p[b + 2] = sameR ? p[FH_RENDER_WIDTH] : rng.Next(1, 65537);
            p[b + 3] = sameR ? p[FH_RENDER_HEIGHT] : rng.Next(1, 65537);
            p[b + 4] = rng.Next(1 << ohBits);
            bool same = rng.Next(2) == 0;
            for (int j = 0; j < REF_FRAMES; j++)
                p[b + 5 + j] = same ? p[FH_LF_REF_DELTAS + j] : rng.Next(-63, 64);
            for (int j = 0; j < MAX_MODE_LF_DELTAS; j++)
                p[b + 5 + REF_FRAMES + j] = same ? p[FH_LF_MODE_DELTAS + j] : rng.Next(-63, 64);
        }
        return p;
    }

    // Splits total superblocks into n tiles (start offsets at p[at..at+n]); returns a
    // max tile size that every tile fits in.
    private static int Partition(Random rng, int total, int n, int[] p, int at)
    {
        var sizes = new int[n];
        for (int i = 0; i < n; i++) sizes[i] = 1;
        for (int left = total - n; left > 0; left--) sizes[rng.Next(n)]++;
        int start = 0, max = 0;
        for (int i = 0; i < n; i++)
        {
            p[at + i] = start;
            start += sizes[i];
            max = Math.Max(max, sizes[i]);
        }
        p[at + n] = start;
        return max + rng.Next(3);
    }
}
