// Port of libaom aom/aom_ext_ratectrl.h
// The enumerators and #define constants are generated from the C header with the p1a generator
// helpers (tools/gen/ctables.py: emit_enums / emit_defines).

global using static Av1rt.Aom.aom_ext_ratectrl;
global using aom_rc_type_t = int;
global using aom_ext_rc_mode_t = int;
global using aom_rc_frame_update_type_t = int;
global using aom_rc_ref_name_t = int;
global using aom_rc_status_t = int;
/*!\brief Abstract rate control model handler
 *
 * The encoder will receive the model handler from create_model() defined in
 * aom_rc_funcs_t.
 */
global using aom_rc_model_t = object;

namespace Av1rt.Aom;

/*!\file
 * \brief Defines structures and callbacks needed for external rate control.
 *
 */

// [port] External rate control is never enabled in the subset; AV1_COMP only embeds the
// (zeroed) AOM_EXT_RATECTRL. The types are ported so that its fields and the av1_ext_ratectrl.h
// prototypes resolve. All are value structs: AOM_EXT_RATECTRL copies them by value
// (av1_extrc_create(aom_rc_funcs_t funcs, aom_rc_config_t ratectrl_config, ...)) and
// av1_extrc_init clears it with memset. `void *priv` is object; pointers to arrays are Ptr;
// `const T *` callback parameters are `in`, `T *` are `ref`.

/*!\brief Superblock parameters */
internal partial struct aom_sb_params
{
    public int q_index; /*< Quantizer step index [0..255]*/
    public int rdmult;  /*< Superblock level Lagrangian multiplier*/
}

/*!\brief Encode frame decision made by the external rate control model
 *
 * The encoder will receive the decision from the external rate control model
 * through get_encodeframe_decision() defined in aom_rc_funcs_t.
 *
 * If q_index = AOM_DEFAULT_Q or rdmult = AOM_DEFAULT_RDMULT, then the encoder
 * will use the default q/rdmult to encode the frame.
 */
internal partial struct aom_rc_encodeframe_decision_t
{
    public int q_index; /*< Required: Quantizer step index [0..255]*/
    public int rdmult;  /*< Required: Frame level Lagrangian multiplier*/
    /*!
     * Optional: Superblock quantization parameters
     * It is zero initialized by default. It will be set for key and ARF frames
     * but not leaf frames.
     */
    public Ptr<int> use_delta_q;
    public Ptr<aom_sb_params> sb_params_list;
}

/*!\brief Information for the frame to be encoded.
 *
 * The encoder will send the information to external rate control model to
 * obtain an encoding decision.
 * The encoder will receive the decision from the external rate control model
 * through get_encodeframe_decision() defined in aom_rc_funcs_t.
 */
internal partial struct aom_rc_encodeframe_info_t
{
    /*!
     * 0: Key frame
     * 1: Inter frame
     * 2: Alternate reference frame
     * 3: Overlay frame
     * 4: Golden frame
     */
    public int frame_type;
    public int show_index;   /*< display index, starts from zero*/
    public int coding_index; /*< coding index, starts from zero*/
    /*!
     * index of the current frame in this group of picture, starts from zero.
     */
    public int gop_index;
    public Arr8<int> ref_frame_coding_indexes; /*< three reference frames' coding indices*/
    /*!
     * The validity of the three reference frames.
     * 0: Invalid
     * 1: Valid
     */
    public Arr8<int> ref_frame_valid_list;
    /*!
     * The length of the current GOP.
     */
    public int gop_size;
    /*!
     * Whether the current GOP uses an alt ref.
     */
    public int use_alt_ref;
}

/*!\brief Frame coding result
 *
 * The encoder will send the result to the external rate control model through
 * update_encodeframe_result() defined in aom_rc_funcs_t.
 */
internal partial struct aom_rc_encodeframe_result_t
{
    public long bit_count;             /*< number of bits spent on coding the frame*/
    public int actual_encoding_qindex; /*< the actual qindex used to encode the frame*/
}

/*!\brief First pass frame stats
 * This is a mirror of FIRSTPASS_STATS except that spatial_layer_id is omitted
 */
