// Stubs for av1/encoder/cost.c: delete this file when av1/encoder/cost.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_cost_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_cost_c
{
    // ---- av1/encoder/cost.c -------------------------------------------------------------
    // extern const uint16_t av1_prob_cost[129];
    internal static ushort[] av1_prob_cost => throw new NotImplementedException();
    // extern uint16_t av1_cost_lut_low[COST_LUT_LOW];  // filled by av1_init_cost_luts
    internal static ushort[] av1_cost_lut_low => throw new NotImplementedException();
    internal static void av1_cost_tokens_from_cdf(Ptr<int> costs, Ptr<ushort> cdf,
                                                  Ptr<int> inv_map) =>
        throw new NotImplementedException();
    // RTCD (av1_rtcd.h): av1_cost_tokens_from_cdf_{2,4}_batch -> av1_cost_tokens_from_cdf_{2,4}_batch_c.
    internal static void av1_cost_tokens_from_cdf_2_batch(Ptr<int> costs, int costs_stride,
                                                          Ptr<ushort> cdf, int cdf_stride,
                                                          int n) =>
        throw new NotImplementedException();
    internal static void av1_cost_tokens_from_cdf_4_batch(Ptr<int> costs, int costs_stride,
                                                          Ptr<ushort> cdf, int cdf_stride,
                                                          int n) =>
        throw new NotImplementedException();
}
