// Port of libaom av1/common/entropy.h

// <gen:aliases>
global using TX_CLASS = int;

global using ENTROPY_CONTEXT = int;
// </gen:aliases>

global using static Av1rt.Aom.entropy;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class entropy
{
    // <gen:consts>
    internal const int TOKEN_CDF_Q_CTXS = 4;
    internal const int TXB_SKIP_CONTEXTS = 13;
    internal const int EOB_COEF_CONTEXTS = 9;
    internal const int SIG_COEF_CONTEXTS_2D = 26;
    internal const int SIG_COEF_CONTEXTS_1D = 16;
    internal const int SIG_COEF_CONTEXTS_EOB = 4;
    internal const int SIG_COEF_CONTEXTS = (SIG_COEF_CONTEXTS_2D + SIG_COEF_CONTEXTS_1D);
    internal const int COEFF_BASE_CONTEXTS = (SIG_COEF_CONTEXTS);
    internal const int DC_SIGN_CONTEXTS = 3;
    internal const int BR_TMP_OFFSET = 12;
    internal const int BR_REF_CAT = 4;
    internal const int LEVEL_CONTEXTS = 21;
    internal const int NUM_BASE_LEVELS = 2;
    internal const int BR_CDF_SIZE = (4);
    internal const int COEFF_BASE_RANGE = (4 * (BR_CDF_SIZE - 1));
    internal const int COEFF_CONTEXT_BITS = 3;
    internal const int COEFF_CONTEXT_MASK = ((1 << COEFF_CONTEXT_BITS) - 1);
    internal const int MAX_BASE_BR_RANGE = (COEFF_BASE_RANGE + NUM_BASE_LEVELS + 1);
    internal const int BASE_CONTEXT_POSITION_NUM = 12;
    internal const int DCT_MAX_VALUE = 16384;
    internal const int DCT_MAX_VALUE_HIGH10 = 65536;
    internal const int DCT_MAX_VALUE_HIGH12 = 262144;
    internal const int REF_TYPES = 2;

    // TX_CLASS
    internal const int TX_CLASS_2D = 0;
    internal const int TX_CLASS_HORIZ = 1;
    internal const int TX_CLASS_VERT = 2;
    internal const int TX_CLASSES = 3;
    // </gen:consts>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int combine_entropy_contexts(ENTROPY_CONTEXT a,
                                                 ENTROPY_CONTEXT b)
    {
        return B(a != 0) + B(b != 0);
    }

    // [port] C reads n consecutive ENTROPY_CONTEXT (char) entries at once through a
    // uint16/uint32/uint64 pointer and tests the word for non-zero; this is the same test.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ENTROPY_CONTEXT any_nonzero(Ptr<ENTROPY_CONTEXT> p, int n)
    {
        for (int i = 0; i < n; i++)
            if (p[i] != 0) return 1;
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_entropy_context(TX_SIZE tx_size, Ptr<ENTROPY_CONTEXT> a,
                                            Ptr<ENTROPY_CONTEXT> l)
    {
        ENTROPY_CONTEXT above_ec = 0, left_ec = 0;

        switch (tx_size)
        {
            case TX_4X4:
                above_ec = B(a[0] != 0);
                left_ec = B(l[0] != 0);
                break;
            case TX_4X8:
                above_ec = B(a[0] != 0);
                left_ec = any_nonzero(l, 2);
                break;
            case TX_8X4:
                above_ec = any_nonzero(a, 2);
                left_ec = B(l[0] != 0);
                break;
            case TX_8X16:
                above_ec = any_nonzero(a, 2);
                left_ec = any_nonzero(l, 4);
                break;
            case TX_16X8:
                above_ec = any_nonzero(a, 4);
                left_ec = any_nonzero(l, 2);
                break;
            case TX_16X32:
                above_ec = any_nonzero(a, 4);
                left_ec = any_nonzero(l, 8);
                break;
            case TX_32X16:
                above_ec = any_nonzero(a, 8);
                left_ec = any_nonzero(l, 4);
                break;
            case TX_8X8:
                above_ec = any_nonzero(a, 2);
                left_ec = any_nonzero(l, 2);
                break;
            case TX_16X16:
                above_ec = any_nonzero(a, 4);
                left_ec = any_nonzero(l, 4);
                break;
            case TX_32X32:
                above_ec = any_nonzero(a, 8);
                left_ec = any_nonzero(l, 8);
                break;
            case TX_64X64:
                above_ec = any_nonzero(a, 16);
                left_ec = any_nonzero(l, 16);
                break;
            case TX_32X64:
                above_ec = any_nonzero(a, 8);
                left_ec = any_nonzero(l, 16);
                break;
            case TX_64X32:
                above_ec = any_nonzero(a, 16);
                left_ec = any_nonzero(l, 8);
                break;
            case TX_4X16:
                above_ec = B(a[0] != 0);
                left_ec = any_nonzero(l, 4);
                break;
            case TX_16X4:
                above_ec = any_nonzero(a, 4);
                left_ec = B(l[0] != 0);
                break;
            case TX_8X32:
                above_ec = any_nonzero(a, 2);
                left_ec = any_nonzero(l, 8);
                break;
            case TX_32X8:
                above_ec = any_nonzero(a, 8);
                left_ec = any_nonzero(l, 2);
                break;
            case TX_16X64:
                above_ec = any_nonzero(a, 4);
                left_ec = any_nonzero(l, 16);
                break;
            case TX_64X16:
                above_ec = any_nonzero(a, 16);
                left_ec = any_nonzero(l, 4);
                break;
            default: Debug.Assert(false, "Invalid transform size."); break;
        }
        return combine_entropy_contexts(above_ec, left_ec);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TX_SIZE get_txsize_entropy_ctx(TX_SIZE txsize)
    {
        return (TX_SIZE)((txsize_sqr_map[txsize] + txsize_sqr_up_map[txsize] + 1) >>
                         1);
    }
}
