using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Av1rt.Diagnostics;

/// <summary>
/// Wall-clock time per frame-level encoder stage, on the thread that drives the encoder.
/// Enabled with the environment variable AV1RT_STAGES=1; otherwise every call is a no-op.
/// </summary>
internal static class StageTimes
{
    internal enum Stage { Total, Lookahead, SceneDetect, EncodeTiles, PickLpf, LoopFilter, CdefSearch, CdefApply, ExtendBorders, PackBitstream, Count }

    /// <summary>Thread-summed worker counters (times in Stopwatch ticks, or event counts).</summary>
    internal enum Counter { EncBusy, EncLaunchDelay, EncLaunchMain, EncLpf, LpfWait, RowSyncWait, RowSyncWaits, PackMt, PackBusy, PackMaxTile, PackWorkers, CdefInPack, Count }

    internal static readonly bool Enabled = Environment.GetEnvironmentVariable("AV1RT_STAGES") == "1";
    private static readonly long[] s_counters = new long[(int)Counter.Count];

    /// <summary>When the encode workers were last launched (for <see cref="Counter.EncLaunchDelay"/>).</summary>
    internal static long LaunchTimestamp;

    internal static void Add(Counter counter, long value)
    {
        if (Enabled) Interlocked.Add(ref s_counters[(int)counter], value);
    }

    /// <summary>A per-frame maximum (reset by the caller, raised with <see cref="Max"/>).</summary>
    internal static long FrameMax;

    internal static void Max(ref long field, long value)
    {
        if (!Enabled) return;
        long cur;
        while (value > (cur = Volatile.Read(ref field)) &&
               Interlocked.CompareExchange(ref field, value, cur) != cur) { }
    }
    private static readonly long[] s_ticks = new long[(int)Stage.Count];
    private static int s_frames;

    internal static long Start() => Enabled ? Stopwatch.GetTimestamp() : 0;

    internal static void Stop(Stage stage, long start)
    {
        if (!Enabled) return;
        s_ticks[(int)stage] += Stopwatch.GetTimestamp() - start;
        if (stage == Stage.Total) s_frames++;
    }

    private static readonly long[] s_frameStart = new long[(int)Stage.Count];

    /// <summary>The stage times since the previous call (one frame when called after each
    /// frame), as "Stage ms" pairs.</summary>
    internal static string FrameStages()
    {
        if (!Enabled) return string.Empty;
        var sb = new StringBuilder();
        for (int i = 1; i < (int)Stage.Count; i++)
        {
            long d = s_ticks[i] - s_frameStart[i];
            s_frameStart[i] = s_ticks[i];
            if (d > 0) sb.Append($" {(Stage)i} {d * 1000.0 / Stopwatch.Frequency:F2}");
        }
        return sb.ToString();
    }

    internal static void Reset()
    {
        Array.Clear(s_frameStart);
        Array.Clear(s_ticks);
        Array.Clear(s_counters);
        s_frames = 0;
    }

    internal static string Report()
    {
        if (!Enabled || s_frames == 0) return string.Empty;
        var sb = new StringBuilder();
        double total = s_ticks[(int)Stage.Total];
        double sum = 0;
        for (int i = 0; i < (int)Stage.Count; i++)
        {
            double ms = s_ticks[i] * 1000.0 / Stopwatch.Frequency / s_frames;
            if (i != (int)Stage.Total) sum += s_ticks[i];
            sb.Append($"{(Stage)i,-14} {ms,7:F2} ms/frame {100 * s_ticks[i] / total,5:F1}%\n");
        }
        double other = (total - sum) * 1000.0 / Stopwatch.Frequency / s_frames;
        sb.Append($"{"Other",-14} {other,7:F2} ms/frame {100 * (total - sum) / total,5:F1}%\n");
        for (int i = 0; i < (int)Counter.Count; i++)
        {
            double v = s_counters[i] / (double)s_frames;
            if ((Counter)i is Counter.RowSyncWaits or Counter.PackWorkers)
                sb.Append($"{(Counter)i,-14} {v,7:F1} /frame\n");
            else
                sb.Append($"{(Counter)i,-14} {v * 1000.0 / Stopwatch.Frequency,7:F2} thread-ms/frame\n");
        }
        return sb.ToString();
    }
}
