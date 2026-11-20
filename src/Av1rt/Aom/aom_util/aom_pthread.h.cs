// Port of libaom aom_util/aom_pthread.h

global using static Av1rt.Aom.aom_pthread;
global using pthread_attr_t = int;

using System.Runtime.CompilerServices;
using System.Threading;

namespace Av1rt.Aom;

// [port] CONFIG_MULTITHREAD on Windows without pthread.h: libaom's "simplistic pthread emulation
// layer" over Win32 threads, SRW locks and condition variables. The port keeps the pthread API
// that the encoder calls and implements it with .NET threading primitives:
//   pthread_t       -> a Thread (started with ThreadPriority.AboveNormal, as SetThreadPriority)
//   pthread_mutex_t -> a System.Threading.Lock
//   pthread_cond_t  -> a Monitor wait/pulse object
// All three are classes. Their C storage is embedded in structs or allocated in arrays and then
// initialized in place (`pthread_mutex_init(&s->mutex, NULL)`); in the port the *_init functions
// take the field or array element by `ref` and create the object, and every other function
// takes the object (C's `&s->mutex` / `&mutexes[i]` become `s.mutex` / `mutexes[i]`).

// [port] HANDLE.
internal sealed partial class pthread_t
{
    public readonly Thread thread;

    public pthread_t(Thread thread)
    {
        this.thread = thread;
    }
}

// [port] SRWLOCK.
internal sealed partial class pthread_mutex_t
{
    public readonly Lock @lock = new();
}

// [port] CONDITION_VARIABLE. pthread_cond_wait enters this object's monitor before releasing the
// mutex and waits on it, and signal/broadcast pulse under the same monitor, so a wakeup issued
// after the waiter released the mutex cannot be lost (the SleepConditionVariableSRW contract).
// Each signal/broadcast also bumps `generation`, which a waiter watches while it spins before
// blocking (see pthread_cond_wait).
internal sealed partial class pthread_cond_t
{
    public readonly object sync = new();
    public int generation;
}

// _beginthreadex requires __stdcall
// [port] THREADFN: `unsigned int __stdcall start(void *arg)`.
internal delegate uint pthread_start_routine(object arg);

internal static partial class aom_pthread
{
    internal const uint THREAD_EXIT_SUCCESS = 0;

    // errno.h
    internal const int EINVAL = 22;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int pthread_attr_init(ref pthread_attr_t attr)
    {
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int pthread_attr_destroy(ref pthread_attr_t attr)
    {
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int pthread_attr_getstacksize(in pthread_attr_t attr, ref int stacksize)
    {
        return EINVAL;
    }

    // [port] Never reached (pthread_attr_getstacksize fails first), but aom_thread.c names it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int pthread_attr_setstacksize(ref pthread_attr_t attr, int stacksize)
    {
        return EINVAL;
    }

    internal static int pthread_create(ref pthread_t thread, in pthread_attr_t attr,
                                       pthread_start_routine start, object arg)
    {
        // [port] _beginthreadex(NULL, 0, start, arg, 0, NULL) and
        // SetThreadPriority(*thread, THREAD_PRIORITY_ABOVE_NORMAL). A background thread, so
        // that like a C thread it does not keep the process alive.
        Thread t = new Thread(() => start(arg))
        {
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
        };
        thread = new pthread_t(t);
        t.Start();
        return 0;
    }

    // [port] `void **value_ptr` is ignored (callers pass NULL).
    internal static int pthread_join(pthread_t thread, object value_ptr)
    {
        thread.thread.Join();
        return 0;
    }

    // Mutex
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int pthread_mutex_init(ref pthread_mutex_t mutex, object mutexattr)
    {
        mutex = new pthread_mutex_t();
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int pthread_mutex_lock(pthread_mutex_t mutex)
    {
        mutex.@lock.Enter();
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int pthread_mutex_unlock(pthread_mutex_t mutex)
    {
        mutex.@lock.Exit();
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int pthread_mutex_destroy(pthread_mutex_t mutex)
    {
        return 0;
    }

    // Condition
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int pthread_cond_destroy(pthread_cond_t condition)
    {
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int pthread_cond_init(ref pthread_cond_t condition, object cond_attr)
    {
        condition = new pthread_cond_t();
        return 0;
    }

    internal static int pthread_cond_signal(pthread_cond_t condition)
    {
        lock (condition.sync)
        {
            condition.generation++;
            Monitor.Pulse(condition.sync);
        }
        return 0;
    }

    internal static int pthread_cond_broadcast(pthread_cond_t condition)
    {
        lock (condition.sync)
        {
            condition.generation++;
            Monitor.PulseAll(condition.sync);
        }
        return 0;
    }

    // [port] How long pthread_cond_wait spins before it blocks. The encoder's waits are mostly
    // short hand-offs (a worker waiting for the row above to advance a few superblocks, the
    // workers waiting for the next stage of the same frame, the main thread waiting for the
    // last worker), while waking a blocked thread costs tens to hundreds of microseconds on
    // Windows, more when the power plan parks idle cores (measured about 0.5 ms per worker
    // launch on a 6-core desktop in the Balanced plan, several percent of a 4K frame).
    private static readonly long s_condSpinTicks = System.Diagnostics.Stopwatch.Frequency / 5000;  // 200 us

    internal static int pthread_cond_wait(pthread_cond_t condition, pthread_mutex_t mutex)
    {
        // Spin while the generation is unchanged, then block. pthread_cond_wait may return
        // without a matching signal (callers recheck their predicate in a loop), so returning
        // as soon as any signal or broadcast happened is enough. The blocking path rechecks the
        // generation under the monitor, so a signal between the spin and Monitor.Wait is kept.
        int generation = Volatile.Read(ref condition.generation);
        mutex.@lock.Exit();
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        do
        {
            for (int i = 0; i < 64; i++)
            {
                if (Volatile.Read(ref condition.generation) != generation) goto Signaled;
                Thread.SpinWait(8);
            }
            // Let another thread that is ready on this processor run (SwitchToThread).
            Thread.Yield();
        } while (System.Diagnostics.Stopwatch.GetTimestamp() - start < s_condSpinTicks);
        lock (condition.sync)
        {
            if (condition.generation == generation) Monitor.Wait(condition.sync);
        }
    Signaled:
        mutex.@lock.Enter();
        return 0;
    }
}
