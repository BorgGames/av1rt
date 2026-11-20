using System.Runtime.InteropServices;

namespace Av1rt.Tests.RateControl;

/// <summary>libaom's ratectrl.c / aq_cyclicrefresh.c through tools/oracle/shim/shim_e7.c.</summary>
/// <remarks>The shim keeps one static encoder context: callers must not run concurrently
/// (all e7 test classes share the "e7" xunit collection).</remarks>
internal static class E7Shim
{
    private const string Dll = "aomshim_e7";

    [DllImport(Dll)] public static extern int e7_state_size();

    [DllImport(Dll)] public static extern double e7_convert_qindex_to_q(int qindex);
    [DllImport(Dll)] public static extern int e7_convert_q_to_qindex(double q);
    [DllImport(Dll)] public static extern int e7_find_qindex(double desired_q, int best_qindex, int worst_qindex);
    [DllImport(Dll)] public static extern int e7_default_min_gf_interval(int width, int height, double framerate);

    [DllImport(Dll)] public static extern int e7_rc_bits_per_mb(ref E7State s, int frame_type, int qindex, double correction_factor);
    [DllImport(Dll)] public static extern int e7_estimate_bits_at_q(ref E7State s, int q, double correction_factor);
    [DllImport(Dll)] public static extern int e7_compute_qdelta(ref E7State s, double qstart, double qtarget);
    [DllImport(Dll)] public static extern int e7_compute_qdelta_by_rate(ref E7State s, int frame_type, int qindex, double rate_target_ratio);
    [DllImport(Dll)] public static extern int e7_cr_rc_bits_per_mb(ref E7State s, int i, double correction_factor);
    [DllImport(Dll)] public static extern int e7_cr_estimate_bits_at_q(ref E7State s, double correction_factor);

    [DllImport(Dll)] public static extern void e7_rc_bits_per_mb_all(ref E7State s, int frame_type, double correction_factor, int[] out256);
    [DllImport(Dll)] public static extern void e7_estimate_bits_at_q_all(ref E7State s, double correction_factor, int[] out256);
    [DllImport(Dll)] public static extern void e7_compute_qdelta_by_rate_all(ref E7State s, int frame_type, double rate_target_ratio, int[] out256);
    [DllImport(Dll)] public static extern void e7_cr_rc_bits_per_mb_all(ref E7State s, double correction_factor, int[] out256);

    [DllImport(Dll)] public static extern void e7_rc_init(ref E7State s);
    [DllImport(Dll)] public static extern void e7_rc_update_framerate(ref E7State s);
    [DllImport(Dll)] public static extern int e7_calc_pframe_target(ref E7State s, int frame_update_type);
    [DllImport(Dll)] public static extern int e7_calc_iframe_target(ref E7State s);
    [DllImport(Dll)] public static extern int e7_rc_drop_frame(ref E7State s);
    [DllImport(Dll)] public static extern void e7_rc_set_frame_target(ref E7State s, int target);
    [DllImport(Dll)] public static extern int e7_pick_q_and_bounds(ref E7State s, out int bottom_index, out int top_index);
    [DllImport(Dll)] public static extern void e7_update_rate_correction_factors(ref E7State s, int is_encode_stage);
    [DllImport(Dll)] public static extern void e7_postencode_update(ref E7State s, ulong bytes_used);
    [DllImport(Dll)] public static extern void e7_postencode_update_drop_frame(ref E7State s);
    [DllImport(Dll)] public static extern int e7_encodedframe_overshoot_cbr(ref E7State s, ref int q);
    [DllImport(Dll)] public static extern int e7_postencode_drop_cbr(ref E7State s, ref int size);
    [DllImport(Dll)] public static extern void e7_cr_update_parameters(ref E7State s);
    [DllImport(Dll)] public static extern void e7_adjust_gf_refresh_qp(ref E7State s);
    [DllImport(Dll)] public static extern void e7_set_rtc_reference_structure(ref E7State s, int gf_update);

    [DllImport(Dll)]
    public static extern int e7_scene_detection(ref E7State s, byte[] src, byte[] last, int stride,
                                                int w, int h, int last_w, int last_h,
                                                ulong[] blk_out, int blk_cap);

    [DllImport(Dll)]
    public static extern int e7_get_one_pass_rt_params(ref E7State s, uint frame_flags, byte[] src,
                                                       byte[] last, int stride, int w, int h,
                                                       ulong[] blk_out, int blk_cap, out int blk_size);

    [DllImport(Dll)]
    public static extern int e7_cr_update_segment(ref E7State s, int mi_row, int mi_col, int bsize,
                                                  long rate, long dist, int skip, int dry_run,
                                                  int mv_row, int mv_col, int ref0, int ref1,
                                                  int segment_id, byte[] enc_map, byte[] seg_map,
                                                  sbyte[] cr_map, int[] x_counts);

    [DllImport(Dll)]
    public static extern int e7_cr_reset_segment_skip(ref E7State s, int mi_row, int mi_col, int bsize,
                                                      int dry_run, int segment_id, int[] x_counts);
}
