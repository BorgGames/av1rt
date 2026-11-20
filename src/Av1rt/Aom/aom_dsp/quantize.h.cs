// Port of libaom aom_dsp/quantize.h

global using static Av1rt.Aom.quantize;

namespace Av1rt.Aom;

// [port] The quantizer functions declared in quantize.h are defined in quantize.c (not part of
// p1c).
internal static partial class quantize
{
    internal const int EOB_FACTOR = 325;
    internal const int SKIP_EOB_FACTOR_ADJUST = 200;
}
