// Port of libaom av1/common/timing.c

namespace Av1rt.Aom;

// [subset] av1_set_aom_dec_model_info, av1_set_dec_model_op_parameters and
// av1_set_resource_availability_parameters never run (the decoder model is off in the subset).
internal static partial class timing
{
    /* Tables for AV1 max bitrates for different levels of main and high tier.
     * The tables are in Kbps instead of Mbps in the specification.
     * Note that depending on the profile, a multiplier is needed.
     */
    private const int UNDEFINED_RATE =
        (1 << 21);  // Placeholder rate for levels with undefined rate
    private const int INVALID_RATE =
        (0);  // For invalid profile-level configuration, set rate to 0

    /* Max Bitrates for levels of Main Tier in kbps. Bitrate in main_kbps [31] */
    /* is a dummy value. The decoder model is not applicable for level 31. */
    private static readonly int[] main_kbps = {
        1500,           3000,           UNDEFINED_RATE, UNDEFINED_RATE,
        6000,           10000,          UNDEFINED_RATE, UNDEFINED_RATE,
        12000,          20000,          UNDEFINED_RATE, UNDEFINED_RATE,
        30000,          40000,          60000,          60000,
        60000,          100000,         160000,         160000,
        UNDEFINED_RATE, UNDEFINED_RATE, UNDEFINED_RATE, UNDEFINED_RATE,
        UNDEFINED_RATE, UNDEFINED_RATE, UNDEFINED_RATE, UNDEFINED_RATE,
        UNDEFINED_RATE, UNDEFINED_RATE, UNDEFINED_RATE, UNDEFINED_RATE
    };

    /* Max Bitrates for levels of High Tier in kbps. Bitrate in high_kbps [31] */
    /* is a dummy value. The decoder model is not applicable for level 31. */
    private static readonly int[] high_kbps = {
        INVALID_RATE,   INVALID_RATE,   INVALID_RATE,   INVALID_RATE,
        INVALID_RATE,   INVALID_RATE,   INVALID_RATE,   INVALID_RATE,
        30000,          50000,          UNDEFINED_RATE, UNDEFINED_RATE,
        100000,         160000,         240000,         240000,
        240000,         480000,         800000,         800000,
        UNDEFINED_RATE, UNDEFINED_RATE, UNDEFINED_RATE, UNDEFINED_RATE,
        UNDEFINED_RATE, UNDEFINED_RATE, UNDEFINED_RATE, UNDEFINED_RATE,
        UNDEFINED_RATE, UNDEFINED_RATE, UNDEFINED_RATE, UNDEFINED_RATE
    };

    /* BitrateProfileFactor */
    private static readonly int[] bitrate_profile_factor = { 1, 2, 3, 0,
                                                             0, 0, 0, 0 };

    internal static long av1_max_level_bitrate(BITSTREAM_PROFILE seq_profile, int seq_level_idx,
                                               int seq_tier)
    {
        long bitrate;

        if (seq_tier != 0)
        {
            bitrate = high_kbps[seq_level_idx] * bitrate_profile_factor[seq_profile];
        }
        else
        {
            bitrate = main_kbps[seq_level_idx] * bitrate_profile_factor[seq_profile];
        }

        return bitrate * 1000;
    }
}
