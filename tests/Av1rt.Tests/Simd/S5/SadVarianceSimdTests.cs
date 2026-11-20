using System;
using System.Reflection;
using System.Runtime.Intrinsics.X86;
using Av1rt.Aom;
using Xunit;

namespace Av1rt.Tests.Simd.S5;

// Randomized differential tests of the SIMD ports of work package s5 (SAD, variance, sub-pixel
// variance, mse, comp_avg_pred, var/sse/sum quad and dual, aom_sse, aom_subtract_block) against
// the `_c` ports, which are verified against libaom (E5.DspTests). Each case puts the blocks at a
// random offset in an array sized exactly to what the C version reads, with random strides, so a
// SIMD read beyond the C footprint fails with an out-of-range exception. The SIMD variant tested
// per function is the one the forwarder dispatches to (the RTCD choice on an AVX2 machine).
public class SadVarianceSimdTests
{
    private static readonly (int w, int h)[] AllSizes =
    {
        (128, 128), (128, 64), (64, 128), (64, 64), (64, 32), (32, 64), (32, 32), (32, 16),
        (16, 32), (16, 16), (16, 8), (8, 16), (8, 8), (8, 4), (4, 8), (4, 4),
    };
    private static readonly (int w, int h)[] AvgSizes =
    {
        (128, 128), (128, 64), (64, 128), (64, 64), (64, 32), (32, 64), (32, 32), (32, 16),
        (16, 32), (16, 16), (16, 8), (8, 16), (8, 8),
    };
    private static readonly (int w, int h)[] SkipSizes =
    {
        (128, 128), (128, 64), (64, 128), (64, 64), (64, 32), (32, 64), (32, 32), (32, 16),
        (16, 32), (16, 16), (8, 16),
    };

    private static readonly Type[] SimdTypes =
    {
        typeof(sad_avx2), typeof(sad_impl_avx2), typeof(sad4d_avx2), typeof(sad_sse2),
        typeof(sad4d_sse2), typeof(variance_avx2), typeof(variance_impl_avx2),
        typeof(variance_sse2), typeof(variance_ssse3), typeof(sse_avx2), typeof(subtract_avx2),
    };

    // Pixel content modes: 0 random, 1 all 0, 2 all 255, 3 random 0/255, 4 near 128 (small
    // differences of both signs), 5 near 0, 6 near 255.
    private const int NumModes = 7;

    private static byte[] Pixels(Random rng, int n, int mode)
    {
        var p = new byte[n];
        switch (mode)
        {
            case 0: rng.NextBytes(p); break;
            case 1: break;
            case 2: Array.Fill(p, (byte)255); break;
            case 3: for (int i = 0; i < n; i++) p[i] = (byte)(rng.Next(2) * 255); break;
            case 4: for (int i = 0; i < n; i++) p[i] = (byte)rng.Next(124, 133); break;
            case 5: for (int i = 0; i < n; i++) p[i] = (byte)rng.Next(0, 4); break;
            default: for (int i = 0; i < n; i++) p[i] = (byte)rng.Next(252, 256); break;
        }
        return p;
    }

    // Mode pairs: every pair deterministically first, then random ones.
    private static (int a, int b) ModesFor(Random rng, int i) =>
        i < NumModes * NumModes ? (i / NumModes, i % NumModes) : (rng.Next(NumModes), rng.Next(NumModes));

    private static int Stride(Random rng, int w) => rng.Next(4) switch
    {
        0 => w,
        1 => w + rng.Next(1, 8),
        2 => w + rng.Next(8, 40),
        _ => w + rng.Next(40, 140),
    };

    // A block of rows x cols at a random offset in an array that ends right after it.
    private static Ptr<byte> Block(Random rng, int rows, int cols, int stride, int mode)
    {
        int off = rng.Next(0, 48);
        byte[] a = Pixels(rng, off + (rows - 1) * stride + cols, mode);
        return new Ptr<byte>(a, off);
    }

    private static T SimdFn<T>(string name) where T : Delegate
    {
        foreach (Type t in SimdTypes)
        {
            MethodInfo m = t.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            if (m != null) return (T)m.CreateDelegate(typeof(T));
        }
        Assert.Fail($"SIMD function {name} is missing");
        return null;
    }

