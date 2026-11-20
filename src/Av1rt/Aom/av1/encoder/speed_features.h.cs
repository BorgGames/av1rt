// Port of libaom av1/encoder/speed_features.h
global using GM_SEARCH_TYPE = int;
global using DIST_WTD_COMP_FLAG = int;
global using DEV_SPEED_FEATURES = int;
global using RECODE_LOOP_TYPE = int;
global using SUBPEL_SEARCH_METHOD = int;
global using LPF_PICK_METHOD = int;
global using CDEF_PICK_METHOD = int;
global using MODE_SEARCH_SKIP_LOGIC = int;
global using TX_TYPE_PRUNE_MODE = int;
global using OVERSHOOT_DETECTION_CBR = int;
global using MULTI_WINNER_MODE_TYPE = int;
global using PRUNE_NEARMV_LEVEL = int;
global using TX_SEARCH_CASE = int;
global using PARTITION_SEARCH_TYPE = int;
global using MAX_PART_PRED_MODE = int;
global using MV_PREC_LOGIC = int;
global using SUPERRES_AUTO_SEARCH_TYPE = int;
global using INTERNAL_COST_UPDATE_TYPE = int;
global using SIMPLE_MOTION_SEARCH_PRUNE_LEVEL = int;
global using PRUNE_MESH_SEARCH_LEVEL = int;
global using INTER_SEARCH_EARLY_TERM_IDX = int;
global using static Av1rt.Aom.speed_features;

namespace Av1rt.Aom;

internal static partial class speed_features
{
    internal const int MAX_MESH_STEP = 4;

    // enum GM_SEARCH_TYPE
    internal const int GM_FULL_SEARCH = 0;
    internal const int GM_REDUCED_REF_SEARCH_SKIP_L2_L3 = 1;
    internal const int GM_REDUCED_REF_SEARCH_SKIP_L2_L3_ARF2 = 2;
    // Same as GM_REDUCED_REF_SEARCH_SKIP_L2_L3_ARF2 but with extra filtering
    // to keep at most two ref frames
    internal const int GM_SEARCH_CLOSEST_REFS_ONLY = 3;
    internal const int GM_DISABLE_SEARCH = 4;
    // enum DIST_WTD_COMP_FLAG
    internal const int DIST_WTD_COMP_ENABLED = 0;
    internal const int DIST_WTD_COMP_SKIP_MV_SEARCH = 1;
    internal const int DIST_WTD_COMP_DISABLED = 2;
    // anonymous enum
    internal const int INTRA_ALL = (1 << DC_PRED) | (1 << V_PRED) | (1 << H_PRED) | (1 << D45_PRED) | (1 << D135_PRED) | (1 << D113_PRED) | (1 << D157_PRED) | (1 << D203_PRED) | (1 << D67_PRED) | (1 << SMOOTH_PRED) | (1 << SMOOTH_V_PRED) | (1 << SMOOTH_H_PRED) | (1 << PAETH_PRED);
    internal const int UV_INTRA_ALL = (1 << UV_DC_PRED) | (1 << UV_V_PRED) | (1 << UV_H_PRED) | (1 << UV_D45_PRED) | (1 << UV_D135_PRED) | (1 << UV_D113_PRED) | (1 << UV_D157_PRED) | (1 << UV_D203_PRED) | (1 << UV_D67_PRED) | (1 << UV_SMOOTH_PRED) | (1 << UV_SMOOTH_V_PRED) | (1 << UV_SMOOTH_H_PRED) | (1 << UV_PAETH_PRED) | (1 << UV_CFL_PRED);
    internal const int UV_INTRA_DC = (1 << UV_DC_PRED);
    internal const int UV_INTRA_DC_CFL = (1 << UV_DC_PRED) | (1 << UV_CFL_PRED);
    internal const int UV_INTRA_DC_TM = (1 << UV_DC_PRED) | (1 << UV_PAETH_PRED);
    internal const int UV_INTRA_DC_PAETH_CFL = (1 << UV_DC_PRED) | (1 << UV_PAETH_PRED) | (1 << UV_CFL_PRED);
    internal const int UV_INTRA_DC_H_V = (1 << UV_DC_PRED) | (1 << UV_V_PRED) | (1 << UV_H_PRED);
    internal const int UV_INTRA_DC_H_V_CFL = (1 << UV_DC_PRED) | (1 << UV_V_PRED) | (1 << UV_H_PRED) | (1 << UV_CFL_PRED);
    internal const int UV_INTRA_DC_PAETH_H_V = (1 << UV_DC_PRED) | (1 << UV_PAETH_PRED) | (1 << UV_V_PRED) | (1 << UV_H_PRED);
    internal const int UV_INTRA_DC_PAETH_H_V_CFL = (1 << UV_DC_PRED) | (1 << UV_PAETH_PRED) | (1 << UV_V_PRED) | (1 << UV_H_PRED) | (1 << UV_CFL_PRED);
    internal const int INTRA_DC = (1 << DC_PRED);
    internal const int INTRA_DC_TM = (1 << DC_PRED) | (1 << PAETH_PRED);
    internal const int INTRA_DC_H_V = (1 << DC_PRED) | (1 << V_PRED) | (1 << H_PRED);
    internal const int INTRA_DC_H_V_SMOOTH = (1 << DC_PRED) | (1 << V_PRED) | (1 << H_PRED) | (1 << SMOOTH_PRED);
    internal const int INTRA_DC_PAETH_H_V = (1 << DC_PRED) | (1 << PAETH_PRED) | (1 << V_PRED) | (1 << H_PRED);
    // anonymous enum
    internal const int INTER_ALL = (1 << NEARESTMV) | (1 << NEARMV) | (1 << GLOBALMV) | (1 << NEWMV) | (1 << NEAREST_NEARESTMV) | (1 << NEAR_NEARMV) | (1 << NEW_NEWMV) | (1 << NEAREST_NEWMV) | (1 << NEAR_NEWMV) | (1 << NEW_NEARMV) | (1 << NEW_NEARESTMV) | (1 << GLOBAL_GLOBALMV);
    internal const int INTER_NEAREST_NEAR_ZERO = (1 << NEARESTMV) | (1 << NEARMV) | (1 << GLOBALMV) | (1 << NEAREST_NEARESTMV) | (1 << GLOBAL_GLOBALMV) | (1 << NEAREST_NEWMV) | (1 << NEW_NEARESTMV) | (1 << NEW_NEARMV) | (1 << NEAR_NEWMV) | (1 << NEAR_NEARMV);
    internal const int INTER_SINGLE_ALL = (1 << NEARESTMV) | (1 << NEARMV) | (1 << GLOBALMV) | (1 << NEWMV);
    // anonymous enum
    internal const int DISABLE_ALL_INTER_SPLIT = (1 << THR_COMP_GA) | (1 << THR_COMP_LA) | (1 << THR_ALTR) | (1 << THR_GOLD) | (1 << THR_LAST);
    internal const int DISABLE_ALL_SPLIT = (1 << THR_INTRA) | DISABLE_ALL_INTER_SPLIT;
    internal const int DISABLE_COMPOUND_SPLIT = (1 << THR_COMP_GA) | (1 << THR_COMP_LA);
    internal const int LAST_AND_INTRA_SPLIT_ONLY = (1 << THR_COMP_GA) | (1 << THR_COMP_LA) | (1 << THR_ALTR) | (1 << THR_GOLD);
    // enum DEV_SPEED_FEATURES
    internal const int TXFM_CODING_SF = 1;
    internal const int INTER_PRED_SF = 2;
    internal const int INTRA_PRED_SF = 4;
    internal const int PARTITION_SF = 8;
    internal const int LOOP_FILTER_SF = 16;
    internal const int RD_SKIP_SF = 32;
    internal const int RESERVE_2_SF = 64;
    internal const int RESERVE_3_SF = 128;
    // enum RECODE_LOOP_TYPE
    // No recodes allowed
    internal const int DISALLOW_RECODE = 0;
    // Allow recode only for KF/ARF/GF frames
    internal const int ALLOW_RECODE_KFARFGF = 1;
    // Allow recode for all frame types based on bitrate constraints.
    internal const int ALLOW_RECODE = 2;
    // enum SUBPEL_SEARCH_METHOD
    internal const int SUBPEL_TREE = 0;
    internal const int SUBPEL_TREE_PRUNED = 1; // Prunes 1/2-pel searches
    internal const int SUBPEL_TREE_PRUNED_MORE = 2; // Prunes 1/2-pel searches more aggressively
    internal const int SUBPEL_SEARCH_METHODS = 3;
    // enum LPF_PICK_METHOD
    // Try the full image with different values.
    internal const int LPF_PICK_FROM_FULL_IMAGE = 0;
    // Try the full image filter search with non-dual filter only.
    internal const int LPF_PICK_FROM_FULL_IMAGE_NON_DUAL = 1;
    // Try a small portion of the image with different values.
    internal const int LPF_PICK_FROM_SUBIMAGE = 2;
    // Estimate the level based on quantizer and frame type
    internal const int LPF_PICK_FROM_Q = 3;
    // Pick 0 to disable LPF if LPF was enabled last frame
    internal const int LPF_PICK_MINIMAL_LPF = 4;
    // enum CDEF_PICK_METHOD
    internal const int CDEF_FULL_SEARCH = 0; // < Full search
    internal const int CDEF_FAST_SEARCH_LVL1 = 1; // < Search among a subset of all possible filters.
    internal const int CDEF_FAST_SEARCH_LVL2 = 2; // < Search reduced subset of filters than Level 1.
    internal const int CDEF_FAST_SEARCH_LVL3 = 3; // < Search reduced subset of secondary filters than // Level 2.
    internal const int CDEF_FAST_SEARCH_LVL4 = 4; // < Search reduced subset of filters than Level 3.
    internal const int CDEF_FAST_SEARCH_LVL5 = 5; // < Search reduced subset of filters than Level 4.
    internal const int CDEF_PICK_FROM_Q = 6; // < Estimate filter strength based on quantizer.
    internal const int CDEF_PICK_METHODS = 7;
    // enum MODE_SEARCH_SKIP_LOGIC
    // Terminate search early based on distortion so far compared to
    // qp step, distortion in the neighborhood of the frame, etc.
    internal const int FLAG_EARLY_TERMINATE = 1 << 0;
    // Skips comp inter modes if the best so far is an intra mode.
    internal const int FLAG_SKIP_COMP_BESTINTRA = 1 << 1;
    // Skips oblique intra modes if the best so far is an inter mode.
    internal const int FLAG_SKIP_INTRA_BESTINTER = 1 << 3;
    // Skips oblique intra modes  at angles 27, 63, 117, 153 if the best
    // intra so far is not one of the neighboring directions.
    internal const int FLAG_SKIP_INTRA_DIRMISMATCH = 1 << 4;
    // Skips intra modes other than DC_PRED if the source variance is small
    internal const int FLAG_SKIP_INTRA_LOWVAR = 1 << 5;
    // enum TX_TYPE_PRUNE_MODE
    // No tx type pruning
    internal const int TX_TYPE_PRUNE_0 = 0;
    // adaptively prunes the least perspective tx types out of all 16
    // (tuned to provide negligible quality loss)
    internal const int TX_TYPE_PRUNE_1 = 1;
    // similar, but applies much more aggressive pruning to get better speed-up
    internal const int TX_TYPE_PRUNE_2 = 2;
    internal const int TX_TYPE_PRUNE_3 = 3;
    // More aggressive pruning based on tx type score and allowed tx count
    internal const int TX_TYPE_PRUNE_4 = 4;
    internal const int TX_TYPE_PRUNE_5 = 5;
    // enum OVERSHOOT_DETECTION_CBR
    // No reaction to rate control on a detected slide/scene change.
    internal const int NO_DETECTION = 0;
    // Set to larger Q based only on the detected slide/scene change and
    // current/past Q.
    internal const int FAST_DETECTION_MAXQ = 1;
    // enum MULTI_WINNER_MODE_TYPE
    // Turns off multi-winner mode. So we will do txfm search on either all modes
    // if winner mode is off, or we will only on txfm search on a single winner
    // mode.
    internal const int MULTI_WINNER_MODE_OFF = 0;
    // Limits the number of winner modes to at most 2
    internal const int MULTI_WINNER_MODE_FAST = 1;
    // Uses the default number of winner modes, which is 3 for intra mode, and 1
    // for inter mode.
    internal const int MULTI_WINNER_MODE_DEFAULT = 2;
    // Maximum number of winner modes allowed.
    internal const int MULTI_WINNER_MODE_LEVELS = 3;
    // enum PRUNE_NEARMV_LEVEL
    internal const int PRUNE_NEARMV_OFF = 0; // Turn off nearmv pruning
    internal const int PRUNE_NEARMV_LEVEL1 = 1; // Prune nearmv for qindex (0-85)
    internal const int PRUNE_NEARMV_LEVEL2 = 2; // Prune nearmv for qindex (0-170)
    internal const int PRUNE_NEARMV_LEVEL3 = 3; // Prune nearmv more aggressively for qindex (0-170)
    internal const int PRUNE_NEARMV_MAX = PRUNE_NEARMV_LEVEL3;
    // enum TX_SEARCH_CASE
    // Default transform search used in evaluation of best inter candidates
    // (MODE_EVAL stage) and motion mode winner processing (WINNER_MODE_EVAL
    // stage).
    internal const int TX_SEARCH_DEFAULT = 0;
    // Transform search in motion mode rd during MODE_EVAL stage.
    internal const int TX_SEARCH_MOTION_MODE = 1;
    // Transform search in compound type mode rd during MODE_EVAL stage.
    internal const int TX_SEARCH_COMP_TYPE_MODE = 2;
    // All transform search cases
    internal const int TX_SEARCH_CASES = 3;
    // enum PARTITION_SEARCH_TYPE
    // Search partitions using RD criterion
    internal const int SEARCH_PARTITION = 0;
    // Always use a fixed size partition
    internal const int FIXED_PARTITION = 1;
    // Partition using source variance
    internal const int VAR_BASED_PARTITION = 2;
    // [subset] #if CONFIG_RT_ML_PARTITIONING is false here.
    // enum MAX_PART_PRED_MODE
    internal const int NOT_IN_USE = 0;
    internal const int DIRECT_PRED = 1;
    internal const int RELAXED_PRED = 2;
    internal const int ADAPT_PRED = 3;
    // enum MV_PREC_LOGIC
    internal const int LAST_MV_DATA = 0;
    internal const int CURRENT_Q = 1;
    internal const int QTR_ONLY = 2;
    // enum SUPERRES_AUTO_SEARCH_TYPE
    internal const int SUPERRES_AUTO_ALL = 0; // Tries all possible superres ratios
    internal const int SUPERRES_AUTO_DUAL = 1; // Tries no superres and q-based superres ratios
    internal const int SUPERRES_AUTO_SOLO = 2; // Only apply the q-based superres ratio
    // enum INTERNAL_COST_UPDATE_TYPE
    internal const int INTERNAL_COST_UPD_OFF = 0; // Turn off cost updates.
    internal const int INTERNAL_COST_UPD_TILE = 1; // Update every tile.
    internal const int INTERNAL_COST_UPD_SBROW_SET = 2; // Update every row_set of height 256 pixs.
    internal const int INTERNAL_COST_UPD_SBROW = 3; // Update every sb rows inside a tile.
    internal const int INTERNAL_COST_UPD_SB = 4; // Update every sb.
    // enum SIMPLE_MOTION_SEARCH_PRUNE_LEVEL
    internal const int NO_PRUNING = -1;
    internal const int SIMPLE_AGG_LVL0 = 0; // Simple prune aggressiveness level 0. speed = 0
    internal const int SIMPLE_AGG_LVL1 = 1; // Simple prune aggressiveness level 1. speed = 1
    internal const int SIMPLE_AGG_LVL2 = 2; // Simple prune aggressiveness level 2. speed = 2
    internal const int SIMPLE_AGG_LVL3 = 3; // Simple prune aggressiveness level 3. speed >= 3
    internal const int SIMPLE_AGG_LVL4 = 4; // Simple prune aggressiveness level 4. speed >= 4
    internal const int SIMPLE_AGG_LVL5 = 5; // Simple prune aggressiveness level 5. speed >= 5
    internal const int QIDX_BASED_AGG_LVL1 = 6; // Qindex based prune aggressiveness level, aggressive // level maps to simple agg level 1 or 2 based on qindex.
    internal const int TOTAL_SIMPLE_AGG_LVLS = QIDX_BASED_AGG_LVL1; // Total number of simple prune // aggressiveness levels.
    internal const int TOTAL_QINDEX_BASED_AGG_LVLS = QIDX_BASED_AGG_LVL1 - SIMPLE_AGG_LVL5;
    // Total number of qindex based simple prune
    // aggressiveness levels.
    internal const int TOTAL_AGG_LVLS = TOTAL_SIMPLE_AGG_LVLS + TOTAL_QINDEX_BASED_AGG_LVLS;
    // Total number of levels.
    // enum PRUNE_MESH_SEARCH_LEVEL
    internal const int PRUNE_MESH_SEARCH_DISABLED = 0; // Prune mesh search level 0.
    internal const int PRUNE_MESH_SEARCH_LVL_1 = 1; // Prune mesh search level 1.
    internal const int PRUNE_MESH_SEARCH_LVL_2 = 2; // Prune mesh search level 2.
    // enum INTER_SEARCH_EARLY_TERM_IDX
    internal const int EARLY_TERM_DISABLED = 0;
    // Early terminate inter mode search based on sse disabled.
    internal const int EARLY_TERM_IDX_1 = 1;
    // Early terminate inter mode search based on sse, index 1.
    internal const int EARLY_TERM_IDX_2 = 2;
    // Early terminate inter mode search based on sse, index 2.
    internal const int EARLY_TERM_IDX_3 = 3;
    // Early terminate inter mode search based on sse, index 3.
    internal const int EARLY_TERM_IDX_4 = 4;
    // Early terminate inter mode search based on sse, index 4.
    internal const int EARLY_TERM_INDICES = 5; // Total number of early terminate indices
}

