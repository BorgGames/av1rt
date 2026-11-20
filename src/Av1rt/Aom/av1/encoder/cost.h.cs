// Port of libaom av1/encoder/cost.h
global using static Av1rt.Aom.cost;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class cost
{
    // Direct lookup table for small p15 (< COST_LUT_LOW).
    // COST_LUT_LOW * 2 B = 8 KB.
    internal const int COST_LUT_LOW = 4096;

    // av1_prob_cost[129] and av1_cost_lut_low[COST_LUT_LOW] (filled by av1_init_cost_luts)
    // are defined in cost.c.

    // The factor to scale from cost in bits to cost in av1_prob_cost units.
    internal const int AV1_PROB_COST_SHIFT = 9;

    // Cost of coding an n bit literal, using 128 (i.e. 50%) probability
    // for each bit.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_cost_literal(int n) => n * (1 << AV1_PROB_COST_SHIFT);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_cost_symbol_clamped(int p15)
    {
        Debug.Assert(0 < p15 && p15 < CDF_PROB_TOP);
        int shift = CDF_PROB_BITS - 1 - get_msb((uint)p15);
        // Since CDF_PROB_TOP == 32768 and av1_prob_cost has 129 entries (including a
        // duplicate 128th entry for prob_idx == 128), get_prob(p15 << shift, 32768) -
        // 128 simplifies to ((p15 << shift) - 16320) >> 7.
        int prob_idx = ((p15 << shift) - 16320) >> 7;
        return av1_prob_cost[prob_idx] + av1_cost_literal(shift);
    }

    // Calculate the cost of a symbol with probability p15 / 2^15
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_cost_symbol(ushort p15)
    {
        // p15 can be out of range [1, CDF_PROB_TOP - 1]. Clamping it, so that the
        // following cost calculation works correctly. Otherwise, if p15 =
        // CDF_PROB_TOP, shift would be -1, and "p15 << shift" would be wrong.
        int clamped = clamp((int)p15, 1, CDF_PROB_TOP - 1);
        return av1_cost_symbol_clamped(clamped);
    }

    // Same as av1_cost_symbol(), but uses a lookup table for small p15.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_cost_symbol_lut(ushort p15)
    {
        if (p15 < COST_LUT_LOW) return av1_cost_lut_low[p15];
        return av1_cost_symbol(p15);
    }

    // Same as av1_cost_tokens_from_cdf() for a 2-symbol CDF with inv_map == NULL.
    // The fixed symbol count removes the CDF-terminator check.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_cost_tokens_from_cdf_2(Ptr<int> costs, Ptr<ushort> cdf)
    {
        ushort prev = 0;
        for (int i = 0; i < 2; ++i)
        {
            ushort c = (ushort)AOM_ICDF(cdf[i]);
            ushort p15 = (ushort)(c - prev);
            p15 = (p15 < EC_MIN_PROB) ? (ushort)EC_MIN_PROB : p15;
            prev = c;
            costs[i] = av1_cost_symbol_lut(p15);
        }
    }

    // Same as av1_cost_tokens_from_cdf() for a 4-symbol CDF with inv_map == NULL.
    // The fixed symbol count removes the CDF-terminator check.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_cost_tokens_from_cdf_4(Ptr<int> costs, Ptr<ushort> cdf)
    {
        ushort prev = 0;
        for (int i = 0; i < 4; ++i)
        {
            ushort c = (ushort)AOM_ICDF(cdf[i]);
            ushort p15 = (ushort)(c - prev);
            p15 = (p15 < EC_MIN_PROB) ? (ushort)EC_MIN_PROB : p15;
            prev = c;
            costs[i] = av1_cost_symbol_lut(p15);
        }
    }
}