internal partial struct aom_rc_frame_stats_t
{
    /*!
     * Frame number in display order, if stats are for a single frame.
     * No real meaning for a collection of frames.
     */
    public double frame;
    /*!
     * Weight assigned to this frame (or total weight for the collection of
     * frames) currently based on intra factor and brightness factor. This is used
     * to distribute bits between easier and harder frames.
     */
    public double weight;
    /*!
     * Intra prediction error.
     */
    public double intra_error;
    /*!
     * Average wavelet energy computed using Discrete Wavelet Transform (DWT).
     */
    public double frame_avg_wavelet_energy;
    /*!
     * Best of intra pred error and inter pred error using last frame as ref.
     */
    public double coded_error;
    /*!
     * Best of intra pred error and inter pred error using golden frame as ref.
     */
    public double sr_coded_error;
    /*!
     * Percentage of blocks with inter pred error < intra pred error.
     */
    public double pcnt_inter;
    /*!
     * Percentage of blocks using (inter prediction and) non-zero motion vectors.
     */
    public double pcnt_motion;
    /*!
     * Percentage of blocks where golden frame was better than last or intra:
     * inter pred error using golden frame < inter pred error using last frame and
     * inter pred error using golden frame < intra pred error
     */
    public double pcnt_second_ref;
    /*!
     * Percentage of blocks where intra and inter prediction errors were very
     * close.
     */
    public double pcnt_neutral;
    /*!
     * Percentage of blocks that have intra error < inter error and inter error <
     * LOW_I_THRESH
     * - bit_depth 8: LOW_I_THRESH = 24000
     * - bit_depth 10: LOW_I_THRESH = 24000 << 4
     * - bit_depth 12: LOW_I_THRESH = 24000 << 8
     */
    public double intra_skip_pct;
    /*!
     * Image mask rows top and bottom.
     */
    public double inactive_zone_rows;
    /*!
     * Image mask columns at left and right edges.
     */
    public double inactive_zone_cols;
    /*!
     * Mean of row motion vectors.
     */
    public double MVr;
    /*!
     * Mean of absolute value of row motion vectors.
     */
    public double mvr_abs;
    /*!
     * Mean of column motion vectors.
     */
    public double MVc;
    /*!
     * Mean of absolute value of column motion vectors.
     */
    public double mvc_abs;
    /*!
     * Variance of row motion vectors.
     */
    public double MVrv;
    /*!
     * Variance of column motion vectors.
     */
    public double MVcv;
    /*!
     * Value in range [-1,1] indicating fraction of row and column motion vectors
     * that point inwards (negative MV value) or outwards (positive MV value).
     * For example, value of 1 indicates, all row/column MVs are inwards.
     */
    public double mv_in_out_count;
    /*!
     * Count of unique non-zero motion vectors.
     */
    public double new_mv_count;
    /*!
     * Duration of the frame / collection of frames.
     */
    public double duration;
    /*!
     * 1.0 if stats are for a single frame, or
     * number of frames whose stats are accumulated.
     */
    public double count;
    /*!
     * standard deviation for (dc - mean_of_dcs) for a frame.
     */
    public double raw_error_stdev;
    /*!
     * Whether the frame contains a flash
     */
    public long is_flash;
    /*!
     * Estimated noise variance
     */
    public double noise_var;
    /*!
     * Correlation coefficient with the previous frame
     */
    public double cor_coeff;
    /*!
     * log of intra_error
     */
    public double log_intra_error;
    /*!
     * log of coded_error
     */
    public double log_coded_error;
}

/*!\brief Collection of first pass frame stats
 */
internal partial struct aom_rc_firstpass_stats_t
{
    /*!
     * Pointer to first pass frame stats.
     * The pointed array of aom_rc_frame_stats_t should have length equal to
     * number of show frames in the video.
     */
    public Ptr<aom_rc_frame_stats_t> frame_stats;
    /*!
     * Number of show frames in the video.
     */
    public int num_frames;
}

/*!\brief Encode config sent to external rate control model
 */
internal partial struct aom_rc_config_t
{
    public int frame_width;      /*< frame width */
    public int frame_height;     /*< frame height */
    public int show_frame_count; /*< number of visible frames in the video */
    public int max_gf_interval;  /*< max GOP size in number of show frames */
    public int min_gf_interval;  /*< min GOP size in number of show frames */
    /*!
     * Target bitrate in kilobytes per second
     */
    public int target_bitrate_kbps;
    public int frame_rate_num; /*< numerator of frame rate */
    public int frame_rate_den; /*< denominator of frame rate */
    /*!
     * The following fields are only for external rate control models that support
     * different rate control modes.
     */
    public aom_ext_rc_mode_t rc_mode; /*< Q mode or VBR mode */
    public int overshoot_percent;     /*< for VBR mode only */
    public int undershoot_percent;    /*< for VBR mode only */
    public int min_base_q_index;      /*< for VBR mode only */
    public int max_base_q_index;      /*< for VBR mode only */
    public int base_qp;               /*< base QP for leaf frames, 0-255 */
    public int superblock_size;       /*< Superblock size */
}

