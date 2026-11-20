// Port of libaom av1/encoder/ratectrl.h
global using static Av1rt.Aom.ratectrl;
global using RATE_FACTOR_LEVEL = int;
global using FRAME_UPDATE_TYPE = int;
global using REFBUF_STATE = int;
global using RESIZE_ACTION = int;
global using RESIZE_STATE = int;

using System;

namespace Av1rt.Aom;

internal static partial class ratectrl
{
    // Bits Per MB at different Q (Multiplied by 512)
    internal const int BPER_MB_NORMBITS = 9;

    // Use this macro to turn on/off use of alt-refs in one-pass mode.
    internal const int USE_ALTREF_FOR_ONE_PASS = 1;

    // Threshold used to define if a KF group is static (e.g. a slide show).
    // Essentially, this means that no frame in the group has more than 1% of MBs
    // that are not marked as coded with 0,0 motion in the first pass.
    internal const int STATIC_KF_GROUP_THRESH = 99;
    internal const double STATIC_KF_GROUP_FLOAT_THRESH = 0.99;

    // The maximum duration of a GF group that is static (e.g. a slide show).
    internal const int MAX_STATIC_GF_GROUP_LENGTH = 250;

    internal const int MIN_GF_INTERVAL = 4;
    internal const int MAX_GF_INTERVAL = 32;
    internal const int FIXED_GF_INTERVAL = 16;
    internal const int MAX_GF_LENGTH_LAP = 16;

    internal const int FIXED_GF_INTERVAL_RT = 80;
    internal const int MAX_GF_INTERVAL_RT = 160;

    internal const int MAX_NUM_GF_INTERVALS = 15;

    internal const int MAX_ARF_LAYERS = 6;

    internal const int DEFAULT_KF_BOOST_RT = 2300;
    internal const int DEFAULT_GF_BOOST_RT = 2000;

    // A passive rate control strategy for screen content type in real-time mode.
    // Since RT mode has a tight restriction on buffer overflow control, it is
    // turned off by default.
    internal const int RT_PASSIVE_STRATEGY = 0;
    internal const int MAX_Q_HISTORY = 1000;

    // RATE_FACTOR_LEVEL
    internal const int INTER_NORMAL = 0;
    internal const int GF_ARF_LOW = 1;
    internal const int GF_ARF_STD = 2;
    internal const int KF_STD = 3;
    internal const int RATE_FACTOR_LEVELS = 4;

    // FRAME_UPDATE_TYPE
    internal const int KF_UPDATE = 0;
    internal const int LF_UPDATE = 1;
    internal const int GF_UPDATE = 2;
    internal const int ARF_UPDATE = 3;
    internal const int OVERLAY_UPDATE = 4;
    internal const int INTNL_OVERLAY_UPDATE = 5; // Internal Overlay Frame
    internal const int INTNL_ARF_UPDATE = 6;     // Internal Altref Frame
    internal const int FRAME_UPDATE_TYPES = 7;

    // REFBUF_STATE
    internal const int REFBUF_RESET = 0;  // Clear reference frame buffer
    internal const int REFBUF_UPDATE = 1; // Refresh reference frame buffer
    internal const int REFBUF_STATES = 2;

    // RESIZE_ACTION
    internal const int NO_RESIZE = 0;
    internal const int DOWN_THREEFOUR = 1; // From orig to 3/4.
    internal const int DOWN_ONEHALF = 2;   // From orig or 3/4 to 1/2.
    internal const int UP_THREEFOUR = -1;  // From 1/2 to 3/4.
    internal const int UP_ORIG = -2;       // From 1/2 or 3/4 to orig.

    // RESIZE_STATE
    internal const int ORIG = 0;
    internal const int THREE_QUARTER = 1;
    internal const int ONE_HALF = 2;

    internal const int MAX_FIRSTPASS_ANALYSIS_FRAMES = 150;
    // [subset] REGION_TYPES / REGIONS (first pass region analysis) not ported:
    // only used by two-pass / LAP code.
}

internal partial struct size_params_type
{
    public int resize_width;
    public int resize_height;
    public byte superres_denom;
}

/// <summary>Rate Control parameters and status.</summary>
/// <remarks>C copies it by value (SVC layer contexts, CODING_CONTEXT save/restore): use CopyFrom.</remarks>
internal sealed partial class RATE_CONTROL
{
    // Rate targetting variables

