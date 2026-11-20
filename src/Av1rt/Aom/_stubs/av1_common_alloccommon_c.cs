// Stubs for av1/common/alloccommon.c: delete this file when av1/common/alloccommon.c is ported.
global using static Av1rt.Aom.stubs_av1_common_alloccommon_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_common_alloccommon_c
{
    // av1/common/alloccommon.c (e5)
    internal static int av1_alloc_context_buffers(AV1_COMMON cm, int width, int height,
                                                  BLOCK_SIZE min_partition_size) =>
        throw new NotImplementedException();

    internal static void av1_free_context_buffers(AV1_COMMON cm) =>
        throw new NotImplementedException();

    // C: AV1CdefWorkerData **cdef_worker (the callee frees and nulls the array).
    internal static void av1_free_cdef_buffers(AV1_COMMON cm, ref AV1CdefWorkerData[] cdef_worker,
                                               AV1CdefSync cdef_sync) =>
        throw new NotImplementedException();
}