/*!\brief Control what ref frame to use and its index.
 */
internal partial struct aom_rc_ref_frame_t
{
    /*!
     * Ref frame index. Its valid range is [0, 7].
     */
    public Arr8<int> index;
    /*!
     * Ref frame name. Its valid range is [0, 7].
     */
    public Arr8<aom_rc_ref_name_t> name;
}

/*!\brief Control what ref frame to use and its index.
 */
internal partial struct aom_rc_single_ref_frame_t
{
    /*!
     * Ref frame index. Its valid range is [0, 7].
     */
    public int index;
    /*!
     * Ref frame name.
     */
    public aom_rc_ref_name_t name;
}

/*!\brief The decision made by the external rate control model to set the
 * group of picture.
 */
internal partial struct aom_rc_gop_frame_t
{
    public int order_idx;   /*< frame index in the GOP */
    public int coding_idx;  /*< coding index of the frame in the GOP */
    public int display_idx; /*< The display index of this frame in this GOP.
                             * Not set by the external RC model. */
    public int order_hint;  /*< order hint */
    /*!
     * global order index of the frame in the video.
     */
    public int global_order_idx;
    /*!
     * global coding index of the frame in the video.
     */
    public int global_coding_idx;
    public int is_key_frame; /*< whether this is a key frame */
    public aom_rc_frame_update_type_t update_type; /*< frame update type */
    public int colocated_ref_idx; /*< Index of colocated reference frame */
    public int update_ref_idx;    /*< Index of reference frame to update */
    /*!
     * Number of extra frames to be refreshed.
     */
    public int extra_refresh_frame_flags;
    public aom_rc_ref_frame_t ref_frame_list; /*< reference frame list */
    public int layer_depth;                   /*< layer depth of the frame */
    public aom_rc_single_ref_frame_t primary_ref_frame; /*< primary ref frame */
}

/*!\brief The decision made by the external rate control model to set the
 * group of picture.
 */
internal partial struct aom_rc_gop_decision_t
{
    public int show_frame_count;         /*< Number of show frames in this GOP */
    public int order_hint_offset;        /*< order hint offset of the GOP */
    public int global_order_idx_offset;  /*< global order index offset */
    public int global_coding_idx_offset; /*< global coding index offset */
    public int is_scene_cut;             /*< whether the GOP starts at a scene cut */
    /*!
     * The q value ratio of the frames in this GOP with respect to the base q.
     */
    public double base_q_ratio;
    public Ptr<aom_rc_gop_frame_t> gop_frame_list; /*< list of frames in the GOP */
    public int gop_frame_count;                     /*< number of frames in the GOP */
}

/*!\brief The decision made by the external rate control model to set the
 * key frame location and the show frame count in the key frame group
 */
internal partial struct aom_rc_key_frame_decision_t
{
    public int key_frame_show_index; /*< This key frame's show index in the video */
    public int key_frame_group_size; /*< Show frame count of this key frame group */
}

/*!\brief Create an external rate control model callback prototype
 *
 * This callback is invoked by the encoder to create an external rate control
 * model.
 *
 * \param[in]  priv                Callback's private data
 * \param[in]  ratectrl_config     Pointer to aom_rc_config_t
 * \param[out] rate_ctrl_model_ptr Pointer to aom_rc_model_t
 */
internal delegate aom_rc_status_t aom_rc_create_model_cb_fn_t(object priv, in aom_rc_config_t ratectrl_config,
                                                              ref aom_rc_model_t rate_ctrl_model_ptr);

/*!\brief Send first pass stats to the external rate control model callback
 * prototype
 *
 * \param[in]  rate_ctrl_model    rate control model
 * \param[in]  first_pass_stats   first pass stats
 */
internal delegate aom_rc_status_t aom_rc_send_firstpass_stats_cb_fn_t(
    aom_rc_model_t rate_ctrl_model, in aom_rc_firstpass_stats_t first_pass_stats);

/*!\brief Send TPL stats for the current GOP to the external rate control model
 * callback prototype
 *
 * \param[in]  rate_ctrl_model     rate control model
 * \param[in]  extrc_tpl_gop_stats TPL stats for the current GOP
 */
