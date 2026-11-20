// Port of libaom av1/encoder/mv_prec.h
global using static Av1rt.Aom.mv_prec;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class mv_prec
{
    // Q threshold for high precision mv.
    internal const int HIGH_PRECISION_MV_QTHRESH = 128;

    // [subset] av1_collect_mv_stats / av1_frame_allows_smart_mv: !CONFIG_REALTIME_ONLY only.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_set_high_precision_mv(AV1_COMP cpi,
                                                   int allow_high_precision_mv,
                                                   int cur_frame_force_integer_mv)
    {
        MvCosts mv_costs = cpi.td.mb.mv_costs;
        // Avoid accessing 'mv_costs' when it is not allocated.
        if (mv_costs == null) return;

        // C: const int copy_hp = cpi->common.features.allow_high_precision_mv = ...;
        // (allow_high_precision_mv is a bool in FeatureFlags.)
        cpi.common.features.allow_high_precision_mv =
            allow_high_precision_mv != 0 && cur_frame_force_integer_mv == 0;
        int copy_hp = B(cpi.common.features.allow_high_precision_mv);

        mv_costs.nmv_cost[0] = new Ptr<int>(mv_costs.nmv_cost_alloc[0], MV_MAX);
        mv_costs.nmv_cost[1] = new Ptr<int>(mv_costs.nmv_cost_alloc[1], MV_MAX);
        mv_costs.nmv_cost_hp[0] = new Ptr<int>(mv_costs.nmv_cost_hp_alloc[0], MV_MAX);
        mv_costs.nmv_cost_hp[1] = new Ptr<int>(mv_costs.nmv_cost_hp_alloc[1], MV_MAX);
        mv_costs.mv_cost_stack =
            copy_hp != 0 ? mv_costs.nmv_cost_hp : mv_costs.nmv_cost;
    }
}
