// Port of libaom av1/encoder/encoder_utils.c
using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class encoder_utils
{
    // [subset] default_tx_type_probs, default_obmc_probs, default_switchable_interp_probs:
    // only read by copy_frame_prob_info under speed features that are off for the rt presets
    // (see encoder_utils.h.cs).

    internal static readonly int[] default_warped_probs = { 64, 64, 64, 64,
                                                            64, 64, 64 };

    // [subset] configure_static_seg_features (two-pass static segmentation) and
    // av1_apply_roi_map (ROI maps, never set): not executed in the subset.

    internal static void av1_apply_active_map(AV1_COMP cpi)
    {
        segmentation seg = cpi.common.seg;
        Ptr<byte> seg_map = cpi.enc_seg.map;
        Ptr<byte> active_map = cpi.active_map.map;
        CommonModeInfoParams mi_params = cpi.common.mi_params;

        Debug.Assert(AM_SEGMENT_ID_ACTIVE == CR_SEGMENT_ID_BASE);

        // Disable the active_maps on intra_only frames, if the input map for the
        // current frame has no inactive blocks, or if the map was set for another
        // frame size.
        if (frame_is_intra_only(cpi.common) != 0 ||
            cpi.rc.percent_blocks_inactive == 0 ||
            (cpi.active_map.enabled != 0 &&
             (cpi.active_map.mi_rows != mi_params.mi_rows ||
              cpi.active_map.mi_cols != mi_params.mi_cols)))
        {
            cpi.active_map.enabled = 0;
            cpi.active_map.update = 1;
        }

        if (cpi.active_map.update != 0)
        {
            if (cpi.active_map.enabled != 0)
            {
                // [subset] active map segmentation (AOME_SET_ACTIVEMAP): never set in the
                // subset.
                _ = seg_map;
                _ = active_map;
                throw new NotSupportedException("active maps are not in the subset");
            }
            else
            {
                av1_disable_segfeature(seg, AM_SEGMENT_ID_INACTIVE, SEG_LVL_SKIP);
                av1_disable_segfeature(seg, AM_SEGMENT_ID_INACTIVE, SEG_LVL_ALT_LF_Y_H);
                av1_disable_segfeature(seg, AM_SEGMENT_ID_INACTIVE, SEG_LVL_ALT_LF_Y_V);
                av1_disable_segfeature(seg, AM_SEGMENT_ID_INACTIVE, SEG_LVL_ALT_LF_U);
                av1_disable_segfeature(seg, AM_SEGMENT_ID_INACTIVE, SEG_LVL_ALT_LF_V);
                if (seg.enabled != 0)
                {
                    seg.update_data = 1;
                    seg.update_map = 1;
                }
            }
            cpi.active_map.update = 0;
        }
    }

    // [subset] process_tpl_stats_frame: !CONFIG_REALTIME_ONLY.

    internal static void av1_set_size_dependent_vars(AV1_COMP cpi, ref int q, ref int bottom_index,
                                                     ref int top_index)
    {
        AV1_COMMON cm = cpi.common;

        // Setup variables that depend on the dimensions of the frame.
        av1_set_speed_features_framesize_dependent(cpi, cpi.speed);

        // [subset] tpl stats processing: !CONFIG_REALTIME_ONLY.

        if (cpi.oxcf.q_cfg.use_fixed_qp_offsets == 2 &&
            cpi.oxcf.rc_cfg.mode == AOM_Q)
        {
            // [subset] fixed qp offsets with AOM_Q: the subset is CBR.
            throw new NotSupportedException("AOM_Q with fixed qp offsets is not in the subset");
        }
        else
        {
            // Decide q and q bounds.
            q = av1_rc_pick_q_and_bounds(cpi, cm.width, cm.height,
                                         cpi.gf_frame_index, ref bottom_index, ref top_index);

            if (cpi.oxcf.rc_cfg.mode == AOM_CBR && cpi.rc.force_max_q != 0)
            {
                q = cpi.rc.worst_quality;
                cpi.rc.force_max_q = 0;
            }

            // [subset] tpl q / fixed qp offsets for AOM_Q: !CONFIG_REALTIME_ONLY.
        }

        // Configure experimental use of segmentation for enhanced coding of
        // static regions if indicated.
        // Only allowed in the second pass of a two pass encode, as it requires
        // lagged coding, and if the relevant speed feature flag is set.
        if (is_stat_consumption_stage_twopass(cpi) != 0 &&
            cpi.sf.hl_sf.static_segmentation != 0)
        {
            // [subset] configure_static_seg_features: two-pass only.
            throw new NotSupportedException("static segmentation is not in the subset");
        }

        if (cpi.oxcf.rc_cfg.force_max_q != 0 || cpi.oxcf.rc_cfg.over_shoot_pct == 0)
        {
            top_index = MAXQ;
        }
        if (cpi.oxcf.rc_cfg.under_shoot_pct == 0) bottom_index = MINQ;
    }

    // [subset] reset_film_grain_chroma_params, av1_update_film_grain_parameters_seq,
    // av1_update_film_grain_parameters: !CONFIG_REALTIME_ONLY.

    internal static void av1_scale_references(AV1_COMP cpi, InterpFilter filter,
                                              int phase, int use_optimized_scaler)
    {
        AV1_COMMON cm = cpi.common;
        int num_planes = av1_num_planes(cm);
        MV_REFERENCE_FRAME ref_frame;

        for (ref_frame = LAST_FRAME; ref_frame <= ALTREF_FRAME; ++ref_frame)
        {
            // Need to convert from AOM_REFFRAME to index into ref_mask (subtract 1).
            if ((cpi.ref_frame_flags & av1_ref_frame_flag_list[ref_frame]) != 0)
            {
                YV12_BUFFER_CONFIG @ref =
                    get_ref_frame_yv12_buf(cm, ref_frame);

                if (@ref == null)
                {
                    cpi.scaled_ref_buf[ref_frame - 1] = null;
                    continue;
                }

                // For RTC-SVC: if force_zero_mode_spatial_ref is enabled, check if the
                // motion search can be skipped for the references: last, golden,
                // altref. If so, we can skip scaling that reference.
                if (cpi.ppi.use_svc != 0 && cpi.svc.force_zero_mode_spatial_ref != 0 &&
                    cpi.ppi.rtc_ref.set_ref_frame_config != 0)
                {
                    if (ref_frame == LAST_FRAME && cpi.svc.skip_mvsearch_last != 0) continue;
                    if (ref_frame == GOLDEN_FRAME && cpi.svc.skip_mvsearch_gf != 0) continue;
                    if (ref_frame == ALTREF_FRAME && cpi.svc.skip_mvsearch_altref != 0)
                        continue;
                }
                // For RTC with superres on: golden reference only needs to be scaled
                // if it was refreshed in previous frame.
                if (is_one_pass_rt_params(cpi) != 0 &&
                    cpi.oxcf.superres_cfg.enable_superres && ref_frame == GOLDEN_FRAME &&
                    cpi.rc.frame_num_last_gf_refresh <
                        (int)cm.current_frame.frame_number - 1)
                {
                    continue;
                }

                if (@ref.y_crop_width != cm.width || @ref.y_crop_height != cm.height)
                {
                    // [subset] reference scaling (aom_yv12_realloc_with_new_border,
                    // get_free_fb, aom_realloc_frame_buffer, av1_resize_and_extend_frame*,
                    // alloc_frame_mvs): resize / superres / spatial layers are not in the
                    // subset, so every reference has the frame size.
                    _ = filter;
                    _ = phase;
                    _ = use_optimized_scaler;
                    _ = num_planes;
                    throw new NotSupportedException("reference scaling is not in the subset");
                }
                else
                {
                    RefCntBuffer buf = get_ref_frame_buf(cm, ref_frame);
                    buf.buf.y_crop_width = @ref.y_crop_width;
                    buf.buf.y_crop_height = @ref.y_crop_height;
                    cpi.scaled_ref_buf[ref_frame - 1] = buf;
                    ++buf.ref_count;
                }
            }
            else
            {
                if (has_no_stats_stage(cpi) == 0) cpi.scaled_ref_buf[ref_frame - 1] = null;
            }
        }
    }

    internal static BLOCK_SIZE av1_select_sb_size(AV1EncoderConfig oxcf, int width,
                                                  int height, int number_spatial_layers)
    {
        if (oxcf.tool_cfg.superblock_size == AOM_SUPERBLOCK_SIZE_64X64)
        {
            return BLOCK_64X64;
        }
        if (oxcf.tool_cfg.superblock_size == AOM_SUPERBLOCK_SIZE_128X128)
        {
            return BLOCK_128X128;
        }
        // [subset] CONFIG_TFLITE: not configured.
        // Force 64x64 superblock size to increase resolution in perceptual
        // AQ and user rating based modes.
        if (oxcf.mode == ALLINTRA &&
            (oxcf.q_cfg.deltaq_mode == DELTA_Q_PERCEPTUAL_AI ||
             oxcf.q_cfg.deltaq_mode == DELTA_Q_USER_RATING_BASED))
        {
            return BLOCK_64X64;
        }
        // Variance Boost only supports 64x64 superblocks.
        if (oxcf.q_cfg.deltaq_mode == DELTA_Q_VARIANCE_BOOST)
        {
            return BLOCK_64X64;
        }
        Debug.Assert(oxcf.tool_cfg.superblock_size == AOM_SUPERBLOCK_SIZE_DYNAMIC);

        if (number_spatial_layers > 1)
        {
            // For spatial layers better selection may be done given the resolutions
            // used across the layers, but for now use 64x64 for spatial layers.
            return BLOCK_64X64;
        }
        else if (oxcf.resize_cfg.resize_mode != RESIZE_NONE)
        {
            // Use the configured size (top resolution) for resize.
            return AOMMIN(oxcf.frm_dim_cfg.width, oxcf.frm_dim_cfg.height) > 720
                       ? BLOCK_128X128
                       : BLOCK_64X64;
        }
        else if (oxcf.mode == REALTIME)
        {
            if (oxcf.tune_cfg.content == AOM_CONTENT_SCREEN)
            {
                // [subset] screen content superblock selection: tune_content is default.
                throw new NotSupportedException("screen content is not in the subset");
            }
            else
            {
                return AOMMIN(width, height) > 720 ? BLOCK_128X128 : BLOCK_64X64;
            }
        }

        // [subset] the non-realtime heuristics (GOOD / ALLINTRA): usage is realtime.
        throw new NotSupportedException("non-realtime superblock size selection is not in the subset");
    }

    internal static void av1_setup_frame(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        // Set up entropy context depending on frame type. The decoder mandates
        // the use of the default context, index 0, for keyframes and inter
        // frames where the error_resilient_mode or intra_only flag is set. For
        // other inter-frames the encoder currently uses only two contexts;
        // context 1 for ALTREF frames and context 0 for the others.

        if (frame_is_intra_only(cm) != 0 || cm.features.error_resilient_mode ||
            cpi.ext_flags.use_primary_ref_none)
        {
            av1_setup_past_independence(cm);
        }

        if ((cm.current_frame.frame_type == KEY_FRAME && cm.show_frame != 0) ||
            frame_is_sframe(cm) != 0)
        {
            if (cpi.ppi.seq_params_locked == 0)
            {
                set_sb_size(cm.seq_params,
                            av1_select_sb_size(cpi.oxcf, cm.width, cm.height,
                                               (int)cpi.ppi.number_spatial_layers));
            }
        }
        else
        {
            RefCntBuffer primary_ref_buf = get_primary_ref_frame_buf(cm);
            if (primary_ref_buf == null)
            {
                av1_setup_past_independence(cm);
                cm.seg.update_map = 1;
                cm.seg.update_data = 1;
            }
            else
            {
                cm.fc.CopyFrom(primary_ref_buf.frame_context);
            }
        }

        av1_zero(cm.cur_frame.interp_filter_selected);
        cm.prev_frame = get_primary_ref_frame_buf(cm);
        cpi.vaq_refresh = 0;
    }

    // [subset] get_interp_filter_selected, av1_setup_interp_filter_search_mask,
    // screen_content_tools_determination, set_encoding_params_for_screen_content,
    // av1_determine_sc_tools_with_encoding: !CONFIG_REALTIME_ONLY.

    private static void fix_interp_filter(ref InterpFilter interp_filter,
                                          FRAME_COUNTS counts)
    {
        if (interp_filter == SWITCHABLE)
        {
            // Check to see if only one of the filters is actually used
            Span<int> count = stackalloc int[SWITCHABLE_FILTERS];
            count.Clear();
            int num_filters_used = 0;
            for (int i = 0; i < SWITCHABLE_FILTERS; ++i)
            {
                for (int j = 0; j < SWITCHABLE_FILTER_CONTEXTS; ++j)
                    count[i] += (int)counts.switchable_interp[j][i];
                num_filters_used += B(count[i] > 0);
            }
            if (num_filters_used == 1)
            {
                // Only one filter is used. So set the filter at frame level
                for (int i = 0; i < SWITCHABLE_FILTERS; ++i)
                {
                    if (count[i] != 0)
                    {
                        interp_filter = i;
                        break;
                    }
                }
            }
        }
    }

    internal static void av1_finalize_encoded_frame(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        CurrentFrame current_frame = cm.current_frame;

        if (cm.seq_params.reduced_still_picture_hdr == 0 &&
            encode_show_existing_frame(cm) != 0)
        {
            // [subset] show_existing_frame: only used for ARF overlays (no lookahead in the
            // subset).
            throw new NotSupportedException("show_existing_frame is not in the subset");
        }

        if (encode_show_existing_frame(cm) == 0 &&
            cm.seq_params.film_grain_params_present != 0 &&
            (cm.show_frame != 0 || cm.showable_frame != 0))
        {
            // [subset] film grain: not in the subset.
            _ = current_frame;
            throw new NotSupportedException("film grain is not in the subset");
        }

        // Initialise all tiles' contexts from the global frame context
        for (int tile_col = 0; tile_col < cm.tiles.cols; tile_col++)
        {
            for (int tile_row = 0; tile_row < cm.tiles.rows; tile_row++)
            {
                int tile_idx = tile_row * cm.tiles.cols + tile_col;
                cpi.tile_data[tile_idx].tctx.CopyFrom(cm.fc);
            }
        }

        if (frame_is_intra_only(cm) == 0)
            fix_interp_filter(ref cm.features.interp_filter, cpi.td.counts);
    }

    // [subset] av1_is_integer_mv (screen content force_integer_mv) and
    // av1_set_mb_ssim_rdmult_scaling (tuning SSIM): not executed in the subset.

    // Coding context that only needs to be saved when recode loop includes
    // filtering (deblocking, CDEF, superres post-encode upscale and/or loop
    // restoraton).
    private static void save_extra_coding_context(AV1_COMP cpi)
    {
        CODING_CONTEXT cc = cpi.coding_context;
        AV1_COMMON cm = cpi.common;

        cc.lf.CopyFrom(cm.lf);
        cc.cdef_info.CopyFrom(cm.cdef_info);
        cc.rc.CopyFrom(cpi.rc);
        cc.mv_stats = cpi.ppi.mv_stats;
        cc.frame_number = cpi.common.current_frame.frame_number;
        cc.frame_index_set = cpi.frame_index_set;
    }

    internal static void av1_save_all_coding_context(AV1_COMP cpi)
    {
        save_extra_coding_context(cpi);
        if (frame_is_intra_only(cpi.common) == 0) release_scaled_references(cpi);
    }

    // [subset] av1_dump_filtered_recon_frames: DUMP_RECON_FRAMES == 0.
}
