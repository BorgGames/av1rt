// Port of libaom aom_util/aom_thread.c
//
// Multi-threaded worker
//
// Original source:
//  https://chromium.googlesource.com/webm/libwebp

using System.Diagnostics;

namespace Av1rt.Aom;

// [port] CONFIG_MULTITHREAD == 1, on the pthread mapping of aom_pthread.h.cs. struct AVxWorkerImpl
// is declared in aom_thread.h.cs. HAVE_PTHREAD_SETNAME_NP is not defined on Windows, so the
// thread naming block of thread_loop is compiled out. aom_set_worker_interface never runs in the
// subset and is not ported.
internal static partial class aom_thread
{
    //------------------------------------------------------------------------------

    // [port] The thread entry point (THREADFN thread_loop(void *ptr)); arg is the AVxWorker.
    private static readonly pthread_start_routine thread_loop_fn = thread_loop;

    private static uint thread_loop(object ptr)
    {
        AVxWorker worker = (AVxWorker)ptr;
        pthread_mutex_lock(worker.impl_.mutex_);
        for (;;)
        {
            while (worker.status_ == AVX_WORKER_STATUS_OK)
            {  // wait in idling mode
                pthread_cond_wait(worker.impl_.condition_, worker.impl_.mutex_);
            }
            if (worker.status_ == AVX_WORKER_STATUS_WORKING)
            {
                // When worker->status_ is AVX_WORKER_STATUS_WORKING, the main thread
                // doesn't change worker->status_ and will wait until the worker changes
                // worker->status_ to AVX_WORKER_STATUS_OK. See change_state(). So the
                // worker can safely call execute() without holding worker->impl_->mutex_.
                // When the worker reacquires worker->impl_->mutex_, worker->status_ must
                // still be AVX_WORKER_STATUS_WORKING.
                pthread_mutex_unlock(worker.impl_.mutex_);
                execute(worker);
                pthread_mutex_lock(worker.impl_.mutex_);
                Debug.Assert(worker.status_ == AVX_WORKER_STATUS_WORKING);
                worker.status_ = AVX_WORKER_STATUS_OK;
                // signal to the main thread that we're done (for sync())
                pthread_cond_signal(worker.impl_.condition_);
            }
            else
            {
                Debug.Assert(worker.status_ == AVX_WORKER_STATUS_NOT_OK);  // finish the worker
                break;
            }
        }
        pthread_mutex_unlock(worker.impl_.mutex_);
        return THREAD_EXIT_SUCCESS;  // Thread is finished
    }

    // main thread state control
    private static void change_state(AVxWorker worker, AVxWorkerStatus new_status)
    {
        // No-op when attempting to change state on a thread that didn't come up.
        // Checking status_ without acquiring the lock first would result in a data
        // race.
        if (worker.impl_ == null) return;

        pthread_mutex_lock(worker.impl_.mutex_);
        if (worker.status_ >= AVX_WORKER_STATUS_OK)
        {
            // wait for the worker to finish
            while (worker.status_ != AVX_WORKER_STATUS_OK)
            {
                pthread_cond_wait(worker.impl_.condition_, worker.impl_.mutex_);
            }
            // assign new status and release the working thread if needed
            if (new_status != AVX_WORKER_STATUS_OK)
            {
                worker.status_ = new_status;
                pthread_cond_signal(worker.impl_.condition_);
            }
        }
        pthread_mutex_unlock(worker.impl_.mutex_);
    }

    //------------------------------------------------------------------------------

    private static void init(AVxWorker worker)
    {
        worker.Clear();  // memset(worker, 0, sizeof(*worker))
        worker.status_ = AVX_WORKER_STATUS_NOT_OK;
    }

    private static int sync(AVxWorker worker)
    {
        // CONFIG_MULTITHREAD
        change_state(worker, AVX_WORKER_STATUS_OK);
        Debug.Assert(worker.status_ <= AVX_WORKER_STATUS_OK);
        return B(worker.had_error == 0);
    }

    private static int reset(AVxWorker worker)
    {
        int ok = 1;
        worker.had_error = 0;
        if (worker.status_ < AVX_WORKER_STATUS_OK)
        {
            // CONFIG_MULTITHREAD
            worker.impl_ = new AVxWorkerImpl();  // aom_calloc(1, sizeof(*worker->impl_))
            if (worker.impl_ == null)
            {
                return 0;
            }
            if (pthread_mutex_init(ref worker.impl_.mutex_, null) != 0)
            {
                goto Error;
            }
            if (pthread_cond_init(ref worker.impl_.condition_, null) != 0)
            {
                pthread_mutex_destroy(worker.impl_.mutex_);
                goto Error;
            }
            pthread_attr_t attr = 0;
            if (pthread_attr_init(ref attr) != 0) goto Error2;
            // Debug ASan builds require at least ~1MiB of stack; prevents
            // failures on macOS arm64 where the default is 512KiB.
            // See: https://crbug.com/aomedia/3379
            const int kMinStackSize = 256 * 1024;
            int stacksize = 0;
            if (pthread_attr_getstacksize(in attr, ref stacksize) == 0)
            {
                if (stacksize < kMinStackSize &&
                    pthread_attr_setstacksize(ref attr, kMinStackSize) != 0)
                {
                    pthread_attr_destroy(ref attr);
                    goto Error2;
                }
            }
            pthread_mutex_lock(worker.impl_.mutex_);
            ok = B(pthread_create(ref worker.impl_.thread_, in attr, thread_loop_fn, worker) == 0);
            if (ok != 0) worker.status_ = AVX_WORKER_STATUS_OK;
            pthread_mutex_unlock(worker.impl_.mutex_);
            pthread_attr_destroy(ref attr);
            if (ok == 0)
            {
                goto Error2;
            }
        }
        else if (worker.status_ > AVX_WORKER_STATUS_OK)
        {
            ok = sync(worker);
        }
        Debug.Assert(ok == 0 || (worker.status_ == AVX_WORKER_STATUS_OK));
        return ok;

    // [port] C jumps into the `if (!ok)` block; the labels are moved after it with the same
    // statements.
    Error2:
        pthread_mutex_destroy(worker.impl_.mutex_);
        pthread_cond_destroy(worker.impl_.condition_);
    Error:
        // aom_free(worker->impl_);
        worker.impl_ = null;
        return 0;
    }

    private static void execute(AVxWorker worker)
    {
        if (worker.hook != null)
        {
            worker.had_error |= B(worker.hook(worker.data1, worker.data2) == 0);
        }
    }

    private static void launch(AVxWorker worker)
    {
        // CONFIG_MULTITHREAD
        change_state(worker, AVX_WORKER_STATUS_WORKING);
    }

    private static void end(AVxWorker worker)
    {
        // CONFIG_MULTITHREAD
        if (worker.impl_ != null)
        {
            change_state(worker, AVX_WORKER_STATUS_NOT_OK);
            pthread_join(worker.impl_.thread_, null);
            pthread_mutex_destroy(worker.impl_.mutex_);
            pthread_cond_destroy(worker.impl_.condition_);
            // aom_free(worker->impl_);
            worker.impl_ = null;
        }
        Debug.Assert(worker.status_ == AVX_WORKER_STATUS_NOT_OK);
    }

    //------------------------------------------------------------------------------

    private static readonly AVxWorkerInterface g_worker_interface = new AVxWorkerInterface
    {
        init = init,
        reset = reset,
        sync = sync,
        launch = launch,
        execute = execute,
        end = end,
    };

    internal static AVxWorkerInterface aom_get_worker_interface()
    {
        return g_worker_interface;
    }

    //------------------------------------------------------------------------------
}
