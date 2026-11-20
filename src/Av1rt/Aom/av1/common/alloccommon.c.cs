// Port of libaom av1/common/alloccommon.c

using System.Diagnostics;

namespace Av1rt.Aom;

// [port] Allocation follows PORTING 4: aom_malloc/aom_calloc/aom_memalign become `new T[n]` with
// element counts (the C byte sizes are kept where libaom stores them, e.g. CdefInfo's
// allocated_*_size, and divided by the element size at the allocation), aom_free becomes a null
// assignment. A calloc'ed array of C structs that are classes in the port (MB_MODE_INFO,
// AV1CdefWorkerData) gets every element constructed, which is what the zeroed C memory is.
// CHECK_MEM_ERROR never fires: a managed allocation either succeeds or throws.
// [subset] Not ported (never executed in the subset): av1_alloc_restoration_buffers and
// av1_free_restoration_buffers (loop restoration is off; they exist in this build only for the
// decoder), av1_init_mi_buffers (decoder only).
internal static partial class alloccommon
{
    internal static int av1_get_MBs(int width, int height)
    {
        int aligned_width = ALIGN_POWER_OF_TWO(width, 3);
        int aligned_height = ALIGN_POWER_OF_TWO(height, 3);
        int mi_cols = aligned_width >> MI_SIZE_LOG2;
        int mi_rows = aligned_height >> MI_SIZE_LOG2;

        int mb_cols = ROUND_POWER_OF_TWO(mi_cols, 2);
        int mb_rows = ROUND_POWER_OF_TWO(mi_rows, 2);
        return mb_rows * mb_cols;
    }

    internal static void av1_free_ref_frame_buffers(BufferPool pool)
    {
        int i;

        for (i = 0; i < pool.num_frame_bufs; ++i)
        {
            if (pool.frame_bufs[i].ref_count > 0 &&
                !pool.frame_bufs[i].raw_frame_buffer.data.IsNull)
            {
                pool.release_fb_cb(pool.cb_priv, pool.frame_bufs[i].raw_frame_buffer);
                pool.frame_bufs[i].raw_frame_buffer.data = Ptr<byte>.Null;
                pool.frame_bufs[i].raw_frame_buffer.size = 0;
                pool.frame_bufs[i].raw_frame_buffer.priv = null;
                pool.frame_bufs[i].ref_count = 0;
            }
            // aom_free(pool->frame_bufs[i].mvs);
            pool.frame_bufs[i].mvs = Ptr<MV_REF>.Null;
            // aom_free(pool->frame_bufs[i].seg_map);
            pool.frame_bufs[i].seg_map = Ptr<byte>.Null;
            aom_free_frame_buffer(pool.frame_bufs[i].buf);
        }
        // aom_free(pool->frame_bufs);
        pool.frame_bufs = null;
        pool.num_frame_bufs = 0;
    }

    private static void free_cdef_linebuf_conditional(
        AV1_COMMON cm, Ptr<int> new_linebuf_size)
    {
        CdefInfo cdef_info = cm.cdef_info;
        for (int plane = 0; plane < MAX_MB_PLANE; plane++)
        {
            if (new_linebuf_size[plane] != cdef_info.allocated_linebuf_size[plane])
            {
                // aom_free(cdef_info->linebuf[plane]);
                cdef_info.linebuf[plane] = Ptr<ushort>.Null;
            }
        }
    }

    private static void free_cdef_bufs_conditional(AV1_COMMON cm,
                                                   Ptr<Ptr<ushort>> colbuf,
                                                   ref Ptr<ushort> srcbuf,
                                                   Ptr<int> new_colbuf_size,
                                                   int new_srcbuf_size)
    {
        CdefInfo cdef_info = cm.cdef_info;
        if (new_srcbuf_size != cdef_info.allocated_srcbuf_size)
        {
            // aom_free(*srcbuf);
            srcbuf = Ptr<ushort>.Null;
        }
        for (int plane = 0; plane < MAX_MB_PLANE; plane++)
        {
            if (new_colbuf_size[plane] != cdef_info.allocated_colbuf_size[plane])
            {
                // aom_free(colbuf[plane]);
                colbuf[plane] = Ptr<ushort>.Null;
            }
        }
    }

