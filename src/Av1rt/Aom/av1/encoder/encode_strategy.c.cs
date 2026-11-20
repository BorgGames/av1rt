// Port of libaom av1/encoder/encode_strategy.c

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

// [subset] One-pass realtime with lag 0 always uses the internal single-layer RTC reference
// structure (av1_set_rtc_reference_structure_one_layer sets refresh_frame.update_pending), so
// the GOP based reference selection (av1_get_ref_frames, get_refresh_idx,
// av1_calc_refresh_idx_for_intnl_arf, ...), is_forced_keyframe_pending (needs ARF source
// offsets) and the two-pass / temporal-filter / denoise paths are not ported.
internal static partial class encode_strategy
{
    // #define TEMPORAL_FILTER_KEY_FRAME (CONFIG_REALTIME_ONLY ? 0 : 1)

    private static void set_refresh_frame_flags(ref RefreshFrameInfo refresh_frame,
                                                bool refresh_gf, bool refresh_bwdref,
                                                bool refresh_arf)
    {
        refresh_frame.golden_frame = refresh_gf;
        refresh_frame.bwd_ref_frame = refresh_bwdref;
        refresh_frame.alt_ref_frame = refresh_arf;
    }

    internal static void av1_configure_buffer_updates(AV1_COMP cpi,
                                                      ref RefreshFrameInfo refresh_frame,
                                                      FRAME_UPDATE_TYPE type,
                                                      REFBUF_STATE refbuf_state,
                                                      int force_refresh_all)
    {
        // NOTE(weitinglin): Should we define another function to take care of
        // cpi->rc.is_$Source_Type to make this function as it is in the comment?
        ref ExtRefreshFrameFlagsInfo ext_refresh_frame_flags =
            ref cpi.ext_flags.refresh_frame;
        cpi.rc.is_src_frame_alt_ref = 0;

        switch (type)
        {
            case KF_UPDATE:
                set_refresh_frame_flags(ref refresh_frame, true, true, true);
                break;

            case LF_UPDATE:
                set_refresh_frame_flags(ref refresh_frame, false, false, false);
                break;

            case GF_UPDATE:
                set_refresh_frame_flags(ref refresh_frame, true, false, false);
                break;

            case OVERLAY_UPDATE:
                if (refbuf_state == REFBUF_RESET)
                    set_refresh_frame_flags(ref refresh_frame, true, true, true);
                else
                    set_refresh_frame_flags(ref refresh_frame, true, false, false);

                cpi.rc.is_src_frame_alt_ref = 1;
                break;

            case ARF_UPDATE:
                // NOTE: BWDREF does not get updated along with ALTREF_FRAME.
                if (refbuf_state == REFBUF_RESET)
                    set_refresh_frame_flags(ref refresh_frame, true, true, true);
                else
                    set_refresh_frame_flags(ref refresh_frame, false, false, true);

                break;

            case INTNL_OVERLAY_UPDATE:
                set_refresh_frame_flags(ref refresh_frame, false, false, false);
                cpi.rc.is_src_frame_alt_ref = 1;
                break;

            case INTNL_ARF_UPDATE:
                set_refresh_frame_flags(ref refresh_frame, false, true, false);
                break;

            default: Debug.Assert(false); break;
        }

        if (ext_refresh_frame_flags.update_pending &&
            (is_stat_generation_stage(cpi) == 0))
        {
            set_refresh_frame_flags(ref refresh_frame,
                                    ext_refresh_frame_flags.golden_frame,
                                    ext_refresh_frame_flags.bwd_ref_frame,
                                    ext_refresh_frame_flags.alt_ref_frame);
            GF_GROUP gf_group = cpi.ppi.gf_group;
            if (ext_refresh_frame_flags.golden_frame)
                gf_group.update_type[cpi.gf_frame_index] = GF_UPDATE;
            if (ext_refresh_frame_flags.alt_ref_frame)
                gf_group.update_type[cpi.gf_frame_index] = ARF_UPDATE;
            if (ext_refresh_frame_flags.bwd_ref_frame)
                gf_group.update_type[cpi.gf_frame_index] = INTNL_ARF_UPDATE;
        }

        if (force_refresh_all != 0)
            set_refresh_frame_flags(ref refresh_frame, true, true, true);
    }

    private static void set_additional_frame_flags(AV1_COMMON cm, ref uint frame_flags)
    {
        if (frame_is_intra_only(cm) != 0)
        {
            frame_flags |= FRAMEFLAGS_INTRAONLY;
        }
        if (frame_is_sframe(cm) != 0)
        {
            frame_flags |= FRAMEFLAGS_SWITCH;
        }
        if (cm.features.error_resilient_mode)
        {
            frame_flags |= FRAMEFLAGS_ERROR_RESILIENT;
        }
    }

