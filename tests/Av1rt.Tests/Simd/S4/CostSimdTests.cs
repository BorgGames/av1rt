using System;
using System.Runtime.Intrinsics.X86;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.cost;
using static Av1rt.Aom.cost_avx2;

namespace Av1rt.Tests.Simd.S4;

// Differential tests of av1_cost_tokens_from_cdf_{2,4}_batch_avx2 against the _c ports on
// valid CDF tables (inverse CDFs non-increasing down to the 0 terminator, plus the counter).
public class CostSimdTests
{
    private const int CDF_PROB_TOP = 32768;

    // One CDF of `nsyms` symbols in libaom's inverse form: cdf[0..nsyms-2] non-increasing in
    // [0, CDF_PROB_TOP], cdf[nsyms-1] = 0 (terminator), then any counter value.
    private static void FillCdf(Random rng, ushort[] cdf, int at, int nsyms, int mode)
    {
        var v = new int[nsyms - 1];
        for (int i = 0; i < v.Length; i++)
        {
            v[i] = mode switch
            {
                0 => rng.Next(0, CDF_PROB_TOP + 1),
                // Probabilities near EC_MIN_PROB and near 1.
                1 => rng.Next(2) == 0 ? rng.Next(0, 16) : CDF_PROB_TOP - rng.Next(0, 16),
                // Exact powers of two (the float-exponent log2) and their neighbours.
                2 => Math.Clamp((1 << rng.Next(0, 16)) + rng.Next(-1, 2), 0, CDF_PROB_TOP),
                // Below COST_LUT_LOW (the scalar path's lookup table) and around it.
                3 => rng.Next(0, 4200),
                _ => rng.Next(2) == 0 ? 0 : CDF_PROB_TOP,
            };
        }
        Array.Sort(v);
        Array.Reverse(v);
        for (int i = 0; i < v.Length; i++) cdf[at + i] = (ushort)v[i];
        cdf[at + nsyms - 1] = 0;
    }

    private delegate void BatchFn(Ptr<int> costs, int costs_stride, Ptr<ushort> cdf, int cdf_stride, int n);

    private static void Run(BatchFn simd, BatchFn c, int nsyms, int[] costStrides, int seed)
    {
        av1_init_cost_luts();
        var rng = new Random(seed);
        for (int iter = 0; iter < 4000; iter++)
        {
            int n = iter < 40 ? iter + 1 : rng.Next(1, 120);
            int cdf_stride = nsyms + 1 + (rng.Next(4) == 0 ? rng.Next(1, 4) : 0);
            int costs_stride = costStrides[rng.Next(costStrides.Length)];
            int mode = rng.Next(5);
            int coff = rng.Next(0, 9);
            // The table ends right after the last CDF's counter: the gathers must stay in bounds.
            var cdf = new ushort[coff + (n - 1) * cdf_stride + nsyms + 1];
            for (int k = 0; k < n; k++)
            {
                FillCdf(rng, cdf, coff + k * cdf_stride, nsyms, mode);
                cdf[coff + k * cdf_stride + nsyms] = (ushort)rng.Next(0, 33);
            }
            int ooff = rng.Next(0, 9);
            var want = new int[ooff + (n - 1) * costs_stride + nsyms];
            for (int i = 0; i < want.Length; i++) want[i] = rng.Next();
            var got = (int[])want.Clone();
            c(new Ptr<int>(want, ooff), costs_stride, new Ptr<ushort>(cdf, coff), cdf_stride, n);
            simd(new Ptr<int>(got, ooff), costs_stride, new Ptr<ushort>(cdf, coff), cdf_stride, n);
            Assert.Equal(want, got);
        }
    }

    [Fact]
    public void CostTokensFromCdf2BatchAvx2MatchesC()
    {
        if (!Avx2.IsSupported) return;
        Run(av1_cost_tokens_from_cdf_2_batch_avx2, av1_cost_tokens_from_cdf_2_batch_c, 2,
            new[] { 2, 3, 4 }, 431);
    }

    [Fact]
    public void CostTokensFromCdf4BatchAvx2MatchesC()
    {
        if (!Avx2.IsSupported) return;
        Run(av1_cost_tokens_from_cdf_4_batch_avx2, av1_cost_tokens_from_cdf_4_batch_c, 4,
            new[] { 4, 5, 8 }, 432);
    }
}
