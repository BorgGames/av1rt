using System;
using System.Collections.Generic;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.aom_encoder;
using static Av1rt.Aom.enc_enums;
using static Av1rt.Aom.encoder;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.filter;

namespace Av1rt.Tests.Frame;

// ethread.c: worker counts per multi-threaded module, tile-to-thread assignment, the row-mt job
// dispatch, frame counter accumulation and the pack bitstream tile order, against libaom.
public class EthreadTests
{
    [Fact]
    public void ConstantsMatchLibaom()
    {
        Assert.Equal(NUM_MT_MODULES, F4Shim.aomshim_f4_num_mt_modules());
        Assert.Equal(MAX_NUM_THREADS, F4Shim.aomshim_f4_max_num_threads());
        // FRAME_COUNTS holds only switchable_interp without CONFIG_ENTROPY_STATS, which the
        // port's av1_accumulate_frame_counts relies on.
        Assert.Equal(SWITCHABLE_FILTER_CONTEXTS * SWITCHABLE_FILTERS, F4Shim.aomshim_f4_frame_counts_size());
    }

    private static readonly int[] ThreadCounts = { 0, 1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64 };

    [Fact]
    public void ComputeNumWorkersForMtMatchesLibaom()
    {
        var rng = new Random(0xF401);
        for (int iter = 0; iter < 400; iter++)
        {
            FrameLayout l = FrameLayout.Random(rng, 540, 960, 8, 8);
            int max_threads = rng.Next(3) == 0 ? rng.Next(0, 65) : ThreadCounts[rng.Next(ThreadCounts.Length)];
            int row_mt = rng.Next(4) == 0 ? 0 : 1;
            int pass = rng.Next(4) == 0 ? AOM_RC_FIRST_PASS : AOM_RC_ONE_PASS;
            int frame_enc_workers = rng.Next(0, 3);

            var c_mod = new int[NUM_MT_MODULES];
            int c_max = F4Shim.aomshim_f4_compute_num_workers(l.mi_rows, l.mi_cols, l.mib_size_log2, l.tile_cols,
                l.tile_rows, l.col_start_sb, l.row_start_sb, max_threads, row_mt, pass, frame_enc_workers, c_mod,
                out int c_weber);

            AV1_COMP cpi = l.CreateCpi();
            cpi.oxcf.max_threads = max_threads;
            cpi.oxcf.row_mt = row_mt != 0;
            cpi.oxcf.pass = pass;
            cpi.weber_bsize = BLOCK_INVALID;
            cpi.ppi.p_mt_info.num_mod_workers[MOD_FRAME_ENC] = frame_enc_workers;
            ethread.av1_compute_num_workers_for_mt(cpi);
            int max = ethread.av1_get_max_num_workers(cpi);

            string what = $"iter {iter}: {l} threads {max_threads} row_mt {row_mt} pass {pass}";
            Assert.True(c_mod.AsSpan().SequenceEqual(cpi.ppi.p_mt_info.num_mod_workers), what);
            Assert.True(c_weber == cpi.weber_bsize, what);
            Assert.True(c_max == max, what);
        }
    }

    [Fact]
    public void InitFrameMtAndAllocWorkersMatchLibaom()
    {
        var rng = new Random(0xF402);
        for (int iter = 0; iter < 300; iter++)
        {
            int num_workers = rng.Next(1, MAX_NUM_THREADS + 1);
            var p_mod = new int[NUM_MT_MODULES];
            for (int i = 0; i < NUM_MT_MODULES; i++) p_mod[i] = rng.Next(0, MAX_NUM_THREADS + 1);
            p_mod[MOD_FRAME_ENC] = rng.Next(0, 3);

            var c_mod = new int[NUM_MT_MODULES];
            var c_alloc = new int[NUM_MT_MODULES];
            F4Shim.aomshim_f4_init_frame_mt(num_workers, p_mod, c_mod, c_alloc);

            var ppi = new AV1_PRIMARY();
            var cpi = new AV1_COMP { ppi = ppi };
            ppi.p_mt_info.num_workers = num_workers;
            Array.Copy(p_mod, ppi.p_mt_info.num_mod_workers, NUM_MT_MODULES);
            ethread.av1_init_frame_mt(ppi, cpi);
            Assert.Equal(c_mod, cpi.mt_info.num_mod_workers);
            Assert.Equal(num_workers, cpi.mt_info.num_workers);
            for (int i = 0; i < NUM_MT_MODULES; i++)
                Assert.Equal(c_alloc[i], ethread.av1_get_num_mod_workers_for_alloc(ppi.p_mt_info, i));
        }
    }

