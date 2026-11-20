// Port of libaom av1/encoder/svc_layercontext.h
global using static Av1rt.Aom.svc_layercontext;

namespace Av1rt.Aom;

// SVC (spatial/temporal layers) is out of the subset: no function of
// svc_layercontext.c runs (use_svc == 0, one spatial and one temporal layer).
// The structures are ported because executed code reads SVC fields
// (spatial_layer_id, number_spatial_layers, ...) and tests use_svc-guarded
// layer_context accesses.
internal static partial class svc_layercontext
{
}

/// <summary>The stucture of quantities related to each spatial and temporal layer.</summary>
internal sealed partial class LAYER_CONTEXT
{
    public RATE_CONTROL rc = new();
    public PRIMARY_RATE_CONTROL p_rc = new();
    public int framerate_factor;
    public long layer_target_bitrate; // In bits per second.
    public int scaling_factor_num;
    public int scaling_factor_den;
    public long target_bandwidth;
    public long spatial_layer_target_bandwidth;
    public double framerate;
    public int avg_frame_size;
    public int max_q;
    public int min_q;
    public int frames_from_key_frame;

    /// Cyclic refresh parameters (aq-mode=3), that need to be updated per-frame.
    public int sb_index;
    /// Segmentation map
    public Ptr<sbyte> map;
    /// Number of blocks on segment 1
    public int actual_num_seg1_blocks;
    /// Number of blocks on segment 2
    public int actual_num_seg2_blocks;
    /// Counter used to detect scene change.
    public int counter_encode_maxq_scene_change;
    /// Speed settings for each layer.
    public byte speed;
    /// GF group index.
    public byte group_index;
    /// If current layer is key frame.
    public int is_key_frame;
    /// Maximum motion magnitude of previous encoded layer.
    public int max_mv_magnitude;
}

/// <summary>The stucture of SVC.</summary>
internal sealed partial class SVC
{
    public int spatial_layer_id;
    public int temporal_layer_id;
    public int number_spatial_layers;
    public int number_temporal_layers;
    public int prev_number_spatial_layers;
    public int prev_number_temporal_layers;
    public int use_flexible_mode;
    public int ksvc_fixed_mode;

    public double base_framerate;
    public uint current_superframe;
    public int skip_mvsearch_last;
    public int skip_mvsearch_gf;
    public int skip_mvsearch_altref;
    public readonly int[] spatial_layer_fb = new int[REF_FRAMES];
    public readonly int[] temporal_layer_fb = new int[REF_FRAMES];
    public int num_encoded_top_layer;
    public int first_layer_denoise;
    public YV12_BUFFER_CONFIG source_last_TL0 = new();
    public YV12_BUFFER_CONFIG source_half = new();
    public YV12_BUFFER_CONFIG last_source_half = new();
    public uint source_half_superframe;
    public int source_half_valid;
    public InterpFilter source_half_filter;
    public int source_half_phase;

    /// Layer context used for rate control in CBR mode.
    /// An array. The index for spatial layer `sl` and temporal layer `tl` is
    /// sl * number_temporal_layers + tl.
    public LAYER_CONTEXT[] layer_context;

    /// Number of layers allocated for layer_context. If nonzero, must be greater
    /// than or equal to number_spatial_layers * number_temporal_layers.
    public int num_allocated_layers;

    /// EIGHTTAP_SMOOTH or BILINEAR
    public readonly InterpFilter[] downsample_filter_type = new InterpFilter[AOM_MAX_SS_LAYERS];

    /// Downsample_filter_phase: = 0 will do sub-sampling (no weighted average),
    /// = 8 will center the target pixel and get a symmetric averaging filter.
    public readonly int[] downsample_filter_phase = new int[AOM_MAX_SS_LAYERS];

    /// Force zero-mv in mode search for the spatial/inter-layer reference.
    public int force_zero_mode_spatial_ref;

    /// Flag to indicate that current spatial layer has a lower quality layer
    /// (at the same timestamp) that can be used as a reference.
    public int has_lower_quality_layer;

    /// Flag to indicate the frame drop mode for SVC: one of the two settings:
    /// AOM_LAYER_DROP (default) or AOM_FULL_SUPERFRAME_DROP.
    public AOM_SVC_FRAME_DROP_MODE framedrop_mode;

    /// Flag to indicate if frame was dropped for a given spatial_layer_id on
    /// previous superframe.
    public readonly bool[] last_layer_dropped = new bool[AOM_MAX_SS_LAYERS];

    /// Flag to indicate if a previous spatial was dropped for the same superframe.
    public readonly bool[] drop_spatial_layer = new bool[AOM_MAX_SS_LAYERS];
}
