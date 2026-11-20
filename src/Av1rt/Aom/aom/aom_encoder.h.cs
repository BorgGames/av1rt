// Port of libaom aom/aom_encoder.h

global using static Av1rt.Aom.aom_encoder;
global using aom_codec_er_flags_t = uint;
global using aom_codec_cx_pkt_kind = int;
global using aom_enc_pass = int;
global using aom_rc_mode = int;
global using aom_kf_mode = int;
global using aom_superres_mode = int;
// [port] C `long` is 32 bits on the (MSVC, Windows x64) oracle build.
global using aom_enc_frame_flags_t = int;
global using aom_fixed_buf = Av1rt.Aom.aom_fixed_buf_t;
global using aom_codec_cx_pkt = Av1rt.Aom.aom_codec_cx_pkt_t;
global using aom_rational = Av1rt.Aom.aom_rational_t;
global using aom_codec_enc_cfg = Av1rt.Aom.aom_codec_enc_cfg_t;

namespace Av1rt.Aom;

/*!\brief Generic fixed size buffer structure
 *
 * This structure is able to hold a reference to any fixed size buffer.
 */
// [port] A struct: aom_codec_enc_cfg embeds it and aom_encoder.c copies it by value. `void *buf`
// is a byte pointer (compressed data or first pass stats bytes).
internal partial struct aom_fixed_buf_t
{
    public Ptr<byte> buf; /*< Pointer to the data. Does NOT own the data! */
    public int sz;        /*< Length of the buffer, in chars */
}

/*!\brief Encoder output packet
 *
 * This structure contains the different kinds of output data the encoder
 * may produce while compressing a frame.
 */
// [port] A class: the API returns pointers to packets (`const aom_codec_cx_pkt_t *`), either into
// the packet list or to aom_codec_priv.enc.cx_data_pkt. The copies C makes
// (`list->pkts[list->cnt++] = *pkt`, `*modified_pkt = *pkt`) use CopyFrom.
internal sealed partial class aom_codec_cx_pkt_t
{
    public aom_codec_cx_pkt_kind kind; /*< packet variant */
    public aom_codec_cx_pkt_data data; /*< packet data */

    public void CopyFrom(aom_codec_cx_pkt_t other)
    {
        kind = other.kind;
        data = other.data;
    }
}

// [port] The `data` union of aom_codec_cx_pkt. Only the compressed frame member exists in the
// port: the PSNR packet (generate_psnr_packet, AOM_CODEC_USE_PSNR) and the two pass stats
// packets never occur in the subset. C code that reads the frame packet through the aliasing
// `data.raw` member (aom_codec_get_cx_data: data.raw.buf, data.raw.sz) uses data.frame.buf and
// data.frame.sz, which share the storage in C.
internal partial struct aom_codec_cx_pkt_data
{
    public aom_codec_cx_pkt_frame frame; /*< data for compressed frame packet */
}

// [port] The anonymous `frame` struct of the aom_codec_cx_pkt data union.
internal partial struct aom_codec_cx_pkt_frame
{
    public Ptr<byte> buf; /*< compressed data buffer */
    public int sz;        /*< length of compressed data */
    /*!\brief time stamp to show frame (in timebase units) */
    public aom_codec_pts_t pts;
    /*!\brief duration to show frame (in timebase units) */
    // [port] unsigned long: 32 bits on the oracle build.
    public uint duration;
    public aom_codec_frame_flags_t flags; /*< flags for this frame */
    /*!\brief the partition id defines the decoding order of the partitions.
     * Only applicable when "output partition" mode is enabled. First
     * partition has id 0.*/
    public int partition_id;
    /*!\brief size of the visible frame in this packet */
    public int vis_frame_size;
}

/*!\brief Rational Number
 *
 * This structure holds a fractional value.
 */
internal partial struct aom_rational_t
{
    public int num; /*< fraction numerator */
    public int den; /*< fraction denominator */
}

/*!\brief Encoder Config Options
 *
 * This type allows to enumerate and control flags defined for encoder control
 * via config file at runtime.
 */
