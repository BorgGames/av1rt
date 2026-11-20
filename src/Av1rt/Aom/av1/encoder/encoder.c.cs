// Port of libaom av1/encoder/encoder.c

using System;
using System.Diagnostics;

using Av1rt.Diagnostics;

namespace Av1rt.Aom;

// [subset] Only the functions executed by the one-pass realtime (CONFIG_REALTIME_ONLY) encode
// path are ported. Two-pass / LAP / TPL / temporal filter / tune (VMAF, butteraugli, SSIM) /
// internal stats / film grain / superres recode / large-scale tile / SVC layer paths are not.
internal static partial class encoder
{
    private const int DEFAULT_EXPLICIT_ORDER_HINT_BITS = 7;

    internal static void av1_initialize_enc(uint usage, int end_usage)
    {
        bool is_allintra = usage == ALLINTRA;

        // [port] av1_rtcd() / aom_dsp_rtcd() / aom_scale_rtcd(): run-time CPU dispatch setup is
        // not needed, the C reference (_c) functions are called directly.
        av1_init_intra_predictors();
        av1_init_me_luts();
        av1_init_cost_luts();
        if (!is_allintra) av1_init_wedge_masks();
        if (!is_allintra || end_usage != AOM_Q) av1_rc_init_minq_luts();
    }

    internal static void av1_new_framerate(AV1_COMP cpi, double framerate)
    {
        cpi.framerate = framerate < 0.1 ? 30 : framerate;
        av1_rc_update_framerate(cpi, cpi.common.width, cpi.common.height);
    }

    // [subset] av1_get_compression_ratio: only used by level stats (keep_level_stats is 0).
    // [subset] auto_tile_size_balancing: only used with explicit tile_widths / tile_heights
    // (tile_width_count / tile_height_count are 0).

    private static void set_tile_info(AV1_COMMON cm, in TileConfig tile_cfg)
    {
        CommonModeInfoParams mi_params = cm.mi_params;
        SequenceHeader seq_params = cm.seq_params;
        CommonTileParams tiles = cm.tiles;

        av1_get_tile_limits(cm);

        int sb_cols =
            CEIL_POWER_OF_TWO(mi_params.mi_cols, seq_params.mib_size_log2);
        // configure tile columns
        if (tile_cfg.tile_width_count == 0 || tile_cfg.tile_height_count == 0)
        {
            tiles.uniform_spacing = 1;
            tiles.log2_cols = AOMMAX(tile_cfg.tile_columns, tiles.min_log2_cols);
            // Add a special case to handle super resolution
            sb_cols = coded_to_superres_mi(sb_cols, cm.superres_scale_denominator);
            int min_log2_cols = 0;
            for (; (tiles.max_width_sb << min_log2_cols) <= sb_cols; ++min_log2_cols)
            {
            }
            tiles.log2_cols = AOMMAX(tiles.log2_cols, min_log2_cols);

            tiles.log2_cols = AOMMIN(tiles.log2_cols, tiles.max_log2_cols);
        }
        else
        {
            // [subset] auto_tile_size_balancing / explicit tile widths: tile_width_count is
            // always 0 (no tile_widths control).
            throw new NotSupportedException("[subset] non-uniform tile columns");
        }
        av1_calculate_tile_cols(seq_params, mi_params.mi_rows, mi_params.mi_cols,
                                tiles);

        // configure tile rows
        int sb_rows =
            CEIL_POWER_OF_TWO(mi_params.mi_rows, seq_params.mib_size_log2);
        _ = sb_rows;
        if (tiles.uniform_spacing != 0)
        {
            tiles.log2_rows = AOMMAX(tile_cfg.tile_rows, tiles.min_log2_rows);
            tiles.log2_rows = AOMMIN(tiles.log2_rows, tiles.max_log2_rows);
        }
        else
        {
            // [subset] auto_tile_size_balancing / explicit tile heights.
            throw new NotSupportedException("[subset] non-uniform tile rows");
        }
        av1_calculate_tile_rows(seq_params, mi_params.mi_rows, tiles);
    }

    private static void init_frame_info(FRAME_INFO frame_info, AV1_COMMON cm)
    {
        CommonModeInfoParams mi_params = cm.mi_params;
        SequenceHeader seq_params = cm.seq_params;
        frame_info.frame_width = cm.width;
        frame_info.frame_height = cm.height;
        frame_info.mi_cols = mi_params.mi_cols;
        frame_info.mi_rows = mi_params.mi_rows;
        frame_info.mb_cols = mi_params.mb_cols;
        frame_info.mb_rows = mi_params.mb_rows;
        frame_info.num_mbs = mi_params.MBs;
        frame_info.bit_depth = seq_params.bit_depth;
        frame_info.subsampling_x = seq_params.subsampling_x;
        frame_info.subsampling_y = seq_params.subsampling_y;
    }

    internal static void av1_update_frame_size(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = cpi.td.mb.e_mbd;

        // Setup mi_params here in case we need more mi's.
        CommonModeInfoParams mi_params = cm.mi_params;
        mi_params.set_mb_mi(mi_params, cm.width, cm.height,
                            cpi.sf.part_sf.default_min_partition_size);

        av1_init_macroblockd(cm, xd);

        if (cpi.ppi.seq_params_locked == 0)
            set_sb_size(cm.seq_params,
                        av1_select_sb_size(cpi.oxcf, cm.width, cm.height,
                                           (int)cpi.ppi.number_spatial_layers));

        set_tile_info(cm, cpi.oxcf.tile_cfg);
        init_frame_info(cpi.frame_info, cm);
    }

    private static int does_level_match(int width, int height, double fps,
                                        int lvl_width, int lvl_height,
                                        double lvl_fps, int lvl_dim_mult)
    {
        long lvl_luma_pels = (long)lvl_width * lvl_height;
        double lvl_display_sample_rate = lvl_luma_pels * lvl_fps;
        long luma_pels = (long)width * height;
        double display_sample_rate = luma_pels * fps;
        return B(luma_pels <= lvl_luma_pels &&
                 display_sample_rate <= lvl_display_sample_rate &&
                 width <= lvl_width * lvl_dim_mult &&
                 height <= lvl_height * lvl_dim_mult);
    }

    private static void set_bitstream_level_tier(AV1_PRIMARY ppi, int width,
                                                 int height, double init_framerate,
                                                 aom_internal_error_info error)
    {
        SequenceHeader seq_params = ppi.seq_params;
        AV1LevelParams level_params = ppi.level_params;
        // TODO(any): This is a placeholder function that only addresses dimensions
        // and max display sample rates.
        // Need to add checks for max bit rate, max decoded luma sample rate, header
        // rate, etc. that are not covered by this function.
        AV1_LEVEL level = SEQ_LEVEL_MAX;
        if (does_level_match(width, height, init_framerate, 512, 288, 30.0, 4) != 0)
        {
            level = SEQ_LEVEL_2_0;
        }
        else if (does_level_match(width, height, init_framerate, 704, 396, 30.0,
                                  4) != 0)
        {
            level = SEQ_LEVEL_2_1;
        }
        else if (does_level_match(width, height, init_framerate, 1088, 612, 30.0,
                                  4) != 0)
        {
            level = SEQ_LEVEL_3_0;
        }
        else if (does_level_match(width, height, init_framerate, 1376, 774, 30.0,
                                  4) != 0)
        {
            level = SEQ_LEVEL_3_1;
        }
        else if (does_level_match(width, height, init_framerate, 2048, 1152, 30.0,
                                  3) != 0)
        {
            level = SEQ_LEVEL_4_0;
        }
        else if (does_level_match(width, height, init_framerate, 2048, 1152, 60.0,
                                  3) != 0)
        {
            level = SEQ_LEVEL_4_1;
        }
        else if (does_level_match(width, height, init_framerate, 4096, 2176, 30.0,
                                  2) != 0)
        {
            level = SEQ_LEVEL_5_0;
        }
        else if (does_level_match(width, height, init_framerate, 4096, 2176, 60.0,
                                  2) != 0)
        {
            level = SEQ_LEVEL_5_1;
        }
        else if (does_level_match(width, height, init_framerate, 4096, 2176, 120.0,
                                  2) != 0)
        {
            level = SEQ_LEVEL_5_2;
        }
        else if (does_level_match(width, height, init_framerate, 8192, 4352, 30.0,
                                  2) != 0)
        {
            level = SEQ_LEVEL_6_0;
        }
        else if (does_level_match(width, height, init_framerate, 8192, 4352, 60.0,
                                  2) != 0)
        {
            level = SEQ_LEVEL_6_1;
        }
        else if (does_level_match(width, height, init_framerate, 8192, 4352, 120.0,
                                  2) != 0)
        {
            level = SEQ_LEVEL_6_2;
        }
        // CONFIG_CWG_C013 is 0.

        for (int i = 0; i < MAX_NUM_OPERATING_POINTS; ++i)
        {
            Debug.Assert(is_valid_seq_level_idx(level_params.target_seq_level_idx[i]) != 0 ||
                         level_params.target_seq_level_idx[i] == SEQ_LEVEL_KEEP_STATS);
            // If a higher target level is specified, it is then used rather than the
            // inferred one from resolution and framerate.
            seq_params.seq_level_idx[i] =
                level_params.target_seq_level_idx[i] < SEQ_LEVELS &&
                        level_params.target_seq_level_idx[i] > level
                    ? level_params.target_seq_level_idx[i]
                    : level;
            // Set the maximum parameters for bitrate and buffer size for this profile,
            // level, and tier
            seq_params.op_params[i].bitrate = av1_max_level_bitrate(
                seq_params.profile, seq_params.seq_level_idx[i], seq_params.tier[i]);
            // Level with seq_level_idx = 31 returns a high "dummy" bitrate to pass the
            // check
            if (seq_params.op_params[i].bitrate == 0)
                aom_internal_error(
                    error, AOM_CODEC_UNSUP_BITSTREAM,
                    "AV1 does not support this combination of profile, level, and tier.");
            // Buffer size in bits is bitrate in bits/s * 1 s
            seq_params.op_params[i].buffer_size = seq_params.op_params[i].bitrate;
        }
    }

    internal static void av1_set_svc_seq_params(AV1_PRIMARY ppi)
    {
        SequenceHeader seq = ppi.seq_params;
        if (seq.operating_points_cnt_minus_1 == 0)
        {
            seq.operating_point_idc[0] = 0;
            seq.has_nonzero_operating_point_idc = false;
        }
        else
        {
            // Set operating_point_idc[] such that the i=0 point corresponds to the
            // highest quality operating point (all layers), and subsequent
            // operarting points (i > 0) are lower quality corresponding to
            // skip decoding enhancement  layers (temporal first).
            int i = 0;
            Debug.Assert(seq.operating_points_cnt_minus_1 ==
                         (int)(ppi.number_spatial_layers * ppi.number_temporal_layers - 1));
            for (uint sl = 0; sl < ppi.number_spatial_layers; sl++)
            {
                for (uint tl = 0; tl < ppi.number_temporal_layers; tl++)
                {
                    seq.operating_point_idc[i] = (int)(
                        (~(~0u << (int)(ppi.number_spatial_layers - sl)) << 8) |
                        ~(~0u << (int)(ppi.number_temporal_layers - tl)));
                    Debug.Assert(seq.operating_point_idc[i] != 0);
                    i++;
                }
            }
            seq.has_nonzero_operating_point_idc = true;
        }
    }

    // Sets the maximum frame size of the sequence header. Without a forced
    // maximum it never shrinks. Returns true if it changed.
    private static bool set_max_frame_size(SequenceHeader seq,
                                           in FrameDimensionCfg frm_dim_cfg)
    {
        int max_width = frm_dim_cfg.forced_max_frame_width != 0
                            ? frm_dim_cfg.forced_max_frame_width
                            : AOMMAX(seq.max_frame_width, frm_dim_cfg.width);
        int max_height =
            frm_dim_cfg.forced_max_frame_height != 0
                ? frm_dim_cfg.forced_max_frame_height
                : AOMMAX(seq.max_frame_height, frm_dim_cfg.height);
        if (max_width == seq.max_frame_width &&
            max_height == seq.max_frame_height)
        {
            return false;
        }
        seq.max_frame_width = max_width;
        seq.max_frame_height = max_height;
        seq.num_bits_width = (max_width > 1) ? get_msb((uint)(max_width - 1)) + 1 : 1;
        seq.num_bits_height = (max_height > 1) ? get_msb((uint)(max_height - 1)) + 1 : 1;
        Debug.Assert(seq.num_bits_width <= 16);
        Debug.Assert(seq.num_bits_height <= 16);
        return true;
    }

    private static void init_seq_coding_tools(AV1_PRIMARY ppi,
                                              AV1EncoderConfig oxcf,
                                              int disable_frame_id_numbers)
    {
        SequenceHeader seq = ppi.seq_params;
        ref readonly FrameDimensionCfg frm_dim_cfg = ref oxcf.frm_dim_cfg;
        ref readonly ToolCfg tool_cfg = ref oxcf.tool_cfg;

        seq.still_picture =
            (byte)B(!tool_cfg.force_video_mode && (oxcf.input_cfg.limit == 1));
        seq.reduced_still_picture_hdr =
            (byte)B(seq.still_picture != 0 && !tool_cfg.full_still_picture_hdr);
        seq.force_screen_content_tools = 2;
        seq.force_integer_mv = 2;
        seq.order_hint_info.enable_order_hint = B(tool_cfg.enable_order_hint);
        seq.frame_id_numbers_present_flag =
            (byte)B(seq.reduced_still_picture_hdr == 0 &&
                    !oxcf.tile_cfg.enable_large_scale_tile &&
                    tool_cfg.error_resilient_mode && disable_frame_id_numbers == 0);
        if (seq.reduced_still_picture_hdr != 0)
        {
            seq.order_hint_info.enable_order_hint = 0;
            seq.force_screen_content_tools = 2;
            seq.force_integer_mv = 2;
        }
        seq.order_hint_info.order_hint_bits_minus_1 =
            seq.order_hint_info.enable_order_hint != 0
                ? DEFAULT_EXPLICIT_ORDER_HINT_BITS - 1
                : -1;

        set_max_frame_size(seq, frm_dim_cfg);

        seq.frame_id_length = FRAME_ID_LENGTH;
        seq.delta_frame_id_length = DELTA_FRAME_ID_LENGTH;

        seq.enable_dual_filter = (byte)B(tool_cfg.enable_dual_filter);
        seq.order_hint_info.enable_dist_wtd_comp =
            B(oxcf.comp_type_cfg.enable_dist_wtd_comp);
        seq.order_hint_info.enable_dist_wtd_comp &=
            seq.order_hint_info.enable_order_hint;
        seq.order_hint_info.enable_ref_frame_mvs = B(tool_cfg.ref_frame_mvs_present);
        seq.order_hint_info.enable_ref_frame_mvs &=
            seq.order_hint_info.enable_order_hint;
        seq.enable_superres = (byte)B(oxcf.superres_cfg.enable_superres);
        seq.enable_cdef = (byte)(tool_cfg.cdef_control != CDEF_NONE ? 1 : 0);
        seq.enable_restoration = (byte)B(tool_cfg.enable_restoration);
        seq.enable_warped_motion = (byte)B(oxcf.motion_mode_cfg.enable_warped_motion);
        seq.enable_interintra_compound = (byte)B(tool_cfg.enable_interintra_comp);
        seq.enable_masked_compound = (byte)B(oxcf.comp_type_cfg.enable_masked_comp);
        seq.enable_intra_edge_filter = (byte)B(oxcf.intra_mode_cfg.enable_intra_edge_filter);
        seq.enable_filter_intra = (byte)B(oxcf.intra_mode_cfg.enable_filter_intra);

        set_bitstream_level_tier(ppi, seq.max_frame_width, seq.max_frame_height,
                                 oxcf.input_cfg.init_framerate, ppi.error);
        av1_set_svc_seq_params(ppi);
    }

    // Shared by init_config_sequence() and av1_change_config_seq(): the decoder model
    // parameter selection.
    // [subset] The decoder model (schedule mode) and resource availability mode are only
    // selected by the timing_info / decoder model controls, which are not supported.
    private static void set_decoder_model_params(AV1_PRIMARY ppi, SequenceHeader seq_params,
                                                 in DecoderModelCfg dec_model_cfg)
    {
        if (dec_model_cfg.decoder_model_info_present_flag)
        {
            // [subset] av1_set_aom_dec_model_info / av1_set_dec_model_op_parameters.
            throw new NotSupportedException("[subset] decoder model info");
        }
        else if (seq_params.timing_info_present != 0 &&
                 seq_params.timing_info.equal_picture_interval != 0 &&
                 seq_params.decoder_model_info_present_flag == 0)
        {
            // [subset] av1_set_resource_availability_parameters.
            throw new NotSupportedException("[subset] resource availability mode");
        }
        else
        {
            seq_params.op_params[0].initial_display_delay =
                10;  // Default value (not signaled)
        }
        _ = ppi;
    }

