// Port of libaom av1/common/av1_common_int.h

// <gen:aliases>
global using REFERENCE_MODE = int;
global using REFRESH_FRAME_CONTEXT_MODE = int;
// </gen:aliases>
global using AV1Common = Av1rt.Aom.AV1_COMMON;

global using static Av1rt.Aom.av1_common_int;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal partial struct TPL_MV_REF
{
    public int_mv mfmv0;
    public byte ref_frame_offset;
}

internal partial struct MV_REF
{
    public int_mv mv;
    public MV_REFERENCE_FRAME ref_frame;
}

internal sealed partial class RefCntBuffer
{
    // For a RefCntBuffer, the following are reference-holding variables:
    // - cm->ref_frame_map[]
    // - cm->cur_frame
    // - cm->scaled_ref_buf[] (encoder only)
    // - pbi->output_frames[] (decoder only)
    // - pbi->output_frame_index[] (decoder only)
    // With that definition, 'ref_count' is the number of reference-holding
    // variables that are currently referencing this buffer.
    // For example:
    // - suppose this buffer is at index 'k' in the buffer pool, and
    // - Total 'n' of the variables / array elements above have value 'k' (that
    // is, they are pointing to buffer at index 'k').
    // Then, pool->frame_bufs[k].ref_count = n.
    public int ref_count;

    public uint order_hint;
    public readonly uint[] ref_order_hints = new uint[INTER_REFS_PER_FRAME];

    // These variables are used only in encoder and compare the absolute
    // display order hint to compute the relative distance and overcome
    // the limitation of get_relative_dist() which returns incorrect
    // distance when a very old frame is used as a reference.
    public uint display_order_hint;
    public readonly uint[] ref_display_order_hint = new uint[INTER_REFS_PER_FRAME];
    // Frame's level within the hierarchical structure.
    public uint pyramid_level;
    // This frame's base qindex.
    public int base_qindex;
    public Ptr<MV_REF> mvs;
    public Ptr<byte> seg_map;
    public readonly segmentation seg = new();
    public int mi_rows;
    public int mi_cols;
    // Width and height give the size of the buffer (before any upscaling, unlike
    // the sizes that can be derived from the buf structure)
    public int width;
    public int height;
    public readonly WarpedMotionParams[] global_motion = new WarpedMotionParams[REF_FRAMES];
    public int showable_frame;  // frame can be used as show existing frame in future
    public byte film_grain_params_present;
    public aom_film_grain_t film_grain_params;
    public aom_codec_frame_buffer_t raw_frame_buffer = new();
    public YV12_BUFFER_CONFIG buf = new();
    public int temporal_id;  // Temporal layer ID of the frame
    public int spatial_id;   // Spatial layer ID of the frame
    public FRAME_TYPE frame_type;

    // This is only used in the encoder but needs to be indexed per ref frame
    // so it's extremely convenient to keep it here.
    public readonly int[] interp_filter_selected = new int[SWITCHABLE];

    // Inter frame reference frame delta for loop filter
    public readonly sbyte[] ref_deltas = new sbyte[REF_FRAMES];

    // 0 = ZERO_MV, MV
    public readonly sbyte[] mode_deltas = new sbyte[MAX_MODE_LF_DELTAS];

    public readonly FRAME_CONTEXT frame_context = new();

    // Frame-level filter level
    public readonly int[] filter_level = new int[2];
}

internal sealed partial class BufferPool
{
    // Protect BufferPool from being accessed by several FrameWorkers at
    // the same time during frame parallel decode.
    // TODO(hkuang): Try to use atomic variable instead of locking the whole pool.
    // TODO(wtc): Remove this. See
    // https://chromium-review.googlesource.com/c/webm/libvpx/+/560630.
    // [port] Created by pthread_mutex_init(ref pool.pool_mutex, ...) (aom_pthread.h.cs).
    public pthread_mutex_t pool_mutex;

    // Private data associated with the frame buffer callbacks.
    public object cb_priv;

    public aom_get_frame_buffer_cb_fn_t get_fb_cb;
    public aom_release_frame_buffer_cb_fn_t release_fb_cb;

    public RefCntBuffer[] frame_bufs;
    public byte num_frame_bufs;

    // Frame buffers allocated internally by the codec.
    public InternalFrameBufferList int_frame_buffers;
}

/*!\cond */
// [port] Class with CopyFrom: the encoder saves/restores it whole
// (`cc->cdef_info = cm->cdef_info` in the coding context).
internal sealed partial class CdefInfo
{
    public readonly Ptr<ushort>[] colbuf = new Ptr<ushort>[MAX_MB_PLANE];
    public readonly Ptr<ushort>[] linebuf = new Ptr<ushort>[MAX_MB_PLANE];
    public Ptr<ushort> srcbuf;
    public readonly int[] allocated_colbuf_size = new int[MAX_MB_PLANE];
    public readonly int[] allocated_linebuf_size = new int[MAX_MB_PLANE];
    public int allocated_srcbuf_size;
    public int cdef_damping;
    public int nb_cdef_strengths;
    public readonly int[] cdef_strengths = new int[CDEF_MAX_STRENGTHS];
    public readonly int[] cdef_uv_strengths = new int[CDEF_MAX_STRENGTHS];
    public int cdef_bits;
    public int allocated_mi_rows;
    public int allocated_num_workers;

    /// <summary>C struct assignment: copies every field (arrays element-wise).</summary>
    public void CopyFrom(CdefInfo other)
    {
        av1_copy(colbuf, other.colbuf);
        av1_copy(linebuf, other.linebuf);
        srcbuf = other.srcbuf;
        av1_copy(allocated_colbuf_size, other.allocated_colbuf_size);
        av1_copy(allocated_linebuf_size, other.allocated_linebuf_size);
        allocated_srcbuf_size = other.allocated_srcbuf_size;
        cdef_damping = other.cdef_damping;
        nb_cdef_strengths = other.nb_cdef_strengths;
        av1_copy(cdef_strengths, other.cdef_strengths);
        av1_copy(cdef_uv_strengths, other.cdef_uv_strengths);
        cdef_bits = other.cdef_bits;
        allocated_mi_rows = other.allocated_mi_rows;
        allocated_num_workers = other.allocated_num_workers;
    }
}

internal partial struct DeltaQInfo
{
    public int delta_q_present_flag;
    public int delta_q_res;
    public int delta_lf_present_flag;
    public int delta_lf_res;
    public int delta_lf_multi;
}

internal partial struct OrderHintInfo
{
    public int enable_order_hint;        // 0 - disable order hint, and related tools
    public int order_hint_bits_minus_1;  // dist_wtd_comp, ref_frame_mvs,
                                         // frame_sign_bias
                                         // if 0, enable_dist_wtd_comp and
                                         // enable_ref_frame_mvs must be set as 0.
    public int enable_dist_wtd_comp;     // 0 - disable dist-wtd compound modes
                                         // 1 - enable it
    public int enable_ref_frame_mvs;     // 0 - disable ref frame mvs
                                         // 1 - enable it
}

// Sequence header structure.
// Note: All syntax elements of sequence_header_obu that need to be
// bit-identical across multiple sequence headers must be part of this struct,
// so that consistency is checked by are_seq_headers_consistent() function.
// One exception is the last member 'op_params' that is ignored by
// are_seq_headers_consistent() function.
internal sealed partial class SequenceHeader
{
    public int num_bits_width;
    public int num_bits_height;
    public int max_frame_width;
    public int max_frame_height;
    // Whether current and reference frame IDs are signaled in the bitstream.
    // Frame id numbers are additional information that do not affect the
    // decoding process, but provide decoders with a way of detecting missing
    // reference frames so that appropriate action can be taken.
    public byte frame_id_numbers_present_flag;
    public int frame_id_length;
    public int delta_frame_id_length;
    public BLOCK_SIZE sb_size;  // Size of the superblock used for this frame
    public int mib_size;        // Size of the superblock in units of MI blocks
    public int mib_size_log2;   // Log 2 of above.

    public OrderHintInfo order_hint_info;

