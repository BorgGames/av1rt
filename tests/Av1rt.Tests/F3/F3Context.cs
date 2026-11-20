using System;
using Av1rt.Aom;
using static Av1rt.Aom.aomcx;
using static Av1rt.Aom.av1_common_int;
using static Av1rt.Aom.block;
using static Av1rt.Aom.blockd;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.enc_enums;
using static Av1rt.Aom.encoder;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.mv;
using static Av1rt.Aom.sad_h;
using static Av1rt.Aom.scale;
using static Av1rt.Aom.seg_common;
using static Av1rt.Aom.speed_features;
using static Av1rt.Aom.var_based_part;

namespace Av1rt.Tests.F3;

// Indices of the parameters passed to the shim (same order as the enum in shim_f3.c).
internal static class P
{
    public const int WIDTH = 0, HEIGHT = 1, SB128 = 2, FRAME_TYPE = 3, BASE_QINDEX = 4, QINDEX = 5,
        LOWSUMDIFF = 6, SPEED = 7, MODE = 8, CONTENT = 9, SPLIT_SHIFT = 10, FORCE_LARGE_INTRA = 11,
        PREFER_LARGE = 12, VAR_PART_QIDX = 13, DISABLE_8X8_QIDX = 14, NOISE_ENABLED = 15, NOISE_VALUE = 16,
        NOISE_THRESH = 17, FRAME_NUMBER = 18, NON_REF_FRAME = 19, FRAME_SOURCE_SAD = 20,
        HIGH_MOTION_SCREEN = 21, PARTITION_SEARCH_TYPE = 22, TEMPORAL_LAYER_ID = 23,
        NUM_TEMPORAL_LAYERS = 24, AVG_SOURCE_SAD = 25, USE_RTC_TF = 26, EST_MOTION = 27,
        NONRD_PRUNE_REF = 28, SHORT_CIRCUIT = 29, PART_EARLY_EXIT_ZEROMV = 30, ZEROMV_SKIP_SRC_SAD = 31,
        INCREASE_SRC_SAD_THRESH = 32, FRAMES_SINCE_KEY = 33, HIGH_SOURCE_SAD = 34, PERCENT_MOTION = 35,
        REF_FRAME_FLAGS = 36, AQ_MODE = 37, SEG_ENABLED = 38, SEG_DELTA_Q1 = 39, SEG_DELTA_Q2 = 40,
        MI_ALLOC_BSIZE = 41, USE_SRC_SAD_BLK = 42, ROI_DELTA_QP = 43, COUNT = 44;

    // Speed 11 real-time defaults for a non-screen 64x64-superblock inter frame.
    public static int[] Default(int width, int height)
    {
        var p = new int[COUNT];
        p[WIDTH] = width;
        p[HEIGHT] = height;
        p[FRAME_TYPE] = INTER_FRAME;
        p[BASE_QINDEX] = 120;
        p[QINDEX] = 120;
        p[SPEED] = 11;
        p[MODE] = REALTIME;
        p[CONTENT] = AOM_CONTENT_DEFAULT;
        p[SPLIT_SHIFT] = 8;
        p[FORCE_LARGE_INTRA] = 1;
        p[PREFER_LARGE] = 3;
        p[VAR_PART_QIDX] = 4;
        p[FRAME_NUMBER] = 10;
        p[PARTITION_SEARCH_TYPE] = VAR_BASED_PARTITION;
        p[NUM_TEMPORAL_LAYERS] = 1;
        p[USE_RTC_TF] = 1;
        p[NONRD_PRUNE_REF] = 2;
        p[SHORT_CIRCUIT] = 1;
        p[PART_EARLY_EXIT_ZEROMV] = 2;
        p[ZEROMV_SKIP_SRC_SAD] = 2;
        p[INCREASE_SRC_SAD_THRESH] = 1;
        p[FRAMES_SINCE_KEY] = 10;
        p[REF_FRAME_FLAGS] = AOM_LAST_FLAG;
        p[AQ_MODE] = CYCLIC_REFRESH_AQ;
        p[MI_ALLOC_BSIZE] = BLOCK_8X8;
        return p;
    }

