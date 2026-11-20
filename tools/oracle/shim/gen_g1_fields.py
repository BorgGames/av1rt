"""Generates the field lists shared by the g1 (encoder.c / av1_cx_iface.c) differential tests.

The shim (C) and the tests (C#) must dump and set the same fields in the same order, so both
are generated from the tables below.
Outputs:
  * tools/oracle/shim/shim_g1_fields.h      g1_dump_cfg / g1_dump_oxcf / g1_dump_seq and
                                            g1_set_cfg / g1_set_extra
  * tests/Av1rt.Tests/G1/G1Fields.g.cs      the same functions over the C# port's types

Dump entry kinds: v = integer / bool value, d = double (bits), f = float (bits),
s = string / char pointer (1 if set), p = buffer pointer (1 if set).

Run: python tools/oracle/shim/gen_g1_fields.py
"""
import os

here = os.path.dirname(os.path.abspath(__file__))
repo = os.path.normpath(os.path.join(here, "..", "..", ".."))


def rng(fmt, n):
    return [fmt.format(i) for i in range(n)]


# aom_codec_enc_cfg_t: every field (aom_codec_enc_config_default output).
CFG_DUMP = [("v", f) for f in [
    "g_usage", "g_threads", "g_profile", "g_w", "g_h", "g_limit", "g_forced_max_frame_width",
    "g_forced_max_frame_height", "g_bit_depth", "g_input_bit_depth", "g_timebase.num",
    "g_timebase.den", "g_error_resilient", "g_pass", "g_lag_in_frames", "rc_dropframe_thresh",
    "rc_resize_mode", "rc_resize_denominator", "rc_resize_kf_denominator", "rc_superres_mode",
    "rc_superres_denominator", "rc_superres_kf_denominator", "rc_superres_qthresh",
    "rc_superres_kf_qthresh", "rc_end_usage", "rc_twopass_stats_in.sz",
    "rc_firstpass_mb_stats_in.sz", "rc_target_bitrate", "rc_min_quantizer", "rc_max_quantizer",
    "rc_undershoot_pct", "rc_overshoot_pct", "rc_buf_sz", "rc_buf_initial_sz",
    "rc_buf_optimal_sz", "rc_2pass_vbr_bias_pct", "rc_2pass_vbr_minsection_pct",
    "rc_2pass_vbr_maxsection_pct", "fwd_kf_enabled", "kf_mode", "kf_min_dist", "kf_max_dist",
    "sframe_dist", "sframe_mode", "large_scale_tile", "monochrome", "full_still_picture_hdr",
    "save_as_annexb", "tile_width_count", "tile_height_count"]
    + rng("tile_widths[{}]", 64) + rng("tile_heights[{}]", 64)
    + ["use_fixed_qp_offsets"] + rng("fixed_qp_offsets[{}]", 5)
    + ["encoder_cfg." + f for f in (
        "init_by_cfg_file super_block_size max_partition_size min_partition_size "
        "disable_ab_partition_type disable_rect_partition_type disable_1to4_partition_type "
        "disable_flip_idtx disable_cdef disable_lr disable_obmc disable_warp_motion "
        "disable_global_motion disable_dist_wtd_comp disable_diff_wtd_comp "
        "disable_inter_intra_comp disable_masked_comp disable_one_sided_comp disable_palette "
        "disable_intrabc disable_cfl disable_smooth_intra disable_filter_intra "
        "disable_dual_filter disable_intra_angle_delta disable_intra_edge_filter "
        "disable_tx_64x64 disable_smooth_inter_intra disable_inter_inter_wedge "
        "disable_inter_intra_wedge disable_paeth_intra disable_trellis_quant "
        "disable_ref_frame_mv reduced_reference_set reduced_tx_type_set").split()]
] + [("p", "rc_twopass_stats_in.buf"), ("p", "rc_firstpass_mb_stats_in.buf")]

