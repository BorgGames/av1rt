// Port of libaom aom/aomcx.h
// The enumerators and #define constants are generated from the C header with the p1a generator
// helpers (tools/gen/ctables.py: emit_enums / emit_defines); the doc comments are not copied.

global using static Av1rt.Aom.aomcx;
global using aome_enc_control_id = int;
global using AOM_SCALING_MODE = int;
global using aom_tune_content = int;
global using aom_screen_detection_mode = int;
global using aom_timing_info_type_t = int;
global using aom_tune_metric = int;
global using aom_dist_metric = int;
global using AOM_SVC_FRAME_DROP_MODE = int;

namespace Av1rt.Aom;

// [port] The structs below are control payloads: the application passes their address to
// aom_codec_control and the control function reads it with va_arg(args, T *), and some
// controls write into them (AV1E_GET_ACTIVEMAP, AV1E_GET_GOP_INFO). They are classes so that the
// pointer semantics survive the trip through va_list (va_arg<T>() returns the caller's object).
// Fixed arrays are allocated with their C length.

/*!\brief  aom region of interest map
 *
 * These defines the data structures for the region of interest map
 *
 * TODO(yaowu): create a unit test for ROI map related APIs
 *
 */
// [port] AV1_COMP embeds one (cpi->roi): `public readonly aom_roi_map_t roi = new();`.
internal sealed partial class aom_roi_map_t
{
    /*! If ROI is enabled. */
    public byte enabled;
    /*! An id between 0 and 7 for each 4x4 region within a frame. */
    public Ptr<byte> roi_map;
    public uint rows;                                         /*< Number of rows. */
    public uint cols;                                         /*< Number of columns. */
    public readonly int[] delta_q = new int[AOM_MAX_SEGMENTS];   /*< Quantizer deltas. */
    public readonly int[] delta_lf = new int[AOM_MAX_SEGMENTS];  /*< Loop filter deltas. */
    public readonly int[] skip = new int[AOM_MAX_SEGMENTS];      /*< Skip this block. */
    public readonly int[] ref_frame = new int[AOM_MAX_SEGMENTS]; /*< Reference frame for this block. */
    public int delta_qp_enabled;                              /*< Delta qp feature enabled. */
    public int reference_enabled;                             /*< Reference frame feature enabled. */
    public int rdmult_delta_qp;                               /*< RD mult for delta qp feature. */
}

/*!\brief  aom active region map
 *
 * These defines the data structures for active region map
 *
 */
internal sealed partial class aom_active_map_t
{
    /*!\brief specify an on (1) or off (0) each 16x16 region within a frame */
    public Ptr<byte> active_map;
    public uint rows; /*< number of rows */
    public uint cols; /*< number of cols */
}

/*!\brief  aom image scaling mode
 *
 * This defines the data structure for image scaling mode
 *
 */
internal sealed partial class aom_scaling_mode_t
{
    public AOM_SCALING_MODE h_scaling_mode; /*< horizontal scaling mode */
    public AOM_SCALING_MODE v_scaling_mode; /*< vertical scaling mode   */
}

/*!\brief Struct for spatial and temporal layer ID */
internal sealed partial class aom_svc_layer_id_t
{
    public int spatial_layer_id;  /*< Spatial layer ID */
    public int temporal_layer_id; /*< Temporal layer ID */
}

/*!\brief Parameter type for SVC
 *
 * In the arrays of size AOM_MAX_LAYERS, the index for spatial layer `sl` and
 * temporal layer `tl` is sl * number_temporal_layers + tl.
 *
 */
internal sealed partial class aom_svc_params_t
{
    /*!Number of spatial layers */
    public int number_spatial_layers;
    /*!Number of temporal layers */
    public int number_temporal_layers;
    public readonly int[] max_quantizers = new int[AOM_MAX_LAYERS]; /*< Max Q for each layer */
    public readonly int[] min_quantizers = new int[AOM_MAX_LAYERS]; /*< Min Q for each layer */
    /*! Scaling factor-numerator */
    public readonly int[] scaling_factor_num = new int[AOM_MAX_SS_LAYERS];
    /*! Scaling factor-denominator: must be greater than or equal to the
     * numerator */
    public readonly int[] scaling_factor_den = new int[AOM_MAX_SS_LAYERS];
    /*! Target bitrate for each layer, in kilobits per second */
    public readonly int[] layer_target_bitrate = new int[AOM_MAX_LAYERS];
    /*! Frame rate factor for each temporal layer */
    public readonly int[] framerate_factor = new int[AOM_MAX_TS_LAYERS];
}