internal partial struct MESH_PATTERN
{
    internal int range;
    internal int interval;
}

// All speed feature structs are classes embedded in SPEED_FEATURES (cpi->sf), used through
// pointers and never copied by value in the subset, so they have no CopyFrom.
// [subset] PYRAMID_ME_SPEED_FEATURES: CONFIG_PYRAMID_ME is 0.

internal sealed partial class TX_TYPE_SEARCH
{
    internal TX_TYPE_PRUNE_MODE prune_2d_txfm_mode;

    // Limit the intra transform search type.
    // 1 : Limit the intra transform search type to the ones in the table
    // av1_derived_intra_tx_used_flag[INTRA_MODES].
    // 2 : Limit the intra transform search type to the default transform.
    internal int fast_intra_tx_type_search;

    // INT_MAX: Disable fast search.
    // 1 - 1024: Probability threshold used for conditionally forcing tx type,
    // during mode search.
    // 0: Force tx type to be DCT_DCT unconditionally, during
    // mode search.
    internal int fast_inter_tx_type_prob_thresh;

    // Prune less likely chosen transforms for each intra mode. The speed
    // feature ranges from 0 to 2, for different speed / compression trade offs.
    internal int use_reduced_intra_txset;

    // Use a skip flag prediction model to detect blocks with skip = 1 early
    // and avoid doing full TX type search for such blocks.
    internal int use_skip_flag_prediction;

    // Threshold used by the ML based method to predict TX block split decisions.
    internal int ml_tx_split_thresh;

    // skip remaining transform type search when we found the rdcost of skip is
    // better than applying transform
    internal int skip_tx_search;

    // Prune tx type search using previous frame stats.
    internal int prune_tx_type_using_stats;
    // Prune tx type search using estimated RDcost
    internal int prune_tx_type_est_rd;

    // Flag used to control the winner mode processing for tx type pruning for
    // inter blocks. It enables further tx type mode pruning based on ML model for
    // mode evaluation and disables tx type mode pruning for winner mode
    // processing.
    internal int winner_mode_tx_type_pruning;
}

internal sealed partial class HIGH_LEVEL_SPEED_FEATURES
{
    // Frame level coding parameter update.
    internal int frame_parameter_update;

    // Cases and frame types for which the recode loop is enabled.
    internal RECODE_LOOP_TYPE recode_loop;

    // Controls the tolerance vs target rate used in deciding whether to
    // recode a frame. It has no meaning if recode is disabled.
    internal int recode_tolerance;

    // Determine how motion vector precision is chosen. The possibilities are:
    // LAST_MV_DATA: use the mv data from the last coded frame
    // CURRENT_Q: use the current q as a threshold
    // QTR_ONLY: use quarter pel precision only.
    internal MV_PREC_LOGIC high_precision_mv_usage;

    // Always set to 0. If on it enables 0 cost background transmission
    // (except for the initial transmission of the segmentation). The feature is
    // disabled because the addition of very large block sizes make the
    // backgrounds very to cheap to encode, and the segmentation we have
    // adds overhead.
    internal int static_segmentation;

    // Superres-auto mode search type:
    internal SUPERRES_AUTO_SEARCH_TYPE superres_auto_search_type;

    // Enable/disable extra screen content test by encoding key frame twice.
    internal int disable_extra_sc_testing;

    // Enable/disable second_alt_ref temporal filtering.
    internal int second_alt_ref_filtering;

    // The number of frames to be used during temporal filtering of an ARF frame
    // is adjusted based on noise level of the current frame. The sf has three
    // levels to decide number of frames to be considered for filtering:
    // 0       : Use default number of frames
    // 1 and 2 : Reduce the number of frames based on noise level with varied
    // aggressiveness
    internal int adjust_num_frames_for_arf_filtering;

    // Decide the bit estimation approach used in qindex decision.
    // 0: estimate bits based on a constant value;
    // 1: estimate bits more accurately based on the frame complexity.
    internal int accurate_bit_estimate;

    // Decide the approach for weight calculation during temporal filtering.
    // 0: Calculate weight using exp()
    // 1: Calculate weight using a lookup table that approximates exp().
    internal int weight_calc_level_in_tf;

    // Decide whether to perform motion estimation at split block (i.e. 16x16)
    // level or not.
    // 0: Always allow motion estimation.
    // 1: Conditionally allow motion estimation based on 4x4 sub-blocks variance.
    internal int allow_sub_blk_me_in_tf;

    // Decide whether to disable temporal mv prediction.
    // 0: Do not disable
    // 1: Conditionally disable
    // 2: Always disable
    internal int ref_frame_mvs_lvl;

    // Decide whether to enable screen detection mode 2 fast detection.
    // 0: Regular detection
    // 1: Fast detection
    internal int screen_detection_mode2_fast_detection;

    // Decide whether to enable weighted chroma distortion.
    // 0: Disable
    // 1: Enable
    internal int weighted_chroma_distortion;
}

