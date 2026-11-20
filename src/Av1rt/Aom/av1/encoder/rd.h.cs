// Port of libaom av1/encoder/rd.h
global using MODE_EVAL_TYPE = int;
global using static Av1rt.Aom.rd;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class rd
{
    internal const int RDDIV_BITS = 7;
    internal const int RD_EPB_SHIFT = 6;

    // C macro:
    //   (ROUND_POWER_OF_TWO(((int64_t)(R)) * (RM), AV1_PROB_COST_SHIFT) + ((D) * (1 << RDDIV_BITS)))
    // All in-scope call sites pass an int64_t (or uint64_t, same bits) D.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long RDCOST(int RM, long R, long D) =>
        ((R * RM + ((1 << AV1_PROB_COST_SHIFT) >> 1)) >> AV1_PROB_COST_SHIFT) +
        D * (1 << RDDIV_BITS);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long RDCOST_NEG_R(int RM, long R, long D) =>
        D * (1 << RDDIV_BITS) -
        ((R * RM + ((1 << AV1_PROB_COST_SHIFT) >> 1)) >> AV1_PROB_COST_SHIFT);

    // [subset] RDCOST_DBL_WITH_NATIVE_BD_DIST: rd-only (never used in the subset).

    internal const int QIDX_SKIP_THRESH = 115;

    internal const int MV_COST_WEIGHT = 108;

    internal const int MV_COST_WEIGHT_SUB = 120;

    // The fractional part of rd_thresh factor is stored with 5 bits. The maximum
    // factor that we allow is two, which is stored as 2 ** (5+1) = 64
    internal const int RD_THRESH_FAC_FRAC_BITS = 5;
    internal const int RD_THRESH_FAC_FRAC_VAL = 1 << RD_THRESH_FAC_FRAC_BITS;
    internal const int RD_THRESH_MAX_FACT = RD_THRESH_FAC_FRAC_VAL << 1;
    internal const int RD_THRESH_LOG_DEC_FACTOR = 4;
    internal const int RD_THRESH_INC = 1;

    // Factor to weigh the rate for switchable interp filters.
    internal const int SWITCHABLE_INTERP_RATE_FACTOR = 1;

    // Macros for common video resolutions: width x height
    // For example, 720p represents video resolution of 1280x720 pixels.
    // (The C macros are unparenthesized products; every in-scope use is a plain comparison.)
    internal const int RESOLUTION_288P = 352 * 288;
    internal const int RESOLUTION_360P = 640 * 360;
    internal const int RESOLUTION_480P = 640 * 480;
    internal const int RESOLUTION_720P = 1280 * 720;
    internal const int RESOLUTION_1080P = 1920 * 1080;
    internal const int RESOLUTION_1440P = 2560 * 1440;
    internal const int RESOLUTION_4K = 3840 * 2160;

    internal const int RTC_REFS = 4;
    internal static readonly MV_REFERENCE_FRAME[][] real_time_ref_combos = {
        new MV_REFERENCE_FRAME[] { LAST_FRAME, NONE_FRAME },
        new MV_REFERENCE_FRAME[] { ALTREF_FRAME, NONE_FRAME },
        new MV_REFERENCE_FRAME[] { GOLDEN_FRAME, NONE_FRAME },
        new MV_REFERENCE_FRAME[] { INTRA_FRAME, NONE_FRAME },
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int mode_offset(PREDICTION_MODE mode)
    {
        if (mode >= NEARESTMV)
        {
            return INTER_OFFSET(mode);
        }
        else
        {
            switch (mode)
            {
                case DC_PRED: return 0;
                case V_PRED: return 1;
                case H_PRED: return 2;
                case SMOOTH_PRED: return 3;
                default: Debug.Assert(false); return -1;
            }
        }
    }

    // enum MODE_EVAL_TYPE
    // Default initialization when we are not using winner mode framework. e.g.
    // intrabc
    internal const int DEFAULT_EVAL = 0;
    // Initialization for selecting winner mode
    internal const int MODE_EVAL = 1;
    // Initialization for winner mode evaluation
    internal const int WINNER_MODE_EVAL = 2;
    // All mode evaluation types
    internal const int MODE_EVAL_TYPES = 3;

    // Trivially small; reached from data-dependent branches of executed partition
    // search code (e.g. the nonrd partition merge checks).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_init_rd_stats(ref RD_STATS rd_stats)
    {
        rd_stats.rate = 0;
        rd_stats.dist = 0;
        rd_stats.rdcost = 0;
        rd_stats.sse = 0;
        rd_stats.skip_txfm = 1;
        rd_stats.zero_rate = 0;
        // [subset] CONFIG_RD_DEBUG txb_coeff_cost reset.
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_invalid_rd_stats(ref RD_STATS rd_stats)
    {
        rd_stats.rate = int.MaxValue;
        rd_stats.dist = long.MaxValue;
        rd_stats.rdcost = long.MaxValue;
        rd_stats.sse = long.MaxValue;
        rd_stats.skip_txfm = 0;
        rd_stats.zero_rate = 0;
        // [subset] CONFIG_RD_DEBUG txb_coeff_cost reset.
    }

    // [subset] av1_merge_rd_stats, av1_merge_rd_stats_weighted, av1_accumulate_rd_stats:
    // only used by the rd mode search (rdopt.c / tx_search.c).

    // Trivially small; av1_rd_cost_update / av1_rd_stats_subtraction are reached from
    // data-dependent branches of executed partition search code.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long av1_calculate_rd_cost(int mult, int rate, long dist)
    {
        Debug.Assert(mult >= 0);
        if (rate >= 0)
        {
            return RDCOST(mult, rate, dist);
        }
        return RDCOST_NEG_R(mult, -rate, dist);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_rd_cost_update(int mult, ref RD_STATS rd_cost)
    {
        if (rd_cost.rate < int.MaxValue && rd_cost.dist < long.MaxValue &&
            rd_cost.rdcost < long.MaxValue)
        {
            rd_cost.rdcost = av1_calculate_rd_cost(mult, rd_cost.rate, rd_cost.dist);
        }
        else
        {
            av1_invalid_rd_stats(ref rd_cost);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_rd_stats_subtraction(int mult,
                                                  in RD_STATS left,
                                                  in RD_STATS right,
                                                  ref RD_STATS result)
    {
        if (left.rate == int.MaxValue || right.rate == int.MaxValue ||
            left.dist == long.MaxValue || right.dist == long.MaxValue ||
            left.rdcost == long.MaxValue || right.rdcost == long.MaxValue)
        {
            av1_invalid_rd_stats(ref result);
        }
        else
        {
            result.rate = left.rate - right.rate;
            result.dist = left.dist - right.dist;
            result.rdcost = av1_calculate_rd_cost(mult, result.rate, result.dist);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void copy_entropy_ctx(Ptr<ENTROPY_CONTEXT> dst,
                                          Ptr<ENTROPY_CONTEXT> src, int n)
    {
        switch (n)
        {
            case 1: dst.Value = src.Value; break;
            case 2: memcpy(dst, src, 2); break;
            case 4: memcpy(dst, src, 4); break;
            case 8: memcpy(dst, src, 8); break;
            case 16: memcpy(dst, src, 16); break;
            case 32: memcpy(dst, src, 32); break;
            default: memcpy(dst, src, n); break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_get_entropy_contexts(
        BLOCK_SIZE plane_bsize, macroblockd_plane pd,
        Ptr<ENTROPY_CONTEXT> t_above /* [MAX_MIB_SIZE] */,
        Ptr<ENTROPY_CONTEXT> t_left /* [MAX_MIB_SIZE] */)
    {
        Debug.Assert(plane_bsize < BLOCK_SIZES_ALL);
        copy_entropy_ctx(t_above, pd.above_entropy_context,
                         mi_size_wide[plane_bsize]);
        copy_entropy_ctx(t_left, pd.left_entropy_context, mi_size_high[plane_bsize]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void reset_thresh_freq_fact(MACROBLOCK x)
    {
        for (int i = 0; i < BLOCK_SIZES_ALL; ++i)
        {
            for (int j = 0; j < MAX_MODES; ++j)
            {
                x.thresh_freq_fact[i][j] = RD_THRESH_FAC_FRAC_VAL;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int rd_less_than_thresh(long best_rd, long thresh,
                                            int thresh_fact)
    {
        return B(best_rd < (thresh * thresh_fact >> 5) || thresh == int.MaxValue);
    }

    // Sets the multiplier to convert mv cost to l2 error during motion search.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_set_error_per_bit(ref int errorperbit, int rdmult)
    {
        errorperbit = AOMMAX(rdmult >> RD_EPB_SHIFT, 1);
    }

    // Get the threshold for R-D optimization of coefficients depending upon mode
    // decision/winner mode processing
    // C: const uint32_t (*const coeff_opt_threshold)[2], i.e. rows of
    // WinnerModeParams::coeff_opt_thresholds[MODE_EVAL_TYPES][2].
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void get_rd_opt_coeff_thresh(
        uint[][] coeff_opt_threshold,
        TxfmSearchParams txfm_params, int enable_winner_mode_for_coeff_opt,
        int is_winner_mode)
    {
        if (enable_winner_mode_for_coeff_opt == 0)
        {
            // Default initialization of threshold
            txfm_params.coeff_opt_thresholds[0] = coeff_opt_threshold[DEFAULT_EVAL][0];
            txfm_params.coeff_opt_thresholds[1] = coeff_opt_threshold[DEFAULT_EVAL][1];
            return;
        }
        // TODO(any): Experiment with coeff_opt_dist_threshold values when
        // enable_winner_mode_for_coeff_opt is ON
        // TODO(any): Skip the winner mode processing for blocks with lower residual
        // energy as R-D optimization of coefficients would have been enabled during
        // mode decision

        // Use conservative threshold during mode decision and perform R-D
        // optimization of coeffs always for winner modes
        if (is_winner_mode != 0)
        {
            txfm_params.coeff_opt_thresholds[0] =
                coeff_opt_threshold[WINNER_MODE_EVAL][0];
            txfm_params.coeff_opt_thresholds[1] =
                coeff_opt_threshold[WINNER_MODE_EVAL][1];
        }
        else
        {
            txfm_params.coeff_opt_thresholds[0] = coeff_opt_threshold[MODE_EVAL][0];
            txfm_params.coeff_opt_thresholds[1] = coeff_opt_threshold[MODE_EVAL][1];
        }
    }

    // [subset] reset_mb_rd_record: MB_RD_RECORD is not allocated in the nonrd path (dropped).
}

// Class: large (threshes is MAX_SEGMENTS x BLOCK_SIZES_ALL x MAX_MODES), used through
// pointers (&cpi->rd), never copied by value.
internal sealed partial class RD_OPT
{
    // Thresh_mult is used to set a threshold for the rd score. A higher value
    // means that we will accept the best mode so far more often. This number
    // is used in combination with the current block size, and thresh_freq_fact
    // to pick a threshold.
    internal readonly int[] thresh_mult = new int[MAX_MODES];

    internal readonly int[][][] threshes = EncArrays.New3<int>(MAX_SEGMENTS, BLOCK_SIZES_ALL, MAX_MODES);

    internal int RDMULT;

    internal double r0;
}
