// Port of libaom av1/common/blockd.h

// <gen:aliases>
global using DIFFWTD_MASK_TYPE = int;
global using FRAME_TYPE = int;
global using mv_precision = int;
// </gen:aliases>
global using MACROBLOCKD_PLANE = Av1rt.Aom.macroblockd_plane;
global using macroblockd = Av1rt.Aom.MACROBLOCKD;
global using MapCdf = ushort[][][];
global using ColorCost = int[][][];

global using static Av1rt.Aom.blockd;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal partial struct BUFFER_SET
{
    public Arr3<Ptr<byte>> plane;  // [MAX_MB_PLANE]
    public Arr3<int> stride;       // [MAX_MB_PLANE]
}

internal partial struct PALETTE_MODE_INFO
{
    // Value of base colors for Y, U, and V
    public Arr24<ushort> palette_colors;  // [3 * PALETTE_MAX_SIZE]
    // Number of base colors for Y (0) and UV (1)
    public Arr2<byte> palette_size;
}

internal partial struct FILTER_INTRA_MODE_INFO
{
    public FILTER_INTRA_MODE filter_intra_mode;
    public byte use_filter_intra;
}

internal partial struct RD_STATS
{
    public int rate;
    // Rate corresponding to "skip" flag being set (all coefficients zero).
    public int zero_rate;
    public long dist;
    // Please be careful of using rdcost, it's not guaranteed to be set all the
    // time.
    // TODO(angiebird): Create a set of functions to manipulate the RD_STATS. In
    // these functions, make sure rdcost is always up-to-date according to
    // rate/dist.
    public long rdcost;
    public long sse;
    public byte skip_txfm;  // sse should equal to dist when skip_txfm == 1
}

internal partial struct INTERINTER_COMPOUND_DATA
{
    public Ptr<byte> seg_mask;
    public sbyte wedge_index;
    public sbyte wedge_sign;
    public DIFFWTD_MASK_TYPE mask_type;
    public COMPOUND_TYPE type;
}

/*! \brief Stores the prediction/txfm mode of the current coding block */
// [port] Class with CopyFrom/Clear: lives in the mi_alloc array and is referenced through
// the mi grid, but the encoder also copies it by value (`*mbmi = best_mbmi`, saved
// candidates in the nonrd mode search, PICK_MODE_CONTEXT.mic).
// PORT-NOTE: the C bitfields (segment_id:3, seg_id_predicted:1, ref_mv_idx:2, skip_mode:1,
// use_intrabc:1, comp_group_idx:1, compound_idx:1, use_wedge_interintra:1,
// cdef_strength:4) are plain byte/sbyte fields; their widths are not enforced.
internal sealed partial class MB_MODE_INFO
{
    /*! \brief The block size of the current coding block */
    public BLOCK_SIZE bsize;
    /*! \brief The partition type of the current coding block. */
    public PARTITION_TYPE partition;
    /*! \brief The prediction mode used */
    public PREDICTION_MODE mode;
    /*! \brief The UV mode when intra is used */
    public UV_PREDICTION_MODE uv_mode;
    /*! \brief The q index for the current coding block. */
    public int current_qindex;

    /*! \brief The motion vectors used by the current inter mode */
    public Arr2<int_mv> mv;
    /*! \brief The reference frames for the MV */
    // [port] An array (not Arr2) because C passes mbmi->ref_frame as a pointer.
    public readonly MV_REFERENCE_FRAME[] ref_frame = new MV_REFERENCE_FRAME[2];
    /*! \brief Filter used in subpel interpolation. */
    public int_interpfilters interp_filters;
    /*! \brief The motion mode used by the inter prediction. */
    public MOTION_MODE motion_mode;
    /*! \brief Number of samples used by warp causal */
    public byte num_proj_ref;
    /*! \brief The number of overlapped neighbors above/left for obmc/warp motion
     * mode. */
    public byte overlappable_neighbors;
    /*! \brief The parameters used in warp motion mode. */
    public WarpedMotionParams wm_params;
    /*! \brief The type of intra mode used by inter-intra */
    public INTERINTRA_MODE interintra_mode;
    /*! \brief The type of wedge used in interintra mode. */
    public sbyte interintra_wedge_index;
    /*! \brief Struct that stores the data used in interinter compound mode. */
    public INTERINTER_COMPOUND_DATA interinter_comp;

    /*! \brief Directional mode delta: the angle is base angle + (angle_delta *
     * step). */
    public Arr2<sbyte> angle_delta;  // [PLANE_TYPES]
    /*! \brief The type of filter intra mode used (if applicable). */
    public FILTER_INTRA_MODE_INFO filter_intra_mode_info;
    /*! \brief Chroma from Luma: Joint sign of alpha Cb and alpha Cr */
    public sbyte cfl_alpha_signs;
    /*! \brief Chroma from Luma: Index of the alpha Cb and alpha Cr combination */
    public byte cfl_alpha_idx;
    /*! \brief Stores the size and colors of palette mode */
    public PALETTE_MODE_INFO palette_mode_info;

    /*! \brief Whether to skip transforming and sending. */
    public byte skip_txfm;
    /*! \brief Transform size when fixed size txfm is used (e.g. intra modes). */
    public TX_SIZE tx_size;
    /*! \brief Transform size when recursive txfm tree is on. */
    // [port] An array because C memsets it and passes it as a pointer.
    public readonly TX_SIZE[] inter_tx_size = new TX_SIZE[INTER_TX_SIZE_BUF_LEN];

    /*! \copydoc MACROBLOCKD::delta_lf_from_base */
    public sbyte delta_lf_from_base;
    /*! \copydoc MACROBLOCKD::delta_lf */
    public Arr4<sbyte> delta_lf;  // [FRAME_LF_COUNT]

    /*! \brief The segment id */
    public byte segment_id;  // : 3
    /*! \brief Only used for temporal segmentation */
    public byte seg_id_predicted;  // : 1
    /*! \brief Which ref_mv to use */
    public byte ref_mv_idx;  // : 2
    /*! \brief Inter skip mode */
    public byte skip_mode;  // : 1
    /*! \brief Whether intrabc is used. */
    public byte use_intrabc;  // : 1
    /*! \brief Indicates if masked compound is used(1) or not (0). */
    public byte comp_group_idx;  // : 1
    /*! \brief Indicates whether dist_wtd_comp(0) is used or not (0). */
    public byte compound_idx;  // : 1
    /*! \brief Whether to use interintra wedge */
    public byte use_wedge_interintra;  // : 1
    /*! \brief CDEF strength per BLOCK_64X64 */
    public sbyte cdef_strength;  // : 4

