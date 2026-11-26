using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Av1rt.Tests.EndToEnd;

/// <summary>
/// A barcode of 16x16 cells on the transform grid, over a still picture (.testdata/strip640.y4m,
/// made by tools/oracle/get-clips.ps1). When two cells in one 32x32 block flip in opposite
/// directions the block's residual sums to zero, and libaom's nonrd skip test took that for a zero
/// DC in every transform block: the block was coded as a skip and kept the previous frame's cells
/// (tools/oracle/patches/nonrd-dc-skip-per-transform-block.patch). Every cell libaom's decoder
/// shows must match the source's.
/// </summary>
public class StaticStripTests
{
    private const int Cells = 32, Cell = 16, StripX = 64, StripY = 448;

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void EveryCellDecodesAsTheSource()
    {
        string main = Paths.MainCheckout();
        string clipPath = Path.Combine(main, ".testdata", "strip640.y4m");
        string aomdec = Path.Combine(main, ".oracle", "build", "Release", "aomdec.exe");
        Assert.True(File.Exists(clipPath), $"missing {clipPath}");
        Assert.True(File.Exists(aomdec), $"missing {aomdec}");

        using var y4m = Y4mReader.Open(clipPath);
        var options = new Av1EncoderOptions
        {
            Width = y4m.Width, Height = y4m.Height, FrameRate = 30, TargetBitrateKbps = 1000, Threads = 1,
        };
        var sources = new List<byte[]>();
        string dir = Path.Combine(Path.GetTempPath(), "av1rt-strip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string ivf = Path.Combine(dir, "strip.ivf"), yuv = Path.Combine(dir, "strip.yuv");
            using (var encoder = new Av1Encoder(options))
            using (var writer = new IvfWriter(File.Create(ivf), options.Width, options.Height, options.FrameRate))
            {
                var output = new ArrayBufferWriter<byte>(1 << 20);
                var frame = new byte[y4m.FrameSize];
                while (y4m.ReadFrame(frame))
                {
                    output.ResetWrittenCount();
                    Av1EncodeResult r = encoder.Encode(frame, output);
                    Assert.False(r.Dropped, $"frame {sources.Count} dropped");
                    writer.WriteFrame(output.WrittenSpan, sources.Count);
                    sources.Add((byte[])frame.Clone());
                }
            }
            Assert.True(sources.Count >= 100, $"only {sources.Count} frames in {clipPath}");

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
            Assert.Equal(sources.Count * size, decoded.Length);
            var wrong = new List<string>();
            for (int i = 0; i < sources.Count; i++)
            {
                uint want = ReadStrip(sources[i], options.Width);
                uint got = ReadStrip(decoded.AsSpan(i * size, size), options.Width);
                if (got != want)
                    wrong.Add($"{i}: {got:x8} for {want:x8}");
            }
            Assert.True(wrong.Count == 0,
                $"{wrong.Count} of {sources.Count} frames with stale cells: {string.Join(", ", wrong.GetRange(0, Math.Min(10, wrong.Count)))}");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>The strip's cells as bits, the leftmost the highest: 1 where a cell's middle is light.</summary>
    private static uint ReadStrip(ReadOnlySpan<byte> luma, int width)
    {
        uint bits = 0;
        for (int k = 0; k < Cells; k++)
        {
            int sum = 0;
            for (int y = StripY + 4; y < StripY + Cell - 4; y++)
                for (int x = StripX + k * Cell + 4; x < StripX + (k + 1) * Cell - 4; x++)
                    sum += luma[y * width + x];
            bits = bits << 1 | (sum > 128 * 64 ? 1u : 0u);
        }
        return bits;
    }
}