    private static T CFn<T>(Type type, string name) where T : Delegate
    {
        MethodInfo m = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.True(m != null, $"{type.Name}.{name} is missing");
        return (T)m.CreateDelegate(typeof(T));
    }

    // The RTCD variant per function family on an AVX2 machine (CONFIG_REALTIME_ONLY).
    private static string SadVariant(int w) => w >= 32 ? "avx2" : "sse2";
    private static string X4dVariant(int w) => w >= 16 ? "avx2" : "sse2";
    private static string VarVariant(int w) => w >= 16 ? "avx2" : "sse2";
    private static string SubpelVariant(int w) => w >= 16 ? "avx2" : "ssse3";
    private static string SubpelAvgVariant(int w) => w >= 32 ? "avx2" : "ssse3";

    [Fact]
    public void SadSimdMatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(5501);
        foreach (var (w, h) in AllSizes)
        {
            string v = SadVariant(w);
            var c = CFn<aom_sad_fn_t>(typeof(sad_h), $"aom_sad{w}x{h}_c");
            var s = SimdFn<aom_sad_fn_t>($"aom_sad{w}x{h}_{v}");
            var fwd = CFn<aom_sad_fn_t>(typeof(sad_h), $"aom_sad{w}x{h}");
            bool hasAvg = Array.IndexOf(AvgSizes, (w, h)) >= 0;
            bool hasSkip = Array.IndexOf(SkipSizes, (w, h)) >= 0;
            var ca = hasAvg ? CFn<aom_sad_avg_fn_t>(typeof(sad_h), $"aom_sad{w}x{h}_avg_c") : null;
            var sa = hasAvg ? SimdFn<aom_sad_avg_fn_t>($"aom_sad{w}x{h}_avg_{v}") : null;
            var cs = hasSkip ? CFn<aom_sad_fn_t>(typeof(sad_h), $"aom_sad_skip_{w}x{h}_c") : null;
            var ss = hasSkip ? SimdFn<aom_sad_fn_t>($"aom_sad_skip_{w}x{h}_{v}") : null;
            for (int i = 0; i < 300; i++)
            {
                var (ma, mb) = ModesFor(rng, i);
                int sst = Stride(rng, w), rst = Stride(rng, w);
                Ptr<byte> src = Block(rng, h, w, sst, ma);
                Ptr<byte> @ref = Block(rng, h, w, rst, mb);
                uint expected = c(src, sst, @ref, rst);
                Assert.Equal(expected, s(src, sst, @ref, rst));
                Assert.Equal(expected, fwd(src, sst, @ref, rst));
                if (hasAvg)
                {
                    Ptr<byte> second = Block(rng, 1, w * h, w * h, rng.Next(NumModes));
                    Assert.Equal(ca(src, sst, @ref, rst, second), sa(src, sst, @ref, rst, second));
                }
                if (hasSkip)
                {
                    Assert.Equal(cs(src, sst, @ref, rst), ss(src, sst, @ref, rst));
                }
            }
        }
    }

    [Fact]
    public void SadMultiSimdMatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(5502);
        string[] names = { "aom_sad{0}x{1}x4d", "aom_sad{0}x{1}x3d", "aom_sad_skip_{0}x{1}x4d" };
        for (int kind = 0; kind < 3; kind++)
        {
            foreach (var (w, h) in kind == 2 ? SkipSizes : AllSizes)
            {
                if (kind == 1 && w < 16) continue;  // x3d below 16 wide stays C (calls x4d).
                string name = string.Format(names[kind], w, h);
                var c = CFn<aom_sad_multi_d_fn_t>(typeof(sad_h), name + "_c");
                var s = SimdFn<aom_sad_multi_d_fn_t>(name + "_" + X4dVariant(w));
                for (int i = 0; i < 200; i++)
                {
                    var (ma, mb) = ModesFor(rng, i);
                    int sst = Stride(rng, w), rst = Stride(rng, w);
                    Ptr<byte> src = Block(rng, h, w, sst, ma);
                    var refs = new Ptr<byte>[4];
                    for (int k = 0; k < 4; k++) refs[k] = Block(rng, h, w, rst, k == 0 ? mb : rng.Next(NumModes));

                    uint[] expected = new uint[4], actual = new uint[4];
                    c(src, sst, refs, rst, expected);
                    s(src, sst, refs, rst, actual);
                    // x3d defines sad_array[0..2] only (libaom's SADx3Test checks 3 blocks; the
                    // AVX2 version stores 0 in [3]).
                    if (kind == 1) Assert.Equal(expected[..3], actual[..3]);
                    else Assert.Equal(expected, actual);
                }
            }
        }
    }

    // x3d below 16 wide is C calling the dispatched x4d: check the whole dispatched chain.
    [Fact]
    public void SadSmallX3dDispatchMatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(5503);
        foreach (var (w, h) in AllSizes)
        {
            if (w >= 16) continue;
            string name = $"aom_sad{w}x{h}x3d";
            var c = CFn<aom_sad_fn_t>(typeof(sad_h), $"aom_sad{w}x{h}_c");
            var fwd = CFn<aom_sad_multi_d_fn_t>(typeof(sad_h), name);
            for (int i = 0; i < 100; i++)
            {
                var (ma, mb) = ModesFor(rng, i);
                int sst = Stride(rng, w), rst = Stride(rng, w);
                Ptr<byte> src = Block(rng, h, w, sst, ma);
                var refs = new Ptr<byte>[4];
                for (int k = 0; k < 4; k++) refs[k] = Block(rng, h, w, rst, mb);
                uint[] actual = new uint[4];
                fwd(src, sst, refs, rst, actual);
                for (int k = 0; k < 3; k++) Assert.Equal(c(src, sst, refs[k], rst), actual[k]);
            }
        }
    }

    [Fact]
    public void VarianceAndMseSimdMatchC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(5504);
        foreach (var (w, h) in AllSizes)
        {
            var c = CFn<aom_variance_fn_t>(typeof(variance_h), $"aom_variance{w}x{h}_c");
            var s = SimdFn<aom_variance_fn_t>($"aom_variance{w}x{h}_{VarVariant(w)}");
            var fwd = CFn<aom_variance_fn_t>(typeof(variance_h), $"aom_variance{w}x{h}");
            bool hasMse = (w, h) is (16, 16) or (16, 8) or (8, 16) or (8, 8);
            var cm = hasMse ? CFn<aom_variance_fn_t>(typeof(variance_h), $"aom_mse{w}x{h}_c") : null;
            var sm = hasMse ? SimdFn<aom_variance_fn_t>($"aom_mse{w}x{h}_{(w == 16 && h == 16 ? "avx2" : "sse2")}") : null;
            for (int i = 0; i < 300; i++)
            {
                var (ma, mb) = ModesFor(rng, i);
                int sst = Stride(rng, w), rst = Stride(rng, w);
                Ptr<byte> a = Block(rng, h, w, sst, ma);
                Ptr<byte> b = Block(rng, h, w, rst, mb);
                uint eSse = 0, aSse = 0, fSse = 0;
                uint expected = c(a, sst, b, rst, ref eSse);
                Assert.Equal(expected, s(a, sst, b, rst, ref aSse));
                Assert.Equal(eSse, aSse);
                Assert.Equal(expected, fwd(a, sst, b, rst, ref fSse));
                Assert.Equal(eSse, fSse);
                if (hasMse)
                {
                    eSse = aSse = 0;
                    Assert.Equal(cm(a, sst, b, rst, ref eSse), sm(a, sst, b, rst, ref aSse));
                    Assert.Equal(eSse, aSse);
                }
            }
        }
    }

    [Fact]
    public void SubPixelVarianceSimdMatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(5505);
        foreach (var (w, h) in AllSizes)
        {
            var c = CFn<aom_subpixvariance_fn_t>(typeof(variance_h), $"aom_sub_pixel_variance{w}x{h}_c");
            var s = SimdFn<aom_subpixvariance_fn_t>($"aom_sub_pixel_variance{w}x{h}_{SubpelVariant(w)}");
            var fwd = CFn<aom_subpixvariance_fn_t>(typeof(variance_h), $"aom_sub_pixel_variance{w}x{h}");
            int cases = w * h >= 64 * 64 ? 3 : 6;
            for (int xoffset = 0; xoffset < 8; xoffset++)
            {
                for (int yoffset = 0; yoffset < 8; yoffset++)
                {
                    for (int i = 0; i < cases; i++)
                    {
                        var (ma, mb) = ModesFor(rng, rng.Next(NumModes * NumModes * 2));
                        int sst = Stride(rng, w + 1), rst = Stride(rng, w);
                        // The C bilinear filter reads (h + 1) x (w + 1) source pixels.
                        Ptr<byte> a = Block(rng, h + 1, w + 1, sst, ma);
                        Ptr<byte> b = Block(rng, h, w, rst, mb);
                        uint eSse = 0, aSse = 0, fSse = 0;
                        uint expected = c(a, sst, xoffset, yoffset, b, rst, ref eSse);
                        Assert.Equal(expected, s(a, sst, xoffset, yoffset, b, rst, ref aSse));
                        Assert.Equal(eSse, aSse);
                        Assert.Equal(expected, fwd(a, sst, xoffset, yoffset, b, rst, ref fSse));
                        Assert.Equal(eSse, fSse);
                    }
                }
            }
        }
    }

    [Fact]
    public void SubPixelAvgVarianceSimdMatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(5506);
        foreach (var (w, h) in AllSizes)
        {
            var c = CFn<aom_subp_avg_variance_fn_t>(typeof(variance_h), $"aom_sub_pixel_avg_variance{w}x{h}_c");
            var s = SimdFn<aom_subp_avg_variance_fn_t>($"aom_sub_pixel_avg_variance{w}x{h}_{SubpelAvgVariant(w)}");
            int cases = w * h >= 64 * 64 ? 3 : 6;
            for (int xoffset = 0; xoffset < 8; xoffset++)
            {
                for (int yoffset = 0; yoffset < 8; yoffset++)
                {
                    for (int i = 0; i < cases; i++)
                    {
                        var (ma, mb) = ModesFor(rng, rng.Next(NumModes * NumModes * 2));
                        int sst = Stride(rng, w + 1), rst = Stride(rng, w);
                        Ptr<byte> a = Block(rng, h + 1, w + 1, sst, ma);
                        Ptr<byte> b = Block(rng, h, w, rst, mb);
                        Ptr<byte> second = Block(rng, 1, w * h, w * h, rng.Next(NumModes));
                        uint eSse = 0, aSse = 0;
                        uint expected = c(a, sst, xoffset, yoffset, b, rst, ref eSse, second);
                        Assert.Equal(expected, s(a, sst, xoffset, yoffset, b, rst, ref aSse, second));
                        Assert.Equal(eSse, aSse);
                    }
                }
            }
        }
    }

    [Fact]
    public void CompAvgPredSimdMatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(5507);
        // Block sizes plus widths that take the `width % 64 == 0` loop and the C fallback.
        var sizes = new System.Collections.Generic.List<(int w, int h)>(AllSizes)
        {
            (192, 4), (256, 2), (4, 16), (12, 4), (24, 6),
        };
        foreach (var (w, h) in sizes)
        {
            for (int i = 0; i < 100; i++)
            {
                var (ma, mb) = ModesFor(rng, i);
                int rst = Stride(rng, w);
                Ptr<byte> pred = Block(rng, 1, w * h, w * h, ma);
                Ptr<byte> @ref = Block(rng, h, w, rst, mb);
                byte[] expected = new byte[w * h], actual = new byte[w * h];
                variance_h.aom_comp_avg_pred_c(expected, pred, w, h, @ref, rst);
                variance_avx2.aom_comp_avg_pred_avx2(actual, pred, w, h, @ref, rst);
                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void GetVarSseSumSimdMatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(5508);
        for (int i = 0; i < 2000; i++)
        {
            var (ma, mb) = ModesFor(rng, i);
            int sst = Stride(rng, 32), rst = Stride(rng, 32);

            // 8x8 quad over an 8x32 strip; the totals accumulate onto non-zero values.
            {
                Ptr<byte> a = Block(rng, 8, 32, sst, ma);
                Ptr<byte> b = Block(rng, 8, 32, rst, mb);
                uint[] eSse = new uint[4], eVar = new uint[4], sse = new uint[4], vr = new uint[4];
                int[] eSum = new int[4], sum = new int[4];
                uint eTotSse = (uint)rng.Next(), totSse = eTotSse;
                int eTotSum = rng.Next(-100000, 100000), totSum = eTotSum;
                variance_h.aom_get_var_sse_sum_8x8_quad_c(a, sst, b, rst, eSse, eSum, ref eTotSse, ref eTotSum, eVar);
                variance_avx2.aom_get_var_sse_sum_8x8_quad_avx2(a, sst, b, rst, sse, sum, ref totSse, ref totSum, vr);
                Assert.Equal(eSse, sse);
                Assert.Equal(eSum, sum);
                Assert.Equal(eVar, vr);
                Assert.Equal(eTotSse, totSse);
                Assert.Equal(eTotSum, totSum);
            }

            // 16x16 dual over a 16x32 strip.
            {
                Ptr<byte> a = Block(rng, 16, 32, sst, ma);
                Ptr<byte> b = Block(rng, 16, 32, rst, mb);
                uint[] eSse = new uint[2], eVar = new uint[2], sse = new uint[2], vr = new uint[2];
                uint eTotSse = (uint)rng.Next(), totSse = eTotSse;
                int eTotSum = rng.Next(-100000, 100000), totSum = eTotSum;
                variance_h.aom_get_var_sse_sum_16x16_dual_c(a, sst, b, rst, eSse, ref eTotSse, ref eTotSum, eVar);
                variance_avx2.aom_get_var_sse_sum_16x16_dual_avx2(a, sst, b, rst, sse, ref totSse, ref totSum, vr);
                Assert.Equal(eSse, sse);
                Assert.Equal(eVar, vr);
                Assert.Equal(eTotSse, totSse);
                Assert.Equal(eTotSum, totSum);
            }
        }
    }

    [Fact]
    public void SseSimdMatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(5509);
        // The block widths (dedicated paths) and other multiples of 4 (the default path, as
        // pickcdef's clipped 8x8 units produce); heights are multiples of 4.
        for (int w = 4; w <= 128; w += 4)
        {
            for (int h = 4; h <= 128; h += 4)
            {
                bool blockDim = (w & (w - 1)) == 0 && (h & (h - 1)) == 0;
                int cases = blockDim ? 20 : (w <= 64 && h <= 64 ? 2 : 1);
                for (int i = 0; i < cases; i++)
                {
                    var (ma, mb) = ModesFor(rng, rng.Next(NumModes * NumModes * 2));
                    int ast = Stride(rng, w), bst = Stride(rng, w);
                    Ptr<byte> a = Block(rng, h, w, ast, ma);
                    Ptr<byte> b = Block(rng, h, w, bst, mb);
                    long expected = sse.aom_sse_c(a, ast, b, bst, w, h);
                    Assert.Equal(expected, sse_avx2.aom_sse_avx2(a, ast, b, bst, w, h));
                    Assert.Equal(expected, sse.aom_sse(a, ast, b, bst, w, h));
                }
            }
        }
    }

    [Fact]
    public void SubtractBlockSimdMatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(5510);
        foreach (var (w, h) in AllSizes)
        {
            for (int i = 0; i < 200; i++)
            {
                var (ma, mb) = ModesFor(rng, i);
                int sst = Stride(rng, w), pst = Stride(rng, w), dst = Stride(rng, w);
                Ptr<byte> src = Block(rng, h, w, sst, ma);
                Ptr<byte> pred = Block(rng, h, w, pst, mb);
                int off = rng.Next(0, 48);
                int n = off + (h - 1) * dst + w;
                // Both outputs start from the same garbage so untouched values compare too.
                short[] expected = new short[n];
                for (int k = 0; k < n; k++) expected[k] = (short)rng.Next(short.MinValue, short.MaxValue);
                short[] actual = (short[])expected.Clone();
                subtract.aom_subtract_block_c(h, w, new Ptr<short>(expected, off), dst, src, sst, pred, pst);
                subtract_avx2.aom_subtract_block_avx2(h, w, new Ptr<short>(actual, off), dst, src, sst, pred, pst);
                Assert.Equal(expected, actual);
            }
        }
    }
}
