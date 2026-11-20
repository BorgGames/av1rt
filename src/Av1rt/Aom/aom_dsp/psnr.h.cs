// Port of libaom aom_dsp/psnr.h

global using static Av1rt.Aom.psnr;

namespace Av1rt.Aom;

// [port] A struct: callers declare it as a local and pass its address to aom_calc_psnr.
internal partial struct PSNR_STATS
{
    public Arr4<double> psnr;           // total/y/u/v
    public Arr4<ulong> sse;             // total/y/u/v
    public Arr4<uint> samples;          // total/y/u/v
    public Arr4<double> psnr_hbd;       // total/y/u/v when input-bit-depth < bit-depth
    public Arr4<ulong> sse_hbd;         // total/y/u/v when input-bit-depth < bit-depth
    public Arr4<uint> samples_hbd;      // total/y/u/v when input-bit-depth < bit-depth
}

// [port] The functions declared in psnr.h are defined in psnr.c (not part of p1c).
internal static partial class psnr
{
    internal const double MAX_PSNR = 100.0;
}