    public byte force_screen_content_tools;  // 0 - force off
                                             // 1 - force on
                                             // 2 - adaptive
    public byte still_picture;               // Video is a single frame still picture
    public byte reduced_still_picture_hdr;   // Use reduced header for still picture
    public byte force_integer_mv;            // 0 - Don't force. MV can use subpel
                                             // 1 - force to integer
                                             // 2 - adaptive
    public byte enable_filter_intra;         // enables/disables filterintra
    public byte enable_intra_edge_filter;    // enables/disables edge upsampling
    public byte enable_interintra_compound;  // enables/disables interintra_compound
    public byte enable_masked_compound;      // enables/disables masked compound
    public byte enable_dual_filter;          // 0 - disable dual interpolation filter
                                             // 1 - enable vert/horz filter selection
    public byte enable_warped_motion;        // 0 - disable warp for the sequence
                                             // 1 - enable warp for the sequence
    public byte enable_superres;             // 0 - Disable superres for the sequence
                                             //     and no frame level superres flag
                                             // 1 - Enable superres for the sequence
                                             //     enable per-frame superres flag
    public byte enable_cdef;                 // To turn on/off CDEF
    public byte enable_restoration;          // To turn on/off loop restoration
    public BITSTREAM_PROFILE profile;

    // Color config.
    public aom_bit_depth_t bit_depth;  // AOM_BITS_8 in profile 0 or 1,
                                       // AOM_BITS_10 or AOM_BITS_12 in profile 2 or 3.
    public byte use_highbitdepth;      // If true, we need to use 16bit frame buffers.
    public byte monochrome;            // Monochrome video
    public aom_color_primaries_t color_primaries;
    public aom_transfer_characteristics_t transfer_characteristics;
    public aom_matrix_coefficients_t matrix_coefficients;
    public int color_range;
    public int subsampling_x;  // Chroma subsampling for x
    public int subsampling_y;  // Chroma subsampling for y
    public aom_chroma_sample_position_t chroma_sample_position;
    public byte separate_uv_delta_q;
    public byte film_grain_params_present;

    // Operating point info.
    public int operating_points_cnt_minus_1;
    public readonly int[] operating_point_idc = new int[MAX_NUM_OPERATING_POINTS];
    // True if operating_point_idc[op] is not equal to 0 for any value of op from
    // 0 to operating_points_cnt_minus_1.
    public bool has_nonzero_operating_point_idc;
    public int timing_info_present;
    public aom_timing_info_t timing_info;
    public byte decoder_model_info_present_flag;
    public aom_dec_model_info_t decoder_model_info;
    public byte display_model_info_present_flag;
    public readonly AV1_LEVEL[] seq_level_idx = new AV1_LEVEL[MAX_NUM_OPERATING_POINTS];
    public readonly byte[] tier = new byte[MAX_NUM_OPERATING_POINTS];  // seq_tier in spec. One bit: 0 or 1.

    // IMPORTANT: the op_params member must be at the end of the struct so that
    // are_seq_headers_consistent() can be implemented with a memcmp() call.
    // TODO(urvang): We probably don't need the +1 here.
    public readonly aom_dec_model_op_parameters_t[] op_params =
        new aom_dec_model_op_parameters_t[MAX_NUM_OPERATING_POINTS + 1];
}

internal partial struct SkipModeInfo
{
    public int skip_mode_allowed;
    public int skip_mode_flag;
    public int ref_frame_idx_0;
    public int ref_frame_idx_1;
}

internal sealed partial class CurrentFrame
{
    public FRAME_TYPE frame_type;
    public REFERENCE_MODE reference_mode;

    public uint order_hint;
    public uint display_order_hint;
    // Frame's level within the hierarchical structure.
    public uint pyramid_level;
    public uint frame_number;
    public SkipModeInfo skip_mode_info;
    public int refresh_frame_flags;  // Which ref frames are overwritten by this frame
    public int frame_refs_short_signaling;
}

/*!\endcond */

/*!
 * \brief Frame level features.
 */
internal sealed partial class FeatureFlags
{
    // If true, CDF update in the symbol encoding/decoding process is disabled.
    public bool disable_cdf_update;
    // If true, motion vectors are specified to eighth pel precision; and
    // if false, motion vectors are specified to quarter pel precision.
    public bool allow_high_precision_mv;
    // If true, force integer motion vectors; if false, use the default.
    public bool cur_frame_force_integer_mv;
    // If true, palette tool and/or intra block copy tools may be used.
    public bool allow_screen_content_tools;
    public bool allow_intrabc;        // If true, intra block copy tool may be used.
    public bool allow_warped_motion;  // If true, frame may use warped motion mode.
    // If true, using previous frames' motion vectors for prediction is allowed.
    public bool allow_ref_frame_mvs;
    // If true, frame is fully lossless at coded resolution.
    public bool coded_lossless;
    // If true, frame is fully lossless at coded and upscaled resolutions.
    public bool all_lossless;
    // If true, the frame is restricted to a reduced subset of the full set of
    // transform types.
    public bool reduced_tx_set_used;
    // If true, error resilient mode is enabled.
    // Note: Error resilient mode allows the syntax of a frame to be parsed
    // independently of previously decoded frames.
    public bool error_resilient_mode;
    // If false, only MOTION_MODE that may be used is SIMPLE_TRANSLATION;
    // if true, all MOTION_MODES may be used.
    public bool switchable_motion_mode;
    public TX_MODE tx_mode;             // Transform mode at frame level.
    public InterpFilter interp_filter;  // Interpolation filter at frame level.
    // The frame context index used as the primary reference frame (or
    // PRIMARY_REF_NONE).
    public int primary_ref_frame;
    // Byte alignment of the planes in the reference buffers.
    public int byte_alignment;
    // Flag signaling how frame contexts should be updated at the end of
    // a frame decode.
    public REFRESH_FRAME_CONTEXT_MODE refresh_frame_context;
}

/*!
 * \brief Params related to tiles.
 */
internal sealed partial class CommonTileParams
{
    public int cols;           // number of tile columns that frame is divided into
    public int rows;           // number of tile rows that frame is divided into
    public int max_width_sb;   // maximum tile width in superblock units.
    public int max_height_sb;  // maximum tile height in superblock units.

    // Min width of non-rightmost tile in MI units. Only valid if cols > 1.
    public int min_inner_width;

    // If true, tiles are uniformly spaced with power-of-2 number of rows and
    // columns.
    // If false, tiles have explicitly configured widths and heights.
    public int uniform_spacing;

    // Only valid when uniform_spacing is true:
    public int log2_cols;  // log2 of 'cols'.
    public int log2_rows;  // log2 of 'rows'.
    public int width;      // tile width in MI units
    public int height;     // tile height in MI units

    // Min num of tile columns possible based on 'max_width_sb' and frame width.
    public int min_log2_cols;
    // Min num of tile rows possible based on 'max_height_sb' and frame height.
    public int min_log2_rows;
    // Min num of tile columns possible based on frame width.
    public int max_log2_cols;
    // Max num of tile columns possible based on frame width.
    public int max_log2_rows;
    // log2 of min number of tiles (same as min_log2_cols + min_log2_rows).
    public int min_log2;
    // col_start_sb[i] is the start position of tile column i in superblock units.
    // valid for 0 <= i <= cols
    public readonly int[] col_start_sb = new int[MAX_TILE_COLS + 1];
    // row_start_sb[i] is the start position of tile row i in superblock units.
    // valid for 0 <= i <= rows
    public readonly int[] row_start_sb = new int[MAX_TILE_ROWS + 1];
    // If true, we are using large scale tile mode.
    public uint large_scale;
    // Only relevant when large_scale == 1.
    // If true, the independent decoding of a single tile or a section of a frame
    // is allowed.
    public uint single_tile_decoding;
}

/*!
 * \brief Params related to MB_MODE_INFO arrays and related info.
 */
internal sealed partial class CommonModeInfoParams
{
    // Number of rows in the frame in 16 pixel units.
    // This is computed from frame height aligned to a multiple of 8.
    public int mb_rows;
    // Number of cols in the frame in 16 pixel units.
    // This is computed from frame width aligned to a multiple of 8.
    public int mb_cols;

    // Total MBs = mb_rows * mb_cols.
    public int MBs;

    // Number of rows in the frame in 4 pixel (MB_MODE_INFO) units.
    // This is computed from frame height aligned to a multiple of 8.
    public int mi_rows;
    // Number of cols in the frame in 4 pixel (MB_MODE_INFO) units.
    // This is computed from frame width aligned to a multiple of 8.
    public int mi_cols;

