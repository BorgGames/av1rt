using System;
using System.Diagnostics;
using System.Text;

namespace Av1rt.Diagnostics;

/// <summary>
/// Wall-clock time per frame-level encoder stage, on the thread that drives the encoder.
/// Enabled with the environment variable AV1RT_STAGES=1; otherwise every call is a no-op.
/// </summary>
internal static class StageTimes
{
    internal enum Stage { Total, EncodeTiles, PickLpf, LoopFilter, CdefSearch, CdefApply, ExtendBorders, PackBitstream, Count }

    internal static readonly bool Enabled = Environment.GetEnvironmentVariable("AV1RT_STAGES") == "1";
    private static readonly long[] s_ticks = new long[(int)Stage.Count];
    private static int s_frames;

    internal static long Start() => Enabled ? Stopwatch.GetTimestamp() : 0;

    internal static void Stop(Stage stage, long start)
    {
        if (!Enabled) return;
        s_ticks[(int)stage] += Stopwatch.GetTimestamp() - start;
        if (stage == Stage.Total) s_frames++;
    }

    internal static void Reset()
    {
        Array.Clear(s_ticks);
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
        return sb.ToString();
    }
}
