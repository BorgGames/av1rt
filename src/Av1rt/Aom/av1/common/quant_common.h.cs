// Port of libaom av1/common/quant_common.h

global using static Av1rt.Aom.quant_common;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class quant_common
{
    // <gen:consts>
    internal const int MINQ = 0;
    internal const int MAXQ = 255;
    internal const int QINDEX_RANGE = (MAXQ - MINQ + 1);
    internal const int QINDEX_BITS = 8;
    internal const int QM_LEVEL_BITS = 4;
    internal const int NUM_QM_LEVELS = (1 << QM_LEVEL_BITS);
    internal const int DEFAULT_QM_Y = 10;
    internal const int DEFAULT_QM_U = 11;
    internal const int DEFAULT_QM_V = 12;
    internal const int DEFAULT_QM_FIRST = 5;
    internal const int DEFAULT_QM_LAST = 9;
    internal const int DEFAULT_QM_FIRST_ALLINTRA = 4;
    internal const int DEFAULT_QM_LAST_ALLINTRA = 10;
    internal const int QM_FIRST_IQ_SSIMULACRA2 = 2;
    internal const int QM_LAST_IQ_SSIMULACRA2 = 10;
    internal const int LOSSLESS_Q_STEP = 4;
    // </gen:consts>

    // Reduce the large number of quantizers to a smaller number of levels for which
    // different matrices may be defined. This is an increasing function in qindex.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int aom_get_qmlevel(int qindex, int first, int last)
    {
        return first + (qindex * (last + 1 - first)) / QINDEX_RANGE;
    }

    // [subset] aom_get_qmlevel_allintra, aom_get_qmlevel_luma_ssimulacra2 and
    // aom_get_qmlevel_444_chroma are for allintra / tune IQ / 4:4:4 and never run.
}
