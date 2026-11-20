using System;
using System.Runtime.Intrinsics.X86;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.loopfilter_avx2;
using static Av1rt.Aom.loopfilter_h;

namespace Av1rt.Tests.Simd.S6;

/// <summary>aom_dsp/x86/loopfilter_avx2.c: the AVX2 quad loop filters against the C port.</summary>
public class LoopFilterAvx2Tests
{
    private delegate void Quad(Ptr<byte> s, int p, Ptr<byte> blimit, Ptr<byte> limit, Ptr<byte> thresh);

    // kind: 0 horizontal_6_quad, 1 horizontal_8_quad, 2 horizontal_14_quad, 3 vertical_14_quad,
    // 4 vertical_6_quad (an av1rt kernel: libaom's is SSE2, through a transpose buffer).
    private static readonly Quad[] Avx2Kernels =
    {
        aom_lpf_horizontal_6_quad_avx2, aom_lpf_horizontal_8_quad_avx2,
        aom_lpf_horizontal_14_quad_avx2, aom_lpf_vertical_14_quad_avx2,
        aom_lpf_vertical_6_quad_avx2,
    };

    private static readonly Quad[] C =
    {
        aom_lpf_horizontal_6_quad_c, aom_lpf_horizontal_8_quad_c,
        aom_lpf_horizontal_14_quad_c, aom_lpf_vertical_14_quad_c,
        aom_lpf_vertical_6_quad_c,
    };

    private const int Rows = 40, Stride = 48, Origin = 16 * Stride + 16;
    private const int MaxLoopFilter = 63;

    /// <summary>
    /// av1_loop_filter_init / update_sharpness: [sharpness][level] = { mblim, lim, hev_thr }, each a
    /// SIMD_WIDTH (16 byte) array like loop_filter_thresh.
    /// </summary>
    private static readonly byte[][][][] Realistic = BuildRealistic();

    private static byte[][][][] BuildRealistic()
    {
        var t = new byte[8][][][];
        for (int sharp = 0; sharp < 8; sharp++)
        {
            t[sharp] = new byte[MaxLoopFilter + 1][][];
            for (int lvl = 0; lvl <= MaxLoopFilter; lvl++)
            {
                int block_inside_limit = lvl >> ((sharp > 0 ? 1 : 0) + (sharp > 4 ? 1 : 0));
                if (sharp > 0 && block_inside_limit > 9 - sharp) block_inside_limit = 9 - sharp;
                if (block_inside_limit < 1) block_inside_limit = 1;
                t[sharp][lvl] = new[]
                {
                    Splat(2 * (lvl + 2) + block_inside_limit), Splat(block_inside_limit), Splat(lvl >> 4),
                };
            }
        }
        return t;
    }

    private static byte[] Splat(int v)
    {
        var a = new byte[16];
        Array.Fill(a, (byte)v);
        return a;
    }

    // libaom test/lpf_test.cc: GetOuterThresh / GetInnerThresh / GetHevThresh.
    private static byte[][] LibaomTestThresholds(Random rng) => new[]
    {
        Splat(rng.Next(3 * MaxLoopFilter + 5)), Splat(rng.Next(MaxLoopFilter + 1)),
        Splat(rng.Next(MaxLoopFilter + 1) >> 4),
    };

