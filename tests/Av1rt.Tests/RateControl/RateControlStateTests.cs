using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.aq_cyclicrefresh;
using static Av1rt.Aom.ratectrl;
using static Av1rt.Tests.RateControl.E7Harness;

namespace Av1rt.Tests.RateControl;

/// <summary>Differential tests of the stateful rate control functions: libaom and the port
/// start from the same random <see cref="E7State"/>; results and the whole resulting state
/// must match.</summary>
[Trait("Category", "Native")]
[Collection("e7")]
public class RateControlStateTests
{
    private const int Iterations = 3000;

    private delegate int ShimCall(ref E7State s);

    private static void Check(int seed, Func<Random, E7State> make, ShimCall shim,
                              Func<AV1_COMP, int> port)
    {
        var r = new Random(seed);
        for (int iter = 0; iter < Iterations; iter++)
        {
            E7State c = make(r);
            E7State p = c;
            int expected = shim(ref c);
            int actual = Run(ref p, port);
            Assert.True(expected == actual, $"iter {iter}: libaom={expected} port={actual}");
            AssertSame(c, p, $"iter {iter}");
        }
    }

    [Fact]
    public void RcInit() =>
        Check(7101, RandomState,
              (ref E7State s) => { E7Shim.e7_rc_init(ref s); return 0; },
              cpi =>
              {
                  av1_primary_rc_init(cpi.oxcf, cpi.ppi.p_rc);
                  av1_rc_init(cpi.oxcf, cpi.rc);
                  return 0;
              });

    [Fact]
    public void RcUpdateFramerate() =>
        Check(7102, RandomState,
              (ref E7State s) => { E7Shim.e7_rc_update_framerate(ref s); return 0; },
              cpi => { av1_rc_update_framerate(cpi, cpi.common.width, cpi.common.height); return 0; });

    [Fact]
    public void CalcPframeTargetSizeOnePassCbr()
    {
        var r = new Random(7103);
        for (int iter = 0; iter < Iterations; iter++)
        {
            E7State c = RandomState(r);
            int update_type = new[] { LF_UPDATE, GF_UPDATE, 4 /* OVERLAY_UPDATE */ }[r.Next(3)];
            if (r.Next(3) == 0) c.oxcf_rc_cfg_gf_cbr_boost_pct = r.Next(0, 400);
            E7State p = c;
            int expected = E7Shim.e7_calc_pframe_target(ref c, update_type);
            int actual = Run(ref p, cpi => av1_calc_pframe_target_size_one_pass_cbr(cpi, update_type));
            Assert.True(expected == actual, $"iter {iter}: libaom={expected} port={actual}");
            AssertSame(c, p, $"iter {iter}");
        }
    }

    [Fact]
    public void CalcIframeTargetSizeOnePassCbr() =>
        Check(7104, RandomState,
              (ref E7State s) => E7Shim.e7_calc_iframe_target(ref s),
              av1_calc_iframe_target_size_one_pass_cbr);

    [Fact]
    public void RcDropFrame() =>
        Check(7105, RandomState,
              (ref E7State s) => E7Shim.e7_rc_drop_frame(ref s),
              av1_rc_drop_frame);

    [Fact]
    public void RcSetFrameTarget()
    {
        var r = new Random(7106);
        for (int iter = 0; iter < Iterations; iter++)
        {
            E7State c = RandomState(r);
            int target = (int)(c.rc_avg_frame_bandwidth * (r.NextDouble() * 10));
            E7State p = c;
            E7Shim.e7_rc_set_frame_target(ref c, target);
            Run(ref p, cpi => av1_rc_set_frame_target(cpi, target, cpi.common.width, cpi.common.height));
            AssertSame(c, p, $"iter {iter}");
        }
    }

    /// <summary>Key frames that are not forced have frame_number 0 in the subset
    /// (av1_encode() resets it); the port rejects the other case.</summary>
    internal static E7State PickQState(Random r)
    {
        E7State s = RandomState(r);
        if (s.common_current_frame_frame_type == KEY_FRAME && s.ppi_p_rc_this_key_frame_forced == 0)
            s.common_current_frame_frame_number = 0;
        s.ppi_gf_group_update_type_0 = s.common_current_frame_frame_type == KEY_FRAME ? KF_UPDATE
                                       : s.ppi_gf_group_update_type_0 == KF_UPDATE ? LF_UPDATE
                                       : s.ppi_gf_group_update_type_0;
        return s;
    }

    [Fact]
    public void RcPickQAndBounds()
    {
        var r = new Random(7107);
        for (int iter = 0; iter < Iterations; iter++)
        {
            E7State c = PickQState(r);
            E7State p = c;
            int expected = E7Shim.e7_pick_q_and_bounds(ref c, out int eb, out int et);
            int ab = 0, at = 0;
            int actual = Run(ref p, cpi => av1_rc_pick_q_and_bounds(cpi, cpi.common.width, cpi.common.height,
                                                                    cpi.gf_frame_index, ref ab, ref at));
            Assert.True(expected == actual, $"iter {iter}: q libaom={expected} port={actual}");
            Assert.Equal(eb, ab);
            Assert.Equal(et, at);
            AssertSame(c, p, $"iter {iter}");
        }
    }