internal sealed partial class FIRST_PASS_SPEED_FEATURES
{
    // \brief Reduces the mv search window.
    // By default, the initial search window is around
    // MIN(MIN(dims), MAX_FULL_PEL_VAL) = MIN(MIN(dims), 1023).
    // Each step reduction decrease the window size by about a factor of 2.
    internal int reduce_mv_step_param;

    // \brief Skips the motion search when the zero mv has small sse.
    internal int skip_motion_search_threshold;

    // \brief Skips reconstruction by using source buffers for prediction
    internal int disable_recon;

    // \brief Skips the motion search centered on 0,0 mv.
    internal int skip_zeromv_motion_search;
}

internal sealed partial class TPL_SPEED_FEATURES
{
    // GOP length adaptive decision.
    // If set to 0, tpl stats of ARFs from base layer, (base+1) layer and
    // (base+2) layer decide whether a shorter gf interval is better.
    // If set to 1, tpl stats of ARFs from base layer, (base+1) layer and GF boost
    // decide whether a shorter gf interval is better.
    // If set to 2, gop length adaptive decision is disabled.
    internal int gop_length_decision_method;
    // Prune the intra modes search by tpl.
    // If set to 0, we will search all intra modes from DC_PRED to PAETH_PRED.
    // If set to 1, we only search DC_PRED, V_PRED, and H_PRED.
    internal int prune_intra_modes;
    // This parameter controls which step in the n-step process we start at.
    internal int reduce_first_step_size;
    // Skip motion estimation based on the precision of center MVs and the
    // difference between center MVs.
    // If set to 0, motion estimation is skipped for duplicate center MVs
    // (default). If set to 1, motion estimation is skipped for duplicate
    // full-pixel center MVs. If set to 2, motion estimation is skipped if the
    // difference between center MVs is less than the threshold.
    internal int skip_alike_starting_mv;

    // When to stop subpel search.
    internal SUBPEL_FORCE_STOP subpel_force_stop;

    // Which search method to use.
    internal SEARCH_METHODS search_method;

    // Prune starting mvs in TPL based on sad scores.
    internal int prune_starting_mv;

    // Prune reference frames in TPL.
    internal int prune_ref_frames_in_tpl;

    // Support compound predictions.
    internal int allow_compound_pred;

    // Calculate rate and distortion based on Y plane only.
    internal int use_y_only_rate_distortion;

    // Use SAD instead of SATD during intra/inter mode search.
    // If set to 0, use SATD always.
    // If set to 1, use SAD during intra/inter mode search for frames in the
    // higher temporal layers of the hierarchical prediction structure.
    // If set to 2, use SAD during intra/inter mode search for all frames.
    // This sf is disabled for the first GF group of the key-frame interval,
    // i.e., SATD is used during intra/inter mode search of the first GF group.
    internal int use_sad_for_mode_decision;

    // Skip tpl processing for frames of type LF_UPDATE.
    // This sf is disabled for the first GF group of the key-frame interval.
    internal int reduce_num_frames;
}

internal sealed partial class GLOBAL_MOTION_SPEED_FEATURES
{
    internal GM_SEARCH_TYPE gm_search_type;

    // During global motion estimation, prune remaining reference frames in a
    // given direction(past/future), if the evaluated ref_frame in that direction
    // yields gm_type as INVALID/TRANSLATION/IDENTITY
    internal int prune_ref_frame_for_gm_search;

    // When the current GM type is set to ZEROMV, prune ZEROMV if its performance
    // is worse than NEWMV under SSE metric.
    // 0 : no pruning
    // 1 : conservative pruning
    // 2 : aggressive pruning
    internal int prune_zero_mv_with_sse;

    // Disable global motion estimation based on stats of previous frames in the
    // GF group
    internal int disable_gm_search_based_on_stats;

    // Downsampling pyramid level to use for global motion estimation
    internal int downsample_level;

    // Number of refinement steps to apply after initial model generation
    internal int num_refinement_steps;

    // Error advantage threshold level used to determine whether global motion
    // compensation should be enabled. It Can take values 0 - 2 increasing
    // aggressiveness of skipping GM in order.
    internal int gm_erroradv_tr_level;
}

internal sealed partial class PARTITION_SPEED_FEATURES
{
    internal PARTITION_SEARCH_TYPE partition_search_type;

    // Used if partition_search_type = FIXED_PARTITION
    internal BLOCK_SIZE fixed_partition_size;

    // Prune extended partition types search based on the current best partition
    // and the combined rdcost of the subblocks estimated from previous
    // partitions. Can take values 0 - 2, 0 referring to no pruning, and 1 - 2
    // increasing aggressiveness of pruning in order.
    internal int prune_ext_partition_types_search_level;

    // Prune part4 based on block size
    internal int prune_part4_search;

    // Use a ML model to prune rectangular, ab and 4-way horz
    // and vert partitions
    internal int ml_prune_partition;

    // Use a ML model to adaptively terminate partition search after trying
    // PARTITION_SPLIT. Can take values 0 - 2, 0 meaning not being enabled, and
    // 1 - 2 increasing aggressiveness in order.
    internal int ml_early_term_after_part_split_level;

    // Skip rectangular partition test when partition type none gives better
    // rd than partition type split. Can take values 0 - 2, 0 referring to no
    // skipping, and 1 - 2 increasing aggressiveness of skipping in order.
    internal int less_rectangular_check_level;

    // Use square partition only beyond this block size.
    internal BLOCK_SIZE use_square_partition_only_threshold;

    // Sets max square partition levels for this superblock based on
    // motion vector and prediction error distribution produced from 16x16
    // simple motion search
    internal MAX_PART_PRED_MODE auto_max_partition_based_on_simple_motion;

    // Min and max square partition size we enable (block_size) as per auto
    // min max, but also used by adjust partitioning, and pick_partitioning.
    internal BLOCK_SIZE default_min_partition_size;
    internal BLOCK_SIZE default_max_partition_size;

    // Sets level of adjustment of variance-based partitioning during
    // rd_use_partition 0 - no partition adjustment, 1 - try to merge partitions
    // for small blocks and high QP, 2 - try to merge partitions, 3 - try to merge
    // and split leaf partitions and 0 - 3 decreasing aggressiveness in order.
    internal int adjust_var_based_rd_partitioning;

    // Partition search early breakout thresholds.
    internal long partition_search_breakout_dist_thr;
    internal int partition_search_breakout_rate_thr;

    // Thresholds for ML based partition search breakout.
    internal readonly float[] ml_partition_search_breakout_thresh = new float[PARTITION_BLOCK_SIZES];

    // ML based partition search breakout model index
    internal int ml_partition_search_breakout_model_index;

    // ML based partition search breakout model index
    internal int ml_4_partition_search_level_index;

    // Aggressiveness levels for pruning split and rectangular partitions based on
    // simple_motion_search. SIMPLE_AGG_LVL0 to SIMPLE_AGG_LVL5 correspond to
    // simple motion search based pruning. QIDX_BASED_AGG_LVL1 corresponds to
    // qindex based and simple motion search based pruning.
    internal int simple_motion_search_prune_agg;

    // Perform simple_motion_search on each possible subblock and use it to prune
    // PARTITION_HORZ and PARTITION_VERT.
    internal int simple_motion_search_prune_rect;

    // Perform simple motion search before none_partition to decide if we
    // want to remove all partitions other than PARTITION_SPLIT. If set to 0, this
    // model is disabled. If set to 1, the model attempts to perform
    // PARTITION_SPLIT only. If set to 2, the model also attempts to prune
    // PARTITION_SPLIT.
    internal int simple_motion_search_split;

    // Use features from simple_motion_search to terminate prediction block
    // partition after PARTITION_NONE
    internal int simple_motion_search_early_term_none;

    // Controls whether to reduce the number of motion search steps. If this is 0,
    // then simple_motion_search has the same number of steps as
    // single_motion_search (assuming no other speed features). Otherwise, reduce
    // the number of steps by the value contained in this variable.
    internal int simple_motion_search_reduce_search_steps;

    // This variable controls the maximum block size where intra blocks can be
    // used in inter frames.
    // TODO(aconverse): Fold this into one of the other many mode skips
    internal BLOCK_SIZE max_intra_bsize;

    // Use CNN with luma pixels on source frame on each of the 64x64 subblock to
    // perform partition pruning in intra frames.
    // 0: No Pruning
    // 1: Prune split and rectangular partitions only
    // 2: Prune none, split and rectangular partitions
    internal int intra_cnn_based_part_prune_level;

    // Disable extended partition search if the current bsize is greater than the
    // threshold. Must be a square block size BLOCK_8X8 or higher.
    internal BLOCK_SIZE ext_partition_eval_thresh;

    // Use best partition decision so far to tune 'ext_partition_eval_thresh'
    internal int ext_part_eval_based_on_cur_best;

    // Disable rectangular partitions for larger block sizes.
    internal int rect_partition_eval_thresh;

    // Prune extended partition search based on whether the split/rect partitions
    // provided an improvement in the previous search.
    // 0 : no pruning
    // 1 : prune 1:4 partition search using winner info from split partitions
    // 2 : prune 1:4 and AB partition search using split and HORZ/VERT info
    internal int prune_ext_part_using_split_info;

    // Prunt rectangular, AB and 4-way partition based on q index and block size
    // 0 : no pruning
    // 1 : prune sub_8x8 at very low quantizers
    // 2 : prune all block size based on qindex
    internal int prune_rectangular_split_based_on_qidx;

    // Prune rectangular partitions based on 4x4 sub-block variance
    // false : no pruning
    // true : prune rectangular partitions based on 4x4 sub-block variance
    // deviation
    //
    // For allintra encode, this speed feature reduces instruction count by 6.4%
    // for speed=6 with coding performance change less than 0.24%. For AVIF image
    // encode, this speed feature reduces encode time by 8.14% for speed 6 on a
    // typical image dataset with coding performance change less than 0.16%. This
    // speed feature is not applicable to speed >= 7.
    internal bool prune_rect_part_using_4x4_var_deviation;

    // Prune rectangular partitions based on prediction mode chosen by NONE
    // partition.
    // false : no pruning
    // true : prunes rectangular partition as described below
    // If prediction mode chosen by NONE partition is
    // DC_PRED or SMOOTH_PRED: Prunes both horizontal and vertical partitions if
    // at least one of the left and top neighbor blocks is larger than the
    // current block.
    // Directional Mode: Prunes either of the horizontal and vertical partition
    // based on center angle of the prediction mode chosen by NONE partition. For
    // example, vertical partition is pruned if center angle of the prediction
    // mode chosen by NONE partition is close to 180 degrees (i.e. horizontal
    // direction) and vice versa.
    // For allintra encode, this speed feature reduces instruction count by 5.1%
    // for speed=6 with coding performance change less than 0.22%. For AVIF image
    // encode, this speed feature reduces encode time by 4.44% for speed 6 on a
    // typical image dataset with coding performance change less than 0.15%.
    // For speed >= 7, variance-based logic is used to determine the partition
    // structure instead of recursive partition search. Therefore, this speed
    // feature is not applicable in such cases.
    internal bool prune_rect_part_using_none_pred_mode;

    // Terminate partition search for child partition,
    // when NONE and SPLIT partition rd_costs are INT64_MAX.
    internal int early_term_after_none_split;

    // Level used to adjust threshold for av1_ml_predict_breakout(). At lower
    // levels, more conservative threshold is used, and value of 0 indicates
    // av1_ml_predict_breakout() is disabled. Value of 3 corresponds to default
    // case with no adjustment to lbd thresholds.
    internal int ml_predict_breakout_level;

