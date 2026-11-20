// Port of libaom av1/encoder/cost.c

using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class cost
{
    // round(-log2(i/256.) * (1 << AV1_PROB_COST_SHIFT)); i = 128~255 (plus 128th
    // duplicate for 256).
    // <gen:tables>
    // uint16_t av1_prob_cost[129]
    internal static readonly ushort[] av1_prob_cost = {
        512, 506, 501, 495, 489, 484, 478, 473, 467, 462, 456, 451, 446, 441, 435, 430, 425, 420,
        415, 410, 405, 400, 395, 390, 385, 380, 375, 371, 366, 361, 356, 352, 347, 343, 338, 333,
        329, 324, 320, 316, 311, 307, 302, 298, 294, 289, 285, 281, 277, 273, 268, 264, 260, 256,
        252, 248, 244, 240, 236, 232, 228, 224, 220, 216, 212, 209, 205, 201, 197, 194, 190, 186,
        182, 179, 175, 171, 168, 164, 161, 157, 153, 150, 146, 143, 139, 136, 132, 129, 125, 122,
        119, 115, 112, 109, 105, 102, 99, 95, 92, 89, 86, 82, 79, 76, 73, 70, 66, 63, 60, 57, 54,
        51, 48, 45, 42, 38, 35, 32, 29, 26, 23, 20, 18, 15, 12, 9, 6, 3, 3
    };
    // </gen:tables>

    internal static readonly ushort[] av1_cost_lut_low = new ushort[COST_LUT_LOW];

    private static void init_cost_luts()
    {
        for (int p = 1; p < COST_LUT_LOW; ++p)
            av1_cost_lut_low[p] = (ushort)av1_cost_symbol((aom_cdf_prob)p);
    }

    internal static void av1_init_cost_luts() { aom_once(init_cost_luts); }

    internal static void av1_cost_tokens_from_cdf(Ptr<int> costs, Ptr<aom_cdf_prob> cdf,
                                                  Ptr<int> inv_map)
    {
        int prev_icdf = CDF_PROB_TOP;
        if (!inv_map.IsNull)
        {
            for (int i = 0; ; ++i)
            {
                int cur_icdf = cdf[i];
                int p15 =
                    clamp(prev_icdf - cur_icdf, EC_MIN_PROB, CDF_PROB_TOP - 1);
                prev_icdf = cur_icdf;
                costs[inv_map[i]] = av1_cost_symbol_clamped(p15);
                if (cur_icdf == 0) break;
            }
        }
        else
        {
            for (int i = 0; ; ++i)
            {
                int cur_icdf = cdf[i];
                int p15 =
                    clamp(prev_icdf - cur_icdf, EC_MIN_PROB, CDF_PROB_TOP - 1);
                prev_icdf = cur_icdf;
                costs[i] = av1_cost_symbol_clamped(p15);
                if (cur_icdf == 0) break;
            }
        }
    }

    // Fills N=2 costs for `n` CDFs: costs[k*costs_stride + 0..1] gets the two
    // symbol costs of cdf[k*cdf_stride + 0..1]. inv_map is implicitly NULL.
    internal static void av1_cost_tokens_from_cdf_2_batch_c(Ptr<int> costs, int costs_stride,
                                                            Ptr<ushort> cdf, int cdf_stride,
                                                            int n)
    {
        for (int i = 0; i < n; ++i)
            av1_cost_tokens_from_cdf_2(costs + i * costs_stride, cdf + i * cdf_stride);
    }

    // Fills N=4 costs for `n` CDFs: costs[k*costs_stride + 0..3] gets the four
    // symbol costs of cdf[k*cdf_stride + 0..3]. inv_map is implicitly NULL.
    internal static void av1_cost_tokens_from_cdf_4_batch_c(Ptr<int> costs, int costs_stride,
                                                            Ptr<ushort> cdf, int cdf_stride,
                                                            int n)
    {
        for (int i = 0; i < n; ++i)
            av1_cost_tokens_from_cdf_4(costs + i * costs_stride, cdf + i * cdf_stride);
    }

    // [port] The C callers (rd.c) pass the first row of a 2-D cost array and of a 2-D CDF array
    // (e.g. pcost->txb_skip_cost[0] and fc->txb_skip_cdf[tx_size][0]) together with their row
    // strides. Those arrays are jagged in the port, so these overloads take the 2-D arrays
    // themselves: row k is costs[k] / cdf[k]. Every C caller passes the row lengths as the
    // strides; they are asserted.
    internal static void av1_cost_tokens_from_cdf_2_batch_c(int[][] costs, int costs_stride,
                                                            ushort[][] cdf, int cdf_stride,
                                                            int n)
    {
        for (int i = 0; i < n; ++i)
        {
            Debug.Assert(costs[i].Length == costs_stride && cdf[i].Length == cdf_stride);
            av1_cost_tokens_from_cdf_2(costs[i], cdf[i]);
        }
    }

    internal static void av1_cost_tokens_from_cdf_4_batch_c(int[][] costs, int costs_stride,
                                                            ushort[][] cdf, int cdf_stride,
                                                            int n)
    {
        for (int i = 0; i < n; ++i)
        {
            Debug.Assert(costs[i].Length == costs_stride && cdf[i].Length == cdf_stride);
            av1_cost_tokens_from_cdf_4(costs[i], cdf[i]);
        }
    }

    // RTCD (av1_rtcd.h): the dispatched names.
    internal static void av1_cost_tokens_from_cdf_2_batch(Ptr<int> costs, int costs_stride,
                                                          Ptr<ushort> cdf, int cdf_stride, int n)
    {
        if (Simd.UseAvx2) av1_cost_tokens_from_cdf_2_batch_avx2(costs, costs_stride, cdf, cdf_stride, n);
        else av1_cost_tokens_from_cdf_2_batch_c(costs, costs_stride, cdf, cdf_stride, n);
    }

    internal static void av1_cost_tokens_from_cdf_4_batch(Ptr<int> costs, int costs_stride,
                                                          Ptr<ushort> cdf, int cdf_stride, int n)
    {
        if (Simd.UseAvx2) av1_cost_tokens_from_cdf_4_batch_avx2(costs, costs_stride, cdf, cdf_stride, n);
        else av1_cost_tokens_from_cdf_4_batch_c(costs, costs_stride, cdf, cdf_stride, n);
    }

    // [port] The jagged overloads have no SIMD version: av1_cost_tokens_from_cdf_{2,4}_batch_avx2
    // gathers from one contiguous CDF table.

    internal static void av1_cost_tokens_from_cdf_2_batch(int[][] costs, int costs_stride,
                                                          ushort[][] cdf, int cdf_stride, int n) =>
        av1_cost_tokens_from_cdf_2_batch_c(costs, costs_stride, cdf, cdf_stride, n);

    internal static void av1_cost_tokens_from_cdf_4_batch(int[][] costs, int costs_stride,
                                                          ushort[][] cdf, int cdf_stride, int n) =>
        av1_cost_tokens_from_cdf_4_batch_c(costs, costs_stride, cdf, cdf_stride, n);
}