    public static string Describe(int[] p) => string.Join(",", p);
}

// Frame buffer layout shared with shim_f3.c (Layout / layout()).
internal readonly struct Layout
{
    public const int Border = 160, UvBorder = Border >> 1;
    public readonly int AlignedW, AlignedH, YStride, UvStride, YSize, UvSize, FrameSize, YOrigin, UvOrigin;

    public Layout(int width, int height)
    {
        AlignedW = (width + 7) & ~7;
        AlignedH = (height + 7) & ~7;
        YStride = (AlignedW + 2 * Border + 31) & ~31;
        UvStride = YStride >> 1;
        YSize = YStride * (AlignedH + 2 * Border);
        UvSize = UvStride * ((AlignedH >> 1) + 2 * UvBorder);
        FrameSize = YSize + 2 * UvSize;
        YOrigin = Border * YStride + Border;
        UvOrigin = UvBorder * UvStride + UvBorder;
    }

    public void SetupBuf(YV12_BUFFER_CONFIG b, byte[] frames, int frame, int width, int height)
    {
        int f = frame * FrameSize;
        b.y_buffer = new Ptr<byte>(frames, f + YOrigin);
        b.u_buffer = new Ptr<byte>(frames, f + YSize + UvOrigin);
        b.v_buffer = new Ptr<byte>(frames, f + YSize + UvSize + UvOrigin);
        b.y_stride = YStride;
        b.uv_stride = UvStride;
        b.y_width = AlignedW;
        b.y_height = AlignedH;
        b.uv_width = AlignedW >> 1;
        b.uv_height = AlignedH >> 1;
        b.y_crop_width = width;
        b.y_crop_height = height;
        b.uv_crop_width = (width + 1) >> 1;
        b.uv_crop_height = (height + 1) >> 1;
        b.border = Border;
        b.subsampling_x = 1;
        b.subsampling_y = 1;
    }
}

// C# mirror of shim_f3.c.
internal static class F3Context
{
    public const int SbInfoCount = 5, SbOutHeader = 14, SbOutCount = SbOutHeader + 105, VarInts = 4, Vp64Vars = 2729;

