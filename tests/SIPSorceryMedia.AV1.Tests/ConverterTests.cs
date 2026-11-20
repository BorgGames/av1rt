using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Av1rt;
using SIPSorceryMedia.Abstractions;
using Xunit;

namespace SIPSorceryMedia.AV1.Tests;

/// <summary>
/// The band-parallel, AVX2 conversion must give exactly what the plain per-pixel formulas give
/// (libyuv's C rows), for every format, entry point and awkward size.
/// </summary>
public class ConverterTests
{
    public static readonly TheoryData<VideoPixelFormatsEnum> Formats = new()
    {
        VideoPixelFormatsEnum.Bgra, VideoPixelFormatsEnum.Rgba, VideoPixelFormatsEnum.Bgr,
        VideoPixelFormatsEnum.Rgb, VideoPixelFormatsEnum.NV12, VideoPixelFormatsEnum.I420,
    };

    // (width, height, extra stride bytes): odd sizes, sizes around the 16-pixel vector step, a
    // band count below and above the parallel threshold.
    private static readonly (int W, int H, int Pad)[] Sizes =
    {
        (16, 16, 0), (17, 19, 3), (33, 9, 0), (47, 31, 5), (64, 48, 0), (97, 35, 7), (130, 66, 0),
    };

    // ---- Reference: the per-pixel formulas, into packed I420 ----

    private static byte RgbToY(int r, int g, int b) => (byte)((66 * r + 129 * g + 25 * b + 0x1080) >> 8);
    private static byte RgbToU(int r, int g, int b) => (byte)((112 * b - 74 * g - 38 * r + 0x8080) >> 8);
    private static byte RgbToV(int r, int g, int b) => (byte)((112 * r - 94 * g - 18 * b + 0x8080) >> 8);