/*!\brief Parameters for setting ref frame config */
internal sealed partial class aom_svc_ref_frame_config_t
{
    // Three arrays need to be set: reference[], ref_idx[], refresh[].
    // reference[i]: is a boolean flag to indicate which of the 7 possible
    // references are used for prediction. Values are 0 (not used as reference)
    // or 1 (use as reference). The index 0 - 6 refers to the references:
    // last(0), last2(1), last3(2), golden(3), bwdref(4), altref2(5), altref(6).
    // ref_idx[i]: maps a reference to one of the 8 buffer slots, values are
    // 0 - 7. Inter frames signal all seven references in the bitstream,
    // including the unused ones (reference[i] = 0):
    // - With order hints disabled (see AV1E_SET_ENABLE_ORDER_HINT), the encoder
    //   points each unused reference to the slot of the first used reference
    //   instead, so their ref_idx[i] may point to any slot, even one holding a
    //   frame that the current frame cannot reference, e.g. due to its size.
    // - With order hints enabled, unused references also affect the decoding
    //   process (e.g. temporal motion vector projection) and are signaled as
    //   configured, so their ref_idx[i] must point to slots holding valid
    //   references for the current frame, e.g. the slot of the first used
    //   reference (usually LAST).
    // refresh[i]: is a boolean flag to indicate if buffer slot i (0 - 7) is
    // updated/refreshed with the current encoded frame. Values are 0 (no refresh)
    // or 1 (refresh). A slot can be refreshed whether or not any ref_idx[] entry
    // points to it. Key frames refresh all slots.
    // Examples for usage (for RTC encoding) are in: examples/svc_encoder_rtc.cc.
    public readonly int[] reference = new int[7]; /*< Reference flag for each of the 7 references. */
    /*! Buffer slot index (0..7) for each of 7 references indexed above. */
    public readonly int[] ref_idx = new int[7];
    public readonly int[] refresh = new int[8]; /*< Refresh flag for each of the 8 buffer slots. */
}

/*!\brief Parameters for setting ref frame compound prediction */
internal sealed partial class aom_svc_ref_frame_comp_pred_t
{
    // Use compound prediction for the ref_frame pairs GOLDEN_LAST (0),
    // LAST2_LAST (1), and ALTREF_LAST (2).
    public readonly int[] use_comp_pred = new int[3]; /*<Compound reference flag. */
}

/*!\brief The GOP structure information determined by the encoder.
 *
 * This is the type of the AV1E_GET_GOP_INFO control.
 */
internal sealed partial class aom_gop_info_t
{
    public int gop_size; /*< The number of frames of this GOP */
    /*! The coding index for each entry in the gop */
    public readonly int[] coding_index = new int[250];
    /*! The display index for each entry in the gop */
    public readonly int[] display_index = new int[250];
    /*! The layer depth for each entry in the gop */
    public readonly int[] layer_depth = new int[250];
}

