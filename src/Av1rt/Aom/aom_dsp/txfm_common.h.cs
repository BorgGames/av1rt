// Port of libaom aom_dsp/txfm_common.h

global using static Av1rt.Aom.txfm_common;
global using TX_SIZE = int;
global using TX_TYPE = int;
global using TxSetType = int;

namespace Av1rt.Aom;

// [port] A struct: callers declare it as a local, fill it and pass its address (PORTING 3.3).
// Functions taking `const TxfmParam *` take `in TxfmParam`.
internal partial struct TxfmParam
{
    // for both forward and inverse transforms
    public TX_TYPE tx_type;
    public TX_SIZE tx_size;
    public int lossless;
    public int bd;
    // are the pixel buffers octets or shorts?  This should collapse to
    // bd==8 implies !is_hbd, but that's not certain right now.
    public int is_hbd;
    public TxSetType tx_set_type;
    // for inverse transforms only
    public int eob;
}

internal static partial class txfm_common
{
    // Constants and Macros used by all idct/dct functions
    internal const int DCT_CONST_BITS = 14;
    internal const int DCT_CONST_ROUNDING = (1 << (DCT_CONST_BITS - 1));

    internal const int UNIT_QUANT_SHIFT = 2;
    internal const int UNIT_QUANT_FACTOR = (1 << UNIT_QUANT_SHIFT);

    // TX_SIZE
    internal const int TX_4X4 = 0;              // 4x4 transform
    internal const int TX_8X8 = 1;              // 8x8 transform
    internal const int TX_16X16 = 2;            // 16x16 transform
    internal const int TX_32X32 = 3;            // 32x32 transform
    internal const int TX_64X64 = 4;            // 64x64 transform
    internal const int TX_4X8 = 5;              // 4x8 transform
    internal const int TX_8X4 = 6;              // 8x4 transform
    internal const int TX_8X16 = 7;             // 8x16 transform
    internal const int TX_16X8 = 8;             // 16x8 transform
    internal const int TX_16X32 = 9;            // 16x32 transform
    internal const int TX_32X16 = 10;           // 32x16 transform
    internal const int TX_32X64 = 11;           // 32x64 transform
    internal const int TX_64X32 = 12;           // 64x32 transform
    internal const int TX_4X16 = 13;            // 4x16 transform
    internal const int TX_16X4 = 14;            // 16x4 transform
    internal const int TX_8X32 = 15;            // 8x32 transform
    internal const int TX_32X8 = 16;            // 32x8 transform
    internal const int TX_16X64 = 17;           // 16x64 transform
    internal const int TX_64X16 = 18;           // 64x16 transform
    internal const int TX_SIZES_ALL = 19;       // Includes rectangular transforms
    internal const int TX_SIZES = TX_4X8;       // Does NOT include rectangular transforms
    internal const int TX_SIZES_LARGEST = TX_64X64;
    internal const int TX_INVALID = 255;        // Invalid transform size

    // TX_TYPE
    internal const int DCT_DCT = 0;             // DCT in both horizontal and vertical
    internal const int ADST_DCT = 1;            // ADST in vertical, DCT in horizontal
    internal const int DCT_ADST = 2;            // DCT in vertical, ADST in horizontal
    internal const int ADST_ADST = 3;           // ADST in both directions
    internal const int FLIPADST_DCT = 4;        // FLIPADST in vertical, DCT in horizontal
    internal const int DCT_FLIPADST = 5;        // DCT in vertical, FLIPADST in horizontal
    internal const int FLIPADST_FLIPADST = 6;   // FLIPADST in both directions
    internal const int ADST_FLIPADST = 7;       // ADST in vertical, FLIPADST in horizontal
    internal const int FLIPADST_ADST = 8;       // FLIPADST in vertical, ADST in horizontal
    internal const int IDTX = 9;                // Identity in both directions
    internal const int V_DCT = 10;              // DCT in vertical, identity in horizontal
    internal const int H_DCT = 11;              // Identity in vertical, DCT in horizontal
    internal const int V_ADST = 12;             // ADST in vertical, identity in horizontal
    internal const int H_ADST = 13;             // Identity in vertical, ADST in horizontal
    internal const int V_FLIPADST = 14;         // FLIPADST in vertical, identity in horizontal
    internal const int H_FLIPADST = 15;         // Identity in vertical, FLIPADST in horizontal
    internal const int TX_TYPES = 16;
    internal const int DCT_ADST_TX_MASK = 0x000F;  // Either DCT or ADST in each direction
    internal const int TX_TYPE_INVALID = 255;      // Invalid transform type

