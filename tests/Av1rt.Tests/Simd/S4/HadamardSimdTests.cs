using System;
using System.Runtime.Intrinsics.X86;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.avg;
using static Av1rt.Aom.avg_intrin_avx2;
using static Av1rt.Aom.avg_intrin_sse2;

namespace Av1rt.Tests.Simd.S4;

// Differential tests of the s4 Hadamard / SATD SIMD ports against the _c ports:
// aom_hadamard_lp_8x8_sse2, aom_hadamard_lp_8x8_dual_avx2, aom_hadamard_lp_16x16_avx2,
// aom_satd_lp_avx2.
public class HadamardSimdTests
{
    // Residual content: 0 random [-255, 255], 1 all -255, 2 all 255, 3 random +-255,
    // 4 checkerboard +-255 (maximizes the high-frequency outputs), 5 random small [-4, 4],
    // 6 random int16 (full range; only for the kernels without a rounding shift).
    internal static void FillResidual(Random rng, short[] buf, int mode)
    {
        for (int i = 0; i < buf.Length; i++)
        {
            buf[i] = mode switch
            {
                0 => (short)rng.Next(-255, 256),
                1 => -255,
                2 => 255,
                3 => (short)(rng.Next(2) == 0 ? -255 : 255),
                4 => (short)(((i + i / 7) & 1) == 0 ? -255 : 255),
                5 => (short)rng.Next(-4, 5),
                _ => (short)rng.Next(short.MinValue, short.MaxValue + 1),
            };
        }
    }

    private delegate void HadamardFn(Ptr<short> src_diff, int src_stride, Ptr<short> coeff);

    private static void Run(HadamardFn simd, HadamardFn c, int bw, int bh, int ncoeff, int maxMode, int seed)
    {
        var rng = new Random(seed);
        for (int iter = 0; iter < 4000; iter++)
        {
            int mode = iter < 8 * (maxMode + 1) ? iter % (maxMode + 1) : rng.Next(maxMode + 1);
            int stride = bw + (rng.Next(3) == 0 ? 0 : rng.Next(1, 40));
            int off = rng.Next(0, 9);
            var src = new short[off + (bh - 1) * stride + bw];
            FillResidual(rng, src, mode);
            int coff = rng.Next(0, 5);
            var want = new short[coff + ncoeff];
            var got = new short[coff + ncoeff];
            c(new Ptr<short>(src, off), stride, new Ptr<short>(want, coff));
            simd(new Ptr<short>(src, off), stride, new Ptr<short>(got, coff));
            for (int i = 0; i < want.Length; i++)
                Assert.True(want[i] == got[i], $"iter {iter} mode {mode} stride {stride}: coeff[{i - coff}] {got[i]} vs C {want[i]}");
        }
    }

    [Fact]
    public void HadamardLp8x8Sse2MatchesC()
    {
        if (!Avx2.IsSupported) return;
        // Only adds and subtracts in 16 bits: bit-exact for any int16 input.
        Run(aom_hadamard_lp_8x8_sse2, aom_hadamard_lp_8x8_c, 8, 8, 64, 6, 401);
    }

    [Fact]
    public void HadamardLp8x8DualAvx2MatchesC()
    {
        if (!Avx2.IsSupported) return;
        Run(aom_hadamard_lp_8x8_dual_avx2, aom_hadamard_lp_8x8_dual_c, 16, 8, 128, 6, 402);
    }

    [Fact]
    public void HadamardLp16x16Avx2MatchesC()
    {
        if (!Avx2.IsSupported) return;
        // The final (a0 + a1) >> 1 stage is computed in 32 bits by C and in 16 bits by SIMD, so
        // they agree for residuals (8-bit: [-255, 255]), as libaom's hadamard_test checks.
        Run(aom_hadamard_lp_16x16_avx2, aom_hadamard_lp_16x16_c, 16, 16, 256, 5, 403);
    }

    [Fact]
    public void SatdLpAvx2MatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(404);
        for (int iter = 0; iter < 6000; iter++)
        {
            int length = 16 * rng.Next(1, 65);  // 16 .. 1024
            int mode = iter < 32 ? iter % 4 : rng.Next(4);
            int off = rng.Next(0, 9);
            var coeff = new short[off + length];
            for (int i = 0; i < length; i++)
            {
                coeff[off + i] = mode switch
                {
                    // The encoder's coefficients are |c| <= 32767 (abs_epi16(-32768) is negative).
                    0 => (short)rng.Next(-32767, 32768),
                    1 => (short)(rng.Next(2) == 0 ? -32767 : 32767),
                    2 => (short)rng.Next(-300, 301),
                    _ => (short)(rng.Next(4) == 0 ? rng.Next(-32767, 32768) : 0),
                };
            }
            var p = new Ptr<short>(coeff, off);
            Assert.Equal(aom_satd_lp_c(p, length), aom_satd_lp_avx2(p, length));
        }
    }

    [Fact]
    public void HadamardThenSatdDispatchMatchesC()
    {
        if (!Avx2.IsSupported) return;
        // The av1_block_yrd chain: 16x16 Hadamard of a residual, then SATD of the result.
        var rng = new Random(405);
        for (int iter = 0; iter < 2000; iter++)
        {
            int stride = 16 * rng.Next(1, 9);
            var src = new short[15 * stride + 16];
            FillResidual(rng, src, iter % 6);
            var a = new short[256];
            var b = new short[256];
            aom_hadamard_lp_16x16_c(src, stride, a);
            aom_hadamard_lp_16x16(src, stride, b);
            Assert.Equal(a, b);
            Assert.Equal(aom_satd_lp_c(a, 256), aom_satd_lp(b, 256));
        }
    }
}
