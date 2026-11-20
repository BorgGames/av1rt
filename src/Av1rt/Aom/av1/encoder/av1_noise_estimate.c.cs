// Port of libaom av1/encoder/av1_noise_estimate.c

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

// [subset] CONFIG_AV1_TEMPORAL_DENOISING == 0: noise_est_svc, copy_frame and every denoiser hook
// are compiled out; CONFIG_AV1_HIGHBITDEPTH == 0: the use_highbitdepth early return is gone.
internal static partial class av1_noise_estimate
{
    internal static void av1_noise_estimate_init(NOISE_ESTIMATE ne, int width, int height)
    {
        long area = (long)width * height;
        ne.enabled = 0;
        ne.level = (area < 1280 * 720) ? kLowLow : kLow;
        ne.value = 0;
        ne.count = 0;
        ne.thresh = 90;
        ne.last_w = 0;
        ne.last_h = 0;
        if (area >= 1920 * 1080)
        {
            ne.thresh = 200;
        }
        else if (area >= 1280 * 720)
        {
            ne.thresh = 140;
        }
        else if (area >= 640 * 360)
        {
            ne.thresh = 115;
        }
        ne.num_frames_estimate = 15;
        ne.adapt_thresh = (3 * ne.thresh) >> 1;
    }

    private static int enable_noise_estimation(AV1_COMP cpi)
    {
        int resize_pending = is_frame_resize_pending(cpi);

        // Only allow noise estimate under certain encoding mode.
        // Enabled for 1 pass CBR, speed >=5, and if resolution is same as original.
        // Not enabled for SVC mode and screen_content_mode.
        // Not enabled for low resolutions.
        if (cpi.oxcf.pass == AOM_RC_ONE_PASS && cpi.oxcf.rc_cfg.mode == AOM_CBR &&
            cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ && cpi.oxcf.speed >= 5 &&
            resize_pending == 0 && cpi.ppi.use_svc == 0 &&
            cpi.oxcf.tune_cfg.content != AOM_CONTENT_SCREEN &&
            cpi.common.width * cpi.common.height >= 640 * 360)
            return 1;
        else
            return 0;
    }

    internal static NOISE_LEVEL av1_noise_estimate_extract_level(NOISE_ESTIMATE ne)
    {
        int noise_level = kLowLow;
        if (ne.value > (ne.thresh << 1))
        {
            noise_level = kHigh;
        }
        else
        {
            if (ne.value > ne.thresh)
                noise_level = kMedium;
            else if (ne.value > (ne.thresh >> 1))
                noise_level = kLow;
            else
                noise_level = kLowLow;
        }
        return noise_level;
    }