    public static AV1_COMP Create(int[] p)
    {
        var cpi = new AV1_COMP();
        cpi.ppi = new AV1_PRIMARY();
        AV1_COMMON cm = cpi.common;
        cm.seq_params = cpi.ppi.seq_params;
        SequenceHeader seq = cm.seq_params;
        seq.bit_depth = 8;
        seq.subsampling_x = 1;
        seq.subsampling_y = 1;
        seq.sb_size = p[P.SB128] != 0 ? BLOCK_128X128 : BLOCK_64X64;
        seq.mib_size = mi_size_wide[seq.sb_size];
        seq.mib_size_log2 = mi_size_wide_log2[seq.sb_size];

        cm.width = p[P.WIDTH];
        cm.height = p[P.HEIGHT];
        cm.current_frame.frame_type = p[P.FRAME_TYPE];
        cm.current_frame.frame_number = (uint)p[P.FRAME_NUMBER];
        cm.quant_params.base_qindex = p[P.BASE_QINDEX];

        cpi.sf.part_sf.partition_search_type = p[P.PARTITION_SEARCH_TYPE];
        cpi.sf.part_sf.disable_8x8_part_based_on_qidx = p[P.DISABLE_8X8_QIDX];
        var rt = cpi.sf.rt_sf;
        rt.var_part_split_threshold_shift = p[P.SPLIT_SHIFT];
        rt.force_large_partition_blocks_intra = p[P.FORCE_LARGE_INTRA];
        rt.prefer_large_partition_blocks = p[P.PREFER_LARGE];
        rt.var_part_based_on_qidx = p[P.VAR_PART_QIDX];
        rt.use_rtc_tf = p[P.USE_RTC_TF];
        rt.estimate_motion_for_var_based_partition = p[P.EST_MOTION];
        rt.nonrd_prune_ref_frame_search = p[P.NONRD_PRUNE_REF];
        rt.short_circuit_low_temp_var = p[P.SHORT_CIRCUIT];
        rt.part_early_exit_zeromv = p[P.PART_EARLY_EXIT_ZEROMV];
        rt.set_zeromv_skip_based_on_source_sad = p[P.ZEROMV_SKIP_SRC_SAD];
        rt.increase_source_sad_thresh = p[P.INCREASE_SRC_SAD_THRESH];

        cpi.oxcf.mode = p[P.MODE];
        cpi.oxcf.speed = p[P.SPEED];
        cpi.oxcf.tune_cfg.content = p[P.CONTENT];
        cpi.oxcf.q_cfg.aq_mode = p[P.AQ_MODE];

        cpi.noise_estimate.enabled = p[P.NOISE_ENABLED];
        cpi.noise_estimate.value = p[P.NOISE_VALUE];
        cpi.noise_estimate.thresh = p[P.NOISE_THRESH];

        cpi.ppi.rtc_ref.non_reference_frame = p[P.NON_REF_FRAME];
        cpi.rc.frame_source_sad = (ulong)p[P.FRAME_SOURCE_SAD];
        cpi.rc.avg_source_sad = (ulong)p[P.AVG_SOURCE_SAD];
        cpi.rc.high_motion_content_screen_rtc = p[P.HIGH_MOTION_SCREEN];
        cpi.rc.frames_since_key = p[P.FRAMES_SINCE_KEY];
        cpi.rc.high_source_sad = p[P.HIGH_SOURCE_SAD];
        cpi.rc.percent_blocks_with_motion = p[P.PERCENT_MOTION];

        cpi.svc.number_spatial_layers = 1;
        cpi.svc.number_temporal_layers = p[P.NUM_TEMPORAL_LAYERS];
        cpi.svc.temporal_layer_id = p[P.TEMPORAL_LAYER_ID];

        cpi.ref_frame_flags = p[P.REF_FRAME_FLAGS];
        cpi.roi.delta_qp_enabled = p[P.ROI_DELTA_QP];

        if (p[P.SEG_ENABLED] != 0)
        {
            segmentation seg = cm.seg;
            seg.enabled = 1;
            av1_clearall_segfeatures(seg);
            av1_enable_segfeature(seg, 1, SEG_LVL_ALT_Q);
            av1_set_segdata(seg, 1, SEG_LVL_ALT_Q, p[P.SEG_DELTA_Q1]);
            av1_enable_segfeature(seg, 2, SEG_LVL_ALT_Q);
            av1_set_segdata(seg, 2, SEG_LVL_ALT_Q, p[P.SEG_DELTA_Q2]);
        }
        return cpi;
    }

    private static void SetSadFns(AV1_PRIMARY ppi)
    {
        ppi.fn_ptr[BLOCK_4X4].sdf = aom_sad4x4;
        ppi.fn_ptr[BLOCK_4X8].sdf = aom_sad4x8;
        ppi.fn_ptr[BLOCK_8X4].sdf = aom_sad8x4;
        ppi.fn_ptr[BLOCK_8X8].sdf = aom_sad8x8;
        ppi.fn_ptr[BLOCK_8X16].sdf = aom_sad8x16;
        ppi.fn_ptr[BLOCK_16X8].sdf = aom_sad16x8;
        ppi.fn_ptr[BLOCK_16X16].sdf = aom_sad16x16;
        ppi.fn_ptr[BLOCK_16X32].sdf = aom_sad16x32;
        ppi.fn_ptr[BLOCK_32X16].sdf = aom_sad32x16;
        ppi.fn_ptr[BLOCK_32X32].sdf = aom_sad32x32;
        ppi.fn_ptr[BLOCK_32X64].sdf = aom_sad32x64;
        ppi.fn_ptr[BLOCK_64X32].sdf = aom_sad64x32;
        ppi.fn_ptr[BLOCK_64X64].sdf = aom_sad64x64;
        ppi.fn_ptr[BLOCK_64X128].sdf = aom_sad64x128;
        ppi.fn_ptr[BLOCK_128X64].sdf = aom_sad128x64;
        ppi.fn_ptr[BLOCK_128X128].sdf = aom_sad128x128;
    }