    private static void free_cdef_bufs(Ptr<Ptr<ushort>> colbuf, ref Ptr<ushort> srcbuf)
    {
        // aom_free(*srcbuf);
        srcbuf = Ptr<ushort>.Null;
        for (int plane = 0; plane < MAX_MB_PLANE; plane++)
        {
            // aom_free(colbuf[plane]);
            colbuf[plane] = Ptr<ushort>.Null;
        }
    }

    private static void free_cdef_row_sync(ref Ptr<AV1CdefRowSync> cdef_row_mt,
                                           int num_mi_rows)
    {
        if (cdef_row_mt.IsNull) return;
        // CONFIG_MULTITHREAD
        for (int row_idx = 0; row_idx < num_mi_rows; row_idx++)
        {
            if (cdef_row_mt[row_idx].row_mutex_ != null)
            {
                pthread_mutex_destroy(cdef_row_mt[row_idx].row_mutex_);
                // aom_free((*cdef_row_mt)[row_idx].row_mutex_);
            }
            if (cdef_row_mt[row_idx].row_cond_ != null)
            {
                pthread_cond_destroy(cdef_row_mt[row_idx].row_cond_);
                // aom_free((*cdef_row_mt)[row_idx].row_cond_);
            }
        }
        // aom_free(*cdef_row_mt);
        cdef_row_mt = Ptr<AV1CdefRowSync>.Null;
    }

    // [port] AV1CdefWorkerData **cdef_worker: the callee frees and nulls the array.
    internal static void av1_free_cdef_buffers(AV1_COMMON cm,
                                               ref AV1CdefWorkerData[] cdef_worker,
                                               AV1CdefSync cdef_sync)
    {
        CdefInfo cdef_info = cm.cdef_info;
        int num_mi_rows = cdef_info.allocated_mi_rows;

        for (int plane = 0; plane < MAX_MB_PLANE; plane++)
        {
            // aom_free(cdef_info->linebuf[plane]);
            cdef_info.linebuf[plane] = Ptr<ushort>.Null;
        }
        // De-allocation of column buffer & source buffer (worker_0).
        free_cdef_bufs(cdef_info.colbuf, ref cdef_info.srcbuf);

        free_cdef_row_sync(ref cdef_sync.cdef_row_mt, num_mi_rows);

        if (cdef_info.allocated_num_workers < 2) return;
        if (cdef_worker != null)
        {
            for (int idx = cdef_info.allocated_num_workers - 1; idx >= 1; idx--)
            {
                // De-allocation of column buffer & source buffer for remaining workers.
                free_cdef_bufs(cdef_worker[idx].colbuf, ref cdef_worker[idx].srcbuf);
            }
            // aom_free(*cdef_worker);
            cdef_worker = null;
        }
    }

    private static void alloc_cdef_linebuf(AV1_COMMON cm, Ptr<Ptr<ushort>> linebuf,
                                           int num_planes)
    {
        CdefInfo cdef_info = cm.cdef_info;
        for (int plane = 0; plane < num_planes; plane++)
        {
            if (linebuf[plane].IsNull)
                // CHECK_MEM_ERROR(cm, linebuf[plane],
                //                 aom_malloc(cdef_info->allocated_linebuf_size[plane]));
                linebuf[plane] = new ushort[cdef_info.allocated_linebuf_size[plane] / sizeof(ushort)];
        }
    }

    private static void alloc_cdef_bufs(AV1_COMMON cm, Ptr<Ptr<ushort>> colbuf,
                                        ref Ptr<ushort> srcbuf, int num_planes)
    {
        CdefInfo cdef_info = cm.cdef_info;
        if (srcbuf.IsNull)
            // CHECK_MEM_ERROR(cm, *srcbuf,
            //                 aom_memalign(16, cdef_info->allocated_srcbuf_size));
            srcbuf = new ushort[cdef_info.allocated_srcbuf_size / sizeof(ushort)];

        for (int plane = 0; plane < num_planes; plane++)
        {
            if (colbuf[plane].IsNull)
                // CHECK_MEM_ERROR(cm, colbuf[plane],
                //                 aom_malloc(cdef_info->allocated_colbuf_size[plane]));
                colbuf[plane] = new ushort[cdef_info.allocated_colbuf_size[plane] / sizeof(ushort)];
        }
    }

