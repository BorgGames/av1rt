// Port of libaom av1/encoder/firstpass.h
global using static Av1rt.Aom.firstpass;
global using FRAME_CONTENT_TYPE = int;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// The first pass / two-pass rate control is out of the subset
// (CONFIG_REALTIME_ONLY: firstpass.c and pass2_strategy.c are not compiled).
// Only the types that the realtime path references are ported: GF_GROUP (the
// rt GF structure), and minimal FIRSTPASS_STATS / STATS_BUFFER_CTX / TWO_PASS.
internal static partial class firstpass
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double DOUBLE_DIVIDE_CHECK(double x)
    {
        return ((x) < 0 ? (x) - 0.000001 : (x) + 0.000001);
    }

    internal const double MIN_ZERO_MOTION = 0.95;
    internal const int MAX_SR_CODED_ERROR = 40;
    internal const int MAX_RAW_ERR_VAR = 2000;
    internal const double MIN_MV_IN_OUT = 0.4;

    internal const int VLOW_MOTION_THRESHOLD = 950;

    internal const int FIRSTPASS_INFO_STATS_PAST_MIN = 1;

    internal const int FIRSTPASS_INFO_STATIC_BUF_SIZE =
        (MAX_LAP_BUFFERS + FIRSTPASS_INFO_STATS_PAST_MIN);

    internal const double FC_ANIMATION_THRESH = 0.15;

    // FRAME_CONTENT_TYPE
    internal const int FC_NORMAL = 0;
    internal const int FC_GRAPHICS_ANIMATION = 1;
    internal const int FRAME_CONTENT_TYPES = 2;

    // [subset] FIRSTPASS_INFO, GF_STATE, TWO_PASS_FRAME, FRAME_STATS,
    // FirstPassData and the av1_firstpass_info_* prototypes are two-pass only.
}

/// <summary>The stucture of accumulated frame stats in the first pass.</summary>
/// <remarks>
/// PORT-NOTE: kept only because av1_cx_iface allocates/frees
/// STATS_BUFFER_CTX::total_stats / total_left_stats; never filled in the subset.
/// </remarks>
internal sealed partial class FIRSTPASS_STATS
{
    /// Frame number in display order, if stats are for a single frame.
    public double frame;
    /// Weight assigned to this frame (or total weight for the collection of frames).
    public double weight;
    /// Intra prediction error.
    public double intra_error;
    /// Average wavelet energy computed using Discrete Wavelet Transform (DWT).
    public double frame_avg_wavelet_energy;
    /// Best of intra pred error and inter pred error using last frame as ref.
    public double coded_error;
    /// Best of intra pred error and inter pred error using golden frame as ref.
    public double sr_coded_error;
    /// Best of intra pred error and inter pred error using altref frame as ref.
    public double lt_coded_error;
    /// Percentage of blocks with inter pred error &lt; intra pred error.
    public double pcnt_inter;
    /// Percentage of blocks using (inter prediction and) non-zero motion vectors.
    public double pcnt_motion;
    /// Percentage of blocks where golden frame was better than last or intra.
    public double pcnt_second_ref;
    /// Percentage of blocks where intra and inter prediction errors were very close.
    public double pcnt_neutral;
    // Percentage of blocks that have intra error < inter error and inter error < LOW_I_THRESH.
    public double intra_skip_pct;
    /// Image mask rows top and bottom.
    public double inactive_zone_rows;
    /// Image mask columns at left and right edges.
    public double inactive_zone_cols;
    /// Average of row motion vectors.
    public double MVr;
    /// Mean of absolute value of row motion vectors.
    public double mvr_abs;
    /// Mean of column motion vectors.
    public double MVc;
    /// Mean of absolute value of column motion vectors.
    public double mvc_abs;
    /// Variance of row motion vectors.
    public double MVrv;
    /// Variance of column motion vectors.
    public double MVcv;
    /// Value in range [-1,1] indicating fraction of row and column motion vectors
    /// that point inwards (negative MV value) or outwards (positive MV value).
    public double mv_in_out_count;
    /// Count of unique non-zero motion vectors.
    public double new_mv_count;
    /// Duration of the frame / collection of frames.
    public double duration;
    /// 1.0 if stats are for a single frame, OR
    /// Number of frames in this collection for which the stats are accumulated.
    public double count;
    /// standard deviation for (0, 0) motion prediction error
    public double raw_error_stdev;
    /// Whether the frame contains a flash
    public long is_flash;
    /// Estimated noise variance
    public double noise_var;
    /// Correlation coefficient with the previous frame
    public double cor_coeff;
    /// log of intra_error
    public double log_intra_error;
    /// log of coded_error
    public double log_coded_error;
}

