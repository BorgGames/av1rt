using System;
using Xunit;
using static Av1rt.Aom.aq_cyclicrefresh;
using static Av1rt.Aom.ratectrl;
using static Av1rt.Tests.RateControl.E7Harness;

namespace Av1rt.Tests.RateControl;

/// <summary>Differential tests of the q / bits helpers of ratectrl.c and aq_cyclicrefresh.c.</summary>
[Trait("Category", "Native")]
[Collection("e7")]
public class RateControlQTests
{
    private static readonly double[] s_correctionFactors =
        { 0.005, 0.01, 0.1, 0.3333, 0.5, 0.7, 1.0, 1.37, 2.5, 7.0, 49.9, 50.0 };

    [Fact]
    public void StateLayoutMatchesShim() => AssertLayout();

    [Fact]
    public void ConvertQindexToQ_AllQindex()
    {
        for (int q = 0; q < 256; q++)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(E7Shim.e7_convert_qindex_to_q(q)),
                         BitConverter.DoubleToInt64Bits(av1_convert_qindex_to_q(q, AOM_BITS_8)));
        }
    }

    [Fact]
    public void ConvertQToQindex()
    {
        var r = new Random(7001);
        for (int i = 0; i < 4000; i++)
        {
            double q = i < 256 ? av1_convert_qindex_to_q(i, AOM_BITS_8)
                     : i < 512 ? av1_convert_qindex_to_q(i - 256, AOM_BITS_8) + (r.NextDouble() - 0.5) * 0.5
                     : r.NextDouble() * 500 - 10;
            Assert.Equal(E7Shim.e7_convert_q_to_qindex(q), av1_convert_q_to_qindex(q, AOM_BITS_8));
        }
    }

    [Fact]
    public void FindQindex()
    {
        var r = new Random(7002);
        for (int i = 0; i < 4000; i++)
        {
            int best = r.Next(256);
            int worst = r.Next(best, 256);
            double q = r.Next(3) == 0 ? av1_convert_qindex_to_q(r.Next(256), AOM_BITS_8)
                                      : r.NextDouble() * 480 - 5;
            Assert.Equal(E7Shim.e7_find_qindex(q, best, worst), av1_find_qindex(q, AOM_BITS_8, best, worst));
        }
    }

    [Fact]
    public void DefaultMinGfInterval()
    {
        var r = new Random(7003);
        for (int i = 0; i < 4000; i++)
        {
            int w = r.Next(2) == 0 ? r.Next(16, 8193) : new[] { 3840, 4096, 7680 }[r.Next(3)];
            int h = r.Next(2) == 0 ? r.Next(16, 4321) : new[] { 2160, 2304, 4320 }[r.Next(3)];
            double fr = r.Next(2) == 0 ? r.NextDouble() * 240 : new[] { 24, 30, 60, 120, 29.97 }[r.Next(5)];
            Assert.Equal(E7Shim.e7_default_min_gf_interval(w, h, fr),
                         av1_rc_get_default_min_gf_interval(w, h, fr));
        }
    }

    [Fact]
    public void RcBitsPerMb_AllFrameTypesQindexAndFactors()
    {
        var r = new Random(7004);
        var expected = new int[256];
        for (int iter = 0; iter < 8; iter++)
        {
            E7State s = RandomState(r);
            s.is_screen_content_type = iter & 1;
            AV1_COMP_Run(s, out var cpi);
            for (int frame_type = 0; frame_type < 4; frame_type++)
            {
                foreach (double cf in s_correctionFactors)
                {
                    E7Shim.e7_rc_bits_per_mb_all(ref s, frame_type, cf, expected);
                    for (int q = 0; q < 256; q++)
                        Assert.Equal(expected[q], av1_rc_bits_per_mb(cpi, frame_type, q, cf, 0));
                }
            }
        }
    }

    [Fact]
    public void EstimateBitsAtQ_AllQindex()
    {
        var r = new Random(7005);
        var expected = new int[256];
        for (int iter = 0; iter < 40; iter++)
        {
            E7State s = RandomState(r);
            AV1_COMP_Run(s, out var cpi);
            double cf = s_correctionFactors[r.Next(s_correctionFactors.Length)];
            E7Shim.e7_estimate_bits_at_q_all(ref s, cf, expected);
            for (int q = 0; q < 256; q++)
                Assert.Equal(expected[q], av1_estimate_bits_at_q(cpi, q, cf));
        }
    }

    [Fact]
    public void ComputeQdelta()
    {
        var r = new Random(7006);
        for (int iter = 0; iter < 3000; iter++)
        {
            E7State s = RandomState(r);
            double qstart = r.Next(2) == 0 ? av1_convert_qindex_to_q(r.Next(256), AOM_BITS_8) : r.NextDouble() * 460;
            double qtarget = r.Next(2) == 0 ? qstart * (0.25 + r.NextDouble() * 1.5) : r.NextDouble() * 460;
            E7State c = s;
            int expected = E7Shim.e7_compute_qdelta(ref c, qstart, qtarget);
            int actual = Run(ref s, cpi => av1_compute_qdelta(cpi.rc, qstart, qtarget, AOM_BITS_8));
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void ComputeQdeltaByRate_AllQindex()
    {
        var r = new Random(7007);
        var expected = new int[256];
        double[] ratios = { 0.125, 0.5, 0.75, 1.0, 1.5, 2.0, 2.25, 3.0, 3.25, 4.0 };
        for (int iter = 0; iter < 12; iter++)
        {
            E7State s = RandomState(r);
            AV1_COMP_Run(s, out var cpi);
            for (int frame_type = 0; frame_type < 2; frame_type++)
            {
                foreach (double ratio in ratios)
                {
                    E7Shim.e7_compute_qdelta_by_rate_all(ref s, frame_type, ratio, expected);
                    for (int q = 0; q < 256; q++)
                        Assert.Equal(expected[q], av1_compute_qdelta_by_rate(cpi, frame_type, q, ratio));
                }
            }
        }
    }

    [Fact]
    public void CyclicRefreshRcBitsPerMb_AllQindex()
    {
        var r = new Random(7008);
        var expected = new int[256];
        for (int iter = 0; iter < 60; iter++)
        {
            E7State s = RandomState(r);
            s.rc_rtc_external_ratectrl = iter % 10 == 0 ? 1 : 0;
            AV1_COMP_Run(s, out var cpi);
            double cf = s_correctionFactors[r.Next(s_correctionFactors.Length)];
            E7Shim.e7_cr_rc_bits_per_mb_all(ref s, cf, expected);
            for (int q = 0; q < 256; q++)
                Assert.Equal(expected[q], av1_cyclic_refresh_rc_bits_per_mb(cpi, q, cf));
        }
    }

    [Fact]
    public void CyclicRefreshEstimateBitsAtQ()
    {
        var r = new Random(7009);
        for (int iter = 0; iter < 3000; iter++)
        {
            E7State s = RandomState(r);
            s.rc_rtc_external_ratectrl = iter % 10 == 0 ? 1 : 0;
            // base_qindex + qindex_delta stays a valid index in the encoder.
            s.common_quant_params_base_qindex = r.Next(90, 256);
            double cf = s_correctionFactors[r.Next(s_correctionFactors.Length)];
            E7State c = s;
            int expected = E7Shim.e7_cr_estimate_bits_at_q(ref c, cf);
            int actual = Run(ref s, cpi => av1_cyclic_refresh_estimate_bits_at_q(cpi, cf));
            Assert.Equal(expected, actual);
            AssertSame(c, s, $"iter {iter}");
        }
    }

    private static void AV1_COMP_Run(in E7State s, out Av1rt.Aom.AV1_COMP cpi) => cpi = NewCpi(s);
}
