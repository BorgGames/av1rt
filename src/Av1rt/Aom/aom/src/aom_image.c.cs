// Port of libaom aom/src/aom_image.c

using System.Diagnostics;

namespace Av1rt.Aom;

// [subset] Only the functions that run in the subset are ported: aom_img_alloc and its helpers,
// aom_img_set_rect, aom_img_free and aom_img_remove_metadata. aom_img_alloc_with_cb,
// aom_img_wrap, aom_img_alloc_with_border, aom_img_flip, aom_img_plane_width/height and the
// metadata functions never run. img_alloc_helper loses its alloc_cb/cb_priv parameters, which
// only aom_img_alloc_with_cb passes.
internal static partial class aom_image
{
    private static uint align_image_dimension(uint d, uint subsampling, uint size_align)
    {
        uint align;

        align = (uint)((1 << (int)subsampling) - 1);
        align = (size_align - 1 > align) ? (size_align - 1) : align;
        return ((d + align) & ~align);
    }

    private static bool is_valid_img_fmt(aom_img_fmt_t fmt)
    {
        switch (fmt)
        {
            case AOM_IMG_FMT_YV12:
            case AOM_IMG_FMT_I420:
            case AOM_IMG_FMT_I422:
            case AOM_IMG_FMT_I444:
            case AOM_IMG_FMT_NV12:
            case AOM_IMG_FMT_I42016:
            case AOM_IMG_FMT_YV1216:
            case AOM_IMG_FMT_I42216:
            case AOM_IMG_FMT_I44416: return true;
            default: return false;
        }
    }

    private static aom_image_t img_alloc_helper(aom_image_t img, aom_img_fmt_t fmt, uint d_w, uint d_h,
                                                uint buf_align, uint stride_align, uint size_align,
                                                uint border, Ptr<byte> img_data)
    {
        /* NOTE: In this function, bit_depth is either 8 or 16 (if
         * AOM_IMG_FMT_HIGHBITDEPTH is set), never 10 or 12.
         */
        uint xcs, ycs, bps, bit_depth;

        if (img != null) img.Clear();

        if (!is_valid_img_fmt(fmt)) goto fail;

        /* Impose maximum values on input parameters so that this function can
         * perform arithmetic operations without worrying about overflows.
         */
        if (d_w > 0x08000000 || d_h > 0x08000000 || buf_align > 65536 ||
            stride_align > 65536 || size_align > 65536 || border > 65536)
        {
            goto fail;
        }

        /* Treat align==0 like align==1 */
        if (buf_align == 0) buf_align = 1;

        /* Validate alignment (must be power of 2) */
        if ((buf_align & (buf_align - 1)) != 0) goto fail;

        /* Treat align==0 like align==1 */
        if (stride_align == 0) stride_align = 1;

        /* Validate alignment (must be power of 2) */
        if ((stride_align & (stride_align - 1)) != 0) goto fail;

        /* Treat align==0 like align==1 */
        if (size_align == 0) size_align = 1;

        /* Validate alignment (must be power of 2) */
        if ((size_align & (size_align - 1)) != 0) goto fail;

        /* Get sample size for this format */
        switch (fmt)
        {
            case AOM_IMG_FMT_I420:
            case AOM_IMG_FMT_YV12:
            case AOM_IMG_FMT_NV12: bps = 12; break;
            case AOM_IMG_FMT_I422: bps = 16; break;
            case AOM_IMG_FMT_I444: bps = 24; break;
            case AOM_IMG_FMT_YV1216:
            case AOM_IMG_FMT_I42016: bps = 24; break;
            case AOM_IMG_FMT_I42216: bps = 32; break;
            case AOM_IMG_FMT_I44416: bps = 48; break;
            default: bps = 16; break;
        }

        bit_depth = (fmt & AOM_IMG_FMT_HIGHBITDEPTH) != 0 ? 16u : 8u;

        /* Get chroma shift values for this format */
        switch (fmt)
        {
            case AOM_IMG_FMT_I420:
            case AOM_IMG_FMT_YV12:
            case AOM_IMG_FMT_NV12:
            case AOM_IMG_FMT_I422:
            case AOM_IMG_FMT_I42016:
            case AOM_IMG_FMT_YV1216:
            case AOM_IMG_FMT_I42216: xcs = 1; break;
            default: xcs = 0; break;
        }

        switch (fmt)
        {
            case AOM_IMG_FMT_I420:
            case AOM_IMG_FMT_YV12:
            case AOM_IMG_FMT_NV12:
            case AOM_IMG_FMT_YV1216:
            case AOM_IMG_FMT_I42016: ycs = 1; break;
            default: ycs = 0; break;
        }

        /* Calculate storage sizes given the chroma subsampling */
        uint w = align_image_dimension(d_w, xcs, size_align);
        Debug.Assert(d_w <= w);
        uint h = align_image_dimension(d_h, ycs, size_align);
        Debug.Assert(d_h <= h);

        ulong s = (ulong)w + 2 * border;
        s = (fmt & AOM_IMG_FMT_PLANAR) != 0 ? s : s * bps / bit_depth;
        s = s * bit_depth / 8;
        s = (s + stride_align - 1) & ~((ulong)stride_align - 1);
        if (s > int.MaxValue) goto fail;
        int stride_in_bytes = (int)s;

        /* Allocate the new image */
        if (img == null)
        {
            // [port] calloc(1, sizeof(aom_image_t)).
            img = new aom_image_t();

            img.self_allocd = 1;
        }

        img.img_data = img_data;

        if (img_data.IsNull)
        {
            ulong alloc_size =
                (fmt & AOM_IMG_FMT_PLANAR) != 0
                    ? (ulong)(h + 2 * border) * (ulong)stride_in_bytes * bps / bit_depth
                    : (ulong)(h + 2 * border) * (ulong)stride_in_bytes;

            // [port] C checks that alloc_size fits size_t; managed arrays are int-indexed.
            if (alloc_size > int.MaxValue) goto fail;

            // [port] aom_memalign(buf_align, alloc_size): a new array starts at offset 0, which
            // counts as aligned (see aom_align_addr).
            img.img_data = new byte[(int)alloc_size];
            img.img_data_owner = 1;
            img.sz = (int)alloc_size;
        }

        if (img.img_data.IsNull) goto fail;

        img.fmt = fmt;
        img.bit_depth = bit_depth;
        // aligned width and aligned height
        img.w = w;
        img.h = h;
        img.x_chroma_shift = xcs;
        img.y_chroma_shift = ycs;
        img.bps = (int)bps;

        /* Calculate strides */
        img.stride[AOM_PLANE_Y] = stride_in_bytes;
        img.stride[AOM_PLANE_U] = img.stride[AOM_PLANE_V] = stride_in_bytes >> (int)xcs;

        if (fmt == AOM_IMG_FMT_NV12)
        {
            // Each row is a row of U and a row of V interleaved, so the stride is twice
            // as long.
            img.stride[AOM_PLANE_U] *= 2;
            img.stride[AOM_PLANE_V] = 0;
        }

        img.cp = AOM_CICP_CP_UNSPECIFIED;
        img.tc = AOM_CICP_TC_UNSPECIFIED;
        img.mc = AOM_CICP_MC_UNSPECIFIED;

        /* Default viewport to entire image. (This aom_img_set_rect call always
         * succeeds.) */
        int ret = aom_img_set_rect(img, 0, 0, d_w, d_h, border);
        Debug.Assert(ret == 0);
        return img;

    fail:
        aom_img_free(img);
        return null;
    }