    /// <summary>C struct assignment (<c>*this = *other</c>): copies every field.</summary>
    public void CopyFrom(MB_MODE_INFO other)
    {
        bsize = other.bsize;
        partition = other.partition;
        mode = other.mode;
        uv_mode = other.uv_mode;
        current_qindex = other.current_qindex;
        mv = other.mv;
        ref_frame[0] = other.ref_frame[0];
        ref_frame[1] = other.ref_frame[1];
        interp_filters = other.interp_filters;
        motion_mode = other.motion_mode;
        num_proj_ref = other.num_proj_ref;
        overlappable_neighbors = other.overlappable_neighbors;
        wm_params = other.wm_params;
        interintra_mode = other.interintra_mode;
        interintra_wedge_index = other.interintra_wedge_index;
        interinter_comp = other.interinter_comp;
        angle_delta = other.angle_delta;
        filter_intra_mode_info = other.filter_intra_mode_info;
        cfl_alpha_signs = other.cfl_alpha_signs;
        cfl_alpha_idx = other.cfl_alpha_idx;
        palette_mode_info = other.palette_mode_info;
        skip_txfm = other.skip_txfm;
        tx_size = other.tx_size;
        Array.Copy(other.inter_tx_size, inter_tx_size, INTER_TX_SIZE_BUF_LEN);
        delta_lf_from_base = other.delta_lf_from_base;
        delta_lf = other.delta_lf;
        segment_id = other.segment_id;
        seg_id_predicted = other.seg_id_predicted;
        ref_mv_idx = other.ref_mv_idx;
        skip_mode = other.skip_mode;
        use_intrabc = other.use_intrabc;
        comp_group_idx = other.comp_group_idx;
        compound_idx = other.compound_idx;
        use_wedge_interintra = other.use_wedge_interintra;
        cdef_strength = other.cdef_strength;
    }

    private static readonly MB_MODE_INFO s_zero = new();

    /// <summary><c>memset(mbmi, 0, sizeof(*mbmi))</c> / <c>av1_zero(*mbmi)</c>.</summary>
    public void Clear() => CopyFrom(s_zero);
}

internal partial struct buf_2d
{
    public Ptr<byte> buf;
    public Ptr<byte> buf0;
    public int width;
    public int height;
    public int stride;
}

internal partial struct eob_info
{
    public ushort eob;
    public ushort max_scan_line;
}

internal sealed partial class CB_BUFFER
{
    // The entire prediction block in this struct
    public readonly int[][] dqcoeff = new_jagged<int>(MAX_MB_PLANE, MAX_SB_SQUARE);
    public readonly eob_info[][] eob_data =
        new_jagged<eob_info>(MAX_MB_PLANE, MAX_SB_SQUARE / (TX_SIZE_W_MIN * TX_SIZE_H_MIN));
    public readonly byte[][] color_index_map = new_jagged<byte>(2, MAX_SB_SQUARE);
}

/*! \brief Variables related to current coding block */
internal sealed partial class macroblockd_plane
{
    public PLANE_TYPE plane_type;
    public int subsampling_x;
    public int subsampling_y;
    public buf_2d dst;
    public readonly buf_2d[] pre = new buf_2d[2];
    public Ptr<ENTROPY_CONTEXT> above_entropy_context;
    public Ptr<ENTROPY_CONTEXT> left_entropy_context;

    // The dequantizers below are true dequantizers used only in the
    // dequantization process.  They have the same coefficient
    // shift/scale as TX.
    public readonly short[][] seg_dequant_QTX = new_jagged<short>(MAX_SEGMENTS, 2);
    // Pointer to color index map of:
    // - Current coding block, on encoder side.
    // - Current superblock, on decoder side.
    public Ptr<byte> color_index_map;

    // block size in pixels
    public byte width, height;

    public readonly Ptr<qm_val_t>[][] seg_iqmatrix = new_jagged<Ptr<qm_val_t>>(MAX_SEGMENTS, TX_SIZES_ALL);
    public readonly Ptr<qm_val_t>[][] seg_qmatrix = new_jagged<Ptr<qm_val_t>>(MAX_SEGMENTS, TX_SIZES_ALL);

    // [port] Not in C. The qm level whose gqmatrix/giqmatrix rows seg_qmatrix[s]/seg_iqmatrix[s]
    // currently hold (-1: unknown), so set_qmatrix can skip re-copying identical rows (the copy
    // of ref-holding structs is costly in .NET and runs on every segment change). Reset by every
    // other writer of the rows.
    public readonly int[] seg_qm_level = [-1, -1, -1, -1, -1, -1, -1, -1];

    /// <summary>C struct assignment: copies every field (arrays element-wise).</summary>
    public void CopyFrom(macroblockd_plane other)
    {
        plane_type = other.plane_type;
        subsampling_x = other.subsampling_x;
        subsampling_y = other.subsampling_y;
        dst = other.dst;
        pre[0] = other.pre[0];
        pre[1] = other.pre[1];
        above_entropy_context = other.above_entropy_context;
        left_entropy_context = other.left_entropy_context;
        av1_copy(seg_dequant_QTX, other.seg_dequant_QTX);
        color_index_map = other.color_index_map;
        width = other.width;
        height = other.height;
        av1_copy(seg_iqmatrix, other.seg_iqmatrix);
        av1_copy(seg_qmatrix, other.seg_qmatrix);
        other.seg_qm_level.AsSpan().CopyTo(seg_qm_level);
    }
}

internal partial struct WienerInfo
{
    public Arr8<short> vfilter;  // InterpKernel
    public Arr8<short> hfilter;  // InterpKernel
}

internal partial struct SgrprojInfo
{
    public int ep;
    public Arr2<int> xqd;
}

internal sealed partial class CFL_CTX
{
    // Q3 reconstructed luma pixels (only Q2 is required, but Q3 is used to avoid
    // shifts)
    public readonly ushort[] recon_buf_q3 = new ushort[CFL_BUF_SQUARE];
    // Q3 AC contributions (reconstructed luma pixels - tx block avg)
    public readonly short[] ac_buf_q3 = new short[CFL_BUF_SQUARE];

    // Cache the DC_PRED when performing RDO, so it does not have to be recomputed
    // for every scaling parameter
    public readonly bool[] dc_pred_is_cached = new bool[CFL_PRED_PLANES];
    public bool use_dc_pred_cache;
    // Only cache the first row of the DC_PRED
    public readonly short[][] dc_pred_cache = new_jagged<short>(CFL_PRED_PLANES, CFL_BUF_LINE);

    // Height and width currently used in the CfL prediction buffer.
    public int buf_height, buf_width;

    public int are_parameters_computed;

    // Chroma subsampling
    public int subsampling_x, subsampling_y;

    // Whether the reconstructed luma pixels need to be stored
    public int store_y;

    /// <summary>C struct assignment: copies every field (arrays element-wise).</summary>
    public void CopyFrom(CFL_CTX other)
    {
        av1_copy(recon_buf_q3, other.recon_buf_q3);
        av1_copy(ac_buf_q3, other.ac_buf_q3);
        av1_copy(dc_pred_is_cached, other.dc_pred_is_cached);
        use_dc_pred_cache = other.use_dc_pred_cache;
        av1_copy(dc_pred_cache, other.dc_pred_cache);
        buf_height = other.buf_height;
        buf_width = other.buf_width;
        are_parameters_computed = other.are_parameters_computed;
        subsampling_x = other.subsampling_x;
        subsampling_y = other.subsampling_y;
        store_y = other.store_y;
    }
}

internal partial struct DIST_WTD_COMP_PARAMS
{
    public int use_dist_wtd_comp_avg;
    public int fwd_offset;
    public int bck_offset;
}

/*! \brief Variables related to current coding block.
 *
 * This is a common set of variables used by both encoder and decoder.
 * Most/all of the pointers are mere pointers to actual arrays are allocated
 * elsewhere. This is mostly for coding convenience.
 */
