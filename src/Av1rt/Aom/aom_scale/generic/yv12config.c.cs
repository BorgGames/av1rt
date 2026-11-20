// Port of libaom aom_scale/generic/yv12config.c

using System.Diagnostics;

namespace Av1rt.Aom;

/* ***************************************************************************
 *  Exports
 ****************************************************************************/

/* ***************************************************************************
 *
 ****************************************************************************/

// [subset] aom_copy_metadata_to_frame_buffer never runs in the subset and is not ported. The
// external frame buffer callback path of realloc_frame_buffer_aligned (cb != NULL) never runs
// either (the encoder always passes NULL); it is kept as an assertion.
internal static partial class yv12config
{
    // TODO(jkoleszar): Maybe replace this with struct aom_image
    internal static int aom_free_frame_buffer(YV12_BUFFER_CONFIG ybf)
    {
        if (ybf != null)
        {
            if (ybf.buffer_alloc_sz > 0)
            {
                ybf.buffer_alloc = null;  // aom_free
            }
            aom_remove_metadata_from_frame_buffer(ybf);
            /* buffer_alloc isn't accessed by most functions.  Rather y_buffer,
              u_buffer and v_buffer point to buffer_alloc and are used.  Clear out
              all of this so that a freed pointer isn't inadvertently used */
            ybf.Clear();
            return 0;
        }

        return AOM_CODEC_MEM_ERROR;
    }

    private static int realloc_frame_buffer_aligned(
        YV12_BUFFER_CONFIG ybf, int width, int height, int ss_x, int ss_y,
        int use_highbitdepth, int border, int byte_alignment,
        aom_codec_frame_buffer_t fb, aom_get_frame_buffer_cb_fn_t cb,
        object cb_priv, int y_stride, ulong yplane_size,
        ulong uvplane_size, int aligned_width,
        int aligned_height, int uv_width, int uv_height,
        int uv_stride, int uv_border_w, int uv_border_h,
        bool alloc_pyramid, int alloc_y_plane_only)
    {
        if (ybf != null)
        {
            int aom_byte_align = (byte_alignment == 0) ? 1 : byte_alignment;
            ulong frame_size =
                (ulong)(1 + use_highbitdepth) * (yplane_size + 2 * uvplane_size);

            Ptr<byte> buf = Ptr<byte>.Null;

            // CONFIG_REALTIME_ONLY:
            // We should only need an 8-bit version of the source frame if we are
            // encoding in non-realtime mode
            Debug.Assert(!alloc_pyramid);

            // The size of ybf->buffer_alloc.
            ulong alloc_size = frame_size;
            // The decoder may allocate REF_FRAMES frame buffers in the frame buffer
            // pool. Bound the total amount of allocated memory as if these REF_FRAMES
            // frame buffers were allocated in a single allocation.
            if (alloc_size > AOM_MAX_ALLOCABLE_MEMORY / REF_FRAMES)
                return AOM_CODEC_MEM_ERROR;

            if (cb != null)
            {
                // [subset] External frame buffers are a decoder feature; the encoder always passes
                // cb = NULL, so this path is not ported.
                Debug.Assert(fb != null);
                Debug.Fail("aom_realloc_frame_buffer: external frame buffer callbacks are not ported");
                return AOM_CODEC_MEM_ERROR;
            }
            else if (frame_size > (ulong)ybf.buffer_alloc_sz)
            {
                // Allocation to hold larger frame, or first allocation.
                ybf.buffer_alloc = null;  // aom_free
                ybf.buffer_alloc_sz = 0;

                // [port] C checks that frame_size fits size_t; managed arrays are int-indexed.
                if (frame_size > int.MaxValue) return AOM_CODEC_MEM_ERROR;

                // [port] aom_memalign(32, frame_size): a new array starts at offset 0, which counts
                // as 32-byte aligned (see aom_align_addr).
                ybf.buffer_alloc = new byte[(int)frame_size];

                ybf.buffer_alloc_sz = (int)frame_size;

                // This memset is needed for fixing valgrind error from C loop filter
                // due to access uninitialized memory in frame border. It could be
                // removed if border is totally removed.
                // [port] new byte[] is already zeroed.
            }

            ybf.y_crop_width = width;
            ybf.y_crop_height = height;
            ybf.y_width = aligned_width;
            ybf.y_height = aligned_height;
            ybf.y_stride = y_stride;

            ybf.uv_crop_width = (width + ss_x) >> ss_x;
            ybf.uv_crop_height = (height + ss_y) >> ss_y;
            ybf.uv_width = uv_width;
            ybf.uv_height = uv_height;
            ybf.uv_stride = uv_stride;

            ybf.border = border;
            ybf.frame_size = (int)frame_size;
            ybf.subsampling_x = ss_x;
            ybf.subsampling_y = ss_y;

            buf = ybf.buffer_alloc;
            if (use_highbitdepth != 0)
            {
                // Store uint16 addresses when using 16bit framebuffers
                // [subset] CONFIG_AV1_HIGHBITDEPTH is 0: CONVERT_TO_BYTEPTR does not exist.
                Debug.Fail("aom_realloc_frame_buffer: high bitdepth frame buffers are not ported");
                ybf.flags = YV12_FLAG_HIGHBITDEPTH;
            }
            else
            {
                ybf.flags = 0;
            }

            ybf.y_buffer = aom_align_addr(buf + (border * y_stride) + border, aom_byte_align);
            if (alloc_y_plane_only == 0)
            {
                ybf.u_buffer = aom_align_addr(
                    buf + (int)yplane_size + (uv_border_h * uv_stride) + uv_border_w,
                    aom_byte_align);
                ybf.v_buffer =
                    aom_align_addr(buf + (int)yplane_size + (int)uvplane_size +
                                       (uv_border_h * uv_stride) + uv_border_w,
                                   aom_byte_align);
            }
            else
            {
                ybf.u_buffer = Ptr<byte>.Null;
                ybf.v_buffer = Ptr<byte>.Null;
            }

            ybf.use_external_reference_buffers = 0;

            ybf.corrupted = 0; /* assume not corrupted by errors */
            return 0;
        }
        return AOM_CODEC_MEM_ERROR;
    }