# AV1EncoderConfig: every field set_encoder_config writes.
OXCF_DUMP = [("v", f) for f in [
    "profile", "max_threads", "mode",
    "frm_dim_cfg.width", "frm_dim_cfg.height", "frm_dim_cfg.forced_max_frame_width",
    "frm_dim_cfg.forced_max_frame_height", "frm_dim_cfg.render_width",
    "frm_dim_cfg.render_height",
    "input_cfg.input_bit_depth"]] + [("d", "input_cfg.init_framerate")] + [("v", f) for f in [
    "input_cfg.limit", "input_cfg.chroma_subsampling_x", "input_cfg.chroma_subsampling_y",
    "dec_model_cfg.timing_info_present", "dec_model_cfg.timing_info.num_units_in_display_tick",
    "dec_model_cfg.timing_info.time_scale", "dec_model_cfg.timing_info.equal_picture_interval",
    "dec_model_cfg.timing_info.num_ticks_per_picture", "dec_model_cfg.num_units_in_decoding_tick",
    "dec_model_cfg.decoder_model_info_present_flag",
    "dec_model_cfg.display_model_info_present_flag",
    "pass", "passes",
    "rc_cfg.max_intra_bitrate_pct", "rc_cfg.max_inter_bitrate_pct", "rc_cfg.gf_cbr_boost_pct",
    "rc_cfg.mode", "rc_cfg.min_cr", "rc_cfg.best_allowed_q", "rc_cfg.worst_allowed_q",
    "rc_cfg.cq_level", "rc_cfg.under_shoot_pct", "rc_cfg.over_shoot_pct",
    "rc_cfg.maximum_buffer_size_ms", "rc_cfg.starting_buffer_level_ms",
    "rc_cfg.optimal_buffer_level_ms", "rc_cfg.target_bandwidth", "rc_cfg.drop_frames_water_mark",
    "rc_cfg.vbr_corpus_complexity_lap", "rc_cfg.vbrbias", "rc_cfg.vbrmin_section",
    "rc_cfg.vbrmax_section", "rc_cfg.force_max_q",
    "tool_cfg.bit_depth", "tool_cfg.cdef_control", "tool_cfg.enable_restoration",
    "tool_cfg.force_video_mode", "tool_cfg.enable_palette", "tool_cfg.enable_ref_frame_mvs",
    "tool_cfg.superblock_size", "tool_cfg.enable_monochrome", "tool_cfg.full_still_picture_hdr",
    "tool_cfg.enable_dual_filter", "tool_cfg.enable_order_hint", "tool_cfg.enable_interintra_comp",
    "tool_cfg.ref_frame_mvs_present", "tool_cfg.enable_global_motion",
    "tool_cfg.error_resilient_mode", "tool_cfg.frame_parallel_decoding_mode",
    "tool_cfg.enable_deltalf_mode",
    "q_cfg.using_qm", "q_cfg.qm_minlevel", "q_cfg.qm_maxlevel", "q_cfg.quant_b_adapt",
    "q_cfg.enable_chroma_deltaq", "q_cfg.aq_mode", "q_cfg.deltaq_mode", "q_cfg.deltaq_strength",
    "q_cfg.use_fixed_qp_offsets", "q_cfg.enable_hdr_deltaq",
    "cost_upd_freq.coeff", "cost_upd_freq.mode", "cost_upd_freq.mv", "cost_upd_freq.dv",
    "resize_cfg.resize_mode", "resize_cfg.resize_scale_denominator",
    "resize_cfg.resize_kf_scale_denominator",
    "algo_cfg.enable_overlay", "algo_cfg.disable_trellis_quant", "algo_cfg.sharpness",
    "algo_cfg.enable_adaptive_sharpness", "algo_cfg.arnr_max_frames", "algo_cfg.arnr_strength",
    "algo_cfg.cdf_update_mode", "algo_cfg.enable_tpl_model", "algo_cfg.loopfilter_control",
    "algo_cfg.skip_postproc_filtering", "algo_cfg.mode_ref_delta_enabled",
    "algo_cfg.screen_detection_mode",
    "twopass_stats_in.sz"]] + [("s", "two_pass_output"), ("s", "second_pass_log")] + [
    ("v", f) for f in [
    "kf_cfg.fwd_kf_enabled", "kf_cfg.auto_key", "kf_cfg.key_freq_min", "kf_cfg.key_freq_max",
    "kf_cfg.sframe_dist", "kf_cfg.sframe_mode", "kf_cfg.enable_sframe",
    "kf_cfg.enable_keyframe_filtering", "kf_cfg.fwd_kf_dist", "kf_cfg.enable_intrabc",
    "speed", "enable_low_complexity_decode",
    "color_cfg.color_primaries", "color_cfg.transfer_characteristics",
    "color_cfg.matrix_coefficients", "color_cfg.color_range", "color_cfg.chroma_sample_position",
    "gf_cfg.lag_in_frames", "gf_cfg.enable_auto_arf", "gf_cfg.enable_auto_brf",
    "gf_cfg.min_gf_interval", "gf_cfg.max_gf_interval", "gf_cfg.gf_min_pyr_height",
    "gf_cfg.gf_max_pyr_height",
    "tune_cfg.tuning"]] + [("s", "tune_cfg.vmaf_model_path")] + [("v", f) for f in [
    "tune_cfg.content", "tune_cfg.film_grain_test_vector"]] + [
    ("s", "tune_cfg.film_grain_table_filename"), ("v", "tune_cfg.dist_metric"),
    ("f", "noise_level")] + [("v", f) for f in [
    "noise_block_size", "enable_dnl_denoising",
    "tile_cfg.num_tile_groups", "tile_cfg.mtu", "tile_cfg.enable_large_scale_tile",
    "tile_cfg.enable_single_tile_decoding", "tile_cfg.tile_columns", "tile_cfg.tile_rows",
    "tile_cfg.tile_width_count", "tile_cfg.tile_height_count"]
    + rng("tile_cfg.tile_widths[{}]", 4) + rng("tile_cfg.tile_heights[{}]", 4) + [
    "tile_cfg.enable_ext_tile_debug",
    "ref_frm_cfg.max_reference_frames", "ref_frm_cfg.enable_reduced_reference_set",
    "ref_frm_cfg.enable_onesided_comp",
    "row_mt", "fp_mt",
    "motion_mode_cfg.enable_obmc", "motion_mode_cfg.enable_warped_motion",
    "motion_mode_cfg.allow_warped_motion",
    "part_cfg.enable_rect_partitions", "part_cfg.enable_ab_partitions",
    "part_cfg.enable_1to4_partitions", "part_cfg.min_partition_size",
    "part_cfg.max_partition_size",
    "intra_mode_cfg.enable_angle_delta", "intra_mode_cfg.enable_intra_edge_filter",
    "intra_mode_cfg.enable_filter_intra", "intra_mode_cfg.enable_smooth_intra",
    "intra_mode_cfg.enable_paeth_intra", "intra_mode_cfg.enable_cfl_intra",
    "intra_mode_cfg.enable_directional_intra", "intra_mode_cfg.enable_diagonal_intra",
    "intra_mode_cfg.auto_intra_tools_off",
    "txfm_cfg.enable_tx64", "txfm_cfg.enable_flip_idtx", "txfm_cfg.enable_rect_tx",
    "txfm_cfg.reduced_tx_type_set", "txfm_cfg.use_intra_dct_only", "txfm_cfg.use_inter_dct_only",
    "txfm_cfg.use_intra_default_tx_only", "txfm_cfg.enable_tx_size_search",
    "comp_type_cfg.enable_dist_wtd_comp", "comp_type_cfg.enable_masked_comp",
    "comp_type_cfg.enable_diff_wtd_comp", "comp_type_cfg.enable_interinter_wedge",
    "comp_type_cfg.enable_smooth_interintra", "comp_type_cfg.enable_interintra_wedge",
    "superres_cfg.superres_mode", "superres_cfg.superres_scale_denominator",
    "superres_cfg.superres_kf_scale_denominator", "superres_cfg.superres_qthresh",
    "superres_cfg.superres_kf_qthresh", "superres_cfg.enable_superres",
    "save_as_annexb", "unit_test_cfg.motion_vector_unit_test",
    "unit_test_cfg.sb_multipass_unit_test", "border_in_pixels"]
    + rng("target_seq_level_idx[{}]", 32) + ["tier_mask"]] + [
    ("s", "partition_info_path"), ("v", "enable_rate_guide_deltaq"),
    ("s", "rate_distribution_info")] + [("v", f) for f in [
    "strict_level_conformance", "kf_max_pyr_height", "sb_qp_sweep"]]

