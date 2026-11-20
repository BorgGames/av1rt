// Port of libaom av1/encoder/encoder_alloc.h
global using static Av1rt.Aom.encoder_alloc;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class encoder_alloc
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void dealloc_context_buffers_ext(
        MBMIExtFrameBufferInfo mbmi_ext_info)
    {
        mbmi_ext_info.frame_base = null;
        mbmi_ext_info.alloc_size = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void alloc_context_buffers_ext(
        AV1_COMMON cm, MBMIExtFrameBufferInfo mbmi_ext_info)
    {
        CommonModeInfoParams mi_params = cm.mi_params;

        int mi_alloc_size_1d = mi_size_wide[mi_params.mi_alloc_bsize];
        int mi_alloc_rows =
            (mi_params.mi_rows + mi_alloc_size_1d - 1) / mi_alloc_size_1d;
        int mi_alloc_cols =
            (mi_params.mi_cols + mi_alloc_size_1d - 1) / mi_alloc_size_1d;
        int new_ext_mi_size = mi_alloc_rows * mi_alloc_cols;

        if (new_ext_mi_size > mbmi_ext_info.alloc_size)
        {
            dealloc_context_buffers_ext(mbmi_ext_info);
            mbmi_ext_info.frame_base = new MB_MODE_INFO_EXT_FRAME[new_ext_mi_size];
            mbmi_ext_info.alloc_size = new_ext_mi_size;
        }
        // The stride needs to be updated regardless of whether new allocation
        // happened or not.
        mbmi_ext_info.stride = mi_alloc_cols;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void alloc_compressor_data(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        CommonModeInfoParams mi_params = cm.mi_params;

        // Setup mi_params
        mi_params.set_mb_mi(mi_params, cm.width, cm.height,
                            cpi.sf.part_sf.default_min_partition_size);

        if (is_stat_generation_stage(cpi) == 0) av1_alloc_txb_buf(cpi);

        cpi.td.mv_costs_alloc = null;
        // Avoid the memory allocation of 'mv_costs_alloc' for allintra encoding
        // mode.
        if (cpi.oxcf.kf_cfg.key_freq_max != 0)
        {
            cpi.td.mv_costs_alloc = new MvCosts();
            cpi.td.mb.mv_costs = cpi.td.mv_costs_alloc;
        }

        av1_setup_shared_coeff_buffer(cm.seq_params, cpi.td.shared_coeff_buf,
                                      cm.error);
        if (av1_setup_sms_tree(cpi, cpi.td) != 0)
        {
            throw new AomException(AOM_CODEC_MEM_ERROR, "Failed to allocate SMS tree");
        }
        // [subset] firstpass_ctx allocation (av1_alloc_pmc) only for the stat
        // generation stage (first pass) or LAP, both out of the subset.
        Debug.Assert(is_stat_generation_stage(cpi) == 0 && cpi.ppi.lap_enabled == 0,
                     "first pass / LAP is out of the subset");
    }

    // Allocate mbmi buffers which are used to store mode information at block
    // level.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void alloc_mb_mode_info_buffers(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        if (av1_alloc_context_buffers(cm, cm.width, cm.height,
                                      cpi.sf.part_sf.default_min_partition_size) != 0)
        {
            throw new AomException(AOM_CODEC_MEM_ERROR, "Failed to allocate context buffers");
        }

        if (is_stat_generation_stage(cpi) == 0)
            alloc_context_buffers_ext(cm, cpi.mbmi_ext_info);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void realloc_segmentation_maps(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        CommonModeInfoParams mi_params = cm.mi_params;
        int max_mi_cols = mi_params.mi_cols;
        int max_mi_rows = mi_params.mi_rows;
        if (cpi.oxcf.frm_dim_cfg.forced_max_frame_width != 0)
        {
            max_mi_cols = size_in_mi(cpi.oxcf.frm_dim_cfg.forced_max_frame_width);
        }
        if (cpi.oxcf.frm_dim_cfg.forced_max_frame_height != 0)
        {
            max_mi_rows = size_in_mi(cpi.oxcf.frm_dim_cfg.forced_max_frame_height);
        }

        // Create the encoder segmentation map and set all entries to 0
        cpi.enc_seg.map = new byte[max_mi_rows * max_mi_cols];

        // Create a map used for cyclic background refresh.
        if (cpi.cyclic_refresh != null) av1_cyclic_refresh_free(cpi.cyclic_refresh);
        cpi.cyclic_refresh = av1_cyclic_refresh_alloc(max_mi_rows, max_mi_cols);

        // Create a map used to mark inactive areas. The app has to set it again.
        cpi.active_map.map = new byte[max_mi_rows * max_mi_cols];
        cpi.active_map.enabled = 0;
    }

    // Allocates the per-block RD multiplier scaling factors (and, with
    // CONFIG_SALIENCY_MAP, the saliency map they are derived from) for the same
    // maximum frame size as the other frame size dependent buffers. They are
    // written for every block of the current frame, so they must be reallocated
    // whenever that maximum grows.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void alloc_rdmult_scaling_factors(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        CommonModeInfoParams mi_params = cm.mi_params;
        int max_mi_cols = mi_params.mi_cols;
        int max_mi_rows = mi_params.mi_rows;
        if (cpi.oxcf.frm_dim_cfg.forced_max_frame_width != 0)
        {
            max_mi_cols = size_in_mi(cpi.oxcf.frm_dim_cfg.forced_max_frame_width);
        }
        if (cpi.oxcf.frm_dim_cfg.forced_max_frame_height != 0)
        {
            max_mi_rows = size_in_mi(cpi.oxcf.frm_dim_cfg.forced_max_frame_height);
        }
        BLOCK_SIZE bsize = BLOCK_16X16;
        int w = mi_size_wide[bsize];
        int h = mi_size_high[bsize];
        int num_cols = (max_mi_cols + w - 1) / w;
        int num_rows = (max_mi_rows + h - 1) / h;

        cpi.ssim_rdmult_scaling_factors = new double[num_rows * num_cols];
        cpi.tpl_rdmult_scaling_factors = Ptr<double>.Null;
        if (cpi.oxcf.mode != ALLINTRA)
        {
            cpi.tpl_rdmult_scaling_factors = new double[num_rows * num_cols];
        }

        // [subset] CONFIG_TUNE_VMAF / CONFIG_TUNE_BUTTERAUGLI / CONFIG_SALIENCY_MAP
        // buffers: all 0 in the subset.
    }

    // [subset] alloc_obmc_buffers / release_obmc_buffers and
    // alloc_compound_type_rd_buffers / release_compound_type_rd_buffers dropped:
    // p1b2 dropped OBMCBuffer and CompoundTypeRdBuffers (OBMC and masked compound
    // search are off in the subset), so the buffers have no C# home.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void dealloc_compressor_data(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        TokenInfo token_info = cpi.token_info;
        int num_planes = av1_num_planes(cm);
        dealloc_context_buffers_ext(cpi.mbmi_ext_info);

        cpi.tile_data = null;
        cpi.allocated_tiles = 0;

        // Delete sementation map
        cpi.enc_seg.map = Ptr<byte>.Null;

        av1_cyclic_refresh_free(cpi.cyclic_refresh);
        cpi.cyclic_refresh = null;

        cpi.active_map.map = Ptr<byte>.Null;

        cpi.roi.roi_map = default;

        cpi.ssim_rdmult_scaling_factors = Ptr<double>.Null;

        cpi.tpl_rdmult_scaling_factors = Ptr<double>.Null;

        // [subset] CONFIG_TUNE_VMAF / CONFIG_TUNE_BUTTERAUGLI / CONFIG_SALIENCY_MAP
        // buffers: all 0 in the subset.

        // [subset] release_obmc_buffers(&cpi->td.mb.obmc_buffer): dropped with
        // MACROBLOCK::obmc_buffer (p1b2).

        cpi.td.mv_costs_alloc = null;
        // [subset] td.dv_costs_alloc dropped (intraBC is off).

        // [subset] td.mb.sb_stats_cache / td.mb.sb_fp_stats: dropped from
        // MACROBLOCK by p1b2 (sb_qp_sweep / sb multipass test only).
        // [subset] td.mb.rdcost: CONFIG_PARTITION_SEARCH_ORDER == 0.

        av1_free_pc_tree_recursive(cpi.td.pc_root, num_planes, 0, 0,
                                   cpi.sf.part_sf.partition_search_type);
        cpi.td.pc_root = null;

        // [subset] td.mb.intrabc_hash_info.hash_value_buffer[] frees and
        // av1_hash_tables_destroy(&td.mb.intrabc_hash_info): intrabc_hash_info is
        // dropped from MACROBLOCK by p1b2 (intraBC is off in the subset).

        cm.tpl_mvs = default;

        cpi.td.pixel_gradient_info = Ptr<PixelLevelGradientInfo>.Null;

        cpi.td.src_var_info_of_4x4_sub_blocks = Ptr<Block4x4VarInfo>.Null;

        cpi.td.vt64x64 = null;

        av1_free_pmc(cpi.td.firstpass_ctx, num_planes);
        cpi.td.firstpass_ctx = null;

        // [subset] tf_dealloc_data(&cpi->td.tf_data, ...),
        // tpl_dealloc_temp_buffers(&cpi->td.tpl_tmp_buffers) and
        // gm_dealloc_data(&cpi->td.gm_data): temporal filtering, TPL and global
        // motion search are out of the subset (those ThreadData members are
        // dropped).

        // This call ensures that CDEF search context buffers are deallocated in case
        // of an error during cdef search.
        av1_cdef_dealloc_data(cpi.cdef_search_ctx);
        cpi.cdef_search_ctx = null;

        av1_dealloc_mb_data(cpi.td.mb, num_planes);

        av1_dealloc_mb_wiener_var_pred_buf(cpi.td);

        av1_free_txb_buf(cpi);
        av1_free_context_buffers(cm);

        aom_free_frame_buffer(cpi.last_frame_uf);
        // [subset] av1_free_restoration_buffers / av1_free_firstpass_data:
        // !CONFIG_REALTIME_ONLY only.

        if (is_stat_generation_stage(cpi) == 0)
        {
            av1_free_cdef_buffers(cm, ref cpi.ppi.p_mt_info.cdef_worker,
                                  cpi.mt_info.cdef_sync);
        }

        // [subset] pick_lr_ctxt.rusi[] / pick_lr_ctxt.dgd_avg frees: loop
        // restoration search is out of the subset (AV1_COMP::pick_lr_ctxt dropped).

        aom_free_frame_buffer(cpi.trial_frame_rst);
        aom_free_frame_buffer(cpi.scaled_source);
        aom_free_frame_buffer(cpi.scaled_last_source);
        aom_free_frame_buffer(cpi.orig_source);
        aom_free_frame_buffer(cpi.svc.source_last_TL0);
        aom_free_frame_buffer(cpi.svc.source_half);
        aom_free_frame_buffer(cpi.svc.last_source_half);

        free_token_info(token_info);

        av1_free_shared_coeff_buffer(cpi.td.shared_coeff_buf);
        av1_free_sms_tree(cpi.td);

        // [subset] aom_free(cpi->td.mb.palette_buffer) and
        // release_compound_type_rd_buffers(&cpi->td.mb.comp_rd_buffer): members
        // dropped from MACROBLOCK by p1b2.
        // PORT-NOTE: C frees these without clearing the pointers; nulling them is
        // equivalent at teardown.
        cpi.td.mb.tmp_conv_dst = Ptr<CONV_BUF_TYPE>.Null;
        cpi.td.mb.upsample_pred = Ptr<byte>.Null;
        for (int j = 0; j < 2; ++j)
        {
            cpi.td.mb.tmp_pred_bufs[j] = Ptr<byte>.Null;
        }

        // [subset] denoise_and_model / film_grain_table: !CONFIG_REALTIME_ONLY only.

        // [subset] av1_free_svc_cyclic_refresh(cpi) when ppi->use_svc: SVC is out of
        // the subset.
        Debug.Assert(cpi.ppi.use_svc == 0, "SVC is out of the subset");
        cpi.svc.layer_context = null;

        cpi.consec_zero_mv = Ptr<byte>.Null;
        cpi.consec_zero_mv_alloc_size = 0;

        cpi.src_sad_blk_64x64 = Ptr<ulong>.Null;

        cpi.mb_weber_stats = null;
        cpi.mb_weber_stats_alloc_size = 0;

        // [subset] prep_rate_estimates / ext_rate_distribution frees: only with
        // oxcf.enable_rate_guide_deltaq (allintra deltaq-mode=3).
        Debug.Assert(cpi.oxcf.enable_rate_guide_deltaq == 0,
                     "rate guided deltaq is out of the subset");

        cpi.mb_delta_q = Ptr<int>.Null;
        cpi.mb_delta_q_alloc_size = 0;

        // [subset] av1_free_tpl_gop_stats: !CONFIG_REALTIME_ONLY only.
    }

    // Not executed in the subset (the gradient caching speed feature is off for
    // the rt nonrd path), but tiny and directly gated by a speed feature query.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void allocate_gradient_info_for_hog(AV1_COMP cpi)
    {
        if (!is_gradient_caching_for_hog_enabled(cpi)) return;

        Ptr<PixelLevelGradientInfo> pixel_gradient_info = cpi.td.pixel_gradient_info;
        if (pixel_gradient_info.IsNull)
        {
            AV1_COMMON cm = cpi.common;
            int plane_types = PLANE_TYPES >> cm.seq_params.monochrome;
            pixel_gradient_info = new PixelLevelGradientInfo[plane_types * MAX_SB_SQUARE];
            cpi.td.pixel_gradient_info = pixel_gradient_info;
        }

        cpi.td.mb.pixel_gradient_info = pixel_gradient_info;
    }

    // Not executed in the subset (speed feature gated); see above.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void allocate_src_var_of_4x4_sub_block_buf(AV1_COMP cpi)
    {
        if (!is_src_var_for_4x4_sub_blocks_caching_enabled(cpi)) return;

        Ptr<Block4x4VarInfo> source_variance_info =
            cpi.td.src_var_info_of_4x4_sub_blocks;
        if (source_variance_info.IsNull)
        {
            AV1_COMMON cm = cpi.common;
            BLOCK_SIZE sb_size = cm.seq_params.sb_size;
            int mi_count_in_sb = mi_size_wide[sb_size] * mi_size_high[sb_size];
            source_variance_info = new Block4x4VarInfo[mi_count_in_sb];
            cpi.td.src_var_info_of_4x4_sub_blocks = source_variance_info;
        }

        cpi.td.mb.src_var_info_of_4x4_sub_blocks = source_variance_info;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void variance_partition_alloc(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        int num_64x64_blocks = (cm.seq_params.sb_size == BLOCK_64X64) ? 1 : 4;
        if (cpi.td.vt64x64 != null)
        {
            if (num_64x64_blocks != cpi.td.num_64x64_blocks)
            {
                cpi.td.vt64x64 = null;
            }
        }
        if (cpi.td.vt64x64 == null)
        {
            // PORT-NOTE: C aom_malloc leaves the tree uninitialized;
            // var_based_part.c fills every node before reading it.
            cpi.td.vt64x64 = alloc_array<VP64x64>(num_64x64_blocks);
            cpi.td.num_64x64_blocks = num_64x64_blocks;
        }
    }

    // [subset] realloc_and_scale_source: never executed (only used by the non-rt
    // superres / resize recode paths in encoder.c).

    // Deallocate allocated thread_data.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void free_thread_data(AV1_PRIMARY ppi)
    {
        PrimaryMultiThreadInfo p_mt_info = ppi.p_mt_info;
        // [subset] num_tf_workers / num_tpl_workers / is_highbitdepth only fed the
        // tf_dealloc_data / tpl_dealloc_temp_buffers calls below (dropped).
        int num_planes = ppi.seq_params.monochrome != 0 ? 1 : MAX_MB_PLANE;
        for (int t = 1; t < p_mt_info.num_workers; ++t)
        {
            EncWorkerData thread_data = p_mt_info.tile_thr_data[t];
            thread_data.td = thread_data.original_td;
            ThreadData td = thread_data.td;
            if (td == null) continue;
            td.tctx = null;
            // [subset] td->palette_buffer: dropped (p1b2 dropped PALETTE_BUFFER).
            td.tmp_conv_dst = Ptr<CONV_BUF_TYPE>.Null;
            td.upsample_pred = Ptr<byte>.Null;
            // [subset] release_compound_type_rd_buffers(&td->comp_rd_buffer):
            // dropped (p1b2 dropped CompoundTypeRdBuffers).
            for (int j = 0; j < 2; ++j)
            {
                td.tmp_pred_bufs[j] = Ptr<byte>.Null;
            }
            td.pixel_gradient_info = Ptr<PixelLevelGradientInfo>.Null;
            td.src_var_info_of_4x4_sub_blocks = Ptr<Block4x4VarInfo>.Null;
            // [subset] release_obmc_buffers(&td->obmc_buffer): dropped (p1b2
            // dropped OBMCBuffer).
            td.vt64x64 = null;

            // [subset] td->hash_value_buffer[] (intraBC hash) dropped.
            td.mv_costs_alloc = null;
            // [subset] td->dv_costs_alloc (IntraBCMVCosts) dropped.
            td.counts = null;
            av1_free_pmc(td.firstpass_ctx, num_planes);
            td.firstpass_ctx = null;
            av1_free_shared_coeff_buffer(td.shared_coeff_buf);
            av1_free_sms_tree(td);
            // [subset] tf_dealloc_data / tpl_dealloc_temp_buffers /
            // gm_dealloc_data: temporal filtering, TPL and global motion search
            // are out of the subset.
            av1_dealloc_mb_data(td.mb, num_planes);
            // [subset] td->mb.sb_stats_cache / sb_fp_stats: dropped from
            // MACROBLOCK by p1b2. td->mb.rdcost: CONFIG_PARTITION_SEARCH_ORDER == 0.
            av1_free_pc_tree_recursive(td.pc_root, num_planes, 0, 0, SEARCH_PARTITION);
            td.pc_root = null;
            av1_dealloc_mb_wiener_var_pred_buf(td);
            thread_data.td = null;
            thread_data.original_td = null;
        }
    }
}
