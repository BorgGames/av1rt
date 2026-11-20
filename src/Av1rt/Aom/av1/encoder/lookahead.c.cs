// Port of libaom av1/encoder/lookahead.c

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

// [port] struct lookahead_entry * results are the lookahead_entry objects of ctx.buf (C's
// `ctx->buf + index`); NULL is null.
// [subset] av1_lookahead_full never runs in the subset (lag_in_frames 0) and is not ported.
internal static partial class lookahead
{
    /* Return the buffer at the given absolute index and increment the index */
    private static lookahead_entry pop(lookahead_ctx ctx, ref int idx)
    {
        int index = idx;
        lookahead_entry buf = ctx.buf[index];

        Debug.Assert(index < ctx.max_sz);
        if (++index >= ctx.max_sz) index -= ctx.max_sz;
        idx = index;
        return buf;
    }

    internal static void av1_lookahead_destroy(lookahead_ctx ctx)
    {
        if (ctx != null)
        {
            if (ctx.buf != null)
            {
                int i;

                for (i = 0; i < ctx.max_sz; i++) aom_free_frame_buffer(ctx.buf[i].img);
                // free(ctx->buf);
                ctx.buf = null;
            }
            // free(ctx);
        }
    }

    internal static lookahead_ctx av1_lookahead_init(
        int width, int height, int subsampling_x, int subsampling_y,
        int use_highbitdepth, int depth, int border_in_pixels, int byte_alignment,
        int num_lap_buffers, bool is_all_intra, bool alloc_pyramid)
    {
        int lag_in_frames = AOMMAX(1, depth);

        // For all-intra frame encoding, previous source frames are not required.
        // Hence max_pre_frames is set to 0 in this case. As previous source frames
        // are accessed using a negative index to av1_lookahead_peek(), setting
        // max_pre_frames to 0 will cause av1_lookahead_peek() to return NULL for a
        // negative index.
        byte max_pre_frames = (byte)(is_all_intra ? 0 : MAX_PRE_FRAMES);

        // Add the lags to depth and clamp
        depth += num_lap_buffers;
        depth = clamp(depth, 1, MAX_TOTAL_BUFFERS);

        // Allocate memory to keep previous source frames available.
        depth += max_pre_frames;

        // Allocate the lookahead structures
        lookahead_ctx ctx = new lookahead_ctx();  // calloc(1, sizeof(*ctx))
        if (ctx != null)
        {
            ctx.max_sz = depth;
            ctx.push_frame_count = 0;
            ctx.max_pre_frames = max_pre_frames;
            ctx.read_ctxs[ENCODE_STAGE].pop_sz = ctx.max_sz - ctx.max_pre_frames;
            ctx.read_ctxs[ENCODE_STAGE].valid = 1;
            if (num_lap_buffers != 0)
            {
                ctx.read_ctxs[LAP_STAGE].pop_sz = lag_in_frames;
                ctx.read_ctxs[LAP_STAGE].valid = 1;
            }
            // calloc(depth, sizeof(*ctx->buf)): zeroed entries, so every element is constructed.
            ctx.buf = new lookahead_entry[depth];
            if (ctx.buf == null) goto fail;
            for (int i = 0; i < depth; i++) ctx.buf[i] = new lookahead_entry();
            for (int i = 0; i < depth; i++)
            {
                if (aom_realloc_frame_buffer(
                        ctx.buf[i].img, width, height, subsampling_x, subsampling_y,
                        use_highbitdepth, border_in_pixels, byte_alignment, null, null,
                        null, alloc_pyramid, 0) != 0)
                {
                    goto fail;
                }
            }
        }
        return ctx;
    fail:
        av1_lookahead_destroy(ctx);
        return null;
    }

