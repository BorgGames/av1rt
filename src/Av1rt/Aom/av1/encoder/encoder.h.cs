// Port of libaom av1/encoder/encoder.h
global using static Av1rt.Aom.encoder;
global using FRAMETYPE_FLAGS = int;
global using AQ_MODE = int;
global using DELTAQ_MODE = int;
global using RESIZE_MODE = int;
global using SS_CFG_OFFSET = int;
global using SCENECUT_MODE = int;
global using MULTI_THREADED_MODULES = int;
global using COST_UPDATE_TYPE = int;
global using LOOPFILTER_CONTROL = int;
global using SKIP_APPLY_POSTPROC_FILTER = int;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class encoder
{
    // Number of frames required to test for scene cut detection
    internal const int SCENE_CUT_KEY_TEST_INTERVAL = 16;

    // Lookahead index threshold to enable temporal filtering for second arf.
    internal const int TF_LOOKAHEAD_IDX_THR = 7;

    internal const int HDR_QP_LEVELS = 10;
    internal const double CHROMA_CB_QP_SCALE = 1.04;
    internal const double CHROMA_CR_QP_SCALE = 1.04;
    internal const double CHROMA_QP_SCALE = -0.46;
    internal const double CHROMA_QP_OFFSET = 9.26;
    internal const double QP_SCALE_FACTOR = 2.0;
    internal const int DISABLE_HDR_LUMA_DELTAQ = 1;

    // FRAMETYPE_FLAGS
    internal const int FRAMEFLAGS_KEY = 1 << 0;
    internal const int FRAMEFLAGS_GOLDEN = 1 << 1;
    internal const int FRAMEFLAGS_BWDREF = 1 << 2;
    // TODO(zoeliu): To determine whether a frame flag is needed for ALTREF2_FRAME
    internal const int FRAMEFLAGS_ALTREF = 1 << 3;
    internal const int FRAMEFLAGS_INTRAONLY = 1 << 4;
    internal const int FRAMEFLAGS_SWITCH = 1 << 5;
    internal const int FRAMEFLAGS_ERROR_RESILIENT = 1 << 6;

    // [subset] FPMT_TEST_ENC_CFG: CONFIG_FPMT_TEST == 0.

    // 0 level frames are sometimes used for rate control purposes, but for
    // reference mapping purposes, the minimum level should be 1.
    internal const int MIN_PYR_LEVEL = 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_true_pyr_level(int frame_level, int frame_order,
                                           int max_layer_depth)
    {
        if (frame_order == 0)
        {
            // Keyframe case
            return MIN_PYR_LEVEL;
        }
        else if (frame_level == MAX_ARF_LAYERS)
        {
            // Leaves
            return max_layer_depth;
        }
        else if (frame_level == (MAX_ARF_LAYERS + 1))
        {
            // Altrefs
            return MIN_PYR_LEVEL;
        }
        return Math.Max(MIN_PYR_LEVEL, frame_level);
    }

    // AQ_MODE
    internal const int NO_AQ = 0;
    internal const int VARIANCE_AQ = 1;
    internal const int COMPLEXITY_AQ = 2;
    internal const int CYCLIC_REFRESH_AQ = 3;
    internal const int AQ_MODE_COUNT = 4; // This should always be the last member of the enum

    // DELTAQ_MODE
    internal const int NO_DELTA_Q = 0;
    internal const int DELTA_Q_OBJECTIVE = 1;         // Modulation to improve objective quality
    internal const int DELTA_Q_PERCEPTUAL = 2;        // Modulation to improve video perceptual quality
    internal const int DELTA_Q_PERCEPTUAL_AI = 3;     // Perceptual quality opt for all intra mode
    internal const int DELTA_Q_USER_RATING_BASED = 4; // User rating based delta q mode
    internal const int DELTA_Q_HDR = 5;               // QP adjustment based on HDR block pixel average
    internal const int DELTA_Q_VARIANCE_BOOST = 6;    // Variance Boost style modulation
    internal const int DELTA_Q_MODE_COUNT = 7;        // This should always be the last member of the enum

    // RESIZE_MODE
    internal const int RESIZE_NONE = 0;    // No frame resizing allowed.
    internal const int RESIZE_FIXED = 1;   // All frames are coded at the specified scale.
    internal const int RESIZE_RANDOM = 2;  // All frames are coded at a random scale.
    internal const int RESIZE_DYNAMIC = 3; // Frames coded at lower scale based on rate control.
    internal const int RESIZE_MODES = 4;

    // SS_CFG_OFFSET
    internal const int SS_CFG_SRC = 0;
    internal const int SS_CFG_LOOKAHEAD = 1;
    internal const int SS_CFG_FPF = 2;
    internal const int SS_CFG_TOTAL = 3;

    // SCENECUT_MODE
    internal const int DISABLE_SCENECUT = 0;       // For LAP, lag_in_frames < 19
    internal const int ENABLE_SCENECUT_MODE_1 = 1; // For LAP, lag_in_frames >=19 and < 33
    internal const int ENABLE_SCENECUT_MODE_2 = 2; // For twopass and LAP - lag_in_frames >=33

    internal const int MAX_VBR_CORPUS_COMPLEXITY = 10000;

    // MULTI_THREADED_MODULES
    internal const int MOD_FP = 0;          // First pass
    internal const int MOD_TF = 1;          // Temporal filtering
    internal const int MOD_TPL = 2;         // TPL
    internal const int MOD_GME = 3;         // Global motion estimation
    internal const int MOD_ENC = 4;         // Encode stage
    internal const int MOD_LPF = 5;         // Deblocking loop filter
    internal const int MOD_CDEF_SEARCH = 6; // CDEF search
    internal const int MOD_CDEF = 7;        // CDEF frame
    internal const int MOD_LR = 8;          // Loop restoration filtering
    internal const int MOD_PACK_BS = 9;     // Pack bitstream
    internal const int MOD_FRAME_ENC = 10;  // Frame Parallel encode
    internal const int MOD_AI = 11;         // All intra
    internal const int NUM_MT_MODULES = 12;

    // COST_UPDATE_TYPE
    internal const int COST_UPD_SB = 0;           /*!< Update every sb. */
    internal const int COST_UPD_SBROW = 1;        /*!< Update every sb rows inside a tile. */
    internal const int COST_UPD_TILE = 2;         /*!< Update every tile. */
    internal const int COST_UPD_OFF = 3;          /*!< Turn off cost updates. */
    internal const int NUM_COST_UPDATE_TYPES = 4; /*!< Number of cost update types. */

    // LOOPFILTER_CONTROL
    internal const int LOOPFILTER_NONE = 0;        /*!< Disable loopfilter on all frames. */
    internal const int LOOPFILTER_ALL = 1;         /*!< Enable loopfilter for all frames. */
    internal const int LOOPFILTER_REFERENCE = 2;   /*!< Disable loopfilter on non reference frames. */
    internal const int LOOPFILTER_SELECTIVELY = 3; /*!< Disable loopfilter on frames with low motion. */

    // SKIP_APPLY_POSTPROC_FILTER
    internal const int SKIP_APPLY_RESTORATION = 1 << 0;
    internal const int SKIP_APPLY_SUPERRES = 1 << 1;
    internal const int SKIP_APPLY_CDEF = 1 << 2;
    internal const int SKIP_APPLY_LOOPFILTER = 1 << 3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_lossless_requested(in RateControlCfg rc_cfg)
    {
        return B(rc_cfg.best_allowed_q == 0 && rc_cfg.worst_allowed_q == 0);
    }

    internal const int INTER_MODE_RD_DATA_OVERALL_SIZE = 6400;

    internal const int MAX_INTER_MODES = 1024;

    // Number of recodes per frame
    internal const int NUM_RECODES_PER_FRAME = 10;

    // Maximum number of frames that can be encoded in parallel
    internal const int MAX_PARALLEL_FRAMES = 4;

    // [subset] StatType / ImageStat: CONFIG_INTERNAL_STATS == 0.
    // [subset] FramePartitionTimingStats: CONFIG_COLLECT_PARTITION_STATS == 0.
    // [subset] TIMING_COMPONENT / get_component_name: CONFIG_COLLECT_COMPONENT_TIMING == 0.

    // The maximum number of internal ARFs except ALTREF_FRAME
    internal const int MAX_INTERNAL_ARFS = (REF_FRAMES - BWDREF_FRAME - 1);

    // [subset] DUCKY_ENCODE_*: !CONFIG_REALTIME_ONLY only.

    // [subset] calc_frame_data_update_flag: CONFIG_FPMT_TEST == 0.

    internal const long TICKS_PER_SEC = 10000000L;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void init_ref_map_pair(
        AV1_COMP cpi, RefFrameMapPair[] ref_frame_map_pairs)
    {
        if (cpi.ppi.gf_group.update_type[cpi.gf_frame_index] == KF_UPDATE)
        {
            // memset(ref_frame_map_pairs, -1, ...): every int field becomes -1.
            for (int i = 0; i < REF_FRAMES; i++)
            {
                ref_frame_map_pairs[i].pyr_level = -1;
                ref_frame_map_pairs[i].disp_order = -1;
            }
            return;
        }
        for (int i = 0; i < REF_FRAMES; i++) ref_frame_map_pairs[i] = default;
        for (int map_idx = 0; map_idx < REF_FRAMES; map_idx++)
        {
            // Get reference frame buffer.
            RefCntBuffer buf = cpi.common.ref_frame_map[map_idx];
            if (ref_frame_map_pairs[map_idx].disp_order == -1) continue;
            if (buf == null)
            {
                ref_frame_map_pairs[map_idx].disp_order = -1;
                ref_frame_map_pairs[map_idx].pyr_level = -1;
                continue;
            }
            else if (buf.ref_count > 1)
            {
                // Once the keyframe is coded, the slots in ref_frame_map will all
                // point to the same frame. In that case, all subsequent pointers
                // matching the current are considered "free" slots. This will find
                // the next occurrence of the current pointer if ref_count indicates
                // there are multiple instances of it and mark it as free.
                for (int idx2 = map_idx + 1; idx2 < REF_FRAMES; ++idx2)
                {
                    RefCntBuffer buf2 = cpi.common.ref_frame_map[idx2];
                    if (buf2 == buf)
                    {
                        ref_frame_map_pairs[idx2].disp_order = -1;
                        ref_frame_map_pairs[idx2].pyr_level = -1;
                    }
                }
            }
            ref_frame_map_pairs[map_idx].disp_order = (int)buf.display_order_hint;
            ref_frame_map_pairs[map_idx].pyr_level = (int)buf.pyramid_level;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long timebase_units_to_ticks(
        aom_rational64_t timestamp_ratio, long n)
    {
        return n * timestamp_ratio.num / timestamp_ratio.den;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long ticks_to_timebase_units(
        aom_rational64_t timestamp_ratio, long n)
    {
        long round = timestamp_ratio.num / 2;
        if (round > 0) --round;
        return (n * timestamp_ratio.den + round) / timestamp_ratio.num;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int frame_is_kf_gf_arf(AV1_COMP cpi)
    {
        GF_GROUP gf_group = cpi.ppi.gf_group;
        FRAME_UPDATE_TYPE update_type =
            gf_group.update_type[cpi.gf_frame_index];

        return B(frame_is_intra_only(cpi.common) != 0 || update_type == ARF_UPDATE ||
                 update_type == GF_UPDATE);
    }

    // TODO(huisu@google.com, youzhou@microsoft.com): enable hash-me for HBD.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_use_hash_me(AV1_COMP cpi)
    {
        return B(cpi.common.features.allow_screen_content_tools &&
                 cpi.common.features.allow_intrabc &&
                 frame_is_intra_only(cpi.common) != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static YV12_BUFFER_CONFIG get_ref_frame_yv12_buf(
        AV1_COMMON cm, MV_REFERENCE_FRAME ref_frame)
    {
        RefCntBuffer buf = get_ref_frame_buf(cm, ref_frame);
        return buf != null ? buf.buf : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void alloc_frame_mvs(AV1_COMMON cm, RefCntBuffer buf)
    {
        Debug.Assert(buf != null);
        ensure_mv_buffer(buf, cm);
        buf.width = cm.width;
        buf.height = cm.height;
    }

    // [subset] allocated_tokens / get_start_tok: never executed; the token
    // buffer only holds palette tokens (palette is off in the subset, and p1b2
    // dropped get_token_alloc accordingly).

    internal const int ALT_MIN_LAG = 3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_altref_enabled(int lag_in_frames, bool enable_auto_arf)
    {
        return B(lag_in_frames >= ALT_MIN_LAG && enable_auto_arf);
    }

    // [subset] can_disable_altref: two-pass / lagged GF structure only.

    // Helper function to compute number of blocks on either side of the frame.
    // Not executed in the subset; tiny and used by ethread.c / encoder.c.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_num_blocks(int frame_length, int mb_length)
    {
        return (frame_length + mb_length - 1) / mb_length;
    }

    // Check if statistics generation stage
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_stat_generation_stage(AV1_COMP cpi)
    {
        Debug.Assert(cpi.compressor_stage != LAP_STAGE ||
                     (cpi.oxcf.pass == AOM_RC_ONE_PASS && cpi.ppi.lap_enabled != 0));
        return B(cpi.oxcf.pass == AOM_RC_FIRST_PASS ||
                 (cpi.compressor_stage == LAP_STAGE));
    }

    // Check if statistics consumption stage
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_stat_consumption_stage_twopass(AV1_COMP cpi)
    {
        return B(cpi.oxcf.pass >= AOM_RC_SECOND_PASS);
    }

    // Check if statistics consumption stage
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_stat_consumption_stage(AV1_COMP cpi)
    {
        return B(is_stat_consumption_stage_twopass(cpi) != 0 ||
                 (cpi.oxcf.pass == AOM_RC_ONE_PASS &&
                  (cpi.compressor_stage == ENCODE_STAGE) && cpi.ppi.lap_enabled != 0 &&
                  cpi.oxcf.mode != REALTIME));
    }

    // Decide whether 'dv_costs' need to be allocated/stored during the encoding.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool av1_need_dv_costs(AV1_COMP cpi)
    {
        return (cpi.sf.rt_sf.use_nonrd_pick_mode == 0 || cpi.sf.rt_sf.rt_use_intrabc != 0) &&
               av1_allow_intrabc(cpi.common) != 0 && is_stat_generation_stage(cpi) == 0;
    }

    /// <summary>
    /// Check if the current stage has statistics
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int has_no_stats_stage(AV1_COMP cpi)
    {
        Debug.Assert(cpi.ppi.lap_enabled != 0 || cpi.compressor_stage == ENCODE_STAGE);
        return B(cpi.oxcf.pass == AOM_RC_ONE_PASS &&
                 (cpi.ppi.lap_enabled == 0 || cpi.oxcf.mode == REALTIME));
    }

    // Function return size of frame stats buffer
    // [subset] get_stats_buf_size: LAP / two-pass only.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_one_pass_rt_lag_params(AV1_COMP cpi)
    {
        return B(cpi.oxcf.pass == AOM_RC_ONE_PASS &&
                 cpi.oxcf.gf_cfg.lag_in_frames > 0 && cpi.oxcf.mode == REALTIME);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_one_pass_rt_params(AV1_COMP cpi)
    {
        return B(has_no_stats_stage(cpi) != 0 && cpi.oxcf.gf_cfg.lag_in_frames == 0 &&
                 (cpi.oxcf.mode == REALTIME || cpi.svc.number_spatial_layers > 1));
    }

    // Use default/internal reference structure for single-layer RTC.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int use_rtc_reference_structure_one_layer(AV1_COMP cpi)
    {
        return B(is_one_pass_rt_params(cpi) != 0 && cpi.ppi.number_spatial_layers == 1 &&
                 cpi.ppi.number_temporal_layers == 1 &&
                 cpi.ppi.rtc_ref.set_ref_frame_config == 0);
    }

    // Check if postencode drop is allowed.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int allow_postencode_drop_rtc(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        return B(is_one_pass_rt_params(cpi) != 0 && cpi.oxcf.rc_cfg.mode == AOM_CBR &&
                 cpi.oxcf.rc_cfg.drop_frames_water_mark > 0 &&
                 cpi.rc.rtc_external_ratectrl == 0 && frame_is_intra_only(cm) == 0 &&
                 cpi.svc.spatial_layer_id == 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_ref_ptrs(AV1_COMMON cm, MACROBLOCKD xd,
                                      MV_REFERENCE_FRAME ref0,
                                      MV_REFERENCE_FRAME ref1)
    {
        xd.block_ref_scale_factors[0] =
            get_ref_scale_factors_const(cm, ref0 >= LAST_FRAME ? ref0 : 1);
        xd.block_ref_scale_factors[1] =
            get_ref_scale_factors_const(cm, ref1 >= LAST_FRAME ? ref1 : 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_chessboard_index(int frame_index)
    {
        return frame_index & 0x1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<int> cond_cost_list_const(AV1_COMP cpi,
                                                  Ptr<int> cost_list)
    {
        bool use_cost_list = cpi.sf.mv_sf.subpel_search_method != SUBPEL_TREE &&
                             cpi.sf.mv_sf.use_fullpel_costlist != 0;
        return use_cost_list ? cost_list : Ptr<int>.Null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<int> cond_cost_list(AV1_COMP cpi, Ptr<int> cost_list)
    {
        bool use_cost_list = cpi.sf.mv_sf.subpel_search_method != SUBPEL_TREE &&
                             cpi.sf.mv_sf.use_fullpel_costlist != 0;
        return use_cost_list ? cost_list : Ptr<int>.Null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LAYER_IDS_TO_IDX(int sl, int tl, int num_tl)
    {
        return ((sl) * (num_tl) + (tl));
    }

    // Returns 1 if a frame is scaled and 0 otherwise.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_resize_scaled(AV1_COMMON cm)
    {
        return B(cm.superres_upscaled_width != cm.render_width ||
                 cm.superres_upscaled_height != cm.render_height);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_frame_scaled(AV1_COMMON cm)
    {
        return B(av1_superres_scaled(cm) != 0 || av1_resize_scaled(cm) != 0);
    }

    // [subset] av1_encode_for_extrc: external rate control is out of the subset.

    // Don't allow a show_existing_frame to coincide with an error resilient
    // frame. An exception can be made for a forward keyframe since it has no
    // previous dependencies.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int encode_show_existing_frame(AV1_COMMON cm)
    {
        return B(cm.show_existing_frame != 0 && (!cm.features.error_resilient_mode ||
                                                 cm.current_frame.frame_type == KEY_FRAME));
    }

    // Get index into the 'cpi->mbmi_ext_info.frame_base' array for the given
    // 'mi_row' and 'mi_col'.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_mi_ext_idx(int mi_row, int mi_col,
                                       BLOCK_SIZE mi_alloc_bsize,
                                       int mbmi_ext_stride)
    {
        int mi_ext_size_1d = mi_size_wide[mi_alloc_bsize];
        int mi_ext_row = mi_row / mi_ext_size_1d;
        int mi_ext_col = mi_col / mi_ext_size_1d;
        return mi_ext_row * mbmi_ext_stride + mi_ext_col;
    }

    // Computes the signed distances from the bottom and right edges of the current
    // prediction block to the corresponding edges of the frame.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_pixels_to_frame_edge(MACROBLOCK x, int bw, int bh,
                                                  int mi_col, int mi_row, int mi_cols,
                                                  int mi_rows, int frame_width,
                                                  int frame_height,
                                                  bool do_border_pad)
    {
        // For do_border_pad = true, compute distances using the actual frame
        // dimensions.
        // For do_border_pad = false, compute distances using the frame dimensions
        // aligned to a multiple of 8 pixels to match the dimensions represented
        // by mi_cols and mi_rows, which are rounded up to multiples of 8 pixels.
        int boundary_frame_width =
            do_border_pad ? frame_width : (mi_cols << MI_SIZE_LOG2);
        int boundary_frame_height =
            do_border_pad ? frame_height : (mi_rows << MI_SIZE_LOG2);

        x.pix_to_bottom_edge =
            boundary_frame_height - ((mi_row + bh) << MI_SIZE_LOG2);
        x.pix_to_right_edge = boundary_frame_width - ((mi_col + bw) << MI_SIZE_LOG2);
    }

    // Lighter version of set_offsets that only sets the mode info
    // pointers.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_mode_info_offsets(
        CommonModeInfoParams mi_params,
        MBMIExtFrameBufferInfo mbmi_ext_info, MACROBLOCK x,
        MACROBLOCKD xd, int mi_row, int mi_col)
    {
        set_mi_offsets(mi_params, xd, mi_row, mi_col);
        int ext_idx = get_mi_ext_idx(mi_row, mi_col, mi_params.mi_alloc_bsize,
                                     mbmi_ext_info.stride);
        // x->mbmi_ext_frame = mbmi_ext_info->frame_base + ext_idx
        Ptr<MB_MODE_INFO_EXT_FRAME> frame_base = mbmi_ext_info.frame_base;
        x.mbmi_ext_frame = frame_base + ext_idx;
    }

    // Check to see if the given partition size is allowed for a specified number
    // of mi block rows and columns remaining in the image.
    // If not then return the largest allowed partition size
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static BLOCK_SIZE find_partition_size(BLOCK_SIZE bsize, int rows_left,
                                                   int cols_left, ref int bh, ref int bw)
    {
        int int_size = (int)bsize;
        if (rows_left <= 0 || cols_left <= 0)
        {
            return Math.Min(bsize, BLOCK_8X8);
        }
        else
        {
            for (; int_size > 0; int_size -= 3)
            {
                bh = mi_size_high[int_size];
                bw = mi_size_wide[int_size];
                if ((bh <= rows_left) && (bw <= cols_left))
                {
                    break;
                }
            }
        }
        return (BLOCK_SIZE)int_size;
    }

    internal static readonly byte[] av1_ref_frame_flag_list = {
        0,
        AOM_LAST_FLAG,
        AOM_LAST2_FLAG,
        AOM_LAST3_FLAG,
        AOM_GOLD_FLAG,
        AOM_BWD_FLAG,
        AOM_ALT2_FLAG,
        AOM_ALT_FLAG
    };

    // When more than 'max_allowed_refs' are available, we reduce the number of
    // reference frames one at a time based on this order.
    internal static readonly MV_REFERENCE_FRAME[] disable_order = {
        LAST3_FRAME,
        LAST2_FRAME,
        ALTREF2_FRAME,
        BWDREF_FRAME,
    };

    internal static readonly MV_REFERENCE_FRAME[]
        ref_frame_priority_order = {
            LAST_FRAME, ALTREF_FRAME, BWDREF_FRAME, GOLDEN_FRAME,
            ALTREF2_FRAME, LAST2_FRAME, LAST3_FRAME,
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_ref_frame_flags(SPEED_FEATURES sf,
                                            int use_one_pass_rt_params,
                                            YV12_BUFFER_CONFIG[] ref_frames,
                                            int ext_ref_frame_flags)
    {
        // cpi->ext_flags.ref_frame_flags allows certain reference types to be
        // disabled by the external interface.  These are set by
        // av1_apply_encoding_flags(). Start with what the external interface allows,
        // then suppress any reference types which we have found to be duplicates.
        int flags = ext_ref_frame_flags;

        for (int i = 1; i < INTER_REFS_PER_FRAME; ++i)
        {
            YV12_BUFFER_CONFIG this_ref = ref_frames[i];
            // If this_ref has appeared before, mark the corresponding ref frame as
            // invalid. For one_pass_rt mode, only disable GOLDEN_FRAME if it's the
            // same as LAST_FRAME or ALTREF_FRAME (if ALTREF is being used in nonrd).
            int index =
                (use_one_pass_rt_params != 0 && ref_frame_priority_order[i] == GOLDEN_FRAME)
                    ? (1 + sf.rt_sf.use_nonrd_altref_frame)
                    : i;
            for (int j = 0; j < index; ++j)
            {
                // If this_ref has appeared before (same as the reference corresponding
                // to lower index j), remove it as a reference only if that reference
                // (for index j) is actually used as a reference.
                if (this_ref == ref_frames[j] &&
                    (flags & (1 << (ref_frame_priority_order[j] - 1))) != 0)
                {
                    flags &= ~(1 << (ref_frame_priority_order[i] - 1));
                    break;
                }
            }
        }
        return flags;
    }

    internal const double MAX_GFUBOOST_FACTOR = 10.0;
    internal const double MIN_GFUBOOST_FACTOR = 4.0;

    // [subset] is_frame_tpl_eligible / is_frame_eligible_for_ref_pruning: TPL /
    // lagged GF structure only (never executed).

    // Get update type of the current frame.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FRAME_UPDATE_TYPE get_frame_update_type(GF_GROUP gf_group,
                                                            int gf_frame_index)
    {
        return gf_group.update_type[gf_frame_index];
    }

    // Not executed in the subset; tiny pure helper.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_pixels_to_mi(int pixels)
    {
        return ALIGN_POWER_OF_TWO(pixels, 3) >> MI_SIZE_LOG2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_psnr_calc_enabled(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;

        return B(cpi.ppi.b_calculate_psnr != 0 && is_stat_generation_stage(cpi) == 0 &&
                 cm.show_frame != 0 && !cpi.is_dropped_frame);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_vmaf_tuning_mode(aom_tune_metric tuning)
    {
        return B(tuning == AOM_TUNE_VMAF_MAX_GAIN ||
                 tuning == AOM_TUNE_VMAF_NEG_MAX_GAIN);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_frame_resize_pending(AV1_COMP cpi)
    {
        ref readonly ResizePendingParams resize_pending_params =
            ref cpi.resize_pending_params;
        return B(resize_pending_params.width != 0 && resize_pending_params.height != 0 &&
                 (cpi.common.width != resize_pending_params.width ||
                  cpi.common.height != resize_pending_params.height));
    }

    // Check if loop filter is used.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_loopfilter_used(AV1_COMMON cm)
    {
        return B(!cm.features.coded_lossless && cm.tiles.large_scale == 0);
    }

    // Check if CDEF is used.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_cdef_used(AV1_COMMON cm)
    {
        return B(cm.seq_params.enable_cdef != 0 && !cm.features.coded_lossless &&
                 cm.tiles.large_scale == 0);
    }

    // Check if loop restoration filter is used.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_restoration_used(AV1_COMMON cm)
    {
        return B(cm.seq_params.enable_restoration != 0 && !cm.features.all_lossless &&
                 cm.tiles.large_scale == 0);
    }

    // Checks if post-processing filters need to be applied.
    // NOTE: This function decides if the application of different post-processing
    // filters on the reconstructed frame can be skipped at the encoder side.
    // However the computation of different filter parameters that are signaled in
    // the bitstream is still required.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint derive_skip_apply_postproc_filters(
        AV1_COMP cpi, int use_loopfilter, int use_cdef, int use_superres,
        int use_restoration)
    {
        // Don't skip CDEF or loop filter when PSNR is enabled, since the comparison
        // against the reconstructed state would then diverge from the ground truth.
        if (cpi.ppi.b_calculate_psnr != 0) return 0;

        // Though CDEF parameter selection should be dependent on
        // deblocked/loop-filtered pixels for cdef_pick_method <=
        // CDEF_FAST_SEARCH_LVL5, CDEF strength values are calculated based on the
        // pixel values that are not loop-filtered in svc real-time encoding mode
        // when PSNR calculation is disabled.
        // Hence this case is handled separately using the condition below.
        if (cpi.ppi.rtc_ref.non_reference_frame != 0)
            return (SKIP_APPLY_LOOPFILTER | SKIP_APPLY_CDEF);

        if (!cpi.oxcf.algo_cfg.skip_postproc_filtering) return 0;
        Debug.Assert(cpi.oxcf.mode == ALLINTRA);

        // Only reachable in ALLINTRA mode (out of the subset); ported anyway
        // because it is small.
        // The post-processing filters are applied one after the other in the
        // following order: deblocking->cdef->superres->restoration. In case of
        // ALLINTRA encoding, the reconstructed frame is not used as a reference
        // frame. Hence, the application of these filters can be skipped when
        // 1. filter parameters of the subsequent stages are not dependent on the
        // filtered output of the current stage or
        // 2. subsequent filtering stages are disabled
        if (use_restoration != 0) return SKIP_APPLY_RESTORATION;
        if (use_superres != 0) return SKIP_APPLY_SUPERRES;
        if (use_cdef != 0)
        {
            // CDEF parameter selection is not dependent on the deblocked frame if
            // cdef_pick_method is CDEF_PICK_FROM_Q. Hence the application of deblocking
            // filters and cdef filters can be skipped in this case.
            return (cpi.sf.lpf_sf.cdef_pick_method == CDEF_PICK_FROM_Q &&
                    use_loopfilter != 0)
                       ? (uint)(SKIP_APPLY_LOOPFILTER | SKIP_APPLY_CDEF)
                       : (uint)SKIP_APPLY_CDEF;
        }
        if (use_loopfilter != 0) return SKIP_APPLY_LOOPFILTER;

        // If we reach here, all post-processing stages are disabled, so none need to
        // be skipped.
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_postproc_filter_default_params(AV1_COMMON cm)
    {
        loopfilter lf = cm.lf;
        CdefInfo cdef_info = cm.cdef_info;
        RestorationInfo[] rst_info = cm.rst_info;

        lf.filter_level[0] = 0;
        lf.filter_level[1] = 0;
        lf.backup_filter_level[0] = 0;
        lf.backup_filter_level[1] = 0;
        cdef_info.cdef_bits = 0;
        cdef_info.cdef_strengths[0] = 0;
        cdef_info.nb_cdef_strengths = 1;
        cdef_info.cdef_uv_strengths[0] = 0;
        rst_info[0].frame_restoration_type = RESTORE_NONE;
        rst_info[1].frame_restoration_type = RESTORE_NONE;
        rst_info[2].frame_restoration_type = RESTORE_NONE;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_inter_tx_size_search_level_one(
        TX_SPEED_FEATURES tx_sf)
    {
        return B(tx_sf.inter_tx_size_search_init_depth_rect >= 1 &&
                 tx_sf.inter_tx_size_search_init_depth_sqr >= 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_lpf_opt_level(SPEED_FEATURES sf)
    {
        int lpf_opt_level = 0;
        if (is_inter_tx_size_search_level_one(sf.tx_sf) != 0)
            lpf_opt_level = (sf.lpf_sf.lpf_pick == LPF_PICK_FROM_Q) ? 2 : 1;
        return lpf_opt_level;
    }

    // Enable switchable motion mode only if warp and OBMC tools are allowed
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool is_switchable_motion_mode_allowed(bool allow_warped_motion,
                                                           bool enable_obmc)
    {
        return (allow_warped_motion || enable_obmc);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int warped_motion_update_num_proj_ref(
        AV1_COMP cpi, MB_MODE_INFO mi)
    {
        return B(cpi.oxcf.motion_mode_cfg.allow_warped_motion && is_inter_block(mi) != 0 &&
                 mi.skip_mode == 0 && is_motion_variation_allowed_bsize(mi.bsize) != 0 &&
                 has_second_ref(mi) == 0);
    }

    // [subset] denoise_svc: CONFIG_AV1_TEMPORAL_DENOISING == 0.
    // [subset] partition / component timing helpers: CONFIG_COLLECT_* == 0.

    // ------------------------------------------------------------------------
    // Port infrastructure (not libaom code).
    // PORT-NOTE: helpers to build C fixed-size arrays of class-mapped types and
    // multi-dimensional arrays (jagged). Candidates to move into CRuntime at
    // integration. Elements of reference types are created; value-type
    // elements are left zeroed.
    // ------------------------------------------------------------------------
    internal static T[] alloc_array<T>(int n) where T : new()
    {
        T[] a = new T[n];
        if (default(T) is null)
            for (int i = 0; i < n; i++) a[i] = new T();
        return a;
    }

    internal static T[][] alloc_array2<T>(int n0, int n1) where T : new()
    {
        T[][] a = new T[n0][];
        for (int i = 0; i < n0; i++) a[i] = alloc_array<T>(n1);
        return a;
    }

    internal static T[][][] alloc_array3<T>(int n0, int n1, int n2) where T : new()
    {
        T[][][] a = new T[n0][][];
        for (int i = 0; i < n0; i++) a[i] = alloc_array2<T>(n1, n2);
        return a;
    }
}

// ----------------------------------------------------------------------------
// Encoder configuration sub-structures. They are value types so that the C
// struct assignment `cpi->oxcf = *oxcf` (AV1EncoderConfig.CopyFrom) and the
// `const XxxCfg *const cfg = &oxcf->xxx_cfg` aliases (`ref`/`in`) work as in C.
// ----------------------------------------------------------------------------

/// <summary>Encoder config related to resize.</summary>
internal partial struct ResizeCfg
{
    /// Indicates the frame resize mode to be used by the encoder.
    public RESIZE_MODE resize_mode;
    /// Indicates the denominator for resize of inter frames, assuming 8 as the
    /// numerator. Its value ranges between 8-16.
    public byte resize_scale_denominator;
    /// Indicates the denominator for resize of key frames, assuming 8 as the
    /// numerator. Its value ranges between 8-16.
    public byte resize_kf_scale_denominator;
}

/// <summary>Encoder config for coding block partitioning.</summary>
internal partial struct PartitionCfg
{
    /// Indicates if rectangular partitions should be enabled.
    public bool enable_rect_partitions;
    /// Indicates if AB partitions should be enabled.
    public bool enable_ab_partitions;
    /// Indicates if 1:4 / 4:1 partitions should be enabled.
    public bool enable_1to4_partitions;
    /// Indicates the minimum partition size that should be allowed. Both width and
    /// height of a partition cannot be smaller than the min_partition_size.
    public BLOCK_SIZE min_partition_size;
    /// Indicates the maximum partition size that should be allowed. Both width and
    /// height of a partition cannot be larger than the max_partition_size.
    public BLOCK_SIZE max_partition_size;
}

/// <summary>Encoder flags for intra prediction.</summary>
internal partial struct IntraModeCfg
{
    /// Indicates if intra edge filtering process should be enabled.
    public bool enable_intra_edge_filter;
    /// Indicates if recursive filtering based intra prediction should be enabled.
    public bool enable_filter_intra;
    /// Indicates if smooth intra prediction modes should be enabled.
    public bool enable_smooth_intra;
    /// Indicates if PAETH intra prediction mode should be enabled.
    public bool enable_paeth_intra;
    /// Indicates if CFL uv intra mode should be enabled.
    public bool enable_cfl_intra;
    /// Indicates if directional modes should be enabled.
    public bool enable_directional_intra;
    /// Indicates if the subset of directional modes from D45 to D203 intra
    /// prediction modes should be enabled.
    public bool enable_diagonal_intra;
    /// Indicates if delta angles for directional intra prediction should be
    /// enabled.
    public bool enable_angle_delta;
    /// Indicates whether to automatically turn off several intral coding tools.
    public bool auto_intra_tools_off;
}

/// <summary>Encoder flags for transform sizes and types.</summary>
internal partial struct TxfmSizeTypeCfg
{
    /// Indicates if 64x64 transform should be enabled.
    public bool enable_tx64;
    /// Indicates if flip and identity transforms should be enabled.
    public bool enable_flip_idtx;
    /// Indicates if rectangular transforms should be enabled.
    public bool enable_rect_tx;
    /// Indicates if transform type for intra blocks should be limited to DCT_DCT.
    public bool reduced_tx_type_set;
    /// Indicates if transform type for intra blocks should be limited to DCT_DCT.
    public bool use_intra_dct_only;
    /// Indicates if transform type for inter blocks should be limited to DCT_DCT.
    public bool use_inter_dct_only;
    /// Indicates if intra blocks should use default transform type
    /// (mode-dependent) only.
    public bool use_intra_default_tx_only;
    /// Indicates if transform size search should be enabled.
    public bool enable_tx_size_search;
}

/// <summary>Encoder flags for compound prediction modes.</summary>
internal partial struct CompoundTypeCfg
{
    /// Indicates if distance-weighted compound type should be enabled.
    public bool enable_dist_wtd_comp;
    /// Indicates if masked (wedge/diff-wtd) compound type should be enabled.
    public bool enable_masked_comp;
    /// Indicates if smooth interintra mode should be enabled.
    public bool enable_smooth_interintra;
    /// Indicates if difference-weighted compound type should be enabled.
    public bool enable_diff_wtd_comp;
    /// Indicates if inter-inter wedge compound type should be enabled.
    public bool enable_interinter_wedge;
    /// Indicates if inter-intra wedge compound type should be enabled.
    public bool enable_interintra_wedge;
}

/// <summary>Encoder config related to frame super-resolution.</summary>
internal partial struct SuperResCfg
{
    /// Indicates the qindex based threshold to be used when AOM_SUPERRES_QTHRESH
    /// mode is used for inter frames.
    public int superres_qthresh;
    /// Indicates the qindex based threshold to be used when AOM_SUPERRES_QTHRESH
    /// mode is used for key frames.
    public int superres_kf_qthresh;
    /// Indicates the denominator of the fraction that specifies the ratio between
    /// the superblock width before and after upscaling for inter frames. The
    /// numerator of this fraction is equal to the constant SCALE_NUMERATOR.
    public byte superres_scale_denominator;
    /// Indicates the denominator of the fraction that specifies the ratio between
    /// the superblock width before and after upscaling for key frames. The
    /// numerator of this fraction is equal to the constant SCALE_NUMERATOR.
    public byte superres_kf_scale_denominator;
    /// Indicates the Super-resolution mode to be used by the encoder.
    public aom_superres_mode superres_mode;
    /// Flag to indicate if super-resolution should be enabled for the sequence.
    public bool enable_superres;
}

/// <summary>Encoder config related to the coding of key frames.</summary>
internal partial struct KeyFrameCfg
{
    /// Indicates the minimum distance to a key frame.
    public int key_freq_min;
    /// Indicates the maximum distance to a key frame.
    public int key_freq_max;
    /// Indicates if temporal filtering should be applied on keyframe.
    public int enable_keyframe_filtering;
    /// Indicates the number of frames after which a frame may be coded as an
    /// S-Frame.
    public int sframe_dist;
    /// Indicates how an S-Frame should be inserted.
    /// 1: the considered frame will be made into an S-Frame only if it is an
    /// altref frame. 2: the next altref frame will be made into an S-Frame.
    public int sframe_mode;
    /// Indicates if encoder should autodetect cut scenes and set the keyframes.
    public bool auto_key;
    /// Indicates the forward key frame distance.
    public int fwd_kf_dist;
    /// Indicates if forward keyframe reference should be enabled.
    public bool fwd_kf_enabled;
    /// Indicates if S-Frames should be enabled for the sequence.
    public bool enable_sframe;
    /// Indicates if intra block copy prediction mode should be enabled or not.
    public bool enable_intrabc;
}

/// <summary>Encoder rate control configuration parameters</summary>
internal partial struct RateControlCfg
{
    /// Indicates the amount of data that will be buffered by the decoding
    /// application prior to beginning playback, and is expressed in units of
    /// time(milliseconds).
    public long starting_buffer_level_ms;
    /// Indicates the amount of data that the encoder should try to maintain in the
    /// decoder's buffer, and is expressed in units of time(milliseconds).
    public long optimal_buffer_level_ms;
    /// Indicates the maximum amount of data that may be buffered by the decoding
    /// application, and is expressed in units of time(milliseconds).
    public long maximum_buffer_size_ms;
    /// Indicates the bandwidth to be used in bits per second.
    public long target_bandwidth;
    /// Indicates average complexity of the corpus in single pass vbr based on
    /// LAP. 0 indicates that corpus complexity vbr mode is disabled.
    public uint vbr_corpus_complexity_lap;
    /// Indicates the maximum allowed bitrate for any intra frame as % of bitrate
    /// target.
    public uint max_intra_bitrate_pct;
    /// Indicates the maximum allowed bitrate for any inter frame as % of bitrate
    /// target.
    public uint max_inter_bitrate_pct;
    /// Indicates the percentage of rate boost for golden frame in CBR mode.
    public uint gf_cbr_boost_pct;
    /// min_cr / 100 indicates the target minimum compression ratio for each
    /// frame.
    public uint min_cr;
    /// Indicates the frame drop threshold.
    public int drop_frames_water_mark;
    /// under_shoot_pct indicates the tolerance of the VBR algorithm to
    /// undershoot and is used as a trigger threshold for more aggressive
    /// adaptation of Q. It's value can range from 0-100.
    public int under_shoot_pct;
    /// over_shoot_pct indicates the tolerance of the VBR algorithm to overshoot
    /// and is used as a trigger threshold for more aggressive adaptation of Q.
    /// It's value can range from 0-1000.
    public int over_shoot_pct;
    /// Indicates the maximum qindex that can be used by the quantizer i.e. the
    /// worst quality qindex.
    public int worst_allowed_q;
    /// Indicates the minimum qindex that can be used by the quantizer i.e. the
    /// best quality qindex.
    public int best_allowed_q;
    /// Indicates the Constant/Constrained Quality level.
    public int cq_level;
    /// Indicates if the encoding mode is vbr, cbr, constrained quality or
    /// constant quality.
    public aom_rc_mode mode;
    /// Indicates the bias (expressed on a scale of 0 to 100) for determining
    /// target size for the current frame. The value 0 indicates the optimal CBR
    /// mode value should be used, and 100 indicates the optimal VBR mode value
    /// should be used.
    public int vbrbias;
    /// Indicates the minimum bitrate to be used for a single frame as a percentage
    /// of the target bitrate.
    public int vbrmin_section;
    /// Indicates the maximum bitrate to be used for a single frame as a percentage
    /// of the target bitrate.
    public int vbrmax_section;
    /// Indicates the maximum consecutive amount of frame drops, in units of time
    /// (milliseconds). This is converted to frame units internally. Only used in
    /// CBR mode.
    public int max_consec_drop_ms;
    /// Indicates if the encoder should force max_q for the next frame.
    public int force_max_q;
}

internal partial struct GFConfig
{
    // Indicates the number of frames lag before encoding is started.
    public int lag_in_frames;
    // Indicates the minimum gf/arf interval to be used.
    public int min_gf_interval;
    // Indicates the maximum gf/arf interval to be used.
    public int max_gf_interval;
    // Indicates the minimum height for GF group pyramid structure to be used.
    public int gf_min_pyr_height;
    // Indicates the maximum height for GF group pyramid structure to be used.
    public int gf_max_pyr_height;
    // Indicates if automatic set and use of altref frames should be enabled.
    public bool enable_auto_arf;
    // Indicates if automatic set and use of (b)ackward (r)ef (f)rames should be
    // enabled.
    public bool enable_auto_brf;
}

internal partial struct TileConfig
{
    // Indicates the number of tile groups.
    public uint num_tile_groups;
    // Indicates the MTU size for a tile group. If mtu is non-zero,
    // num_tile_groups is set to DEFAULT_MAX_NUM_TG.
    public uint mtu;
    // Indicates the number of tile columns in log2.
    public int tile_columns;
    // Indicates the number of tile rows in log2.
    public int tile_rows;
    // Indicates the number of widths in the tile_widths[] array.
    public int tile_width_count;
    // Indicates the number of heights in the tile_heights[] array.
    public int tile_height_count;
    // Indicates the tile widths, and may be empty.
    public Arr64<int> tile_widths;  // [MAX_TILE_COLS]
    // Indicates the tile heights, and may be empty.
    public Arr64<int> tile_heights; // [MAX_TILE_ROWS]
    // Indicates if large scale tile coding should be used.
    public bool enable_large_scale_tile;
    // Indicates if single tile decoding mode should be enabled.
    public bool enable_single_tile_decoding;
    // Indicates if EXT_TILE_DEBUG should be enabled.
    public bool enable_ext_tile_debug;
}

internal partial struct FrameDimensionCfg
{
    // Indicates the frame width.
    public int width;
    // Indicates the frame height.
    public int height;
    // Indicates the maximum frame width possible. This is used for allocating
    // the frame buffers and memory for the encoder.
    public int forced_max_frame_width;
    // Indicates the maximum frame height possible. This is used for allocating
    // the frame buffers and memory for the encoder.
    public int forced_max_frame_height;
    // Indicates the frame width after applying both super-resolution and resize
    // to the coded frame.
    public int render_width;
    // Indicates the frame height after applying both super-resolution and resize
    // to the coded frame.
    public int render_height;
}

internal partial struct MotionModeCfg
{
    // Indicates if warped motion should be enabled.
    public bool enable_warped_motion;
    // Indicates if warped motion should be evaluated or not.
    public bool allow_warped_motion;
    // Indicates if OBMC motion should be enabled.
    public bool enable_obmc;
}

internal partial struct DecoderModelCfg
{
    // Timing info for each frame.
    public aom_timing_info_t timing_info;
    // Indicates the number of time units of a decoding clock.
    public uint num_units_in_decoding_tick;
    // Indicates if decoder model information is present in the coded sequence
    // header.
    public bool decoder_model_info_present_flag;
    // Indicates if display model information is present in the coded sequence
    // header.
    public bool display_model_info_present_flag;
    // Indicates if timing info for each frame is present.
    public bool timing_info_present;
}

internal partial struct CostUpdateFreq
{
    // Indicates the update frequency for coeff costs.
    public COST_UPDATE_TYPE coeff;
    // Indicates the update frequency for mode costs.
    public COST_UPDATE_TYPE mode;
    // Indicates the update frequency for mv costs.
    public COST_UPDATE_TYPE mv;
    // Indicates the update frequency for dv costs.
    public COST_UPDATE_TYPE dv;
}

internal partial struct RefFrameCfg
{
    // Indicates the maximum number of reference frames allowed per frame.
    public uint max_reference_frames;
    // Indicates if the reduced set of references should be enabled.
    public bool enable_reduced_reference_set;
    // Indicates if one-sided compound should be enabled.
    public bool enable_onesided_comp;
}

internal partial struct ColorCfg
{
    // Indicates the color space that should be used.
    public aom_color_primaries_t color_primaries;
    // Indicates the characteristics of transfer function to be used.
    public aom_transfer_characteristics_t transfer_characteristics;
    // Indicates the matrix coefficients to be used for the transfer function.
    public aom_matrix_coefficients_t matrix_coefficients;
    // Indicates the chroma 4:2:0 sample position info.
    public aom_chroma_sample_position_t chroma_sample_position;
    // Indicates if a limited color range or full color range should be used.
    public aom_color_range_t color_range;
}

internal partial struct UnitTestCfg
{
    // Indicates if motion vector unit test should be enabled or not.
    public uint motion_vector_unit_test;
    // Indicates if superblock multipass unit test should be enabled or not.
    public uint sb_multipass_unit_test;
}

internal partial struct TuneCfg
{
    // Indicates the VMAF model path.
    public string vmaf_model_path;
    // Indicates the path to the film grain parameters.
    public string film_grain_table_filename;
    // Indicates the visual tuning metric.
    public aom_tune_metric tuning;
    // Indicates if the current content is screen or default type.
    public aom_tune_content content;
    // Indicates the film grain parameters.
    public int film_grain_test_vector;
    // Indicates the in-block distortion metric to use.
    public aom_dist_metric dist_metric;
}

internal partial struct InputCfg
{
    // Indicates the framerate of the input video.
    public double init_framerate;
    // Indicates the bit-depth of the input video.
    public uint input_bit_depth;
    // Indicates the maximum number of frames to be encoded.
    public uint limit;
    // Indicates the chrome subsampling x value.
    public uint chroma_subsampling_x;
    // Indicates the chrome subsampling y value.
    public uint chroma_subsampling_y;
}

internal partial struct QuantizationCfg
{
    // Indicates if ths ids of quantization matrices used for luma and chroma
    // are fixed (bitstream-level qm_y / qm_u / qm_v) or adaptive.
    public int use_fixed_qp_offsets;
    // Indicates the minimum flatness of the quantization matrix.
    public int qm_minlevel;
    // Indicates the maximum flatness of the quantization matrix.
    public int qm_maxlevel;
    // Indicates if adaptive quantize_b should be enabled.
    public int quant_b_adapt;
    // Indicates the Adaptive Quantization mode to be used.
    public AQ_MODE aq_mode;
    // Indicates the delta q mode to be used.
    public DELTAQ_MODE deltaq_mode;
    // Indicates the delta q mode strength.
    public uint deltaq_strength;
    // Indicates if delta quantization should be enabled in chroma planes.
    public bool enable_chroma_deltaq;
    // Indicates if delta quantization should be enabled for hdr video
    public bool enable_hdr_deltaq;
    // Indicates if encoding with quantization matrices should be enabled.
    public bool using_qm;
}

/// <summary>Algorithm configuration parameters.</summary>
internal partial struct AlgoCfg
{
    /// Controls the level at which rate-distortion optimization of transform
    /// coefficients favors sharpness in the block. Has no impact on RD when set
    /// to zero (default).
    public int sharpness;
    /// Enable adaptive sharpness based on qindex.
    public bool enable_adaptive_sharpness;
    /// Indicates the trellis optimization mode of quantized coefficients.
    /// 0: disabled
    /// 1: enabled
    /// 2: enabled for rd search
    /// 3: true for estimate yrd search
    public int disable_trellis_quant;
    /// The maximum number of frames used to create an arf.
    public int arnr_max_frames;
    /// The temporal filter strength for arf used when creating ARFs.
    public int arnr_strength;
    /// Indicates the CDF update mode
    /// 0: no update
    /// 1: update on every frame(default)
    /// 2: update selectively
    public byte cdf_update_mode;
    /// Indicates if RDO based on frame temporal dependency should be enabled.
    public bool enable_tpl_model;
    /// Indicates if coding of overlay frames for filtered ALTREF frames is
    /// enabled.
    public bool enable_overlay;
    /// Controls loop filtering
    /// 0: Loop filter is disabled for all frames
    /// 1: Loop filter is enabled for all frames
    /// 2: Loop filter is disabled for non-reference frames
    /// 3: Loop filter is disables for the frames with low motion
    public LOOPFILTER_CONTROL loopfilter_control;
    /// Indicates if the application of post-processing filters should be skipped
    /// on reconstructed frame.
    public bool skip_postproc_filtering;
    /// Controls delta-lf / mode_ref_delta usage
    public int mode_ref_delta_enabled;
    /// Controls screen content detection mode
    public aom_screen_detection_mode screen_detection_mode;
}

/// <summary>Encoder configuration parameters for the AV1 coding tools.</summary>
internal partial struct ToolCfg
{
    // Indicates the internal bit depth.
    public aom_bit_depth_t bit_depth;
    // Indicates the superblock size that should be used by the encoder.
    public aom_superblock_size_t superblock_size;
    // Indicates if loopfilter modulation should be enabled.
    public bool enable_deltalf_mode;
    // Indicates how CDEF should be applied.
    public CDEF_CONTROL cdef_control;
    // Indicates if loop restoration filter should be enabled.
    public bool enable_restoration;
    // When enabled, video mode should be used even for single frame input.
    public bool force_video_mode;
    // Indicates if the error resiliency features should be enabled.
    public bool error_resilient_mode;
    // Indicates if frame parallel decoding feature should be enabled.
    public bool frame_parallel_decoding_mode;
    // Indicates if the input should be encoded as monochrome.
    public bool enable_monochrome;
    // When enabled, the encoder will use a full header even for still pictures.
    // When disabled, a reduced header is used for still pictures.
    public bool full_still_picture_hdr;
    // Indicates if dual interpolation filters should be enabled.
    public bool enable_dual_filter;
    // Indicates if frame order hint should be enabled or not.
    public bool enable_order_hint;
    // Indicates if ref_frame_mvs should be enabled at the sequence level.
    public bool ref_frame_mvs_present;
    // Indicates if ref_frame_mvs should be enabled at the frame level.
    public bool enable_ref_frame_mvs;
    // Indicates if interintra compound mode is enabled.
    public bool enable_interintra_comp;
    // Indicates if global motion should be enabled.
    public bool enable_global_motion;
    // Indicates if palette should be enabled.
    public bool enable_palette;
}

/// <summary>Main encoder configuration data structure.</summary>
/// <remarks>C copies it by value (<c>cpi->oxcf = *oxcf</c>): use CopyFrom.</remarks>
internal sealed partial class AV1EncoderConfig
{
    /*!\cond */
    // Configuration related to the input video.
    public InputCfg input_cfg;

    // Configuration related to frame-dimensions.
    public FrameDimensionCfg frm_dim_cfg;

    /*!\endcond */
    /// Encoder algorithm configuration.
    public AlgoCfg algo_cfg;

    /// Configuration related to key-frames.
    public KeyFrameCfg kf_cfg;

    /// Rate control configuration
    public RateControlCfg rc_cfg;
    /*!\cond */

    // Configuration related to Quantization.
    public QuantizationCfg q_cfg;

    // Internal frame size scaling.
    public ResizeCfg resize_cfg;

    // Frame Super-Resolution size scaling.
    public SuperResCfg superres_cfg;

    /*!\endcond */
    /// stats_in buffer contains all of the stats packets produced in the first
    /// pass, concatenated.
    public aom_fixed_buf_t twopass_stats_in;
    /*!\cond */

    // Configuration related to encoder toolsets.
    public ToolCfg tool_cfg;

    // Configuration related to Group of frames.
    public GFConfig gf_cfg;

    // Tile related configuration parameters.
    public TileConfig tile_cfg;

    // Configuration related to Tune.
    public TuneCfg tune_cfg;

    // Configuration related to color.
    public ColorCfg color_cfg;

    // Configuration related to decoder model.
    public DecoderModelCfg dec_model_cfg;

    // Configuration related to reference frames.
    public RefFrameCfg ref_frm_cfg;

    // Configuration related to unit tests.
    public UnitTestCfg unit_test_cfg;

    // Flags related to motion mode.
    public MotionModeCfg motion_mode_cfg;

    // Flags related to intra mode search.
    public IntraModeCfg intra_mode_cfg;

    // Flags related to transform size/type.
    public TxfmSizeTypeCfg txfm_cfg;

    // Flags related to compound type.
    public CompoundTypeCfg comp_type_cfg;

    // Partition related information.
    public PartitionCfg part_cfg;

    // Configuration related to frequency of cost update.
    public CostUpdateFreq cost_upd_freq;

    // CONFIG_DENOISE == 1:
    // Indicates the noise level.
    public float noise_level;
    // Indicates the the denoisers block size.
    public int noise_block_size;
    // Indicates whether to apply denoising to the frame to be encoded
    public int enable_dnl_denoising;

    // [subset] noise_sensitivity: CONFIG_AV1_TEMPORAL_DENOISING == 0.

    // Bit mask to specify which tier each of the 32 possible operating points
    // conforms to.
    public uint tier_mask;

    // Indicates the number of pixels off the edge of a reference frame we're
    // allowed to go when forming an inter prediction.
    public int border_in_pixels;

    // Indicates the maximum number of threads that may be used by the encoder.
    public int max_threads;

    // Indicates the speed preset to be used.
    public int speed;

    // Enable the low complexity decode mode.
    public uint enable_low_complexity_decode;

    // Indicates the target sequence level index for each operating point(OP).
    public readonly AV1_LEVEL[] target_seq_level_idx = new AV1_LEVEL[MAX_NUM_OPERATING_POINTS];

    // Indicates the bitstream profile to be used.
    public BITSTREAM_PROFILE profile;

    /*!\endcond */
    /// Indicates the current encoder pass :
    /// AOM_RC_ONE_PASS = One pass encode,
    /// AOM_RC_FIRST_PASS = First pass of multiple-pass
    /// AOM_RC_SECOND_PASS = Second pass of multiple-pass
    /// AOM_RC_THIRD_PASS = Third pass of multiple-pass
    public aom_enc_pass pass;
    /*!\cond */

    // Total number of encoding passes.
    public int passes;

    // the name of the second pass output file when passes > 2
    public string two_pass_output;

    // the name of the second pass log file when passes > 2
    public string second_pass_log;

    // Indicates if the encoding is GOOD or REALTIME.
    public MODE mode;

    // Indicates if row-based multi-threading should be enabled or not.
    public bool row_mt;

    // Indicates if frame parallel multi-threading should be enabled or not.
    public bool fp_mt;

    // Indicates if 16bit frame buffers are to be used i.e., the content is >
    // 8-bit.
    public bool use_highbitdepth;

    // Indicates the bitstream syntax mode. 0 indicates bitstream is saved as
    // Section 5 bitstream, while 1 indicates the bitstream is saved in Annex - B
    // format.
    public bool save_as_annexb;

    // The path for partition stats reading and writing, used in the experiment
    // CONFIG_PARTITION_SEARCH_ORDER.
    public string partition_info_path;

    // The flag that indicates whether we use an external rate distribution to
    // guide adaptive quantization. It requires --deltaq-mode=3. The rate
    // distribution map file name is stored in |rate_distribution_info|.
    public uint enable_rate_guide_deltaq;

    // The input file of rate distribution information used in all intra mode
    // to determine delta quantization.
    public string rate_distribution_info;

    // Exit the encoder when it fails to encode to a given level.
    public int strict_level_conformance;

    // Max depth for the GOP after a key frame
    public int kf_max_pyr_height;

    // A flag to control if we enable the superblock qp sweep for a given lambda
    public int sb_qp_sweep;
    /*!\endcond */

    /// <summary>C struct assignment (<c>*this = *other</c>).</summary>
    public void CopyFrom(AV1EncoderConfig other)
    {
        input_cfg = other.input_cfg;
        frm_dim_cfg = other.frm_dim_cfg;
        algo_cfg = other.algo_cfg;
        kf_cfg = other.kf_cfg;
        rc_cfg = other.rc_cfg;
        q_cfg = other.q_cfg;
        resize_cfg = other.resize_cfg;
        superres_cfg = other.superres_cfg;
        twopass_stats_in = other.twopass_stats_in;
        tool_cfg = other.tool_cfg;
        gf_cfg = other.gf_cfg;
        tile_cfg = other.tile_cfg;
        tune_cfg = other.tune_cfg;
        color_cfg = other.color_cfg;
        dec_model_cfg = other.dec_model_cfg;
        ref_frm_cfg = other.ref_frm_cfg;
        unit_test_cfg = other.unit_test_cfg;
        motion_mode_cfg = other.motion_mode_cfg;
        intra_mode_cfg = other.intra_mode_cfg;
        txfm_cfg = other.txfm_cfg;
        comp_type_cfg = other.comp_type_cfg;
        part_cfg = other.part_cfg;
        cost_upd_freq = other.cost_upd_freq;
        noise_level = other.noise_level;
        noise_block_size = other.noise_block_size;
        enable_dnl_denoising = other.enable_dnl_denoising;
        tier_mask = other.tier_mask;
        border_in_pixels = other.border_in_pixels;
        max_threads = other.max_threads;
        speed = other.speed;
        enable_low_complexity_decode = other.enable_low_complexity_decode;
        other.target_seq_level_idx.AsSpan(0, MAX_NUM_OPERATING_POINTS).CopyTo(target_seq_level_idx);
        profile = other.profile;
        pass = other.pass;
        passes = other.passes;
        two_pass_output = other.two_pass_output;
        second_pass_log = other.second_pass_log;
        mode = other.mode;
        row_mt = other.row_mt;
        fp_mt = other.fp_mt;
        use_highbitdepth = other.use_highbitdepth;
        save_as_annexb = other.save_as_annexb;
        partition_info_path = other.partition_info_path;
        enable_rate_guide_deltaq = other.enable_rate_guide_deltaq;
        rate_distribution_info = other.rate_distribution_info;
        strict_level_conformance = other.strict_level_conformance;
        kf_max_pyr_height = other.kf_max_pyr_height;
        sb_qp_sweep = other.sb_qp_sweep;
    }
}

/// <summary>Encoder-side probabilities for pruning of various AV1 tools</summary>
internal sealed partial class FrameProbInfo
{
    /// obmc_probs[i][j] is the probability of OBMC being the best motion mode for
    /// jth block size and ith frame update type, averaged over past frames. If
    /// obmc_probs[i][j] &lt; thresh, then OBMC search is pruned.
    public readonly int[][] obmc_probs = alloc_array2<int>(FRAME_UPDATE_TYPES, BLOCK_SIZES_ALL);

    /// warped_probs[i] is the probability of warped motion being the best motion
    /// mode for ith frame update type, averaged over past frames. If
    /// warped_probs[i] &lt; thresh, then warped motion search is pruned.
    public readonly int[] warped_probs = new int[FRAME_UPDATE_TYPES];

    /// tx_type_probs[i][j][k] is the probability of kth tx_type being the best
    /// for jth transform size and ith frame update type, averaged over past
    /// frames. If tx_type_probs[i][j][k] &lt; thresh, then transform search for
    /// that type is pruned.
    public readonly int[][][] tx_type_probs = alloc_array3<int>(FRAME_UPDATE_TYPES, TX_SIZES_ALL, TX_TYPES);

    /// switchable_interp_probs[i][j][k] is the probability of kth interpolation
    /// filter being the best for jth filter context and ith frame update type,
    /// averaged over past frames. If switchable_interp_probs[i][j][k] &lt; thresh,
    /// then interpolation filter search is pruned for that case.
    public readonly int[][][] switchable_interp_probs =
        alloc_array3<int>(FRAME_UPDATE_TYPES, SWITCHABLE_FILTER_CONTEXTS, SWITCHABLE_FILTERS);
}

/// <remarks>C copies it by value (<c>*td->counts = cpi->counts</c>): use CopyFrom.
/// <c>av1_zero(*counts)</c>: use Clear.</remarks>
internal sealed partial class FRAME_COUNTS
{
    // Note: This structure should only contain 'unsigned int' fields, or
    // aggregates built solely from 'unsigned int' fields/elements
    // [subset] all CONFIG_ENTROPY_STATS counters: CONFIG_ENTROPY_STATS == 0.
    public readonly uint[][] switchable_interp =
        alloc_array2<uint>(SWITCHABLE_FILTER_CONTEXTS, SWITCHABLE_FILTERS);

    /// <summary>C struct assignment (<c>*this = *other</c>).</summary>
    public void CopyFrom(FRAME_COUNTS other)
    {
        for (int i = 0; i < SWITCHABLE_FILTER_CONTEXTS; i++)
            other.switchable_interp[i].AsSpan(0, SWITCHABLE_FILTERS).CopyTo(switchable_interp[i]);
    }

    /// <summary>av1_zero(*this).</summary>
    public void Clear()
    {
        for (int i = 0; i < SWITCHABLE_FILTER_CONTEXTS; i++)
            switchable_interp[i].AsSpan().Clear();
    }
}

internal partial struct InterModeRdModel
{
    public int ready;
    public double a;
    public double b;
    public double dist_mean;
    public double ld_mean;
    public double sse_mean;
    public double sse_sse_mean;
    public double sse_ld_mean;
    public int num;
    public double dist_sum;
    public double ld_sum;
    public double sse_sum;
    public double sse_sse_sum;
    public double sse_ld_sum;
}

// [subset] RdIdxPair and InterModesInfo dropped: only used by the rd (non-rt)
// inter mode search in rdopt.c (MACROBLOCK::inter_modes_info is dropped by p1b2).

// Variance-based partitioning tree. These are classes (not structs) because
// var_based_part.c builds `variance_node`s that hold pointers into them
// (node->part_variances = &vt->part_variances; node->split[i] = ...none).

/// <summary>This structure contains the sum of squared error and the sum of error.</summary>
internal sealed partial class VPartVar
{
    /// Sum of squared errors.
    public uint sum_square_error;
    /// Sum of errors.
    public int sum_error;
    /// Number of 4x4 pixels in the block (log2).
    public int log2_count;
    /// Variance.
    public int variance;
}

/// <summary>Variance of the block and its sub-blocks for each partition type.</summary>
internal sealed partial class VPVariance
{
    public VPartVar none = new();
    public readonly VPartVar[] horz = alloc_array<VPartVar>(2);
    public readonly VPartVar[] vert = alloc_array<VPartVar>(2);
}

internal sealed partial class VP4x4
{
    public VPVariance part_variances = new();
    public readonly VPartVar[] split = alloc_array<VPartVar>(4);
}

internal sealed partial class VP8x8
{
    public VPVariance part_variances = new();
    public readonly VP4x4[] split = alloc_array<VP4x4>(4);
}

internal sealed partial class VP16x16
{
    public VPVariance part_variances = new();
    public readonly VP8x8[] split = alloc_array<VP8x8>(4);
}

internal sealed partial class VP32x32
{
    public VPVariance part_variances = new();
    public readonly VP16x16[] split = alloc_array<VP16x16>(4);
}

internal sealed partial class VP64x64
{
    public VPVariance part_variances = new();
    public readonly VP32x32[] split = alloc_array<VP32x32>(4);
}

internal sealed partial class VP128x128
{
    public VPVariance part_variances = new();
    // C: VP64x64 *split; points at td->vt64x64 (4 entries).
    public VP64x64[] split;
}

/// <summary>Thresholds for variance based partitioning.</summary>
internal sealed partial class VarBasedPartitionInfo
{
    /// If block variance > threshold, then that block is forced to split.
    /// thresholds[0] - threshold for 128x128;
    /// thresholds[1] - threshold for 64x64;
    /// thresholds[2] - threshold for 32x32;
    /// thresholds[3] - threshold for 16x16;
    /// thresholds[4] - threshold for 8x8;
    public readonly long[] thresholds = new long[5];

    /// MinMax variance threshold for 8x8 sub blocks of a 16x16 block. If actual
    /// minmax > threshold_minmax for any 8x8 sub block, then that 16x16 block is
    /// forced to split.
    public long threshold_minmax;
}

// PORT-NOTE: the C fields are anonymous function pointers; these delegate
// names are ours.
internal delegate void enc_row_mt_sync_read_t(AV1EncRowMultiThreadSync row_mt_sync, int r, int c);
internal delegate void enc_row_mt_sync_write_t(AV1EncRowMultiThreadSync row_mt_sync, int r, int c, int cols);

/// <summary>Encoder parameters for synchronization of row based multi-threading</summary>
internal sealed partial class AV1EncRowMultiThreadSync
{
    // CONFIG_MULTITHREAD == 1:
    /// Mutex lock object per sb row (C: pthread_mutex_t *mutex_, an array).
    public pthread_mutex_t[] mutex_;
    /// Condition variable per sb row (C: pthread_cond_t *cond_, an array).
    public pthread_cond_t[] cond_;
    /// Buffer to store the superblock whose encoding is complete.
    /// num_finished_cols[i] stores the number of superblocks which finished
    /// encoding in the ith superblock row.
    public Ptr<int> num_finished_cols;
    /// Denotes the superblock interval at which conditional signalling should
    /// happen. Also denotes the minimum number of extra superblocks of the top row
    /// to be complete to start encoding the current superblock. A value of 1
    /// indicates top-right dependency.
    public int sync_range;
    /// Denotes the additional number of superblocks in the previous row to be
    /// complete to start encoding the current superblock when intraBC tool is
    /// enabled. This additional top-right delay is required to satisfy the
    /// hardware constraints for intraBC tool when row multithreading is enabled.
    public int intrabc_extra_top_right_sb_delay;
    /// Number of superblock rows.
    public int rows;
    /// The superblock row (in units of MI blocks) to be processed next.
    public int next_mi_row;
    /// Number of threads processing the current tile.
    public int num_threads_working;

    /// <summary>av1_zero(*this) (encodeframe.c / ethread.c reset it in place).</summary>
    public void Clear()
    {
        mutex_ = null;
        cond_ = null;
        num_finished_cols = Ptr<int>.Null;
        sync_range = 0;
        intrabc_extra_top_right_sb_delay = 0;
        rows = 0;
        next_mi_row = 0;
        num_threads_working = 0;
    }
}

/// <summary>Encoder data related to multi-threading for allintra deltaq-mode=3</summary>
// [subset] AV1EncAllIntraMultiThreadInfo: allintra deltaq-mode=3 only.

/// <summary>Encoder parameters related to a tile.</summary>
internal sealed partial class TileDataEnc
{
    public TileInfo tile_info;
    // C: DECLARE_ALIGNED(16, FRAME_CONTEXT, tctx) embedded; assigned by value
    // (tile_data->tctx = *cm->fc).
    public FRAME_CONTEXT tctx = new();
    public FRAME_CONTEXT[] row_ctx;
    public ulong abs_sum_level;
    public byte allow_update_cdf;
    public readonly InterModeRdModel[] inter_mode_rd_models = new InterModeRdModel[BLOCK_SIZES_ALL];
    public AV1EncRowMultiThreadSync row_mt_sync = new();
    public MV firstpass_top_mv;
}

/// <remarks>C copies it by value (<c>td->rd_counts = cpi->td.rd_counts</c>): use CopyFrom.</remarks>
internal sealed partial class RD_COUNTS
{
    public int compound_ref_used_flag;
    public int skip_mode_used_flag;
    public readonly int[][] tx_type_used = alloc_array2<int>(TX_SIZES_ALL, TX_TYPES);
    public readonly int[][] obmc_used = alloc_array2<int>(BLOCK_SIZES_ALL, 2);
    public readonly int[] warped_used = new int[2];
    public int newmv_or_intra_blocks;
    public readonly ulong[] seg_tmp_pred_cost = new ulong[2];

    /// <summary>C struct assignment (<c>*this = *other</c>).</summary>
    public void CopyFrom(RD_COUNTS other)
    {
        compound_ref_used_flag = other.compound_ref_used_flag;
        skip_mode_used_flag = other.skip_mode_used_flag;
        for (int i = 0; i < TX_SIZES_ALL; i++)
            other.tx_type_used[i].AsSpan(0, TX_TYPES).CopyTo(tx_type_used[i]);
        for (int i = 0; i < BLOCK_SIZES_ALL; i++)
            other.obmc_used[i].AsSpan(0, 2).CopyTo(obmc_used[i]);
        other.warped_used.AsSpan(0, 2).CopyTo(warped_used);
        newmv_or_intra_blocks = other.newmv_or_intra_blocks;
        other.seg_tmp_pred_cost.AsSpan(0, 2).CopyTo(seg_tmp_pred_cost);
    }
}

// Class: used through pointers (EncWorkerData::td, &cpi->td). Never copied by
// value as a whole (only its mb / rd_counts / counts members are).
//
// [subset] dropped fields, following p1b2 (block.h) which dropped the types or
// the MACROBLOCK members they feed:
//   dv_costs_alloc (IntraBCMVCosts), hash_value_buffer[2] (feeds
//   mb.intrabc_hash_info), obmc_buffer (OBMCBuffer), palette_buffer
//   (PALETTE_BUFFER), comp_rd_buffer (CompoundTypeRdBuffers): intraBC, OBMC,
//   palette and masked-compound search are off in the subset.
internal sealed partial class ThreadData
{
    public MACROBLOCK mb = new();
    public MvCosts mv_costs_alloc;
    // [port] Not in C: the MvCosts prepare_enc_workers allocates for this thread, kept for the
    // next frame (C mallocs it per frame; it is fully overwritten by the copy from cpi->td.mb).
    public MvCosts mv_costs_pool;
    public RD_COUNTS rd_counts = new();
    public FRAME_COUNTS counts;
    public PC_TREE_SHARED_BUFFERS shared_coeff_buf = new();
    public SIMPLE_MOTION_DATA_TREE[] sms_tree;
    public SIMPLE_MOTION_DATA_TREE sms_root;
    public Ptr<CONV_BUF_TYPE> tmp_conv_dst;
    public ulong abs_sum_level;
    public readonly Ptr<byte>[] tmp_pred_bufs = new Ptr<byte>[2];
    public Ptr<byte> upsample_pred;
    public Ptr<byte> wiener_tmp_pred_buf;
    public int intrabc_used;
    public int deltaq_used;
    public int coefficient_size;
    public int max_mv_magnitude;
    public readonly int[] interp_filter_selected = new int[SWITCHABLE];
    public FRAME_CONTEXT tctx;
    public VP64x64[] vt64x64;
    public int num_64x64_blocks;
    public PICK_MODE_CONTEXT firstpass_ctx;
    // [subset] tf_data (TemporalFilterData), tpl_tmp_buffers (TplBuffers),
    // tpl_txfm_stats (TplTxfmStats), gm_data (GlobalMotionData) dropped:
    // temporal filtering / TPL / global motion search are out of the subset.
    // Buffer to store the pixel gradient information of a superblock. The
    // buffer is reused across the superblocks of a frame.
    public Ptr<PixelLevelGradientInfo> pixel_gradient_info;
    // Buffer to store the source variance of 4x4 sub-blocks of a
    // superblock. The buffer is reused across the superblocks of a frame.
    public Ptr<Block4x4VarInfo> src_var_info_of_4x4_sub_blocks;
    // Pointer to pc tree root.
    public PC_TREE pc_root;
}

/// <summary>Encoder data related to row-based multi-threading</summary>
internal sealed partial class AV1EncRowMultiThreadInfo
{
    /// Number of tile rows for which row synchronization memory is allocated.
    public int allocated_tile_rows;
    /// Number of tile cols for which row synchronization memory is allocated.
    public int allocated_tile_cols;
    /// Number of rows for which row synchronization memory is allocated
    /// per tile. During first-pass/look-ahead stage this equals the
    /// maximum number of macroblock rows in a tile. During encode stage,
    /// this equals the maximum number of superblock rows in a tile.
    public int allocated_rows;
    /// Number of columns for which entropy context memory is allocated
    /// per tile. During encode stage, this equals the maximum number of
    /// superblock columns in a tile minus 1. The entropy context memory
    /// is not allocated during first-pass/look-ahead stage.
    public int allocated_cols;

    /// thread_id_to_tile_id[i] indicates the tile id assigned to the ith thread.
    public readonly int[] thread_id_to_tile_id = new int[MAX_NUM_THREADS];

    /// num_tile_cols_done[i] indicates the number of tile columns whose encoding
    /// is complete in the ith superblock row.
    public Ptr<int> num_tile_cols_done;

    /// Number of superblock rows in a frame for which 'num_tile_cols_done' is
    /// allocated.
    public int allocated_sb_rows;

    /// Initialized to false, set to true by the worker thread that encounters an
    /// error in order to abort the processing of other worker threads.
    public bool row_mt_exit;

    // [subset] firstpass_mt_exit (first pass) and mb_wiener_mt_exit (allintra
    // deltaq) dropped.

    // CONFIG_MULTITHREAD == 1:
    /// Mutex lock used while dispatching jobs.
    public pthread_mutex_t mutex_;
    /// Condition variable used to dispatch loopfilter jobs.
    public pthread_cond_t cond_;

    /// Row synchronization related function pointers.
    public enc_row_mt_sync_read_t sync_read_ptr;
    public enc_row_mt_sync_write_t sync_write_ptr;
}

// [subset] RestoreStateBuffers (frame-parallel MT restore of CDEF/LR buffers),
// RestUnitSearchInfo and AV1LrPickStruct (loop restoration search) dropped.

/// <summary>Primary Encoder parameters related to multi-threading.</summary>
internal sealed partial class PrimaryMultiThreadInfo
{
    /// Number of workers created for multi-threading.
    public int num_workers;

    /// Number of workers used for different MT modules.
    public readonly int[] num_mod_workers = new int[NUM_MT_MODULES];

    /// Synchronization object used to launch job in the worker thread.
    public AVxWorker[] workers;

    /// Data specific to each worker in encoder multi-threading.
    /// tile_thr_data[i] stores the worker data of the ith thread.
    public EncWorkerData[] tile_thr_data;

    /// CDEF row multi-threading data.
    public AV1CdefWorkerData[] cdef_worker;

    // [subset] p_workers[MAX_PARALLEL_FRAMES], p_num_workers: frame-parallel
    // encoding is out of the subset.

    /// Tracks the number of workers in encode stage multi-threading.
    public int prev_num_enc_workers;
}

/// <summary>Encoder parameters related to multi-threading.</summary>
internal sealed partial class MultiThreadInfo
{
    /// Number of workers created for multi-threading.
    public int num_workers;

    /// Number of workers used for different MT modules.
    public readonly int[] num_mod_workers = new int[NUM_MT_MODULES];

    /// Synchronization object used to launch job in the worker thread.
    public AVxWorker[] workers;

    /// Data specific to each worker in encoder multi-threading.
    /// tile_thr_data[i] stores the worker data of the ith thread.
    public EncWorkerData[] tile_thr_data;

    /// When set, indicates that row based multi-threading of the encoder is
    /// enabled.
    public bool row_mt_enabled;

    /// When set, indicates that multi-threading for bitstream packing is enabled.
    public bool pack_bs_mt_enabled;

    /// Encoder row multi-threading data.
    public AV1EncRowMultiThreadInfo enc_row_mt = new();

    // [subset] intra_mt (AV1EncAllIntraMultiThreadInfo): allintra only.

    /// Tpl row multi-threading data.
    public AV1TplRowMultiThreadInfo tpl_row_mt = new();

    /// Loop Filter multi-threading object.
    public AV1LfSync lf_row_sync = new();

    // [subset] lr_row_sync (AV1LrSync): loop restoration is out of the subset.

    /// Pack bitstream multi-threading object.
    public AV1EncPackBSSync pack_bs_sync = new();

    /// Global Motion multi-threading object.
    public AV1GlobalMotionSync gm_sync = new();

    // [subset] tf_sync (AV1TemporalFilterSync): temporal filtering is out of the
    // subset.

    /// CDEF search multi-threading object.
    public AV1CdefSync cdef_sync = new();

    /// Pointer to CDEF row multi-threading data for the frame.
    public AV1CdefWorkerData[] cdef_worker;

    // [subset] restore_state_buf (RestoreStateBuffers): frame-parallel only.

    /// In multi-threaded realtime encoding with row-mt enabled, pipeline
    /// loop-filtering after encoding.
    public int pipeline_lpf_mt_with_enc;

    // [av1rt] CDEF filtering deferred into the bitstream packing's worker launch (see
    // cdef_restoration_frame): the number of workers it was set up for (0: none pending), and the
    // av1_cdef_frame_mt arguments for running it on its own if the packing does not take it.
    public int av1rt_cdef_pending;
    public MACROBLOCKD av1rt_cdef_xd;
    public int av1rt_cdef_do_extend_border;

    // [port] Not in C: av1_write_tile_obu_mt's stack arrays (PackBSParams pack_bs_params[MAX_TILES],
    // unsigned int tile_size[MAX_TILES]), kept across frames.
    public PackBSParams[] pack_bs_params_buf;
    public uint[] pack_bs_tile_size_buf;
}

internal sealed partial class ActiveMap
{
    public int enabled;
    public int update;
    public int mi_rows;
    public int mi_cols;
    public Ptr<byte> map;
}

/// <summary>Encoder info used for decision on forcing integer motion vectors.</summary>
internal sealed partial class ForceIntegerMVInfo
{
    /// cs_rate_array[i] is the fraction of blocks in a frame which either match
    /// with the collocated block or are smooth, where i is the rate_index.
    public readonly double[] cs_rate_array = new double[32];
    /// rate_index is used to index cs_rate_array.
    public int rate_index;
    /// rate_size is the total number of entries populated in cs_rate_array.
    public int rate_size;
}

// [subset] EncRefCntBuffer: unused.

/// <summary>Buffer to store mode information at mi_alloc_bsize (4x4 or 8x8) level</summary>
/// <remarks>
/// This is used for bitstream preparation.
/// </remarks>
internal sealed partial class MBMIExtFrameBufferInfo
{
    /// frame_base[mi_row * stride + mi_col] stores the mode information of
    /// block (mi_row,mi_col).
    public MB_MODE_INFO_EXT_FRAME[] frame_base;
    /// Size of frame_base buffer.
    public int alloc_size;
    /// Stride of frame_base buffer.
    public int stride;
}

/// <summary>Parameters related to global motion search</summary>
internal sealed partial class GlobalMotionInfo
{
    /// Flag to indicate if global motion search needs to be rerun.
    public bool search_done;
    // [subset] ref_buf[], num_ref_frames[], reference_frames[][],
    // segment_map_w, segment_map_h: global motion search is out of the subset.
}

/// <summary>Flags related to interpolation filter search</summary>
internal sealed partial class InterpSearchFlags
{
    /// Stores the default value of skip flag depending on chroma format
    /// Set as 1 for monochrome and 3 for other color formats
    public int default_interp_skip_flags;
    /// Filter mask to allow certain interp_filter type.
    public ushort interp_filter_search_mask;
}

/// <summary>Parameters for motion vector search process</summary>
internal sealed partial class MotionVectorSearchParams
{
    /// Largest MV component used in a frame.
    /// The value from the previous frame is used to set the full pixel search
    /// range for the current frame.
    public int max_mv_magnitude;
    /// Parameter indicating initial search window to be used in full-pixel search.
    /// Range [0, MAX_MVSEARCH_STEPS-2]. Lower value indicates larger window.
    public int mv_step_param;
    /// Pointer to sub-pixel search function.
    /// In encoder: av1_find_best_sub_pixel_tree
    ///             av1_find_best_sub_pixel_tree_pruned
    ///             av1_find_best_sub_pixel_tree_pruned_more
    /// In MV unit test: av1_return_max_sub_pixel_mv
    ///                  av1_return_min_sub_pixel_mv
    public fractional_mv_step_fp find_fractional_mv_step;
    /// Search site configuration for full-pel MV search.
    /// search_site_cfg[SS_CFG_SRC]: Used in tpl, rd/non-rd inter mode loop, simple
    /// motion search. search_site_cfg[SS_CFG_LOOKAHEAD]: Used in intraBC, temporal
    /// filter search_site_cfg[SS_CFG_FPF]: Used during first pass and lookahead
    public readonly search_site_config[][] search_site_cfg =
        alloc_array2<search_site_config>(SS_CFG_TOTAL, NUM_DISTINCT_SEARCH_METHODS);
}

/// <summary>Refresh frame flags for different type of frames.</summary>
/// <remarks>
/// If the refresh flag is true for a particular reference frame, after the
/// current frame is encoded, the reference frame gets refreshed (updated) to
/// be the current frame. Note: Usually at most one flag will be set to true at
/// a time. But, for key-frames, all flags are set to true at once.
/// </remarks>
internal partial struct RefreshFrameInfo
{
    public bool golden_frame;  /*!< Refresh flag for golden frame */
    public bool bwd_ref_frame; /*!< Refresh flag for bwd-ref frame */
    public bool alt_ref_frame; /*!< Refresh flag for alt-ref frame */
}

/// <summary>Desired dimensions for an externally triggered resize.</summary>
/// <remarks>
/// When resize is triggered externally, the desired dimensions are stored in
/// this struct until used in the next frame to be coded. These values are
/// effective only for one frame and are reset after they are used.
/// </remarks>
internal partial struct ResizePendingParams
{
    public int width;  /*!< Desired resized width */
    public int height; /*!< Desired resized height */
}

/// <summary>Reference frame distance related variables.</summary>
internal sealed partial class RefFrameDistanceInfo
{
    /// True relative distance of reference frames w.r.t. the current frame.
    public readonly int[] ref_relative_dist = new int[INTER_REFS_PER_FRAME];
    /// The nearest reference w.r.t. current frame in the past.
    public sbyte nearest_past_ref;
    /// The nearest reference w.r.t. current frame in the future.
    public sbyte nearest_future_ref;
}

/// <summary>Parameters used for winner mode processing.</summary>
/// <remarks>
/// This is a basic two pass approach: in the first pass, we reduce the number of
/// transform searches based on some thresholds during the rdopt process to find
/// the  "winner mode". In the second pass, we perform a more through tx search
/// on the winner mode.
/// There are some arrays in the struct, and their indices are used in the
/// following manner:
/// Index 0: Default mode evaluation, Winner mode processing is not applicable
/// (Eg : IntraBc).
/// Index 1: Mode evaluation.
/// Index 2: Winner mode evaluation
/// Index 1 and 2 are only used when the respective speed feature is on.
/// </remarks>
internal sealed partial class WinnerModeParams
{
    /// Threshold to determine if trellis optimization is to be enabled
    /// based on :
    /// 0 : dist threshold
    /// 1 : satd threshold
    /// Corresponds to enable_winner_mode_for_coeff_opt speed feature.
    public readonly uint[][] coeff_opt_thresholds = alloc_array2<uint>(MODE_EVAL_TYPES, 2);

    /// Determines the tx size search method during rdopt.
    /// Corresponds to enable_winner_mode_for_tx_size_srch speed feature.
    public readonly TX_SIZE_SEARCH_METHOD[] tx_size_search_methods = new TX_SIZE_SEARCH_METHOD[MODE_EVAL_TYPES];

    /// Controls how often we should approximate prediction error with tx
    /// coefficients. If it's 0, then never. If 1, then it's during the tx_type
    /// search only. If 2, then always.
    /// Corresponds to tx_domain_dist_level speed feature.
    public readonly uint[] use_transform_domain_distortion = new uint[MODE_EVAL_TYPES];

    /// Threshold to approximate pixel domain distortion with transform domain
    /// distortion. This is only used if use_transform_domain_distortion is on.
    /// Corresponds to enable_winner_mode_for_use_tx_domain_dist speed feature.
    public readonly uint[] tx_domain_dist_threshold = new uint[MODE_EVAL_TYPES];

    /// Controls how often we should try to skip the transform process based on
    /// result from dct.
    /// Corresponds to use_skip_flag_prediction speed feature.
    public readonly uint[] skip_txfm_level = new uint[MODE_EVAL_TYPES];

    /// Predict DC only txfm blocks for default, mode and winner mode evaluation.
    /// Index 0: Default mode evaluation, Winner mode processing is not applicable.
    /// Index 1: Mode evaluation, Index 2: Winner mode evaluation
    public readonly uint[] predict_dc_level = new uint[MODE_EVAL_TYPES];
}

/// <summary>Frame refresh flags set by the external interface.</summary>
/// <remarks>
/// Flags set by external interface to determine which reference buffers are
/// refreshed by this frame. When set, the encoder will update the particular
/// reference frame buffer with the contents of the current frame.
/// </remarks>
internal partial struct ExtRefreshFrameFlagsInfo
{
    public bool last_frame;     /*!< Refresh flag for last frame */
    public bool golden_frame;   /*!< Refresh flag for golden frame */
    public bool bwd_ref_frame;  /*!< Refresh flag for bwd-ref frame */
    public bool alt2_ref_frame; /*!< Refresh flag for alt2-ref frame */
    public bool alt_ref_frame;  /*!< Refresh flag for alt-ref frame */
    /// Flag indicating if the update of refresh frame flags is pending.
    public bool update_pending;
}

/// <summary>Flags signalled by the external interface at frame level.</summary>
internal sealed partial class ExternalFlags
{
    /// Bit mask to disable certain reference frame types.
    public int ref_frame_flags;

    /// Frame refresh flags set by the external interface.
    public ExtRefreshFrameFlagsInfo refresh_frame;

    /// Flag to enable the update of frame contexts at the end of a frame decode.
    public bool refresh_frame_context;

    /// Flag to indicate that update of refresh_frame_context from external
    /// interface is pending.
    public bool refresh_frame_context_pending;

    /// Flag to enable temporal MV prediction.
    public bool use_ref_frame_mvs;

    /// Indicates whether the current frame is to be coded as error resilient.
    public bool use_error_resilient;

    /// Indicates whether the current frame is to be coded as s-frame.
    public bool use_s_frame;

    /// Indicates whether the current frame's primary_ref_frame is set to
    /// PRIMARY_REF_NONE.
    public bool use_primary_ref_none;
}

internal partial struct MV_STATS
{
    // Data for decision making
    public int high_prec;
    public int q;
    public int order;

    // Coding statistics
    public int inter_count;
    public int intra_count;
    public int default_mvs;
    public Arr4<int> mv_joint_count;
    public int last_bit_zero;
    public int last_bit_nonzero;

    // Rate statistics
    public int total_mv_rate;
    public int hp_total_mv_rate;
    public int lp_total_mv_rate;

    // Texture information
    public int horz_text;
    public int vert_text;
    public int diag_text;

    // Whether the current struct contains valid data
    public int valid;
}

internal partial struct WeberStats
{
    public long mb_wiener_variance;
    public long src_variance;
    public long rec_variance;
    public short src_pix_max;
    public short rec_pix_max;
    public long distortion;
    public long satd;
    public double max_scale;
}

internal partial struct FRAME_INDEX_SET
{
    public int show_frame_count;
}

/// <summary>Coding context (saved for post-encode frame drop / recode).</summary>
internal sealed partial class CODING_CONTEXT
{
    public loopfilter lf = new();
    public CdefInfo cdef_info = new();
    // [subset] copy_buffer (YV12_BUFFER_CONFIG): only used by the non-rt recode
    // loop (never referenced in the subset).
    public RATE_CONTROL rc = new();
    public MV_STATS mv_stats;
    // The frame number of the frame that this context belongs to.
    public uint frame_number;
    public FRAME_INDEX_SET frame_index_set;
}

internal sealed partial class FRAME_INFO
{
    public int frame_width;
    public int frame_height;
    public int mi_rows;
    public int mi_cols;
    public int mb_rows;
    public int mb_cols;
    public int num_mbs;
    public aom_bit_depth_t bit_depth;
    public int subsampling_x;
    public int subsampling_y;
}

/// <summary>Segmentation related information for the current frame.</summary>
internal sealed partial class EncSegmentationInfo
{
    /// 3-bit number containing the segment affiliation for each 4x4 block in the
    /// frame. map[y * stride + x] contains the segment id of the 4x4 block at
    /// (x,y) position.
    public Ptr<byte> map;
    /// Flag to indicate if current frame has lossless segments or not.
    /// 1: frame has at least one lossless segment.
    /// 0: frame has no lossless segments.
    public bool has_lossless_segment;
}

/// <summary>Frame time stamps.</summary>
internal sealed partial class TimeStamps
{
    /// Start time stamp of the previous frame
    public long prev_ts_start;
    /// End time stamp of the previous frame
    public long prev_ts_end;
    /// Start time stamp of the first frame
    public long first_ts_start;
}

/// <summary>Pointers to the memory allocated for frame level transform coeff related info.</summary>
internal sealed partial class CoeffBufferPool
{
    /// Pointer to the transformed coefficients buffer allocated for the frame.
    public Ptr<int> tcoeff;
    /// Pointer to the eobs buffer allocated for the frame.
    public Ptr<ushort> eobs;
    /// Pointer to the entropy_ctx buffer allocated for the frame.
    public Ptr<byte> entropy_ctx;
}

/// <summary>Structure to hold data corresponding to an encoded frame.</summary>
internal sealed partial class AV1_COMP_DATA
{
    /// Buffer to store packed bitstream data of a frame.
    public Ptr<byte> cx_data;

    /// Allocated size of the cx_data buffer.
    public int cx_data_sz;

    /// Size of data written in the cx_data buffer.
    public int frame_size;

    /// Flags for the frame.
    public uint lib_flags;

    /// Time stamp for start of frame.
    public long ts_frame_start;

    /// Time stamp for end of frame.
    public long ts_frame_end;

    /// Flag to indicate flush call.
    public int flush;

    /// Time base for sequence.
    public aom_rational64_t timestamp_ratio;

    /// Decide to pop the source for this frame from input buffer queue.
    public int pop_lookahead;
}

// Holds the time base of the encoded stream (alias for struct aom_rational).
// Class: AV1_COMP_DATA::timestamp_ratio points at the codec context's instance.
internal sealed partial class aom_rational64_t
{
    public long num; // fraction numerator
    public int den;  // fraction denominator
}

/// <summary>The stucture of RTC_REF.</summary>
internal sealed partial class RTC_REF
{
    /// LAST_FRAME (0), LAST2_FRAME(1), LAST3_FRAME(2), GOLDEN_FRAME(3),
    /// BWDREF_FRAME(4), ALTREF2_FRAME(5), ALTREF_FRAME(6).
    public readonly int[] reference = new int[INTER_REFS_PER_FRAME];
    public readonly int[] ref_idx = new int[INTER_REFS_PER_FRAME];
    public readonly int[] refresh = new int[REF_FRAMES];
    public int set_ref_frame_config;
    public int non_reference_frame;
    public readonly int[] ref_frame_comp = new int[3];
    public int gld_idx_1layer;
    /// Frame number of the last frame that refreshed the buffer slot.
    public readonly uint[] buffer_time_index = new uint[REF_FRAMES];
    /// Spatial layer id of the last frame that refreshed the buffer slot.
    public readonly byte[] buffer_spatial_layer = new byte[REF_FRAMES];
    /// Flag to indicate whether closest reference was the previous frame.
    public bool reference_was_previous_frame;
    /// Flag to indicate this frame is based on longer term reference only,
    /// for recovery from past loss, and it should be biased for improved coding.
    public bool bias_recovery_frame;
}

/// <summary>Top level primary encoder structure</summary>
internal sealed partial class AV1_PRIMARY
{
    /// Array of frame level encoder stage top level structures
    public readonly AV1_COMP[] parallel_cpi = new AV1_COMP[MAX_PARALLEL_FRAMES];

    /// Array of structures to hold data of frames encoded in a given parallel
    /// encode set.
    public readonly AV1_COMP_DATA[] parallel_frames_data = alloc_array<AV1_COMP_DATA>(MAX_PARALLEL_FRAMES - 1);

    // [subset] fpmt_unit_test_cfg, temp_frame_probs, temp_frame_probs_simulation,
    // temp_valid_gm_model_found: CONFIG_FPMT_TEST == 0.

    /// Copy of cm->ref_frame_map maintained to facilitate sequential update of
    /// ref_frame_map by lower layer depth frames encoded ahead of time in a
    /// parallel encode set.
    public readonly RefCntBuffer[] ref_frame_map_copy = new RefCntBuffer[REF_FRAMES];

    /// Start time stamp of the last encoded show frame
    public long ts_start_last_show_frame;

    /// End time stamp of the last encoded show frame
    public long ts_end_last_show_frame;

    /// Number of frame level contexts(cpis)
    public int num_fp_contexts;

    /// Loopfilter levels of the previous encoded frame.
    public readonly int[] filter_level = new int[2];

    /// Chrominance component loopfilter level of the previous encoded frame.
    public int filter_level_u;

    /// Chrominance component loopfilter level of the previous encoded frame.
    public int filter_level_v;

    /// Encode stage top level structure
    /// During frame parallel encode, this is the same as parallel_cpi[0]
    public AV1_COMP cpi;

    /// Lookahead (LAP) stage top level structure
    public AV1_COMP cpi_lap;

    /// Look-ahead context.
    public lookahead_ctx lookahead;

    /// Sequence parameters have been transmitted already and locked
    /// or not. Once locked av1_change_config cannot change the seq
    /// parameters. Note that for SVC encoding the sequence parameters
    /// (operating_points_cnt_minus_1, operating_point_idc[],
    /// has_nonzero_operating_point_idc) should be updated whenever the
    /// number of layers is changed. This is done in the
    /// ctrl_set_svc_params().
    public int seq_params_locked;

    /// Pointer to internal utility functions that manipulate aom_codec_* data
    /// structures.
    public aom_codec_pkt_list output_pkt_list;

    /// When set, indicates that internal ARFs are enabled.
    public int internal_altref_allowed;

    /// Tell if OVERLAY frame shows existing alt_ref frame.
    public int show_existing_alt_ref;

    /// Information related to a gf group.
    public GF_GROUP gf_group = new();

    // [subset] gf_state (GF_STATE): two-pass only.

    /// Flag indicating whether look ahead processing (LAP) is enabled.
    public int lap_enabled;

    /// Parameters for AV1 bitstream levels.
    public AV1LevelParams level_params = new();

    /// Calculates PSNR on each frame when set to 1.
    public int b_calculate_psnr;

    /// Number of frames left to be encoded, is 0 if limit is not set.
    public int frames_left;

    /// Information related to two pass encoding.
    public TWO_PASS twopass = new();

    /// Rate control related parameters.
    public PRIMARY_RATE_CONTROL p_rc = new();

    // [subset] tf_info (TEMPORAL_FILTER_INFO): temporal filtering is out of the
    // subset.

    /// Elements part of the sequence header, that are applicable for all the
    /// frames in the video.
    public SequenceHeader seq_params = new();

    /// Indicates whether to use SVC.
    public int use_svc;

    /// If true, buffer removal times are present.
    public bool buffer_removal_time_present;

    /// Number of temporal layers: may be > 1 for SVC (scalable vector coding).
    public uint number_temporal_layers;

    /// Number of spatial layers: may be > 1 for SVC (scalable vector coding).
    public uint number_spatial_layers;

    /// Code and details about current error status.
    public aom_internal_error_info error = new();

    /// Function pointers to variants of sse/sad/variance computation functions.
    /// fn_ptr[i] indicates the list of function pointers corresponding to block
    /// size i.
    public readonly aom_variance_fn_ptr_t[] fn_ptr = alloc_array<aom_variance_fn_ptr_t>(BLOCK_SIZES_ALL);

    /// tpl_sb_rdmult_scaling_factors[i] stores the RD multiplier scaling factor of
    /// the ith 16 x 16 block in raster scan order.
    public Ptr<double> tpl_sb_rdmult_scaling_factors;

    // [subset] tpl_data (TplParams): TPL is out of the subset. Its buffers are
    // never allocated, so the frees in av1_remove_primary_compressor are no-ops.

    // [subset] pme_data: CONFIG_PYRAMID_ME == 0.

    /// Motion vector stats of the previous encoded frame.
    public MV_STATS mv_stats;

    // [subset] internal stats: CONFIG_INTERNAL_STATS == 0.
    // [subset] aggregate_fc: CONFIG_ENTROPY_STATS == 0.

    /// For each type of reference frame, this contains the index of a reference
    /// frame buffer for a reference frame of the same type.  We use this to
    /// choose our primary reference frame (which is the most recent reference
    /// frame of the same type as the current frame).
    public readonly int[] fb_of_context_type = new int[REF_FRAMES];

    /// Primary Multi-threading parameters.
    public PrimaryMultiThreadInfo p_mt_info = new();

    /// Probabilities for pruning of various AV1 tools.
    public FrameProbInfo frame_probs = new();

    /// Indicates if a valid global motion model has been found in the different
    /// frame update types of a GF group.
    /// valid_gm_model_found[i] indicates if valid global motion model has been
    /// found in the frame update type with enum value equal to i
    public readonly int[] valid_gm_model_found = new int[FRAME_UPDATE_TYPES];

    /// Struct for the reference structure for RTC.
    public RTC_REF rtc_ref = new();

    /// Struct for all intra mode row multi threading in the preprocess stage
    /// when --deltaq-mode=3.
    public AV1EncRowMultiThreadSync intra_row_mt_sync = new();

    /// Flag indicating whether internal state should be frozen (no updates).
    public int b_freeze_internal_state;
}

/// <summary>Top level encoder structure.</summary>
internal sealed partial class AV1_COMP
{
    /// Pointer to top level primary encoder structure
    public AV1_PRIMARY ppi;

    /// Quantization and dequantization parameters for internal quantizer setup
    /// in the encoder.
    public EncQuantDequantParams enc_quant_dequant_params = new();

    /// Structure holding thread specific variables.
    public ThreadData td = new();

    /// Statistics collected at frame level.
    public FRAME_COUNTS counts = new();

    /// Holds buffer storing mode information at 4x4/8x8 level.
    public MBMIExtFrameBufferInfo mbmi_ext_info = new();

    /// Buffer holding the transform block related information.
    /// coeff_buffer_base[i] stores the transform block related information of the
    /// ith superblock in raster scan order.
    public CB_COEFF_BUFFER[] coeff_buffer_base;

    /// Stride of coeff_buffer_base (number of superblock columns).
    public int coeff_buffer_stride;

    /// Structure holding pointers to frame level memory allocated for transform
    /// block related information.
    public CoeffBufferPool coeff_buffer_pool = new();

    /// Structure holding variables common to encoder and decoder.
    public AV1_COMMON common = new();

    /// Encoder configuration related parameters.
    public AV1EncoderConfig oxcf = new();

    /// Stores the trellis optimization type at segment level.
    /// optimize_seg_arr[i] stores the trellis opt type for ith segment.
    public readonly TRELLIS_OPT_TYPE[] optimize_seg_arr = new TRELLIS_OPT_TYPE[MAX_SEGMENTS];

    /// Pointer to the frame buffer holding the source frame to be used during the
    /// current stage of encoding. It can be the raw input, temporally filtered
    /// input or scaled input.
    public YV12_BUFFER_CONFIG source;

    /// Pointer to the frame buffer holding the last raw source frame.
    /// last_source is NULL for the following cases:
    /// 1) First frame
    /// 2) Alt-ref frames
    /// 3) All frames for all-intra frame encoding.
    public YV12_BUFFER_CONFIG last_source;

    /// Pointer to the frame buffer holding the unscaled source frame.
    /// It can be either the raw input or temporally filtered input.
    public YV12_BUFFER_CONFIG unscaled_source;

    /// Frame buffer holding the resized source frame (cropping / superres).
    public YV12_BUFFER_CONFIG scaled_source = new();

    /// Pointer to the frame buffer holding the unscaled last source frame.
    public YV12_BUFFER_CONFIG unscaled_last_source;

    /// Frame buffer holding the resized last source frame.
    public YV12_BUFFER_CONFIG scaled_last_source = new();

    /// Pointer to the original source frame. This is used to determine if the
    /// content is screen.
    public YV12_BUFFER_CONFIG unfiltered_source;

    /// Frame buffer holding the orig source frame for PSNR calculation in rtc tf
    /// case.
    public YV12_BUFFER_CONFIG orig_source = new();

    /// Skip tpl setup when tpl data from gop length decision can be reused.
    public int skip_tpl_setup_stats;

    /// Scaling factors used in the RD multiplier modulation.
    /// TODO(sdeng): consider merge the following arrays.
    /// tpl_rdmult_scaling_factors is a temporary buffer used to store the
    /// intermediate scaling factors which are used in the calculation of
    /// tpl_sb_rdmult_scaling_factors. tpl_rdmult_scaling_factors[i] stores the
    /// intermediate scaling factor of the ith 16 x 16 block in raster scan order.
    public int cb_delta_rdmult_enabled;
    public Ptr<double> tpl_rdmult_scaling_factors;

    // [subset] tf_ctx (TemporalFilterCtx): temporal filtering is out of the
    // subset.

    /// Pointer to CDEF search context.
    public CdefSearchCtx cdef_search_ctx;

    /// Variables related to forcing integer mv decisions for the current frame.
    public ForceIntegerMVInfo force_intpel_info = new();

    /// Pointer to the buffer holding the scaled reference frames.
    /// scaled_ref_buf[i] holds the scaled reference frame of type i.
    public readonly RefCntBuffer[] scaled_ref_buf = new RefCntBuffer[INTER_REFS_PER_FRAME];

    /// Pointer to the buffer holding the last show frame.
    public RefCntBuffer last_show_frame_buf;

    /// Refresh frame flags for golden, bwd-ref and alt-ref frames.
    public RefreshFrameInfo refresh_frame;

    /// Flag to reduce the number of reference frame buffers used in rt.
    public int rt_reduce_num_ref_buffers;

    /// Flags signalled by the external interface at frame level.
    public ExternalFlags ext_flags = new();

    /// Temporary frame buffer used to store the non-loop filtered reconstructed
    /// frame during the search of loop filter level.
    public YV12_BUFFER_CONFIG last_frame_uf = new();

    /// Temporary frame buffer used to store the loop restored frame during loop
    /// restoration search.
    public YV12_BUFFER_CONFIG trial_frame_rst = new();

    /// Ambient reconstruction err target for force key frames.
    public long ambient_err;

    /// Parameters related to rate distortion optimization.
    public RD_OPT rd = new();

    /// Temporary coding context used to save and restore when encoding with and
    /// without super-resolution.
    public CODING_CONTEXT coding_context = new();

    /// Parameters related to global motion search.
    public GlobalMotionInfo gm_info = new();

    /// Parameters related to winner mode processing.
    public WinnerModeParams winner_mode_params = new();

    /// Frame time stamps.
    public TimeStamps time_stamps = new();

    /// Rate control related parameters.
    public RATE_CONTROL rc = new();

    /// Frame rate of the video.
    public double framerate;

    /// Bitmask indicating which reference buffers may be referenced by this frame.
    public int ref_frame_flags;

    /// speed is passed as a per-frame parameter into the encoder.
    public int speed;

    /// sf contains fine-grained config set internally based on speed.
    public SPEED_FEATURES sf = new();

    /// Parameters for motion vector search process.
    public MotionVectorSearchParams mv_search_params = new();

    /// When set, indicates that all reference frames are forward references,
    /// i.e., all the reference frames are output before the current frame.
    public int all_one_sided_refs;

    /// Segmentation related information for current frame.
    public EncSegmentationInfo enc_seg = new();

    /// Parameters related to cyclic refresh aq-mode.
    public CYCLIC_REFRESH cyclic_refresh;

    /// Parameters related to active map. Active maps indicate
    /// if there is any activity on a 4x4 block basis.
    public ActiveMap active_map = new();

    /// The frame processing order within a GOP.
    public byte gf_frame_index;

    // [subset] internal stats: CONFIG_INTERNAL_STATS == 0.
    // [subset] tx_search_count: CONFIG_SPEED_STATS == 0.

    /// When set, indicates that the frame is droppable, i.e., this frame
    /// does not update any reference buffers.
    public int droppable;

    /// Stores the frame parameters during encoder initialization.
    public FRAME_INFO frame_info = new();

    /// Stores different types of frame indices.
    public FRAME_INDEX_SET frame_index_set;

    /// Stores the cm->width in the last call of alloc_compressor_data(). Helps
    /// determine whether compressor data should be reallocated when cm->width
    /// changes.
    public int data_alloc_width;

    /// Stores the cm->height in the last call of alloc_compressor_data(). Helps
    /// determine whether compressor data should be reallocated when cm->height
    /// changes.
    public int data_alloc_height;

    /// Number of MBs in the full-size frame; to be used to
    /// normalize the firstpass stats. This will differ from the
    /// number of MBs in the current frame when the frame is
    /// scaled.
    public int initial_mbs;

    /// Flag to indicate whether the frame size inforamation has been
    /// setup and propagated to associated allocations.
    public bool frame_size_related_setup_done;

    /// The width of the frame that is lastly encoded.
    /// It is updated in the function "encoder_encode()".
    public int last_coded_width;

    /// The height of the frame that is lastly encoded.
    /// It is updated in the function "encoder_encode()".
    public int last_coded_height;

    /// Resize related parameters.
    public ResizePendingParams resize_pending_params;

    /// Pointer to struct holding adaptive data/contexts/models for the tile during
    /// encoding.
    public TileDataEnc[] tile_data;
    /// Number of tiles for which memory has been allocated for tile_data.
    public int allocated_tiles;

    /// Structure to store the palette token related information.
    public TokenInfo token_info = new();

    /// VARIANCE_AQ segment map refresh.
    public int vaq_refresh;

    /// Thresholds for variance based partitioning.
    public VarBasedPartitionInfo vbp_info = new();

    /// Number of recodes in the frame.
    public int num_frame_recode;

    /// Current frame probability of parallel frames, across recodes.
    public readonly FrameProbInfo[] frame_new_probs = alloc_array<FrameProbInfo>(NUM_RECODES_PER_FRAME);

    /// Retain condition for transform type frame_probability calculation
    public readonly int[] do_update_frame_probs_txtype = new int[NUM_RECODES_PER_FRAME];

    /// Retain condition for obmc frame_probability calculation
    public readonly int[] do_update_frame_probs_obmc = new int[NUM_RECODES_PER_FRAME];

    /// Retain condition for warped motion frame_probability calculation
    public readonly int[] do_update_frame_probs_warp = new int[NUM_RECODES_PER_FRAME];

    /// Retain condition for interpolation filter frame_probability calculation
    public readonly int[] do_update_frame_probs_interpfilter = new int[NUM_RECODES_PER_FRAME];

    // [subset] temp_framerate: CONFIG_FPMT_TEST == 0.

    /// Updated framerate for the current parallel frame.
    /// cpi->framerate is updated with new_framerate during
    /// post encode updates for parallel frames.
    public double new_framerate;

    /// Retain condition for fast_extra_bits calculation.
    public int do_update_vbr_bits_off_target_fast;

    /// Multi-threading parameters.
    public MultiThreadInfo mt_info = new();

    /// Specifies the frame to be output. It is valid only if show_existing_frame
    /// is 1. When show_existing_frame is 0, existing_fb_idx_to_show is set to
    /// INVALID_IDX.
    public int existing_fb_idx_to_show;

    /// A flag to indicate if intrabc is ever used in current frame.
    public int intrabc_used;

    /// Mark which ref frames can be skipped for encoding current frame during RDO.
    public int prune_ref_frame_mask;

    /// Loop Restoration context used during pick stage.
    // [subset] lr_ctxt (AV1LrStruct) and pick_lr_ctxt (AV1LrPickStruct): loop
    // restoration is out of the subset (their buffers are never allocated).

    /// Mask of reference frames that are kept as single references.
    public int keep_single_ref_frame_mask;

    /// Mask of reference frames that are kept as compound references.
    public int keep_comp_ref_frame_mask;

    // [subset] film_grain_table, denoise_and_model: film grain / noise model
    // (!CONFIG_REALTIME_ONLY) are out of the subset.

    /// Flags related to interpolation filter search.
    public InterpSearchFlags interp_search_flags = new();

    /// Turn on screen content tools flag.
    /// Note that some videos are not screen content videos, but
    /// screen content tools could also improve coding efficiency.
    /// For example, videos with large flat regions, gaming videos that look
    /// like natural videos.
    public int use_screen_content_tools;

    /// A flag to indicate "real" screen content videos.
    /// For example, screen shares, screen editing.
    /// This type is true indicates |use_screen_content_tools| must be true.
    /// In addition, rate control strategy is adjusted when this flag is true.
    public int is_screen_content_type;

    /// Count the number of blocks with high variance for screen content.
    public int sc_high_var_blocks;

    // [subset] partition_stats: CONFIG_COLLECT_PARTITION_STATS == 0.
    // [subset] component timing: CONFIG_COLLECT_COMPONENT_TIMING == 0.

    /// Number of frame headers written (or OBUs).
    public int frame_header_count;

    /// Whether any no-zero delta_q was actually used.
    public int deltaq_used;

    /// Refrence frame distance related variables.
    public RefFrameDistanceInfo ref_frame_dist_info = new();

    /// ssim_rdmult_scaling_factors[i] stores the RD multiplier scaling factor of
    /// the ith 16 x 16 block in raster scan order. This scaling factor is used for
    /// RD multiplier modulation when SSIM tuning is enabled.
    public Ptr<double> ssim_rdmult_scaling_factors;

    // [subset] vmaf_info, butteraugli_info: CONFIG_TUNE_* == 0.

    /// Parameters for scalable video coding.
    public SVC svc = new();

    /// Indicates whether current processing stage is encode stage or LAP stage.
    public COMPRESSOR_STAGE compressor_stage;

    /// Frame type of the last frame. May be used in some heuristics for speeding
    /// up the encoding.
    public FRAME_TYPE last_frame_type;

    /// Number of tile-groups.
    public int num_tg;

    /// Super-resolution mode currently being used by the encoder.
    /// This may / may not be same as user-supplied mode in oxcf->superres_mode
    /// (when we are recoding to try multiple options for example).
    public aom_superres_mode superres_mode;

    // [subset] firstpass_data (FirstPassData): first pass is out of the subset.

    /// Temporal Noise Estimate
    public NOISE_ESTIMATE noise_estimate = new();

    // [subset] denoiser: CONFIG_AV1_TEMPORAL_DENOISING == 0.

    /// Count on how many consecutive times a block uses small/zeromv for encoding
    /// in a scale of 8x8 block.
    public Ptr<byte> consec_zero_mv;

    /// Allocated memory size for |consec_zero_mv|.
    public int consec_zero_mv_alloc_size;

    /// Block size of first pass encoding
    public BLOCK_SIZE fp_block_size;

    // [subset] sb_counter: first pass only.

    /// Partition search controlled by the external partition model.
    public ExtPartController ext_part_controller;

    /// Motion vector stats of the current encoded frame, used to update the
    /// ppi->mv_stats during postencode.
    public MV_STATS mv_stats;

    /// Stores the reference refresh index for the current frame.
    public int ref_refresh_index;

    /// A flag to indicate if the reference refresh index is available for the
    /// current frame.
    public bool refresh_idx_available;

    /// Reference frame index corresponding to the frame to be excluded from being
    /// used as a reference by frame_parallel_level 2 frame in a parallel
    /// encode set of lower layer frames.
    public int ref_idx_to_skip;

    // [subset] wanted_fb: CONFIG_FPMT_TEST == 0.

    /// A flag to indicate frames that will update their data to the primary
    /// context at the end of the encode. It is set for non-parallel frames and the
    /// last frame in encode order in a given parallel encode set.
    public bool do_frame_data_update;

    // [subset] rd_command: CONFIG_RD_COMMAND == 0.

    /// Buffer to store MB variance after Wiener filter.
    public WeberStats[] mb_weber_stats;

    /// Buffer to store rate cost estimates for each macro block (8x8) in the
    /// preprocessing stage used in allintra mode.
    public int mb_weber_stats_alloc_size;

    // [subset] prep_rate_estimates, ext_rate_distribution, ext_rate_scale:
    // allintra rate-guided deltaq only.

    /// Block size used in Wiener variance computations.
    public BLOCK_SIZE weber_bsize;

    // [subset] norm_wiener_variance: allintra deltaq only.

    /// Buffer to store delta-q values for delta-q mode 4.
    public Ptr<int> mb_delta_q;

    /// Allocated memory size for |mb_delta_q|.
    public int mb_delta_q_alloc_size;

    /// Flag to indicate that current frame is dropped.
    public bool is_dropped_frame;

    // [subset] vbr_rc_info: CONFIG_BITRATE_ACCURACY == 0.
    // [subset] rc_log: CONFIG_RATECTRL_LOG == 0.
    // [subset] twopass_frame (TWO_PASS_FRAME): two-pass only.
    // [subset] third_pass_ctx: CONFIG_THREE_PASS == 0.
    // [subset] second_pass_log_stream (FILE *): two-pass logging only.

    /// Buffer to store 64x64 SAD
    public Ptr<ulong> src_sad_blk_64x64;

    /// Allocated memory size for |src_sad_blk_64x64|.
    public int src_sad_blk_alloc_size;

    /// SSE between the current frame and the reconstructed last frame
    /// It is only used for CBR mode.
    /// It is not used if the reference frame has a different frame size.
    public ulong rec_sse;

    /// A flag to indicate whether the encoder is controlled by DuckyEncode or not.
    /// 1:yes 0:no
    public int use_ducky_encode;

    // [subset] ducky_encode_info: !CONFIG_REALTIME_ONLY only.

    /// Frames since last frame with cdf update.
    public int frames_since_last_update;

    /// Block level thresholds to force zeromv-skip at partition level.
    public readonly uint[] zeromv_skip_thresh_exit_part = new uint[BLOCK_SIZES_ALL];

    /// Should we allocate a downsampling pyramid for each frame buffer?
    /// This is currently only used for global motion
    public bool alloc_pyramid;

    // [subset] saliency_map, sm_scaling_factor: CONFIG_SALIENCY_MAP == 0.

    /// Number of pixels that choose palette mode for luma in the
    /// fast encoding pass in av1_determine_sc_tools_with_encoding().
    public int palette_pixel_num;

    /// Flag to indicate scaled_last_source is available,
    /// so scaling is not needed for last_source.
    public int scaled_last_source_available;

    /// ROI map.
    public aom_roi_map_t roi = new();

    /// Extended rate control.
    public AOM_EXT_RATECTRL ext_ratectrl;

    // [subset] extrc_tpl_gop_stats (AomTplGopStats): external rate control / TPL
    // only.

    /// Whether to pad the frame border with the true frame dimensions.
    public bool do_border_pad;
}

/// <summary>Input frames and last input frame</summary>
internal sealed partial class EncodeFrameInput
{
    /*!\cond */
    public YV12_BUFFER_CONFIG source;
    public YV12_BUFFER_CONFIG last_source;
    public long ts_duration;
    /*!\endcond */
}

/// <summary>contains per-frame encoding parameters decided upon by
/// av1_encode_strategy() and passed down to av1_encode().</summary>
internal sealed partial class EncodeFrameParams
{
    /// Is error resilient mode enabled
    public int error_resilient_mode;
    /// Frame type (eg KF vs inter frame etc)
    public FRAME_TYPE frame_type;

    /*!\cond */
    public int primary_ref_frame;
    public int order_offset;

    /*!\endcond */
    /// Should the current frame be displayed after being decoded
    public int show_frame;

    /*!\cond */
    public int refresh_frame_flags;

    public int show_existing_frame;
    public int existing_fb_idx_to_show;

    /*!\endcond */
    /// Bitmask of which reference buffers may be referenced by this frame.
    public int ref_frame_flags;

    /// Reference buffer assignment for this frame.
    public readonly int[] remapped_ref_idx = new int[REF_FRAMES];

    /// Flags which determine which reference buffers are refreshed by this
    /// frame.
    public RefreshFrameInfo refresh_frame;

    /// Speed level to use for this frame: Bigger number means faster.
    public int speed;
}

internal partial struct RefFrameMapPair
{
    // Pyramid level of the reference frame
    public int pyr_level;
    // Display order of the reference frame
    public int disp_order;
}