// [port] Class with CopyFrom: MACROBLOCK (which embeds it as e_mbd) is copied by value
// when worker threads are set up (`thread_data->td->mb = cpi->td.mb`).
internal sealed partial class MACROBLOCKD
{
    // Position of the current coding block in units of MI (4x4). mi_row/mi_col are
    // relative to the frame.
    public int mi_row;
    public int mi_col;
    // The stride of the mi grid (in units of MI).
    public int mi_stride;

    // True if current block transmits chroma information.
    public bool is_chroma_ref;

    // Info specific to each plane.
    public readonly macroblockd_plane[] plane = new_array<macroblockd_plane>(MAX_MB_PLANE);

    // Tile related info.
    public TileInfo tile;

    // Appropriate offset inside cm->mi_params.mi_grid_base based on current
    // mi_row and mi_col.
    public Ptr<MB_MODE_INFO> mi;

    // True if 4x4 block above the current block is available.
    public bool up_available;
    // True if 4x4 block to the left of the current block is available.
    public bool left_available;
    // True if the above chrome reference block is available.
    public bool chroma_up_available;
    // True if the left chrome reference block is available.
    public bool chroma_left_available;

    // MB_MODE_INFO for 4x4 block to the left of the current block, if
    // left_available == true; otherwise NULL.
    public MB_MODE_INFO left_mbmi;
    // MB_MODE_INFO for 4x4 block above the current block, if
    // up_available == true; otherwise NULL.
    public MB_MODE_INFO above_mbmi;
    // Above chroma reference block if is_chroma_ref == true for the current block
    // and chroma_up_available == true; otherwise NULL.
    public MB_MODE_INFO chroma_left_mbmi;
    // Left chroma reference block if is_chroma_ref == true for the current block
    // and chroma_left_available == true; otherwise NULL.
    public MB_MODE_INFO chroma_above_mbmi;

    // Appropriate offset based on current 'mi_row' and 'mi_col', inside
    // 'tx_type_map' in one of 'CommonModeInfoParams', 'PICK_MODE_CONTEXT' or
    // 'MACROBLOCK' structs.
    // PORT-NOTE: C declares uint8_t * here and TX_TYPE * in CommonModeInfoParams; both are
    // Ptr<byte> in the port (TX_TYPE values fit), so writes need a (byte) cast.
    public Ptr<byte> tx_type_map;
    // Stride for 'tx_type_map'. Note that this may / may not be same as
    // 'mi_stride', depending on which actual array 'tx_type_map' points to.
    public int tx_type_map_stride;

    // Distance of this macroblock from frame edges in 1/8th pixel units.
    public int mb_to_left_edge;
    public int mb_to_right_edge;
    public int mb_to_top_edge;
    public int mb_to_bottom_edge;

    // Scale factors for reference frames of the current block.
    // These are pointers into 'cm->ref_scale_factors'.
    public readonly scale_factors[] block_ref_scale_factors = new scale_factors[2];

    // - On encoder side: points to cpi->source, which is the
    //   final input source frame (after resizing etc.).
    // - On decoder side: points to cm->cur_frame->buf.
    public YV12_BUFFER_CONFIG cur_buf;

    // Entropy contexts for the above blocks.
    // above_entropy_context[i][j] corresponds to above entropy context for ith
    // plane and jth mi column of this *frame*, wrt current 'mi_row'.
    // These are pointers into 'cm->above_contexts.entropy'.
    public readonly Ptr<ENTROPY_CONTEXT>[] above_entropy_context = new Ptr<ENTROPY_CONTEXT>[MAX_MB_PLANE];
    // Entropy contexts for the left blocks.
    // left_entropy_context[i][j] corresponds to left entropy context for ith
    // plane and jth mi row of this *superblock*, wrt current 'mi_col'.
    // Note: These contain actual data, NOT pointers.
    public readonly ENTROPY_CONTEXT[][] left_entropy_context = new_jagged<ENTROPY_CONTEXT>(MAX_MB_PLANE, MAX_MIB_SIZE);

    // Partition contexts for the above blocks.
    // above_partition_context[i] corresponds to above partition context for ith
    // mi column of this *frame*, wrt current 'mi_row'.
    // This is a pointer into 'cm->above_contexts.partition'.
    public Ptr<PARTITION_CONTEXT> above_partition_context;
    // Partition contexts for the left blocks.
    // left_partition_context[i] corresponds to left partition context for ith
    // mi row of this *superblock*, wrt current 'mi_col'.
    // Note: These contain actual data, NOT pointers.
    public readonly PARTITION_CONTEXT[] left_partition_context = new PARTITION_CONTEXT[MAX_MIB_SIZE];

    // Transform contexts for the above blocks.
    // above_txfm_context[i] corresponds to above transform context for ith mi col
    // from the current position (mi row and mi column) for this *frame*.
    // This is a pointer into 'cm->above_contexts.txfm'.
    public Ptr<TXFM_CONTEXT> above_txfm_context;
    // Transform contexts for the left blocks.
    // left_txfm_context[i] corresponds to left transform context for ith mi row
    // from the current position (mi_row and mi_col) for this *superblock*.
    // This is a pointer into 'left_txfm_context_buffer'.
    public Ptr<TXFM_CONTEXT> left_txfm_context;
    // left_txfm_context_buffer[i] is the left transform context for ith mi_row
    // in this *superblock*.
    // Behaves like an internal actual buffer which 'left_txt_context' points to,
    // and never accessed directly except to fill in initial default values.
    public readonly TXFM_CONTEXT[] left_txfm_context_buffer = new TXFM_CONTEXT[MAX_MIB_SIZE];

    // Default values for the two restoration filters for each plane.
    // Default values for the two restoration filters for each plane.
    // These values are used as reference values when writing the bitstream. That
    // is, we transmit the delta between the actual values in
    // cm->rst_info[plane].unit_info[unit_idx] and these reference values.
    public readonly WienerInfo[] wiener_info = new WienerInfo[MAX_MB_PLANE];    // Defaults for Wiener filter
    public readonly SgrprojInfo[] sgrproj_info = new SgrprojInfo[MAX_MB_PLANE]; // Defaults for SGR filter

    // Block dimensions in MB_MODE_INFO units.
    public byte width;   // Block width in MB_MODE_INFO units
    public byte height;  // Block height in MB_MODE_INFO units

    // Contains the motion vector candidates found during motion vector prediction
    // process. ref_mv_stack[i] contains the candidates for ith type of
    // reference frame (single/compound). The actual number of candidates found in
    // ref_mv_stack[i] is stored in either dcb->ref_mv_count[i] (decoder side)
    // or mbmi_ext->ref_mv_count[i] (encoder side).
    public readonly CANDIDATE_MV[][] ref_mv_stack = new_jagged<CANDIDATE_MV>(MODE_CTX_REF_FRAMES, MAX_REF_MV_STACK_SIZE);
    // weight[i][j] is the weight for ref_mv_stack[i][j] and used to compute the
    // DRL (dynamic reference list) mode contexts.
    public readonly ushort[][] weight = new_jagged<ushort>(MODE_CTX_REF_FRAMES, MAX_REF_MV_STACK_SIZE);

    // True if this is the last vertical rectangular block in a VERTICAL or
    // VERTICAL_4 partition.
    public bool is_last_vertical_rect;
    // True if this is the 1st horizontal rectangular block in a HORIZONTAL or
    // HORIZONTAL_4 partition.
    public bool is_first_horizontal_rect;

