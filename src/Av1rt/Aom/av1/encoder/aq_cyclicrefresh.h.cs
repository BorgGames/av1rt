// Port of libaom av1/encoder/aq_cyclicrefresh.h
global using static Av1rt.Aom.aq_cyclicrefresh;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class aq_cyclicrefresh
{
    // The segment ids used in cyclic refresh: from base (no boost) to increasing
    // boost (higher delta-qp).
    internal const int CR_SEGMENT_ID_BASE = 0;
    internal const int CR_SEGMENT_ID_BOOST1 = 1;
    internal const int CR_SEGMENT_ID_BOOST2 = 2;

    // Maximum rate target ratio for setting segment delta-qp.
    internal const double CR_MAX_RATE_TARGET_RATIO = 4.0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int cyclic_refresh_segment_id_boosted(int segment_id)
    {
        return B(segment_id == CR_SEGMENT_ID_BOOST1 ||
                 segment_id == CR_SEGMENT_ID_BOOST2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int cyclic_refresh_segment_id(int segment_id)
    {
        if (segment_id == CR_SEGMENT_ID_BOOST1)
            return CR_SEGMENT_ID_BOOST1;
        else if (segment_id == CR_SEGMENT_ID_BOOST2)
            return CR_SEGMENT_ID_BOOST2;
        else
            return CR_SEGMENT_ID_BASE;
    }
}

/// <summary>The stucture of CYCLIC_REFRESH.</summary>
internal sealed partial class CYCLIC_REFRESH
{
    /// Percentage of blocks per frame that are targeted as candidates
    /// for cyclic refresh.
    public int percent_refresh;

    /// Active adjustment delta for cyclic refresh for rate control.
    public int percent_refresh_adjustment;

    /// Maximum q-delta as percentage of base q.
    public int max_qdelta_perc;
    /// Superblock starting index for cycling through the frame.
    public int sb_index;
    /// Superblock index cyclic refresh index last frame
    public int last_sb_index;
    /// Controls how long block will need to wait to be refreshed again, in
    /// excess of the cycle time, i.e., in the case of all zero motion, block
    /// will be refreshed every (100/percent_refresh + time_for_refresh) frames.
    public int time_for_refresh;
    /// Target number of (4x4) blocks that are set for delta-q.
    public int target_num_seg_blocks;
    /// Actual number of (4x4) blocks that were applied delta-q,
    /// for segment 1.
    public int actual_num_seg1_blocks;
    /// Actual number of (4x4) blocks that were applied delta-q,
    /// for segment 2.
    public int actual_num_seg2_blocks;
    /// RD mult. parameters for segment 1.
    public int rdmult;
    /// Cyclic refresh map.
    public Ptr<sbyte> map;
    /// Threshold applied to the projected rate of the coding block,
    /// when deciding whether block should be refreshed.
    public long thresh_rate_sb;
    /// Threshold applied to the projected distortion of the coding block,
    /// when deciding whether block should be refreshed.
    public long thresh_dist_sb;
    /// Threshold applied to the motion vector (in units of 1/8 pel) of the
    /// coding block, when deciding whether block should be refreshed.
    public short motion_thresh;
    /// Rate target ratio to set q delta.
    public double rate_ratio_qdelta;

    /// Active adjustment of qdelta rate ratio for enhanced rate control
    public double rate_ratio_qdelta_adjustment;

    /// Boost factor for rate target ratio, for segment CR_SEGMENT_ID_BOOST2.
    public int rate_boost_fac;

    public readonly int[] qindex_delta = new int[3];
    public int apply_cyclic_refresh;
    public int skip_over4x4;
    public int counter_encode_maxq_scene_change;
    public int use_block_sad_scene_det;
}