    private static void set_ext_overrides(AV1_COMMON cm, EncodeFrameParams frame_params,
                                          ExternalFlags ext_flags)
    {
        // Overrides the defaults with the externally supplied values with
        // av1_update_reference() and av1_update_entropy() calls
        // Note: The overrides are valid only for the next frame passed
        // to av1_encode_lowlevel()

        if (ext_flags.use_s_frame)
        {
            frame_params.frame_type = S_FRAME;
        }

        if (ext_flags.refresh_frame_context_pending)
        {
            cm.features.refresh_frame_context = B(ext_flags.refresh_frame_context);
            ext_flags.refresh_frame_context_pending = false;
        }
        cm.features.allow_ref_frame_mvs = ext_flags.use_ref_frame_mvs;

        frame_params.error_resilient_mode = B(ext_flags.use_error_resilient);
        // A keyframe is already error resilient and keyframes with
        // error_resilient_mode interferes with the use of show_existing_frame
        // when forward reference keyframes are enabled.
        frame_params.error_resilient_mode &= B(frame_params.frame_type != KEY_FRAME);
        // For bitstream conformance, s-frames must be error-resilient
        frame_params.error_resilient_mode |= B(frame_params.frame_type == S_FRAME);
    }

    private static int choose_primary_ref_frame(AV1_COMP cpi, EncodeFrameParams frame_params)
    {
        AV1_COMMON cm = cpi.common;

        int intra_only = B(frame_params.frame_type == KEY_FRAME ||
                           frame_params.frame_type == INTRA_ONLY_FRAME);
        if (intra_only != 0 || frame_params.error_resilient_mode != 0 ||
            cpi.ext_flags.use_primary_ref_none)
        {
            return PRIMARY_REF_NONE;
        }

        // In large scale case, always use Last frame's frame contexts.
        // Note(yunqing): In other cases, primary_ref_frame is chosen based on
        // cpi->ppi->gf_group.layer_depth[cpi->gf_frame_index], which also controls
        // frame bit allocation.
        if (cm.tiles.large_scale != 0) return (LAST_FRAME - LAST_FRAME);

        // [subset] av1_svc_primary_ref_frame: needs use_svc or set_ref_frame_config (SVC
        // controls), neither of which the subset uses.
        if (cpi.ppi.use_svc != 0 || cpi.ppi.rtc_ref.set_ref_frame_config != 0)
            throw new NotSupportedException("SVC / external reference config not supported");

        // Find the most recent reference frame with the same reference type as the
        // current frame
        int current_ref_type = get_current_frame_ref_type(cpi);
        int wanted_fb = cpi.ppi.fb_of_context_type[current_ref_type];
        int primary_ref_frame = PRIMARY_REF_NONE;
        for (int ref_frame = LAST_FRAME; ref_frame <= ALTREF_FRAME; ref_frame++)
        {
            if (get_ref_frame_map_idx(cm, ref_frame) == wanted_fb)
            {
                primary_ref_frame = ref_frame - LAST_FRAME;
            }
        }

        return primary_ref_frame;
    }

    private static void adjust_frame_rate(AV1_COMP cpi, long ts_start, long ts_end)
    {
        TimeStamps time_stamps = cpi.time_stamps;
        long this_duration;
        int step = 0;

        // Clear down mmx registers

        if (is_one_pass_rt_params(cpi) != 0 ||
            (cpi.ppi.use_svc != 0 && cpi.ppi.rtc_ref.set_ref_frame_config != 0 &&
             cpi.svc.number_spatial_layers > 1))
        {
            // ts_start is the timestamp for the current frame and ts_end is the
            // expected next timestamp given the duration passed into codec_encode().
            // See the setting in encoder_encode() in av1_cx_iface.c:
            // ts_start = timebase_units_to_ticks(cpi_data.timestamp_ratio, ptsvol),
            // ts_end = timebase_units_to_ticks(cpi_data.timestamp_ratio, ptsvol +
            // duration). So the difference ts_end - ts_start is the duration passed
            // in by the user. For RTC or spatial layers SVC set the framerate based
            // directly on the duration, and bypass the adjustments below.
            this_duration = ts_end - ts_start;
            if (this_duration > 0)
            {
                cpi.new_framerate = 10000000.0 / this_duration;
                av1_new_framerate(cpi, cpi.new_framerate);
                time_stamps.prev_ts_start = ts_start;
                time_stamps.prev_ts_end = ts_end;
                return;
            }
        }

        if (ts_start == time_stamps.first_ts_start)
        {
            this_duration = ts_end - ts_start;
            step = 1;
        }
        else
        {
            long last_duration =
                time_stamps.prev_ts_end - time_stamps.prev_ts_start;

            this_duration = ts_end - time_stamps.prev_ts_end;

            // do a step update if the duration changes by 10%
            if (last_duration != 0)
                step = (int)((this_duration - last_duration) * 10 / last_duration);
        }

        if (this_duration != 0)
        {
            if (step != 0)
            {
                cpi.new_framerate = 10000000.0 / this_duration;
                av1_new_framerate(cpi, cpi.new_framerate);
            }
            else
            {
                // Average this frame's rate into the last second's average
                // frame rate. If we haven't seen 1 second yet, then average
                // over the whole interval seen.
                double interval =
                    Math.Min((double)(ts_end - time_stamps.first_ts_start), 10000000.0);
                double avg_duration = 10000000.0 / cpi.framerate;
                avg_duration *= (interval - avg_duration + this_duration);
                avg_duration /= interval;
                cpi.new_framerate = (10000000.0 / avg_duration);
                // For parallel frames update cpi->framerate with new_framerate
                // during av1_post_encode_updates()
                double framerate =
                    (cpi.ppi.gf_group.frame_parallel_level[cpi.gf_frame_index] > 0)
                        ? cpi.framerate
                        : cpi.new_framerate;
                av1_new_framerate(cpi, framerate);
            }
        }

        time_stamps.prev_ts_start = ts_start;
        time_stamps.prev_ts_end = ts_end;
    }

