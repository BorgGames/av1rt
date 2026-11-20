/* Shim for work package f4 (frame): exports libaom's ethread.c multi-threading helpers so
 * tests/Av1rt.Tests/Frame can compare the C# port against them (docs/TESTING.md).
 *
 * The job scheduling helpers of ethread.c are file-static and their only public caller,
 * av1_encode_tiles_row_mt(), encodes a whole frame. The section "verbatim copies" below therefore
 * carries byte-for-byte copies of those static functions (CONFIG_REALTIME_ONLY == 1 as in the
 * oracle build); the tests drive them with the same deterministic operation sequences as the
 * C# port. Everything else calls the libaom functions linked from aom.lib. */
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>

#include "config/aom_config.h"

#include "aom_util/aom_pthread.h"
#include "av1/common/tile_common.h"
#include "av1/encoder/bitstream.h"
#include "av1/encoder/encoder.h"
#include "av1/encoder/ethread.h"

#define EXPORT __declspec(dllexport)

#if !CONFIG_REALTIME_ONLY || !CONFIG_MULTITHREAD
#error "shim_f4 expects the CONFIG_REALTIME_ONLY=1, CONFIG_MULTITHREAD=1 oracle build"
#endif

/* ---- verbatim copies of file-static functions of av1/encoder/ethread.c ------------------- */

static inline void assign_tile_to_thread(int *thread_id_to_tile_id,
                                         int num_tiles, int num_workers) {
  int tile_id = 0;
  int i;

  for (i = 0; i < num_workers; i++) {
    thread_id_to_tile_id[i] = tile_id++;
    if (tile_id == num_tiles) tile_id = 0;
  }
}

static inline int get_next_job(TileDataEnc *const tile_data,
                               int *current_mi_row, int mib_size) {
  AV1EncRowMultiThreadSync *const row_mt_sync = &tile_data->row_mt_sync;
  const int mi_row_end = tile_data->tile_info.mi_row_end;

  if (row_mt_sync->next_mi_row < mi_row_end) {
    *current_mi_row = row_mt_sync->next_mi_row;
    row_mt_sync->num_threads_working++;
    row_mt_sync->next_mi_row += mib_size;
    return 1;
  }
  return 0;
}

static inline void switch_tile_and_get_next_job(
    AV1_COMMON *const cm, TileDataEnc *const tile_data, int *cur_tile_id,
    int *current_mi_row, int *end_of_frame, int is_firstpass,
    const BLOCK_SIZE fp_block_size) {
  const int tile_cols = cm->tiles.cols;
  const int tile_rows = cm->tiles.rows;

  int tile_id = -1;  // Stores the tile ID with minimum proc done
  int max_mis_to_encode = 0;
  int min_num_threads_working = INT_MAX;

  for (int tile_row = 0; tile_row < tile_rows; tile_row++) {
    for (int tile_col = 0; tile_col < tile_cols; tile_col++) {
      int tile_index = tile_row * tile_cols + tile_col;
      TileDataEnc *const this_tile = &tile_data[tile_index];
      AV1EncRowMultiThreadSync *const row_mt_sync = &this_tile->row_mt_sync;

#if CONFIG_REALTIME_ONLY
      int num_b_rows_in_tile =
          av1_get_sb_rows_in_tile(cm, &this_tile->tile_info);
      int num_b_cols_in_tile =
          av1_get_sb_cols_in_tile(cm, &this_tile->tile_info);
#else
      int num_b_rows_in_tile =
          is_firstpass
              ? av1_get_unit_rows_in_tile(&this_tile->tile_info, fp_block_size)
              : av1_get_sb_rows_in_tile(cm, &this_tile->tile_info);
      int num_b_cols_in_tile =
          is_firstpass
              ? av1_get_unit_cols_in_tile(&this_tile->tile_info, fp_block_size)
              : av1_get_sb_cols_in_tile(cm, &this_tile->tile_info);
#endif
      int theoretical_limit_on_threads =
          AOMMIN((num_b_cols_in_tile + 1) >> 1, num_b_rows_in_tile);
      int num_threads_working = row_mt_sync->num_threads_working;

      if (num_threads_working < theoretical_limit_on_threads) {
        int num_mis_to_encode =
            this_tile->tile_info.mi_row_end - row_mt_sync->next_mi_row;

        // Tile to be processed by this thread is selected on the basis of
        // availability of jobs:
        // 1) If jobs are available, tile to be processed is chosen on the
        // basis of minimum number of threads working for that tile. If two or
        // more tiles have same number of threads working for them, then the
        // tile with maximum number of jobs available will be chosen.
        // 2) If no jobs are available, then end_of_frame is reached.
        if (num_mis_to_encode > 0) {
          if (num_threads_working < min_num_threads_working) {
            min_num_threads_working = num_threads_working;
            max_mis_to_encode = 0;
          }
          if (num_threads_working == min_num_threads_working &&
              num_mis_to_encode > max_mis_to_encode) {
            tile_id = tile_index;
            max_mis_to_encode = num_mis_to_encode;
          }
        }
      }
    }
  }
  if (tile_id == -1) {
    *end_of_frame = 1;
  } else {
    // Update the current tile id to the tile id that will be processed next,
    // which will be the least processed tile.
    *cur_tile_id = tile_id;
    const int unit_height = mi_size_high[fp_block_size];
    get_next_job(&tile_data[tile_id], current_mi_row,
                 is_firstpass ? unit_height : cm->seq_params->mib_size);
  }
}