    internal static int av1_lookahead_push(lookahead_ctx ctx, YV12_BUFFER_CONFIG src,
                                           long ts_start, long ts_end, int use_highbitdepth,
                                           bool alloc_pyramid, aom_enc_frame_flags_t flags)
    {
        int width = src.y_crop_width;
        int height = src.y_crop_height;
        int uv_width = src.uv_crop_width;
        int uv_height = src.uv_crop_height;
        int subsampling_x = src.subsampling_x;
        int subsampling_y = src.subsampling_y;
        int new_dimensions;

        Debug.Assert(ctx.read_ctxs[ENCODE_STAGE].valid == 1);
        if (ctx.read_ctxs[ENCODE_STAGE].sz + ctx.max_pre_frames > ctx.max_sz)
            return 1;

        ctx.read_ctxs[ENCODE_STAGE].sz++;
        if (ctx.read_ctxs[LAP_STAGE].valid != 0)
        {
            ctx.read_ctxs[LAP_STAGE].sz++;
        }

        lookahead_entry buf = pop(ctx, ref ctx.write_idx);

        new_dimensions = B(width != buf.img.y_crop_width ||
                           height != buf.img.y_crop_height ||
                           uv_width != buf.img.uv_crop_width ||
                           uv_height != buf.img.uv_crop_height);

        if (new_dimensions != 0)
        {
            if (aom_realloc_frame_buffer(buf.img, width, height, subsampling_x,
                                         subsampling_y, use_highbitdepth,
                                         AOM_BORDER_IN_PIXELS, 0, null, null, null,
                                         alloc_pyramid, 0) != 0)
                return 1;
        }
        av1_copy_and_extend_frame(src, buf.img, ctx.copy_threads, ctx.copy_workers);

        buf.ts_start = ts_start;
        buf.ts_end = ts_end;
        buf.display_idx = ctx.push_frame_count;
        buf.flags = flags;
        ++ctx.push_frame_count;
        aom_remove_metadata_from_frame_buffer(buf.img);
        if (src.metadata != null)
        {
            // [subset] aom_copy_metadata_to_frame_buffer: frame metadata is not used in the
            // subset (yv12config.c.cs does not port it).
            throw new NotSupportedException("av1_lookahead_push: frame metadata is not ported");
        }
        return 0;
    }

    internal static lookahead_entry av1_lookahead_pop(lookahead_ctx ctx, int drain,
                                                      COMPRESSOR_STAGE stage)
    {
        lookahead_entry buf = null;
        if (ctx != null)
        {
            ref read_ctx read_ctx = ref ctx.read_ctxs[stage];
            Debug.Assert(read_ctx.valid == 1);
            if (read_ctx.sz != 0 && (drain != 0 || read_ctx.sz == read_ctx.pop_sz))
            {
                buf = pop(ctx, ref read_ctx.read_idx);
                read_ctx.sz--;
            }
        }
        return buf;
    }

    internal static lookahead_entry av1_lookahead_peek(lookahead_ctx ctx, int index,
                                                       COMPRESSOR_STAGE stage)
    {
        lookahead_entry buf = null;
        if (ctx == null)
        {
            return buf;
        }

        ref read_ctx read_ctx = ref ctx.read_ctxs[stage];
        Debug.Assert(read_ctx.valid == 1);
        if (index >= 0)
        {
            // Forward peek
            if (index < read_ctx.sz)
            {
                index += read_ctx.read_idx;
                if (index >= ctx.max_sz) index -= ctx.max_sz;
                buf = ctx.buf[index];
            }
        }
        else if (index < 0)
        {
            // Backward peek
            if (-index <= ctx.max_pre_frames)
            {
                index += (int)(read_ctx.read_idx);
                if (index < 0) index += (int)(ctx.max_sz);
                buf = ctx.buf[index];
            }
        }

        return buf;
    }

    internal static int av1_lookahead_depth(lookahead_ctx ctx, COMPRESSOR_STAGE stage)
    {
        Debug.Assert(ctx != null);

        ref read_ctx read_ctx = ref ctx.read_ctxs[stage];
        Debug.Assert(read_ctx.valid == 1);
        return read_ctx.sz;
    }

    internal static int av1_lookahead_pop_sz(lookahead_ctx ctx, COMPRESSOR_STAGE stage)
    {
        Debug.Assert(ctx != null);

        ref read_ctx read_ctx = ref ctx.read_ctxs[stage];
        Debug.Assert(read_ctx.valid == 1);
        return read_ctx.pop_sz;
    }
}