    private static void init_config_sequence(AV1_PRIMARY ppi,
                                             AV1EncoderConfig oxcf)
    {
        SequenceHeader seq_params = ppi.seq_params;
        ref readonly DecoderModelCfg dec_model_cfg = ref oxcf.dec_model_cfg;
        ref readonly ColorCfg color_cfg = ref oxcf.color_cfg;

        ppi.use_svc = 0;
        ppi.number_spatial_layers = 1;
        ppi.number_temporal_layers = 1;

        seq_params.profile = oxcf.profile;
        seq_params.bit_depth = oxcf.tool_cfg.bit_depth;
        seq_params.use_highbitdepth = (byte)B(oxcf.use_highbitdepth);
        seq_params.color_primaries = color_cfg.color_primaries;
        seq_params.transfer_characteristics = color_cfg.transfer_characteristics;
        seq_params.matrix_coefficients = color_cfg.matrix_coefficients;
        seq_params.monochrome = (byte)B(oxcf.tool_cfg.enable_monochrome);
        seq_params.chroma_sample_position = color_cfg.chroma_sample_position;
        seq_params.color_range = (int)color_cfg.color_range;
        seq_params.timing_info_present = B(dec_model_cfg.timing_info_present);
        seq_params.timing_info.num_units_in_display_tick =
            dec_model_cfg.timing_info.num_units_in_display_tick;
        seq_params.timing_info.time_scale = dec_model_cfg.timing_info.time_scale;
        seq_params.timing_info.equal_picture_interval =
            dec_model_cfg.timing_info.equal_picture_interval;
        seq_params.timing_info.num_ticks_per_picture =
            dec_model_cfg.timing_info.num_ticks_per_picture;

        seq_params.display_model_info_present_flag =
            (byte)B(dec_model_cfg.display_model_info_present_flag);
        seq_params.decoder_model_info_present_flag =
            (byte)B(dec_model_cfg.decoder_model_info_present_flag);
        set_decoder_model_params(ppi, seq_params, dec_model_cfg);

        if (seq_params.monochrome != 0)
        {
            seq_params.subsampling_x = 1;
            seq_params.subsampling_y = 1;
        }
        else if (seq_params.color_primaries == AOM_CICP_CP_BT_709 &&
                 seq_params.transfer_characteristics == AOM_CICP_TC_SRGB &&
                 seq_params.matrix_coefficients == AOM_CICP_MC_IDENTITY)
        {
            seq_params.subsampling_x = 0;
            seq_params.subsampling_y = 0;
        }
        else
        {
            if (seq_params.profile == 0)
            {
                seq_params.subsampling_x = 1;
                seq_params.subsampling_y = 1;
            }
            else if (seq_params.profile == 1)
            {
                seq_params.subsampling_x = 0;
                seq_params.subsampling_y = 0;
            }
            else
            {
                if (seq_params.bit_depth == AOM_BITS_12)
                {
                    seq_params.subsampling_x = (int)oxcf.input_cfg.chroma_subsampling_x;
                    seq_params.subsampling_y = (int)oxcf.input_cfg.chroma_subsampling_y;
                }
                else
                {
                    seq_params.subsampling_x = 1;
                    seq_params.subsampling_y = 0;
                }
            }
        }
        bool dummy = false;
        av1_change_config_seq(ppi, oxcf, ref dummy, false);
    }

    private static void init_config(AV1_COMP cpi, AV1EncoderConfig oxcf)
    {
        AV1_COMMON cm = cpi.common;
        ref ResizePendingParams resize_pending_params = ref cpi.resize_pending_params;

        cpi.oxcf.CopyFrom(oxcf);
        cpi.framerate = oxcf.input_cfg.init_framerate;

        cm.width = oxcf.frm_dim_cfg.width;
        cm.height = oxcf.frm_dim_cfg.height;
        cpi.is_dropped_frame = false;

        alloc_compressor_data(cpi);

        cpi.data_alloc_width = oxcf.frm_dim_cfg.forced_max_frame_width != 0
                                   ? oxcf.frm_dim_cfg.forced_max_frame_width
                                   : cm.width;
        cpi.data_alloc_height = oxcf.frm_dim_cfg.forced_max_frame_height != 0
                                    ? oxcf.frm_dim_cfg.forced_max_frame_height
                                    : cm.height;
        cpi.frame_size_related_setup_done = false;

        // Single thread case: use counts in common.
        cpi.td.counts = cpi.counts;

        // Init SVC parameters.
        cpi.svc.number_spatial_layers = 1;
        cpi.svc.number_temporal_layers = 1;
        cm.spatial_layer_id = 0;
        cm.temporal_layer_id = 0;
        cpi.src_sad_blk_alloc_size = 0;
        // Init rtc_ref parameters.
        cpi.ppi.rtc_ref.set_ref_frame_config = 0;
        cpi.ppi.rtc_ref.non_reference_frame = 0;
        cpi.ppi.rtc_ref.ref_frame_comp[0] = 0;
        cpi.ppi.rtc_ref.ref_frame_comp[1] = 0;
        cpi.ppi.rtc_ref.ref_frame_comp[2] = 0;

        // change includes all joint functionality
        av1_change_config(cpi, oxcf, false);

        cpi.ref_frame_flags = 0;

        // Reset resize pending flags
        resize_pending_params.width = 0;
        resize_pending_params.height = 0;

        // Setup identity scale factor
        av1_setup_scale_factors_for_frame(cm.sf_identity, 1, 1, 1, 1);

        init_buffer_indices(cpi.force_intpel_info, cm.remapped_ref_idx);

        av1_noise_estimate_init(cpi.noise_estimate, cm.width, cm.height);
    }

    // [port] bool *is_sb_size_changed: C passes NULL from init_config_sequence(); the port
    // passes has_is_sb_size_changed = false with a dummy ref.
    internal static void av1_change_config_seq(AV1_PRIMARY ppi,
                                               AV1EncoderConfig oxcf,
                                               ref bool is_sb_size_changed,
                                               bool has_is_sb_size_changed = true)
    {
        SequenceHeader seq_params = ppi.seq_params;
        ref readonly FrameDimensionCfg frm_dim_cfg = ref oxcf.frm_dim_cfg;
        ref readonly DecoderModelCfg dec_model_cfg = ref oxcf.dec_model_cfg;
        ref readonly ColorCfg color_cfg = ref oxcf.color_cfg;

        if (seq_params.profile != oxcf.profile) seq_params.profile = oxcf.profile;
        seq_params.bit_depth = oxcf.tool_cfg.bit_depth;
        seq_params.color_primaries = color_cfg.color_primaries;
        seq_params.transfer_characteristics = color_cfg.transfer_characteristics;
        seq_params.matrix_coefficients = color_cfg.matrix_coefficients;
        seq_params.monochrome = (byte)B(oxcf.tool_cfg.enable_monochrome);
        seq_params.chroma_sample_position = color_cfg.chroma_sample_position;
        seq_params.color_range = (int)color_cfg.color_range;

        Debug.Assert(!(seq_params.profile <= PROFILE_1) ||
                     seq_params.bit_depth <= AOM_BITS_10);

        seq_params.timing_info_present = B(dec_model_cfg.timing_info_present);
        seq_params.timing_info.num_units_in_display_tick =
            dec_model_cfg.timing_info.num_units_in_display_tick;
        seq_params.timing_info.time_scale = dec_model_cfg.timing_info.time_scale;
        seq_params.timing_info.equal_picture_interval =
            dec_model_cfg.timing_info.equal_picture_interval;
        seq_params.timing_info.num_ticks_per_picture =
            dec_model_cfg.timing_info.num_ticks_per_picture;

        seq_params.display_model_info_present_flag =
            (byte)B(dec_model_cfg.display_model_info_present_flag);
        seq_params.decoder_model_info_present_flag =
            (byte)B(dec_model_cfg.decoder_model_info_present_flag);
        set_decoder_model_params(ppi, seq_params, dec_model_cfg);

        // !CONFIG_REALTIME_ONLY: av1_update_film_grain_parameters_seq compiled out.

        int sb_size = seq_params.sb_size;
        // Superblock size should not be updated after the first key frame.
        if (ppi.seq_params_locked == 0)
        {
            set_sb_size(seq_params, av1_select_sb_size(oxcf, frm_dim_cfg.width,
                                                       frm_dim_cfg.height,
                                                       (int)ppi.number_spatial_layers));
            for (int i = 0; i < MAX_NUM_OPERATING_POINTS; ++i)
                seq_params.tier[i] = (byte)((oxcf.tier_mask >> i) & 1);
        }
        if (has_is_sb_size_changed && sb_size != seq_params.sb_size)
            is_sb_size_changed = true;

        // Init sequence level coding tools
        // This should not be called after the first key frame.
        // Note that for SVC encoding the sequence parameters
        // (operating_points_cnt_minus_1, operating_point_idc[],
        // has_nonzero_operating_point_idc) should be updated whenever the
        // number of layers is changed. This is done in the
        // ctrl_set_svc_params().
        if (ppi.seq_params_locked == 0)
        {
            seq_params.operating_points_cnt_minus_1 =
                (ppi.number_spatial_layers > 1 || ppi.number_temporal_layers > 1)
                    ? (int)(ppi.number_spatial_layers * ppi.number_temporal_layers) - 1
                    : 0;
            init_seq_coding_tools(ppi, oxcf,
                                  B(ppi.use_svc != 0 || ppi.rtc_ref.set_ref_frame_config != 0));
        }
        seq_params.timing_info_present &= B(seq_params.reduced_still_picture_hdr == 0);

        // CONFIG_AV1_HIGHBITDEPTH is 0: highbd_set_var_fns compiled out.

        set_primary_rc_buffer_sizes(oxcf, ppi);
    }

    internal static void av1_change_config(AV1_COMP cpi, AV1EncoderConfig oxcf,
                                           bool is_sb_size_changed)
    {
        AV1_COMMON cm = cpi.common;
        SequenceHeader seq_params = cm.seq_params;
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        MACROBLOCK x = cpi.td.mb;
        AV1LevelParams level_params = cpi.ppi.level_params;
        ref RefreshFrameInfo refresh_frame = ref cpi.refresh_frame;
        FeatureFlags features = cm.features;
        int is_highbitdepth = seq_params.use_highbitdepth;
        _ = is_highbitdepth;

        // in case of LAP, lag in frames is set according to number of lap buffers
        // calculated at init time. This stores and restores LAP's lag in frames to
        // prevent override by new cfg.
        int lap_lag_in_frames = -1;
        if (cpi.ppi.lap_enabled != 0 && cpi.compressor_stage == LAP_STAGE)
        {
            lap_lag_in_frames = cpi.oxcf.gf_cfg.lag_in_frames;
        }

        cpi.oxcf.CopyFrom(oxcf);
        // [port] C: const FrameDimensionCfg *frm_dim_cfg = &cpi->oxcf.frm_dim_cfg and
        // const RateControlCfg *rc_cfg = &oxcf->rc_cfg (read after the copy).
        ref readonly FrameDimensionCfg frm_dim_cfg = ref cpi.oxcf.frm_dim_cfg;
        ref readonly RateControlCfg rc_cfg = ref oxcf.rc_cfg;

        // !CONFIG_REALTIME_ONLY: av1_update_film_grain_parameters compiled out.

        // When user provides superres_mode = AOM_SUPERRES_AUTO, we still initialize
        // superres mode for current encoding = AOM_SUPERRES_NONE. This is to ensure
        // that any analysis (e.g. TPL) happening outside the main encoding loop still
        // happens at full resolution.
        // This value will later be set appropriately just before main encoding loop.
        cpi.superres_mode = oxcf.superres_cfg.superres_mode == AOM_SUPERRES_AUTO
                                ? AOM_SUPERRES_NONE
                                : oxcf.superres_cfg.superres_mode;  // default
        x.e_mbd.bd = (int)seq_params.bit_depth;
        x.e_mbd.global_motion = cm.global_motion;

        Array.Copy(cpi.oxcf.target_seq_level_idx, level_params.target_seq_level_idx,
                   MAX_NUM_OPERATING_POINTS);
        level_params.keep_level_stats = 0;
        for (int i = 0; i < MAX_NUM_OPERATING_POINTS; ++i)
        {
            if (level_params.target_seq_level_idx[i] < SEQ_LEVELS ||
                level_params.target_seq_level_idx[i] == SEQ_LEVEL_KEEP_STATS)
            {
                // [subset] Level stats / level targeting (target_seq_level_idx defaults to
                // SEQ_LEVEL_MAX, the AV1E_SET_TARGET_SEQ_LEVEL_IDX control is not supported).
                throw new NotSupportedException("[subset] level stats");
            }
        }

        // TODO(huisu@): level targeting currently only works for the 0th operating
        // point, so scalable coding is not supported yet.
        // [subset] config_target_level: target_seq_level_idx[0] is never < SEQ_LEVELS (see
        // above).

        if (has_no_stats_stage(cpi) != 0 && (rc_cfg.mode == AOM_Q))
        {
            p_rc.baseline_gf_interval = FIXED_GF_INTERVAL;
        }
        else if (is_one_pass_rt_params(cpi) == 0 ||
                 cm.current_frame.frame_number == 0)
        {
            // For rtc mode: logic for setting the baseline_gf_interval is done
            // in av1_get_one_pass_rt_params(), and it should not be reset here in
            // change_config(), unless after init_config (first frame).
            p_rc.baseline_gf_interval = (MIN_GF_INTERVAL + MAX_GF_INTERVAL) / 2;
        }

        refresh_frame.golden_frame = false;
        refresh_frame.bwd_ref_frame = false;

        features.refresh_frame_context =
            (oxcf.tool_cfg.frame_parallel_decoding_mode)
                ? REFRESH_FRAME_CONTEXT_DISABLED
                : REFRESH_FRAME_CONTEXT_BACKWARD;
        if (oxcf.tile_cfg.enable_large_scale_tile)
            features.refresh_frame_context = REFRESH_FRAME_CONTEXT_DISABLED;

        // [subset] x->palette_buffer: dropped with MACROBLOCK::palette_buffer (phase 1).

        if (cpi.oxcf.mode != ALLINTRA)
        {
            if (x.tmp_conv_dst.IsNull)
            {
                x.tmp_conv_dst = new CONV_BUF_TYPE[MAX_SB_SIZE * MAX_SB_SIZE];
                x.e_mbd.tmp_conv_dst = x.tmp_conv_dst;
            }

            if (x.upsample_pred.IsNull)
            {
                // The buffer 'upsampled_pred' is used to store the output of horizontal
                // filtering in aom_(highbd_)upsampled_pred() function. As the length of
                // the interpolation filter used is SUBPEL_TAPS a buffer size of
                // (MAX_SB_SIZE + SUBPEL_TAPS) * MAX_SB_SIZE is allocated.
                x.upsample_pred = new byte[(1 + is_highbitdepth) * (MAX_SB_SIZE + SUBPEL_TAPS) *
                                           MAX_SB_SIZE];
                x.e_mbd.tmp_upsample_pred = x.upsample_pred;
            }
        }

        // The buffers 'tmp_pred_bufs[]' and 'comp_rd_buffer' are used in inter frames
        // to store intermediate inter mode prediction results and are not required
        // for allintra encoding mode. Hence, the memory allocations for these buffers
        // are avoided for allintra encoding mode.
        if (cpi.oxcf.kf_cfg.key_freq_max != 0)
        {
            // [subset] alloc_compound_type_rd_buffers: x->comp_rd_buffer dropped (phase 1).

            for (int i = 0; i < 2; ++i)
            {
                if (x.tmp_pred_bufs[i].IsNull)
                {
                    x.tmp_pred_bufs[i] = new byte[2 * MAX_MB_PLANE * MAX_SB_SQUARE];
                    x.e_mbd.tmp_obmc_bufs[i] = x.tmp_pred_bufs[i];
                }
            }
        }

        av1_reset_segment_features(cm);

        av1_set_high_precision_mv(cpi, 1, 0);

        // Under a configuration change, where maximum_buffer_size may change,
        // keep buffer level clipped to the maximum allowed buffer size.
        p_rc.bits_off_target =
            AOMMIN(p_rc.bits_off_target, p_rc.maximum_buffer_size);
        p_rc.buffer_level = AOMMIN(p_rc.buffer_level, p_rc.maximum_buffer_size);

        // Set up frame rate and related parameters rate control values.
        av1_new_framerate(cpi, cpi.framerate);

        // Set absolute upper and lower quality limits
        rc.worst_quality = rc_cfg.worst_allowed_q;
        rc.best_quality = rc_cfg.best_allowed_q;

        // If lossless has been requested make sure average Q accumulators are reset.
        if (is_lossless_requested(cpi.oxcf.rc_cfg) != 0)
        {
            int i;
            for (i = 0; i < FRAME_TYPES; ++i)
            {
                p_rc.avg_frame_qindex[i] = 0;
            }
        }

        cm.tiles.large_scale = (uint)B(oxcf.tile_cfg.enable_large_scale_tile);
        cm.tiles.single_tile_decoding = (uint)B(oxcf.tile_cfg.enable_single_tile_decoding);
        features.interp_filter =
            oxcf.tile_cfg.enable_large_scale_tile ? EIGHTTAP_REGULAR : SWITCHABLE;
        features.switchable_motion_mode = is_switchable_motion_mode_allowed(
            features.allow_warped_motion, oxcf.motion_mode_cfg.enable_obmc);

        if (frm_dim_cfg.render_width > 0 && frm_dim_cfg.render_height > 0)
        {
            cm.render_width = frm_dim_cfg.render_width;
            cm.render_height = frm_dim_cfg.render_height;
        }
        else
        {
            cm.render_width = frm_dim_cfg.width;
            cm.render_height = frm_dim_cfg.height;
        }

        int last_width = cm.width;
        int last_height = cm.height;
        cm.width = frm_dim_cfg.width;
        cm.height = frm_dim_cfg.height;
        cm.superres_upscaled_width = frm_dim_cfg.width;
        cm.superres_upscaled_height = frm_dim_cfg.height;

        // Before the first frame, size the buffers for the forced maximum like
        // init_config() does, so a later upscale keeps the reference frames.
        int alloc_width = cm.width;
        int alloc_height = cm.height;
        if (cpi.ppi.seq_params_locked == 0)
        {
            if (frm_dim_cfg.forced_max_frame_width != 0)
                alloc_width = frm_dim_cfg.forced_max_frame_width;
            if (frm_dim_cfg.forced_max_frame_height != 0)
                alloc_height = frm_dim_cfg.forced_max_frame_height;
        }
        if (alloc_width > cpi.data_alloc_width ||
            alloc_height > cpi.data_alloc_height || is_sb_size_changed)
        {
            av1_free_context_buffers(cm);
            av1_free_shared_coeff_buffer(cpi.td.shared_coeff_buf);
            av1_free_sms_tree(cpi.td);
            av1_free_pmc(cpi.td.firstpass_ctx, av1_num_planes(cm));
            cpi.td.firstpass_ctx = null;
            alloc_compressor_data(cpi);
            realloc_segmentation_maps(cpi);
            alloc_rdmult_scaling_factors(cpi);
            cpi.data_alloc_width = oxcf.frm_dim_cfg.forced_max_frame_width != 0
                                       ? oxcf.frm_dim_cfg.forced_max_frame_width
                                       : cm.width;
            cpi.data_alloc_height = oxcf.frm_dim_cfg.forced_max_frame_height != 0
                                        ? oxcf.frm_dim_cfg.forced_max_frame_height
                                        : cm.height;
            cpi.frame_size_related_setup_done = false;
        }
        av1_update_frame_size(cpi);

        if (cm.width != last_width || cm.height != last_height)
        {
            if (cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ)
            {
                int mi_rows = cpi.common.mi_params.mi_rows;
                int mi_cols = cpi.common.mi_params.mi_cols;
                if (cpi.oxcf.frm_dim_cfg.forced_max_frame_width != 0)
                {
                    mi_cols = size_in_mi(cpi.oxcf.frm_dim_cfg.forced_max_frame_width);
                }
                if (cpi.oxcf.frm_dim_cfg.forced_max_frame_height != 0)
                {
                    mi_rows = size_in_mi(cpi.oxcf.frm_dim_cfg.forced_max_frame_height);
                }
                cpi.cyclic_refresh.map = new sbyte[mi_rows * mi_cols];
                if (cpi.svc.number_spatial_layers > 1)
                {
                    // [subset] SVC layer contexts (use_svc is 0).
                    throw new NotSupportedException("[subset] svc layer cyclic refresh maps");
                }
            }
        }

        // av1_reset_segment_features() turned segmentation off, so set the active
        // map up again if it was set for this frame size.
        if (cpi.active_map.mi_rows != cm.mi_params.mi_rows ||
            cpi.active_map.mi_cols != cm.mi_params.mi_cols)
        {
            cpi.active_map.enabled = 0;
        }
        cpi.active_map.update = cpi.active_map.enabled;

        rc.is_src_frame_alt_ref = 0;

        if (cpi.ppi.rtc_ref.set_ref_frame_config == 0)
            cpi.ext_flags.refresh_frame.update_pending = false;
        cpi.ext_flags.refresh_frame_context_pending = false;

        if (cpi.ppi.use_svc != 0)
        {
            // [subset] av1_update_layer_context_change_config (use_svc is 0).
            throw new NotSupportedException("[subset] svc");
        }

        check_reset_rc_flag(cpi);

        // restore the value of lag_in_frame for LAP stage.
        if (lap_lag_in_frames != -1)
        {
            cpi.oxcf.gf_cfg.lag_in_frames = lap_lag_in_frames;
        }

        // CONFIG_REALTIME_ONLY
        Debug.Assert(!oxcf.tool_cfg.enable_global_motion);
        cpi.alloc_pyramid = false;
    }