    // Counts of each reference frame in the above and left neighboring blocks.
    // NOTE: Take into account both single and comp references.
    public readonly byte[] neighbors_ref_counts = new byte[REF_FRAMES];

    // Current CDFs of all the symbols for the current tile.
    public FRAME_CONTEXT tile_ctx;

    // Bit depth: copied from cm->seq_params->bit_depth for convenience.
    public int bd;

    // Quantizer index for each segment (base qindex + delta for each segment).
    public readonly int[] qindex = new int[MAX_SEGMENTS];
    // lossless[s] is true if segment 's' is coded losslessly.
    public readonly int[] lossless = new int[MAX_SEGMENTS];
    // Q index for the coding blocks in this superblock will be stored in
    // mbmi->current_qindex. Now, when cm->delta_q_info.delta_q_present_flag is
    // true, mbmi->current_qindex is computed by taking 'current_base_qindex' as
    // the base, and adding any transmitted delta qindex on top of it.
    // Precisely, this is the latest qindex used by the first coding block of a
    // non-skip superblock in the current tile; OR
    // same as cm->quant_params.base_qindex (if not explicitly set yet).
    // Note: This is 'CurrentQIndex' in the AV1 spec.
    public int current_base_qindex;

    // Same as cm->features.cur_frame_force_integer_mv.
    public int cur_frame_force_integer_mv;

    // Pointer to cm->error.
    public aom_internal_error_info error_info;

    // Same as cm->global_motion.
    public Ptr<WarpedMotionParams> global_motion;

    // Since actual frame level loop filtering level value is not available
    // at the beginning of the tile (only available during actual filtering)
    // at encoder side.we record the delta_lf (against the frame level loop
    // filtering level) and code the delta between previous superblock's delta
    // lf and current delta lf. It is equivalent to the delta between previous
    // superblock's actual lf and current lf.
    public sbyte delta_lf_from_base;
    // We have four frame filter levels for different plane and direction. So, to
    // support the per superblock update, we need to add a few more params:
    // 0. delta loop filter level for y plane vertical
    // 1. delta loop filter level for y plane horizontal
    // 2. delta loop filter level for u plane
    // 3. delta loop filter level for v plane
    // To make it consistent with the reference to each filter level in segment,
    // we need to -1, since
    // - SEG_LVL_ALT_LF_Y_V = 1;
    // - SEG_LVL_ALT_LF_Y_H = 2;
    // - SEG_LVL_ALT_LF_U = 3;
    // - SEG_LVL_ALT_LF_V = 4;
    public readonly sbyte[] delta_lf = new sbyte[FRAME_LF_COUNT];
    // cdef_transmitted[i] is true if CDEF strength for ith CDEF unit in the
    // current superblock has already been read from (decoder) / written to
    // (encoder) the bitstream; and false otherwise.
    // More detail:
    // 1. CDEF strength is transmitted only once per CDEF unit, in the 1st
    // non-skip coding block. So, we need this array to keep track of whether CDEF
    // strengths for the given CDEF units have been transmitted yet or not.
    // 2. Superblock size can be either 128x128 or 64x64, but CDEF unit size is
    // fixed to be 64x64. So, there may be 4 CDEF units within a superblock (if
    // superblock size is 128x128). Hence the array size is 4.
    // 3. In the current implementation, CDEF strength for this CDEF unit is
    // stored in the MB_MODE_INFO of the 1st block in this CDEF unit (inside
    // cm->mi_params.mi_grid_base).
    public readonly bool[] cdef_transmitted = new bool[4];

    // Mask for this block used for compound prediction.
    public Ptr<byte> seg_mask;

    // CFL (chroma from luma) related parameters.
    public readonly CFL_CTX cfl = new();

    // Offset to plane[p].color_index_map.
    // Currently:
    // - On encoder side, this is always 0 as 'color_index_map' is allocated per
    // *coding block* there.
    // - On decoder side, this may be non-zero, as 'color_index_map' is a (static)
    // memory pointing to the base of a *superblock* there, and we need an offset
    // to it to get the color index map for current coding block.
    public readonly ushort[] color_index_map_offset = new ushort[2];

    // Temporary buffer used for convolution in case of compound reference only
    // for (weighted or uniform) averaging operation.
    // There are pointers to actual buffers allocated elsewhere: e.g.
    // - In decoder, 'pbi->td.tmp_conv_dst' or
    // 'pbi->thread_data[t].td->xd.tmp_conv_dst' and
    // - In encoder, 'x->tmp_conv_dst' or
    // 'cpi->tile_thr_data[t].td->mb.tmp_conv_dst'.
    public Ptr<CONV_BUF_TYPE> tmp_conv_dst;
    // Temporary buffers used to build OBMC prediction by above (index 0) and left
    // (index 1) predictors respectively.
    // tmp_obmc_bufs[i][p * MAX_SB_SQUARE] is the buffer used for plane 'p'.
    // There are pointers to actual buffers allocated elsewhere: e.g.
    // - In decoder, 'pbi->td.tmp_obmc_bufs' or
    // 'pbi->thread_data[t].td->xd.tmp_conv_dst' and
    // -In encoder, 'x->tmp_pred_bufs' or
    // 'cpi->tile_thr_data[t].td->mb.tmp_pred_bufs'.
    public readonly Ptr<byte>[] tmp_obmc_bufs = new Ptr<byte>[2];

    // Temporary buffer used for upsampled prediction.
    public Ptr<byte> tmp_upsample_pred;

    /// <summary>C struct assignment: copies every field (embedded arrays element-wise,
    /// pointers by reference).</summary>
    public void CopyFrom(MACROBLOCKD other)
    {
        mi_row = other.mi_row;
        mi_col = other.mi_col;
        mi_stride = other.mi_stride;
        is_chroma_ref = other.is_chroma_ref;
        for (int i = 0; i < MAX_MB_PLANE; i++) plane[i].CopyFrom(other.plane[i]);
        tile = other.tile;
        mi = other.mi;
        up_available = other.up_available;
        left_available = other.left_available;
        chroma_up_available = other.chroma_up_available;
        chroma_left_available = other.chroma_left_available;
        left_mbmi = other.left_mbmi;
        above_mbmi = other.above_mbmi;
        chroma_left_mbmi = other.chroma_left_mbmi;
        chroma_above_mbmi = other.chroma_above_mbmi;
        tx_type_map = other.tx_type_map;
        tx_type_map_stride = other.tx_type_map_stride;
        mb_to_left_edge = other.mb_to_left_edge;
        mb_to_right_edge = other.mb_to_right_edge;
        mb_to_top_edge = other.mb_to_top_edge;
        mb_to_bottom_edge = other.mb_to_bottom_edge;
        av1_copy(block_ref_scale_factors, other.block_ref_scale_factors);
        cur_buf = other.cur_buf;
        av1_copy(above_entropy_context, other.above_entropy_context);
        av1_copy(left_entropy_context, other.left_entropy_context);
        above_partition_context = other.above_partition_context;
        av1_copy(left_partition_context, other.left_partition_context);
        above_txfm_context = other.above_txfm_context;
        // C copies the pointer as is (it may point into the other xd's buffer).
        left_txfm_context = other.left_txfm_context;
        av1_copy(left_txfm_context_buffer, other.left_txfm_context_buffer);
        av1_copy(wiener_info, other.wiener_info);
        av1_copy(sgrproj_info, other.sgrproj_info);
        width = other.width;
        height = other.height;
        av1_copy(ref_mv_stack, other.ref_mv_stack);
        av1_copy(weight, other.weight);
        is_last_vertical_rect = other.is_last_vertical_rect;
        is_first_horizontal_rect = other.is_first_horizontal_rect;
        av1_copy(neighbors_ref_counts, other.neighbors_ref_counts);
        tile_ctx = other.tile_ctx;
        bd = other.bd;
        av1_copy(qindex, other.qindex);
        av1_copy(lossless, other.lossless);
        current_base_qindex = other.current_base_qindex;
        cur_frame_force_integer_mv = other.cur_frame_force_integer_mv;
        error_info = other.error_info;
        global_motion = other.global_motion;
        delta_lf_from_base = other.delta_lf_from_base;
        av1_copy(delta_lf, other.delta_lf);
        av1_copy(cdef_transmitted, other.cdef_transmitted);
        seg_mask = other.seg_mask;
        cfl.CopyFrom(other.cfl);
        av1_copy(color_index_map_offset, other.color_index_map_offset);
        tmp_conv_dst = other.tmp_conv_dst;
        av1_copy(tmp_obmc_bufs, other.tmp_obmc_bufs);
        tmp_upsample_pred = other.tmp_upsample_pred;
    }
}

