// Port of libaom av1/av1_cx_iface.c

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

// [port] A struct: C copies it by value (`struct av1_extracfg extra_cfg = ctx->extra_cfg;`,
// `ctx->extra_cfg = *extra_cfg;`). The `const char *` members are strings.
internal partial struct av1_extracfg
{
    public int cpu_used;
    public uint enable_auto_alt_ref;
    public uint enable_auto_bwd_ref;
    public uint noise_sensitivity;
    public uint sharpness;
    public uint enable_adaptive_sharpness;
    public uint static_thresh;
    public uint row_mt;
    public uint fp_mt;
    public uint tile_columns;
    public uint tile_rows;
    public uint auto_tiles;
    public uint enable_tpl_model;
    public uint enable_keyframe_filtering;
    public uint arnr_max_frames;
    public uint arnr_strength;
    public uint min_gf_interval;
    public uint max_gf_interval;
    public uint gf_min_pyr_height;
    public uint gf_max_pyr_height;
    public aom_tune_metric tuning;
    public string vmaf_model_path;
    public string partition_info_path;
    public uint enable_rate_guide_deltaq;
    public string rate_distribution_info;
    public aom_dist_metric dist_metric;
    public uint cq_level;  // constrained quality level
    public uint rc_max_intra_bitrate_pct;
    public uint rc_max_inter_bitrate_pct;
    public uint gf_cbr_boost_pct;
    public uint lossless;
    public uint enable_cdef;
    public uint enable_restoration;
    public uint force_video_mode;
    public uint enable_obmc;
    public uint disable_trellis_quant;
    public uint enable_qm;
    public uint qm_y;
    public uint qm_u;
    public uint qm_v;
    public uint qm_min;
    public uint qm_max;
    public uint num_tg;
    public uint mtu_size;

    public aom_timing_info_type_t timing_info_type;
    public uint frame_parallel_decoding_mode;
    public int enable_dual_filter;
    public uint enable_chroma_deltaq;
    public byte aq_mode;  // [port] AQ_MODE is UENUM1BYTE: uint8_t storage
    public byte deltaq_mode;  // [port] DELTAQ_MODE is UENUM1BYTE: uint8_t storage
    public uint deltaq_strength;
    public int deltalf_mode;
    public uint frame_periodic_boost;
    public aom_tune_content content;
    public aom_color_primaries_t color_primaries;
    public aom_transfer_characteristics_t transfer_characteristics;
    public aom_matrix_coefficients_t matrix_coefficients;
    public aom_chroma_sample_position_t chroma_sample_position;
    public int color_range;
    public int render_width;
    public int render_height;
    public aom_superblock_size_t superblock_size;
    public uint single_tile_decoding;
    public int error_resilient_mode;
    public int s_frame_mode;

    public int film_grain_test_vector;
    public string film_grain_table_filename;
    public uint motion_vector_unit_test;
    // CONFIG_FPMT_TEST is 0: fpmt_unit_test is compiled out.
    public uint cdf_update_mode;
    public int enable_rect_partitions;    // enable rectangular partitions for sequence
    public int enable_ab_partitions;      // enable AB partitions for sequence
    public int enable_1to4_partitions;    // enable 1:4 and 4:1 partitions for sequence
    public int min_partition_size;        // min partition size [4,8,16,32,64,128]
    public int max_partition_size;        // max partition size [4,8,16,32,64,128]
    public int enable_intra_edge_filter;  // enable intra-edge filter for sequence
    public int enable_order_hint;         // enable order hint for sequence
    public int enable_tx64;               // enable 64-pt transform usage for sequence
    public int enable_flip_idtx;          // enable flip and identity transform types
    public int enable_rect_tx;            // enable rectangular transform usage for sequence
    public int enable_dist_wtd_comp;      // enable dist wtd compound for sequence
    public int max_reference_frames;      // maximum number of references per frame
    public int enable_reduced_reference_set;  // enable reduced set of references
    public int enable_ref_frame_mvs;      // sequence level
    public int allow_ref_frame_mvs;       // frame level
    public int enable_masked_comp;        // enable masked compound for sequence
    public int enable_onesided_comp;      // enable one sided compound for sequence
    public int enable_interintra_comp;    // enable interintra compound for sequence
    public int enable_smooth_interintra;  // enable smooth interintra mode usage
    public int enable_diff_wtd_comp;      // enable diff-wtd compound usage
    public int enable_interinter_wedge;   // enable interinter-wedge compound usage
    public int enable_interintra_wedge;   // enable interintra-wedge compound usage
    public int enable_global_motion;      // enable global motion usage for sequence
    public int enable_warped_motion;      // sequence level
    public int allow_warped_motion;       // frame level
    public int enable_filter_intra;       // enable filter intra for sequence
    public int enable_smooth_intra;       // enable smooth intra modes for sequence
    public int enable_paeth_intra;        // enable Paeth intra mode for sequence
    public int enable_cfl_intra;          // enable CFL uv intra mode for sequence
    public int enable_directional_intra;  // enable directional modes for sequence
    public int enable_diagonal_intra;     // enable D45 to D203 intra modes for sequence
    public int enable_superres;
    public int enable_overlay;  // enable overlay for filtered arf frames
    public int enable_palette;
    public int enable_intrabc;
    public int enable_angle_delta;
    // CONFIG_DENOISE is 1.
    public float noise_level;
    public int noise_block_size;
    public int enable_dnl_denoising;

    public uint enable_low_complexity_decode;
    public uint chroma_subsampling_x;
    public uint chroma_subsampling_y;
    public int reduced_tx_type_set;
    public int use_intra_dct_only;
    public int use_inter_dct_only;
    public int use_intra_default_tx_only;
    public int enable_tx_size_search;
    public int quant_b_adapt;
    public uint vbr_corpus_complexity_lap;
    // AV1_LEVEL target_seq_level_idx[MAX_NUM_OPERATING_POINTS]
    public Arr32<byte> target_seq_level_idx;  // [port] AV1_LEVEL is UENUM1BYTE: uint8_t storage
    // Bit mask to specify which tier each of the 32 possible operating points
    // conforms to.
    public uint tier_mask;
    // min_cr / 100 is the target minimum compression ratio for each frame.
    public uint min_cr;
    public COST_UPDATE_TYPE coeff_cost_upd_freq;
    public COST_UPDATE_TYPE mode_cost_upd_freq;
    public COST_UPDATE_TYPE mv_cost_upd_freq;
    public COST_UPDATE_TYPE dv_cost_upd_freq;
    public uint ext_tile_debug;
    public uint sb_multipass_unit_test;
    // Total number of passes. If this number is -1, then we assume passes = 1 or
    // 2 (passes = 1 if pass == AOM_RC_ONE_PASS and passes = 2 otherwise).
    public int passes;
    public int fwd_kf_dist;

    public LOOPFILTER_CONTROL loopfilter_control;
    // Indicates if the application of post-processing filters should be skipped
    // on reconstructed frame.
    public uint skip_postproc_filtering;
    // the name of the second pass output file when passes > 2
    public int mode_ref_delta_enabled;
    public string two_pass_output;
    public string second_pass_log;
    // Automatically determine whether to disable several intra tools
    // when "--deltaq-mode=3" is true.
    // Default as 0.
    // When set to 1, the encoder will analyze the reconstruction quality
    // as compared to the source image in the preprocessing pass.
    // If the recontruction quality is considered high enough, we disable
    // the following intra coding tools, for better encoding speed:
    // "--enable_smooth_intra",
    // "--enable_paeth_intra",
    // "--enable_cfl_intra",
    // "--enable_diagonal_intra".
    public int auto_intra_tools_off;
    public int strict_level_conformance;
    public int kf_max_pyr_height;
    public int sb_qp_sweep;
    public aom_screen_detection_mode screen_detection_mode;
    public uint validate_hbd_input;
    public uint force_max_q;
}

// struct aom_codec_alg_priv (the base class aom_codec_priv_t is the C `base` member).
internal sealed partial class aom_codec_alg_priv_t
{
    public readonly aom_codec_enc_cfg_t cfg = new();
    public av1_extracfg extra_cfg;
    public readonly aom_rational64_t timestamp_ratio = new();
    public aom_codec_pts_t pts_offset;
    public byte pts_offset_initialized;
    public readonly AV1EncoderConfig oxcf = new();
    public AV1_PRIMARY ppi;
    // [port] unsigned char *cx_data (malloc'ed by encoder_encode).
    public byte[] cx_data;
    public int cx_data_sz;
    public int pending_cx_data_sz;
    // [subset] aom_image_t preview_img: only used by encoder_get_preview, which never runs.
    public aom_enc_frame_flags_t next_frame_flags;
    // aom_codec_pkt_list_decl(256) pkt_list
    public readonly aom_codec_pkt_list_decl pkt_list = new(256);
    public uint fixed_kf_cntr;
    // BufferPool that holds all reference frames.
    public BufferPool buffer_pool;

    // lookahead instance variables
    public BufferPool buffer_pool_lap;
    // [subset] FIRSTPASS_STATS *frame_stats_buffer and STATS_BUFFER_CTX stats_buf_context:
    // allocated by create_stats_buffer, which is !CONFIG_REALTIME_ONLY code.
    // Number of stats buffers required for look ahead
    public int num_lap_buffers;
    public bool monochrome_on_init;
}

internal static partial class av1_cx_iface
{
    // CONFIG_REALTIME_ONLY is 1: the realtime-only default_extra_cfg.
    // Some settings are changed for realtime only build.
    private static readonly av1_extracfg default_extra_cfg = make_default_extra_cfg();

    private static av1_extracfg make_default_extra_cfg()
    {
        av1_extracfg c = default;
        c.cpu_used = 10;                    // cpu_used
        c.enable_auto_alt_ref = 1;          // enable_auto_alt_ref
        c.enable_auto_bwd_ref = 0;          // enable_auto_bwd_ref
        c.noise_sensitivity = 0;            // noise_sensitivity
        c.sharpness = 0;                    // sharpness
        c.enable_adaptive_sharpness = 0;    // enable_adaptive_sharpness
        c.static_thresh = 0;                // static_thresh
        c.row_mt = 1;                       // row_mt
        c.fp_mt = 0;                        // fp_mt
        c.tile_columns = 0;                 // tile_columns
        c.tile_rows = 0;                    // tile_rows
        c.auto_tiles = 0;                   // auto_tiles
        c.enable_tpl_model = 0;             // enable_tpl_model
        c.enable_keyframe_filtering = 0;    // enable_keyframe_filtering
        c.arnr_max_frames = 7;              // arnr_max_frames
        c.arnr_strength = 5;                // arnr_strength
        c.min_gf_interval = 0;              // min_gf_interval; 0 -> default decision
        c.max_gf_interval = 0;              // max_gf_interval; 0 -> default decision
        c.gf_min_pyr_height = 0;            // gf_min_pyr_height
        c.gf_max_pyr_height = 5;            // gf_max_pyr_height
        c.tuning = AOM_TUNE_PSNR;           // tuning
        c.vmaf_model_path = "/usr/local/share/model/vmaf_v0.6.1.json";  // VMAF model path
        c.partition_info_path = ".";        // partition info path
        c.enable_rate_guide_deltaq = 0;     // enable rate guide deltaq
        c.rate_distribution_info = "./rate_map.txt";  // rate distribution input
        c.dist_metric = AOM_DIST_METRIC_PSNR;  // dist_metric
        c.cq_level = 10;                    // cq_level
        c.rc_max_intra_bitrate_pct = 300;   // rc_max_intra_bitrate_pct
        c.rc_max_inter_bitrate_pct = 0;     // rc_max_inter_bitrate_pct
        c.gf_cbr_boost_pct = 0;             // gf_cbr_boost_pct
        c.lossless = 0;                     // lossless
        c.enable_cdef = 1;                  // enable_cdef
        c.enable_restoration = 0;           // enable_restoration
        c.force_video_mode = 0;             // force_video_mode
        c.enable_obmc = 0;                  // enable_obmc
        c.disable_trellis_quant = 3;        // disable_trellis_quant
        c.enable_qm = 0;                    // enable_qm
        c.qm_y = DEFAULT_QM_Y;              // qm_y
        c.qm_u = DEFAULT_QM_U;              // qm_u
        c.qm_v = DEFAULT_QM_V;              // qm_v
        c.qm_min = DEFAULT_QM_FIRST;        // qm_min
        c.qm_max = DEFAULT_QM_LAST;         // qm_max
        c.num_tg = 1;                       // max number of tile groups
        c.mtu_size = 0;                     // mtu_size
        c.timing_info_type = AOM_TIMING_UNSPECIFIED;  // No picture timing signaling in bitstream
        c.frame_parallel_decoding_mode = 0; // frame_parallel_decoding_mode
        c.enable_dual_filter = 0;           // enable dual filter
        c.enable_chroma_deltaq = 0;         // enable delta quant in chroma planes
        c.aq_mode = CYCLIC_REFRESH_AQ;      // aq_mode
        c.deltaq_mode = NO_DELTA_Q;         // deltaq_mode
        c.deltaq_strength = 100;            // deltaq_strength
        c.deltalf_mode = 0;                 // delta lf mode
        c.frame_periodic_boost = 0;         // frame_periodic_boost
        c.content = AOM_CONTENT_DEFAULT;    // content
        c.color_primaries = AOM_CICP_CP_UNSPECIFIED;  // CICP color primaries
        c.transfer_characteristics = AOM_CICP_TC_UNSPECIFIED;  // CICP transfer characteristics
        c.matrix_coefficients = AOM_CICP_MC_UNSPECIFIED;  // CICP matrix coefficients
        c.chroma_sample_position = AOM_CSP_UNKNOWN;  // chroma sample position
        c.color_range = 0;                  // color range
        c.render_width = 0;                 // render width
        c.render_height = 0;                // render height
        c.superblock_size = AOM_SUPERBLOCK_SIZE_DYNAMIC;  // superblock_size
        c.single_tile_decoding = 1;         // this depends on large_scale_tile.
        c.error_resilient_mode = 0;         // error_resilient_mode off by default.
        c.s_frame_mode = 0;                 // s_frame_mode off by default.
        c.film_grain_test_vector = 0;       // film_grain_test_vector
        c.film_grain_table_filename = null; // film_grain_table_filename
        c.motion_vector_unit_test = 0;      // motion_vector_unit_test
        c.cdf_update_mode = 1;              // CDF update mode
        c.enable_rect_partitions = 0;       // enable rectangular partitions
        c.enable_ab_partitions = 0;         // enable ab shape partitions
        c.enable_1to4_partitions = 0;       // enable 1:4 and 4:1 partitions
        c.min_partition_size = 4;           // min_partition_size
        c.max_partition_size = 128;         // max_partition_size
        c.enable_intra_edge_filter = 0;     // enable intra edge filter
        c.enable_order_hint = 0;            // frame order hint
        c.enable_tx64 = 0;                  // enable 64-pt transform usage
        c.enable_flip_idtx = 1;             // enable flip and identity transform
        c.enable_rect_tx = 1;               // enable rectangular transform usage
        c.enable_dist_wtd_comp = 0;         // dist-wtd compound
        c.max_reference_frames = 3;         // max_reference_frames
        c.enable_reduced_reference_set = 0; // enable_reduced_reference_set
        c.enable_ref_frame_mvs = 0;         // enable_ref_frame_mvs sequence level
        c.allow_ref_frame_mvs = 0;          // allow ref_frame_mvs frame level
        c.enable_masked_comp = 0;           // enable masked compound at sequence level
        c.enable_onesided_comp = 0;         // enable one sided compound at sequence level
        c.enable_interintra_comp = 0;       // enable interintra compound at sequence level
        c.enable_smooth_interintra = 0;     // enable smooth interintra mode
        c.enable_diff_wtd_comp = 0;         // enable difference-weighted compound
        c.enable_interinter_wedge = 0;      // enable interinter wedge compound
        c.enable_interintra_wedge = 0;      // enable interintra wedge compound
        c.enable_global_motion = 0;         // enable_global_motion usage
        c.enable_warped_motion = 0;         // enable_warped_motion at sequence level
        c.allow_warped_motion = 0;          // allow_warped_motion at frame level
        c.enable_filter_intra = 0;          // enable filter intra at sequence level
        c.enable_smooth_intra = 0;          // enable smooth intra modes usage for sequence
        c.enable_paeth_intra = 0;           // enable Paeth intra mode usage for sequence
        c.enable_cfl_intra = 0;             // enable CFL uv intra mode usage for sequence
        c.enable_directional_intra = 1;     // enable directional intra mode usage for sequence
        c.enable_diagonal_intra = 1;        // enable D45 to D203 intra mode usage for sequence
        c.enable_superres = 0;              // superres
        c.enable_overlay = 0;               // enable overlay
        c.enable_palette = 1;               // enable palette
        c.enable_intrabc = 0;               // enable intrabc
        c.enable_angle_delta = 0;           // enable angle delta
        // CONFIG_DENOISE is 1.
        c.noise_level = 0;                  // noise_level
        c.noise_block_size = 32;            // noise_block_size
        c.enable_dnl_denoising = 1;         // enable_dnl_denoising
        c.enable_low_complexity_decode = 0; // enable_low_complexity_decode
        c.chroma_subsampling_x = 0;         // chroma_subsampling_x
        c.chroma_subsampling_y = 0;         // chroma_subsampling_y
        c.reduced_tx_type_set = 0;          // reduced_tx_type_set
        c.use_intra_dct_only = 0;           // use_intra_dct_only
        c.use_inter_dct_only = 0;           // use_inter_dct_only
        c.use_intra_default_tx_only = 1;    // use_intra_default_tx_only
        c.enable_tx_size_search = 1;        // enable_tx_size_search
        c.quant_b_adapt = 0;                // quant_b_adapt
        c.vbr_corpus_complexity_lap = 0;    // vbr_corpus_complexity_lap
        for (int i = 0; i < MAX_NUM_OPERATING_POINTS; i++)
            c.target_seq_level_idx[i] = SEQ_LEVEL_MAX;  // target_seq_level_idx
        c.tier_mask = 0;                    // tier_mask
        c.min_cr = 0;                       // min_cr
        c.coeff_cost_upd_freq = COST_UPD_OFF;  // coeff_cost_upd_freq
        c.mode_cost_upd_freq = COST_UPD_OFF;   // mode_cost_upd_freq
        c.mv_cost_upd_freq = COST_UPD_OFF;     // mv_cost_upd_freq
        c.dv_cost_upd_freq = COST_UPD_OFF;     // dv_cost_upd_freq
        c.ext_tile_debug = 0;               // ext_tile_debug
        c.sb_multipass_unit_test = 0;       // sb_multipass_unit_test
        c.passes = -1;                      // passes
        c.fwd_kf_dist = -1;                 // fwd_kf_dist
        c.loopfilter_control = LOOPFILTER_ALL;  // loopfilter_control
        c.skip_postproc_filtering = 0;      // skip_postproc_filtering
        c.mode_ref_delta_enabled = 1;       // mode_ref_delta_enabled
        c.two_pass_output = null;           // two_pass_output
        c.second_pass_log = null;           // second_pass_log
        c.auto_intra_tools_off = 0;         // auto_intra_tools_off
        c.strict_level_conformance = 0;     // strict_level_conformance
        c.kf_max_pyr_height = -1;           // kf_max_pyr_height
        c.sb_qp_sweep = 0;                  // sb_qp_sweep
        c.screen_detection_mode = AOM_SCREEN_DETECTION_STANDARD;
        c.validate_hbd_input = 1;           // validate_hbd_input
        c.force_max_q = 0;                  // force_max_q
        return c;
    }

