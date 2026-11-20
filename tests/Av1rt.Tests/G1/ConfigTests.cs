using System;
using System.Collections.Generic;
using System.Linq;
using Av1rt.Aom;
using Xunit;
using Xunit.Abstractions;
using static Av1rt.Aom.aom_codec;
using static Av1rt.Aom.aom_encoder;
using static Av1rt.Aom.aomcx;
using static Av1rt.Aom.av1_cx_iface;
using static Av1rt.Aom.encoder;
using static Av1rt.Tests.G1.G1Port;

namespace Av1rt.Tests.G1;

/// <summary>
/// av1_cx_iface.c / aom_encoder.c / encoder.c configuration layer against libaom:
/// aom_codec_enc_config_default, validate_config, set_encoder_config, av1_get_encoder_config,
/// aom_codec_enc_init (reject path) and init_seq_coding_tools (via av1_create_primary_compressor).
/// </summary>
public class ConfigTests
{
    private readonly ITestOutputHelper output;

    public ConfigTests(ITestOutputHelper output) => this.output = output;

    private static readonly long[] Interesting =
    {
        -2, -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 15, 16, 17, 31, 32, 33, 35, 63, 64,
        100, 101, 127, 128, 129, 1000, 1001, 10000, 10001, 65536, 65537, 2000000, 2000001,
        1000000000, 1000000001, int.MaxValue, uint.MaxValue,
    };

    private static readonly (int w, int h)[] Resolutions =
    {
        (16, 16), (176, 144), (320, 240), (352, 288), (640, 360), (640, 480), (1280, 720), (1920, 1080),
        (2560, 1440), (3840, 2160), (7680, 4320),
    };

    private static readonly int[] Threads = { 1, 2, 4, 8, 16, 64 };

    private static IEnumerable<long> AllSetIds()
    {
        for (int i = 0; i < G1Fields.CfgSetCount; i++) yield return i;
        for (int i = 0; i < G1Fields.ExtraSetCount; i++) yield return 1000 + i;
    }

    [Fact]
    public void FieldTablesMatchShim()
    {
        var counts = new int[5];
        Assert.Equal(5, G1Shim.aomshim_g1_counts(counts));
        Assert.Equal(new[] { G1Fields.CfgDumpCount, G1Fields.OxcfDumpCount, G1Fields.SeqDumpCount,
                             G1Fields.CfgSetCount, G1Fields.ExtraSetCount }, counts);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(0xffffffffu)]
    public void ConfigDefaultMatchesLibaom(uint usage)
    {
        var expected = new long[G1Fields.CfgDumpCount];
        int n = G1Shim.aomshim_g1_config_default(usage, expected);

        var cfg = new aom_codec_enc_cfg_t();
        int res = aom_codec_enc_config_default(aom_codec_av1_cx(), cfg, usage);
        if (n < 0)
        {
            Assert.Equal(-n, res);
            return;
        }
        Assert.Equal(0, res);
        var actual = new long[G1Fields.CfgDumpCount];
        int m = G1Fields.DumpCfg(cfg, actual);
        AssertDumpEqual("aom_codec_enc_cfg_t", G1Fields.CfgDumpNames, expected, n, actual, m, $"usage {usage}");
    }

    [Fact]
    public void ConfigDefaultRejectsNullArguments()
    {
        Assert.Equal(AOM_CODEC_INVALID_PARAM, aom_codec_enc_config_default(null, new aom_codec_enc_cfg_t(), 1));
        Assert.Equal(AOM_CODEC_INVALID_PARAM, aom_codec_enc_config_default(aom_codec_av1_cx(), null, 1));
    }

    private static void CompareValidate(long[] kv, List<string> failures)
    {
        var (eRes, eDetail) = G1Shim.ValidateConfig(kv);
        var (aRes, aDetail) = ValidateConfig(kv);
        if (eRes != aRes || eDetail != aDetail)
            failures.Add($"{Describe(kv)}: libaom {eRes} \"{eDetail}\", port {aRes} \"{aDetail}\"");
    }

    private static void AssertNoFailures(List<string> failures)
    {
        if (failures.Count > 0)
            Assert.Fail($"{failures.Count} mismatches:\n  " + string.Join("\n  ", failures.Take(40)));
    }

    [Fact]
    public void ValidateConfigSingleFieldSweep()
    {
        var failures = new List<string>();
        int accepted = 0, rejected = 0;
        CompareValidate(Array.Empty<long>(), failures);
        foreach (long id in AllSetIds())
        {
            foreach (long v in Interesting)
            {
                long[] kv = { id, v };
                CompareValidate(kv, failures);
                if (G1Shim.ValidateConfig(kv).res == 0) accepted++;
                else rejected++;
            }
        }
        AssertNoFailures(failures);
        Assert.True(accepted > 1000 && rejected > 1000, $"accepted {accepted}, rejected {rejected}");
    }