    private static int calc_stride_and_planesize(
        int ss_x, int ss_y, int aligned_width,
        int aligned_height, int border, int byte_alignment,
        int alloc_y_plane_only, ref int y_stride, ref int uv_stride,
        ref ulong yplane_size, ref ulong uvplane_size, int uv_height)
    {
        /* Only support allocating buffers that have a border that's a multiple
         * of 32. The border restriction is required to get 16-byte alignment of
         * the start of the chroma rows without introducing an arbitrary gap
         * between planes, which would break the semantics of things like
         * aom_img_set_rect(). */
        if ((border & 0x1f) != 0) return AOM_CODEC_MEM_ERROR;
        y_stride = aom_calc_y_stride(aligned_width, border);
        yplane_size =
            (ulong)(aligned_height + 2 * border) * (ulong)y_stride + (ulong)byte_alignment;

        if (alloc_y_plane_only == 0)
        {
            uv_stride = y_stride >> ss_x;
            uvplane_size =
                (ulong)(uv_height + 2 * (border >> ss_y)) * (ulong)uv_stride +
                (ulong)byte_alignment;
        }
        else
        {
            uv_stride = 0;
            uvplane_size = 0;
        }
        return 0;
    }

    internal static int aom_realloc_frame_buffer(YV12_BUFFER_CONFIG ybf, int width, int height,
                                                 int ss_x, int ss_y, int use_highbitdepth,
                                                 int border, int byte_alignment,
                                                 aom_codec_frame_buffer_t fb,
                                                 aom_get_frame_buffer_cb_fn_t cb, object cb_priv,
                                                 bool alloc_pyramid, int alloc_y_plane_only)
    {
        if (ybf != null)
        {
            int y_stride = 0;
            int uv_stride = 0;
            ulong yplane_size = 0;
            ulong uvplane_size = 0;
            int aligned_width = (width + 7) & ~7;
            int aligned_height = (height + 7) & ~7;
            int uv_width = aligned_width >> ss_x;
            int uv_height = aligned_height >> ss_y;
            int uv_border_w = border >> ss_x;
            int uv_border_h = border >> ss_y;

            int error = calc_stride_and_planesize(
                ss_x, ss_y, aligned_width, aligned_height, border, byte_alignment,
                alloc_y_plane_only, ref y_stride, ref uv_stride, ref yplane_size, ref uvplane_size,
                uv_height);
            if (error != 0) return error;
            return realloc_frame_buffer_aligned(
                ybf, width, height, ss_x, ss_y, use_highbitdepth, border,
                byte_alignment, fb, cb, cb_priv, y_stride, yplane_size, uvplane_size,
                aligned_width, aligned_height, uv_width, uv_height, uv_stride,
                uv_border_w, uv_border_h, alloc_pyramid, alloc_y_plane_only);
        }
        return AOM_CODEC_MEM_ERROR;
    }

    internal static int aom_alloc_frame_buffer(YV12_BUFFER_CONFIG ybf, int width, int height,
                                               int ss_x, int ss_y, int use_highbitdepth, int border,
                                               int byte_alignment, bool alloc_pyramid,
                                               int alloc_y_plane_only)
    {
        if (ybf != null)
        {
            aom_free_frame_buffer(ybf);
            return aom_realloc_frame_buffer(
                ybf, width, height, ss_x, ss_y, use_highbitdepth, border,
                byte_alignment, null, null, null, alloc_pyramid, alloc_y_plane_only);
        }
        return AOM_CODEC_MEM_ERROR;
    }

    internal static void aom_remove_metadata_from_frame_buffer(YV12_BUFFER_CONFIG ybf)
    {
        if (ybf != null && ybf.metadata != null)
        {
            // [port] aom_img_metadata_array_free(ybf->metadata)
            ybf.metadata = null;
        }
    }
}