    /// Baseline target rate for frame before adjustment for previous under or over shoot.
    public int base_frame_target;
    /// Target rate for frame after adjustment for previous under or over shoot.
    public int this_frame_target; // Actual frame target after rc adjustment.

    /// Projected size for current frame
    public int projected_frame_size;

    /// Bit size of transform coefficient for current frame.
    public int coefficient_size;

    /// Super block rate target used with some adaptive quantization strategies.
    public int sb64_target_rate;

    /// Number of frames since the last ARF / GF.
    public int frames_since_golden;

    /// Number of frames till the next ARF / GF is due.
    public int frames_till_gf_update_due;

    /// Number of determined gf groups left
    public int intervals_till_gf_calculate_due;

    public int min_gf_interval;
    public int max_gf_interval;
    public int static_scene_max_gf_interval;
    /// Frames before the next key frame
    public int frames_to_key;
    public int frames_since_key;
    public int frames_to_fwd_kf;
    public int is_src_frame_alt_ref;
    public int sframe_due;

    public int high_source_sad;
    // [subset] high_source_sad_lag[MAX_GF_INTERVAL] dropped: only used with
    // lag_in_frames > 0 (rt lag / LAP).
    public int high_motion_content_screen_rtc;
    public ulong avg_source_sad;
    public ulong prev_avg_source_sad;
    public ulong frame_source_sad;
    // [subset] frame_source_sad_lag[MAX_GF_INTERVAL] dropped (see above).
    public uint last_frame_low_source_sad;
    public ulong frame_spatial_variance;
    public int static_since_last_scene_change;
    public int last_encoded_size_keyframe;
    public int last_target_size_keyframe;
    public int frames_since_scene_change;
    public int perc_spatial_flat_blocks;
    public int num_col_blscroll_last_tl0;
    public int num_row_blscroll_last_tl0;

    public int avg_frame_bandwidth; // Average frame size target for clip
    public int min_frame_bandwidth; // Minimum allocation used for any frame
    public int max_frame_bandwidth; // Maximum burst rate allowed for a frame.
    public int prev_avg_frame_bandwidth;

    public int ni_av_qi;
    public int ni_tot_qi;

    public int decimation_factor;
    public int decimation_count;
    public int prev_frame_is_dropped;
    public int drop_count_consec;
    public int max_consec_drop;
    public int force_max_q;
    public int postencode_drop;

    /// Frame number for encoded frames (non-dropped). Use for setting the qp for
    /// the first encoded frame, so the qp for a dropped first frame is not used.
    public uint frame_number_encoded;

    /// User specified maximum Q allowed for current frame
    public int worst_quality;
    /// User specified minimum Q allowed for current frame
    public int best_quality;

    // rate control history for last frame(1) and the frame before(2).
    // -1: overshoot
    //  1: undershoot
    //  0: not initialized.
    public int rc_1_frame;
    public int rc_2_frame;
    public int q_1_frame;
    public int q_2_frame;

    /// Proposed maximum allowed Q for current frame
    public int active_worst_quality;

    // Track amount of low motion in scene
    public int avg_frame_low_motion;
    public int cnt_zeromv;

    // signals if number of blocks with motion is high
    public int percent_blocks_with_motion;

    // signals percentage of 16x16 blocks that are inactive, via active_maps
    public int percent_blocks_inactive;

    // Maximum value of source sad across all blocks of frame.
    public ulong max_block_source_sad;

    // For dynamic resize, 1 pass cbr.
    public RESIZE_STATE resize_state;
    public int resize_avg_qp;
    public int resize_buffer_underflow;
    public int resize_count;

    // Flag to disable content related qp adjustment.
    public int rtc_external_ratectrl;

    // Stores fast_extra_bits of the current frame.
    public int frame_level_fast_extra_bits;

    public readonly double[] frame_level_rate_correction_factors = new double[RATE_FACTOR_LEVELS];

    public int frame_num_last_gf_refresh;

    public int prev_coded_width;
    public int prev_coded_height;

    // The ratio used for inter frames in bit estimation.
    public int bit_est_ratio;

    // Whether to use a fixed qp for the frame, bypassing internal rate control.
    // This flag will reset to 0 after every frame.
    public int use_external_qp_one_pass;