    public static IEnumerable<object[]> ValidateCombos()
    {
        object[][] combos =
        {
            new object[] { },
            new object[] { "g_w", 32768, "g_h", 32768 },
            new object[] { "g_w", 32769, "g_h", 32768 },
            new object[] { "g_w", 65536, "g_h", 65536 },
            new object[] { "g_forced_max_frame_width", 32768, "g_forced_max_frame_height", 32769 },
            new object[] { "g_forced_max_frame_width", 100, "g_w", 320 },
            new object[] { "g_forced_max_frame_height", 100, "g_h", 240 },
            new object[] { "g_forced_max_frame_width", 1920, "g_forced_max_frame_height", 1080, "g_w", 1920, "g_h", 1080 },
            new object[] { "rc_min_quantizer", 40, "rc_max_quantizer", 30 },
            new object[] { "rc_min_quantizer", 30, "rc_max_quantizer", 30 },
            new object[] { "lossless", 1, "aq_mode", 1 },
            new object[] { "lossless", 1, "aq_mode", 0, "enable_chroma_deltaq", 1 },
            new object[] { "lossless", 1, "aq_mode", 0 },
            new object[] { "large_scale_tile", 1, "aq_mode", 3 },
            new object[] { "large_scale_tile", 1, "aq_mode", 0 },
            new object[] { "g_profile", 1, "monochrome", 1 },
            new object[] { "g_profile", 2, "monochrome", 1 },
            new object[] { "g_profile", 0, "g_bit_depth", 12, "g_input_bit_depth", 8 },
            new object[] { "g_profile", 1, "g_bit_depth", 12 },
            new object[] { "g_profile", 2, "g_bit_depth", 12 },
            new object[] { "g_bit_depth", 8, "g_input_bit_depth", 10 },
            new object[] { "g_bit_depth", 10, "g_input_bit_depth", 10 },
            new object[] { "g_bit_depth", 10, "g_input_bit_depth", 12 },
            new object[] { "rc_end_usage", 3, "use_fixed_qp_offsets", 1 },
            new object[] { "rc_end_usage", 3, "use_fixed_qp_offsets", 2 },
            new object[] { "rc_end_usage", 3, "use_fixed_qp_offsets", 3 },
            new object[] { "rc_end_usage", 1, "use_fixed_qp_offsets", 1 },
            new object[] { "rc_end_usage", 2, "use_fixed_qp_offsets", 1 },
            new object[] { "g_pass", 0, "passes", 2 },
            new object[] { "g_pass", 0, "passes", 1 },
            new object[] { "g_pass", 1, "passes", 1 },
            new object[] { "g_pass", 1, "passes", 2 },
            new object[] { "g_pass", 2, "passes", 2 },
            new object[] { "g_pass", 3, "passes", 3 },
            new object[] { "rc_end_usage", 0, "vbr_corpus_complexity_lap", 10000 },
            new object[] { "rc_end_usage", 0, "vbr_corpus_complexity_lap", 10001 },
            new object[] { "rc_end_usage", 1, "vbr_corpus_complexity_lap", 1 },
            new object[] { "rc_end_usage", 0, "g_pass", 1, "vbr_corpus_complexity_lap", 1 },
            new object[] { "matrix_coefficients", 0, "chroma_subsampling_x", 1, "chroma_subsampling_y", 0 },
            new object[] { "matrix_coefficients", 0, "chroma_subsampling_x", 0, "chroma_subsampling_y", 1 },
            new object[] { "matrix_coefficients", 0, "chroma_subsampling_x", 0, "chroma_subsampling_y", 0 },
            new object[] { "min_partition_size", 64, "max_partition_size", 32 },
            new object[] { "min_partition_size", 32, "max_partition_size", 32 },
            new object[] { "kf_max_pyr_height", 2, "gf_min_pyr_height", 3 },
            new object[] { "kf_max_pyr_height", 3, "gf_min_pyr_height", 3 },
            new object[] { "kf_max_pyr_height", -1, "gf_min_pyr_height", 5 },
            new object[] { "gf_min_pyr_height", 4, "gf_max_pyr_height", 3 },
            new object[] { "max_gf_interval", 1 },
            new object[] { "max_gf_interval", 4, "min_gf_interval", 8 },
            new object[] { "max_gf_interval", 8, "min_gf_interval", 8 },
            new object[] { "target_seq_level_idx[0]", 2 },
            new object[] { "target_seq_level_idx[1]", 3 },
            new object[] { "target_seq_level_idx[0]", 24 },
            new object[] { "target_seq_level_idx[0]", 28 },
            new object[] { "target_seq_level_idx[0]", 31 },
            new object[] { "target_seq_level_idx[1]", 32 },
            new object[] { "target_seq_level_idx[0]", 33 },
            new object[] { "tuning", AOM_TUNE_BUTTERAUGLI },
            new object[] { "tuning", 3 },
            new object[] { "tuning", AOM_TUNE_VMAF_WITH_PREPROCESSING },
            new object[] { "tuning", 7 },
            new object[] { "tuning", AOM_TUNE_IQ },
            new object[] { "tuning", AOM_TUNE_SSIMULACRA2 },
            new object[] { "tuning", 12 },
            new object[] { "cpu_used", 12, "g_usage", 1 },
            new object[] { "cpu_used", 10, "g_usage", 0 },
            new object[] { "g_usage", 2, "g_lag_in_frames", 1 },
            new object[] { "g_usage", 2, "kf_max_dist", 1 },
            new object[] { "rc_end_usage", 0, "g_lag_in_frames", 35 },
            new object[] { "g_lag_in_frames", 36 },
            new object[] { "g_threads", 64, "row_mt", 1, "tile_columns", 6, "tile_rows", 6, "auto_tiles", 1 },
            new object[] { "g_timebase.num", 0 },
            new object[] { "g_timebase.den", 0 },
        };
        foreach (object[] c in combos) yield return new object[] { Kv(c) };
    }

