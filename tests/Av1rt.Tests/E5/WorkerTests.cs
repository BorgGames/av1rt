using System.Threading;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.aom_thread;

namespace Av1rt.Tests.E5;

// aom_thread.c: the AVxWorker state machine on real threads, used the way the encoder's
// multithreading code uses it (reset once, then launch/sync rounds, execute on the main thread
// for the last worker, end).
public class WorkerTests
{
    private sealed class Job
    {
        public int id;
        public int round;
        public long result;
        public int calls;
        public int failOnRound = -1;
    }

    private static int Hook(object data1, object data2)
    {
        var job = (Job)data1;
        var input = (long[])data2;
        long sum = 0;
        for (int i = 0; i < input.Length; i++) sum += input[i] * (job.id + 1) + job.round;
        job.result = sum;
        Interlocked.Increment(ref job.calls);
        return job.round == job.failOnRound ? 0 : 1;
    }

    private static long Expected(Job job, long[] input)
    {
        long sum = 0;
        for (int i = 0; i < input.Length; i++) sum += input[i] * (job.id + 1) + job.round;
        return sum;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void LaunchSyncRoundsComputeAllJobs(int num_workers)
    {
        AVxWorkerInterface winterface = aom_get_worker_interface();
        Assert.Same(winterface, aom_get_worker_interface());

        var input = new long[10000];
        for (int i = 0; i < input.Length; i++) input[i] = i * 7 - 3;

        var workers = new AVxWorker[num_workers];
        var jobs = new Job[num_workers];
        for (int i = 0; i < num_workers; i++)
        {
            workers[i] = new AVxWorker();
            winterface.init(workers[i]);
            Assert.Equal(AVX_WORKER_STATUS_NOT_OK, workers[i].status_);
            jobs[i] = new Job { id = i, failOnRound = i == num_workers / 2 ? 7 : -1 };
            // As in av1_create_workers: the main thread's worker (the last one) gets no thread.
            if (i < num_workers - 1)
            {
                Assert.Equal(1, winterface.reset(workers[i]));
                Assert.Equal(AVX_WORKER_STATUS_OK, workers[i].status_);
                Assert.NotNull(workers[i].impl_);
            }
            else
            {
                Assert.Null(workers[i].impl_);  // stays AVX_WORKER_STATUS_NOT_OK
            }
        }

        const int rounds = 25;
        for (int round = 0; round < rounds; round++)
        {
            for (int i = 0; i < num_workers; i++)
            {
                jobs[i].round = round;
                jobs[i].result = 0;
                workers[i].hook = Hook;
                workers[i].data1 = jobs[i];
                workers[i].data2 = input;
                workers[i].had_error = 0;
            }
            // Launch in reverse order and run the last worker on this thread, as
            // av1_encode_tiles_mt / launch_workers do.
            for (int i = num_workers - 1; i >= 0; i--)
            {
                if (i == num_workers - 1) winterface.execute(workers[i]);
                else winterface.launch(workers[i]);
            }
            for (int i = num_workers - 1; i >= 0; i--)
            {
                int ok = winterface.sync(workers[i]);
                Assert.Equal(i < num_workers - 1 ? AVX_WORKER_STATUS_OK : AVX_WORKER_STATUS_NOT_OK,
                             workers[i].status_);
                Assert.Equal(Expected(jobs[i], input), jobs[i].result);
                bool shouldFail = round == jobs[i].failOnRound;
                Assert.Equal(shouldFail ? 0 : 1, ok);
                Assert.Equal(shouldFail ? 1 : 0, workers[i].had_error);
            }
        }
        for (int i = 0; i < num_workers; i++) Assert.Equal(rounds, jobs[i].calls);

        // A reset of a worker whose thread already runs only clears the error state.
        if (num_workers > 1)
        {
            workers[0].had_error = 1;
            Assert.Equal(1, winterface.reset(workers[0]));
            Assert.Equal(0, workers[0].had_error);
        }

        for (int i = num_workers - 1; i >= 0; i--)
        {
            winterface.end(workers[i]);
            Assert.Null(workers[i].impl_);
            Assert.Equal(AVX_WORKER_STATUS_NOT_OK, workers[i].status_);
        }
    }

    [Fact]
    public void LaunchWithoutSyncThenEndJoinsThread()
    {
        AVxWorkerInterface winterface = aom_get_worker_interface();
        var worker = new AVxWorker();
        winterface.init(worker);
        Assert.Equal(1, winterface.reset(worker));
        var job = new Job { id = 3, round = 1 };
        var input = new long[] { 1, 2, 3 };
        worker.hook = Hook;
        worker.data1 = job;
        worker.data2 = input;
        winterface.launch(worker);
        // end() waits for the running hook (change_state waits until the worker is idle).
        winterface.end(worker);
        Assert.Equal(1, job.calls);
        Assert.Equal(Expected(job, input), job.result);
        Assert.Null(worker.impl_);
    }

    [Fact]
    public void NullHookSucceeds()
    {
        AVxWorkerInterface winterface = aom_get_worker_interface();
        var worker = new AVxWorker();
        winterface.init(worker);
        Assert.Equal(1, winterface.reset(worker));
        winterface.launch(worker);
        Assert.Equal(1, winterface.sync(worker));
        winterface.end(worker);
    }
}
