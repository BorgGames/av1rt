// Port of libaom av1/encoder/partition_search.h
global using static Av1rt.Aom.partition_search;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class partition_search
{
    // PORT-NOTE: C takes `uint16_t *cb_offset`; callers pass x->cb_offset (ushort[] here) and
    // x->mbmi_ext_frame->cb_offset (an Arr2<ushort> inside the MB_MODE_INFO_EXT_FRAME struct),
    // so the parameter is a Span<ushort> rather than a Ptr<ushort>.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_cb_offsets(Span<ushort> cb_offset,
                                        ushort cb_offset_y,
                                        ushort cb_offset_uv)
    {
        cb_offset[PLANE_TYPE_Y] = cb_offset_y;
        cb_offset[PLANE_TYPE_UV] = cb_offset_uv;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void update_cb_offsets(MACROBLOCK x, BLOCK_SIZE bsize,
                                           int subsampling_x,
                                           int subsampling_y)
    {
        x.cb_offset[PLANE_TYPE_Y] += (ushort)(block_size_wide[bsize] * block_size_high[bsize]);
        if (x.e_mbd.is_chroma_ref)
        {
            BLOCK_SIZE plane_bsize =
                get_plane_block_size(bsize, subsampling_x, subsampling_y);
            Debug.Assert(plane_bsize != BLOCK_INVALID);
            x.cb_offset[PLANE_TYPE_UV] +=
                (ushort)(block_size_wide[plane_bsize] * block_size_high[plane_bsize]);
        }
    }
}