    public sealed class VbpResult
    {
        public long[] Thresholds = new long[6];
        public int[] SbOut, VtOut, GridOut;
        public int NumSb;
    }

    public static (int miRows, int miCols, int numSb, int num64) Dims(int[] p)
    {
        var l = new Layout(p[P.WIDTH], p[P.HEIGHT]);
        int miRows = l.AlignedH >> MI_SIZE_LOG2, miCols = l.AlignedW >> MI_SIZE_LOG2;
        int sbMi = p[P.SB128] != 0 ? 32 : 16;
        int numSb = ((miRows + sbMi - 1) / sbMi) * ((miCols + sbMi - 1) / sbMi);
        return (miRows, miCols, numSb, p[P.SB128] != 0 ? 4 : 1);
    }

    public static VbpResult AllocResult(int[] p)
    {
        var (miRows, miCols, numSb, num64) = Dims(p);
        return new VbpResult
        {
            SbOut = new int[SbOutCount * numSb],
            VtOut = new int[numSb * num64 * Vp64Vars * VarInts],
            GridOut = new int[miRows * miCols],
        };
    }

    public static VbpResult RunC(int[] p, byte[] frames, uint[] threshExitPart, ulong[] srcSadBlk, int[] sbInfo)
    {
        VbpResult r = AllocResult(p);
        r.NumSb = F3Shim.aomshim_f3_choose_vbp(p, frames, threshExitPart, srcSadBlk, sbInfo, r.Thresholds, r.SbOut,
                                               r.VtOut, r.GridOut);
        return r;
    }

