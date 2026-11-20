using System;
using SIPSorceryMedia.Abstractions;

namespace SIPSorceryMedia.AV1;

/// <summary>
/// Converts the pixel formats SIPSorcery sources deliver into a packed I420 buffer, with libyuv's
/// BT.601 limited-range coefficients (what libwebrtc uses), averaging each 2x2 block for chroma.
/// </summary>
internal static class I420Converter
{
    public static void Convert(ReadOnlySpan<byte> src, int stride, VideoPixelFormatsEnum format, int width, int height, Span<byte> dst)
    {
        int cw = (width + 1) >> 1, ch = (height + 1) >> 1;
        Span<byte> y = dst.Slice(0, width * height);
        Span<byte> u = dst.Slice(width * height, cw * ch);
        Span<byte> v = dst.Slice(width * height + cw * ch, cw * ch);

        switch (format)
        {
            case VideoPixelFormatsEnum.I420:
                CopyI420(src, stride, width, height, y, u, v);
                return;
            case VideoPixelFormatsEnum.NV12:
                ConvertNv12(src, stride, width, height, y, u, v);
                return;
            case VideoPixelFormatsEnum.Bgra:
                ConvertPacked(src, stride, width, height, 4, 2, 1, 0, y, u, v);
                return;
            case VideoPixelFormatsEnum.Rgba:
                ConvertPacked(src, stride, width, height, 4, 0, 1, 2, y, u, v);
                return;
            case VideoPixelFormatsEnum.Bgr:
                ConvertPacked(src, stride, width, height, 3, 2, 1, 0, y, u, v);
                return;
            case VideoPixelFormatsEnum.Rgb:
                ConvertPacked(src, stride, width, height, 3, 0, 1, 2, y, u, v);
                return;
            default:
                throw new NotSupportedException($"Pixel format {format} is not supported by the AV1 encoder.");
        }
    }

    /// <summary>The minimum source length for a frame of this format, size and (luma or packed) stride.</summary>
    public static int SourceSize(VideoPixelFormatsEnum format, int stride, int width, int height)
    {
        int ch = (height + 1) >> 1;
        return format switch
        {
            VideoPixelFormatsEnum.I420 => stride * height + 2 * ((stride + 1) >> 1) * ch,
            VideoPixelFormatsEnum.NV12 => stride * height + stride * ch,
            _ => stride * height,
        };
    }

    /// <summary>The natural stride of a tightly packed frame.</summary>
    public static int PackedStride(VideoPixelFormatsEnum format, int width) => format switch
    {
        VideoPixelFormatsEnum.Bgra or VideoPixelFormatsEnum.Rgba => width * 4,
        VideoPixelFormatsEnum.Bgr or VideoPixelFormatsEnum.Rgb => width * 3,
        _ => width,
    };

    private static void CopyI420(ReadOnlySpan<byte> src, int stride, int width, int height, Span<byte> y, Span<byte> u, Span<byte> v)
    {
        int cw = (width + 1) >> 1, ch = (height + 1) >> 1, cstride = (stride + 1) >> 1;
        for (int r = 0; r < height; r++)
            src.Slice(r * stride, width).CopyTo(y.Slice(r * width, width));
        ReadOnlySpan<byte> su = src.Slice(stride * height);
        ReadOnlySpan<byte> sv = su.Slice(cstride * ch);
        for (int r = 0; r < ch; r++)
        {
            su.Slice(r * cstride, cw).CopyTo(u.Slice(r * cw, cw));
            sv.Slice(r * cstride, cw).CopyTo(v.Slice(r * cw, cw));
        }
    }

    private static void ConvertNv12(ReadOnlySpan<byte> src, int stride, int width, int height, Span<byte> y, Span<byte> u, Span<byte> v)
    {
        int cw = (width + 1) >> 1, ch = (height + 1) >> 1;
        for (int r = 0; r < height; r++)
            src.Slice(r * stride, width).CopyTo(y.Slice(r * width, width));
        ReadOnlySpan<byte> uv = src.Slice(stride * height);
        for (int r = 0; r < ch; r++)
        {
            ReadOnlySpan<byte> row = uv.Slice(r * stride, 2 * cw);
            for (int c = 0; c < cw; c++)
            {
                u[r * cw + c] = row[2 * c];
                v[r * cw + c] = row[2 * c + 1];
            }
        }
    }

    // libyuv RGBToY / RGBToU / RGBToV (BT.601, limited range), chroma from the rounded 2x2 average.
    private static byte RgbToY(int r, int g, int b) => (byte)((66 * r + 129 * g + 25 * b + 0x1080) >> 8);
    private static byte RgbToU(int r, int g, int b) => (byte)((112 * b - 74 * g - 38 * r + 0x8080) >> 8);
    private static byte RgbToV(int r, int g, int b) => (byte)((112 * r - 94 * g - 18 * b + 0x8080) >> 8);

    private static void ConvertPacked(ReadOnlySpan<byte> src, int stride, int width, int height, int bpp,
                                      int ri, int gi, int bi, Span<byte> y, Span<byte> u, Span<byte> v)
    {
        int cw = (width + 1) >> 1;
        for (int r = 0; r < height; r += 2)
        {
            ReadOnlySpan<byte> row0 = src.Slice(r * stride, width * bpp);
            ReadOnlySpan<byte> row1 = r + 1 < height ? src.Slice((r + 1) * stride, width * bpp) : row0;
            Span<byte> y0 = y.Slice(r * width, width);
            Span<byte> y1 = r + 1 < height ? y.Slice((r + 1) * width, width) : default;
            int crow = (r >> 1) * cw;
            for (int c = 0; c < width; c += 2)
            {
                int c1 = c + 1 < width ? c + 1 : c;
                int o00 = c * bpp, o01 = c1 * bpp;
                int r00 = row0[o00 + ri], g00 = row0[o00 + gi], b00 = row0[o00 + bi];
                int r01 = row0[o01 + ri], g01 = row0[o01 + gi], b01 = row0[o01 + bi];
                int r10 = row1[o00 + ri], g10 = row1[o00 + gi], b10 = row1[o00 + bi];
                int r11 = row1[o01 + ri], g11 = row1[o01 + gi], b11 = row1[o01 + bi];
                y0[c] = RgbToY(r00, g00, b00);
                if (c + 1 < width) y0[c + 1] = RgbToY(r01, g01, b01);
                if (!y1.IsEmpty)
                {
                    y1[c] = RgbToY(r10, g10, b10);
                    if (c + 1 < width) y1[c + 1] = RgbToY(r11, g11, b11);
                }
                // libyuv AVGB: (a + b + 1) >> 1, rows first then columns.
                int ar = (((r00 + r10 + 1) >> 1) + ((r01 + r11 + 1) >> 1) + 1) >> 1;
                int ag = (((g00 + g10 + 1) >> 1) + ((g01 + g11 + 1) >> 1) + 1) >> 1;
                int ab = (((b00 + b10 + 1) >> 1) + ((b01 + b11 + 1) >> 1) + 1) >> 1;
                u[crow + (c >> 1)] = RgbToU(ar, ag, ab);
                v[crow + (c >> 1)] = RgbToV(ar, ag, ab);
            }
        }
    }
}
