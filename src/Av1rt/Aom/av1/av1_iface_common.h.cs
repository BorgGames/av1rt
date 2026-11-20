// Port of libaom av1/av1_iface_common.h

global using static Av1rt.Aom.av1_iface_common;

using System.Diagnostics;

namespace Av1rt.Aom;

// [subset] yuvconfig2image (used by the preview / reference-frame controls and the decoder)
// never runs in the subset and is not ported.
internal static partial class av1_iface_common
{
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
