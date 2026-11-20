// Port of libaom av1/encoder/block.h
global using TX_PRUNE_TYPE = int;
global using SOURCE_SAD = int;
global using MACROBLOCK_PLANE = Av1rt.Aom.macroblock_plane;
global using macroblock = Av1rt.Aom.MACROBLOCK;
global using static Av1rt.Aom.block;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class block
{
    // Minimum linear dimension of a tpl block
    internal const int MIN_TPL_BSIZE_1D = 16;
    // Maximum number of tpl block in a super block
    internal const int MAX_TPL_BLK_IN_SB = MAX_SB_SIZE / MIN_TPL_BSIZE_1D;
    // Number of txfm hash records kept for the partition block.
    internal const int RD_RECORD_BUFFER_LEN = 8;

    internal const int MAX_TX_TYPE_PROB = 1024;

    internal const int TOP_INTER_TX_NO_SPLIT_COUNT = 4;

    // Compute color sensitivity index for given plane
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int COLOR_SENS_IDX(int plane) => plane - 1;

    // Enable timer statistics of mode search in non-rd
    internal const int COLLECT_NONRD_PICK_MODE_STAT = 0;
    // [subset] mode_search_stat_nonrd: COLLECT_NONRD_PICK_MODE_STAT is 0.

    // Number of compound rd stats
    internal const int MAX_COMP_RD_STATS = 64;

    internal const int TOP_COMP_AVG_EST_RD_COUNT = 5;
    internal const int TOP_SMOOTH_INTERINTRA_RD_COUNT = 2;

    // enum TX_PRUNE_TYPE
    // Do not prune transform depths.
    internal const int TX_PRUNE_NONE = 0;
    // Prune largest transform (depth 0) based on NN model.
    internal const int TX_PRUNE_LARGEST = 1;
    // Prune split transforms (depth>=1) based on NN model.
    internal const int TX_PRUNE_SPLIT = 2;
    // enum SOURCE_SAD
    internal const int kZeroSad = 0;
    internal const int kVeryLowSad = 1;
    internal const int kLowSad = 2;
    internal const int kMedSad = 3;
    internal const int kHighSad = 4;

    // [subset] zero_winner_mode_stats: rd-only (winner_mode_stats is not allocated in the
    // nonrd path).

    private static readonly sbyte[] is_rect_tx_allowed_bsize_LUT = {
        0,  // BLOCK_4X4
        1,  // BLOCK_4X8
        1,  // BLOCK_8X4
        0,  // BLOCK_8X8
        1,  // BLOCK_8X16
        1,  // BLOCK_16X8
        0,  // BLOCK_16X16
        1,  // BLOCK_16X32
        1,  // BLOCK_32X16
        0,  // BLOCK_32X32
        1,  // BLOCK_32X64
        1,  // BLOCK_64X32
        0,  // BLOCK_64X64
        0,  // BLOCK_64X128
        0,  // BLOCK_128X64
        0,  // BLOCK_128X128
        1,  // BLOCK_4X16
        1,  // BLOCK_16X4
        1,  // BLOCK_8X32
        1,  // BLOCK_32X8
        1,  // BLOCK_16X64
        1,  // BLOCK_64X16
    };

    // Only reached from assert()s in bitstream.c / partition_search.c; kept so those
    // asserts can be ported as Debug.Assert.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_rect_tx_allowed_bsize(BLOCK_SIZE bsize)
    {
        return is_rect_tx_allowed_bsize_LUT[bsize];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_rect_tx_allowed(MACROBLOCKD xd, MB_MODE_INFO mbmi)
    {
        return B(is_rect_tx_allowed_bsize(mbmi.bsize) != 0 &&
                 xd.lossless[mbmi.segment_id] == 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int tx_size_to_depth(TX_SIZE tx_size, BLOCK_SIZE bsize)
    {
        TX_SIZE ctx_size = max_txsize_rect_lookup[bsize];
        int depth = 0;
        while (tx_size != ctx_size)
        {
            depth++;
            ctx_size = sub_tx_size_map[ctx_size];
            Debug.Assert(depth <= MAX_TX_DEPTH);
        }
        return depth;
    }

    // [subset] set_blk_skip / is_blk_skip: only used by the rd transform search.
}

// [subset] SuperBlockEnc: the TPL info (tpl_data_count, tpl_inter_cost, tpl_intra_cost, tpl_mv,
// tpl_stride) is dropped (no TPL in the subset).
internal partial struct SuperBlockEnc
{
    // Maximum partition size for the sb.
    internal BLOCK_SIZE min_partition_size;
    // Minimum partition size for the sb.
    internal BLOCK_SIZE max_partition_size;
}

// [subset] WinnerModeStats: rd-only (winner mode processing), dropped.

// Each source plane of the current macroblock
// Class: used through pointers (struct macroblock_plane *p = &x->plane[plane]); copied
// only as part of MACROBLOCK (CopyFrom).
internal sealed partial class macroblock_plane
{
    // Stores source - pred so the txfm can be computed later
    internal Ptr<short> src_diff;
    // Dequantized coefficients
    internal Ptr<int> dqcoeff;
    // Quantized coefficients
    internal Ptr<int> qcoeff;
    // Transformed coefficients
    internal Ptr<int> coeff;
    // Location of the end of qcoeff (end of block).
    internal Ptr<ushort> eobs;
    // Contexts used to code the transform coefficients.
    internal Ptr<byte> txb_entropy_ctx;
    // A buffer containing the source frame.
    internal buf_2d src;

    // Quantization step size used by AV1_XFORM_QUANT_FP.
    internal Ptr<short> quant_fp_QTX;
    // Offset used for rounding in the quantizer process by AV1_XFORM_QUANT_FP.
    internal Ptr<short> round_fp_QTX;
    // Quantization step size used by AV1_XFORM_QUANT_B.
    internal Ptr<short> quant_QTX;
    // Offset used for rounding in the quantizer process by AV1_XFORM_QUANT_B.
    internal Ptr<short> round_QTX;
    // Scale factor to shift coefficients toward zero. Only used by QUANT_B.
    internal Ptr<short> quant_shift_QTX;
    // Size of the quantization bin around 0. Only Used by QUANT_B
    internal Ptr<short> zbin_QTX;
    // Dequantizer
    internal Ptr<short> dequant_QTX;

    internal void CopyFrom(macroblock_plane o)
    {
        src_diff = o.src_diff;
        dqcoeff = o.dqcoeff;
        qcoeff = o.qcoeff;
        coeff = o.coeff;
        eobs = o.eobs;
        txb_entropy_ctx = o.txb_entropy_ctx;
        src = o.src;
        quant_fp_QTX = o.quant_fp_QTX;
        round_fp_QTX = o.round_fp_QTX;
        quant_QTX = o.quant_QTX;
        round_QTX = o.round_QTX;
        quant_shift_QTX = o.quant_shift_QTX;
        zbin_QTX = o.zbin_QTX;
        dequant_QTX = o.dequant_QTX;
    }
}

// Costs for encoding the coefficients within a level.
internal sealed partial class LV_MAP_COEFF_COST
{
    // Cost to skip txfm for the current txfm block.
    internal readonly int[][] txb_skip_cost = EncArrays.New2<int>(TXB_SKIP_CONTEXTS, 2);
    // Cost for encoding the base_eob of a level.
    internal readonly int[][] base_eob_cost = EncArrays.New2<int>(SIG_COEF_CONTEXTS_EOB, 3);
    // Cost for encoding the base level of a coefficient.
    internal readonly int[][] base_cost = EncArrays.New2<int>(SIG_COEF_CONTEXTS, 8);
    // Cost for encoding the last non-zero coefficient.
    internal readonly int[][] eob_extra_cost = EncArrays.New2<int>(EOB_COEF_CONTEXTS, 2);
    // Cost for encoding the dc_sign
    internal readonly int[][] dc_sign_cost = EncArrays.New2<int>(DC_SIGN_CONTEXTS, 2);
    // Cost for encoding an increment to the coefficient
    internal readonly int[][] lps_cost = EncArrays.New2<int>(LEVEL_CONTEXTS, COEFF_BASE_RANGE + 1 + COEFF_BASE_RANGE + 1);

    internal void CopyFrom(LV_MAP_COEFF_COST o)
    {
        EncArrays.Copy2(txb_skip_cost, o.txb_skip_cost);
        EncArrays.Copy2(base_eob_cost, o.base_eob_cost);
        EncArrays.Copy2(base_cost, o.base_cost);
        EncArrays.Copy2(eob_extra_cost, o.eob_extra_cost);
        EncArrays.Copy2(dc_sign_cost, o.dc_sign_cost);
        EncArrays.Copy2(lps_cost, o.lps_cost);
    }
}

// Costs for encoding the eob.
internal sealed partial class LV_MAP_EOB_COST
{
    // eob_cost.
    internal readonly int[][] eob_cost = EncArrays.New2<int>(2, 11);

    internal void CopyFrom(LV_MAP_EOB_COST o)
    {
        EncArrays.Copy2(eob_cost, o.eob_cost);
    }
}

// A buffer that contains the coefficients of a superblock.
// Class: elements of cpi->coeff_buffer_base are reached through x->cb_coef_buff pointers.
internal sealed partial class CB_COEFF_BUFFER
{
    // The transformed coefficients.
    internal readonly Ptr<int>[] tcoeff = new Ptr<int>[MAX_MB_PLANE];
    // Where the transformed coefficients end.
    internal readonly Ptr<ushort>[] eobs = new Ptr<ushort>[MAX_MB_PLANE];
    // Contexts used to code the transform coefficients.
    internal readonly Ptr<byte>[] entropy_ctx = new Ptr<byte>[MAX_MB_PLANE];

    internal void CopyFrom(CB_COEFF_BUFFER o)
    {
        EncArrays.Copy1(tcoeff, o.tcoeff);
        EncArrays.Copy1(eobs, o.eobs);
        EncArrays.Copy1(entropy_ctx, o.entropy_ctx);
    }
}

// Extended mode info derived from mbmi.
// Class: embedded in MACROBLOCK, used through pointers (&x->mbmi_ext).
internal sealed partial class MB_MODE_INFO_EXT
{
    // TODO(angiebird): Reduce the buffer size according to sb_type
    // The reference mv list for the current block.
    internal readonly CANDIDATE_MV[][] ref_mv_stack = EncArrays.New2<CANDIDATE_MV>(MODE_CTX_REF_FRAMES, USABLE_REF_MV_STACK_SIZE);
    // The weights used to compute the ref mvs.
    internal readonly ushort[][] weight = EncArrays.New2<ushort>(MODE_CTX_REF_FRAMES, USABLE_REF_MV_STACK_SIZE);
    // Number of ref mvs in the drl.
    internal readonly byte[] ref_mv_count = new byte[MODE_CTX_REF_FRAMES];
    // Global mvs
    internal readonly int_mv[] global_mvs = new int_mv[REF_FRAMES];
    // Context used to encode the current mode.
    internal readonly short[] mode_context = new short[MODE_CTX_REF_FRAMES];

    internal void CopyFrom(MB_MODE_INFO_EXT o)
    {
        EncArrays.Copy2(ref_mv_stack, o.ref_mv_stack);
        EncArrays.Copy2(weight, o.weight);
        EncArrays.Copy1(ref_mv_count, o.ref_mv_count);
        EncArrays.Copy1(global_mvs, o.global_mvs);
        EncArrays.Copy1(mode_context, o.mode_context);
    }
}

// Stores best extended mode information at frame level.
// Struct: stored by value in the frame-level array (mbmi_ext_info.frame_base) and in
// PICK_MODE_CONTEXT::mbmi_ext_best, and copied by value (*x->mbmi_ext_frame = ...).
// Pointers to it are Ptr<MB_MODE_INFO_EXT_FRAME> (into frame_base) or ref.
internal partial struct MB_MODE_INFO_EXT_FRAME
{
    // \copydoc MB_MODE_INFO_EXT::ref_mv_stack
    internal Arr4<CANDIDATE_MV> ref_mv_stack; // [USABLE_REF_MV_STACK_SIZE]
    // \copydoc MB_MODE_INFO_EXT::weight
    internal Arr4<ushort> weight; // [USABLE_REF_MV_STACK_SIZE]
    // \copydoc MB_MODE_INFO_EXT::ref_mv_count
    internal byte ref_mv_count;
    // TODO(Ravi/Remya): Reduce the buffer size of global_mvs
    // \copydoc MB_MODE_INFO_EXT::global_mvs
    internal Arr8<int_mv> global_mvs; // [REF_FRAMES]
    // \copydoc MB_MODE_INFO_EXT::mode_context
    internal short mode_context;
    // Offset of current coding block's coeff buffer relative to the sb.
    internal Arr2<ushort> cb_offset; // [PLANE_TYPES]
}

// [subset] MB_RD_INFO / MB_RD_RECORD (inter-mode txfm hash records, sf.rd_sf.use_mb_rd_hash),
// COMP_RD_STATS (compound rd), OBMCBuffer (OBMC), PALETTE_BUFFER (palette) and
// CompoundTypeRdBuffers (masked compound) are dropped: they only serve features that are off
// in the subset.

// Stores the best partition information found during partition search.
// [subset] the CNN partition fields (quad_tree_idx, cnn_output_valid, cnn_buffer, log_q) are
// !CONFIG_REALTIME_ONLY.
internal sealed partial class PartitionSearchInfo
{
    // Variance of the subblocks in the superblock.
    internal readonly byte[] variance_low = new byte[105];

    internal void CopyFrom(PartitionSearchInfo o)
    {
        EncArrays.Copy1(variance_low, o.variance_low);
    }
}

// Parameters controlling the transform search.
internal sealed partial class TxfmSearchParams
{
    // Whether to limit the intra txfm search type to the default txfm.
    internal int use_default_intra_tx_type;
    // Whether to limit the intra txfm search to the derived txfm set.
    internal int use_derived_intra_tx_type_set;
    // Probability threshold used for conditionally forcing tx type
    internal int default_inter_tx_type_prob_thresh;
    // Whether to prune 2d transforms based on 1d transform results.
    internal int prune_2d_txfm_mode;
    // Variable from \ref WinnerModeParams based on current eval mode.
    internal readonly uint[] coeff_opt_thresholds = new uint[2];
    // \copydoc coeff_opt_thresholds
    internal uint tx_domain_dist_threshold;
    // \copydoc coeff_opt_thresholds
    internal TX_SIZE_SEARCH_METHOD tx_size_search_method;
    // \copydoc coeff_opt_thresholds
    internal uint use_transform_domain_distortion;
    // \copydoc coeff_opt_thresholds
    internal uint skip_txfm_level;
    // How to search for the optimal tx_size
    internal TX_MODE tx_mode_search_type;
    // Determine whether to predict the dc only skip.
    internal uint predict_dc_level;
    // Whether to use the qm distortion metric.
    internal int use_qm_dist_metric;
    // Keep track of the mode evaluation stage (DEFAULT_EVAL, MODE_EVAL, WINNER_MODE_EVAL).
    internal int mode_eval_type;
    // [subset] nn_prune_depths_for_intra_tx / enable_nn_prune_intra_tx_depths: !CONFIG_REALTIME_ONLY.

    internal void CopyFrom(TxfmSearchParams o)
    {
        use_default_intra_tx_type = o.use_default_intra_tx_type;
        use_derived_intra_tx_type_set = o.use_derived_intra_tx_type_set;
        default_inter_tx_type_prob_thresh = o.default_inter_tx_type_prob_thresh;
        prune_2d_txfm_mode = o.prune_2d_txfm_mode;
        EncArrays.Copy1(coeff_opt_thresholds, o.coeff_opt_thresholds);
        tx_domain_dist_threshold = o.tx_domain_dist_threshold;
        tx_size_search_method = o.tx_size_search_method;
        use_transform_domain_distortion = o.use_transform_domain_distortion;
        skip_txfm_level = o.skip_txfm_level;
        tx_mode_search_type = o.tx_mode_search_type;
        predict_dc_level = o.predict_dc_level;
        use_qm_dist_metric = o.use_qm_dist_metric;
        mode_eval_type = o.mode_eval_type;
    }
}

// Stores various encoding/search decisions related to txfm search.
// [subset] mb_rd_record (MB_RD_RECORD, only allocated when !use_nonrd_pick_mode) and
// tx_search_count (CONFIG_SPEED_STATS) are dropped.
internal sealed partial class TxfmSearchInfo
{
    // Whether to skip transform and quantization on a partition block level.
    internal byte skip_txfm;
    // Whether to skip transform and quantization on a txfm block level.
    internal readonly byte[] blk_skip = new byte[MAX_MIB_SIZE * MAX_MIB_SIZE];
    // Transform types inside the partition block
    internal readonly byte[] tx_type_map_ = new byte[MAX_MIB_SIZE * MAX_MIB_SIZE];
    // TODO(chiyotsai@google.com): Move this to a more appropriate location such
    // as ThreadData.
    // Number of txb splits.
    internal uint txb_split_count;

    internal void CopyFrom(TxfmSearchInfo o)
    {
        skip_txfm = o.skip_txfm;
        EncArrays.Copy1(blk_skip, o.blk_skip);
        EncArrays.Copy1(tx_type_map_, o.tx_type_map_);
        txb_split_count = o.txb_split_count;
    }
}

// Holds the entropy costs for various modes sent to the bitstream.
// All fields are kept: av1_fill_mode_rates() fills them all.
internal sealed partial class ModeCosts
{
    internal readonly int[][] partition_cost = EncArrays.New2<int>(PARTITION_CONTEXTS, EXT_PARTITION_TYPES);
    internal readonly int[][] mbmode_cost = EncArrays.New2<int>(BLOCK_SIZE_GROUPS, INTRA_MODES);
    internal readonly int[][][] y_mode_costs = EncArrays.New3<int>(INTRA_MODES, INTRA_MODES, INTRA_MODES);
    internal readonly int[][][] intra_uv_mode_cost = EncArrays.New3<int>(CFL_ALLOWED_TYPES, INTRA_MODES, UV_INTRA_MODES);
    internal readonly int[][] filter_intra_cost = EncArrays.New2<int>(BLOCK_SIZES_ALL, 2);
    internal readonly int[] filter_intra_mode_cost = new int[FILTER_INTRA_MODES];
    internal readonly int[][] angle_delta_cost = EncArrays.New2<int>(DIRECTIONAL_MODES, 2 * MAX_ANGLE_DELTA + 1);
    internal readonly int[][][] cfl_cost = EncArrays.New3<int>(CFL_JOINT_SIGNS, CFL_PRED_PLANES, CFL_ALPHABET_SIZE);
    internal readonly int[] intrabc_cost = new int[2];
    internal readonly int[][] palette_y_size_cost = EncArrays.New2<int>(PALATTE_BSIZE_CTXS, PALETTE_SIZES);
    internal readonly int[][] palette_uv_size_cost = EncArrays.New2<int>(PALATTE_BSIZE_CTXS, PALETTE_SIZES);
    internal readonly int[][][] palette_y_color_cost = EncArrays.New3<int>(PALETTE_SIZES, PALETTE_COLOR_INDEX_CONTEXTS, PALETTE_COLORS);
    internal readonly int[][][] palette_uv_color_cost = EncArrays.New3<int>(PALETTE_SIZES, PALETTE_COLOR_INDEX_CONTEXTS, PALETTE_COLORS);
    internal readonly int[][][] palette_y_mode_cost = EncArrays.New3<int>(PALATTE_BSIZE_CTXS, PALETTE_Y_MODE_CONTEXTS, 2);
    internal readonly int[][] palette_uv_mode_cost = EncArrays.New2<int>(PALETTE_UV_MODE_CONTEXTS, 2);
    internal readonly int[][] skip_mode_cost = EncArrays.New2<int>(SKIP_MODE_CONTEXTS, 2);
    internal readonly int[][] newmv_mode_cost = EncArrays.New2<int>(NEWMV_MODE_CONTEXTS, 2);
    internal readonly int[][] zeromv_mode_cost = EncArrays.New2<int>(GLOBALMV_MODE_CONTEXTS, 2);
    internal readonly int[][] refmv_mode_cost = EncArrays.New2<int>(REFMV_MODE_CONTEXTS, 2);
    internal readonly int[][] drl_mode_cost0 = EncArrays.New2<int>(DRL_MODE_CONTEXTS, 2);
    internal readonly int[][][] single_ref_cost = EncArrays.New3<int>(REF_CONTEXTS, SINGLE_REFS - 1, 2);
    internal readonly int[][] comp_inter_cost = EncArrays.New2<int>(COMP_INTER_CONTEXTS, 2);
    internal readonly int[][] comp_ref_type_cost = EncArrays.New2<int>(COMP_REF_TYPE_CONTEXTS, CDF_SIZE(COMP_REFERENCE_TYPES));
    internal readonly int[][][] uni_comp_ref_cost = EncArrays.New3<int>(UNI_COMP_REF_CONTEXTS, UNIDIR_COMP_REFS - 1, CDF_SIZE(2));
    internal readonly int[][][] comp_ref_cost = EncArrays.New3<int>(REF_CONTEXTS, FWD_REFS - 1, 2);
    internal readonly int[][][] comp_bwdref_cost = EncArrays.New3<int>(REF_CONTEXTS, BWD_REFS - 1, 2);
    internal readonly int[][] intra_inter_cost = EncArrays.New2<int>(INTRA_INTER_CONTEXTS, 2);
    internal readonly int[][] inter_compound_mode_cost = EncArrays.New2<int>(INTER_MODE_CONTEXTS, INTER_COMPOUND_MODES);
    internal readonly int[][] compound_type_cost = EncArrays.New2<int>(BLOCK_SIZES_ALL, MASKED_COMPOUND_TYPES);
    internal readonly int[][] wedge_idx_cost = EncArrays.New2<int>(BLOCK_SIZES_ALL, 16);
    internal readonly int[][] interintra_cost = EncArrays.New2<int>(BLOCK_SIZE_GROUPS, 2);
    internal readonly int[][] wedge_interintra_cost = EncArrays.New2<int>(BLOCK_SIZES_ALL, 2);
    internal readonly int[][] interintra_mode_cost = EncArrays.New2<int>(BLOCK_SIZE_GROUPS, INTERINTRA_MODES);
    internal readonly int[][] comp_idx_cost = EncArrays.New2<int>(COMP_INDEX_CONTEXTS, 2);
    internal readonly int[][] comp_group_idx_cost = EncArrays.New2<int>(COMP_GROUP_IDX_CONTEXTS, 2);
    internal readonly int[][] motion_mode_cost = EncArrays.New2<int>(BLOCK_SIZES_ALL, MOTION_MODES);
    internal readonly int[][] motion_mode_cost1 = EncArrays.New2<int>(BLOCK_SIZES_ALL, 2);
    internal readonly int[][] switchable_interp_costs = EncArrays.New2<int>(SWITCHABLE_FILTER_CONTEXTS, SWITCHABLE_FILTERS);
    internal readonly int[][] skip_txfm_cost = EncArrays.New2<int>(SKIP_CONTEXTS, 2);
    internal readonly int[][][] tx_size_cost = EncArrays.New3<int>(TX_SIZES - 1, TX_SIZE_CONTEXTS, TX_SIZES);
    internal readonly int[][] txfm_partition_cost = EncArrays.New2<int>(TXFM_PARTITION_CONTEXTS, 2);
    internal readonly int[][][] inter_tx_type_costs = EncArrays.New3<int>(EXT_TX_SETS_INTER, EXT_TX_SIZES, TX_TYPES);
    internal readonly int[][][][] intra_tx_type_costs = EncArrays.New4<int>(EXT_TX_SETS_INTRA, EXT_TX_SIZES, INTRA_MODES, TX_TYPES);
    internal readonly int[] switchable_restore_cost = new int[RESTORE_SWITCHABLE_TYPES];
    internal readonly int[] wiener_restore_cost = new int[2];
    internal readonly int[] sgrproj_restore_cost = new int[2];
    internal readonly int[][] tmp_pred_cost = EncArrays.New2<int>(SEG_TEMPORAL_PRED_CTXS, 2);
    internal readonly int[][] spatial_pred_cost = EncArrays.New2<int>(SPATIAL_PREDICTION_PROBS, MAX_SEGMENTS);

    internal void CopyFrom(ModeCosts o)
    {
        EncArrays.Copy2(partition_cost, o.partition_cost);
        EncArrays.Copy2(mbmode_cost, o.mbmode_cost);
        EncArrays.Copy3(y_mode_costs, o.y_mode_costs);
        EncArrays.Copy3(intra_uv_mode_cost, o.intra_uv_mode_cost);
        EncArrays.Copy2(filter_intra_cost, o.filter_intra_cost);
        EncArrays.Copy1(filter_intra_mode_cost, o.filter_intra_mode_cost);
        EncArrays.Copy2(angle_delta_cost, o.angle_delta_cost);
        EncArrays.Copy3(cfl_cost, o.cfl_cost);
        EncArrays.Copy1(intrabc_cost, o.intrabc_cost);
        EncArrays.Copy2(palette_y_size_cost, o.palette_y_size_cost);
        EncArrays.Copy2(palette_uv_size_cost, o.palette_uv_size_cost);
        EncArrays.Copy3(palette_y_color_cost, o.palette_y_color_cost);
        EncArrays.Copy3(palette_uv_color_cost, o.palette_uv_color_cost);
        EncArrays.Copy3(palette_y_mode_cost, o.palette_y_mode_cost);
        EncArrays.Copy2(palette_uv_mode_cost, o.palette_uv_mode_cost);
        EncArrays.Copy2(skip_mode_cost, o.skip_mode_cost);
        EncArrays.Copy2(newmv_mode_cost, o.newmv_mode_cost);
        EncArrays.Copy2(zeromv_mode_cost, o.zeromv_mode_cost);
        EncArrays.Copy2(refmv_mode_cost, o.refmv_mode_cost);
        EncArrays.Copy2(drl_mode_cost0, o.drl_mode_cost0);
        EncArrays.Copy3(single_ref_cost, o.single_ref_cost);
        EncArrays.Copy2(comp_inter_cost, o.comp_inter_cost);
        EncArrays.Copy2(comp_ref_type_cost, o.comp_ref_type_cost);
        EncArrays.Copy3(uni_comp_ref_cost, o.uni_comp_ref_cost);
        EncArrays.Copy3(comp_ref_cost, o.comp_ref_cost);
        EncArrays.Copy3(comp_bwdref_cost, o.comp_bwdref_cost);
        EncArrays.Copy2(intra_inter_cost, o.intra_inter_cost);
        EncArrays.Copy2(inter_compound_mode_cost, o.inter_compound_mode_cost);
        EncArrays.Copy2(compound_type_cost, o.compound_type_cost);
        EncArrays.Copy2(wedge_idx_cost, o.wedge_idx_cost);
        EncArrays.Copy2(interintra_cost, o.interintra_cost);
        EncArrays.Copy2(wedge_interintra_cost, o.wedge_interintra_cost);
        EncArrays.Copy2(interintra_mode_cost, o.interintra_mode_cost);
        EncArrays.Copy2(comp_idx_cost, o.comp_idx_cost);
        EncArrays.Copy2(comp_group_idx_cost, o.comp_group_idx_cost);
        EncArrays.Copy2(motion_mode_cost, o.motion_mode_cost);
        EncArrays.Copy2(motion_mode_cost1, o.motion_mode_cost1);
        EncArrays.Copy2(switchable_interp_costs, o.switchable_interp_costs);
        EncArrays.Copy2(skip_txfm_cost, o.skip_txfm_cost);
        EncArrays.Copy3(tx_size_cost, o.tx_size_cost);
        EncArrays.Copy2(txfm_partition_cost, o.txfm_partition_cost);
        EncArrays.Copy3(inter_tx_type_costs, o.inter_tx_type_costs);
        EncArrays.Copy4(intra_tx_type_costs, o.intra_tx_type_costs);
        EncArrays.Copy1(switchable_restore_cost, o.switchable_restore_cost);
        EncArrays.Copy1(wiener_restore_cost, o.wiener_restore_cost);
        EncArrays.Copy1(sgrproj_restore_cost, o.sgrproj_restore_cost);
        EncArrays.Copy2(tmp_pred_cost, o.tmp_pred_cost);
        EncArrays.Copy2(spatial_pred_cost, o.spatial_pred_cost);
    }
}

// Holds mv costs for encoding and motion search.
internal sealed partial class MvCosts
{
    // Costs for coding the zero components.
    internal readonly int[] nmv_joint_cost = new int[MV_JOINTS];

    // Allocates memory for 1/4-pel motion vector costs.
    internal readonly int[][] nmv_cost_alloc = EncArrays.New2<int>(2, MV_VALS);

    // Allocates memory for 1/8-pel motion vector costs.
    internal readonly int[][] nmv_cost_hp_alloc = EncArrays.New2<int>(2, MV_VALS);

    // Points to the middle of \ref nmv_cost_alloc
    internal readonly Ptr<int>[] nmv_cost = new Ptr<int>[2];

    // Points to the middle of \ref nmv_cost_hp_alloc
    internal readonly Ptr<int>[] nmv_cost_hp = new Ptr<int>[2];

    // Points to the nmv_cost_hp in use.
    // C: int **mv_cost_stack, pointing at nmv_cost or nmv_cost_hp of this struct.
    internal Ptr<int>[] mv_cost_stack;

    // C copies MvCosts by value (*td->mb.mv_costs = *cpi->td.mb.mv_costs). Like C, the pointer
    // members are copied verbatim, so they keep pointing into `o`'s arrays.
    // PORT-NOTE: this mirrors the C aliasing (nmv_cost/mv_cost_stack of the copy refer to the
    // source's buffers until av1_set_high_precision_mv() / av1_fill_mv_costs() re-point them).
    internal void CopyFrom(MvCosts o)
    {
        EncArrays.Copy1(nmv_joint_cost, o.nmv_joint_cost);
        EncArrays.Copy2(nmv_cost_alloc, o.nmv_cost_alloc);
        EncArrays.Copy2(nmv_cost_hp_alloc, o.nmv_cost_hp_alloc);
        EncArrays.Copy1(nmv_cost, o.nmv_cost);
        EncArrays.Copy1(nmv_cost_hp, o.nmv_cost_hp);
        mv_cost_stack = o.mv_cost_stack;
    }
}

// [subset] IntraBCMVCosts: intra block copy is off in the subset (dv costs never needed).

// Holds the costs needed to encode the coefficients
internal sealed partial class CoeffCosts
{
    // Costs for coding the coefficients.
    internal readonly LV_MAP_COEFF_COST[][] coeff_costs = EncArrays.NewObj2<LV_MAP_COEFF_COST>(TX_SIZES, PLANE_TYPES);
    // Costs for coding the eobs.
    internal readonly LV_MAP_EOB_COST[][] eob_costs = EncArrays.NewObj2<LV_MAP_EOB_COST>(7, 2);

    internal void CopyFrom(CoeffCosts o)
    {
        EncArrays.CopyObj2(coeff_costs, o.coeff_costs);
        EncArrays.CopyObj2(eob_costs, o.eob_costs);
    }
}

// Stores the warp samples of a reference frame.
internal partial struct WARP_SAMPLE_INFO
{
    internal int num;
    internal Arr16<int> pts;
    internal Arr16<int> pts_inref;
}

// Source content state of a superblock (passed by value).
internal partial struct CONTENT_STATE_SB
{
    internal SOURCE_SAD source_sad_nonrd;
    internal SOURCE_SAD source_sad_rd;
    internal int lighting_change;
    internal int low_sumdiff;
}

// Structure to hold pixel level gradient info.
internal partial struct PixelLevelGradientInfo
{
    internal ushort abs_dx_abs_dy_sum;
    internal sbyte hist_bin_idx;
    internal bool is_dx_zero;
}

// Structure to hold the variance and log(1 + variance) for 4x4 sub-blocks.
internal partial struct Block4x4VarInfo
{
    internal double log_var;
    internal int var;
}

// C: only defined when NDEBUG is not defined (assert bookkeeping); kept unconditionally.
internal partial struct SetOffsetsLoc
{
    internal int mi_row;
    internal int mi_col;
    internal BLOCK_SIZE bsize;
}

// Encoder's parameters related to the current coding block.
// Class (large, used through pointers). C copies it by value
// (ethread.c: thread_data->td->mb = cpi->td.mb), hence CopyFrom.
//
// [subset] dropped fields (they only serve features that are off in the subset):
// obmc_buffer, palette_buffer, comp_rd_buffer, dv_costs, best_pred_mv_sad, tpl_keep_ref_frame,
// picked_ref_frames_mask, winner_mode_stats, winner_mode_count, rd_model, inter_modes_info,
// compound_idx, comp_rd_stats, comp_rd_stats_idx, recalc_luma_mc_data, intrabc_hash_info,
// use_mb_mode_cache, mb_mode_cache, sub_block_energy_diff, sub_block_energy_bsize,
// est_pred (CONFIG_RT_ML_PARTITIONING), ms_stat_nonrd (COLLECT_NONRD_PICK_MODE_STAT),
// top_inter_tx_no_split_rd, sb_stats_cache, sb_fp_stats, top_comp_avg_est_rd,
// top_smooth_interintra_rd, rdcost (CONFIG_PARTITION_SEARCH_ORDER).
internal sealed partial class MACROBLOCK
{
    /*****************************************************************************
     * \name Source, Buffers and Decoder
     ****************************************************************************/
    // Each of the encoding plane.
    internal readonly macroblock_plane[] plane = EncArrays.NewObj1<macroblock_plane>(MAX_MB_PLANE);

    // Decoder's internal information.
    internal readonly MACROBLOCKD e_mbd = new();

    // Derived coding information.
    internal readonly MB_MODE_INFO_EXT mbmi_ext = new();
    // Finalized mbmi_ext for the whole frame.
    internal Ptr<MB_MODE_INFO_EXT_FRAME> mbmi_ext_frame;

    // Entropy context for the current row.
    internal FRAME_CONTEXT row_ctx;
    // Entropy context for the current tile.
    internal FRAME_CONTEXT tile_pb_ctx;

    // Buffer of transformed coefficients
    internal CB_COEFF_BUFFER cb_coef_buff;
    // Offset of current coding block's coeff buffer relative to the sb.
    internal readonly ushort[] cb_offset = new ushort[PLANE_TYPES];

    // Buffer to store convolution during averaging process in compound mode.
    internal Ptr<CONV_BUF_TYPE> tmp_conv_dst;

    // Temporary buffer to hold prediction.
    internal readonly Ptr<byte>[] tmp_pred_bufs = new Ptr<byte>[2];

    // Buffer used for upsampled prediction.
    internal Ptr<byte> upsample_pred;

    /*****************************************************************************
     * \name Rdopt Costs
     ****************************************************************************/
    // Quantization index for the current partition block.
    internal int qindex;
    // Difference between frame-level qindex and current qindex.
    internal int delta_qindex;
    // Difference between frame-level qindex and qindex used to compute rdmult.
    internal int rdmult_delta_qindex;
    // Current qindex (before being adjusted by delta_q_res) used to derive rdmult_delta_qindex.
    internal int rdmult_cur_qindex;
    // Rate-distortion multiplier.
    internal int rdmult;
    // Intra only, per sb rd adjustment.
    internal int intra_sb_rdmult_modifier;
    // Superblock level distortion propagation factor.
    internal double rb;
    // Energy in the current source coding block. Used to calculate \ref rdmult
    internal int mb_energy;
    // Energy in the current source superblock. Used to calculate \ref rdmult
    internal int sb_energy_level;

    // The rate needed to signal a mode to the bitstream.
    internal readonly ModeCosts mode_costs = new();
    // The rate needed to encode a new motion vector to the bitstream and some
    // multipliers for motion search.
    internal MvCosts mv_costs;
    // The rate needed to signal the txfm coefficients to the bitstream.
    internal readonly CoeffCosts coeff_costs = new();

    /*****************************************************************************
     * \name Rate to Distortion Multipliers
     ****************************************************************************/
    // A multiplier that converts mv cost to l2 error.
    internal int errorperbit;
    // A multiplier that converts mv cost to l1 error.
    internal int sadperbit;

    /*****************************************************************************
     * \name Segmentation
     ****************************************************************************/
    // Skip mode for the segment
    internal int seg_skip_block;
    // Number of segment 1 blocks
    internal int actual_num_seg1_blocks;
    // Number of segment 2 blocks
    internal int actual_num_seg2_blocks;
    // Number of zero motion vectors
    internal int cnt_zeromv;
    // Flag to force zeromv-skip at superblock level, for nonrd path.
    internal int force_zeromv_skip_for_sb;
    // Flag to force zeromv-skip at block level, for nonrd path.
    internal int force_zeromv_skip_for_blk;
    // Previous segment id for which qmatrices were updated.
    internal int prev_segment_id;

    /*****************************************************************************
     * \name Superblock
     ****************************************************************************/
    // Information on a whole superblock level.
    // TODO(chiyotsai@google.com): Refactor this out of macroblock
    internal SuperBlockEnc sb_enc;

    // Characteristics of the current superblock.
    internal CONTENT_STATE_SB content_state_sb;

    /*****************************************************************************
     * \name Reference Frame Search
     ****************************************************************************/
    // Sum absolute distortion of the predicted mv for each ref frame.
    internal readonly int[] pred_mv_sad = new int[REF_FRAMES];
    // The sad of the 1st mv ref (nearest).
    internal readonly int[] pred_mv0_sad = new int[REF_FRAMES];
    // The sad of the 2nd mv ref (near).
    internal readonly int[] pred_mv1_sad = new int[REF_FRAMES];

    // Warp motion samples buffer.
    internal readonly WARP_SAMPLE_INFO[] warp_sample_info = new WARP_SAMPLE_INFO[REF_FRAMES];

    // Prune ref frames in real-time mode.
    internal int nonrd_prune_ref_frame_search;

    /*****************************************************************************
     * \name Partition Search
     ****************************************************************************/
    // Stores some partition-search related buffers.
    internal readonly PartitionSearchInfo part_search_info = new();

    // Whether to disable some features to force a mode in current block.
    internal int must_find_valid_partition;

    /*****************************************************************************
     * \name Prediction Mode Search
     ****************************************************************************/
    // Whether to disable some features to force a mode in current block.
    internal int skip_mode;

    // Factors used for rd-thresholding.
    internal readonly int[][] thresh_freq_fact = EncArrays.New2<int>(BLOCK_SIZES_ALL, MAX_MODES);

    /*****************************************************************************
     * \name MV Search
     ****************************************************************************/
    // Pointer to the buffer which caches gradient information.
    internal Ptr<PixelLevelGradientInfo> pixel_gradient_info;
    // Flags indicating the availability of cached gradient info.
    internal readonly bool[] is_sb_gradient_cached = new bool[PLANE_TYPES];

    // Flag to reuse predicted samples of inter block.
    internal bool reuse_inter_pred;

    // Context used to determine the initial step size in motion search.
    internal readonly uint[] max_mv_context = new uint[REF_FRAMES];

    // Limit for the range of motion vectors.
    internal FullMvLimits mv_limits;

    // Buffer for storing the search site config.
    internal readonly search_site_config[] search_site_cfg_buf =
        EncArrays.NewObj1<search_site_config>(NUM_DISTINCT_SEARCH_METHODS);

    /*****************************************************************************
     * \name Txfm Search
     ****************************************************************************/
    // Parameters that control how motion search is done.
    internal readonly TxfmSearchParams txfm_search_params = new();

    // Results of the txfm searches that have been done.
    internal readonly TxfmSearchInfo txfm_search_info = new();

    /*****************************************************************************
     * \name Misc
     ****************************************************************************/
    // Variance of the source frame.
    internal readonly byte[] color_sensitivity_sb = new byte[MAX_MB_PLANE - 1];
    // Color sensitivity flag for the superblock for golden reference.
    internal readonly byte[] color_sensitivity_sb_g = new byte[MAX_MB_PLANE - 1];
    // Color sensitivity flag for the superblock for altref reference.
    internal readonly byte[] color_sensitivity_sb_alt = new byte[MAX_MB_PLANE - 1];
    // Color sensitivity flag for the coding block.
    internal readonly byte[] color_sensitivity = new byte[MAX_MB_PLANE - 1];
    // Coding block distortion value for uv/color, minimum over the inter modes.
    internal long min_dist_inter_uv;

    // Threshold on the number of colors for testing palette mode.
    internal int color_palette_thresh;

    // Used in REALTIME coding mode: flag to indicate if the color_sensitivity
    // should be checked at the coding block level.
    internal int force_color_check_block_level;

    // The buffer used by search_tx_type() to swap dqcoeff in macroblock_plane
    // so we can keep dqcoeff of the best tx_type.
    internal Ptr<int> dqcoeff_buf;

    // Variance of the source frame.
    internal uint source_variance;
    // Flag to indicate coding block is zero sad.
    internal int block_is_zero_sad;
    // Flag to indicate superblock ME in variance partition is determined to be
    // good/reliable, and so the superblock MV will be tested in the
    // nonrd_pickmode. This is only used for LAST_FRAME.
    internal int sb_me_partition;
    // Flag to indicate to test the superblock MV for the coding block in the
    // nonrd_pickmode.
    internal int sb_me_block;
    // Flag to indicate superblock selected column scroll.
    internal int sb_col_scroll;
    // Flag to indicate superblock selected row scroll.
    internal int sb_row_scroll;
    // Motion vector from superblock MV derived from int_pro_motion() in
    // the variance_partitioning.
    internal int_mv sb_me_mv;
    // Flag to indicate if a fixed partition should be used, only if the
    // speed feature rt_sf->use_fast_fixed_part is enabled.
    internal int sb_force_fixed_part;
    // SSE of the current predictor.
    // Reference frame whose prediction a skip-segment superblock copied when the
    // started, or NONE_FRAME. Only set for skip segment superblocks.
    internal MV_REFERENCE_FRAME sb_copied_ref;
    // SSE of the current predictor.
    internal readonly uint[] pred_sse = new uint[REF_FRAMES];
    // Prediction for ML based partition.
    // [subset] est_pred: CONFIG_RT_ML_PARTITIONING is 0.
    // Flag to indicate if the merge partition search should be tried.
    internal int try_merge_partition;

    // Pointer to buffer which caches sub-block variances in a superblock.
    internal Ptr<Block4x4VarInfo> src_var_info_of_4x4_sub_blocks;

    // Location of the last call to av1_set_offsets() (assert bookkeeping, !NDEBUG in C).
    internal SetOffsetsLoc last_set_offsets_loc;

    // Indicates the number of pixels with a palette count.
    internal int palette_pixels;

    // Number of pixels to the bottom/right edge of the frame (used by the encoder only).
    internal int pix_to_bottom_edge;
    internal int pix_to_right_edge;

    // C: `*dst = *src` of the whole MACROBLOCK. Pointer members are copied as pointers
    // (shared), embedded structs/arrays are deep-copied.
    internal void CopyFrom(MACROBLOCK o)
    {
        EncArrays.CopyObj1(plane, o.plane);
        e_mbd.CopyFrom(o.e_mbd);
        mbmi_ext.CopyFrom(o.mbmi_ext);
        mbmi_ext_frame = o.mbmi_ext_frame;
        row_ctx = o.row_ctx;
        tile_pb_ctx = o.tile_pb_ctx;
        cb_coef_buff = o.cb_coef_buff;
        EncArrays.Copy1(cb_offset, o.cb_offset);
        tmp_conv_dst = o.tmp_conv_dst;
        EncArrays.Copy1(tmp_pred_bufs, o.tmp_pred_bufs);
        upsample_pred = o.upsample_pred;
        qindex = o.qindex;
        delta_qindex = o.delta_qindex;
        rdmult_delta_qindex = o.rdmult_delta_qindex;
        rdmult_cur_qindex = o.rdmult_cur_qindex;
        rdmult = o.rdmult;
        intra_sb_rdmult_modifier = o.intra_sb_rdmult_modifier;
        rb = o.rb;
        mb_energy = o.mb_energy;
        sb_energy_level = o.sb_energy_level;
        mode_costs.CopyFrom(o.mode_costs);
        mv_costs = o.mv_costs;
        coeff_costs.CopyFrom(o.coeff_costs);
        errorperbit = o.errorperbit;
        sadperbit = o.sadperbit;
        seg_skip_block = o.seg_skip_block;
        actual_num_seg1_blocks = o.actual_num_seg1_blocks;
        actual_num_seg2_blocks = o.actual_num_seg2_blocks;
        cnt_zeromv = o.cnt_zeromv;
        force_zeromv_skip_for_sb = o.force_zeromv_skip_for_sb;
        force_zeromv_skip_for_blk = o.force_zeromv_skip_for_blk;
        prev_segment_id = o.prev_segment_id;
        sb_enc = o.sb_enc;
        content_state_sb = o.content_state_sb;
        EncArrays.Copy1(pred_mv_sad, o.pred_mv_sad);
        EncArrays.Copy1(pred_mv0_sad, o.pred_mv0_sad);
        EncArrays.Copy1(pred_mv1_sad, o.pred_mv1_sad);
        EncArrays.Copy1(warp_sample_info, o.warp_sample_info);
        nonrd_prune_ref_frame_search = o.nonrd_prune_ref_frame_search;
        part_search_info.CopyFrom(o.part_search_info);
        must_find_valid_partition = o.must_find_valid_partition;
        skip_mode = o.skip_mode;
        EncArrays.Copy2(thresh_freq_fact, o.thresh_freq_fact);
        pixel_gradient_info = o.pixel_gradient_info;
        EncArrays.Copy1(is_sb_gradient_cached, o.is_sb_gradient_cached);
        reuse_inter_pred = o.reuse_inter_pred;
        EncArrays.Copy1(max_mv_context, o.max_mv_context);
        mv_limits = o.mv_limits;
        EncArrays.CopyObj1(search_site_cfg_buf, o.search_site_cfg_buf);
        txfm_search_params.CopyFrom(o.txfm_search_params);
        txfm_search_info.CopyFrom(o.txfm_search_info);
        EncArrays.Copy1(color_sensitivity_sb, o.color_sensitivity_sb);
        EncArrays.Copy1(color_sensitivity_sb_g, o.color_sensitivity_sb_g);
        EncArrays.Copy1(color_sensitivity_sb_alt, o.color_sensitivity_sb_alt);
        EncArrays.Copy1(color_sensitivity, o.color_sensitivity);
        min_dist_inter_uv = o.min_dist_inter_uv;
        color_palette_thresh = o.color_palette_thresh;
        force_color_check_block_level = o.force_color_check_block_level;
        dqcoeff_buf = o.dqcoeff_buf;
        source_variance = o.source_variance;
        block_is_zero_sad = o.block_is_zero_sad;
        sb_me_partition = o.sb_me_partition;
        sb_me_block = o.sb_me_block;
        sb_col_scroll = o.sb_col_scroll;
        sb_row_scroll = o.sb_row_scroll;
        sb_me_mv = o.sb_me_mv;
        sb_force_fixed_part = o.sb_force_fixed_part;
        sb_copied_ref = o.sb_copied_ref;
        EncArrays.Copy1(pred_sse, o.pred_sse);
        try_merge_partition = o.try_merge_partition;
        src_var_info_of_4x4_sub_blocks = o.src_var_info_of_4x4_sub_blocks;
        last_set_offsets_loc = o.last_set_offsets_loc;
        palette_pixels = o.palette_pixels;
        pix_to_bottom_edge = o.pix_to_bottom_edge;
        pix_to_right_edge = o.pix_to_right_edge;
    }
}

// Allocation/copy helpers for the jagged arrays that stand in for C multi-dimensional
// arrays inside classes of this work package (p1b2). Not a libaom function.
internal static partial class EncArrays
{
    internal static T[][] New2<T>(int a, int b)
    {
        var r = new T[a][];
        for (int i = 0; i < a; i++) r[i] = new T[b];
        return r;
    }

    internal static T[][][] New3<T>(int a, int b, int c)
    {
        var r = new T[a][][];
        for (int i = 0; i < a; i++) r[i] = New2<T>(b, c);
        return r;
    }

    internal static T[][][][] New4<T>(int a, int b, int c, int d)
    {
        var r = new T[a][][][];
        for (int i = 0; i < a; i++) r[i] = New3<T>(b, c, d);
        return r;
    }

    internal static T[] NewObj1<T>(int a) where T : new()
    {
        var r = new T[a];
        for (int i = 0; i < a; i++) r[i] = new T();
        return r;
    }

    internal static T[][] NewObj2<T>(int a, int b) where T : new()
    {
        var r = new T[a][];
        for (int i = 0; i < a; i++) r[i] = NewObj1<T>(b);
        return r;
    }

    internal static void Copy1<T>(T[] dst, T[] src) => Array.Copy(src, dst, src.Length);

    internal static void Copy2<T>(T[][] dst, T[][] src)
    {
        for (int i = 0; i < src.Length; i++) Copy1(dst[i], src[i]);
    }

    internal static void Copy3<T>(T[][][] dst, T[][][] src)
    {
        for (int i = 0; i < src.Length; i++) Copy2(dst[i], src[i]);
    }

    internal static void Copy4<T>(T[][][][] dst, T[][][][] src)
    {
        for (int i = 0; i < src.Length; i++) Copy3(dst[i], src[i]);
    }

    // Element-wise CopyFrom for arrays of class-mapped C structs.
    internal static void CopyObj1(macroblock_plane[] dst, macroblock_plane[] src)
    {
        for (int i = 0; i < src.Length; i++) dst[i].CopyFrom(src[i]);
    }

    internal static void CopyObj1(search_site_config[] dst, search_site_config[] src)
    {
        for (int i = 0; i < src.Length; i++) dst[i].CopyFrom(src[i]);
    }

    internal static void CopyObj2(LV_MAP_COEFF_COST[][] dst, LV_MAP_COEFF_COST[][] src)
    {
        for (int i = 0; i < src.Length; i++)
            for (int j = 0; j < src[i].Length; j++) dst[i][j].CopyFrom(src[i][j]);
    }

    internal static void CopyObj2(LV_MAP_EOB_COST[][] dst, LV_MAP_EOB_COST[][] src)
    {
        for (int i = 0; i < src.Length; i++)
            for (int j = 0; j < src[i].Length; j++) dst[i][j].CopyFrom(src[i][j]);
    }
}
