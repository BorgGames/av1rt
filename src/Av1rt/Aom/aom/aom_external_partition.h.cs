// Port of libaom aom/aom_external_partition.h
// The enumerators and #define constants are generated from the C header with the p1a generator
// helpers (tools/gen/ctables.py: emit_enums / emit_defines).

global using static Av1rt.Aom.aom_external_partition;
global using aom_ext_part_decision_mode_t = int;
global using AOM_EXT_PART_FEATURE_ID = int;
global using aom_ext_part_status_t = int;
/*!\brief Abstract external partition model handler
 */
global using aom_ext_part_model_t = object;

namespace Av1rt.Aom;

/*!\defgroup aom_encoder AOMedia AOM/AV1 Encoder
 * \ingroup aom
 *
 * @{
 */
/*!\file
 * \brief Provides function pointer definitions for the external partition.
 *
 * \note The external partition API should be considered experimental. Until the
 * external partition API is declared stable, breaking changes may be made to
 * this API in a future libaom release.
 */

// [port] The external partition model is never enabled in the subset; AV1_COMP only embeds the
// (zeroed) ExtPartController. The config, stats and function table are value structs (copied by
// value: av1_ext_part_create(aom_ext_part_funcs_t funcs, aom_ext_part_config_t config)); the
// large feature and decision records are classes whose fixed arrays are allocated with their C
// length (sizes 17, 31 and 2048 have no ArrN). `const T *` callback parameters of struct type are
// `in`, `T *` are `ref`; class-typed ones are the reference.

/*!\brief Config information sent to the external partition model.
 *
 * For example, the maximum superblock size determined by the sequence header.
 */
internal partial struct aom_ext_part_config_t
{
    public int superblock_size; /*< super block size (either 64x64 or 128x128) */
}

/*!\brief Features pass to the external model to make partition decisions.
 * Specifically, features collected before NONE partition.
 * Features "f" are used to determine:
 * partition_none_allowed, partition_horz_allowed, partition_vert_allowed,
 * do_rectangular_split, do_square_split
 * Features "f_part2" are used to determine:
 * prune_horz, prune_vert.
 */
internal sealed partial class aom_partition_features_before_none_t
{
    /*! features to determine whether skip partition none and do split directly */
    public readonly float[] f = new float[AOM_EXT_PART_SIZE_DIRECT_SPLIT];
    /*! features to determine whether to prune rectangular partition */
    public readonly float[] f_part2 = new float[AOM_EXT_PART_SIZE_PRUNE_PART];
}

/*!\brief Features pass to the external model to make partition decisions.
 * Specifically, features collected after NONE partition.
 */
internal sealed partial class aom_partition_features_none_t
{
    /*! features to prune split and rectangular partition */
    public readonly float[] f = new float[AOM_EXT_PART_SIZE_PRUNE_NONE];
    /*! features to determine termination of partition */
    public readonly float[] f_terminate = new float[AOM_EXT_PART_SIZE_TERM_NONE];
}

/*!\brief Features pass to the external model to make partition decisions.
 * Specifically, features collected after SPLIT partition.
 */
internal sealed partial class aom_partition_features_split_t
{
    /*! features to determine termination of  partition */
    public readonly float[] f_terminate = new float[AOM_EXT_PART_SIZE_TERM_SPLIT];
    /*! features to determine pruning rect partition */
    public readonly float[] f_prune_rect = new float[AOM_EXT_PART_SIZE_PRUNE_RECT];
}

/*!\brief Features pass to the external model to make partition decisions.
 * Specifically, features collected after RECTANGULAR partition.
 */
internal sealed partial class aom_partition_features_rect_t
{
    /*! features to determine pruning AB partition */
    public readonly float[] f = new float[AOM_EXT_PART_SIZE_PRUNE_AB];
}

/*!\brief Features pass to the external model to make partition decisions.
 * Specifically, features collected after AB partition: HORZ_A, HORZ_B, VERT_A,
 * VERT_B.
 */
