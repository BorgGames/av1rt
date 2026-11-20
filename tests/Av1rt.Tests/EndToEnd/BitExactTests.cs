using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Xunit;

namespace Av1rt.Tests.EndToEnd;

/// <summary>
/// Encodes each case of cases.txt with <see cref="Av1Encoder"/> and requires every temporal unit to
/// be byte-identical to the pure-C oracle's (.oracle/ref, made by tools/oracle/make-refs.sh).
/// </summary>
public class BitExactTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (string line in File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "EndToEnd", "cases.txt")))
        {
            if (line.Length == 0 || line[0] == '#') continue;
            string[] parts = line.Split('|');
            yield return new object[] { parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), parts[3].Trim() };
        }
    }

    [Theory]
    [Trait("Category", "EndToEnd")]
    [MemberData(nameof(Cases))]
    public void MatchesOracle(string name, string clip, string api, string args)
    {
        string main = Paths.MainCheckout();
        string refIvf = Path.Combine(main, ".oracle", "ref", name + ".ivf");
        string clipPath = Path.Combine(main, ".testdata", clip);
        Assert.True(File.Exists(refIvf), $"missing {refIvf}: run tools/oracle/make-refs.sh");
        Assert.True(File.Exists(clipPath), $"missing {clipPath}");

        List<byte[]> expected = ReadIvf(refIvf);

        using var y4m = Y4mReader.Open(clipPath);
        var (options, limit) = ParseOracleArgs(args, y4m.Width, y4m.Height);
        using var encoder = new Av1Encoder(options);
        var frame = new byte[y4m.FrameSize];
        var output = new ArrayBufferWriter<byte>(1 << 20);
        int framesIn = 0, framesOut = 0;
        while ((limit == 0 || framesIn < limit) && y4m.ReadFrame(frame))
        {
            output.ResetWrittenCount();
            // The case's input API: the I420Frame overload, the encoder's own buffer
            // (GetInputFrame) or the byte[] overload.
            Av1EncodeResult r;
            switch (api)
            {
                case "i420":
                    r = encoder.Encode(I420Frame.FromPacked(frame, options.Width, options.Height), output);
                    break;
                case "input":
                    WritePacked(frame, encoder.GetInputFrame());
                    r = encoder.EncodeInputFrame(output);
                    break;
                case "packed":
                    r = encoder.Encode(frame, output);
                    break;
                default:
                    throw new ArgumentException($"{name}: unknown input API {api}");
            }
            framesIn++;
            if (r.Dropped) continue;
            Assert.True(framesOut < expected.Count, $"{name}: encoder produced more frames than the oracle ({expected.Count})");
            ReadOnlySpan<byte> got = output.WrittenSpan, want = expected[framesOut];
            if (!got.SequenceEqual(want))
            {
                int at = 0;
                while (at < got.Length && at < want.Length && got[at] == want[at]) at++;
                Assert.Fail($"{name}: output frame {framesOut} (input {framesIn - 1}) differs at byte {at}: " +
                            $"{got.Length} bytes vs oracle {want.Length}");
            }
            framesOut++;
        }
        Assert.Equal(expected.Count, framesOut);
    }

    // Writes a packed I420 picture into the planes of an Av1InputFrame.
    internal static void WritePacked(ReadOnlySpan<byte> i420, Av1InputFrame input)
    {
        int w = input.Width, h = input.Height, cw = input.ChromaWidth, ch = input.ChromaHeight;
        Span<byte> y = input.Y.Span, u = input.U.Span, v = input.V.Span;
        for (int r = 0; r < h; r++)
            i420.Slice(r * w, w).CopyTo(y.Slice(r * input.StrideY, w));
        ReadOnlySpan<byte> su = i420.Slice(w * h, cw * ch), sv = i420.Slice(w * h + cw * ch, cw * ch);
        for (int r = 0; r < ch; r++)
        {
            su.Slice(r * cw, cw).CopyTo(u.Slice(r * input.StrideUV, cw));
            sv.Slice(r * cw, cw).CopyTo(v.Slice(r * input.StrideUV, cw));
        }
    }

    internal static (Av1EncoderOptions Options, int Limit) ParseOracleArgs(string args, int width, int height)
    {
        var o = new Av1EncoderOptions { Width = width, Height = height };
        int limit = 0;
        string[] a = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 1 < a.Length; i += 2)
        {
            int n = int.Parse(a[i + 1], CultureInfo.InvariantCulture);
            switch (a[i])
            {
                case "--speed": o.Speed = n; break;
                case "--bitrate": o.TargetBitrateKbps = n; break;
                case "--threads": o.Threads = n; break;
                case "--limit": limit = n; break;
                case "--qmax": o.MaxQuantizer = n; break;
                case "--drop": o.FrameDropThreshold = n; break;
                case "--kf-interval": o.KeyFrameInterval = n; break;
                case "--fps": o.FrameRate = n; break;
                case "--sb": o.Superblock = n switch { 64 => Av1SuperblockSize.Size64, 128 => Av1SuperblockSize.Size128, _ => Av1SuperblockSize.Auto }; break;
                case "--tile-cols": o.TileColumnsLog2 = n; break;
                case "--tile-rows": o.TileRowsLog2 = n; break;
                case "--aq": o.EnableCyclicRefresh = n == 3; break;
                case "--cdef": o.EnableCdef = n != 0; break;
                case "--primaries": o.ColorPrimaries = (Av1ColorPrimaries)n; break;
                case "--transfer": o.TransferCharacteristics = (Av1TransferCharacteristics)n; break;
                case "--matrix": o.MatrixCoefficients = (Av1MatrixCoefficients)n; break;
                case "--range": o.FullRange = n != 0; break;
                default: throw new ArgumentException($"unknown option {a[i]}");
            }
        }
        return (o, limit);
    }

    internal static List<byte[]> ReadIvf(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        int headerLength = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6));
        var frames = new List<byte[]>();
        for (int pos = headerLength; pos + 12 <= data.Length;)
        {
            int size = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos));
            frames.Add(data.AsSpan(pos + 12, size).ToArray());
            pos += 12 + size;
        }
        return frames;
    }
}

internal static class Paths
{
    /// <summary>
    /// The main git checkout (where the uncommitted .oracle and .testdata live), also when the tests
    /// run from a git worktree.
    /// </summary>
    public static string MainCheckout()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "av1rt.slnx")))
            dir = Path.GetDirectoryName(dir);
        if (dir == null) throw new InvalidOperationException("repository root not found");
        string git = Path.Combine(dir, ".git");
        if (File.Exists(git))
        {
            // A worktree: ".git" is a file "gitdir: <main>/.git/worktrees/<name>".
            string gitdir = File.ReadAllText(git).Trim().Substring("gitdir:".Length).Trim();
            string mainGit = Path.GetFullPath(Path.Combine(gitdir, "..", ".."));
            return Path.GetDirectoryName(mainGit);
        }
        return dir;
    }
}
