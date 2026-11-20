// Port of libaom av1/encoder/ethread.c
using System;
using System.Diagnostics;

namespace Av1rt.Aom;

// PORT-NOTE: error handling in the worker hooks. C arms `setjmp(error_info->jmp)` and a failing
// callee longjmps back after aom_internal_error() stored the code and message in the
// aom_internal_error_info. In the port callees throw AomException (PORTING.md §5), so each hook
// body runs in try/catch(AomException): the catch stores what aom_internal_error() would have
// stored (record_error_info) and then runs the C recovery code of the setjmp branch. Other
// exception types are port bugs and are not caught. aom_internal_error_copy(dst, src) (which
// longjmps through dst) becomes `throw new AomException(src.error_code, src.detail)`.
internal static partial class ethread
{
    // What aom_internal_error() stores in `info` before it longjmps.
    private static void record_error_info(aom_internal_error_info info, AomException e)
    {
        info.error_code = e.ErrorCode;
        info.has_detail = 1;
        info.detail = e.Message;
    }

    private static void accumulate_rd_opt(ThreadData td, ThreadData td_t)
    {
        td.rd_counts.compound_ref_used_flag |=
            td_t.rd_counts.compound_ref_used_flag;
        td.rd_counts.skip_mode_used_flag |= td_t.rd_counts.skip_mode_used_flag;

        for (int i = 0; i < TX_SIZES_ALL; i++)
        {
            for (int j = 0; j < TX_TYPES; j++)
                td.rd_counts.tx_type_used[i][j] += td_t.rd_counts.tx_type_used[i][j];
        }

        for (int i = 0; i < BLOCK_SIZES_ALL; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                td.rd_counts.obmc_used[i][j] += td_t.rd_counts.obmc_used[i][j];
            }
        }

        for (int i = 0; i < 2; i++)
        {
            td.rd_counts.warped_used[i] += td_t.rd_counts.warped_used[i];
        }

        td.rd_counts.seg_tmp_pred_cost[0] += td_t.rd_counts.seg_tmp_pred_cost[0];
        td.rd_counts.seg_tmp_pred_cost[1] += td_t.rd_counts.seg_tmp_pred_cost[1];

