// Stubs for av1/common/thread_common.c: delete this file when av1/common/thread_common.c is ported.
global using static Av1rt.Aom.stubs_av1_common_thread_common_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_common_thread_common_c
{
    // av1/common/thread_common.c
    internal static void av1_loop_filter_dealloc(AV1LfSync lf_sync) =>
        throw new NotImplementedException();

    internal static void av1_loop_filter_alloc(AV1LfSync lf_sync, AV1_COMMON cm, int rows,
                                               int width, int num_workers) =>
        throw new NotImplementedException();

    internal static void av1_set_vert_loop_filter_done(AV1_COMMON cm, AV1LfSync lf_sync,
                                                       int num_mis_in_lpf_unit_height_log2) =>
        throw new NotImplementedException();

    internal static int av1_get_intrabc_extra_top_right_sb_delay(AV1_COMMON cm) =>
        throw new NotImplementedException();

    internal static void av1_thread_loop_filter_rows(
        YV12_BUFFER_CONFIG frame_buffer, AV1_COMMON cm, macroblockd_plane[] planes,
        MACROBLOCKD xd, int mi_row, int plane, int dir, int lpf_opt_level, AV1LfSync lf_sync,
        aom_internal_error_info error_info, Ptr<AV1_DEBLOCKING_PARAMETERS> params_buf,
        Ptr<TX_SIZE> tx_buf, int mib_size_log2) =>
        throw new NotImplementedException();
}