    [Theory]
    [MemberData(nameof(ValidateCombos))]
    public void ValidateConfigCombosMatchLibaom(long[] kv)
    {
        var failures = new List<string>();
        CompareValidate(kv, failures);
        AssertNoFailures(failures);
    }

    // aom_codec_enc_init_ver -> encoder_init -> validate_config: the reject path (with ctx.err
    // and ctx.err_detail), which stops before the compressor is created.
    [Fact]
    public void EncInitRejectsLikeLibaom()
    {
        var failures = new List<string>();
        int checkedCount = 0;
        for (long id = 0; id < G1Fields.CfgSetCount; id++)
        {
            foreach (long v in Interesting)
            {
                long[] kv = { id, v };
                var (eRes, eDetail) = G1Shim.EncInit(kv);
                if (eRes == 0) continue;  // accepted configs create a full encoder
                checkedCount++;

                var (cfg, _) = MakeConfig(kv);
                var ctx = new aom_codec_ctx_t();
                int aRes = aom_codec_enc_init_ver(ctx, aom_codec_av1_cx(), cfg, 0, AOM_ENCODER_ABI_VERSION);
                if (eRes != aRes || eDetail != ctx.err_detail || ctx.err != aRes || ctx.priv != null)
                    failures.Add($"{Describe(kv)}: libaom {eRes} \"{eDetail}\", port {aRes} \"{ctx.err_detail}\" err {ctx.err}");
            }
        }
        AssertNoFailures(failures);
        Assert.True(checkedCount > 100, $"only {checkedCount} rejects");
    }

    [Fact]
    public void EncInitChecksArgumentsAndAbi()
    {
        var cfg = new aom_codec_enc_cfg_t();
        Assert.Equal(0, aom_codec_enc_config_default(aom_codec_av1_cx(), cfg, AOM_USAGE_REALTIME));
        var ctx = new aom_codec_ctx_t();
        Assert.Equal(AOM_CODEC_ABI_MISMATCH, aom_codec_enc_init_ver(ctx, aom_codec_av1_cx(), cfg, 0, AOM_ENCODER_ABI_VERSION + 1));
        Assert.Equal(AOM_CODEC_INVALID_PARAM, aom_codec_enc_init_ver(ctx, null, cfg, 0, AOM_ENCODER_ABI_VERSION));
        Assert.Equal(AOM_CODEC_INVALID_PARAM, aom_codec_enc_init_ver(ctx, aom_codec_av1_cx(), null, 0, AOM_ENCODER_ABI_VERSION));
        Assert.Equal(AOM_CODEC_INCAPABLE, aom_codec_enc_init_ver(ctx, aom_codec_av1_cx(), cfg, AOM_CODEC_USE_HIGHBITDEPTH, AOM_ENCODER_ABI_VERSION));
    }

    // ---- set_encoder_config ----

    private static void CompareSetEncoderConfig(long[] kv)
    {
        var expected = new long[G1Fields.OxcfDumpCount];
        var tiles = new long[2];
        int n = G1Shim.aomshim_g1_set_encoder_config(kv, kv.Length / 2, expected, tiles);

        AV1EncoderConfig oxcf = SetEncoderConfig(kv, out av1_extracfg extra);
        var actual = new long[G1Fields.OxcfDumpCount];
        int m = G1Fields.DumpOxcf(oxcf, actual);
        AssertDumpEqual("AV1EncoderConfig", G1Fields.OxcfDumpNames, expected, n, actual, m, Describe(kv));
        Assert.True(tiles[0] == extra.tile_columns && tiles[1] == extra.tile_rows,
                    $"extra_cfg tiles for {Describe(kv)}: libaom {tiles[0]}x{tiles[1]}, port {extra.tile_columns}x{extra.tile_rows}");
    }