// [port] A struct (35 unsigned ints): it is embedded in aom_codec_enc_cfg and copied with it.
internal partial struct cfg_options_t
{
    /*!\brief Indicate init by cfg file
     * 0 or 1
     */
    public uint init_by_cfg_file;
    /*!\brief Superblock size
     * 0, 64 or 128
     */
    public uint super_block_size;
    /*!\brief max partition size
     * 8, 16, 32, 64, 128
     */
    public uint max_partition_size;
    /*!\brief min partition size
     * 8, 16, 32, 64, 128
     */
    public uint min_partition_size;
    /*!\brief disable AB Shape partition type
     *
     */
    public uint disable_ab_partition_type;
    /*!\brief disable rectangular partition type
     *
     */
    public uint disable_rect_partition_type;
    /*!\brief disable 1:4/4:1 partition type
     *
     */
    public uint disable_1to4_partition_type;
    /*!\brief disable flip and identity transform type
     *
     */
    public uint disable_flip_idtx;
    /*!\brief disable CDEF filter
     *
     */
    public uint disable_cdef;
    /*!\brief disable Loop Restoration Filter
     *
     */
    public uint disable_lr;
    /*!\brief disable OBMC
     *
     */
    public uint disable_obmc;
    /*!\brief disable Warped Motion
     *
     */
    public uint disable_warp_motion;
    /*!\brief disable global motion
     *
     */
    public uint disable_global_motion;
    /*!\brief disable dist weighted compound
     *
     */
    public uint disable_dist_wtd_comp;
    /*!\brief disable diff weighted compound
     *
     */
    public uint disable_diff_wtd_comp;
    /*!\brief disable inter/intra compound
     *
     */
    public uint disable_inter_intra_comp;
    /*!\brief disable masked compound
     *
     */
    public uint disable_masked_comp;
    /*!\brief disable one sided compound
     *
     */
    public uint disable_one_sided_comp;
    /*!\brief disable Palette
     *
     */
    public uint disable_palette;
    /*!\brief disable Intra Block Copy
     *
     */
    public uint disable_intrabc;
    /*!\brief disable chroma from luma
     *
     */
    public uint disable_cfl;
    /*!\brief disable intra smooth mode
     *
     */
    public uint disable_smooth_intra;
    /*!\brief disable filter intra
     *
     */
    public uint disable_filter_intra;
    /*!\brief disable dual filter
     *
     */
    public uint disable_dual_filter;
    /*!\brief disable intra angle delta
     *
     */
    public uint disable_intra_angle_delta;
    /*!\brief disable intra edge filter
     *
     */
    public uint disable_intra_edge_filter;
    /*!\brief disable 64x64 transform
     *
     */
    public uint disable_tx_64x64;
    /*!\brief disable smooth inter/intra
     *
     */
    public uint disable_smooth_inter_intra;
    /*!\brief disable inter/inter wedge comp
     *
     */
    public uint disable_inter_inter_wedge;
    /*!\brief disable inter/intra wedge comp
     *
     */
    public uint disable_inter_intra_wedge;
    /*!\brief disable paeth intra
     *
     */
    public uint disable_paeth_intra;
    /*!\brief disable trellis quantization
     *
     */
    public uint disable_trellis_quant;
    /*!\brief disable ref frame MV
     *
     */
    public uint disable_ref_frame_mv;
    /*!\brief use reduced reference frame set
     *
     */
    public uint reduced_reference_set;
    /*!\brief use reduced transform type set
     *
     */
    public uint reduced_tx_type_set;
}

/*!\brief Encoder configuration structure
 *
 * This structure contains the encoder settings that have common representations
 * across all codecs. This doesn't imply that all codecs support all features,
 * however.
 */
// [port] A class: it is handled through pointers (ctx->config.enc, const aom_codec_enc_cfg_t *)
// and copied by value in a few places (`priv->cfg = *ctx->config.enc`,
// `*cfg = iface->enc.cfgs[i]`), which use CopyFrom. Every field is a value type, so CopyFrom is
// the full C struct copy.
internal sealed partial class aom_codec_enc_cfg_t
{
    /*
     * generic settings (g)
     */

    /*!\brief Algorithm specific "usage" value
     *
     * Algorithms may define multiple values for usage, which may convey the
     * intent of how the application intends to use the stream. If this value
     * is non-zero, consult the documentation for the codec to determine its
     * meaning.
     */
    public uint g_usage;

    /*!\brief Maximum number of threads to use
     *
     * For multi-threaded implementations, use no more than this number of
     * threads. The codec may use fewer threads than allowed. The value
     * 0 is equivalent to the value 1.
     */
    public uint g_threads;

    /*!\brief Bitstream profile to use
     *
     * Some codecs support a notion of multiple bitstream profiles. Typically
     * this maps to a set of features that are turned on or off. Often the
     * profile to use is determined by the features of the intended decoder.
     * Consult the documentation for the codec to determine the valid values
     * for this parameter, or set to zero for a sane default.
     */
    public uint g_profile; /*< profile of bitstream to use */