    // C# mirror of aomshim_f3_choose_vbp().
    public static VbpResult RunPort(int[] p, byte[] frames, uint[] threshExitPart, ulong[] srcSadBlk, int[] sbInfo)
    {
        VbpResult r = AllocResult(p);
        AV1_COMP cpi = Create(p);
        AV1_COMMON cm = cpi.common;
        CommonModeInfoParams mi_params = cm.mi_params;
        SequenceHeader seq = cm.seq_params;
        int width = p[P.WIDTH], height = p[P.HEIGHT];
        var l = new Layout(width, height);

        SetSadFns(cpi.ppi);
        for (int i = 0; i < BLOCK_SIZES_ALL; ++i) cpi.zeromv_skip_thresh_exit_part[i] = threshExitPart[i];
        cpi.src_sad_blk_64x64 = p[P.USE_SRC_SAD_BLK] != 0 ? srcSadBlk : Ptr<ulong>.Null;

        mi_params.mi_cols = l.AlignedW >> MI_SIZE_LOG2;
        mi_params.mi_rows = l.AlignedH >> MI_SIZE_LOG2;
        mi_params.mi_stride = calc_mi_size(mi_params.mi_cols);
        mi_params.mi_alloc_bsize = p[P.MI_ALLOC_BSIZE];
        int alloc1d = mi_size_wide[mi_params.mi_alloc_bsize];
        mi_params.mi_alloc_stride = (mi_params.mi_stride + alloc1d - 1) / alloc1d;
        int alignedMiRows = calc_mi_size(mi_params.mi_rows);
        mi_params.mi_alloc_size = mi_params.mi_alloc_stride * ((alignedMiRows + alloc1d - 1) / alloc1d);
        mi_params.mi_alloc = new MB_MODE_INFO[mi_params.mi_alloc_size];
        for (int i = 0; i < mi_params.mi_alloc.Length; ++i) mi_params.mi_alloc[i] = new MB_MODE_INFO();
        mi_params.mi_grid_size = mi_params.mi_stride * alignedMiRows;
        mi_params.mi_grid_base = new MB_MODE_INFO[mi_params.mi_grid_size];

        var bufs = new[] { new RefCntBuffer(), new RefCntBuffer() };
        l.SetupBuf(bufs[0].buf, frames, 1, width, height);
        l.SetupBuf(bufs[1].buf, frames, 2, width, height);
        for (int i = 0; i < REF_FRAMES; ++i) cm.remapped_ref_idx[i] = 0;
        cm.remapped_ref_idx[GOLDEN_FRAME - LAST_FRAME] = 1;
        cm.ref_frame_map[0] = bufs[0];
        cm.ref_frame_map[1] = bufs[1];
        for (int i = 0; i < 2; ++i)
            av1_setup_scale_factors_for_frame(cm.ref_scale_factors[i], width, height, width, height);
        var src = new YV12_BUFFER_CONFIG();
        l.SetupBuf(src, frames, 0, width, height);

        av1_set_variance_partition_thresholds(cpi, p[P.QINDEX], p[P.LOWSUMDIFF]);
        for (int i = 0; i < 5; ++i) r.Thresholds[i] = cpi.vbp_info.thresholds[i];
        r.Thresholds[5] = cpi.vbp_info.threshold_minmax;

        ThreadData td = cpi.td;
        int num64 = seq.sb_size == BLOCK_64X64 ? 1 : 4;
        td.vt64x64 = alloc_array<VP64x64>(num64);
        td.num_64x64_blocks = num64;
        MACROBLOCK x = td.mb;
        MACROBLOCKD xd = x.e_mbd;
        for (int i = 0; i < MAX_MB_PLANE; ++i)
        {
            xd.plane[i].subsampling_x = i != 0 ? 1 : 0;
            xd.plane[i].subsampling_y = i != 0 ? 1 : 0;
        }
        var tile = new TileInfo { mi_row_end = mi_params.mi_rows, mi_col_end = mi_params.mi_cols };

        int sbMi = seq.mib_size;
        int sb = 0;
        for (int mi_row = 0; mi_row < mi_params.mi_rows; mi_row += sbMi)
        {
            for (int mi_col = 0; mi_col < mi_params.mi_cols; mi_col += sbMi, ++sb)
            {
                int info = SbInfoCount * sb;
                int gridIdx = get_mi_grid_idx(mi_params, mi_row, mi_col);
                xd.mi = new Ptr<MB_MODE_INFO>(mi_params.mi_grid_base, gridIdx);
                MB_MODE_INFO sbMbmi = mi_params.mi_alloc[get_alloc_mi_idx(mi_params, mi_row, mi_col)];
                xd.mi[0] = sbMbmi;
                xd.mi[0].segment_id = (byte)sbInfo[info];
                xd.mi_row = mi_row;
                xd.mi_col = mi_col;
                xd.mb_to_bottom_edge = GET_MV_SUBPEL((mi_params.mi_rows - sbMi - mi_row) * MI_SIZE);
                xd.mb_to_right_edge = GET_MV_SUBPEL((mi_params.mi_cols - sbMi - mi_col) * MI_SIZE);
                xd.up_available = false;
                xd.left_available = false;
                x.plane[0].src.buf = src.y_buffer + mi_row * MI_SIZE * src.y_stride + mi_col * MI_SIZE;
                x.plane[0].src.stride = src.y_stride;
                x.plane[1].src.buf = src.u_buffer + (mi_row * MI_SIZE >> 1) * src.uv_stride + (mi_col * MI_SIZE >> 1);
                x.plane[1].src.stride = src.uv_stride;
                x.plane[2].src.buf = src.v_buffer + (mi_row * MI_SIZE >> 1) * src.uv_stride + (mi_col * MI_SIZE >> 1);
                x.plane[2].src.stride = src.uv_stride;
                x.content_state_sb.source_sad_nonrd = sbInfo[info + 1];
                x.content_state_sb.source_sad_rd = sbInfo[info + 2];
                x.content_state_sb.lighting_change = sbInfo[info + 3];
                x.content_state_sb.low_sumdiff = sbInfo[info + 4];
                x.delta_qindex = 0;
                x.nonrd_prune_ref_frame_search = 0;
                x.sb_me_partition = 0;
                x.force_zeromv_skip_for_sb = 0;
                Array.Clear(x.part_search_info.variance_low);
                Array.Clear(x.color_sensitivity_sb);
                Array.Clear(x.color_sensitivity_sb_g);
                Array.Clear(x.color_sensitivity_sb_alt);

                int ret = av1_choose_var_based_partitioning(cpi, in tile, td, x, mi_row, mi_col);

                int o = SbOutCount * sb;
                r.SbOut[o + 0] = ret;
                r.SbOut[o + 1] = x.force_zeromv_skip_for_sb;
                r.SbOut[o + 2] = x.nonrd_prune_ref_frame_search;
                r.SbOut[o + 3] = x.sb_me_partition;
                r.SbOut[o + 4] = sbMbmi.ref_frame[0];
                r.SbOut[o + 5] = (int)sbMbmi.mv[0].as_int;
                r.SbOut[o + 6] = (int)x.source_variance;
                r.SbOut[o + 7] = x.color_sensitivity_sb[0];
                r.SbOut[o + 8] = x.color_sensitivity_sb[1];
                r.SbOut[o + 9] = x.color_sensitivity_sb_g[0];
                r.SbOut[o + 10] = x.color_sensitivity_sb_g[1];
                r.SbOut[o + 11] = x.color_sensitivity_sb_alt[0];
                r.SbOut[o + 12] = x.color_sensitivity_sb_alt[1];
                r.SbOut[o + 13] = sbMbmi.bsize;
                for (int i = 0; i < 105; ++i) r.SbOut[o + SbOutHeader + i] = x.part_search_info.variance_low[i];
                int v = sb * num64 * Vp64Vars * VarInts;
                for (int i = 0; i < num64; ++i) v = DumpVt64(td.vt64x64[i], r.VtOut, v);
            }
        }
        r.NumSb = sb;

        for (int row = 0; row < mi_params.mi_rows; ++row)
        {
            for (int col = 0; col < mi_params.mi_cols; ++col)
            {
                MB_MODE_INFO mi = mi_params.mi_grid_base[row * mi_params.mi_stride + col];
                r.GridOut[row * mi_params.mi_cols + col] = mi != null ? mi.bsize : -1;
            }
        }
        return r;
    }

