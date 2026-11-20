using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Av1rt.Tests.EndToEnd;

/// <summary>
/// Speed 12 is an av1rt extension with no libaom reference stream: check that libaom's decoder
/// accepts it and that the pictures are close to the source.
/// </summary>
public class Speed12Tests
{
    [Theory]
    [Trait("Category", "EndToEnd")]
    [InlineData("dota352.y4m", 600, 1)]
    [InlineData("dota1080.y4m", 6000, 4)]
    public void DecodesCloseToSource(string clip, int kbps, int threads)
    {
        string main = Paths.MainCheckout();
        string clipPath = Path.Combine(main, ".testdata", clip);
        string aomdec = Path.Combine(main, ".oracle", "build", "Release", "aomdec.exe");
        Assert.True(File.Exists(clipPath), $"missing {clipPath}");
        Assert.True(File.Exists(aomdec), $"missing {aomdec}");

        const int frames = 20;
        using var y4m = Y4mReader.Open(clipPath);
        var options = new Av1EncoderOptions
        {
            Width = y4m.Width, Height = y4m.Height, Speed = 12, TargetBitrateKbps = kbps, Threads = threads,
        };
        var sources = new List<byte[]>();
        var units = new List<byte[]>();
        using (var encoder = new Av1Encoder(options))
        {
            var output = new ArrayBufferWriter<byte>(1 << 20);
            for (int i = 0; i < frames; i++)
            {
                var frame = new byte[y4m.FrameSize];
                Assert.True(y4m.ReadFrame(frame));
                output.ResetWrittenCount();
                Av1EncodeResult r = encoder.Encode(frame, output);
                Assert.False(r.Dropped);
                sources.Add(frame);
                units.Add(output.WrittenSpan.ToArray());
            }
        }

        string dir = Path.Combine(Path.GetTempPath(), "av1rt-speed12-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string ivf = Path.Combine(dir, "s12.ivf"), yuv = Path.Combine(dir, "s12.yuv");
            WriteIvf(ivf, options.Width, options.Height, units);
            var psi = new ProcessStartInfo(aomdec, $"--rawvideo -o \"{yuv}\" \"{ivf}\"")
            {
                RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false,
            };
            using (var p = Process.Start(psi)!)
            {
                string err = p.StandardError.ReadToEnd();
                p.WaitForExit();
                Assert.True(p.ExitCode == 0, $"aomdec failed: {err}");
            }

            byte[] decoded = File.ReadAllBytes(yuv);
            int size = y4m.FrameSize;
            Assert.Equal(frames * size, decoded.Length);
            for (int i = 0; i < frames; i++)
            {
                double psnr = LumaPsnr(sources[i], decoded.AsSpan(i * size, size), options.Width * options.Height);
                Assert.True(psnr > 30, $"frame {i}: luma PSNR {psnr:F2} dB");
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static double LumaPsnr(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, int n)
    {
        long sse = 0;
        for (int i = 0; i < n; i++)
        {
            int d = a[i] - b[i];
            sse += d * d;
        }
        return sse == 0 ? 99 : 10 * Math.Log10(255.0 * 255.0 * n / sse);
    }

    private static void WriteIvf(string path, int width, int height, List<byte[]> units)
    {
        using var f = File.Create(path);
        Span<byte> h = stackalloc byte[32];
        h.Clear();
        "DKIF"u8.CopyTo(h);
        BinaryPrimitives.WriteUInt16LittleEndian(h[6..], 32);
        "AV01"u8.CopyTo(h[8..]);
        BinaryPrimitives.WriteUInt16LittleEndian(h[12..], (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(h[14..], (ushort)height);
        BinaryPrimitives.WriteUInt32LittleEndian(h[16..], 60);
        BinaryPrimitives.WriteUInt32LittleEndian(h[20..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(h[24..], (uint)units.Count);
        f.Write(h);
        Span<byte> fh = stackalloc byte[12];
        for (int i = 0; i < units.Count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(fh, (uint)units[i].Length);
            BinaryPrimitives.WriteInt64LittleEndian(fh[4..], i);
            f.Write(fh);
            f.Write(units[i]);
        }
    }
}
