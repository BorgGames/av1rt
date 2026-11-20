#nullable enable
using System.Collections.Generic;

namespace Av1rt;

/// <summary>
/// Frame durations from caller timestamps, for libaom's realtime rate control, which takes the
/// frame rate from each frame's duration and gives the frame target bitrate x duration bits.
/// </summary>
/// <remarks>
/// A duration is the average interval between the frames of the last second, so jitter averages
/// out, and the time of a pause is shared by the frames of the second after it. Of a pause longer
/// than the window, only one second counts: the first frame after it gets one second.
/// </remarks>
internal sealed class FrameIntervals(long nominal)
{
    // Timestamps of the frames of the last second, oldest first.
    private readonly Queue<long> _window = new();
    private long _last;

    /// <summary>The duration of the frame at <paramref name="pts"/>, both in <see cref="Av1Encoder.TimestampRate"/> units.</summary>
    public long Next(long pts)
    {
        long duration;
        if (_window.Count == 0 || pts <= _last)
        {
            // The first frame, or the timestamps went back: start over at the nominal rate.
            _window.Clear();
            duration = nominal;
        }
        else
        {
            while (_window.Count > 0 && _window.Peek() <= pts - Av1Encoder.TimestampRate)
                _window.Dequeue();
            duration = _window.Count == 0 ? Av1Encoder.TimestampRate : (pts - _window.Peek()) / _window.Count;
        }
        _window.Enqueue(pts);
        _last = pts;
        return duration;
    }
}