internal sealed partial class aom_partition_features_ab_t
{
    /*! features to determine pruning 4-way partition */
    public readonly float[] f = new float[AOM_EXT_PART_SIZE_PRUNE_4_WAY];
}

/*!\brief Feature id to tell the external model the current stage in partition
 * pruning and what features to use to make decisions accordingly.
 */
// (AOM_EXT_PART_FEATURE_ID constants are in the static class below.)

/*!\brief Features collected from the tpl process.
 *
 * The tpl process collects information that help measure the inter-frame
 * dependency.
 * The tpl process is computed in the unit of tpl_bsize_1d (16x16).
 * Therefore, the max number of units inside a superblock is
 * 128x128 / (16x16) = 64. Change it if the tpl process changes.
 */
internal sealed partial class aom_sb_tpl_features_t
{
    public int available;        /*< If tpl stats are available */
    public int tpl_unit_length;  /*< The block length of tpl process */
    public int num_units;        /*< The number of units inside the current superblock */
    public readonly long[] intra_cost = new long[64];  /*< The intra cost of each unit */
    public readonly long[] inter_cost = new long[64];  /*< The inter cost of each unit */
    public readonly long[] mc_dep_cost = new long[64]; /*< The motion compensated dependency cost */
}

/*!\brief Features collected from the simple motion process.
 *
 * The simple motion process collects information by applying motion compensated
 * prediction on each block.
 * The block size is 16x16, which could be changed.
 * Therefore, the max number of units inside a superblock is
 * 128x128 / (16x16) = 64. Change it if the simple motion process changes.
 */
internal sealed partial class aom_sb_simple_motion_features_t
{
    public int unit_length;  /*< The block length of the simple motion process */
    public int num_units;    /*< The number of units inside the current superblock */
    public readonly int[] block_sse = new int[64]; /*< Sum of squared error of each unit */
    public readonly int[] block_var = new int[64]; /*< Variance of each unit */
}

/*!\brief Features of each super block.
 *
 * Features collected for each super block before partition search.
 */
internal sealed partial class aom_sb_features_t
{
    /*! Features from motion search */
    public readonly aom_sb_simple_motion_features_t motion_features = new();
    /*! Features from tpl process */
    public readonly aom_sb_tpl_features_t tpl_features = new();
}

/*!\brief Features pass to the external model to make partition decisions.
 *
 * The encoder sends these features to the external model through
 * "func()" defined in .....
 *
 * NOTE: new member variables may be added to this structure in the future.
 * Once new features are finalized, bump the major version of libaom.
 */
internal sealed partial class aom_partition_features_t
{
    // Features for the current supervised multi-stage ML model.
    /*! Feature ID to indicate active features */
    public AOM_EXT_PART_FEATURE_ID id;
    /*! Features collected before NONE partition */
    public readonly aom_partition_features_before_none_t before_part_none = new();
    /*! Features collected after NONE partition */
    public readonly aom_partition_features_none_t after_part_none = new();
    /*! Features collected after SPLIT partition */
    public readonly aom_partition_features_split_t after_part_split = new();
    /*! Features collected after RECTANGULAR partition */
    public readonly aom_partition_features_rect_t after_part_rect = new();
    /*! Features collected after AB partition */
    public readonly aom_partition_features_ab_t after_part_ab = new();

