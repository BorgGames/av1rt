// Port of libaom aom_dsp/aom_filter.h

global using static Av1rt.Aom.aom_filter;

namespace Av1rt.Aom;

// [port] `typedef int16_t InterpKernel[SUBPEL_TAPS]` has no alias: the kernel tables are
// flattened to short[] (see av1/common/filter.h.cs) and a kernel is a Ptr<short> to its first
// tap, as the C code passes it (const int16_t *).
internal static partial class aom_filter
{
    internal const int FILTER_BITS = 7;

    internal const int SUBPEL_BITS = 4;
    internal const int SUBPEL_MASK = ((1 << SUBPEL_BITS) - 1);
    internal const int SUBPEL_SHIFTS = (1 << SUBPEL_BITS);
    internal const int SUBPEL_TAPS = 8;

    internal const int SCALE_SUBPEL_BITS = 10;
    internal const int SCALE_SUBPEL_SHIFTS = (1 << SCALE_SUBPEL_BITS);
    internal const int SCALE_SUBPEL_MASK = (SCALE_SUBPEL_SHIFTS - 1);
    internal const int SCALE_EXTRA_BITS = (SCALE_SUBPEL_BITS - SUBPEL_BITS);
    internal const int SCALE_EXTRA_OFF = ((1 << SCALE_EXTRA_BITS) / 2);

    internal const int RS_SUBPEL_BITS = 6;
    internal const int RS_SUBPEL_MASK = ((1 << RS_SUBPEL_BITS) - 1);
    internal const int RS_SCALE_SUBPEL_BITS = 14;
    internal const int RS_SCALE_SUBPEL_MASK = ((1 << RS_SCALE_SUBPEL_BITS) - 1);
    internal const int RS_SCALE_EXTRA_BITS = (RS_SCALE_SUBPEL_BITS - RS_SUBPEL_BITS);
    internal const int RS_SCALE_EXTRA_OFF = (1 << (RS_SCALE_EXTRA_BITS - 1));

    internal const int BIL_SUBPEL_BITS = 3;
    internal const int BIL_SUBPEL_SHIFTS = (1 << BIL_SUBPEL_BITS);

    // 2 tap bilinear filters
    // uint8_t bilinear_filters_2t[BIL_SUBPEL_SHIFTS][2]
    internal static readonly byte[][] bilinear_filters_2t = {
        new byte[] { 128, 0 }, new byte[] { 112, 16 }, new byte[] { 96, 32 }, new byte[] { 80, 48 },
        new byte[] { 64, 64 }, new byte[] { 48, 80 },  new byte[] { 32, 96 }, new byte[] { 16, 112 },
    };
}
