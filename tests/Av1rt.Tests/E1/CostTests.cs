using System;
using System.Collections.Generic;
using System.Linq;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.cost;
using static Av1rt.Aom.entropy;
using static Av1rt.Aom.entropymode;
using static Av1rt.Aom.entropymv;

namespace Av1rt.Tests.E1;

/// <summary>cost.c against libaom, on every default CDF.</summary>
[Trait("Category", "Native")]
public class CostTests
{
    public CostTests()
    {
        // The low-probability costs come from av1_cost_lut_low, which av1_init_cost_luts fills
        // (av1_initialize_enc does it in the encoder). Initialise it on both sides, otherwise the
        // results depend on which tests ran first.
        av1_init_cost_luts();
        E1Shim.aomshim_e1_cost_lut_low(new ushort[COST_LUT_LOW]);
    }

    private static FRAME_CONTEXT DefaultContext(int base_qindex)
    {
        var cm = new AV1_COMMON { fc = new FRAME_CONTEXT() };
        cm.quant_params.base_qindex = base_qindex;
        av1_default_coef_probs(cm);
        av1_init_mode_probs(cm.fc);
        av1_init_mv_probs(cm);
        return cm.fc;
    }

    public static IEnumerable<object[]> QIndices() => new[] { 0, 40, 100, 200 }.Select(q => new object[] { q });

    [Theory]
    [MemberData(nameof(QIndices))]
    public void CostTokensFromCdfMatchesLibaomOnAllDefaultCdfs(int base_qindex)
    {
        var rng = new Random(base_qindex);
        var image = new FrameContextImage(DefaultContext(base_qindex));
        foreach (FrameContextImage.Row row in image.Rows)
        {
            ushort[] cdf = row.Cdf;
            int nsymbs = Array.IndexOf(cdf, (ushort)0) + 1;  // av1_cost_tokens_from_cdf stops at icdf 0
            Assert.True(nsymbs > 0, row.Path);

            var expected = new int[cdf.Length];
            var actual = new int[cdf.Length];
            E1Shim.aomshim_e1_cost_tokens_from_cdf(expected, cdf, null);
            av1_cost_tokens_from_cdf(actual, cdf, null);  // C callers pass NULL
            Assert.True(expected.AsSpan().SequenceEqual(actual), row.Path);

            // With a symbol permutation as inv_map.
            int[] inv_map = Enumerable.Range(0, nsymbs).OrderBy(_ => rng.Next()).ToArray();
            Array.Clear(expected);
            Array.Clear(actual);
            E1Shim.aomshim_e1_cost_tokens_from_cdf(expected, cdf, inv_map);
            av1_cost_tokens_from_cdf(actual, cdf, inv_map);
            Assert.True(expected.AsSpan().SequenceEqual(actual), row.Path + " (inv_map)");
        }
    }

    [Fact]
    public void CostTokensFromCdfMatchesLibaomOnRandomCdfs()
    {
        var rng = new Random(5);
        for (int iter = 0; iter < 5000; iter++)
        {
            int nsymbs = rng.Next(2, 17);
            var cuts = new int[nsymbs - 1];
            // Include degenerate (equal, near 0 / near 32768) cut points to exercise the clamps.
            for (int i = 0; i < cuts.Length; i++)
                cuts[i] = rng.Next(4) switch { 0 => rng.Next(0, 4), 1 => rng.Next(32764, 32769), _ => rng.Next(32769) };
            Array.Sort(cuts);
            var cdf = new ushort[nsymbs + 1];
            for (int i = 0; i < nsymbs - 1; i++) cdf[i] = (ushort)(32768 - cuts[i]);
            cdf[nsymbs - 1] = 0;
            if (Array.IndexOf(cdf, (ushort)0) != nsymbs - 1) continue;  // an earlier 0 ends the CDF early in both

            var expected = new int[16];
            var actual = new int[16];
            E1Shim.aomshim_e1_cost_tokens_from_cdf(expected, cdf, null);
            av1_cost_tokens_from_cdf(actual, cdf, Ptr<int>.Null);
            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [MemberData(nameof(QIndices))]
    public void BatchCostsMatchLibaom(int base_qindex)
    {
        FRAME_CONTEXT fc = DefaultContext(base_qindex);
        for (int tx_size = 0; tx_size < fc.coeff_base_cdf.Length; tx_size++)
        {
            for (int plane = 0; plane < 2; plane++)
            {
                Check(fc.txb_skip_cdf[tx_size], 2, 2);
                Check(fc.eob_extra_cdf[tx_size][plane], 2, 2);
                Check(fc.coeff_base_cdf[tx_size][plane], 4, 8);
                Check(fc.coeff_br_cdf[tx_size][plane], 4, 4);
            }
        }

        static void Check(ushort[][] rows, int n_sym, int costs_stride)
        {
            int n = rows.Length, cdf_stride = rows[0].Length;
            ushort[] flatCdf = rows.SelectMany(r => r).ToArray();
            var expected = new int[n * costs_stride];
            if (n_sym == 2) E1Shim.aomshim_e1_cost_tokens_from_cdf_2_batch(expected, costs_stride, flatCdf, cdf_stride, n);
            else E1Shim.aomshim_e1_cost_tokens_from_cdf_4_batch(expected, costs_stride, flatCdf, cdf_stride, n);

            // Flat (pointer) form.
            var flat = new int[n * costs_stride];
            if (n_sym == 2) av1_cost_tokens_from_cdf_2_batch(flat, costs_stride, flatCdf, cdf_stride, n);
            else av1_cost_tokens_from_cdf_4_batch(flat, costs_stride, flatCdf, cdf_stride, n);
            Assert.Equal(expected, flat);

            // Jagged form used with the port's 2-D cost / CDF arrays.
            int[][] jagged = Enumerable.Range(0, n).Select(_ => new int[costs_stride]).ToArray();
            if (n_sym == 2) av1_cost_tokens_from_cdf_2_batch(jagged, costs_stride, rows, cdf_stride, n);
            else av1_cost_tokens_from_cdf_4_batch(jagged, costs_stride, rows, cdf_stride, n);
            Assert.Equal(expected, jagged.SelectMany(r => r).ToArray());
        }
    }
}