    private static void init_frame_index_set(ref FRAME_INDEX_SET frame_index_set)
    {
        frame_index_set.show_frame_count = 0;
    }

    private static void update_counters_for_show_frame(AV1_COMP cpi)
    {
        Debug.Assert(cpi.common.show_frame != 0);
        cpi.frame_index_set.show_frame_count++;
        cpi.common.current_frame.frame_number++;
    }

    private static void BFP(AV1_PRIMARY ppi, BLOCK_SIZE BT, aom_sad_fn_t SDF,
                            aom_sad_avg_fn_t SDAF, aom_variance_fn_t VF,
                            aom_subpixvariance_fn_t SVF, aom_subp_avg_variance_fn_t SVAF,
                            aom_sad_multi_d_fn_t SDX4DF, aom_sad_multi_d_fn_t SDX3DF)
    {
        ppi.fn_ptr[BT].sdf = SDF;
        ppi.fn_ptr[BT].sdaf = SDAF;
        ppi.fn_ptr[BT].vf = VF;
        ppi.fn_ptr[BT].svf = SVF;
        ppi.fn_ptr[BT].svaf = SVAF;
        ppi.fn_ptr[BT].sdx4df = SDX4DF;
        ppi.fn_ptr[BT].sdx3df = SDX3DF;
    }

    private static void SDSFP(AV1_PRIMARY ppi, BLOCK_SIZE BT, aom_sad_fn_t SDSF,
                              aom_sad_multi_d_fn_t SDSX4DF)
    {
        ppi.fn_ptr[BT].sdsf = SDSF;
        ppi.fn_ptr[BT].sdsx4df = SDSX4DF;
    }

    internal static AV1_PRIMARY av1_create_primary_compressor(
        aom_codec_pkt_list pkt_list_head, int num_lap_buffers,
        AV1EncoderConfig oxcf)
    {
        AV1_PRIMARY ppi = new AV1_PRIMARY();

        try
        {
            ppi.error.setjmp = 1;

            ppi.seq_params_locked = 0;
            ppi.lap_enabled = B(num_lap_buffers > 0);
            ppi.output_pkt_list = pkt_list_head;
            ppi.b_calculate_psnr = 0;  // CONFIG_INTERNAL_STATS
            ppi.frames_left = (int)oxcf.input_cfg.limit;
            ppi.num_fp_contexts = 1;

            init_config_sequence(ppi, oxcf);

            av1_primary_rc_init(oxcf, ppi.p_rc);

            ppi.p_rc.enable_scenecut_detection = ENABLE_SCENECUT_MODE_2;
            if (ppi.lap_enabled != 0)
            {
                // [subset] LAP (lag_in_frames is 0 for realtime).
                throw new NotSupportedException("[subset] lap");
            }

            // !CONFIG_REALTIME_ONLY: 4X16, 16X4, 8X32, 32X8, 16X64, 64X16 compiled out.

            BFP(ppi, BLOCK_128X128, aom_sad128x128, aom_sad128x128_avg, aom_variance128x128,
                aom_sub_pixel_variance128x128, aom_sub_pixel_avg_variance128x128,
                aom_sad128x128x4d, aom_sad128x128x3d);

            BFP(ppi, BLOCK_128X64, aom_sad128x64, aom_sad128x64_avg, aom_variance128x64,
                aom_sub_pixel_variance128x64, aom_sub_pixel_avg_variance128x64,
                aom_sad128x64x4d, aom_sad128x64x3d);

            BFP(ppi, BLOCK_64X128, aom_sad64x128, aom_sad64x128_avg, aom_variance64x128,
                aom_sub_pixel_variance64x128, aom_sub_pixel_avg_variance64x128,
                aom_sad64x128x4d, aom_sad64x128x3d);

            BFP(ppi, BLOCK_32X16, aom_sad32x16, aom_sad32x16_avg, aom_variance32x16,
                aom_sub_pixel_variance32x16, aom_sub_pixel_avg_variance32x16,
                aom_sad32x16x4d, aom_sad32x16x3d);

            BFP(ppi, BLOCK_16X32, aom_sad16x32, aom_sad16x32_avg, aom_variance16x32,
                aom_sub_pixel_variance16x32, aom_sub_pixel_avg_variance16x32,
                aom_sad16x32x4d, aom_sad16x32x3d);

            BFP(ppi, BLOCK_64X32, aom_sad64x32, aom_sad64x32_avg, aom_variance64x32,
                aom_sub_pixel_variance64x32, aom_sub_pixel_avg_variance64x32,
                aom_sad64x32x4d, aom_sad64x32x3d);

            BFP(ppi, BLOCK_32X64, aom_sad32x64, aom_sad32x64_avg, aom_variance32x64,
                aom_sub_pixel_variance32x64, aom_sub_pixel_avg_variance32x64,
                aom_sad32x64x4d, aom_sad32x64x3d);

            BFP(ppi, BLOCK_32X32, aom_sad32x32, aom_sad32x32_avg, aom_variance32x32,
                aom_sub_pixel_variance32x32, aom_sub_pixel_avg_variance32x32,
                aom_sad32x32x4d, aom_sad32x32x3d);

            BFP(ppi, BLOCK_64X64, aom_sad64x64, aom_sad64x64_avg, aom_variance64x64,
                aom_sub_pixel_variance64x64, aom_sub_pixel_avg_variance64x64,
                aom_sad64x64x4d, aom_sad64x64x3d);

            BFP(ppi, BLOCK_16X16, aom_sad16x16, aom_sad16x16_avg, aom_variance16x16,
                aom_sub_pixel_variance16x16, aom_sub_pixel_avg_variance16x16,
                aom_sad16x16x4d, aom_sad16x16x3d);

            BFP(ppi, BLOCK_16X8, aom_sad16x8, aom_sad16x8_avg, aom_variance16x8,
                aom_sub_pixel_variance16x8, aom_sub_pixel_avg_variance16x8,
                aom_sad16x8x4d, aom_sad16x8x3d);

            BFP(ppi, BLOCK_8X16, aom_sad8x16, aom_sad8x16_avg, aom_variance8x16,
                aom_sub_pixel_variance8x16, aom_sub_pixel_avg_variance8x16,
                aom_sad8x16x4d, aom_sad8x16x3d);

            BFP(ppi, BLOCK_8X8, aom_sad8x8, aom_sad8x8_avg, aom_variance8x8,
                aom_sub_pixel_variance8x8, aom_sub_pixel_avg_variance8x8, aom_sad8x8x4d,
                aom_sad8x8x3d);

            BFP(ppi, BLOCK_8X4, aom_sad8x4, /*SDAF=*/null, aom_variance8x4,
                aom_sub_pixel_variance8x4, aom_sub_pixel_avg_variance8x4, aom_sad8x4x4d,
                aom_sad8x4x3d);

            BFP(ppi, BLOCK_4X8, aom_sad4x8, /*SDAF=*/null, aom_variance4x8,
                aom_sub_pixel_variance4x8, aom_sub_pixel_avg_variance4x8, aom_sad4x8x4d,
                aom_sad4x8x3d);

            BFP(ppi, BLOCK_4X4, aom_sad4x4, /*SDAF=*/null, aom_variance4x4,
                aom_sub_pixel_variance4x4, aom_sub_pixel_avg_variance4x4, aom_sad4x4x4d,
                aom_sad4x4x3d);

            // !CONFIG_REALTIME_ONLY: OBFP (obmc) compiled out.

            // [subset] MBFP (masked SAD / masked sub-pixel variance): msdf / msvf are only
            // used by compound wedge / diff-weighted search, which the nonrd path never runs.
            // The aom_masked_* kernels are not ported; the entries stay null.

            SDSFP(ppi, BLOCK_128X128, aom_sad_skip_128x128, aom_sad_skip_128x128x4d);
            SDSFP(ppi, BLOCK_128X64, aom_sad_skip_128x64, aom_sad_skip_128x64x4d);
            SDSFP(ppi, BLOCK_64X128, aom_sad_skip_64x128, aom_sad_skip_64x128x4d);
            SDSFP(ppi, BLOCK_64X64, aom_sad_skip_64x64, aom_sad_skip_64x64x4d);
            SDSFP(ppi, BLOCK_64X32, aom_sad_skip_64x32, aom_sad_skip_64x32x4d);
            SDSFP(ppi, BLOCK_32X64, aom_sad_skip_32x64, aom_sad_skip_32x64x4d);
            SDSFP(ppi, BLOCK_32X32, aom_sad_skip_32x32, aom_sad_skip_32x32x4d);
            SDSFP(ppi, BLOCK_32X16, aom_sad_skip_32x16, aom_sad_skip_32x16x4d);
            SDSFP(ppi, BLOCK_16X32, aom_sad_skip_16x32, aom_sad_skip_16x32x4d);
            SDSFP(ppi, BLOCK_16X16, aom_sad_skip_16x16, aom_sad_skip_16x16x4d);
            SDSFP(ppi, BLOCK_8X16, aom_sad_skip_8x16, aom_sad_skip_8x16x4d);
            // !CONFIG_REALTIME_ONLY: 64X16, 16X64, 8X32, 4X16 compiled out.

            // CONFIG_AV1_HIGHBITDEPTH is 0: highbd_set_var_fns compiled out.

            {
                CommonModeInfoParams mi_params = new CommonModeInfoParams();
                enc_set_mb_mi(mi_params, oxcf.frm_dim_cfg.width, oxcf.frm_dim_cfg.height,
                              BLOCK_4X4);
                if (oxcf.mode != ALLINTRA)
                {
                    BLOCK_SIZE bsize = BLOCK_16X16;
                    int w = mi_size_wide[bsize];
                    int h = mi_size_high[bsize];
                    int num_cols = (mi_params.mi_cols + w - 1) / w;
                    int num_rows = (mi_params.mi_rows + h - 1) / h;
                    ppi.tpl_sb_rdmult_scaling_factors = new double[num_rows * num_cols];
                }
                // CONFIG_INTERNAL_STATS is 0.
            }

            ppi.error.setjmp = 0;
            return ppi;
        }
        catch (AomException e)
        {
            aom_set_error_from_exception(ppi.error, e);
            ppi.error.setjmp = 0;
            av1_remove_primary_compressor(ppi);
            return null;
        }
    }

    internal static AV1_COMP av1_create_compressor(AV1_PRIMARY ppi, AV1EncoderConfig oxcf,
                                                   BufferPool pool, COMPRESSOR_STAGE stage,
                                                   int lap_lag_in_frames)
    {
        AV1_COMP cpi = new AV1_COMP();

        cpi.ppi = ppi;

        AV1_COMMON cm = cpi.common;
        cm.seq_params = ppi.seq_params;
        cm.error = new aom_internal_error_info();

        // The jmp_buf is valid only for the duration of the function that calls
        // setjmp(). Therefore, this function must reset the 'setjmp' field to 0
        // before it returns.
        try
        {
            cm.error.setjmp = 1;
            cpi.compressor_stage = stage;

            cpi.do_frame_data_update = true;

            CommonModeInfoParams mi_params = cm.mi_params;
            mi_params.free_mi = enc_free_mi;
            mi_params.setup_mi = enc_setup_mi;
            mi_params.set_mb_mi =
                (oxcf.pass == AOM_RC_FIRST_PASS || cpi.compressor_stage == LAP_STAGE)
                    ? stat_stage_set_mb_mi
                    : enc_set_mb_mi;

            mi_params.mi_alloc_bsize = BLOCK_4X4;

            cm.fc = new FRAME_CONTEXT();
            cm.default_frame_context = new FRAME_CONTEXT();

            cpi.common.buffer_pool = pool;

            init_config(cpi, oxcf);
            if (cpi.compressor_stage == LAP_STAGE)
            {
                cpi.oxcf.gf_cfg.lag_in_frames = lap_lag_in_frames;
            }

            av1_rc_init(cpi.oxcf, cpi.rc);

            init_frame_info(cpi.frame_info, cm);
            init_frame_index_set(ref cpi.frame_index_set);

            cm.current_frame.frame_number = 0;
            cpi.rc.frame_number_encoded = 0;
            cpi.rc.prev_frame_is_dropped = 0;
            cpi.rc.max_consec_drop = int.MaxValue;
            cpi.rc.drop_count_consec = 0;
            cm.current_frame_id = -1;
            cpi.tile_data = null;
            cpi.last_show_frame_buf = null;
            realloc_segmentation_maps(cpi);

            cpi.refresh_frame.alt_ref_frame = false;

            cpi.time_stamps.first_ts_start = long.MaxValue;

            // !CONFIG_REALTIME_ONLY: is_stat_consumption_stage() two-pass setup compiled out.

            // The buffer "obmc_buffer" is used in inter frames for fast obmc search.
            // Hence, the memory allocation for the same is avoided for allintra encoding
            // mode.
            // [subset] alloc_obmc_buffers: MACROBLOCK::obmc_buffer dropped (phase 1).

            // [subset] td.mb.intrabc_hash_info.hash_value_buffer[]: intraBC hash dropped
            // (phase 1); crc_initialized with it.

            av1_set_speed_features_framesize_independent(cpi, oxcf.speed);
            av1_set_speed_features_framesize_dependent(cpi, oxcf.speed);

            int max_mi_cols = mi_params.mi_cols;
            int max_mi_rows = mi_params.mi_rows;
            if (oxcf.frm_dim_cfg.forced_max_frame_width != 0)
            {
                max_mi_cols = size_in_mi(oxcf.frm_dim_cfg.forced_max_frame_width);
            }
            if (oxcf.frm_dim_cfg.forced_max_frame_height != 0)
            {
                max_mi_rows = size_in_mi(oxcf.frm_dim_cfg.forced_max_frame_height);
            }

            if (cpi.oxcf.mode != ALLINTRA)
            {
                int consec_zero_mv_alloc_size = (max_mi_rows * max_mi_cols) >> 2;
                cpi.consec_zero_mv = new byte[consec_zero_mv_alloc_size];
                cpi.consec_zero_mv_alloc_size = consec_zero_mv_alloc_size;
            }

            // [subset] mb_weber_stats / mb_delta_q (deltaq perceptual AI) dropped (phase 1).
            cpi.palette_pixel_num = 0;
            cpi.scaled_last_source_available = 0;

            alloc_rdmult_scaling_factors(cpi);

            // CONFIG_TUNE_VMAF / CONFIG_TUNE_BUTTERAUGLI / CONFIG_COLLECT_PARTITION_STATS are 0.

            // Initialize the members of DeltaQuantParams with INT_MAX to ensure that
            // the quantizer tables are correctly initialized using the default deltaq
            // parameters when av1_init_quantizer is called for the first time.
            ref DeltaQuantParams prev_deltaq_params =
                ref cpi.enc_quant_dequant_params.prev_deltaq_params;
            prev_deltaq_params.y_dc_delta_q = int.MaxValue;
            prev_deltaq_params.u_dc_delta_q = int.MaxValue;
            prev_deltaq_params.v_dc_delta_q = int.MaxValue;
            prev_deltaq_params.u_ac_delta_q = int.MaxValue;
            prev_deltaq_params.v_ac_delta_q = int.MaxValue;

            av1_init_quantizer(cpi.enc_quant_dequant_params, cm.quant_params,
                               cm.seq_params.bit_depth, cpi.oxcf.algo_cfg.sharpness);
            av1_qm_init(cm.quant_params, av1_num_planes(cm));

            av1_loop_filter_init(cm);
            cm.superres_scale_denominator = SCALE_NUMERATOR;
            cm.superres_upscaled_width = oxcf.frm_dim_cfg.width;
            cm.superres_upscaled_height = oxcf.frm_dim_cfg.height;
            // !CONFIG_REALTIME_ONLY: av1_loop_restoration_precal compiled out.
            // CONFIG_THREE_PASS is 0.

            // [subset] second_pass_log_stream / use_ducky_encode (two-pass / ducky encode).

            cm.error.setjmp = 0;
            return cpi;
        }
        catch (AomException e)
        {
            aom_set_error_from_exception(cm.error, e);
            cm.error.setjmp = 0;
            av1_remove_compressor(cpi);
            return null;
        }
    }