    internal static void av1_update_noise_estimate(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        CommonModeInfoParams mi_params = cm.mi_params;

        NOISE_ESTIMATE ne = cpi.noise_estimate;
        int low_res = B(cm.width <= 352 && cm.height <= 288);
        // Estimate of noise level every frame_period frames.
        int frame_period = 8;
        int thresh_consec_zeromv = 2;
        int frame_counter = (int)cm.current_frame.frame_number;
        // Estimate is between current source and last source.
        YV12_BUFFER_CONFIG last_source = cpi.last_source;
        ne.enabled = enable_noise_estimation(cpi);
        if (cpi.svc.number_spatial_layers > 1)
            frame_counter = (int)cpi.svc.current_superframe;
        if (ne.enabled == 0 || frame_counter % frame_period != 0 ||
            last_source == null ||
            (cpi.svc.number_spatial_layers == 1 &&
             (ne.last_w != cm.width || ne.last_h != cm.height)))
        {
            if (last_source != null)
            {
                ne.last_w = cm.width;
                ne.last_h = cm.height;
            }
            return;
        }
        else if (frame_counter > 60 && cpi.svc.num_encoded_top_layer > 1 &&
                 cpi.rc.frames_since_key > cpi.svc.number_spatial_layers &&
                 cpi.svc.spatial_layer_id == cpi.svc.number_spatial_layers - 1 &&
                 cpi.rc.avg_frame_low_motion < (low_res != 0 ? 60 : 40))
        {
            // Force noise estimation to 0 and denoiser off if content has high motion.
            ne.level = kLowLow;
            ne.count = 0;
            ne.num_frames_estimate = 10;
            return;
        }
        else
        {
            uint bin_size = 100;
            Span<uint> hist = stackalloc uint[MAX_VAR_HIST_BINS];  // = { 0 }
            Span<uint> hist_avg = stackalloc uint[MAX_VAR_HIST_BINS];
            uint max_bin = 0;
            uint max_bin_count = 0;
            uint bin_cnt;
            BLOCK_SIZE bsize = BLOCK_16X16;
            // Loop over sub-sample of 16x16 blocks of frame, and for blocks that have
            // been encoded as zero/small mv at least x consecutive frames, compute
            // the variance to update estimate of noise in the source.
            Ptr<byte> src_y = cpi.source.y_buffer;
            int src_ystride = cpi.source.y_stride;
            Ptr<byte> last_src_y = last_source.y_buffer;
            int last_src_ystride = last_source.y_stride;
            int mi_row, mi_col;
            int num_low_motion = 0;
            int frame_low_motion = 1;
            for (mi_row = 0; mi_row < mi_params.mi_rows; mi_row += 2)
            {
                for (mi_col = 0; mi_col < mi_params.mi_cols; mi_col += 2)
                {
                    int bl_index =
                        (mi_row >> 1) * (mi_params.mi_cols >> 1) + (mi_col >> 1);
                    if (cpi.consec_zero_mv[bl_index] > thresh_consec_zeromv)
                        num_low_motion++;
                }
            }
            if (num_low_motion <
                (((3 * (mi_params.mi_rows * mi_params.mi_cols) >> 2)) >> 3))
                frame_low_motion = 0;
            // [port] C walks every mi position, advancing src_y/last_src_y by 4 pixels, and only
            // samples where mi_row and mi_col are multiples of 8; this visits those positions
            // directly (same blocks, same order).
            for (mi_row = 0; mi_row < mi_params.mi_rows; mi_row += 8)
            {
                for (mi_col = 0; mi_col < mi_params.mi_cols; mi_col += 8)
                {
                    // 16x16 blocks, 1/4 sample of frame.
                    if (mi_row < mi_params.mi_rows - 3 &&
                        mi_col < mi_params.mi_cols - 3)
                    {
                        src_y = cpi.source.y_buffer + (mi_row * 4) * src_ystride + mi_col * 4;
                        last_src_y = last_source.y_buffer + (mi_row * 4) * last_src_ystride + mi_col * 4;
                        int bl_index =
                            (mi_row >> 1) * (mi_params.mi_cols >> 1) + (mi_col >> 1);
                        int bl_index1 = bl_index + 1;
                        int bl_index2 = bl_index + (mi_params.mi_cols >> 1);
                        int bl_index3 = bl_index2 + 1;
                        int consec_zeromv =
                            AOMMIN(cpi.consec_zero_mv[bl_index],
                                   AOMMIN(cpi.consec_zero_mv[bl_index1],
                                          AOMMIN(cpi.consec_zero_mv[bl_index2],
                                                 cpi.consec_zero_mv[bl_index3])));
                        // Only consider blocks that are likely steady background. i.e, have
                        // been encoded as zero/low motion x (= thresh_consec_zeromv) frames
                        // in a row. consec_zero_mv[] defined for 8x8 blocks, so consider all
                        // 4 sub-blocks for 16x16 block. And exclude this frame if
                        // high_source_sad is true (i.e., scene/content change).
                        if (frame_low_motion != 0 && consec_zeromv > thresh_consec_zeromv &&
                            cpi.rc.high_source_sad == 0)
                        {
                            uint sse = 0;
                            // Compute variance between co-located blocks from current and
                            // last input frames.
                            uint variance = cpi.ppi.fn_ptr[bsize].vf(
                                src_y, src_ystride, last_src_y, last_src_ystride, ref sse);
                            uint hist_index = variance / bin_size;
                            if (hist_index < MAX_VAR_HIST_BINS)
                                hist[(int)hist_index]++;
                            else if (hist_index < 3 * (MAX_VAR_HIST_BINS >> 1))
                                hist[MAX_VAR_HIST_BINS - 1]++;  // Account for the tail
                        }
                    }
                }
            }
            ne.last_w = cm.width;
            ne.last_h = cm.height;
            // Adjust histogram to account for effect that histogram flattens
            // and shifts to zero as scene darkens.
            if (hist[0] > 10 && (hist[MAX_VAR_HIST_BINS - 1] > hist[0] >> 2))
            {
                hist[0] = 0;
                hist[1] >>= 2;
                hist[2] >>= 2;
                hist[3] >>= 2;
                hist[4] >>= 1;
                hist[5] >>= 1;
                hist[6] = 3 * hist[6] >> 1;
                hist[MAX_VAR_HIST_BINS - 1] >>= 1;
            }

            // Average hist[] and find largest bin
            for (bin_cnt = 0; bin_cnt < MAX_VAR_HIST_BINS; bin_cnt++)
            {
                int b = (int)bin_cnt;  // [port] span index for hist[bin_cnt]
                if (bin_cnt == 0)
                    hist_avg[b] = (hist[0] + hist[1] + hist[2]) / 3;
                else if (bin_cnt == MAX_VAR_HIST_BINS - 1)
                    hist_avg[b] = hist[MAX_VAR_HIST_BINS - 1] >> 2;
                else if (bin_cnt == MAX_VAR_HIST_BINS - 2)
                    hist_avg[b] = (hist[b - 1] + 2 * hist[b] +
                                   (hist[b + 1] >> 1) + 2) >>
                                  2;
                else
                    hist_avg[b] =
                        (hist[b - 1] + 2 * hist[b] + hist[b + 1] + 2) >>
                        2;

                if (hist_avg[b] > max_bin_count)
                {
                    max_bin_count = hist_avg[b];
                    max_bin = bin_cnt;
                }
            }
            // Scale by 40 to work with existing thresholds
            ne.value = (int)(((uint)(3 * ne.value) + max_bin * 40) >> 2);
            // Quickly increase VNR strength when the noise level increases suddenly.
            if (ne.level < kMedium && ne.value > ne.adapt_thresh)
            {
                ne.count = ne.num_frames_estimate;
            }
            else
            {
                ne.count++;
            }
            if (ne.count == ne.num_frames_estimate)
            {
                // Reset counter and check noise level condition.
                ne.num_frames_estimate = 30;
                ne.count = 0;
                ne.level = av1_noise_estimate_extract_level(ne);
            }
        }
    }
}