    // An array of MB_MODE_INFO structs for every 'mi_alloc_bsize' sized block
    // in the frame.
    // Note: This array should be treated like a scratch memory, and should NOT be
    // accessed directly, in most cases. Please use 'mi_grid_base' array instead.
    public MB_MODE_INFO[] mi_alloc;
    // Number of allocated elements in 'mi_alloc'.
    public int mi_alloc_size;
    // Stride for 'mi_alloc' array.
    public int mi_alloc_stride;
    // The minimum block size that each element in 'mi_alloc' can correspond to.
    // For decoder, this is always BLOCK_4X4.
    // For encoder, this is BLOCK_8X8 for resolution >= 4k case or REALTIME mode
    // case. Otherwise, this is BLOCK_4X4.
    // TODO(urvang): Can we change this to BLOCK_8X8 for all cases?
    public BLOCK_SIZE mi_alloc_bsize;

    // Grid of pointers to 4x4 MB_MODE_INFO structs allocated in 'mi_alloc'.
    // It's possible that:
    // - Multiple pointers in the grid point to the same element in 'mi_alloc'
    // (for example, for all 4x4 blocks that belong to the same partition block).
    // - Some pointers can be NULL (for example, for blocks outside visible area).
    public MB_MODE_INFO[] mi_grid_base;
    // Number of allocated elements in 'mi_grid_base' (and 'tx_type_map' also).
    public int mi_grid_size;
    // Stride for 'mi_grid_base' (and 'tx_type_map' also).
    public int mi_stride;

    // An array of tx types for each 4x4 block in the frame.
    // Number of allocated elements is same as 'mi_grid_size', and stride is
    // same as 'mi_grid_size'. So, indexing into 'tx_type_map' is same as that of
    // 'mi_grid_base'.
    // PORT-NOTE: TX_TYPE * in C; Ptr<byte> like MACROBLOCKD.tx_type_map (see there).
    public Ptr<byte> tx_type_map;

    // Function pointers to allow separate logic for encoder and decoder.
    // [port] C has no typedef names for these function pointer types.
    public Action<CommonModeInfoParams> free_mi;
    public Action<CommonModeInfoParams> setup_mi;
    public Action<CommonModeInfoParams, int, int, BLOCK_SIZE> set_mb_mi;
}

/*!
 * \brief Parameters related to quantization at the frame level.
 */
internal sealed partial class CommonQuantParams
{
    // Base qindex of the frame in the range 0 to 255.
    public int base_qindex;

    // Sharpness level of the frame.
    public int sharpness;

    // Delta of qindex (from base_qindex) for Y plane DC coefficient.
    // Note: y_ac_delta_q is implicitly 0.
    public int y_dc_delta_q;

    // Delta of qindex (from base_qindex) for U plane DC coefficients.
    public int u_dc_delta_q;
    // Delta of qindex (from base_qindex) for U plane AC coefficients.
    public int v_dc_delta_q;

    // Delta of qindex (from base_qindex) for V plane DC coefficients.
    // Same as those for U plane if cm->seq_params->separate_uv_delta_q == 0.
    public int u_ac_delta_q;
    // Delta of qindex (from base_qindex) for V plane AC coefficients.
    // Same as those for U plane if cm->seq_params->separate_uv_delta_q == 0.
    public int v_ac_delta_q;

    // The dequantizers below are true dequantizers used only in the
    // dequantization process.  They have the same coefficient
    // shift/scale as TX.
    public readonly short[][] y_dequant_QTX = new_jagged<short>(MAX_SEGMENTS, 2);  // Dequant for Y plane
    public readonly short[][] u_dequant_QTX = new_jagged<short>(MAX_SEGMENTS, 2);  // Dequant for U plane
    public readonly short[][] v_dequant_QTX = new_jagged<short>(MAX_SEGMENTS, 2);  // Dequant for V plane

    // Global quant matrix tables
    public readonly Ptr<qm_val_t>[][][] giqmatrix = new_jagged<Ptr<qm_val_t>>(NUM_QM_LEVELS, 3, TX_SIZES_ALL);
    public readonly Ptr<qm_val_t>[][][] gqmatrix = new_jagged<Ptr<qm_val_t>>(NUM_QM_LEVELS, 3, TX_SIZES_ALL);

    // Local quant matrix tables for each frame
    public readonly Ptr<qm_val_t>[][] y_iqmatrix = new_jagged<Ptr<qm_val_t>>(MAX_SEGMENTS, TX_SIZES_ALL);
    public readonly Ptr<qm_val_t>[][] u_iqmatrix = new_jagged<Ptr<qm_val_t>>(MAX_SEGMENTS, TX_SIZES_ALL);
    public readonly Ptr<qm_val_t>[][] v_iqmatrix = new_jagged<Ptr<qm_val_t>>(MAX_SEGMENTS, TX_SIZES_ALL);

    // Flag indicating whether quantization matrices are being used:
    //  - If true, qm_level_y, qm_level_u and qm_level_v indicate the level
    //    indices to be used to access appropriate global quant matrix tables.
    //  - If false, we implicitly use level index 'NUM_QM_LEVELS - 1'.
    public bool using_qmatrix;
    public int qmatrix_level_y;  // Level index for Y plane
    public int qmatrix_level_u;  // Level index for U plane
    public int qmatrix_level_v;  // Level index for V plane
}

/*!
 * \brief Context used for transmitting various symbols in the bistream.
 */
internal sealed partial class CommonContexts
{
    // Context used by 'partition' symbol.
    // partition[i][j] is the context for ith tile row, jth mi_col.
    public Ptr<PARTITION_CONTEXT>[] partition;

    // Context used to derive context for multiple symbols:
    // - 'all_zero' symbol for transform coefficients
    // - 'dc_sign' symbol for transform coefficients
    // entropy[i][j][k] is the context for ith plane, jth tile row, kth mi_col.
    public readonly Ptr<ENTROPY_CONTEXT>[][] entropy = new Ptr<ENTROPY_CONTEXT>[MAX_MB_PLANE][];

    // Context used to derive context for 'txfm_split' symbol.
    // txfm[i][j] is the context for ith tile row, jth mi_col.
    public Ptr<TXFM_CONTEXT>[] txfm;

    // Dimensions that were used to allocate the arrays above.
    // If these dimensions change, the arrays may have to be re-allocated.
    public int num_planes;     // Corresponds to av1_num_planes(cm)
    public int num_tile_rows;  // Corresponds to cm->tiles.row
    public int num_mi_cols;    // Corresponds to cm->mi_params.mi_cols
}

/*!
 * \brief Top level common structure used by both encoder and decoder.
 */
internal sealed partial class AV1_COMMON
{
    // Information about the current frame that is being coded.
    public readonly CurrentFrame current_frame = new();
    // Code and details about current error status.
    public aom_internal_error_info error;

    // AV1 allows two types of frame scaling operations:
    // 1. Frame super-resolution: that allows coding a frame at lower resolution
    // and after decoding the frame, normatively scales and restores the frame --
    // inside the coding loop.
    // 2. Frame resize: that allows coding frame at lower/higher resolution, and
    // then non-normatively upscale the frame at the time of rendering -- outside
    // the coding loop.
    // Hence, the need for 3 types of dimensions.

    // Coded frame dimensions.
    public int width;   // Coded frame width
    public int height;  // Coded frame height

    // Rendered frame dimensions, after applying both super-resolution and resize
    // to the coded frame.
    // Different from coded dimensions if super-resolution and/or resize are
    // being used for this frame.
    public int render_width;   // Rendered frame width
    public int render_height;  // Rendered frame height

    // Frame dimensions after applying super-resolution to the coded frame (if
    // present), but before applying resize.
    // Larger than the coded dimensions if super-resolution is being used for
    // this frame.
    // Different from rendered dimensions if resize is being used for this
    // frame.
    public int superres_upscaled_width;   // Super-resolved frame width
    public int superres_upscaled_height;  // Super-resolved frame height

    // The denominator of the superres scale used by this frame.
    // Note: The numerator is fixed to be SCALE_NUMERATOR.
    public byte superres_scale_denominator;

    // buffer_removal_times[op_num] specifies the frame removal time in units of
    // DecCT clock ticks counted from the removal time of the last random access
    // point for operating point op_num.
    // TODO(urvang): We probably don't need the +1 here.
    public readonly uint[] buffer_removal_times = new uint[MAX_NUM_OPERATING_POINTS + 1];
    // Presentation time of the frame in clock ticks DispCT counted from the
    // removal time of the last random access point for the operating point that
    // is being decoded.
    public uint frame_presentation_time;

    // Buffer where previous frame is stored.
    public RefCntBuffer prev_frame;

    // Buffer into which the current frame will be stored and other related info.
    // TODO(hkuang): Combine this with cur_buf in macroblockd.
    public RefCntBuffer cur_frame;