    // Check if we should encode an ARF or internal ARF.  If not, try a LAST
    // Do some setup associated with the chosen source
    // temporal_filtered, flush, and frame_update_type are outputs.
    // Return the frame source, or NULL if we couldn't find one
    private static lookahead_entry choose_frame_source(AV1_COMP cpi, ref int flush,
                                                       ref int pop_lookahead,
                                                       ref lookahead_entry last_source,
                                                       ref int show_frame)
    {
        AV1_COMMON cm = cpi.common;
        GF_GROUP gf_group = cpi.ppi.gf_group;
        lookahead_entry source = null;

        // Source index in lookahead buffer.
        int src_index = gf_group.arf_src_offset[cpi.gf_frame_index];

        // TODO(Aasaipriya): Forced key frames need to be fixed when rc_mode != AOM_Q
        // [subset] is_forced_keyframe_pending(): src_index (the ARF source offset) is always 0
        // in one-pass realtime, which has no ARFs.
        if (src_index != 0)
            throw new NotSupportedException("ARF source offsets are not supported");

        // If the current frame is arf, then we should not pop from the lookahead
        // buffer. If the current frame is not arf, then pop it. This assumes the
        // first frame in the GF group is not arf. May need to change if it is not
        // true.
        pop_lookahead = B(src_index == 0);
        // If this is a key frame and keyframe filtering is enabled with overlay,
        // then do not pop.
        if (pop_lookahead != 0 && cpi.oxcf.kf_cfg.enable_keyframe_filtering > 1 &&
            gf_group.update_type[cpi.gf_frame_index] == ARF_UPDATE &&
            is_stat_generation_stage(cpi) == 0 && cpi.ppi.lookahead != null)
        {
            if (cpi.ppi.lookahead.read_ctxs[cpi.compressor_stage].sz != 0 &&
                (flush != 0 ||
                 cpi.ppi.lookahead.read_ctxs[cpi.compressor_stage].sz ==
                     cpi.ppi.lookahead.read_ctxs[cpi.compressor_stage].pop_sz))
            {
                pop_lookahead = 0;
            }
        }

        // LAP stage does not have ARFs or forward key-frames,
        // hence, always pop_lookahead here.
        if (is_stat_generation_stage(cpi) != 0)
        {
            pop_lookahead = 1;
            src_index = 0;
        }

        show_frame = pop_lookahead;

        {
            // Future frame in parallel encode set
            if (gf_group.src_offset[cpi.gf_frame_index] != 0 &&
                is_stat_generation_stage(cpi) == 0)
                src_index = gf_group.src_offset[cpi.gf_frame_index];
        }
        if (show_frame != 0)
        {
            // show frame, pop from buffer
            // Get last frame source.
            if (cm.current_frame.frame_number > 0)
            {
                last_source = av1_lookahead_peek(cpi.ppi.lookahead, src_index - 1,
                                                 cpi.compressor_stage);
            }
            // Read in the source frame.
            source = av1_lookahead_peek(cpi.ppi.lookahead, src_index,
                                        cpi.compressor_stage);
        }
        else
        {
            // no show frames are arf frames
            source = av1_lookahead_peek(cpi.ppi.lookahead, src_index,
                                        cpi.compressor_stage);
            if (source != null)
            {
                cm.showable_frame = 1;
            }
        }
        return source;
    }