    internal static void av1_remove_primary_compressor(AV1_PRIMARY ppi)
    {
        if (ppi == null) return;
        // CONFIG_ENTROPY_STATS / CONFIG_INTERNAL_STATS are 0.

        // [subset] level_params.level_info[]: never allocated (level stats unsupported).
        av1_lookahead_destroy(ppi.lookahead);

        ppi.tpl_sb_rdmult_scaling_factors = Ptr<double>.Null;

        // [subset] tpl_data (txfm_stats_list, tpl_stats_pool[], tpl_rec_pool[],
        // prev_gop_arf_src / prev_gop_arf_tpl_recon) dropped (phase 1: no TPL in realtime).
        // CONFIG_INTERNAL_STATS (ssim_vars) is 0.

        av1_terminate_workers(ppi);
        free_thread_data(ppi);

        ppi.p_mt_info.tile_thr_data = null;
        ppi.p_mt_info.workers = null;
        ppi.p_mt_info.num_workers = 0;

        // aom_free(ppi): garbage collected.
    }

    internal static void av1_remove_compressor(AV1_COMP cpi)
    {
        if (cpi == null) return;
        // CONFIG_RATECTRL_LOG / CONFIG_SPEED_STATS / CONFIG_COLLECT_PARTITION_STATS are 0.
        AV1_COMMON cm = cpi.common;
        // CONFIG_AV1_TEMPORAL_DENOISING is 0.

        if (cm.error != null)
        {
            // Help detect use after free of the error detail string.
            cm.error.detail = new string('A', ARG_ERR_MSG_MAX_LEN - 1);
            // aom_free(cm->error): garbage collected.
        }
        cpi.td.tctx = null;
        MultiThreadInfo mt_info = cpi.mt_info;
        // CONFIG_MULTITHREAD: enc_row_mt / gm_sync / tpl_row_mt / pack_bs_sync mutexes and the
        // enc_row_mt condition variable are managed objects; there is nothing to destroy.
        av1_row_mt_mem_dealloc(cpi);

        if (mt_info.num_workers > 1)
        {
            av1_row_mt_sync_mem_dealloc(cpi.ppi.intra_row_mt_sync);
            av1_loop_filter_dealloc(mt_info.lf_row_sync);
            av1_cdef_mt_dealloc(mt_info.cdef_sync);
            // !CONFIG_REALTIME_ONLY: loop restoration / tf sync dealloc compiled out.
        }
        // CONFIG_THREE_PASS is 0.

        dealloc_compressor_data(cpi);

        av1_ext_part_delete(ref cpi.ext_part_controller);

        av1_remove_common(cm);

        // aom_free(cpi): garbage collected.
    }

    // [subset] generate_psnr_packet: b_calculate_psnr is 0 (CONFIG_INTERNAL_STATS) and
    // AOM_CODEC_USE_PSNR is not supported.

    internal static int av1_use_as_reference(ref int ext_ref_frame_flags, int ref_frame_flags)
    {
        if (ref_frame_flags > ((1 << INTER_REFS_PER_FRAME) - 1)) return -1;

        ext_ref_frame_flags = ref_frame_flags;
        return 0;
    }

    internal static void av1_set_mv_search_params(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        MotionVectorSearchParams mv_search_params = cpi.mv_search_params;
        int max_mv_def = AOMMAX(cm.width, cm.height);

        // Default based on max resolution.
        mv_search_params.mv_step_param = av1_init_search_range(max_mv_def);

        if (cpi.sf.mv_sf.auto_mv_step_size != 0)
        {
            if (frame_is_intra_only(cm) != 0)
            {
                // Initialize max_mv_magnitude for use in the first INTER frame
                // after a key/intra-only frame.
                mv_search_params.max_mv_magnitude = max_mv_def;
            }
            else
            {
                // Use adaptive mv steps based on previous frame stats for show frames and
                // internal arfs.
                FRAME_UPDATE_TYPE cur_update_type =
                    cpi.ppi.gf_group.update_type[cpi.gf_frame_index];
                int use_auto_mv_step =
                    B((cm.show_frame != 0 || cur_update_type == INTNL_ARF_UPDATE) &&
                      mv_search_params.max_mv_magnitude != -1 &&
                      cpi.sf.mv_sf.auto_mv_step_size >= 2);
                if (use_auto_mv_step != 0)
                {
                    // Allow mv_steps to correspond to twice the max mv magnitude found
                    // in the previous frame, capped by the default max_mv_magnitude based
                    // on resolution.
                    mv_search_params.mv_step_param = av1_init_search_range(
                        AOMMIN(max_mv_def, 2 * mv_search_params.max_mv_magnitude));
                }
                // Reset max_mv_magnitude based on update flag.
                if (cpi.do_frame_data_update) mv_search_params.max_mv_magnitude = -1;
            }
        }
    }

    internal static void av1_set_screen_content_options(AV1_COMP cpi, FeatureFlags features)
    {
        AV1_COMMON cm = cpi.common;
        cpi.sc_high_var_blocks = 0;

        if (cm.seq_params.force_screen_content_tools != 2)
        {
            features.allow_screen_content_tools = features.allow_intrabc =
                cm.seq_params.force_screen_content_tools != 0;
            return;
        }

        if (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN)
        {
            features.allow_screen_content_tools = true;
            features.allow_intrabc =
                (cpi.oxcf.mode == REALTIME && cpi.sf.rt_sf.rt_use_intrabc == 0) ? false : true;
            cpi.is_screen_content_type = 1;
            cpi.use_screen_content_tools = 1;
            return;
        }

        if (cpi.oxcf.mode == REALTIME)
        {
            features.allow_screen_content_tools = features.allow_intrabc = false;
            return;
        }

        // [subset] Screen content detection (estimate_screen_content*) is only reached in
        // GOOD / ALLINTRA mode.
        throw new NotSupportedException("[subset] screen content estimation");
    }

    private static void init_motion_estimation(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        MotionVectorSearchParams mv_search_params = cpi.mv_search_params;
        int aligned_width = (cm.width + 7) & ~7;
        int y_stride =
            aom_calc_y_stride(aligned_width, cpi.oxcf.border_in_pixels);
        // cpi->ppi->lookahead->buf is a multi-slot buffer, so buf[0] may not be
        // the current frame's slot and can retain an old stride after a frame resize
        // in av1_lookahead_push(). During av1_encode(), cpi->unscaled_source points
        // to the current frame's lookahead buffer entry (frame_input->source).
        int lookahead_y_stride = cpi.unscaled_source != null
                                     ? cpi.unscaled_source.y_stride
                                     : cpi.ppi.lookahead.buf[0].img.y_stride;
        int y_stride_src = ((cpi.oxcf.frm_dim_cfg.width != cm.width ||
                             cpi.oxcf.frm_dim_cfg.height != cm.height) ||
                            av1_superres_scaled(cm) != 0)
                               ? y_stride
                               : lookahead_y_stride;
        int fpf_y_stride =
            cm.cur_frame != null ? cm.cur_frame.buf.y_stride : y_stride;

        // Update if search_site_cfg is uninitialized or the current frame has a new
        // stride
        bool should_update =
            mv_search_params.search_site_cfg[SS_CFG_SRC][DIAMOND].stride == 0 ||
            mv_search_params.search_site_cfg[SS_CFG_LOOKAHEAD][DIAMOND].stride == 0 ||
            mv_search_params.search_site_cfg[SS_CFG_FPF][DIAMOND].stride == 0 ||
            (y_stride !=
             mv_search_params.search_site_cfg[SS_CFG_SRC][DIAMOND].stride) ||
            (y_stride_src !=
             mv_search_params.search_site_cfg[SS_CFG_LOOKAHEAD][DIAMOND].stride) ||
            (fpf_y_stride !=
             mv_search_params.search_site_cfg[SS_CFG_FPF][DIAMOND].stride);

        if (!should_update)
        {
            return;
        }

        // Initialization of search_site_cfg for NUM_DISTINCT_SEARCH_METHODS.
        for (SEARCH_METHODS i = DIAMOND; i < NUM_DISTINCT_SEARCH_METHODS; i++)
        {
            int level = ((i == NSTEP_8PT) || (i == CLAMPED_DIAMOND)) ? 1 : 0;
            av1_init_motion_compensation[i](
                mv_search_params.search_site_cfg[SS_CFG_SRC][i], y_stride, level);
            av1_init_motion_compensation[i](
                mv_search_params.search_site_cfg[SS_CFG_LOOKAHEAD][i], y_stride_src,
                level);
        }

        // First pass search site config initialization.
        av1_init_motion_fpf(mv_search_params.search_site_cfg[SS_CFG_FPF][DIAMOND],
                            fpf_y_stride);
        for (SEARCH_METHODS i = NSTEP; i < NUM_DISTINCT_SEARCH_METHODS; i++)
        {
            mv_search_params.search_site_cfg[SS_CFG_FPF][i].CopyFrom(
                mv_search_params.search_site_cfg[SS_CFG_FPF][DIAMOND]);
        }
    }

    private static void init_ref_frame_bufs(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        int i;
        if (cm.cur_frame != null)
        {
            cm.cur_frame.ref_count--;
            cm.cur_frame = null;
        }
        for (i = 0; i < REF_FRAMES; ++i)
        {
            if (cm.ref_frame_map[i] != null)
            {
                cm.ref_frame_map[i].ref_count--;
                cm.ref_frame_map[i] = null;
            }
        }
#if DEBUG
        BufferPool pool = cm.buffer_pool;
        for (i = 0; i < pool.num_frame_bufs; ++i)
        {
            Debug.Assert(pool.frame_bufs[i].ref_count == 0);
        }
#endif
    }

    // TODO(chengchen): consider renaming this function as it is necessary
    // for the encoder to setup critical parameters, and it does not
    // deal with initial width any longer.
    internal static aom_codec_err_t av1_check_initial_width(AV1_COMP cpi, int use_highbitdepth,
                                                            int subsampling_x, int subsampling_y)
    {
        AV1_COMMON cm = cpi.common;
        SequenceHeader seq_params = cm.seq_params;

        if (!cpi.frame_size_related_setup_done ||
            seq_params.use_highbitdepth != use_highbitdepth ||
            seq_params.subsampling_x != subsampling_x ||
            seq_params.subsampling_y != subsampling_y)
        {
            seq_params.subsampling_x = subsampling_x;
            seq_params.subsampling_y = subsampling_y;
            seq_params.use_highbitdepth = (byte)use_highbitdepth;

            av1_set_speed_features_framesize_independent(cpi, cpi.oxcf.speed);
            av1_set_speed_features_framesize_dependent(cpi, cpi.oxcf.speed);

            // !CONFIG_REALTIME_ONLY: av1_tf_info_alloc compiled out.
            init_ref_frame_bufs(cpi);

            init_motion_estimation(cpi);  // TODO(agrange) This can be removed.

            cpi.initial_mbs = cm.mi_params.MBs;
            cpi.frame_size_related_setup_done = true;
        }
        return AOM_CODEC_OK;
    }

    // [subset] set_size_literal / av1_set_target_rate: the coded size only differs from
    // cm->width / cm->height with internal resize or superres, which realtime mode does not
    // use (resize_mode / superres_mode are RESIZE_NONE / AOM_SUPERRES_NONE).
    internal static void av1_set_frame_size(AV1_COMP cpi, int width, int height)
    {
        AV1_COMMON cm = cpi.common;
        SequenceHeader seq_params = cm.seq_params;
        int num_planes = av1_num_planes(cm);
        MACROBLOCKD xd = cpi.td.mb.e_mbd;
        int ref_frame;

        if (width != cm.width || height != cm.height)
        {
            // There has been a change in the encoded frame size
            // [subset] set_size_literal (internal resize / superres).
            throw new NotSupportedException("[subset] coded frame size change");
        }
        if (is_stat_consumption_stage(cpi) != 0)
        {
            // [subset] av1_set_target_rate (two-pass stats consumption).
            throw new NotSupportedException("[subset] stat consumption stage");
        }

        alloc_frame_mvs(cm, cm.cur_frame);

        // Allocate above context buffers
        CommonContexts above_contexts = cm.above_contexts;
        if (above_contexts.num_planes < av1_num_planes(cm) ||
            above_contexts.num_mi_cols < cm.mi_params.mi_cols ||
            above_contexts.num_tile_rows < cm.tiles.rows)
        {
            av1_free_above_context_buffers(above_contexts);
            if (av1_alloc_above_context_buffers(above_contexts, cm.tiles.rows,
                                                cm.mi_params.mi_cols,
                                                av1_num_planes(cm)) != 0)
                aom_internal_error(cm.error, AOM_CODEC_MEM_ERROR,
                                   "Failed to allocate context buffers");
        }

        AV1EncoderConfig oxcf = cpi.oxcf;
        oxcf.border_in_pixels = av1_get_enc_border_size(
            av1_is_resize_needed(oxcf), oxcf.kf_cfg.key_freq_max == 0,
            cm.seq_params.sb_size);

        // Reset the frame pointers to the current frame size.
        if (aom_realloc_frame_buffer(
                cm.cur_frame.buf, cm.width, cm.height, seq_params.subsampling_x,
                seq_params.subsampling_y, seq_params.use_highbitdepth,
                cpi.oxcf.border_in_pixels, cm.features.byte_alignment, null, null,
                null, cpi.alloc_pyramid, 0) != 0)
            aom_internal_error(cm.error, AOM_CODEC_MEM_ERROR,
                               "Failed to allocate frame buffer");

        if (is_stat_generation_stage(cpi) == 0) av1_init_cdef_worker(cpi);

        // !CONFIG_REALTIME_ONLY: loop restoration buffers compiled out.

        init_motion_estimation(cpi);

        int has_valid_ref_frame = 0;
        for (ref_frame = LAST_FRAME; ref_frame <= ALTREF_FRAME; ++ref_frame)
        {
            RefCntBuffer buf = get_ref_frame_buf(cm, ref_frame);
            if (buf != null)
            {
                // Motion search may read up to border_in_pixels outside a reference.
                // The border grows when resizing is turned on, so give references
                // coded before that the same border.
                if (buf.buf.border < cpi.oxcf.border_in_pixels)
                {
                    // [subset] aom_yv12_realloc_with_new_border: the border only grows when
                    // resizing gets enabled (av1_is_resize_needed), which realtime does not use.
                    throw new NotSupportedException("[subset] reference border growth");
                }
                scale_factors sf = get_ref_scale_factors(cm, ref_frame);
                av1_setup_scale_factors_for_frame(sf, buf.buf.y_crop_width,
                                                  buf.buf.y_crop_height, cm.width,
                                                  cm.height);
                has_valid_ref_frame |= av1_is_valid_scale(sf);
                if (av1_is_scaled(sf) != 0) aom_extend_frame_borders(buf.buf, num_planes);
            }
        }
        // For 1 pass CBR mode: we can skip this check for spatial enhancement
        // layer if the target_bandwidth is zero, since it will be dropped.
        bool dropped_frame =
            has_no_stats_stage(cpi) != 0 && cpi.oxcf.rc_cfg.mode == AOM_CBR &&
            cpi.svc.spatial_layer_id > 0 && cpi.oxcf.rc_cfg.target_bandwidth == 0;
        if (frame_is_intra_only(cm) == 0 && has_valid_ref_frame == 0 && !dropped_frame)
        {
            aom_internal_error(
                cm.error, AOM_CODEC_CORRUPT_FRAME,
                "Can't find at least one reference frame with valid size");
        }

        av1_setup_scale_factors_for_frame(cm.sf_identity, cm.width, cm.height,
                                          cm.width, cm.height);

        set_ref_ptrs(cm, xd, LAST_FRAME, LAST_FRAME);
    }

