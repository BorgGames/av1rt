using System;
using System.Runtime.Intrinsics.X86;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.loopfilter_h;
using static Av1rt.Aom.loopfilter_sse2;

namespace Av1rt.Tests.Simd.S6;

/// <summary>
/// aom_dsp/x86/loopfilter_sse2.c: every ported aom_lpf_*_sse2 kernel against the aom_lpf_*_c
/// port, on random edges with realistic (av1_loop_filter_init) and lpf_test.cc thresholds.
/// </summary>
public class LoopFilterSse2Tests
{
    private delegate void Single(Ptr<byte> s, int p, Ptr<byte> b, Ptr<byte> l, Ptr<byte> t);

    private delegate void Dual(Ptr<byte> s, int p, Ptr<byte> b0, Ptr<byte> l0, Ptr<byte> t0,
                               Ptr<byte> b1, Ptr<byte> l1, Ptr<byte> t1);

    private sealed record Kernel(bool Vertical, int Taps, int Lanes, Delegate Simd, Delegate C);

    private static Kernel Get(string name) => name switch
    {
        "horizontal_4" => new(false, 4, 4, (Single)aom_lpf_horizontal_4_sse2, (Single)aom_lpf_horizontal_4_c),
        "horizontal_6" => new(false, 6, 4, (Single)aom_lpf_horizontal_6_sse2, (Single)aom_lpf_horizontal_6_c),
        "horizontal_8" => new(false, 8, 4, (Single)aom_lpf_horizontal_8_sse2, (Single)aom_lpf_horizontal_8_c),
        "horizontal_14" => new(false, 14, 4, (Single)aom_lpf_horizontal_14_sse2, (Single)aom_lpf_horizontal_14_c),
        "horizontal_4_dual" => new(false, 4, 8, (Dual)aom_lpf_horizontal_4_dual_sse2, (Dual)aom_lpf_horizontal_4_dual_c),
        "horizontal_6_dual" => new(false, 6, 8, (Dual)aom_lpf_horizontal_6_dual_sse2, (Dual)aom_lpf_horizontal_6_dual_c),
        "horizontal_8_dual" => new(false, 8, 8, (Dual)aom_lpf_horizontal_8_dual_sse2, (Dual)aom_lpf_horizontal_8_dual_c),
        "horizontal_14_dual" => new(false, 14, 8, (Dual)aom_lpf_horizontal_14_dual_sse2, (Dual)aom_lpf_horizontal_14_dual_c),
        "horizontal_4_quad" => new(false, 4, 16, (Single)aom_lpf_horizontal_4_quad_sse2, (Single)aom_lpf_horizontal_4_quad_c),
        "vertical_4" => new(true, 4, 4, (Single)aom_lpf_vertical_4_sse2, (Single)aom_lpf_vertical_4_c),
        "vertical_6" => new(true, 6, 4, (Single)aom_lpf_vertical_6_sse2, (Single)aom_lpf_vertical_6_c),
        "vertical_8" => new(true, 8, 4, (Single)aom_lpf_vertical_8_sse2, (Single)aom_lpf_vertical_8_c),
        "vertical_14" => new(true, 14, 4, (Single)aom_lpf_vertical_14_sse2, (Single)aom_lpf_vertical_14_c),
        "vertical_4_dual" => new(true, 4, 8, (Dual)aom_lpf_vertical_4_dual_sse2, (Dual)aom_lpf_vertical_4_dual_c),
        "vertical_6_dual" => new(true, 6, 8, (Dual)aom_lpf_vertical_6_dual_sse2, (Dual)aom_lpf_vertical_6_dual_c),
        "vertical_8_dual" => new(true, 8, 8, (Dual)aom_lpf_vertical_8_dual_sse2, (Dual)aom_lpf_vertical_8_dual_c),
        "vertical_14_dual" => new(true, 14, 8, (Dual)aom_lpf_vertical_14_dual_sse2, (Dual)aom_lpf_vertical_14_dual_c),
        "vertical_4_quad" => new(true, 4, 16, (Single)aom_lpf_vertical_4_quad_sse2, (Single)aom_lpf_vertical_4_quad_c),
        "vertical_6_quad" => new(true, 6, 16, (Single)aom_lpf_vertical_6_quad_sse2, (Single)aom_lpf_vertical_6_quad_c),
        "vertical_8_quad" => new(true, 8, 16, (Single)aom_lpf_vertical_8_quad_sse2, (Single)aom_lpf_vertical_8_quad_c),
        _ => throw new ArgumentException(name),
    };

