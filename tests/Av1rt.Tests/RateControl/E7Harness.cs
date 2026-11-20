using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.aq_cyclicrefresh;
using static Av1rt.Aom.ratectrl;

namespace Av1rt.Tests.RateControl;

/// <summary>Builds C# encoder contexts from an <see cref="E7State"/> the same way shim_e7.c
/// does for libaom, generates random (but in-subset) states, and compares states.</summary>
internal static class E7Harness
{
    internal const int KEY_FRAME = 0;
    internal const int INTER_FRAME = 1;
    internal const int BLOCK_64X64 = 12;
    internal const int BLOCK_128X128 = 15;
    internal const int AOM_BITS_8 = 8;
    internal const int AOM_CBR = 1;
    internal const int REALTIME = 1;
    internal const int CYCLIC_REFRESH_AQ = 3;

    /// <summary>Mirror of shim_e7.c setup(): a fresh context holding the state.</summary>
    internal static AV1_COMP NewCpi(in E7State s)
    {
        av1_rc_init_minq_luts(); // av1_create_compressor() does this once
        var cpi = new AV1_COMP();
        cpi.ppi = new AV1_PRIMARY();
        cpi.common.seq_params = cpi.ppi.seq_params;
        cpi.common.seq_params.bit_depth = AOM_BITS_8;
        cpi.oxcf.tool_cfg.bit_depth = AOM_BITS_8;
        cpi.cyclic_refresh = av1_cyclic_refresh_alloc(1, 1);
        s.Load(cpi);
        cpi.common.prev_frame = s.x_has_prev_frame != 0
            ? new RefCntBuffer { width = (int)s.x_prev_frame_width, height = (int)s.x_prev_frame_height }
            : null;
        cpi.ppi.fn_ptr[BLOCK_64X64].sdf = Sad64x64;
        // What save_all_coding_context() leaves for restore_all_coding_context().
        cpi.coding_context.rc.CopyFrom(cpi.rc);
        cpi.coding_context.frame_number = cpi.common.current_frame.frame_number;
        return cpi;
    }

    /// <summary>Runs <paramref name="f"/> on a context built from <paramref name="s"/> and
    /// stores the context back into <paramref name="s"/>.</summary>
    internal static T Run<T>(ref E7State s, Func<AV1_COMP, T> f)
    {
        AV1_COMP cpi = NewCpi(s);
        T r = f(cpi);
        s.Store(cpi);
        return r;
    }

    internal static void Run(ref E7State s, Action<AV1_COMP> f)
    {
        AV1_COMP cpi = NewCpi(s);
        f(cpi);
        s.Store(cpi);
    }

    // aom_sad64x64_c: the C# sad port belongs to another package; the shim uses the C one.
    internal static uint Sad64x64(Ptr<byte> a, int a_stride, Ptr<byte> b, int b_stride)
    {
        uint sad = 0;
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                sad += (uint)Math.Abs(a[y * a_stride + x] - b[y * b_stride + x]);
        return sad;
    }

    private static readonly FieldInfo[] s_fields = Array.ConvertAll(
        E7State.FieldNames, n => typeof(E7State).GetField(n)!);

    /// <summary>Asserts that every field matches (doubles bit-exactly).</summary>
    internal static void AssertSame(in E7State expected, in E7State actual, string context)
    {
        object e = expected, a = actual;
        foreach (FieldInfo f in s_fields)
        {
            object ev = f.GetValue(e)!, av = f.GetValue(a)!;
            bool same = ev is double ed
                ? BitConverter.DoubleToInt64Bits(ed) == BitConverter.DoubleToInt64Bits((double)av)
                : ev.Equals(av);
            if (!same)
                Assert.Fail($"{context}: {f.Name} libaom={ev} port={av}");
        }
    }

    internal static void AssertLayout()
    {
        Assert.Equal(E7Shim.e7_state_size(), Marshal.SizeOf<E7State>());
    }

    private static readonly (int w, int h)[] s_sizes =
    {
        (176, 144), (320, 240), (352, 288), (426, 240), (640, 360), (640, 480), (854, 480),
        (1280, 720), (1366, 768), (1920, 1080), (100, 60), (66, 66),
    };

    private static readonly double[] s_framerates = { 5, 7.5, 10, 15, 24, 25, 29.97, 30, 50, 60 };

    private static double LogUniform(Random r, double lo, double hi) =>
        Math.Exp(Math.Log(lo) + r.NextDouble() * (Math.Log(hi) - Math.Log(lo)));