        td.rd_counts.newmv_or_intra_blocks += td_t.rd_counts.newmv_or_intra_blocks;
    }

    // [subset] update_delta_lf_for_row_mt: only called when
    // cm->delta_q_info.delta_lf_present_flag is set (deltaq_mode 0 in the subset).

    internal static void av1_row_mt_sync_read_dummy(AV1EncRowMultiThreadSync row_mt_sync, int r,
                                                    int c)
    {
    }

    internal static void av1_row_mt_sync_write_dummy(AV1EncRowMultiThreadSync row_mt_sync, int r,
                                                     int c, int cols)
    {
    }

    internal static void av1_row_mt_sync_read(AV1EncRowMultiThreadSync row_mt_sync, int r, int c)
    {
        int nsync = row_mt_sync.sync_range;

        if (r != 0)
        {
            pthread_mutex_t mutex = row_mt_sync.mutex_[r - 1];
            pthread_mutex_lock(mutex);

            while (c > row_mt_sync.num_finished_cols[r - 1] - nsync -
                           row_mt_sync.intrabc_extra_top_right_sb_delay)
            {
                pthread_cond_wait(row_mt_sync.cond_[r - 1], mutex);
            }
            pthread_mutex_unlock(mutex);
        }
    }

    internal static void av1_row_mt_sync_write(AV1EncRowMultiThreadSync row_mt_sync, int r, int c,
                                               int cols)
    {
        int nsync = row_mt_sync.sync_range;
        int cur;
        // Only signal when there are enough encoded blocks for next row to run.
        int sig = 1;

        if (c < cols - 1)
        {
            cur = c;
            if (c % nsync != 0) sig = 0;
        }
        else
        {
            cur = cols + nsync + row_mt_sync.intrabc_extra_top_right_sb_delay;
        }

        if (sig != 0)
        {
            pthread_mutex_lock(row_mt_sync.mutex_[r]);

            // When a thread encounters an error, num_finished_cols[r] is set to maximum
            // column number. In this case, the AOMMAX operation here ensures that
            // num_finished_cols[r] is not overwritten with a smaller value thus
            // preventing the infinite waiting of threads in the relevant sync_read()
            // function.
            row_mt_sync.num_finished_cols[r] =
                AOMMAX(row_mt_sync.num_finished_cols[r], cur);

            pthread_cond_signal(row_mt_sync.cond_[r]);
            pthread_mutex_unlock(row_mt_sync.mutex_[r]);
        }
    }

    // Allocate memory for row synchronization
    private static void row_mt_sync_mem_alloc(AV1EncRowMultiThreadSync row_mt_sync,
                                              AV1_COMMON cm, int rows)
    {
        int i;

        row_mt_sync.mutex_ = new pthread_mutex_t[rows];
        if (row_mt_sync.mutex_ != null)
        {
            for (i = 0; i < rows; ++i)
            {
                pthread_mutex_init(ref row_mt_sync.mutex_[i], null);
            }
        }

        row_mt_sync.cond_ = new pthread_cond_t[rows];
        if (row_mt_sync.cond_ != null)
        {
            for (i = 0; i < rows; ++i)
            {
                pthread_cond_init(ref row_mt_sync.cond_[i], null);
            }
        }

        row_mt_sync.num_finished_cols = new int[rows];

        row_mt_sync.rows = rows;
        // Set up nsync.
        row_mt_sync.sync_range = 1;
    }

    // Deallocate row based multi-threading synchronization related mutex and data
    internal static void av1_row_mt_sync_mem_dealloc(AV1EncRowMultiThreadSync row_mt_sync)
    {
        if (row_mt_sync != null)
        {
            int i;

            if (row_mt_sync.mutex_ != null)
            {
                for (i = 0; i < row_mt_sync.rows; ++i)
                {
                    pthread_mutex_destroy(row_mt_sync.mutex_[i]);
                }
                row_mt_sync.mutex_ = null;
            }
            if (row_mt_sync.cond_ != null)
            {
                for (i = 0; i < row_mt_sync.rows; ++i)
                {
                    pthread_cond_destroy(row_mt_sync.cond_[i]);
                }
                row_mt_sync.cond_ = null;
            }
            row_mt_sync.num_finished_cols = Ptr<int>.Null;

            // clear the structure as the source of this call may be dynamic change
            // in tiles in which case this call will be followed by an _alloc()
            // which may fail.
            row_mt_sync.Clear();
        }
    }

    private static int get_sb_rows_in_frame(AV1_COMMON cm)
    {
        return CEIL_POWER_OF_TWO(cm.mi_params.mi_rows,
                                 cm.seq_params.mib_size_log2);
    }

    private static void row_mt_mem_alloc(AV1_COMP cpi, int max_rows, int max_cols,
                                         int alloc_row_ctx)
    {
        AV1_COMMON cm = cpi.common;
        AV1EncRowMultiThreadInfo enc_row_mt = cpi.mt_info.enc_row_mt;
        int tile_cols = cm.tiles.cols;
        int tile_rows = cm.tiles.rows;
        int tile_col, tile_row;

        av1_row_mt_mem_dealloc(cpi);

        // Allocate memory for row based multi-threading
        Debug.Assert(cpi.allocated_tiles == tile_cols * tile_rows);
        for (tile_row = 0; tile_row < tile_rows; tile_row++)
        {
            for (tile_col = 0; tile_col < tile_cols; tile_col++)
            {
                int tile_index = tile_row * tile_cols + tile_col;
                TileDataEnc this_tile = cpi.tile_data[tile_index];

                row_mt_sync_mem_alloc(this_tile.row_mt_sync, cm, max_rows);

                if (alloc_row_ctx != 0)
                {
                    Debug.Assert(max_cols > 0);
                    int num_row_ctx = AOMMAX(1, (max_cols - 1));
                    this_tile.row_ctx = new_array<FRAME_CONTEXT>(num_row_ctx);
                }
            }
        }
        enc_row_mt.allocated_tile_cols = tile_cols;
        enc_row_mt.allocated_tile_rows = tile_rows;
        int sb_rows = get_sb_rows_in_frame(cm);
        enc_row_mt.num_tile_cols_done = new int[sb_rows];

        enc_row_mt.allocated_rows = max_rows;
        enc_row_mt.allocated_cols = max_cols - 1;
        enc_row_mt.allocated_sb_rows = sb_rows;
    }

    internal static void av1_row_mt_mem_dealloc(AV1_COMP cpi)
    {
        AV1EncRowMultiThreadInfo enc_row_mt = cpi.mt_info.enc_row_mt;
        int tile_cols = enc_row_mt.allocated_tile_cols;
        int tile_rows = enc_row_mt.allocated_tile_rows;
        int tile_col, tile_row;

        // Free row based multi-threading sync memory
        for (tile_row = 0; tile_row < tile_rows; tile_row++)
        {
            for (tile_col = 0; tile_col < tile_cols; tile_col++)
            {
                int tile_index = tile_row * tile_cols + tile_col;
                TileDataEnc this_tile = cpi.tile_data[tile_index];

                av1_row_mt_sync_mem_dealloc(this_tile.row_mt_sync);

                if (cpi.oxcf.algo_cfg.cdf_update_mode != 0)
                {
                    this_tile.row_ctx = null;
                }
            }
        }
        enc_row_mt.allocated_tile_cols = 0;
        enc_row_mt.allocated_tile_rows = 0;
        enc_row_mt.num_tile_cols_done = Ptr<int>.Null;
        enc_row_mt.allocated_rows = 0;
        enc_row_mt.allocated_cols = 0;
        enc_row_mt.allocated_sb_rows = 0;
    }

    private static void assign_tile_to_thread(Ptr<int> thread_id_to_tile_id,
                                              int num_tiles, int num_workers)
    {
        int tile_id = 0;
        int i;

        for (i = 0; i < num_workers; i++)
        {
            thread_id_to_tile_id[i] = tile_id++;
            if (tile_id == num_tiles) tile_id = 0;
        }
    }

    private static int get_next_job(TileDataEnc tile_data,
                                    ref int current_mi_row, int mib_size)
    {
        AV1EncRowMultiThreadSync row_mt_sync = tile_data.row_mt_sync;
        int mi_row_end = tile_data.tile_info.mi_row_end;

        if (row_mt_sync.next_mi_row < mi_row_end)
        {
            current_mi_row = row_mt_sync.next_mi_row;
            row_mt_sync.num_threads_working++;
            row_mt_sync.next_mi_row += mib_size;
            return 1;
        }
        return 0;
    }

    private static void switch_tile_and_get_next_job(
        AV1_COMMON cm, TileDataEnc[] tile_data, ref int cur_tile_id,
        ref int current_mi_row, ref int end_of_frame, int is_firstpass,
        BLOCK_SIZE fp_block_size)
    {
        int tile_cols = cm.tiles.cols;
        int tile_rows = cm.tiles.rows;

        int tile_id = -1;  // Stores the tile ID with minimum proc done
        int max_mis_to_encode = 0;
        int min_num_threads_working = int.MaxValue;

        for (int tile_row = 0; tile_row < tile_rows; tile_row++)
        {
            for (int tile_col = 0; tile_col < tile_cols; tile_col++)
            {
                int tile_index = tile_row * tile_cols + tile_col;
                TileDataEnc this_tile = tile_data[tile_index];
                AV1EncRowMultiThreadSync row_mt_sync = this_tile.row_mt_sync;

                // CONFIG_REALTIME_ONLY
                int num_b_rows_in_tile =
                    av1_get_sb_rows_in_tile(cm, in this_tile.tile_info);
                int num_b_cols_in_tile =
                    av1_get_sb_cols_in_tile(cm, in this_tile.tile_info);
                int theoretical_limit_on_threads =
                    AOMMIN((num_b_cols_in_tile + 1) >> 1, num_b_rows_in_tile);
                int num_threads_working = row_mt_sync.num_threads_working;

                if (num_threads_working < theoretical_limit_on_threads)
                {
                    int num_mis_to_encode =
                        this_tile.tile_info.mi_row_end - row_mt_sync.next_mi_row;

                    // Tile to be processed by this thread is selected on the basis of
                    // availability of jobs:
                    // 1) If jobs are available, tile to be processed is chosen on the
                    // basis of minimum number of threads working for that tile. If two or
                    // more tiles have same number of threads working for them, then the
                    // tile with maximum number of jobs available will be chosen.
                    // 2) If no jobs are available, then end_of_frame is reached.
                    if (num_mis_to_encode > 0)
                    {
                        if (num_threads_working < min_num_threads_working)
                        {
                            min_num_threads_working = num_threads_working;
                            max_mis_to_encode = 0;
                        }
                        if (num_threads_working == min_num_threads_working &&
                            num_mis_to_encode > max_mis_to_encode)
                        {
                            tile_id = tile_index;
                            max_mis_to_encode = num_mis_to_encode;
                        }
                    }
                }
            }
        }
        if (tile_id == -1)
        {
            end_of_frame = 1;
        }
        else
        {
            // Update the current tile id to the tile id that will be processed next,
            // which will be the least processed tile.
            cur_tile_id = tile_id;
            int unit_height = mi_size_high[fp_block_size];
            get_next_job(tile_data[tile_id], ref current_mi_row,
                         is_firstpass != 0 ? unit_height : cm.seq_params.mib_size);
        }
    }

    // [subset] set_firstpass_encode_done / fp_enc_row_mt_worker_hook: !CONFIG_REALTIME_ONLY.

    private static void launch_loop_filter_rows(AV1_COMMON cm, EncWorkerData thread_data,
                                                AV1EncRowMultiThreadInfo enc_row_mt,
                                                int mib_size_log2)
    {
        AV1LfSync lf_sync = thread_data.lf_sync;
        int sb_rows = get_sb_rows_in_frame(cm);
        Ptr<AV1LfMTInfo> cur_job_info;
        bool row_mt_exit = false;
        pthread_mutex_t enc_row_mt_mutex_ = enc_row_mt.mutex_;

        while (!(cur_job_info = get_lf_job_info(lf_sync)).IsNull)
        {
            LFWorkerData lf_data = thread_data.lf_data;
            int lpf_opt_level = cur_job_info.Value.lpf_opt_level;
            int cur_sb_row = cur_job_info.Value.mi_row >> mib_size_log2;
            int next_sb_row = AOMMIN(sb_rows - 1, cur_sb_row + 1);
            // Wait for current and next superblock row to finish encoding.
            pthread_mutex_lock(enc_row_mt_mutex_);
            while (!enc_row_mt.row_mt_exit &&
                   (enc_row_mt.num_tile_cols_done[cur_sb_row] < cm.tiles.cols ||
                    enc_row_mt.num_tile_cols_done[next_sb_row] < cm.tiles.cols))
            {
                pthread_cond_wait(enc_row_mt.cond_, enc_row_mt_mutex_);
            }
            row_mt_exit = enc_row_mt.row_mt_exit;
            pthread_mutex_unlock(enc_row_mt_mutex_);
            if (row_mt_exit) return;

            av1_thread_loop_filter_rows(
                lf_data.frame_buffer, lf_data.cm, lf_data.planes, lf_data.xd,
                cur_job_info.Value.mi_row, cur_job_info.Value.plane, cur_job_info.Value.dir,
                lpf_opt_level, lf_sync, thread_data.error_info, lf_data.params_buf,
                lf_data.tx_buf, mib_size_log2);
        }
    }

    // Not executed in the subset (error path of enc_row_mt_worker_hook), ported with it.
    private static void set_encoding_done(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        int tile_cols = cm.tiles.cols;
        int tile_rows = cm.tiles.rows;
        AV1EncRowMultiThreadInfo enc_row_mt = cpi.mt_info.enc_row_mt;
        int mib_size = cm.seq_params.mib_size;

        // In case of row-multithreading, due to top-right dependency, the worker on
        // an SB row waits for the completion of the encode of the top and top-right
        // SBs. Hence, in case a thread (main/worker) encounters an error, update that
        // encoding of every SB row in the frame is complete in order to avoid the
        // dependent workers of every tile from waiting indefinitely.
        for (int tile_row = 0; tile_row < tile_rows; tile_row++)
        {
            for (int tile_col = 0; tile_col < tile_cols; tile_col++)
            {
                TileDataEnc this_tile =
                    cpi.tile_data[tile_row * tile_cols + tile_col];
                ref TileInfo tile_info = ref this_tile.tile_info;
                AV1EncRowMultiThreadSync row_mt_sync = this_tile.row_mt_sync;
                int sb_cols_in_tile = av1_get_sb_cols_in_tile(cm, in tile_info);
                for (int mi_row = tile_info.mi_row_start, sb_row_in_tile = 0;
                     mi_row < tile_info.mi_row_end;
                     mi_row += mib_size, sb_row_in_tile++)
                {
                    enc_row_mt.sync_write_ptr(row_mt_sync, sb_row_in_tile,
                                              sb_cols_in_tile - 1, sb_cols_in_tile);
                }
            }
        }
    }

    private static bool lpf_mt_with_enc_enabled(int pipeline_lpf_mt_with_enc,
                                                int[] filter_level)
    {
        return pipeline_lpf_mt_with_enc != 0 && (filter_level[0] != 0 || filter_level[1] != 0);
    }

    private static readonly AVxWorkerHook enc_row_mt_worker_hook_fn = enc_row_mt_worker_hook;

    private static int enc_row_mt_worker_hook(object arg1, object unused)
    {
        EncWorkerData thread_data = (EncWorkerData)arg1;
        AV1_COMP cpi = thread_data.cpi;
        int thread_id = thread_data.thread_id;
        AV1EncRowMultiThreadInfo enc_row_mt = cpi.mt_info.enc_row_mt;
        pthread_mutex_t enc_row_mt_mutex_ = enc_row_mt.mutex_;

        aom_internal_error_info error_info = thread_data.error_info;
        AV1LfSync lf_sync = thread_data.lf_sync;
        MACROBLOCKD xd = thread_data.td.mb.e_mbd;
        xd.error_info = error_info;
        AV1_COMMON cm = cpi.common;
        bool do_pipelined_lpf_mt_with_enc = lpf_mt_with_enc_enabled(
            cpi.mt_info.pipeline_lpf_mt_with_enc, cm.lf.filter_level);

        // The jmp_buf is valid only for the duration of the function that calls
        // setjmp(). Therefore, this function must reset the 'setjmp' field to 0
        // before it returns.
        error_info.setjmp = 1;
        try
        {
            int mib_size_log2 = cm.seq_params.mib_size_log2;
            int cur_tile_id = enc_row_mt.thread_id_to_tile_id[thread_id];

            // Preallocate the pc_tree for realtime coding to reduce the cost of memory
            // allocation.
            if (cpi.sf.rt_sf.use_nonrd_pick_mode != 0)
            {
                thread_data.td.pc_root = av1_alloc_pc_tree_node(cm.seq_params.sb_size);
                if (thread_data.td.pc_root == null)
                    throw new AomException(AOM_CODEC_MEM_ERROR, "Failed to allocate PC_TREE");
            }
            else
            {
                thread_data.td.pc_root = null;
            }

            Debug.Assert(cur_tile_id != -1);

            BLOCK_SIZE fp_block_size = cpi.fp_block_size;
            int end_of_frame = 0;
            bool row_mt_exit = false;

            // When master thread does not have a valid job to process, xd->tile_ctx
            // is not set and it contains NULL pointer. This can result in NULL pointer
            // access violation if accessed beyond the encode stage. Hence, updating
            // thread_data->td->mb.e_mbd.tile_ctx is initialized with common frame
            // context to avoid NULL pointer access in subsequent stages.
            thread_data.td.mb.e_mbd.tile_ctx = cm.fc;
            while (true)
            {
                int current_mi_row = -1;
                pthread_mutex_lock(enc_row_mt_mutex_);
                row_mt_exit = enc_row_mt.row_mt_exit;
                // row_mt_exit check here can be avoided as it is checked after
                // sync_read_ptr() in encode_sb_row(). However, checking row_mt_exit here,
                // tries to return before calling the function get_next_job().
                if (!row_mt_exit &&
                    get_next_job(cpi.tile_data[cur_tile_id], ref current_mi_row,
                                 cm.seq_params.mib_size) == 0)
                {
                    // No jobs are available for the current tile. Query for the status of
                    // other tiles and get the next job if available
                    switch_tile_and_get_next_job(cm, cpi.tile_data, ref cur_tile_id,
                                                 ref current_mi_row, ref end_of_frame, 0,
                                                 fp_block_size);
                }
                pthread_mutex_unlock(enc_row_mt_mutex_);
                // When row_mt_exit is set to true, other workers need not pursue any
                // further jobs.
                if (row_mt_exit)
                {
                    error_info.setjmp = 0;
                    return 1;
                }

                if (end_of_frame != 0) break;

                TileDataEnc this_tile = cpi.tile_data[cur_tile_id];
                AV1EncRowMultiThreadSync row_mt_sync = this_tile.row_mt_sync;
                ref TileInfo tile_info = ref this_tile.tile_info;
                int tile_row = tile_info.tile_row;
                int tile_col = tile_info.tile_col;
                ThreadData td = thread_data.td;
                int sb_row = current_mi_row >> mib_size_log2;

                Debug.Assert(current_mi_row != -1 && current_mi_row <= tile_info.mi_row_end);

                td.mb.e_mbd.tile_ctx = td.tctx;
                td.mb.tile_pb_ctx = this_tile.tctx;
                td.abs_sum_level = 0;

                if (this_tile.allow_update_cdf != 0)
                {
                    // [subset] td->mb.row_ctx = this_tile->row_ctx and the row-start context
                    // restore: allow_update_cdf is 0 in the subset (av1_init_tile_data clears
                    // it because delay_wait_for_top_right_sb() is 1 for the rt cost update
                    // levels).
                    throw new NotSupportedException("allow_update_cdf with row mt is not in the subset");
                }
                else
                {
                    td.mb.e_mbd.tile_ctx.CopyFrom(this_tile.tctx);
                }

                av1_init_above_context(cm.above_contexts, av1_num_planes(cm), tile_row,
                                       td.mb.e_mbd);
                // [subset] av1_crc32c_calculator_init(&mb_rd_record->crc_calculator): the
                // mb_rd_record (sf.rd_sf.use_mb_rd_hash) is dropped from MACROBLOCK.

                av1_encode_sb_row(cpi, td, tile_row, tile_col, current_mi_row);
                pthread_mutex_lock(enc_row_mt_mutex_);
                this_tile.abs_sum_level += td.abs_sum_level;
                row_mt_sync.num_threads_working--;
                enc_row_mt.num_tile_cols_done[sb_row]++;
                pthread_cond_broadcast(enc_row_mt.cond_);
                pthread_mutex_unlock(enc_row_mt_mutex_);
            }
            if (do_pipelined_lpf_mt_with_enc)
            {
                // Loop-filter a superblock row if encoding of the current and next
                // superblock row is complete.
                // TODO(deepa.kg @ittiam.com) Evaluate encoder speed by interleaving
                // encoding and loop filter stage.
                launch_loop_filter_rows(cm, thread_data, enc_row_mt, mib_size_log2);
            }
            av1_free_pc_tree_recursive(thread_data.td.pc_root, av1_num_planes(cm), 0, 0,
                                       cpi.sf.part_sf.partition_search_type);
            thread_data.td.pc_root = null;
            error_info.setjmp = 0;
            return 1;
        }
        catch (AomException e)
        {
            record_error_info(error_info, e);
            error_info.setjmp = 0;
            pthread_mutex_lock(enc_row_mt_mutex_);
            enc_row_mt.row_mt_exit = true;
            // Wake up all the workers waiting in launch_loop_filter_rows() to exit in
            // case of an error.
            pthread_cond_broadcast(enc_row_mt.cond_);
            pthread_mutex_unlock(enc_row_mt_mutex_);
            set_encoding_done(cpi);

            if (do_pipelined_lpf_mt_with_enc)
            {
                pthread_mutex_lock(lf_sync.job_mutex);
                lf_sync.lf_mt_exit = true;
                pthread_mutex_unlock(lf_sync.job_mutex);
                av1_set_vert_loop_filter_done(cpi.common, lf_sync,
                                              cpi.common.seq_params.mib_size_log2);
            }
            return 0;
        }
    }

    // [subset] enc_worker_hook / av1_encode_tiles_mt: tile multi-threading of the encode stage,
    // only used with row_mt == 0 (the subset always encodes with row_mt == 1).

    internal static void av1_init_frame_mt(AV1_PRIMARY ppi, AV1_COMP cpi)
    {
        cpi.mt_info.workers = ppi.p_mt_info.workers;
        cpi.mt_info.num_workers = ppi.p_mt_info.num_workers;
        cpi.mt_info.tile_thr_data = ppi.p_mt_info.tile_thr_data;
        int i;
        for (i = MOD_FP; i < NUM_MT_MODULES; i++)
        {
            cpi.mt_info.num_mod_workers[i] =
                AOMMIN(cpi.mt_info.num_workers, ppi.p_mt_info.num_mod_workers[i]);
        }
    }

    internal static void av1_init_cdef_worker(AV1_COMP cpi)
    {
        // The allocation is done only for level 0 parallel frames. No change
        // in config is supported in the middle of a parallel encode set, since the
        // rest of the MT modules also do not support dynamic change of config.
        if (cpi.ppi.gf_group.frame_parallel_level[cpi.gf_frame_index] > 0) return;
        PrimaryMultiThreadInfo p_mt_info = cpi.ppi.p_mt_info;
        int num_cdef_workers = av1_get_num_mod_workers_for_alloc(p_mt_info, MOD_CDEF);

        av1_alloc_cdef_buffers(cpi.common, ref p_mt_info.cdef_worker,
                               cpi.mt_info.cdef_sync, num_cdef_workers, 1);
        cpi.mt_info.cdef_worker = p_mt_info.cdef_worker;
    }

    // [subset] av1_init_lr_mt_buffers: !CONFIG_REALTIME_ONLY.

    internal static void av1_init_mt_sync(AV1_COMP cpi, int is_first_pass)
    {
        AV1_COMMON cm = cpi.common;
        MultiThreadInfo mt_info = cpi.mt_info;

        cm.error.setjmp = 1;
        try
        {
            // Initialize enc row MT object.
            if (is_first_pass != 0 || cpi.oxcf.row_mt)
            {
                AV1EncRowMultiThreadInfo enc_row_mt = mt_info.enc_row_mt;
                if (enc_row_mt.mutex_ == null)
                {
                    pthread_mutex_init(ref enc_row_mt.mutex_, null);
                }
                if (enc_row_mt.cond_ == null)
                {
                    pthread_cond_init(ref enc_row_mt.cond_, null);
                }
            }

            if (is_first_pass == 0)
            {
                // Initialize global motion MT object.
                AV1GlobalMotionSync gm_sync = mt_info.gm_sync;
                if (gm_sync.mutex_ == null)
                {
                    pthread_mutex_init(ref gm_sync.mutex_, null);
                }
                // [subset] temporal filtering MT object (tf_sync): !CONFIG_REALTIME_ONLY.
                // Initialize CDEF MT object.
                AV1CdefSync cdef_sync = mt_info.cdef_sync;
                if (cdef_sync.mutex_ == null)
                {
                    pthread_mutex_init(ref cdef_sync.mutex_, null);
                }

                // Initialize loop filter MT object.
                AV1LfSync lf_sync = mt_info.lf_row_sync;
                // Number of superblock rows
                int sb_rows =
                    CEIL_POWER_OF_TWO(cm.height >> MI_SIZE_LOG2, MAX_MIB_SIZE_LOG2);
                PrimaryMultiThreadInfo p_mt_info = cpi.ppi.p_mt_info;
                int num_lf_workers = av1_get_num_mod_workers_for_alloc(p_mt_info, MOD_LPF);

                if (lf_sync.sync_range == 0 || sb_rows != lf_sync.rows ||
                    num_lf_workers > lf_sync.num_workers)
                {
                    av1_loop_filter_dealloc(lf_sync);
                    av1_loop_filter_alloc(lf_sync, cm, sb_rows, cm.width, num_lf_workers);
                }

                // Initialize tpl MT object.
                AV1TplRowMultiThreadInfo tpl_row_mt = mt_info.tpl_row_mt;
                if (tpl_row_mt.mutex_ == null)
                {
                    pthread_mutex_init(ref tpl_row_mt.mutex_, null);
                }

                // [subset] loop restoration MT object (lr_row_sync): !CONFIG_REALTIME_ONLY.

                // Initialization of pack bitstream MT object.
                AV1EncPackBSSync pack_bs_sync = mt_info.pack_bs_sync;
                if (pack_bs_sync.mutex_ == null)
                {
                    pthread_mutex_init(ref pack_bs_sync.mutex_, null);
                }
            }
            cm.error.setjmp = 0;
        }
        catch (AomException e)
        {
            record_error_info(cm.error, e);
            cm.error.setjmp = 0;
            // aom_internal_error_copy(&cpi->ppi->error, cm->error)
            throw new AomException(cm.error.error_code, cm.error.detail);
        }
    }

    // Computes the number of workers to be considered while allocating memory for a
    // multi-threaded module under FPMT.
    internal static int av1_get_num_mod_workers_for_alloc(PrimaryMultiThreadInfo p_mt_info,
                                                          MULTI_THREADED_MODULES mod_name)
    {
        int num_mod_workers = p_mt_info.num_mod_workers[mod_name];
        if (p_mt_info.num_mod_workers[MOD_FRAME_ENC] > 1)
        {
            // TODO(anyone): Change num_mod_workers to num_mod_workers[MOD_FRAME_ENC].
            // As frame parallel jobs will only perform multi-threading for the encode
            // stage, we can limit the allocations according to num_enc_workers per
            // frame parallel encode(a.k.a num_mod_workers[MOD_FRAME_ENC]).
            num_mod_workers = p_mt_info.num_workers;
        }
        return num_mod_workers;
    }

    internal static void av1_init_tile_thread_data(AV1_PRIMARY ppi, int is_first_pass)
    {
        PrimaryMultiThreadInfo p_mt_info = ppi.p_mt_info;

        Debug.Assert(p_mt_info.workers != null);
        Debug.Assert(p_mt_info.tile_thr_data != null);

        // [subset] is_highbitdepth: use_highbitdepth is 0, so the upsample_pred factor
        // (1 + is_highbitdepth) is 1.
        Debug.Assert(ppi.seq_params.use_highbitdepth == 0);
        int num_workers = p_mt_info.num_workers;
        int num_enc_workers = av1_get_num_mod_workers_for_alloc(p_mt_info, MOD_ENC);
        Debug.Assert(num_enc_workers <= num_workers);
        for (int i = num_workers - 1; i >= 0; i--)
        {
            EncWorkerData thread_data = p_mt_info.tile_thr_data[i];

            if (i > 0)
            {
                // Allocate thread data.
                ThreadData td = new ThreadData();  // aom_memalign + av1_zero(*td)
                thread_data.original_td = thread_data.td = td;

                // Set up shared coeff buffers.
                av1_setup_shared_coeff_buffer(ppi.seq_params, td.shared_coeff_buf,
                                              ppi.error);
                if (ppi.cpi.oxcf.mode != ALLINTRA)
                {
                    td.tmp_conv_dst = new CONV_BUF_TYPE[MAX_SB_SIZE * MAX_SB_SIZE];
                }

                // [subset] firstpass PICK_MODE_CONTEXT (is_first_pass || lap_enabled):
                // no first pass and no lookahead in the subset.
                Debug.Assert(!((is_first_pass != 0 || ppi.lap_enabled != 0) &&
                               i < p_mt_info.num_mod_workers[MOD_FP]));

                if (ppi.cpi.oxcf.mode != ALLINTRA)
                {
                    td.upsample_pred = new byte[(MAX_SB_SIZE + SUBPEL_TAPS) * MAX_SB_SIZE];
                }

                if (is_first_pass == 0 && i < num_enc_workers)
                {
                    // Set up sms_tree.
                    if (av1_setup_sms_tree(ppi.cpi, td) != 0)
                    {
                        throw new AomException(AOM_CODEC_MEM_ERROR, "Failed to allocate SMS tree");
                    }

                    // [subset] td->hash_value_buffer[2]: intra block copy hash (dropped
                    // from ThreadData).

                    // Allocate frame counters in thread data.
                    td.counts = new FRAME_COUNTS();

                    // [subset] td->palette_buffer: palette is off (dropped from ThreadData).

                    // The buffers 'tmp_pred_bufs[]', 'comp_rd_buffer' and 'obmc_buffer' are
                    // used in inter frames to store intermediate inter mode prediction
                    // results and are not required for allintra encoding mode. Hence, the
                    // memory allocations for these buffers are avoided for allintra
                    // encoding mode.
                    if (ppi.cpi.oxcf.kf_cfg.key_freq_max != 0)
                    {
                        // [subset] alloc_obmc_buffers / alloc_compound_type_rd_buffers:
                        // OBMC and masked compound are off (dropped from ThreadData).

                        for (int j = 0; j < 2; ++j)
                        {
                            td.tmp_pred_bufs[j] = new byte[2 * MAX_MB_PLANE * MAX_SB_SQUARE];
                        }
                    }

                    int plane_types = PLANE_TYPES >> ppi.seq_params.monochrome;
                    td.pixel_gradient_info =
                        new PixelLevelGradientInfo[plane_types * MAX_SB_SQUARE];

                    if (is_src_var_for_4x4_sub_blocks_caching_enabled(ppi.cpi))
                    {
                        BLOCK_SIZE sb_size = ppi.cpi.common.seq_params.sb_size;
                        int mi_count_in_sb =
                            mi_size_wide[sb_size] * mi_size_high[sb_size];

                        td.src_var_info_of_4x4_sub_blocks =
                            new Block4x4VarInfo[mi_count_in_sb];
                    }

                    if (ppi.cpi.sf.part_sf.partition_search_type == VAR_BASED_PARTITION)
                    {
                        int num_64x64_blocks =
                            (ppi.seq_params.sb_size == BLOCK_64X64) ? 1 : 4;
                        td.vt64x64 = alloc_array<VP64x64>(num_64x64_blocks);
                    }
                }
            }

            if (is_first_pass == 0 && ppi.cpi.oxcf.row_mt && i < num_enc_workers)
            {
                if (i == 0)
                {
                    for (int j = 0; j < ppi.num_fp_contexts; j++)
                    {
                        ppi.parallel_cpi[j].td.tctx = new FRAME_CONTEXT();
                    }
                }
                else
                {
                    thread_data.td.tctx = new FRAME_CONTEXT();
                }
            }
        }

        // Record the number of workers in encode stage multi-threading for which
        // allocation is done.
        p_mt_info.prev_num_enc_workers = num_enc_workers;
    }

    internal static void av1_create_workers(AV1_PRIMARY ppi, int num_workers)
    {
        PrimaryMultiThreadInfo p_mt_info = ppi.p_mt_info;
        AVxWorkerInterface winterface = aom_get_worker_interface();
        Debug.Assert(p_mt_info.num_workers == 0);

        p_mt_info.workers = new_array<AVxWorker>(num_workers);

        p_mt_info.tile_thr_data = new_array<EncWorkerData>(num_workers);

        for (int i = 0; i < num_workers; ++i)
        {
            AVxWorker worker = p_mt_info.workers[i];
            EncWorkerData thread_data = p_mt_info.tile_thr_data[i];

            winterface.init(worker);
            worker.thread_name = "aom enc worker";

            thread_data.thread_id = i;
            // Set the starting tile for each thread.
            thread_data.start = i;

            if (i > 0)
            {
                // Create threads
                if (winterface.reset(worker) == 0)
                    throw new AomException(AOM_CODEC_ERROR, "Tile encoder thread creation failed");
            }
            winterface.sync(worker);

            // Ensure p_mt_info->num_workers is the number of threads successfuly
            // created. Important to av1_terminate_workers().
            ++p_mt_info.num_workers;
        }
    }

    // This function will change the state and free the mutex of corresponding
    // workers and terminate the object. The object can not be re-used unless a call
    // to reset() is made.
    internal static void av1_terminate_workers(AV1_PRIMARY ppi)
    {
        PrimaryMultiThreadInfo p_mt_info = ppi.p_mt_info;
        for (int t = 0; t < p_mt_info.num_workers; ++t)
        {
            AVxWorker worker = p_mt_info.workers[t];
            aom_get_worker_interface().end(worker);
        }
    }

    // This function returns 1 if frame parallel encode is supported for
    // the current configuration. Returns 0 otherwise.
    private static int is_fpmt_config(AV1_PRIMARY ppi, AV1EncoderConfig oxcf)
    {
        // FPMT is enabled for AOM_Q and AOM_VBR.
        // TODO(Tarun): Test and enable resize config.
        if (oxcf.rc_cfg.mode == AOM_CBR || oxcf.rc_cfg.mode == AOM_CQ)
        {
            return 0;
        }
        // [subset] the remaining checks (svc, large scale tile, timing info, mode, error
        // resilience, resize, pass, threads, fp_mt): the subset is CBR, so they never run.
        throw new NotSupportedException("frame parallel multi-threading needs a non-CBR rate control mode");
    }

    internal static int av1_check_fpmt_config(AV1_PRIMARY ppi, AV1EncoderConfig oxcf)
    {
        if (is_fpmt_config(ppi, oxcf) != 0) return 1;
        // Reset frame parallel configuration for unsupported config
        if (ppi.num_fp_contexts > 1)
        {
            // [subset] frame parallel contexts only exist with FPMT (never in the subset).
            throw new NotSupportedException("frame parallel contexts are not in the subset");
        }
        return 0;
    }

    // [subset] compute_max_num_enc_workers, av1_compute_num_fp_contexts,
    // compute_num_workers_per_frame, prepare_fpmt_workers, launch_fpmt_workers,
    // restore_workers_after_fpmt, sync_fpmt_workers, get_compressed_data_hook,
    // av1_compress_parallel_frames: frame parallel multi-threading (FPMT), never executed.

    private static void launch_workers(MultiThreadInfo mt_info, int num_workers)
    {
        AVxWorkerInterface winterface = aom_get_worker_interface();
        for (int i = num_workers - 1; i >= 0; i--)
        {
            AVxWorker worker = mt_info.workers[i];
            worker.had_error = 0;
            if (i == 0)
                winterface.execute(worker);
            else
                winterface.launch(worker);
        }
    }

    private static void sync_enc_workers(MultiThreadInfo mt_info, AV1_COMMON cm, int num_workers)
    {
        AVxWorkerInterface winterface = aom_get_worker_interface();
        AVxWorker worker_main = mt_info.workers[0];
        int had_error = worker_main.had_error;
        // struct aom_internal_error_info error_info (copied by value in C).
        aom_codec_err_t error_code = 0;
        string error_detail = null;

        // Read the error_info of main thread.
        if (had_error != 0)
        {
            aom_internal_error_info main_error_info = ((EncWorkerData)worker_main.data1).error_info;
            error_code = main_error_info.error_code;
            error_detail = main_error_info.detail;
        }

        // Encoding ends.
        for (int i = num_workers - 1; i > 0; i--)
        {
            AVxWorker worker = mt_info.workers[i];
            if (winterface.sync(worker) == 0)
            {
                had_error = 1;
                aom_internal_error_info worker_error_info = ((EncWorkerData)worker.data1).error_info;
                error_code = worker_error_info.error_code;
                error_detail = worker_error_info.detail;
            }
        }

        // aom_internal_error_copy(cm->error, &error_info)
        if (had_error != 0) throw new AomException(error_code, error_detail);

        // Restore xd->error_info of the main thread back to cm->error so that the
        // multithreaded code, when executed using a single thread, has a valid
        // xd->error_info.
        MACROBLOCKD xd = ((EncWorkerData)worker_main.data1).td.mb.e_mbd;
        xd.error_info = cm.error;
    }

    private static void accumulate_counters_enc_workers(AV1_COMP cpi, int num_workers)
    {
        for (int i = num_workers - 1; i >= 0; i--)
        {
            AVxWorker worker = cpi.mt_info.workers[i];
            EncWorkerData thread_data = (EncWorkerData)worker.data1;
            cpi.intrabc_used |= thread_data.td.intrabc_used;
            cpi.deltaq_used |= thread_data.td.deltaq_used;
            // Accumulate rtc counters.
            if (frame_is_intra_only(cpi.common) == 0)
                av1_accumulate_rtc_counters(cpi, thread_data.td.mb);
            cpi.palette_pixel_num += thread_data.td.mb.palette_pixels;
            if (thread_data.td != cpi.td)
            {
                // Keep these conditional expressions in sync with the corresponding ones
                // in prepare_enc_workers().
                if (cpi.sf.inter_sf.mv_cost_upd_level != INTERNAL_COST_UPD_OFF)
                {
                    thread_data.td.mv_costs_alloc = null;
                }
                // [subset] dv_costs_alloc free: dv_cost_upd_level is OFF in the subset (and
                // the IntraBC dv costs are dropped from ThreadData).
                Debug.Assert(cpi.sf.intra_sf.dv_cost_upd_level == INTERNAL_COST_UPD_OFF);
            }
            av1_dealloc_mb_data(thread_data.td.mb, av1_num_planes(cpi.common));

            // Accumulate counters.
            if (i > 0)
            {
                av1_accumulate_frame_counts(cpi.counts, thread_data.td.counts);
                accumulate_rd_opt(cpi.td, thread_data.td);
                cpi.td.mb.txfm_search_info.txb_split_count +=
                    thread_data.td.mb.txfm_search_info.txb_split_count;
            }
        }
    }

    private static void prepare_enc_workers(AV1_COMP cpi, AVxWorkerHook hook,
                                            int num_workers)
    {
        MultiThreadInfo mt_info = cpi.mt_info;
        AV1_COMMON cm = cpi.common;

        // If a previous frame's encode raised an error after this function ran,
        // accumulate_counters_enc_workers() was skipped via longjmp and the mb
        // buffers (including cpi->td.mb) were never freed. Free them here so the
        // shallow copy of cpi->td.mb below cannot leave a worker's mb aliasing
        // cpi->td.mb's heap allocations on a subsequent partial-allocation failure
        // (which would double-free in encoder_destroy()).
        for (int i = num_workers - 1; i >= 0; i--)
        {
            EncWorkerData thread_data = mt_info.tile_thr_data[i];
            ThreadData td = (i == 0) ? cpi.td : thread_data.original_td;
            if (td != null) av1_dealloc_mb_data(td.mb, av1_num_planes(cm));
        }

        for (int i = num_workers - 1; i >= 0; i--)
        {
            AVxWorker worker = mt_info.workers[i];
            EncWorkerData thread_data = mt_info.tile_thr_data[i];

            worker.hook = hook;
            worker.data1 = thread_data;
            worker.data2 = num_workers;  // (void *)(intptr_t)num_workers

            thread_data.thread_id = i;
            // Set the starting tile for each thread.
            thread_data.start = i;

            thread_data.cpi = cpi;
            if (i == 0)
            {
                thread_data.td = cpi.td;
            }
            else
            {
                thread_data.td = thread_data.original_td;
            }

            thread_data.td.intrabc_used = 0;
            thread_data.td.deltaq_used = 0;
            thread_data.td.abs_sum_level = 0;
            thread_data.td.rd_counts.seg_tmp_pred_cost[0] = 0;
            thread_data.td.rd_counts.seg_tmp_pred_cost[1] = 0;

            // Before encoding a frame, copy the thread data from cpi.
            if (thread_data.td != cpi.td)
            {
                thread_data.td.mb.CopyFrom(cpi.td.mb);
                thread_data.td.rd_counts.CopyFrom(cpi.td.rd_counts);
                // [subset] td->mb.obmc_buffer = td->obmc_buffer and
                // td->mb.intrabc_hash_info.hash_value_buffer[x] = td->hash_value_buffer[x]:
                // OBMC and intra block copy buffers are dropped.

                // Keep these conditional expressions in sync with the corresponding ones
                // in accumulate_counters_enc_workers().
                if (cpi.sf.inter_sf.mv_cost_upd_level != INTERNAL_COST_UPD_OFF)
                {
                    thread_data.td.mv_costs_alloc = thread_data.td.mv_costs_pool ??= new MvCosts();
                    thread_data.td.mb.mv_costs = thread_data.td.mv_costs_alloc;
                    thread_data.td.mb.mv_costs.CopyFrom(cpi.td.mb.mv_costs);
                }
                // [subset] dv_costs (dv_cost_upd_level != OFF): IntraBC is off.
                Debug.Assert(cpi.sf.intra_sf.dv_cost_upd_level == INTERNAL_COST_UPD_OFF);
            }
            av1_alloc_mb_data(cpi, thread_data.td.mb);

            // Reset rtc counters.
            av1_init_rtc_counters(thread_data.td.mb);

            thread_data.td.mb.palette_pixels = 0;

            if (thread_data.td.counts != cpi.counts)
            {
                thread_data.td.counts.CopyFrom(cpi.counts);
            }

            if (i > 0)
            {
                // [subset] mb.palette_buffer / mb.comp_rd_buffer: dropped (palette and
                // masked compound are off).
                thread_data.td.mb.tmp_conv_dst = thread_data.td.tmp_conv_dst;
                thread_data.td.mb.upsample_pred = thread_data.td.upsample_pred;
                for (int j = 0; j < 2; ++j)
                {
                    thread_data.td.mb.tmp_pred_bufs[j] =
                        thread_data.td.tmp_pred_bufs[j];
                }
                thread_data.td.mb.pixel_gradient_info =
                    thread_data.td.pixel_gradient_info;

                thread_data.td.mb.src_var_info_of_4x4_sub_blocks =
                    thread_data.td.src_var_info_of_4x4_sub_blocks;

                thread_data.td.mb.e_mbd.tmp_conv_dst = thread_data.td.mb.tmp_conv_dst;
                thread_data.td.mb.e_mbd.tmp_upsample_pred =
                    thread_data.td.mb.upsample_pred;
                for (int j = 0; j < 2; ++j)
                {
                    thread_data.td.mb.e_mbd.tmp_obmc_bufs[j] =
                        thread_data.td.mb.tmp_pred_bufs[j];
                }
            }
        }
    }

    // [subset] fp_prepare_enc_workers: !CONFIG_REALTIME_ONLY.

    // Computes the number of workers for row multi-threading of encoding stage
    private static int compute_num_enc_row_mt_workers(AV1_COMMON cm, int max_threads)
    {
        TileInfo tile_info = default;
        int tile_cols = cm.tiles.cols;
        int tile_rows = cm.tiles.rows;
        int total_num_threads_row_mt = 0;
        for (int row = 0; row < tile_rows; row++)
        {
            for (int col = 0; col < tile_cols; col++)
            {
                av1_tile_init(ref tile_info, cm, row, col);
                int num_sb_rows_in_tile = av1_get_sb_rows_in_tile(cm, in tile_info);
                int num_sb_cols_in_tile = av1_get_sb_cols_in_tile(cm, in tile_info);
                total_num_threads_row_mt +=
                    AOMMIN((num_sb_cols_in_tile + 1) >> 1, num_sb_rows_in_tile);
            }
        }
        return AOMMIN(max_threads, total_num_threads_row_mt);
    }

    // Computes the number of workers for tile multi-threading of encoding stage
    private static int compute_num_enc_tile_mt_workers(AV1_COMMON cm, int max_threads)
    {
        int tile_cols = cm.tiles.cols;
        int tile_rows = cm.tiles.rows;
        return AOMMIN(max_threads, tile_cols * tile_rows);
    }

    // Find max worker of all MT stages
    internal static int av1_get_max_num_workers(AV1_COMP cpi)
    {
        int max_num_workers = 0;
        for (int i = MOD_FP; i < NUM_MT_MODULES; i++)
            max_num_workers =
                AOMMAX(cpi.ppi.p_mt_info.num_mod_workers[i], max_num_workers);
        Debug.Assert(max_num_workers >= 1);
        return AOMMIN(max_num_workers, cpi.oxcf.max_threads);
    }

    // Computes the number of workers for encoding stage (row/tile multi-threading)
    private static int compute_num_enc_workers(AV1_COMP cpi, int max_workers)
    {
        if (max_workers <= 1) return 1;
        if (cpi.oxcf.row_mt)
            return compute_num_enc_row_mt_workers(cpi.common, max_workers);
        else
            return compute_num_enc_tile_mt_workers(cpi.common, max_workers);
    }

    // Accumulate frame counts. FRAME_COUNTS consist solely of 'unsigned int'
    // members, so we treat it as an array, and sum over the whole length.
    // PORT-NOTE: the subset's FRAME_COUNTS holds only switchable_interp (which is all of
    // FRAME_COUNTS without CONFIG_ENTROPY_STATS); every member is summed.
    internal static void av1_accumulate_frame_counts(FRAME_COUNTS acc_counts,
                                                     FRAME_COUNTS counts)
    {
        for (int i = 0; i < SWITCHABLE_FILTER_CONTEXTS; i++)
        {
            uint[] acc = acc_counts.switchable_interp[i];
            uint[] cnt = counts.switchable_interp[i];
            for (int j = 0; j < SWITCHABLE_FILTERS; j++) acc[j] += cnt[j];
        }
    }

    // Computes the maximum number of sb rows and sb_cols across tiles which are
    // used to allocate memory for multi-threaded encoding with row-mt=1.
    private static void compute_max_sb_rows_cols(AV1_COMMON cm,
                                                 ref int max_sb_rows_in_tile,
                                                 ref int max_sb_cols_in_tile)
    {
        int tile_rows = cm.tiles.rows;
        int mib_size_log2 = cm.seq_params.mib_size_log2;
        int num_mi_rows = cm.mi_params.mi_rows;
        int[] row_start_sb = cm.tiles.row_start_sb;
        for (int row = 0; row < tile_rows; row++)
        {
            int mi_row_start = row_start_sb[row] << mib_size_log2;
            int mi_row_end =
                AOMMIN(row_start_sb[row + 1] << mib_size_log2, num_mi_rows);
            int num_sb_rows_in_tile =
                CEIL_POWER_OF_TWO(mi_row_end - mi_row_start, mib_size_log2);
            max_sb_rows_in_tile = AOMMAX(max_sb_rows_in_tile, num_sb_rows_in_tile);
        }

        int tile_cols = cm.tiles.cols;
        int num_mi_cols = cm.mi_params.mi_cols;
        int[] col_start_sb = cm.tiles.col_start_sb;
        for (int col = 0; col < tile_cols; col++)
        {
            int mi_col_start = col_start_sb[col] << mib_size_log2;
            int mi_col_end =
                AOMMIN(col_start_sb[col + 1] << mib_size_log2, num_mi_cols);
            int num_sb_cols_in_tile =
                CEIL_POWER_OF_TWO(mi_col_end - mi_col_start, mib_size_log2);
            max_sb_cols_in_tile = AOMMAX(max_sb_cols_in_tile, num_sb_cols_in_tile);
        }
    }

    // [subset] av1_fp_compute_num_enc_workers / fp_compute_max_mb_rows: !CONFIG_REALTIME_ONLY.

    private static void lpf_pipeline_mt_init(AV1_COMP cpi, int num_workers)
    {
        // Pipelining of loop-filtering after encoding is enabled when loop-filter
        // level is chosen based on quantizer and frame type. It is disabled in case
        // of 'LOOPFILTER_SELECTIVELY' as the stats collected during encoding stage
        // decides the filter level. Loop-filtering is disabled when
        // SKIP_APPLY_LOOPFILTER is set and for frames with intra block copy tool
        // enabled.
        AV1_COMMON cm = cpi.common;
        int use_loopfilter = is_loopfilter_used(cm);
        int use_superres = av1_superres_scaled(cm);
        int use_cdef = is_cdef_used(cm);
        int use_restoration = is_restoration_used(cm);
        MultiThreadInfo mt_info = cpi.mt_info;
        MACROBLOCKD xd = cpi.td.mb.e_mbd;

        uint skip_apply_postproc_filters =
            derive_skip_apply_postproc_filters(cpi, use_loopfilter, use_cdef,
                                               use_superres, use_restoration);
        mt_info.pipeline_lpf_mt_with_enc = B(
            (cpi.oxcf.mode == REALTIME) && (cpi.oxcf.speed >= 5) &&
            (cpi.sf.lpf_sf.lpf_pick == LPF_PICK_FROM_Q) &&
            (cpi.oxcf.algo_cfg.loopfilter_control != LOOPFILTER_SELECTIVELY) &&
            !cm.features.allow_intrabc &&
            ((skip_apply_postproc_filters & SKIP_APPLY_LOOPFILTER) == 0));

        if (mt_info.pipeline_lpf_mt_with_enc == 0) return;

        set_postproc_filter_default_params(cm);

        if (use_loopfilter == 0) return;

        LPF_PICK_METHOD method = cpi.sf.lpf_sf.lpf_pick;
        Debug.Assert(method == LPF_PICK_FROM_Q);
        Debug.Assert(cpi.oxcf.algo_cfg.loopfilter_control != LOOPFILTER_SELECTIVELY);

        av1_pick_filter_level(cpi.source, cpi, method);

        loopfilter lf = cm.lf;
        int plane_start = 0;
        int plane_end = av1_num_planes(cm);
        int[] planes_to_lf = new int[MAX_MB_PLANE];
        if (lpf_mt_with_enc_enabled(cpi.mt_info.pipeline_lpf_mt_with_enc,
                                    lf.filter_level))
        {
            set_planes_to_loop_filter(lf, planes_to_lf, plane_start, plane_end);
            int lpf_opt_level = get_lpf_opt_level(cpi.sf);
            Debug.Assert(lpf_opt_level == 2);

            int start_mi_row = 0;
            int end_mi_row = start_mi_row + cm.mi_params.mi_rows;

            av1_loop_filter_frame_init(cm, plane_start, plane_end);

            Debug.Assert(mt_info.num_mod_workers[MOD_ENC] ==
                         mt_info.num_mod_workers[MOD_LPF]);
            loop_filter_frame_mt_init(cm, start_mi_row, end_mi_row, planes_to_lf,
                                      mt_info.num_mod_workers[MOD_LPF],
                                      mt_info.lf_row_sync, lpf_opt_level,
                                      cm.seq_params.mib_size_log2);

            for (int i = num_workers - 1; i >= 0; i--)
            {
                EncWorkerData thread_data = mt_info.tile_thr_data[i];
                // Initialize loopfilter data
                thread_data.lf_sync = mt_info.lf_row_sync;
                thread_data.lf_data = thread_data.lf_sync.lfdata[i];
                loop_filter_data_reset(thread_data.lf_data, cm.cur_frame.buf, cm, xd);
            }
        }
    }

    internal static void av1_encode_tiles_row_mt(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        MultiThreadInfo mt_info = cpi.mt_info;
        AV1EncRowMultiThreadInfo enc_row_mt = mt_info.enc_row_mt;
        int tile_cols = cm.tiles.cols;
        int tile_rows = cm.tiles.rows;
        int sb_rows_in_frame = get_sb_rows_in_frame(cm);
        int[] thread_id_to_tile_id = enc_row_mt.thread_id_to_tile_id;
        int max_sb_rows_in_tile = 0, max_sb_cols_in_tile = 0;
        int num_workers = mt_info.num_mod_workers[MOD_ENC];

        compute_max_sb_rows_cols(cm, ref max_sb_rows_in_tile, ref max_sb_cols_in_tile);
        bool alloc_row_mt_mem =
            (enc_row_mt.allocated_tile_cols != tile_cols ||
             enc_row_mt.allocated_tile_rows != tile_rows ||
             enc_row_mt.allocated_rows != max_sb_rows_in_tile ||
             enc_row_mt.allocated_cols != (max_sb_cols_in_tile - 1) ||
             enc_row_mt.allocated_sb_rows != sb_rows_in_frame);
        bool alloc_tile_data = cpi.allocated_tiles != tile_cols * tile_rows;

        Debug.Assert(cpi.tile_data != null || alloc_tile_data);
        Debug.Assert(cpi.tile_data != null || cpi.allocated_tiles == 0);
        if (alloc_tile_data)
        {
            av1_alloc_tile_data(cpi);
        }

        Debug.Assert(!alloc_tile_data || alloc_row_mt_mem);
        if (alloc_row_mt_mem)
        {
            row_mt_mem_alloc(cpi, max_sb_rows_in_tile, max_sb_cols_in_tile,
                             cpi.oxcf.algo_cfg.cdf_update_mode);
        }

        num_workers = AOMMIN(num_workers, mt_info.num_workers);
        lpf_pipeline_mt_init(cpi, num_workers);

        av1_init_tile_data(cpi);

        memset<int>(thread_id_to_tile_id, -1, MAX_NUM_THREADS);
        memset(enc_row_mt.num_tile_cols_done, 0, sb_rows_in_frame);
        enc_row_mt.row_mt_exit = false;

        for (int tile_row = 0; tile_row < tile_rows; tile_row++)
        {
            for (int tile_col = 0; tile_col < tile_cols; tile_col++)
            {
                int tile_index = tile_row * tile_cols + tile_col;
                TileDataEnc this_tile = cpi.tile_data[tile_index];
                AV1EncRowMultiThreadSync row_mt_sync = this_tile.row_mt_sync;

                // Initialize num_finished_cols to -1 for all rows.
                memset(row_mt_sync.num_finished_cols, -1, max_sb_rows_in_tile);
                row_mt_sync.next_mi_row = this_tile.tile_info.mi_row_start;
                row_mt_sync.num_threads_working = 0;
                row_mt_sync.intrabc_extra_top_right_sb_delay =
                    av1_get_intrabc_extra_top_right_sb_delay(cm);

                av1_inter_mode_data_init(this_tile);
                av1_zero_above_context(cm, cpi.td.mb.e_mbd,
                                       this_tile.tile_info.mi_col_start,
                                       this_tile.tile_info.mi_col_end, tile_row);
            }
        }

        assign_tile_to_thread(thread_id_to_tile_id, tile_cols * tile_rows,
                              num_workers);
        prepare_enc_workers(cpi, enc_row_mt_worker_hook_fn, num_workers);
        launch_workers(cpi.mt_info, num_workers);
        sync_enc_workers(cpi.mt_info, cm, num_workers);
        if (cm.delta_q_info.delta_lf_present_flag != 0)
        {
            // [subset] update_delta_lf_for_row_mt: delta_lf_present_flag needs deltaq_mode.
            throw new NotSupportedException("delta lf is not in the subset");
        }
        accumulate_counters_enc_workers(cpi, num_workers);
    }

    // [subset] intrabc_hash_worker_hook / av1_build_intrabc_hash_tables_mt: intra block copy.
    // [subset] dealloc_thread_data_src_diff_buf, av1_fp_encode_tiles_row_mt, the tpl row-mt
    // functions (av1_tpl_row_mt_sync_*, tpl_worker_hook, av1_tpl_dealloc, av1_tpl_alloc,
    // av1_mc_flow_dispenser_mt), the temporal filter / global motion / loop restoration MT
    // functions and the allintra wiener variance MT functions: !CONFIG_REALTIME_ONLY or never
    // executed in the subset.

    // Compare and order tiles based on absolute sum of tx coeffs.
    private static int compare_tile_order(PackBSTileOrder tile_a, PackBSTileOrder tile_b)
    {
        if (tile_a.abs_sum_level > tile_b.abs_sum_level)
            return -1;
        else if (tile_a.abs_sum_level == tile_b.abs_sum_level)
            return (tile_a.tile_idx > tile_b.tile_idx ? 1 : -1);
        else
            return 1;
    }

    // PORT-NOTE: qsort -> Span.Sort. compare_tile_order is a strict total order over distinct
    // tile_idx values, so the sorted order is unique whatever the algorithm; the wrapper only
    // answers 0 for an element compared with itself (which the C comparator never sees from
    // qsort, but .NET's introsort may ask).
    private static readonly Comparison<PackBSTileOrder> compare_tile_order_fn =
        (a, b) => a.tile_idx == b.tile_idx ? 0 : compare_tile_order(a, b);

    // Get next tile index to be processed for pack bitstream
    private static int get_next_pack_bs_tile_idx(AV1EncPackBSSync pack_bs_sync, int num_tiles)
    {
        Debug.Assert(pack_bs_sync.next_job_idx <= num_tiles);
        if (pack_bs_sync.next_job_idx == num_tiles) return -1;

        return pack_bs_sync.pack_bs_tile_order[pack_bs_sync.next_job_idx++]
            .tile_idx;
    }

    // Calculates bitstream chunk size based on total buffer size and tile or tile
    // group size.
    private static int get_bs_chunk_size(int tg_or_tile_size,
                                         int frame_or_tg_size,
                                         ref int remain_buf_size,
                                         int max_buf_size, int is_last_chunk)
    {
        int this_chunk_size;
        Debug.Assert(remain_buf_size > 0);
        if (is_last_chunk != 0)
        {
            this_chunk_size = remain_buf_size;
            remain_buf_size = 0;
        }
        else
        {
            ulong size_scale = (ulong)max_buf_size * (ulong)tg_or_tile_size;
            this_chunk_size = (int)(size_scale / (ulong)frame_or_tg_size);
            remain_buf_size -= this_chunk_size;
            Debug.Assert(remain_buf_size > 0);
        }
        Debug.Assert(this_chunk_size > 0);
        return this_chunk_size;
    }

    // Initializes params required for pack bitstream tile.
    private static void init_tile_pack_bs_params(AV1_COMP cpi, Ptr<byte> dst,
                                                 int dst_size,
                                                 Ptr<aom_write_bit_buffer> saved_wb,
                                                 PackBSParams[] pack_bs_params_arr,
                                                 byte obu_extn_header)
    {
        MACROBLOCKD xd = cpi.td.mb.e_mbd;
        AV1_COMMON cm = cpi.common;
        CommonTileParams tiles = cm.tiles;
        int num_tiles = tiles.cols * tiles.rows;
        // Fixed size tile groups for the moment
        int num_tg_hdrs = cpi.num_tg;
        // Tile group size in terms of number of tiles.
        int tg_size_in_tiles = (num_tiles + num_tg_hdrs - 1) / num_tg_hdrs;
        Ptr<byte> tile_dst = dst;
        Ptr<byte> tile_data_curr = dst;
        // Max tile group count can not be more than MAX_TILES.
        int[] tg_size_mi = new int[MAX_TILES];  // Size of tile group in mi units
        int tile_idx;
        int tg_idx = 0;
        int tile_count_in_tg = 0;
        int new_tg = 1;

        // Populate pack bitstream params of all tiles.
        for (tile_idx = 0; tile_idx < num_tiles; tile_idx++)
        {
            ref TileInfo tile_info = ref cpi.tile_data[tile_idx].tile_info;
            PackBSParams pack_bs_params = pack_bs_params_arr[tile_idx];
            // Calculate tile size in mi units.
            int tile_size_mi = (tile_info.mi_col_end - tile_info.mi_col_start) *
                               (tile_info.mi_row_end - tile_info.mi_row_start);
            int is_last_tile_in_tg = 0;
            tile_count_in_tg++;
            if (tile_count_in_tg == tg_size_in_tiles || tile_idx == (num_tiles - 1))
                is_last_tile_in_tg = 1;

            // Populate pack bitstream params of this tile.
            pack_bs_params.curr_tg_hdr_size = 0;
            pack_bs_params.obu_extn_header = obu_extn_header;
            pack_bs_params.saved_wb = saved_wb;
            pack_bs_params.obu_header_size = 0;
            pack_bs_params.is_last_tile_in_tg = is_last_tile_in_tg;
            pack_bs_params.new_tg = new_tg;
            pack_bs_params.tile_col = tile_info.tile_col;
            pack_bs_params.tile_row = tile_info.tile_row;
            pack_bs_params.tile_size_mi = tile_size_mi;
            tg_size_mi[tg_idx] += tile_size_mi;

            if (new_tg != 0) new_tg = 0;
            if (is_last_tile_in_tg != 0)
            {
                tile_count_in_tg = 0;
                new_tg = 1;
                tg_idx++;
            }
        }

        Debug.Assert(dst_size > 0);
        int[] tg_buf_size = new int[MAX_TILES];
        int max_buf_size = dst_size;
        int remain_buf_size = max_buf_size;
        int frame_size_mi = cm.mi_params.mi_rows * cm.mi_params.mi_cols;

        tile_idx = 0;
        // Prepare obu, tile group and frame header of each tile group.
        for (tg_idx = 0; tg_idx < cpi.num_tg; tg_idx++)
        {
            PackBSParams pack_bs_params = pack_bs_params_arr[tile_idx];
            int is_last_tg = B(tg_idx == cpi.num_tg - 1);
            // Prorate bitstream buffer size based on tile group size and available
            // buffer size. This buffer will be used to store headers and tile data.
            tg_buf_size[tg_idx] =
                get_bs_chunk_size(tg_size_mi[tg_idx], frame_size_mi, ref remain_buf_size,
                                  max_buf_size, is_last_tg);

            pack_bs_params.dst = tile_dst;
            pack_bs_params.tile_data_curr = tile_dst;

            // Write obu, tile group and frame header at first tile in the tile
            // group.
            av1_write_obu_tg_tile_headers(cpi, xd, pack_bs_params, tile_idx);
            tile_dst += tg_buf_size[tg_idx];

            // Exclude headers from tile group buffer size.
            tg_buf_size[tg_idx] -= pack_bs_params.curr_tg_hdr_size;
            tile_idx += tg_size_in_tiles;
        }

        tg_idx = 0;
        // Calculate bitstream buffer size of each tile in the tile group.
        for (tile_idx = 0; tile_idx < num_tiles; tile_idx++)
        {
            PackBSParams pack_bs_params = pack_bs_params_arr[tile_idx];

            if (pack_bs_params.new_tg != 0)
            {
                max_buf_size = tg_buf_size[tg_idx];
                remain_buf_size = max_buf_size;
            }

            // Prorate bitstream buffer size of this tile based on tile size and
            // available buffer size. For this proration, header size is not accounted.
            int tile_buf_size = get_bs_chunk_size(
                pack_bs_params.tile_size_mi, tg_size_mi[tg_idx], ref remain_buf_size,
                max_buf_size, pack_bs_params.is_last_tile_in_tg);
            pack_bs_params.tile_buf_size = tile_buf_size;

            // Update base address of bitstream buffer for tile and tile group.
            if (pack_bs_params.new_tg != 0)
            {
                tile_dst = pack_bs_params.dst;
                tile_data_curr = pack_bs_params.tile_data_curr;
                // Account header size in first tile of a tile group.
                pack_bs_params.tile_buf_size += pack_bs_params.curr_tg_hdr_size;
            }
            else
            {
                pack_bs_params.dst = tile_dst;
                pack_bs_params.tile_data_curr = tile_data_curr;
            }

            if (pack_bs_params.is_last_tile_in_tg != 0) tg_idx++;
            tile_dst += pack_bs_params.tile_buf_size;
        }
    }

    private static readonly AVxWorkerHook pack_bs_worker_hook_fn = pack_bs_worker_hook;

    // Worker hook function of pack bitsteam multithreading.
    private static int pack_bs_worker_hook(object arg1, object arg2)
    {
        EncWorkerData thread_data = (EncWorkerData)arg1;
        PackBSParams[] pack_bs_params = (PackBSParams[])arg2;
        AV1_COMP cpi = thread_data.cpi;
        AV1_COMMON cm = cpi.common;
        AV1EncPackBSSync pack_bs_sync = cpi.mt_info.pack_bs_sync;
        CommonTileParams tiles = cm.tiles;
        int num_tiles = tiles.cols * tiles.rows;

        pthread_mutex_t pack_bs_mutex = pack_bs_sync.mutex_;
        MACROBLOCKD xd = thread_data.td.mb.e_mbd;
        aom_internal_error_info error_info = thread_data.error_info;
        xd.error_info = error_info;

        // The jmp_buf is valid only for the duration of the function that calls
        // setjmp(). Therefore, this function must reset the 'setjmp' field to 0
        // before it returns.
        error_info.setjmp = 1;
        try
        {
            while (true)
            {
                pthread_mutex_lock(pack_bs_mutex);
                int tile_idx =
                    pack_bs_sync.pack_bs_mt_exit
                        ? -1
                        : get_next_pack_bs_tile_idx(pack_bs_sync, num_tiles);
                pthread_mutex_unlock(pack_bs_mutex);
                // When pack_bs_mt_exit is set to true, other workers need not pursue any
                // further jobs.
                if (tile_idx == -1) break;
                TileDataEnc this_tile = cpi.tile_data[tile_idx];
                thread_data.td.mb.e_mbd.tile_ctx = this_tile.tctx;

                av1_pack_tile_info(cpi, thread_data.td, pack_bs_params[tile_idx]);
            }

            error_info.setjmp = 0;
            return 1;
        }
        catch (AomException e)
        {
            record_error_info(error_info, e);
            error_info.setjmp = 0;
            pthread_mutex_lock(pack_bs_mutex);
            pack_bs_sync.pack_bs_mt_exit = true;
            pthread_mutex_unlock(pack_bs_mutex);
            return 0;
        }
    }

    // Prepares thread data and workers of pack bitsteam multithreading.
    private static void prepare_pack_bs_workers(AV1_COMP cpi,
                                                PackBSParams[] pack_bs_params,
                                                AVxWorkerHook hook, int num_workers)
    {
        MultiThreadInfo mt_info = cpi.mt_info;
        for (int i = num_workers - 1; i >= 0; i--)
        {
            AVxWorker worker = mt_info.workers[i];
            EncWorkerData thread_data = mt_info.tile_thr_data[i];
            if (i == 0)
            {
                thread_data.td = cpi.td;
            }
            else
            {
                thread_data.td = thread_data.original_td;
            }

            if (thread_data.td != cpi.td) thread_data.td.mb.CopyFrom(cpi.td.mb);

            thread_data.cpi = cpi;
            thread_data.start = i;
            thread_data.thread_id = i;
            av1_reset_pack_bs_thread_data(thread_data.td);

            worker.hook = hook;
            worker.data1 = thread_data;
            worker.data2 = pack_bs_params;
        }

        AV1_COMMON cm = cpi.common;
        AV1EncPackBSSync pack_bs_sync = mt_info.pack_bs_sync;
        ushort num_tiles = (ushort)(cm.tiles.rows * cm.tiles.cols);
        pack_bs_sync.next_job_idx = 0;
        pack_bs_sync.pack_bs_mt_exit = false;

        PackBSTileOrder[] pack_bs_tile_order = pack_bs_sync.pack_bs_tile_order;
        // Reset tile order data of pack bitstream
        pack_bs_tile_order.AsSpan(0, num_tiles).Clear();

        // Populate pack bitstream tile order structure
        for (ushort tile_idx = 0; tile_idx < num_tiles; tile_idx++)
        {
            pack_bs_tile_order[tile_idx].abs_sum_level =
                cpi.tile_data[tile_idx].abs_sum_level;
            pack_bs_tile_order[tile_idx].tile_idx = tile_idx;
        }

        // Sort tiles in descending order based on tile area.
        pack_bs_tile_order.AsSpan(0, num_tiles).Sort(compare_tile_order_fn);
    }

    // Accumulates data after pack bitsteam processing.
    private static void accumulate_pack_bs_data(
        AV1_COMP cpi, PackBSParams[] pack_bs_params_arr,
        Ptr<byte> dst, ref uint total_size, in FrameHeaderInfo fh_info,
        ref int largest_tile_id, ref uint max_tile_size,
        ref uint obu_header_size, ref Ptr<byte> tile_data_start,
        int num_workers)
    {
        AV1_COMMON cm = cpi.common;
        CommonTileParams tiles = cm.tiles;
        int tile_count = tiles.cols * tiles.rows;
        // Fixed size tile groups for the moment
        int curr_tg_data_size = 0;
        int is_first_tg = 1;
        Ptr<byte> curr_tg_start = dst;
        int src_offset = 0;
        int dst_offset = 0;

        for (int tile_idx = 0; tile_idx < tile_count; tile_idx++)
        {
            // PackBSParams stores all parameters required to pack tile and header
            // info.
            PackBSParams pack_bs_params = pack_bs_params_arr[tile_idx];
            uint tile_size = 0;

            if (pack_bs_params.new_tg != 0)
            {
                curr_tg_start = dst + (int)total_size;
                curr_tg_data_size = pack_bs_params.curr_tg_hdr_size;
                tile_data_start += pack_bs_params.curr_tg_hdr_size;
                obu_header_size = pack_bs_params.obu_header_size;
            }
            curr_tg_data_size +=
                pack_bs_params.buf.size + (pack_bs_params.is_last_tile_in_tg != 0 ? 0 : 4);

            if ((uint)pack_bs_params.buf.size > max_tile_size)
            {
                largest_tile_id = tile_idx;
                max_tile_size = (uint)pack_bs_params.buf.size;
            }
            tile_size +=
                (uint)pack_bs_params.buf.size + pack_bs_params.total_size.Value;

            // Pack all the chunks of tile bitstreams together
            if (tile_idx != 0) memmove(dst + dst_offset, dst + src_offset, (int)tile_size);

            if (pack_bs_params.is_last_tile_in_tg != 0)
                av1_write_last_tile_info(
                    cpi, in fh_info, pack_bs_params.saved_wb, ref curr_tg_data_size,
                    curr_tg_start, ref tile_size, ref tile_data_start, ref largest_tile_id,
                    ref is_first_tg, obu_header_size, pack_bs_params.obu_extn_header);
            src_offset += pack_bs_params.tile_buf_size;
            dst_offset += (int)tile_size;
            total_size += tile_size;
        }

        // Accumulate thread data
        MultiThreadInfo mt_info = cpi.mt_info;
        for (int idx = num_workers - 1; idx >= 0; idx--)
        {
            ThreadData td = mt_info.tile_thr_data[idx].td;
            av1_accumulate_pack_bs_thread_data(cpi, td);
        }
    }

    internal static void av1_write_tile_obu_mt(
        AV1_COMP cpi, Ptr<byte> dst, int dst_size, ref uint total_size,
        Ptr<aom_write_bit_buffer> saved_wb, byte obu_extn_header,
        in FrameHeaderInfo fh_info, ref int largest_tile_id, ref uint max_tile_size,
        ref uint obu_header_size, ref Ptr<byte> tile_data_start, int num_workers)
    {
        MultiThreadInfo mt_info = cpi.mt_info;

        // [port] C stack arrays (pack_bs_params uninitialised, tile_size = { 0 }), kept in
        // mt_info across frames.
        PackBSParams[] pack_bs_params = mt_info.pack_bs_params_buf;
        uint[] tile_size = mt_info.pack_bs_tile_size_buf;
        if (pack_bs_params == null)
        {
            mt_info.pack_bs_params_buf = pack_bs_params = new_array<PackBSParams>(MAX_TILES);
            mt_info.pack_bs_tile_size_buf = tile_size = new uint[MAX_TILES];
        }
        tile_size.AsSpan().Clear();
        for (int tile_idx = 0; tile_idx < MAX_TILES; tile_idx++)
            pack_bs_params[tile_idx].total_size = new Ptr<uint>(tile_size, tile_idx);

        init_tile_pack_bs_params(cpi, dst, dst_size, saved_wb, pack_bs_params,
                                 obu_extn_header);
        prepare_pack_bs_workers(cpi, pack_bs_params, pack_bs_worker_hook_fn,
                                num_workers);
        launch_workers(mt_info, num_workers);
        sync_enc_workers(mt_info, cpi.common, num_workers);
        accumulate_pack_bs_data(cpi, pack_bs_params, dst, ref total_size, in fh_info,
                                ref largest_tile_id, ref max_tile_size, ref obu_header_size,
                                ref tile_data_start, num_workers);
    }

    // Deallocate memory for CDEF search multi-thread synchronization.
    internal static void av1_cdef_mt_dealloc(AV1CdefSync cdef_sync)
    {
        Debug.Assert(cdef_sync != null);
        if (cdef_sync.mutex_ != null)
        {
            pthread_mutex_destroy(cdef_sync.mutex_);
            cdef_sync.mutex_ = null;
        }
    }

    // Updates the row and column indices of the next job to be processed.
    // Also updates end_of_frame flag when the processing of all blocks is complete.
    private static void update_next_job_info(AV1CdefSync cdef_sync, int nvfb, int nhfb)
    {
        cdef_sync.fbc++;
        if (cdef_sync.fbc == nhfb)
        {
            cdef_sync.fbr++;
            if (cdef_sync.fbr == nvfb)
            {
                cdef_sync.end_of_frame = 1;
            }
            else
            {
                cdef_sync.fbc = 0;
            }
        }
    }

    // Initializes cdef_sync parameters.
    private static void cdef_reset_job_info(AV1CdefSync cdef_sync)
    {
        if (cdef_sync.mutex_ != null) pthread_mutex_init(ref cdef_sync.mutex_, null);
        cdef_sync.end_of_frame = 0;
        cdef_sync.fbr = 0;
        cdef_sync.fbc = 0;
        cdef_sync.cdef_mt_exit = false;
    }

    // Checks if a job is available. If job is available,
    // populates next job information and returns 1, else returns 0.
    private static int cdef_get_next_job(AV1CdefSync cdef_sync,
                                         CdefSearchCtx cdef_search_ctx,
                                         ref int cur_fbr,
                                         ref int cur_fbc,
                                         ref int sb_count)
    {
        pthread_mutex_lock(cdef_sync.mutex_);
        int do_next_block = 0;
        int nvfb = cdef_search_ctx.nvfb;
        int nhfb = cdef_search_ctx.nhfb;

        // If a block is skip, do not process the block and
        // check the skip condition for the next block.
        while (!cdef_sync.cdef_mt_exit && cdef_sync.end_of_frame == 0 &&
               cdef_sb_skip(cdef_search_ctx.mi_params, cdef_sync.fbr,
                            cdef_sync.fbc) != 0)
        {
            update_next_job_info(cdef_sync, nvfb, nhfb);
        }

        // Populates information needed for current job and update the row,
        // column indices of the next block to be processed.
        if (!cdef_sync.cdef_mt_exit && cdef_sync.end_of_frame == 0)
        {
            do_next_block = 1;
            cur_fbr = cdef_sync.fbr;
            cur_fbc = cdef_sync.fbc;
            sb_count = cdef_search_ctx.sb_count;
            cdef_search_ctx.sb_count++;
            update_next_job_info(cdef_sync, nvfb, nhfb);
        }
        pthread_mutex_unlock(cdef_sync.mutex_);
        return do_next_block;
    }

    private static readonly AVxWorkerHook cdef_filter_block_worker_hook_fn =
        cdef_filter_block_worker_hook;

    // Hook function for each thread in CDEF search multi-threading.
    private static int cdef_filter_block_worker_hook(object arg1, object arg2)
    {
        EncWorkerData thread_data = (EncWorkerData)arg1;
        AV1CdefSync cdef_sync = (AV1CdefSync)arg2;

        pthread_mutex_t cdef_mutex_ = cdef_sync.mutex_;
        aom_internal_error_info error_info = thread_data.error_info;
        CdefSearchCtx cdef_search_ctx = thread_data.cpi.cdef_search_ctx;

        // The jmp_buf is valid only for the duration of the function that calls
        // setjmp(). Therefore, this function must reset the 'setjmp' field to 0
        // before it returns.
        error_info.setjmp = 1;
        try
        {
            int cur_fbr = 0, cur_fbc = 0, sb_count = 0;
            while (cdef_get_next_job(cdef_sync, cdef_search_ctx, ref cur_fbr, ref cur_fbc,
                                     ref sb_count) != 0)
            {
                av1_cdef_mse_calc_block(cdef_search_ctx, error_info, cur_fbr, cur_fbc,
                                        sb_count,
                                        thread_data.cpi.sf.lpf_sf.adaptive_cdef_mode);
            }
            error_info.setjmp = 0;
            return 1;
        }
        catch (AomException e)
        {
            record_error_info(error_info, e);
            error_info.setjmp = 0;
            pthread_mutex_lock(cdef_mutex_);
            cdef_sync.cdef_mt_exit = true;
            pthread_mutex_unlock(cdef_mutex_);
            return 0;
        }
    }

    // Assigns CDEF search hook function and thread data to each worker.
    private static void prepare_cdef_workers(AV1_COMP cpi, AVxWorkerHook hook,
                                             int num_workers)
    {
        MultiThreadInfo mt_info = cpi.mt_info;
        for (int i = num_workers - 1; i >= 0; i--)
        {
            AVxWorker worker = mt_info.workers[i];
            EncWorkerData thread_data = mt_info.tile_thr_data[i];

            thread_data.cpi = cpi;
            worker.hook = hook;
            worker.data1 = thread_data;
            worker.data2 = mt_info.cdef_sync;
        }
    }

    // Implements multi-threading for CDEF search.
    internal static void av1_cdef_mse_calc_frame_mt(AV1_COMP cpi)
    {
        MultiThreadInfo mt_info = cpi.mt_info;
        AV1CdefSync cdef_sync = mt_info.cdef_sync;
        int num_workers = mt_info.num_mod_workers[MOD_CDEF_SEARCH];

        cdef_reset_job_info(cdef_sync);
        prepare_cdef_workers(cpi, cdef_filter_block_worker_hook_fn, num_workers);
        launch_workers(mt_info, num_workers);
        sync_enc_workers(mt_info, cpi.common, num_workers);
    }

    // Computes num_workers for temporal filter multi-threading.
    private static int compute_num_tf_workers(AV1_COMP cpi)
    {
        // For single-pass encode, using no. of workers as per tf block size was not
        // found to improve speed. Hence the thread assignment for single-pass encode
        // is kept based on compute_num_enc_workers().
        if (cpi.oxcf.pass < AOM_RC_SECOND_PASS)
            return compute_num_enc_workers(cpi, cpi.oxcf.max_threads);

        // [subset] the two-pass computation (tf block rows): one pass only.
        throw new NotSupportedException("two-pass encoding is not in the subset");
    }

    // Computes num_workers for tpl multi-threading.
    private static int compute_num_tpl_workers(AV1_COMP cpi)
    {
        return compute_num_enc_workers(cpi, cpi.oxcf.max_threads);
    }

    // Computes num_workers for loop filter multi-threading.
    private static int compute_num_lf_workers(AV1_COMP cpi)
    {
        return compute_num_enc_workers(cpi, cpi.oxcf.max_threads);
    }

    // Computes num_workers for cdef multi-threading.
    private static int compute_num_cdef_workers(AV1_COMP cpi)
    {
        return compute_num_enc_workers(cpi, cpi.oxcf.max_threads);
    }

    // Computes num_workers for loop-restoration multi-threading.
    private static int compute_num_lr_workers(AV1_COMP cpi)
    {
        return compute_num_enc_workers(cpi, cpi.oxcf.max_threads);
    }

    // Computes num_workers for pack bitstream multi-threading.
    private static int compute_num_pack_bs_workers(AV1_COMP cpi)
    {
        if (cpi.oxcf.max_threads <= 1) return 1;
        return compute_num_enc_tile_mt_workers(cpi.common, cpi.oxcf.max_threads);
    }

    // Computes num_workers for all intra multi-threading.
    private static int compute_num_ai_workers(AV1_COMP cpi)
    {
        if (cpi.oxcf.max_threads <= 1) return 1;
        // The multi-threading implementation of deltaq-mode = 3 in allintra
        // mode is based on row multi threading.
        if (!cpi.oxcf.row_mt) return 1;
        cpi.weber_bsize = BLOCK_8X8;
        BLOCK_SIZE bsize = cpi.weber_bsize;
        int mb_step = mi_size_wide[bsize];
        int num_mb_rows = cpi.common.mi_params.mi_rows / mb_step;
        return AOMMIN(num_mb_rows, cpi.oxcf.max_threads);
    }

    private static int compute_num_mod_workers(AV1_COMP cpi,
                                               MULTI_THREADED_MODULES mod_name)
    {
        int num_mod_workers = 0;
        switch (mod_name)
        {
            case MOD_FP:
                if (cpi.oxcf.pass >= AOM_RC_SECOND_PASS)
                    num_mod_workers = 0;
                else
                    num_mod_workers = compute_num_enc_workers(cpi, cpi.oxcf.max_threads);
                break;
            case MOD_TF: num_mod_workers = compute_num_tf_workers(cpi); break;
            case MOD_TPL: num_mod_workers = compute_num_tpl_workers(cpi); break;
            case MOD_GME: num_mod_workers = 1; break;
            case MOD_ENC:
                num_mod_workers = compute_num_enc_workers(cpi, cpi.oxcf.max_threads);
                break;
            case MOD_LPF: num_mod_workers = compute_num_lf_workers(cpi); break;
            case MOD_CDEF_SEARCH:
                num_mod_workers = compute_num_cdef_workers(cpi);
                break;
            case MOD_CDEF: num_mod_workers = compute_num_cdef_workers(cpi); break;
            case MOD_LR: num_mod_workers = compute_num_lr_workers(cpi); break;
            case MOD_PACK_BS: num_mod_workers = compute_num_pack_bs_workers(cpi); break;
            case MOD_FRAME_ENC:
                num_mod_workers = cpi.ppi.p_mt_info.num_mod_workers[MOD_FRAME_ENC];
                break;
            case MOD_AI:
                if (cpi.oxcf.pass == AOM_RC_ONE_PASS)
                {
                    num_mod_workers = compute_num_ai_workers(cpi);
                }
                else
                {
                    num_mod_workers = 0;
                }
                break;
            default: Debug.Assert(false); break;
        }
        return (num_mod_workers);
    }

    // Computes the number of workers for each MT modules in the encoder
    internal static void av1_compute_num_workers_for_mt(AV1_COMP cpi)
    {
        for (int i = MOD_FP; i < NUM_MT_MODULES; i++)
        {
            cpi.ppi.p_mt_info.num_mod_workers[i] =
                compute_num_mod_workers(cpi, (MULTI_THREADED_MODULES)i);
        }
    }
}