    /*!\brief Width of the frame
     *
     * This value identifies the presentation resolution of the frame,
     * in pixels. Note that the frames passed as input to the encoder must
     * have this resolution. Frames will be presented by the decoder in this
     * resolution, independent of any spatial resampling the encoder may do.
     */
    public uint g_w;

    /*!\brief Height of the frame
     *
     * This value identifies the presentation resolution of the frame,
     * in pixels. Note that the frames passed as input to the encoder must
     * have this resolution. Frames will be presented by the decoder in this
     * resolution, independent of any spatial resampling the encoder may do.
     */
    public uint g_h;

    /*!\brief Max number of frames to encode
     *
     * If force video mode is off (the default) and g_limit is 1, the encoder
     * will encode a still picture (still_picture is set to 1 in the sequence
     * header OBU). If in addition full_still_picture_hdr is 0 (the default),
     * the encoder will use a reduced header (reduced_still_picture_header is
     * set to 1 in the sequence header OBU) for the still picture.
     */
    public uint g_limit;

    /*!\brief Forced maximum width of the frame
     *
     * If this value is non-zero then it is used to force the maximum frame
     * width written in write_sequence_header().
     */
    public uint g_forced_max_frame_width;

    /*!\brief Forced maximum height of the frame
     *
     * If this value is non-zero then it is used to force the maximum frame
     * height written in write_sequence_header().
     */
    public uint g_forced_max_frame_height;

    /*!\brief Bit-depth of the codec
     *
     * This value identifies the bit_depth of the codec,
     * Only certain bit-depths are supported as identified in the
     * aom_bit_depth_t enum.
     */
    public aom_bit_depth_t g_bit_depth;

    /*!\brief Bit-depth of the input frames
     *
     * This value identifies the bit_depth of the input frames in bits.
     * Note that the frames passed as input to the encoder must have
     * this bit-depth.
     */
    public uint g_input_bit_depth;

    /*!\brief Stream timebase units
     *
     * Indicates the smallest interval of time, in fractional seconds, used by
     * the stream. For fixed frame rate material, or variable frame rate
     * material where frames are timed at a multiple of a given clock (ex:
     * video capture), the \ref RECOMMENDED method is to set the timebase to that
     * clock frequency. For video material where frames are timed in seconds,
     * the timebase is the reciprocal of the frame rate (ex: 1001/30000
     * for 29.970 Hz NTSC). This value must be positive.
     */
    public aom_rational_t g_timebase;

    /*!\brief Enable error resilient modes.
     *
     * The error resilient bitfield indicates to the encoder which features
     * it should enable to take measures for streaming over lossy or noisy
     * links.
     */
    public aom_codec_er_flags_t g_error_resilient;

    /*!\brief Multi-pass Encoding Mode
     *
     * This value should be set to the current phase for multi-pass encoding.
     * For single pass, set to #AOM_RC_ONE_PASS.
     */
    public aom_enc_pass g_pass;

    /*!\brief Allow lagged encoding
     *
     * If set, this value allows the encoder to consume a number of input
     * frames before producing output frames. This allows the encoder to
     * base decisions for the current frame on future frames. This does
     * increase the latency of the encoding pipeline, so it is not appropriate
     * in all situations (ex: realtime encoding).
     *
     * Note that this is a maximum value -- the encoder may produce frames
     * sooner than the given limit. Set this value to 0 to disable this
     * feature.
     */
    public uint g_lag_in_frames;

    /*
     * rate control settings (rc)
     */

    /*!\brief Temporal resampling configuration, if supported by the codec.
     *
     * Temporal resampling allows the codec to "drop" frames as a strategy to
     * meet its target data rate. This can cause temporal discontinuities in
     * the encoded video, which may appear as stuttering during playback. This
     * trade-off is often acceptable, but for many applications is not. It can
     * be disabled in these cases.
     *
     * Note that not all codecs support this feature. All aom AVx codecs do.
     * For other codecs, consult the documentation for that algorithm.
     *
     * This threshold is described as a percentage of the target data buffer.
     * When the data buffer falls below this percentage of fullness, a
     * dropped frame is indicated. Set the threshold to zero (0) to disable
     * this feature.
     */
    public uint rc_dropframe_thresh;

    /*!\brief Mode for spatial resampling, if supported by the codec.
     *
     * Spatial resampling allows the codec to compress a lower resolution
     * version of the frame, which is then upscaled by the decoder to the
     * correct presentation resolution. This increases visual quality at
     * low data rates, at the expense of CPU time on the encoder/decoder.
     */
    public uint rc_resize_mode;

