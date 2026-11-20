// Stubs for av1/encoder/ethread.c: delete this file when av1/encoder/ethread.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_ethread_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_ethread_c
{
    // av1/encoder/ethread.c (f4), called from encoder.c / av1_cx_iface.c (g1)
    internal static void av1_row_mt_mem_dealloc(AV1_COMP cpi) =>
        throw new NotImplementedException();

    internal static void av1_row_mt_sync_mem_dealloc(AV1EncRowMultiThreadSync row_mt_sync) =>
        throw new NotImplementedException();

    internal static void av1_compute_num_workers_for_mt(AV1_COMP cpi) =>
        throw new NotImplementedException();

    internal static int av1_get_max_num_workers(AV1_COMP cpi) =>
        throw new NotImplementedException();

    internal static int av1_get_num_mod_workers_for_alloc(PrimaryMultiThreadInfo p_mt_info,
                                                          MULTI_THREADED_MODULES mod_name) =>
        throw new NotImplementedException();

    internal static void av1_create_workers(AV1_PRIMARY ppi, int num_workers) =>
        throw new NotImplementedException();

    internal static void av1_terminate_workers(AV1_PRIMARY ppi) =>
        throw new NotImplementedException();

    internal static void av1_init_frame_mt(AV1_PRIMARY ppi, AV1_COMP cpi) =>
        throw new NotImplementedException();

    internal static void av1_init_cdef_worker(AV1_COMP cpi) =>
        throw new NotImplementedException();

    internal static void av1_init_mt_sync(AV1_COMP cpi, int is_first_pass) =>
        throw new NotImplementedException();

    internal static void av1_init_tile_thread_data(AV1_PRIMARY ppi, int is_first_pass) =>
        throw new NotImplementedException();

    internal static void av1_cdef_mt_dealloc(AV1CdefSync cdef_sync) =>
        throw new NotImplementedException();

    internal static int av1_check_fpmt_config(AV1_PRIMARY ppi, AV1EncoderConfig oxcf) =>
        throw new NotImplementedException();
}
