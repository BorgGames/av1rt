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

    // av1/common/thread_common.c (e4), called from encoder.c (g1)
    internal static void av1_cdef_frame_mt(AV1_COMMON cm, MACROBLOCKD xd,
                                           AV1CdefWorkerData[] cdef_worker,
                                           AVxWorker[] workers, AV1CdefSync cdef_sync,
                                           int num_workers,
                                           cdef_init_fb_row_t cdef_init_fb_row_fn,
                                           int do_extend_border) =>
        throw new NotImplementedException();

    internal static void av1_cdef_init_fb_row_mt(AV1_COMMON cm, MACROBLOCKD xd,
                                                 CdefBlockInfo fb_info, Ptr<ushort>[] linebuf,
                                                 Ptr<ushort> src, AV1CdefSync cdef_sync,
                                                 int fbr) =>
        throw new NotImplementedException();

    internal static void av1_loop_filter_frame_mt(YV12_BUFFER_CONFIG frame, AV1_COMMON cm,
                                                  MACROBLOCKD xd, int plane_start,
                                                  int plane_end, int partial_frame,
                                                  AVxWorker[] workers, int num_workers,
                                                  AV1LfSync lf_sync, int lpf_opt_level) =>
        throw new NotImplementedException();
}
