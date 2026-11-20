// Port of libaom av1/common/cfl.c
// [subset] CfL is disabled in the realtime subset: only cfl_init (called from
// av1_init_macroblockd) runs. cfl_store_dc_pred, cfl_load_dc_pred*, cfl_pad,
// subtract_average_c, cfl_idx_to_alpha, cfl_predict_lbd_c, cfl_compute_parameters,
// av1_cfl_predict_block, the cfl_luma_subsampling_* kernels, cfl_store*, sub8x8_adjust_offset
// and max_intra_block_{width,height} never execute and are not ported.

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class cfl
{
    internal static void cfl_init(CFL_CTX cfl, SequenceHeader seq_params)
    {
        Debug.Assert(block_size_wide[CFL_MAX_BLOCK_SIZE] == CFL_BUF_LINE);
        Debug.Assert(block_size_high[CFL_MAX_BLOCK_SIZE] == CFL_BUF_LINE);

        cfl.recon_buf_q3.AsSpan().Clear();
        cfl.ac_buf_q3.AsSpan().Clear();
        cfl.subsampling_x = seq_params.subsampling_x;
        cfl.subsampling_y = seq_params.subsampling_y;
        cfl.are_parameters_computed = 0;
        cfl.store_y = 0;
        // The DC_PRED cache is disabled by default and is only enabled in
        // cfl_rd_pick_alpha
        clear_cfl_dc_pred_cache_flags(cfl);
    }
}
