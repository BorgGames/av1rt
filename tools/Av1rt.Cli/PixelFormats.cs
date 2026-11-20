using System;
using SIPSorceryMedia.Abstractions;

/// <summary>
/// Test input for `bench --input adapter-*`: a clip's I420 frames as the pixel formats capture
/// pipelines deliver (BT.601 limited range, the inverse of the adapter's RGB conversion).
/// </summary>
internal static class PixelFormats
{
    public static byte[] FromI420(byte[] i420, int w, int h, VideoPixelFormatsEnum format)
    {
        int cw = (w + 1) >> 1, ch = (h + 1) >> 1, uo = w * h, vo = w * h + cw * ch;
        switch (format)
        {
            case VideoPixelFormatsEnum.NV12:
            {
                int stride = (w + 1) & ~1;
                var dst = new byte[stride * h + stride * ch];
                for (int y = 0; y < h; y++) i420.AsSpan(y * w, w).CopyTo(dst.AsSpan(y * stride));
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                    {
                        dst[stride * h + y * stride + 2 * x] = i420[uo + y * cw + x];
                        dst[stride * h + y * stride + 2 * x + 1] = i420[vo + y * cw + x];
                    }
                return dst;
            }
            case VideoPixelFormatsEnum.Bgra:
            {
                var dst = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int c = i420[y * w + x] - 16;
                        int d = i420[uo + (y >> 1) * cw + (x >> 1)] - 128;
                        int e = i420[vo + (y >> 1) * cw + (x >> 1)] - 128;
                        int o = 4 * (y * w + x);
                        dst[o] = Clamp((298 * c + 516 * d + 128) >> 8);
                        dst[o + 1] = Clamp((298 * c - 100 * d - 208 * e + 128) >> 8);
                        dst[o + 2] = Clamp((298 * c + 409 * e + 128) >> 8);
                        dst[o + 3] = 255;
                    }
                return dst;
            }
            default:
                throw new NotSupportedException(format.ToString());
        }
    }

    private static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);
}