    // TxSetType
    // DCT only
    internal const int EXT_TX_SET_DCTONLY = 0;
    // DCT + Identity only
    internal const int EXT_TX_SET_DCT_IDTX = 1;
    // Discrete Trig transforms w/o flip (4) + Identity (1)
    internal const int EXT_TX_SET_DTT4_IDTX = 2;
    // Discrete Trig transforms w/o flip (4) + Identity (1) + 1D Hor/vert DCT (2)
    internal const int EXT_TX_SET_DTT4_IDTX_1DDCT = 3;
    // Discrete Trig transforms w/ flip (9) + Identity (1) + 1D Hor/Ver DCT (2)
    internal const int EXT_TX_SET_DTT9_IDTX_1DDCT = 4;
    // Discrete Trig transforms w/ flip (9) + Identity (1) + 1D Hor/Ver (6)
    internal const int EXT_TX_SET_ALL16 = 5;
    internal const int EXT_TX_SET_TYPES = 6;

    // Constants:
    //  for (int i = 1; i< 32; ++i)
    //    printf("static const int cospi_%d_64 = %.0f;\n", i,
    //           round(16384 * cos(i*PI/64)));
    // Note: sin(k*Pi/64) = cos((32-k)*Pi/64)
    internal const tran_high_t cospi_1_64 = 16364;
    internal const tran_high_t cospi_2_64 = 16305;
    internal const tran_high_t cospi_3_64 = 16207;
    internal const tran_high_t cospi_4_64 = 16069;
    internal const tran_high_t cospi_5_64 = 15893;
    internal const tran_high_t cospi_6_64 = 15679;
    internal const tran_high_t cospi_7_64 = 15426;
    internal const tran_high_t cospi_8_64 = 15137;
    internal const tran_high_t cospi_9_64 = 14811;
    internal const tran_high_t cospi_10_64 = 14449;
    internal const tran_high_t cospi_11_64 = 14053;
    internal const tran_high_t cospi_12_64 = 13623;
    internal const tran_high_t cospi_13_64 = 13160;
    internal const tran_high_t cospi_14_64 = 12665;
    internal const tran_high_t cospi_15_64 = 12140;
    internal const tran_high_t cospi_16_64 = 11585;
    internal const tran_high_t cospi_17_64 = 11003;
    internal const tran_high_t cospi_18_64 = 10394;
    internal const tran_high_t cospi_19_64 = 9760;
    internal const tran_high_t cospi_20_64 = 9102;
    internal const tran_high_t cospi_21_64 = 8423;
    internal const tran_high_t cospi_22_64 = 7723;
    internal const tran_high_t cospi_23_64 = 7005;
    internal const tran_high_t cospi_24_64 = 6270;
    internal const tran_high_t cospi_25_64 = 5520;
    internal const tran_high_t cospi_26_64 = 4756;
    internal const tran_high_t cospi_27_64 = 3981;
    internal const tran_high_t cospi_28_64 = 3196;
    internal const tran_high_t cospi_29_64 = 2404;
    internal const tran_high_t cospi_30_64 = 1606;
    internal const tran_high_t cospi_31_64 = 804;

    //  16384 * sqrt(2) * sin(kPi/9) * 2 / 3
    internal const tran_high_t sinpi_1_9 = 5283;
    internal const tran_high_t sinpi_2_9 = 9929;
    internal const tran_high_t sinpi_3_9 = 13377;
    internal const tran_high_t sinpi_4_9 = 15212;

    // 16384 * sqrt(2)
    internal const tran_high_t Sqrt2 = 23170;
    internal const tran_high_t InvSqrt2 = 11585;

    // [subset] fdct_round_shift never runs in the subset and is not ported.
}
