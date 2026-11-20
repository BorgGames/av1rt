// Port of libaom av1/encoder/mv_prec.c
namespace Av1rt.Aom;

internal static partial class mv_prec
{
    // [subset] get_ref_mv_for_mv_stats, get_mv_component_cost, ... av1_collect_mv_stats,
    // get_smart_mv_prec and the MV_STATS bookkeeping: all under !CONFIG_REALTIME_ONLY.

    internal static void av1_pick_and_set_high_precision_mv(AV1_COMP cpi, int qindex)
    {
        int use_hp = B(qindex < HIGH_PRECISION_MV_QTHRESH);

        if (cpi.sf.hl_sf.high_precision_mv_usage == QTR_ONLY)
        {
            use_hp = 0;
        }
        // [subset] else if (high_precision_mv_usage == LAST_MV_DATA && ...): the smart MV
        // precision decision (get_smart_mv_prec) is !CONFIG_REALTIME_ONLY.

        av1_set_high_precision_mv(cpi, use_hp,
                                  B(cpi.common.features.cur_frame_force_integer_mv));
    }
}
