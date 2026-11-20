// Port of libaom av1/common/thread_common.h

// [port] Struct tags that other headers and .c files use in place of the typedef names.
global using AV1LfSyncData = Av1rt.Aom.AV1LfSync;
global using AV1LrSyncData = Av1rt.Aom.AV1LrSync;
global using LoopRestorationWorkerData = Av1rt.Aom.LRWorkerData;
global using AV1CdefWorker = Av1rt.Aom.AV1CdefWorkerData;
global using AV1CdefSyncData = Av1rt.Aom.AV1CdefSync;

global using static Av1rt.Aom.thread_common;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// Loopfilter row synchronization
internal partial struct AV1LfMTInfo
{
    public int mi_row;
    public int plane;
    public int dir;
    public int lpf_opt_level;
}

// [port] pthread objects follow aom_pthread.h.cs: `pthread_mutex_t *mutex_[MAX_MB_PLANE]`
// (one array of sb_rows mutexes per plane) is a jagged array of mutex objects; a single
// allocated `pthread_mutex_t *` is the object itself.
internal sealed partial class AV1LfSync
{
    public readonly pthread_mutex_t[][] mutex_ = new pthread_mutex_t[MAX_MB_PLANE][];
    public readonly pthread_cond_t[][] cond_ = new pthread_cond_t[MAX_MB_PLANE][];
    // Allocate memory to store the loop-filtered superblock index in each row.
    public readonly Ptr<int>[] cur_sb_col = new Ptr<int>[MAX_MB_PLANE];
    // The optimal sync_range for different resolution and platform should be
    // determined by testing. Currently, it is chosen to be a power-of-2 number.
    public int sync_range;
    public int rows;

    // Row-based parallel loopfilter data
    public LFWorkerData[] lfdata;
    // [port] Not in C: the last lfdata array, reused by the next av1_loop_filter_alloc for the
    // same worker count. The encoder re-allocates the sync every frame (av1_init_mt_sync counts
    // 128-pixel superblock rows, lpf_pipeline_mt_init the sequence's), which is a cheap malloc
    // in C but rebuilds every LFWorkerData's plane buffers here.
    public LFWorkerData[] lfdata_pool;
    public int num_workers;
    public pthread_mutex_t job_mutex;
    public Ptr<AV1LfMTInfo> job_queue;
    public int jobs_enqueued;
    public int jobs_dequeued;

    // Initialized to false, set to true by the worker thread that encounters an
    // error in order to abort the processing of other worker threads.
    public bool lf_mt_exit;
}

internal partial struct AV1LrMTInfo
{
    public int v_start;
    public int v_end;
    public int lr_unit_row;
    public int plane;
    public int sync_mode;
    public int v_copy_start;
    public int v_copy_end;
}

internal sealed partial class LRWorkerData
{
    public Ptr<int> rst_tmpbuf;
    public object rlbs;
    public object lr_ctxt;
    public int do_extend_border;
    public readonly aom_internal_error_info error_info = new();
}

// Looprestoration row synchronization
internal sealed partial class AV1LrSync
{
    public readonly pthread_mutex_t[][] mutex_ = new pthread_mutex_t[MAX_MB_PLANE][];
    public readonly pthread_cond_t[][] cond_ = new pthread_cond_t[MAX_MB_PLANE][];
    // Allocate memory to store the loop-restoration block index in each row.
    public readonly Ptr<int>[] cur_sb_col = new Ptr<int>[MAX_MB_PLANE];
    // The optimal sync_range for different resolution and platform should be
    // determined by testing. Currently, it is chosen to be a power-of-2 number.
    public int sync_range;
    public int rows;
    public int num_planes;

    public int num_workers;

    public pthread_mutex_t job_mutex;
    // Row-based parallel loopfilter data
    public LRWorkerData[] lrworkerdata;