    /*!\brief Frame resize denominator.
     *
     * The denominator for resize to use, assuming 8 as the numerator.
     *
     * Valid denominators are  8 - 16 for now.
     */
    public uint rc_resize_denominator;

    /*!\brief Keyframe resize denominator.
     *
     * The denominator for resize to use, assuming 8 as the numerator.
     *
     * Valid denominators are  8 - 16 for now.
     */
    public uint rc_resize_kf_denominator;

    /*!\brief Frame super-resolution scaling mode.
     *
     * Similar to spatial resampling, frame super-resolution integrates
     * upscaling after the encode/decode process. Taking control of upscaling and
     * using restoration filters should allow it to outperform normal resizing.
     */
    public aom_superres_mode rc_superres_mode;

    /*!\brief Frame super-resolution denominator.
     *
     * The denominator for superres to use. If fixed it will only change if the
     * cumulative scale change over resizing and superres is greater than 1/2;
     * this forces superres to reduce scaling.
     *
     * Valid denominators are 8 to 16.
     *
     * Used only by AOM_SUPERRES_FIXED.
     */
    public uint rc_superres_denominator;

    /*!\brief Keyframe super-resolution denominator.
     *
     * The denominator for superres to use. If fixed it will only change if the
     * cumulative scale change over resizing and superres is greater than 1/2;
     * this forces superres to reduce scaling.
     *
     * Valid denominators are 8 - 16 for now.
     */
    public uint rc_superres_kf_denominator;

    /*!\brief Frame super-resolution q threshold.
     *
     * The q level threshold after which superres is used.
     * Valid values are 1 to 63.
     *
     * Used only by AOM_SUPERRES_QTHRESH
     */
    public uint rc_superres_qthresh;

    /*!\brief Keyframe super-resolution q threshold.
     *
     * The q level threshold after which superres is used for key frames.
     * Valid values are 1 to 63.
     *
     * Used only by AOM_SUPERRES_QTHRESH
     */
    public uint rc_superres_kf_qthresh;

    /*!\brief Rate control algorithm to use.
     *
     * Indicates whether the end usage of this stream is to be streamed over
     * a bandwidth constrained link, indicating that Constant Bit Rate (CBR)
     * mode should be used, or whether it will be played back on a high
     * bandwidth link, as from a local disk, where higher variations in
     * bitrate are acceptable.
     */
    public aom_rc_mode rc_end_usage;

    /*!\brief Two-pass stats buffer.
     *
     * A buffer containing all of the stats packets produced in the first
     * pass, concatenated.
     */
    public aom_fixed_buf_t rc_twopass_stats_in;

    /*!\brief first pass mb stats buffer.
     *
     * A buffer containing all of the first pass mb stats packets produced
     * in the first pass, concatenated.
     */
    public aom_fixed_buf_t rc_firstpass_mb_stats_in;

    /*!\brief Target data rate
     *
     * Target bitrate to use for this stream, in kilobits per second.
     * Max allowed value is 2000000
     */
    public uint rc_target_bitrate;

    /*
     * quantizer settings
     */

    /*!\brief Minimum (Best Quality) Quantizer
     *
     * The quantizer is the most direct control over the quality of the
     * encoded image. The range of valid values for the quantizer is codec
     * specific. Consult the documentation for the codec to determine the
     * values to use. To determine the range programmatically, call
     * aom_codec_enc_config_default() with a usage value of 0.
     */
    public uint rc_min_quantizer;

    /*!\brief Maximum (Worst Quality) Quantizer
     *
     * The quantizer is the most direct control over the quality of the
     * encoded image. The range of valid values for the quantizer is codec
     * specific. Consult the documentation for the codec to determine the
     * values to use. To determine the range programmatically, call
     * aom_codec_enc_config_default() with a usage value of 0.
     */
    public uint rc_max_quantizer;

    /*
     * bitrate tolerance
     */

    /*!\brief Rate control adaptation undershoot control
     *
     * This value, controls the tolerance of the VBR algorithm to undershoot
     * and is used as a trigger threshold for more aggressive adaptation of Q.
     *
     * Valid values in the range 0-100.
     */
    public uint rc_undershoot_pct;

    /*!\brief Rate control adaptation overshoot control
     *
     * This value, controls the tolerance of the VBR algorithm to overshoot
     * and is used as a trigger threshold for more aggressive adaptation of Q.
     *
     * Valid values in the range 0-100.
     */
    public uint rc_overshoot_pct;