    private static void alloc_cdef_row_sync(AV1_COMMON cm,
                                            ref Ptr<AV1CdefRowSync> cdef_row_mt,
                                            int num_mi_rows)
    {
        if (!cdef_row_mt.IsNull) return;

        // CHECK_MEM_ERROR(cm, *cdef_row_mt,
        //                 aom_calloc(num_mi_rows, sizeof(**cdef_row_mt)));
        cdef_row_mt = new AV1CdefRowSync[num_mi_rows];
        // CONFIG_MULTITHREAD
        for (int row_idx = 0; row_idx < num_mi_rows; row_idx++)
        {
            // CHECK_MEM_ERROR(cm, (*cdef_row_mt)[row_idx].row_mutex_,
            //                 aom_malloc(sizeof(*(*cdef_row_mt)[row_idx].row_mutex_)));
            pthread_mutex_init(ref cdef_row_mt[row_idx].row_mutex_, null);

            // CHECK_MEM_ERROR(cm, (*cdef_row_mt)[row_idx].row_cond_,
            //                 aom_malloc(sizeof(*(*cdef_row_mt)[row_idx].row_cond_)));
            pthread_cond_init(ref cdef_row_mt[row_idx].row_cond_, null);
        }
    }

    // PORT-NOTE: libaom sizes the CDEF line buffers with sizeof(*cdef_info->linebuf), which is
    // sizeof(uint16_t *) (8 on the 64-bit oracle build), not sizeof(uint16_t). The port keeps
    // the same byte count, so the buffers have as many elements as libaom's do.
    private const int sizeof_linebuf_elem = 8;