    // One 14-tap line across the edge (positions -7..6, p6..q6), in one of several shapes so that
    // the filter / flat / flat2 / hev masks take both values.
    private static void FillLine(Random rng, byte[] buf, int q0, int step, int limit)
    {
        int shape = rng.Next(8);
        int a = rng.Next(256), b = Math.Clamp(a + rng.Next(-24, 25), 0, 255);
        int noise = rng.Next(3);
        int prev = rng.Next(256);
        for (int i = -7; i <= 6; i++)
        {
            int v;
            switch (shape)
            {
                case 0: v = rng.Next(256); break;                                         // noise
                case 1: v = (i < 0 ? a : b) + rng.Next(-noise, noise + 1); break;          // two flat sides
                case 2: v = a + (b - a) * (i + 7) / 13 + rng.Next(-noise, noise + 1); break; // ramp
                case 3: v = i < 0 ? 0 : 255; break;                                        // extreme step
                case 4: v = a + rng.Next(-1, 2); break;                                    // flat
                case 5: v = (i < 0 ? a : b) + (Math.Abs(i) > 3 ? rng.Next(-12, 13) : 0); break; // flat inner only
                default:
                    // lpf_test.cc InitInput: steps of +-(limit - 1), occasionally a random jump.
                    int r = rng.Next(256);
                    if ((r & 0x80) != 0) v = rng.Next(65536) & 0xff;
                    else if ((r & 0x20) != 0) v = (prev + (limit - 1)) & 0xff;
                    else v = (prev - (limit - 1)) & 0xff;
                    break;
            }
            v = Math.Clamp(v, 0, 255);
            buf[q0 + i * step] = (byte)v;
            prev = v;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void MatchesC(int kind)
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(6000 + kind);
        bool vertical = kind >= 3;
        int filtered = 0, flat = 0, flat2 = 0;

        for (int iter = 0; iter < 20000; iter++)
        {
            var buf = new byte[Rows * Stride];
            int fill = rng.Next(16);
            if (fill == 0) Array.Fill(buf, (byte)0);
            else if (fill == 1) Array.Fill(buf, (byte)255);
            else rng.NextBytes(buf);

            byte[][] t = rng.Next(2) == 0
                ? Realistic[rng.Next(8)][rng.Next(MaxLoopFilter + 1)]
                : LibaomTestThresholds(rng);

            if (fill >= 2)
            {
                for (int lane = 0; lane < 16; lane++)
                {
                    if (!vertical) FillLine(rng, buf, Origin + lane, Stride, t[1][0]); // lanes are columns
                    else FillLine(rng, buf, Origin + lane * Stride, 1, t[1][0]);    // lanes are rows
                }
            }

            var expected = (byte[])buf.Clone();
            C[kind](new Ptr<byte>(expected, Origin), Stride, t[0], t[1], t[2]);

            var actual = (byte[])buf.Clone();
            Avx2Kernels[kind](new Ptr<byte>(actual, Origin), Stride, t[0], t[1], t[2]);

            Assert.True(expected.AsSpan().SequenceEqual(actual), $"kind {kind} iteration {iter}");

            // Which paths ran (per lane): filter4 (p0/q0 changed), flat (p2/q2 changed, not by
            // filter4) and, for the 14-tap kernels, flat2 (p5/q5 changed).
            int step = vertical ? 1 : Stride, lane_step = vertical ? Stride : 1;
            for (int lane = 0; lane < 16; lane++)
            {
                int q0 = Origin + lane * lane_step;
                if (expected[q0] != buf[q0] || expected[q0 - step] != buf[q0 - step]) filtered++;
                if (kind is 0 or 4)
                {
                    // The 6-tap flat path writes p1..q1 only: count filtered lanes whose input is flat.
                    int p2v = buf[q0 - 3 * step], p1v = buf[q0 - 2 * step], p0v = buf[q0 - step];
                    int q0v = buf[q0], q1v = buf[q0 + step], q2v = buf[q0 + 2 * step];
                    bool isFlat = Math.Abs(p1v - p0v) <= 1 && Math.Abs(q1v - q0v) <= 1 &&
                                  Math.Abs(p2v - p0v) <= 1 && Math.Abs(q2v - q0v) <= 1;
                    if (isFlat && (expected[q0] != buf[q0] || expected[q0 - step] != buf[q0 - step])) flat++;
                }
                else if (expected[q0 + 2 * step] != buf[q0 + 2 * step] || expected[q0 - 3 * step] != buf[q0 - 3 * step]) flat++;
                if (kind is 2 or 3 && (expected[q0 + 5 * step] != buf[q0 + 5 * step] || expected[q0 - 6 * step] != buf[q0 - 6 * step])) flat2++;
            }
        }
        int lanes = 20000 * 16;
        Assert.True(filtered > lanes / 20, $"filter4 reached on {filtered} of {lanes} lanes");
        Assert.True(flat > lanes / 100, $"flat reached on {flat} of {lanes} lanes");
        if (kind is 2 or 3) Assert.True(flat2 > lanes / 200, $"flat2 reached on {flat2} of {lanes} lanes");
    }

    /// <summary>
    /// The kernels on a buffer that holds exactly the pixels they load: any access outside that
    /// footprint throws. Horizontal: rows -taps/2 .. taps/2-1 of 16 columns; vertical 14: 16 rows of
    /// columns -8..7 (one column more on each side than the C reads, as in libaom).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void StaysInFootprint(int kind)
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(7000 + kind);
        int half = kind == 0 ? 3 : kind == 1 ? 4 : 7;
        for (int iter = 0; iter < 2000; iter++)
        {
            byte[][] t = Realistic[rng.Next(8)][rng.Next(MaxLoopFilter + 1)];
            byte[] buf;
            int origin, stride = 16;
            if (kind < 3)
            {
                buf = new byte[2 * half * stride];
                origin = half * stride;
                rng.NextBytes(buf);
                for (int lane = 0; lane < 16; lane++)
                {
                    // Only positions -half..half-1 exist; fill the full 14-tap line into a scratch
                    // and copy the part that fits.
                    var line = new byte[14 * 16];
                    FillLine(rng, line, 7 * 16, 16, t[1][0]);
                    for (int i = -half; i < half; i++) buf[origin + lane + i * stride] = line[7 * 16 + i * 16];
                }
            }
            else
            {
                buf = new byte[16 * stride];
                origin = 8;
                rng.NextBytes(buf);
                for (int lane = 0; lane < 16; lane++) FillLine(rng, buf, origin + lane * stride, 1, t[1][0]);
            }

            var expected = (byte[])buf.Clone();
            C[kind](new Ptr<byte>(expected, origin), stride, t[0], t[1], t[2]);
            var actual = (byte[])buf.Clone();
            Avx2Kernels[kind](new Ptr<byte>(actual, origin), stride, t[0], t[1], t[2]);
            Assert.True(expected.AsSpan().SequenceEqual(actual), $"kind {kind} iteration {iter}");
        }
    }
}