    private static int extend_borders_mt(AV1_COMP cpi,
                                         MULTI_THREADED_MODULES stage, int plane)
    {
        AV1_COMMON cm = cpi.common;
        if (cpi.mt_info.num_mod_workers[stage] < 2) return 0;
        switch (stage)
        {
            // TODO(deepa.kg@ittiam.com): When cdef and loop-restoration are disabled,
            // multi-thread frame border extension along with loop filter frame.
            // As loop-filtering of a superblock row modifies the pixels of the
            // above superblock row, border extension requires that loop filtering
            // of the current and above superblock row is complete.
            case MOD_LPF: return 0;
            case MOD_CDEF:
                return B(is_cdef_used(cm) != 0 && !cm.features.allow_intrabc &&
                         cpi.ppi.rtc_ref.non_reference_frame == 0 &&
                         is_restoration_used(cm) == 0 && av1_superres_scaled(cm) == 0);
            case MOD_LR:
                return B(is_restoration_used(cm) != 0 &&
                         (cm.rst_info[plane].frame_restoration_type != RESTORE_NONE));
            default: Debug.Assert(false); break;
        }
        return 0;
    }

    /*!\brief Select and apply cdef filters and switchable restoration filters
     *
     * \ingroup high_level_algo
     */
    private static void cdef_restoration_frame(AV1_COMP cpi, AV1_COMMON cm,
                                               MACROBLOCKD xd, int use_restoration,
                                               int use_cdef,
                                               uint skip_apply_postproc_filters)
    {
        // CONFIG_REALTIME_ONLY
        _ = use_restoration;

        if (use_cdef != 0)
        {
            int num_workers = cpi.mt_info.num_mod_workers[MOD_CDEF];
            // Find CDEF parameters
            long t0 = StageTimes.Start();
            av1_cdef_search(cpi);
            StageTimes.Stop(StageTimes.Stage.CdefSearch, t0);

            // Apply the filter
            if ((skip_apply_postproc_filters & SKIP_APPLY_CDEF) == 0)
            {
                Debug.Assert(cpi.ppi.rtc_ref.non_reference_frame == 0 ||
                             cpi.ppi.b_calculate_psnr != 0);
                t0 = StageTimes.Start();
                if (num_workers > 1)
                {
                    // Extension of frame borders is multi-threaded along with cdef.
                    int do_extend_border =
                        extend_borders_mt(cpi, MOD_CDEF, /* plane */ 0);
                    av1_cdef_frame_mt(cm, xd, cpi.mt_info.cdef_worker,
                                      cpi.mt_info.workers, cpi.mt_info.cdef_sync,
                                      num_workers, av1_cdef_init_fb_row_mt,
                                      do_extend_border);
                }
                else
                {
                    av1_cdef_frame(cm.cur_frame.buf, cm, xd, av1_cdef_init_fb_row);
                }
                StageTimes.Stop(StageTimes.Stage.CdefApply, t0);
            }
        }

        int use_superres = av1_superres_scaled(cm);
        if (use_superres != 0)
        {
            // [subset] av1_superres_post_encode (superres is not used in realtime).
            throw new NotSupportedException("[subset] superres");
        }

        // !CONFIG_REALTIME_ONLY: loop restoration compiled out.
    }

    private static void extend_frame_borders(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        // TODO(debargha): Fix mv search range on encoder side
        for (int plane = 0; plane < av1_num_planes(cm); ++plane)
        {
            bool extend_border_done = extend_borders_mt(cpi, MOD_CDEF, plane) != 0 ||
                                      extend_borders_mt(cpi, MOD_LR, plane) != 0;
            if (!extend_border_done)
            {
                YV12_BUFFER_CONFIG ybf = cm.cur_frame.buf;
                aom_extend_frame_borders_plane_row(ybf, plane, 0,
                                                   ybf.crop_heights[plane > 0 ? 1 : 0]);
            }
        }
    }

    /*!\brief Select and apply deblocking filters, cdef filters, and restoration
     * filters.
     *
     * \ingroup high_level_algo
     */
    private static void loopfilter_frame(AV1_COMP cpi, AV1_COMMON cm)
    {
        MultiThreadInfo mt_info = cpi.mt_info;
        int num_workers = mt_info.num_mod_workers[MOD_LPF];
        int num_planes = av1_num_planes(cm);
        MACROBLOCKD xd = cpi.td.mb.e_mbd;
        cpi.td.mb.rdmult = cpi.rd.RDMULT;

        Debug.Assert(is_lossless_requested(cpi.oxcf.rc_cfg) == 0 ||
                     (cm.features.coded_lossless && cm.features.all_lossless));

        int use_loopfilter =
            B(is_loopfilter_used(cm) != 0 && cpi.mt_info.pipeline_lpf_mt_with_enc == 0);
        int use_cdef = is_cdef_used(cm);
        int use_superres = av1_superres_scaled(cm);
        int use_restoration = is_restoration_used(cm);

        uint skip_apply_postproc_filters =
            derive_skip_apply_postproc_filters(cpi, use_loopfilter, use_cdef,
                                               use_superres, use_restoration);

        if (use_loopfilter != 0)
        {
            long t0 = StageTimes.Start();
            av1_pick_filter_level(cpi.source, cpi, cpi.sf.lpf_sf.lpf_pick);
            StageTimes.Stop(StageTimes.Stage.PickLpf, t0);
            loopfilter lf = cm.lf;
            if ((lf.filter_level[0] != 0 || lf.filter_level[1] != 0) &&
                (skip_apply_postproc_filters & SKIP_APPLY_LOOPFILTER) == 0)
            {
                Debug.Assert(cpi.ppi.rtc_ref.non_reference_frame == 0 ||
                             cpi.ppi.b_calculate_psnr != 0);
                // lpf_opt_level = 1 : Enables dual/quad loop-filtering.
                // lpf_opt_level is set to 1 if transform size search depth in inter
                // blocks is limited to one as quad loop filtering assumes that all the
                // transform blocks within a 16x8/8x16/16x16 prediction block are of the
                // same size. lpf_opt_level = 2 : Filters both chroma planes together, in
                // addition to enabling dual/quad loop-filtering. This is enabled when lpf
                // pick method is LPF_PICK_FROM_Q as u and v plane filter levels are
                // equal.
                int lpf_opt_level = get_lpf_opt_level(cpi.sf);
                long t1 = StageTimes.Start();
                av1_loop_filter_frame_mt(cm.cur_frame.buf, cm, xd, 0, num_planes, 0,
                                         mt_info.workers, num_workers,
                                         mt_info.lf_row_sync, lpf_opt_level);
                StageTimes.Stop(StageTimes.Stage.LoopFilter, t1);
            }
        }

        cdef_restoration_frame(cpi, cm, xd, use_restoration, use_cdef,
                               skip_apply_postproc_filters);
    }

    private static void update_motion_stat(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        CommonModeInfoParams mi_params = cm.mi_params;
        RATE_CONTROL rc = cpi.rc;
        int avg_cnt_zeromv =
            100 * cpi.rc.cnt_zeromv / (mi_params.mi_rows * mi_params.mi_cols);
        if (cpi.ppi.use_svc == 0)
        {
            rc.avg_frame_low_motion =
                (rc.avg_frame_low_motion == 0)
                    ? avg_cnt_zeromv
                    : (3 * rc.avg_frame_low_motion + avg_cnt_zeromv) / 4;
            // For SVC: set avg_frame_low_motion (only computed on top spatial layer)
            // to all lower spatial layers.
            // [subset] SVC layer propagation (use_svc is 0).
        }
        else
        {
            // [subset] SVC (use_svc is 0).
            throw new NotSupportedException("[subset] svc");
        }
    }

    private static int is_quarter_scale(AV1_COMP cpi,
                                        YV12_BUFFER_CONFIG unscaled)
    {
        AV1_COMMON cm = cpi.common;
        SVC svc = cpi.svc;
        return B(cpi.ppi.use_svc != 0 && cm.seq_params.use_highbitdepth == 0 &&
                 svc.spatial_layer_id + 1 < svc.number_spatial_layers &&
                 unscaled.y_crop_width == cm.width << 2 &&
                 unscaled.y_crop_height == cm.height << 2);
    }

    // [subset] scale_twostage: only reached for SVC quarter scaling (is_quarter_scale).

    private static YV12_BUFFER_CONFIG scale_source(AV1_COMP cpi,
                                                   YV12_BUFFER_CONFIG unscaled,
                                                   InterpFilter filter, int phase)
    {
        AV1_COMMON cm = cpi.common;
        SVC svc = cpi.svc;
        if (is_quarter_scale(cpi, unscaled) != 0)
        {
            // [subset] scale_twostage (SVC).
            throw new NotSupportedException("[subset] svc two-stage scaling");
        }
        if (svc.source_half_valid != 0)
        {
            // [subset] SVC source_half reuse: source_half_valid is only set by the SVC
            // two-stage scaling above.
            throw new NotSupportedException("[subset] svc source_half");
        }
        return av1_realloc_and_scale_if_required(
            cm, unscaled, cpi.scaled_source, filter, phase, true, false,
            cpi.oxcf.border_in_pixels, cpi.alloc_pyramid);
    }

    /*!\brief Encode a frame without the recode loop, usually used in one-pass
     * encoding and realtime coding.
     *
     * \ingroup high_level_algo
     *
     * \param[in]    cpi             Top-level encoder structure
     *
     * \return Returns a value to indicate if the encoding is done successfully.
     * \retval #AOM_CODEC_OK
     * \retval #AOM_CODEC_ERROR
     */
    private static int encode_without_recode(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        ref readonly QuantizationCfg q_cfg = ref cpi.oxcf.q_cfg;
        SVC svc = cpi.svc;
        int resize_pending = is_frame_resize_pending(cpi);
        int top_index = 0, bottom_index = 0, q = 0;
        YV12_BUFFER_CONFIG unscaled = cpi.unscaled_source;
        InterpFilter filter_scaler =
            cpi.ppi.use_svc != 0 ? svc.downsample_filter_type[svc.spatial_layer_id]
                                 : EIGHTTAP_SMOOTH;
        int phase_scaler = cpi.ppi.use_svc != 0
                               ? svc.downsample_filter_phase[svc.spatial_layer_id]
                               : 0;

        if (cpi.rc.postencode_drop != 0 && allow_postencode_drop_rtc(cpi) != 0)
            av1_save_all_coding_context(cpi);

        set_size_independent_vars(cpi);
        av1_setup_frame_size(cpi);
        cm.prev_frame = get_primary_ref_frame_buf(cm);
        av1_set_size_dependent_vars(cpi, ref q, ref bottom_index, ref top_index);
        av1_set_mv_search_params(cpi);

        SequenceHeader seq_params = cm.seq_params;
        if ((cpi.svc.source_last_TL0.buffer_alloc_sz == 0 ||
             cpi.svc.source_last_TL0.y_crop_width != cpi.oxcf.frm_dim_cfg.width ||
             cpi.svc.source_last_TL0.y_crop_height != cpi.oxcf.frm_dim_cfg.height ||
             cpi.svc.source_last_TL0.subsampling_x != seq_params.subsampling_x ||
             cpi.svc.source_last_TL0.subsampling_y != seq_params.subsampling_y ||
             cpi.svc.source_last_TL0.flags != cpi.source.flags) &&
            (cpi.ppi.use_svc != 0 || cpi.oxcf.rc_cfg.drop_frames_water_mark > 0))
        {
            if (aom_alloc_frame_buffer(
                    cpi.svc.source_last_TL0, cpi.oxcf.frm_dim_cfg.width,
                    cpi.oxcf.frm_dim_cfg.height, seq_params.subsampling_x,
                    seq_params.subsampling_y, seq_params.use_highbitdepth,
                    cpi.oxcf.border_in_pixels, cm.features.byte_alignment, false,
                    0) != 0)
            {
                aom_internal_error(cm.error, AOM_CODEC_MEM_ERROR,
                                   "Failed to allocate buffer for source_last_TL0");
            }
        }

        if (cpi.ppi.use_svc == 0)
        {
            phase_scaler = 8;
            // 2:1 scaling.
            if ((cm.width << 1) == unscaled.y_crop_width &&
                (cm.height << 1) == unscaled.y_crop_height)
            {
                filter_scaler = BILINEAR;
                // For lower resolutions use eighttap_smooth.
                if (cm.width * cm.height <= 320 * 180) filter_scaler = EIGHTTAP_SMOOTH;
            }
            else if ((cm.width << 2) == unscaled.y_crop_width &&
                     (cm.height << 2) == unscaled.y_crop_height)
            {
                // 4:1 scaling.
                filter_scaler = EIGHTTAP_SMOOTH;
            }
            else if ((cm.width << 2) == 3 * unscaled.y_crop_width &&
                     (cm.height << 2) == 3 * unscaled.y_crop_height)
            {
                // 4:3 scaling.
                filter_scaler = EIGHTTAP_REGULAR;
            }
        }

        allocate_gradient_info_for_hog(cpi);

        allocate_src_var_of_4x4_sub_block_buf(cpi);

        SPEED_FEATURES sf = cpi.sf;
        if (sf.part_sf.partition_search_type == VAR_BASED_PARTITION)
            variance_partition_alloc(cpi);

        if (cm.current_frame.frame_type == KEY_FRAME ||
            ((sf.inter_sf.extra_prune_warped != 0 && cpi.refresh_frame.golden_frame)))
            copy_frame_prob_info(cpi);

        // CONFIG_TUNE_BUTTERAUGLI is 0.

        cpi.source = scale_source(cpi, unscaled, filter_scaler, phase_scaler);
        if (cpi.oxcf.mode != ALLINTRA &&
            (frame_is_intra_only(cm) != 0 || resize_pending != 0 ||
             cpi.consec_zero_mv.IsNull))
        {
            int current_size =
                (cm.mi_params.mi_rows * cm.mi_params.mi_cols) >> 2;
            if (cpi.consec_zero_mv.IsNull ||
                cpi.consec_zero_mv_alloc_size < current_size)
            {
                cpi.consec_zero_mv = Ptr<byte>.Null;
                cpi.consec_zero_mv_alloc_size = 0;
                cpi.consec_zero_mv = new byte[current_size];
                cpi.consec_zero_mv_alloc_size = current_size;
            }
            Debug.Assert(!cpi.consec_zero_mv.IsNull);
            memset(cpi.consec_zero_mv, (byte)0, current_size);
        }

        if (cpi.scaled_last_source_available != 0)
        {
            cpi.last_source = cpi.scaled_last_source;
            cpi.scaled_last_source_available = 0;
        }
        else if (cpi.unscaled_last_source != null)
        {
            if (is_quarter_scale(cpi, cpi.unscaled_last_source) != 0)
            {
                // [subset] scale_twostage (SVC).
                throw new NotSupportedException("[subset] svc two-stage scaling");
            }
            cpi.last_source =
                av1_realloc_and_scale_if_required(
                    cm, cpi.unscaled_last_source, cpi.scaled_last_source,
                    filter_scaler, phase_scaler, true, false,
                    cpi.oxcf.border_in_pixels, cpi.alloc_pyramid);
        }

        if (cpi.sf.rt_sf.use_temporal_noise_estimate != 0)
        {
            av1_update_noise_estimate(cpi);
        }

        // CONFIG_AV1_TEMPORAL_DENOISING is 0.

        // For 1 spatial layer encoding: if the (non-LAST) reference has different
        // resolution from the source then disable that reference. This is to avoid
        // significant increase in encode time from scaling the references in
        // av1_scale_references. Note GOLDEN is forced to update on the (first/tigger)
        // resized frame and ALTREF will be refreshed ~4 frames later, so both
        // references become available again after few frames.
        // For superres: don't disable golden reference.
        if (svc.number_spatial_layers == 1)
        {
            if (!cpi.oxcf.superres_cfg.enable_superres)
            {
                if ((cpi.ref_frame_flags & av1_ref_frame_flag_list[GOLDEN_FRAME]) != 0)
                {
                    YV12_BUFFER_CONFIG @ref =
                        get_ref_frame_yv12_buf(cm, GOLDEN_FRAME);
                    if (@ref == null || @ref.y_crop_width != cm.width ||
                        @ref.y_crop_height != cm.height)
                    {
                        cpi.ref_frame_flags ^= AOM_GOLD_FLAG;
                    }
                }
            }
            if ((cpi.ref_frame_flags & av1_ref_frame_flag_list[ALTREF_FRAME]) != 0)
            {
                YV12_BUFFER_CONFIG @ref =
                    get_ref_frame_yv12_buf(cm, ALTREF_FRAME);
                if (@ref == null || @ref.y_crop_width != cm.width ||
                    @ref.y_crop_height != cm.height)
                {
                    cpi.ref_frame_flags ^= AOM_ALT_FLAG;
                }
            }
        }

        int scale_references = 0;
        // CONFIG_FPMT_TEST is 0.
        if (scale_references != 0 ||
            cpi.ppi.gf_group.frame_parallel_level[cpi.gf_frame_index] == 0)
        {
            if (frame_is_intra_only(cm) == 0)
            {
                av1_scale_references(cpi, filter_scaler, phase_scaler, 1);
            }
        }

        av1_set_quantizer(cm, q_cfg.qm_minlevel, q_cfg.qm_maxlevel, q,
                          B(q_cfg.enable_chroma_deltaq), B(q_cfg.enable_hdr_deltaq),
                          cpi.oxcf.mode == ALLINTRA, cpi.oxcf.tune_cfg.tuning);
        av1_set_speed_features_qindex_dependent(cpi, cpi.oxcf.speed);
        av1_init_quantizer(cpi.enc_quant_dequant_params, cm.quant_params,
                           cm.seq_params.bit_depth, cpi.oxcf.algo_cfg.sharpness);
        av1_set_variance_partition_thresholds(cpi, q, 0);
        av1_setup_frame(cpi);

        // Check if this high_source_sad (scene/slide change) frame should be
        // encoded at high/max QP, and if so, set the q and adjust some rate
        // control parameters.
        if (cpi.sf.rt_sf.overshoot_detection_cbr == FAST_DETECTION_MAXQ &&
            cpi.rc.high_source_sad != 0)
        {
            if (av1_encodedframe_overshoot_cbr(cpi, ref q) != 0)
            {
                av1_set_quantizer(cm, q_cfg.qm_minlevel, q_cfg.qm_maxlevel, q,
                                  B(q_cfg.enable_chroma_deltaq), B(q_cfg.enable_hdr_deltaq),
                                  cpi.oxcf.mode == ALLINTRA, cpi.oxcf.tune_cfg.tuning);
                av1_set_speed_features_qindex_dependent(cpi, cpi.oxcf.speed);
                av1_init_quantizer(cpi.enc_quant_dequant_params, cm.quant_params,
                                   cm.seq_params.bit_depth,
                                   cpi.oxcf.algo_cfg.sharpness);
                av1_set_variance_partition_thresholds(cpi, q, 0);
                if (frame_is_intra_only(cm) != 0 || cm.features.error_resilient_mode ||
                    cm.features.primary_ref_frame == PRIMARY_REF_NONE)
                    av1_setup_frame(cpi);
            }
        }
        av1_apply_active_map(cpi);
        if (cpi.roi.enabled != 0)
        {
            // For now if roi map is used: don't setup cyclic refresh.
            // [subset] av1_apply_roi_map (the ROI map control is not supported).
            throw new NotSupportedException("[subset] roi map");
        }
        else if (q_cfg.aq_mode == CYCLIC_REFRESH_AQ)
        {
            av1_cyclic_refresh_setup(cpi);
        }
        if (cm.seg.enabled != 0)
        {
            av1_reuse_ref_segment_features(cm);
            if (cm.seg.update_data == 0 && cm.prev_frame != null)
            {
                segfeatures_copy(cm.seg, cm.prev_frame.seg);
                cm.seg.enabled = cm.prev_frame.seg.enabled;
            }
            else
            {
                av1_calculate_segdata(cm.seg);
            }
        }
        else
        {
            cm.seg.CopyFrom(new segmentation());  // memset(&cm->seg, 0, sizeof(cm->seg))
        }
        segfeatures_copy(cm.cur_frame.seg, cm.seg);
        cm.cur_frame.seg.enabled = cm.seg.enabled;

        // This is for rtc temporal filtering case.
        if (is_psnr_calc_enabled(cpi) != 0 && cpi.sf.rt_sf.use_rtc_tf != 0)
        {
            // [subset] PSNR calculation (b_calculate_psnr is 0).
            throw new NotSupportedException("[subset] psnr");
        }

        // Set the motion vector precision based on mv stats from the last coded
        // frame.
        if (frame_is_intra_only(cm) == 0) av1_pick_and_set_high_precision_mv(cpi, q);

        if (svc.temporal_layer_id == 0)
        {
            cpi.rc.num_col_blscroll_last_tl0 = 0;
            cpi.rc.num_row_blscroll_last_tl0 = 0;
        }

        // transform / motion compensation build reconstruction frame
        long tEnc = StageTimes.Start();
        av1_encode_frame(cpi);
        StageTimes.Stop(StageTimes.Stage.EncodeTiles, tEnc);

        if (cpi.rc.rtc_external_ratectrl == 0 && frame_is_intra_only(cm) == 0)
            update_motion_stat(cpi);

        // Adjust the refresh of the golden (longer-term) reference based on QP
        // selected for this frame. This is for CBR real-time mode, and only
        // for single layer without usage of the set_ref_frame_config (so
        // reference structure for 1 layer is set internally).
        if (frame_is_intra_only(cm) == 0 && cpi.oxcf.rc_cfg.mode == AOM_CBR &&
            cpi.oxcf.mode == REALTIME && svc.number_spatial_layers == 1 &&
            svc.number_temporal_layers == 1 && cpi.rc.rtc_external_ratectrl == 0 &&
            cpi.ppi.rtc_ref.set_ref_frame_config == 0 &&
            sf.rt_sf.gf_refresh_based_on_qp != 0)
            av1_adjust_gf_refresh_qp_one_pass_rt(cpi);

        // For non-svc: if scaling is required, copy scaled_source
        // into scaled_last_source.
        if (cm.current_frame.frame_number > 1 && cpi.ppi.use_svc == 0 &&
            !cpi.scaled_source.y_buffer.IsNull &&
            !cpi.scaled_last_source.y_buffer.IsNull &&
            cpi.scaled_source.y_crop_width == cpi.scaled_last_source.y_crop_width &&
            cpi.scaled_source.y_crop_height ==
                cpi.scaled_last_source.y_crop_height &&
            (cm.width != cpi.unscaled_source.y_crop_width ||
             cm.height != cpi.unscaled_source.y_crop_height))
        {
            cpi.scaled_last_source_available = 1;
            aom_yv12_copy_y(cpi.scaled_source, cpi.scaled_last_source, 1);
            aom_yv12_copy_u(cpi.scaled_source, cpi.scaled_last_source, 1);
            aom_yv12_copy_v(cpi.scaled_source, cpi.scaled_last_source, 1);
        }

        // CONFIG_INTERNAL_STATS is 0.

        return AOM_CODEC_OK;
    }