    /*
     * decoder buffer model parameters
     */

    /*!\brief Decoder Buffer Size
     *
     * This value indicates the amount of data that may be buffered by the
     * decoding application. Note that this value is expressed in units of
     * time (milliseconds). For example, a value of 5000 indicates that the
     * client will buffer (at least) 5000ms worth of encoded data. Use the
     * target bitrate (#rc_target_bitrate) to convert to bits/bytes, if
     * necessary.
     */
    public uint rc_buf_sz;

    /*!\brief Decoder Buffer Initial Size
     *
     * This value indicates the amount of data that will be buffered by the
     * decoding application prior to beginning playback. This value is
     * expressed in units of time (milliseconds). Use the target bitrate
     * (#rc_target_bitrate) to convert to bits/bytes, if necessary.
     */
    public uint rc_buf_initial_sz;

    /*!\brief Decoder Buffer Optimal Size
     *
     * This value indicates the amount of data that the encoder should try
     * to maintain in the decoder's buffer. This value is expressed in units
     * of time (milliseconds). Use the target bitrate (#rc_target_bitrate)
     * to convert to bits/bytes, if necessary.
     */
    public uint rc_buf_optimal_sz;

    /*
     * 2 pass rate control parameters
     */

    /*!\brief Two-pass mode CBR/VBR bias
     *
     * Bias, expressed on a scale of 0 to 100, for determining target size
     * for the current frame. The value 0 indicates the optimal CBR mode
     * value should be used. The value 100 indicates the optimal VBR mode
     * value should be used. Values in between indicate which way the
     * encoder should "lean."
     */
    public uint rc_2pass_vbr_bias_pct;

    /*!\brief Two-pass mode per-GOP minimum bitrate
     *
     * This value, expressed as a percentage of the target bitrate, indicates
     * the minimum bitrate to be used for a single GOP (aka "section")
     */
    public uint rc_2pass_vbr_minsection_pct;

    /*!\brief Two-pass mode per-GOP maximum bitrate
     *
     * This value, expressed as a percentage of the target bitrate, indicates
     * the maximum bitrate to be used for a single GOP (aka "section")
     */
    public uint rc_2pass_vbr_maxsection_pct;

    /*
     * keyframing settings (kf)
     */

    /*!\brief Option to enable forward reference key frame
     *
     */
    public int fwd_kf_enabled;

    /*!\brief Keyframe placement mode
     *
     * This value indicates whether the encoder should place keyframes at a
     * fixed interval, or determine the optimal placement automatically
     * (as governed by the #kf_min_dist and #kf_max_dist parameters)
     */
    public aom_kf_mode kf_mode;

    /*!\brief Keyframe minimum interval
     *
     * This value, expressed as a number of frames, prevents the encoder from
     * placing a keyframe nearer than kf_min_dist to the previous keyframe. At
     * least kf_min_dist frames non-keyframes will be coded before the next
     * keyframe. Set kf_min_dist equal to kf_max_dist for a fixed interval.
     */
    public uint kf_min_dist;

    /*!\brief Keyframe maximum interval
     *
     * This value, expressed as a number of frames, forces the encoder to code
     * a keyframe if one has not been coded in the last kf_max_dist frames.
     * A value of 0 implies all frames will be keyframes. Set kf_min_dist
     * equal to kf_max_dist for a fixed interval.
     */
    public uint kf_max_dist;

    /*!\brief sframe interval
     *
     * This value, expressed as a number of frames, forces the encoder to code
     * an S-Frame every sframe_dist frames.
     */
    public uint sframe_dist;

    /*!\brief sframe insertion mode
     *
     * This value must be set to 1 or 2, and tells the encoder how to insert
     * S-Frames. It will only have an effect if sframe_dist != 0.
     *
     * If altref is enabled:
     *   - if sframe_mode == 1, the considered frame will be made into an
     *     S-Frame only if it is an altref frame
     *   - if sframe_mode == 2, the next altref frame will be made into an
     *     S-Frame.
     *
     * Otherwise: the considered frame will be made into an S-Frame.
     *
     * \attention Not implemented.
     */
    public uint sframe_mode;

    /*!\brief Tile coding mode
     *
     * This value indicates the tile coding mode.
     * A value of 0 implies a normal non-large-scale tile coding. A value of 1
     * implies a large-scale tile coding.
     */
    public uint large_scale_tile;

    /*!\brief Monochrome mode
     *
     * If this is nonzero, the encoder will generate a monochrome stream
     * with no chroma planes.
     */
    public uint monochrome;

