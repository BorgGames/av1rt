// Port of libaom av1/av1_iface_common.h

global using static Av1rt.Aom.av1_iface_common;

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class av1_iface_common
{
    // [port] yuvconfig2image is used by the AV1_GET_NEW_FRAME_IMAGE control (reconstruction
    // output) of av1_cx_iface.c.
    internal static void yuvconfig2image(aom_image_t img, YV12_BUFFER_CONFIG yv12, object user_priv)
    {
        /* aom_img_wrap() doesn't allow specifying independent strides for
         * the Y, U, and V planes, nor other alignment adjustments that
         * might be representable by a YV12_BUFFER_CONFIG, so we just
         * initialize all the fields.
         */
        int bps;
        if (yv12.subsampling_y == 0)
        {
            if (yv12.subsampling_x == 0)
            {
                img.fmt = AOM_IMG_FMT_I444;
                bps = 24;
            }
            else
            {
                img.fmt = AOM_IMG_FMT_I422;
                bps = 16;
            }
        }
        else
        {
            img.fmt = AOM_IMG_FMT_I420;
            bps = 12;
        }
        img.cp = yv12.color_primaries;
        img.tc = yv12.transfer_characteristics;
        img.mc = yv12.matrix_coefficients;
        img.monochrome = yv12.monochrome;
        img.csp = yv12.chroma_sample_position;
        img.range = yv12.color_range;
        img.bit_depth = 8;
        img.w = (uint)yv12.y_width;
        img.h = (uint)yv12.y_height;
        img.d_w = (uint)yv12.y_crop_width;
        img.d_h = (uint)yv12.y_crop_height;
        img.r_w = (uint)yv12.render_width;
        img.r_h = (uint)yv12.render_height;
        img.x_chroma_shift = (uint)yv12.subsampling_x;
        img.y_chroma_shift = (uint)yv12.subsampling_y;
        img.planes[AOM_PLANE_Y] = yv12.y_buffer;
        img.planes[AOM_PLANE_U] = yv12.u_buffer;
        img.planes[AOM_PLANE_V] = yv12.v_buffer;
        img.stride[AOM_PLANE_Y] = yv12.y_stride;
        img.stride[AOM_PLANE_U] = yv12.uv_stride;
        img.stride[AOM_PLANE_V] = yv12.uv_stride;
        if ((yv12.flags & YV12_FLAG_HIGHBITDEPTH) != 0)
        {
            // [subset] CONFIG_AV1_HIGHBITDEPTH is 0: frame buffers are never high bitdepth
            // (CONVERT_TO_SHORTPTR has no port).
            throw new NotSupportedException("[subset] high bitdepth frame buffer");
        }
        img.bps = bps;
        img.user_priv = user_priv;
        img.img_data = yv12.buffer_alloc;
        img.img_data_owner = 0;
        img.self_allocd = 0;
        img.sz = yv12.frame_size;
        Debug.Assert(yv12.metadata == null);
        img.metadata = null;
    }

    internal static aom_codec_err_t image2yuvconfig(aom_image_t img, YV12_BUFFER_CONFIG yv12)
    {
        yv12.y_buffer = img.planes[AOM_PLANE_Y];
        yv12.u_buffer = img.planes[AOM_PLANE_U];
        yv12.v_buffer = img.planes[AOM_PLANE_V];

        yv12.y_crop_width = (int)img.d_w;
        yv12.y_crop_height = (int)img.d_h;
        yv12.render_width = (int)img.r_w;
        yv12.render_height = (int)img.r_h;
        yv12.y_width = (int)img.w;
        yv12.y_height = (int)img.h;

        yv12.uv_width = (int)(((uint)yv12.y_width + img.x_chroma_shift) >> (int)img.x_chroma_shift);
        yv12.uv_height =
            (int)(((uint)yv12.y_height + img.y_chroma_shift) >> (int)img.y_chroma_shift);
        yv12.uv_crop_width =
            (int)(((uint)yv12.y_crop_width + img.x_chroma_shift) >> (int)img.x_chroma_shift);
        yv12.uv_crop_height =
            (int)(((uint)yv12.y_crop_height + img.y_chroma_shift) >> (int)img.y_chroma_shift);

        yv12.y_stride = img.stride[AOM_PLANE_Y];
        yv12.uv_stride = img.stride[AOM_PLANE_U];
        yv12.color_primaries = img.cp;
        yv12.transfer_characteristics = img.tc;
        yv12.matrix_coefficients = img.mc;
        yv12.monochrome = (byte)img.monochrome;
        yv12.chroma_sample_position = img.csp;
        yv12.color_range = img.range;

        if ((img.fmt & AOM_IMG_FMT_HIGHBITDEPTH) != 0)
        {
            // In aom_image_t
            //     planes point to uint8 address of start of data
            //     stride counts uint8s to reach next row
            // In YV12_BUFFER_CONFIG
            //     y_buffer, u_buffer, v_buffer point to uint16 address of data
            //     stride and border counts in uint16s
            // This means that all the address calculations in the main body of code
            // should work correctly.
            // However, before we do any pixel operations we need to cast the address
            // to a uint16 ponter and double its value.
            // [subset] CONFIG_AV1_HIGHBITDEPTH is 0 and aom_codec_encode rejects high bitdepth
            // images, so this branch never runs; CONVERT_TO_BYTEPTR has no port.
            Debug.Fail("image2yuvconfig: high bitdepth images are not ported");
            yv12.y_stride >>= 1;
            yv12.uv_stride >>= 1;
            yv12.flags = YV12_FLAG_HIGHBITDEPTH;
        }
        else
        {
            yv12.flags = 0;
        }

        // Note(yunqing): if img is allocated the same as the frame buffer, y_stride
        // is 32-byte aligned. Also, handle the cases while allocating img without a
        // border or stride_align is less than 32.
        int border = (yv12.y_stride - (int)((img.w + 31) & ~31u)) / 2;
        yv12.border = (border < 0) ? 0 : border;
        yv12.subsampling_x = (int)img.x_chroma_shift;
        yv12.subsampling_y = (int)img.y_chroma_shift;
        yv12.metadata = img.metadata;
        return AOM_CODEC_OK;
    }
}