static inline void compute_max_sb_rows_cols(const AV1_COMMON *cm,
                                            int *max_sb_rows_in_tile,
                                            int *max_sb_cols_in_tile) {
  const int tile_rows = cm->tiles.rows;
  const int mib_size_log2 = cm->seq_params->mib_size_log2;
  const int num_mi_rows = cm->mi_params.mi_rows;
  const int *const row_start_sb = cm->tiles.row_start_sb;
  for (int row = 0; row < tile_rows; row++) {
    const int mi_row_start = row_start_sb[row] << mib_size_log2;
    const int mi_row_end =
        AOMMIN(row_start_sb[row + 1] << mib_size_log2, num_mi_rows);
    const int num_sb_rows_in_tile =
        CEIL_POWER_OF_TWO(mi_row_end - mi_row_start, mib_size_log2);
    *max_sb_rows_in_tile = AOMMAX(*max_sb_rows_in_tile, num_sb_rows_in_tile);
  }

  const int tile_cols = cm->tiles.cols;
  const int num_mi_cols = cm->mi_params.mi_cols;
  const int *const col_start_sb = cm->tiles.col_start_sb;
  for (int col = 0; col < tile_cols; col++) {
    const int mi_col_start = col_start_sb[col] << mib_size_log2;
    const int mi_col_end =
        AOMMIN(col_start_sb[col + 1] << mib_size_log2, num_mi_cols);
    const int num_sb_cols_in_tile =
        CEIL_POWER_OF_TWO(mi_col_end - mi_col_start, mib_size_log2);
    *max_sb_cols_in_tile = AOMMAX(*max_sb_cols_in_tile, num_sb_cols_in_tile);
  }
}

// Compare and order tiles based on absolute sum of tx coeffs.
static int compare_tile_order(const void *a, const void *b) {
  const PackBSTileOrder *const tile_a = (const PackBSTileOrder *)a;
  const PackBSTileOrder *const tile_b = (const PackBSTileOrder *)b;

  if (tile_a->abs_sum_level > tile_b->abs_sum_level)
    return -1;
  else if (tile_a->abs_sum_level == tile_b->abs_sum_level)
    return (tile_a->tile_idx > tile_b->tile_idx ? 1 : -1);
  else
    return 1;
}

/* ---- frame setup ----------------------------------------------------------------------- */

EXPORT int aomshim_f4_num_mt_modules(void) { return NUM_MT_MODULES; }

EXPORT int aomshim_f4_max_num_threads(void) { return MAX_NUM_THREADS; }

/* The fields the ethread.c helpers read: the superblock size, the mi grid size and an explicit
 * tile layout (tile_cols + 1 column starts and tile_rows + 1 row starts in superblocks). */