# SequenceHeader after av1_create_primary_compressor (init_config_sequence ->
# av1_change_config_seq -> init_seq_coding_tools, set_bitstream_level_tier, ...).
SEQ_DUMP = [("v", f) for f in [
    "profile", "still_picture", "reduced_still_picture_hdr", "force_screen_content_tools",
    "force_integer_mv", "order_hint_info.enable_order_hint",
    "order_hint_info.order_hint_bits_minus_1", "order_hint_info.enable_dist_wtd_comp",
    "order_hint_info.enable_ref_frame_mvs", "enable_dual_filter", "enable_warped_motion",
    "enable_superres", "enable_cdef", "enable_restoration", "enable_interintra_compound",
    "enable_masked_compound", "enable_intra_edge_filter", "enable_filter_intra", "sb_size",
    "mib_size", "mib_size_log2", "num_bits_width", "num_bits_height", "max_frame_width",
    "max_frame_height", "frame_id_numbers_present_flag", "frame_id_length",
    "delta_frame_id_length", "bit_depth", "use_highbitdepth", "monochrome", "color_primaries",
    "transfer_characteristics", "matrix_coefficients", "color_range", "subsampling_x",
    "subsampling_y", "chroma_sample_position", "separate_uv_delta_q",
    "film_grain_params_present", "operating_points_cnt_minus_1"]
    + rng("operating_point_idc[{}]", 4) + [
    "has_nonzero_operating_point_idc", "timing_info_present",
    "timing_info.num_units_in_display_tick", "timing_info.time_scale",
    "timing_info.equal_picture_interval", "timing_info.num_ticks_per_picture",
    "decoder_model_info_present_flag", "decoder_model_info.num_units_in_decoding_tick",
    "decoder_model_info.encoder_decoder_buffer_delay_length",
    "decoder_model_info.buffer_removal_time_length",
    "decoder_model_info.frame_presentation_time_length",
    "display_model_info_present_flag"]
    + rng("seq_level_idx[{}]", 32) + rng("tier[{}]", 32)
    + [f"op_params[{i}].{m}" for i in range(2) for m in (
        "decoder_model_param_present_flag", "bitrate", "buffer_size", "decoder_buffer_delay",
        "encoder_buffer_delay", "low_delay_mode_flag", "display_model_param_present_flag",
        "initial_display_delay")]]

