// Port of libaom av1/encoder/rc_utils.h
global using static Av1rt.Aom.rc_utils;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class rc_utils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void check_reset_rc_flag(AV1_COMP cpi)
    {
        RATE_CONTROL rc = cpi.rc;
        PRIMARY_RATE_CONTROL p_rc = cpi.ppi.p_rc;
        if (cpi.common.current_frame.frame_number >
            (uint)cpi.svc.number_spatial_layers)
        {
            if (cpi.ppi.use_svc != 0)
            {
                // [subset] av1_svc_check_reset_layer_rc_flag: SVC is out of the subset.
                throw new NotSupportedException("SVC is out of the subset");
            }
            else
            {
                if (rc.avg_frame_bandwidth / 3 > (rc.prev_avg_frame_bandwidth >> 1) ||
                    rc.avg_frame_bandwidth < (rc.prev_avg_frame_bandwidth >> 1))
                {
                    rc.rc_1_frame = 0;
                    rc.rc_2_frame = 0;
                    p_rc.bits_off_target = p_rc.optimal_buffer_level;
                    p_rc.buffer_level = p_rc.optimal_buffer_level;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_primary_rc_buffer_sizes(AV1EncoderConfig oxcf,
                                                     AV1_PRIMARY ppi)
    {
        PRIMARY_RATE_CONTROL p_rc = ppi.p_rc;
        ref readonly RateControlCfg rc_cfg = ref oxcf.rc_cfg;

        long bandwidth = rc_cfg.target_bandwidth;
        long starting = rc_cfg.starting_buffer_level_ms;
        long optimal = rc_cfg.optimal_buffer_level_ms;
        long maximum = rc_cfg.maximum_buffer_size_ms;

        p_rc.starting_buffer_level = starting * bandwidth / 1000;
        p_rc.optimal_buffer_level =
            (optimal == 0) ? bandwidth / 8 : optimal * bandwidth / 1000;
        p_rc.maximum_buffer_size =
            (maximum == 0) ? bandwidth / 8 : maximum * bandwidth / 1000;

        // Under a configuration change, where maximum_buffer_size may change,
        // keep buffer level clipped to the maximum allowed buffer size.
        p_rc.bits_off_target =
            AOMMIN(p_rc.bits_off_target, p_rc.maximum_buffer_size);
        p_rc.buffer_level = AOMMIN(p_rc.buffer_level, p_rc.maximum_buffer_size);
    }

    // [subset] config_target_level (level conformance), the !CONFIG_REALTIME_ONLY
    // recode / boost helpers (recode_loop_test, av1_get_gfu_boost_projection_factor,
    // get_gfu_boost_from_r0_lap, av1_get_kf_boost_projection_factor,
    // get_regulated_q_overshoot / undershoot, recode_loop_update_q, ...): never
    // executed in the subset.
}