    private static long Pick(Random r, params long[] values) => values[r.Next(values.Length)];

    /// <summary>Sets the frame size and everything derived from it.</summary>
    internal static void SetSize(ref E7State s, int w, int h)
    {
        s.common_width = w;
        s.common_height = h;
        s.common_render_width = w;
        s.common_render_height = h;
        s.common_superres_upscaled_width = w;
        s.common_superres_upscaled_height = h;
        int mi_cols = ((w + 7) & ~7) >> 2;
        int mi_rows = ((h + 7) & ~7) >> 2;
        s.common_mi_params_mi_cols = mi_cols;
        s.common_mi_params_mi_rows = mi_rows;
        s.common_mi_params_MBs = ((mi_rows + 2) >> 2) * ((mi_cols + 2) >> 2);
        s.oxcf_frm_dim_cfg_width = w;
        s.oxcf_frm_dim_cfg_height = h;
        s.rc_prev_coded_width = w;
        s.rc_prev_coded_height = h;
        s.x_prev_frame_width = w;
        s.x_prev_frame_height = h;
    }

    /// <summary>A random encoder state inside the subset (1 layer CBR realtime, 8-bit, no
    /// screen content, no SVC / external RC / active maps / ROI).</summary>
    internal static E7State RandomState(Random r)
    {
        var s = new E7State();
        (int w, int h) = s_sizes[r.Next(s_sizes.Length)];
        SetSize(ref s, w, h);
        // Occasionally a coded size different from the configured one (resize_rate_factor).
        if (r.Next(8) == 0)
        {
            s.oxcf_frm_dim_cfg_width = w * 4 / 3;
            s.oxcf_frm_dim_cfg_height = h * 4 / 3;
        }
        if (r.Next(8) == 0)
        {
            s.common_superres_upscaled_width = w + 8;
        }
        bool big_sb = r.Next(4) == 0;
        s.common_seq_params_sb_size = big_sb ? BLOCK_128X128 : BLOCK_64X64;
        s.common_seq_params_mib_size = big_sb ? 32 : 16;
        s.common_show_frame = r.Next(10) == 0 ? 0 : 1;
        s.common_current_frame_frame_type = r.Next(5) == 0 ? KEY_FRAME : INTER_FRAME;
        s.common_current_frame_frame_number = r.Next(4) == 0 ? r.Next(12) : r.Next(5000);
        s.common_quant_params_base_qindex = r.Next(256);
        s.common_seg_enabled = r.Next(2);

        double framerate = s_framerates[r.Next(s_framerates.Length)];
        s.framerate = framerate;
        s.oxcf_input_cfg_init_framerate = r.Next(4) == 0 ? s_framerates[r.Next(s_framerates.Length)] : framerate;
        long target_bandwidth = (long)LogUniform(r, 20_000, 20_000_000);
        s.oxcf_rc_cfg_target_bandwidth = target_bandwidth;
        s.oxcf_speed = Pick(r, 11, 11, 11, 10, 9, 7);
        s.oxcf_rc_cfg_mode = AOM_CBR;
        s.oxcf_mode = REALTIME;
        s.oxcf_q_cfg_aq_mode = r.Next(5) == 0 ? 0 : CYCLIC_REFRESH_AQ;
        int worst_allowed_q = r.Next(4) == 0 ? 255 : r.Next(100, 256);
        int best_allowed_q = r.Next(3) == 0 ? 0 : r.Next(0, 80);
        s.oxcf_rc_cfg_worst_allowed_q = worst_allowed_q;
        s.oxcf_rc_cfg_best_allowed_q = best_allowed_q;
        s.oxcf_rc_cfg_under_shoot_pct = r.Next(3) == 0 ? r.Next(0, 101) : 50;
        s.oxcf_rc_cfg_over_shoot_pct = r.Next(3) == 0 ? r.Next(0, 101) : 50;
        s.oxcf_rc_cfg_max_intra_bitrate_pct = r.Next(3) == 0 ? r.Next(0, 1000) : 300;
        s.oxcf_rc_cfg_max_inter_bitrate_pct = r.Next(4) == 0 ? r.Next(50, 1000) : 0;
        s.oxcf_rc_cfg_drop_frames_water_mark = r.Next(3) == 0 ? 0 : r.Next(1, 61);
        s.oxcf_rc_cfg_vbrmin_section = r.Next(3) == 0 ? r.Next(0, 100) : 0;
        s.oxcf_rc_cfg_vbrmax_section = r.Next(3) == 0 ? r.Next(100, 3000) : 2000;
        s.oxcf_rc_cfg_max_consec_drop_ms = Pick(r, 0, 250, 250, 1000);
        s.oxcf_kf_cfg_key_freq_max = r.Next(3) == 0 ? r.Next(1, 300) : 9999;
        s.oxcf_kf_cfg_auto_key = r.Next(5) == 0 ? 0 : 1;
        s.oxcf_gf_cfg_min_gf_interval = r.Next(3) == 0 ? r.Next(2, 20) : 0;
        s.oxcf_gf_cfg_max_gf_interval = r.Next(3) == 0 ? r.Next(20, 200) : 0;
        s.oxcf_target_seq_level_idx_0 = r.Next(3) == 0 ? r.Next(0, 28) : 31;

        // sf (speed 9-11 realtime, not screen content).
        s.sf_rt_sf_gf_length_lvl = r.Next(2);
        s.sf_rt_sf_check_scene_detection = r.Next(4) == 0 ? 0 : 1;
        s.sf_rt_sf_overshoot_detection_cbr = r.Next(2);
        s.sf_rt_sf_higher_thresh_scene_detection = r.Next(2);
        s.sf_rt_sf_force_only_last_ref = r.Next(2);
        s.sf_rt_sf_ref_frame_comp_nonrd_1 = r.Next(2);
        s.sf_rt_sf_sad_based_adp_altref_lag = r.Next(5);

        s.svc_number_spatial_layers = 1;
        s.svc_number_temporal_layers = 1;
        s.svc_prev_number_spatial_layers = r.Next(10) == 0 ? 0 : 1;
        s.svc_prev_number_temporal_layers = 1;

        // rc
        int avg_frame_bandwidth = (int)Math.Round(target_bandwidth / framerate);
        s.rc_avg_frame_bandwidth = avg_frame_bandwidth;
        s.rc_prev_avg_frame_bandwidth = r.Next(4) == 0 ? (long)(avg_frame_bandwidth * LogUniform(r, 0.5, 2)) : avg_frame_bandwidth;
        s.rc_min_frame_bandwidth = 200;
        s.rc_max_frame_bandwidth = r.Next(4) == 0 ? avg_frame_bandwidth * 2L : Math.Max(2025000, s.common_mi_params_MBs * 250);
        s.rc_this_frame_target = (long)(avg_frame_bandwidth * LogUniform(r, 0.05, 20));
        s.rc_base_frame_target = s.rc_this_frame_target;
        s.rc_projected_frame_size = (long)(avg_frame_bandwidth * LogUniform(r, 0.05, 20));
        s.rc_sb64_target_rate = r.Next(0, 100000);
        s.rc_frames_since_key = r.Next(4) == 0 ? r.Next(0, 12) : r.Next(0, 1000);
        s.rc_frames_to_key = r.Next(4) == 0 ? 0 : r.Next(0, 9999);
        s.rc_frames_till_gf_update_due = r.Next(3) == 0 ? 0 : r.Next(0, 161);
        s.rc_frames_since_golden = r.Next(0, 200);
        s.rc_min_gf_interval = r.Next(2, 20);
        s.rc_max_gf_interval = r.Next(20, 250);
        s.rc_static_scene_max_gf_interval = 250;
        s.rc_frames_to_fwd_kf = r.Next(0, 100);
        s.rc_is_src_frame_alt_ref = r.Next(10) == 0 ? 1 : 0;
        s.rc_high_source_sad = r.Next(5) == 0 ? 1 : 0;
        s.rc_static_since_last_scene_change = r.Next(2);
        s.rc_last_encoded_size_keyframe = (long)(avg_frame_bandwidth * LogUniform(r, 1, 30));
        s.rc_last_target_size_keyframe = (long)(avg_frame_bandwidth * LogUniform(r, 1, 30));
        s.rc_frames_since_scene_change = r.Next(0, 500);
        s.rc_ni_av_qi = r.Next(256);
        s.rc_ni_tot_qi = r.Next(0, 1_000_000);
        s.rc_decimation_factor = r.Next(4);
        s.rc_decimation_count = r.Next(4);
        s.rc_prev_frame_is_dropped = r.Next(5) == 0 ? 1 : 0;
        s.rc_drop_count_consec = r.Next(0, 12);
        s.rc_max_consec_drop = r.Next(3) == 0 ? 0 : r.Next(1, 30);
        s.rc_force_max_q = r.Next(5) == 0 ? 1 : 0;
        int worst_quality = r.Next(4) == 0 ? 255 : r.Next(100, 256);
        int best_quality = r.Next(3) == 0 ? 0 : r.Next(0, Math.Min(worst_quality, 80));
        s.rc_worst_quality = worst_quality;
        s.rc_best_quality = best_quality;
        s.rc_rc_1_frame = r.Next(-1, 2);
        s.rc_rc_2_frame = r.Next(-1, 2);
        s.rc_q_1_frame = r.Next(6) == 0 ? 0 : r.Next(best_quality, worst_quality + 1);
        s.rc_q_2_frame = r.Next(6) == 0 ? 0 : r.Next(best_quality, worst_quality + 1);
        s.rc_active_worst_quality = r.Next(best_quality, worst_quality + 1);
        s.rc_avg_frame_low_motion = r.Next(3) == 0 ? 0 : r.Next(0, 101);
        s.rc_percent_blocks_with_motion = r.Next(0, 101);
        s.rc_frame_num_last_gf_refresh = r.Next(0, (int)s.common_current_frame_frame_number + 1);
        s.rc_frame_number_encoded = r.Next(4) == 0 ? r.Next(0, 12) : r.Next(0, 5000);
        s.rc_last_frame_low_source_sad = r.Next(0, 5000);
        s.rc_avg_source_sad = r.Next(4) == 0 ? 0 : (long)LogUniform(r, 1, 2_000_000);
        s.rc_prev_avg_source_sad = r.Next(4) == 0 ? 0 : (long)LogUniform(r, 1, 2_000_000);
        s.rc_frame_source_sad = r.Next(4) == 0 ? 0 : (long)LogUniform(r, 1, 2_000_000);
        s.rc_frame_spatial_variance = r.Next(0, 5000);
        s.rc_max_block_source_sad = r.Next(0, 1_000_000);
        if (r.Next(10) == 0)
        {
            s.rc_prev_coded_width = 0;
            s.rc_prev_coded_height = 0;
        }

        // p_rc: buffer model in bits, 600/600/1000 ms by default.
        long optimal = target_bandwidth * Pick(r, 600, 600, 300, 1000) / 1000;
        long maximum = Math.Max(optimal, target_bandwidth * Pick(r, 1000, 1000, 2000, 600) / 1000);
        s.ppi_p_rc_starting_buffer_level = optimal;
        s.ppi_p_rc_optimal_buffer_level = optimal;
        s.ppi_p_rc_maximum_buffer_size = maximum;
        long buffer_level = (long)((r.NextDouble() * 2.4 - 1.2) * maximum);
        s.ppi_p_rc_buffer_level = buffer_level;
        s.ppi_p_rc_bits_off_target = buffer_level;
        s.ppi_p_rc_total_actual_bits = r.Next(0, int.MaxValue);
        s.ppi_p_rc_total_target_bits = r.Next(0, int.MaxValue);
        s.ppi_p_rc_kf_boost = 2300;
        s.ppi_p_rc_gfu_boost = r.Next(0, 3000);
        s.ppi_p_rc_baseline_gf_interval = r.Next(1, 161);
        s.ppi_p_rc_constrained_gf_group = r.Next(2);
        s.ppi_p_rc_this_key_frame_forced = r.Next(5) == 0 ? 1 : 0;
        s.ppi_p_rc_ni_frames = r.Next(0, 5000);
        s.ppi_p_rc_last_kf_qindex = r.Next(256);
        s.ppi_p_rc_avg_frame_qindex_0 = r.Next(256);
        s.ppi_p_rc_avg_frame_qindex_1 = r.Next(256);
        s.ppi_p_rc_last_boosted_qindex = r.Next(256);
        s.ppi_p_rc_last_q_0 = r.Next(256);
        s.ppi_p_rc_last_q_1 = r.Next(256);
        s.ppi_p_rc_rolling_target_bits = avg_frame_bandwidth;
        s.ppi_p_rc_rolling_actual_bits = (long)(avg_frame_bandwidth * LogUniform(r, 0.5, 2));
        s.ppi_p_rc_tot_q = r.NextDouble() * 100000;
        s.ppi_p_rc_avg_q = r.NextDouble() * 400;
        s.ppi_p_rc_rate_correction_factors_0 = LogUniform(r, 0.01, 40);
        s.ppi_p_rc_rate_correction_factors_1 = LogUniform(r, 0.01, 40);
        s.ppi_p_rc_rate_correction_factors_2 = LogUniform(r, 0.01, 40);
        s.ppi_p_rc_rate_correction_factors_3 = LogUniform(r, 0.01, 40);

        // cyclic refresh
        long mi_count = s.common_mi_params_mi_rows * s.common_mi_params_mi_cols;
        s.cyclic_refresh_percent_refresh = r.Next(0, 16);
        s.cyclic_refresh_percent_refresh_adjustment = r.Next(-5, 6);
        s.cyclic_refresh_max_qdelta_perc = Pick(r, 50, 60);
        s.cyclic_refresh_sb_index = r.Next(0, 100);
        s.cyclic_refresh_target_num_seg_blocks = (long)(mi_count * r.NextDouble() * 0.2);
        s.cyclic_refresh_actual_num_seg1_blocks = (long)(mi_count * r.NextDouble() * 0.2);
        s.cyclic_refresh_actual_num_seg2_blocks = (long)(mi_count * r.NextDouble() * 0.1);
        s.cyclic_refresh_rate_boost_fac = Pick(r, 10, 13, 15);
        s.cyclic_refresh_qindex_delta_1 = -r.Next(0, 60);
        s.cyclic_refresh_qindex_delta_2 = -r.Next(0, 90);
        s.cyclic_refresh_apply_cyclic_refresh = r.Next(4) == 0 ? 0 : 1;
        s.cyclic_refresh_skip_over4x4 = 1;
        s.cyclic_refresh_counter_encode_maxq_scene_change = r.Next(0, 400);
        s.cyclic_refresh_use_block_sad_scene_det = r.Next(2);
        s.cyclic_refresh_motion_thresh = Pick(r, 16, 32);
        s.cyclic_refresh_thresh_rate_sb = r.Next(0, int.MaxValue);
        s.cyclic_refresh_thresh_dist_sb = r.Next(0, 1_000_000);
        s.cyclic_refresh_rate_ratio_qdelta = 1.0 + r.NextDouble() * 2.25;
        s.cyclic_refresh_rate_ratio_qdelta_adjustment = r.Next(0, 6) * 0.05;

        s.refresh_frame_golden_frame = r.Next(4) == 0 ? 1 : 0;
        s.refresh_frame_alt_ref_frame = r.Next(8) == 0 ? 1 : 0;
        s.noise_estimate_enabled = r.Next(2);
        s.noise_estimate_level = r.Next(4);
        s.rec_sse = r.Next(4) == 0 ? -1 : r.Next(0, int.MaxValue);

        s.ppi_gf_group_update_type_0 = Pick(r, LF_UPDATE, LF_UPDATE, GF_UPDATE, KF_UPDATE);
        s.ppi_gf_group_frame_type_0 = s.common_current_frame_frame_type;
        s.ppi_gf_group_size = r.Next(1, 161);
        s.ppi_rtc_ref_ref_idx_0 = r.Next(8);
        s.ppi_rtc_ref_ref_idx_1 = r.Next(8);
        s.ppi_rtc_ref_ref_idx_2 = r.Next(8);
        s.ppi_rtc_ref_ref_idx_3 = r.Next(8);
        s.ppi_rtc_ref_ref_idx_4 = r.Next(8);
        s.ppi_rtc_ref_ref_idx_5 = r.Next(8);
        s.ppi_rtc_ref_ref_idx_6 = r.Next(8);
        s.ppi_rtc_ref_refresh_0 = r.Next(2);
        s.ppi_rtc_ref_refresh_3 = r.Next(2);
        s.ppi_rtc_ref_refresh_6 = r.Next(2);
        s.ppi_rtc_ref_gld_idx_1layer = 6;

        s.x_has_prev_frame = r.Next(8) == 0 ? 0 : 1;
        if (r.Next(8) == 0)
        {
            s.x_prev_frame_width = w / 2;
            s.x_prev_frame_height = h / 2;
        }
        return s;
    }

    /// <summary>A plane of <paramref name="w"/> x <paramref name="h"/> pixels padded to whole
    /// 64x64 blocks (the SAD loop reads whole blocks past the right/bottom edge).</summary>
    internal static (byte[] buf, int stride) NewPlane(int w, int h)
    {
        int stride = ((w + 63) & ~63) + 64;
        int rows = ((h + 63) & ~63) + 64;
        return (new byte[stride * rows], stride);
    }
}