static AV1_COMP *create_cpi(int mi_rows, int mi_cols, int mib_size_log2, int tile_cols,
                            int tile_rows, const int *col_start_sb, const int *row_start_sb) {
  AV1_PRIMARY *ppi = (AV1_PRIMARY *)calloc(1, sizeof(*ppi));
  AV1_COMP *cpi = (AV1_COMP *)calloc(1, sizeof(*cpi));
  if (!ppi || !cpi) abort();
  cpi->ppi = ppi;
  AV1_COMMON *cm = &cpi->common;
  cm->seq_params = &ppi->seq_params;
  cm->seq_params->mib_size_log2 = mib_size_log2;
  cm->seq_params->mib_size = 1 << mib_size_log2;
  cm->seq_params->sb_size = mib_size_log2 == 5 ? BLOCK_128X128 : BLOCK_64X64;
  cm->mi_params.mi_rows = mi_rows;
  cm->mi_params.mi_cols = mi_cols;
  cm->tiles.cols = tile_cols;
  cm->tiles.rows = tile_rows;
  memcpy(cm->tiles.col_start_sb, col_start_sb, sizeof(int) * (tile_cols + 1));
  memcpy(cm->tiles.row_start_sb, row_start_sb, sizeof(int) * (tile_rows + 1));
  return cpi;
}

static void destroy_cpi(AV1_COMP *cpi) {
  free(cpi->ppi);
  free(cpi);
}

/* ---- worker counts --------------------------------------------------------------------- */

/* av1_compute_num_workers_for_mt() then av1_get_max_num_workers(). Outputs
 * ppi->p_mt_info.num_mod_workers[NUM_MT_MODULES] and cpi->weber_bsize (set by
 * compute_num_ai_workers()); returns av1_get_max_num_workers(). */
EXPORT int aomshim_f4_compute_num_workers(int mi_rows, int mi_cols, int mib_size_log2,
                                          int tile_cols, int tile_rows,
                                          const int *col_start_sb, const int *row_start_sb,
                                          int max_threads, int row_mt, int pass,
                                          int frame_enc_workers, int *num_mod_workers,
                                          int *weber_bsize) {
  AV1_COMP *cpi = create_cpi(mi_rows, mi_cols, mib_size_log2, tile_cols, tile_rows,
                             col_start_sb, row_start_sb);
  cpi->oxcf.max_threads = max_threads;
  cpi->oxcf.row_mt = (unsigned int)row_mt;
  cpi->oxcf.pass = (enum aom_enc_pass)pass;
  cpi->weber_bsize = BLOCK_INVALID;
  cpi->ppi->p_mt_info.num_mod_workers[MOD_FRAME_ENC] = frame_enc_workers;
  av1_compute_num_workers_for_mt(cpi);
  memcpy(num_mod_workers, cpi->ppi->p_mt_info.num_mod_workers,
         sizeof(int) * NUM_MT_MODULES);
  *weber_bsize = cpi->weber_bsize;
  const int max_num_workers = av1_get_max_num_workers(cpi);
  destroy_cpi(cpi);
  return max_num_workers;
}

/* av1_init_frame_mt() with p_mt_info.num_workers = num_workers and the given per-module worker
 * counts; outputs cpi->mt_info.num_mod_workers. Also returns, in alloc_workers, the result of
 * av1_get_num_mod_workers_for_alloc() for every module. */
EXPORT void aomshim_f4_init_frame_mt(int num_workers, const int *p_num_mod_workers,
                                     int *num_mod_workers, int *alloc_workers) {
  AV1_PRIMARY *ppi = (AV1_PRIMARY *)calloc(1, sizeof(*ppi));
  AV1_COMP *cpi = (AV1_COMP *)calloc(1, sizeof(*cpi));
  if (!ppi || !cpi) abort();
  cpi->ppi = ppi;
  ppi->p_mt_info.num_workers = num_workers;
  memcpy(ppi->p_mt_info.num_mod_workers, p_num_mod_workers, sizeof(int) * NUM_MT_MODULES);
  av1_init_frame_mt(ppi, cpi);
  memcpy(num_mod_workers, cpi->mt_info.num_mod_workers, sizeof(int) * NUM_MT_MODULES);
  for (int i = 0; i < NUM_MT_MODULES; i++)
    alloc_workers[i] =
        av1_get_num_mod_workers_for_alloc(&ppi->p_mt_info, (MULTI_THREADED_MODULES)i);
  free(cpi);
  free(ppi);
}

/* ---- tile layout / job scheduling ------------------------------------------------------ */