    // For encoder, we have a two-level mapping from reference frame type to the
    // corresponding buffer in the buffer pool:
    // * 'remapped_ref_idx[i - 1]' maps reference type 'i' (range: LAST_FRAME ...
    // EXTREF_FRAME) to a remapped index 'j' (in range: 0 ... REF_FRAMES - 1)
    // * Later, 'cm->ref_frame_map[j]' maps the remapped index 'j' to a pointer to
    // the reference counted buffer structure RefCntBuffer, taken from the buffer
    // pool cm->buffer_pool->frame_bufs.
    //
    // LAST_FRAME,                        ...,      EXTREF_FRAME
    //      |                                           |
    //      v                                           v
    // remapped_ref_idx[LAST_FRAME - 1],  ...,  remapped_ref_idx[EXTREF_FRAME - 1]
    //      |                                           |
    //      v                                           v
    // ref_frame_map[],                   ...,     ref_frame_map[]
    //
    // Note: INTRA_FRAME always refers to the current frame, so there's no need to
    // have a remapped index for the same.
    public readonly int[] remapped_ref_idx = new int[REF_FRAMES];

    // Scale of the current frame with respect to itself.
    // This is currently used for intra block copy, which behaves like an inter
    // prediction mode, where the reference frame is the current frame itself.
    public readonly scale_factors sf_identity = new();

    // Scale factors of the reference frame with respect to the current frame.
    // This is required for generating inter prediction and will be non-identity
    // for a reference frame, if it has different dimensions than the coded
    // dimensions of the current frame.
    public readonly scale_factors[] ref_scale_factors = new_array<scale_factors>(REF_FRAMES);

    // For decoder, ref_frame_map[i] maps reference type 'i' to a pointer to
    // the buffer in the buffer pool 'cm->buffer_pool.frame_bufs'.
    // For encoder, ref_frame_map[j] (where j = remapped_ref_idx[i]) maps
    // remapped reference index 'j' (that is, original reference type 'i') to
    // a pointer to the buffer in the buffer pool 'cm->buffer_pool.frame_bufs'.
    public readonly RefCntBuffer[] ref_frame_map = new RefCntBuffer[REF_FRAMES];

    // If true, this frame is actually shown after decoding.
    // If false, this frame is coded in the bitstream, but not shown. It is only
    // used as a reference for other frames coded later.
    public int show_frame;

    // If true, this frame can be used as a show-existing frame for other frames
    // coded later.
    // When 'show_frame' is true, this is always true for all non-keyframes.
    // When 'show_frame' is false, this value is transmitted in the bitstream.
    public int showable_frame;

    // If true, show an existing frame coded before, instead of actually coding a
    // frame. The existing frame comes from one of the existing reference buffers,
    // as signaled in the bitstream.
    public int show_existing_frame;

    // Whether some features are allowed or not.
    public readonly FeatureFlags features = new();

    // Params related to MB_MODE_INFO arrays and related info.
    public readonly CommonModeInfoParams mi_params = new();

    // Params related to quantization at the frame level.
    public readonly CommonQuantParams quant_params = new();

    // Segmentation info for current frame.
    public readonly segmentation seg = new();

    // Segmentation map for previous frame.
    public Ptr<byte> last_frame_seg_map;

    // Loop filter info.
    public readonly loop_filter_info_n lf_info = new();  // Loop filter info
    public readonly loopfilter lf = new();               // Loop filter parameters

    // Loop restoration info.
    public readonly RestorationInfo[] rst_info = new_array<RestorationInfo>(MAX_MB_PLANE);  // Loop Restoration info
    public Ptr<int> rst_tmpbuf;               // Scratch buffer for self-guided restoration
    public RestorationLineBuffers rlbs;       // Line buffers needed by loop restoration
    public YV12_BUFFER_CONFIG rst_frame = new();  // Stores the output of loop restoration

    // CDEF (Constrained Directional Enhancement Filter) parameters.
    public readonly CdefInfo cdef_info = new();

    // Parameters for film grain synthesis.
    public aom_film_grain_t film_grain_params;

    // Parameters for delta quantization and delta loop filter level.
    public DeltaQInfo delta_q_info;

    // Global motion parameters for each reference frame.
    public readonly WarpedMotionParams[] global_motion = new WarpedMotionParams[REF_FRAMES];

    // Elements part of the sequence header, that are applicable for all the
    // frames in the video.
    public SequenceHeader seq_params;

    // Current CDFs of all the symbols for the current frame.
    public FRAME_CONTEXT fc;
    // Default CDFs used when features.primary_ref_frame = PRIMARY_REF_NONE
    // (e.g. for a keyframe). These default CDFs are defined by the bitstream and
    // copied from default CDF tables for each symbol.
    public FRAME_CONTEXT default_frame_context;

    // Parameters related to tiling.
    public readonly CommonTileParams tiles = new();

    // External BufferPool passed from outside.
    public BufferPool buffer_pool;

    // Above context buffers and their sizes.
    // Note: above contexts are allocated in this struct, as their size is
    // dependent on frame width, while left contexts are declared and allocated in
    // MACROBLOCKD struct, as they have a fixed size.
    public readonly CommonContexts above_contexts = new();

    // When cm->seq_params->frame_id_numbers_present_flag == 1, current and
    // reference frame IDs are signaled in the bitstream.
    public int current_frame_id;  // frame ID for the current frame.
    public readonly int[] ref_frame_id = new int[REF_FRAMES];  // frame IDs for the reference frames.

    // Motion vectors provided by motion field estimation.
    // tpl_mvs[row * stride + col] stores MV for block at [mi_row, mi_col] where:
    // mi_row = 2 * row,
    // mi_col = 2 * col, and
    // stride = cm->mi_params.mi_stride / 2
    public Ptr<TPL_MV_REF> tpl_mvs;
    // Allocated size of 'tpl_mvs' array. Refer to 'ensure_mv_buffer()' function.
    public int tpl_mvs_mem_size;
    // ref_frame_sign_bias[k] is 1 if relative distance between reference 'k' and
    // current frame is positive; and 0 otherwise.
    public readonly int[] ref_frame_sign_bias = new int[REF_FRAMES];
    // ref_frame_side[k] is 1 if relative distance between reference 'k' and
    // current frame is positive, -1 if relative distance is 0; and 0 otherwise.
    // TODO(jingning): This can be combined with sign_bias later.
    public readonly sbyte[] ref_frame_side = new sbyte[REF_FRAMES];

    // Temporal layer ID of this frame
    // (in the range 0 ... (number_temporal_layers - 1)).
    public int temporal_layer_id;

    // Spatial layer ID of this frame
    // (in the range 0 ... (number_spatial_layers - 1)).
    public int spatial_layer_id;
}

internal static partial class av1_common_int
{
    // <gen:consts>
    internal const int CDEF_MAX_STRENGTHS = 16;
    internal const int FRAME_ID_LENGTH = 15;
    internal const int DELTA_FRAME_ID_LENGTH = 14;
    internal const int FRAME_CONTEXTS = (FRAME_BUFFERS + 1);
    internal const int FRAME_CONTEXT_DEFAULTS = (FRAME_CONTEXTS - 1);
    internal const int PRIMARY_REF_BITS = 3;
    internal const int PRIMARY_REF_NONE = 7;
    internal const int NUM_PING_PONG_BUFFERS = 2;
    internal const int MAX_NUM_TEMPORAL_LAYERS = 8;
    internal const int MAX_NUM_SPATIAL_LAYERS = 4;
    internal const int MAX_NUM_OPERATING_POINTS = (MAX_NUM_TEMPORAL_LAYERS * MAX_NUM_SPATIAL_LAYERS);
    internal const int TXCOEFF_TIMER = 0;
    internal const int TXCOEFF_COST_TIMER = 0;
    internal const int MFMV_STACK_SIZE = 3;

    // REFERENCE_MODE
    internal const int SINGLE_REFERENCE = 0;
    internal const int COMPOUND_REFERENCE = 1;
    internal const int REFERENCE_MODE_SELECT = 2;
    internal const int REFERENCE_MODES = 3;

    // REFRESH_FRAME_CONTEXT_MODE
    internal const int REFRESH_FRAME_CONTEXT_DISABLED = 0;
    internal const int REFRESH_FRAME_CONTEXT_BACKWARD = 1;

    // PARTITION_TYPE base_partitions[4]
    private static readonly PARTITION_TYPE[] base_partitions = {
        PARTITION_INVALID, PARTITION_HORZ, PARTITION_VERT, PARTITION_SPLIT
    };
    // </gen:consts>

