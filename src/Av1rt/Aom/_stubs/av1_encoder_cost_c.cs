// Stubs for av1/encoder/cost.c: delete this file when av1/encoder/cost.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_cost_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_cost_c
{
    // ---- av1/encoder/cost.c -------------------------------------------------------------
    // PORT-NOTE (e6): av1_prob_cost and av1_cost_tokens_from_cdf have working bodies here
    // (the table copied from cost.c by script, the function a verbatim port) so that the e6
    // differential tests of av1_build_nmv_cost_table / av1_mv_bit_cost can run before e1's
    // cost.c port is merged. e1's cost.c.cs replaces this whole file.

    // extern const uint16_t av1_prob_cost[129];
    internal static ushort[] av1_prob_cost => s_av1_prob_cost;

    private static readonly ushort[] s_av1_prob_cost = {
        512, 506, 501, 495, 489, 484, 478, 473, 467, 462, 456, 451, 446, 441, 435,
        430, 425, 420, 415, 410, 405, 400, 395, 390, 385, 380, 375, 371, 366, 361,
        356, 352, 347, 343, 338, 333, 329, 324, 320, 316, 311, 307, 302, 298, 294,
        289, 285, 281, 277, 273, 268, 264, 260, 256, 252, 248, 244, 240, 236, 232,
        228, 224, 220, 216, 212, 209, 205, 201, 197, 194, 190, 186, 182, 179, 175,
        171, 168, 164, 161, 157, 153, 150, 146, 143, 139, 136, 132, 129, 125, 122,
        119, 115, 112, 109, 105, 102, 99,  95,  92,  89,  86,  82,  79,  76,  73,
        70,  66,  63,  60,  57,  54,  51,  48,  45,  42,  38,  35,  32,  29,  26,
        23,  20,  18,  15,  12,  9,   6,   3,   3,
    };

    // extern uint16_t av1_cost_lut_low[COST_LUT_LOW];  // filled by av1_init_cost_luts
    internal static ushort[] av1_cost_lut_low => throw new NotImplementedException();

    internal static void av1_cost_tokens_from_cdf(Ptr<int> costs, Ptr<ushort> cdf,
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
}