    private static int gcd(long a, int b)
    {
        int remainder;
        while (b > 0)
        {
            remainder = (int)(a % b);
            a = b;
            b = remainder;
        }

        return (int)a;
    }

    private static void reduce_ratio(aom_rational64_t ratio)
    {
        int denom = gcd(ratio.num, ratio.den);
        ratio.num /= denom;
        ratio.den /= denom;
    }

    // Called by encoder_encode() only. Must not be called by encoder_init()
    // because the `error` paramerer will be destroyed by aom_codec_enc_init_ver()
    // after encoder_init() returns an error. See the "IMPORTANT" comment in
    // aom_codec_enc_init_ver().
    private static aom_codec_err_t update_error_state(aom_codec_alg_priv_t ctx,
                                                      aom_internal_error_info error)
    {
        aom_codec_err_t res = error.error_code;

        if (res != AOM_CODEC_OK)
            ctx.@base.err_detail = error.has_detail != 0 ? error.detail : null;

        return res;
    }

    // [subset] allocate_and_set_string: only used by the string-valued controls and
    // encoder_set_option, which never run.

    // #define ERROR(str) do { ctx->base.err_detail = str; return AOM_CODEC_INVALID_PARAM; } while (0)
    // [port] Callers write `return ERROR(ctx, str);`. RANGE_CHECK, RANGE_CHECK_HI and
    // RANGE_CHECK_BOOL are expanded in place; their messages are the stringized macro arguments
    // (`#memb " out of range [" #lo ".." #hi "]"`), copied from the C source text.
    private static aom_codec_err_t ERROR(aom_codec_alg_priv_t ctx, string str)
    {
        ctx.@base.err_detail = str;
        return AOM_CODEC_INVALID_PARAM;
    }

    private static aom_codec_err_t validate_config(aom_codec_alg_priv_t ctx,
                                                   aom_codec_enc_cfg_t cfg,
                                                   in av1_extracfg extra_cfg)
    {
        if (!(cfg.g_w >= (1) && cfg.g_w <= (65536)))  // 16 bits available
            return ERROR(ctx, "g_w out of range [1..65536]");
        if (!(cfg.g_h >= (1) && cfg.g_h <= (65536)))  // 16 bits available
            return ERROR(ctx, "g_h out of range [1..65536]");
        if (!(cfg.g_forced_max_frame_width <= (65536)))  // 16 bits available
            return ERROR(ctx, "g_forced_max_frame_width out of range [..65536]");
        if (!(cfg.g_forced_max_frame_height <= (65536)))  // 16 bits available
            return ERROR(ctx, "g_forced_max_frame_height out of range [..65536]");
        if (cfg.g_forced_max_frame_width != 0)
        {
            if (!(cfg.g_w <= (cfg.g_forced_max_frame_width)))
                return ERROR(ctx, "g_w out of range [..cfg->g_forced_max_frame_width]");
        }
        if (cfg.g_forced_max_frame_height != 0)
        {
            if (!(cfg.g_h <= (cfg.g_forced_max_frame_height)))
                return ERROR(ctx, "g_h out of range [..cfg->g_forced_max_frame_height]");
        }
        // To avoid integer overflows when multiplying width by height (or values
        // derived from width and height) using the int type, impose a maximum frame
        // area (width * height) of 2^30.
        uint max_frame_width =
            cfg.g_forced_max_frame_width != 0 ? cfg.g_forced_max_frame_width : cfg.g_w;
        uint max_frame_height = cfg.g_forced_max_frame_height != 0
                                    ? cfg.g_forced_max_frame_height
                                    : cfg.g_h;
        long max_frame_area = (long)max_frame_width * max_frame_height;
        if (max_frame_area > (1 << 30))
        {
            return ERROR(ctx, "max_frame_area out of range [..2^30]");
        }
        if (!(cfg.g_timebase.den >= (1) && cfg.g_timebase.den <= (1000000000)))
            return ERROR(ctx, "g_timebase.den out of range [1..1000000000]");
        if (!(cfg.g_timebase.num >= (1) && cfg.g_timebase.num <= (1000000000)))
            return ERROR(ctx, "g_timebase.num out of range [1..1000000000]");
        if (!(cfg.g_profile <= (MAX_PROFILES - 1)))
            return ERROR(ctx, "g_profile out of range [..MAX_PROFILES - 1]");

        if (!(cfg.rc_target_bitrate <= (2000000)))
            return ERROR(ctx, "rc_target_bitrate out of range [..2000000]");
        if (!(cfg.rc_max_quantizer <= (63)))
            return ERROR(ctx, "rc_max_quantizer out of range [..63]");
        if (!(cfg.rc_min_quantizer <= (cfg.rc_max_quantizer)))
            return ERROR(ctx, "rc_min_quantizer out of range [..cfg->rc_max_quantizer]");
        if (B(extra_cfg.lossless != 0) != extra_cfg.lossless)
            return ERROR(ctx, "lossless expected boolean");
        if (!(extra_cfg.aq_mode <= (AQ_MODE_COUNT - 1)))
            return ERROR(ctx, "aq_mode out of range [..AQ_MODE_COUNT - 1]");
        if (!(extra_cfg.deltaq_mode <= (DELTA_Q_MODE_COUNT - 1)))
            return ERROR(ctx, "deltaq_mode out of range [..DELTA_Q_MODE_COUNT - 1]");
        if (!(extra_cfg.deltalf_mode <= (1)))
            return ERROR(ctx, "deltalf_mode out of range [..1]");
        if (!(extra_cfg.frame_periodic_boost <= (1)))
            return ERROR(ctx, "frame_periodic_boost out of range [..1]");
        // CONFIG_REALTIME_ONLY is 1.
        if (!(cfg.g_usage >= (AOM_USAGE_REALTIME) && cfg.g_usage <= (AOM_USAGE_REALTIME)))
            return ERROR(ctx, "g_usage out of range [AOM_USAGE_REALTIME..AOM_USAGE_REALTIME]");
        if (!(cfg.g_threads <= (MAX_NUM_THREADS)))
            return ERROR(ctx, "g_threads out of range [..MAX_NUM_THREADS]");
        if (!(cfg.rc_end_usage >= (AOM_VBR) && cfg.rc_end_usage <= (AOM_Q)))
            return ERROR(ctx, "rc_end_usage out of range [AOM_VBR..AOM_Q]");
        if (!(cfg.rc_undershoot_pct <= (100)))
            return ERROR(ctx, "rc_undershoot_pct out of range [..100]");
        if (!(cfg.rc_overshoot_pct <= (100)))
            return ERROR(ctx, "rc_overshoot_pct out of range [..100]");
        if (!(cfg.rc_2pass_vbr_bias_pct <= (100)))
            return ERROR(ctx, "rc_2pass_vbr_bias_pct out of range [..100]");
        if (!(cfg.kf_mode >= (AOM_KF_DISABLED) && cfg.kf_mode <= (AOM_KF_AUTO)))
            return ERROR(ctx, "kf_mode out of range [AOM_KF_DISABLED..AOM_KF_AUTO]");
        if (!(cfg.rc_dropframe_thresh <= (100)))
            return ERROR(ctx, "rc_dropframe_thresh out of range [..100]");
        if (!(cfg.g_pass >= (AOM_RC_ONE_PASS) && cfg.g_pass <= (AOM_RC_THIRD_PASS)))
            return ERROR(ctx, "g_pass out of range [AOM_RC_ONE_PASS..AOM_RC_THIRD_PASS]");
        if (!(cfg.g_lag_in_frames <= (MAX_LAG_BUFFERS)))
            return ERROR(ctx, "g_lag_in_frames out of range [..MAX_LAG_BUFFERS]");
        if (cfg.g_usage == AOM_USAGE_ALL_INTRA)
        {
            if (!(cfg.g_lag_in_frames <= (0)))
                return ERROR(ctx, "g_lag_in_frames out of range [..0]");
            if (!(cfg.kf_max_dist <= (0)))
                return ERROR(ctx, "kf_max_dist out of range [..0]");
        }
        if (!(extra_cfg.min_gf_interval <= (MAX_LAG_BUFFERS - 1)))
            return ERROR(ctx, "min_gf_interval out of range [..MAX_LAG_BUFFERS - 1]");
        if (!(extra_cfg.max_gf_interval <= (MAX_LAG_BUFFERS - 1)))
            return ERROR(ctx, "max_gf_interval out of range [..MAX_LAG_BUFFERS - 1]");
        if (extra_cfg.max_gf_interval > 0)
        {
            if (!(extra_cfg.max_gf_interval >= (AOMMAX(2u, extra_cfg.min_gf_interval)) &&
                  extra_cfg.max_gf_interval <= ((MAX_LAG_BUFFERS - 1))))
                return ERROR(ctx,
                             "max_gf_interval out of range [AOMMAX(2, extra_cfg->min_gf_interval).." +
                             "(MAX_LAG_BUFFERS - 1)]");
        }
        if (!(extra_cfg.gf_min_pyr_height <= (5)))
            return ERROR(ctx, "gf_min_pyr_height out of range [..5]");
        if (!(extra_cfg.gf_max_pyr_height <= (5)))
            return ERROR(ctx, "gf_max_pyr_height out of range [..5]");
        if (extra_cfg.gf_min_pyr_height > extra_cfg.gf_max_pyr_height)
        {
            return ERROR(ctx,
                "gf_min_pyr_height must be less than or equal to " +
                "gf_max_pyramid_height");
        }

        if (!(cfg.rc_resize_mode <= (RESIZE_MODES - 1)))
            return ERROR(ctx, "rc_resize_mode out of range [..RESIZE_MODES - 1]");
        if (!(cfg.rc_resize_denominator >= (SCALE_NUMERATOR) &&
              cfg.rc_resize_denominator <= (SCALE_NUMERATOR << 1)))
            return ERROR(ctx,
                         "rc_resize_denominator out of range [SCALE_NUMERATOR..SCALE_NUMERATOR << 1]");
        if (!(cfg.rc_resize_kf_denominator >= (SCALE_NUMERATOR) &&
              cfg.rc_resize_kf_denominator <= (SCALE_NUMERATOR << 1)))
            return ERROR(ctx,
                         "rc_resize_kf_denominator out of range [SCALE_NUMERATOR..SCALE_NUMERATOR << 1]");
        if (!(cfg.rc_superres_mode <= (AOM_SUPERRES_AUTO)))
            return ERROR(ctx, "rc_superres_mode out of range [..AOM_SUPERRES_AUTO]");
        if (!(cfg.rc_superres_denominator >= (SCALE_NUMERATOR) &&
              cfg.rc_superres_denominator <= (SCALE_NUMERATOR << 1)))
            return ERROR(ctx,
                         "rc_superres_denominator out of range [SCALE_NUMERATOR..SCALE_NUMERATOR << 1]");
        if (!(cfg.rc_superres_kf_denominator >= (SCALE_NUMERATOR) &&
              cfg.rc_superres_kf_denominator <= (SCALE_NUMERATOR << 1)))
            return ERROR(ctx,
                         "rc_superres_kf_denominator out of range [SCALE_NUMERATOR..SCALE_NUMERATOR << 1]");
        if (!(cfg.rc_superres_qthresh >= (1) && cfg.rc_superres_qthresh <= (63)))
            return ERROR(ctx, "rc_superres_qthresh out of range [1..63]");
        if (!(cfg.rc_superres_kf_qthresh >= (1) && cfg.rc_superres_kf_qthresh <= (63)))
            return ERROR(ctx, "rc_superres_kf_qthresh out of range [1..63]");
        if (!(extra_cfg.cdf_update_mode <= (2)))
            return ERROR(ctx, "cdf_update_mode out of range [..2]");

        if (!(extra_cfg.motion_vector_unit_test <= (2)))
            return ERROR(ctx, "motion_vector_unit_test out of range [..2]");
        // CONFIG_FPMT_TEST is 0: the fpmt_unit_test check is compiled out.
        if (!(extra_cfg.sb_multipass_unit_test <= (1)))
            return ERROR(ctx, "sb_multipass_unit_test out of range [..1]");
        if (!(extra_cfg.ext_tile_debug <= (1)))
            return ERROR(ctx, "ext_tile_debug out of range [..1]");
        if (!(extra_cfg.enable_auto_alt_ref <= (1)))
            return ERROR(ctx, "enable_auto_alt_ref out of range [..1]");
        if (!(extra_cfg.enable_auto_bwd_ref <= (2)))
            return ERROR(ctx, "enable_auto_bwd_ref out of range [..2]");
        if (!(extra_cfg.cpu_used >= (0) &&
              extra_cfg.cpu_used <= ((cfg.g_usage == AOM_USAGE_REALTIME) ? 12 : 9)))
            return ERROR(ctx,
                         "cpu_used out of range [0..(cfg->g_usage == AOM_USAGE_REALTIME) ? 12 : 9]");
        if (!(extra_cfg.noise_sensitivity <= (6)))
            return ERROR(ctx, "noise_sensitivity out of range [..6]");
        if (!(extra_cfg.superblock_size >= (AOM_SUPERBLOCK_SIZE_64X64) &&
              extra_cfg.superblock_size <= (AOM_SUPERBLOCK_SIZE_DYNAMIC)))
            return ERROR(ctx,
                         "superblock_size out of range [AOM_SUPERBLOCK_SIZE_64X64..AOM_SUPERBLOCK_SIZE_DYNAMIC]");
        if (!(cfg.large_scale_tile <= (1)))
            return ERROR(ctx, "large_scale_tile out of range [..1]");
        if (!(extra_cfg.single_tile_decoding <= (1)))
            return ERROR(ctx, "single_tile_decoding out of range [..1]");
        if (!(extra_cfg.enable_rate_guide_deltaq <= (1)))
            return ERROR(ctx, "enable_rate_guide_deltaq out of range [..1]");

        if (!(extra_cfg.row_mt <= (1)))
            return ERROR(ctx, "row_mt out of range [..1]");
        if (!(extra_cfg.fp_mt <= (1)))
            return ERROR(ctx, "fp_mt out of range [..1]");

        if (!(extra_cfg.tile_columns <= (6)))
            return ERROR(ctx, "tile_columns out of range [..6]");
        if (!(extra_cfg.tile_rows <= (6)))
            return ERROR(ctx, "tile_rows out of range [..6]");
        if (!(extra_cfg.auto_tiles <= (1)))
            return ERROR(ctx, "auto_tiles out of range [..1]");

        if (!(cfg.monochrome <= (1)))
            return ERROR(ctx, "monochrome out of range [..1]");

        if (cfg.large_scale_tile != 0 && extra_cfg.aq_mode != 0)
            return ERROR(ctx,
                "Adaptive quantization are not supported in large scale tile " +
                "coding.");

        if (!(extra_cfg.sharpness <= (7)))
            return ERROR(ctx, "sharpness out of range [..7]");
        if (!(extra_cfg.enable_adaptive_sharpness <= (1)))
            return ERROR(ctx, "enable_adaptive_sharpness out of range [..1]");
        if (!(extra_cfg.arnr_max_frames <= (15)))
            return ERROR(ctx, "arnr_max_frames out of range [..15]");
        if (!(extra_cfg.arnr_strength <= (6)))
            return ERROR(ctx, "arnr_strength out of range [..6]");
        if (!(extra_cfg.cq_level <= (63)))
            return ERROR(ctx, "cq_level out of range [..63]");
        if (!(cfg.g_bit_depth >= (AOM_BITS_8) && cfg.g_bit_depth <= (AOM_BITS_12)))
            return ERROR(ctx, "g_bit_depth out of range [AOM_BITS_8..AOM_BITS_12]");
        if (!(cfg.g_input_bit_depth >= (8) && cfg.g_input_bit_depth <= (12)))
            return ERROR(ctx, "g_input_bit_depth out of range [8..12]");
        if (cfg.g_input_bit_depth > (uint)cfg.g_bit_depth)
            return ERROR(ctx, "Input bit-depth must not exceed codec bit-depth");
        if (!(extra_cfg.content >= (AOM_CONTENT_DEFAULT) && extra_cfg.content <= (AOM_CONTENT_INVALID - 1)))
            return ERROR(ctx, "content out of range [AOM_CONTENT_DEFAULT..AOM_CONTENT_INVALID - 1]");

        if (cfg.g_pass >= AOM_RC_SECOND_PASS)
        {
            if (cfg.rc_twopass_stats_in.buf.IsNull)
                return ERROR(ctx, "rc_twopass_stats_in.buf not set.");

            // [subset] The remaining checks read FIRSTPASS_STATS packets from
            // rc_twopass_stats_in: two-pass encoding is not in the realtime subset.
            throw new NotSupportedException("[subset] two-pass stats input");
        }

        if (extra_cfg.passes != -1 && cfg.g_pass == AOM_RC_ONE_PASS &&
            extra_cfg.passes != 1)
        {
            return ERROR(ctx, "One pass encoding but passes != 1.");
        }

        if (extra_cfg.passes != -1 && (int)cfg.g_pass > extra_cfg.passes)
        {
            return ERROR(ctx, "Current pass is larger than total number of passes.");
        }

        if (cfg.g_profile == (uint)PROFILE_1 && cfg.monochrome != 0)
        {
            return ERROR(ctx, "Monochrome is not supported in profile 1");
        }

        if (cfg.g_profile <= (uint)PROFILE_1 &&
            cfg.g_bit_depth > AOM_BITS_10)
        {
            return ERROR(ctx, "Codec bit-depth 12 not supported in profile < 2");
        }

        if (cfg.rc_end_usage == AOM_Q)
        {
            if (!(cfg.use_fixed_qp_offsets <= (2)))
                return ERROR(ctx, "use_fixed_qp_offsets out of range [..2]");
        }
        else
        {
            if (cfg.use_fixed_qp_offsets > 0)
            {
                return ERROR(ctx, "--use_fixed_qp_offsets can only be used with --end-usage=q");
            }
        }

        if (!(extra_cfg.color_primaries >= (AOM_CICP_CP_BT_709) &&
              extra_cfg.color_primaries <= (AOM_CICP_CP_EBU_3213)))  // Need to check range more precisely to
                                                                    // check for reserved values?
            return ERROR(ctx, "color_primaries out of range [AOM_CICP_CP_BT_709..AOM_CICP_CP_EBU_3213]");
        if (!(extra_cfg.transfer_characteristics >= (AOM_CICP_TC_BT_709) &&
              extra_cfg.transfer_characteristics <= (AOM_CICP_TC_HLG)))
            return ERROR(ctx, "transfer_characteristics out of range [AOM_CICP_TC_BT_709..AOM_CICP_TC_HLG]");
        if (!(extra_cfg.matrix_coefficients >= (AOM_CICP_MC_IDENTITY) &&
              extra_cfg.matrix_coefficients <= (AOM_CICP_MC_YCGCO_RO)))
            return ERROR(ctx, "matrix_coefficients out of range [AOM_CICP_MC_IDENTITY..AOM_CICP_MC_YCGCO_RO]");
        if (!(extra_cfg.color_range >= (0) && extra_cfg.color_range <= (1)))
            return ERROR(ctx, "color_range out of range [0..1]");

        /* Average corpus complexity is supported only in the case of single pass
         * VBR*/
        if (cfg.g_pass == AOM_RC_ONE_PASS && cfg.rc_end_usage == AOM_VBR)
        {
            if (!(extra_cfg.vbr_corpus_complexity_lap <= (MAX_VBR_CORPUS_COMPLEXITY)))
                return ERROR(ctx, "vbr_corpus_complexity_lap out of range [..MAX_VBR_CORPUS_COMPLEXITY]");
        }
        else if (extra_cfg.vbr_corpus_complexity_lap != 0)
            return ERROR(ctx,
                "VBR corpus complexity is supported only in the case of single pass " +
                "VBR mode.");

        // CONFIG_TUNE_BUTTERAUGLI is 0.
        if (extra_cfg.tuning == AOM_TUNE_BUTTERAUGLI)
        {
            return ERROR(ctx,
                "This error may be related to the wrong configuration options: try to " +
                "set -DCONFIG_TUNE_BUTTERAUGLI=1 at the time CMake is run.");
        }

        // CONFIG_TUNE_VMAF is 0.
        if (is_vmaf_tuning_mode(extra_cfg.tuning) != 0)
        {
            return ERROR(ctx,
                "This error may be related to the wrong configuration options: try to " +
                "set -DCONFIG_TUNE_VMAF=1 at the time CMake is run.");
        }

        if (!(extra_cfg.tuning >= (AOM_TUNE_PSNR) && extra_cfg.tuning <= (AOM_TUNE_SSIMULACRA2)))
            return ERROR(ctx, "tuning out of range [AOM_TUNE_PSNR..AOM_TUNE_SSIMULACRA2]");

        if (!(extra_cfg.dist_metric >= (AOM_DIST_METRIC_PSNR) &&
              extra_cfg.dist_metric <= (AOM_DIST_METRIC_QM_PSNR)))
            return ERROR(ctx, "dist_metric out of range [AOM_DIST_METRIC_PSNR..AOM_DIST_METRIC_QM_PSNR]");

        if (!(extra_cfg.timing_info_type >= (AOM_TIMING_UNSPECIFIED) &&
              extra_cfg.timing_info_type <= (AOM_TIMING_DEC_MODEL)))
            return ERROR(ctx, "timing_info_type out of range [AOM_TIMING_UNSPECIFIED..AOM_TIMING_DEC_MODEL]");

        if (!(extra_cfg.film_grain_test_vector >= (0) && extra_cfg.film_grain_test_vector <= (16)))
            return ERROR(ctx, "film_grain_test_vector out of range [0..16]");

        if (extra_cfg.lossless != 0)
        {
            if (extra_cfg.aq_mode != 0)
                return ERROR(ctx, "Only --aq_mode=0 can be used with --lossless=1.");
            if (extra_cfg.enable_chroma_deltaq != 0)
                return ERROR(ctx, "Only --enable_chroma_deltaq=0 can be used with --lossless=1.");
        }

        if (!(extra_cfg.max_reference_frames >= (3) && extra_cfg.max_reference_frames <= (7)))
            return ERROR(ctx, "max_reference_frames out of range [3..7]");
        if (!(extra_cfg.enable_reduced_reference_set >= (0) && extra_cfg.enable_reduced_reference_set <= (1)))
            return ERROR(ctx, "enable_reduced_reference_set out of range [0..1]");

        if (!(extra_cfg.enable_low_complexity_decode <= (1)))
            return ERROR(ctx, "enable_low_complexity_decode out of range [..1]");
        if (!(extra_cfg.chroma_subsampling_x <= (1)))
            return ERROR(ctx, "chroma_subsampling_x out of range [..1]");
        if (!(extra_cfg.chroma_subsampling_y <= (1)))
            return ERROR(ctx, "chroma_subsampling_y out of range [..1]");
        // 6.4.2 Color config semantics
        // If matrix_coefficients is equal to MC_IDENTITY, it is a requirement of
        // bitstream conformance that subsampling_x is equal to 0 and subsampling_y
        // is equal to 0.
        if (extra_cfg.matrix_coefficients == AOM_CICP_MC_IDENTITY &&
            (extra_cfg.chroma_subsampling_x != 0 ||
             extra_cfg.chroma_subsampling_y != 0))
        {
            return ERROR(ctx, "Subsampling must be 0 with AOM_CICP_MC_IDENTITY.");
        }

        if (!(extra_cfg.disable_trellis_quant <= (3)))
            return ERROR(ctx, "disable_trellis_quant out of range [..3]");
        if (!(extra_cfg.coeff_cost_upd_freq >= (0) && extra_cfg.coeff_cost_upd_freq <= (3)))
            return ERROR(ctx, "coeff_cost_upd_freq out of range [0..3]");
        if (!(extra_cfg.mode_cost_upd_freq >= (0) && extra_cfg.mode_cost_upd_freq <= (3)))
            return ERROR(ctx, "mode_cost_upd_freq out of range [0..3]");
        if (!(extra_cfg.mv_cost_upd_freq >= (0) && extra_cfg.mv_cost_upd_freq <= (3)))
            return ERROR(ctx, "mv_cost_upd_freq out of range [0..3]");
        if (!(extra_cfg.dv_cost_upd_freq >= (0) && extra_cfg.dv_cost_upd_freq <= (3)))
            return ERROR(ctx, "dv_cost_upd_freq out of range [0..3]");

        if (!(extra_cfg.min_partition_size >= (4) && extra_cfg.min_partition_size <= (128)))
            return ERROR(ctx, "min_partition_size out of range [4..128]");
        if (!(extra_cfg.max_partition_size >= (4) && extra_cfg.max_partition_size <= (128)))
            return ERROR(ctx, "max_partition_size out of range [4..128]");
        if (!(extra_cfg.min_partition_size <= (extra_cfg.max_partition_size)))
            return ERROR(ctx, "min_partition_size out of range [..extra_cfg->max_partition_size]");

        for (int i = 0; i < MAX_NUM_OPERATING_POINTS; ++i)
        {
            int level_idx = extra_cfg.target_seq_level_idx[i];
            if (is_valid_seq_level_idx(level_idx) == 0 &&
                level_idx != SEQ_LEVEL_KEEP_STATS)
            {
                return ERROR(ctx, "Target sequence level index is invalid");
            }
        }

        if (!(extra_cfg.deltaq_strength <= (1000)))
            return ERROR(ctx, "deltaq_strength out of range [..1000]");
        if (!(extra_cfg.loopfilter_control <= (3)))
            return ERROR(ctx, "loopfilter_control out of range [..3]");
        if (B(extra_cfg.skip_postproc_filtering != 0) != extra_cfg.skip_postproc_filtering)
            return ERROR(ctx, "skip_postproc_filtering expected boolean");
        if (B(extra_cfg.mode_ref_delta_enabled != 0) != extra_cfg.mode_ref_delta_enabled)
            return ERROR(ctx, "mode_ref_delta_enabled expected boolean");
        if (!(extra_cfg.enable_cdef <= (3)))
            return ERROR(ctx, "enable_cdef out of range [..3]");
        if (B(extra_cfg.auto_intra_tools_off != 0) != extra_cfg.auto_intra_tools_off)
            return ERROR(ctx, "auto_intra_tools_off expected boolean");
        if (B(extra_cfg.strict_level_conformance != 0) != extra_cfg.strict_level_conformance)
            return ERROR(ctx, "strict_level_conformance expected boolean");
        if (B(extra_cfg.sb_qp_sweep != 0) != extra_cfg.sb_qp_sweep)
            return ERROR(ctx, "sb_qp_sweep expected boolean");

        if (!(extra_cfg.kf_max_pyr_height >= (-1) && extra_cfg.kf_max_pyr_height <= (5)))
            return ERROR(ctx, "kf_max_pyr_height out of range [-1..5]");
        if (extra_cfg.kf_max_pyr_height != -1 &&
            extra_cfg.kf_max_pyr_height < (int)extra_cfg.gf_min_pyr_height)
        {
            return ERROR(ctx,
                "The value of kf-max-pyr-height should not be smaller than " +
                "gf-min-pyr-height");
        }

        if (!(extra_cfg.screen_detection_mode >= (1) && extra_cfg.screen_detection_mode <= (2)))
            return ERROR(ctx, "screen_detection_mode out of range [1..2]");
        return AOM_CODEC_OK;
    }

