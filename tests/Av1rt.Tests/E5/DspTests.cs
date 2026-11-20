using System;
using System.Reflection;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.avg;
using static Av1rt.Aom.sse;
using static Av1rt.Aom.subtract;
using static Av1rt.Aom.variance_h;

namespace Av1rt.Tests.E5;

// Differential tests of the aom_dsp kernels of work package e5 (sad.c, variance.c, avg.c, sse.c,
// subtract.c) against libaom's C code (shim_e5.c), for every block size, random and extreme
// pixels and strides that are not the block width.
public class DspTests
{
    // Block sizes compiled with CONFIG_REALTIME_ONLY.
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
    private static readonly (int w, int h)[] MseSizes = { (16, 16), (16, 8), (8, 16), (8, 8) };

    // Extra stride beyond the block width: 0 (packed) and odd values.
    private static readonly int[] StrideExtras = { 0, 1, 3, 17 };

    // Pixel content: 0 random, 1 all zero, 2 all 255, 3 random 0/255.
    private static readonly (int a, int b)[] Modes = { (0, 0), (1, 2), (2, 1), (3, 3), (2, 2), (0, 2) };

    internal static byte[] Pixels(Random rng, int n, int mode)
    {
        var p = new byte[n];
        switch (mode)
        {
            case 0: rng.NextBytes(p); break;
            case 1: break;
            case 2: Array.Fill(p, (byte)255); break;
            default:
                for (int i = 0; i < n; i++) p[i] = (byte)(rng.Next(2) * 255);
                break;
        }
        return p;
    }

    private static T Fn<T>(Type type, string name) where T : Delegate
    {
        MethodInfo m = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.True(m != null, $"{type.Name}.{name} is missing");
        return (T)m.CreateDelegate(typeof(T));
    }

    [Fact]
    public void SadMatchesLibaom()
    {
        var rng = new Random(501);
        foreach (var (w, h) in AllSizes)
        {
            var sdf = Fn<aom_sad_fn_t>(typeof(sad_h), $"aom_sad{w}x{h}");
            bool hasAvg = Array.IndexOf(AvgSizes, (w, h)) >= 0;
            bool hasSkip = Array.IndexOf(SkipSizes, (w, h)) >= 0;
            var sdaf = hasAvg ? Fn<aom_sad_avg_fn_t>(typeof(sad_h), $"aom_sad{w}x{h}_avg") : null;
            var sdsf = hasSkip ? Fn<aom_sad_fn_t>(typeof(sad_h), $"aom_sad_skip_{w}x{h}") : null;
            foreach (int extra in StrideExtras)
            {
                foreach (var (ma, mb) in Modes)
                {
                    int ss = w + extra, rs = w + 2 * extra + 1;
                    byte[] src = Pixels(rng, ss * h, ma);
                    byte[] @ref = Pixels(rng, rs * h, mb);
                    byte[] second = Pixels(rng, w * h, ma);

                    uint expected = E5Shim.aomshim_e5_sad(w, h, src, ss, @ref, rs);
                    Assert.Equal(expected, sdf(src, ss, @ref, rs));
                    if (hasAvg)
                    {
                        expected = E5Shim.aomshim_e5_sad_avg(w, h, src, ss, @ref, rs, second);
                        Assert.Equal(expected, sdaf(src, ss, @ref, rs, second));
                    }
                    if (hasSkip)
                    {
                        expected = E5Shim.aomshim_e5_sad_skip(w, h, src, ss, @ref, rs);
                        Assert.Equal(expected, sdsf(src, ss, @ref, rs));
                    }
                }
            }
        }
    }

    [Fact]
    public void SadMultiMatchesLibaom()
    {
        var rng = new Random(502);
        string[] names = { "aom_sad{0}x{1}x4d", "aom_sad{0}x{1}x3d", "aom_sad_skip_{0}x{1}x4d" };
        for (int kind = 0; kind < 3; kind++)
        {
            foreach (var (w, h) in kind == 2 ? SkipSizes : AllSizes)
            {
                var fn = Fn<aom_sad_multi_d_fn_t>(typeof(sad_h), string.Format(names[kind], w, h));
                foreach (int extra in StrideExtras)
                {
                    foreach (var (ma, mb) in Modes)
                    {
                        int ss = w + extra, rs = w + 2 * extra + 5;
                        byte[] src = Pixels(rng, ss * h, ma);
                        // One reference area with room for 4 block positions.
                        byte[] @ref = Pixels(rng, rs * (h + 8) + 8, mb);
                        int[] offsets = new int[4];
                        for (int i = 0; i < 4; i++) offsets[i] = rng.Next(8) * rs + rng.Next(8);

                        uint[] expected = new uint[4];
                        Assert.Equal(1, E5Shim.aomshim_e5_sad_multi(kind, w, h, src, ss, @ref, offsets, rs, expected));

                        var refs = new Ptr<byte>[4];
                        for (int i = 0; i < 4; i++) refs[i] = new Ptr<byte>(@ref, offsets[i]);
                        uint[] actual = new uint[4];
                        fn(src, ss, refs, rs, actual);
                        Assert.Equal(expected, actual);
                    }
                }
            }
        }
    }