# Settable aom_codec_enc_cfg_t fields (id = index) with the C# type of the field.
CFG_SET = [
    ("g_w", "uint"), ("g_h", "uint"), ("g_threads", "uint"), ("g_profile", "uint"),
    ("g_timebase.num", "int"), ("g_timebase.den", "int"), ("g_error_resilient", "uint"),
    ("g_lag_in_frames", "uint"), ("rc_dropframe_thresh", "uint"), ("rc_resize_mode", "uint"),
    ("rc_resize_denominator", "uint"), ("rc_resize_kf_denominator", "uint"),
    ("rc_superres_mode", "int"), ("rc_superres_denominator", "uint"),
    ("rc_superres_kf_denominator", "uint"), ("rc_superres_qthresh", "uint"),
    ("rc_superres_kf_qthresh", "uint"), ("rc_end_usage", "int"), ("rc_target_bitrate", "uint"),
    ("rc_min_quantizer", "uint"), ("rc_max_quantizer", "uint"), ("rc_undershoot_pct", "uint"),
    ("rc_overshoot_pct", "uint"), ("rc_buf_sz", "uint"), ("rc_buf_initial_sz", "uint"),
    ("rc_buf_optimal_sz", "uint"), ("rc_2pass_vbr_bias_pct", "uint"), ("kf_mode", "int"),
    ("kf_min_dist", "uint"), ("kf_max_dist", "uint"), ("monochrome", "uint"),
    ("g_forced_max_frame_width", "uint"), ("g_forced_max_frame_height", "uint"),
    ("g_limit", "uint"), ("g_bit_depth", "int"), ("g_input_bit_depth", "uint"),
    ("large_scale_tile", "uint"), ("use_fixed_qp_offsets", "uint"), ("save_as_annexb", "uint"),
    ("g_usage", "uint"), ("g_pass", "int"), ("fwd_kf_enabled", "int"), ("sframe_dist", "uint"),
    ("sframe_mode", "uint"), ("full_still_picture_hdr", "uint"), ("tile_width_count", "int"),
    ("tile_height_count", "int"), ("tile_widths[0]", "int"), ("tile_widths[1]", "int"),
    ("tile_heights[0]", "int"), ("tile_heights[1]", "int"),
    ("rc_2pass_vbr_minsection_pct", "uint"), ("rc_2pass_vbr_maxsection_pct", "uint"),
]