    public Ptr<AV1LrMTInfo> job_queue;
    public int jobs_enqueued;
    public int jobs_dequeued;
    // Initialized to false, set to true by the worker thread that encounters
    // an error in order to abort the processing of other worker threads.
    public bool lr_mt_exit;
}

internal sealed partial class AV1CdefWorkerData
{
    public AV1_COMMON cm;
    public MACROBLOCKD xd;
    public readonly Ptr<ushort>[] colbuf = new Ptr<ushort>[MAX_MB_PLANE];
    public Ptr<ushort> srcbuf;
    public readonly Ptr<ushort>[] linebuf = new Ptr<ushort>[MAX_MB_PLANE];
    public cdef_init_fb_row_t cdef_init_fb_row_fn;
    public int do_extend_border;
    public readonly aom_internal_error_info error_info = new();
}

internal partial struct AV1CdefRowSync
{
    public pthread_mutex_t row_mutex_;
    public pthread_cond_t row_cond_;
    public int is_row_done;
}

// Data related to CDEF search multi-thread synchronization.
internal sealed partial class AV1CdefSync
{
    // Mutex lock used while dispatching jobs.
    public pthread_mutex_t mutex_;
    // Data related to CDEF row mt sync information
    public Ptr<AV1CdefRowSync> cdef_row_mt;
    // Flag to indicate all blocks are processed and end of frame is reached
    public int end_of_frame;
    // Row index in units of 64x64 block
    public int fbr;
    // Column index in units of 64x64 block
    public int fbc;
    // Initialized to false, set to true by the worker thread that encounters
    // an error in order to abort the processing of other worker threads.
    public bool cdef_mt_exit;
}