internal partial struct BitDepthInfo
{
    public int bit_depth;
    /*! Is the image buffer high bit depth?
     * Low bit depth buffer uses uint8_t.
     * High bit depth buffer uses uint16_t.
     * Equivalent to cm->seq_params->use_highbitdepth
     */
    public int use_highbitdepth_buf;
}

internal delegate void foreach_transformed_block_visitor(int plane, int block,
                                                         int blk_row, int blk_col,
                                                         BLOCK_SIZE plane_bsize,
                                                         TX_SIZE tx_size, object arg);

internal partial struct Av1ColorMapParam
{
    public int rows;
    public int cols;
    public int n_colors;
    public int plane_width;
    public int plane_height;
    public Ptr<byte> color_map;
    public MapCdf map_cdf;
    public ColorCost color_cost;
}

internal static partial class blockd
{
    // <gen:consts>
    internal const int USE_B_QUANT_NO_TRELLIS = 1;
    internal const int MAX_MB_PLANE = 3;
    internal const int MAX_DIFFWTD_MASK_BITS = 1;
    internal const int INTERINTRA_WEDGE_SIGN = 0;
    internal const int DEFAULT_INTER_TX_TYPE = DCT_DCT;
    internal const int MAX_PALETTE_BLOCK_WIDTH = 64;
    internal const int MAX_PALETTE_BLOCK_HEIGHT = 64;
    internal const int INTER_TX_SIZE_BUF_LEN = 16;
    internal const int TXK_TYPE_BUF_LEN = 64;
    internal const int CFL_MAX_BLOCK_SIZE = (BLOCK_32X32);
    internal const int CFL_BUF_LINE = (32);
    internal const int CFL_BUF_LINE_I128 = (CFL_BUF_LINE >> 3);
    internal const int CFL_BUF_LINE_I256 = (CFL_BUF_LINE >> 4);
    internal const int CFL_BUF_SQUARE = (CFL_BUF_LINE * CFL_BUF_LINE);
    internal const int MAX_INTERINTRA_SB_SQUARE = 32 * 32;

    // DIFFWTD_MASK_TYPE
    internal const int DIFFWTD_38 = 0;
    internal const int DIFFWTD_38_INV = 1;
    internal const int DIFFWTD_MASK_TYPES = 2;

    // FRAME_TYPE
    internal const int KEY_FRAME = 0;
    internal const int INTER_FRAME = 1;
    internal const int INTRA_ONLY_FRAME = 2;
    internal const int S_FRAME = 3;
    internal const int FRAME_TYPES = 4;

    // mv_precision
    internal const int MV_PRECISION_Q3 = 0;
    internal const int MV_PRECISION_Q4 = 1;

    // PREDICTION_MODE fimode_to_intradir[FILTER_INTRA_MODES]
    internal static readonly PREDICTION_MODE[] fimode_to_intradir = {
        DC_PRED, V_PRED, H_PRED, D157_PRED, DC_PRED
    };

    // int av1_num_ext_tx_set[EXT_TX_SET_TYPES]
    internal static readonly int[] av1_num_ext_tx_set = {
        1, 2, 5, 7, 12, 16
    };