// [port] aom_codec_av1_cx_algo / aom_codec_av1_cx() are defined in av1/av1_cx_iface.c. The
// AOM_CTRL_USE_TYPE(id, type) lines only declare the C argument type of each control for
// AOM_CODEC_CONTROL_TYPECHECKED; the control functions read that type with CAST<type>(id, args).
internal static partial class aomcx
{
    // aome_enc_control_id
    internal const int AOME_USE_REFERENCE = 7;
    internal const int AOME_SET_ROI_MAP = 8;
    internal const int AOME_SET_ACTIVEMAP = 9;
    internal const int AOME_SET_SCALEMODE = 11;
    internal const int AOME_SET_SPATIAL_LAYER_ID = 12;
    internal const int AOME_SET_CPUUSED = 13;
    internal const int AOME_SET_ENABLEAUTOALTREF = 14;
    internal const int AOME_SET_SHARPNESS = AOME_SET_ENABLEAUTOALTREF + 2;
    internal const int AOME_SET_STATIC_THRESHOLD = 17;
    internal const int AOME_GET_LAST_QUANTIZER = AOME_SET_STATIC_THRESHOLD + 2;
    internal const int AOME_GET_LAST_QUANTIZER_64 = 20;
    internal const int AOME_SET_ARNR_MAXFRAMES = 21;
    internal const int AOME_SET_ARNR_STRENGTH = 22;
    internal const int AOME_SET_TUNING = AOME_SET_ARNR_STRENGTH + 2;
    internal const int AOME_SET_CQ_LEVEL = 25;
    internal const int AOME_SET_MAX_INTRA_BITRATE_PCT = 26;
    internal const int AOME_SET_NUMBER_SPATIAL_LAYERS = 27;
    internal const int AV1E_SET_MAX_INTER_BITRATE_PCT = AOME_SET_MAX_INTRA_BITRATE_PCT + 2;
    internal const int AV1E_SET_GF_CBR_BOOST_PCT = 29;
    internal const int AV1E_SET_LOSSLESS = AV1E_SET_GF_CBR_BOOST_PCT + 2;
    internal const int AV1E_SET_ROW_MT = 32;
    internal const int AV1E_SET_TILE_COLUMNS = 33;
    internal const int AV1E_SET_TILE_ROWS = 34;
    internal const int AV1E_SET_ENABLE_TPL_MODEL = 35;
    internal const int AV1E_SET_ENABLE_KEYFRAME_FILTERING = 36;
    internal const int AV1E_SET_FRAME_PARALLEL_DECODING = 37;
    internal const int AV1E_SET_ERROR_RESILIENT_MODE = 38;
    internal const int AV1E_SET_S_FRAME_MODE = 39;
    internal const int AV1E_SET_AQ_MODE = 40;
    internal const int AV1E_SET_FRAME_PERIODIC_BOOST = 41;
    internal const int AV1E_SET_NOISE_SENSITIVITY = 42;
    internal const int AV1E_SET_TUNE_CONTENT = 43;
    internal const int AV1E_SET_CDF_UPDATE_MODE = 44;
    internal const int AV1E_SET_COLOR_PRIMARIES = 45;
    internal const int AV1E_SET_TRANSFER_CHARACTERISTICS = 46;
    internal const int AV1E_SET_MATRIX_COEFFICIENTS = 47;
    internal const int AV1E_SET_CHROMA_SAMPLE_POSITION = 48;
    internal const int AV1E_SET_MIN_GF_INTERVAL = 49;
    internal const int AV1E_SET_MAX_GF_INTERVAL = 50;
    internal const int AV1E_GET_ACTIVEMAP = 51;
    internal const int AV1E_SET_COLOR_RANGE = 52;
    internal const int AV1E_SET_RENDER_SIZE = 53;
    internal const int AV1E_SET_TARGET_SEQ_LEVEL_IDX = 54;
    internal const int AV1E_GET_SEQ_LEVEL_IDX = 55;
    internal const int AV1E_SET_SUPERBLOCK_SIZE = 56;
    internal const int AOME_SET_ENABLEAUTOBWDREF = 57;
    internal const int AV1E_SET_ENABLE_CDEF = 58;
    internal const int AV1E_SET_ENABLE_RESTORATION = 59;
    internal const int AV1E_SET_FORCE_VIDEO_MODE = 60;
    internal const int AV1E_SET_ENABLE_OBMC = 61;
    internal const int AV1E_SET_DISABLE_TRELLIS_QUANT = 62;
    internal const int AV1E_SET_ENABLE_QM = 63;
    internal const int AV1E_SET_QM_MIN = 64;
    internal const int AV1E_SET_QM_MAX = 65;
    internal const int AV1E_SET_QM_Y = 66;
    internal const int AV1E_SET_QM_U = 67;
    internal const int AV1E_SET_QM_V = 68;
    internal const int AV1E_SET_NUM_TG = 70;
    internal const int AV1E_SET_MTU = 71;
    internal const int AV1E_SET_ENABLE_RECT_PARTITIONS = 73;
    internal const int AV1E_SET_ENABLE_AB_PARTITIONS = 74;
    internal const int AV1E_SET_ENABLE_1TO4_PARTITIONS = 75;
    internal const int AV1E_SET_MIN_PARTITION_SIZE = 76;
    internal const int AV1E_SET_MAX_PARTITION_SIZE = 77;
    internal const int AV1E_SET_ENABLE_INTRA_EDGE_FILTER = 78;
    internal const int AV1E_SET_ENABLE_ORDER_HINT = 79;
    internal const int AV1E_SET_ENABLE_TX64 = 80;
    internal const int AV1E_SET_ENABLE_FLIP_IDTX = 81;
    internal const int AV1E_SET_ENABLE_RECT_TX = 82;
    internal const int AV1E_SET_ENABLE_DIST_WTD_COMP = 83;
    internal const int AV1E_SET_ENABLE_REF_FRAME_MVS = 84;
    internal const int AV1E_SET_ALLOW_REF_FRAME_MVS = 85;
    internal const int AV1E_SET_ENABLE_DUAL_FILTER = 86;
    internal const int AV1E_SET_ENABLE_CHROMA_DELTAQ = 87;
    internal const int AV1E_SET_ENABLE_MASKED_COMP = 88;
    internal const int AV1E_SET_ENABLE_ONESIDED_COMP = 89;
    internal const int AV1E_SET_ENABLE_INTERINTRA_COMP = 90;
    internal const int AV1E_SET_ENABLE_SMOOTH_INTERINTRA = 91;
    internal const int AV1E_SET_ENABLE_DIFF_WTD_COMP = 92;
    internal const int AV1E_SET_ENABLE_INTERINTER_WEDGE = 93;
    internal const int AV1E_SET_ENABLE_INTERINTRA_WEDGE = 94;
    internal const int AV1E_SET_ENABLE_GLOBAL_MOTION = 95;
    internal const int AV1E_SET_ENABLE_WARPED_MOTION = 96;
    internal const int AV1E_SET_ALLOW_WARPED_MOTION = 97;
    internal const int AV1E_SET_ENABLE_FILTER_INTRA = 98;
    internal const int AV1E_SET_ENABLE_SMOOTH_INTRA = 99;
    internal const int AV1E_SET_ENABLE_PAETH_INTRA = 100;
    internal const int AV1E_SET_ENABLE_CFL_INTRA = 101;
    internal const int AV1E_SET_ENABLE_SUPERRES = 102;
    internal const int AV1E_SET_ENABLE_OVERLAY = 103;
    internal const int AV1E_SET_ENABLE_PALETTE = 104;
    internal const int AV1E_SET_ENABLE_INTRABC = 105;
    internal const int AV1E_SET_ENABLE_ANGLE_DELTA = 106;
    internal const int AV1E_SET_DELTAQ_MODE = 107;
    internal const int AV1E_SET_DELTALF_MODE = 108;
    internal const int AV1E_SET_SINGLE_TILE_DECODING = 109;
    internal const int AV1E_ENABLE_MOTION_VECTOR_UNIT_TEST = 110;
    internal const int AV1E_SET_TIMING_INFO_TYPE = 111;
    internal const int AV1E_SET_FILM_GRAIN_TEST_VECTOR = 112;
    internal const int AV1E_SET_FILM_GRAIN_TABLE = 113;
    internal const int AV1E_SET_DENOISE_NOISE_LEVEL = 114;
    internal const int AV1E_SET_DENOISE_BLOCK_SIZE = 115;
    internal const int AV1E_SET_CHROMA_SUBSAMPLING_X = 116;
    internal const int AV1E_SET_CHROMA_SUBSAMPLING_Y = 117;
    internal const int AV1E_SET_REDUCED_TX_TYPE_SET = 118;
    internal const int AV1E_SET_INTRA_DCT_ONLY = 119;
    internal const int AV1E_SET_INTER_DCT_ONLY = 120;
    internal const int AV1E_SET_INTRA_DEFAULT_TX_ONLY = 121;
    internal const int AV1E_SET_QUANT_B_ADAPT = 122;
    internal const int AV1E_SET_GF_MAX_PYRAMID_HEIGHT = 123;
    internal const int AV1E_SET_MAX_REFERENCE_FRAMES = 124;
    internal const int AV1E_SET_REDUCED_REFERENCE_SET = 125;
    internal const int AV1E_SET_COEFF_COST_UPD_FREQ = 126;
    internal const int AV1E_SET_MODE_COST_UPD_FREQ = 127;
    internal const int AV1E_SET_MV_COST_UPD_FREQ = 128;
    internal const int AV1E_SET_TIER_MASK = 129;
    internal const int AV1E_SET_MIN_CR = 130;
    internal const int AV1E_SET_SVC_LAYER_ID = 131;
    internal const int AV1E_SET_SVC_PARAMS = 132;
    internal const int AV1E_SET_SVC_REF_FRAME_CONFIG = 133;
    internal const int AV1E_SET_VMAF_MODEL_PATH = 134;
    internal const int AV1E_ENABLE_EXT_TILE_DEBUG = 135;
    internal const int AV1E_ENABLE_SB_MULTIPASS_UNIT_TEST = 136;
    internal const int AV1E_SET_GF_MIN_PYRAMID_HEIGHT = 137;
    internal const int AV1E_SET_VBR_CORPUS_COMPLEXITY_LAP = 138;
    internal const int AV1E_GET_BASELINE_GF_INTERVAL = 139;
    internal const int AV1E_SET_ENABLE_DNL_DENOISING = 140;
    internal const int AV1E_SET_ENABLE_DIAGONAL_INTRA = 141;
    internal const int AV1E_SET_DV_COST_UPD_FREQ = 142;
    internal const int AV1E_SET_PARTITION_INFO_PATH = 143;
    internal const int AV1E_SET_EXTERNAL_PARTITION = 144;
    internal const int AV1E_SET_ENABLE_DIRECTIONAL_INTRA = 145;
    internal const int AV1E_SET_ENABLE_TX_SIZE_SEARCH = 146;
    internal const int AV1E_SET_SVC_REF_FRAME_COMP_PRED = 147;
    internal const int AV1E_SET_DELTAQ_STRENGTH = 148;
    internal const int AV1E_SET_LOOPFILTER_CONTROL = 149;
    internal const int AOME_GET_LOOPFILTER_LEVEL = 150;
    internal const int AV1E_SET_AUTO_INTRA_TOOLS_OFF = 151;
    internal const int AV1E_SET_RTC_EXTERNAL_RC = 152;
    internal const int AV1E_SET_FP_MT = 153;
    internal const int AV1E_SET_FP_MT_UNIT_TEST = 154;
    internal const int AV1E_GET_TARGET_SEQ_LEVEL_IDX = 155;
    internal const int AV1E_GET_NUM_OPERATING_POINTS = 156;
    internal const int AV1E_SET_SKIP_POSTPROC_FILTERING = 157;
    internal const int AV1E_ENABLE_SB_QP_SWEEP = 158;
    internal const int AV1E_SET_QUANTIZER_ONE_PASS = 159;
    internal const int AV1E_ENABLE_RATE_GUIDE_DELTAQ = 160;
    internal const int AV1E_SET_RATE_DISTRIBUTION_INFO = 161;
    internal const int AV1E_GET_LUMA_CDEF_STRENGTH = 162;
    internal const int AV1E_SET_BITRATE_ONE_PASS_CBR = 163;
    internal const int AV1E_SET_MAX_CONSEC_FRAME_DROP_CBR = 164;
    internal const int AV1E_SET_SVC_FRAME_DROP_MODE = 165;
    internal const int AV1E_SET_AUTO_TILES = 166;
    internal const int AV1E_GET_HIGH_MOTION_CONTENT_SCREEN_RTC = 167;
    internal const int AV1E_SET_POSTENCODE_DROP_RTC = 168;
    internal const int AV1E_SET_MAX_CONSEC_FRAME_DROP_MS_CBR = 169;
    internal const int AV1E_SET_ENABLE_LOW_COMPLEXITY_DECODE = 170;
    internal const int AV1E_SET_SCREEN_CONTENT_DETECTION_MODE = 171;
    internal const int AV1E_SET_ENABLE_ADAPTIVE_SHARPNESS = 172;
    internal const int AV1E_SET_EXTERNAL_RATE_CONTROL = 173;
    internal const int AV1E_GET_GOP_INFO = 174;
    internal const int AOME_SET_VALIDATE_HBD_INPUT = 175;
    internal const int AV1E_SET_MODE_REF_DELTA_ENABLED = 176;