internal static partial class thread_common
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool skip_loop_filter_plane(
        Ptr<int> planes_to_lf, int plane, int lpf_opt_level)
    {
        // If LPF_PICK_METHOD is LPF_PICK_FROM_Q, we have the option to filter both
        // chroma planes together
        if (lpf_opt_level == 2)
        {
            if (plane == AOM_PLANE_Y)
            {
                return planes_to_lf[plane] == 0;
            }
            if (plane == AOM_PLANE_U)
            {
                // U and V are handled together
                return planes_to_lf[1] == 0 && planes_to_lf[2] == 0;
            }
            Debug.Assert(plane == AOM_PLANE_V);
            if (plane == AOM_PLANE_V)
            {
                // V is handled when u is filtered
                return true;
            }
        }

        // Normal operation mode
        return planes_to_lf[plane] == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void enqueue_lf_jobs(AV1LfSync lf_sync, int start, int stop,
                                         Ptr<int> planes_to_lf,
                                         int lpf_opt_level,
                                         int num_mis_in_lpf_unit_height)
    {
        int mi_row, plane, dir;
        Ptr<AV1LfMTInfo> lf_job_queue = lf_sync.job_queue;
        lf_sync.jobs_enqueued = 0;
        lf_sync.jobs_dequeued = 0;

        // Launch all vertical jobs first, as they are blocking the horizontal ones.
        // Launch top row jobs for all planes first, in case the output can be
        // partially reconstructed row by row.
        for (dir = 0; dir < 2; ++dir)
        {
            for (mi_row = start; mi_row < stop; mi_row += num_mis_in_lpf_unit_height)
            {
                for (plane = 0; plane < MAX_MB_PLANE; ++plane)
                {
                    if (skip_loop_filter_plane(planes_to_lf, plane, lpf_opt_level))
                    {
                        continue;
                    }
                    if (planes_to_lf[plane] == 0) continue;
                    lf_job_queue.Value.mi_row = mi_row;
                    lf_job_queue.Value.plane = plane;
                    lf_job_queue.Value.dir = dir;
                    lf_job_queue.Value.lpf_opt_level = lpf_opt_level;
                    lf_job_queue++;
                    lf_sync.jobs_enqueued++;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void loop_filter_frame_mt_init(
        AV1_COMMON cm, int start_mi_row, int end_mi_row,
        Ptr<int> planes_to_lf, int num_workers, AV1LfSync lf_sync,
        int lpf_opt_level, int num_mis_in_lpf_unit_height_log2)
    {
        // Number of superblock rows
        int sb_rows =
            CEIL_POWER_OF_TWO(cm.mi_params.mi_rows, num_mis_in_lpf_unit_height_log2);

        // [av1rt] C reallocates when the row count differs: the encoder's setup counts 128-pixel
        // rows and the loop filter pipelined with encoding 64-pixel rows, so it reallocated
        // twice per frame. lf_sync.rows only sizes the arrays; keep the larger allocation.
        if (lf_sync.sync_range == 0 || sb_rows > lf_sync.rows ||
            num_workers > lf_sync.num_workers)
        {
            av1_loop_filter_dealloc(lf_sync);
            av1_loop_filter_alloc(lf_sync, cm, sb_rows, cm.width, num_workers);
        }
        lf_sync.lf_mt_exit = false;

        // Initialize cur_sb_col to -1 for all SB rows.
        for (int i = 0; i < MAX_MB_PLANE; i++)
        {
            memset(lf_sync.cur_sb_col[i], -1, sb_rows);
        }

        enqueue_lf_jobs(lf_sync, start_mi_row, end_mi_row, planes_to_lf,
                        lpf_opt_level, (1 << num_mis_in_lpf_unit_height_log2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<AV1LfMTInfo> get_lf_job_info(AV1LfSync lf_sync)
    {
        Ptr<AV1LfMTInfo> cur_job_info = Ptr<AV1LfMTInfo>.Null;

        pthread_mutex_lock(lf_sync.job_mutex);

        if (!lf_sync.lf_mt_exit && lf_sync.jobs_dequeued < lf_sync.jobs_enqueued)
        {
            cur_job_info = lf_sync.job_queue + lf_sync.jobs_dequeued;
            lf_sync.jobs_dequeued++;
        }

        pthread_mutex_unlock(lf_sync.job_mutex);

        return cur_job_info;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void loop_filter_data_reset(LFWorkerData lf_data,
                                                YV12_BUFFER_CONFIG frame_buffer,
                                                AV1_COMMON cm,
                                                MACROBLOCKD xd)
    {
        macroblockd_plane[] pd = xd.plane;
        lf_data.frame_buffer = frame_buffer;
        lf_data.cm = cm;
        lf_data.xd = xd;
        for (int i = 0; i < MAX_MB_PLANE; i++)
        {
            lf_data.planes[i].dst = pd[i].dst;
            lf_data.planes[i].subsampling_x = pd[i].subsampling_x;
            lf_data.planes[i].subsampling_y = pd[i].subsampling_y;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_planes_to_loop_filter(loopfilter lf,
                                                   Ptr<int> planes_to_lf,
                                                   int plane_start, int plane_end)
    {
        // For each luma and chroma plane, whether to filter it or not.
        planes_to_lf[0] = B((lf.filter_level[0] != 0 || lf.filter_level[1] != 0) &&
                            plane_start <= 0 && 0 < plane_end);
        planes_to_lf[1] = B(lf.filter_level_u != 0 && plane_start <= 1 && 1 < plane_end);
        planes_to_lf[2] = B(lf.filter_level_v != 0 && plane_start <= 2 && 2 < plane_end);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int check_planes_to_loop_filter(loopfilter lf,
                                                    Ptr<int> planes_to_lf,
                                                    int plane_start, int plane_end)
    {
        set_planes_to_loop_filter(lf, planes_to_lf, plane_start, plane_end);
        // If the luma plane is purposely not filtered, neither are the chroma
        // planes.
        if (planes_to_lf[0] == 0 && plane_start <= 0 && 0 < plane_end) return 0;
        // Early exit.
        if (planes_to_lf[0] == 0 && planes_to_lf[1] == 0 && planes_to_lf[2] == 0) return 0;
        return 1;
    }
}