    // Prune sub_8x8 (BLOCK_4X4, BLOCK_4X8 and BLOCK_8X4) partitions.
    // 0 : no pruning
    // 1 : pruning based on neighbour block information
    // 2 : prune always
    internal int prune_sub_8x8_partition_level;

    // Prune rectangular split based on simple motion search split/no_split score.
    // 0: disable pruning, 1: enable pruning
    internal int simple_motion_search_rect_split;

    // The current encoder adopts a DFS search for block partitions.
    // Therefore the mode selection and associated rdcost is ready for smaller
    // blocks before the mode selection for some partition types.
    // AB partition could use previous rd information and skip mode search.
    // An example is:
    //
    //  current block
    //  +---+---+
    //  |       |
    //  +       +
    //  |       |
    //  +-------+
    //
    //  SPLIT partition has been searched first before trying HORZ_A
    //  +---+---+
    //  | R | R |
    //  +---+---+
    //  | R | R |
    //  +---+---+
    //
    //  HORZ_A
    //  +---+---+
    //  |   |   |
    //  +---+---+
    //  |       |
    //  +-------+
    //
    //  With this speed feature, the top two sub blocks can directly use rdcost
    //  searched in split partition, and the mode info is also copied from
    //  saved info. Similarly, the bottom rectangular block can also use
    //  the available information from previous rectangular search.
    internal int reuse_prev_rd_results_for_part_ab;

    // Reuse the best prediction modes found in PARTITION_SPLIT and PARTITION_RECT
    // when encoding PARTITION_AB.
    internal int reuse_best_prediction_for_part_ab;

    // The current partition search records the best rdcost so far and uses it
    // in mode search and transform search to early skip when some criteria is
    // met. For example, when the current rdcost is larger than the best rdcost,
    // or the model rdcost is larger than the best rdcost times some thresholds.
    // By default, this feature is turned on to speed up the encoder partition
    // search.
    // If disabling it, at speed 0, 30 frames, we could get
    // about -0.25% quality gain (psnr, ssim, vmaf), with about 13% slowdown.
    internal int use_best_rd_for_pruning;

    // Skip evaluation of non-square partitions based on the corresponding NONE
    // partition.
    // 0: no pruning
    // 1: prune extended partitions if NONE is skippable
    // 2: on top of 1, prune rectangular partitions if NONE is inter, not a newmv
    // mode and skippable
    internal int skip_non_sq_part_based_on_none;

    // Disables 8x8 and below partitions for low quantizers.
    internal int disable_8x8_part_based_on_qidx;

    // Disables either of PARTITION_HORZ_4 or PARTITION_VERT_4 using SSE from
    // simple motion search.
    internal bool prune_h_or_v_4part_using_sms_info;

    // Decoder side speed feature to add penalty for use of smaller partitions.
    // Takes values 0 - 2, 0 indicating no penalty and higher level indicating
    // increased penalty.
    internal int split_partition_penalty_level;
}

internal sealed partial class MV_SPEED_FEATURES
{
    // Motion search method (Diamond, NSTEP, Hex, Big Diamond, Square, etc).
    internal SEARCH_METHODS search_method;

    // Enable the use of faster, less accurate mv search method
    // 0: Disable
    // 1 - 3: Disable for larger bsize
    // 4: Based on bsize, SAD and qp
    // TODO(chiyotsai@google.com): Take the clip's resolution and mv activity into
    // account.
    internal int use_bsize_dependent_search_method;

    // If this is set to 1, we limit the motion search range to 2 times the
    // largest motion vector found in the last frame.
    internal int auto_mv_step_size;

    // Subpel_search_method can only be subpel_tree which does a subpixel
    // logarithmic search that keeps stepping at 1/2 pixel units until
    // you stop getting a gain, and then goes on to 1/4 and repeats
    // the same process. Along the way it skips many diagonals.
    internal SUBPEL_SEARCH_METHOD subpel_search_method;

    // Maximum number of steps in logarithmic subpel search before giving up.
    internal int subpel_iters_per_step;

    // When to stop subpel search.
    internal SUBPEL_FORCE_STOP subpel_force_stop;

    // When to stop subpel search in simple motion search.
    internal SUBPEL_FORCE_STOP simple_motion_subpel_force_stop;

    // If true, sub-pixel search uses the exact convolve function used for final
    // encoding and decoding; otherwise, it uses bilinear interpolation.
    internal SUBPEL_SEARCH_TYPE use_accurate_subpel_search;

    // Threshold for allowing exhaustive motion search.
    internal int exhaustive_searches_thresh;

    // Pattern to be used for any exhaustive mesh searches (except intraBC ME).
    internal readonly MESH_PATTERN[] mesh_patterns = new MESH_PATTERN[MAX_MESH_STEP];

    // Pattern to be used for exhaustive mesh searches of intraBC ME.
    internal readonly MESH_PATTERN[] intrabc_mesh_patterns = new MESH_PATTERN[MAX_MESH_STEP];

    // Reduce single motion search range based on MV result of prior ref_mv_idx.
    internal int reduce_search_range;

    // Prune mesh search.
    internal PRUNE_MESH_SEARCH_LEVEL prune_mesh_search;

    // Use the rd cost around the best FULLPEL_MV to speed up subpel search
    internal int use_fullpel_costlist;

    // Set the full pixel search level of obmc
    // 0: obmc_full_pixel_diamond
    // 1: obmc_refining_search_sad (faster)
    internal int obmc_full_pixel_search_level;

    // Accurate full pixel motion search based on TPL stats.
    internal int full_pixel_search_level;

    // Allow intrabc motion search
    internal int use_intrabc;

    // Prune intrabc candidate block hash search
    // 0: check every block hash candidate
    // 1: check the first 64 block hash candidates only
    internal int prune_intrabc_candidate_block_hash_search;

    // Intrabc search level
    // 0: top + left search, all block sizes, always hash plus pixel search
    // 1: top search only, 4x4, 8x8 and 16x16 block sizes only, perform pixel
    //    search if and only if hash search failed to find a candidate
    internal int intrabc_search_level;

    // Whether the maximum intrabc block size to hash is 8x8
    // 0: Hash from 4x4 up to superblock size
    // 1: Hash 4x4 and 8x8 only
    internal int hash_max_8x8_intrabc_blocks;

    // Whether to downsample the rows in sad calculation during motion search.
    // This is only active when there are at least 16 rows. When this sf is
    // active, if there is a large discrepancy in the SAD values for the final
    // motion vector between skipping vs not skipping, motion search is redone
    // with skip row features off.
    // 0: Disabled (do not downsample rows)
    // 1: Skip SAD calculation of odd rows if the SAD deviation of the even and
    //    odd rows for the starting MV is small. Redo motion search with sf off
    //    when SAD deviation is high for the final motion vector.
    // 2: Skip SAD calculation of odd rows. SAD deviation is not tested for the
    //    start MV and tested only for the final MV.
    internal int use_downsampled_sad;

    // Enable/disable extensive joint motion search.
    internal int disable_extensive_joint_motion_search;

    // Enable second best mv check in joint mv search.
    // 0: allow second MV (use rd cost as the metric)
    // 1: use var as the metric
    // 2: disable second MV
    internal int disable_second_mv;

    // Skips full pixel search based on closeness of start mv and ref mv
    // of previous search.
    // 0: Disabled
    // 1: Skips the full pixel search upto 4 neighbor full-pel start MV and ref MV
    // positions.
    // 2: Skips the full pixel search upto 8 neighbor full-pel start MV and ref MV
    // positions.
    internal int skip_fullpel_search_using_startmv_refmv;

    // Method to use for refining WARPED_CAUSAL motion vectors
    // TODO(rachelbarker): Can this be unified with OBMC in some way?
    internal WARP_SEARCH_METHOD warp_search_method;

    // Maximum number of iterations in WARPED_CAUSAL refinement search
    internal int warp_search_iters;
}

internal sealed partial class INTER_MODE_SPEED_FEATURES
{
    // 2-pass inter mode model estimation where the preliminary pass skips
    // transform search and uses a model to estimate rd, while the final pass
    // computes the full transform search. Two types of models are supported:
    // 0: not used
    // 1: used with online dynamic rd model
    // 2: used with static rd model
    internal int inter_mode_rd_model_estimation;

    // Bypass transform search based on skip rd at following stages
    //   i. Compound type mode search
    //  ii. Motion mode search (mode evaluation and winner motion mode stage)
    // iii. Transform search for best inter candidates
    internal readonly int[] txfm_rd_gate_level = new int[TX_SEARCH_CASES];

    // Limit the inter mode tested in the RD loop
    internal int reduce_inter_modes;

    // This variable is used to cap the maximum number of times we skip testing a
    // mode to be evaluated. A high value means we will be faster.
    internal int adaptive_rd_thresh;

    // Aggressively prune inter modes when best mode is skippable.
    internal int prune_inter_modes_if_skippable;

    // Drop less likely to be picked reference frames in the RD search.
    // Has seven levels for now: 0, 1, 2, 3, 4, 5 and 6 where higher levels prune
    // more aggressively than lower ones. (0 means no pruning).
    internal int selective_ref_frame;

    // Retain LAST2_FRAME on mixed static/high-motion frames when
    // selective_ref_frame == 5.
    internal int retain_last2_in_partial_high_motion;

    // Prune reference frames for rectangular partitions.
    // 0 implies no pruning
    // 1 implies prune for extended partition
    // 2 implies prune horiz, vert and extended partition
    internal int prune_ref_frame_for_rect_partitions;

    // Prune inter modes w.r.t past reference frames
    // 0 no pruning
    // 1 prune inter modes w.r.t ALTREF2 and ALTREF reference frames
    // 2 prune inter modes w.r.t BWDREF, ALTREF2 and ALTREF reference frames
    internal int alt_ref_search_fp;

    // Prune reference frames for single prediction modes based on temporal
    // distance and pred MV SAD. Feasible values are 0-4. The feature is
    // disabled for 0. An increasing value indicates more aggressive pruning
    // threshold.
    internal int prune_single_ref;

    // Prune compound reference frames
    // 0 no pruning
    // 1 prune based on temporal distance and pred_mv_sad. However, disallow
    //   pruning of important reference frame pairs decided based on temporal
    //   distance and quality.
    // 2 prune compound references which do not satisfy the two conditions:
    //   a) The references are at a nearest distance from the current frame in
    //   both past and future direction.
    //   b) The references have minimum pred_mv_sad in both past and future
    //   direction.
    // 3 prune compound references except the one with nearest distance from the
    //   current frame in both past and future direction.
    internal int prune_comp_ref_frames;

    // Skip the evaluation of the current ref_mv in NEW_MV mode based on motion
    // vectors, rate cost, modeled RD cost, and search error:
    // 0: Disabled.
    // 1: After subpel search, skip if subpel MV is identical to an earlier DRL
    //    candidate and rate cost is worse/equal.
    // 2, 3: In addition to 1, prune based on modeled RD cost when rate is worse
    // (level 2: relaxed threshold for ref_mv_idx < 2 and resolution < 720p, level
    // 3: strict threshold). 4: In addition to post-subpel pruning, skip before
    // subpel search if the
    //    full-pel search MV is identical to an earlier DRL candidate and full-pel
    //    rate cost is worse/equal.
    // 5, 6: In addition to 4, skip before subpel search if full-pel search error
    //    and drl_cost are worse (level 5: relaxed threshold with 25% margin,
    //    level 6: strict threshold).
    internal int skip_newmv_in_drl;

    // This speed feature checks duplicate ref MVs among NEARESTMV, NEARMV,
    // GLOBALMV and skips NEARMV or GLOBALMV (in order) if a duplicate is found
    // TODO(any): Instead of skipping repeated ref mv, use the recalculated
    // rd-cost based on mode rate and skip the mode evaluation
    internal int skip_repeated_ref_mv;

