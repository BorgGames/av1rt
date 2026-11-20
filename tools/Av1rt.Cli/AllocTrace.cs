using System;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// AV1RT_ALLOCS=1 (JIT runs only: <c>dotnet Av1rt.Cli.dll bench ...</c>): samples the GC's
/// allocation ticks (one per ~100 KB allocated) by type once <see cref="Start"/> is called, and
/// <see cref="Report"/> lists the types that allocate the most.
/// </summary>
internal sealed class AllocTrace : EventListener
{
    internal static readonly bool Enabled = Environment.GetEnvironmentVariable("AV1RT_ALLOCS") == "1";

    private readonly ConcurrentDictionary<string, long> _bytes = new();
    private volatile bool _started;
    private readonly ConcurrentDictionary<int, byte> _seen = new();

    internal void Start() => _started = true;

    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name == "Microsoft-Windows-DotNETRuntime")
            EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1 /* GC */);
    }

    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        if (!_started) return;
        if (Environment.GetEnvironmentVariable("AV1RT_ALLOCS_DEBUG") == "1" && _seen.TryAdd(e.EventId, 0))
            Console.Error.WriteLine($"event {e.EventId} {e.EventName} {string.Join(",", e.PayloadNames ?? new System.Collections.ObjectModel.ReadOnlyCollection<string>(Array.Empty<string>()))}");
        if (e.EventName == null || !e.EventName.StartsWith("GCAllocationTick", StringComparison.Ordinal)) return;
        string type = "?";
        long amount = 0;
        for (int i = 0; i < e.PayloadNames!.Count; i++)
        {
            switch (e.PayloadNames[i])
            {
                case "TypeName": type = e.Payload![i] as string ?? "?"; break;
                case "AllocationAmount64": amount = Convert.ToInt64(e.Payload![i], CultureInfo.InvariantCulture); break;
            }
        }
        _bytes.AddOrUpdate(type, amount, (_, v) => v + amount);
    }

    internal string Report(int frames)
    {
        var sb = new StringBuilder();
        foreach (var kv in _bytes.OrderByDescending(k => k.Value).Take(25))
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {kv.Value / (double)frames / 1024,8:F1} KiB/frame  {kv.Key}"));
        return sb.ToString();
    }
}