    private static aom_codec_err_t validate_img(aom_codec_alg_priv_t ctx, aom_image_t img)
    {
        switch (img.fmt)
        {
            case AOM_IMG_FMT_YV12:
            case AOM_IMG_FMT_NV12:
            case AOM_IMG_FMT_I420:
            case AOM_IMG_FMT_YV1216:
            case AOM_IMG_FMT_I42016: break;
            case AOM_IMG_FMT_I444:
            case AOM_IMG_FMT_I44416:
                if (ctx.cfg.g_profile == (uint)PROFILE_0 &&
                    ctx.cfg.monochrome == 0)
                {
                    return ERROR(ctx, "Invalid image format. I444 images not supported in profile.");
                }
                break;
            case AOM_IMG_FMT_I422:
            case AOM_IMG_FMT_I42216:
                if (ctx.cfg.g_profile != (uint)PROFILE_2)
                {
                    return ERROR(ctx, "Invalid image format. I422 images not supported in profile.");
                }
                break;
            default:
                return ERROR(ctx,
                    "Invalid image format. Only YV12, NV12, I420, I422, I444 images are " +
                    "supported.");
        }

        if (img.d_w != ctx.cfg.g_w || img.d_h != ctx.cfg.g_h)
            return ERROR(ctx, "Image size must match encoder init configuration size");
        Debug.Assert((img.fmt & AOM_IMG_FMT_PLANAR) != 0);
        if (ctx.cfg.monochrome == 0 && img.fmt != AOM_IMG_FMT_NV12 &&
            img.stride[AOM_PLANE_U] != img.stride[AOM_PLANE_V])
            return ERROR(ctx, "Image U/V strides must match");

        // 6.4.2 Color config semantics
        // If matrix_coefficients is equal to MC_IDENTITY, it is a requirement of
        // bitstream conformance that subsampling_x is equal to 0 and subsampling_y
        // is equal to 0.
        aom_matrix_coefficients_t matrix_coefficients =
            ctx.extra_cfg.matrix_coefficients;
        if (matrix_coefficients == AOM_CICP_MC_IDENTITY &&
            (img.x_chroma_shift != 0 || img.y_chroma_shift != 0))
        {
            return ERROR(ctx, "Subsampling must be 0 with AOM_CICP_MC_IDENTITY.");
        }

        // CONFIG_TUNE_BUTTERAUGLI and CONFIG_AV1_HIGHBITDEPTH are 0.

        return AOM_CODEC_OK;
    }

    private static int get_image_bps(aom_image_t img)
    {
        switch (img.fmt)
        {
            case AOM_IMG_FMT_YV12:
            case AOM_IMG_FMT_NV12:
            case AOM_IMG_FMT_I420: return 12;
            case AOM_IMG_FMT_I422: return 16;
            case AOM_IMG_FMT_I444: return 24;
            case AOM_IMG_FMT_YV1216:
            case AOM_IMG_FMT_I42016: return 24;
            case AOM_IMG_FMT_I42216: return 32;
            case AOM_IMG_FMT_I44416: return 48;
            default: Debug.Fail("Invalid image format"); break;
        }
        return 0;
    }

    // Set appropriate options to disable frame super-resolution.
    private static void disable_superres(ref SuperResCfg superres_cfg)
    {
        superres_cfg.superres_mode = AOM_SUPERRES_NONE;
        superres_cfg.superres_scale_denominator = SCALE_NUMERATOR;
        superres_cfg.superres_kf_scale_denominator = SCALE_NUMERATOR;
        superres_cfg.superres_qthresh = 255;
        superres_cfg.superres_kf_qthresh = 255;
    }

    private static void set_auto_tiles(ref TileConfig tile_cfg, uint width, uint height,
                                       uint threads)
    {
        int tile_cols_log2 = 0;
        int tile_rows_log2 = 0;
        if (threads < 2) return;
        // Avoid small tiles because they are particularly bad for coding.
        // Use no more tiles than the number of threads. Aim for one tile per
        // thread. Using more than one thread inside one tile could be less
        // efficient. Using more tiles than the number of threads would result
        // in a compression penalty without much benefit.
        const uint kMinTileArea = 128 * 128;
        const uint kMaxTiles = 32;
        uint frame_area = width * height;
        uint tiles = (frame_area + kMinTileArea - 1) / kMinTileArea;
        if (tiles > kMaxTiles)
        {
            tiles = kMaxTiles;
        }
        if (tiles > threads)
        {
            tiles = threads;
        }
        int tiles_log2 = (int)Math.Log2(tiles);
        // If the frame width is equal or greater than the height, use more tile
        // columns than tile rows.
        if (width >= height)
        {
            tile_cols_log2 = (tiles_log2 + 1) / 2;
            tile_rows_log2 = tiles_log2 - tile_cols_log2;
        }
        else
        {
            tile_rows_log2 = (tiles_log2 + 1) / 2;
            tile_cols_log2 = tiles_log2 - tile_rows_log2;
        }
        tile_cfg.tile_columns = tile_cols_log2;
        tile_cfg.tile_rows = tile_rows_log2;
    }

    // [subset] update_default_encoder_config: only runs when cfg->encoder_cfg.init_by_cfg_file
    // is set (a --cfg file of aomenc); aom_codec_enc_config_default leaves it 0.