    /*!\brief full_still_picture_hdr
     *
     * If this is nonzero, the encoder will generate a full header
     * (reduced_still_picture_header is set to 0 in the sequence header OBU) even
     * for still picture encoding. If this is zero (the default), a reduced
     * header (reduced_still_picture_header is set to 1 in the sequence header
     * OBU) is used for still picture encoding. This flag has no effect when a
     * regular video with more than a single frame is encoded.
     */
    public uint full_still_picture_hdr;

    /*!\brief Bitstream syntax mode
     *
     * This value indicates the bitstream syntax mode.
     * A value of 0 indicates bitstream is saved as Section 5 bitstream. A value
     * of 1 indicates the bitstream is saved in Annex-B format
     */
    public uint save_as_annexb;

    /*!\brief Number of explicit tile widths specified
     *
     * This value indicates the number of tile widths specified
     * A value of 0 implies no tile widths are specified.
     * Tile widths are given in the array tile_widths[]
     */
    public int tile_width_count;

    /*!\brief Number of explicit tile heights specified
     *
     * This value indicates the number of tile heights specified
     * A value of 0 implies no tile heights are specified.
     * Tile heights are given in the array tile_heights[]
     */
    public int tile_height_count;

    /*!\brief Array of specified tile widths
     *
     * This array specifies tile widths (and may be empty)
     * The number of widths specified is given by tile_width_count
     */
    public Arr64<int> tile_widths;

    /*!\brief Array of specified tile heights
     *
     * This array specifies tile heights (and may be empty)
     * The number of heights specified is given by tile_height_count
     */
    public Arr64<int> tile_heights;

    /*!\brief Whether encoder should use fixed QP offsets.
     *
     * If a value of 1 is provided, encoder will use fixed QP offsets for frames
     * at different levels of the pyramid.
     * If a value of 0 is provided, encoder will NOT use fixed QP offsets.
     * Note: This option is only relevant for --end-usage=q.
     */
    public uint use_fixed_qp_offsets;

    /*!\brief Deprecated and ignored. DO NOT USE.
     *
     * TODO(aomedia:3269): Remove fixed_qp_offsets in libaom v4.0.0.
     */
    public Arr5<int> fixed_qp_offsets;

    /*!\brief Options defined per config file
     *
     */
    public cfg_options_t encoder_cfg;

    public void CopyFrom(aom_codec_enc_cfg_t o)
    {
        g_usage = o.g_usage;
        g_threads = o.g_threads;
        g_profile = o.g_profile;
        g_w = o.g_w;
        g_h = o.g_h;
        g_limit = o.g_limit;
        g_forced_max_frame_width = o.g_forced_max_frame_width;
        g_forced_max_frame_height = o.g_forced_max_frame_height;
        g_bit_depth = o.g_bit_depth;
        g_input_bit_depth = o.g_input_bit_depth;
        g_timebase = o.g_timebase;
        g_error_resilient = o.g_error_resilient;
        g_pass = o.g_pass;
        g_lag_in_frames = o.g_lag_in_frames;
        rc_dropframe_thresh = o.rc_dropframe_thresh;
        rc_resize_mode = o.rc_resize_mode;
        rc_resize_denominator = o.rc_resize_denominator;
        rc_resize_kf_denominator = o.rc_resize_kf_denominator;
        rc_superres_mode = o.rc_superres_mode;
        rc_superres_denominator = o.rc_superres_denominator;
        rc_superres_kf_denominator = o.rc_superres_kf_denominator;
        rc_superres_qthresh = o.rc_superres_qthresh;
        rc_superres_kf_qthresh = o.rc_superres_kf_qthresh;
        rc_end_usage = o.rc_end_usage;
        rc_twopass_stats_in = o.rc_twopass_stats_in;
        rc_firstpass_mb_stats_in = o.rc_firstpass_mb_stats_in;
        rc_target_bitrate = o.rc_target_bitrate;
        rc_min_quantizer = o.rc_min_quantizer;
        rc_max_quantizer = o.rc_max_quantizer;
        rc_undershoot_pct = o.rc_undershoot_pct;
        rc_overshoot_pct = o.rc_overshoot_pct;
        rc_buf_sz = o.rc_buf_sz;
        rc_buf_initial_sz = o.rc_buf_initial_sz;
        rc_buf_optimal_sz = o.rc_buf_optimal_sz;
        rc_2pass_vbr_bias_pct = o.rc_2pass_vbr_bias_pct;
        rc_2pass_vbr_minsection_pct = o.rc_2pass_vbr_minsection_pct;
        rc_2pass_vbr_maxsection_pct = o.rc_2pass_vbr_maxsection_pct;
        fwd_kf_enabled = o.fwd_kf_enabled;
        kf_mode = o.kf_mode;
        kf_min_dist = o.kf_min_dist;
        kf_max_dist = o.kf_max_dist;
        sframe_dist = o.sframe_dist;
        sframe_mode = o.sframe_mode;
        large_scale_tile = o.large_scale_tile;
        monochrome = o.monochrome;
        full_still_picture_hdr = o.full_still_picture_hdr;
        save_as_annexb = o.save_as_annexb;
        tile_width_count = o.tile_width_count;
        tile_height_count = o.tile_height_count;
        tile_widths = o.tile_widths;
        tile_heights = o.tile_heights;
        use_fixed_qp_offsets = o.use_fixed_qp_offsets;
        fixed_qp_offsets = o.fixed_qp_offsets;
        encoder_cfg = o.encoder_cfg;
    }
}

