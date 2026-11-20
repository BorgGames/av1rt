using System;
using System.Buffers;
using System.Collections.Generic;
using Av1rt.Tests.EndToEnd;
using Xunit;

namespace Av1rt.Tests.Api;

/// <summary>
/// <see cref="Av1Encoder.GetInputFrame"/> / <see cref="Av1Encoder.EncodeInputFrame"/> (the picture
/// written into the encoder's lookahead buffer, which is then only border-extended) must produce
/// the same bitstream as the copying overloads.
/// </summary>
public class InputFrameTests
{
    // A moving pattern with texture, so that motion search, inter modes and the borders matter.
    private static byte[] Frame(int w, int h, int t)
    {
        int cw = (w + 1) >> 1, ch = (h + 1) >> 1;
        var f = new byte[I420Frame.PackedSize(w, h)];
        var rnd = new Random(t * 7919 + 1);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                f[y * w + x] = (byte)(((x + 3 * t) * 5 ^ (y + t) * 3) + rnd.Next(8));
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
            {
                f[w * h + y * cw + x] = (byte)(128 + ((x + t) & 31) - 16);
                f[w * h + cw * ch + y * cw + x] = (byte)(128 + ((y - t) & 15) - 8);
            }
        return f;
    }

    private enum Path { ByteArray, I420Frame, InputFrame }

    private static List<byte[]> Encode(int w, int h, int threads, int frames, Path path)
    {
        var options = new Av1EncoderOptions { Width = w, Height = h, Threads = threads, TargetBitrateKbps = 2000 };
        using var encoder = new Av1Encoder(options);
        var output = new ArrayBufferWriter<byte>();
        var units = new List<byte[]>();
        for (int t = 0; t < frames; t++)
        {
            byte[] f = Frame(w, h, t);
            output.ResetWrittenCount();
            Av1EncodeResult r;
            switch (path)
            {
                case Path.ByteArray:
                    r = encoder.Encode(f, output);
                    break;
                case Path.I420Frame:
                    r = encoder.Encode(I420Frame.FromPacked(f, w, h), output);
                    break;
                default:
                    BitExactTests.WritePacked(f, encoder.GetInputFrame());
                    r = encoder.EncodeInputFrame(output);
                    break;
            }
            units.Add(r.Dropped ? Array.Empty<byte>() : output.WrittenSpan.ToArray());
        }
        return units;
    }

    [Theory]
    [InlineData(160, 96, 1)]   // serial copy_and_extend_plane
    [InlineData(322, 258, 4)]  // copy_and_extend_frame_mt (>= 256 rows), odd chroma sizes
    public void SameBitstreamAsCopyingOverloads(int w, int h, int threads)
    {
        List<byte[]> expected = Encode(w, h, threads, 8, Path.ByteArray);
        foreach (Path path in new[] { Path.I420Frame, Path.InputFrame })
        {
            List<byte[]> got = Encode(w, h, threads, 8, path);
            for (int i = 0; i < expected.Count; i++)
                Assert.True(expected[i].AsSpan().SequenceEqual(got[i]), $"{path}: frame {i} differs");
        }
    }

    [Fact]
    public void InputFrameIsTheNextLookaheadBuffer()
    {
        using var encoder = new Av1Encoder(new Av1EncoderOptions { Width = 64, Height = 64, Threads = 1 });
        var output = new ArrayBufferWriter<byte>();
        Assert.Throws<InvalidOperationException>(() => encoder.EncodeInputFrame(output));

        // Before the first encode: the image's own buffer (copied once by the push).
        Av1InputFrame first = encoder.GetInputFrame();
        Assert.Equal(64, first.Width);
        Assert.Equal(32, first.ChromaWidth);
        Assert.True(first.StrideY >= 64 && first.StrideUV >= 32);
        encoder.EncodeInputFrame(output);
        Assert.Throws<InvalidOperationException>(() => encoder.EncodeInputFrame(output));

        // Afterwards the lookahead buffers, alternating (lag 0: the current and the previous source).
        Av1InputFrame a = encoder.GetInputFrame();
        Assert.True(a.Y.Span.Overlaps(encoder.GetInputFrame().Y.Span), "repeated calls return the same buffer");
        encoder.EncodeInputFrame(output);
        Av1InputFrame b = encoder.GetInputFrame();
        Assert.False(a.Y.Span.Overlaps(b.Y.Span));
        encoder.EncodeInputFrame(output);
        Assert.True(a.Y.Span.Overlaps(encoder.GetInputFrame().Y.Span));

        // Another encode call discards the pending input frame.
        encoder.Encode(new byte[I420Frame.PackedSize(64, 64)], output);
        Assert.Throws<InvalidOperationException>(() => encoder.EncodeInputFrame(output));
    }

    [Fact]
    public void ParallelForRunsEveryIndexOnTheWorkers()
    {
        using var encoder = new Av1Encoder(new Av1EncoderOptions { Width = 320, Height = 256, Threads = 4 });
        var hits = new int[64];
        var threads = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
        void Body(int i)
        {
            System.Threading.Interlocked.Increment(ref hits[i]);
            threads[Environment.CurrentManagedThreadId] = true;
            System.Threading.Thread.Sleep(1);
        }

        // Before the first encode there are no workers: serial on this thread.
        encoder.ParallelFor(hits.Length, Body);
        Assert.All(hits, h => Assert.Equal(1, h));
        Assert.Single(threads);

        BitExactTests.WritePacked(Frame(320, 256, 0), encoder.GetInputFrame());
        encoder.EncodeInputFrame(new ArrayBufferWriter<byte>());
        threads.Clear();
        encoder.ParallelFor(hits.Length, Body);
        Assert.All(hits, h => Assert.Equal(2, h));
        Assert.True(threads.Count > 1, "the workers took part");
    }
}
