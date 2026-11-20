using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.loopfilter_h;

namespace Av1rt.Tests.E4;

/// <summary>aom_dsp/loopfilter.c: every aom_lpf_*_c kernel against libaom.</summary>
public class LoopFilterKernelTests
{
    private delegate void Single(Ptr<byte> s, int p, Ptr<byte> b, Ptr<byte> l, Ptr<byte> t);

    private delegate void Dual(Ptr<byte> s, int p, Ptr<byte> b0, Ptr<byte> l0, Ptr<byte> t0,
                               Ptr<byte> b1, Ptr<byte> l1, Ptr<byte> t1);

    private static readonly Single[,] Singles =
    {
        { aom_lpf_horizontal_4_c, aom_lpf_horizontal_6_c, aom_lpf_horizontal_8_c, aom_lpf_horizontal_14_c },
        { aom_lpf_vertical_4_c, aom_lpf_vertical_6_c, aom_lpf_vertical_8_c, aom_lpf_vertical_14_c },
    };

    private static readonly Dual[,] Duals =
    {
        { aom_lpf_horizontal_4_dual_c, aom_lpf_horizontal_6_dual_c, aom_lpf_horizontal_8_dual_c, aom_lpf_horizontal_14_dual_c },
        { aom_lpf_vertical_4_dual_c, aom_lpf_vertical_6_dual_c, aom_lpf_vertical_8_dual_c, aom_lpf_vertical_14_dual_c },
    };

    private static readonly Single[,] Quads =
    {
        { aom_lpf_horizontal_4_quad_c, aom_lpf_horizontal_6_quad_c, aom_lpf_horizontal_8_quad_c, aom_lpf_horizontal_14_quad_c },
        { aom_lpf_vertical_4_quad_c, aom_lpf_vertical_6_quad_c, aom_lpf_vertical_8_quad_c, aom_lpf_vertical_14_quad_c },
    };

    private const int Rows = 40, Stride = 44, Origin = 16 * Stride + 16;

    /// <summary>libaom's loop_filter_thresh table for a sharpness: [level][0..2] = mblim, lim, hev_thr.</summary>
    internal static byte[][][] Thresholds(int sharpness)
    {
        var raw = new byte[64 * 48];
        E4Shim.aomshim_e4_lf_init(sharpness, raw);
        var t = new byte[64][][];
        for (int l = 0; l < 64; l++)
        {
            t[l] = new byte[3][];
            for (int k = 0; k < 3; k++) t[l][k] = raw.AsSpan(l * 48 + k * 16, 16).ToArray();
        }
        return t;
    }

    // One 14-tap line across the edge (positions -7..6, p6..q6), in one of several shapes so that
    // the filter / flat / flat2 / hev masks take both values.
    private static void FillLine(Random rng, byte[] buf, int q0, int step)
    {
        int shape = rng.Next(6);
        int a = rng.Next(256), b = Math.Clamp(a + rng.Next(-24, 25), 0, 255);
        int noise = rng.Next(3);
        for (int i = -7; i <= 6; i++)
        {
            int v = shape switch
            {
                0 => rng.Next(256),                                        // noise
                1 => (i < 0 ? a : b) + rng.Next(-noise, noise + 1),       // two flat sides
                2 => a + (b - a) * (i + 7) / 13 + rng.Next(-noise, noise + 1), // ramp
                3 => (i < 0 ? 0 : 255),                                    // extreme step
                4 => a + rng.Next(-1, 2),                                  // flat
                _ => (i < 0 ? a : b) + (Math.Abs(i) > 3 ? rng.Next(-12, 13) : 0), // flat inner only
            };
            buf[q0 + i * step] = (byte)Math.Clamp(v, 0, 255);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    [InlineData(12)] [InlineData(13)] [InlineData(14)] [InlineData(15)]
    [InlineData(16)] [InlineData(17)] [InlineData(18)] [InlineData(19)]
    [InlineData(20)] [InlineData(21)] [InlineData(22)] [InlineData(23)]
    public void KernelMatchesLibaom(int kind)
    {
        int dir = kind / 12, size = kind % 12 / 3, variant = kind % 3;
        var rng = new Random(1000 + kind);
        var tables = new byte[8][][][];
        for (int s = 0; s < 8; s++) tables[s] = Thresholds(s);
        int lanes = variant == 0 ? 4 : variant == 1 ? 8 : 16;

        for (int iter = 0; iter < 3000; iter++)
        {
            var buf = new byte[Rows * Stride];
            rng.NextBytes(buf);
            for (int lane = 0; lane < lanes; lane++)
            {
                if (dir == 0) FillLine(rng, buf, Origin + lane, Stride); // horizontal edge: lanes are columns
                else FillLine(rng, buf, Origin + lane * Stride, 1);     // vertical edge: lanes are rows
            }
            byte[][][] tab = tables[rng.Next(8)];
            byte[][] t0 = tab[rng.Next(64)], t1 = tab[rng.Next(64)];

            var expected = (byte[])buf.Clone();
            E4Shim.Lpf(kind, expected, Origin, Stride, t0[0], t0[1], t0[2], t1[0], t1[1], t1[2]);

            var actual = (byte[])buf.Clone();
            var s = new Ptr<byte>(actual, Origin);
            if (variant == 0) Singles[dir, size](s, Stride, t0[0], t0[1], t0[2]);
            else if (variant == 1) Duals[dir, size](s, Stride, t0[0], t0[1], t0[2], t1[0], t1[1], t1[2]);
            else Quads[dir, size](s, Stride, t0[0], t0[1], t0[2]);

            Assert.True(expected.AsSpan().SequenceEqual(actual), $"kind {kind} iteration {iter}");
        }
    }
}