EXPORT void aomshim_f4_max_sb_rows_cols(int mi_rows, int mi_cols, int mib_size_log2,
                                        int tile_cols, int tile_rows, const int *col_start_sb,
                                        const int *row_start_sb, int *max_sb_rows,
                                        int *max_sb_cols) {
  AV1_COMP *cpi = create_cpi(mi_rows, mi_cols, mib_size_log2, tile_cols, tile_rows,
                             col_start_sb, row_start_sb);
  *max_sb_rows = 0;
  *max_sb_cols = 0;
  compute_max_sb_rows_cols(&cpi->common, max_sb_rows, max_sb_cols);
  destroy_cpi(cpi);
}

/* Replays the job dispatch of enc_row_mt_worker_hook() for num_workers workers over the tiles of
 * the layout: the tile data is set up as in av1_encode_tiles_row_mt() and the workers start at
 * the tiles assign_tile_to_thread() gives them (thread_id_to_tile_id: MAX_NUM_THREADS ints,
 * initialized to -1 as there).
 * ops: num_ops (worker, kind) pairs. kind 0: the worker requests a job (the mutex-protected
 * get_next_job() / switch_tile_and_get_next_job() section of the hook); ignored while the
 * worker holds a job or has reached the end of the frame. kind 1: the worker finishes its job
 * (num_threads_working--); ignored when it holds none.
 * out: 4 ints per op (0 ignored / 1 request / 2 finish, tile id, mi_row, end_of_frame).
 * tile_state: 2 ints per tile (next_mi_row, num_threads_working) after the last op. */
EXPORT void aomshim_f4_schedule(int mi_rows, int mi_cols, int mib_size_log2, int tile_cols,
                                int tile_rows, const int *col_start_sb,
                                const int *row_start_sb, int num_workers, const int *ops,
                                int num_ops, int *thread_id_to_tile_id, int *out,
                                int *tile_state) {
  AV1_COMP *cpi = create_cpi(mi_rows, mi_cols, mib_size_log2, tile_cols, tile_rows,
                             col_start_sb, row_start_sb);
  AV1_COMMON *const cm = &cpi->common;
  const int num_tiles = tile_cols * tile_rows;
  TileDataEnc *tile_data = (TileDataEnc *)calloc(num_tiles, sizeof(*tile_data));
  if (!tile_data) abort();
  for (int tile_row = 0; tile_row < tile_rows; tile_row++) {
    for (int tile_col = 0; tile_col < tile_cols; tile_col++) {
      TileDataEnc *const this_tile = &tile_data[tile_row * tile_cols + tile_col];
      av1_tile_init(&this_tile->tile_info, cm, tile_row, tile_col);
      this_tile->row_mt_sync.next_mi_row = this_tile->tile_info.mi_row_start;
      this_tile->row_mt_sync.num_threads_working = 0;
    }
  }
  memset(thread_id_to_tile_id, -1, sizeof(*thread_id_to_tile_id) * MAX_NUM_THREADS);
  assign_tile_to_thread(thread_id_to_tile_id, num_tiles, num_workers);

  int cur_tile_id[MAX_NUM_THREADS];
  int busy_tile[MAX_NUM_THREADS];
  int end_of_frame[MAX_NUM_THREADS];
  for (int w = 0; w < num_workers; w++) {
    cur_tile_id[w] = thread_id_to_tile_id[w];
    busy_tile[w] = -1;
    end_of_frame[w] = 0;
  }
  for (int i = 0; i < num_ops; i++) {
    const int w = ops[2 * i];
    const int kind = ops[2 * i + 1];
    int *const o = out + 4 * i;
    o[0] = 0;
    o[1] = -1;
    o[2] = -1;
    o[3] = 0;
    if (kind == 0) {
      if (busy_tile[w] >= 0 || end_of_frame[w]) continue;
      int current_mi_row = -1;
      if (!get_next_job(&tile_data[cur_tile_id[w]], &current_mi_row,
                        cm->seq_params->mib_size)) {
        switch_tile_and_get_next_job(cm, tile_data, &cur_tile_id[w], &current_mi_row,
                                     &end_of_frame[w], 0, BLOCK_16X16);
      }
      o[0] = 1;
      o[1] = cur_tile_id[w];
      o[2] = current_mi_row;
      o[3] = end_of_frame[w];
      if (!end_of_frame[w]) busy_tile[w] = cur_tile_id[w];
    } else {
      if (busy_tile[w] < 0) continue;
      tile_data[busy_tile[w]].row_mt_sync.num_threads_working--;
      busy_tile[w] = -1;
      o[0] = 2;
    }
  }
  for (int t = 0; t < num_tiles; t++) {
    tile_state[2 * t] = tile_data[t].row_mt_sync.next_mi_row;
    tile_state[2 * t + 1] = tile_data[t].row_mt_sync.num_threads_working;
  }
  free(tile_data);
  destroy_cpi(cpi);
}