    [Fact]
    public void AssignTileToThreadMatchesLibaom()
    {
        // Through the schedule shim, which records thread_id_to_tile_id after
        // assign_tile_to_thread() (an empty op list).
        for (int num_tiles = 1; num_tiles <= 64; num_tiles++)
        {
            for (int num_workers = 1; num_workers <= MAX_NUM_THREADS; num_workers++)
            {
                var l = new FrameLayout
                {
                    mib_size_log2 = 4, mi_rows = 16, mi_cols = 16 * num_tiles, tile_cols = num_tiles, tile_rows = 1,
                    row_start_sb = new[] { 0, 1 },
                };
                l.col_start_sb = new int[num_tiles + 1];
                for (int i = 0; i <= num_tiles; i++) l.col_start_sb[i] = i;

                var c = new int[MAX_NUM_THREADS];
                F4Shim.aomshim_f4_schedule(l.mi_rows, l.mi_cols, l.mib_size_log2, l.tile_cols, l.tile_rows,
                    l.col_start_sb, l.row_start_sb, num_workers, Array.Empty<int>(), 0, c, Array.Empty<int>(),
                    new int[2 * num_tiles]);

                var cs = new int[MAX_NUM_THREADS];
                Array.Fill(cs, -1);
                EthreadPrivate.assign_tile_to_thread(new Ptr<int>(cs), num_tiles, num_workers);
                Assert.Equal(c, cs);
            }
        }
    }

    [Fact]
    public void MaxSbRowsColsMatchesLibaom()
    {
        var rng = new Random(0xF403);
        for (int iter = 0; iter < 500; iter++)
        {
            FrameLayout l = FrameLayout.Random(rng, 540, 960, 64, 64);
            F4Shim.aomshim_f4_max_sb_rows_cols(l.mi_rows, l.mi_cols, l.mib_size_log2, l.tile_cols, l.tile_rows,
                l.col_start_sb, l.row_start_sb, out int c_rows, out int c_cols);
            AV1_COMP cpi = l.CreateCpi();
            int rows = 0, cols = 0;
            EthreadPrivate.compute_max_sb_rows_cols(cpi.common, ref rows, ref cols);
            Assert.True(c_rows == rows && c_cols == cols, $"iter {iter}: {l}");
        }
    }

    // The job dispatch of enc_row_mt_worker_hook, replayed op by op (see aomshim_f4_schedule).
    private sealed class Dispatch
    {
        private readonly AV1_COMMON cm;
        private readonly TileDataEnc[] tile_data;
        private readonly int[] cur_tile_id, busy_tile, end_of_frame;
        public readonly int[] thread_id_to_tile_id = new int[MAX_NUM_THREADS];

        public Dispatch(FrameLayout l, int num_workers)
        {
            cm = l.CreateCpi().common;
            tile_data = new TileDataEnc[l.NumTiles];
            for (int tile_row = 0; tile_row < l.tile_rows; tile_row++)
            {
                for (int tile_col = 0; tile_col < l.tile_cols; tile_col++)
                {
                    var t = new TileDataEnc();
                    tile_common.av1_tile_init(ref t.tile_info, cm, tile_row, tile_col);
                    t.row_mt_sync.next_mi_row = t.tile_info.mi_row_start;
                    t.row_mt_sync.num_threads_working = 0;
                    tile_data[tile_row * l.tile_cols + tile_col] = t;
                }
            }
            Array.Fill(thread_id_to_tile_id, -1);
            EthreadPrivate.assign_tile_to_thread(new Ptr<int>(thread_id_to_tile_id), l.NumTiles, num_workers);
            cur_tile_id = new int[num_workers];
            busy_tile = new int[num_workers];
            end_of_frame = new int[num_workers];
            for (int w = 0; w < num_workers; w++)
            {
                cur_tile_id[w] = thread_id_to_tile_id[w];
                busy_tile[w] = -1;
            }
        }