    // [port] int64_t *sse / int64_t *rate: always NULL from encode_frame_to_data_rate() in
    // realtime (encode_with_and_without_superres is not used), so they are dropped.
    private static int encode_with_recode_loop_and_filter(AV1_COMP cpi, ref int size,
                                                          Ptr<byte> dest, int dest_size,
                                                          ref int largest_tile_id)
    {
        for (int i = 0; i < NUM_RECODES_PER_FRAME; i++)
        {
            cpi.do_update_frame_probs_txtype[i] = 0;
            cpi.do_update_frame_probs_obmc[i] = 0;
            cpi.do_update_frame_probs_warp[i] = 0;
            cpi.do_update_frame_probs_interpfilter[i] = 0;
        }

        cpi.do_update_vbr_bits_off_target_fast = 0;
        int err;
        // CONFIG_REALTIME_ONLY
        err = encode_without_recode(cpi);
        if (err != AOM_CODEC_OK)
        {
            if (err == -1)
            {
                err = AOM_CODEC_OK;
                // sse / rate are NULL.
                largest_tile_id = 0;
            }
            return err;
        }

        AV1_COMMON cm = cpi.common;
        SequenceHeader seq_params = cm.seq_params;

        if (cpi.ppi.p_rc.next_key_frame_forced != 0 && cpi.rc.frames_to_key == 1)
        {
            // [subset] aom_get_y_sse: next_key_frame_forced is only set by two-pass rate
            // control.
            throw new NotSupportedException("[subset] forced key frame ambient error");
        }

        cm.cur_frame.buf.color_primaries = seq_params.color_primaries;
        cm.cur_frame.buf.transfer_characteristics =
            seq_params.transfer_characteristics;
        cm.cur_frame.buf.matrix_coefficients = seq_params.matrix_coefficients;
        cm.cur_frame.buf.monochrome = seq_params.monochrome;
        cm.cur_frame.buf.chroma_sample_position =
            seq_params.chroma_sample_position;
        cm.cur_frame.buf.color_range = seq_params.color_range;
        cm.cur_frame.buf.render_width = cm.render_width;
        cm.cur_frame.buf.render_height = cm.render_height;

        if (cpi.mt_info.pipeline_lpf_mt_with_enc == 0)
            set_postproc_filter_default_params(cpi.common);

        if (!cm.features.allow_intrabc)
        {
            loopfilter_frame(cpi, cm);
        }

        if (cpi.oxcf.mode != ALLINTRA && cpi.ppi.rtc_ref.non_reference_frame == 0)
        {
            long tExt = StageTimes.Start();
            extend_frame_borders(cpi);
            StageTimes.Stop(StageTimes.Stage.ExtendBorders, tExt);
        }

        if (cpi.oxcf.tune_cfg.content == AOM_CONTENT_FILM)
        {
            // [subset] set_grain_syn_params (film grain is not supported).
            throw new NotSupportedException("[subset] film grain");
        }

        av1_finalize_encoded_frame(cpi);
        cpi.rc.coefficient_size = 0;
        long tPack = StageTimes.Start();
        if (av1_pack_bitstream(cpi, dest, dest_size, ref size, ref largest_tile_id) !=
            AOM_CODEC_OK)
            return AOM_CODEC_ERROR;
        StageTimes.Stop(StageTimes.Stage.PackBitstream, tPack);

        if (cpi.rc.postencode_drop != 0 && allow_postencode_drop_rtc(cpi) != 0 &&
            av1_postencode_drop_cbr(cpi, ref size) != 0)
        {
            return AOM_CODEC_OK;
        }

        // sse / rate are NULL.
        // !CONFIG_REALTIME_ONLY: ducky encode compiled out.

        return AOM_CODEC_OK;
    }

    // Conditions to disable cdf_update mode in selective mode for real-time.
    // Handle case for layers, scene change, and resizing.
    private static int selective_disable_cdf_rtc(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        RATE_CONTROL rc = cpi.rc;
        // For single layer.
        if (cpi.svc.number_spatial_layers == 1 &&
            cpi.svc.number_temporal_layers == 1)
        {
            CYCLIC_REFRESH cr = cpi.cyclic_refresh;
            // Don't disable on intra_only, scene change (high_source_sad = 1),
            // or resized frame. To avoid quality loss force enable at
            // for ~30 frames after key or scene/slide change, and
            // after 8 frames since last update if frame_source_sad > 0.
            // The cyclic refresh counter only advances on frames that apply
            // cyclic refresh. An active map can keep cyclic refresh off for the
            // whole stream, so use frames_since_scene_change then.
            if (frame_is_intra_only(cm) != 0 || is_frame_resize_pending(cpi) != 0 ||
                rc.high_source_sad != 0 || rc.frames_since_key < 30 ||
                (cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ &&
                 ((cr.apply_cyclic_refresh != 0 || cpi.active_map.enabled == 0)
                      ? cr.counter_encode_maxq_scene_change
                      : rc.frames_since_scene_change) < 30) ||
                (cpi.frames_since_last_update > 8 && cpi.rc.frame_source_sad > 0))
                return 0;
            else
                return 1;
        }
        else if (cpi.svc.number_temporal_layers > 1)
        {
            // Disable only on top temporal enhancement layer for now.
            return B(cpi.svc.temporal_layer_id == cpi.svc.number_temporal_layers - 1);
        }
        return 1;
    }