internal static partial class aom_encoder
{
    /*!\brief Current ABI version number
     *
     * \internal
     * If this file is altered in any way that changes the ABI, this value
     * must be bumped.  Examples include, but are not limited to, changing
     * types, removing or reassigning enums, adding/removing/rearranging
     * fields to structures
     *
     * Note: In the definition of AOM_ENCODER_ABI_VERSION, 3 is the value of
     * AOM_EXT_PART_ABI_VERSION in libaom v3.2.0. The old value of
     * AOM_EXT_PART_ABI_VERSION is used so as to not break the ABI version check in
     * aom_codec_enc_init_ver() when an application compiled against libaom v3.2.0
     * passes the old value of AOM_ENCODER_ABI_VERSION to aom_codec_enc_init_ver().
     * The external partition API is still experimental. When it is declared stable,
     * we will replace 3 with AOM_EXT_PART_ABI_VERSION in the definition of
     * AOM_ENCODER_ABI_VERSION.
     */
    internal const int AOM_ENCODER_ABI_VERSION =
        (10 + AOM_CODEC_ABI_VERSION + /*AOM_EXT_PART_ABI_VERSION=*/3);

    /*! \brief Encoder capabilities bitfield
     *
     *  Each encoder advertises the capabilities it supports as part of its
     *  ::aom_codec_iface_t interface structure. Capabilities are extra
     *  interfaces or functionality, and are not required to be supported
     *  by an encoder.
     *
     *  The available flags are specified by AOM_CODEC_CAP_* defines.
     */
    internal const int AOM_CODEC_CAP_PSNR = 0x10000; /*< Can issue PSNR packets */

    /*! Can support input images at greater than 8 bitdepth.
     */
    internal const int AOM_CODEC_CAP_HIGHBITDEPTH = 0x40000;

    /*! \brief Initialization-time Feature Enabling
     *
     *  Certain codec features must be known at initialization time, to allow
     *  for proper memory allocation.
     *
     *  The available flags are specified by AOM_CODEC_USE_* defines.
     */
    internal const int AOM_CODEC_USE_PSNR = 0x10000;         /*< Calculate PSNR on each frame */
    internal const int AOM_CODEC_USE_HIGHBITDEPTH = 0x40000; /*< Use high bitdepth */

    /*!\brief Improve resiliency against losses of whole frames */
    internal const int AOM_ERROR_RESILIENT_DEFAULT = 0x1;

    /*!\brief Encoder output packet variants
     *
     * This enumeration lists the different kinds of data packets that can be
     * returned by calls to aom_codec_get_cx_data(). Algorithms \ref MAY
     * extend this list to provide additional functionality.
     */
    // aom_codec_cx_pkt_kind
    internal const int AOM_CODEC_CX_FRAME_PKT = 0;    /*< Compressed video frame */
    internal const int AOM_CODEC_STATS_PKT = 1;       /*< Two-pass statistics for this frame */
    internal const int AOM_CODEC_FPMB_STATS_PKT = 2;  /*< first pass mb statistics for this frame */
    internal const int AOM_CODEC_PSNR_PKT = 3;        /*< PSNR statistics for this frame */
    internal const int AOM_CODEC_CUSTOM_PKT = 256;    /*< Algorithm extensions  */