# Settable struct av1_extracfg fields (id = 1000 + index).
EXTRA_SET = [
    ("cpu_used", "int"), ("noise_sensitivity", "uint"), ("row_mt", "uint"),
    ("tile_columns", "uint"), ("tile_rows", "uint"), ("auto_tiles", "uint"),
    ("enable_tpl_model", "uint"), ("rc_max_intra_bitrate_pct", "uint"), ("enable_cdef", "uint"),
    ("enable_restoration", "uint"), ("enable_obmc", "uint"), ("disable_trellis_quant", "uint"),
    ("enable_qm", "uint"), ("enable_dual_filter", "int"), ("enable_rect_partitions", "int"),
    ("enable_intra_edge_filter", "int"), ("enable_order_hint", "int"), ("enable_tx64", "int"),
    ("enable_dist_wtd_comp", "int"), ("max_reference_frames", "int"),
    ("enable_ref_frame_mvs", "int"), ("allow_ref_frame_mvs", "int"),
    ("enable_masked_comp", "int"), ("enable_interintra_comp", "int"),
    ("enable_smooth_interintra", "int"), ("enable_diff_wtd_comp", "int"),
    ("enable_interinter_wedge", "int"), ("enable_interintra_wedge", "int"),
    ("enable_global_motion", "int"), ("enable_warped_motion", "int"),
    ("allow_warped_motion", "int"), ("enable_filter_intra", "int"),
    ("enable_smooth_intra", "int"), ("enable_paeth_intra", "int"), ("enable_cfl_intra", "int"),
    ("enable_palette", "int"), ("enable_intrabc", "int"), ("enable_angle_delta", "int"),
    ("aq_mode", "byte"), ("use_intra_default_tx_only", "int"), ("coeff_cost_upd_freq", "int"),
    ("mode_cost_upd_freq", "int"), ("mv_cost_upd_freq", "int"), ("dv_cost_upd_freq", "int"),
    ("deltaq_mode", "byte"), ("superblock_size", "int"), ("lossless", "uint"),
    ("timing_info_type", "int"), ("error_resilient_mode", "int"), ("content", "int"),
    ("color_primaries", "int"), ("transfer_characteristics", "int"),
    ("matrix_coefficients", "int"), ("color_range", "int"), ("chroma_subsampling_x", "uint"),
    ("chroma_subsampling_y", "uint"), ("passes", "int"), ("kf_max_pyr_height", "int"),
    ("gf_min_pyr_height", "uint"), ("gf_max_pyr_height", "uint"), ("min_gf_interval", "uint"),
    ("max_gf_interval", "uint"), ("min_partition_size", "int"), ("max_partition_size", "int"),
    ("tuning", "int"), ("dist_metric", "int"), ("screen_detection_mode", "int"),
    ("enable_superres", "int"), ("deltalf_mode", "int"), ("cq_level", "uint"),
    ("sharpness", "uint"), ("mtu_size", "uint"), ("num_tg", "uint"),
    ("enable_low_complexity_decode", "uint"), ("enable_chroma_deltaq", "uint"),
    ("target_seq_level_idx[0]", "byte"), ("target_seq_level_idx[1]", "byte"),
    ("vbr_corpus_complexity_lap", "uint"), ("sb_qp_sweep", "int"),
    ("strict_level_conformance", "int"), ("auto_intra_tools_off", "int"),
    ("skip_postproc_filtering", "uint"), ("mode_ref_delta_enabled", "int"),
    ("loopfilter_control", "int"), ("deltaq_strength", "uint"),
    ("frame_periodic_boost", "uint"), ("single_tile_decoding", "uint"),
    ("film_grain_test_vector", "int"), ("enable_auto_alt_ref", "uint"),
    ("enable_auto_bwd_ref", "uint"), ("arnr_max_frames", "uint"), ("arnr_strength", "uint"),
    ("cdf_update_mode", "uint"), ("motion_vector_unit_test", "uint"),
    ("sb_multipass_unit_test", "uint"), ("ext_tile_debug", "uint"), ("fp_mt", "uint"),
    ("enable_keyframe_filtering", "uint"), ("enable_overlay", "int"),
    ("enable_flip_idtx", "int"), ("enable_rect_tx", "int"), ("reduced_tx_type_set", "int"),
    ("use_intra_dct_only", "int"), ("use_inter_dct_only", "int"),
    ("enable_tx_size_search", "int"), ("quant_b_adapt", "int"),
    ("enable_directional_intra", "int"), ("enable_diagonal_intra", "int"),
    ("enable_ab_partitions", "int"), ("enable_1to4_partitions", "int"),
    ("enable_reduced_reference_set", "int"), ("enable_onesided_comp", "int"),
    ("render_width", "int"), ("render_height", "int"), ("s_frame_mode", "int"),
    ("fwd_kf_dist", "int"), ("tier_mask", "uint"), ("min_cr", "uint"),
    ("rc_max_inter_bitrate_pct", "uint"), ("gf_cbr_boost_pct", "uint"),
    ("frame_parallel_decoding_mode", "uint"), ("enable_adaptive_sharpness", "uint"),
    ("enable_rate_guide_deltaq", "uint"), ("force_video_mode", "uint"), ("force_max_q", "uint"),
    ("chroma_sample_position", "int"), ("qm_min", "uint"), ("qm_max", "uint"),
    ("noise_block_size", "int"), ("enable_dnl_denoising", "int"), ("static_thresh", "uint"),
]

