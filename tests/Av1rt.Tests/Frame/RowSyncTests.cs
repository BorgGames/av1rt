using System;
using System.Threading;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.aom_pthread;

namespace Av1rt.Tests.Frame;

// ethread.c row synchronization (av1_row_mt_sync_read / av1_row_mt_sync_write): the
// num_finished_cols updates against libaom, and the top-right dependency hand-off on real threads.
public class RowSyncTests
{
    // A sync object as row_mt_sync_mem_alloc() + av1_encode_tiles_row_mt() set it up.
    private static AV1EncRowMultiThreadSync CreateSync(int rows, int sync_range, int intrabc_delay)
    {
        var sync = new AV1EncRowMultiThreadSync();
        EthreadPrivate.row_mt_sync_mem_alloc(sync, new AV1_COMMON(), rows);
        Assert.Equal(rows, sync.rows);
        Assert.Equal(1, sync.sync_range);
        Assert.Equal(rows, sync.mutex_.Length);
        Assert.Equal(rows, sync.cond_.Length);
        sync.sync_range = sync_range;
        sync.intrabc_extra_top_right_sb_delay = intrabc_delay;
        CRuntime.memset(sync.num_finished_cols, -1, rows);
        return sync;
    }

    [Fact]
    public void SyncWriteMatchesLibaom()
    {
        var rng = new Random(0xF411);
        for (int iter = 0; iter < 300; iter++)
        {
            int rows = rng.Next(1, 21);
            int cols = rng.Next(1, 31);
            int sync_range = rng.Next(1, 5);
            int intrabc_delay = rng.Next(0, 3);
            int num_ops = rng.Next(1, 200);
            var ops = new int[2 * num_ops];
            // Mostly in-order columns per row (as encode_sb_row and set_encoding_done write), with
            // random writes mixed in to exercise the AOMMAX guard.
            var next_col = new int[rows];
            for (int i = 0; i < num_ops; i++)
            {
                int r = rng.Next(rows);
                int c = rng.Next(3) == 0 ? rng.Next(cols) : Math.Min(next_col[r]++, cols - 1);
                ops[2 * i] = r;
                ops[2 * i + 1] = c;
            }

            var c_out = new int[num_ops * rows];
            F4Shim.aomshim_f4_row_sync_write(rows, sync_range, intrabc_delay, cols, ops, num_ops, c_out);

            AV1EncRowMultiThreadSync sync = CreateSync(rows, sync_range, intrabc_delay);
            for (int i = 0; i < num_ops; i++)
            {
                ethread.av1_row_mt_sync_write(sync, ops[2 * i], ops[2 * i + 1], cols);
                for (int r = 0; r < rows; r++)
                {
                    Assert.True(c_out[i * rows + r] == sync.num_finished_cols[r],
                        $"iter {iter} op {i} row {r}: libaom {c_out[i * rows + r]} port {sync.num_finished_cols[r]}");
                }
            }

            ethread.av1_row_mt_sync_mem_dealloc(sync);
            Assert.Null(sync.mutex_);
            Assert.Null(sync.cond_);
            Assert.True(sync.num_finished_cols.IsNull);
            Assert.Equal(0, sync.rows);
            Assert.Equal(0, sync.sync_range);
        }
    }

    // Superblocks a reader of (r, c) may rely on after av1_row_mt_sync_read(r, c - delay_wait):
    // the columns 0..min(c - delay_wait + sync_range + intrabc_delay, cols - 1) of row r - 1.
    private static int RequiredAboveCol(int c, int delay_wait, int sync_range, int intrabc_delay, int cols) =>
        Math.Min(c - delay_wait + sync_range + intrabc_delay, cols - 1);

