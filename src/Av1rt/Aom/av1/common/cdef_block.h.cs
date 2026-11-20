// Port of libaom av1/common/cdef_block.h

global using static Av1rt.Aom.cdef_block;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal partial struct cdef_list
{
    public byte by;
    public byte bx;
}

// PORT-NOTE: C declares `void *dest`; the 8-bit filters (cdef_filter_8_*) write uint8_t and
// the 16-bit ones (cdef_filter_16_*) write uint16_t, and cdef_block.c keeps them in separate
// tables. The typedef is therefore generic over the destination element type:
// cdef_filter_block_func<byte> / cdef_filter_block_func<ushort>.
internal delegate void cdef_filter_block_func<T>(Ptr<T> dest, int dstride,
                                                 Ptr<ushort> @in, int pri_strength,
                                                 int sec_strength, int dir,
                                                 int pri_damping, int sec_damping,
                                                 int coeff_shift, int block_width,
                                                 int block_height);

internal static partial class cdef_block
{
    // <gen:consts>
    internal const int CDEF_BLOCKSIZE = 64;
    internal const int CDEF_BLOCKSIZE_LOG2 = 6;
    internal const int CDEF_NBLOCKS = ((1 << MAX_SB_SIZE_LOG2) / 8);
    internal const int CDEF_SB_SHIFT = (MAX_SB_SIZE_LOG2 - CDEF_BLOCKSIZE_LOG2);
    internal const int CDEF_VBORDER = (2);
    internal const int CDEF_HBORDER = (8);
    internal const int CDEF_VERY_LARGE = (0x4000);
    // </gen:consts>

    // CDEF_BSTRIDE: ALIGN_POWER_OF_TWO((1 << MAX_SB_SIZE_LOG2) + 2 * CDEF_HBORDER, 3)
    internal const int CDEF_BSTRIDE = (((1 << MAX_SB_SIZE_LOG2) + 2 * CDEF_HBORDER) + ((1 << 3) - 1)) & ~((1 << 3) - 1);

    internal const int CDEF_INBUF_SIZE = (CDEF_BSTRIDE * ((1 << MAX_SB_SIZE_LOG2) + 2 * CDEF_VBORDER));

    // cdef_pri_taps[2][2], cdef_sec_taps[2] and cdef_directions are defined in cdef_block.c
    // (not part of p1a).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void fill_rect(Ptr<ushort> dst, int dstride, int v, int h,
                                   ushort x)
    {
        // [av1rt] Row fills within one checked window (C: element loop).
        if (v <= 0 || h <= 0) return;
        Span<ushort> d = dst.AsSpan(0, (v - 1) * dstride + h);
        for (int i = 0; i < v; i++)
            d.Slice(i * dstride, h).Fill(x);
    }
}