    /*!\brief Run the final pass encoding for 1-pass/2-pass encoding mode, and pack
     * the bitstream
     *
     * \ingroup high_level_algo
     * \callgraph
     * \callergraph
     *
     * \param[in]    cpi             Top-level encoder structure
     * \param[in]    size            Bitstream size
     * \param[out]   dest            Bitstream output buffer
     * \param[in]    dest_size       Bitstream output buffer size
     *
     * \return Returns a value to indicate if the encoding is done successfully.
     * \retval #AOM_CODEC_OK
     * \retval #AOM_CODEC_ERROR
     */
    private static int encode_frame_to_data_rate(AV1_COMP cpi, ref int size,
                                                 Ptr<byte> dest, int dest_size)
    {
        AV1_COMMON cm = cpi.common;
        SequenceHeader seq_params = cm.seq_params;
        CurrentFrame current_frame = cm.current_frame;
        AV1EncoderConfig oxcf = cpi.oxcf;
        segmentation seg = cm.seg;
        FeatureFlags features = cm.features;
        ref readonly TileConfig tile_cfg = ref oxcf.tile_cfg;
        Debug.Assert(cpi.source != null);
        cpi.td.mb.e_mbd.cur_buf = cpi.source;

        // !CONFIG_REALTIME_ONLY: calculate_frame_avg_haar_energy compiled out.

        cm.cur_frame.frame_type = current_frame.frame_type;

        cm.tiles.large_scale = (uint)B(tile_cfg.enable_large_scale_tile);
        cm.tiles.single_tile_decoding = (uint)B(tile_cfg.enable_single_tile_decoding);

        features.allow_ref_frame_mvs &= frame_might_allow_ref_frame_mvs(cm) != 0;
        features.allow_ref_frame_mvs &= cm.tiles.large_scale == 0;

        features.allow_warped_motion = oxcf.motion_mode_cfg.allow_warped_motion &&
                                       frame_might_allow_warped_motion(cm) != 0;

        cpi.last_frame_type = current_frame.frame_type;

        if (frame_is_intra_only(cm) != 0)
        {
            cpi.frames_since_last_update = 0;
        }

        if (frame_is_sframe(cm) != 0)
        {
            GF_GROUP gf_group = cpi.ppi.gf_group;
            gf_group.update_type[gf_group.size] = GF_UPDATE;
        }

        if (encode_show_existing_frame(cm) != 0)
        {
            // [subset] show_existing_frame: one-pass realtime never shows an existing frame
            // (no ARF / overlay).
            throw new NotSupportedException("[subset] show existing frame");
        }

        if (is_stat_generation_stage(cpi) == 0 &&
            cpi.common.features.allow_screen_content_tools &&
            frame_is_intra_only(cm) == 0 && cpi.sf.rt_sf.use_nonrd_pick_mode == 0)
        {
            // [subset] av1_is_integer_mv: screen content with the RD pick mode (realtime
            // uses use_nonrd_pick_mode = 1).
            throw new NotSupportedException("[subset] rd screen content integer mv");
        }
        else
        {
            cpi.common.features.cur_frame_force_integer_mv = false;
        }

        cpi.td.mb.e_mbd.cur_frame_force_integer_mv =
            B(cpi.common.features.cur_frame_force_integer_mv);

        cm.lf.mode_ref_delta_update = 0;

        if (frame_is_intra_only(cm) != 0 || frame_is_sframe(cm) != 0)
        {
            av1_reset_segment_features(cm);

            if (seg.enabled != 0)
            {
                seg.update_map = 1;
                seg.update_data = 1;
            }
        }
        if (tile_cfg.mtu == 0)
        {
            cpi.num_tg = (int)tile_cfg.num_tile_groups;
        }
        else
        {
            cpi.num_tg = DEFAULT_MAX_NUM_TG;
        }

        if (has_no_stats_stage(cpi) != 0 && oxcf.rc_cfg.mode == AOM_CBR)
        {
            if ((cpi.svc.spatial_layer_id > 0 &&
                 cpi.oxcf.rc_cfg.target_bandwidth == 0) ||
                av1_rc_drop_frame(cpi) != 0)
            {
                cpi.is_dropped_frame = true;
            }
            if (cpi.is_dropped_frame)
            {
                av1_setup_frame_size(cpi);
                av1_set_mv_search_params(cpi);
                av1_rc_postencode_update_drop_frame(cpi);
                release_scaled_references(cpi);
                cpi.ppi.gf_group.is_frame_dropped[cpi.gf_frame_index] = true;
                if (cm.show_frame != 0)
                {
                    update_counters_for_show_frame(cpi);
                }
                return AOM_CODEC_OK;
            }
        }

        if (oxcf.tune_cfg.tuning == AOM_TUNE_SSIM ||
            oxcf.tune_cfg.tuning == AOM_TUNE_IQ ||
            oxcf.tune_cfg.tuning == AOM_TUNE_SSIMULACRA2)
        {
            // [subset] av1_set_mb_ssim_rdmult_scaling (tuning is AOM_TUNE_PSNR).
            throw new NotSupportedException("[subset] ssim tuning");
        }
        // CONFIG_SALIENCY_MAP / CONFIG_TUNE_VMAF are 0.

        if (cpi.oxcf.q_cfg.deltaq_mode == DELTA_Q_PERCEPTUAL_AI &&
            cpi.sf.rt_sf.use_nonrd_pick_mode == 0)
        {
            // [subset] wiener variance deltaq (deltaq_mode is 0 in realtime).
            throw new NotSupportedException("[subset] perceptual AI deltaq");
        }

        if (cpi.oxcf.q_cfg.deltaq_mode == DELTA_Q_USER_RATING_BASED)
        {
            // [subset] user rating based deltaq.
            throw new NotSupportedException("[subset] user rating deltaq");
        }

        // CONFIG_INTERNAL_STATS is 0.

        if (seq_params.frame_id_numbers_present_flag != 0)
        {
            /* Non-normative definition of current_frame_id ("frame counter" with
             * wraparound) */
            if (cm.current_frame_id == -1)
            {
                int lsb, msb;
                /* quasi-random initialization of current_frame_id for a key frame */
                if ((cpi.source.flags & YV12_FLAG_HIGHBITDEPTH) != 0)
                {
                    // CONFIG_AV1_HIGHBITDEPTH is 0.
                    throw new NotSupportedException("[subset] high bitdepth");
                }
                else
                {
                    lsb = cpi.source.y_buffer[0] & 0xff;
                    msb = cpi.source.y_buffer[1] & 0xff;
                }
                cm.current_frame_id =
                    ((msb << 8) + lsb) % (1 << seq_params.frame_id_length);

                if (oxcf.kf_cfg.sframe_dist != 0) cm.current_frame_id = 0x37;
            }
            else
            {
                cm.current_frame_id =
                    (cm.current_frame_id + 1 + (1 << seq_params.frame_id_length)) %
                    (1 << seq_params.frame_id_length);
            }
        }

        switch (oxcf.algo_cfg.cdf_update_mode)
        {
            case 0:  // No CDF update for any frames(4~6% compression loss).
                features.disable_cdf_update = true;
                break;
            case 1:  // Enable CDF update for all frames.
                if (cpi.sf.rt_sf.disable_cdf_update_non_reference_frame &&
                    cpi.ppi.rtc_ref.non_reference_frame != 0 && cpi.rc.frames_since_key > 2)
                    features.disable_cdf_update = true;
                else if (cpi.sf.rt_sf.selective_cdf_update != 0)
                    features.disable_cdf_update = selective_disable_cdf_rtc(cpi) != 0;
                else
                    features.disable_cdf_update = false;
                break;
            case 2:
                if (oxcf.mode == GOOD || oxcf.mode == ALLINTRA)
                {
                    features.disable_cdf_update =
                        (frame_is_intra_only(cm) != 0 || cm.show_frame == 0) ? false : true;
                }
                else
                {
                    features.disable_cdf_update = selective_disable_cdf_rtc(cpi) != 0;
                }
                break;
        }

        if (!cpi.do_frame_data_update &&
            cpi.ppi.gf_group.update_type[cpi.gf_frame_index] == INTNL_ARF_UPDATE)
        {
            Debug.Assert(cpi.ppi.gf_group.frame_parallel_level[cpi.gf_frame_index] == 1);
            features.disable_cdf_update = true;
        }

        // !CONFIG_REALTIME_ONLY: global motion pyramid invalidation compiled out.

        int largest_tile_id = 0;
        if (av1_superres_in_recode_allowed(cpi) != 0)
        {
            // [subset] encode_with_and_without_superres (superres auto mode).
            throw new NotSupportedException("[subset] superres recode");
        }
        else
        {
            aom_superres_mode orig_superres_mode = cpi.superres_mode;  // save
            cpi.superres_mode = cpi.oxcf.superres_cfg.superres_mode;
            if (encode_with_recode_loop_and_filter(cpi, ref size, dest, dest_size,
                                                   ref largest_tile_id) != AOM_CODEC_OK)
            {
                return AOM_CODEC_ERROR;
            }
            cpi.superres_mode = orig_superres_mode;  // restore
        }

        if (seq_params.frame_id_numbers_present_flag != 0)
        {
            for (int i = 0; i < REF_FRAMES; i++)
            {
                if (((current_frame.refresh_frame_flags >> i) & 1) != 0)
                {
                    cm.ref_frame_id[i] = cm.current_frame_id;
                }
            }
        }

        if (cpi.svc.spatial_layer_id == cpi.svc.number_spatial_layers - 1)
            cpi.svc.num_encoded_top_layer++;

        if (cm.seg.enabled != 0 && cm.seg.update_map == 0)
        {
            int seg_map_size = cm.cur_frame.mi_cols *
                               cm.cur_frame.mi_rows;
            if (!cm.last_frame_seg_map.IsNull)
            {
                memcpy(cm.cur_frame.seg_map, cm.last_frame_seg_map, seg_map_size);
            }
            else
            {
                memset(cm.cur_frame.seg_map, (byte)0, seg_map_size);
            }
        }

        int release_scaled_refs = 0;
        // CONFIG_FPMT_TEST is 0.
        if (release_scaled_refs != 0 ||
            cpi.ppi.gf_group.frame_parallel_level[cpi.gf_frame_index] == 0)
        {
            if (frame_is_intra_only(cm) == 0)
            {
                release_scaled_references(cpi);
            }
        }
        // CONFIG_AV1_TEMPORAL_DENOISING is 0.

        if (cm.show_frame != 0) cpi.last_show_frame_buf = cm.cur_frame;

        if (features.refresh_frame_context == REFRESH_FRAME_CONTEXT_BACKWARD)
        {
            cm.fc.CopyFrom(cpi.tile_data[largest_tile_id].tctx);
            av1_reset_cdf_symbol_counters(cm.fc);
        }
        if (cm.tiles.large_scale == 0)
        {
            cm.cur_frame.frame_context.CopyFrom(cm.fc);
        }

        if (tile_cfg.enable_ext_tile_debug)
        {
            if (cm.tiles.large_scale != 0 && is_stat_consumption_stage(cpi) != 0)
            {
                // [subset] av1_print_frame_contexts (large scale tile debug output).
                throw new NotSupportedException("[subset] ext tile debug");
            }
        }

        cpi.last_frame_type = current_frame.frame_type;

        if (cm.features.disable_cdf_update)
        {
            cpi.frames_since_last_update++;
        }
        else
        {
            cpi.frames_since_last_update = 1;
        }

        if (cpi.svc.spatial_layer_id == cpi.svc.number_spatial_layers - 1)
        {
            cpi.svc.prev_number_spatial_layers = cpi.svc.number_spatial_layers;
        }
        cpi.svc.prev_number_temporal_layers = cpi.svc.number_temporal_layers;

        cm.seg.update_map = 0;
        cm.seg.update_data = 0;
        cm.lf.mode_ref_delta_update = 0;

        if (cm.show_frame != 0)
        {
            update_counters_for_show_frame(cpi);
        }

        return AOM_CODEC_OK;
    }

    internal static int av1_encode(AV1_COMP cpi, Ptr<byte> dest, int dest_size,
                                   EncodeFrameInput frame_input,
                                   EncodeFrameParams frame_params,
                                   ref int frame_size)
    {
        AV1_COMMON cm = cpi.common;
        CurrentFrame current_frame = cm.current_frame;

        cpi.unscaled_source = frame_input.source;
        cpi.source = frame_input.source;
        cpi.unscaled_last_source = frame_input.last_source;

        current_frame.refresh_frame_flags = frame_params.refresh_frame_flags;
        cm.features.error_resilient_mode = frame_params.error_resilient_mode != 0;
        cm.features.primary_ref_frame = frame_params.primary_ref_frame;
        cm.current_frame.frame_type = frame_params.frame_type;
        cm.show_frame = frame_params.show_frame;
        cpi.ref_frame_flags = frame_params.ref_frame_flags;
        cpi.speed = frame_params.speed;
        cm.show_existing_frame = frame_params.show_existing_frame;
        cpi.existing_fb_idx_to_show = frame_params.existing_fb_idx_to_show;

        if (cpi.ppi.seq_params_locked != 0 && is_stat_generation_stage(cpi) == 0 &&
            cm.current_frame.frame_type == KEY_FRAME && cm.show_frame != 0 &&
            cm.show_existing_frame == 0)
        {
            SequenceHeader seq_params = cm.seq_params;
            if (set_max_frame_size(seq_params, cpi.oxcf.frm_dim_cfg))
            {
                set_bitstream_level_tier(cpi.ppi, seq_params.max_frame_width,
                                         seq_params.max_frame_height,
                                         cpi.oxcf.input_cfg.init_framerate, cm.error);
            }
        }

        if (cpi.oxcf.mode == GOOD &&
            cpi.oxcf.q_cfg.deltaq_mode == DELTA_Q_OBJECTIVE &&
            cpi.oxcf.algo_cfg.enable_tpl_model && cpi.oxcf.q_cfg.aq_mode == NO_AQ &&
            cpi.common.seg.enabled == 0 && cpi.roi.enabled == 0 && cpi.oxcf.sb_qp_sweep == 0 &&
            cpi.use_ducky_encode == 0 && cpi.oxcf.algo_cfg.sharpness != 3)
        {
            cpi.do_border_pad = true;
        }
        else
        {
            cpi.do_border_pad = false;
        }

        Array.Copy(frame_params.remapped_ref_idx, cm.remapped_ref_idx, REF_FRAMES);

        cpi.refresh_frame = frame_params.refresh_frame;

        if (current_frame.frame_type == KEY_FRAME &&
            cpi.ppi.gf_group.refbuf_state[cpi.gf_frame_index] == REFBUF_RESET)
        {
            current_frame.frame_number = 0;
        }

        current_frame.order_hint =
            (uint)(current_frame.frame_number + frame_params.order_offset);

        current_frame.display_order_hint = current_frame.order_hint;
        current_frame.order_hint %=
            (uint)(1 << (cm.seq_params.order_hint_info.order_hint_bits_minus_1 + 1));

        current_frame.pyramid_level = (uint)get_true_pyr_level(
            cpi.ppi.gf_group.layer_depth[cpi.gf_frame_index],
            (int)current_frame.display_order_hint, cpi.ppi.gf_group.max_layer_depth);

        // !CONFIG_REALTIME_ONLY: TPL prev_gop_arf buffers compiled out.

        if (is_stat_generation_stage(cpi) != 0)
        {
            // !CONFIG_REALTIME_ONLY: first pass compiled out.
        }
        else if (cpi.oxcf.pass == AOM_RC_ONE_PASS ||
                 cpi.oxcf.pass >= AOM_RC_SECOND_PASS)
        {
            if (encode_frame_to_data_rate(cpi, ref frame_size, dest, dest_size) !=
                AOM_CODEC_OK)
            {
                return AOM_CODEC_ERROR;
            }
        }
        else
        {
            return AOM_CODEC_ERROR;
        }

        if (cpi.ext_ratectrl.ready != 0)
        {
            // [subset] av1_extrc_update_encodeframe_result: the external rate control
            // interface is never set (ext_ratectrl.ready is false).
            throw new NotSupportedException("[subset] external rate control");
        }

        return AOM_CODEC_OK;
    }

    // [subset] apply_denoise_2d: CONFIG_DENOISE && !CONFIG_REALTIME_ONLY.

    internal static int av1_receive_raw_frame(AV1_COMP cpi, aom_enc_frame_flags_t frame_flags,
                                              YV12_BUFFER_CONFIG sd, long time_stamp,
                                              long end_time)
    {
        AV1_COMMON cm = cpi.common;
        SequenceHeader seq_params = cm.seq_params;
        int res = 0;
        int subsampling_x = sd.subsampling_x;
        int subsampling_y = sd.subsampling_y;
        int use_highbitdepth = B((sd.flags & YV12_FLAG_HIGHBITDEPTH) != 0);

        if ((seq_params.profile == PROFILE_0) && seq_params.monochrome == 0 &&
            (subsampling_x != 1 || subsampling_y != 1))
        {
            aom_set_error(cm.error, AOM_CODEC_INVALID_PARAM,
                          "Non-4:2:0 color format requires profile 1 or 2");
            return -1;
        }
        if ((seq_params.profile == PROFILE_1) &&
            !(subsampling_x == 0 && subsampling_y == 0))
        {
            aom_set_error(cm.error, AOM_CODEC_INVALID_PARAM,
                          "Profile 1 requires 4:4:4 color format");
            return -1;
        }
        if ((seq_params.profile == PROFILE_2) &&
            (seq_params.bit_depth <= AOM_BITS_10) &&
            !(subsampling_x == 1 && subsampling_y == 0))
        {
            aom_set_error(cm.error, AOM_CODEC_INVALID_PARAM,
                          "Profile 2 bit-depth <= 10 requires 4:2:2 color format");
            return -1;
        }

        // CONFIG_TUNE_VMAF / CONFIG_INTERNAL_STATS / CONFIG_AV1_TEMPORAL_DENOISING are 0.

        // CONFIG_DENOISE
        if (cpi.oxcf.noise_level > 0 && !(cpi.oxcf.pass == AOM_RC_FIRST_PASS &&
                                          cpi.oxcf.enable_dnl_denoising == 0))
        {
            // !CONFIG_REALTIME_ONLY: noise estimation / apply_denoise_2d compiled out.
        }

        if (av1_lookahead_push(cpi.ppi.lookahead, sd, time_stamp, end_time,
                               use_highbitdepth, cpi.alloc_pyramid, frame_flags) != 0)
        {
            aom_set_error(cm.error, AOM_CODEC_ERROR, "av1_lookahead_push() failed");
            res = -1;
        }

        return res;
    }

    private static void update_keyframe_counters(AV1_COMP cpi)
    {
        if (cpi.common.show_frame != 0 && cpi.rc.frames_to_key != 0)
        {
            // !CONFIG_REALTIME_ONLY: firstpass_info compiled out.
            if (cpi.svc.spatial_layer_id == cpi.svc.number_spatial_layers - 1)
            {
                cpi.rc.frames_since_key++;
                cpi.rc.frames_to_key--;
                cpi.rc.frames_to_fwd_kf--;
                cpi.rc.frames_since_scene_change++;
            }
        }
    }

    private static void update_frames_till_gf_update(AV1_COMP cpi)
    {
        if (cpi.common.show_frame != 0 ||
            is_frame_droppable(cpi.ppi.rtc_ref, cpi.ext_flags.refresh_frame) != 0)
        {
            if (cpi.rc.frames_till_gf_update_due > 0)
                cpi.rc.frames_till_gf_update_due--;
        }
    }

    private static void update_gf_group_index(AV1_COMP cpi)
    {
        ++cpi.gf_frame_index;
        if (cpi.gf_frame_index == MAX_STATIC_GF_GROUP_LENGTH)
            cpi.gf_frame_index = 0;
    }

    private static void update_fb_of_context_type(AV1_COMP cpi,
                                                  int[] fb_of_context_type)
    {
        AV1_COMMON cm = cpi.common;
        int current_frame_ref_type = get_current_frame_ref_type(cpi);

        if (frame_is_intra_only(cm) != 0 || cm.features.error_resilient_mode ||
            cpi.ext_flags.use_primary_ref_none)
        {
            for (int i = 0; i < REF_FRAMES; i++)
            {
                fb_of_context_type[i] = -1;
            }
            fb_of_context_type[current_frame_ref_type] =
                cm.show_frame != 0 ? get_ref_frame_map_idx(cm, GOLDEN_FRAME)
                                   : get_ref_frame_map_idx(cm, ALTREF_FRAME);
        }

        if (encode_show_existing_frame(cm) == 0)
        {
            // Refresh fb_of_context_type[]: see encoder.h for explanation
            if (cm.current_frame.frame_type == KEY_FRAME)
            {
                // All ref frames are refreshed, pick one that will live long enough
                fb_of_context_type[current_frame_ref_type] = 0;
            }
            else
            {
                // If more than one frame is refreshed, it doesn't matter which one we
                // pick so pick the first.  LST sometimes doesn't refresh any: this is ok

                for (int i = 0; i < REF_FRAMES; i++)
                {
                    if ((cm.current_frame.refresh_frame_flags & (1 << i)) != 0)
                    {
                        fb_of_context_type[current_frame_ref_type] = i;
                        break;
                    }
                }
            }
        }
    }

    private static void update_rc_counts(AV1_COMP cpi)
    {
        update_keyframe_counters(cpi);
        update_frames_till_gf_update(cpi);
        update_gf_group_index(cpi);
    }

    private static void update_end_of_frame_stats(AV1_COMP cpi)
    {
        if (cpi.do_frame_data_update)
        {
            // Store current frame loopfilter levels in ppi, if update flag is set.
            if (cpi.common.show_existing_frame == 0)
            {
                AV1_COMMON cm = cpi.common;
                loopfilter lf = cm.lf;
                cpi.ppi.filter_level[0] = lf.backup_filter_level[0];
                cpi.ppi.filter_level[1] = lf.backup_filter_level[1];
                cpi.ppi.filter_level_u = lf.backup_filter_level_u;
                cpi.ppi.filter_level_v = lf.backup_filter_level_v;
            }
        }
        // Store frame level mv_stats from cpi to ppi.
        cpi.ppi.mv_stats = cpi.mv_stats;
    }

    // Updates frame level stats related to global motion
    private static void update_gm_stats(AV1_COMP cpi)
    {
        FRAME_UPDATE_TYPE update_type =
            cpi.ppi.gf_group.update_type[cpi.gf_frame_index];
        int i, is_gm_present = 0;

        // Check if the current frame has any valid global motion model across its
        // reference frames
        for (i = 0; i < REF_FRAMES; i++)
        {
            if (cpi.common.global_motion[i].wmtype != IDENTITY)
            {
                is_gm_present = 1;
                break;
            }
        }
        int update_actual_stats = 1;
        // CONFIG_FPMT_TEST is 0.
        if (update_actual_stats != 0)
        {
            if (cpi.ppi.valid_gm_model_found[update_type] == int.MaxValue)
            {
                cpi.ppi.valid_gm_model_found[update_type] = is_gm_present;
            }
            else
            {
                cpi.ppi.valid_gm_model_found[update_type] |= is_gm_present;
            }
        }
    }

