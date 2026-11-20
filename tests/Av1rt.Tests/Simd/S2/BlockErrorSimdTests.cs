using System;
using System.Runtime.Intrinsics.X86;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.av1_quantize;
using static Av1rt.Aom.avg;
using static Av1rt.Aom.error_intrin_avx2;
using static Av1rt.Aom.nonrd_opt;
using static Av1rt.Aom.rdopt;

namespace Av1rt.Tests.Simd.S2;

// Differential tests of av1_block_error_lp_avx2 (av1/encoder/x86/error_intrin_avx2.c) against
// av1_block_error_lp_c. block_size is a multiple of 16 (16, 32, then multiples of 64), and the
// SIMD version takes dqcoeff - coeff in 16 bits like libaom's: inputs keep that difference in
// int16 (true for the nonrd RD estimate, where dqcoeff is the dequantized coeff or 0).
public class BlockErrorSimdTests
{
    private static readonly int[] Sizes = { 16, 32, 64, 128, 256, 1024 };

    [Fact]
    public void BlockErrorLp_Avx2MatchesC()
    {
        if (!Avx2.IsSupported) return; // skipped: no AVX2 on this machine
        var rng = new Random(0x52E0);
        foreach (int n in Sizes)
        {
            for (int iter = 0; iter < 200; iter++)
            {
                var coeff = new short[n];
                var dqcoeff = new short[n];
                int kase = iter % 6;
                for (int i = 0; i < n; i++)
                {
                    switch (kase)
                    {
                        case 0: // small residuals
                            coeff[i] = (short)rng.Next(-256, 257);
                            dqcoeff[i] = (short)(coeff[i] + rng.Next(-16, 17));
                            break;
                        case 1: // quantized to zero: diff is -coeff
                            coeff[i] = (short)rng.Next(-short.MaxValue, short.MaxValue + 1);
                            dqcoeff[i] = 0;
                            break;
                        case 2: // extremes: four squares overflow a signed 32-bit lane
                            coeff[i] = (short)(rng.Next(2) == 0 ? short.MaxValue : -short.MaxValue);
                            dqcoeff[i] = 0;
                            break;
                        case 3: // large coefficients, dequantized within a step of 2048
                            coeff[i] = (short)rng.Next(-30000, 30001);
                            dqcoeff[i] = (short)(coeff[i] + rng.Next(-2048, 2049));
                            break;
                        case 4:
                            coeff[i] = 0;
                            dqcoeff[i] = 0;
                            break;
                        default: // any pair whose difference fits in int16
                            coeff[i] = (short)rng.Next(short.MinValue, short.MaxValue + 1);
                            int d = rng.Next(-short.MaxValue, short.MaxValue + 1);
                            dqcoeff[i] = (short)Math.Clamp(coeff[i] + d, short.MinValue, short.MaxValue);
                            break;
                    }
                }
                long expected = av1_block_error_lp_c(coeff, dqcoeff, n);
                long actual = av1_block_error_lp_avx2(coeff, dqcoeff, n);
                Assert.True(expected == actual, $"block_error_lp n={n} iter={iter}: C={expected} SIMD={actual}");
            }
        }
    }

    // The real producer: Hadamard coefficients of 8-bit residuals, quantized by av1_quantize_lp
    // with the av1_build_quantizer *_fp tables.
    [Fact]
    public void BlockErrorLp_Avx2MatchesC_QuantizedHadamard()
    {
        if (!Avx2.IsSupported) return; // skipped: no AVX2 on this machine
        var rng = new Random(0x52E1);
        var residual = new short[256];
        foreach (S2QuantTables t in S2QuantTables.All)
        {
            for (int q = 0; q < 256; q++)
            {
                var (_, _, _, _, dequant, roundFp, quantFp) = t.Plane(q % 3, q);
                for (int is16 = 0; is16 < 2; is16++)
                {
                    int w = is16 != 0 ? 16 : 8, n = w * w;
                    int amp = (q & 1) != 0 ? 255 : 32;
                    for (int i = 0; i < n; i++) residual[i] = (short)rng.Next(-amp, amp + 1);
                    var coeff = new short[n];
                    var qcoeff = new short[n];
                    var dqcoeff = new short[n];
                    var eob = new ushort[1];
                    if (is16 != 0)
                    {
                        aom_hadamard_lp_16x16_c(residual, w, coeff);
                        av1_quantize_lp_c(coeff, n, roundFp, quantFp, qcoeff, dqcoeff, dequant, eob,
                                          default_scan_lp_16x16_transpose,
                                          av1_default_iscan_lp_16x16_transpose);
                    }
                    else
                    {
                        aom_hadamard_lp_8x8_c(residual, w, coeff);
                        av1_quantize_lp_c(coeff, n, roundFp, quantFp, qcoeff, dqcoeff, dequant, eob,
                                          default_scan_8x8_transpose, av1_default_iscan_8x8_transpose);
                    }
                    for (int bs = 16; bs <= n; bs *= 4)
                    {
                        long expected = av1_block_error_lp_c(coeff, dqcoeff, bs);
                        long actual = av1_block_error_lp_avx2(coeff, dqcoeff, bs);
                        Assert.True(expected == actual,
                                    $"block_error_lp hadamard n={n} bs={bs} tables={t.Name} q={q}: C={expected} SIMD={actual}");
                    }
                }
            }
        }
    }
}
