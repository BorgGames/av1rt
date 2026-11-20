// Stubs for av1/common/quant_common.c: delete this file when av1/common/quant_common.c is ported.
global using static Av1rt.Aom.stubs_av1_common_quant_common_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_common_quant_common_c
{
    // av1/common/quant_common.c (e1)
    // [e7] Working stub (8-bit only) so the rate control port and its differential tests
    // run before e1 lands. The table is a verbatim copy of libaom's ac_qlookup_QTX.
    // Delete it with this file when quant_common.c.cs is merged.
    private static readonly short[] e7_ac_qlookup_QTX = {
        4,    8,    9,    10,   11,   12,   13,   14,   15,   16,   17,   18,   19,
        20,   21,   22,   23,   24,   25,   26,   27,   28,   29,   30,   31,   32,
        33,   34,   35,   36,   37,   38,   39,   40,   41,   42,   43,   44,   45,
        46,   47,   48,   49,   50,   51,   52,   53,   54,   55,   56,   57,   58,
        59,   60,   61,   62,   63,   64,   65,   66,   67,   68,   69,   70,   71,
        72,   73,   74,   75,   76,   77,   78,   79,   80,   81,   82,   83,   84,
        85,   86,   87,   88,   89,   90,   91,   92,   93,   94,   95,   96,   97,
        98,   99,   100,  101,  102,  104,  106,  108,  110,  112,  114,  116,  118,
        120,  122,  124,  126,  128,  130,  132,  134,  136,  138,  140,  142,  144,
        146,  148,  150,  152,  155,  158,  161,  164,  167,  170,  173,  176,  179,
        182,  185,  188,  191,  194,  197,  200,  203,  207,  211,  215,  219,  223,
        227,  231,  235,  239,  243,  247,  251,  255,  260,  265,  270,  275,  280,
        285,  290,  295,  300,  305,  311,  317,  323,  329,  335,  341,  347,  353,
        359,  366,  373,  380,  387,  394,  401,  408,  416,  424,  432,  440,  448,
        456,  465,  474,  483,  492,  501,  510,  520,  530,  540,  550,  560,  571,
        582,  593,  604,  615,  627,  639,  651,  663,  676,  689,  702,  715,  729,
        743,  757,  771,  786,  801,  816,  832,  848,  864,  881,  898,  915,  933,
        951,  969,  988,  1007, 1026, 1046, 1066, 1087, 1108, 1129, 1151, 1173, 1196,
        1219, 1243, 1267, 1292, 1317, 1343, 1369, 1396, 1423, 1451, 1479, 1508, 1537,
        1567, 1597, 1628, 1660, 1692, 1725, 1759, 1793, 1828,
    };

    internal static short av1_ac_quant_QTX(int qindex, int delta, aom_bit_depth_t bit_depth)
    {
        int q_clamped = clamp(qindex + delta, 0, MAXQ);
        switch (bit_depth)
        {
            case AOM_BITS_8: return e7_ac_qlookup_QTX[q_clamped];
            default: throw new NotImplementedException();
        }
    }
}