    /*!\brief Multi-pass Encoding Pass
     *
     * AOM_RC_LAST_PASS is kept for backward compatibility.
     * If passes is not given and pass==2, the codec will assume passes=2.
     * For new code, it is recommended to use AOM_RC_SECOND_PASS and set
     * the "passes" member to 2 via the key & val API for two-pass encoding.
     */
    // aom_enc_pass
    internal const int AOM_RC_ONE_PASS = 0;    /*< Single pass mode */
    internal const int AOM_RC_FIRST_PASS = 1;  /*< First pass of multi-pass mode */
    internal const int AOM_RC_SECOND_PASS = 2; /*< Second pass of multi-pass mode */
    internal const int AOM_RC_THIRD_PASS = 3;  /*< Third pass of multi-pass mode */
    internal const int AOM_RC_LAST_PASS = 2;   /*< Final pass of two-pass mode */

    /*!\brief Rate control mode */
    // aom_rc_mode
    internal const int AOM_VBR = 0; /*< Variable Bit Rate (VBR) mode */
    internal const int AOM_CBR = 1; /*< Constant Bit Rate (CBR) mode */
    internal const int AOM_CQ = 2;  /*< Constrained Quality (CQ)  mode */
    internal const int AOM_Q = 3;   /*< Constant Quality (Q) mode */

    /*!\brief Keyframe placement mode.
     *
     * This enumeration determines whether keyframes are placed automatically by
     * the encoder or whether this behavior is disabled. Older releases of this
     * SDK were implemented such that AOM_KF_FIXED meant keyframes were disabled.
     * This name is confusing for this behavior, so the new symbols to be used
     * are AOM_KF_AUTO and AOM_KF_DISABLED.
     */
    // aom_kf_mode
    internal const int AOM_KF_FIXED = 0;    /*< deprecated, implies AOM_KF_DISABLED */
    internal const int AOM_KF_AUTO = 1;     /*< Encoder determines optimal placement automatically */
    internal const int AOM_KF_DISABLED = 0; /*< Encoder does not place keyframes. */

    /*!\brief Frame super-resolution mode. */
    // aom_superres_mode
    /*< Frame super-resolution is disabled for all frames. */
    internal const int AOM_SUPERRES_NONE = 0;
    /*< All frames are coded at the specified scale and super-resolved. */
    internal const int AOM_SUPERRES_FIXED = 1;
    /*< All frames are coded at a random scale and super-resolved. */
    internal const int AOM_SUPERRES_RANDOM = 2;
    /*< Super-resolution scale for each frame is determined based on the q index
       of that frame. */
    internal const int AOM_SUPERRES_QTHRESH = 3;
    /*< Full-resolution or super-resolution and the scale (in case of
       super-resolution) are automatically selected for each frame. */
    internal const int AOM_SUPERRES_AUTO = 4;

    /*!\brief Encoded Frame Flags
     *
     * This type indicates a bitfield to be passed to aom_codec_encode(), defining
     * per-frame boolean values. By convention, bits common to all codecs will be
     * named AOM_EFLAG_*, and bits specific to an algorithm will be named
     * /algo/_eflag_*. The lower order 16 bits are reserved for common use.
     */
    internal const int AOM_EFLAG_FORCE_KF = (1 << 0); /*< Force this frame to be a keyframe */
    /*!\brief Calculate PSNR for this frame, requires g_lag_in_frames to be 0. */
    internal const int AOM_EFLAG_CALCULATE_PSNR = (1 << 1);
    /*!\brief Encode this frame without changing the encoder's internal state. */
    internal const int AOM_EFLAG_FREEZE_INTERNAL_STATE = (1 << 2);

    internal const int MAX_TILE_WIDTHS = 64;  // maximum tile width array length
    internal const int MAX_TILE_HEIGHTS = 64;  // maximum tile height array length

    /*!\brief usage parameter analogous to AV1 GOOD QUALITY mode. */
    internal const uint AOM_USAGE_GOOD_QUALITY = 0u;
    /*!\brief usage parameter analogous to AV1 REALTIME mode. */
    internal const uint AOM_USAGE_REALTIME = 1u;
    /*!\brief usage parameter analogous to AV1 all intra mode. */
    internal const uint AOM_USAGE_ALL_INTRA = 2u;

    // [port] The functions declared in aom_encoder.h (aom_codec_enc_init_ver,
    // aom_codec_enc_config_default, aom_codec_encode, aom_codec_get_cx_data, ...) are defined
    // in aom/src/aom_encoder.c and belong to its port. aom_codec_enc_init(ctx, iface, cfg, flags)
    // is aom_codec_enc_init_ver(ctx, iface, cfg, flags, AOM_ENCODER_ABI_VERSION).
}