    // int av1_ext_tx_used[EXT_TX_SET_TYPES][TX_TYPES]
    internal static readonly int[][] av1_ext_tx_used = {
        new int[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        new int[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 },
        new int[] { 1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 },
        new int[] { 1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0 },
        new int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0 },
        new int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }
    };

    // uint16_t av1_derived_intra_tx_used_flag[INTRA_MODES]
    internal static readonly ushort[] av1_derived_intra_tx_used_flag = {
        0x0209, 0x0403, 0x0805, 0x020F, 0x0009, 0x0009, 0x0009, 0x0805, 0x0403, 0x0205, 0x0403,
        0x0805, 0x0209
    };

    // uint16_t av1_reduced_intra_tx_used_flag[INTRA_MODES]
    internal static readonly ushort[] av1_reduced_intra_tx_used_flag = {
        0x080F, 0x040F, 0x080F, 0x020F, 0x080F, 0x040F, 0x080F, 0x080F, 0x040F, 0x080F, 0x040F,
        0x080F, 0x0C0E
    };

    // uint16_t av1_ext_tx_used_flag[EXT_TX_SET_TYPES]
    internal static readonly ushort[] av1_ext_tx_used_flag = {
        0x0001, 0x0201, 0x020F, 0x0E0F, 0x0FFF, 0xFFFF
    };

    // TxSetType av1_ext_tx_set_lookup[2][2][TX_SIZES_ALL]
    internal static readonly TxSetType[][][] av1_ext_tx_set_lookup = {
        new TxSetType[][] {
            new TxSetType[] {
                EXT_TX_SET_DTT4_IDTX_1DDCT, EXT_TX_SET_DTT4_IDTX_1DDCT, EXT_TX_SET_DTT4_IDTX,
                EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY, EXT_TX_SET_DTT4_IDTX_1DDCT,
                EXT_TX_SET_DTT4_IDTX_1DDCT, EXT_TX_SET_DTT4_IDTX_1DDCT, EXT_TX_SET_DTT4_IDTX_1DDCT,
                EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY,
                EXT_TX_SET_DTT4_IDTX_1DDCT, EXT_TX_SET_DTT4_IDTX_1DDCT, EXT_TX_SET_DCTONLY,
                EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY
            },
            new TxSetType[] {
                EXT_TX_SET_ALL16, EXT_TX_SET_ALL16, EXT_TX_SET_DTT9_IDTX_1DDCT,
                EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCTONLY, EXT_TX_SET_ALL16, EXT_TX_SET_ALL16,
                EXT_TX_SET_ALL16, EXT_TX_SET_ALL16, EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCT_IDTX,
                EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY, EXT_TX_SET_ALL16, EXT_TX_SET_ALL16,
                EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY
            }
        },
        new TxSetType[][] {
            new TxSetType[] {
                EXT_TX_SET_DTT4_IDTX, EXT_TX_SET_DTT4_IDTX, EXT_TX_SET_DTT4_IDTX,
                EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY, EXT_TX_SET_DTT4_IDTX, EXT_TX_SET_DTT4_IDTX,
                EXT_TX_SET_DTT4_IDTX, EXT_TX_SET_DTT4_IDTX, EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY,
                EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY, EXT_TX_SET_DTT4_IDTX, EXT_TX_SET_DTT4_IDTX,
                EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY
            },
            new TxSetType[] {
                EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCT_IDTX,
                EXT_TX_SET_DCTONLY, EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCT_IDTX,
                EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCTONLY,
                EXT_TX_SET_DCTONLY, EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCT_IDTX,
                EXT_TX_SET_DCT_IDTX, EXT_TX_SET_DCTONLY, EXT_TX_SET_DCTONLY
            }
        }
    };

    // int ext_tx_set_index[2][EXT_TX_SET_TYPES]
    internal static readonly int[][] ext_tx_set_index = {
        new int[] { 0, -1, 2, 1, -1, -1 },
        new int[] { 0, 3, -1, -1, 2, 1 }
    };

    // uint8_t mode_to_angle_map[INTRA_MODES]
    internal static readonly byte[] mode_to_angle_map = {
        0, 90, 180, 45, 135, 113, 157, 203, 67, 0, 0, 0, 0
    };

    // int max_neighbor_obmc[6]
    internal static readonly int[] max_neighbor_obmc = {
        0, 1, 2, 3, 4, 4
    };

    // PREDICTION_MODE uv2y[]
    private static readonly PREDICTION_MODE[] uv2y = {
        DC_PRED, V_PRED, H_PRED, D45_PRED, D135_PRED, D113_PRED, D157_PRED, D203_PRED, D67_PRED,
        SMOOTH_PRED, SMOOTH_V_PRED, SMOOTH_H_PRED, PAETH_PRED, DC_PRED, INTRA_INVALID,
        INTRA_INVALID
    };

    // TX_TYPE _intra_mode_to_tx_type[INTRA_MODES]
    private static readonly TX_TYPE[] _intra_mode_to_tx_type = {
        DCT_DCT, ADST_DCT, DCT_ADST, DCT_DCT, ADST_ADST, ADST_DCT, DCT_ADST, DCT_ADST, ADST_DCT,
        ADST_ADST, ADST_DCT, DCT_ADST, ADST_ADST
    };

    // uint8_t tw_w_log2_table[BLOCK_SIZES_ALL]
    private static readonly byte[] tw_w_log2_table = {
        0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 3, 3, 3, 0, 1, 1, 2, 2, 3
    };

    // uint8_t tw_h_log2_table[BLOCK_SIZES_ALL]
    private static readonly byte[] tw_h_log2_table = {
        0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 3, 3, 3, 1, 0, 2, 1, 3, 2
    };

    // uint8_t stride_log2_table[BLOCK_SIZES_ALL]
    private static readonly byte[] stride_log2_table = {
        0, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 1, 2, 2, 0, 1, 0, 1, 0, 1
    };

    // uint8_t bsize_to_max_depth_table[BLOCK_SIZES_ALL]
    private static readonly byte[] bsize_to_max_depth_table = {
        0, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2
    };

    // uint8_t bsize_to_tx_size_depth_table[BLOCK_SIZES_ALL]
    private static readonly byte[] bsize_to_tx_size_depth_table = {
        0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 4, 4, 4, 2, 2, 3, 3, 4, 4
    };

    // TX_SIZE adjusted_tx_size[TX_SIZES_ALL]
    private static readonly TX_SIZE[] adjusted_tx_size = {
        TX_4X4, TX_8X8, TX_16X16, TX_32X32, TX_32X32, TX_4X8, TX_8X4, TX_8X16, TX_16X8, TX_16X32,
        TX_32X16, TX_32X32, TX_32X32, TX_4X16, TX_16X4, TX_8X32, TX_32X8, TX_16X32, TX_32X16
    };

    // MV_REFERENCE_FRAME lut[]
    private static readonly MV_REFERENCE_FRAME[] comp_ref0_lut = {
        LAST_FRAME, LAST_FRAME, LAST_FRAME, BWDREF_FRAME, LAST2_FRAME, LAST2_FRAME, LAST3_FRAME,
        BWDREF_FRAME, ALTREF2_FRAME
    };

    // MV_REFERENCE_FRAME lut[]
    private static readonly MV_REFERENCE_FRAME[] comp_ref1_lut = {
        LAST2_FRAME, LAST3_FRAME, GOLDEN_FRAME, ALTREF_FRAME, LAST3_FRAME, GOLDEN_FRAME,
        GOLDEN_FRAME, ALTREF2_FRAME, ALTREF_FRAME
    };
    // </gen:consts>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_comp_ref_allowed(BLOCK_SIZE bsize)
    {
        return B(AOMMIN(block_size_wide[bsize], block_size_high[bsize]) >= 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_inter_mode(PREDICTION_MODE mode)
    {
        return B(mode >= INTER_MODE_START && mode < INTER_MODE_END);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_inter_singleref_mode(PREDICTION_MODE mode)
    {
        return B(mode >= SINGLE_INTER_MODE_START && mode < SINGLE_INTER_MODE_END);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_inter_compound_mode(PREDICTION_MODE mode)
    {
        return B(mode >= COMP_INTER_MODE_START && mode < COMP_INTER_MODE_END);
    }

    // [subset] compound_ref0_mode / compound_ref1_mode: compound prediction is disabled.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int have_nearmv_in_inter_mode(PREDICTION_MODE mode)
    {
        return B(mode == NEARMV || mode == NEAR_NEARMV || mode == NEAR_NEWMV ||
                 mode == NEW_NEARMV);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int have_newmv_in_inter_mode(PREDICTION_MODE mode)
    {
        return B(mode == NEWMV || mode == NEW_NEWMV || mode == NEAREST_NEWMV ||
                 mode == NEW_NEARESTMV || mode == NEAR_NEWMV || mode == NEW_NEARMV);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_masked_compound_type(COMPOUND_TYPE type)
    {
        return B(type == COMPOUND_WEDGE || type == COMPOUND_DIFFWTD);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_intrabc_block(MB_MODE_INFO mbmi)
    {
        return mbmi.use_intrabc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PREDICTION_MODE get_uv_mode(UV_PREDICTION_MODE mode)
    {
        Debug.Assert(mode < UV_INTRA_MODES);
        return uv2y[mode];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_inter_block(MB_MODE_INFO mbmi)
    {
        return B(is_intrabc_block(mbmi) != 0 || mbmi.ref_frame[0] > INTRA_FRAME);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int has_second_ref(MB_MODE_INFO mbmi)
    {
        return B(mbmi.ref_frame[1] > INTRA_FRAME);
    }

    // [port] Only reached from the comp-pred branches of av1_get_comp_reference_type_context
    // (pred_common.c), which depend on the neighbours' data; ported so that function is complete.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int has_uni_comp_refs(MB_MODE_INFO mbmi)
    {
        return B(has_second_ref(mbmi) != 0 &&
                 !((mbmi.ref_frame[0] >= BWDREF_FRAME) ^
                   (mbmi.ref_frame[1] >= BWDREF_FRAME)));
    }

    // [port] comp_ref0 / comp_ref1 never execute (compound prediction is disabled); ported
    // because get_uni_comp_ref_idx (mvref_common.h) calls them.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MV_REFERENCE_FRAME comp_ref0(int ref_idx)
    {
        Debug.Assert(comp_ref0_lut.Length == TOTAL_UNIDIR_COMP_REFS);
        return comp_ref0_lut[ref_idx];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MV_REFERENCE_FRAME comp_ref1(int ref_idx)
    {
        Debug.Assert(comp_ref1_lut.Length == TOTAL_UNIDIR_COMP_REFS);
        return comp_ref1_lut[ref_idx];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_global_mv_block(MB_MODE_INFO mbmi,
                                           TransformationType type)
    {
        PREDICTION_MODE mode = mbmi.mode;
        BLOCK_SIZE bsize = mbmi.bsize;
        int block_size_allowed =
            B(AOMMIN(block_size_wide[bsize], block_size_high[bsize]) >= 8);
        return B((mode == GLOBALMV || mode == GLOBAL_GLOBALMV) && type > TRANSLATION &&
                 block_size_allowed != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int BLOCK_OFFSET(int i) => i << 4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_cur_buf_hbd(MACROBLOCKD xd)
    {
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_sqr_bsize_idx(BLOCK_SIZE bsize)
    {
        switch (bsize)
        {
            case BLOCK_4X4: return 0;
            case BLOCK_8X8: return 1;
            case BLOCK_16X16: return 2;
            case BLOCK_32X32: return 3;
            case BLOCK_64X64: return 4;
            case BLOCK_128X128: return 5;
            default: return SQR_BLOCK_SIZES;
        }
    }

    // For a square block size 'bsize', returns the size of the sub-blocks used by
    // the given partition type. If the partition produces sub-blocks of different
    // sizes, then the function returns the largest sub-block size.
    // Implements the Partition_Subsize lookup table in the spec (Section 9.3.
    // Conversion tables).
    // Note: the input block size should be square.
    // Otherwise it's considered invalid.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static BLOCK_SIZE get_partition_subsize(BLOCK_SIZE bsize,
                                                     PARTITION_TYPE partition)
    {
        if (partition == PARTITION_INVALID)
        {
            return BLOCK_INVALID;
        }
        else
        {
            int sqr_bsize_idx = get_sqr_bsize_idx(bsize);
            return sqr_bsize_idx >= SQR_BLOCK_SIZES
                       ? BLOCK_INVALID
                       : subsize_lookup[partition][sqr_bsize_idx];
        }
    }

    // [port] C `static` (not inline) in the header, so every includer can call it.
    internal static TX_TYPE intra_mode_to_tx_type(MB_MODE_INFO mbmi,
                                                  PLANE_TYPE plane_type)
    {
        PREDICTION_MODE mode =
            (plane_type == PLANE_TYPE_Y) ? mbmi.mode : get_uv_mode(mbmi.uv_mode);
        Debug.Assert(mode < INTRA_MODES);
        return _intra_mode_to_tx_type[mode];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int block_signals_txsize(BLOCK_SIZE bsize)
    {
        return B(bsize > BLOCK_4X4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TxSetType av1_get_ext_tx_set_type(TX_SIZE tx_size, int is_inter,
                                                      int use_reduced_set)
    {
        Debug.Assert(tx_size < TX_SIZES_ALL);
        return av1_ext_tx_set_lookup[B(use_reduced_set != 0)][B(is_inter != 0)][tx_size];
    }

    // Maps tx set types to the indices.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_ext_tx_set(TX_SIZE tx_size, int is_inter,
                                       int use_reduced_set)
    {
        TxSetType set_type =
            av1_get_ext_tx_set_type(tx_size, is_inter, use_reduced_set);
        return ext_tx_set_index[is_inter][set_type];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_ext_tx_types(TX_SIZE tx_size, int is_inter,
                                         int use_reduced_set)
    {
        int set_type =
            av1_get_ext_tx_set_type(tx_size, is_inter, use_reduced_set);
        return av1_num_ext_tx_set[set_type];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TX_SIZE TXSIZEMAX(TX_SIZE t1, TX_SIZE t2) =>
        tx_size_2d[(t1)] >= tx_size_2d[(t2)] ? (t1) : (t2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TX_SIZE TXSIZEMIN(TX_SIZE t1, TX_SIZE t2) =>
        tx_size_2d[(t1)] <= tx_size_2d[(t2)] ? (t1) : (t2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TX_SIZE tx_size_from_tx_mode(BLOCK_SIZE bsize, TX_MODE tx_mode)
    {
        TX_SIZE largest_tx_size = tx_mode_to_biggest_tx_size[tx_mode];
        TX_SIZE max_rect_tx_size = max_txsize_rect_lookup[bsize];
        if (bsize == BLOCK_4X4)
            return AOMMIN(max_txsize_lookup[bsize], largest_tx_size);
        if (txsize_sqr_map[max_rect_tx_size] <= largest_tx_size)
            return max_rect_tx_size;
        else
            return largest_tx_size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static BLOCK_SIZE get_plane_block_size(BLOCK_SIZE bsize,
                                                    int subsampling_x,
                                                    int subsampling_y)
    {
        Debug.Assert(bsize < BLOCK_SIZES_ALL);
        Debug.Assert(subsampling_x >= 0 && subsampling_x < 2);
        Debug.Assert(subsampling_y >= 0 && subsampling_y < 2);
        return av1_ss_size_lookup[bsize][subsampling_x][subsampling_y];
    }

    /*
     * Logic to generate the lookup tables:
     *
     * TX_SIZE txs = max_txsize_rect_lookup[bsize];
     * for (int level = 0; level < MAX_VARTX_DEPTH - 1; ++level)
     *   txs = sub_tx_size_map[txs];
     * const int tx_w_log2 = tx_size_wide_log2[txs] - MI_SIZE_LOG2;
     * const int tx_h_log2 = tx_size_high_log2[txs] - MI_SIZE_LOG2;
     * const int bw_uint_log2 = mi_size_wide_log2[bsize];
     * const int stride_log2 = bw_uint_log2 - tx_w_log2;
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_get_txb_size_index(BLOCK_SIZE bsize, int blk_row,
                                               int blk_col)
    {
        int index =
            ((blk_row >> tw_h_log2_table[bsize]) << stride_log2_table[bsize]) +
            (blk_col >> tw_w_log2_table[bsize]);
        Debug.Assert(index < INTER_TX_SIZE_BUF_LEN);
        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void update_txk_array(MACROBLOCKD xd, int blk_row,
                                          int blk_col, TX_SIZE tx_size,
                                          TX_TYPE tx_type)
    {
        int stride = xd.tx_type_map_stride;
        xd.tx_type_map[blk_row * stride + blk_col] = (byte)tx_type;
        int txw = tx_size_wide_unit[tx_size];
        int txh = tx_size_high_unit[tx_size];
        // The 16x16 unit is due to the constraint from tx_64x64 which sets the
        // maximum tx size for chroma as 32x32. Coupled with 4x1 transform block
        // size, the constraint takes effect in 32x16 / 16x32 size too. To solve
        // the intricacy, cover all the 16x16 units inside a 64 level transform.
        if (txw == tx_size_wide_unit[TX_64X64] ||
            txh == tx_size_high_unit[TX_64X64])
        {
            int tx_unit = tx_size_wide_unit[TX_16X16];
            for (int idy = 0; idy < txh; idy += tx_unit)
            {
                for (int idx = 0; idx < txw; idx += tx_unit)
                {
                    xd.tx_type_map[(blk_row + idy) * stride + blk_col + idx] = (byte)tx_type;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TX_TYPE av1_get_tx_type(MACROBLOCKD xd,
                                            PLANE_TYPE plane_type, int blk_row,
                                            int blk_col, TX_SIZE tx_size,
                                            int reduced_tx_set)
    {
        MB_MODE_INFO mbmi = xd.mi[0];
        if (xd.lossless[mbmi.segment_id] != 0 || txsize_sqr_up_map[tx_size] > TX_32X32)
        {
            return DCT_DCT;
        }

        TX_TYPE tx_type;
        if (plane_type == PLANE_TYPE_Y)
        {
            tx_type = xd.tx_type_map[blk_row * xd.tx_type_map_stride + blk_col];
        }
        else
        {
            if (is_inter_block(mbmi) != 0)
            {
                // scale back to y plane's coordinate
                macroblockd_plane pd = xd.plane[plane_type];
                blk_row <<= pd.subsampling_y;
                blk_col <<= pd.subsampling_x;
                tx_type = xd.tx_type_map[blk_row * xd.tx_type_map_stride + blk_col];
            }
            else
            {
                // In intra mode, uv planes don't share the same prediction mode as y
                // plane, so the tx_type should not be shared
                tx_type = intra_mode_to_tx_type(mbmi, PLANE_TYPE_UV);
            }
            TxSetType tx_set_type =
                av1_get_ext_tx_set_type(tx_size, is_inter_block(mbmi), reduced_tx_set);
            if (av1_ext_tx_used[tx_set_type][tx_type] == 0) tx_type = DCT_DCT;
        }
        Debug.Assert(tx_type < TX_TYPES);
        Debug.Assert(av1_ext_tx_used[av1_get_ext_tx_set_type(tx_size, is_inter_block(mbmi),
                                                             reduced_tx_set)][tx_type] != 0);
        return tx_type;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int bsize_to_max_depth(BLOCK_SIZE bsize)
    {
        return bsize_to_max_depth_table[bsize];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int bsize_to_tx_size_cat(BLOCK_SIZE bsize)
    {
        Debug.Assert(bsize < BLOCK_SIZES_ALL);
        int depth = bsize_to_tx_size_depth_table[bsize];
        Debug.Assert(depth <= MAX_TX_CATS);
        return depth - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TX_SIZE av1_get_adjusted_tx_size(TX_SIZE tx_size)
    {
        Debug.Assert(tx_size < TX_SIZES_ALL);
        return adjusted_tx_size[tx_size];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TX_SIZE av1_get_max_uv_txsize(BLOCK_SIZE bsize, int subsampling_x,
                                                  int subsampling_y)
    {
        BLOCK_SIZE plane_bsize =
            get_plane_block_size(bsize, subsampling_x, subsampling_y);
        Debug.Assert(plane_bsize < BLOCK_SIZES_ALL);
        TX_SIZE uv_tx = max_txsize_rect_lookup[plane_bsize];
        return av1_get_adjusted_tx_size(uv_tx);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TX_SIZE av1_get_tx_size(int plane, MACROBLOCKD xd)
    {
        MB_MODE_INFO mbmi = xd.mi[0];
        if (xd.lossless[mbmi.segment_id] != 0) return TX_4X4;
        if (plane == 0) return mbmi.tx_size;
        macroblockd_plane pd = xd.plane[plane];
        return av1_get_max_uv_txsize(mbmi.bsize, pd.subsampling_x,
                                     pd.subsampling_y);
    }

    // [port] Never executed on its own (inter-intra is disabled); reached only through
    // is_interintra_pred's short-circuit.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_interintra_allowed_bsize(BLOCK_SIZE bsize)
    {
        return B((bsize >= BLOCK_8X8) && (bsize <= BLOCK_32X32));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_interintra_allowed_mode(PREDICTION_MODE mode)
    {
        return B((mode >= SINGLE_INTER_MODE_START) && (mode < SINGLE_INTER_MODE_END));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_interintra_allowed_ref(Ptr<MV_REFERENCE_FRAME> rf)
    {
        return B((rf[0] > INTRA_FRAME) && (rf[1] <= INTRA_FRAME));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_interintra_allowed(MB_MODE_INFO mbmi)
    {
        return B(is_interintra_allowed_bsize(mbmi.bsize) != 0 &&
                 is_interintra_allowed_mode(mbmi.mode) != 0 &&
                 is_interintra_allowed_ref(mbmi.ref_frame) != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_interintra_pred(MB_MODE_INFO mbmi)
    {
        return B(mbmi.ref_frame[0] > INTRA_FRAME &&
                 mbmi.ref_frame[1] == INTRA_FRAME && is_interintra_allowed(mbmi) != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_vartx_max_txsize(MACROBLOCKD xd, BLOCK_SIZE bsize,
                                             int plane)
    {
        if (xd.lossless[xd.mi[0].segment_id] != 0) return TX_4X4;
        TX_SIZE max_txsize = max_txsize_rect_lookup[bsize];
        if (plane == 0) return max_txsize;            // luma
        return av1_get_adjusted_tx_size(max_txsize);  // chroma
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_allow_palette(int allow_screen_content_tools,
                                          BLOCK_SIZE sb_type)
    {
        Debug.Assert(sb_type < BLOCK_SIZES_ALL);
        return B(allow_screen_content_tools != 0 &&
                 block_size_wide[sb_type] <= MAX_PALETTE_BLOCK_WIDTH &&
                 block_size_high[sb_type] <= MAX_PALETTE_BLOCK_HEIGHT &&
                 sb_type >= BLOCK_8X8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_nontrans_global_motion(MACROBLOCKD xd,
                                                  MB_MODE_INFO mbmi)
    {
        int @ref;
        if (mbmi.mode != GLOBALMV && mbmi.mode != GLOBAL_GLOBALMV) return 0;

        if (AOMMIN(mi_size_wide[mbmi.bsize], mi_size_high[mbmi.bsize]) < 2)
            return 0;

        // Now check if all global motion is non translational
        for (@ref = 0; @ref < 1 + has_second_ref(mbmi); ++@ref)
        {
            if (xd.global_motion[mbmi.ref_frame[@ref]].wmtype == TRANSLATION) return 0;
        }
        return 1;
    }

    // [port] Never executed on its own; ported because encoder.h's
    // warped_motion_update_num_proj_ref calls it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_motion_variation_allowed_bsize(BLOCK_SIZE bsize)
    {
        Debug.Assert(bsize < BLOCK_SIZES_ALL);
        return B(AOMMIN(block_size_wide[bsize], block_size_high[bsize]) >= 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PLANE_TYPE get_plane_type(int plane)
    {
        return (plane == 0) ? PLANE_TYPE_Y : PLANE_TYPE_UV;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_get_max_eob(TX_SIZE tx_size)
    {
        if (tx_size == TX_64X64 || tx_size == TX_64X32 || tx_size == TX_32X64)
        {
            return 1024;
        }
        if (tx_size == TX_16X64 || tx_size == TX_64X16)
        {
            return 512;
        }
        return tx_size_2d[tx_size];
    }
}