    internal static aom_image_t aom_img_alloc(aom_image_t img, aom_img_fmt_t fmt, uint d_w, uint d_h, uint align)
    {
        return img_alloc_helper(img, fmt, d_w, d_h, align, align, 1, 0, Ptr<byte>.Null);
    }

    internal static int aom_img_set_rect(aom_image_t img, uint x, uint y, uint w, uint h, uint border)
    {
        if (img.stride[AOM_PLANE_Y] < 0) return -1;

        if (x <= uint.MaxValue - w && x + w <= img.w && y <= uint.MaxValue - h &&
            y + h <= img.h)
        {
            img.d_w = w;
            img.d_h = h;

            x += border;
            y += border;

            /* Calculate plane pointers */
            if ((img.fmt & AOM_IMG_FMT_PLANAR) == 0)
            {
                img.planes[AOM_PLANE_PACKED] =
                    img.img_data + (int)(x * (uint)img.bps / 8 + y * (uint)img.stride[AOM_PLANE_PACKED]);
            }
            else
            {
                int bytes_per_sample =
                    (img.fmt & AOM_IMG_FMT_HIGHBITDEPTH) != 0 ? 2 : 1;
                Ptr<byte> data = img.img_data;

                img.planes[AOM_PLANE_Y] =
                    data + (int)(x * (uint)bytes_per_sample + y * (uint)img.stride[AOM_PLANE_Y]);
                data += (int)(((ulong)img.h + 2 * border) * (ulong)img.stride[AOM_PLANE_Y]);

                uint uv_border_h = border >> (int)img.y_chroma_shift;
                uint uv_x = x >> (int)img.x_chroma_shift;
                uint uv_y = y >> (int)img.y_chroma_shift;
                if (img.fmt == AOM_IMG_FMT_NV12)
                {
                    img.planes[AOM_PLANE_U] = data + (int)(uv_x * (uint)bytes_per_sample * 2 +
                                                           uv_y * (uint)img.stride[AOM_PLANE_U]);
                    img.planes[AOM_PLANE_V] = Ptr<byte>.Null;
                }
                else if ((img.fmt & AOM_IMG_FMT_UV_FLIP) == 0)
                {
                    img.planes[AOM_PLANE_U] =
                        data + (int)(uv_x * (uint)bytes_per_sample + uv_y * (uint)img.stride[AOM_PLANE_U]);
                    data += (int)(((ulong)(img.h >> (int)img.y_chroma_shift) + 2 * uv_border_h) *
                                  (ulong)img.stride[AOM_PLANE_U]);
                    img.planes[AOM_PLANE_V] =
                        data + (int)(uv_x * (uint)bytes_per_sample + uv_y * (uint)img.stride[AOM_PLANE_V]);
                }
                else
                {
                    img.planes[AOM_PLANE_V] =
                        data + (int)(uv_x * (uint)bytes_per_sample + uv_y * (uint)img.stride[AOM_PLANE_V]);
                    data += (int)(((ulong)(img.h >> (int)img.y_chroma_shift) + 2 * uv_border_h) *
                                  (ulong)img.stride[AOM_PLANE_V]);
                    img.planes[AOM_PLANE_U] =
                        data + (int)(uv_x * (uint)bytes_per_sample + uv_y * (uint)img.stride[AOM_PLANE_U]);
                }
            }
            return 0;
        }
        return -1;
    }

    internal static void aom_img_free(aom_image_t img)
    {
        if (img != null)
        {
            aom_img_remove_metadata(img);
            if (!img.img_data.IsNull && img.img_data_owner != 0) img.img_data = Ptr<byte>.Null;  // aom_free

            // [port] `if (img->self_allocd) free(img);`: the garbage collector reclaims it.
        }
    }

    internal static void aom_img_remove_metadata(aom_image_t img)
    {
        if (img != null && img.metadata != null)
        {
            // [port] aom_img_metadata_array_free(img->metadata)
            img.metadata = null;
        }
    }
}