    public static IEnumerable<object[]> ResolutionThreadCases()
    {
        foreach (var (w, h) in Resolutions)
            foreach (int t in Threads)
                yield return new object[] { w, h, t };
    }

    [Theory]
    [MemberData(nameof(ResolutionThreadCases))]
    public void SetEncoderConfigResolutionsAndThreads(int w, int h, int threads)
    {
        long[] size = Kv("g_w", w, "g_h", h, "g_threads", threads);
        foreach (int autoTiles in new[] { 0, 1 })
        {
            foreach (int rowMt in new[] { 0, 1 })
            {
                CompareSetEncoderConfig(Concat(size, Kv("auto_tiles", autoTiles, "row_mt", rowMt)));
                CompareSetEncoderConfig(Concat(size, Kv("auto_tiles", autoTiles, "row_mt", rowMt,
                                                        "tile_columns", 2, "tile_rows", 1)));
            }
        }
        foreach (int endUsage in new[] { 0, 1, 2, 3 })
            CompareSetEncoderConfig(Concat(size, Kv("rc_end_usage", endUsage, "rc_target_bitrate", w * h / 100,
                                                    "auto_tiles", 1)));
        CompareSetEncoderConfig(Concat(size, Kv("g_forced_max_frame_width", w * 2, "g_forced_max_frame_height", h * 2)));
        CompareSetEncoderConfig(Concat(size, Kv("rc_resize_mode", 1, "rc_resize_denominator", 12,
                                                "rc_superres_mode", 1, "enable_superres", 1)));
    }