        public bool Busy(int w) => busy_tile[w] >= 0;
        public bool Done(int w) => end_of_frame[w] != 0;
        public TileDataEnc Tile(int t) => tile_data[t];

        // Returns (kind, tile, mi_row, end_of_frame) as the shim records it.
        public (int, int, int, int) Apply(int w, int kind)
        {
            if (kind == 0)
            {
                if (busy_tile[w] >= 0 || end_of_frame[w] != 0) return (0, -1, -1, 0);
                int current_mi_row = -1;
                if (EthreadPrivate.get_next_job(tile_data[cur_tile_id[w]], ref current_mi_row, cm.seq_params.mib_size) == 0)
                {
                    EthreadPrivate.switch_tile_and_get_next_job(cm, tile_data, ref cur_tile_id[w], ref current_mi_row,
                        ref end_of_frame[w], 0, BLOCK_16X16);
                }
                if (end_of_frame[w] == 0) busy_tile[w] = cur_tile_id[w];
                return (1, cur_tile_id[w], current_mi_row, end_of_frame[w]);
            }
            if (busy_tile[w] < 0) return (0, -1, -1, 0);
            tile_data[busy_tile[w]].row_mt_sync.num_threads_working--;
            busy_tile[w] = -1;
            return (2, -1, -1, 0);
        }
    }

    [Fact]
    public void RowMtJobSchedulingMatchesLibaom()
    {
        var rng = new Random(0xF404);
        for (int iter = 0; iter < 150; iter++)
        {
            FrameLayout l = FrameLayout.Random(rng, 300, 500, 6, 6);
            int num_workers = rng.Next(1, 33);
            double finish_prob = 0.2 + 0.7 * rng.NextDouble();

            // The C# dispatch picks the ops (random workers that have not reached the end of the
            // frame) until every worker has; the shim then replays the same ops.
            var d = new Dispatch(l, num_workers);
            var ops = new List<int>();
            var results = new List<(int, int, int, int)>();
            var dispatched = new HashSet<(int, int)>();
            int remaining = num_workers;
            while (remaining > 0)
            {
                Assert.True(ops.Count < 4_000_000, $"iter {iter}: dispatch does not terminate");
                int w = rng.Next(num_workers);
                if (d.Done(w)) continue;
                int kind = d.Busy(w) ? (rng.NextDouble() < finish_prob ? 1 : 0) : (rng.Next(8) == 0 ? 1 : 0);
                ops.Add(w);
                ops.Add(kind);
                (int k, int tile, int mi_row, int eof) r = d.Apply(w, kind);
                results.Add(r);
                if (r.k == 1 && r.eof == 0) Assert.True(dispatched.Add((r.tile, r.mi_row)), $"iter {iter}: job dispatched twice");
                if (r.k == 1 && r.eof != 0) remaining--;
            }

            int n = results.Count;
            var c_out = new int[4 * n];
            var c_map = new int[MAX_NUM_THREADS];
            var c_tiles = new int[2 * l.NumTiles];
            F4Shim.aomshim_f4_schedule(l.mi_rows, l.mi_cols, l.mib_size_log2, l.tile_cols, l.tile_rows,
                l.col_start_sb, l.row_start_sb, num_workers, ops.ToArray(), n, c_map, c_out, c_tiles);

            Assert.Equal(c_map, d.thread_id_to_tile_id);
            for (int i = 0; i < n; i++)
            {
                var c = (c_out[4 * i], c_out[4 * i + 1], c_out[4 * i + 2], c_out[4 * i + 3]);
                Assert.True(c == results[i], $"iter {iter} op {i} (worker {ops[2 * i]} kind {ops[2 * i + 1]}): " +
                    $"libaom {c} port {results[i]}; {l} workers {num_workers}");
            }

            // Every superblock row of every tile was dispatched exactly once and no job is open.
            int jobs = 0;
            for (int t = 0; t < l.NumTiles; t++)
            {
                TileDataEnc tile = d.Tile(t);
                Assert.Equal(c_tiles[2 * t], tile.row_mt_sync.next_mi_row);
                Assert.Equal(c_tiles[2 * t + 1], tile.row_mt_sync.num_threads_working);
                Assert.Equal(0, tile.row_mt_sync.num_threads_working);
                for (int mi_row = tile.tile_info.mi_row_start; mi_row < tile.tile_info.mi_row_end;
                     mi_row += 1 << l.mib_size_log2)
                {
                    Assert.Contains((t, mi_row), dispatched);
                    jobs++;
                }
            }
            Assert.Equal(jobs, dispatched.Count);
        }
    }