    /// <summary>C struct assignment (<c>*this = *other</c>).</summary>
    public void CopyFrom(RATE_CONTROL other)
    {
        base_frame_target = other.base_frame_target;
        this_frame_target = other.this_frame_target;
        projected_frame_size = other.projected_frame_size;
        coefficient_size = other.coefficient_size;
        sb64_target_rate = other.sb64_target_rate;
        frames_since_golden = other.frames_since_golden;
        frames_till_gf_update_due = other.frames_till_gf_update_due;
        intervals_till_gf_calculate_due = other.intervals_till_gf_calculate_due;
        min_gf_interval = other.min_gf_interval;
        max_gf_interval = other.max_gf_interval;
        static_scene_max_gf_interval = other.static_scene_max_gf_interval;
        frames_to_key = other.frames_to_key;
        frames_since_key = other.frames_since_key;
        frames_to_fwd_kf = other.frames_to_fwd_kf;
        is_src_frame_alt_ref = other.is_src_frame_alt_ref;
        sframe_due = other.sframe_due;
        high_source_sad = other.high_source_sad;
        high_motion_content_screen_rtc = other.high_motion_content_screen_rtc;
        avg_source_sad = other.avg_source_sad;
        prev_avg_source_sad = other.prev_avg_source_sad;
        frame_source_sad = other.frame_source_sad;
        last_frame_low_source_sad = other.last_frame_low_source_sad;
        frame_spatial_variance = other.frame_spatial_variance;
        static_since_last_scene_change = other.static_since_last_scene_change;
        last_encoded_size_keyframe = other.last_encoded_size_keyframe;
        last_target_size_keyframe = other.last_target_size_keyframe;
        frames_since_scene_change = other.frames_since_scene_change;
        perc_spatial_flat_blocks = other.perc_spatial_flat_blocks;
        num_col_blscroll_last_tl0 = other.num_col_blscroll_last_tl0;
        num_row_blscroll_last_tl0 = other.num_row_blscroll_last_tl0;
        avg_frame_bandwidth = other.avg_frame_bandwidth;
        min_frame_bandwidth = other.min_frame_bandwidth;
        max_frame_bandwidth = other.max_frame_bandwidth;
        prev_avg_frame_bandwidth = other.prev_avg_frame_bandwidth;
        ni_av_qi = other.ni_av_qi;
        ni_tot_qi = other.ni_tot_qi;
        decimation_factor = other.decimation_factor;
        decimation_count = other.decimation_count;
        prev_frame_is_dropped = other.prev_frame_is_dropped;
        drop_count_consec = other.drop_count_consec;
        max_consec_drop = other.max_consec_drop;
        force_max_q = other.force_max_q;
        postencode_drop = other.postencode_drop;
        frame_number_encoded = other.frame_number_encoded;
        worst_quality = other.worst_quality;
        best_quality = other.best_quality;
        rc_1_frame = other.rc_1_frame;
        rc_2_frame = other.rc_2_frame;
        q_1_frame = other.q_1_frame;
        q_2_frame = other.q_2_frame;
        active_worst_quality = other.active_worst_quality;
        avg_frame_low_motion = other.avg_frame_low_motion;
        cnt_zeromv = other.cnt_zeromv;
        percent_blocks_with_motion = other.percent_blocks_with_motion;
        percent_blocks_inactive = other.percent_blocks_inactive;
        max_block_source_sad = other.max_block_source_sad;
        resize_state = other.resize_state;
        resize_avg_qp = other.resize_avg_qp;
        resize_buffer_underflow = other.resize_buffer_underflow;
        resize_count = other.resize_count;
        rtc_external_ratectrl = other.rtc_external_ratectrl;
        frame_level_fast_extra_bits = other.frame_level_fast_extra_bits;
        other.frame_level_rate_correction_factors.AsSpan(0, RATE_FACTOR_LEVELS).CopyTo(frame_level_rate_correction_factors);
        frame_num_last_gf_refresh = other.frame_num_last_gf_refresh;
        prev_coded_width = other.prev_coded_width;
        prev_coded_height = other.prev_coded_height;
        bit_est_ratio = other.bit_est_ratio;
        use_external_qp_one_pass = other.use_external_qp_one_pass;
    }
}

/// <summary>
/// Primary Rate Control parameters and status (shared by the frame contexts of
/// AV1_PRIMARY).
/// </summary>
/// <remarks>C copies it by value (SVC layer contexts): use CopyFrom.</remarks>
internal sealed partial class PRIMARY_RATE_CONTROL
{
    // [subset] gf_group_bits, gfu_boost_average, gf_intervals[], cur_gf_index,
    // num_regions, regions[], regions_offset, frames_till_regions_update dropped:
    // two-pass / LAP GF group allocation only.

    /// Boost factor used to calculate the extra bits allocated to the key frame
    public int kf_boost;