    private static void set_encoder_config(AV1EncoderConfig oxcf, aom_codec_enc_cfg_t cfg,
                                           ref av1_extracfg extra_cfg)
    {
        if (cfg.encoder_cfg.init_by_cfg_file != 0)
        {
            throw new NotSupportedException("[subset] update_default_encoder_config");
        }

        ref TuneCfg tune_cfg = ref oxcf.tune_cfg;
        ref FrameDimensionCfg frm_dim_cfg = ref oxcf.frm_dim_cfg;
        ref TileConfig tile_cfg = ref oxcf.tile_cfg;
        ref ResizeCfg resize_cfg = ref oxcf.resize_cfg;
        ref GFConfig gf_cfg = ref oxcf.gf_cfg;
        ref PartitionCfg part_cfg = ref oxcf.part_cfg;
        ref IntraModeCfg intra_mode_cfg = ref oxcf.intra_mode_cfg;
        ref TxfmSizeTypeCfg txfm_cfg = ref oxcf.txfm_cfg;
        ref CompoundTypeCfg comp_type_cfg = ref oxcf.comp_type_cfg;
        ref SuperResCfg superres_cfg = ref oxcf.superres_cfg;
        ref KeyFrameCfg kf_cfg = ref oxcf.kf_cfg;
        ref DecoderModelCfg dec_model_cfg = ref oxcf.dec_model_cfg;
        ref RateControlCfg rc_cfg = ref oxcf.rc_cfg;
        ref QuantizationCfg q_cfg = ref oxcf.q_cfg;
        ref ColorCfg color_cfg = ref oxcf.color_cfg;
        ref InputCfg input_cfg = ref oxcf.input_cfg;
        ref AlgoCfg algo_cfg = ref oxcf.algo_cfg;
        ref ToolCfg tool_cfg = ref oxcf.tool_cfg;

        oxcf.profile = (BITSTREAM_PROFILE)cfg.g_profile;
        oxcf.max_threads = (int)cfg.g_threads;

        switch (cfg.g_usage)
        {
            case AOM_USAGE_REALTIME: oxcf.mode = REALTIME; break;
            case AOM_USAGE_ALL_INTRA: oxcf.mode = ALLINTRA; break;
            default: oxcf.mode = GOOD; break;
        }

        // Set frame-dimension related configuration.
        frm_dim_cfg.width = (int)cfg.g_w;
        frm_dim_cfg.height = (int)cfg.g_h;
        frm_dim_cfg.forced_max_frame_width = (int)cfg.g_forced_max_frame_width;
        frm_dim_cfg.forced_max_frame_height = (int)cfg.g_forced_max_frame_height;
        frm_dim_cfg.render_width = extra_cfg.render_width;
        frm_dim_cfg.render_height = extra_cfg.render_height;

        // Set input video related configuration.
        input_cfg.input_bit_depth = cfg.g_input_bit_depth;
        // guess a frame rate if out of whack, use 30
        input_cfg.init_framerate = (double)cfg.g_timebase.den / cfg.g_timebase.num;
        if (cfg.g_pass >= AOM_RC_SECOND_PASS)
        {
            // [subset] input_cfg->limit = n_packets - 1 (two-pass stats input).
            throw new NotSupportedException("[subset] two-pass stats input");
        }
        else
        {
            input_cfg.limit = cfg.g_limit;
        }
        input_cfg.chroma_subsampling_x = extra_cfg.chroma_subsampling_x;
        input_cfg.chroma_subsampling_y = extra_cfg.chroma_subsampling_y;
        if (input_cfg.init_framerate > 180)
        {
            input_cfg.init_framerate = 30;
            dec_model_cfg.timing_info_present = false;
        }

        // Set Decoder model configuration.
        if (extra_cfg.timing_info_type == AOM_TIMING_EQUAL ||
            extra_cfg.timing_info_type == AOM_TIMING_DEC_MODEL)
        {
            dec_model_cfg.timing_info_present = true;
            dec_model_cfg.timing_info.num_units_in_display_tick = (uint)cfg.g_timebase.num;
            dec_model_cfg.timing_info.time_scale = (uint)cfg.g_timebase.den;
            dec_model_cfg.timing_info.num_ticks_per_picture = 1;
        }
        else
        {
            dec_model_cfg.timing_info_present = false;
        }
        if (extra_cfg.timing_info_type == AOM_TIMING_EQUAL)
        {
            dec_model_cfg.timing_info.equal_picture_interval = 1;
            dec_model_cfg.decoder_model_info_present_flag = false;
            dec_model_cfg.display_model_info_present_flag = true;
        }
        else if (extra_cfg.timing_info_type == AOM_TIMING_DEC_MODEL)
        {
            dec_model_cfg.num_units_in_decoding_tick = (uint)cfg.g_timebase.num;
            dec_model_cfg.timing_info.equal_picture_interval = 0;
            dec_model_cfg.decoder_model_info_present_flag = true;
            dec_model_cfg.display_model_info_present_flag = true;
        }

        oxcf.pass = cfg.g_pass;
        // For backward compatibility, assume that if extra_cfg->passes==-1, then
        // passes = 1 or 2.
        if (extra_cfg.passes == -1)
        {
            if (cfg.g_pass == AOM_RC_ONE_PASS)
            {
                oxcf.passes = 1;
            }
            else
            {
                oxcf.passes = 2;
            }
        }
        else
        {
            oxcf.passes = extra_cfg.passes;
        }

        // Set Rate Control configuration.
        rc_cfg.max_intra_bitrate_pct = extra_cfg.rc_max_intra_bitrate_pct;
        rc_cfg.max_inter_bitrate_pct = extra_cfg.rc_max_inter_bitrate_pct;
        rc_cfg.gf_cbr_boost_pct = extra_cfg.gf_cbr_boost_pct;
        rc_cfg.mode = cfg.rc_end_usage;
        rc_cfg.min_cr = extra_cfg.min_cr;
        rc_cfg.best_allowed_q =
            extra_cfg.lossless != 0 ? 0 : av1_quantizer_to_qindex((int)cfg.rc_min_quantizer);
        rc_cfg.worst_allowed_q =
            extra_cfg.lossless != 0 ? 0 : av1_quantizer_to_qindex((int)cfg.rc_max_quantizer);
        rc_cfg.cq_level = av1_quantizer_to_qindex((int)extra_cfg.cq_level);
        rc_cfg.under_shoot_pct = (int)cfg.rc_undershoot_pct;
        rc_cfg.over_shoot_pct = (int)cfg.rc_overshoot_pct;
        rc_cfg.maximum_buffer_size_ms = cfg.rc_buf_sz;
        rc_cfg.starting_buffer_level_ms = cfg.rc_buf_initial_sz;
        rc_cfg.optimal_buffer_level_ms = cfg.rc_buf_optimal_sz;
        // Convert target bandwidth from Kbit/s to Bit/s
        // [port] unsigned int arithmetic in C (wraps at 2^32), then widened to int64_t.
        rc_cfg.target_bandwidth = unchecked(1000u * cfg.rc_target_bitrate);
        rc_cfg.drop_frames_water_mark = (int)cfg.rc_dropframe_thresh;
        rc_cfg.vbr_corpus_complexity_lap = extra_cfg.vbr_corpus_complexity_lap;
        rc_cfg.vbrbias = (int)cfg.rc_2pass_vbr_bias_pct;
        rc_cfg.vbrmin_section = (int)cfg.rc_2pass_vbr_minsection_pct;
        rc_cfg.vbrmax_section = (int)cfg.rc_2pass_vbr_maxsection_pct;
        rc_cfg.force_max_q = (int)extra_cfg.force_max_q;

        // Set Toolset related configuration.
        tool_cfg.bit_depth = cfg.g_bit_depth;
        tool_cfg.cdef_control = (CDEF_CONTROL)extra_cfg.enable_cdef;
        tool_cfg.enable_restoration =
            (cfg.g_usage == AOM_USAGE_REALTIME) ? false : extra_cfg.enable_restoration != 0;
        tool_cfg.force_video_mode = extra_cfg.force_video_mode != 0;
        tool_cfg.enable_palette = extra_cfg.enable_palette != 0;
        // FIXME(debargha): Should this be:
        // tool_cfg->enable_ref_frame_mvs  = extra_cfg->allow_ref_frame_mvs &
        //                                         extra_cfg->enable_order_hint ?
        // Disallow using temporal MVs while large_scale_tile = 1.
        tool_cfg.enable_ref_frame_mvs =
            extra_cfg.allow_ref_frame_mvs != 0 && cfg.large_scale_tile == 0;
        tool_cfg.superblock_size = extra_cfg.superblock_size;
        tool_cfg.enable_monochrome = cfg.monochrome != 0;
        tool_cfg.full_still_picture_hdr = cfg.full_still_picture_hdr != 0;
        tool_cfg.enable_dual_filter = extra_cfg.enable_dual_filter != 0;
        tool_cfg.enable_order_hint = extra_cfg.enable_order_hint != 0;
        tool_cfg.enable_interintra_comp = extra_cfg.enable_interintra_comp != 0;
        tool_cfg.ref_frame_mvs_present =
            (extra_cfg.enable_ref_frame_mvs & extra_cfg.enable_order_hint) != 0;

        // Explicitly disable global motion in a few cases:
        // * For realtime mode, we never search global motion, and disabling
        //   it here prevents later code from allocating buffers we don't need
        // * For large scale tile mode, some of the intended use cases expect
        //   all frame headers to be identical. This breaks if global motion is
        //   used, since global motion data is stored in the frame header.
        //   eg, see test/lightfield_test.sh, which checks that all frame headers
        //   are the same.
        tool_cfg.enable_global_motion = extra_cfg.enable_global_motion != 0 &&
                                        cfg.g_usage != AOM_USAGE_REALTIME &&
                                        cfg.large_scale_tile == 0;

        tool_cfg.error_resilient_mode =
            (cfg.g_error_resilient | (uint)extra_cfg.error_resilient_mode) != 0;
        tool_cfg.frame_parallel_decoding_mode =
            extra_cfg.frame_parallel_decoding_mode != 0;

        // Set Quantization related configuration.
        q_cfg.using_qm = extra_cfg.enable_qm != 0;
        q_cfg.qm_minlevel = (int)extra_cfg.qm_min;
        q_cfg.qm_maxlevel = (int)extra_cfg.qm_max;
        q_cfg.quant_b_adapt = extra_cfg.quant_b_adapt;
        q_cfg.enable_chroma_deltaq = extra_cfg.enable_chroma_deltaq != 0;
        q_cfg.aq_mode = extra_cfg.aq_mode;
        q_cfg.deltaq_mode = extra_cfg.deltaq_mode;
        q_cfg.deltaq_strength = extra_cfg.deltaq_strength;
        q_cfg.use_fixed_qp_offsets =
            (rc_cfg.mode == AOM_Q) ? (int)cfg.use_fixed_qp_offsets : 0;

        q_cfg.enable_hdr_deltaq =
            (q_cfg.deltaq_mode == DELTA_Q_HDR) &&
            (cfg.g_bit_depth == AOM_BITS_10) &&
            (extra_cfg.color_primaries == AOM_CICP_CP_BT_2020);

        tool_cfg.enable_deltalf_mode =
            (q_cfg.deltaq_mode != NO_DELTA_Q) && extra_cfg.deltalf_mode != 0;

        // Set cost update frequency configuration.
        oxcf.cost_upd_freq.coeff = (COST_UPDATE_TYPE)extra_cfg.coeff_cost_upd_freq;
        oxcf.cost_upd_freq.mode = (COST_UPDATE_TYPE)extra_cfg.mode_cost_upd_freq;
        // Avoid MV cost update for allintra encoding mode.
        oxcf.cost_upd_freq.mv = (cfg.kf_max_dist != 0)
                                    ? (COST_UPDATE_TYPE)extra_cfg.mv_cost_upd_freq
                                    : COST_UPD_OFF;
        oxcf.cost_upd_freq.dv = (COST_UPDATE_TYPE)extra_cfg.dv_cost_upd_freq;

        // Set frame resize mode configuration.
        resize_cfg.resize_mode = (RESIZE_MODE)cfg.rc_resize_mode;
        resize_cfg.resize_scale_denominator = (byte)cfg.rc_resize_denominator;
        resize_cfg.resize_kf_scale_denominator =
            (byte)cfg.rc_resize_kf_denominator;
        if (resize_cfg.resize_mode == RESIZE_FIXED &&
            resize_cfg.resize_scale_denominator == SCALE_NUMERATOR &&
            resize_cfg.resize_kf_scale_denominator == SCALE_NUMERATOR)
            resize_cfg.resize_mode = RESIZE_NONE;

        // Set encoder algorithm related configuration.
        algo_cfg.enable_overlay = extra_cfg.enable_overlay != 0;
        algo_cfg.disable_trellis_quant = (int)extra_cfg.disable_trellis_quant;
        algo_cfg.sharpness = (int)extra_cfg.sharpness;
        algo_cfg.enable_adaptive_sharpness = extra_cfg.enable_adaptive_sharpness != 0;
        algo_cfg.arnr_max_frames = (int)extra_cfg.arnr_max_frames;
        algo_cfg.arnr_strength = (int)extra_cfg.arnr_strength;
        algo_cfg.cdf_update_mode = (byte)extra_cfg.cdf_update_mode;
        // TODO(any): Fix and Enable TPL for resize-mode > 0
        algo_cfg.enable_tpl_model =
            resize_cfg.resize_mode != 0 ? false : extra_cfg.enable_tpl_model != 0;
        algo_cfg.loopfilter_control = extra_cfg.loopfilter_control;
        algo_cfg.skip_postproc_filtering = extra_cfg.skip_postproc_filtering != 0;
        algo_cfg.mode_ref_delta_enabled = extra_cfg.mode_ref_delta_enabled;
        algo_cfg.screen_detection_mode = extra_cfg.screen_detection_mode;

        // Set two-pass stats configuration.
        oxcf.twopass_stats_in = cfg.rc_twopass_stats_in;

        if (extra_cfg.two_pass_output != null)
            oxcf.two_pass_output = extra_cfg.two_pass_output;

        oxcf.second_pass_log = extra_cfg.second_pass_log;

        // Set Key frame configuration.
        kf_cfg.fwd_kf_enabled = cfg.fwd_kf_enabled != 0;
        kf_cfg.auto_key =
            cfg.kf_mode == AOM_KF_AUTO && cfg.kf_min_dist != cfg.kf_max_dist;
        kf_cfg.key_freq_min = (int)cfg.kf_min_dist;
        kf_cfg.key_freq_max = (int)cfg.kf_max_dist;
        kf_cfg.sframe_dist = (int)cfg.sframe_dist;
        kf_cfg.sframe_mode = (int)cfg.sframe_mode;
        kf_cfg.enable_sframe = extra_cfg.s_frame_mode != 0;
        kf_cfg.enable_keyframe_filtering = (int)extra_cfg.enable_keyframe_filtering;
        kf_cfg.fwd_kf_dist = extra_cfg.fwd_kf_dist;
        // Disable key frame filtering in all intra mode.
        if (cfg.kf_max_dist == 0)
        {
            kf_cfg.enable_keyframe_filtering = 0;
        }
        kf_cfg.enable_intrabc = extra_cfg.enable_intrabc != 0;

        oxcf.speed = extra_cfg.cpu_used;
        if (oxcf.mode == REALTIME)
        {
            // CONFIG_REALTIME_ONLY is 1.
            oxcf.speed = AOMMAX(oxcf.speed, 5);
            // TODO(yunqingwang, any) In REALTIME mode, 1080p performance at speed 5 & 6
            // is quite bad. Force to use speed 7 for now. Will investigate it when we
            // work on rd path optimization later.
            if (AOMMIN(cfg.g_w, cfg.g_h) >= 1080 && oxcf.speed < 7) oxcf.speed = 7;
        }

        // Now, low complexity decode mode supports good-quality encoding (speed 1 to
        // 3) for vertical videos (608p to 1080p) and horizontal videos (720p to
        // 1080p) when sharpness is not equal to 3. This can be further modified if
        // needed.
        bool is_low_complexity_decode_mode_supported =
            (cfg.g_usage == AOM_USAGE_GOOD_QUALITY) &&
            (oxcf.speed >= 1 && oxcf.speed <= 3) &&
            (AOMMIN(cfg.g_w, cfg.g_h) >= 608 &&
             AOMMIN(cfg.g_w, cfg.g_h) <= 1080) &&
            (extra_cfg.sharpness != 3);
        oxcf.enable_low_complexity_decode =
            (uint)B(extra_cfg.enable_low_complexity_decode != 0 &&
                    is_low_complexity_decode_mode_supported);

        // Set Color related configuration.
        color_cfg.color_primaries = extra_cfg.color_primaries;
        color_cfg.transfer_characteristics = extra_cfg.transfer_characteristics;
        color_cfg.matrix_coefficients = extra_cfg.matrix_coefficients;
        color_cfg.color_range = extra_cfg.color_range;
        color_cfg.chroma_sample_position = extra_cfg.chroma_sample_position;

        // Set Group of frames configuration.
        // CONFIG_REALTIME_ONLY is 1.
        // When CONFIG_REALTIME_ONLY=1 and mode=REALTIME, then force lag_in_frames
        // = 0.
        gf_cfg.lag_in_frames = (oxcf.mode == REALTIME)
                                   ? 0
                                   : clamp((int)cfg.g_lag_in_frames, 0, MAX_LAG_BUFFERS);

        // Modify lag_in_frames slightly for better coding performance (e.g. better
        // temporal filtering result).
        if (oxcf.mode == GOOD && gf_cfg.lag_in_frames >= 32 &&
            gf_cfg.lag_in_frames < 39)
            gf_cfg.lag_in_frames = AOMMIN(39, MAX_LAG_BUFFERS);

        gf_cfg.enable_auto_arf = extra_cfg.enable_auto_alt_ref != 0;
        gf_cfg.enable_auto_brf = extra_cfg.enable_auto_bwd_ref != 0;
        gf_cfg.min_gf_interval = (int)extra_cfg.min_gf_interval;
        gf_cfg.max_gf_interval = (int)extra_cfg.max_gf_interval;
        gf_cfg.gf_min_pyr_height = (int)extra_cfg.gf_min_pyr_height;
        gf_cfg.gf_max_pyr_height = (int)extra_cfg.gf_max_pyr_height;

        // Set tune related configuration.
        tune_cfg.tuning = extra_cfg.tuning;
        tune_cfg.vmaf_model_path = extra_cfg.vmaf_model_path;
        tune_cfg.content = extra_cfg.content;
        if (cfg.large_scale_tile != 0)
        {
            tune_cfg.film_grain_test_vector = 0;
            tune_cfg.film_grain_table_filename = null;
        }
        else
        {
            tune_cfg.film_grain_test_vector = extra_cfg.film_grain_test_vector;
            tune_cfg.film_grain_table_filename = extra_cfg.film_grain_table_filename;
        }
        tune_cfg.dist_metric = extra_cfg.dist_metric;
        // CONFIG_DENOISE is 1.
        oxcf.noise_level = extra_cfg.noise_level;
        oxcf.noise_block_size = extra_cfg.noise_block_size;
        oxcf.enable_dnl_denoising = extra_cfg.enable_dnl_denoising;

        // CONFIG_AV1_TEMPORAL_DENOISING is 0: oxcf->noise_sensitivity is compiled out.

        // Set Tile related configuration.
        tile_cfg.num_tile_groups = extra_cfg.num_tg;
        // In large-scale tile encoding mode, num_tile_groups is always 1.
        if (cfg.large_scale_tile != 0) tile_cfg.num_tile_groups = 1;
        tile_cfg.mtu = extra_cfg.mtu_size;
        tile_cfg.enable_large_scale_tile = cfg.large_scale_tile != 0;
        tile_cfg.enable_single_tile_decoding =
            (tile_cfg.enable_large_scale_tile) ? extra_cfg.single_tile_decoding != 0 : false;
        if (extra_cfg.auto_tiles != 0)
        {
            set_auto_tiles(ref tile_cfg, cfg.g_w, cfg.g_h, cfg.g_threads);
            extra_cfg.tile_columns = (uint)tile_cfg.tile_columns;
            extra_cfg.tile_rows = (uint)tile_cfg.tile_rows;
        }
        else
        {
            tile_cfg.tile_columns = (int)extra_cfg.tile_columns;
            tile_cfg.tile_rows = (int)extra_cfg.tile_rows;
        }
        tile_cfg.tile_width_count = AOMMIN(cfg.tile_width_count, MAX_TILE_COLS);
        tile_cfg.tile_height_count = AOMMIN(cfg.tile_height_count, MAX_TILE_ROWS);
        for (int i = 0; i < tile_cfg.tile_width_count; i++)
        {
            tile_cfg.tile_widths[i] = cfg.tile_widths[i];
        }
        for (int i = 0; i < tile_cfg.tile_height_count; i++)
        {
            tile_cfg.tile_heights[i] = cfg.tile_heights[i];
        }
        tile_cfg.enable_ext_tile_debug = extra_cfg.ext_tile_debug != 0;

        if (tile_cfg.enable_large_scale_tile)
        {
            // The superblock_size can only be AOM_SUPERBLOCK_SIZE_64X64 or
            // AOM_SUPERBLOCK_SIZE_128X128 while tile_cfg->enable_large_scale_tile = 1.
            // If superblock_size = AOM_SUPERBLOCK_SIZE_DYNAMIC, hard set it to
            // AOM_SUPERBLOCK_SIZE_64X64(default value in large_scale_tile).
            if (extra_cfg.superblock_size != AOM_SUPERBLOCK_SIZE_64X64 &&
                extra_cfg.superblock_size != AOM_SUPERBLOCK_SIZE_128X128)
                tool_cfg.superblock_size = AOM_SUPERBLOCK_SIZE_64X64;
        }

        // Set reference frame related configuration.
        oxcf.ref_frm_cfg.max_reference_frames = (uint)extra_cfg.max_reference_frames;
        oxcf.ref_frm_cfg.enable_reduced_reference_set =
            extra_cfg.enable_reduced_reference_set != 0;
        oxcf.ref_frm_cfg.enable_onesided_comp = extra_cfg.enable_onesided_comp != 0;

        oxcf.row_mt = extra_cfg.row_mt != 0;
        oxcf.fp_mt = extra_cfg.fp_mt != 0;

        // Set motion mode related configuration.
        oxcf.motion_mode_cfg.enable_obmc = extra_cfg.enable_obmc != 0;
        oxcf.motion_mode_cfg.enable_warped_motion = extra_cfg.enable_warped_motion != 0;
        // CONFIG_REALTIME_ONLY is 1.
        oxcf.motion_mode_cfg.allow_warped_motion =
            (cfg.g_usage == AOM_USAGE_REALTIME && oxcf.speed >= 7)
                ? false
                : (extra_cfg.allow_warped_motion & extra_cfg.enable_warped_motion) != 0;

        // Set partition related configuration.
        part_cfg.enable_rect_partitions = extra_cfg.enable_rect_partitions != 0;
        part_cfg.enable_ab_partitions = extra_cfg.enable_ab_partitions != 0;
        part_cfg.enable_1to4_partitions = extra_cfg.enable_1to4_partitions != 0;
        part_cfg.min_partition_size = extra_cfg.min_partition_size;
        part_cfg.max_partition_size = extra_cfg.max_partition_size;

        // Set intra mode configuration.
        intra_mode_cfg.enable_angle_delta = extra_cfg.enable_angle_delta != 0;
        intra_mode_cfg.enable_intra_edge_filter =
            extra_cfg.enable_intra_edge_filter != 0;
        intra_mode_cfg.enable_filter_intra = extra_cfg.enable_filter_intra != 0;
        intra_mode_cfg.enable_smooth_intra = extra_cfg.enable_smooth_intra != 0;
        intra_mode_cfg.enable_paeth_intra = extra_cfg.enable_paeth_intra != 0;
        intra_mode_cfg.enable_cfl_intra = extra_cfg.enable_cfl_intra != 0;
        intra_mode_cfg.enable_directional_intra =
            extra_cfg.enable_directional_intra != 0;
        intra_mode_cfg.enable_diagonal_intra = extra_cfg.enable_diagonal_intra != 0;
        intra_mode_cfg.auto_intra_tools_off = extra_cfg.auto_intra_tools_off != 0;

        // Set transform size/type configuration.
        txfm_cfg.enable_tx64 = extra_cfg.enable_tx64 != 0;
        txfm_cfg.enable_flip_idtx = extra_cfg.enable_flip_idtx != 0;
        txfm_cfg.enable_rect_tx = extra_cfg.enable_rect_tx != 0;
        txfm_cfg.reduced_tx_type_set = extra_cfg.reduced_tx_type_set != 0;
        txfm_cfg.use_intra_dct_only = extra_cfg.use_intra_dct_only != 0;
        txfm_cfg.use_inter_dct_only = extra_cfg.use_inter_dct_only != 0;
        txfm_cfg.use_intra_default_tx_only = extra_cfg.use_intra_default_tx_only != 0;
        txfm_cfg.enable_tx_size_search = extra_cfg.enable_tx_size_search != 0;

        // Set compound type configuration.
        comp_type_cfg.enable_dist_wtd_comp =
            (extra_cfg.enable_dist_wtd_comp & extra_cfg.enable_order_hint) != 0;
        comp_type_cfg.enable_masked_comp = extra_cfg.enable_masked_comp != 0;
        comp_type_cfg.enable_diff_wtd_comp =
            (extra_cfg.enable_masked_comp & extra_cfg.enable_diff_wtd_comp) != 0;
        comp_type_cfg.enable_interinter_wedge =
            (extra_cfg.enable_masked_comp & extra_cfg.enable_interinter_wedge) != 0;
        comp_type_cfg.enable_smooth_interintra =
            extra_cfg.enable_interintra_comp != 0 && extra_cfg.enable_smooth_interintra != 0;
        comp_type_cfg.enable_interintra_wedge =
            (extra_cfg.enable_interintra_comp & extra_cfg.enable_interintra_wedge) != 0;

        // Set Super-resolution mode configuration.
        if (extra_cfg.lossless != 0 || cfg.large_scale_tile != 0)
        {
            disable_superres(ref superres_cfg);
        }
        else
        {
            superres_cfg.superres_mode = cfg.rc_superres_mode;
            superres_cfg.superres_scale_denominator =
                (byte)cfg.rc_superres_denominator;
            superres_cfg.superres_kf_scale_denominator =
                (byte)cfg.rc_superres_kf_denominator;
            superres_cfg.superres_qthresh =
                av1_quantizer_to_qindex((int)cfg.rc_superres_qthresh);
            superres_cfg.superres_kf_qthresh =
                av1_quantizer_to_qindex((int)cfg.rc_superres_kf_qthresh);
            if (superres_cfg.superres_mode == AOM_SUPERRES_FIXED &&
                superres_cfg.superres_scale_denominator == SCALE_NUMERATOR &&
                superres_cfg.superres_kf_scale_denominator == SCALE_NUMERATOR)
            {
                disable_superres(ref superres_cfg);
            }
            if (superres_cfg.superres_mode == AOM_SUPERRES_QTHRESH &&
                superres_cfg.superres_qthresh == 255 &&
                superres_cfg.superres_kf_qthresh == 255)
            {
                disable_superres(ref superres_cfg);
            }
        }

        superres_cfg.enable_superres =
            (superres_cfg.superres_mode != AOM_SUPERRES_NONE) &&
            extra_cfg.enable_superres != 0;
        if (!superres_cfg.enable_superres)
        {
            disable_superres(ref superres_cfg);
        }

        if (input_cfg.limit == 1)
        {
            // still picture mode, display model and timing is meaningless
            dec_model_cfg.display_model_info_present_flag = false;
            dec_model_cfg.timing_info_present = false;
        }

        oxcf.save_as_annexb = cfg.save_as_annexb != 0;

        // Set unit test related configuration.
        oxcf.unit_test_cfg.motion_vector_unit_test =
            extra_cfg.motion_vector_unit_test;
        oxcf.unit_test_cfg.sb_multipass_unit_test =
            extra_cfg.sb_multipass_unit_test;

        oxcf.border_in_pixels =
            av1_get_enc_border_size(av1_is_resize_needed(oxcf),
                                    (oxcf.kf_cfg.key_freq_max == 0), BLOCK_128X128);
        for (int i = 0; i < MAX_NUM_OPERATING_POINTS; i++)
            oxcf.target_seq_level_idx[i] = extra_cfg.target_seq_level_idx[i];
        oxcf.tier_mask = extra_cfg.tier_mask;

        oxcf.partition_info_path = extra_cfg.partition_info_path;

        oxcf.enable_rate_guide_deltaq = extra_cfg.enable_rate_guide_deltaq;
        oxcf.rate_distribution_info = extra_cfg.rate_distribution_info;

        oxcf.strict_level_conformance = extra_cfg.strict_level_conformance;

        oxcf.kf_max_pyr_height = extra_cfg.kf_max_pyr_height;

        oxcf.sb_qp_sweep = extra_cfg.sb_qp_sweep;
    }

