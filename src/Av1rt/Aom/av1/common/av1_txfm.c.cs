// Port of libaom av1/common/av1_txfm.c

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class av1_txfm
{
    // [port] av1_cospi_arr_data[4][64] and av1_sinpi_arr_data[4][5] are not duplicated here:
    // cospi_arr()/sinpi_arr() in av1_txfm.h.cs read identical inline copies of these tables.
    // [subset] The *_q13 / *_s32 tables are only used by the Arm Neon transforms (HAVE_NEON).

    internal static void av1_round_shift_array_c(Ptr<int> arr, int size, int bit)
    {
        int i;
        if (bit == 0)
        {
            return;
        }
        else
        {
            if (bit > 0)
            {
                for (i = 0; i < size; i++)
                {
                    arr[i] = round_shift(arr[i], bit);
                }
            }
            else
            {
                for (i = 0; i < size; i++)
                {
                    arr[i] = (int)clamp64(((long)1 << (-bit)) * arr[i], int.MinValue,
                                          int.MaxValue);
                }
            }
        }
    }

    // av1_rtcd.h dispatched name.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_round_shift_array(Ptr<int> arr, int size, int bit) =>
        av1_round_shift_array_c(arr, size, bit);

    internal static readonly TXFM_TYPE[][] av1_txfm_type_ls = {
        new TXFM_TYPE[] { TXFM_TYPE_DCT4, TXFM_TYPE_ADST4, TXFM_TYPE_ADST4, TXFM_TYPE_IDENTITY4 },
        new TXFM_TYPE[] { TXFM_TYPE_DCT8, TXFM_TYPE_ADST8, TXFM_TYPE_ADST8, TXFM_TYPE_IDENTITY8 },
        new TXFM_TYPE[] { TXFM_TYPE_DCT16, TXFM_TYPE_ADST16, TXFM_TYPE_ADST16, TXFM_TYPE_IDENTITY16 },
        new TXFM_TYPE[] { TXFM_TYPE_DCT32, TXFM_TYPE_INVALID, TXFM_TYPE_INVALID,
                          TXFM_TYPE_IDENTITY32 },
        new TXFM_TYPE[] { TXFM_TYPE_DCT64, TXFM_TYPE_INVALID, TXFM_TYPE_INVALID, TXFM_TYPE_INVALID }
    };

    internal static readonly sbyte[] av1_txfm_stage_num_list = {
        4,   // TXFM_TYPE_DCT4
        6,   // TXFM_TYPE_DCT8
        8,   // TXFM_TYPE_DCT16
        10,  // TXFM_TYPE_DCT32
        12,  // TXFM_TYPE_DCT64
        7,   // TXFM_TYPE_ADST4
        8,   // TXFM_TYPE_ADST8
        10,  // TXFM_TYPE_ADST16
        1,   // TXFM_TYPE_IDENTITY4
        1,   // TXFM_TYPE_IDENTITY8
        1,   // TXFM_TYPE_IDENTITY16
        1,   // TXFM_TYPE_IDENTITY32
    };

    // CONFIG_COEFFICIENT_RANGE_CHECKING is 0: the check compiles to nothing.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_range_check_buf(int stage, Ptr<int> input, Ptr<int> buf, int size,
                                             sbyte bit)
    {
        _ = stage;
        _ = input;
        _ = buf;
        _ = size;
        _ = bit;
    }

    // [port] Overload for the 1-D transforms, whose stage buffers (bf0/bf1) are spans over
    // `output` or the stack `step` array.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_range_check_buf(int stage, Ptr<int> input, ReadOnlySpan<int> buf,
                                             int size, sbyte bit)
    {
        _ = stage;
        _ = input;
        _ = buf;
        _ = size;
        _ = bit;
    }
}