    [Fact]
    public void VarianceAndMseMatchLibaom()
    {
        var rng = new Random(503);
        foreach (var (w, h) in AllSizes)
        {
            var vf = Fn<aom_variance_fn_t>(typeof(variance_h), $"aom_variance{w}x{h}");
            bool hasMse = Array.IndexOf(MseSizes, (w, h)) >= 0;
            var msef = hasMse ? Fn<aom_variance_fn_t>(typeof(variance_h), $"aom_mse{w}x{h}") : null;
            foreach (int extra in StrideExtras)
            {
                foreach (var (ma, mb) in Modes)
                {
                    int ss = w + extra, rs = w + 2 * extra + 1;
                    byte[] a = Pixels(rng, ss * h, ma);
                    byte[] b = Pixels(rng, rs * h, mb);

                    uint expected = E5Shim.aomshim_e5_variance(w, h, a, ss, b, rs, out uint expectedSse);
                    uint sse = 0;
                    Assert.Equal(expected, vf(a, ss, b, rs, ref sse));
                    Assert.Equal(expectedSse, sse);
                    if (hasMse)
                    {
                        expected = E5Shim.aomshim_e5_mse(w, h, a, ss, b, rs, out expectedSse);
                        sse = 0;
                        Assert.Equal(expected, msef(a, ss, b, rs, ref sse));
                        Assert.Equal(expectedSse, sse);
                    }
                }
            }
        }
    }

    [Fact]
    public void SubPixelVarianceMatchesLibaom()
    {
        var rng = new Random(504);
        foreach (var (w, h) in AllSizes)
        {
            var svf = Fn<aom_subpixvariance_fn_t>(typeof(variance_h), $"aom_sub_pixel_variance{w}x{h}");
            var svaf = Fn<aom_subp_avg_variance_fn_t>(typeof(variance_h), $"aom_sub_pixel_avg_variance{w}x{h}");
            for (int xoffset = 0; xoffset < 8; xoffset++)
            {
                for (int yoffset = 0; yoffset < 8; yoffset++)
                {
                    var (ma, mb) = Modes[(xoffset * 8 + yoffset) % Modes.Length];
                    int extra = StrideExtras[(xoffset + yoffset) % StrideExtras.Length];
                    int ss = w + extra + 1, rs = w + 2 * extra;
                    // The bilinear filter reads one row and one column beyond the block.
                    byte[] a = Pixels(rng, ss * (h + 1) + 1, ma);
                    byte[] b = Pixels(rng, rs * h, mb);
                    byte[] second = Pixels(rng, w * h, mb);

                    uint expected = E5Shim.aomshim_e5_sub_pixel_variance(w, h, a, ss, xoffset, yoffset, b, rs, out uint expectedSse);
                    uint sse = 0;
                    Assert.Equal(expected, svf(a, ss, xoffset, yoffset, b, rs, ref sse));
                    Assert.Equal(expectedSse, sse);

                    expected = E5Shim.aomshim_e5_sub_pixel_avg_variance(w, h, a, ss, xoffset, yoffset, b, rs, out expectedSse, second);
                    sse = 0;
                    Assert.Equal(expected, svaf(a, ss, xoffset, yoffset, b, rs, ref sse, second));
                    Assert.Equal(expectedSse, sse);
                }
            }
        }
    }