    internal static AV1EncoderConfig av1_get_encoder_config(aom_codec_enc_cfg_t cfg)
    {
        AV1EncoderConfig oxcf = new AV1EncoderConfig();
        av1_extracfg extra_cfg = default_extra_cfg;
        set_encoder_config(oxcf, cfg, ref extra_cfg);
        return oxcf;
    }

    // ENABLE_SETJMP(cpi) / DISABLE_SETJMP(cpi) around av1_change_config: on an error the C code
    // longjmps back, clears setjmp, copies the detail and returns the error code.
    // [port] Returns AOM_CODEC_OK when av1_change_config completed.
    private static aom_codec_err_t change_config_setjmp(aom_codec_alg_priv_t ctx, AV1_COMP cpi,
                                                        bool is_sb_size_changed)
    {
        aom_internal_error_info enable_setjmp_error = cpi.common.error;
        try
        {
            enable_setjmp_error.setjmp = 1;
            av1_change_config(cpi, ctx.oxcf, is_sb_size_changed);
        }
        catch (AomException e)
        {
            aom_set_error_from_exception(enable_setjmp_error, e);
            enable_setjmp_error.setjmp = 0;
            ctx.@base.err_detail = enable_setjmp_error.has_detail != 0
                                       ? enable_setjmp_error.detail
                                       : null;
            return enable_setjmp_error.error_code;
        }
        enable_setjmp_error.setjmp = 0;
        return AOM_CODEC_OK;
    }

    private static aom_codec_err_t encoder_set_config(aom_codec_alg_priv_t ctx,
                                                      aom_codec_enc_cfg_t cfg)
    {
        aom_codec_err_t res;
        int force_key = 0;

        if (cfg.g_w != ctx.cfg.g_w || cfg.g_h != ctx.cfg.g_h)
        {
            if (cfg.g_lag_in_frames > 1 || cfg.g_pass != AOM_RC_ONE_PASS)
                return ERROR(ctx, "Cannot change width or height after initialization");
            // Note: function encoder_set_config() is allowed to be called multiple
            // times. In one-pass mode without lookahead (g_lag_in_frames == 0),
            // and with the maximum frame size declared up front via
            // g_forced_max_frame_width/height, reference frame scaling allows upscaling
            // up to 16x and downscaling by up to 2x without forcing a keyframe. The
            // forced maximum frame size is required because the internal buffers are
            // then allocated for that maximum; without it they are only sized for the
            // current frame size and a larger frame would overflow them. Outside of
            // this mode, or if reference frame scaling constraints are violated, a
            // keyframe is forced (for single spatial layer, since otherwise a previous
            // encoded frame at a lower layer may be the desired reference). To make
            // sure the correct detection of a forced key frame, we need to update the
            // frame width and height only when the actual encoding is performed.
            // cpi->last_coded_width and cpi->last_coded_height are used to track the
            // actual coded frame size.
            bool allow_ref_scaled_upscale =
                cfg.g_forced_max_frame_width != 0 && cfg.g_forced_max_frame_height != 0 &&
                cfg.g_pass == AOM_RC_ONE_PASS && cfg.g_lag_in_frames == 0;
            if (ctx.ppi.cpi.svc.number_spatial_layers == 1 &&
                ctx.ppi.cpi.last_coded_width != 0 && ctx.ppi.cpi.last_coded_height != 0 &&
                (valid_ref_frame_size(ctx.ppi.cpi.last_coded_width,
                                      ctx.ppi.cpi.last_coded_height, (int)cfg.g_w,
                                      (int)cfg.g_h) == 0 ||
                 (!allow_ref_scaled_upscale &&
                  (((int)cfg.g_w > ctx.ppi.cpi.last_coded_width) ||
                   ((int)cfg.g_h > ctx.ppi.cpi.last_coded_height)))))
            {
                force_key = 1;
            }
            // Only a key frame can carry a sequence header with a larger maximum
            // frame size.
            if (ctx.ppi.seq_params_locked != 0 &&
                ((int)cfg.g_w > ctx.ppi.seq_params.max_frame_width ||
                 (int)cfg.g_h > ctx.ppi.seq_params.max_frame_height))
            {
                force_key = 1;
            }
        }

        if (ctx.monochrome_on_init && cfg.monochrome == 0)
        {
            // TODO(aomedia:3465): Allow this case to work without requiring re-init
            // of encoder.
            return ERROR(ctx, "Cannot change to monochrome = 0 after init with monochrome");
        }

        // Prevent increasing lag_in_frames. This check is stricter than it needs
        // to be -- the limit is not increasing past the first lag_in_frames
        // value, but we don't track the initial config, only the last successful
        // config.
        if (cfg.g_lag_in_frames > ctx.cfg.g_lag_in_frames)
            return ERROR(ctx, "Cannot increase lag_in_frames");
        // Prevent changing lag_in_frames if Lookahead Processing is enabled
        if (cfg.g_lag_in_frames != ctx.cfg.g_lag_in_frames &&
            ctx.num_lap_buffers > 0)
            return ERROR(ctx, "Cannot change lag_in_frames if LAP is enabled");

        res = validate_config(ctx, cfg, in ctx.extra_cfg);

        if (res == AOM_CODEC_OK)
        {
            ctx.cfg.CopyFrom(cfg);
            set_encoder_config(ctx.oxcf, ctx.cfg, ref ctx.extra_cfg);
            // On profile change, request a key frame
            force_key |= B(ctx.ppi.seq_params.profile != ctx.oxcf.profile);
            bool is_sb_size_changed = false;
            av1_change_config_seq(ctx.ppi, ctx.oxcf, ref is_sb_size_changed);
            for (int i = 0; i < ctx.ppi.num_fp_contexts; i++)
            {
                AV1_COMP cpi = ctx.ppi.parallel_cpi[i];
                aom_codec_err_t err = change_config_setjmp(ctx, cpi, is_sb_size_changed);
                if (err != AOM_CODEC_OK) return err;
            }
            if (ctx.ppi.cpi_lap != null)
            {
                AV1_COMP cpi = ctx.ppi.cpi_lap;
                aom_codec_err_t err = change_config_setjmp(ctx, cpi, is_sb_size_changed);
                if (err != AOM_CODEC_OK) return err;
            }
        }

        if (force_key != 0) ctx.next_frame_flags |= AOM_EFLAG_FORCE_KF;

        return res;
    }

    // [subset] encoder_get_global_headers (av1_get_global_headers): the realtime API never asks
    // for global headers.

    private static aom_codec_err_t ctrl_get_quantizer(aom_codec_alg_priv_t ctx, va_list args)
    {
        Ptr<int> arg = args.va_arg<Ptr<int>>();
        if (arg.IsNull) return AOM_CODEC_INVALID_PARAM;
        arg.Value = av1_get_quantizer(ctx.ppi.cpi);
        return AOM_CODEC_OK;
    }

    private static aom_codec_err_t ctrl_get_quantizer64(aom_codec_alg_priv_t ctx, va_list args)
    {
        Ptr<int> arg = args.va_arg<Ptr<int>>();
        if (arg.IsNull) return AOM_CODEC_INVALID_PARAM;
        arg.Value = av1_qindex_to_quantizer(av1_get_quantizer(ctx.ppi.cpi));
        return AOM_CODEC_OK;
    }

    private static aom_codec_err_t update_encoder_cfg(aom_codec_alg_priv_t ctx)
    {
        // Disable denoiser for spatial layers. Bug: 485332522.
        // TODO: bug 485332522 - Disable denoiser for spatial layers until
        // more testing is done
        if (ctx.ppi.cpi.svc.number_spatial_layers > 1)
            ctx.extra_cfg.noise_sensitivity = 0;
        set_encoder_config(ctx.oxcf, ctx.cfg, ref ctx.extra_cfg);
        av1_check_fpmt_config(ctx.ppi, ctx.oxcf);
        bool is_sb_size_changed = false;
        av1_change_config_seq(ctx.ppi, ctx.oxcf, ref is_sb_size_changed);
        for (int i = 0; i < ctx.ppi.num_fp_contexts; i++)
        {
            AV1_COMP cpi = ctx.ppi.parallel_cpi[i];
            aom_codec_err_t err = change_config_setjmp(ctx, cpi, is_sb_size_changed);
            if (err != AOM_CODEC_OK) return err;
        }
        if (ctx.ppi.cpi_lap != null)
        {
            AV1_COMP cpi_lap = ctx.ppi.cpi_lap;
            aom_codec_err_t err = change_config_setjmp(ctx, cpi_lap, is_sb_size_changed);
            if (err != AOM_CODEC_OK) return err;
        }
        return AOM_CODEC_OK;
    }

    private static aom_codec_err_t update_extra_cfg(aom_codec_alg_priv_t ctx,
                                                    in av1_extracfg extra_cfg)
    {
        aom_codec_err_t res = validate_config(ctx, ctx.cfg, in extra_cfg);
        if (res == AOM_CODEC_OK)
        {
            ctx.extra_cfg = extra_cfg;
            return update_encoder_cfg(ctx);
        }
        return res;
    }