    // AOM_SCALING_MODE
    internal const int AOME_NORMAL = 0;
    internal const int AOME_FOURFIVE = 1;
    internal const int AOME_THREEFIVE = 2;
    internal const int AOME_THREEFOUR = 3;
    internal const int AOME_ONEFOUR = 4;
    internal const int AOME_ONEEIGHT = 5;
    internal const int AOME_ONETWO = 6;
    internal const int AOME_TWOTHREE = 7;
    internal const int AOME_ONETHREE = 8;

    // aom_tune_content
    internal const int AOM_CONTENT_DEFAULT = 0;
    internal const int AOM_CONTENT_SCREEN = AOM_CONTENT_DEFAULT + 1;
    internal const int AOM_CONTENT_FILM = AOM_CONTENT_SCREEN + 1;
    internal const int AOM_CONTENT_INVALID = AOM_CONTENT_FILM + 1;

    // aom_screen_detection_mode
    internal const int AOM_SCREEN_DETECTION_STANDARD = 1;
    internal const int AOM_SCREEN_DETECTION_ANTIALIASING_AWARE = 2;

    // aom_timing_info_type_t
    internal const int AOM_TIMING_UNSPECIFIED = 0;
    internal const int AOM_TIMING_EQUAL = AOM_TIMING_UNSPECIFIED + 1;
    internal const int AOM_TIMING_DEC_MODEL = AOM_TIMING_EQUAL + 1;