    internal static void av1_post_encode_updates(AV1_COMP cpi,
                                                 AV1_COMP_DATA cpi_data)
    {
        AV1_PRIMARY ppi = cpi.ppi;
        AV1_COMMON cm = cpi.common;

        if (ppi.b_freeze_internal_state != 0)
        {
            // Restore the frame state (frame_number, refresh_frame) at the end of a
            // frozen encode: only the source is popped.
            if (ppi.b_calculate_psnr != 0 && cpi_data.frame_size > 0)
            {
                // [subset] generate_psnr_packet (b_calculate_psnr is 0).
                throw new NotSupportedException("[subset] psnr packet");
            }

            if (cpi_data.pop_lookahead == 1)
            {
                av1_lookahead_pop(cpi.ppi.lookahead, cpi_data.flush,
                                  cpi.compressor_stage);
            }
            return;
        }

        update_gm_stats(cpi);

        // !CONFIG_REALTIME_ONLY: twopass stats compiled out.
        // CONFIG_OUTPUT_FRAME_SIZE is 0.

        if (is_stat_generation_stage(cpi) == 0 && !cpi.is_dropped_frame)
        {
            // Before calling refresh_reference_frames(), copy ppi->ref_frame_map_copy
            // to cm->ref_frame_map for frame_parallel_level 2 frame in a parallel
            // encode set of lower layer frames.
            // TODO(Remya): Move ref_frame_map from AV1_COMMON to AV1_PRIMARY to avoid
            // copy.
            if (ppi.gf_group.frame_parallel_level[cpi.gf_frame_index] == 2 &&
                ppi.gf_group.frame_parallel_level[cpi.gf_frame_index - 1] == 1 &&
                ppi.gf_group.update_type[cpi.gf_frame_index - 1] ==
                    INTNL_ARF_UPDATE)
            {
                Array.Copy(ppi.ref_frame_map_copy, cm.ref_frame_map, REF_FRAMES);
            }
            refresh_reference_frames(cpi);
            // For frame_parallel_level 1 frame in a parallel encode set of lower layer
            // frames, store the updated cm->ref_frame_map in ppi->ref_frame_map_copy.
            if (ppi.gf_group.frame_parallel_level[cpi.gf_frame_index] == 1 &&
                ppi.gf_group.update_type[cpi.gf_frame_index] == INTNL_ARF_UPDATE)
            {
                Array.Copy(cm.ref_frame_map, ppi.ref_frame_map_copy, REF_FRAMES);
            }
            av1_rc_postencode_update(cpi, (ulong)cpi_data.frame_size);
        }

        if (cpi_data.pop_lookahead == 1)
        {
            av1_lookahead_pop(cpi.ppi.lookahead, cpi_data.flush,
                              cpi.compressor_stage);
        }
        if (cpi.common.show_frame != 0)
        {
            cpi.ppi.ts_start_last_show_frame = cpi_data.ts_frame_start;
            cpi.ppi.ts_end_last_show_frame = cpi_data.ts_frame_end;
        }
        if (ppi.level_params.keep_level_stats != 0 && is_stat_generation_stage(cpi) == 0)
        {
            // [subset] av1_init_level_info / av1_update_level_info (level stats are never
            // kept, see av1_change_config).
            throw new NotSupportedException("[subset] level stats");
        }

        if (is_stat_generation_stage(cpi) == 0)
        {
            // !CONFIG_REALTIME_ONLY: av1_twopass_postencode_update compiled out.
            update_fb_of_context_type(cpi, ppi.fb_of_context_type);
            update_rc_counts(cpi);
            update_end_of_frame_stats(cpi);
        }

        // CONFIG_THREE_PASS is 0.

        if (ppi.rtc_ref.set_ref_frame_config != 0 && !cpi.is_dropped_frame)
        {
            // [subset] av1_svc_update_buffer_slot_refreshed /
            // av1_svc_set_reference_was_previous (AV1E_SET_SVC_REF_FRAME_CONFIG).
            throw new NotSupportedException("[subset] svc ref frame config");
        }

        if (ppi.use_svc != 0)
        {
            // [subset] av1_save_layer_context (use_svc is 0).
            throw new NotSupportedException("[subset] svc");
        }

        if (ppi.b_calculate_psnr != 0 && cpi_data.frame_size > 0)
        {
            // [subset] generate_psnr_packet (b_calculate_psnr is 0).
            throw new NotSupportedException("[subset] psnr packet");
        }

        // CONFIG_INTERNAL_STATS / CONFIG_THREE_PASS are 0.
    }

    internal static int av1_get_compressed_data(AV1_COMP cpi, AV1_COMP_DATA cpi_data)
    {
        AV1EncoderConfig oxcf = cpi.oxcf;
        AV1_COMMON cm = cpi.common;

        try
        {
            cm.error.setjmp = 1;
            cpi_data.pop_lookahead = 0;

            // CONFIG_INTERNAL_STATS / CONFIG_ENTROPY_STATS / CONFIG_BITSTREAM_DEBUG are 0.
            if (cpi.ppi.use_svc != 0)
            {
                // [subset] av1_one_pass_cbr_svc_start_layer (use_svc is 0).
                throw new NotSupportedException("[subset] svc");
            }

            cpi.is_dropped_frame = false;
            cm.showable_frame = 0;
            cpi_data.frame_size = 0;
            av1_set_high_precision_mv(cpi, 1, 0);

            // Normal defaults
            cm.features.refresh_frame_context =
                oxcf.tool_cfg.frame_parallel_decoding_mode
                    ? REFRESH_FRAME_CONTEXT_DISABLED
                    : REFRESH_FRAME_CONTEXT_BACKWARD;
            if (oxcf.tile_cfg.enable_large_scale_tile)
                cm.features.refresh_frame_context = REFRESH_FRAME_CONTEXT_DISABLED;

            if (assign_cur_frame_new_fb(cm) == null)
            {
                aom_internal_error(cpi.common.error, AOM_CODEC_ERROR,
                                   "Failed to allocate new cur_frame");
            }

            int result = av1_encode_strategy(
                cpi, ref cpi_data.frame_size, cpi_data.cx_data, cpi_data.cx_data_sz,
                ref cpi_data.lib_flags, ref cpi_data.ts_frame_start, ref cpi_data.ts_frame_end,
                cpi_data.timestamp_ratio, ref cpi_data.pop_lookahead, cpi_data.flush);

            // CONFIG_COLLECT_COMPONENT_TIMING is 0.

            // Reset the flag to 0 afer encoding.
            cpi.rc.use_external_qp_one_pass = 0;

            if (result == -1)
            {
                cm.error.setjmp = 0;
                // Returning -1 indicates no frame encoded; more input is required.
                return -1;
            }
            if (result != AOM_CODEC_OK)
            {
                aom_internal_error(cpi.common.error, AOM_CODEC_ERROR,
                                   "Failed to encode frame");
            }
            // CONFIG_INTERNAL_STATS / CONFIG_SPEED_STATS are 0.

            cm.error.setjmp = 0;
            return AOM_CODEC_OK;
        }
        catch (AomException e)
        {
            aom_set_error_from_exception(cm.error, e);
            cm.error.setjmp = 0;
            if (cpi_data.pop_lookahead == 1 && oxcf.pass == AOM_RC_ONE_PASS &&
                oxcf.gf_cfg.lag_in_frames == 0)
            {
                av1_lookahead_pop(cpi.ppi.lookahead, cpi_data.flush,
                                  cpi.compressor_stage);
            }
            return cm.error.error_code;
        }
    }

    // Initialize parallel frame contexts with screen content decisions.
    internal static void av1_init_sc_decisions(AV1_PRIMARY ppi)
    {
        AV1_COMP first_cpi = ppi.cpi;
        for (int i = 1; i < ppi.num_fp_contexts; ++i)
        {
            AV1_COMP cur_cpi = ppi.parallel_cpi[i];
            cur_cpi.common.features.allow_screen_content_tools =
                first_cpi.common.features.allow_screen_content_tools;
            cur_cpi.common.features.allow_intrabc =
                first_cpi.common.features.allow_intrabc;
            cur_cpi.use_screen_content_tools = first_cpi.use_screen_content_tools;
            cur_cpi.is_screen_content_type = first_cpi.is_screen_content_type;
        }
    }

    // [port] YV12_BUFFER_CONFIG *frame: C copies the struct (*frame = buf); the port copies
    // into the caller's buffer object.
    internal static int av1_get_last_show_frame(AV1_COMP cpi, YV12_BUFFER_CONFIG frame)
    {
        if (cpi.last_show_frame_buf == null ||
            cpi.oxcf.algo_cfg.skip_postproc_filtering)
            return -1;

        frame.CopyFrom(cpi.last_show_frame_buf.buf);
        return 0;
    }

    internal static int av1_get_quantizer(AV1_COMP cpi)
    {
        return cpi.common.quant_params.base_qindex;
    }

    // [port] buffer_size and *frame_size are size_t (int in the port).
    internal static int av1_convert_sect5obus_to_annexb(Ptr<byte> buffer, int buffer_size,
                                                        ref int frame_size)
    {
        Debug.Assert(frame_size <= buffer_size);
        int output_size = 0;
        int remaining_size = frame_size;
        Ptr<byte> buff_ptr = buffer;

        // go through each OBUs
        while (remaining_size > 0)
        {
            byte[] saved_obu_header = new byte[2];
            ulong obu_payload_size = 0;
            int length_of_payload_size = 0;
            int length_of_obu_size;
            uint obu_header_size = ((buff_ptr[0] >> 2) & 0x1) != 0 ? 2u : 1u;
            int obu_bytes_read = (int)obu_header_size;  // bytes read for current obu

            // save the obu header (1 or 2 bytes)
            memcpy<byte>(saved_obu_header, buff_ptr, (int)obu_header_size);
            // clear the obu_has_size_field
            saved_obu_header[0] &= unchecked((byte)~0x2);

            // get the payload_size and length of payload_size
            if (aom_uleb_decode(buff_ptr + (int)obu_header_size,
                                remaining_size - (int)obu_header_size, ref obu_payload_size,
                                ref length_of_payload_size) != 0)
            {
                return AOM_CODEC_ERROR;
            }
            obu_bytes_read += length_of_payload_size;

            // calculate the length of size of the obu header plus payload
            ulong obu_size = obu_header_size + obu_payload_size;
            length_of_obu_size = aom_uleb_size_in_bytes(obu_size);

            if (length_of_obu_size + obu_header_size >
                buffer_size - output_size - (remaining_size - obu_bytes_read))
            {
                return AOM_CODEC_ERROR;
            }
            // move the rest of data to new location
            memmove(buff_ptr + length_of_obu_size + (int)obu_header_size,
                    buff_ptr + obu_bytes_read, remaining_size - obu_bytes_read);
            obu_bytes_read += (int)obu_payload_size;

            // write the new obu size
            int coded_obu_size = 0;
            if (aom_uleb_encode(obu_size, length_of_obu_size, buff_ptr,
                                ref coded_obu_size) != 0 ||
                coded_obu_size != length_of_obu_size)
            {
                return AOM_CODEC_ERROR;
            }

            // write the saved (modified) obu_header following obu size
            memcpy<byte>(buff_ptr + length_of_obu_size, saved_obu_header, (int)obu_header_size);

            remaining_size -= obu_bytes_read;
            buff_ptr += length_of_obu_size + (int)obu_size;
            output_size += length_of_obu_size + (int)obu_size;
        }

        frame_size = output_size;
        return AOM_CODEC_OK;
    }

    // [subset] rtc_set_updates_ref_frame_config / rtc_set_references_external_ref_frame_config:
    // only with AV1E_SET_SVC_REF_FRAME_CONFIG (set_ref_frame_config is 0).

    internal static void av1_apply_encoding_flags(AV1_COMP cpi, aom_enc_frame_flags_t flags)
    {
        // TODO(yunqingwang): For what references to use, external encoding flags
        // should be consistent with internal reference frame selection. Need to
        // ensure that there is not conflict between the two. In AV1 encoder, the
        // priority rank for 7 reference frames are: LAST, ALTREF, LAST2, LAST3,
        // GOLDEN, BWDREF, ALTREF2.

        ExternalFlags ext_flags = cpi.ext_flags;
        ref ExtRefreshFrameFlagsInfo ext_refresh_frame_flags =
            ref ext_flags.refresh_frame;
        ext_flags.ref_frame_flags = AOM_REFFRAME_ALL;
        if ((flags &
             (AOM_EFLAG_NO_REF_LAST | AOM_EFLAG_NO_REF_LAST2 | AOM_EFLAG_NO_REF_LAST3 |
              AOM_EFLAG_NO_REF_GF | AOM_EFLAG_NO_REF_ARF | AOM_EFLAG_NO_REF_BWD |
              AOM_EFLAG_NO_REF_ARF2)) != 0)
        {
            int @ref = AOM_REFFRAME_ALL;

            if ((flags & AOM_EFLAG_NO_REF_LAST) != 0) @ref ^= AOM_LAST_FLAG;
            if ((flags & AOM_EFLAG_NO_REF_LAST2) != 0) @ref ^= AOM_LAST2_FLAG;
            if ((flags & AOM_EFLAG_NO_REF_LAST3) != 0) @ref ^= AOM_LAST3_FLAG;

            if ((flags & AOM_EFLAG_NO_REF_GF) != 0) @ref ^= AOM_GOLD_FLAG;

            if ((flags & AOM_EFLAG_NO_REF_ARF) != 0)
            {
                @ref ^= AOM_ALT_FLAG;
                @ref ^= AOM_BWD_FLAG;
                @ref ^= AOM_ALT2_FLAG;
            }
            else
            {
                if ((flags & AOM_EFLAG_NO_REF_BWD) != 0) @ref ^= AOM_BWD_FLAG;
                if ((flags & AOM_EFLAG_NO_REF_ARF2) != 0) @ref ^= AOM_ALT2_FLAG;
            }

            av1_use_as_reference(ref ext_flags.ref_frame_flags, @ref);
        }
        else
        {
            if (cpi.ppi.rtc_ref.set_ref_frame_config != 0)
            {
                // [subset] rtc_set_references_external_ref_frame_config.
                throw new NotSupportedException("[subset] svc ref frame config");
            }
        }

        if ((flags &
             (AOM_EFLAG_NO_UPD_LAST | AOM_EFLAG_NO_UPD_GF | AOM_EFLAG_NO_UPD_ARF)) != 0)
        {
            int upd = AOM_REFFRAME_ALL;

            // Refreshing LAST/LAST2/LAST3 is handled by 1 common flag.
            if ((flags & AOM_EFLAG_NO_UPD_LAST) != 0) upd ^= AOM_LAST_FLAG;

            if ((flags & AOM_EFLAG_NO_UPD_GF) != 0) upd ^= AOM_GOLD_FLAG;

            if ((flags & AOM_EFLAG_NO_UPD_ARF) != 0)
            {
                upd ^= AOM_ALT_FLAG;
                upd ^= AOM_BWD_FLAG;
                upd ^= AOM_ALT2_FLAG;
            }

            ext_refresh_frame_flags.last_frame = (upd & AOM_LAST_FLAG) != 0;
            ext_refresh_frame_flags.golden_frame = (upd & AOM_GOLD_FLAG) != 0;
            ext_refresh_frame_flags.alt_ref_frame = (upd & AOM_ALT_FLAG) != 0;
            ext_refresh_frame_flags.bwd_ref_frame = (upd & AOM_BWD_FLAG) != 0;
            ext_refresh_frame_flags.alt2_ref_frame = (upd & AOM_ALT2_FLAG) != 0;
            ext_refresh_frame_flags.update_pending = true;
        }
        else
        {
            if (cpi.ppi.rtc_ref.set_ref_frame_config != 0)
            {
                // [subset] rtc_set_updates_ref_frame_config.
                throw new NotSupportedException("[subset] svc ref frame config");
            }
            else
                ext_refresh_frame_flags.update_pending = false;
        }

        ext_flags.use_ref_frame_mvs = cpi.oxcf.tool_cfg.enable_ref_frame_mvs &
                                      ((flags & AOM_EFLAG_NO_REF_FRAME_MVS) == 0);
        ext_flags.use_error_resilient = cpi.oxcf.tool_cfg.error_resilient_mode |
                                        ((flags & AOM_EFLAG_ERROR_RESILIENT) != 0);
        ext_flags.use_s_frame =
            cpi.oxcf.kf_cfg.enable_sframe | ((flags & AOM_EFLAG_SET_S_FRAME) != 0);
        ext_flags.use_primary_ref_none =
            (flags & AOM_EFLAG_SET_PRIMARY_REF_NONE) != 0;

        if ((flags & AOM_EFLAG_NO_UPD_ENTROPY) != 0)
        {
            update_entropy(ref ext_flags.refresh_frame_context,
                           ref ext_flags.refresh_frame_context_pending, false);
        }
    }

    // [subset] av1_get_global_headers: AOM_CODEC_USE_... global headers (the
    // get_global_headers interface) are not supported.
}
