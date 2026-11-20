using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
using Xunit;

namespace SIPSorceryMedia.AV1.Tests;

/// <summary>
/// The encoder plugged into a real SIPSorcery video source, as in the package README. Catches the
/// adapter and SIPSorcery disagreeing on SIPSorceryMedia.Abstractions (its enums were renumbered
/// in 10.0.17, so a build against 10.0.10 asked SIPSorcery for VP9).
/// </summary>
public class SipsorceryIntegrationTests
{
    [Fact]
    public async Task TestPatternSourceProducesAv1Frames()
    {
        using var encoder = new AV1VideoEncoder(new Av1rt.Av1EncoderOptions { Threads = 2 });
        var video = new VideoTestPatternSource(encoder);
        VideoFormat av1 = video.GetVideoSourceFormats().Single(f => f.Codec == VideoCodecsEnum.AV1);
        video.SetVideoSourceFormat(av1);

        int frames = 0;
        var gotFrames = new TaskCompletionSource();
        video.OnVideoSourceEncodedSample += (duration, sample) =>
        {
            Assert.NotEmpty(sample);
            if (Interlocked.Increment(ref frames) == 5) gotFrames.TrySetResult();
        };
        await video.StartVideo();
        try
        {
            await gotFrames.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            await video.CloseVideo();
        }
    }
}