    // 16-byte margin on every side of the widest SIMD footprint (vertical 14: s-8..s+7;
    // quads: 16 rows / 16 columns from s).
    private const int Rows = 40, Stride = 48, Origin = 16 * Stride + 16;
    private const int Iterations = 4096;

    private static byte[] Fill(int v)
    {
        var a = new byte[16];  // SIMD_WIDTH, like loop_filter_thresh
        a.AsSpan().Fill((byte)v);
        return a;
    }

    // libaom av1/common/av1_loopfilter.c update_sharpness() + av1_loop_filter_init(): the
    // lfthr[lvl].{mblim, lim, hev_thr} arrays the encoder passes.
    private static byte[][] Realistic(int sharpness, int lvl)
    {
        int block_inside_limit = lvl >> ((sharpness > 0 ? 1 : 0) + (sharpness > 4 ? 1 : 0));
        if (sharpness > 0 && block_inside_limit > 9 - sharpness) block_inside_limit = 9 - sharpness;
        if (block_inside_limit < 1) block_inside_limit = 1;
        return new[] { Fill(2 * (lvl + 2) + block_inside_limit), Fill(block_inside_limit), Fill(lvl >> 4) };
    }

    // libaom test/lpf_test.cc GetOuterThresh / GetInnerThresh / GetHevThresh (MAX_LOOP_FILTER 63).
    private static byte[][] LpfTest(Random rng) =>
        new[] { Fill(rng.Next(3 * 63 + 5)), Fill(rng.Next(63 + 1)), Fill(rng.Next(63 + 1) >> 4) };

    // Iteration i alternates realistic thresholds (sweeping all 8 sharpness x 64 levels in
    // order, so each pair is hit Iterations / 1024 times) and lpf_test.cc thresholds.
    private static byte[][] Thresholds(Random rng, int i, int half) =>
        (i & 1) == 0
            ? (half == 0 ? Realistic((i >> 1) & 7, (i >> 4) & 63) : Realistic(rng.Next(8), rng.Next(64)))
            : LpfTest(rng);