    // Features for a new ML model.
    public readonly aom_sb_features_t sb_features = new(); /*< Features collected for the super block */
    public int mi_row;                      /*< Mi_row position of the block */
    public int mi_col;                      /*< Mi_col position of the block */
    public int frame_width;                 /*< Frame width */
    public int frame_height;                /*< Frame height */
    public int block_size;                  /*< As "BLOCK_SIZE" in av1/common/enums.h */
    /*!
     * Valid partition types. A bitmask is used.  "1" represents the
     * corresponding type is valid. The bitmask follows the enum order for
     * PARTITION_TYPE in "enums.h" to represent one partition type at a bit.
     * For example, 0x01 stands for only PARTITION_NONE is valid,
     * 0x09 (00...001001) stands for PARTITION_NONE and PARTITION_SPLIT are valid.
     */
    public int valid_partition_types;
    public int update_type;    /*< Frame update type, defined in ratectrl.h */
    public int qindex;         /*< Quantization index, range: [0, 255] */
    public int rdmult;         /*< Rate-distortion multiplier */
    public int pyramid_level;  /*< The level of this frame in the hierarchical structure */
    public int has_above_block;     /*< Has above neighbor block */
    public int above_block_width;   /*< Width of the above block, -1 if not exist */
    public int above_block_height;  /*< Height of the above block, -1 if not exist */
    public int has_left_block;      /*< Has left neighbor block */
    public int left_block_width;    /*< Width of the left block, -1 if not exist */
    public int left_block_height;   /*< Height of the left block, -1 if not exist */
    /*!
     * The following parameters are collected from applying simple motion search.
     * Sum of squared error (SSE) and variance of motion compensated residual
     * are good indicators of block partitioning.
     * If a block is a square, we also apply motion search for its 4 sub blocks.
     * If not a square, their values are -1.
     * If a block is able to split horizontally, we apply motion search and get
     * stats for horizontal blocks. If not, their values are -1.
     * If a block is able to split vertically, we apply motion search and get
     * stats for vertical blocks. If not, their values are -1.
     */
    public uint block_sse;                              /*< SSE of motion compensated residual */
    public uint block_var;                              /*< Variance of motion compensated residual */
    public readonly uint[] sub_block_sse = new uint[4];   /*< SSE of sub blocks. */
    public readonly uint[] sub_block_var = new uint[4];   /*< Variance of sub blocks. */
    public readonly uint[] horz_block_sse = new uint[2];  /*< SSE of horz sub blocks */
    public readonly uint[] horz_block_var = new uint[2];  /*< Variance of horz sub blocks */
    public readonly uint[] vert_block_sse = new uint[2];  /*< SSE of vert sub blocks */
    public readonly uint[] vert_block_var = new uint[2];  /*< Variance of vert sub blocks */
    /*!
     * The following parameters are calculated from tpl model.
     * If tpl model is not available, their values are -1.
     */
    public long tpl_intra_cost;   /*< Intra cost, ref to "TplDepStats" in tpl_model.h */
    public long tpl_inter_cost;   /*< Inter cost in tpl model */
    public long tpl_mc_dep_cost;  /*< Motion compensated dependency cost in tpl model */
}

/*!\brief Partition decisions received from the external model.
 *
 * The encoder receives partition decisions and encodes the superblock
 * with the given partition type.
 * The encoder receives it from "func()" define in ....
 *
 * NOTE: new member variables may be added to this structure in the future.
 * Once new features are finalized, bump the major version of libaom.
 */
internal sealed partial class aom_partition_decision_t
{
    // Decisions for directly set partition types
    public int is_final_decision;  /*< The flag whether it's the final decision */
    public int num_nodes;          /*< The number of leaf nodes */
    public readonly int[] partition_decision = new int[2048]; /*< Partition decisions */
    public int current_decision;   /*< Partition decision for the current block */

    // Decisions for partition type pruning
    public int terminate_partition_search;  /*< Terminate further partition search */
    public int partition_none_allowed;      /*< Allow partition none type */
    public readonly int[] partition_rect_allowed = new int[2]; /*< Allow rectangular partitions */
    public int do_rectangular_split;        /*< Try rectangular split partition */
    public int do_square_split;             /*< Try square split partition */
    public readonly int[] prune_rect_part = new int[2]; /*< Prune rectangular partition */
    public int horza_partition_allowed;     /*< Allow HORZ_A partition */
    public int horzb_partition_allowed;     /*< Allow HORZ_B partition */
    public int verta_partition_allowed;     /*< Allow VERT_A partition */
    public int vertb_partition_allowed;     /*< Allow VERT_B partition */
    public int partition_horz4_allowed;     /*< Allow HORZ4 partition */
    public int partition_vert4_allowed;     /*< Allow VERT4 partition */
}

