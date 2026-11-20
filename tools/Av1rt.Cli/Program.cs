using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Av1rt;

// Av1rt.Cli encode in.y4m out.ivf [options]   — same options and per-frame output as tools/oracle/av1rt_oracle.c
// Av1rt.Cli bench in.y4m [options] [--frames N] — preloads the clip, loops it, reports encode fps
return args.Length > 0 && args[0] == "bench" ? Bench(args) :
       args.Length > 0 && args[0] == "kbench" ? KernelBench.Run(args) :
       args.Length > 0 && args[0] == "encode" ? Encode(args) :
       Usage();

static int Usage()
{
    Console.Error.WriteLine("""
        usage:
          Av1rt.Cli encode in.y4m out.ivf [options]
          Av1rt.Cli bench in.y4m [options] [--frames N]
        options (defaults match tools/oracle/av1rt_oracle.c):
          --speed 11 --bitrate 35000 --threads 0(auto) --limit 0 --qmax 56 --drop 0
          --kf-interval 0 --fps 60 --sb 0|64|128 --tile-cols -1 --tile-rows -1 --aq 3 --cdef 1
        """);
    return 2;
}

static (Av1EncoderOptions Options, Dictionary<string, string> Extra) Parse(string[] args, int first, Y4mReader y4m)
{
    var o = new Av1EncoderOptions { Width = y4m.Width, Height = y4m.Height };
    var extra = new Dictionary<string, string>();
    for (int i = first; i < args.Length; i += 2)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"missing value for {args[i]}");
        string k = args[i], v = args[i + 1];
        int n = int.Parse(v, CultureInfo.InvariantCulture);
        switch (k)
        {
            case "--speed": o.Speed = n; break;
            case "--bitrate": o.TargetBitrateKbps = n; break;
            case "--threads": o.Threads = Math.Max(n, 0); break;
            case "--qmax": o.MaxQuantizer = n; break;
            case "--drop": o.FrameDropThreshold = n; break;
            case "--kf-interval": o.KeyFrameInterval = n; break;
            case "--fps": o.FrameRate = n; break;
            case "--sb": o.Superblock = n switch { 64 => Av1SuperblockSize.Size64, 128 => Av1SuperblockSize.Size128, _ => Av1SuperblockSize.Auto }; break;
            case "--tile-cols": o.TileColumnsLog2 = n; break;
            case "--tile-rows": o.TileRowsLog2 = n; break;
            case "--aq": o.EnableCyclicRefresh = n == 3; break;
            case "--cdef": o.EnableCdef = n != 0; break;
            case "--limit": case "--frames": extra[k] = v; break;
            default: throw new ArgumentException($"unknown option {k}");
        }
    }
    return (o, extra);
}

static int Encode(string[] args)
{
    if (args.Length < 3) return Usage();
    using var y4m = Y4mReader.Open(args[1]);
    var (o, extra) = Parse(args, 3, y4m);
    int limit = extra.TryGetValue("--limit", out var l) ? int.Parse(l, CultureInfo.InvariantCulture) : 0;

    using var enc = new Av1Encoder(o);
    using var file = File.Create(args[2]);
    using var ivf = new IvfWriter(file, o.Width, o.Height, o.FrameRate);
    var frame = new byte[y4m.FrameSize];
    var output = new ArrayBufferWriter<byte>(1 << 20);
    int framesIn = 0, framesOut = 0;
    long totalBytes = 0;
    var sw = Stopwatch.StartNew();
    while ((limit == 0 || framesIn < limit) && y4m.ReadFrame(frame))
    {
        output.ResetWrittenCount();
        var r = enc.Encode(I420Frame.FromPacked(frame, o.Width, o.Height), output);
        if (!r.Dropped)
        {
            ivf.WriteFrame(output.WrittenSpan, r.Timestamp / (Av1Encoder.TimestampRate / o.FrameRate));
            totalBytes += r.Length;
            Console.WriteLine($"frame {framesOut} in {framesIn} size {r.Length} q {r.QIndex}{(r.IsKeyFrame ? " key" : "")}");
            framesOut++;
        }
        framesIn++;
    }
    sw.Stop();
    double kbps = framesIn == 0 ? 0 : totalBytes * 8.0 / 1000 / ((double)framesIn / o.FrameRate);
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"frames_in {framesIn} frames_out {framesOut} bytes {totalBytes} kbps {kbps:F1}"));
    Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"{framesIn / sw.Elapsed.TotalSeconds:F2} fps"));
    return 0;
}

static int Bench(string[] args)
{
    if (args.Length < 2) return Usage();
    using var y4m = Y4mReader.Open(args[1]);
    var (o, extra) = Parse(args, 2, y4m);
    int total = extra.TryGetValue("--frames", out var f) ? int.Parse(f, CultureInfo.InvariantCulture) : 600;

    var frames = new List<byte[]>();
    for (var buf = new byte[y4m.FrameSize]; y4m.ReadFrame(buf); buf = new byte[y4m.FrameSize])
        frames.Add(buf);
    if (frames.Count == 0) throw new InvalidDataException("empty clip");

    using var enc = new Av1Encoder(o);
    var output = new ArrayBufferWriter<byte>(1 << 22);
    long bytes = 0;
    int warmup = Math.Min(10, total / 10);
    var sw = new Stopwatch();
    long alloc0 = 0;
    int gc0 = 0;
    TimeSpan user0 = default, kernel0 = default;
    using var self = Process.GetCurrentProcess();
    for (int i = 0; i < total; i++)
    {
        if (i == warmup)
        {
            alloc0 = GC.GetTotalAllocatedBytes(true);
            gc0 = GC.CollectionCount(0);
            self.Refresh();
            user0 = self.UserProcessorTime;
            kernel0 = self.PrivilegedProcessorTime;
            Av1rt.Diagnostics.StageTimes.Reset();
            sw.Start();
        }
        // Ping-pong through the clip so there's no artificial scene cut at the loop point.
        int period = Math.Max(1, 2 * frames.Count - 2);
        int k = i % period;
        int idx = k < frames.Count ? k : period - k;
        output.ResetWrittenCount();
        long t0 = Av1rt.Diagnostics.StageTimes.Start();
        var r = enc.Encode(frames[idx], output);
        Av1rt.Diagnostics.StageTimes.Stop(Av1rt.Diagnostics.StageTimes.Stage.Total, t0);
        if (i >= warmup) bytes += r.Length;
    }
    sw.Stop();
    int timed = total - warmup;
    double fps = timed / sw.Elapsed.TotalSeconds;
    double kbps = bytes * 8.0 / 1000 / ((double)timed / o.FrameRate);
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"{o.Width}x{o.Height} speed {o.Speed}: {fps:F2} fps over {timed} frames, {kbps:F0} kbps at {o.FrameRate} fps nominal"));
    Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"allocated {(GC.GetTotalAllocatedBytes(true) - alloc0) / (double)timed / 1024:F0} KiB/frame, {GC.CollectionCount(0) - gc0} gen0 collections"));
    self.Refresh();
    Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"cpu {(self.UserProcessorTime - user0).TotalMilliseconds / timed:F1} ms user + {(self.PrivilegedProcessorTime - kernel0).TotalMilliseconds / timed:F1} ms kernel per frame"));
    Console.Error.Write(Av1rt.Diagnostics.StageTimes.Report());
    return 0;
}