    // Don't allow a show_existing_frame to coincide with an error resilient or
    // S-Frame. An exception can be made in the case of a keyframe, since it does
    // not depend on any previous frames.
    private static int allow_show_existing(AV1_COMP cpi, uint frame_flags)
    {
        if (cpi.common.current_frame.frame_number == 0) return 0;

        lookahead_entry lookahead_src =
            av1_lookahead_peek(cpi.ppi.lookahead, 0, cpi.compressor_stage);
        if (lookahead_src == null) return 1;

        int is_error_resilient =
            B(cpi.oxcf.tool_cfg.error_resilient_mode ||
              (lookahead_src.flags & AOM_EFLAG_ERROR_RESILIENT) != 0);
        int is_s_frame = B(cpi.oxcf.kf_cfg.enable_sframe ||
                           (lookahead_src.flags & AOM_EFLAG_SET_S_FRAME) != 0);
        int is_key_frame =
            B((cpi.rc.frames_to_key == 0) || (frame_flags & FRAMEFLAGS_KEY) != 0);
        return B(!(is_error_resilient != 0 || is_s_frame != 0) || is_key_frame != 0);
    }

    // Update frame_flags to tell the encoder's caller what sort of frame was
    // encoded.
    private static void update_frame_flags(AV1_COMMON cm, in RefreshFrameInfo refresh_frame,
                                           ref uint frame_flags)
    {
        if (encode_show_existing_frame(cm) != 0)
        {
            frame_flags &= ~(uint)FRAMEFLAGS_GOLDEN;
            frame_flags &= ~(uint)FRAMEFLAGS_BWDREF;
            frame_flags &= ~(uint)FRAMEFLAGS_ALTREF;
            frame_flags &= ~(uint)FRAMEFLAGS_KEY;
            return;
        }

        if (refresh_frame.golden_frame)
        {
            frame_flags |= FRAMEFLAGS_GOLDEN;
        }
        else
        {
            frame_flags &= ~(uint)FRAMEFLAGS_GOLDEN;
        }

        if (refresh_frame.alt_ref_frame)
        {
            frame_flags |= FRAMEFLAGS_ALTREF;
        }
        else
        {
            frame_flags &= ~(uint)FRAMEFLAGS_ALTREF;
        }

        if (refresh_frame.bwd_ref_frame)
        {
            frame_flags |= FRAMEFLAGS_BWDREF;
        }
        else
        {
            frame_flags &= ~(uint)FRAMEFLAGS_BWDREF;
        }

        if (cm.current_frame.frame_type == KEY_FRAME)
        {
            frame_flags |= FRAMEFLAGS_KEY;
        }
        else
        {
            frame_flags &= ~(uint)FRAMEFLAGS_KEY;
        }
    }

    internal static int av1_get_refresh_frame_flags(
        AV1_COMP cpi, EncodeFrameParams frame_params,
        FRAME_UPDATE_TYPE frame_update_type, int gf_index, int cur_disp_order,
        RefFrameMapPair[] ref_frame_map_pairs)
    {
        AV1_COMMON cm = cpi.common;
        ref ExtRefreshFrameFlagsInfo ext_refresh_frame_flags =
            ref cpi.ext_flags.refresh_frame;

        GF_GROUP gf_group = cpi.ppi.gf_group;
        if (gf_group.refbuf_state[gf_index] == REFBUF_RESET)
            return SELECT_ALL_BUF_SLOTS;

        // TODO(jingning): Deprecate the following operations.
        // Switch frames and shown key-frames overwrite all reference slots
        if (frame_params.frame_type == S_FRAME) return SELECT_ALL_BUF_SLOTS;

        // show_existing_frames don't actually send refresh_frame_flags so set the
        // flags to 0 to keep things consistent.
        if (frame_params.show_existing_frame != 0) return 0;

        RTC_REF rtc_ref = cpi.ppi.rtc_ref;
        if (is_frame_droppable(rtc_ref, in ext_refresh_frame_flags) != 0) return 0;

        // [subset] The external rate control GOP decision (get_new_fb_map_idx_rc):
        // ext_ratectrl.ready is never set.
        Debug.Assert(cpi.ext_ratectrl.ready == 0);

        int refresh_mask = 0;
        if (ext_refresh_frame_flags.update_pending)
        {
            if (rtc_ref.set_ref_frame_config != 0 ||
                use_rtc_reference_structure_one_layer(cpi) != 0)
            {
                // refresh[] is indexed by buffer slot. A refreshed slot does not have to
                // be pointed to by any of the ref_idx[] entries.
                for (int i = 0; i < REF_FRAMES; i++)
                {
                    refresh_mask |= rtc_ref.refresh[i] << i;
                }
                return refresh_mask;
            }
            // [subset] The old refresh_*_frame flag overrides without the RTC reference
            // structure: one-pass realtime with one layer always uses it.
            throw new NotSupportedException("reference refresh overrides need the RTC structure");
        }

        // [subset] GOP based slot selection (get_free_ref_map_index / get_refresh_idx): the
        // RTC reference structure always sets update_pending.
        throw new NotSupportedException("GOP based reference refresh is not supported");
    }