for table in (CFG_SET, EXTRA_SET):
    names = [n for n, _ in table]
    assert len(names) == len(set(names)), "duplicate setter"
for table in (CFG_DUMP, OXCF_DUMP, SEQ_DUMP):
    names = [n for _, n in table]
    assert len(names) == len(set(names)), "duplicate dump field"


def c_dump(func, ctype, table):
    out = [f"static int {func}(const {ctype} *o, int64_t *out) {{", "  int n = 0;"]
    for kind, f in table:
        if kind == "v":
            out.append(f"  out[n++] = (int64_t)(o->{f});")
        elif kind == "d":
            out.append(f"  {{ double v = o->{f}; int64_t b; memcpy(&b, &v, 8); out[n++] = b; }}")
        elif kind == "f":
            out.append(f"  {{ float v = o->{f}; int32_t b; memcpy(&b, &v, 4); out[n++] = b; }}")
        else:
            out.append(f"  out[n++] = o->{f} != NULL;")
    out += ["  return n;", "}", ""]
    return out


def cs_dump(func, cstype, table):
    out = [f"    public static int {func}({cstype} o, long[] output)", "    {", "        int n = 0;"]
    for kind, f in table:
        if kind == "v":
            out.append(f"        output[n++] = L(o.{f});")
        elif kind == "d":
            out.append(f"        output[n++] = BitConverter.DoubleToInt64Bits(o.{f});")
        elif kind == "f":
            out.append(f"        output[n++] = BitConverter.SingleToInt32Bits(o.{f});")
        elif kind == "s":
            out.append(f"        output[n++] = o.{f} != null ? 1 : 0;")
        else:
            out.append(f"        output[n++] = o.{f}.IsNull ? 0 : 1;")
    out += ["        return n;", "    }", ""]
    return out


def c_set(func, ctype, table):
    out = [f"static void {func}({ctype} *o, int id, int64_t v) {{", "  switch (id) {"]
    for i, (f, _) in enumerate(table):
        out.append(f"    case {i}: o->{f} = v; break;")
    out += ["    default: abort();", "  }", "}", ""]
    return out