    [Fact]
    public void RcUpdateRateCorrectionFactors()
    {
        var r = new Random(7108);
        for (int iter = 0; iter < Iterations; iter++)
        {
            E7State c = RandomState(r);
            int is_encode_stage = r.Next(2);
            E7State p = c;
            E7Shim.e7_update_rate_correction_factors(ref c, is_encode_stage);
            Run(ref p, cpi => av1_rc_update_rate_correction_factors(cpi, is_encode_stage,
                                                                    cpi.common.width, cpi.common.height));
            AssertSame(c, p, $"iter {iter}");
        }
    }

    [Fact]
    public void RcPostencodeUpdate()
    {
        var r = new Random(7109);
        for (int iter = 0; iter < Iterations; iter++)
        {
            E7State c = RandomState(r);
            ulong bytes = (ulong)(c.rc_avg_frame_bandwidth * Math.Exp(r.NextDouble() * 6 - 3) / 8);
            E7State p = c;
            E7Shim.e7_postencode_update(ref c, bytes);
            Run(ref p, cpi => av1_rc_postencode_update(cpi, bytes));
            AssertSame(c, p, $"iter {iter}");
        }
    }

    [Fact]
    public void RcPostencodeUpdateDropFrame() =>
        Check(7110, RandomState,
              (ref E7State s) => { E7Shim.e7_postencode_update_drop_frame(ref s); return 0; },
              cpi => { av1_rc_postencode_update_drop_frame(cpi); return 0; });

    [Fact]
    public void EncodedframeOvershootCbr()
    {
        var r = new Random(7111);
        for (int iter = 0; iter < Iterations; iter++)
        {
            E7State c = RandomState(r);
            int q0 = r.Next(256);
            int qc = q0, qp = q0;
            E7State p = c;
            int expected = E7Shim.e7_encodedframe_overshoot_cbr(ref c, ref qc);
            int actual = Run(ref p, cpi => av1_encodedframe_overshoot_cbr(cpi, ref qp));
            Assert.Equal(expected, actual);
            Assert.Equal(qc, qp);
            AssertSame(c, p, $"iter {iter}");
        }
    }

    [Fact]
    public void PostencodeDropCbr()
    {
        var r = new Random(7112);
        int drops = 0;
        for (int iter = 0; iter < Iterations; iter++)
        {
            E7State c = RandomState(r);
            if (r.Next(2) == 0)
            {
                // Make the drop conditions likely: low buffer, low q, big frame.
                c.rc_high_source_sad = 0;
                c.ppi_p_rc_buffer_level = c.ppi_p_rc_optimal_buffer_level / 8;
                c.common_quant_params_base_qindex = r.Next(0, 150);
            }
            int size0 = (int)Math.Min(int.MaxValue / 16, c.rc_avg_frame_bandwidth * Math.Exp(r.NextDouble() * 6 - 2) / 8);
            int sc = size0, sp = size0;
            E7State p = c;
            int expected = E7Shim.e7_postencode_drop_cbr(ref c, ref sc);
            int actual = Run(ref p, cpi => av1_postencode_drop_cbr(cpi, ref sp));
            Assert.Equal(expected, actual);
            Assert.Equal(sc, sp);
            AssertSame(c, p, $"iter {iter}");
            drops += expected;
        }
        Assert.True(drops > 100, $"only {drops} drops exercised");
    }

    [Fact]
    public void CyclicRefreshUpdateParameters()
    {
        var r = new Random(7113);
        for (int iter = 0; iter < Iterations; iter++)
        {
            E7State c = RandomState(r);
            if (r.Next(4) == 0) c.oxcf_tune_cfg_content = 1; // AOM_CONTENT_SCREEN
            if (r.Next(10) == 0) c.rc_rtc_external_ratectrl = 1;
            if (r.Next(10) == 0) c.roi_enabled = 1;
            if (r.Next(2) == 0)
            {
                // Values on both sides of the thresholds in the function.
                int qp_thresh = Math.Max(16, (int)c.rc_best_quality + 4);
                c.rc_frames_since_key = new[] { 0, 1, 19, 20, 21, 39, 40, 41, 79, 80, 81, 159, 160, 161 }[r.Next(14)];
                c.cyclic_refresh_counter_encode_maxq_scene_change = c.rc_frames_since_key + r.Next(-2, 3);
                c.ppi_p_rc_avg_frame_qindex_1 = new[] { qp_thresh - 1, qp_thresh, 234, 235, 236 }[r.Next(5)];
                c.rc_avg_frame_low_motion = r.Next(28, 32);
                c.rc_avg_frame_bandwidth = r.Next(2998, 3002);
                c.cyclic_refresh_percent_refresh_adjustment = r.Next(-5, 6);
            }
            E7State p = c;
            E7Shim.e7_cr_update_parameters(ref c);
            Run(ref p, av1_cyclic_refresh_update_parameters);
            AssertSame(c, p, $"iter {iter}");
        }
    }

    [Fact]
    public void AdjustGfRefreshQpOnePassRt() =>
        Check(7114, RandomState,
              (ref E7State s) => { E7Shim.e7_adjust_gf_refresh_qp(ref s); return 0; },
              cpi => { av1_adjust_gf_refresh_qp_one_pass_rt(cpi); return 0; });

    [Fact]
    public void SetRtcReferenceStructureOneLayer()
    {
        var r = new Random(7115);
        for (int iter = 0; iter < Iterations; iter++)
        {
            E7State c = RandomState(r);
            int gf_update = r.Next(2);
            E7State p = c;
            E7Shim.e7_set_rtc_reference_structure(ref c, gf_update);
            Run(ref p, cpi => av1_set_rtc_reference_structure_one_layer(cpi, gf_update));
            AssertSame(c, p, $"iter {iter}");
        }
    }
}
