// Port of libaom av1/encoder/av1_noise_estimate.h
global using static Av1rt.Aom.av1_noise_estimate;
global using NOISE_LEVEL = int;

namespace Av1rt.Aom;

internal static partial class av1_noise_estimate
{
    internal const int MAX_VAR_HIST_BINS = 20;

    // NOISE_LEVEL
    internal const int kLowLow = 0;
    internal const int kLow = 1;
    internal const int kMedium = 2;
    internal const int kHigh = 3;
}

internal sealed partial class NOISE_ESTIMATE
{
    public int enabled;
    public NOISE_LEVEL level;
    public int value;
    public int thresh;
    public int adapt_thresh;
    public int count;
    public int last_w;
    public int last_h;
    public int num_frames_estimate;
}
