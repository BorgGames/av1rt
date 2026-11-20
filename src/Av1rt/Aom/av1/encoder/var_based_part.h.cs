// Port of libaom av1/encoder/var_based_part.h
global using static Av1rt.Aom.var_based_part;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class var_based_part
{
    // Calculate block index x and y from split level and index
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GET_BLK_IDX_X(int idx, int level) => (((idx) & (0x01)) << (level));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GET_BLK_IDX_Y(int idx, int level) => (((idx) >> (0x01)) << (level));

    internal const int QINDEX_LARGE_BLOCK_THR =
        100; // Use increased thresholds for midres for speed 9 when qindex is above
             // this threshold

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long CALC_CHROMA_THRESH_FOR_ZEROMV_SKIP(long thresh_exit_part) =>
        ((3 * (thresh_exit_part)) >> 2);
}