    // Flag used to control the ref_best_rd based gating for chroma
    internal int perform_best_rd_based_gating_for_chroma;

    // Reuse the inter_intra_mode search result from NEARESTMV mode to other
    // single ref modes
    internal int reuse_inter_intra_mode;

    // prune wedge and compound segment approximate rd evaluation based on
    // compound average modeled rd
    internal int prune_comp_type_by_model_rd;

    // prune wedge and compound segment approximate rd evaluation based on
    // compound average rd/ref_best_rd
    internal int prune_comp_type_by_comp_avg;

    // Skip some ref frames in compound motion search by single motion search
    // result. Has three levels for now: 0 referring to no skipping, and 1 - 3
    // increasing aggressiveness of skipping in order.
    // Note: The search order might affect the result. It assumes that the single
    // reference modes are searched before compound modes. It is better to search
    // same single inter mode as a group.
    internal int prune_comp_search_by_single_result;

    // Instead of performing a full MV search, do a simple translation first
    // and only perform a full MV search on the motion vectors that performed
    // well.
    internal int prune_mode_search_simple_translation;

    // Only search compound modes with at least one "good" reference frame.
    // A reference frame is good if, after looking at its performance among
    // the single reference modes, it is one of the two best performers.
    internal int prune_compound_using_single_ref;

    // Skip extended compound mode (NEAREST_NEWMV, NEW_NEARESTMV, NEAR_NEWMV,
    // NEW_NEARMV) using ref frames of above and left neighbor
    // blocks.
    // 0 : no pruning
    // 1 : prune ext compound modes using neighbor blocks (less aggressiveness)
    // 2 : prune ext compound modes using neighbor blocks (high aggressiveness)
    // 3 : prune ext compound modes unconditionally (highest aggressiveness)
    internal int prune_ext_comp_using_neighbors;

    // Skip NEW_NEARMV and NEAR_NEWMV extended compound modes
    internal int skip_ext_comp_nearmv_mode;

    // Skip extended compound mode when ref frame corresponding to NEWMV does not
    // have NEWMV as single mode winner.
    // 0 : no pruning
    // 1 : prune extended compound mode (less aggressiveness)
    // 2 : prune extended compound mode (high aggressiveness)
    internal int prune_comp_using_best_single_mode_ref;

    // Skip NEARESTMV and NEARMV using weight computed in ref mv list population
    //
    // Pruning is enabled only when both the top and left neighbor blocks are
    // available and when the current block already has a valid inter prediction.
    internal int prune_nearest_near_mv_using_refmv_weight;

    // Based on previous ref_mv_idx search result, prune the following search.
    internal int prune_ref_mv_idx_search;

    // Disable one sided compound modes.
    internal int disable_onesided_comp;

    // Prune obmc search using previous frame stats.
    // INT_MAX : disable obmc search
    internal int prune_obmc_prob_thresh;

    // Prune warped motion search using previous frame stats.
    internal int prune_warped_prob_thresh;

    // Variance threshold to enable/disable Interintra wedge search
    internal uint disable_interintra_wedge_var_thresh;

    // Variance threshold to enable/disable Interinter wedge search
    internal uint disable_interinter_wedge_var_thresh;

    // De-couple wedge and mode search during interintra RDO.
    internal int fast_interintra_wedge_search;

    // Whether fast wedge sign estimate is used
    internal int fast_wedge_sign_estimate;

    // Enable/disable ME for interinter wedge search.
    internal int disable_interinter_wedge_newmv_search;

    // Decide when and how to use joint_comp.
    internal DIST_WTD_COMP_FLAG use_dist_wtd_comp_flag;

    // Clip the frequency of updating the mv cost.
    internal INTERNAL_COST_UPDATE_TYPE mv_cost_upd_level;

    // Clip the frequency of updating the coeff cost.
    internal INTERNAL_COST_UPDATE_TYPE coeff_cost_upd_level;

    // Clip the frequency of updating the mode cost.
    internal INTERNAL_COST_UPDATE_TYPE mode_cost_upd_level;

    // Prune inter modes based on tpl stats
    // 0 : no pruning
    // 1 : Allow pruning of LAST2 frame
    // 2 - 4: Allow pruning of all reference frames with increased aggressiveness
    // of pruning in order
    internal int prune_inter_modes_based_on_tpl;

    // Skip NEARMV and NEAR_NEARMV modes using ref frames of above and left
    // neighbor blocks and qindex.
    internal PRUNE_NEARMV_LEVEL prune_nearmv_using_neighbors;

    // Model based breakout after interpolation filter search
    // 0: no breakout
    // 1: use model based rd breakout
    internal int model_based_post_interp_filter_breakout;

    // Reuse compound type rd decision when exact match is found
    // 0: No reuse
    // 1: Reuse the compound type decision
    internal int reuse_compound_type_decision;

    // Enable/disable masked compound.
    internal int disable_masked_comp;

    // Enable/disable MV refinement for compound modes corresponds to compound
    // types COMPOUND_AVERAGE, COMPOUND_DISTWTD (currently, this compound type
    // is disabled for speeds >= 2 using the sf 'use_dist_wtd_comp_flag') and
    // COMPOUND_DIFFWTD based on the availability. Levels 0 to 3 indicate
    // increasing order of aggressiveness to disable MV refinement.
    // 0: MV Refinement is enabled and for NEW_NEWMV mode used two iterations of
    // refinement in av1_joint_motion_search().
    // 1: MV Refinement is disabled for COMPOUND_DIFFWTD and enabled for
    // COMPOUND_AVERAGE & COMPOUND_DISTWTD.
    // 2: MV Refinement is enabled for COMPOUND_AVERAGE & COMPOUND_DISTWTD for
    // NEW_NEWMV mode with one iteration of refinement in
    // av1_joint_motion_search() and MV Refinement is disabled for other compound
    // type modes.
    // 3: MV Refinement is disabled.
    internal int enable_fast_compound_mode_search;

    // Reuse masked compound type search results
    internal int reuse_mask_search_results;

    // Enable/disable fast search for wedge masks
    internal int enable_fast_wedge_mask_search;

    // Early breakout from transform search of inter modes
    internal int inter_mode_txfm_breakout;

    // Limit number of inter modes for txfm search if a newmv mode gets
    // evaluated among the top modes.
    // 0: no pruning
    // 1 to 3 indicate increasing order of aggressiveness
    internal int limit_inter_mode_cands;

    // Cap the no. of txfm searches for a given prediction mode.
    // 0: no cap, 1: cap beyond first 4 searches, 2: cap beyond first 3 searches.
    internal int limit_txfm_eval_per_mode;

    // Prune warped motion search based on block size.
    internal int extra_prune_warped;

    // Do not search compound modes for ARF.
    // The intuition is that ARF is predicted by frames far away from it,
    // whose temporal correlations with the ARF are likely low.
    // It is therefore likely that compound modes do not work as well for ARF
    // as other inter frames.
    // Speed/quality impact:
    // Speed 1: 12% faster, 0.1% psnr loss.
    // Speed 2: 2%  faster, 0.05% psnr loss.
    // No change for speed 3 and up, because |disable_onesided_comp| is true.
    internal int skip_arf_compound;

    // Percentage of scaling used to increase the rd cost of warp mode so that
    // encoder decisions are biased against local warp, favoring low complexity
    // modes.
    internal float bias_warp_mode_rd_scale_pct;

    // Percentage of scaling used to increase the rd cost of obmc motion mode so
    // that encoder decisions are biased against local obmc, favoring low
    // complexity modes.
    internal float bias_obmc_mode_rd_scale_pct;

    // Percentage of scaling used to increase the rd cost of GLOBALMV and
    // GLOBAL_GLOBALMV modes of type ROTZOOM so that encoder decisions are biased
    // against these modes, favoring low complexity modes.
    internal float bias_gm_mode_rd_scale_pct;

    // Avoid further evaluation of compound modes using top estimate RD Costs of
    // compound average.
    // Values are 0 (not used),1 - 3 with progressively increasing
    // aggressiveness, i.e., decreasing number of top candidates.
    internal int skip_cmp_using_top_cmp_avg_est_rd_lvl;

    // Skip interinter wedge search based on MSE between the two predictors.
    internal int skip_interinter_wedge_search_based_on_mse;

    // Enable/disable model RD based fast compound wedge mask search.
    internal int enable_comp_wedge_search_using_model_rd;

    // Avoid further evaluation of interintra modes using top estimate RD Costs of
    // smooth interintra. Values are 0 (not used),1 - 2 with progressively
    // increasing aggressiveness, i.e., decreasing number of top candidates.
    internal int skip_interintra_using_top_smooth_rd_lvl;
}

internal sealed partial class INTERP_FILTER_SPEED_FEATURES
{
    // Do limited interpolation filter search for dual filters, since best choice
    // usually includes EIGHTTAP_REGULAR.
    internal int use_fast_interpolation_filter_search;

    // Disable dual filter
    internal int disable_dual_filter;

    // Save results of av1_interpolation_filter_search for a block
    // Check mv and ref_frames before search, if they are very close with previous
    // saved results, filter search can be skipped.
    internal int use_interp_filter;

    // skip sharp_filter evaluation based on regular and smooth filter rd for
    // dual_filter=0 case
    internal int skip_sharp_interp_filter_search;

    // skip interpolation filter search for a block in chessboard pattern
    internal int cb_pred_filter_search;

    // adaptive interp_filter search to allow skip of certain filter types.
    internal int adaptive_interp_filter_search;

    // Forces interpolation filter to EIGHTTAP_REGULAR and skips interpolation
    // filter search.
    internal int skip_interp_filter_search;

    // Bias towards sharp filter
    internal int use_more_sharp_interp;

    // Skip model RD evaluation of chroma planes during interpolation filter
    // search.
    internal int skip_model_rd_uv;
}

internal sealed partial class INTRA_MODE_SPEED_FEATURES
{
    // These bit masks allow you to enable or disable intra modes for each
    // transform size separately.
    internal readonly int[] intra_y_mode_mask = new int[TX_SIZES];
    internal readonly int[] intra_uv_mode_mask = new int[TX_SIZES];

    // flag to allow skipping intra mode for inter frame prediction
    internal int skip_intra_in_interframe;

    // Prune intra mode candidates based on source block histogram of gradient.
    // Applies to luma plane only.
    // Feasible values are 0..4. The feature is disabled for 0. An increasing
    // value indicates more aggressive pruning threshold.
    internal int intra_pruning_with_hog;

    // Prune intra mode candidates based on source block histogram of gradient.
    // Applies to chroma plane only.
    // Feasible values are 0..4. The feature is disabled for 0. An increasing
    // value indicates more aggressive pruning threshold.
    internal int chroma_intra_pruning_with_hog;

    // Enable/disable smooth intra modes.
    internal int disable_smooth_intra;

    // Prune UV_SMOOTH_PRED mode for chroma based on chroma source variance.
    // false : No pruning
    // true  : Prune UV_SMOOTH_PRED mode based on chroma source variance
    //
    // For allintra encode, this speed feature reduces instruction count
    // by 1.90%, 2.21% and 1.97% for speed 6, 7 and 8 with coding performance
    // change less than 0.04%. For AVIF image encode, this speed feature reduces
    // encode time by 1.56%, 2.14% and 0.90% for speed 6, 7 and 8 on a typical
    // image dataset with coding performance change less than 0.05%.
    internal bool prune_smooth_intra_mode_for_chroma;