    public static IEnumerable<object[]> SetEncoderConfigCases()
    {
        object[][] cases =
        {
            new object[] { },
            new object[] { "rc_end_usage", 1, "rc_target_bitrate", 0 },
            new object[] { "rc_target_bitrate", 2000000 },
            new object[] { "rc_end_usage", 3, "use_fixed_qp_offsets", 2, "cq_level", 40 },
            new object[] { "rc_end_usage", 2, "cq_level", 20, "rc_min_quantizer", 4, "rc_max_quantizer", 50 },
            new object[] { "lossless", 1, "aq_mode", 0 },
            new object[] { "rc_min_quantizer", 0, "rc_max_quantizer", 0 },
            new object[] { "aq_mode", 3, "deltaq_mode", 0, "enable_cdef", 2, "loopfilter_control", 2 },
            new object[] { "aq_mode", 1, "deltaq_mode", 1, "deltaq_strength", 400 },
            new object[] { "g_error_resilient", 1 },
            new object[] { "error_resilient_mode", 1 },
            new object[] { "g_lag_in_frames", 0, "kf_mode", 0 },
            new object[] { "kf_mode", 1, "kf_min_dist", 30, "kf_max_dist", 30 },
            new object[] { "kf_max_dist", 0 },
            new object[] { "kf_min_dist", 99, "kf_max_dist", 9999, "fwd_kf_enabled", 1, "fwd_kf_dist", 50 },
            new object[] { "sframe_dist", 30, "sframe_mode", 2 },
            new object[] { "s_frame_mode", 1 },
            new object[] { "rc_dropframe_thresh", 30, "rc_buf_sz", 1000, "rc_buf_initial_sz", 600, "rc_buf_optimal_sz", 600 },
            new object[] { "rc_undershoot_pct", 50, "rc_overshoot_pct", 50, "rc_max_intra_bitrate_pct", 300, "rc_max_inter_bitrate_pct", 100, "gf_cbr_boost_pct", 25 },
            new object[] { "rc_resize_mode", 1, "rc_resize_denominator", 16, "rc_resize_kf_denominator", 9 },
            new object[] { "rc_resize_mode", 3 },
            new object[] { "rc_superres_mode", 1, "rc_superres_denominator", 16, "rc_superres_kf_denominator", 10 },
            new object[] { "rc_superres_mode", 2, "rc_superres_qthresh", 40, "rc_superres_kf_qthresh", 30 },
            new object[] { "rc_superres_mode", 4 },
            new object[] { "enable_superres", 0, "rc_superres_mode", 1 },
            new object[] { "g_timebase.num", 1, "g_timebase.den", 1000000000 },
            new object[] { "g_timebase.num", 1001, "g_timebase.den", 30000 },
            new object[] { "g_timebase.num", 1000000000, "g_timebase.den", 1 },
            new object[] { "timing_info_type", 1 },
            new object[] { "timing_info_type", 2 },
            new object[] { "g_limit", 10, "g_profile", 2, "g_bit_depth", 10, "g_input_bit_depth", 8 },
            new object[] { "monochrome", 1, "full_still_picture_hdr", 1 },
            new object[] { "chroma_subsampling_x", 0, "chroma_subsampling_y", 0, "g_profile", 1 },
            new object[] { "color_primaries", 9, "transfer_characteristics", 16, "matrix_coefficients", 9, "color_range", 1, "chroma_sample_position", 1 },
            new object[] { "tile_width_count", 2, "tile_widths[0]", 3, "tile_widths[1]", 5, "tile_height_count", 1, "tile_heights[0]", 4 },
            new object[] { "large_scale_tile", 1, "single_tile_decoding", 1, "ext_tile_debug", 1, "aq_mode", 0 },
            new object[] { "num_tg", 4, "mtu_size", 1200 },
            new object[] { "superblock_size", 1 },
            new object[] { "superblock_size", 2 },
            new object[] { "min_partition_size", 8, "max_partition_size", 64 },
            new object[] { "cpu_used", 5 },
            new object[] { "cpu_used", 12 },
            // "content" = screen (AOM_CONTENT_SCREEN) is not in the subset: the port throws NotSupportedException.
            new object[] { "content", 2, "screen_detection_mode", 2 },
            new object[] { "noise_sensitivity", 1, "noise_block_size", 16, "enable_dnl_denoising", 0 },
            new object[] { "enable_order_hint", 0, "enable_ref_frame_mvs", 0, "allow_ref_frame_mvs", 0, "enable_dist_wtd_comp", 0 },
            new object[] { "enable_global_motion", 1, "enable_warped_motion", 1, "allow_warped_motion", 1, "enable_obmc", 1 },
            new object[] { "enable_tx64", 0, "enable_flip_idtx", 0, "enable_rect_tx", 0, "reduced_tx_type_set", 1, "use_intra_dct_only", 1, "use_inter_dct_only", 1, "use_intra_default_tx_only", 1, "enable_tx_size_search", 0 },
            new object[] { "enable_rect_partitions", 0, "enable_ab_partitions", 0, "enable_1to4_partitions", 0 },
            new object[] { "enable_angle_delta", 0, "enable_intra_edge_filter", 0, "enable_filter_intra", 0, "enable_smooth_intra", 0, "enable_paeth_intra", 0, "enable_cfl_intra", 0, "enable_directional_intra", 0, "enable_diagonal_intra", 0 },
            new object[] { "enable_palette", 1, "enable_intrabc", 1 },
            new object[] { "enable_masked_comp", 0, "enable_interintra_comp", 0, "enable_smooth_interintra", 0, "enable_diff_wtd_comp", 0, "enable_interinter_wedge", 0, "enable_interintra_wedge", 0 },
            new object[] { "enable_restoration", 1, "enable_cdef", 0, "enable_dual_filter", 0 },
            new object[] { "max_reference_frames", 3, "enable_reduced_reference_set", 1, "enable_onesided_comp", 0 },
            new object[] { "enable_qm", 1, "qm_min", 2, "qm_max", 12, "quant_b_adapt", 1, "enable_chroma_deltaq", 1 },
            new object[] { "coeff_cost_upd_freq", 3, "mode_cost_upd_freq", 2, "mv_cost_upd_freq", 1, "dv_cost_upd_freq", 0 },
            new object[] { "deltalf_mode", 1, "deltaq_mode", 1, "enable_tpl_model", 1 },
            new object[] { "sharpness", 7, "enable_adaptive_sharpness", 1, "disable_trellis_quant", 2 },
            new object[] { "enable_overlay", 1, "arnr_max_frames", 7, "arnr_strength", 5, "cdf_update_mode", 2, "enable_auto_alt_ref", 1, "enable_auto_bwd_ref", 2 },
            new object[] { "min_gf_interval", 4, "max_gf_interval", 16, "gf_min_pyr_height", 1, "gf_max_pyr_height", 3, "kf_max_pyr_height", 2 },
            new object[] { "tuning", 1, "dist_metric", 1 },
            new object[] { "film_grain_test_vector", 3 },
            new object[] { "target_seq_level_idx[0]", 8, "target_seq_level_idx[1]", 31, "tier_mask", 1, "strict_level_conformance", 1 },
            new object[] { "target_seq_level_idx[0]", 32 },
            new object[] { "render_width", 1280, "render_height", 720 },
            new object[] { "min_cr", 100, "frame_parallel_decoding_mode", 1, "force_video_mode", 1, "force_max_q", 1 },
            new object[] { "enable_low_complexity_decode", 1, "skip_postproc_filtering", 1, "mode_ref_delta_enabled", 1 },
            new object[] { "motion_vector_unit_test", 1, "sb_multipass_unit_test", 1, "fp_mt", 1 },
            new object[] { "save_as_annexb", 1 },
            new object[] { "g_pass", 1 },
            new object[] { "sb_qp_sweep", 1, "enable_rate_guide_deltaq", 1, "auto_intra_tools_off", 1 },
            new object[] { "enable_keyframe_filtering", 0, "vbr_corpus_complexity_lap", 0, "frame_periodic_boost", 1 },
            new object[] { "static_thresh", 1000, "row_mt", 0 },
        };
        foreach (object[] c in cases) yield return new object[] { Kv(c) };
    }