    private static byte[] Reference(byte[] src, int stride, VideoPixelFormatsEnum format, int w, int h)
    {
        int cw = (w + 1) >> 1, ch = (h + 1) >> 1;
        var dst = new byte[w * h + 2 * cw * ch];
        int uo = w * h, vo = w * h + cw * ch;
        switch (format)
        {
            case VideoPixelFormatsEnum.I420:
            {
                int cs = (stride + 1) >> 1, su = stride * h, sv = su + cs * ch;
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) dst[y * w + x] = src[y * stride + x];
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                    {
                        dst[uo + y * cw + x] = src[su + y * cs + x];
                        dst[vo + y * cw + x] = src[sv + y * cs + x];
                    }
                return dst;
            }
            case VideoPixelFormatsEnum.NV12:
            {
                int suv = stride * h;
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) dst[y * w + x] = src[y * stride + x];
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                    {
                        dst[uo + y * cw + x] = src[suv + y * stride + 2 * x];
                        dst[vo + y * cw + x] = src[suv + y * stride + 2 * x + 1];
                    }
                return dst;
            }
        }
        int bpp = format is VideoPixelFormatsEnum.Bgra or VideoPixelFormatsEnum.Rgba ? 4 : 3;
        bool bgr = format is VideoPixelFormatsEnum.Bgra or VideoPixelFormatsEnum.Bgr;
        int ri = bgr ? 2 : 0, bi = bgr ? 0 : 2;
        int R(int x, int y) => src[y * stride + x * bpp + ri];
        int G(int x, int y) => src[y * stride + x * bpp + 1];
        int B(int x, int y) => src[y * stride + x * bpp + bi];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dst[y * w + x] = RgbToY(R(x, y), G(x, y), B(x, y));
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
            {
                int x0 = 2 * x, x1 = Math.Min(2 * x + 1, w - 1), y0 = 2 * y, y1 = Math.Min(2 * y + 1, h - 1);
                int Avg(Func<int, int, int> c) =>
                    (((c(x0, y0) + c(x0, y1) + 1) >> 1) + ((c(x1, y0) + c(x1, y1) + 1) >> 1) + 1) >> 1;
                int ar = Avg(R), ag = Avg(G), ab = Avg(B);
                dst[uo + y * cw + x] = RgbToU(ar, ag, ab);
                dst[vo + y * cw + x] = RgbToV(ar, ag, ab);
            }
        return dst;
    }

    private static byte[] Packed(Av1InputFrame f)
    {
        int w = f.Width, h = f.Height, cw = f.ChromaWidth, ch = f.ChromaHeight;
        var dst = new byte[w * h + 2 * cw * ch];
        for (int y = 0; y < h; y++) f.Y.Span.Slice(y * f.StrideY, w).CopyTo(dst.AsSpan(y * w));
        for (int y = 0; y < ch; y++)
        {
            f.U.Span.Slice(y * f.StrideUV, cw).CopyTo(dst.AsSpan(w * h + y * cw));
            f.V.Span.Slice(y * f.StrideUV, cw).CopyTo(dst.AsSpan(w * h + cw * ch + y * cw));
        }
        return dst;
    }

    private static int SourceStride(VideoPixelFormatsEnum format, int w, int pad)
    {
        // NV12 rounds odd widths up to an even stride (whole interleaved chroma rows).
        return I420Converter.MinStride(format, w) + pad;
    }

    private enum Entry { Span, Memory, Native }

    [Theory]
    [MemberData(nameof(Formats))]
    public void MatchesPerPixelFormulas(VideoPixelFormatsEnum format)
    {
        var rnd = new Random((int)format);
        foreach (var (w, h, pad) in Sizes)
        {
            int stride = SourceStride(format, w, pad);
            var src = new byte[I420Converter.SourceSize(format, stride, w, h)];
            rnd.NextBytes(src);
            byte[] want = Reference(src, stride, format, w, h);

            using var encoder = new Av1Encoder(new Av1EncoderOptions { Width = w, Height = h, Threads = 1 });
            // A 4-thread encoder that has encoded a frame (so its workers exist) runs the bands.
            using var workers = new Av1Encoder(new Av1EncoderOptions { Width = 320, Height = 256, Threads = 4 });
            Av1InputFrame warm = workers.GetInputFrame();
            workers.EncodeInputFrame(new ArrayBufferWriter<byte>());
            foreach (Entry entry in Enum.GetValues<Entry>())
            {
                Av1InputFrame frame = encoder.GetInputFrame();
                frame.Y.Span.Fill(0xEE);
                frame.U.Span.Fill(0xEE);
                frame.V.Span.Fill(0xEE);
                switch (entry)
                {
                    case Entry.Span:
                        I420Converter.Convert(src, stride, format, frame);
                        break;
                    case Entry.Memory:
                        I420Converter.Convert(src.AsMemory(), stride, format, frame, workers);
                        break;
                    default:
                        IntPtr p = Marshal.AllocHGlobal(src.Length);
                        try
                        {
                            Marshal.Copy(src, 0, p, src.Length);
                            I420Converter.Convert(p, stride, format, frame, workers);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(p);
                        }
                        break;
                }
                byte[] got = Packed(frame);
                int at = got.AsSpan().CommonPrefixLength(want);
                Assert.True(at == want.Length, $"{format} {w}x{h} pad {pad} via {entry}: differs at byte {at}");
            }
        }
    }

    // The adapter (convert into the encoder's buffer, encode in place) must produce what encoding
    // the reference I420 with Av1Encoder produces.
    [Theory]
    [InlineData(VideoPixelFormatsEnum.I420)]
    [InlineData(VideoPixelFormatsEnum.Bgra)]
    [InlineData(VideoPixelFormatsEnum.NV12)]
    public void AdapterBitstreamMatchesEncoder(VideoPixelFormatsEnum format)
    {
        const int w = 194, h = 130;
        int stride = I420Converter.PackedStride(format, w);
        using var adapter = new AV1VideoEncoder(new Av1EncoderOptions { Threads = 2 });
        using var direct = new Av1Encoder(new Av1EncoderOptions { Width = w, Height = h, Threads = 2 });
        var output = new ArrayBufferWriter<byte>();
        for (int t = 0; t < 6; t++)
        {
            var src = new byte[I420Converter.SourceSize(format, stride, w, h)];
            for (int i = 0; i < src.Length; i++) src[i] = (byte)((i * 7 + t * 13) ^ (i >> 9));
            output.ResetWrittenCount();
            Av1EncodeResult r = direct.Encode(Reference(src, stride, format, w, h), output);
            byte[] want = r.Dropped ? null : output.WrittenSpan.ToArray();
            // The IVideoEncoder overload times frames by their arrival; this one, without a
            // timestamp, at the nominal rate like `direct`.
            output.ResetWrittenCount();
            byte[] got = adapter.EncodeVideo(w, h, src, 0, format, output).Dropped ? null : output.WrittenSpan.ToArray();
            Assert.Equal(want, got);
        }
    }

    // SIPSorcery passes no timestamps; the adapter stamps frames with their arrival time.
    [Fact]
    public void IVideoEncoderFramesAreTimedByArrival()
    {
        const int w = 64, h = 64;
        using var adapter = new AV1VideoEncoder(new Av1EncoderOptions { Threads = 1 });
        var stamps = new List<long>();
        adapter.FrameEncoded += r => stamps.Add(r.Timestamp);
        var frame = new byte[I420Converter.SourceSize(VideoPixelFormatsEnum.I420, w, w, h)];
        for (int t = 0; t < 3; t++)
        {
            if (t > 0) Thread.Sleep(100);
            adapter.EncodeVideo(w, h, frame, VideoPixelFormatsEnum.I420, VideoCodecsEnum.AV1);
        }
        Assert.Equal(3, stamps.Count);
        for (int t = 1; t < 3; t++)
            Assert.InRange(stamps[t] - stamps[t - 1], 100 * 90, 2000 * 90);
    }
}
