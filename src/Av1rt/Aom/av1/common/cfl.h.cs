// Port of libaom av1/common/cfl.h
// [subset] CfL is disabled; only the predicates the encoder evaluates are ported. The
// CFL_* SIMD wrapper macros and the never-executed helpers are not.

global using static Av1rt.Aom.cfl;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class cfl
{
    // Can we use CfL for the current block?
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CFL_ALLOWED_TYPE is_cfl_allowed(MACROBLOCKD xd)
    {
        MB_MODE_INFO mbmi = xd.mi[0];
        BLOCK_SIZE bsize = mbmi.bsize;
        Debug.Assert(bsize < BLOCK_SIZES_ALL);
        if (xd.lossless[mbmi.segment_id] != 0)
        {
            // In lossless, CfL is available when the partition size is equal to the
            // transform size.
            int ssx = xd.plane[AOM_PLANE_U].subsampling_x;
            int ssy = xd.plane[AOM_PLANE_U].subsampling_y;
            int plane_bsize = get_plane_block_size(bsize, ssx, ssy);
            return (CFL_ALLOWED_TYPE)B(plane_bsize == BLOCK_4X4);
        }
        // Spec: CfL is available to luma partitions lesser than or equal to 32x32
        return (CFL_ALLOWED_TYPE)B(block_size_wide[bsize] <= 32 &&
                                   block_size_high[bsize] <= 32);
    }

    // Do we need to save the luma pixels from the current block,
    // for a possible future CfL prediction?
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CFL_ALLOWED_TYPE store_cfl_required(AV1_COMMON cm,
                                                        MACROBLOCKD xd)
    {
        MB_MODE_INFO mbmi = xd.mi[0];

        if (cm.seq_params.monochrome != 0) return CFL_DISALLOWED;

        if (!xd.is_chroma_ref)
        {
            // For non-chroma-reference blocks, we should always store the luma pixels,
            // in case the corresponding chroma-reference block uses CfL.
            // Note that this can only happen for block sizes which are <8 on
            // their shortest side, as otherwise they would be chroma reference
            // blocks.
            return CFL_ALLOWED;
        }

        // If this block has chroma information, we know whether we're
        // actually going to perform a CfL prediction
        return (CFL_ALLOWED_TYPE)B(is_inter_block(mbmi) == 0 &&
                                   mbmi.uv_mode == UV_CFL_PRED);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void clear_cfl_dc_pred_cache_flags(CFL_CTX cfl)
    {
        cfl.use_dc_pred_cache = false;
        cfl.dc_pred_is_cached[CFL_PRED_U] = false;
        cfl.dc_pred_is_cached[CFL_PRED_V] = false;
    }
}