    // aom_tune_metric
    internal const int AOM_TUNE_PSNR = 0;
    internal const int AOM_TUNE_SSIM = 1;
    internal const int AOM_TUNE_VMAF_WITH_PREPROCESSING = 4;
    internal const int AOM_TUNE_VMAF_WITHOUT_PREPROCESSING = 5;
    internal const int AOM_TUNE_VMAF_MAX_GAIN = 6;
    internal const int AOM_TUNE_VMAF_NEG_MAX_GAIN = 7;
    internal const int AOM_TUNE_BUTTERAUGLI = 8;
    internal const int AOM_TUNE_VMAF_SALIENCY_MAP = 9;
    internal const int AOM_TUNE_IQ = 10;
    internal const int AOM_TUNE_SSIMULACRA2 = 11;

    // aom_dist_metric
    internal const int AOM_DIST_METRIC_PSNR = 0;
    internal const int AOM_DIST_METRIC_QM_PSNR = AOM_DIST_METRIC_PSNR + 1;

    // AOM_SVC_FRAME_DROP_MODE
    internal const int AOM_LAYER_DROP = 0;
    internal const int AOM_FULL_SUPERFRAME_DROP = AOM_LAYER_DROP + 1;
    internal const int AOM_EFLAG_NO_REF_LAST = (1 << 16);
    internal const int AOM_EFLAG_NO_REF_LAST2 = (1 << 17);
    internal const int AOM_EFLAG_NO_REF_LAST3 = (1 << 18);
    internal const int AOM_EFLAG_NO_REF_GF = (1 << 19);
    internal const int AOM_EFLAG_NO_REF_ARF = (1 << 20);
    internal const int AOM_EFLAG_NO_REF_BWD = (1 << 21);
    internal const int AOM_EFLAG_NO_REF_ARF2 = (1 << 22);
    internal const int AOM_EFLAG_NO_UPD_LAST = (1 << 23);
    internal const int AOM_EFLAG_NO_UPD_GF = (1 << 24);
    internal const int AOM_EFLAG_NO_UPD_ARF = (1 << 25);
    internal const int AOM_EFLAG_NO_UPD_ENTROPY = (1 << 26);
    internal const int AOM_EFLAG_NO_REF_FRAME_MVS = (1 << 27);
    internal const int AOM_EFLAG_ERROR_RESILIENT = (1 << 28);
    internal const int AOM_EFLAG_SET_S_FRAME = (1 << 29);
    internal const int AOM_EFLAG_SET_PRIMARY_REF_NONE = (1 << 30);
    internal const int AOM_MAX_SEGMENTS = 8;
    internal const int AOM_HAVE_TUNE_IQ = 1;
    internal const int AOM_HAVE_TUNE_SSIMULACRA2 = 1;
    internal const int AOM_MAX_LAYERS = 32;
    internal const int AOM_MAX_SS_LAYERS = 4;
    internal const int AOM_MAX_TS_LAYERS = 8;

}
