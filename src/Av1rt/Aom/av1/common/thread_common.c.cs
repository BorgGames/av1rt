// Port of libaom av1/common/thread_common.c
//
// [subset] Only the loop-filter and CDEF parts are ported (package e4). Loop restoration is off
// in the subset: get_lr_sync_range, lr_sync_read/write, av1_loop_restoration_alloc/dealloc,
// enqueue_lr_jobs, get_lr_job_info, set_loop_restoration_done, loop_restoration_row_worker,
// sync_lr_workers, foreach_rest_unit_in_planes_mt and av1_loop_restoration_filter_frame_mt are
// not ported. av1_alloc_cdef_sync / av1_free_cdef_sync are decoder-only (never executed by the
// encoder, which allocates cdef_sync->mutex_ in ethread.c).

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class thread_common
{
    // Set up nsync by width.
    private static int get_sync_range(int width)
    {
        // nsync numbers are picked by testing. For example, for 4k
        // video, using 4 gives best performance.
        if (width < 640)
            return 1;
        else if (width <= 1280)
            return 2;
        else if (width <= 4096)
            return 4;
        else
            return 8;
    }

    // Allocate memory for lf row synchronization
    internal static void av1_loop_filter_alloc(AV1LfSync lf_sync, AV1_COMMON cm, int rows,
                                               int width, int num_workers)
    {
        lf_sync.rows = rows;
        {
            int i, j;

            for (j = 0; j < MAX_MB_PLANE; j++)
            {
                lf_sync.mutex_[j] = new pthread_mutex_t[rows];
                for (i = 0; i < rows; ++i)
                {
                    pthread_mutex_init(ref lf_sync.mutex_[j][i], null);
                }

                lf_sync.cond_[j] = new pthread_cond_t[rows];
                for (i = 0; i < rows; ++i)
                {
                    pthread_cond_init(ref lf_sync.cond_[j][i], null);
                }
            }

            pthread_mutex_init(ref lf_sync.job_mutex, null);
        }
        // [port] aom_malloc of num_workers LFWorkerData structs: the class instances are
        // created here (their fields are set by loop_filter_data_reset before use).
        if (lf_sync.lfdata_pool == null || lf_sync.lfdata_pool.Length != num_workers)
            lf_sync.lfdata_pool = new_array<LFWorkerData>(num_workers);
        lf_sync.lfdata = lf_sync.lfdata_pool;
        lf_sync.num_workers = num_workers;

        for (int j = 0; j < MAX_MB_PLANE; j++)
        {
            lf_sync.cur_sb_col[j] = new int[rows];
        }
        lf_sync.job_queue = new AV1LfMTInfo[rows * MAX_MB_PLANE * 2];
        // Set up nsync.
        lf_sync.sync_range = get_sync_range(width);
    }

    // Deallocate lf synchronization related mutex and data
    internal static void av1_loop_filter_dealloc(AV1LfSync lf_sync)
    {
        if (lf_sync != null)
        {
            int j;
            int i;
            for (j = 0; j < MAX_MB_PLANE; j++)
            {
                if (lf_sync.mutex_[j] != null)
                {
                    for (i = 0; i < lf_sync.rows; ++i)
                    {
                        pthread_mutex_destroy(lf_sync.mutex_[j][i]);
                    }
                    lf_sync.mutex_[j] = null;
                }
                if (lf_sync.cond_[j] != null)
                {
                    for (i = 0; i < lf_sync.rows; ++i)
                    {
                        pthread_cond_destroy(lf_sync.cond_[j][i]);
                    }
                    lf_sync.cond_[j] = null;
                }
            }
            if (lf_sync.job_mutex != null)
            {
                pthread_mutex_destroy(lf_sync.job_mutex);
                lf_sync.job_mutex = null;
            }
            lf_sync.lfdata = null;
            for (j = 0; j < MAX_MB_PLANE; j++)
            {
                lf_sync.cur_sb_col[j] = Ptr<int>.Null;
            }

            lf_sync.job_queue = Ptr<AV1LfMTInfo>.Null;
            // clear the structure as the source of this call may be a resize in which
            // case this call will be followed by an _alloc() which may fail.
            // [port] av1_zero(*lf_sync): every field reset by hand (the arrays above are
            // already null).
            lf_sync.sync_range = 0;
            lf_sync.rows = 0;
            lf_sync.num_workers = 0;
            lf_sync.jobs_enqueued = 0;
            lf_sync.jobs_dequeued = 0;
            lf_sync.lf_mt_exit = false;
        }
    }

    private static void cdef_row_mt_sync_read(AV1CdefSync cdef_sync, int row)
    {
        if (row == 0) return;
        Ptr<AV1CdefRowSync> cdef_row_mt = cdef_sync.cdef_row_mt;
        pthread_mutex_lock(cdef_row_mt[row - 1].row_mutex_);
        while (cdef_row_mt[row - 1].is_row_done != 1)
            pthread_cond_wait(cdef_row_mt[row - 1].row_cond_,
                              cdef_row_mt[row - 1].row_mutex_);
        cdef_row_mt[row - 1].is_row_done = 0;
        pthread_mutex_unlock(cdef_row_mt[row - 1].row_mutex_);
    }

    private static void cdef_row_mt_sync_write(AV1CdefSync cdef_sync, int row)
    {
        Ptr<AV1CdefRowSync> cdef_row_mt = cdef_sync.cdef_row_mt;
        pthread_mutex_lock(cdef_row_mt[row].row_mutex_);
        pthread_cond_signal(cdef_row_mt[row].row_cond_);
        cdef_row_mt[row].is_row_done = 1;
        pthread_mutex_unlock(cdef_row_mt[row].row_mutex_);
    }

    // Value of cur_sb_col[plane][r] after a worker has encountered an error. It
    // wakes up the workers waiting in sync_read() and tells them to exit.
    private const int LF_SYNC_EXIT = int.MaxValue;

    // Returns true if the sync read is successful and the caller can continue.
    // Returns false if the caller should exit because a worker has encountered an
    // error.
    private static bool sync_read(AV1LfSync lf_sync, int r, int c, int plane)
    {
        bool lf_mt_continue = true;
        int nsync = lf_sync.sync_range;

        if (r != 0 && (c & (nsync - 1)) == 0)
        {
            pthread_mutex_t mutex = lf_sync.mutex_[plane][r - 1];
            pthread_mutex_lock(mutex);

            while (c > lf_sync.cur_sb_col[plane][r - 1] - nsync)
            {
                pthread_cond_wait(lf_sync.cond_[plane][r - 1], mutex);
            }
            lf_mt_continue = lf_sync.cur_sb_col[plane][r - 1] != LF_SYNC_EXIT;
            pthread_mutex_unlock(mutex);
        }
        return lf_mt_continue;
    }

    private static void sync_write(AV1LfSync lf_sync, int r, int c,
                                   int sb_cols, int plane)
    {
        int nsync = lf_sync.sync_range;
        int cur;
        // Only signal when there are enough filtered SB for next row to run.
        int sig = 1;

        if (c < sb_cols - 1)
        {
            cur = c;
            if ((c % nsync) != 0) sig = 0;
        }
        else
        {
            cur = sb_cols + nsync;
        }

        if (sig != 0)
        {
            pthread_mutex_lock(lf_sync.mutex_[plane][r]);

            // After an error cur_sb_col[plane][r] holds LF_SYNC_EXIT. AOMMAX keeps it
            // from being lowered, so workers waiting in sync_read() still see it and
            // exit.
            lf_sync.cur_sb_col[plane][r] = AOMMAX(lf_sync.cur_sb_col[plane][r], cur);

            pthread_cond_broadcast(lf_sync.cond_[plane][r]);
            pthread_mutex_unlock(lf_sync.mutex_[plane][r]);
        }
    }

    // One job of row loopfiltering.
    // [port] `struct macroblockd_plane *planes` is the planes array (LFWorkerData.planes or
    // xd->plane); `AV1_DEBLOCKING_PARAMETERS *params_buf` / `TX_SIZE *tx_buf` are Ptrs.
    internal static void av1_thread_loop_filter_rows(
        YV12_BUFFER_CONFIG frame_buffer, AV1_COMMON cm,
        macroblockd_plane[] planes, MACROBLOCKD xd, int mi_row, int plane,
        int dir, int lpf_opt_level, AV1LfSync lf_sync,
        aom_internal_error_info error_info,
        Ptr<AV1_DEBLOCKING_PARAMETERS> params_buf, Ptr<TX_SIZE> tx_buf,
        int num_mis_in_lpf_unit_height_log2)
    {
        // TODO(aomedia:3276): Pass error_info to the low-level functions as required
        // in future to handle error propagation.
        int sb_cols =
            CEIL_POWER_OF_TWO(cm.mi_params.mi_cols, MAX_MIB_SIZE_LOG2);
        int r = mi_row >> num_mis_in_lpf_unit_height_log2;
        int mi_col, c;

        bool joint_filter_chroma = (lpf_opt_level == 2) && plane > AOM_PLANE_Y;
        int num_planes = joint_filter_chroma ? 2 : 1;
        Debug.Assert(!joint_filter_chroma || plane == AOM_PLANE_U);

        if (lpf_opt_level == 0)
        {
            // [subset] av1_filter_block_plane_vert / av1_filter_block_plane_horz (lpf_opt_level
            // 0): speed 11 uses LPF_PICK_FROM_Q with depth-1 inter tx search, so
            // get_lpf_opt_level() is always 2; never executed.
            throw new NotSupportedException("av1_thread_loop_filter_rows: lpf_opt_level == 0");
        }

        if (dir == 0)
        {
            for (mi_col = 0; mi_col < cm.mi_params.mi_cols; mi_col += MAX_MIB_SIZE)
            {
                c = mi_col >> MAX_MIB_SIZE_LOG2;

                av1_setup_dst_planes(planes, cm.seq_params.sb_size, frame_buffer,
                                     mi_row, mi_col, plane, plane + num_planes);
                if (plane == AOM_PLANE_Y)
                {
                    av1_filter_block_plane_vert_opt(cm, xd, planes[plane], (uint)mi_row,
                                                    (uint)mi_col, params_buf, tx_buf,
                                                    num_mis_in_lpf_unit_height_log2);
                }
                else
                {
                    av1_filter_block_plane_vert_opt_chroma(
                        cm, xd, new Ptr<macroblockd_plane>(planes, plane), (uint)mi_row,
                        (uint)mi_col, params_buf, tx_buf, plane,
                        joint_filter_chroma, num_mis_in_lpf_unit_height_log2);
                }
                if (lf_sync != null)
                {
                    sync_write(lf_sync, r, c, sb_cols, plane);
                }
            }
        }
        else if (dir == 1)
        {
            for (mi_col = 0; mi_col < cm.mi_params.mi_cols; mi_col += MAX_MIB_SIZE)
            {
                c = mi_col >> MAX_MIB_SIZE_LOG2;

                if (lf_sync != null)
                {
                    // Wait for vertical edge filtering of the top-right block to be
                    // completed. Exit in case any worker has encountered an error.
                    if (!sync_read(lf_sync, r, c, plane)) return;

                    // Wait for vertical edge filtering of the right block to be completed
                    if (!sync_read(lf_sync, r + 1, c, plane)) return;
                }

                av1_setup_dst_planes(planes, cm.seq_params.sb_size, frame_buffer,
                                     mi_row, mi_col, plane, plane + num_planes);
                if (plane == AOM_PLANE_Y)
                {
                    av1_filter_block_plane_horz_opt(cm, xd, planes[plane], (uint)mi_row,
                                                    (uint)mi_col, params_buf, tx_buf,
                                                    num_mis_in_lpf_unit_height_log2);
                }
                else
                {
                    av1_filter_block_plane_horz_opt_chroma(
                        cm, xd, new Ptr<macroblockd_plane>(planes, plane), (uint)mi_row,
                        (uint)mi_col, params_buf, tx_buf, plane,
                        joint_filter_chroma, num_mis_in_lpf_unit_height_log2);
                }
            }
        }
    }

    // [port] Not executed in the coverage runs: called by ethread.c and the LPF row worker when a
    // worker hits an error (data-dependent error path); small.
    internal static void av1_set_vert_loop_filter_done(AV1_COMMON cm, AV1LfSync lf_sync,
                                                       int num_mis_in_lpf_unit_height_log2)
    {
        int sb_rows =
            CEIL_POWER_OF_TWO(cm.mi_params.mi_rows, num_mis_in_lpf_unit_height_log2);

        // In case of loopfilter row-multithreading, the worker on an SB row waits for
        // the vertical edge filtering of the right and top-right SBs. Hence, in case
        // a thread (main/worker) encounters an error, mark every SB row in the frame
        // with LF_SYNC_EXIT. This avoids dependent workers waiting indefinitely and
        // makes them exit instead of filtering.
        for (int sb_row = 0; sb_row < sb_rows; ++sb_row)
        {
            for (int plane = 0; plane < MAX_MB_PLANE; ++plane)
            {
                pthread_mutex_lock(lf_sync.mutex_[plane][sb_row]);
                lf_sync.cur_sb_col[plane][sb_row] = LF_SYNC_EXIT;
                pthread_cond_broadcast(lf_sync.cond_[plane][sb_row]);
                pthread_mutex_unlock(lf_sync.mutex_[plane][sb_row]);
            }
        }
    }

    // [subset] sync_lf_workers, loop_filter_row_worker and loop_filter_rows_mt: frame-level
    // multi-threaded deblocking only runs when LPF is not pipelined with encoding; in the
    // subset (RT, speed 11, LPF_PICK_FROM_Q, no intra BC) pipeline_lpf_mt_with_enc is set
    // whenever more than one LPF worker exists, so av1_loop_filter_frame_mt always gets
    // num_workers == 1. Never executed.

    private static void loop_filter_rows(YV12_BUFFER_CONFIG frame, AV1_COMMON cm,
                                         MACROBLOCKD xd, int start, int stop,
                                         Ptr<int> planes_to_lf,
                                         int lpf_opt_level)
    {
        // Filter top rows of all planes first, in case the output can be partially
        // reconstructed row by row.
        int mi_row, plane, dir;

        // [port] Stack arrays of the C function; allocated once per frame.
        AV1_DEBLOCKING_PARAMETERS[] params_buf = new AV1_DEBLOCKING_PARAMETERS[MAX_MIB_SIZE];
        TX_SIZE[] tx_buf = new TX_SIZE[MAX_MIB_SIZE];
        for (mi_row = start; mi_row < stop; mi_row += MAX_MIB_SIZE)
        {
            for (plane = 0; plane < MAX_MB_PLANE; ++plane)
            {
                if (skip_loop_filter_plane(planes_to_lf, plane, lpf_opt_level))
                {
                    continue;
                }

                for (dir = 0; dir < 2; ++dir)
                {
                    av1_thread_loop_filter_rows(frame, cm, xd.plane, xd, mi_row, plane,
                                                dir, lpf_opt_level, /*lf_sync=*/null,
                                                xd.error_info, params_buf, tx_buf,
                                                MAX_MIB_SIZE_LOG2);
                }
            }
        }
    }

    internal static void av1_loop_filter_frame_mt(YV12_BUFFER_CONFIG frame, AV1_COMMON cm,
                                                  MACROBLOCKD xd, int plane_start, int plane_end,
                                                  int partial_frame, AVxWorker[] workers,
                                                  int num_workers, AV1LfSync lf_sync,
                                                  int lpf_opt_level)
    {
        int start_mi_row, end_mi_row, mi_rows_to_filter;
        int[] planes_to_lf = new int[MAX_MB_PLANE];

        if (check_planes_to_loop_filter(cm.lf, planes_to_lf, plane_start,
                                        plane_end) == 0)
            return;

        start_mi_row = 0;
        mi_rows_to_filter = cm.mi_params.mi_rows;
        if (partial_frame != 0 && cm.mi_params.mi_rows > 8)
        {
            start_mi_row = cm.mi_params.mi_rows >> 1;
            start_mi_row &= unchecked((int)0xfffffff8);
            mi_rows_to_filter = AOMMAX(cm.mi_params.mi_rows / 8, 8);
        }
        end_mi_row = start_mi_row + mi_rows_to_filter;
        av1_loop_filter_frame_init(cm, plane_start, plane_end);

        if (num_workers > 1)
        {
            // Enqueue and execute loopfiltering jobs.
            // [subset] loop_filter_rows_mt: see the note above sync_lf_workers.
            throw new NotSupportedException("av1_loop_filter_frame_mt: num_workers > 1 (LPF not pipelined with encoding)");
        }
        else
        {
            // Directly filter in the main thread.
            loop_filter_rows(frame, cm, xd, start_mi_row, end_mi_row, planes_to_lf,
                             lpf_opt_level);
        }
    }

    // [port] aom_internal_error_copy(info, src): records src's error in info and raises it
    // (PORTING 5: the longjmp becomes an AomException).
    private static void internal_error_copy(aom_internal_error_info info,
                                            aom_internal_error_info src)
    {
        Debug.Assert(info != src);
        Debug.Assert(src.setjmp == 0);
        info.error_code = src.error_code;
        info.has_detail = src.has_detail;
        info.detail = src.has_detail != 0 ? src.detail : null;
        throw new AomException(src.error_code, src.has_detail != 0 ? src.detail : null);
    }

    // [port] The setjmp() landing pad of a worker hook: the AomException raised by
    // aom_internal_error is recorded in the worker's error_info.
    // PORT-NOTE: C's aom_internal_error fills the error_info it was given before longjmp-ing;
    // here the exception carries the code and message and the hook stores them.
    private static void set_error_from_exception(aom_internal_error_info error_info,
                                                 AomException e)
    {
        error_info.setjmp = 0;
        error_info.error_code = e.ErrorCode;
        error_info.has_detail = 1;
        error_info.detail = e.Message;
    }

    // Initializes cdef_sync parameters.
    private static void reset_cdef_job_info(AV1CdefSync cdef_sync)
    {
        cdef_sync.end_of_frame = 0;
        cdef_sync.fbr = 0;
        cdef_sync.fbc = 0;
        cdef_sync.cdef_mt_exit = false;
    }

    private static void launch_cdef_workers(AVxWorker[] workers,
                                            int num_workers)
    {
        AVxWorkerInterface winterface = aom_get_worker_interface();
        for (int i = num_workers - 1; i >= 0; i--)
        {
            AVxWorker worker = workers[i];
            worker.had_error = 0;
            if (i == 0)
                winterface.execute(worker);
            else
                winterface.launch(worker);
        }
    }

    private static void sync_cdef_workers(AVxWorker[] workers,
                                          AV1_COMMON cm, int num_workers)
    {
        AVxWorkerInterface winterface = aom_get_worker_interface();
        int had_error = workers[0].had_error;
        aom_internal_error_info error_info = null;

        // Read the error_info of main thread.
        if (had_error != 0)
        {
            AVxWorker worker = workers[0];
            error_info = ((AV1CdefWorkerData)worker.data2).error_info;
        }

        // Wait till all rows are finished.
        for (int i = num_workers - 1; i > 0; --i)
        {
            AVxWorker worker = workers[i];
            if (winterface.sync(worker) == 0)
            {
                had_error = 1;
                error_info = ((AV1CdefWorkerData)worker.data2).error_info;
            }
        }
        if (had_error != 0) internal_error_copy(cm.error, error_info);
    }

    // Updates the row index of the next job to be processed.
    // Also updates end_of_frame flag when the processing of all rows is complete.
    private static void update_cdef_row_next_job_info(AV1CdefSync cdef_sync,
                                                      int nvfb)
    {
        cdef_sync.fbr++;
        if (cdef_sync.fbr == nvfb)
        {
            cdef_sync.end_of_frame = 1;
        }
    }

    // Checks if a job is available. If job is available,
    // populates next job information and returns 1, else returns 0.
    private static int get_cdef_row_next_job(AV1CdefSync cdef_sync,
                                             ref int cur_fbr, int nvfb)
    {
        pthread_mutex_lock(cdef_sync.mutex_);
        int do_next_row = 0;
        // Populates information needed for current job and update the row
        // index of the next row to be processed.
        if (!cdef_sync.cdef_mt_exit && cdef_sync.end_of_frame == 0)
        {
            do_next_row = 1;
            cur_fbr = cdef_sync.fbr;
            update_cdef_row_next_job_info(cdef_sync, nvfb);
        }
        pthread_mutex_unlock(cdef_sync.mutex_);
        return do_next_row;
    }

    // [port] Only reached from the error path of cdef_sb_row_worker_hook; small.
    private static void set_cdef_init_fb_row_done(AV1CdefSync cdef_sync, int nvfb)
    {
        for (int fbr = 0; fbr < nvfb; fbr++) cdef_row_mt_sync_write(cdef_sync, fbr);
    }

    // Hook function for each thread in CDEF multi-threading.
    private static int cdef_sb_row_worker_hook(object arg1, object arg2)
    {
        AV1CdefSync cdef_sync = (AV1CdefSync)arg1;
        AV1CdefWorkerData cdef_worker = (AV1CdefWorkerData)arg2;
        AV1_COMMON cm = cdef_worker.cm;
        int nvfb = (cm.mi_params.mi_rows + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;

        pthread_mutex_t job_mutex_ = cdef_sync.mutex_;
        aom_internal_error_info error_info = cdef_worker.error_info;

        // The jmp_buf is valid only for the duration of the function that calls
        // setjmp(). Therefore, this function must reset the 'setjmp' field to 0
        // before it returns.
        error_info.setjmp = 1;
        try
        {
            int cur_fbr = 0;
            int num_planes = av1_num_planes(cm);
            while (get_cdef_row_next_job(cdef_sync, ref cur_fbr, nvfb) != 0)
            {
                MACROBLOCKD xd = cdef_worker.xd;
                av1_cdef_fb_row(cm, xd, cdef_worker.linebuf, cdef_worker.colbuf,
                                cdef_worker.srcbuf, cur_fbr,
                                cdef_worker.cdef_init_fb_row_fn, cdef_sync, error_info);
                if (cdef_worker.do_extend_border != 0)
                {
                    for (int plane = 0; plane < num_planes; ++plane)
                    {
                        YV12_BUFFER_CONFIG ybf = cm.cur_frame.buf;
                        int is_uv = B(plane > 0);
                        int mi_high = MI_SIZE_LOG2 - xd.plane[plane].subsampling_y;
                        int unit_height = MI_SIZE_64X64 << mi_high;
                        int v_start = cur_fbr * unit_height;
                        int v_end =
                            AOMMIN(v_start + unit_height, ybf.crop_heights[is_uv]);
                        aom_extend_frame_borders_plane_row(ybf, plane, v_start, v_end);
                    }
                }
            }
        }
        catch (AomException e)
        {
            set_error_from_exception(error_info, e);
            pthread_mutex_lock(job_mutex_);
            cdef_sync.cdef_mt_exit = true;
            pthread_mutex_unlock(job_mutex_);
            // In case of cdef row-multithreading, the worker on a filter block row
            // (fbr) waits for the line buffers (top and bottom) copy of the above row.
            // Hence, in case a thread (main/worker) encounters an error before copying
            // of the line buffers, update that line buffer copy is complete in order to
            // avoid dependent workers waiting indefinitely.
            set_cdef_init_fb_row_done(cdef_sync, nvfb);
            return 0;
        }
        error_info.setjmp = 0;
        return 1;
    }

    // Assigns CDEF hook function and thread data to each worker.
    private static void prepare_cdef_frame_workers(
        AV1_COMMON cm, MACROBLOCKD xd, AV1CdefWorkerData[] cdef_worker,
        AVxWorkerHook hook, AVxWorker[] workers, AV1CdefSync cdef_sync,
        int num_workers, cdef_init_fb_row_t cdef_init_fb_row_fn,
        int do_extend_border)
    {
        prepare_cdef_worker_data(cm, xd, cdef_worker, num_workers, cdef_init_fb_row_fn,
                                 do_extend_border);
        for (int i = num_workers - 1; i >= 0; i--)
        {
            AVxWorker worker = workers[i];
            worker.hook = hook;
            worker.data1 = cdef_sync;
            worker.data2 = cdef_worker[i];
        }
    }

    // [av1rt] The thread data half of prepare_cdef_frame_workers.
    private static void prepare_cdef_worker_data(
        AV1_COMMON cm, MACROBLOCKD xd, AV1CdefWorkerData[] cdef_worker, int num_workers,
        cdef_init_fb_row_t cdef_init_fb_row_fn, int do_extend_border)
    {
        int num_planes = av1_num_planes(cm);

        cdef_worker[0].srcbuf = cm.cdef_info.srcbuf;
        for (int plane = 0; plane < num_planes; plane++)
            cdef_worker[0].colbuf[plane] = cm.cdef_info.colbuf[plane];
        for (int i = num_workers - 1; i >= 0; i--)
        {
            cdef_worker[i].cm = cm;
            cdef_worker[i].xd = xd;
            cdef_worker[i].cdef_init_fb_row_fn = cdef_init_fb_row_fn;
            cdef_worker[i].do_extend_border = do_extend_border;
            for (int plane = 0; plane < num_planes; plane++)
                cdef_worker[i].linebuf[plane] = cm.cdef_info.linebuf[plane];
        }
    }

    // [av1rt] av1_cdef_frame_mt without the worker launch, for running the CDEF rows inside
    // another launch: av1rt_cdef_rows_worker on each of the num_workers threads (row jobs are
    // handed out in order and a row only waits for the line buffers of the row above, which the
    // thread that took it fills first). The caller checks the returned error.
    internal static void av1rt_cdef_frame_mt_setup(AV1_COMMON cm, MACROBLOCKD xd,
                                                   AV1CdefWorkerData[] cdef_worker,
                                                   AV1CdefSync cdef_sync, int num_workers,
                                                   cdef_init_fb_row_t cdef_init_fb_row_fn,
                                                   int do_extend_border)
    {
        YV12_BUFFER_CONFIG frame = cm.cur_frame.buf;
        int num_planes = av1_num_planes(cm);

        av1_setup_dst_planes(xd.plane, cm.seq_params.sb_size, frame, 0, 0, 0,
                             num_planes);

        reset_cdef_job_info(cdef_sync);
        prepare_cdef_worker_data(cm, xd, cdef_worker, num_workers, cdef_init_fb_row_fn,
                                 do_extend_border);
    }

    internal static int av1rt_cdef_rows_worker(AV1CdefSync cdef_sync,
                                               AV1CdefWorkerData cdef_worker) =>
        cdef_sb_row_worker_hook(cdef_sync, cdef_worker);

    // Initializes row-level parameters for CDEF frame.
    internal static void av1_cdef_init_fb_row_mt(AV1_COMMON cm,
                                                 MACROBLOCKD xd,
                                                 CdefBlockInfo fb_info,
                                                 Ptr<ushort>[] linebuf, Ptr<ushort> src,
                                                 AV1CdefSyncData cdef_sync, int fbr)
    {
        int num_planes = av1_num_planes(cm);
        int nvfb = (cm.mi_params.mi_rows + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;
        int luma_stride =
            ALIGN_POWER_OF_TWO(cm.mi_params.mi_cols << MI_SIZE_LOG2, 4);

        // for the current filter block, it's top left corner mi structure (mi_tl)
        // is first accessed to check whether the top and left boundaries are
        // frame boundaries. Then bottom-left and top-right mi structures are
        // accessed to check whether the bottom and right boundaries
        // (respectively) are frame boundaries.
        //
        // Note that we can't just check the bottom-right mi structure - eg. if
        // we're at the right-hand edge of the frame but not the bottom, then
        // the bottom-right mi is NULL but the bottom-left is not.
        fb_info.frame_boundary[TOP] = (MI_SIZE_64X64 * fbr == 0) ? 1 : 0;
        if (fbr != nvfb - 1)
            fb_info.frame_boundary[BOTTOM] =
                (MI_SIZE_64X64 * (fbr + 1) == cm.mi_params.mi_rows) ? 1 : 0;
        else
            fb_info.frame_boundary[BOTTOM] = 1;

        fb_info.src = src;
        fb_info.damping = cm.cdef_info.cdef_damping;
        fb_info.coeff_shift = AOMMAX(cm.seq_params.bit_depth - 8, 0);
        av1_zero(fb_info.dir);
        av1_zero(fb_info.var);

        for (int plane = 0; plane < num_planes; plane++)
        {
            int stride = luma_stride >> xd.plane[plane].subsampling_x;
            Ptr<ushort> top_linebuf = linebuf[plane];
            Ptr<ushort> bot_linebuf = linebuf[plane] + nvfb * CDEF_VBORDER * stride;
            {
                int mi_high_l2 = MI_SIZE_LOG2 - xd.plane[plane].subsampling_y;
                int top_offset = MI_SIZE_64X64 * (fbr + 1) << mi_high_l2;
                int bot_offset = MI_SIZE_64X64 * (fbr + 1) << mi_high_l2;

                if (fbr != nvfb - 1)  // if (fbr != 0)  // top line buffer copy
                    av1_cdef_copy_sb8_16(
                        cm, top_linebuf + (fbr + 1) * CDEF_VBORDER * stride, stride,
                        xd.plane[plane].dst.buf, top_offset - CDEF_VBORDER, 0,
                        xd.plane[plane].dst.stride, CDEF_VBORDER, stride);
                if (fbr != nvfb - 1)  // bottom line buffer copy
                    av1_cdef_copy_sb8_16(cm, bot_linebuf + fbr * CDEF_VBORDER * stride,
                                         stride, xd.plane[plane].dst.buf, bot_offset, 0,
                                         xd.plane[plane].dst.stride, CDEF_VBORDER, stride);
            }

            fb_info.top_linebuf[plane] = linebuf[plane] + fbr * CDEF_VBORDER * stride;
            fb_info.bot_linebuf[plane] =
                linebuf[plane] + (nvfb * CDEF_VBORDER * stride + (fbr * CDEF_VBORDER * stride));
        }

        cdef_row_mt_sync_write(cdef_sync, fbr);
        cdef_row_mt_sync_read(cdef_sync, fbr);
    }

    // Implements multi-threading for CDEF.
    // Perform CDEF on input frame.
    // Inputs:
    //   frame: Pointer to input frame buffer.
    //   cm: Pointer to common structure.
    //   xd: Pointer to common current coding block structure.
    // Returns:
    //   Nothing will be returned.
    internal static void av1_cdef_frame_mt(AV1_COMMON cm, MACROBLOCKD xd,
                                           AV1CdefWorkerData[] cdef_worker,
                                           AVxWorker[] workers, AV1CdefSync cdef_sync,
                                           int num_workers, cdef_init_fb_row_t cdef_init_fb_row_fn,
                                           int do_extend_border)
    {
        YV12_BUFFER_CONFIG frame = cm.cur_frame.buf;
        int num_planes = av1_num_planes(cm);

        av1_setup_dst_planes(xd.plane, cm.seq_params.sb_size, frame, 0, 0, 0,
                             num_planes);

        reset_cdef_job_info(cdef_sync);
        prepare_cdef_frame_workers(cm, xd, cdef_worker, cdef_sb_row_worker_hook,
                                   workers, cdef_sync, num_workers,
                                   cdef_init_fb_row_fn, do_extend_border);
        launch_cdef_workers(workers, num_workers);
        sync_cdef_workers(workers, cm, num_workers);
    }

    internal static int av1_get_intrabc_extra_top_right_sb_delay(AV1_COMMON cm)
    {
        // No additional top-right delay when intraBC tool is not enabled.
        if (av1_allow_intrabc(cm) == 0) return 0;
        // Due to the hardware constraints on processing the intraBC tool with row
        // multithreading, a top-right delay of 3 superblocks of size 128x128 or 5
        // superblocks of size 64x64 is mandated. However, a minimum top-right delay
        // of 1 superblock is assured with 'sync_range'. Hence return only the
        // additional superblock delay when the intraBC tool is enabled.
        return cm.seq_params.sb_size == BLOCK_128X128 ? 2 : 4;
    }
}