    // Prune filter intra modes in intra frames.
    // 0 : No pruning
    // 1 : Evaluate applicable filter intra modes based on best intra mode so far
    // 2 : Do not evaluate filter intra modes
    internal int prune_filter_intra_level;

    // prune palette search
    // 0: No pruning
    // 1: Perform coarse search to prune the palette colors. For winner colors,
    // neighbors are also evaluated using a finer search.
    // 2: Perform 2 way palette search from max colors to min colors (and min
    // colors to remaining colors) and terminate the search if current number of
    // palette colors is not the winner.
    internal int prune_palette_search_level;

    // Terminate early in luma palette_size search. Speed feature values indicate
    // increasing level of pruning.
    // 0: No early termination
    // 1: Terminate early for higher luma palette_size, if header rd cost of lower
    // palette_size is more than 2 * best_rd. This level of pruning is more
    // conservative when compared to sf level 2 as the cases which will get pruned
    // with sf level 1 is a subset of the cases which will get pruned with sf
    // level 2.
    // 2: Terminate early for higher luma palette_size, if header rd cost of lower
    // palette_size is more than best_rd.
    // For allintra encode, this sf reduces instruction count by 2.49%, 1.07%,
    // 2.76%, 2.30%, 1.84%, 2.69%, 2.04%, 2.05% and 1.44% for speed 0, 1, 2, 3, 4,
    // 5, 6, 7 and 8 on screen content set with coding performance change less
    // than 0.01% for speed <= 2 and less than 0.03% for speed >= 3. For AVIF
    // image encode, this sf reduces instruction count by 1.94%, 1.13%, 1.29%,
    // 0.93%, 0.89%, 1.03%, 1.07%, 1.20% and 0.18% for speed 0, 1, 2, 3, 4, 5, 6,
    // 7 and 8 on a typical image dataset with coding performance change less than
    // 0.01%.
    internal int prune_luma_palette_size_search_level;

    // Prune chroma intra modes based on luma intra mode winner.
    // 0: No pruning
    // 1: Prune chroma intra modes other than UV_DC_PRED, UV_SMOOTH_PRED,
    // UV_CFL_PRED and the mode that corresponds to luma intra mode winner.
    internal int prune_chroma_modes_using_luma_winner;

    // Clip the frequency of updating the mv cost for intrabc.
    internal INTERNAL_COST_UPDATE_TYPE dv_cost_upd_level;

    // We use DCT_DCT transform followed by computing SATD (Sum of Absolute
    // Transformed Differences) as an estimation of RD score to quickly find the
    // best possible Chroma from Luma (CFL) parameter. Then we do a full RD search
    // near the best possible parameter. The search range is set here.
    // The range of cfl_searh_range should be [1, 33], and the following are the
    // recommended values.
    // 1: Fastest mode.
    // 3: Default mode that provides good speedup without losing compression
    // performance at speed 0.
    // 33: Exhaustive rd search (33 == CFL_MAGS_SIZE). This mode should only
    // be used for debugging purpose.
    internal int cfl_search_range;

    // TOP_INTRA_MODEL_COUNT is 4 that is the number of top model rd to store in
    // intra mode decision. Here, add a speed feature to reduce this number for
    // higher speeds.
    internal int top_intra_model_count_allowed;

    // Adapt top_intra_model_count_allowed locally to prune luma intra modes using
    // neighbor block and quantizer information.
    internal int adapt_top_model_rd_count_using_neighbors;

    // Prune the evaluation of odd delta angles of directional luma intra modes by
    // using the rdcosts of neighbouring delta angles.
    // For allintra encode, this speed feature reduces instruction count
    // by 4.461%, 3.699% and 3.536% for speed 6, 7 and 8 on a typical video
    // dataset with coding performance change less than 0.26%. For AVIF image
    // encode, this speed feature reduces encode time by 2.849%, 2.471%,
    // and 2.051% for speed 6, 7 and 8 on a typical image dataset with coding
    // performance change less than 0.27%.
    internal int prune_luma_odd_delta_angles_in_intra;

    // Terminate early in chroma palette_size search.
    // 0: No early termination
    // 1: Terminate early for higher palette_size, if header rd cost of lower
    // palette_size is more than best_rd.
    // For allintra encode, this sf reduces instruction count by 0.45%,
    // 0.62%, 1.73%, 2.50%, 2.89%, 3.09% and 3.86% for speed 0 to 6 on screen
    // content set with coding performance change less than 0.01%.
    // For AVIF image encode, this sf reduces instruction count by 0.45%, 0.81%,
    // 0.85%, 1.05%, 1.45%, 1.66% and 1.95% for speed 0 to 6 on a typical image
    // dataset with no quality drop.
    internal int early_term_chroma_palette_size_search;

    // Skips the evaluation of filter intra modes in inter frames if rd evaluation
    // of luma intra dc mode results in invalid rd stats.
    internal int skip_filter_intra_in_inter_frames;
}

internal sealed partial class TX_SPEED_FEATURES
{
    // Init search depth for square and rectangular transform partitions.
    // Values:
    // 0 - search full tree, 1: search 1 level, 2: search the highest level only
    internal int inter_tx_size_search_init_depth_sqr;
    internal int inter_tx_size_search_init_depth_rect;
    internal int intra_tx_size_search_init_depth_sqr;
    internal int intra_tx_size_search_init_depth_rect;

    // If any dimension of a coding block size above 64, always search the
    // largest transform only, since the largest transform block size is 64x64.
    internal int tx_size_search_lgr_block;

    internal readonly TX_TYPE_SEARCH tx_type_search = new();

    // Skip split transform block partition when the collocated bigger block
    // is selected as all zero coefficients.
    internal int txb_split_cap;

    // Shortcut the transform block partition and type search when the target
    // rdcost is relatively lower.
    // Values are 0 (not used) , or 1 - 2 with progressively increasing
    // aggressiveness
    internal int adaptive_txb_search_level;

    // Prune level for tx_size_type search for inter based on rd model
    // 0: no pruning
    // 1-2: progressively increasing aggressiveness of pruning
    internal int model_based_prune_tx_search_level;

    // Refine TX type after fast TX search.
    internal int refine_fast_tx_search_results;

    // Prune transform split/no_split eval based on residual properties. A value
    // of 0 indicates no pruning, and the aggressiveness of pruning progressively
    // increases from levels 1 to 3.
    internal int prune_tx_size_level;

    // Prune the evaluation of transform depths as decided by the NN model.
    // false: No pruning.
    // true : Avoid the evaluation of specific transform depths using NN model.
    //
    // For allintra encode, this speed feature reduces instruction count
    // by 4.76%, 8.92% and 11.28% for speed 6, 7 and 8 with coding performance
    // change less than 0.32%. For AVIF image encode, this speed feature reduces
    // encode time by 4.65%, 9.16% and 10.45% for speed 6, 7 and 8 on a typical
    // image dataset with coding performance change less than 0.19%.
    internal bool prune_intra_tx_depths_using_nn;

    // Enable/disable early breakout during transform search of intra modes, by
    // using the minimum rd cost possible. By using this approach, the rd
    // evaluation of applicable transform blocks (in the current block) can be
    // avoided as
    // 1) best_rd evolves during the search in choose_tx_size_type_from_rd()
    // 2) appropriate ref_best_rd is passed in intra_block_yrd()
    //
    // For allintra encode, this speed feature reduces instruction count
    // by 1.11%, 1.08%, 1.02% and 0.93% for speed 3, 6, 7 and 8 with coding
    // performance change less than 0.02%. For AVIF image encode, this speed
    // feature reduces encode time by 0.93%, 1.46%, 1.07%, 0.84%, 0.99% and 0.73%
    // for speed 3, 4, 5, 6, 7 and 8 on a typical image dataset with coding
    // performance change less than 0.004%.
    internal bool use_rd_based_breakout_for_intra_tx_search;

    // Prune RD evaluation of transform split using RD Costs of transform no-split
    // of inter modes that are evaluated so far.
    // Values are 0 (not used),  1 - 2 with progressively increasing
    // aggressiveness, i.e., decreasing number of top candidates
    internal int prune_inter_tx_split_rd_eval_lvl;

    // If 1, use a trellis rd multiplier that favors chroma plane more.
    internal int use_chroma_trellis_rd_mult;
}

internal sealed partial class RD_CALC_SPEED_FEATURES
{
    // Fast approximation of av1_model_rd_from_var_lapndz
    internal int simple_model_rd_from_var;

    // Perform faster distortion computation during the R-D evaluation by trying
    // to approximate the prediction error with transform coefficients (faster but
    // less accurate) rather than computing distortion in the pixel domain (slower
    // but more accurate). The following methods are used for distortion
    // computation:
    // Method 0: Always compute distortion in the pixel domain
    // Method 1: Based on block error, try using transform domain distortion for
    // tx_type search and compute distortion in pixel domain for final RD_STATS
    // Method 2: Based on block error, try to compute distortion in transform
    // domain
    // Methods 1 and 2 may fallback to computing distortion in the pixel domain in
    // case the block error is less than the threshold, which is controlled by the
    // speed feature tx_domain_dist_thres_level.
    //
    // The speed feature tx_domain_dist_level decides which of the above methods
    // needs to be used across different mode evaluation stages as described
    // below:
    // Eval type:    Default      Mode        Winner
    // Level 0  :    Method 0    Method 2    Method 0
    // Level 1  :    Method 1    Method 2    Method 0
    // Level 2  :    Method 2    Method 2    Method 0
    // Level 3  :    Method 2    Method 2    Method 2
    internal int tx_domain_dist_level;

    // Transform domain distortion threshold level
    internal int tx_domain_dist_thres_level;

    // Trellis (dynamic programming) optimization of quantized values
    internal TRELLIS_OPT_TYPE optimize_coefficients;

    // Use hash table to store macroblock RD search results
    // to avoid repeated search on the same residue signal.
    internal int use_mb_rd_hash;

    // Flag used to control the extent of coeff R-D optimization
    internal int perform_coeff_opt;
}

internal sealed partial class WINNER_MODE_SPEED_FEATURES
{
    // Flag used to control the winner mode processing for better R-D optimization
    // of quantized coeffs
    internal int enable_winner_mode_for_coeff_opt;

    // Flag used to control the winner mode processing for transform size
    // search method
    internal int enable_winner_mode_for_tx_size_srch;

    // Control transform size search level
    // Eval type: Default       Mode        Winner
    // Level 0  : FULL RD     LARGEST ALL   FULL RD
    // Level 1  : FAST RD     LARGEST ALL   FULL RD
    // Level 2  : LARGEST ALL LARGEST ALL   FULL RD
    // Level 3 :  LARGEST ALL LARGEST ALL   LARGEST ALL
    internal int tx_size_search_level;

    // Flag used to control the winner mode processing for use transform
    // domain distortion
    internal int enable_winner_mode_for_use_tx_domain_dist;

    // Flag used to enable processing of multiple winner modes
    internal MULTI_WINNER_MODE_TYPE multi_winner_mode_type;

    // Motion mode for winner candidates:
    // 0: speed feature OFF
    // 1 / 2 : Use configured number of winner candidates
    internal int motion_mode_for_winner_cand;

    // Controls the prediction of transform skip block or DC only block.
    //
    // Different speed feature values (0 to 3) decide the aggressiveness of
    // prediction (refer to predict_dc_levels[][] in speed_features.c) to be used
    // during different mode evaluation stages.
    internal int dc_blk_pred_level;

    // If on, disables interpolation filter search in handle_inter_mode loop, and
    // performs it during winner mode processing by \ref
    // tx_search_best_inter_candidates.
    internal int winner_mode_ifs;