def cs_set(func, cstype, table, by_ref):
    r = "ref " if by_ref else ""
    out = [f"    public static void {func}({r}{cstype} o, int id, long v)", "    {",
           "        switch (id)", "        {"]
    for i, (f, t) in enumerate(table):
        out.append(f"            case {i}: o.{f} = unchecked(({t})v); break;")
    out += ["            default: throw new ArgumentOutOfRangeException(nameof(id));", "        }",
            "    }", ""]
    return out


h = ["/* Generated by gen_g1_fields.py: do not edit. */",
     "/* Field dumps and setters shared with tests/Av1rt.Tests/G1/G1Fields.g.cs. */",
     "#pragma warning(disable : 4244 4245 4267)", ""]
h += [f"#define G1_CFG_DUMP_COUNT {len(CFG_DUMP)}", f"#define G1_OXCF_DUMP_COUNT {len(OXCF_DUMP)}",
      f"#define G1_SEQ_DUMP_COUNT {len(SEQ_DUMP)}", f"#define G1_CFG_SET_COUNT {len(CFG_SET)}",
      f"#define G1_EXTRA_SET_COUNT {len(EXTRA_SET)}", ""]
h += c_dump("g1_dump_cfg", "aom_codec_enc_cfg_t", CFG_DUMP)
h += c_dump("g1_dump_oxcf", "AV1EncoderConfig", OXCF_DUMP)
h += c_dump("g1_dump_seq", "SequenceHeader", SEQ_DUMP)
h += c_set("g1_set_cfg", "aom_codec_enc_cfg_t", CFG_SET)
h += c_set("g1_set_extra", "struct av1_extracfg", EXTRA_SET)
with open(os.path.join(here, "shim_g1_fields.h"), "w", newline="\n") as f:
    f.write("\n".join(h))

cs = ["// Generated by tools/oracle/shim/gen_g1_fields.py: do not edit.", "using System;",
      "using Av1rt.Aom;", "", "namespace Av1rt.Tests.G1;", "",
      "internal static class G1Fields", "{",
      f"    public const int CfgDumpCount = {len(CFG_DUMP)};",
      f"    public const int OxcfDumpCount = {len(OXCF_DUMP)};",
      f"    public const int SeqDumpCount = {len(SEQ_DUMP)};",
      f"    public const int CfgSetCount = {len(CFG_SET)};",
      f"    public const int ExtraSetCount = {len(EXTRA_SET)};", "",
      "    public static readonly string[] CfgDumpNames = { " + ", ".join(f'"{n}"' for _, n in CFG_DUMP) + " };",
      "    public static readonly string[] OxcfDumpNames = { " + ", ".join(f'"{n}"' for _, n in OXCF_DUMP) + " };",
      "    public static readonly string[] SeqDumpNames = { " + ", ".join(f'"{n}"' for _, n in SEQ_DUMP) + " };",
      "    public static readonly string[] CfgSetNames = { " + ", ".join(f'"{n}"' for n, _ in CFG_SET) + " };",
      "    public static readonly string[] ExtraSetNames = { " + ", ".join(f'"{n}"' for n, _ in EXTRA_SET) + " };",
      "",
      "    private static long L(bool v) => v ? 1 : 0;",
      "    private static long L(long v) => v;",
      "    private static long L(ulong v) => unchecked((long)v);",
      ""]
cs += cs_dump("DumpCfg", "aom_codec_enc_cfg_t", CFG_DUMP)
cs += cs_dump("DumpOxcf", "AV1EncoderConfig", OXCF_DUMP)
cs += cs_dump("DumpSeq", "SequenceHeader", SEQ_DUMP)
cs += cs_set("SetCfg", "aom_codec_enc_cfg_t", CFG_SET, False)
cs += cs_set("SetExtra", "av1_extracfg", EXTRA_SET, True)
cs += ["}", ""]
os.makedirs(os.path.join(repo, "tests", "Av1rt.Tests", "G1"), exist_ok=True)
with open(os.path.join(repo, "tests", "Av1rt.Tests", "G1", "G1Fields.g.cs"), "w", newline="\n") as f:
    f.write("\n".join(cs))
print("cfg", len(CFG_DUMP), "oxcf", len(OXCF_DUMP), "seq", len(SEQ_DUMP), "cfgset", len(CFG_SET),
      "extraset", len(EXTRA_SET))