    // One 14-tap line across the edge (positions -7..6, p6..q6), in one of several shapes so that
    // the filter / flat / flat2 / hev masks take both values (as in E4/LoopFilterKernelTests).
    private static void FillLine(Random rng, byte[] buf, int q0, int step, int lo = -7, int hi = 6)
    {
        int shape = rng.Next(6);
        int a = rng.Next(256), b = Math.Clamp(a + rng.Next(-24, 25), 0, 255);
        int noise = rng.Next(3);
        for (int i = lo; i <= hi; i++)
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

    private static void Run(Delegate f, byte[] buf, int origin, int stride, byte[][] t0, byte[][] t1)
    {
        var s = new Ptr<byte>(buf, origin);
        if (f is Dual d) d(s, stride, t0[0], t0[1], t0[2], t1[0], t1[1], t1[2]);
        else ((Single)f)(s, stride, t0[0], t0[1], t0[2]);
    }

    public static readonly TheoryData<string> Names = new()
    {
        "horizontal_4", "horizontal_6", "horizontal_8", "horizontal_14",
        "horizontal_4_dual", "horizontal_6_dual", "horizontal_8_dual", "horizontal_14_dual",
        "horizontal_4_quad",
        "vertical_4", "vertical_6", "vertical_8", "vertical_14",
        "vertical_4_dual", "vertical_6_dual", "vertical_8_dual", "vertical_14_dual",
        "vertical_4_quad", "vertical_6_quad", "vertical_8_quad",
    };

    [Theory]
    [MemberData(nameof(Names))]
    public void Sse2MatchesC(string name)
    {
        if (!Avx2.IsSupported) return;
        Kernel k = Get(name);
        var rng = new Random(StableSeed(name));
        var buf = new byte[Rows * Stride];
        var simd = new byte[buf.Length];
        var c = new byte[buf.Length];
        int filtered = 0;

        for (int iter = 0; iter < Iterations; iter++)
        {
            rng.NextBytes(buf);
            for (int lane = 0; lane < k.Lanes; lane++)
            {
                if (k.Vertical) FillLine(rng, buf, Origin + lane * Stride, 1);
                else FillLine(rng, buf, Origin + lane, Stride);
            }
            byte[][] t0 = Thresholds(rng, iter, 0);
            byte[][] t1 = Thresholds(rng, iter, 1);  // second half of a dual: independent

            buf.CopyTo(simd, 0);
            buf.CopyTo(c, 0);
            Run(k.Simd, simd, Origin, Stride, t0, t1);
            Run(k.C, c, Origin, Stride, t0, t1);

            if (!simd.AsSpan().SequenceEqual(c))
            {
                int i = simd.AsSpan().CommonPrefixLength(c);
                Assert.Fail($"{name} iter {iter}: byte {i} (row {i / Stride - 16}, col {i % Stride - 16}) " +
                            $"sse2 {simd[i]} c {c[i]}; thresholds {t0[0][0]}/{t0[1][0]}/{t0[2][0]}, " +
                            $"{t1[0][0]}/{t1[1][0]}/{t1[2][0]}");
            }
            if (!c.AsSpan().SequenceEqual(buf)) filtered++;
        }
        // The edge shapes must exercise the filter, not just the no-op path.
        Assert.True(filtered > Iterations / 4, $"{name}: only {filtered} of {Iterations} iterations filtered");
    }

    private static int StableSeed(string name)
    {
        int h = 17;
        foreach (char ch in name) h = h * 31 + ch;
        return h & 0x7fffffff;
    }

    // Rows / columns (relative to s) that the SSE2 kernel loads and stores. It may touch a few
    // bytes beyond the C kernel's window (e.g. vertical_4 loads 8 bytes from s - 2; the vertical
    // quads transpose 8 columns from s - 4); frame buffers have >= 32-pixel borders, as in libaom.
    private static (int rowLo, int rowHi, int colLo, int colHi) Footprint(Kernel k)
    {
        int half = k.Taps switch { 4 => 2, 6 => 3, 8 => 4, _ => 7 };
        if (!k.Vertical) return (-half, half - 1, 0, k.Lanes - 1);
        if (k.Lanes == 16) return (0, 15, -4, 3);
        return k.Taps switch
        {
            4 => (0, k.Lanes - 1, -2, 5),
            6 => (0, k.Lanes - 1, -3, 4),
            8 => (0, k.Lanes - 1, -4, 3),
            _ => (0, k.Lanes - 1, -8, 7),
        };
    }

    // Runs each kernel in an array that ends exactly at the first and last byte of its declared
    // footprint: an access before the first row's start or after the last row's end throws
    // (Ptr / span bounds checks), and the result must still match C.
    [Theory]
    [MemberData(nameof(Names))]
    public void Sse2StaysInsideFootprint(string name)
    {
        if (!Avx2.IsSupported) return;
        Kernel k = Get(name);
        const int stride = 24;
        var (rowLo, rowHi, colLo, colHi) = Footprint(k);
        int first = rowLo * stride + colLo, last = rowHi * stride + colHi;
        int half = k.Taps switch { 4 => 2, 6 => 3, 8 => 4, _ => 7 };
        var rng = new Random(StableSeed(name) ^ 0x2468);
        var buf = new byte[last - first + 1];
        var simd = new byte[buf.Length];
        var c = new byte[buf.Length];
        int origin = -first;

        for (int iter = 0; iter < 256; iter++)
        {
            rng.NextBytes(buf);
            int lanes = k.Lanes;
            for (int lane = 0; lane < lanes; lane++)
            {
                // Only the positions the kernel's taps read.
                if (k.Vertical) FillLine(rng, buf, origin + lane * stride, 1, -half, half - 1);
                else FillLine(rng, buf, origin + lane, stride, -half, half - 1);
            }
            byte[][] t0 = Thresholds(rng, iter, 0);
            byte[][] t1 = Thresholds(rng, iter, 1);

            buf.CopyTo(simd, 0);
            buf.CopyTo(c, 0);
            Run(k.Simd, simd, origin, stride, t0, t1);
            Run(k.C, c, origin, stride, t0, t1);
            Assert.True(simd.AsSpan().SequenceEqual(c), $"{name} iter {iter}");
        }
    }
}
