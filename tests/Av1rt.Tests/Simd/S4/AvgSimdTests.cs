using System;
using System.Runtime.Intrinsics.X86;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.avg;
using static Av1rt.Aom.avg_intrin_avx2;
using static Av1rt.Aom.avg_intrin_sse2;

namespace Av1rt.Tests.Simd.S4;

// Differential tests of the s4 block-average and projection SIMD ports against the _c ports:
// aom_avg_4x4_sse2, aom_avg_8x8_sse2, aom_avg_8x8_quad_avx2, aom_int_pro_row_avx2 (and the
// aom_int_pro_row_sse2 it falls back to), aom_int_pro_col_avx2, aom_vector_var_avx2.
public class AvgSimdTests
{
    // Pixel content: 0 random, 1 all zero, 2 all 255, 3 random 0/255, 4 random near 255.
    private static byte[] Pixels(Random rng, int n, int mode)
    {
        var p = new byte[n];
        switch (mode)
        {
            case 0: rng.NextBytes(p); break;
            case 1: break;
            case 2: Array.Fill(p, (byte)255); break;
            case 3:
                for (int i = 0; i < n; i++) p[i] = (byte)(rng.Next(2) * 255);
                break;
            default:
                for (int i = 0; i < n; i++) p[i] = (byte)rng.Next(240, 256);
                break;
        }
        return p;
    }

    [Fact]
    public void Avg4x4And8x8Sse2MatchC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(411);
        for (int iter = 0; iter < 10000; iter++)
        {
            int stride = 8 + rng.Next(0, 70);
            int off = rng.Next(0, 33);
            byte[] buf = Pixels(rng, off + 7 * stride + 8, iter < 20 ? iter % 5 : rng.Next(5));
            var s = new Ptr<byte>(buf, off);
            Assert.Equal(aom_avg_8x8_c(s, stride), aom_avg_8x8_sse2(s, stride));
            // 4x4 at the end of the buffer too (the loads must stay in bounds).
            var s4 = new Ptr<byte>(buf, buf.Length - 3 * stride - 4);
            Assert.Equal(aom_avg_4x4_c(s, stride), aom_avg_4x4_sse2(s, stride));
            Assert.Equal(aom_avg_4x4_c(s4, stride), aom_avg_4x4_sse2(s4, stride));
        }
    }

    [Fact]
    public void Avg8x8QuadAvx2MatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(412);
        for (int iter = 0; iter < 10000; iter++)
        {
            int x16_idx = 16 * rng.Next(0, 5), y16_idx = 16 * rng.Next(0, 5);
            int stride = x16_idx + 16 + rng.Next(0, 40);
            int off = rng.Next(0, 33);
            byte[] buf = Pixels(rng, off + (y16_idx + 15) * stride + x16_idx + 16,
                                iter < 20 ? iter % 5 : rng.Next(5));
            var s = new Ptr<byte>(buf, off);
            var want = new int[4];
            var got = new int[4];
            aom_avg_8x8_quad_c(s, stride, x16_idx, y16_idx, want);
            aom_avg_8x8_quad_avx2(s, stride, x16_idx, y16_idx, got);
            Assert.Equal(want, got);
        }
    }

    [Fact]
    public void IntProRowAvx2MatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(413);
        for (int iter = 0; iter < 6000; iter++)
        {
            // av1_int_pro_motion_estimation: width = left + right + bw (multiples of 16), height =
            // bh, norm_factor = mi_size_high_log2 + 1. Odd multiples of 16 take the sse2 path.
            int width = 16 * rng.Next(1, 25);
            int height = iter % 3 == 0 ? 2 * rng.Next(1, 65) : 4 << rng.Next(2, 6);
            int norm_factor = iter % 3 == 0 ? rng.Next(0, 8) : System.Numerics.BitOperations.Log2((uint)height) - 1;
            int stride = width + rng.Next(0, 50);
            int off = rng.Next(0, 33);
            byte[] buf = Pixels(rng, off + (height - 1) * stride + width, iter < 20 ? iter % 5 : rng.Next(5));
            var want = new short[width + 2];
            var got = new short[width + 2];
            aom_int_pro_row_c(new Ptr<short>(want, 1), new Ptr<byte>(buf, off), stride, width, height, norm_factor);
            aom_int_pro_row_avx2(new Ptr<short>(got, 1), new Ptr<byte>(buf, off), stride, width, height, norm_factor);
            Assert.Equal(want, got);
        }
    }

    [Fact]
    public void IntProColAvx2MatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(414);
        int[] widths = { 16, 32, 64, 128 };
        for (int iter = 0; iter < 6000; iter++)
        {
            int width = widths[iter % 4];
            // 8 rows per step for width 16, 4 rows otherwise (the encoder's heights are
            // top + bottom + bh with top/bottom multiples of 16).
            int height = width == 16 ? 8 * rng.Next(1, 41) : 4 * rng.Next(1, 81);
            int norm_factor = iter % 2 == 0 ? 3 + (width >> 5) : rng.Next(0, 9);
            int stride = width + rng.Next(0, 50);
            int off = rng.Next(0, 33);
            byte[] buf = Pixels(rng, off + (height - 1) * stride + width, iter < 20 ? iter % 5 : rng.Next(5));
            var want = new short[height + 2];
            var got = new short[height + 2];
            aom_int_pro_col_c(new Ptr<short>(want, 1), new Ptr<byte>(buf, off), stride, width, height, norm_factor);
            aom_int_pro_col_avx2(new Ptr<short>(got, 1), new Ptr<byte>(buf, off), stride, width, height, norm_factor);
            Assert.Equal(want, got);
        }
    }

    [Fact]
    public void VectorVarAvx2MatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(415);
        for (int iter = 0; iter < 20000; iter++)
        {
            int bwl = 2 + iter % 4;
            int width = 4 << bwl;
            int mode = iter < 40 ? (iter / 4) % 5 : rng.Next(5);
            int roff = rng.Next(0, 40), soff = rng.Next(0, 9);
            var r = new short[roff + width];
            var s = new short[soff + width];
            for (int i = 0; i < width; i++)
            {
                // ref, src: [0, 510] (projections of 8-bit pixels).
                switch (mode)
                {
                    case 0: r[roff + i] = (short)rng.Next(0, 511); s[soff + i] = (short)rng.Next(0, 511); break;
                    case 1: r[roff + i] = 510; s[soff + i] = 0; break;
                    case 2: r[roff + i] = 0; s[soff + i] = 510; break;
                    case 3: r[roff + i] = (short)(rng.Next(2) * 510); s[soff + i] = (short)(rng.Next(2) * 510); break;
                    default: r[roff + i] = (short)rng.Next(200, 230); s[soff + i] = (short)rng.Next(200, 230); break;
                }
            }
            var rp = new Ptr<short>(r, roff);
            var sp = new Ptr<short>(s, soff);
            Assert.Equal(aom_vector_var_c(rp, sp, bwl), aom_vector_var_avx2(rp, sp, bwl));
        }
    }
}