    // Threads take superblock rows from a shared counter (as get_next_job under enc_row_mt's mutex)
    // and code them left to right with the read/write protocol of encode_sb_row. Each superblock
    // checks that every superblock above-right it depends on is complete and visible (plain array
    // writes published only through the sync primitives).
    [Theory]
    [InlineData(2, 1, 1, 0)]
    [InlineData(3, 0, 1, 0)]
    [InlineData(4, 1, 1, 0)]
    [InlineData(8, 1, 1, 0)]
    [InlineData(8, 0, 2, 0)]
    [InlineData(6, 1, 1, 2)]
    [InlineData(16, 1, 3, 1)]
    public void ReadWriteStressKeepsTopRightDependency(int num_threads, int delay_wait, int sync_range, int intrabc_delay)
    {
        const int rows = 45, cols = 37, rounds = 12;
        var seed_rng = new Random(0xF412 + num_threads * 31 + delay_wait * 7 + sync_range);
        for (int round = 0; round < rounds; round++)
        {
            AV1EncRowMultiThreadSync sync = CreateSync(rows, sync_range, intrabc_delay);
            var done = new int[rows * cols];  // written by the coding thread, never with atomics
            pthread_mutex_t job_mutex = null;
            pthread_mutex_init(ref job_mutex, null);
            int next_row = 0;
            int violations = 0;
            string first_violation = null;

            var threads = new Thread[num_threads];
            for (int t = 0; t < num_threads; t++)
            {
                int seed = seed_rng.Next();
                threads[t] = new Thread(() =>
                {
                    var rng = new Random(seed);
                    while (true)
                    {
                        pthread_mutex_lock(job_mutex);
                        int r = next_row++;
                        pthread_mutex_unlock(job_mutex);
                        if (r >= rows) return;
                        for (int c = 0; c < cols; c++)
                        {
                            ethread.av1_row_mt_sync_read(sync, r, c - delay_wait);
                            if (r > 0)
                            {
                                int need = RequiredAboveCol(c, delay_wait, sync_range, intrabc_delay, cols);
                                for (int j = 0; j <= need; j++)
                                {
                                    if (done[(r - 1) * cols + j] == 0)
                                    {
                                        if (Interlocked.Increment(ref violations) == 1)
                                            first_violation = $"({r},{c}) ran before ({r - 1},{j})";
                                    }
                                }
                            }
                            int spin = rng.Next(4) == 0 ? rng.Next(2000) : rng.Next(50);
                            Thread.SpinWait(spin);
                            if (rng.Next(64) == 0) Thread.Yield();
                            done[r * cols + c] = 1;
                            ethread.av1_row_mt_sync_write(sync, r, c, cols);
                        }
                    }
                }) { IsBackground = true };
            }
            foreach (Thread th in threads) th.Start();
            foreach (Thread th in threads)
                Assert.True(th.Join(TimeSpan.FromSeconds(60)), "row sync deadlock (worker did not finish)");

            Assert.True(violations == 0, first_violation);
            Assert.All(done, v => Assert.Equal(1, v));
            for (int r = 0; r < rows; r++)
                Assert.Equal(cols + sync_range + intrabc_delay, sync.num_finished_cols[r]);
        }
    }

    // The error path of enc_row_mt_worker_hook (set_encoding_done): writing the last column of
    // every row releases every reader, whatever column it waits for, and a later smaller write
    // cannot lower num_finished_cols again.
    [Fact]
    public void FinalColumnWriteReleasesAllReaders()
    {
        const int rows = 12, cols = 9;
        AV1EncRowMultiThreadSync sync = CreateSync(rows, 1, 0);
        var released = new int[rows];
        var threads = new Thread[rows - 1];
        for (int r = 1; r < rows; r++)
        {
            int row = r;
            threads[r - 1] = new Thread(() =>
            {
                ethread.av1_row_mt_sync_read(sync, row, cols - 1);
                Volatile.Write(ref released[row], 1);
            }) { IsBackground = true };
            threads[r - 1].Start();
        }

        Thread.Sleep(50);
        for (int r = 1; r < rows; r++) Assert.Equal(0, Volatile.Read(ref released[r]));

        for (int r = 0; r < rows; r++) ethread.av1_row_mt_sync_write(sync, r, cols - 1, cols);
        foreach (Thread th in threads)
            Assert.True(th.Join(TimeSpan.FromSeconds(30)), "reader not released by the final column write");

        ethread.av1_row_mt_sync_write(sync, 0, 0, cols);
        Assert.Equal(cols + 1, sync.num_finished_cols[0]);
    }

    [Fact]
    public void DummySyncNeverBlocks()
    {
        var sync = new AV1EncRowMultiThreadSync();
        ethread.av1_row_mt_sync_read_dummy(sync, 5, 100);
        ethread.av1_row_mt_sync_write_dummy(sync, 5, 100, 3);
        Assert.Null(sync.mutex_);
    }
}