/*!\brief Encoding stats for the given partition decision.
 *
 * The encoding stats collected by encoding the superblock with the
 * given partition types.
 * The encoder sends the stats to the external model for training
 * or inference through "func()" defined in ....
 */
internal partial struct aom_partition_stats_t
{
    public int rate;    /*< Rate cost of the block */
    public long dist;   /*< Distortion of the block */
    public long rdcost; /*< Rate-distortion cost of the block */
}

/*!\brief Callback of creating an external partition model.
 *
 * The callback is invoked by the encoder to create an external partition
 * model.
 *
 * \param[in] priv Callback's private data
 * \param[in] part_config Config information pointer for model creation
 * \param[out] ext_part_model Pointer to the model
 */
internal delegate aom_ext_part_status_t aom_ext_part_create_model_fn_t(
    object priv, in aom_ext_part_config_t part_config, ref aom_ext_part_model_t ext_part_model);

/*!\brief Callback of sending features to the external partition model.
 *
 * The callback is invoked by the encoder to send features to the external
 * partition model.
 *
 * \param[in] ext_part_model The external model
 * \param[in] part_features Pointer to the features
 */
internal delegate aom_ext_part_status_t aom_ext_part_send_features_fn_t(
    aom_ext_part_model_t ext_part_model, aom_partition_features_t part_features);

/*!\brief Callback of receiving partition decisions from the external
 * partition model.
 *
 * The callback is invoked by the encoder to receive partition decisions from
 * the external partition model.
 *
 * \param[in] ext_part_model The external model
 * \param[in] ext_part_decision Pointer to the partition decisions
 */
internal delegate aom_ext_part_status_t aom_ext_part_get_decision_fn_t(
    aom_ext_part_model_t ext_part_model, aom_partition_decision_t ext_part_decision);

/*!\brief Callback of sending stats to the external partition model.
 *
 * The callback is invoked by the encoder to send encoding stats to
 * the external partition model.
 *
 * \param[in] ext_part_model The external model
 * \param[in] ext_part_stats Pointer to the encoding stats
 */
internal delegate aom_ext_part_status_t aom_ext_part_send_partition_stats_fn_t(
    aom_ext_part_model_t ext_part_model, in aom_partition_stats_t ext_part_stats);

/*!\brief Callback of deleting the external partition model.
 *
 * The callback is invoked by the encoder to delete the external partition
 * model.
 *
 * \param[in] ext_part_model The external model
 */
internal delegate aom_ext_part_status_t aom_ext_part_delete_model_fn_t(aom_ext_part_model_t ext_part_model);

/*!\brief Callback function set for external partition model.
 *
 * Uses can enable external partition model by registering a set of
 * callback functions with the flag: AV1E_SET_EXTERNAL_PARTITION_MODEL
 */
internal partial struct aom_ext_part_funcs_t
{
    /*!
     * Create an external partition model.
     */
    public aom_ext_part_create_model_fn_t create_model;

    /*!
     * Send features to the external partition model to make partition decisions.
     */
    public aom_ext_part_send_features_fn_t send_features;

    /*!
     * Get partition decisions from the external partition model.
     */
    public aom_ext_part_get_decision_fn_t get_partition_decision;

    /*!
     * Send stats of the current partition to the external model.
     */
    public aom_ext_part_send_partition_stats_fn_t send_partition_stats;

    /*!
     * Delete the external partition model.
     */
    public aom_ext_part_delete_model_fn_t delete_model;

    /*!
     * The decision mode of the model.
     */
    public aom_ext_part_decision_mode_t decision_mode;

    /*!
     * Private data for the external partition model.
     */
    public object priv;
}