    private static int DumpVar(VPartVar v, int[] o, int n)
    {
        o[n] = (int)v.sum_square_error;
        o[n + 1] = v.sum_error;
        o[n + 2] = v.log2_count;
        o[n + 3] = v.variance;
        return n + VarInts;
    }

    private static int DumpPv(VPVariance pv, int[] o, int n)
    {
        n = DumpVar(pv.none, o, n);
        n = DumpVar(pv.horz[0], o, n);
        n = DumpVar(pv.horz[1], o, n);
        n = DumpVar(pv.vert[0], o, n);
        return DumpVar(pv.vert[1], o, n);
    }

    private static int DumpVt64(VP64x64 v64, int[] o, int n)
    {
        n = DumpPv(v64.part_variances, o, n);
        foreach (VP32x32 v32 in v64.split)
        {
            n = DumpPv(v32.part_variances, o, n);
            foreach (VP16x16 v16 in v32.split)
            {
                n = DumpPv(v16.part_variances, o, n);
                foreach (VP8x8 v8 in v16.split)
                {
                    n = DumpPv(v8.part_variances, o, n);
                    foreach (VP4x4 v4 in v8.split)
                    {
                        n = DumpPv(v4.part_variances, o, n);
                        foreach (VPartVar v in v4.split) n = DumpVar(v, o, n);
                    }
                }
            }
        }
        return n;
    }

