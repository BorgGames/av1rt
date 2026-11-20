// Port of libaom av1/common/cdef.h

// <gen:aliases>
global using BOUNDARY = int;
// </gen:aliases>

global using static Av1rt.Aom.cdef;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

/*!\brief Parameters related to CDEF Block */
// [port] Class: large (dlist, dir, var) and only used through pointers.
internal sealed partial class CdefBlockInfo
{
    public Ptr<ushort> src;                                           /*!< CDEF intermediate buffer */
    public readonly Ptr<ushort>[] top_linebuf = new Ptr<ushort>[MAX_MB_PLANE]; /*!< CDEF top line buffer */
    public readonly Ptr<ushort>[] bot_linebuf = new Ptr<ushort>[MAX_MB_PLANE]; /*!< CDEF bottom line buffer */
    public Ptr<byte> dst;                                             /*!< CDEF destination buffer */
    public readonly cdef_list[] dlist = new cdef_list[MI_SIZE_64X64 * MI_SIZE_64X64]; /*!< CDEF 8x8 block positions */

    public int xdec;                       /*!< Sub-sampling X */
    public int ydec;                       /*!< Sub-sampling X */
    public int mi_wide_l2;                 /*!< Pixels per mi unit in width */
    public int mi_high_l2;                 /*!< Pixels per mi unit in height */
    public readonly int[] frame_boundary = new int[BOUNDARIES]; /*!< frame boundaries */

    public int damping;     /*!< CDEF damping factor */
    public int coeff_shift; /*!< Bit-depth based shift for calculating filter strength */
    public int level;       /*!< CDEF filtering level */
    public int sec_strength; /*!< CDEF secondary strength */
    public int cdef_count;   /*!< Number of CDEF sub-blocks in superblock */
    public readonly int[][] dir = new_jagged<int>(CDEF_NBLOCKS, CDEF_NBLOCKS); /*!< CDEF filter direction for all 8x8 sub-blocks*/
    public readonly int[][] var = new_jagged<int>(CDEF_NBLOCKS, CDEF_NBLOCKS); /*!< variance for all 8x8 sub-blocks */

    public int dst_stride; /*!< CDEF destination buffer stride */
    public int coffset;    /*!< current superblock offset in a row */
    public int roffset;    /*!< current row offset */
}

// [port] `uint16_t **const linebuf` is the caller's array of MAX_MB_PLANE line buffers.
internal delegate void cdef_init_fb_row_t(
    AV1_COMMON cm, MACROBLOCKD xd,
    CdefBlockInfo fb_info, Ptr<ushort>[] linebuf, Ptr<ushort> src,
    AV1CdefSync cdef_sync, int fbr);

internal static partial class cdef
{
    // <gen:consts>
    internal const int CDEF_STRENGTH_BITS = 6;
    internal const int CDEF_PRI_STRENGTHS = 16;
    internal const int CDEF_SEC_STRENGTHS = 4;

    // BOUNDARY
    internal const int TOP = 0;
    internal const int LEFT = 1;
    internal const int BOTTOM = 2;
    internal const int RIGHT = 3;
    internal const int BOUNDARIES = 4;
    // </gen:consts>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int sign(int i) { return i < 0 ? -1 : 1; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int constrain(int diff, int threshold, int damping)
    {
        if (threshold == 0) return 0;

        int shift = damping - get_msb((uint)threshold);
        shift = AOMMAX(0, shift);
        return sign(diff) * clamp(threshold - (Math.Abs(diff) >> shift), 0, Math.Abs(diff));
    }
}