    [Fact]
    public void AccumulateFrameCountsMatchesLibaom()
    {
        var rng = new Random(0xF405);
        int n = F4Shim.aomshim_f4_frame_counts_size();
        for (int iter = 0; iter < 200; iter++)
        {
            var acc = new uint[n];
            var cnt = new uint[n];
            for (int i = 0; i < n; i++)
            {
                // Include values that wrap (unsigned int arithmetic in C).
                acc[i] = rng.Next(4) == 0 ? uint.MaxValue - (uint)rng.Next(1000) : (uint)rng.Next(1 << 20);
                cnt[i] = rng.Next(4) == 0 ? (uint)rng.NextInt64(0, 1L << 32) : (uint)rng.Next(1 << 20);
            }

            var acc_counts = new FRAME_COUNTS();
            var counts = new FRAME_COUNTS();
            Unflatten(acc, acc_counts);
            Unflatten(cnt, counts);
            ethread.av1_accumulate_frame_counts(acc_counts, counts);

            Assert.Equal(0, F4Shim.aomshim_f4_accumulate_frame_counts(acc, cnt, n));
            Assert.Equal(acc, Flatten(acc_counts));
            Assert.Equal(cnt, Flatten(counts));  // the source counts are not modified
        }
    }

    // FRAME_COUNTS image in C memory order: switchable_interp[SWITCHABLE_FILTER_CONTEXTS][SWITCHABLE_FILTERS].
    private static void Unflatten(uint[] flat, FRAME_COUNTS counts)
    {
        for (int i = 0; i < SWITCHABLE_FILTER_CONTEXTS; i++)
            Array.Copy(flat, i * SWITCHABLE_FILTERS, counts.switchable_interp[i], 0, SWITCHABLE_FILTERS);
    }

    private static uint[] Flatten(FRAME_COUNTS counts)
    {
        var flat = new uint[SWITCHABLE_FILTER_CONTEXTS * SWITCHABLE_FILTERS];
        for (int i = 0; i < SWITCHABLE_FILTER_CONTEXTS; i++)
            Array.Copy(counts.switchable_interp[i], 0, flat, i * SWITCHABLE_FILTERS, SWITCHABLE_FILTERS);
        return flat;
    }

    [Fact]
    public void PackBsTileOrderMatchesQsort()
    {
        // prepare_pack_bs_workers sorts with qsort(compare_tile_order); the port with Span.Sort.
        var rng = new Random(0xF406);
        for (int iter = 0; iter < 500; iter++)
        {
            int num_tiles = rng.Next(4) == 0 ? rng.Next(1, MAX_TILES + 1) : rng.Next(1, 65);
            int distinct = rng.Next(1, 6);
            var levels = new ulong[num_tiles];
            for (int i = 0; i < num_tiles; i++)
                levels[i] = rng.Next(3) == 0 ? (ulong)rng.NextInt64() : (ulong)rng.Next(distinct);  // many ties

            var c = new ushort[num_tiles];
            F4Shim.aomshim_f4_pack_bs_tile_order(levels, num_tiles, c);

            var order = new PackBSTileOrder[MAX_TILES];
            for (ushort tile_idx = 0; tile_idx < num_tiles; tile_idx++)
            {
                order[tile_idx].abs_sum_level = levels[tile_idx];
                order[tile_idx].tile_idx = tile_idx;
            }
            order.AsSpan(0, num_tiles).Sort(EthreadPrivate.compare_tile_order_fn);
            for (int i = 0; i < num_tiles; i++) Assert.Equal(c[i], order[i].tile_idx);
        }
    }
}