    // Controls the disabling of winner mode processing. Speed feature levels
    // are ordered in increasing aggressiveness of pruning. The method considered
    // for disabling, depends on the sf level value and it is described as below.
    // 0: Do not disable
    // 1: Disable for blocks with low source variance.
    // 2: Disable for blocks which turn out to be transform skip (skipped based on
    // eob) during MODE_EVAL stage except NEWMV mode.
    // 3: Disable for blocks which turn out to be transform skip during MODE_EVAL
    // stage except NEWMV mode. For high quantizers, prune conservatively based on
    // transform skip (skipped based on eob) except for NEWMV mode.
    // 4: Disable for blocks which turn out to be transform skip during MODE_EVAL
    // stage.
    internal int prune_winner_mode_eval_level;
}

internal sealed partial class LOOP_FILTER_SPEED_FEATURES
{
    // This feature controls how the loop filter level is determined.
    internal LPF_PICK_METHOD lpf_pick;

    // Skip some final iterations in the determination of the best loop filter
    // level.
    internal int use_coarse_filter_level_search;

    // Reset luma filter levels to zero based on minimum filter levels of
    // reference frames and current frame's pyramid level.
    internal int adaptive_luma_loop_filter_skip;

    // Reset luma filter levels to zero when the percentage of SSE difference
    // between the unfiltered and filtered versions of the current frame is below
    // a threshold.
    internal int skip_loop_filter_using_filt_error;

    // Control how the CDEF strength is determined.
    internal CDEF_PICK_METHOD cdef_pick_method;

    // Decoder side speed feature for adaptive CDEF with strength reduction.
    // Zero out values with low CDEF strengths (luma and/or chroma). This is
    // done as CDEF is a relatively-expensive filter to compute during decode.
    // This speed feature is only enabled in all intra mode.
    internal bool zero_low_cdef_strengths;

    // Decoder side speed feature for adaptive CDEF control based on MSE
    // 0 : Enable CDEF for all planes
    // 1 : Disable CDEF for chroma planes and disable for luma adaptively based on
    //     current frame's pyramid level and improvement in MSE after CDEF
    //     filtering.
    internal int adaptive_cdef_mode;

    // Decoder side speed feature to add penalty for use of dual-sgr filters.
    // Takes values 0 - 10, 0 indicating no penalty and each additional level
    // adding a penalty of 1%
    internal int dual_sgr_penalty_level;

    // Restricts loop restoration to RESTORE_SWITCHABLE by skipping RD cost
    // comparisons for RESTORE_WIENER and RESTORE_SGRPROJ. Also applies a bias
    // during switchable restoration search: each level adds a 0.5% penalty to
    // Wiener and SGR selection.
    // 0 : No restriction or bias (all restoration types allowed)
    // 1+: Skip WIENER/SGRPROJ and apply (level x 0.5%) penalty in
    // search_switchable()
    internal int switchable_lr_with_bias_level;

    // Enable fast search in self guided restoration
    // 0 : Search over all 16 SGR projection parameters listed
    //     in av1_sgr_params[SGRPROJ_PARAMS].
    // 1 : Approximate search using binary search like mechanism,
    //     a total of 8 SGR projection parameters are searched.
    // 2 : Search only 'ep' values in
    //     sgproj_ep_grp1_seed[SGRPROJ_EP_GRP1_SEARCH_COUNT],
    //     a total of 4 SGR projection parameters are searched.
    internal int enable_sgr_ep_pruning;

    // Disable loop restoration for Chroma plane
    internal int disable_loop_restoration_chroma;

    // Disable loop restoration for luma plane
    internal int disable_loop_restoration_luma;

    // Range of loop restoration unit sizes to search
    // The minimum size is clamped against the superblock size in
    // av1_pick_filter_restoration, so that the code which sets this value does
    // not need to know the superblock size ahead of time.
    internal int min_lr_unit_size;
    internal int max_lr_unit_size;

    // Prune RESTORE_WIENER evaluation based on source variance
    // 0 : no pruning
    // 1 : conservative pruning
    // 2 : aggressive pruning
    internal int prune_wiener_based_on_src_var;

    // Prune self-guided loop restoration based on wiener search results
    // 0 : no pruning
    // 1 : pruning based on rdcost ratio of RESTORE_WIENER and RESTORE_NONE
    // 2 : pruning based on winner restoration type among RESTORE_WIENER and
    // RESTORE_NONE
    internal int prune_sgr_based_on_wiener;

    // Reduce the wiener filter win size for luma
    internal int reduce_wiener_window_size;

    // Flag to disable Wiener Loop restoration filter.
    internal bool disable_wiener_filter;

    // Flag to disable Self-guided Loop restoration filter.
    internal bool disable_sgr_filter;

    // Disable the refinement search around the wiener filter coefficients.
    internal bool disable_wiener_coeff_refine_search;

    // Whether to downsample the rows in computation of wiener stats.
    internal int use_downsampled_wiener_stats;
}

internal sealed partial class REAL_TIME_SPEED_FEATURES
{
    // check intra prediction for non-RD mode.
    internal int check_intra_pred_nonrd;

    // Skip checking intra prediction.
    // 0 - don't skip
    // 1 - skip if TX is skipped and best mode is not NEWMV
    // 2 - skip if TX is skipped
    // Skipping aggressiveness increases from level 1 to 2.
    internal int skip_intra_pred;

    // Estimate motion before calculating variance in variance-based partition
    // 0 - Only use zero MV
    // 1 - perform coarse ME
    // 2 - perform coarse ME, and also use neighbours' MVs
    // 3 - use neighbours' MVs without performing coarse ME
    internal int estimate_motion_for_var_based_partition;

    // For nonrd_use_partition: mode of extra check of leaf partition
    // 0 - don't check merge
    // 1 - always check merge
    // 2 - check merge and prune checking final split
    // 3 - check merge and prune checking final split based on bsize and qindex
    internal int nonrd_check_partition_merge_mode;

    // For nonrd_use_partition: check of leaf partition extra split
    internal int nonrd_check_partition_split;

    // Implements various heuristics to skip searching modes
    // The heuristics selected are based on  flags
    // defined in the MODE_SEARCH_SKIP_HEURISTICS enum
    internal uint mode_search_skip_flags;

    // For nonrd: Reduces ref frame search.
    // 0 - low level of search prune in non last frames
    // 1 - pruned search in non last frames
    // 2 - more pruned search in non last frames
    internal int nonrd_prune_ref_frame_search;

    // This flag controls the use of non-RD mode decision.
    internal int use_nonrd_pick_mode;

    // Flag that controls discounting for color map cost during palette search.
    // This saves about 5% of CPU and in non-RD speeds delivers better results
    // across rtc_screen set (on speed 10 overall BDRate growth is 13%)
    internal int discount_color_cost;

    // Use ALTREF frame in non-RD mode decision.
    internal int use_nonrd_altref_frame;

    // Use compound reference for non-RD mode.
    internal int use_comp_ref_nonrd;

    // Reference frames for compound prediction for nonrd pickmode:
    // LAST_GOLDEN (0), LAST_LAST2 (1), or LAST_ALTREF (2).
    internal readonly int[] ref_frame_comp_nonrd = new int[3];

    // use reduced ref set for real-time mode
    internal int use_real_time_ref_set;

    // Skip a number of expensive mode evaluations for blocks with very low
    // temporal variance.
    internal int short_circuit_low_temp_var;

    // Reuse inter prediction in fast non-rd mode.
    internal int reuse_inter_pred_nonrd;

    // Number of best inter modes to search transform. INT_MAX - search all.
    internal int num_inter_modes_for_tx_search;

    // Use interpolation filter search in non-RD mode decision.
    internal int use_nonrd_filter_search;

    // Use simplified RD model for interpolation search and Intra
    internal int use_simple_rd_model;

    // For nonrd mode: use hybrid intra mode search for intra only frames based on
    // block properties.
    // 0 : use nonrd pick intra for all blocks
    // 1 : use rd for bsize < 16x16, nonrd otherwise
    // 2 : use rd for bsize < 16x16 and src var >= 101, nonrd otherwise
    internal int hybrid_intra_pickmode;

    // Filter blocks by certain criteria such as SAD, source variance, such that
    // fewer blocks will go through the palette search.
    // For nonrd encoding path, enable this feature reduces encoding time when
    // palette mode is used. Disabling it leads to better compression efficiency.
    // 0: off
    // 1: less aggressive pruning mode
    // 2, 3: more aggressive pruning mode
    internal int prune_palette_search_nonrd;

    // Compute variance/sse on source difference, prior to encoding superblock.
    internal int source_metrics_sb_nonrd;

    // Flag to indicate process for handling overshoot on slide/scene change,
    // for real-time CBR mode.
    internal OVERSHOOT_DETECTION_CBR overshoot_detection_cbr;

    // Check for scene/content change detection on every frame before encoding.
    internal int check_scene_detection;

    // For keyframes in rtc: adjust the rc_bits_per_mb, to reduce overshoot.
    internal int rc_adjust_keyframe;

    // On scene change or keyframe: compute spatial variance.
    internal int rc_compute_spatial_var_sc_kf;

    // For nonrd mode: Prefer larger partition blks in variance based partitioning
    // 0: disabled, 1-3: increasing aggressiveness
    internal int prefer_large_partition_blocks;

    // uses results of temporal noise estimate
    internal int use_temporal_noise_estimate;

    // Parameter indicating initial search window to be used in full-pixel search
    // for nonrd_pickmode. Range [0, MAX_MVSEARCH_STEPS - 1]. Lower value
    // indicates larger window. If set to 0, step_param is set based on internal
    // logic in set_mv_search_params().
    internal int fullpel_search_step_param;

    // Bit mask to enable or disable intra modes for each prediction block size
    // separately, for nonrd_pickmode.  Currently, the sf is not respected when
    // 'force_intra_check' is true in 'av1_estimate_intra_mode()' function. Also,
    // H and V pred modes allowed through this sf can be further pruned when
    //'prune_hv_pred_modes_using_src_sad' sf is true.
    internal readonly int[] intra_y_mode_bsize_mask_nrd = new int[BLOCK_SIZES];

    // Prune H and V intra predition modes evalution in inter frame.
    // The sf does not have any impact.
    // i. when frame_source_sad is 1.1 times greater than avg_source_sad
    // ii. when cyclic_refresh_segment_id_boosted is enabled
    // iii. when SB level source sad is greater than kMedSad
    // iv. when color sensitivity is non zero for both the chroma channels
    internal bool prune_hv_pred_modes_using_src_sad;

    // Skips mode checks more aggressively in nonRD mode
    internal int nonrd_aggressive_skip;

    // Skip cdef on 64x64 blocks/
    // 0: disabled
    // 1: skip when NEWMV or INTRA is not picked or color sensitivity is off.
    // When color sensitivity is on for a superblock, all 64x64 blocks within
    // will not skip.
    // 2: more aggressive mode where skip is done for all frames where
    // rc->high_source_sad = 0 (non slide-changes), and color sensitivity off.
    internal int skip_cdef_sb;

    // Force selective cdf update.
    internal int selective_cdf_update;
    // Use IntraBC for realtime mode.
    internal int rt_use_intrabc;
    // Prune IntraBC for nonrd pickmode.
    internal int rt_prune_intrabc_nonrd;
    // Fallback search mode for IntraBC on hash misses in nonrd pickmode.
    // 0: Full pixel search (Diamond/Hex search)
    // 1: Block Vector Predictor (BVP) only
    // 2: BVP + 12-point integer offset probe
    // 3: Skip fallback search entirely (Hash-only)
    internal int rt_intrabc_miss_mode;

