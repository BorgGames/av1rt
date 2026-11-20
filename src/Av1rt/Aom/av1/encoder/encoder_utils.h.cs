// Port of libaom av1/encoder/encoder_utils.h
global using static Av1rt.Aom.encoder_utils;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class encoder_utils
{
    internal const int AM_SEGMENT_ID_INACTIVE = 7;
    internal const int AM_SEGMENT_ID_ACTIVE = 0;
    internal const int DUMP_RECON_FRAMES = 0;

    // default_tx_type_probs / default_obmc_probs / default_warped_probs /
    // default_switchable_interp_probs are defined in encoder_utils.c (f4).
    // default_warped_probs is stubbed in _stubs/p1b1.cs (only it is read in the
    // subset, by copy_frame_prob_info).

    // [subset] suppress_active_map: never executed (only called when the active
    // map is enabled through the non-rt path of encoder.c).

    // Returns 'size' in the number of Mode Info (MI) units. 'size' is either the
    // width or height.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int size_in_mi(int size)
    {
        // Ensure that the decoded width and height are both multiples of
        // 8 luma pixels (note: this may only be a multiple of 4 chroma pixels if
        // subsampling is used).
        // This simplifies the implementation of various experiments,
        // eg. cdef, which operates on units of 8x8 luma pixels.
        int aligned_size = ALIGN_POWER_OF_TWO(size, 3);
        return aligned_size >> MI_SIZE_LOG2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_mb_mi(CommonModeInfoParams mi_params, int width,
                                   int height)
    {
        mi_params.mi_cols = size_in_mi(width);
        mi_params.mi_rows = size_in_mi(height);
        mi_params.mi_stride = calc_mi_size(mi_params.mi_cols);

        mi_params.mb_cols = ROUND_POWER_OF_TWO(mi_params.mi_cols, 2);
        mi_params.mb_rows = ROUND_POWER_OF_TWO(mi_params.mi_rows, 2);
        mi_params.MBs = mi_params.mb_rows * mi_params.mb_cols;

        int mi_alloc_size_1d = mi_size_wide[mi_params.mi_alloc_bsize];
        mi_params.mi_alloc_stride =
            (mi_params.mi_stride + mi_alloc_size_1d - 1) / mi_alloc_size_1d;

        Debug.Assert(mi_size_wide[mi_params.mi_alloc_bsize] ==
                     mi_size_high[mi_params.mi_alloc_bsize]);
    }

    // Installed as CommonModeInfoParams::free_mi by encoder.c.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void enc_free_mi(CommonModeInfoParams mi_params)
    {
        mi_params.mi_alloc = default;
        mi_params.mi_alloc_size = 0;
        mi_params.mi_grid_base = default;
        mi_params.mi_grid_size = 0;
        mi_params.tx_type_map = default;
    }

    // Installed as CommonModeInfoParams::set_mb_mi by encoder.c.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void enc_set_mb_mi(CommonModeInfoParams mi_params, int width,
                                       int height, BLOCK_SIZE min_partition_size)
    {
        mi_params.mi_alloc_bsize = min_partition_size;

        set_mb_mi(mi_params, width, height);
    }

    // Not executed in the subset (first pass / LAP only); kept because encoder.c
    // installs it as CommonModeInfoParams::set_mb_mi for the stat stage.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void stat_stage_set_mb_mi(CommonModeInfoParams mi_params,
                                              int width, int height,
                                              BLOCK_SIZE min_partition_size)
    {
        mi_params.mi_alloc_bsize = BLOCK_16X16;

        set_mb_mi(mi_params, width, height);
    }

    // PORT-NOTE: never executed in the subset; kept because encoder.c installs
    // it as CommonModeInfoParams::setup_mi. memset(0) of the MB_MODE_INFO
    // storage is written element-wise so that it works whatever representation
    // p1a picks (array of classes / structs, T[] or Ptr<T>).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void enc_setup_mi(CommonModeInfoParams mi_params)
    {
        int mi_grid_size =
            mi_params.mi_stride * calc_mi_size(mi_params.mi_rows);
        for (int i = 0; i < mi_params.mi_alloc_size; i++)
            mi_params.mi_alloc[i] = new MB_MODE_INFO();
        for (int i = 0; i < mi_grid_size; i++)
            mi_params.mi_grid_base[i] = default;
        for (int i = 0; i < mi_grid_size; i++)
            mi_params.tx_type_map[i] = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void init_buffer_indices(
        ForceIntegerMVInfo force_intpel_info, int[] remapped_ref_idx)
    {
        int fb_idx;
        for (fb_idx = 0; fb_idx < REF_FRAMES; ++fb_idx)
            remapped_ref_idx[fb_idx] = fb_idx;
        force_intpel_info.rate_index = 0;
        force_intpel_info.rate_size = 0;
    }

    // [subset] HIGHBD_BFP* / MAKE_BFP_* / MAKE_SDSF_* / MAKE_OBFP_* wrappers and
    // highbd_set_var_fns: CONFIG_AV1_HIGHBITDEPTH == 0.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void copy_frame_prob_info(AV1_COMP cpi)
    {
        FrameProbInfo frame_probs = cpi.ppi.frame_probs;
        // [subset] tx_type_probs copy: sf.tx_sf.tx_type_search.prune_tx_type_using_stats
        // is 0 for the rt speed presets.
        Debug.Assert(cpi.sf.tx_sf.tx_type_search.prune_tx_type_using_stats == 0,
                     "prune_tx_type_using_stats is off in the subset");
        // [subset] obmc_probs copy: sf.inter_sf.prune_obmc_prob_thresh is 0 for
        // the rt speed presets.
        Debug.Assert(!(cpi.sf.inter_sf.prune_obmc_prob_thresh > 0 &&
                       cpi.sf.inter_sf.prune_obmc_prob_thresh < int.MaxValue),
                     "prune_obmc_prob_thresh is off in the subset");
        if (cpi.sf.inter_sf.prune_warped_prob_thresh > 0)
        {
            copy_array(frame_probs.warped_probs, default_warped_probs);
        }
        // [subset] switchable_interp_probs copy:
        // sf.interp_sf.adaptive_interp_filter_search is never 2 for the rt presets.
        Debug.Assert(cpi.sf.interp_sf.adaptive_interp_filter_search != 2,
                     "adaptive_interp_filter_search == 2 is off in the subset");

        // [subset] temp_frame_probs*: CONFIG_FPMT_TEST == 0.
    }

    // Not executed in the subset; reached from restore_extra_coding_context
    // (post-encode frame drop path, data dependent).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void restore_cdef_coding_context(CdefInfo dst,
                                                     CdefInfo src)
    {
        dst.cdef_bits = src.cdef_bits;
        dst.cdef_damping = src.cdef_damping;
        for (int i = 0; i < CDEF_MAX_STRENGTHS; i++)
        {
            dst.cdef_strengths[i] = src.cdef_strengths[i];
            dst.cdef_uv_strengths[i] = src.cdef_uv_strengths[i];
        }
        dst.nb_cdef_strengths = src.nb_cdef_strengths;
    }

    // Not executed in the subset; reached from restore_all_coding_context (frame
    // drop / recode paths, data dependent).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void restore_extra_coding_context(AV1_COMP cpi)
    {
        CODING_CONTEXT cc = cpi.coding_context;
        AV1_COMMON cm = cpi.common;
        cm.lf.CopyFrom(cc.lf);
        restore_cdef_coding_context(cm.cdef_info, cc.cdef_info);
        cpi.rc.CopyFrom(cc.rc);
        cpi.ppi.mv_stats = cc.mv_stats;
        cpi.common.current_frame.frame_number = cc.frame_number;
        cpi.frame_index_set = cc.frame_index_set;
    }

    // [subset] equal_dimensions_and_border: never executed (non-rt scaling path).

    // Not executed in the subset; tiny, reached from av1_cx_iface controls
    // (AV1E_SET_... / aom_codec_encode flags), i.e. API-data dependent.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int update_entropy(ref bool ext_refresh_frame_context,
                                       ref bool ext_refresh_frame_context_pending,
                                       bool update)
    {
        ext_refresh_frame_context = update;
        ext_refresh_frame_context_pending = true;
        return 0;
    }

    // [subset] combine_prior_with_tpl_boost: !CONFIG_REALTIME_ONLY only.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_size_independent_vars(AV1_COMP cpi)
    {
        int i;
        AV1_COMMON cm = cpi.common;
        for (i = LAST_FRAME; i <= ALTREF_FRAME; ++i)
        {
            cm.global_motion[i] = default_warp_params;
        }
        cpi.gm_info.search_done = false;

        av1_set_speed_features_framesize_independent(cpi, cpi.speed);
        av1_set_rd_speed_thresholds(cpi);
        cm.features.interp_filter = SWITCHABLE;
        cm.features.switchable_motion_mode = is_switchable_motion_mode_allowed(
            cm.features.allow_warped_motion, cpi.oxcf.motion_mode_cfg.enable_obmc);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void release_scaled_references(AV1_COMP cpi)
    {
        // Scaled references should only need to be released under certain conditions:
        // if the reference will be updated, or if the scaled reference has same
        // resolution. For now only apply this to Golden for non-svc RTC mode.
        AV1_COMMON cm = cpi.common;
        bool refresh_golden = cpi.refresh_frame.golden_frame;
        bool release_golden = true;
        for (int i = 0; i < INTER_REFS_PER_FRAME; ++i)
        {
            RefCntBuffer buf = cpi.scaled_ref_buf[i];
            bool golden_ref = (i == GOLDEN_FRAME - 1);
            if (golden_ref && is_one_pass_rt_params(cpi) != 0 && cpi.ppi.use_svc == 0 &&
                buf != null)
            {
                RefCntBuffer @ref = get_ref_frame_buf(cm, GOLDEN_FRAME);
                bool same_resoln = buf.buf.y_crop_width == @ref.buf.y_crop_width &&
                                   buf.buf.y_crop_height == @ref.buf.y_crop_height;
                release_golden = refresh_golden || same_resoln;
            }
            if (buf != null && (!golden_ref || (golden_ref && release_golden)))
            {
                --buf.ref_count;
                cpi.scaled_ref_buf[i] = null;
            }
        }
    }

    // Not executed in the subset; reached from the post-encode frame drop path
    // (data dependent).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void restore_all_coding_context(AV1_COMP cpi)
    {
        restore_extra_coding_context(cpi);
        if (frame_is_intra_only(cpi.common) == 0) release_scaled_references(cpi);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int reduce_num_ref_buffers(AV1_COMP cpi)
    {
        SequenceHeader seq_params = cpi.common.seq_params;
        return B(is_one_pass_rt_params(cpi) != 0 &&
                 use_rtc_reference_structure_one_layer(cpi) != 0 &&
                 (seq_params.order_hint_info.enable_order_hint == 0) &&
                 cpi.rt_reduce_num_ref_buffers != 0);
    }

    // Refresh reference frame buffers according to refresh_frame_flags.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void refresh_reference_frames(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        // All buffers are refreshed for shown keyframes and S-frames.
        // In case of RT, golden frame refreshes the 6th slot and other reference
        // frames refresh slots 0 to 5. Slot 7 is not refreshed by any reference
        // frame. Thus, only 7 buffers are refreshed for keyframes and S-frames
        // instead of 8.
        int num_ref_buffers = REF_FRAMES;
        if (reduce_num_ref_buffers(cpi) != 0)
        {
            bool refresh_all_bufs =
                (cpi.ppi.gf_group.refbuf_state[cpi.gf_frame_index] == REFBUF_RESET ||
                 frame_is_sframe(cm) != 0);
            Debug.Assert(IMPLIES(((cm.current_frame.refresh_frame_flags >> 7) & 1) == 1,
                                 refresh_all_bufs));
            num_ref_buffers--;
        }

        for (int ref_frame = 0; ref_frame < num_ref_buffers; ref_frame++)
        {
            if (((cm.current_frame.refresh_frame_flags >> ref_frame) & 1) == 1)
            {
                assign_frame_buffer_p(ref cm.ref_frame_map[ref_frame], cm.cur_frame);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_get_enc_border_size(bool resize, bool all_intra,
                                                BLOCK_SIZE sb_size)
    {
        // For allintra encoding mode, inter-frame motion search is not applicable and
        // the intraBC motion vectors are restricted within the tile boundaries. Hence
        // a smaller frame border size (AOM_ENC_ALLINTRA_BORDER) is used in this case.
        if (resize)
        {
            return AOM_BORDER_IN_PIXELS;
        }
        if (all_intra)
        {
            return AOM_ENC_ALLINTRA_BORDER;
        }
        return block_size_wide[sb_size] + 32;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool av1_is_resize_needed(AV1EncoderConfig oxcf)
    {
        ref readonly ResizeCfg resize_cfg = ref oxcf.resize_cfg;
        ref readonly SuperResCfg superres_cfg = ref oxcf.superres_cfg;
        return resize_cfg.resize_mode != 0 || superres_cfg.superres_mode != 0;
    }
}