    [Theory]
    [MemberData(nameof(SetEncoderConfigCases))]
    public void SetEncoderConfigMatchesLibaom(long[] kv)
    {
        Assert.Equal(0, G1Shim.ValidateConfig(kv).res);
        CompareSetEncoderConfig(kv);
    }

    // Random combinations of (field, value) pairs that libaom accepts on their own; every
    // combination libaom still accepts is run through set_encoder_config.
    [Fact]
    public void SetEncoderConfigRandomCombinations()
    {
        var accepted = new List<(long id, long v)>();
        foreach (long id in AllSetIds())
        {
            // A second pass makes set_encoder_config read two-pass stats: [subset].
            if (Name(id) == "g_pass") continue;
            foreach (long v in Interesting)
                if (G1Shim.ValidateConfig(new[] { id, v }).res == 0) accepted.Add((id, v));
        }

        var rng = new Random(1234);
        var failures = new List<string>();
        int compared = 0;
        for (int iter = 0; iter < 3000; iter++)
        {
            int count = 1 + rng.Next(12);
            var kv = new List<long>();
            for (int i = 0; i < count; i++)
            {
                var (id, v) = accepted[rng.Next(accepted.Count)];
                kv.Add(id);
                kv.Add(v);
            }
            long[] arr = kv.ToArray();
            CompareValidate(arr, failures);
            if (G1Shim.ValidateConfig(arr).res != 0) continue;
            compared++;
            CompareSetEncoderConfig(arr);
        }
        AssertNoFailures(failures);
        Assert.True(compared > 500, $"only {compared} accepted combinations");
    }

    // av1_get_encoder_config returns set_encoder_config's output for the default extra config.
    // Its local AV1EncoderConfig is not zeroed in C, so only the fields set_encoder_config
    // always writes are compared.
    [Fact]
    public void GetEncoderConfigMatchesLibaom()
    {
        var notAlwaysWritten = new HashSet<string>
        {
            // Written only for AOM_TIMING_EQUAL / AOM_TIMING_DEC_MODEL.
            "dec_model_cfg.timing_info.num_units_in_display_tick", "dec_model_cfg.timing_info.time_scale",
            "dec_model_cfg.timing_info.equal_picture_interval", "dec_model_cfg.timing_info.num_ticks_per_picture",
            "dec_model_cfg.num_units_in_decoding_tick", "dec_model_cfg.decoder_model_info_present_flag",
            "dec_model_cfg.display_model_info_present_flag",
            // Written only when extra_cfg->two_pass_output is set.
            "two_pass_output",
        };
        // Written only below tile_width_count / tile_height_count.
        for (int i = 0; i < 4; i++)
        {
            notAlwaysWritten.Add($"tile_cfg.tile_widths[{i}]");
            notAlwaysWritten.Add($"tile_cfg.tile_heights[{i}]");
        }
        foreach (var (w, h) in Resolutions)
        {
            foreach (int t in new[] { 1, 4 })
            {
                long[] kv = Kv("g_w", w, "g_h", h, "g_threads", t, "rc_end_usage", t == 1 ? 1 : 0);
                var expected = new long[G1Fields.OxcfDumpCount];
                int n = G1Shim.aomshim_g1_get_encoder_config(kv, kv.Length / 2, expected);

                var (cfg, _) = MakeConfig(kv);
                AV1EncoderConfig oxcf = av1_get_encoder_config(cfg);
                var actual = new long[G1Fields.OxcfDumpCount];
                int m = G1Fields.DumpOxcf(oxcf, actual);
                for (int i = 0; i < G1Fields.OxcfDumpCount; i++)
                {
                    if (notAlwaysWritten.Contains(G1Fields.OxcfDumpNames[i])) expected[i] = actual[i];
                }
                AssertDumpEqual("av1_get_encoder_config", G1Fields.OxcfDumpNames, expected, n, actual, m, Describe(kv));
            }
        }
    }

    // ---- init_seq_coding_tools and the rest of the sequence header setup ----