    [Fact]
    public void CompAvgPredMatchesLibaom()
    {
        var rng = new Random(505);
        foreach (var (w, h) in AllSizes)
        {
            foreach (var (ma, mb) in Modes)
            {
                int rs = w + 7;
                byte[] pred = Pixels(rng, w * h, ma);
                byte[] @ref = Pixels(rng, rs * h, mb);
                byte[] expected = new byte[w * h];
                E5Shim.aomshim_e5_comp_avg_pred(expected, pred, w, h, @ref, rs);
                byte[] actual = new byte[w * h];
                aom_comp_avg_pred(actual, pred, w, h, @ref, rs);
                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void GetVarSseSumMatchesLibaom()
    {
        var rng = new Random(506);
        foreach (int extra in StrideExtras)
        {
            foreach (var (ma, mb) in Modes)
            {
                int ss = 32 + extra, rs = 32 + 2 * extra + 1;
                byte[] a = Pixels(rng, ss * 16, ma);
                byte[] b = Pixels(rng, rs * 16, mb);

                // 8x8 quad over an 8x32 strip; the totals accumulate onto non-zero values.
                uint[] eSse = new uint[4], eVar = new uint[4];
                int[] eSum = new int[4];
                uint eTotSse = 1234;
                int eTotSum = -77;
                E5Shim.aomshim_e5_get_var_sse_sum_8x8_quad(a, ss, b, rs, eSse, eSum, ref eTotSse, ref eTotSum, eVar);
                uint[] sse = new uint[4], vr = new uint[4];
                int[] sum = new int[4];
                uint totSse = 1234;
                int totSum = -77;
                aom_get_var_sse_sum_8x8_quad(a, ss, b, rs, sse, sum, ref totSse, ref totSum, vr);
                Assert.Equal(eSse, sse);
                Assert.Equal(eSum, sum);
                Assert.Equal(eVar, vr);
                Assert.Equal(eTotSse, totSse);
                Assert.Equal(eTotSum, totSum);

                // 16x16 dual over a 16x32 strip.
                eSse = new uint[2];
                eVar = new uint[2];
                eTotSse = 99;
                eTotSum = 5;
                E5Shim.aomshim_e5_get_var_sse_sum_16x16_dual(a, ss, b, rs, eSse, ref eTotSse, ref eTotSum, eVar);
                sse = new uint[2];
                vr = new uint[2];
                totSse = 99;
                totSum = 5;
                aom_get_var_sse_sum_16x16_dual(a, ss, b, rs, sse, ref totSse, ref totSum, vr);
                Assert.Equal(eSse, sse);
                Assert.Equal(eVar, vr);
                Assert.Equal(eTotSse, totSse);
                Assert.Equal(eTotSum, totSum);
            }
        }
    }

    [Fact]
    public void AvgMatchesLibaom()
    {
        var rng = new Random(507);
        foreach (int extra in StrideExtras)
        {
            for (int mode = 0; mode < 4; mode++)
            {
                int p = 32 + extra;
                byte[] s = Pixels(rng, p * 32, mode);
                for (int y = 0; y < 16; y += 3)
                {
                    for (int x = 0; x < 16; x += 5)
                    {
                        int off = y * p + x;
                        byte[] blk = s.AsSpan(off).ToArray();
                        Assert.Equal(E5Shim.aomshim_e5_avg_4x4(blk, p), aom_avg_4x4(new Ptr<byte>(s, off), p));
                        Assert.Equal(E5Shim.aomshim_e5_avg_8x8(blk, p), aom_avg_8x8(new Ptr<byte>(s, off), p));
                        int[] expected = new int[4], actual = new int[4];
                        E5Shim.aomshim_e5_avg_8x8_quad(s, p, x, y, expected);
                        aom_avg_8x8_quad(s, p, x, y, actual);
                        Assert.Equal(expected, actual);
                    }
                }
            }
        }
    }

    // Residuals as the encoder produces them: src - pred in [-255, 255].
    private static short[] Residuals(Random rng, int n, int mode)
    {
        var d = new short[n];
        for (int i = 0; i < n; i++)
        {
            d[i] = mode switch
            {
                0 => (short)rng.Next(-255, 256),
                1 => 255,
                2 => -255,
                _ => (short)(rng.Next(2) == 0 ? -255 : 255),
            };
        }
        return d;
    }

    [Fact]
    public void HadamardAndSatdMatchLibaom()
    {
        var rng = new Random(508);
        int[] strides = { 8, 16, 19, 32, 64 };
        for (int kind = 0; kind < 3; kind++)
        {
            int bw = kind == 0 ? 8 : 16, bh = kind == 2 ? 16 : 8, n = bw * bh;
            foreach (int stride in strides)
            {
                if (stride < bw) continue;
                for (int mode = 0; mode < 4; mode++)
                {
                    short[] src = Residuals(rng, stride * bh, mode);
                    short[] expected = new short[n];
                    E5Shim.aomshim_e5_hadamard_lp(kind, src, stride, expected);
                    short[] actual = new short[n];
                    switch (kind)
                    {
                        case 0: aom_hadamard_lp_8x8(src, stride, actual); break;
                        case 1: aom_hadamard_lp_8x8_dual(src, stride, actual); break;
                        default: aom_hadamard_lp_16x16(src, stride, actual); break;
                    }
                    Assert.Equal(expected, actual);

                    foreach (int length in new[] { 16, 64, n })
                    {
                        if (length > n) continue;
                        Assert.Equal(E5Shim.aomshim_e5_satd_lp(expected, length), aom_satd_lp(actual, length));
                    }
                }
            }
        }

        // satd_lp on extreme coefficients up to the 1024-coefficient range.
        foreach (int length in new[] { 16, 32, 64, 128, 256, 512, 1024 })
        {
            short[] coeff = new short[length];
            for (int i = 0; i < length; i++) coeff[i] = (short)(rng.Next(2) == 0 ? -32640 : rng.Next(-32640, 32641));
            Assert.Equal(E5Shim.aomshim_e5_satd_lp(coeff, length), aom_satd_lp(coeff, length));
        }
    }

    [Fact]
    public void IntProAndVectorVarMatchLibaom()
    {
        var rng = new Random(509);
        int[] dims = { 16, 32, 64, 128 };
        foreach (int width in dims)
        {
            foreach (int height in dims)
            {
                for (int mode = 0; mode < 4; mode++)
                {
                    int stride = width + 9;
                    byte[] @ref = Pixels(rng, stride * height, mode);
                    foreach (int norm in new[] { 0, 2, 3, 4, 5, 6 })
                    {
                        // [0, 32640] must stay in int16 before the shift, as in the encoder.
                        short[] eh = new short[width], ah = new short[width];
                        E5Shim.aomshim_e5_int_pro_row(eh, @ref, stride, width, height, norm);
                        aom_int_pro_row(ah, @ref, stride, width, height, norm);
                        Assert.Equal(eh, ah);

                        short[] ev = new short[height], av = new short[height];
                        E5Shim.aomshim_e5_int_pro_col(ev, @ref, stride, width, height, norm);
                        aom_int_pro_col(av, @ref, stride, width, height, norm);
                        Assert.Equal(ev, av);
                    }
                }
            }
        }

        for (int bwl = 2; bwl <= 5; bwl++)
        {
            int width = 4 << bwl;
            for (int mode = 0; mode < 5; mode++)
            {
                short[] r = new short[width], s = new short[width];
                for (int i = 0; i < width; i++)
                {
                    (r[i], s[i]) = mode switch
                    {
                        0 => ((short)rng.Next(511), (short)rng.Next(511)),
                        1 => ((short)510, (short)0),
                        2 => ((short)0, (short)510),
                        3 => ((short)(rng.Next(2) * 510), (short)(rng.Next(2) * 510)),
                        _ => ((short)rng.Next(511), (short)rng.Next(511)),
                    };
                }
                Assert.Equal(E5Shim.aomshim_e5_vector_var(r, s, bwl), aom_vector_var(r, s, bwl));
            }
        }
    }

    [Fact]
    public void SseMatchesLibaom()
    {
        var rng = new Random(510);
        int[] dims = { 1, 4, 7, 8, 13, 16, 32, 33, 64, 128 };
        foreach (int width in dims)
        {
            foreach (int height in dims)
            {
                foreach (var (ma, mb) in Modes)
                {
                    int sa = width + 3, sb = width + 11;
                    byte[] a = Pixels(rng, sa * height, ma);
                    byte[] b = Pixels(rng, sb * height, mb);
                    Assert.Equal(E5Shim.aomshim_e5_sse(a, sa, b, sb, width, height),
                                 aom_sse(a, sa, b, sb, width, height));
                }
            }
        }
    }

    [Fact]
    public void SubtractBlockMatchesLibaom()
    {
        var rng = new Random(511);
        int[] dims = { 4, 8, 16, 32, 64, 128, 5, 11 };
        foreach (int rows in dims)
        {
            foreach (int cols in dims)
            {
                foreach (var (ma, mb) in Modes)
                {
                    int ss = cols + 1, ps = cols + 5, ds = cols + 2;
                    byte[] src = Pixels(rng, ss * rows, ma);
                    byte[] pred = Pixels(rng, ps * rows, mb);
                    short[] expected = new short[ds * rows];
                    short[] actual = new short[ds * rows];
                    for (int i = 0; i < expected.Length; i++) expected[i] = actual[i] = (short)i;
                    E5Shim.aomshim_e5_subtract_block(rows, cols, expected, ds, src, ss, pred, ps);
                    aom_subtract_block(rows, cols, actual, ds, src, ss, pred, ps);
                    Assert.Equal(expected, actual);
                }
            }
        }
    }
}
