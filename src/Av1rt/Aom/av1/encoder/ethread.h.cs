// Port of libaom av1/encoder/ethread.h
global using static Av1rt.Aom.ethread;

namespace Av1rt.Aom;

internal static partial class ethread
{
    // All other declarations are prototypes of ethread.c.
}

// Class: tile_thr_data is an array that is used through pointers
// (&p_mt_info->tile_thr_data[t], AVxWorker::data1).
internal sealed partial class EncWorkerData
{
    public AV1_COMP cpi;
    public ThreadData td;
    public ThreadData original_td;
    public aom_internal_error_info error_info = new();
    public AV1LfSync lf_sync;
    public LFWorkerData lf_data;
    public int start;
    public int thread_id;
}
