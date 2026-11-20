using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Av1rt.Tests.Api;

/// <summary>Frame durations from caller timestamps (90 kHz), with a nominal 60 fps.</summary>
public class FrameIntervalsTests
{
    private static List<long> Durations(IEnumerable<long> stamps)
    {
        var intervals = new FrameIntervals(1500);
        return stamps.Select(intervals.Next).ToList();
    }

    [Fact]
    public void FollowsTheActualRate()
    {
        List<long> d = Durations(Enumerable.Range(0, 100).Select(i => 1000L + i * 3000));
        Assert.Equal(1500, d[0]);
        Assert.All(d.Skip(1), x => Assert.Equal(3000, x));
    }

    [Fact]
    public void AveragesJitter()
    {
        // 30 fps, each frame up to 10 ms late.
        List<long> d = Durations(Enumerable.Range(0, 120).Select(i => i * 3000L + (i * 7919 % 11) * 90));
        Assert.All(d.Skip(31), x => Assert.InRange(x, 2970, 3030));
    }

    [Fact]
    public void SharesAShortPauseOverTheNextSecond()
    {
        // 30 fps, half a second missing after frame 59.
        var stamps = Enumerable.Range(0, 60).Select(i => i * 3000L)
            .Concat(Enumerable.Range(0, 60).Select(i => 59 * 3000L + 45000 + i * 3000)).ToList();
        List<long> d = Durations(stamps);
        Assert.InRange(d[60], 3001, 9000);
        // The durations after the last frame before the pause add up to the time that passed.
        Assert.InRange(d.Skip(60).Sum(), stamps[^1] - stamps[59] - 30, stamps[^1] - stamps[59] + 30);
    }

    [Fact]
    public void CountsAtMostOneSecondOfALongPause()
    {
        List<long> d = Durations([0, 3000, 6000, 6000 + 5 * 90000, 9000 + 5 * 90000]);
        Assert.Equal([1500, 3000, 3000, 90000, 3000], d);
    }

    [Fact]
    public void StartsOverWhenTheTimestampsGoBack()
    {
        List<long> d = Durations([90000, 93000, 96000, 1000, 4000]);
        Assert.Equal([1500, 3000, 3000, 1500, 3000], d);
    }
}