    // [port] C `static` (not inline) in the header.
    internal static void lock_buffer_pool(BufferPool pool)
    {
        pthread_mutex_lock(pool.pool_mutex);
    }

    internal static void unlock_buffer_pool(BufferPool pool)
    {
        pthread_mutex_unlock(pool.pool_mutex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_free_fb(AV1_COMMON cm)
    {
        RefCntBuffer[] frame_bufs = cm.buffer_pool.frame_bufs;
        int i;

        lock_buffer_pool(cm.buffer_pool);
        int num_frame_bufs = cm.buffer_pool.num_frame_bufs;
        for (i = 0; i < num_frame_bufs; ++i)
            if (frame_bufs[i].ref_count == 0) break;

        if (i != num_frame_bufs)
        {
            if (frame_bufs[i].buf.use_external_reference_buffers != 0)
            {
                // If this frame buffer's y_buffer, u_buffer, and v_buffer point to the
                // external reference buffers. Restore the buffer pointers to point to the
                // internally allocated memory.
                YV12_BUFFER_CONFIG ybf = frame_bufs[i].buf;
                ybf.y_buffer = ybf.store_buf_adr[0];
                ybf.u_buffer = ybf.store_buf_adr[1];
                ybf.v_buffer = ybf.store_buf_adr[2];
                ybf.use_external_reference_buffers = 0;
            }

            frame_bufs[i].ref_count = 1;
        }
        else
        {
            // We should never run out of free buffers. If this assertion fails, there
            // is a reference leak.
            Debug.Assert(false, "Ran out of free frame buffers. Likely a reference leak.");
            // Reset i to be INVALID_IDX to indicate no free buffer found.
            i = INVALID_IDX;
        }

        unlock_buffer_pool(cm.buffer_pool);
        return i;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static RefCntBuffer assign_cur_frame_new_fb(AV1_COMMON cm)
    {
        // Release the previously-used frame-buffer
        if (cm.cur_frame != null)
        {
            --cm.cur_frame.ref_count;
            cm.cur_frame = null;
        }

        // Assign a new framebuffer
        int new_fb_idx = get_free_fb(cm);
        if (new_fb_idx == INVALID_IDX) return null;

        cm.cur_frame = cm.buffer_pool.frame_bufs[new_fb_idx];
        av1_zero(cm.cur_frame.interp_filter_selected);
        return cm.cur_frame;
    }

    // Modify 'lhs_ptr' to reference the buffer at 'rhs_ptr', and update the ref
    // counts accordingly.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void assign_frame_buffer_p(ref RefCntBuffer lhs_ptr,
                                               RefCntBuffer rhs_ptr)
    {
        RefCntBuffer old_ptr = lhs_ptr;
        if (old_ptr != null)
        {
            Debug.Assert(old_ptr.ref_count > 0);
            // One less reference to the buffer at 'old_ptr', so decrease ref count.
            --old_ptr.ref_count;
        }

        lhs_ptr = rhs_ptr;
        // One more reference to the buffer at 'rhs_ptr', so increase ref count.
        ++rhs_ptr.ref_count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int frame_is_intra_only(AV1_COMMON cm)
    {
        return B(cm.current_frame.frame_type == KEY_FRAME ||
                 cm.current_frame.frame_type == INTRA_ONLY_FRAME);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int frame_is_sframe(AV1_COMMON cm)
    {
        return B(cm.current_frame.frame_type == S_FRAME);
    }

    // These functions take a reference frame label between LAST_FRAME and
    // EXTREF_FRAME inclusive.  Note that this is different to the indexing
    // previously used by the frame_refs[] array.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_ref_frame_map_idx(AV1_COMMON cm,
                                              MV_REFERENCE_FRAME ref_frame)
    {
        return (ref_frame >= LAST_FRAME && ref_frame <= EXTREF_FRAME)
                   ? cm.remapped_ref_idx[ref_frame - LAST_FRAME]
                   : INVALID_IDX;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static RefCntBuffer get_ref_frame_buf(
        AV1_COMMON cm, MV_REFERENCE_FRAME ref_frame)
    {
        int map_idx = get_ref_frame_map_idx(cm, ref_frame);
        return (map_idx != INVALID_IDX) ? cm.ref_frame_map[map_idx] : null;
    }

    // Both const and non-const versions of this function are provided so that it
    // can be used with a const AV1_COMMON if needed.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static scale_factors get_ref_scale_factors_const(
        AV1_COMMON cm, MV_REFERENCE_FRAME ref_frame)
    {
        int map_idx = get_ref_frame_map_idx(cm, ref_frame);
        return (map_idx != INVALID_IDX) ? cm.ref_scale_factors[map_idx] : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static scale_factors get_ref_scale_factors(
        AV1_COMMON cm, MV_REFERENCE_FRAME ref_frame)
    {
        int map_idx = get_ref_frame_map_idx(cm, ref_frame);
        return (map_idx != INVALID_IDX) ? cm.ref_scale_factors[map_idx] : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static RefCntBuffer get_primary_ref_frame_buf(
        AV1_COMMON cm)
    {
        int primary_ref_frame = cm.features.primary_ref_frame;
        if (primary_ref_frame == PRIMARY_REF_NONE) return null;
        int map_idx = get_ref_frame_map_idx(cm, primary_ref_frame + 1);
        return (map_idx != INVALID_IDX) ? cm.ref_frame_map[map_idx] : null;
    }

    // Returns 1 if this frame might allow mvs from some reference frame.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int frame_might_allow_ref_frame_mvs(AV1_COMMON cm)
    {
        return B(!cm.features.error_resilient_mode &&
                 cm.seq_params.order_hint_info.enable_ref_frame_mvs != 0 &&
                 cm.seq_params.order_hint_info.enable_order_hint != 0 &&
                 frame_is_intra_only(cm) == 0);
    }

    // Returns 1 if this frame might use warped_motion
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int frame_might_allow_warped_motion(AV1_COMMON cm)
    {
        return B(!cm.features.error_resilient_mode && frame_is_intra_only(cm) == 0 &&
                 cm.seq_params.enable_warped_motion != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ensure_mv_buffer(RefCntBuffer buf, AV1_COMMON cm)
    {
        int buf_rows = buf.mi_rows;
        int buf_cols = buf.mi_cols;
        CommonModeInfoParams mi_params = cm.mi_params;

        if (buf.mvs.IsNull || buf_rows != mi_params.mi_rows ||
            buf_cols != mi_params.mi_cols)
        {
            buf.mi_rows = mi_params.mi_rows;
            buf.mi_cols = mi_params.mi_cols;
            buf.mvs = new MV_REF[((mi_params.mi_rows + 1) >> 1) *
                                 ((mi_params.mi_cols + 1) >> 1)];
            buf.seg_map = new byte[mi_params.mi_rows * mi_params.mi_cols];
        }

        int mem_size =
            ((mi_params.mi_rows + MAX_MIB_SIZE) >> 1) * (mi_params.mi_stride >> 1);

        if (cm.tpl_mvs.IsNull || cm.tpl_mvs_mem_size < mem_size)
        {
            cm.tpl_mvs = new TPL_MV_REF[mem_size];
            cm.tpl_mvs_mem_size = mem_size;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_num_planes(AV1_COMMON cm)
    {
        return cm.seq_params.monochrome != 0 ? 1 : MAX_MB_PLANE;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_init_above_context(CommonContexts above_contexts,
                                                int num_planes, int tile_row,
                                                MACROBLOCKD xd)
    {
        for (int i = 0; i < num_planes; ++i)
        {
            xd.above_entropy_context[i] = above_contexts.entropy[i][tile_row];
        }
        xd.above_partition_context = above_contexts.partition[tile_row];
        xd.above_txfm_context = above_contexts.txfm[tile_row];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_init_macroblockd(AV1_COMMON cm, MACROBLOCKD xd)
    {
        int num_planes = av1_num_planes(cm);
        CommonQuantParams quant_params = cm.quant_params;

        for (int i = 0; i < num_planes; ++i)
        {
            if (xd.plane[i].plane_type == PLANE_TYPE_Y)
            {
                av1_copy(xd.plane[i].seg_dequant_QTX, quant_params.y_dequant_QTX);
                av1_copy(xd.plane[i].seg_iqmatrix, quant_params.y_iqmatrix);
            }
            else
            {
                if (i == AOM_PLANE_U)
                {
                    av1_copy(xd.plane[i].seg_dequant_QTX, quant_params.u_dequant_QTX);
                    av1_copy(xd.plane[i].seg_iqmatrix, quant_params.u_iqmatrix);
                }
                else
                {
                    av1_copy(xd.plane[i].seg_dequant_QTX, quant_params.v_dequant_QTX);
                    av1_copy(xd.plane[i].seg_iqmatrix, quant_params.v_iqmatrix);
                }
            }
        }
        xd.mi_stride = cm.mi_params.mi_stride;
        xd.error_info = cm.error;
        cfl_init(xd.cfl, cm.seq_params);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_entropy_context(MACROBLOCKD xd, int mi_row, int mi_col,
                                             int num_planes)
    {
        int i;
        int row_offset = mi_row;
        int col_offset = mi_col;
        for (i = 0; i < num_planes; ++i)
        {
            macroblockd_plane pd = xd.plane[i];
            // Offset the buffer pointer
            BLOCK_SIZE bsize = xd.mi[0].bsize;
            if (pd.subsampling_y != 0 && (mi_row & 0x01) != 0 && (mi_size_high[bsize] == 1))
                row_offset = mi_row - 1;
            if (pd.subsampling_x != 0 && (mi_col & 0x01) != 0 && (mi_size_wide[bsize] == 1))
                col_offset = mi_col - 1;
            int above_idx = col_offset;
            int left_idx = row_offset & MAX_MIB_MASK;
            pd.above_entropy_context =
                xd.above_entropy_context[i] + (above_idx >> pd.subsampling_x);
            pd.left_entropy_context =
                new Ptr<ENTROPY_CONTEXT>(xd.left_entropy_context[i], left_idx >> pd.subsampling_y);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int calc_mi_size(int len)
    {
        // len is in mi units. Align to a multiple of SBs.
        return ALIGN_POWER_OF_TWO(len, MAX_MIB_SIZE_LOG2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_plane_n4(MACROBLOCKD xd, int bw, int bh,
                                      int num_planes)
    {
        int i;
        for (i = 0; i < num_planes; i++)
        {
            xd.plane[i].width = (byte)((bw * MI_SIZE) >> xd.plane[i].subsampling_x);
            xd.plane[i].height = (byte)((bh * MI_SIZE) >> xd.plane[i].subsampling_y);

            xd.plane[i].width = (byte)AOMMAX(xd.plane[i].width, 4);
            xd.plane[i].height = (byte)AOMMAX(xd.plane[i].height, 4);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_mi_row_col(MACROBLOCKD xd, in TileInfo tile,
                                        int mi_row, int bh, int mi_col, int bw,
                                        int mi_rows, int mi_cols)
    {
        xd.mb_to_top_edge = -GET_MV_SUBPEL(mi_row * MI_SIZE);
        xd.mb_to_bottom_edge = GET_MV_SUBPEL((mi_rows - bh - mi_row) * MI_SIZE);
        xd.mb_to_left_edge = -GET_MV_SUBPEL((mi_col * MI_SIZE));
        xd.mb_to_right_edge = GET_MV_SUBPEL((mi_cols - bw - mi_col) * MI_SIZE);

        xd.mi_row = mi_row;
        xd.mi_col = mi_col;

        // Are edges available for intra prediction?
        xd.up_available = (mi_row > tile.mi_row_start);

        int ss_x = xd.plane[1].subsampling_x;
        int ss_y = xd.plane[1].subsampling_y;

        xd.left_available = (mi_col > tile.mi_col_start);
        xd.chroma_up_available = xd.up_available;
        xd.chroma_left_available = xd.left_available;
        if (ss_x != 0 && bw < mi_size_wide[BLOCK_8X8])
            xd.chroma_left_available = (mi_col - 1) > tile.mi_col_start;
        if (ss_y != 0 && bh < mi_size_high[BLOCK_8X8])
            xd.chroma_up_available = (mi_row - 1) > tile.mi_row_start;
        if (xd.up_available)
        {
            xd.above_mbmi = xd.mi[-xd.mi_stride];
        }
        else
        {
            xd.above_mbmi = null;
        }

        if (xd.left_available)
        {
            xd.left_mbmi = xd.mi[-1];
        }
        else
        {
            xd.left_mbmi = null;
        }

        int chroma_ref = B(((mi_row & 0x01) != 0 || (bh & 0x01) == 0 || ss_y == 0) &&
                           ((mi_col & 0x01) != 0 || (bw & 0x01) == 0 || ss_x == 0));
        xd.is_chroma_ref = chroma_ref != 0;
        if (chroma_ref != 0)
        {
            // To help calculate the "above" and "left" chroma blocks, note that the
            // current block may cover multiple luma blocks (e.g., if partitioned into
            // 4x4 luma blocks).
            // First, find the top-left-most luma block covered by this chroma block
            Ptr<MB_MODE_INFO> base_mi =
                xd.mi + (-(mi_row & ss_y) * xd.mi_stride - (mi_col & ss_x));

            // Then, we consider the luma region covered by the left or above 4x4 chroma
            // prediction. We want to point to the chroma reference block in that
            // region, which is the bottom-right-most mi unit.
            // This leads to the following offsets:
            MB_MODE_INFO chroma_above_mi =
                xd.chroma_up_available ? base_mi[-xd.mi_stride + ss_x] : null;
            xd.chroma_above_mbmi = chroma_above_mi;

            MB_MODE_INFO chroma_left_mi =
                xd.chroma_left_available ? base_mi[ss_y * xd.mi_stride - 1] : null;
            xd.chroma_left_mbmi = chroma_left_mi;
        }

        xd.height = (byte)bh;
        xd.width = (byte)bw;

        xd.is_last_vertical_rect = false;
        if (xd.width < xd.height)
        {
            if (((mi_col + xd.width) & (xd.height - 1)) == 0)
            {
                xd.is_last_vertical_rect = true;
            }
        }

        xd.is_first_horizontal_rect = false;
        if (xd.width > xd.height)
            if ((mi_row & (xd.width - 1)) == 0) xd.is_first_horizontal_rect = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<ushort> get_y_mode_cdf(FRAME_CONTEXT tile_ctx,
                                               MB_MODE_INFO above_mi,
                                               MB_MODE_INFO left_mi)
    {
        PREDICTION_MODE above = av1_above_block_mode(above_mi);
        PREDICTION_MODE left = av1_left_block_mode(left_mi);
        int above_ctx = intra_mode_context[above];
        int left_ctx = intra_mode_context[left];
        return tile_ctx.kf_y_cdf[above_ctx][left_ctx];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void update_partition_context(MACROBLOCKD xd, int mi_row,
                                                  int mi_col, BLOCK_SIZE subsize,
                                                  BLOCK_SIZE bsize)
    {
        Ptr<PARTITION_CONTEXT> above_ctx = xd.above_partition_context + mi_col;
        Ptr<PARTITION_CONTEXT> left_ctx =
            new Ptr<PARTITION_CONTEXT>(xd.left_partition_context, mi_row & MAX_MIB_MASK);

        int bw = mi_size_wide[bsize];
        int bh = mi_size_high[bsize];
        memset(above_ctx, partition_context_lookup[subsize].above, bw);
        memset(left_ctx, partition_context_lookup[subsize].left, bh);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ushort cdf_element_prob(Ptr<ushort> cdf,
                                            int element)
    {
        Debug.Assert(!cdf.IsNull);
        return (ushort)((element > 0 ? cdf[element - 1] : CDF_PROB_TOP) - cdf[element]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void partition_gather_horz_alike(Ptr<ushort> @out,
                                                     Ptr<ushort> @in,
                                                     BLOCK_SIZE bsize)
    {
        @out[0] = CDF_PROB_TOP;
        @out[0] -= cdf_element_prob(@in, PARTITION_HORZ);
        @out[0] -= cdf_element_prob(@in, PARTITION_SPLIT);
        @out[0] -= cdf_element_prob(@in, PARTITION_HORZ_A);
        @out[0] -= cdf_element_prob(@in, PARTITION_HORZ_B);
        @out[0] -= cdf_element_prob(@in, PARTITION_VERT_A);
        if (bsize != BLOCK_128X128) @out[0] -= cdf_element_prob(@in, PARTITION_HORZ_4);
        @out[0] = (ushort)AOM_ICDF(@out[0]);
        @out[1] = (ushort)AOM_ICDF(CDF_PROB_TOP);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void partition_gather_vert_alike(Ptr<ushort> @out,
                                                     Ptr<ushort> @in,
                                                     BLOCK_SIZE bsize)
    {
        @out[0] = CDF_PROB_TOP;
        @out[0] -= cdf_element_prob(@in, PARTITION_VERT);
        @out[0] -= cdf_element_prob(@in, PARTITION_SPLIT);
        @out[0] -= cdf_element_prob(@in, PARTITION_HORZ_A);
        @out[0] -= cdf_element_prob(@in, PARTITION_VERT_A);
        @out[0] -= cdf_element_prob(@in, PARTITION_VERT_B);
        if (bsize != BLOCK_128X128) @out[0] -= cdf_element_prob(@in, PARTITION_VERT_4);
        @out[0] = (ushort)AOM_ICDF(@out[0]);
        @out[1] = (ushort)AOM_ICDF(CDF_PROB_TOP);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void update_ext_partition_context(MACROBLOCKD xd, int mi_row,
                                                      int mi_col, BLOCK_SIZE subsize,
                                                      BLOCK_SIZE bsize,
                                                      PARTITION_TYPE partition)
    {
        if (bsize >= BLOCK_8X8)
        {
            int hbs = mi_size_wide[bsize] / 2;
            BLOCK_SIZE bsize2 = get_partition_subsize(bsize, PARTITION_SPLIT);
            switch (partition)
            {
                case PARTITION_SPLIT:
                    if (bsize != BLOCK_8X8) break;
                    goto case PARTITION_NONE;  // AOM_FALLTHROUGH_INTENDED
                case PARTITION_NONE:
                case PARTITION_HORZ:
                case PARTITION_VERT:
                case PARTITION_HORZ_4:
                case PARTITION_VERT_4:
                    update_partition_context(xd, mi_row, mi_col, subsize, bsize);
                    break;
                case PARTITION_HORZ_A:
                    update_partition_context(xd, mi_row, mi_col, bsize2, subsize);
                    update_partition_context(xd, mi_row + hbs, mi_col, subsize, subsize);
                    break;
                case PARTITION_HORZ_B:
                    update_partition_context(xd, mi_row, mi_col, subsize, subsize);
                    update_partition_context(xd, mi_row + hbs, mi_col, bsize2, subsize);
                    break;
                case PARTITION_VERT_A:
                    update_partition_context(xd, mi_row, mi_col, bsize2, subsize);
                    update_partition_context(xd, mi_row, mi_col + hbs, subsize, subsize);
                    break;
                case PARTITION_VERT_B:
                    update_partition_context(xd, mi_row, mi_col, subsize, subsize);
                    update_partition_context(xd, mi_row, mi_col + hbs, bsize2, subsize);
                    break;
                default: Debug.Assert(false, "Invalid partition type"); break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int partition_plane_context(MACROBLOCKD xd, int mi_row,
                                                int mi_col, BLOCK_SIZE bsize)
    {
        Ptr<PARTITION_CONTEXT> above_ctx = xd.above_partition_context + mi_col;
        Ptr<PARTITION_CONTEXT> left_ctx =
            new Ptr<PARTITION_CONTEXT>(xd.left_partition_context, mi_row & MAX_MIB_MASK);
        // Minimum partition point is 8x8. Offset the bsl accordingly.
        int bsl = mi_size_wide_log2[bsize] - mi_size_wide_log2[BLOCK_8X8];
        int above = (above_ctx.Value >> bsl) & 1, left = (left_ctx.Value >> bsl) & 1;

        Debug.Assert(mi_size_wide_log2[bsize] == mi_size_high_log2[bsize]);
        Debug.Assert(bsl >= 0);

        return (left * 2 + above) + bsl * PARTITION_PLOFFSET;
    }

    // Return the number of elements in the partition CDF when
    // partitioning the (square) block with luma block size of bsize.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int partition_cdf_length(BLOCK_SIZE bsize)
    {
        if (bsize <= BLOCK_8X8)
            return PARTITION_TYPES;
        else if (bsize == BLOCK_128X128)
            return EXT_PARTITION_TYPES - 2;
        else
            return EXT_PARTITION_TYPES;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int max_block_wide(MACROBLOCKD xd, BLOCK_SIZE bsize,
                                       int plane)
    {
        Debug.Assert(bsize < BLOCK_SIZES_ALL);
        int max_blocks_wide = block_size_wide[bsize];

        if (xd.mb_to_right_edge < 0)
        {
            macroblockd_plane pd = xd.plane[plane];
            max_blocks_wide += xd.mb_to_right_edge >> (3 + pd.subsampling_x);
        }

        // Scale the width in the transform block unit.
        return max_blocks_wide >> MI_SIZE_LOG2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int max_block_high(MACROBLOCKD xd, BLOCK_SIZE bsize,
                                       int plane)
    {
        int max_blocks_high = block_size_high[bsize];

        if (xd.mb_to_bottom_edge < 0)
        {
            macroblockd_plane pd = xd.plane[plane];
            max_blocks_high += xd.mb_to_bottom_edge >> (3 + pd.subsampling_y);
        }

        // Scale the height in the transform block unit.
        return max_blocks_high >> MI_SIZE_LOG2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_zero_above_context(AV1_COMMON cm,
                                                MACROBLOCKD xd,
                                                int mi_col_start, int mi_col_end,
                                                int tile_row)
    {
        SequenceHeader seq_params = cm.seq_params;
        int num_planes = av1_num_planes(cm);
        int width = mi_col_end - mi_col_start;
        int aligned_width =
            ALIGN_POWER_OF_TWO(width, seq_params.mib_size_log2);
        int offset_y = mi_col_start;
        int width_y = aligned_width;
        int offset_uv = offset_y >> seq_params.subsampling_x;
        int width_uv = width_y >> seq_params.subsampling_x;
        CommonContexts above_contexts = cm.above_contexts;

        av1_zero_array(above_contexts.entropy[0][tile_row] + offset_y, width_y);
        if (num_planes > 1)
        {
            if (!above_contexts.entropy[1][tile_row].IsNull &&
                !above_contexts.entropy[2][tile_row].IsNull)
            {
                av1_zero_array(above_contexts.entropy[1][tile_row] + offset_uv,
                               width_uv);
                av1_zero_array(above_contexts.entropy[2][tile_row] + offset_uv,
                               width_uv);
            }
            else
            {
                throw new AomException(AOM_CODEC_CORRUPT_FRAME,
                                       "Invalid value of planes");
            }
        }

        av1_zero_array(above_contexts.partition[tile_row] + mi_col_start,
                       aligned_width);

        memset(above_contexts.txfm[tile_row] + mi_col_start,
               tx_size_wide[TX_SIZES_LARGEST], aligned_width);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_zero_left_context(MACROBLOCKD xd)
    {
        av1_zero(xd.left_entropy_context);
        av1_zero(xd.left_partition_context);

        memset<TXFM_CONTEXT>(xd.left_txfm_context_buffer, tx_size_high[TX_SIZES_LARGEST],
                             MAX_MIB_SIZE);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_txfm_ctx(Ptr<TXFM_CONTEXT> txfm_ctx, byte txs, int len)
    {
        int i;
        for (i = 0; i < len; ++i) txfm_ctx[i] = txs;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_txfm_ctxs(TX_SIZE tx_size, int n4_w, int n4_h, int skip,
                                       MACROBLOCKD xd)
    {
        byte bw = (byte)tx_size_wide[tx_size];
        byte bh = (byte)tx_size_high[tx_size];

        if (skip != 0)
        {
            bw = (byte)(n4_w * MI_SIZE);
            bh = (byte)(n4_h * MI_SIZE);
        }

        set_txfm_ctx(xd.above_txfm_context, bw, n4_w);
        set_txfm_ctx(xd.left_txfm_context, bh, n4_h);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_mi_grid_idx(CommonModeInfoParams mi_params,
                                        int mi_row, int mi_col)
    {
        return mi_row * mi_params.mi_stride + mi_col;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_alloc_mi_idx(CommonModeInfoParams mi_params,
                                         int mi_row, int mi_col)
    {
        int mi_alloc_size_1d = mi_size_wide[mi_params.mi_alloc_bsize];
        int mi_alloc_row = mi_row / mi_alloc_size_1d;
        int mi_alloc_col = mi_col / mi_alloc_size_1d;

        return mi_alloc_row * mi_params.mi_alloc_stride + mi_alloc_col;
    }

    // For this partition block, set pointers in mi_params->mi_grid_base and xd->mi.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_mi_offsets(CommonModeInfoParams mi_params,
                                        MACROBLOCKD xd, int mi_row,
                                        int mi_col)
    {
        // 'mi_grid_base' should point to appropriate memory in 'mi'.
        int mi_grid_idx = get_mi_grid_idx(mi_params, mi_row, mi_col);
        int mi_alloc_idx = get_alloc_mi_idx(mi_params, mi_row, mi_col);
        mi_params.mi_grid_base[mi_grid_idx] = mi_params.mi_alloc[mi_alloc_idx];
        // 'xd->mi' should point to an offset in 'mi_grid_base';
        xd.mi = new Ptr<MB_MODE_INFO>(mi_params.mi_grid_base, mi_grid_idx);
        // 'xd->tx_type_map' should point to an offset in 'mi_params->tx_type_map'.
        xd.tx_type_map = mi_params.tx_type_map + mi_grid_idx;
        xd.tx_type_map_stride = mi_params.mi_stride;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void txfm_partition_update(Ptr<TXFM_CONTEXT> above_ctx,
                                               Ptr<TXFM_CONTEXT> left_ctx,
                                               TX_SIZE tx_size, TX_SIZE txb_size)
    {
        BLOCK_SIZE bsize = txsize_to_bsize[txb_size];
        int bh = mi_size_high[bsize];
        int bw = mi_size_wide[bsize];
        byte txw = (byte)tx_size_wide[tx_size];
        byte txh = (byte)tx_size_high[tx_size];
        int i;
        for (i = 0; i < bh; ++i) left_ctx[i] = txh;
        for (i = 0; i < bw; ++i) above_ctx[i] = txw;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TX_SIZE get_sqr_tx_size(int tx_dim)
    {
        switch (tx_dim)
        {
            case 128:
            case 64: return TX_64X64;
            case 32: return TX_32X32;
            case 16: return TX_16X16;
            case 8: return TX_8X8;
            default: return TX_4X4;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int txfm_partition_context(Ptr<TXFM_CONTEXT> above_ctx,
                                               Ptr<TXFM_CONTEXT> left_ctx,
                                               BLOCK_SIZE bsize, TX_SIZE tx_size)
    {
        byte txw = (byte)tx_size_wide[tx_size];
        byte txh = (byte)tx_size_high[tx_size];
        int above = B(above_ctx.Value < txw);
        int left = B(left_ctx.Value < txh);
        int category = TXFM_PARTITION_CONTEXTS;

        // dummy return, not used by others.
        if (tx_size <= TX_4X4) return 0;

        TX_SIZE max_tx_size =
            get_sqr_tx_size(AOMMAX(block_size_wide[bsize], block_size_high[bsize]));

        if (max_tx_size >= TX_8X8)
        {
            category =
                B(txsize_sqr_up_map[tx_size] != max_tx_size && max_tx_size > TX_8X8) +
                (TX_SIZES - 1 - max_tx_size) * 2;
        }
        Debug.Assert(category != TXFM_PARTITION_CONTEXTS);
        return category * 3 + above + left;
    }

    // Compute the next partition in the direction of the sb_type stored in the mi
    // array, starting with bsize.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PARTITION_TYPE get_partition(AV1_COMMON cm,
                                                 int mi_row, int mi_col,
                                                 BLOCK_SIZE bsize)
    {
        CommonModeInfoParams mi_params = cm.mi_params;
        if (mi_row >= mi_params.mi_rows || mi_col >= mi_params.mi_cols)
            return PARTITION_INVALID;

        int offset = mi_row * mi_params.mi_stride + mi_col;
        Ptr<MB_MODE_INFO> mi = new Ptr<MB_MODE_INFO>(mi_params.mi_grid_base, offset);
        BLOCK_SIZE subsize = mi[0].bsize;

        Debug.Assert(bsize < BLOCK_SIZES_ALL);

        if (subsize == bsize) return PARTITION_NONE;

        int bhigh = mi_size_high[bsize];
        int bwide = mi_size_wide[bsize];
        int sshigh = mi_size_high[subsize];
        int sswide = mi_size_wide[subsize];

        if (bsize > BLOCK_8X8 && mi_row + bwide / 2 < mi_params.mi_rows &&
            mi_col + bhigh / 2 < mi_params.mi_cols)
        {
            // In this case, the block might be using an extended partition
            // type.
            MB_MODE_INFO mbmi_right = mi[bwide / 2];
            MB_MODE_INFO mbmi_below = mi[bhigh / 2 * mi_params.mi_stride];

            if (sswide == bwide)
            {
                // Smaller height but same width. Is PARTITION_HORZ_4, PARTITION_HORZ or
                // PARTITION_HORZ_B. To distinguish the latter two, check if the lower
                // half was split.
                if (sshigh * 4 == bhigh) return PARTITION_HORZ_4;
                Debug.Assert(sshigh * 2 == bhigh);

                if (mbmi_below.bsize == subsize)
                    return PARTITION_HORZ;
                else
                    return PARTITION_HORZ_B;
            }
            else if (sshigh == bhigh)
            {
                // Smaller width but same height. Is PARTITION_VERT_4, PARTITION_VERT or
                // PARTITION_VERT_B. To distinguish the latter two, check if the right
                // half was split.
                if (sswide * 4 == bwide) return PARTITION_VERT_4;
                Debug.Assert(sswide * 2 == bwide);

                if (mbmi_right.bsize == subsize)
                    return PARTITION_VERT;
                else
                    return PARTITION_VERT_B;
            }
            else
            {
                // Smaller width and smaller height. Might be PARTITION_SPLIT or could be
                // PARTITION_HORZ_A or PARTITION_VERT_A. If subsize isn't halved in both
                // dimensions, we immediately know this is a split (which will recurse to
                // get to subsize). Otherwise look down and to the right. With
                // PARTITION_VERT_A, the right block will have height bhigh; with
                // PARTITION_HORZ_A, the lower block with have width bwide. Otherwise
                // it's PARTITION_SPLIT.
                if (sswide * 2 != bwide || sshigh * 2 != bhigh) return PARTITION_SPLIT;

                if (mi_size_wide[mbmi_below.bsize] == bwide) return PARTITION_HORZ_A;
                if (mi_size_high[mbmi_right.bsize] == bhigh) return PARTITION_VERT_A;

                return PARTITION_SPLIT;
            }
        }
        int vert_split = B(sswide < bwide);
        int horz_split = B(sshigh < bhigh);
        int split_idx = (vert_split << 1) | horz_split;
        Debug.Assert(split_idx != 0);

        return base_partitions[split_idx];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_sb_size(SequenceHeader seq_params,
                                     BLOCK_SIZE sb_size)
    {
        seq_params.sb_size = sb_size;
        seq_params.mib_size = mi_size_wide[seq_params.sb_size];
        seq_params.mib_size_log2 = mi_size_wide_log2[seq_params.sb_size];
    }

    // Returns true if the frame is fully lossless at the coded resolution.
    // Note: If super-resolution is used, such a frame will still NOT be lossless at
    // the upscaled resolution.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_coded_lossless(AV1_COMMON cm,
                                          MACROBLOCKD xd)
    {
        int coded_lossless = 1;
        if (cm.seg.enabled != 0)
        {
            for (int i = 0; i < MAX_SEGMENTS; ++i)
            {
                if (xd.lossless[i] == 0)
                {
                    coded_lossless = 0;
                    break;
                }
            }
        }
        else
        {
            coded_lossless = xd.lossless[0];
        }
        return coded_lossless;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_valid_seq_level_idx(AV1_LEVEL seq_level_idx)
    {
        return B(seq_level_idx == SEQ_LEVEL_MAX ||
                 (seq_level_idx < SEQ_LEVELS &&
                  // The following levels are currently undefined.
                  seq_level_idx != SEQ_LEVEL_2_2 && seq_level_idx != SEQ_LEVEL_2_3 &&
                  seq_level_idx != SEQ_LEVEL_3_2 && seq_level_idx != SEQ_LEVEL_3_3 &&
                  seq_level_idx != SEQ_LEVEL_4_2 && seq_level_idx != SEQ_LEVEL_4_3
                  && seq_level_idx != SEQ_LEVEL_7_0 && seq_level_idx != SEQ_LEVEL_7_1 &&
                  seq_level_idx != SEQ_LEVEL_7_2 && seq_level_idx != SEQ_LEVEL_7_3 &&
                  seq_level_idx != SEQ_LEVEL_8_0 && seq_level_idx != SEQ_LEVEL_8_1 &&
                  seq_level_idx != SEQ_LEVEL_8_2 && seq_level_idx != SEQ_LEVEL_8_3
                  ));
    }
}