    internal static int av1_encode_strategy(AV1_COMP cpi, ref int size, Ptr<byte> dest,
                                            int dest_size, ref uint frame_flags,
                                            ref long time_stamp, ref long time_end,
                                            aom_rational64_t timestamp_ratio,
                                            ref int pop_lookahead, int flush)
    {
        AV1EncoderConfig oxcf = cpi.oxcf;
        AV1_COMMON cm = cpi.common;
        GF_GROUP gf_group = cpi.ppi.gf_group;
        ExternalFlags ext_flags = cpi.ext_flags;
        ref GFConfig gf_cfg = ref oxcf.gf_cfg;

        EncodeFrameInput frame_input = new EncodeFrameInput();
        EncodeFrameParams frame_params = new EncodeFrameParams();
        int frame_size;
        frame_size = 0;

        // Check if we need to stuff more src frames
        if (flush == 0)
        {
            int srcbuf_size =
                av1_lookahead_depth(cpi.ppi.lookahead, cpi.compressor_stage);
            int pop_size =
                av1_lookahead_pop_sz(cpi.ppi.lookahead, cpi.compressor_stage);

            // Continue buffering look ahead buffer.
            if (srcbuf_size < pop_size) return -1;
        }

        if (av1_lookahead_peek(cpi.ppi.lookahead, 0, cpi.compressor_stage) == null)
        {
            return -1;
        }

        if (has_no_stats_stage(cpi) != 0 && is_one_pass_rt_lag_params(cpi) == 0)
        {
            gf_cfg.gf_max_pyr_height =
                Math.Min(gf_cfg.gf_max_pyr_height, USE_ALTREF_FOR_ONE_PASS);
            gf_cfg.gf_min_pyr_height =
                Math.Min(gf_cfg.gf_min_pyr_height, gf_cfg.gf_max_pyr_height);
        }

        // Allocation of mi buffers.
        alloc_mb_mode_info_buffers(cpi);

        cpi.cb_delta_rdmult_enabled = 0;

        cpi.skip_tpl_setup_stats = 0;

        if (is_stat_generation_stage(cpi) == 0)
        {
            // TODO(jingning): fwd key frame always uses show existing frame?
            if (gf_group.update_type[cpi.gf_frame_index] == OVERLAY_UPDATE &&
                gf_group.refbuf_state[cpi.gf_frame_index] == REFBUF_RESET)
            {
                frame_params.show_existing_frame = 1;
            }
            else
            {
                frame_params.show_existing_frame =
                    B((cpi.ppi.show_existing_alt_ref != 0 &&
                       gf_group.update_type[cpi.gf_frame_index] == OVERLAY_UPDATE) ||
                      gf_group.update_type[cpi.gf_frame_index] == INTNL_OVERLAY_UPDATE);
            }
            frame_params.show_existing_frame &= allow_show_existing(cpi, frame_flags);

            // Special handling to reset 'show_existing_frame' in case of dropped
            // frames.
            if (oxcf.rc_cfg.drop_frames_water_mark != 0 &&
                (gf_group.update_type[cpi.gf_frame_index] == OVERLAY_UPDATE ||
                 gf_group.update_type[cpi.gf_frame_index] == INTNL_OVERLAY_UPDATE))
            {
                // During the encode of an OVERLAY_UPDATE/INTNL_OVERLAY_UPDATE frame, loop
                // over the gf group to check if the corresponding
                // ARF_UPDATE/INTNL_ARF_UPDATE frame was dropped.
                int cur_disp_idx = gf_group.display_idx[cpi.gf_frame_index];
                for (int idx = 0; idx < cpi.gf_frame_index; idx++)
                {
                    if (cur_disp_idx == gf_group.display_idx[idx])
                    {
                        Debug.Assert(gf_group.update_type[cpi.gf_frame_index] != OVERLAY_UPDATE ||
                                     gf_group.update_type[idx] == ARF_UPDATE);
                        Debug.Assert(gf_group.update_type[cpi.gf_frame_index] !=
                                         INTNL_OVERLAY_UPDATE ||
                                     gf_group.update_type[idx] == INTNL_ARF_UPDATE);
                        // Reset show_existing_frame and set cpi->is_dropped_frame to true if
                        // the frame was dropped during its first encode.
                        if (gf_group.is_frame_dropped[idx])
                        {
                            frame_params.show_existing_frame = 0;
                            Debug.Assert(!cpi.is_dropped_frame);
                            cpi.is_dropped_frame = true;
                        }
                        break;
                    }
                }
            }

            // Reset show_existing_alt_ref decision to 0 after it is used.
            if (gf_group.update_type[cpi.gf_frame_index] == OVERLAY_UPDATE)
            {
                cpi.ppi.show_existing_alt_ref = 0;
            }
        }
        else
        {
            frame_params.show_existing_frame = 0;
        }

        lookahead_entry source = null;
        lookahead_entry last_source = null;
        if (frame_params.show_existing_frame != 0)
        {
            source = av1_lookahead_peek(cpi.ppi.lookahead, 0, cpi.compressor_stage);
            pop_lookahead = 1;
            frame_params.show_frame = 1;
        }
        else
        {
            source = choose_frame_source(cpi, ref flush, ref pop_lookahead, ref last_source,
                                         ref frame_params.show_frame);
        }

        if (source == null)
        {  // If no source was found, we can't encode a frame.
            return -1;
        }

        // reset src_offset to allow actual encode call for this frame to get its
        // source.
        gf_group.src_offset[cpi.gf_frame_index] = 0;

        // Source may be changed if temporal filtered later.
        frame_input.source = source.img;
        if ((cpi.ppi.use_svc != 0 || cpi.rc.prev_frame_is_dropped != 0) &&
            last_source != null)
            av1_svc_set_last_source(cpi, frame_input, last_source.img);
        else
            frame_input.last_source = last_source != null ? last_source.img : null;
        frame_input.ts_duration = source.ts_end - source.ts_start;
        // Save unfiltered source. It is used in av1_get_second_pass_params().
        cpi.unfiltered_source = frame_input.source;

        time_stamp = source.ts_start;
        time_end = source.ts_end;
        if (source.ts_start < cpi.time_stamps.first_ts_start)
        {
            cpi.time_stamps.first_ts_start = source.ts_start;
            cpi.time_stamps.prev_ts_end = source.ts_start;
        }

        av1_apply_encoding_flags(cpi, source.flags);
        frame_flags = (source.flags & AOM_EFLAG_FORCE_KF) != 0 ? (uint)FRAMEFLAGS_KEY : 0;

        // Shown frames and arf-overlay frames need frame-rate considering
        if (frame_params.show_frame != 0)
            adjust_frame_rate(cpi, source.ts_start, source.ts_end);

        if (frame_params.show_existing_frame == 0)
        {
            if (cm.seq_params.decoder_model_info_present_flag != 0 &&
                cm.seq_params.timing_info.equal_picture_interval == 0)
            {
                // only one operating point supported now
                long pts64 =
                    ticks_to_timebase_units(timestamp_ratio, time_stamp);
                int frame_presentation_time_length =
                    cm.seq_params.decoder_model_info.frame_presentation_time_length;
                long pts_mask = (1L << frame_presentation_time_length) - 1L;
                cm.frame_presentation_time = (uint)(pts64 & pts_mask);
            }
        }

        av1_get_one_pass_rt_params(cpi, ref frame_params.frame_type, frame_input,
                                   frame_flags);
        if (use_rtc_reference_structure_one_layer(cpi) != 0)
            av1_set_rtc_reference_structure_one_layer(cpi, B(cpi.gf_frame_index == 0));

        FRAME_UPDATE_TYPE frame_update_type =
            get_frame_update_type(gf_group, cpi.gf_frame_index);

        if (frame_params.show_existing_frame != 0 &&
            frame_params.frame_type != KEY_FRAME)
        {
            // Force show-existing frames to be INTER, except forward keyframes
            frame_params.frame_type = INTER_FRAME;
        }

        // Per-frame encode speed.  In theory this can vary, but things may have
        // been written assuming speed-level will not change within a sequence, so
        // this parameter should be used with caution.
        frame_params.speed = oxcf.speed;

        // Work out some encoding parameters specific to the pass:
        if (has_no_stats_stage(cpi) != 0 && oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ)
        {
            av1_cyclic_refresh_update_parameters(cpi);
        }
        else if (is_stat_generation_stage(cpi) != 0)
        {
            // [subset] First pass (lossless request): no two-pass encoding.
            throw new NotSupportedException("first pass is not supported");
        }

        if (is_stat_generation_stage(cpi) == 0)
            set_ext_overrides(cm, frame_params, ext_flags);

        // Shown keyframes and S frames refresh all reference buffers
        int force_refresh_all =
            B(((frame_params.frame_type == KEY_FRAME && frame_params.show_frame != 0) ||
               frame_params.frame_type == S_FRAME) &&
              frame_params.show_existing_frame == 0);

        av1_configure_buffer_updates(
            cpi, ref frame_params.refresh_frame, frame_update_type,
            gf_group.refbuf_state[cpi.gf_frame_index], force_refresh_all);

        if (is_stat_generation_stage(cpi) == 0)
        {
            YV12_BUFFER_CONFIG[] ref_frame_buf = new YV12_BUFFER_CONFIG[INTER_REFS_PER_FRAME];

            RefFrameMapPair[] ref_frame_map_pairs = new RefFrameMapPair[REF_FRAMES];
            init_ref_map_pair(cpi, ref_frame_map_pairs);
            int order_offset = gf_group.arf_src_offset[cpi.gf_frame_index];
            int cur_frame_disp =
                (int)cpi.common.current_frame.frame_number + order_offset;

            int get_ref_frames = 0;
            if (get_ref_frames != 0 ||
                gf_group.frame_parallel_level[cpi.gf_frame_index] == 0)
            {
                if (!ext_flags.refresh_frame.update_pending)
                {
                    // [subset] av1_get_ref_frames (GOP based reference mapping): the RTC
                    // reference structure always sets update_pending.
                    throw new NotSupportedException("GOP based reference mapping is not supported");
                }
                else if (cpi.ppi.rtc_ref.set_ref_frame_config != 0 ||
                         use_rtc_reference_structure_one_layer(cpi) != 0)
                {
                    // With set_ref_frame_config, the ref_idx[] entries of references
                    // that are not in use are don't-cares, and may point to slots that
                    // are not valid references for this frame, e.g. a slot of another
                    // spatial layer. All seven references are signaled in the bitstream
                    // though, and decoders reject a frame with an invalid reference.
                    // Without order hints, unused references otherwise only affect how
                    // the frame size is signaled, so point them to the first reference in
                    // use. With order hints, they also affect the decoding process (e.g.
                    // temporal motion vector projection), so leave them as configured.
                    int first_ref_map_idx = INVALID_IDX;
                    if (cpi.ppi.rtc_ref.set_ref_frame_config != 0 &&
                        cm.seq_params.order_hint_info.enable_order_hint == 0)
                    {
                        for (int i = 0; i < INTER_REFS_PER_FRAME; i++)
                        {
                            if (cpi.ppi.rtc_ref.reference[i] != 0)
                            {
                                first_ref_map_idx = cpi.ppi.rtc_ref.ref_idx[i];
                                break;
                            }
                        }
                    }
                    for (int i = 0; i < INTER_REFS_PER_FRAME; i++)
                    {
                        cm.remapped_ref_idx[i] = (first_ref_map_idx != INVALID_IDX &&
                                                  cpi.ppi.rtc_ref.reference[i] == 0)
                                                     ? first_ref_map_idx
                                                     : cpi.ppi.rtc_ref.ref_idx[i];
                    }
                }
            }

            // Get the reference frames
            bool has_ref_frames = false;
            for (int i = 0; i < INTER_REFS_PER_FRAME; ++i)
            {
                RefCntBuffer ref_frame =
                    get_ref_frame_buf(cm, ref_frame_priority_order[i]);
                ref_frame_buf[i] = ref_frame != null ? ref_frame.buf : null;
                if (ref_frame != null) has_ref_frames = true;
            }
            if (!has_ref_frames && (frame_params.frame_type == INTER_FRAME ||
                                    frame_params.frame_type == S_FRAME))
            {
                return AOM_CODEC_ERROR;
            }

            // Work out which reference frame slots may be used.
            frame_params.ref_frame_flags =
                get_ref_frame_flags(cpi.sf, is_one_pass_rt_params(cpi), ref_frame_buf,
                                    ext_flags.ref_frame_flags);

            // Set primary_ref_frame of non-reference frames as PRIMARY_REF_NONE.
            if (cpi.ppi.gf_group.is_frame_non_ref[cpi.gf_frame_index])
            {
                frame_params.primary_ref_frame = PRIMARY_REF_NONE;
            }
            else
            {
                frame_params.primary_ref_frame =
                    choose_primary_ref_frame(cpi, frame_params);
            }

            frame_params.order_offset = gf_group.arf_src_offset[cpi.gf_frame_index];

            // Call av1_get_refresh_frame_flags() if refresh index not available.
            if (!cpi.refresh_idx_available)
            {
                frame_params.refresh_frame_flags = av1_get_refresh_frame_flags(
                    cpi, frame_params, frame_update_type, cpi.gf_frame_index,
                    cur_frame_disp, ref_frame_map_pairs);
            }
            else
            {
                Debug.Assert(cpi.ref_refresh_index != INVALID_IDX);
                frame_params.refresh_frame_flags = (1 << cpi.ref_refresh_index);
            }

            // Make the frames marked as is_frame_non_ref to non-reference frames.
            if (gf_group.is_frame_non_ref[cpi.gf_frame_index])
                frame_params.refresh_frame_flags = 0;

            frame_params.existing_fb_idx_to_show = INVALID_IDX;
            // Find the frame buffer to show based on display order.
            if (frame_params.show_existing_frame != 0)
            {
                for (int frame = 0; frame < REF_FRAMES; frame++)
                {
                    RefCntBuffer buf = cm.ref_frame_map[frame];
                    if (buf == null) continue;
                    int frame_order = (int)buf.display_order_hint;
                    if (frame_order == cur_frame_disp)
                        frame_params.existing_fb_idx_to_show = frame;
                }
            }
        }

        // The way frame_params->remapped_ref_idx is setup is a placeholder.
        // Currently, reference buffer assignment is done by update_ref_frame_map()
        // which is called by high-level strategy AFTER encoding a frame.  It
        // modifies cm->remapped_ref_idx.  If you want to use an alternative method
        // to determine reference buffer assignment, just put your assignments into
        // frame_params->remapped_ref_idx here and they will be used when encoding
        // this frame.  If frame_params->remapped_ref_idx is setup independently of
        // cm->remapped_ref_idx then update_ref_frame_map() will have no effect.
        Array.Copy(cm.remapped_ref_idx, frame_params.remapped_ref_idx, REF_FRAMES);

        cpi.td.mb.rdmult_delta_qindex = cpi.td.mb.delta_qindex = 0;

        if (frame_params.show_existing_frame == 0)
        {
            cm.quant_params.using_qmatrix = oxcf.q_cfg.using_qm;
        }

        int is_intra_frame = B(frame_params.frame_type == KEY_FRAME ||
                               frame_params.frame_type == INTRA_ONLY_FRAME);
        FeatureFlags features = cm.features;
        if (is_stat_generation_stage(cpi) == 0 &&
            (oxcf.pass == AOM_RC_ONE_PASS || oxcf.pass >= AOM_RC_SECOND_PASS) &&
            is_intra_frame != 0)
        {
            av1_set_screen_content_options(cpi, features);
        }

        if (av1_encode(cpi, dest, dest_size, frame_input, frame_params,
                       ref frame_size) != AOM_CODEC_OK)
        {
            return AOM_CODEC_ERROR;
        }

        // This is used in rtc temporal filter case. Use true source in the PSNR
        // calculation.
        // [subset] PSNR calculation (b_calculate_psnr) is never enabled.
        Debug.Assert(!(is_psnr_calc_enabled(cpi) != 0 && cpi.sf.rt_sf.use_rtc_tf != 0));

        if (is_stat_generation_stage(cpi) == 0)
        {
            // First pass doesn't modify reference buffer assignment or produce frame
            // flags
            update_frame_flags(cpi.common, in cpi.refresh_frame, ref frame_flags);
            set_additional_frame_flags(cm, ref frame_flags);
        }

        size = frame_size;

        // Leave a signal for a higher level caller about if this frame is droppable
        if (size > 0)
        {
            cpi.droppable =
                is_frame_droppable(cpi.ppi.rtc_ref, in ext_flags.refresh_frame);
        }

        // For SVC, or when frame-dropper is enabled:
        // keep track of the (unscaled) source corresponding to the refresh of LAST
        // reference (base temporal layer - TL0). Copy only for the
        // top spatial enhancement layer so all spatial layers of the next
        // superframe have last_source to be aligned with previous TL0 superframe.
        // Avoid cases where resolution changes for unscaled source (top spatial
        // layer). Only needs to be done for frame that are encoded (size > 0).
        if (size > 0 &&
            (cpi.ppi.use_svc != 0 || cpi.oxcf.rc_cfg.drop_frames_water_mark > 0) &&
            cpi.svc.spatial_layer_id == cpi.svc.number_spatial_layers - 1 &&
            cpi.svc.temporal_layer_id == 0 &&
            cpi.unscaled_source.y_width == cpi.svc.source_last_TL0.y_width &&
            cpi.unscaled_source.y_height == cpi.svc.source_last_TL0.y_height)
        {
            aom_yv12_copy_y(cpi.unscaled_source, cpi.svc.source_last_TL0, 1);
            aom_yv12_copy_u(cpi.unscaled_source, cpi.svc.source_last_TL0, 1);
            aom_yv12_copy_v(cpi.unscaled_source, cpi.svc.source_last_TL0, 1);
        }

        return AOM_CODEC_OK;
    }
}