    // [port] AV1CdefWorkerData **cdef_worker: the callee may (re)allocate or free the array.
    internal static void av1_alloc_cdef_buffers(AV1_COMMON cm,
                                                ref AV1CdefWorkerData[] cdef_worker,
                                                AV1CdefSync cdef_sync, int num_workers,
                                                int init_worker)
    {
        int num_planes = av1_num_planes(cm);
        int[] new_linebuf_size = new int[MAX_MB_PLANE];  // size_t [MAX_MB_PLANE] = { 0 }
        int[] new_colbuf_size = new int[MAX_MB_PLANE];   // size_t [MAX_MB_PLANE] = { 0 }
        int new_srcbuf_size = 0;
        CdefInfo cdef_info = cm.cdef_info;
        // Check for configuration change
        int num_mi_rows =
            (cm.mi_params.mi_rows + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;
        int is_num_workers_changed =
            B(cdef_info.allocated_num_workers != num_workers);
        int is_cdef_enabled =
            B(cm.seq_params.enable_cdef != 0 && cm.tiles.single_tile_decoding == 0);

        // num-bufs=3 represents ping-pong buffers for top linebuf,
        // followed by bottom linebuf.
        // ping-pong is to avoid top linebuf over-write by consecutive row.
        int num_bufs = 3;
        if (num_workers > 1)
            num_bufs = (cm.mi_params.mi_rows + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;

        if (is_cdef_enabled != 0)
        {
            // Calculate src buffer size
            new_srcbuf_size = sizeof(ushort) * CDEF_INBUF_SIZE;
            for (int plane = 0; plane < num_planes; plane++)
            {
                int shift =
                    plane == AOM_PLANE_Y ? 0 : cm.seq_params.subsampling_x;
                // Calculate top and bottom line buffer size
                int luma_stride =
                    ALIGN_POWER_OF_TWO(cm.mi_params.mi_cols << MI_SIZE_LOG2, 4);
                new_linebuf_size[plane] = sizeof_linebuf_elem * num_bufs *
                                          (CDEF_VBORDER << 1) * (luma_stride >> shift);
                // Calculate column buffer size
                int block_height =
                    (CDEF_BLOCKSIZE << (MI_SIZE_LOG2 - shift)) * 2 * CDEF_VBORDER;
                new_colbuf_size[plane] =
                    sizeof(ushort) * block_height * CDEF_HBORDER;
            }
        }

        // Free src, line and column buffers for worker 0 in case of reallocation
        free_cdef_linebuf_conditional(cm, new_linebuf_size);
        free_cdef_bufs_conditional(cm, cdef_info.colbuf, ref cdef_info.srcbuf,
                                   new_colbuf_size, new_srcbuf_size);

        // The flag init_worker indicates if cdef_worker has to be allocated for the
        // frame. This is passed as 1 always from decoder. At encoder side, it is 0
        // when called for parallel frames during FPMT (where cdef_worker is shared
        // across parallel frames) and 1 otherwise.
        if (cdef_worker != null && init_worker != 0)
        {
            if (is_num_workers_changed != 0)
            {
                // Free src and column buffers for remaining workers in case of change in
                // num_workers
                for (int idx = cdef_info.allocated_num_workers - 1; idx >= 1; idx--)
                    free_cdef_bufs(cdef_worker[idx].colbuf, ref cdef_worker[idx].srcbuf);

                // aom_free(*cdef_worker);
                cdef_worker = null;
            }
            else if (num_workers > 1)
            {
                // Free src and column buffers for remaining workers in case of
                // reallocation
                for (int idx = num_workers - 1; idx >= 1; idx--)
                    free_cdef_bufs_conditional(cm, cdef_worker[idx].colbuf,
                                               ref cdef_worker[idx].srcbuf, new_colbuf_size,
                                               new_srcbuf_size);
            }
        }

        if (cdef_info.allocated_mi_rows != num_mi_rows)
            free_cdef_row_sync(ref cdef_sync.cdef_row_mt, cdef_info.allocated_mi_rows);

        // Store allocated sizes for reallocation
        cdef_info.allocated_srcbuf_size = new_srcbuf_size;
        av1_copy(cdef_info.allocated_colbuf_size, new_colbuf_size);
        av1_copy(cdef_info.allocated_linebuf_size, new_linebuf_size);
        // Store configuration to check change in configuration
        cdef_info.allocated_mi_rows = num_mi_rows;
        cdef_info.allocated_num_workers = num_workers;

        if (is_cdef_enabled == 0) return;

        // Memory allocation of column buffer & source buffer (worker_0).
        alloc_cdef_bufs(cm, cdef_info.colbuf, ref cdef_info.srcbuf, num_planes);
        alloc_cdef_linebuf(cm, cdef_info.linebuf, num_planes);

        if (num_workers < 2) return;

        if (init_worker != 0)
        {
            if (cdef_worker == null)
            {
                // CHECK_MEM_ERROR(cm, *cdef_worker,
                //                 aom_calloc(num_workers, sizeof(**cdef_worker)));
                cdef_worker = new AV1CdefWorkerData[num_workers];
                for (int i = 0; i < num_workers; i++) cdef_worker[i] = new AV1CdefWorkerData();
            }

            // Memory allocation of column buffer & source buffer for remaining workers.
            for (int idx = num_workers - 1; idx >= 1; idx--)
                alloc_cdef_bufs(cm, cdef_worker[idx].colbuf,
                                ref cdef_worker[idx].srcbuf, num_planes);
        }

        alloc_cdef_row_sync(cm, ref cdef_sync.cdef_row_mt,
                            cdef_info.allocated_mi_rows);
    }

    internal static void av1_free_above_context_buffers(CommonContexts above_contexts)
    {
        int i;
        int num_planes = above_contexts.num_planes;

        for (int tile_row = 0; tile_row < above_contexts.num_tile_rows; tile_row++)
        {
            for (i = 0; i < num_planes; i++)
            {
                if (above_contexts.entropy[i] == null) break;
                // aom_free(above_contexts->entropy[i][tile_row]);
                above_contexts.entropy[i][tile_row] = Ptr<ENTROPY_CONTEXT>.Null;
            }
            if (above_contexts.partition != null)
            {
                // aom_free(above_contexts->partition[tile_row]);
                above_contexts.partition[tile_row] = Ptr<PARTITION_CONTEXT>.Null;
            }

            if (above_contexts.txfm != null)
            {
                // aom_free(above_contexts->txfm[tile_row]);
                above_contexts.txfm[tile_row] = Ptr<TXFM_CONTEXT>.Null;
            }
        }
        for (i = 0; i < num_planes; i++)
        {
            // aom_free(above_contexts->entropy[i]);
            above_contexts.entropy[i] = null;
        }
        // aom_free(above_contexts->partition);
        above_contexts.partition = null;

        // aom_free(above_contexts->txfm);
        above_contexts.txfm = null;

        above_contexts.num_tile_rows = 0;
        above_contexts.num_mi_cols = 0;
        above_contexts.num_planes = 0;
    }

    internal static void av1_free_context_buffers(AV1_COMMON cm)
    {
        if (cm.mi_params.free_mi != null) cm.mi_params.free_mi(cm.mi_params);

        av1_free_above_context_buffers(cm.above_contexts);
    }

    internal static int av1_alloc_above_context_buffers(CommonContexts above_contexts,
                                                        int num_tile_rows, int num_mi_cols,
                                                        int num_planes)
    {
        int aligned_mi_cols =
            ALIGN_POWER_OF_TWO(num_mi_cols, MAX_MIB_SIZE_LOG2);

        // Allocate above context buffers
        above_contexts.num_tile_rows = num_tile_rows;
        above_contexts.num_mi_cols = aligned_mi_cols;
        above_contexts.num_planes = num_planes;
        for (int plane_idx = 0; plane_idx < num_planes; plane_idx++)
        {
            above_contexts.entropy[plane_idx] = new Ptr<ENTROPY_CONTEXT>[num_tile_rows];
            if (above_contexts.entropy[plane_idx] == null) return 1;
        }

        above_contexts.partition = new Ptr<PARTITION_CONTEXT>[num_tile_rows];
        if (above_contexts.partition == null) return 1;

        above_contexts.txfm = new Ptr<TXFM_CONTEXT>[num_tile_rows];
        if (above_contexts.txfm == null) return 1;

        for (int tile_row = 0; tile_row < num_tile_rows; tile_row++)
        {
            for (int plane_idx = 0; plane_idx < num_planes; plane_idx++)
            {
                above_contexts.entropy[plane_idx][tile_row] = new ENTROPY_CONTEXT[aligned_mi_cols];
                if (above_contexts.entropy[plane_idx][tile_row].IsNull) return 1;
            }

            above_contexts.partition[tile_row] = new PARTITION_CONTEXT[aligned_mi_cols];
            if (above_contexts.partition[tile_row].IsNull) return 1;

            above_contexts.txfm[tile_row] = new TXFM_CONTEXT[aligned_mi_cols];
            if (above_contexts.txfm[tile_row].IsNull) return 1;
        }

        return 0;
    }

    // Allocate the dynamically allocated arrays in 'mi_params' assuming
    // 'mi_params->set_mb_mi()' was already called earlier to initialize the rest of
    // the struct members.
    private static int alloc_mi(CommonModeInfoParams mi_params)
    {
        int aligned_mi_rows = calc_mi_size(mi_params.mi_rows);
        int mi_grid_size = mi_params.mi_stride * aligned_mi_rows;
        int alloc_size_1d = mi_size_wide[mi_params.mi_alloc_bsize];
        int alloc_mi_size =
            mi_params.mi_alloc_stride * (aligned_mi_rows / alloc_size_1d);

        if (mi_params.mi_alloc_size < alloc_mi_size ||
            mi_params.mi_grid_size < mi_grid_size)
        {
            mi_params.free_mi(mi_params);

            // aom_calloc(alloc_mi_size, sizeof(*mi_params->mi_alloc)): zeroed MB_MODE_INFO
            // structs, so every element is constructed.
            mi_params.mi_alloc = new MB_MODE_INFO[alloc_mi_size];
            for (int i = 0; i < alloc_mi_size; i++) mi_params.mi_alloc[i] = new MB_MODE_INFO();
            mi_params.mi_alloc_size = alloc_mi_size;

            // aom_calloc of MB_MODE_INFO *: null references.
            mi_params.mi_grid_base = new MB_MODE_INFO[mi_grid_size];

            mi_params.tx_type_map = new byte[mi_grid_size];
            mi_params.mi_grid_size = mi_grid_size;
        }

        return 0;
    }

    internal static int av1_alloc_context_buffers(AV1_COMMON cm, int width, int height,
                                                  BLOCK_SIZE min_partition_size)
    {
        CommonModeInfoParams mi_params = cm.mi_params;
        mi_params.set_mb_mi(mi_params, width, height, min_partition_size);
        if (alloc_mi(mi_params) != 0) goto fail;
        return 0;

    fail:
        // clear the mi_* values to force a realloc on resync
        mi_params.set_mb_mi(mi_params, 0, 0, BLOCK_4X4);
        av1_free_context_buffers(cm);
        return 1;
    }

    internal static void av1_remove_common(AV1_COMMON cm)
    {
        av1_free_context_buffers(cm);

        // aom_free(cm->fc);
        cm.fc = null;
        // aom_free(cm->default_frame_context);
        cm.default_frame_context = null;
    }
}