/// <summary>Data related to the current GF/ARF group and the individual frames within the group.</summary>
internal sealed partial class GF_GROUP
{
    // Frame update type, e.g. ARF/GF/LF/Overlay
    public readonly FRAME_UPDATE_TYPE[] update_type = new FRAME_UPDATE_TYPE[MAX_STATIC_GF_GROUP_LENGTH];
    public readonly byte[] arf_src_offset = new byte[MAX_STATIC_GF_GROUP_LENGTH];
    // [subset] cur_frame_idx[] dropped (two-pass GOP structure only).
    public readonly int[] layer_depth = new int[MAX_STATIC_GF_GROUP_LENGTH];
    // [subset] arf_boost[] dropped (two-pass GOP structure only).
    public int max_layer_depth;
    // [subset] max_layer_depth_allowed, q_val[], rdmult_val[], bit_allocation[]
    // dropped (two-pass / AOM_Q GOP structure only).
    // The frame coding type - inter/intra frame
    public readonly FRAME_TYPE[] frame_type = new FRAME_TYPE[MAX_STATIC_GF_GROUP_LENGTH];
    // The reference frame buffer control - update or reset
    public readonly REFBUF_STATE[] refbuf_state = new REFBUF_STATE[MAX_STATIC_GF_GROUP_LENGTH];
    // [subset] arf_index dropped (two-pass GOP structure only).
    public int size; // The total length of a GOP

    // The offset into lookahead_ctx for choosing
    // source of frame parallel encodes.
    public readonly int[] src_offset = new int[MAX_STATIC_GF_GROUP_LENGTH];
    // Stores the display order hint of each frame in the current GF_GROUP.
    public readonly int[] display_idx = new int[MAX_STATIC_GF_GROUP_LENGTH];

    // [subset] ref_frame_list[][] dropped (external RC / two-pass only).
    // Update frame index
    public readonly int[] update_ref_idx = new int[MAX_STATIC_GF_GROUP_LENGTH];
    // [subset] primary_ref_idx[] dropped (external RC only).

    // Indicates the level of parallelism in frame parallel encodes.
    // 0 : frame is independently encoded (not part of parallel encodes).
    // 1 : frame is the first in encode order in a given parallel encode set.
    // 2 : frame occurs later in encode order in a given parallel encode set.
    public readonly int[] frame_parallel_level = new int[MAX_STATIC_GF_GROUP_LENGTH];
    // Indicates whether a frame should act as non-reference frame.
    public readonly bool[] is_frame_non_ref = new bool[MAX_STATIC_GF_GROUP_LENGTH];
    // Indicates whether a frame is dropped.
    public readonly bool[] is_frame_dropped = new bool[MAX_STATIC_GF_GROUP_LENGTH];

    // Stores the display order hint of the frames not to be
    // refreshed by the current frame.
    // PORT-NOTE: jagged [MAX_STATIC_GF_GROUP_LENGTH][REF_FRAMES]; the only
    // cross-row memset (ethread.c, frame-parallel reset) is out of the subset.
    public readonly int[][] skip_frame_refresh = new_int_array2(MAX_STATIC_GF_GROUP_LENGTH, REF_FRAMES);
    // Stores the display order hint of the frame to be excluded during reference
    // assignment.
    public readonly int[] skip_frame_as_ref = new int[MAX_STATIC_GF_GROUP_LENGTH];
    // [subset] is_sframe_due, use_ext_ref_frame_map[] dropped (two-pass /
    // external RC only).

    private static int[][] new_int_array2(int n, int m)
    {
        int[][] a = new int[n][];
        for (int i = 0; i < n; i++) a[i] = new int[m];
        return a;
    }
}

internal sealed partial class STATS_BUFFER_CTX
{
    // [subset] stats_in_start, stats_in_end, stats_in_buf_end dropped: two-pass
    // stats input only.
    public FIRSTPASS_STATS total_stats;
    public FIRSTPASS_STATS total_left_stats;
}

/// <summary>Two pass status and control data.</summary>
/// <remarks>
/// PORT-NOTE: minimal. Only section_intra_rating is read by in-scope code
/// (picklpf.c, behind a two-pass condition). frame_stats_arr, stats_buf_ctx,
/// firstpass_info, bits_left, modified_error_*, kf_group_*, bpm_factor,
/// rolling_arf_group_*, sr_update_lag, kf_zeromotion_pct,
/// last_kfgroup_zeromotion_pct, extend_minq/maxq are two-pass only.
/// </remarks>
internal sealed partial class TWO_PASS
{
    public uint section_intra_rating;
}