    // Force only single reference (LAST) for prediction.
    internal int force_only_last_ref;

    // Forces larger partition blocks in variance based partitioning for intra
    // frames
    internal int force_large_partition_blocks_intra;

    // Use fixed partition for superblocks based on source_sad.
    // 0: disabled
    // 1: enabled
    internal int use_fast_fixed_part;

    // Increase source_sad thresholds in nonrd pickmode.
    internal int increase_source_sad_thresh;

    // Skip evaluation of no split in tx size selection for merge partition
    internal int skip_tx_no_split_var_based_partition;

    // Intermediate termination of newMV mode evaluation based on so far best mode
    // sse
    internal int skip_newmv_mode_based_on_sse;

    // Define gf length multiplier.
    // Level 0: use large multiplier, level 1: use medium multiplier.
    internal int gf_length_lvl;

    // Prune inter modes with golden frame as reference for NEARMV and NEWMV modes
    internal int prune_inter_modes_with_golden_ref;

    // Prune inter modes w.r.t golden or alt-ref frame based on sad
    internal int prune_inter_modes_wrt_gf_arf_based_on_sad;

    // Prune inter mode search in rd path based on current block's temporal
    // variance wrt LAST reference.
    internal int prune_inter_modes_using_temp_var;

    // Reduce MV precision to halfpel for higher int MV value & frame-level motion
    // 0: disabled
    // 1-2: Reduce precision to halfpel, fullpel based on conservative
    // thresholds, aggressiveness increases with increase in level
    // 3: Reduce precision to halfpel using more aggressive thresholds
    internal int reduce_mv_pel_precision_highmotion;

    // Reduce MV precision for low complexity blocks
    // 0: disabled
    // 1: Reduce the mv resolution for zero mv if the variance is low
    // 2: Switch to halfpel, fullpel based on low block spatial-temporal
    // complexity.
    internal int reduce_mv_pel_precision_lowcomplex;

    // Prune intra mode evaluation in inter frames based on mv range.
    internal BLOCK_SIZE prune_intra_mode_based_on_mv_range;
    // The number of times to left shift the splitting thresholds in variance
    // based partitioning. The minimum values should be 7 to avoid left shifting
    // by a negative number.
    internal int var_part_split_threshold_shift;

    // Qindex based variance partition threshold index, which determines
    // the aggressiveness of partition pruning
    // 0: disabled for speeds 9,10
    // 1,2: (rd-path) lowers qindex thresholds conditionally (for low SAD sb)
    // 3,4: (non-rd path) uses pre-tuned qindex thresholds
    internal int var_part_based_on_qidx;

    // Enable GF refresh based on Q value.
    internal int gf_refresh_based_on_qp;

    // Temporal filtering
    // The value can be 1 or 2, which indicates the threshold to use.
    // Must be off for lossless mode.
    internal int use_rtc_tf;

    // Use of the identity transform in nonrd_pickmode,
    internal int use_idtx_nonrd;

    // Prune the use of the identity transform in nonrd_pickmode:
    // only for smaller blocks and higher spatial variance, and when skip_txfm
    // is not already set.
    internal int prune_idtx_nonrd;

    // Force to only use dct for palette search in nonrd pickmode.
    internal int dct_only_palette_nonrd;

    // Skip loopfilter, for static content after slide change
    // or key frame, once quality has ramped up.
    // 0: disabled
    // 1: skip only after quality is ramped up.
    // 2: aggrssive mode, where skip is done for all frames that
    // where rc->high_source_sad = 0 (no slide-changes).
    internal int skip_lf_screen;

    // Threshold on the active/inactive region percent to disable
    // the loopfilter and cdef. Setting to 100 disables this feature.
    internal int thresh_active_maps_skip_lf_cdef;

    // For nonrd: early exit out of variance partition that sets the
    // block size to superblock size, and sets mode to zeromv-last skip.
    // 0: disabled
    // 1: zeromv-skip is enabled at SB level only
    // 2: zeromv-skip is enabled at SB level and coding block level
    internal int part_early_exit_zeromv;

    // Early terminate inter mode search based on sse in non-rd path.
    internal INTER_SEARCH_EARLY_TERM_IDX sse_early_term_inter_search;

    // SAD based adaptive altref selection
    internal int sad_based_adp_altref_lag;

    // Enable/disable partition direct merging.
    internal int partition_direct_merging;

    // Level of aggressiveness for obtaining tx size based on qstep
    internal int tx_size_level_based_on_qstep;

    // Avoid the partitioning of a 16x16 block in variance based partitioning
    // (VBP) by making use of minimum and maximum sub-block variances.
    // For allintra encode, this speed feature reduces instruction count by 5.39%
    // for speed 9 on a typical video dataset with coding performance gain
    // of 1.44%.
    // For AVIF image encode, this speed feature reduces encode time
    // by 8.44% for speed 9 on a typical image dataset with coding performance
    // gain of 0.78%.
    internal bool vbp_prune_16x16_split_using_min_max_sub_blk_var;

    // A qindex threshold that determines whether to use qindex based CDEF filter
    // strength estimation for screen content types. The strength estimation model
    // used for screen contents prefers to allow cdef filtering for more frames.
    // This sf is used to limit the frames which go through cdef filtering and
    // following explains the setting of the same.
    // MAXQ (255): This disables the usage of this sf. Here, frame does not use a
    // screen content model thus reduces the number of frames that go through cdef
    // filtering.
    // MINQ (0): Frames always use screen content model thus increasing the number
    // of frames that go through cdef filtering.
    // This speed feature has a substantial gain on coding metrics, with moderate
    // increase encoding time. Select threshold based on speed vs quality
    // trade-off.
    internal int screen_content_cdef_filter_qindex_thresh;

    // Prune compound mode if its variance is higher than the variance of single
    // modes.
    internal bool prune_compoundmode_with_singlecompound_var;

    // Allow mode cost update at frame level every couple frames. This
    // overrides the command line setting --mode-cost-upd-freq=3 (never update
    // except on key frame and first delta).
    internal bool frame_level_mode_cost_update;

    // Prune H_PRED during intra mode evaluation in the nonrd path based on best
    // mode so far.
    //
    // For allintra encode, this speed feature reduces instruction count by 1.10%
    // for speed 9 with coding performance change less than 0.04%.
    // For AVIF image encode, this speed feature reduces encode time by 1.03% for
    // speed 9 on a typical image dataset with coding performance change less than
    // 0.08%.
    internal bool prune_h_pred_using_best_mode_so_far;

    // Enable pruning of intra mode evaluations in nonrd path based on source
    // variance and best mode so far. The pruning logic is enabled only if the
    // mode is not a winner mode of both the neighboring blocks (left/top).
    //
    // For allintra encode, this speed feature reduces instruction count by 3.96%
    // for speed 9 with coding performance change less than 0.38%.
    // For AVIF image encode, this speed feature reduces encode time by 3.46% for
    // speed 9 on a typical image dataset with coding performance change less than
    // -0.06%.
    internal bool enable_intra_mode_pruning_using_neighbors;

    // Prune intra mode evaluations in nonrd path based on best sad so far.
    //
    // For allintra encode, this speed feature reduces instruction count by 3.05%
    // for speed 9 with coding performance change less than 0.24%.
    // For AVIF image encode, this speed feature reduces encode time by 1.87% for
    // speed 9 on a typical image dataset with coding performance change less than
    // 0.16%.
    internal bool prune_intra_mode_using_best_sad_so_far;

    // If compound is enabled, and the current block size is \geq BLOCK_16X16,
    // limit the compound modes to GLOBAL_GLOBALMV. This does not apply to the
    // base layer of svc.
    internal bool check_only_zero_zeromv_on_large_blocks;

    // Allow for disabling cdf update for non reference frames in svc mode.
    internal bool disable_cdf_update_non_reference_frame;

    // Prune compound modes if the single modes variances do not perform well.
    internal bool prune_compoundmode_with_singlemode_var;

    // Skip searching all compound mode if the variance of single_mode residue is
    // sufficiently low.
    internal bool skip_compound_based_on_var;

    // Sets force_zeromv_skip based on the source sad available. Aggressiveness
    // increases with increase in the level set for speed feature.
    // 0: No setting
    // 1: If source sad is kZeroSad
    // 2: If source sad <= kVeryLowSad
    internal int set_zeromv_skip_based_on_source_sad;

    // Downgrades the block-level subpel motion search to
    // av1_find_best_sub_pixel_tree_pruned_more for higher QP and when fullpel
    // search performed well, zeromv has low sad or low source_var
    internal bool use_adaptive_subpel_search;

    // A flag used in RTC case to control frame_refs_short_signaling. Note that
    // the final decision is made in check_frame_refs_short_signaling(). The flag
    // can only be turned on when res < 360p and speed >= 9, in which case only
    // LAST and GOLDEN ref frames are used now.
    internal bool enable_ref_short_signaling;

    // A flag that controls if we check or bypass GLOBALMV in rtc single ref frame
    // case.
    internal bool check_globalmv_on_single_ref;

    // Allows for increasing the color_threshold for palette prediction.
    // This generally leads to better coding efficiency but with some speed loss.
    // Only used for screen content and for nonrd_pickmode.
    internal bool increase_color_thresh_palette;

    // Flag to indicate selecting of higher threshold for scenee change detection.
    internal int higher_thresh_scene_detection;

    // FLag to indicate skip testing of NEWMV for flat blocks.
    internal int skip_newmv_flat_blocks_screen;

    // Flag to force skip encoding for non_reference_frame on slide/scene changes.
    internal int skip_encoding_non_reference_slide_change;

    // Flag to indicate more aggressive QP downward adjustment for screen static
    // content, to make convergence to min_qp faster.
    internal int rc_faster_convergence_static;

    // Skip NEWMV mode evaluation based on sad for screen content.
    internal int skip_newmv_mode_sad_screen;
}

internal sealed partial class SPEED_FEATURES
{
    // Sequence/frame level speed features:
    internal readonly HIGH_LEVEL_SPEED_FEATURES hl_sf = new();

    // Speed features for the first pass.
    internal readonly FIRST_PASS_SPEED_FEATURES fp_sf = new();

    // Speed features related to how tpl's searches are done.
    internal readonly TPL_SPEED_FEATURES tpl_sf = new();

    // [subset] #if CONFIG_PYRAMID_ME is false here.

    // Global motion speed features:
    internal readonly GLOBAL_MOTION_SPEED_FEATURES gm_sf = new();

    // Partition search speed features:
    internal readonly PARTITION_SPEED_FEATURES part_sf = new();

    // Motion search speed features:
    internal readonly MV_SPEED_FEATURES mv_sf = new();

    // Inter mode search speed features:
    internal readonly INTER_MODE_SPEED_FEATURES inter_sf = new();

    // Interpolation filter search speed features:
    internal readonly INTERP_FILTER_SPEED_FEATURES interp_sf = new();

    // Intra mode search speed features:
    internal readonly INTRA_MODE_SPEED_FEATURES intra_sf = new();

    // Transform size/type search speed features:
    internal readonly TX_SPEED_FEATURES tx_sf = new();

    // RD calculation speed features:
    internal readonly RD_CALC_SPEED_FEATURES rd_sf = new();

    // Two-pass mode evaluation features:
    internal readonly WINNER_MODE_SPEED_FEATURES winner_mode_sf = new();

    // In-loop filter speed features:
    internal readonly LOOP_FILTER_SPEED_FEATURES lpf_sf = new();

    // Real-time mode speed features:
    internal readonly REAL_TIME_SPEED_FEATURES rt_sf = new();
}