/* ---- row synchronization --------------------------------------------------------------- */

/* A row sync object of `rows` rows set up as row_mt_sync_mem_alloc() and
 * av1_encode_tiles_row_mt() do (num_finished_cols = -1), with the given sync_range and
 * intrabc_extra_top_right_sb_delay. Applies av1_row_mt_sync_write(sync, r, c, cols) for each
 * (r, c) pair of ops and stores num_finished_cols (rows ints) after every op in out. */
EXPORT void aomshim_f4_row_sync_write(int rows, int sync_range, int intrabc_delay, int cols,
                                      const int *ops, int num_ops, int *out) {
  AV1EncRowMultiThreadSync sync;
  memset(&sync, 0, sizeof(sync));
  sync.mutex_ = (pthread_mutex_t *)malloc(sizeof(*sync.mutex_) * rows);
  sync.cond_ = (pthread_cond_t *)malloc(sizeof(*sync.cond_) * rows);
  sync.num_finished_cols = (int *)malloc(sizeof(*sync.num_finished_cols) * rows);
  if (!sync.mutex_ || !sync.cond_ || !sync.num_finished_cols) abort();
  for (int i = 0; i < rows; ++i) {
    pthread_mutex_init(&sync.mutex_[i], NULL);
    pthread_cond_init(&sync.cond_[i], NULL);
  }
  sync.rows = rows;
  sync.sync_range = sync_range;
  sync.intrabc_extra_top_right_sb_delay = intrabc_delay;
  memset(sync.num_finished_cols, -1, sizeof(*sync.num_finished_cols) * rows);

  for (int i = 0; i < num_ops; i++) {
    av1_row_mt_sync_write(&sync, ops[2 * i], ops[2 * i + 1], cols);
    memcpy(out + i * rows, sync.num_finished_cols, sizeof(int) * rows);
  }

  for (int i = 0; i < rows; ++i) {
    pthread_mutex_destroy(&sync.mutex_[i]);
    pthread_cond_destroy(&sync.cond_[i]);
  }
  free(sync.mutex_);
  free(sync.cond_);
  free(sync.num_finished_cols);
}

/* ---- frame counts / pack bitstream tile order ------------------------------------------ */

EXPORT int aomshim_f4_frame_counts_size(void) {
  return (int)(sizeof(FRAME_COUNTS) / sizeof(unsigned int));
}

/* av1_accumulate_frame_counts(acc, counts) on flat FRAME_COUNTS images of n unsigned ints. */
EXPORT int aomshim_f4_accumulate_frame_counts(unsigned int *acc, const unsigned int *counts,
                                              int n) {
  if (n != aomshim_f4_frame_counts_size()) return -1;
  FRAME_COUNTS a, c;
  memcpy(&a, acc, sizeof(a));
  memcpy(&c, counts, sizeof(c));
  av1_accumulate_frame_counts(&a, &c);
  memcpy(acc, &a, sizeof(a));
  return 0;
}

/* The tile order of prepare_pack_bs_workers(): tile_idx after qsort with compare_tile_order. */
EXPORT void aomshim_f4_pack_bs_tile_order(const uint64_t *abs_sum_level, int num_tiles,
                                          uint16_t *tile_idx_out) {
  PackBSTileOrder pack_bs_tile_order[MAX_TILES];
  av1_zero_array(pack_bs_tile_order, num_tiles);
  for (uint16_t tile_idx = 0; tile_idx < num_tiles; tile_idx++) {
    pack_bs_tile_order[tile_idx].abs_sum_level = abs_sum_level[tile_idx];
    pack_bs_tile_order[tile_idx].tile_idx = tile_idx;
  }
  qsort(pack_bs_tile_order, num_tiles, sizeof(*pack_bs_tile_order), compare_tile_order);
  for (int i = 0; i < num_tiles; i++) tile_idx_out[i] = pack_bs_tile_order[i].tile_idx;
}