    // Deterministic content for the three frames (source, LAST, GOLDEN), borders included.
    // Source: a gradient plus, per 8x8 block, texture of an amplitude drawn per 16x16 / 32x32 /
    // 64x64 region (flat to very busy), so the variance tree crosses the thresholds at every
    // level. References: per 64x64 region (one superblock row/col unit), one of
    //   0: LAST identical, GOLDEN noisy      1: LAST noisy, GOLDEN identical (GOLDEN wins)
    //   2: both noisy, LAST less             3: LAST replaced by other content, GOLDEN noisy
    //   4: LAST lightly noisy with busy 16x16 patches (local splits)
    // where "noisy" uses an amplitude of refNoise (0 makes most regions identical).
    public static byte[] MakeFrames(int width, int height, int seed, int refNoise)
    {
        var l = new Layout(width, height);
        var frames = new byte[3 * l.FrameSize];
        var rng = new Random(seed);
        int[] amps = { 0, 0, 1, 3, 6, 12, 25, 50, 90 };
        int Hash(int a, int b, int c) => (int)((uint)((a * 73856093) ^ (b * 19349663) ^ (c * 83492791) ^ seed) % 1000003u);
        int Amp(int lr, int lc) // luma coordinates
        {
            int a64 = Hash(lr >> 6, lc >> 6, 1) % amps.Length;
            int a16 = Hash(lr >> 4, lc >> 4, 2) % 4;
            int a = a16 == 0 ? Hash(lr >> 4, lc >> 4, 3) % amps.Length : a64;
            return amps[a];
        }
        for (int f = 0; f < 3; ++f)
        {
            for (int plane = 0; plane < 3; ++plane)
            {
                int stride = plane == 0 ? l.YStride : l.UvStride;
                int off = f * l.FrameSize + (plane == 0 ? 0 : l.YSize + (plane - 1) * l.UvSize);
                int rows = (plane == 0 ? l.YSize : l.UvSize) / stride;
                int border = plane == 0 ? Layout.Border : Layout.UvBorder;
                int sh = plane == 0 ? 0 : 1;
                for (int r = 0; r < rows; ++r)
                {
                    for (int c = 0; c < stride; ++c)
                    {
                        // luma coordinates relative to the frame origin
                        int lr = (r - border) << sh, lc = (c - border) << sh;
                        int i = off + r * stride + c;
                        if (f == 0)
                        {
                            int amp = Amp(lr, lc);
                            int v = (lr * 3 + lc * 2) / (plane + 3) + 60 + plane * 20;
                            if (amp > 0) v += rng.Next(-amp, amp + 1);
                            frames[i] = (byte)Math.Clamp(v, 0, 255);
                            continue;
                        }
                        byte s = frames[i - f * l.FrameSize];
                        int mode = Hash(lr >> 6, lc >> 6, 7) % 5;
                        bool isLast = f == 1;
                        int noise = mode switch
                        {
                            0 => isLast ? 0 : refNoise + 2,
                            1 => isLast ? refNoise + 2 : 0,
                            2 => isLast ? refNoise : 2 * refNoise + 1,
                            3 => isLast ? -1 : refNoise,
                            _ => isLast ? (Hash(lr >> 4, lc >> 4, 9) % 3 == 0 ? 40 : Math.Min(refNoise, 1)) : refNoise + 3,
                        };
                        int val = noise < 0 ? 255 - s + rng.Next(-20, 21)
                                : noise == 0 ? s
                                : s + rng.Next(-noise, noise + 1);
                        frames[i] = (byte)Math.Clamp(val, 0, 255);
                    }
                }
            }
        }
        return frames;
    }

    // populate_thresh_to_force_zeromv_skip() (encodeframe.c).
    public static uint[] ThreshExitPart()
    {
        var t = new uint[BLOCK_SIZES_ALL];
        int num128 = block_size_wide[BLOCK_128X128] * block_size_high[BLOCK_128X128];
        for (int bsize = BLOCK_4X4; bsize < BLOCK_SIZES_ALL; bsize++)
        {
            int numBlockPix = block_size_wide[bsize] * block_size_high[bsize];
            uint thresh = (uint)(10000u * Math.Sqrt((double)numBlockPix / num128) + 0.5);
            t[bsize] = Math.Min(thresh, (uint)(4 * numBlockPix));
        }
        return t;
    }
}