internal static partial class aom_external_partition
{
    /*!\brief Current ABI version number
     *
     * \internal
     * If this file is altered in any way that changes the ABI, this value
     * must be bumped. Examples include, but are not limited to, changing
     * types, removing or reassigning enums, adding/removing/rearranging
     * fields to structures.
     */
    internal const int AOM_EXT_PART_ABI_VERSION = 8;

    /*!\brief Number of features to determine whether to skip partition none and
     * do partition split directly. The same as "FEATURE_SIZE_SMS_SPLIT".
     */
    internal const int AOM_EXT_PART_SIZE_DIRECT_SPLIT = 17;

    /*!\brief Number of features to use simple motion search to prune out
     * rectangular partition.
     * The same as "FEATURE_SIZE_SMS_PRUNE_PART".
     */
    internal const int AOM_EXT_PART_SIZE_PRUNE_PART = 25;

    /*!\brief Number of features to prune split and rectangular partition
     * after PARTITION_NONE.
     */
    internal const int AOM_EXT_PART_SIZE_PRUNE_NONE = 4;

    /*!\brief Number of features to terminates partition after partition none using
     * simple_motion_search features and the rate, distortion, and rdcost of
     * PARTITION_NONE. The same as "FEATURE_SIZE_SMS_TERM_NONE".
     */
    internal const int AOM_EXT_PART_SIZE_TERM_NONE = 28;

    /*!\brief Number of features to terminates partition after partition split.
     */
    internal const int AOM_EXT_PART_SIZE_TERM_SPLIT = 31;

    /*!\brief Number of features to prune rectangular partition using stats
     * collected after partition split.
     */
    internal const int AOM_EXT_PART_SIZE_PRUNE_RECT = 9;

    /*!\brief Number of features to prune AB partition using stats
     * collected after rectangular partition..
     */
    internal const int AOM_EXT_PART_SIZE_PRUNE_AB = 10;

    /*!\brief Number of features to prune 4-way partition using stats
     * collected after AB partition.
     */
    internal const int AOM_EXT_PART_SIZE_PRUNE_4_WAY = 18;

    /*!\brief Decision mode of the external partition model.
     * AOM_EXT_PART_WHOLE_TREE: the external partition model should provide the
     * whole partition tree for the superblock.
     *
     * AOM_EXT_PART_RECURSIVE: the external partition model provides the partition
     * decision of the current block only. The decision process starts from
     * the superblock size, down to the smallest block size (4x4) recursively.
     */
    // aom_ext_part_decision_mode_t
    internal const int AOM_EXT_PART_WHOLE_TREE = 0;
    internal const int AOM_EXT_PART_RECURSIVE = 1;

    // AOM_EXT_PART_FEATURE_ID
    internal const int AOM_EXT_PART_FEATURE_BEFORE_NONE = 0;
    internal const int AOM_EXT_PART_FEATURE_BEFORE_NONE_PART2 = 1;
    internal const int AOM_EXT_PART_FEATURE_AFTER_NONE = 2;
    internal const int AOM_EXT_PART_FEATURE_AFTER_NONE_PART2 = 3;
    internal const int AOM_EXT_PART_FEATURE_AFTER_SPLIT = 4;
    internal const int AOM_EXT_PART_FEATURE_AFTER_SPLIT_PART2 = 5;
    internal const int AOM_EXT_PART_FEATURE_AFTER_RECT = 6;
    internal const int AOM_EXT_PART_FEATURE_AFTER_AB = 7;

    /*!\brief The status of external partition model.
     * AOM_EXT_PART_OK: no error,
     * AOM_EXT_PART_ERROR: general error,
     * AOM_EXT_PART_TEST: called from tests.
     */
    // aom_ext_part_status_t
    internal const int AOM_EXT_PART_OK = 0;
    internal const int AOM_EXT_PART_ERROR = 1;
    internal const int AOM_EXT_PART_TEST = 2;
}