    // The SequenceHeader fields init_seq_coding_tools writes (with set_max_frame_size,
    // set_bitstream_level_tier and av1_set_svc_seq_params), plus its inputs profile and tier[].
    private static readonly HashSet<string> InitSeqCodingToolsFields = new(
        new[]
        {
            "profile", "still_picture", "reduced_still_picture_hdr", "force_screen_content_tools",
            "force_integer_mv", "order_hint_info.enable_order_hint", "order_hint_info.order_hint_bits_minus_1",
            "order_hint_info.enable_dist_wtd_comp", "order_hint_info.enable_ref_frame_mvs",
            "frame_id_numbers_present_flag", "num_bits_width", "num_bits_height", "max_frame_width",
            "max_frame_height", "frame_id_length", "delta_frame_id_length", "enable_dual_filter",
            "enable_superres", "enable_cdef", "enable_restoration", "enable_warped_motion",
            "enable_interintra_compound", "enable_masked_compound", "enable_intra_edge_filter",
            "enable_filter_intra", "operating_points_cnt_minus_1", "operating_point_idc[0]",
            "has_nonzero_operating_point_idc",
        }
        .Concat(Enumerable.Range(0, 32).Select(i => $"seq_level_idx[{i}]"))
        .Concat(Enumerable.Range(0, 32).Select(i => $"tier[{i}]"))
        .Concat(Enumerable.Range(0, 2).SelectMany(i => new[] { $"op_params[{i}].bitrate", $"op_params[{i}].buffer_size" })));

    // init_seq_coding_tools (file-static in encoder.c) on a fresh AV1_PRIMARY, with the inputs
    // av1_change_config_seq sets before calling it for a new realtime encoder (profile, tier[]
    // from tier_mask, one spatial / temporal layer, disable_frame_id_numbers = 0). libaom's
    // values come from the full av1_create_primary_compressor path.
    private static void CompareInitSeqCodingTools(long[] kv)
    {
        var expected = new long[G1Fields.SeqDumpCount];
        int n = G1Shim.aomshim_g1_seq_params(kv, kv.Length / 2, expected);

        AV1EncoderConfig oxcf = SetEncoderConfig(kv, out _);
        var ppi = new AV1_PRIMARY();
        ppi.number_spatial_layers = 1;
        ppi.number_temporal_layers = 1;
        ppi.seq_params.profile = oxcf.profile;
        for (int i = 0; i < ppi.seq_params.tier.Length; i++) ppi.seq_params.tier[i] = (byte)((oxcf.tier_mask >> i) & 1);
        ppi.seq_params.operating_points_cnt_minus_1 = 0;
        AomException error = null;
        try
        {
            InitSeqCodingTools(ppi, oxcf, 0);
        }
        catch (AomException e)
        {
            error = e;
        }
        if (n < 0)
        {
            // set_bitstream_level_tier: "AV1 does not support this combination of profile, level, and tier."
            Assert.True(error != null && error.ErrorCode == AOM_CODEC_UNSUP_BITSTREAM,
                        $"libaom failed to create the primary compressor for {Describe(kv)}, the port did not");
            return;
        }
        Assert.True(error == null, $"init_seq_coding_tools failed for {Describe(kv)}: {error?.Message}");
        var actual = new long[G1Fields.SeqDumpCount];
        int m = G1Fields.DumpSeq(ppi.seq_params, actual);
        for (int i = 0; i < G1Fields.SeqDumpCount; i++)
        {
            if (!InitSeqCodingToolsFields.Contains(G1Fields.SeqDumpNames[i])) expected[i] = actual[i];
        }
        AssertDumpEqual("SequenceHeader (init_seq_coding_tools)", G1Fields.SeqDumpNames, expected, n, actual, m, Describe(kv));
    }

    // av1_create_primary_compressor -> init_config_sequence -> av1_change_config_seq calls
    // av1_select_sb_size (encoder_utils.c, package f4). While that is still a placeholder in
    // src/Av1rt/Aom/_stubs, the full comparison stops there and is logged as pending; the
    // init_seq_coding_tools part is compared directly either way.
    private void CompareSeqParams(long[] kv)
    {
        CompareInitSeqCodingTools(kv);

        var expected = new long[G1Fields.SeqDumpCount];
        int n = G1Shim.aomshim_g1_seq_params(kv, kv.Length / 2, expected);

        var (cfg, _) = MakeConfig(kv);
        AV1EncoderConfig oxcf = SetEncoderConfig(kv, out _);
        av1_initialize_enc(cfg.g_usage, cfg.rc_end_usage);
        var pktList = new aom_codec_pkt_list_decl(256);
        AV1_PRIMARY ppi;
        try
        {
            ppi = av1_create_primary_compressor(pktList.head, 0, oxcf);
        }
        catch (NotImplementedException e) when (PendingStub.Is(e))
        {
            output.WriteLine($"pending stub {PendingStub.Name(e)}: {Describe(kv)}");
            return;
        }
        if (n < 0)
        {
            // libaom's setjmp handler returned NULL (aom_internal_error during setup).
            Assert.True(ppi == null, $"libaom failed to create the primary compressor for {Describe(kv)}, the port did not");
            return;
        }
        Assert.True(ppi != null, $"the port failed to create the primary compressor for {Describe(kv)}");
        var actual = new long[G1Fields.SeqDumpCount];
        int m = G1Fields.DumpSeq(ppi.seq_params, actual);
        av1_remove_primary_compressor(ppi);
        AssertDumpEqual("SequenceHeader", G1Fields.SeqDumpNames, expected, n, actual, m, Describe(kv));
    }