internal delegate aom_rc_status_t aom_rc_send_tpl_gop_stats_cb_fn_t(
    aom_rc_model_t rate_ctrl_model, in AomTplGopStats extrc_tpl_gop_stats);

/*!\brief Receive encode frame decision callback prototype
 *
 * \param[in]  rate_ctrl_model  rate control model
 * \param[in]  frame_gop_index  index of the frame in the GOP
 * \param[out] frame_decision   encode decision of the coding frame
 */
internal delegate aom_rc_status_t aom_rc_get_encodeframe_decision_cb_fn_t(
    aom_rc_model_t rate_ctrl_model, int frame_gop_index,
    ref aom_rc_encodeframe_decision_t frame_decision);

/*!\brief Update encode frame result callback prototype
 *
 * \param[in]  rate_ctrl_model     rate control model
 * \param[out] encode_frame_result encode result of the coding frame
 */
internal delegate aom_rc_status_t aom_rc_update_encodeframe_result_cb_fn_t(
    aom_rc_model_t rate_ctrl_model, in aom_rc_encodeframe_result_t encode_frame_result);

/*!\brief Get the key frame decision from the external rate control model.
 *
 * \param[in]  rate_ctrl_model    rate control model
 * \param[out] key_frame_decision key frame decision from the model
 */
internal delegate aom_rc_status_t aom_rc_get_key_frame_decision_cb_fn_t(
    aom_rc_model_t rate_ctrl_model, ref aom_rc_key_frame_decision_t key_frame_decision);

/*!\brief Get the GOP structure from the external rate control model.
 *
 * \param[in]  rate_ctrl_model    rate control model
 * \param[out] gop_decision       GOP structure decision from the model
 */
internal delegate aom_rc_status_t aom_rc_get_gop_decision_cb_fn_t(aom_rc_model_t rate_ctrl_model,
                                                                  ref aom_rc_gop_decision_t gop_decision);

/*!\brief Get the frame rdmult from the external rate control model.
 *
 * \param[in]  rate_ctrl_model rate control model
 * \param[in]  frame_info      information collected from the encoder
 * \param[out] rdmult          frame rate-distortion multiplier from the model
 */
internal delegate aom_rc_status_t aom_rc_get_frame_rdmult_cb_fn_t(
    aom_rc_model_t rate_ctrl_model, in aom_rc_encodeframe_info_t frame_info, ref int rdmult);

/*!\brief Delete the external rate control model callback prototype
 *
 * This callback is invoked by the encoder to delete the external rate control
 * model.
 *
 * \param[in]  rate_ctrl_model     rate control model
 */
internal delegate aom_rc_status_t aom_rc_delete_model_cb_fn_t(aom_rc_model_t rate_ctrl_model);

/*!\brief Callback function set for external rate control.
 *
 * The user can enable external rate control by registering
 * a set of callback functions with the codec control flag
 * #AV1E_SET_EXTERNAL_RATE_CONTROL.
 */
internal partial struct aom_rc_funcs_t
{
    /*!
     * The rate control type of this API.
     */
    public aom_rc_type_t rc_type;
    /*!
     * Create an external rate control model.
     */
    public aom_rc_create_model_cb_fn_t create_model;
    /*!
     * Send first pass stats to the external rate control model.
     */
    public aom_rc_send_firstpass_stats_cb_fn_t send_firstpass_stats;
    /*!
     * Send TPL stats for each GOP to the external rate control model.
     */
    public aom_rc_send_tpl_gop_stats_cb_fn_t send_tpl_gop_stats;
    /*!
     * Get encodeframe decision from the external rate control model.
     */
    public aom_rc_get_encodeframe_decision_cb_fn_t get_encodeframe_decision;
    /*!
     * Update encodeframe result to the external rate control model.
     */
    public aom_rc_update_encodeframe_result_cb_fn_t update_encodeframe_result;
    /*!
     * Get key frame decision from the external rate control model.
     */
    public aom_rc_get_key_frame_decision_cb_fn_t get_key_frame_decision;
    /*!
     * Get GOP decisions from the external rate control model.
     */
    public aom_rc_get_gop_decision_cb_fn_t get_gop_decision;
    /*!
     * Get rdmult for the frame from the external rate control model.
     */
    public aom_rc_get_frame_rdmult_cb_fn_t get_frame_rdmult;
    /*!
     * Delete the external rate control model.
     */
    public aom_rc_delete_model_cb_fn_t delete_model;
    /*!
     * Private data for the external rate control model.
     */
    public object priv;
}