    private static aom_codec_err_t ctrl_set_cpuused(aom_codec_alg_priv_t ctx, va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.cpu_used = CAST<int>(AOME_SET_CPUUSED, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_noise_sensitivity(aom_codec_alg_priv_t ctx,
                                                              va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.noise_sensitivity = CAST<uint>(AV1E_SET_NOISE_SENSITIVITY, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_row_mt(aom_codec_alg_priv_t ctx, va_list args)
    {
        uint row_mt = CAST<uint>(AV1E_SET_ROW_MT, args);
        if (row_mt == ctx.extra_cfg.row_mt) return AOM_CODEC_OK;
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.row_mt = row_mt;
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_tile_columns(aom_codec_alg_priv_t ctx, va_list args)
    {
        // If the control AUTO_TILES is used (set to 1) then don't override
        // the tile_columns set via the AUTO_TILES control.
        if (ctx.extra_cfg.auto_tiles != 0)
        {
            return ERROR(ctx, "AUTO_TILES is set so AV1E_SET_TILE_COLUMNS should not be called.");
        }
        av1_extracfg extra_cfg = ctx.extra_cfg;
        uint tile_columns = CAST<uint>(AV1E_SET_TILE_COLUMNS, args);
        if (tile_columns == extra_cfg.tile_columns) return AOM_CODEC_OK;
        extra_cfg.tile_columns = tile_columns;
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_tile_rows(aom_codec_alg_priv_t ctx, va_list args)
    {
        // If the control AUTO_TILES is used (set to 1) then don't override
        // the tile_rows set via the AUTO_TILES control.
        if (ctx.extra_cfg.auto_tiles != 0)
        {
            return ERROR(ctx, "AUTO_TILES is set so AV1E_SET_TILE_ROWS should not be called.");
        }
        av1_extracfg extra_cfg = ctx.extra_cfg;
        uint tile_rows = CAST<uint>(AV1E_SET_TILE_ROWS, args);
        if (tile_rows == extra_cfg.tile_rows) return AOM_CODEC_OK;
        extra_cfg.tile_rows = tile_rows;
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_tpl_model(aom_codec_alg_priv_t ctx,
                                                             va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        uint tpl_model_arg = CAST<uint>(AV1E_SET_ENABLE_TPL_MODEL, args);
        // CONFIG_REALTIME_ONLY is 1.
        if (tpl_model_arg != 0)
        {
            return ERROR(ctx, "TPL model can't be turned on in realtime only build.");
        }
        extra_cfg.enable_tpl_model = tpl_model_arg;
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_rc_max_intra_bitrate_pct(aom_codec_alg_priv_t ctx,
                                                                     va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.rc_max_intra_bitrate_pct =
            CAST<uint>(AOME_SET_MAX_INTRA_BITRATE_PCT, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_cdef(aom_codec_alg_priv_t ctx, va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_cdef = CAST<uint>(AV1E_SET_ENABLE_CDEF, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_restoration(aom_codec_alg_priv_t ctx,
                                                               va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        uint restoration_arg = CAST<uint>(AV1E_SET_ENABLE_RESTORATION, args);
        // CONFIG_REALTIME_ONLY is 1.
        if (restoration_arg != 0)
        {
            return ERROR(ctx, "Restoration can't be turned on in realtime only build.");
        }
        extra_cfg.enable_restoration = restoration_arg;
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_obmc(aom_codec_alg_priv_t ctx, va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        uint obmc_arg = CAST<uint>(AV1E_SET_ENABLE_OBMC, args);
        // CONFIG_REALTIME_ONLY is 1.
        if (obmc_arg != 0)
        {
            return ERROR(ctx, "OBMC can't be enabled in realtime only build.");
        }
        extra_cfg.enable_obmc = obmc_arg;
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_disable_trellis_quant(aom_codec_alg_priv_t ctx,
                                                                  va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.disable_trellis_quant = CAST<uint>(AV1E_SET_DISABLE_TRELLIS_QUANT, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_qm(aom_codec_alg_priv_t ctx, va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_qm = CAST<uint>(AV1E_SET_ENABLE_QM, args);
        // CONFIG_QUANT_MATRIX is 1: the "QM can't be enabled" check is compiled out.
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_dual_filter(aom_codec_alg_priv_t ctx,
                                                               va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_dual_filter = CAST<int>(AV1E_SET_ENABLE_DUAL_FILTER, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_rect_partitions(aom_codec_alg_priv_t ctx,
                                                                   va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_rect_partitions =
            CAST<int>(AV1E_SET_ENABLE_RECT_PARTITIONS, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_intra_edge_filter(aom_codec_alg_priv_t ctx,
                                                                     va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_intra_edge_filter =
            CAST<int>(AV1E_SET_ENABLE_INTRA_EDGE_FILTER, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_order_hint(aom_codec_alg_priv_t ctx,
                                                              va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_order_hint = CAST<int>(AV1E_SET_ENABLE_ORDER_HINT, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_tx64(aom_codec_alg_priv_t ctx, va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_tx64 = CAST<int>(AV1E_SET_ENABLE_TX64, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_dist_wtd_comp(aom_codec_alg_priv_t ctx,
                                                                 va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_dist_wtd_comp = CAST<int>(AV1E_SET_ENABLE_DIST_WTD_COMP, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_max_reference_frames(aom_codec_alg_priv_t ctx,
                                                                 va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.max_reference_frames = CAST<int>(AV1E_SET_MAX_REFERENCE_FRAMES, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_ref_frame_mvs(aom_codec_alg_priv_t ctx,
                                                                 va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_ref_frame_mvs = CAST<int>(AV1E_SET_ENABLE_REF_FRAME_MVS, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_masked_comp(aom_codec_alg_priv_t ctx,
                                                               va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_masked_comp = CAST<int>(AV1E_SET_ENABLE_MASKED_COMP, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_interintra_comp(aom_codec_alg_priv_t ctx,
                                                                   va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_interintra_comp =
            CAST<int>(AV1E_SET_ENABLE_INTERINTRA_COMP, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_smooth_interintra(aom_codec_alg_priv_t ctx,
                                                                     va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_smooth_interintra =
            CAST<int>(AV1E_SET_ENABLE_SMOOTH_INTERINTRA, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_diff_wtd_comp(aom_codec_alg_priv_t ctx,
                                                                 va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_diff_wtd_comp = CAST<int>(AV1E_SET_ENABLE_DIFF_WTD_COMP, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_interintra_wedge(aom_codec_alg_priv_t ctx,
                                                                    va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_interintra_wedge =
            CAST<int>(AV1E_SET_ENABLE_INTERINTRA_WEDGE, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_global_motion(aom_codec_alg_priv_t ctx,
                                                                 va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        int global_motion_arg = CAST<int>(AV1E_SET_ENABLE_GLOBAL_MOTION, args);
        // CONFIG_REALTIME_ONLY is 1.
        if (global_motion_arg != 0)
        {
            return ERROR(ctx, "Global motion can't be enabled in realtime only build.");
        }
        extra_cfg.enable_global_motion = global_motion_arg;
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_warped_motion(aom_codec_alg_priv_t ctx,
                                                                 va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        int warped_motion_arg = CAST<int>(AV1E_SET_ENABLE_WARPED_MOTION, args);
        // CONFIG_REALTIME_ONLY is 1.
        if (warped_motion_arg != 0)
        {
            return ERROR(ctx, "Warped motion can't be enabled in realtime only build.");
        }
        extra_cfg.enable_warped_motion = warped_motion_arg;
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_filter_intra(aom_codec_alg_priv_t ctx,
                                                                va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_filter_intra = CAST<int>(AV1E_SET_ENABLE_FILTER_INTRA, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_smooth_intra(aom_codec_alg_priv_t ctx,
                                                                va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_smooth_intra = CAST<int>(AV1E_SET_ENABLE_SMOOTH_INTRA, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_paeth_intra(aom_codec_alg_priv_t ctx,
                                                               va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_paeth_intra = CAST<int>(AV1E_SET_ENABLE_PAETH_INTRA, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_cfl_intra(aom_codec_alg_priv_t ctx,
                                                             va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_cfl_intra = CAST<int>(AV1E_SET_ENABLE_CFL_INTRA, args);
        // CONFIG_REALTIME_ONLY is 1.
        if (extra_cfg.enable_cfl_intra != 0)
        {
            return ERROR(ctx, "cfl can't be turned on in realtime only build.");
        }
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_palette(aom_codec_alg_priv_t ctx,
                                                           va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_palette = CAST<int>(AV1E_SET_ENABLE_PALETTE, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_intrabc(aom_codec_alg_priv_t ctx,
                                                           va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_intrabc = CAST<int>(AV1E_SET_ENABLE_INTRABC, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_enable_angle_delta(aom_codec_alg_priv_t ctx,
                                                               va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.enable_angle_delta = CAST<int>(AV1E_SET_ENABLE_ANGLE_DELTA, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_aq_mode(aom_codec_alg_priv_t ctx, va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.aq_mode = unchecked((byte)CAST<uint>(AV1E_SET_AQ_MODE, args));

        // Skip AQ mode if using fixed QP for current frame.
        if (ctx.ppi.cpi.rc.use_external_qp_one_pass != 0) extra_cfg.aq_mode = 0;
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_intra_default_tx_only(aom_codec_alg_priv_t ctx,
                                                                  va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.use_intra_default_tx_only =
            CAST<int>(AV1E_SET_INTRA_DEFAULT_TX_ONLY, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_coeff_cost_upd_freq(aom_codec_alg_priv_t ctx,
                                                                va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.coeff_cost_upd_freq =
            (COST_UPDATE_TYPE)CAST<uint>(AV1E_SET_COEFF_COST_UPD_FREQ, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_mode_cost_upd_freq(aom_codec_alg_priv_t ctx,
                                                               va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.mode_cost_upd_freq =
            (COST_UPDATE_TYPE)CAST<uint>(AV1E_SET_MODE_COST_UPD_FREQ, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_mv_cost_upd_freq(aom_codec_alg_priv_t ctx,
                                                             va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.mv_cost_upd_freq =
            (COST_UPDATE_TYPE)CAST<uint>(AV1E_SET_MV_COST_UPD_FREQ, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_deltaq_mode(aom_codec_alg_priv_t ctx, va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        byte deltaq_arg = unchecked((byte)CAST<uint>(AV1E_SET_DELTAQ_MODE, args));  // uint8_t
        // CONFIG_REALTIME_ONLY is 1.
        if (deltaq_arg > NO_DELTA_Q)
        {
            return ERROR(ctx, "Delta Q mode can't be enabled in realtime only build.");
        }
        extra_cfg.deltaq_mode = deltaq_arg;
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_max_consec_frame_drop_ms_cbr(aom_codec_alg_priv_t ctx,
                                                                         va_list args)
    {
        AV1_PRIMARY ppi = ctx.ppi;
        AV1_COMP cpi = ppi.cpi;
        int max_consec_drop_ms =
            CAST<int>(AV1E_SET_MAX_CONSEC_FRAME_DROP_MS_CBR, args);
        if (max_consec_drop_ms < 0) return AOM_CODEC_INVALID_PARAM;
        // max_consec_drop_ms will be converted to frame units inside encoder
        // based on framerate (which can change dynamically).
        ctx.oxcf.rc_cfg.max_consec_drop_ms = max_consec_drop_ms;
        cpi.rc.drop_count_consec = 0;
        return AOM_CODEC_OK;
    }

    private static aom_codec_err_t ctrl_set_auto_tiles(aom_codec_alg_priv_t ctx, va_list args)
    {
        uint auto_tiles = CAST<uint>(AV1E_SET_AUTO_TILES, args);
        if (auto_tiles == ctx.extra_cfg.auto_tiles) return AOM_CODEC_OK;
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.auto_tiles = auto_tiles;
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_postencode_drop_rtc(aom_codec_alg_priv_t ctx,
                                                                va_list args)
    {
        AV1_PRIMARY ppi = ctx.ppi;
        AV1_COMP cpi = ppi.cpi;
        int enable_postencode_drop = CAST<int>(AV1E_SET_POSTENCODE_DROP_RTC, args);
        if (enable_postencode_drop > 1 || enable_postencode_drop < 0)
            return AOM_CODEC_INVALID_PARAM;
        cpi.rc.postencode_drop = enable_postencode_drop;
        return AOM_CODEC_OK;
    }

    // [subset] create_stats_buffer: !CONFIG_REALTIME_ONLY.

    internal static aom_codec_err_t av1_create_context_and_bufferpool(
        AV1_PRIMARY ppi, ref AV1_COMP p_cpi, ref BufferPool p_buffer_pool,
        AV1EncoderConfig oxcf, COMPRESSOR_STAGE stage, int lap_lag_in_frames)
    {
        aom_codec_err_t res = AOM_CODEC_OK;
        BufferPool buffer_pool = p_buffer_pool;

        if (buffer_pool == null)
        {
            buffer_pool = new BufferPool();
            buffer_pool.num_frame_bufs =
                (byte)((oxcf.mode == ALLINTRA) ? FRAME_BUFFERS_ALLINTRA : FRAME_BUFFERS);
            buffer_pool.frame_bufs = alloc_array<RefCntBuffer>(buffer_pool.num_frame_bufs);
            // CONFIG_MULTITHREAD is 1.
            if (pthread_mutex_init(ref buffer_pool.pool_mutex, null) != 0)
            {
                buffer_pool.frame_bufs = null;
                buffer_pool.num_frame_bufs = 0;
                return AOM_CODEC_MEM_ERROR;
            }
            p_buffer_pool = buffer_pool;
        }
        p_cpi =
            av1_create_compressor(ppi, oxcf, buffer_pool, stage, lap_lag_in_frames);
        if (p_cpi == null) res = AOM_CODEC_MEM_ERROR;

        return res;
    }

    // [subset] ctrl_set_fp_mt, ctrl_set_auto_intra_tools_off: not in the realtime API's control
    // set.

    private static aom_codec_err_t encoder_init(aom_codec_ctx_t ctx)
    {
        aom_codec_err_t res = AOM_CODEC_OK;

        if (ctx.priv == null)
        {
            aom_codec_alg_priv_t priv = new aom_codec_alg_priv_t();

            ctx.priv = priv;
            ctx.priv.init_flags = ctx.init_flags;

            // Update the reference to the config structure to an internal copy.
            Debug.Assert(ctx.config.enc != null);
            priv.cfg.CopyFrom(ctx.config.enc);
            ctx.config.enc = priv.cfg;

            priv.extra_cfg = default_extra_cfg;
            // Special handling:
            // By default, if omitted: --enable-cdef=1, --screen-detection-mode=1,
            // --qm-min=5, and --qm-max=9.
            // Here we set its default values to --enable-cdef=0,
            // --screen-detection-mode=2, --qm-min=4, and --qm-max=10 when --allintra
            // is turned on.
            // However, if users set --enable-cdef, --screen-detection-mode, --qm-min,
            // or --qm-max, either from the command line or aom_codec_control(), the
            // encoder still respects it.
            if (priv.cfg.g_usage == AOM_USAGE_ALL_INTRA)
            {
                // CDEF has been found to blur images, so it's disabled in all-intra mode
                priv.extra_cfg.enable_cdef = 0;
                // Enable "anti-aliased text and graphics aware" screen detection mode.
                priv.extra_cfg.screen_detection_mode =
                    AOM_SCREEN_DETECTION_ANTIALIASING_AWARE;
                // These QM min/max values have been found to be beneficial for images,
                // when used with an alternative QM formula (see
                // aom_get_qmlevel_allintra()).
                // These values could also be beneficial for other usage modes, but
                // further testing is required.
                priv.extra_cfg.qm_min = DEFAULT_QM_FIRST_ALLINTRA;
                priv.extra_cfg.qm_max = DEFAULT_QM_LAST_ALLINTRA;
            }
            av1_initialize_enc(priv.cfg.g_usage, priv.cfg.rc_end_usage);

            res = validate_config(priv, priv.cfg, in priv.extra_cfg);

            if (res == AOM_CODEC_OK)
            {
                ref int num_lap_buffers = ref priv.num_lap_buffers;
                int lap_lag_in_frames = 0;
                num_lap_buffers = 0;
                priv.timestamp_ratio.den = priv.cfg.g_timebase.den;
                priv.timestamp_ratio.num =
                    (long)priv.cfg.g_timebase.num * TICKS_PER_SEC;
                reduce_ratio(priv.timestamp_ratio);

                set_encoder_config(priv.oxcf, priv.cfg, ref priv.extra_cfg);
                if (priv.oxcf.pass == AOM_RC_ONE_PASS)
                {
                    // Enable look ahead.
                    // CONFIG_REALTIME_ONLY is 1.
                    // When CONFIG_REALTIME_ONLY=1 and mode=REALTIME, then force
                    // lag_in_frames = 0.
                    int lag_in_frames =
                        (priv.oxcf.mode == REALTIME) ? 0 : (int)priv.cfg.g_lag_in_frames;
                    num_lap_buffers =
                        AOMMIN(lag_in_frames,
                               AOMMIN(MAX_LAP_BUFFERS, priv.oxcf.kf_cfg.key_freq_max +
                                                           SCENE_CUT_KEY_TEST_INTERVAL));
                    if ((int)priv.cfg.g_lag_in_frames - (num_lap_buffers) >=
                        LAP_LAG_IN_FRAMES)
                    {
                        lap_lag_in_frames = LAP_LAG_IN_FRAMES;
                    }
                }
                priv.oxcf.use_highbitdepth =
                    (ctx.init_flags & AOM_CODEC_USE_HIGHBITDEPTH) != 0 ? true : false;

                priv.monochrome_on_init = priv.cfg.monochrome != 0;

                priv.ppi = av1_create_primary_compressor(priv.pkt_list.head,
                                                         num_lap_buffers, priv.oxcf);
                if (priv.ppi == null) return AOM_CODEC_MEM_ERROR;

                // !CONFIG_REALTIME_ONLY: create_stats_buffer and the twopass stats buffer
                // setup are compiled out.

                Debug.Assert(priv.ppi.num_fp_contexts >= 1);
                res = av1_create_context_and_bufferpool(
                    priv.ppi, ref priv.ppi.parallel_cpi[0], ref priv.buffer_pool,
                    priv.oxcf, ENCODE_STAGE, -1);
                if (res != AOM_CODEC_OK)
                {
                    priv.@base.err_detail = "av1_create_context_and_bufferpool() failed";
                    return res;
                }
                // !CONFIG_REALTIME_ONLY: twopass_frame.stats_in setup compiled out.
                priv.ppi.cpi = priv.ppi.parallel_cpi[0];

                // Create another compressor if look ahead is enabled
                if (res == AOM_CODEC_OK && num_lap_buffers != 0)
                {
                    // [subset] LAP: num_lap_buffers is 0 for realtime (lag_in_frames is
                    // forced to 0).
                    throw new NotSupportedException("[subset] lookahead processing (LAP)");
                }
            }
        }

        return res;
    }

    internal static void av1_destroy_context_and_bufferpool(AV1_COMP cpi,
                                                            ref BufferPool p_buffer_pool)
    {
        av1_remove_compressor(cpi);
        if (p_buffer_pool != null)
        {
            av1_free_ref_frame_buffers(p_buffer_pool);
            // CONFIG_MULTITHREAD is 1.
            pthread_mutex_destroy(p_buffer_pool.pool_mutex);
            // aom_free(*p_buffer_pool);
            p_buffer_pool = null;
        }
    }

    // [port] The stats buffers are never allocated in the realtime-only build
    // (create_stats_buffer is compiled out), so the frees are no-ops.
    private static void destroy_stats_buffer(aom_codec_alg_priv_t ctx)
    {
        // aom_free(stats_buf_context->total_left_stats);
        // aom_free(stats_buf_context->total_stats);
        // aom_free(frame_stats_buffer);
    }

    private static void check_and_free_string(string default_str, ref string ptr)
    {
        if ((object)ptr == default_str)
        {
            // Default should be a literal. Do not free.
            return;
        }
        // aom_free((void *)*ptr);
        ptr = null;
    }

    private static void destroy_extra_config(ref av1_extracfg extra_cfg)
    {
        // CONFIG_TUNE_VMAF is 0.
        check_and_free_string(default_extra_cfg.two_pass_output,
                              ref extra_cfg.two_pass_output);
        check_and_free_string(default_extra_cfg.two_pass_output,
                              ref extra_cfg.second_pass_log);
        check_and_free_string(default_extra_cfg.partition_info_path,
                              ref extra_cfg.partition_info_path);
        check_and_free_string(default_extra_cfg.rate_distribution_info,
                              ref extra_cfg.rate_distribution_info);
        check_and_free_string(default_extra_cfg.film_grain_table_filename,
                              ref extra_cfg.film_grain_table_filename);
    }

    private static aom_codec_err_t encoder_destroy(aom_codec_alg_priv_t ctx)
    {
        // free(ctx->cx_data);
        ctx.cx_data = null;
        destroy_extra_config(ref ctx.extra_cfg);

        if (ctx.ppi != null)
        {
            AV1_PRIMARY ppi = ctx.ppi;
            if (ppi.cpi != null)
            {
                av1_extrc_delete(ref ppi.cpi.ext_ratectrl);
            }
            for (int i = 0; i < MAX_PARALLEL_FRAMES - 1; i++)
            {
                if (!ppi.parallel_frames_data[i].cx_data.IsNull)
                {
                    // free(ppi->parallel_frames_data[i].cx_data);
                    ppi.parallel_frames_data[i].cx_data = Ptr<byte>.Null;
                }
            }
            // CONFIG_ENTROPY_STATS and CONFIG_INTERNAL_STATS are 0.

            for (int i = 0; i < MAX_PARALLEL_FRAMES; i++)
            {
                av1_destroy_context_and_bufferpool(ppi.parallel_cpi[i],
                                                   ref ctx.buffer_pool);
            }
            ppi.cpi = null;

            if (ppi.cpi_lap != null)
            {
                av1_destroy_context_and_bufferpool(ppi.cpi_lap, ref ctx.buffer_pool_lap);
            }
            av1_remove_primary_compressor(ppi);
        }
        destroy_stats_buffer(ctx);
        // aom_free(ctx);
        return AOM_CODEC_OK;
    }

    private static aom_codec_frame_flags_t get_frame_pkt_flags(AV1_COMP cpi, uint lib_flags)
    {
        aom_codec_frame_flags_t flags = lib_flags << 16;
        if ((lib_flags & FRAMEFLAGS_KEY) != 0) flags |= AOM_FRAME_IS_KEY;
        if ((lib_flags & FRAMEFLAGS_INTRAONLY) != 0) flags |= AOM_FRAME_IS_INTRAONLY;
        if ((lib_flags & FRAMEFLAGS_SWITCH) != 0) flags |= AOM_FRAME_IS_SWITCH;
        if ((lib_flags & FRAMEFLAGS_ERROR_RESILIENT) != 0)
            flags |= AOM_FRAME_IS_ERROR_RESILIENT;
        if (cpi.droppable != 0) flags |= AOM_FRAME_IS_DROPPABLE;

        return flags;
    }

    private static int get_src_border_in_pixels(AV1_COMP cpi, BLOCK_SIZE sb_size)
    {
        if (cpi.oxcf.mode != REALTIME || av1_is_resize_needed(cpi.oxcf))
            return cpi.oxcf.border_in_pixels;

        int sb_size_in_pixels_log2 = mi_size_wide_log2[sb_size] + MI_SIZE_LOG2;
        int sb_aligned_width =
            ALIGN_POWER_OF_TWO(cpi.oxcf.frm_dim_cfg.width, sb_size_in_pixels_log2);
        int sb_aligned_height =
            ALIGN_POWER_OF_TWO(cpi.oxcf.frm_dim_cfg.height, sb_size_in_pixels_log2);
        // Align the border pixels to a multiple of 32.
        int border_pixels_width =
            ALIGN_POWER_OF_TWO(sb_aligned_width - cpi.oxcf.frm_dim_cfg.width, 5);
        int border_pixels_height =
            ALIGN_POWER_OF_TWO(sb_aligned_height - cpi.oxcf.frm_dim_cfg.height, 5);
        int border_in_pixels =
            AOMMAX(AOMMAX(border_pixels_width, border_pixels_height), 32);
        return border_in_pixels;
    }

    // TODO(Mufaddal): Check feasibility of abstracting functions related to LAP
    // into a separate function.
    private static aom_codec_err_t encoder_encode(aom_codec_alg_priv_t ctx, aom_image_t img,
                                                  aom_codec_pts_t pts, uint duration,
                                                  aom_enc_frame_flags_t enc_flags)
    {
        const int kMinCompressedSize = 8192;
        aom_codec_err_t res = AOM_CODEC_OK;
        AV1_PRIMARY ppi = ctx.ppi;
        aom_codec_pts_t ptsvol = pts;
        AV1_COMP_DATA cpi_data = new AV1_COMP_DATA();

        cpi_data.timestamp_ratio = ctx.timestamp_ratio;
        cpi_data.flush = B(img == null);
        // LAP context
        AV1_COMP cpi_lap = ppi.cpi_lap;
        if (ppi.cpi == null) return AOM_CODEC_INVALID_PARAM;

        ppi.cpi.last_coded_width = ppi.cpi.oxcf.frm_dim_cfg.width;
        ppi.cpi.last_coded_height = ppi.cpi.oxcf.frm_dim_cfg.height;

        if (ppi.lap_enabled != 0 && cpi_lap == null &&
            ppi.cpi.oxcf.pass == AOM_RC_ONE_PASS)
            return AOM_CODEC_INVALID_PARAM;

        if (img != null)
        {
            res = validate_img(ctx, img);
            if (res == AOM_CODEC_OK)
            {
                ulong uncompressed_frame_sz64 =
                    (ulong)ALIGN_POWER_OF_TWO_UNSIGNED(ctx.cfg.g_w, 5) *
                    ALIGN_POWER_OF_TWO_UNSIGNED(ctx.cfg.g_h, 5) * (ulong)get_image_bps(img) / 8;
                // [port] size_t is int in the port: buffers larger than 2^31 bytes are refused
                // like C refuses sizes above SIZE_MAX.
                if (uncompressed_frame_sz64 > int.MaxValue) return AOM_CODEC_MEM_ERROR;
                long uncompressed_frame_sz = (long)uncompressed_frame_sz64;

                // Due to the presence of no-show frames, the ctx->cx_data buffer holds
                // compressed data corresponding to multiple frames. As no-show frames are
                // not possible for all intra frame encoding with no forward key frames,
                // the buffer is allocated with a smaller size in this case.
                //
                // For pseudo random input, the compressed frame size is seen to exceed
                // the uncompressed frame size, but is less than 2 times the uncompressed
                // frame size. https://issues.oss-fuzz.com/issues/514006304 further shows
                // that multithreaded bitstream packing may need more than 2 times the
                // uncompressed frame size. Hence the size of the buffer is chosen as 2.5
                // times the uncompressed frame size.
                aom_rational_t multiplier;
                multiplier.num = 8;
                multiplier.den = 1;
                if (ppi.cpi.oxcf.kf_cfg.key_freq_max == 0 &&
                    !ppi.cpi.oxcf.kf_cfg.fwd_kf_enabled)
                {
                    multiplier.num = 5;
                    multiplier.den = 2;
                }
                if (uncompressed_frame_sz > int.MaxValue / multiplier.num)
                    return AOM_CODEC_MEM_ERROR;
                int data_sz = (int)(uncompressed_frame_sz * multiplier.num / multiplier.den);
                if (data_sz < kMinCompressedSize) data_sz = kMinCompressedSize;
                if (ctx.cx_data == null || ctx.cx_data_sz < data_sz)
                {
                    ctx.cx_data_sz = data_sz;
                    // free(ctx->cx_data);
                    ctx.cx_data = new byte[ctx.cx_data_sz];
                }
                for (int i = 0; i < ppi.num_fp_contexts - 1; i++)
                {
                    if (ppi.parallel_frames_data[i].cx_data.IsNull ||
                        ppi.parallel_frames_data[i].cx_data_sz < data_sz)
                    {
                        ppi.parallel_frames_data[i].cx_data_sz = data_sz;
                        // free(ppi->parallel_frames_data[i].cx_data);
                        ppi.parallel_frames_data[i].frame_size = 0;
                        ppi.parallel_frames_data[i].cx_data =
                            new byte[ppi.parallel_frames_data[i].cx_data_sz];
                    }
                }
            }
        }

        aom_codec_pkt_list_init(ctx.pkt_list);

        aom_enc_frame_flags_t flags = enc_flags;

        // The jmp_buf is valid only for the duration of the function that calls
        // setjmp(). Therefore, this function must reset the 'setjmp' field to 0
        // before it returns.
        // [port] setjmp(ppi->error.jmp) becomes the try/catch below.
        try
        {
            ppi.error.setjmp = 1;

            if (ppi.use_svc != 0 && ppi.cpi.svc.use_flexible_mode == 0 && flags == 0)
            {
                // [subset] av1_set_svc_fixed_mode: SVC is not in the subset.
                throw new NotSupportedException("[subset] svc fixed mode");
            }

            // Note(yunqing): While applying encoding flags, always start from enabling
            // all, and then modifying according to the flags. Previous frame's flags are
            // overwritten.
            av1_apply_encoding_flags(ppi.cpi, flags);
            if (cpi_lap != null)
            {
                av1_apply_encoding_flags(cpi_lap, flags);
            }

            // CONFIG_TUNE_VMAF is 0.

            // Handle fixed keyframe intervals
            if (is_stat_generation_stage(ppi.cpi) != 0 || is_one_pass_rt_params(ppi.cpi) != 0)
            {
                if (ctx.cfg.kf_mode == AOM_KF_AUTO &&
                    ctx.cfg.kf_min_dist == ctx.cfg.kf_max_dist)
                {
                    if (ppi.cpi.common.spatial_layer_id == 0 &&
                        ++ctx.fixed_kf_cntr > ctx.cfg.kf_min_dist)
                    {
                        flags |= AOM_EFLAG_FORCE_KF;
                        ctx.fixed_kf_cntr = 1;
                    }
                }
            }

            if (res == AOM_CODEC_OK)
            {
                AV1_COMP cpi = ppi.cpi;

                // Per-frame PSNR is not supported when g_lag_in_frames is greater than 0.
                if ((flags & AOM_EFLAG_CALCULATE_PSNR) != 0 && ctx.cfg.g_lag_in_frames != 0)
                {
                    aom_internal_error(
                        ppi.error, AOM_CODEC_INCAPABLE,
                        "Cannot calculate per-frame PSNR when g_lag_in_frames is nonzero");
                }

                // Don't attempt to freeze internal state when lag is non-zero in order to
                // minimize risk of state leaking when e.g. multi-pass and b-frames are
                // used.
                if ((flags & AOM_EFLAG_FREEZE_INTERNAL_STATE) != 0 &&
                    ctx.cfg.g_lag_in_frames != 0)
                {
                    aom_internal_error(
                        ppi.error, AOM_CODEC_INCAPABLE,
                        "Cannot freeze internal state when g_lag_in_frames is nonzero");
                }

                // Set up internal flags
                // CONFIG_INTERNAL_STATS is 0.
                ppi.b_calculate_psnr = B((ctx.@base.init_flags & AOM_CODEC_USE_PSNR) != 0 ||
                                         (flags & AOM_EFLAG_CALCULATE_PSNR) != 0);

                ppi.b_freeze_internal_state =
                    B((flags & AOM_EFLAG_FREEZE_INTERNAL_STATE) != 0);

                if (img != null)
                {
                    if (ctx.pts_offset_initialized == 0)
                    {
                        ctx.pts_offset = ptsvol;
                        ctx.pts_offset_initialized = 1;
                    }
                    if (ptsvol < ctx.pts_offset)
                    {
                        aom_internal_error(ppi.error, AOM_CODEC_INVALID_PARAM,
                                           "pts is smaller than initial pts");
                    }
                    ptsvol -= ctx.pts_offset;
                    if (ptsvol > long.MaxValue / cpi_data.timestamp_ratio.num)
                    {
                        aom_internal_error(
                            ppi.error, AOM_CODEC_INVALID_PARAM,
                            "conversion of relative pts to ticks would overflow");
                    }
                    long src_time_stamp =
                        timebase_units_to_ticks(cpi_data.timestamp_ratio, ptsvol);
                    // ULONG_MAX > INT64_MAX is false (unsigned long is 32 bits).
                    if (ptsvol > long.MaxValue - (long)duration)
                    {
                        aom_internal_error(ppi.error, AOM_CODEC_INVALID_PARAM,
                                           "relative pts + duration is too big");
                    }
                    aom_codec_pts_t pts_end = ptsvol + (long)duration;
                    if (pts_end > long.MaxValue / cpi_data.timestamp_ratio.num)
                    {
                        aom_internal_error(
                            ppi.error, AOM_CODEC_INVALID_PARAM,
                            "conversion of relative pts + duration to ticks would overflow");
                    }
                    long src_end_time_stamp =
                        timebase_units_to_ticks(cpi_data.timestamp_ratio, pts_end);

                    YV12_BUFFER_CONFIG sd = new YV12_BUFFER_CONFIG();
                    res = image2yuvconfig(img, sd);
                    // When generating a monochrome stream, make |sd| a monochrome image.
                    if (ctx.cfg.monochrome != 0)
                    {
                        sd.u_buffer = Ptr<byte>.Null;
                        sd.v_buffer = Ptr<byte>.Null;
                        sd.uv_stride = 0;
                        sd.monochrome = 1;
                    }
                    int use_highbitdepth = B((sd.flags & YV12_FLAG_HIGHBITDEPTH) != 0);
                    int subsampling_x = sd.subsampling_x;
                    int subsampling_y = sd.subsampling_y;

                    if (ppi.lookahead == null)
                    {
                        int lag_in_frames = cpi_lap != null ? cpi_lap.oxcf.gf_cfg.lag_in_frames
                                                            : cpi.oxcf.gf_cfg.lag_in_frames;
                        AV1EncoderConfig oxcf = cpi.oxcf;
                        BLOCK_SIZE sb_size = av1_select_sb_size(
                            oxcf, oxcf.frm_dim_cfg.width, oxcf.frm_dim_cfg.height,
                            (int)ppi.number_spatial_layers);
                        oxcf.border_in_pixels =
                            av1_get_enc_border_size(av1_is_resize_needed(oxcf),
                                                    oxcf.kf_cfg.key_freq_max == 0, sb_size);
                        for (int i = 0; i < ppi.num_fp_contexts; i++)
                        {
                            ppi.parallel_cpi[i].oxcf.border_in_pixels = oxcf.border_in_pixels;
                        }

                        int src_border_in_pixels = get_src_border_in_pixels(cpi, sb_size);
                        ppi.lookahead = av1_lookahead_init(
                            cpi.oxcf.frm_dim_cfg.width, cpi.oxcf.frm_dim_cfg.height,
                            subsampling_x, subsampling_y, use_highbitdepth, lag_in_frames,
                            src_border_in_pixels, cpi.common.features.byte_alignment,
                            ctx.num_lap_buffers, (cpi.oxcf.kf_cfg.key_freq_max == 0),
                            cpi.alloc_pyramid);
                    }
                    if (ppi.lookahead == null)
                        aom_internal_error(ppi.error, AOM_CODEC_MEM_ERROR,
                                           "Failed to allocate lag buffers");
                    for (int i = 0; i < ppi.num_fp_contexts; i++)
                    {
                        aom_codec_err_t err =
                            av1_check_initial_width(ppi.parallel_cpi[i], use_highbitdepth,
                                                    subsampling_x, subsampling_y);
                        if (err != AOM_CODEC_OK)
                        {
                            aom_internal_error(ppi.error, err,
                                               "av1_check_initial_width() failed");
                        }
                    }
                    if (cpi_lap != null)
                    {
                        aom_codec_err_t err = av1_check_initial_width(
                            cpi_lap, use_highbitdepth, subsampling_x, subsampling_y);
                        if (err != AOM_CODEC_OK)
                        {
                            aom_internal_error(ppi.error, err,
                                               "av1_check_initial_width() failed");
                        }
                    }

                    // Store the original flags in to the frame buffer. Will extract the
                    // key frame flag when we actually encode this frame.
                    if (av1_receive_raw_frame(cpi, flags | ctx.next_frame_flags, sd,
                                              src_time_stamp, src_end_time_stamp) != 0)
                    {
                        res = update_error_state(ctx, cpi.common.error);
                    }
                    ctx.next_frame_flags = 0;
                }

                cpi_data.cx_data = ctx.cx_data;
                cpi_data.cx_data_sz = ctx.cx_data_sz;

                /* Any pending invisible frames? */
                if (ctx.pending_cx_data_sz != 0)
                {
                    cpi_data.cx_data += ctx.pending_cx_data_sz;
                    cpi_data.cx_data_sz -= ctx.pending_cx_data_sz;

                    /* TODO: this is a minimal check, the underlying codec doesn't respect
                     * the buffer size anyway.
                     */
                    if (cpi_data.cx_data_sz < ctx.cx_data_sz / 2)
                    {
                        aom_internal_error(ppi.error, AOM_CODEC_ERROR,
                                           "Compressed data buffer too small");
                    }
                }

                int is_frame_visible = 0;
                int has_no_show_keyframe = 0;
                int num_workers = 0;

                if (cpi.oxcf.pass == AOM_RC_FIRST_PASS)
                {
                    // !CONFIG_REALTIME_ONLY: av1_fp_compute_num_enc_workers compiled out.
                }
                else
                {
                    av1_compute_num_workers_for_mt(cpi);
                    num_workers = av1_get_max_num_workers(cpi);
                }
                if (num_workers > 1 && ppi.p_mt_info.num_workers < num_workers)
                {
                    // Obtain the maximum no. of frames that can be supported in a parallel
                    // encode set.
                    if (is_stat_consumption_stage(cpi) != 0)
                    {
                        // [subset] av1_compute_num_fp_contexts: stats consumption (two-pass /
                        // LAP) is not in the subset.
                        throw new NotSupportedException("[subset] frame parallel contexts");
                    }
                    if (ppi.p_mt_info.num_workers > 0)
                    {
                        av1_terminate_workers(ppi);
                        free_thread_data(ppi);
                        // aom_free(ppi->p_mt_info.tile_thr_data);
                        ppi.p_mt_info.tile_thr_data = null;
                        // aom_free(ppi->p_mt_info.workers);
                        ppi.p_mt_info.workers = null;
                        ppi.p_mt_info.num_workers = 0;
                        for (int j = 0; j < ppi.num_fp_contexts; j++)
                        {
                            // aom_free(ppi->parallel_cpi[j]->td.tctx);
                            ppi.parallel_cpi[j].td.tctx = null;
                        }
                    }
                    av1_create_workers(ppi, num_workers);
                    av1_init_tile_thread_data(ppi, B(cpi.oxcf.pass == AOM_RC_FIRST_PASS));
                }

                // Re-allocate thread data if workers for encoder multi-threading stage
                // exceeds prev_num_enc_workers.
                int num_enc_workers =
                    av1_get_num_mod_workers_for_alloc(ppi.p_mt_info, MOD_ENC);
                if (ppi.p_mt_info.prev_num_enc_workers < num_enc_workers &&
                    num_enc_workers <= ppi.p_mt_info.num_workers)
                {
                    free_thread_data(ppi);
                    for (int j = 0; j < ppi.num_fp_contexts; j++)
                    {
                        // aom_free(ppi->parallel_cpi[j]->td.tctx);
                        ppi.parallel_cpi[j].td.tctx = null;
                    }
                    av1_init_tile_thread_data(ppi, B(cpi.oxcf.pass == AOM_RC_FIRST_PASS));
                }

                for (int i = 0; i < ppi.num_fp_contexts; i++)
                {
                    av1_init_frame_mt(ppi, ppi.parallel_cpi[i]);
                }
                if (cpi_lap != null)
                {
                    av1_init_frame_mt(ppi, cpi_lap);
                }
                // CONFIG_MULTITHREAD is 1.
                if (ppi.p_mt_info.num_workers > 1)
                {
                    for (int i = 0; i < ppi.num_fp_contexts; i++)
                    {
                        av1_init_mt_sync(ppi.parallel_cpi[i],
                                         B(ppi.parallel_cpi[i].oxcf.pass == AOM_RC_FIRST_PASS));
                    }
                    if (cpi_lap != null)
                    {
                        av1_init_mt_sync(cpi_lap, 1);
                    }
                }

                // Call for LAP stage
                if (cpi_lap != null && is_one_pass_rt_lag_params(cpi) == 0)
                {
                    if (cpi_lap.ppi.b_freeze_internal_state != 0)
                    {
                        av1_save_all_coding_context(cpi_lap);
                    }
                    AV1_COMP_DATA cpi_lap_data = new AV1_COMP_DATA();
                    cpi_lap_data.flush = B(img == null);
                    cpi_lap_data.timestamp_ratio = ctx.timestamp_ratio;
                    int status = av1_get_compressed_data(cpi_lap, cpi_lap_data);
                    if (status > AOM_CODEC_OK)
                    {
                        aom_internal_error_copy(ppi.error, cpi_lap.common.error);
                    }
                    av1_post_encode_updates(cpi_lap, cpi_lap_data);
                    if (cpi_lap.ppi.b_freeze_internal_state != 0)
                    {
                        restore_all_coding_context(cpi_lap);
                    }
                }

                // Recalculate the maximum number of frames that can be encoded in
                // parallel at the beginning of sub gop.
                if (is_stat_consumption_stage(cpi) != 0 && ppi.gf_group.size > 0 &&
                    cpi.gf_frame_index == ppi.gf_group.size)
                {
                    // [subset] av1_compute_num_fp_contexts (stats consumption stage).
                    throw new NotSupportedException("[subset] frame parallel contexts");
                }

                // Get the next visible frame. Invisible frames get packed with the next
                // visible frame.
                while (cpi_data.cx_data_sz >= ctx.cx_data_sz / 2 && is_frame_visible == 0)
                {
                    int simulate_parallel_frame = 0;
                    int status = -1;
                    cpi.do_frame_data_update = true;
                    cpi.ref_idx_to_skip = INVALID_IDX;
                    cpi.ref_refresh_index = INVALID_IDX;
                    cpi.refresh_idx_available = false;
                    if (cpi.ppi.b_freeze_internal_state != 0)
                    {
                        av1_save_all_coding_context(cpi);
                    }

                    // CONFIG_FPMT_TEST is 0.
                    if (simulate_parallel_frame == 0)
                    {
                        // Add a range check here to give an error for frame_parallel_level
                        // array out-of-bounds access.
                        if (cpi.gf_frame_index >= MAX_STATIC_GF_GROUP_LENGTH)
                        {
                            aom_internal_error(ppi.error, AOM_CODEC_ERROR,
                                               "cpi->gf_frame_index is out of range");
                        }

                        // May need a better way for checking the frame's frame_parallel_level,
                        // especially for the first frame of the following gop.
                        if (ppi.gf_group.frame_parallel_level[cpi.gf_frame_index] == 0)
                        {
                            status = av1_get_compressed_data(cpi, cpi_data);
                        }
                        else
                        {
                            // [subset] av1_compress_parallel_frames /
                            // av1_get_parallel_frame_enc_data: frame parallel encoding
                            // (frame_parallel_level 1 and 2) needs a lookahead GOP.
                            throw new NotSupportedException("[subset] frame parallel encoding");
                        }
                    }
                    if (status == -1) break;
                    if (status != AOM_CODEC_OK)
                    {
                        aom_internal_error_copy(ppi.error, cpi.common.error);
                    }
                    if (ppi.num_fp_contexts > 0 && frame_is_intra_only(cpi.common) != 0)
                    {
                        av1_init_sc_decisions(ppi);
                    }

                    ppi.seq_params_locked = 1;
                    av1_post_encode_updates(cpi, cpi_data);

                    if (cpi.ppi.b_freeze_internal_state != 0)
                    {
                        restore_all_coding_context(cpi);
                    }

                    // CONFIG_ENTROPY_STATS and CONFIG_INTERNAL_STATS are 0.

                    if (cpi_data.frame_size == 0) continue;
                    Debug.Assert(!cpi_data.cx_data.IsNull && cpi_data.cx_data_sz != 0);
                    if (cpi_data.frame_size > cpi_data.cx_data_sz)
                    {
                        aom_internal_error(ppi.error, AOM_CODEC_ERROR,
                                           "cpi_data.cx_data buffer overflow");
                    }
                    int write_temporal_delimiter =
                        B(cpi.common.spatial_layer_id == 0 && ctx.pending_cx_data_sz == 0);

                    if (write_temporal_delimiter != 0)
                    {
                        uint obu_header_size = 1;
                        const uint obu_payload_size = 0;
                        int length_field_size =
                            aom_uleb_size_in_bytes(obu_payload_size);

                        int move_offset = (int)obu_header_size + length_field_size;
                        Debug.Assert(ctx.cx_data_sz == cpi_data.cx_data_sz);
                        if (move_offset > ctx.cx_data_sz - cpi_data.frame_size)
                        {
                            aom_internal_error(ppi.error, AOM_CODEC_ERROR,
                                               "ctx->cx_data buffer full");
                        }
                        memmove<byte>((Ptr<byte>)ctx.cx_data + move_offset, ctx.cx_data, cpi_data.frame_size);
                        obu_header_size = av1_write_obu_header(
                            ppi.level_params, ref cpi.frame_header_count,
                            OBU_TEMPORAL_DELIMITER,
                            ppi.seq_params.has_nonzero_operating_point_idc,
                            /*is_layer_specific_obu=*/false, 0, ctx.cx_data);
                        if (obu_header_size != 1)
                        {
                            aom_internal_error(ppi.error, AOM_CODEC_ERROR, null);
                        }

                        // OBUs are preceded/succeeded by an unsigned leb128 coded integer.
                        if (av1_write_uleb_obu_size((int)obu_payload_size,
                                                    (Ptr<byte>)ctx.cx_data + (int)obu_header_size,
                                                    length_field_size) != AOM_CODEC_OK)
                        {
                            aom_internal_error(ppi.error, AOM_CODEC_ERROR, null);
                        }

                        cpi_data.frame_size += move_offset;
                    }

                    if (ctx.oxcf.save_as_annexb)
                    {
                        if (av1_convert_sect5obus_to_annexb(
                                cpi_data.cx_data, cpi_data.cx_data_sz, ref cpi_data.frame_size) !=
                            AOM_CODEC_OK)
                        {
                            aom_internal_error(ppi.error, AOM_CODEC_ERROR, null);
                        }

                        // B_PRIME (add frame size)
                        int length_field_size =
                            aom_uleb_size_in_bytes((ulong)cpi_data.frame_size);
                        if (length_field_size > cpi_data.cx_data_sz - cpi_data.frame_size)
                        {
                            aom_internal_error(ppi.error, AOM_CODEC_ERROR,
                                               "cpi_data.cx_data buffer full");
                        }
                        memmove(cpi_data.cx_data + length_field_size, cpi_data.cx_data,
                                cpi_data.frame_size);
                        if (av1_write_uleb_obu_size(cpi_data.frame_size, cpi_data.cx_data,
                                                    length_field_size) != AOM_CODEC_OK)
                        {
                            aom_internal_error(ppi.error, AOM_CODEC_ERROR, null);
                        }
                        cpi_data.frame_size += length_field_size;
                    }

                    ctx.pending_cx_data_sz += cpi_data.frame_size;

                    cpi_data.cx_data += cpi_data.frame_size;
                    cpi_data.cx_data_sz -= cpi_data.frame_size;

                    is_frame_visible = cpi.common.show_frame;

                    has_no_show_keyframe |=
                        B(is_frame_visible == 0 &&
                          cpi.common.current_frame.frame_type == KEY_FRAME);
                }
                if (is_frame_visible != 0)
                {
                    // Add the frame packet to the list of returned packets.
                    aom_codec_cx_pkt_t pkt = new aom_codec_cx_pkt_t();

                    // decrement frames_left counter
                    ppi.frames_left = AOMMAX(0, ppi.frames_left - 1);
                    if (ctx.oxcf.save_as_annexb)
                    {
                        //  B_PRIME (add TU size)
                        int tu_size = ctx.pending_cx_data_sz;
                        int length_field_size = aom_uleb_size_in_bytes((ulong)tu_size);
                        if (tu_size > ctx.cx_data_sz)
                        {
                            aom_internal_error(ppi.error, AOM_CODEC_ERROR,
                                               "ctx->cx_data buffer overflow");
                        }
                        if (length_field_size > ctx.cx_data_sz - tu_size)
                        {
                            aom_internal_error(ppi.error, AOM_CODEC_ERROR,
                                               "ctx->cx_data buffer full");
                        }
                        memmove<byte>((Ptr<byte>)ctx.cx_data + length_field_size, ctx.cx_data, tu_size);
                        if (av1_write_uleb_obu_size(tu_size, ctx.cx_data, length_field_size) !=
                            AOM_CODEC_OK)
                        {
                            aom_internal_error(ppi.error, AOM_CODEC_ERROR, null);
                        }
                        ctx.pending_cx_data_sz += length_field_size;
                    }

                    pkt.kind = AOM_CODEC_CX_FRAME_PKT;

                    pkt.data.frame.buf = ctx.cx_data;
                    pkt.data.frame.sz = ctx.pending_cx_data_sz;
                    pkt.data.frame.partition_id = -1;
                    pkt.data.frame.vis_frame_size = cpi_data.frame_size;

                    pkt.data.frame.pts = ticks_to_timebase_units(cpi_data.timestamp_ratio,
                                                                 cpi_data.ts_frame_start) +
                                         ctx.pts_offset;
                    pkt.data.frame.flags = get_frame_pkt_flags(cpi, cpi_data.lib_flags);
                    if (has_no_show_keyframe != 0)
                    {
                        // If one of the invisible frames in the packet is a keyframe, set
                        // the delayed random access point flag.
                        pkt.data.frame.flags |= AOM_FRAME_IS_DELAYED_RANDOM_ACCESS_POINT;
                    }
                    long duration64 = ticks_to_timebase_units(
                        cpi_data.timestamp_ratio,
                        cpi_data.ts_frame_end - cpi_data.ts_frame_start);
                    if (duration64 > uint.MaxValue)
                    {
                        aom_internal_error(ppi.error, AOM_CODEC_ERROR, null);
                    }
                    pkt.data.frame.duration = (uint)duration64;

                    aom_codec_pkt_list_add(ctx.pkt_list.head, pkt);

                    ctx.pending_cx_data_sz = 0;
                }
            }
        }
        catch (AomException e)
        {
            aom_set_error_from_exception(ppi.error, e);
            ppi.error.setjmp = 0;
            res = update_error_state(ctx, ppi.error);
            return res;
        }

        ppi.error.setjmp = 0;
        return res;
    }

    private static aom_codec_cx_pkt_t encoder_get_cxdata(aom_codec_alg_priv_t ctx,
                                                         ref aom_codec_iter_t iter)
    {
        return aom_codec_pkt_list_get(ctx.pkt_list.head, ref iter);
    }

    // [subset] ctrl_set_reference, ctrl_copy_reference, ctrl_get_reference,
    // ctrl_get_activemap and the other reference / preview controls: not used by the realtime API.

    private static aom_codec_err_t ctrl_get_new_frame_image(aom_codec_alg_priv_t ctx,
                                                            va_list args)
    {
        aom_image_t new_img = args.va_arg<aom_image_t>();

        if (new_img != null)
        {
            YV12_BUFFER_CONFIG new_frame = new YV12_BUFFER_CONFIG();

            if (av1_get_last_show_frame(ctx.ppi.cpi, new_frame) == 0)
            {
                yuvconfig2image(new_img, new_frame, null);
                return AOM_CODEC_OK;
            }
            else
            {
                return AOM_CODEC_ERROR;
            }
        }
        else
        {
            return AOM_CODEC_INVALID_PARAM;
        }
    }

    private static aom_codec_err_t ctrl_set_color_primaries(aom_codec_alg_priv_t ctx,
                                                            va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.color_primaries = CAST<int>(AV1E_SET_COLOR_PRIMARIES, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_transfer_characteristics(
        aom_codec_alg_priv_t ctx, va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.transfer_characteristics =
            CAST<int>(AV1E_SET_TRANSFER_CHARACTERISTICS, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_matrix_coefficients(aom_codec_alg_priv_t ctx,
                                                                va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.matrix_coefficients = CAST<int>(AV1E_SET_MATRIX_COEFFICIENTS, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_color_range(aom_codec_alg_priv_t ctx,
                                                        va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.color_range = CAST<int>(AV1E_SET_COLOR_RANGE, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    private static aom_codec_err_t ctrl_set_superblock_size(aom_codec_alg_priv_t ctx,
                                                            va_list args)
    {
        av1_extracfg extra_cfg = ctx.extra_cfg;
        extra_cfg.superblock_size = (aom_superblock_size_t)CAST<uint>(AV1E_SET_SUPERBLOCK_SIZE, args);
        return update_extra_cfg(ctx, in extra_cfg);
    }

    // [subset] The control map lists the controls the realtime API uses (the set
    // tools/oracle/av1rt_oracle.c and Api/Av1Encoder.cs pass). Other control IDs are not found
    // and aom_codec_control returns AOM_CODEC_ERROR ("Invalid control ID").
    private static readonly aom_codec_ctrl_fn_map_t[] encoder_ctrl_maps =
    {
        new(AOME_SET_CPUUSED, ctrl_set_cpuused),
        new(AV1E_SET_ROW_MT, ctrl_set_row_mt),
        new(AV1E_SET_TILE_COLUMNS, ctrl_set_tile_columns),
        new(AV1E_SET_TILE_ROWS, ctrl_set_tile_rows),
        new(AV1E_SET_ENABLE_TPL_MODEL, ctrl_set_enable_tpl_model),
        new(AOME_SET_MAX_INTRA_BITRATE_PCT, ctrl_set_rc_max_intra_bitrate_pct),
        new(AV1E_SET_ENABLE_CDEF, ctrl_set_enable_cdef),
        new(AV1E_SET_ENABLE_RESTORATION, ctrl_set_enable_restoration),
        new(AV1E_SET_ENABLE_OBMC, ctrl_set_enable_obmc),
        new(AV1E_SET_DISABLE_TRELLIS_QUANT, ctrl_set_disable_trellis_quant),
        new(AV1E_SET_ENABLE_QM, ctrl_set_enable_qm),
        new(AV1E_SET_ENABLE_RECT_PARTITIONS, ctrl_set_enable_rect_partitions),
        new(AV1E_SET_ENABLE_DUAL_FILTER, ctrl_set_enable_dual_filter),
        new(AV1E_SET_ENABLE_INTRA_EDGE_FILTER, ctrl_set_enable_intra_edge_filter),
        new(AV1E_SET_ENABLE_ORDER_HINT, ctrl_set_enable_order_hint),
        new(AV1E_SET_ENABLE_TX64, ctrl_set_enable_tx64),
        new(AV1E_SET_ENABLE_DIST_WTD_COMP, ctrl_set_enable_dist_wtd_comp),
        new(AV1E_SET_MAX_REFERENCE_FRAMES, ctrl_set_max_reference_frames),
        new(AV1E_SET_ENABLE_REF_FRAME_MVS, ctrl_set_enable_ref_frame_mvs),
        new(AV1E_SET_ENABLE_MASKED_COMP, ctrl_set_enable_masked_comp),
        new(AV1E_SET_ENABLE_INTERINTRA_COMP, ctrl_set_enable_interintra_comp),
        new(AV1E_SET_ENABLE_SMOOTH_INTERINTRA, ctrl_set_enable_smooth_interintra),
        new(AV1E_SET_ENABLE_DIFF_WTD_COMP, ctrl_set_enable_diff_wtd_comp),
        new(AV1E_SET_ENABLE_INTERINTRA_WEDGE, ctrl_set_enable_interintra_wedge),
        new(AV1E_SET_ENABLE_GLOBAL_MOTION, ctrl_set_enable_global_motion),
        new(AV1E_SET_ENABLE_WARPED_MOTION, ctrl_set_enable_warped_motion),
        new(AV1E_SET_ENABLE_FILTER_INTRA, ctrl_set_enable_filter_intra),
        new(AV1E_SET_ENABLE_SMOOTH_INTRA, ctrl_set_enable_smooth_intra),
        new(AV1E_SET_ENABLE_PAETH_INTRA, ctrl_set_enable_paeth_intra),
        new(AV1E_SET_ENABLE_CFL_INTRA, ctrl_set_enable_cfl_intra),
        new(AV1E_SET_ENABLE_PALETTE, ctrl_set_enable_palette),
        new(AV1E_SET_ENABLE_INTRABC, ctrl_set_enable_intrabc),
        new(AV1E_SET_ENABLE_ANGLE_DELTA, ctrl_set_enable_angle_delta),
        new(AV1E_SET_AQ_MODE, ctrl_set_aq_mode),
        new(AV1E_SET_INTRA_DEFAULT_TX_ONLY, ctrl_set_intra_default_tx_only),
        new(AV1E_SET_COEFF_COST_UPD_FREQ, ctrl_set_coeff_cost_upd_freq),
        new(AV1E_SET_MODE_COST_UPD_FREQ, ctrl_set_mode_cost_upd_freq),
        new(AV1E_SET_MV_COST_UPD_FREQ, ctrl_set_mv_cost_upd_freq),
        new(AV1E_SET_DELTAQ_MODE, ctrl_set_deltaq_mode),
        new(AV1E_SET_COLOR_PRIMARIES, ctrl_set_color_primaries),
        new(AV1E_SET_TRANSFER_CHARACTERISTICS, ctrl_set_transfer_characteristics),
        new(AV1E_SET_MATRIX_COEFFICIENTS, ctrl_set_matrix_coefficients),
        new(AV1E_SET_COLOR_RANGE, ctrl_set_color_range),
        new(AV1E_SET_NOISE_SENSITIVITY, ctrl_set_noise_sensitivity),
        new(AV1E_SET_SUPERBLOCK_SIZE, ctrl_set_superblock_size),
        new(AV1E_SET_AUTO_TILES, ctrl_set_auto_tiles),
        new(AV1E_SET_POSTENCODE_DROP_RTC, ctrl_set_postencode_drop_rtc),
        new(AV1E_SET_MAX_CONSEC_FRAME_DROP_MS_CBR, ctrl_set_max_consec_frame_drop_ms_cbr),

        // Getters
        new(AOME_GET_LAST_QUANTIZER, ctrl_get_quantizer),
        new(AOME_GET_LAST_QUANTIZER_64, ctrl_get_quantizer64),
        new(AV1_GET_NEW_FRAME_IMAGE, ctrl_get_new_frame_image),

        CTRL_MAP_END,
    };

    // CONFIG_REALTIME_ONLY is 1: only the AOM_USAGE_REALTIME entry is compiled in.
    private static readonly aom_codec_enc_cfg_t[] encoder_usage_cfg =
    {
        make_realtime_usage_cfg(),
    };

    private static aom_codec_enc_cfg_t make_realtime_usage_cfg()
    {
        aom_codec_enc_cfg_t c = new aom_codec_enc_cfg_t();
        // NOLINT
        c.g_usage = AOM_USAGE_REALTIME;  // g_usage - real-time usage
        c.g_threads = 0;                 // g_threads
        c.g_profile = 0;                 // g_profile

        c.g_w = 320;                     // g_w
        c.g_h = 240;                     // g_h
        c.g_limit = 0;                   // g_limit
        c.g_forced_max_frame_width = 0;  // g_forced_max_frame_width
        c.g_forced_max_frame_height = 0; // g_forced_max_frame_height
        c.g_bit_depth = AOM_BITS_8;      // g_bit_depth
        c.g_input_bit_depth = 8;         // g_input_bit_depth

        c.g_timebase.num = 1;            // g_timebase
        c.g_timebase.den = 30;

        c.g_error_resilient = 0;         // g_error_resilient

        c.g_pass = AOM_RC_ONE_PASS;      // g_pass

        c.g_lag_in_frames = 0;           // g_lag_in_frames

        c.rc_dropframe_thresh = 0;                     // rc_dropframe_thresh
        c.rc_resize_mode = RESIZE_NONE;                // rc_resize_mode
        c.rc_resize_denominator = SCALE_NUMERATOR;     // rc_resize_denominator
        c.rc_resize_kf_denominator = SCALE_NUMERATOR;  // rc_resize_kf_denominator

        c.rc_superres_mode = AOM_SUPERRES_NONE;          // rc_superres_mode
        c.rc_superres_denominator = SCALE_NUMERATOR;     // rc_superres_denominator
        c.rc_superres_kf_denominator = SCALE_NUMERATOR;  // rc_superres_kf_denominator
        c.rc_superres_qthresh = 63;                      // rc_superres_qthresh
        c.rc_superres_kf_qthresh = 32;                   // rc_superres_kf_qthresh

        c.rc_end_usage = AOM_CBR;        // rc_end_usage
        c.rc_twopass_stats_in = default; // rc_twopass_stats_in
        c.rc_firstpass_mb_stats_in = default;  // rc_firstpass_mb_stats_in
        c.rc_target_bitrate = 256;       // rc_target_bitrate
        c.rc_min_quantizer = 0;          // rc_min_quantizer
        c.rc_max_quantizer = 63;         // rc_max_quantizer
        c.rc_undershoot_pct = 50;        // rc_undershoot_pct
        c.rc_overshoot_pct = 50;         // rc_overshoot_pct

        c.rc_buf_sz = 1000;              // rc_buf_sz
        c.rc_buf_initial_sz = 600;       // rc_buf_initial_sz
        c.rc_buf_optimal_sz = 600;       // rc_buf_optimal_sz

        c.rc_2pass_vbr_bias_pct = 50;          // rc_2pass_vbr_bias_pct
        c.rc_2pass_vbr_minsection_pct = 0;     // rc_2pass_vbr_minsection_pct
        c.rc_2pass_vbr_maxsection_pct = 2000;  // rc_2pass_vbr_maxsection_pct

        // keyframing settings (kf)
        c.fwd_kf_enabled = 0;            // fwd_kf_enabled
        c.kf_mode = AOM_KF_AUTO;         // kf_mode
        c.kf_min_dist = 0;               // kf_min_dist
        c.kf_max_dist = 9999;            // kf_max_dist
        c.sframe_dist = 0;               // sframe_dist
        c.sframe_mode = 1;               // sframe_mode
        c.large_scale_tile = 0;          // large_scale_tile
        c.monochrome = 0;                // monochrome
        c.full_still_picture_hdr = 0;    // full_still_picture_hdr
        c.save_as_annexb = 0;            // save_as_annexb
        c.tile_width_count = 0;          // tile_width_count
        c.tile_height_count = 0;         // tile_height_count
        c.tile_widths = default;         // tile_widths
        c.tile_heights = default;        // tile_heights
        c.use_fixed_qp_offsets = 0;      // use_fixed_qp_offsets
        for (int i = 0; i < 5; i++) c.fixed_qp_offsets[i] = -1;  // fixed_qp_offsets
        // encoder_cfg: { 0, 128, 128, 4, 0, ... }
        c.encoder_cfg = default;
        c.encoder_cfg.init_by_cfg_file = 0;
        c.encoder_cfg.super_block_size = 128;
        c.encoder_cfg.max_partition_size = 128;
        c.encoder_cfg.min_partition_size = 4;
        return c;
    }

    // This data structure and function are exported in aom/aomcx.h
    // [port] VERSION_STRING comes from the oracle build's config/aom_version.h.
    private const string VERSION_STRING = " 3.15.3-232-gd03d2c63a1";

    private static readonly aom_codec_iface_t aom_codec_av1_cx_algo = new aom_codec_iface_t
    {
        name = "AOMedia Project AV1 Encoder" + VERSION_STRING,
        abi_version = AOM_CODEC_INTERNAL_ABI_VERSION,
        // CONFIG_AV1_HIGHBITDEPTH is 0.
        caps = AOM_CODEC_CAP_ENCODER | AOM_CODEC_CAP_PSNR,  // aom_codec_caps_t
        init = encoder_init,                                // aom_codec_init_fn_t
        destroy = encoder_destroy,                          // aom_codec_destroy_fn_t
        ctrl_maps = encoder_ctrl_maps,                      // aom_codec_ctrl_fn_map_t
        // [port] The decoder function table (all NULL) is not part of the port's iface.
        enc = new aom_codec_enc_iface
        {
            cfg_count = encoder_usage_cfg.Length,  // cfg_count
            cfgs = encoder_usage_cfg,              // aom_codec_enc_cfg_t
            encode = encoder_encode,               // aom_codec_encode_fn_t
            get_cx_data = encoder_get_cxdata,      // aom_codec_get_cx_data_fn_t
            cfg_set = encoder_set_config,          // aom_codec_enc_config_set_fn_t
            // [subset] encoder_get_global_headers and encoder_get_preview never run.
            get_glob_hdrs = null,                  // aom_codec_get_global_headers_fn_t
            get_preview = null,                    // aom_codec_get_preview_frame_fn_t
        },
        // [subset] encoder_set_option (aom_codec_set_option) never runs.
        set_option = null,  // aom_codec_set_option_fn_t
    };

    internal static aom_codec_iface_t aom_codec_av1_cx() { return aom_codec_av1_cx_algo; }
}