    [Theory]
    [MemberData(nameof(ResolutionThreadCases))]
    public void SeqParamsResolutionsAndThreads(int w, int h, int threads)
    {
        long[] size = Kv("g_w", w, "g_h", h, "g_threads", threads);
        CompareSeqParams(size);
        foreach (int sb in new[] { 0, 1, 2 })
            foreach (int speed in new[] { 7, 10 })
                CompareSeqParams(Concat(size, Kv("superblock_size", sb, "cpu_used", speed, "row_mt", 1)));
        CompareSeqParams(Concat(size, Kv("g_forced_max_frame_width", Math.Min(w * 2, 65536),
                                         "g_forced_max_frame_height", Math.Min(h * 2, 65536) / 2)));
        CompareSeqParams(Concat(size, Kv("rc_resize_mode", 3, "rc_end_usage", 1)));
    }

    public static IEnumerable<object[]> SeqParamsCases()
    {
        object[][] cases =
        {
            new object[] { },
            new object[] { "enable_order_hint", 0 },
            new object[] { "enable_order_hint", 1, "enable_ref_frame_mvs", 0, "enable_dist_wtd_comp", 1 },
            new object[] { "enable_cdef", 0, "enable_restoration", 1 },
            new object[] { "enable_cdef", 3 },
            new object[] { "monochrome", 1 },
            new object[] { "g_profile", 1, "chroma_subsampling_x", 0, "chroma_subsampling_y", 0 },
            new object[] { "g_profile", 2, "g_bit_depth", 10, "g_input_bit_depth", 8 },
            // timing_info_type (AV1E_SET_TIMING_INFO_TYPE, decoder model) is not supported: [subset].
            new object[] { "g_error_resilient", 1 },
            new object[] { "full_still_picture_hdr", 1 },
            new object[] { "enable_superres", 1, "rc_superres_mode", 1 },
            new object[] { "enable_superres", 0 },
            new object[] { "film_grain_test_vector", 1 },
            new object[] { "target_seq_level_idx[0]", 8, "tier_mask", 1 },
            new object[] { "target_seq_level_idx[0]", 31 },
            new object[] { "target_seq_level_idx[0]", 32 },
            new object[] { "color_primaries", 1, "transfer_characteristics", 13, "matrix_coefficients", 1, "color_range", 1, "chroma_sample_position", 2 },
            new object[] { "matrix_coefficients", 0, "chroma_subsampling_x", 0, "chroma_subsampling_y", 0, "g_profile", 1 },
            new object[] { "enable_dual_filter", 0, "enable_warped_motion", 0, "enable_filter_intra", 0, "enable_intra_edge_filter", 0 },
            new object[] { "enable_interintra_comp", 0, "enable_masked_comp", 0, "enable_global_motion", 1 },
            new object[] { "enable_tx64", 0, "enable_palette", 1, "enable_intrabc", 1 },
            // "content" = screen (AOM_CONTENT_SCREEN) is not in the subset: the port throws NotSupportedException.
            new object[] { "kf_max_dist", 0 },
            new object[] { "lossless", 1, "aq_mode", 0 },
            new object[] { "rc_end_usage", 3, "cq_level", 30 },
            new object[] { "large_scale_tile", 1, "aq_mode", 0 },
            new object[] { "save_as_annexb", 1 },
            new object[] { "enable_qm", 1, "deltaq_mode", 1, "deltalf_mode", 1 },
            new object[] { "g_lag_in_frames", 0, "g_usage", 1 },
        };
        foreach (object[] c in cases) yield return new object[] { Kv(c) };
    }

    [Theory]
    [MemberData(nameof(SeqParamsCases))]
    public void SeqParamsMatchLibaom(long[] kv)
    {
        Assert.Equal(0, G1Shim.ValidateConfig(kv).res);
        CompareSeqParams(kv);
    }
}