    /// Boost factor used to calculate the extra bits allocated to ARFs and GFs
    public int gfu_boost;

    /// Stores the determined gf group lengths for a set amount of gf groups
    public int baseline_gf_interval;
    public int constrained_gf_group;
    public int this_key_frame_forced;
    public int next_key_frame_forced;

    /// Initial buffuer level in ms for CBR / low delay encoding
    public long starting_buffer_level;

    /// Optimum / target buffuer level in ms for CBR / low delay encoding
    public long optimal_buffer_level;

    /// Maximum target buffuer level in ms for CBR / low delay encoding
    public long maximum_buffer_size;

    /// Q index used for ALT frame
    public int arf_q;

    // [subset] arf_boost_factor, base_layer_qp, num_stats_used_for_kf_boost,
    // num_stats_used_for_gfu_boost, num_stats_required_for_gfu_boost dropped
    // (two-pass / LAP / SVC-only).

    /// Enable scenecut detection
    public int enable_scenecut_detection;

    // [subset] use_arf_in_this_kf_group dropped (two-pass only).

    public int ni_frames;

    public double tot_q;

    /// Q used for last boosted (non leaf) frame
    public int last_kf_qindex;

    /// Average of q index of previous encoded frames in a sequence.
    public readonly int[] avg_frame_qindex = new int[FRAME_TYPES];

    // [subset] temp_* fields: CONFIG_FPMT_TEST == 0.
    // [subset] active_best_quality[MAX_ARF_LAYERS + 1] dropped: only read for
    // pyramid levels > 1 (lagged GF groups).

    /// Q used for last boosted (non leaf) frame (GF/KF/ARF)
    public int last_boosted_qindex;

    /// Average Q value of previous inter frames
    public double avg_q;

    /// Q used on last encoded frame of the given type.
    public readonly int[] last_q = new int[FRAME_TYPES];

    /// Correction factors used to adjust the q estimate for a given target rate
    /// in the encode loop.
    public readonly double[] rate_correction_factors = new double[RATE_FACTOR_LEVELS];

    /// Number of bits spent on the frames so far.
    public long total_actual_bits;

    /// Total number of bits allocated to the frames so far.
    public long total_target_bits;

    /// Buffer level.
    public long buffer_level;

    // [subset] rate_error_estimate, vbr_bits_off_target, vbr_bits_off_target_fast
    // dropped (VBR / two-pass only).

    /// Bits allocated to frames minus bits actually used (CBR buffer model).
    public long bits_off_target;

    /// Rolling monitor target bits updated based on current frame target size.
    public int rolling_target_bits;

    /// Rolling monitor actual bits updated based on current frame final projected
    /// size.
    public int rolling_actual_bits;

    // [subset] q_history[MAX_Q_HISTORY] dropped (only written/read by the
    // unexecuted lagged rt path).

    /// <summary>C struct assignment (<c>*this = *other</c>).</summary>
    public void CopyFrom(PRIMARY_RATE_CONTROL other)
    {
        kf_boost = other.kf_boost;
        gfu_boost = other.gfu_boost;
        baseline_gf_interval = other.baseline_gf_interval;
        constrained_gf_group = other.constrained_gf_group;
        this_key_frame_forced = other.this_key_frame_forced;
        next_key_frame_forced = other.next_key_frame_forced;
        starting_buffer_level = other.starting_buffer_level;
        optimal_buffer_level = other.optimal_buffer_level;
        maximum_buffer_size = other.maximum_buffer_size;
        arf_q = other.arf_q;
        enable_scenecut_detection = other.enable_scenecut_detection;
        ni_frames = other.ni_frames;
        tot_q = other.tot_q;
        last_kf_qindex = other.last_kf_qindex;
        other.avg_frame_qindex.AsSpan(0, FRAME_TYPES).CopyTo(avg_frame_qindex);
        last_boosted_qindex = other.last_boosted_qindex;
        avg_q = other.avg_q;
        other.last_q.AsSpan(0, FRAME_TYPES).CopyTo(last_q);
        other.rate_correction_factors.AsSpan(0, RATE_FACTOR_LEVELS).CopyTo(rate_correction_factors);
        total_actual_bits = other.total_actual_bits;
        total_target_bits = other.total_target_bits;
        buffer_level = other.buffer_level;
        bits_off_target = other.bits_off_target;
        rolling_target_bits = other.rolling_target_bits;
        rolling_actual_bits = other.rolling_actual_bits;
    }
}