internal static partial class aom_ext_ratectrl
{
    /*!\brief Current ABI version number
     *
     * \internal
     * If this file is altered in any way that changes the ABI, this value
     * must be bumped. Examples include, but are not limited to, changing
     * types, removing or reassigning enums, adding/removing/rearranging
     * fields to structures.
     */
    internal const int AOM_EXT_RATECTRL_ABI_VERSION = (2 + AOM_TPL_ABI_VERSION);

    /*!\brief Corresponds to MAX_STATIC_GF_GROUP_LENGTH defined in ratectrl.h
     */
    internal const int AOM_RC_MAX_STATIC_GF_GROUP_LENGTH = 250;

    /*!\brief Max number of ref frames returned by the external RC.
     *
     * Corresponds to MAX_REF_FRAMES defined in av1_ext_ratectrl.h.
     */
    internal const int AOM_RC_MAX_REF_FRAMES = 8;

    /*!\brief The type of the external rate control.
     *
     * This controls what encoder parameters are determined by the external rate
     * control.
     */
    // aom_rc_type_t
    /*!
     * The external rate control doesn't determine anything.
     * This mode is used as baseline.
     */
    internal const int AOM_RC_NONE = 0;
    /*!
     * The external rate control model determines the quantization parameter (QP)
     * for each frame.
     */
    internal const int AOM_RC_QP = 1 << 0;
    /*!
     * The external rate control model determines the group of picture (GOP) of the
     * video sequence.
     */
    internal const int AOM_RC_GOP = 1 << 1;
    /*!
     * The external rate control model determines the rate-distortion multiplier
     * (rdmult) for the current frame.
     */
    internal const int AOM_RC_RDMULT = 1 << 2;
    /*!
     * The external rate control model determines both QP and GOP.
     */
    internal const int AOM_RC_GOP_QP = AOM_RC_QP | AOM_RC_GOP;
    /*!
     * The external rate control model determines the QP, GOP and the rdmult.
     */
    internal const int AOM_RC_GOP_QP_RDMULT = AOM_RC_QP | AOM_RC_GOP | AOM_RC_RDMULT;

    /*!\brief The rate control mode for the external rate control model.
     */
    // aom_ext_rc_mode_t
    internal const int AOM_RC_QMODE = 0;
    internal const int AOM_RC_VBR = 1;
    internal const int AOM_RC_CQ = 2;

    /*!\brief Corresponds to FRAME_UPDATE_TYPE defined in ratectrl.h.
     */
    // aom_rc_frame_update_type_t
    internal const int AOM_RC_INVALID_UPDATE_TYPE = -1;
    internal const int AOM_RC_KF_UPDATE = 0;
    internal const int AOM_RC_LF_UPDATE = 1;
    internal const int AOM_RC_GF_UPDATE = 2;
    internal const int AOM_RC_ARF_UPDATE = 3;
    internal const int AOM_RC_OVERLAY_UPDATE = 4;
    internal const int AOM_RC_INTNL_OVERLAY_UPDATE = 5;
    internal const int AOM_RC_INTNL_ARF_UPDATE = 6;

    /*!\brief Name for the ref frames returned by ext RC.
     *
     * Corresponds to the ref frames defined in enums.h.
     */
    // aom_rc_ref_name_t
    internal const int AOM_RC_INVALID_REF_FRAME = -1;
    internal const int AOM_RC_INTRA_FRAME = 0;
    internal const int AOM_RC_LAST_FRAME = 1;
    internal const int AOM_RC_LAST2_FRAME = 2;
    internal const int AOM_RC_LAST3_FRAME = 3;
    internal const int AOM_RC_GOLDEN_FRAME = 4;
    internal const int AOM_RC_BWDREF_FRAME = 5;
    internal const int AOM_RC_ALTREF2_FRAME = 6;
    internal const int AOM_RC_ALTREF_FRAME = 7;

    /*!\brief The max number of arf layers.
     */
    internal const int AOM_RC_MAX_ARF_LAYERS = 6;

    internal const int AOM_DEFAULT_Q = -1;
    internal const int AOM_DEFAULT_RDMULT = -1;

    /*!\brief Rate control status
     */
    // aom_rc_status_t
    internal const int AOM_RC_OK = 0;
    internal const int AOM_RC_ERROR = 1;
}
