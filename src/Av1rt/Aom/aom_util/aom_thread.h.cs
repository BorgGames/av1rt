// Port of libaom aom_util/aom_thread.h
//
// Copyright 2013 Google Inc. All Rights Reserved.
//
// Use of this source code is governed by a BSD-style license
// that can be found in the COPYING file in the root of the source
// tree. An additional intellectual property rights grant can be found
// in the file PATENTS. All contributing project authors may
// be found in the AUTHORS file in the root of the source tree.
// -----------------------------------------------------------------------------
//
// Multi-threaded worker
//
// Original source:
//  https://chromium.googlesource.com/webm/libwebp

global using static Av1rt.Aom.aom_thread;
global using AVxWorkerStatus = int;

using System;

namespace Av1rt.Aom;

// Function to be called by the worker thread. Takes two opaque pointers as
// arguments (data1 and data2). Should return true on success and return false
// in case of error.
internal delegate int AVxWorkerHook(object data1, object data2);

// Platform-dependent implementation details for the worker.
// [port] struct AVxWorkerImpl is defined in aom_thread.c; it is declared here because C# has no
// incomplete types and AVxWorker names it. The pthread members are created by
// pthread_mutex_init / pthread_cond_init / pthread_create (see aom_pthread.h.cs).
internal sealed partial class AVxWorkerImpl
{
    public pthread_mutex_t mutex_;
    public pthread_cond_t condition_;
    public pthread_t thread_;
}

// Synchronization object used to launch job in the worker thread
// [port] A class: workers live in arrays (cpi->workers) and are used through pointers. Clear()
// is the `memset(worker, 0, sizeof(*worker))` of the worker interface's init().
internal sealed partial class AVxWorker
{
    public AVxWorkerImpl impl_;
    public AVxWorkerStatus status_;
    // Thread name for the debugger. If not NULL, must point to a string that
    // outlives the worker thread. For portability, use a name <= 15 characters
    // long (not including the terminating NUL character).
    public string thread_name;
    public AVxWorkerHook hook;  // hook to call
    public object data1;        // first argument passed to 'hook'
    public object data2;        // second argument passed to 'hook'
    public int had_error;       // true if a call to 'hook' returned false

    public void Clear()
    {
        impl_ = null;
        status_ = 0;
        thread_name = null;
        hook = null;
        data1 = null;
        data2 = null;
        had_error = 0;
    }
}

// The interface for all thread-worker related functions. All these functions
// must be implemented.
// [port] A class of delegates (aom_get_worker_interface returns a pointer to the one instance).
internal sealed partial class AVxWorkerInterface
{
    // Must be called first, before any other method.
    public Action<AVxWorker> init;
    // Must be called to initialize the object and spawn the thread. Re-entrant.
    // Will potentially launch the thread. Returns false in case of error.
    public Func<AVxWorker, int> reset;
    // Makes sure the previous work is finished. Returns true if worker->had_error
    // was not set and no error condition was triggered by the working thread.
    public Func<AVxWorker, int> sync;
    // Triggers the thread to call hook() with data1 and data2 arguments. These
    // hook/data1/data2 values can be changed at any time before calling this
    // function, but not be changed afterward until the next call to Sync().
    public Action<AVxWorker> launch;
    // This function is similar to launch() except that it calls the
    // hook directly instead of using a thread. Convenient to bypass the thread
    // mechanism while still using the AVxWorker structs. sync() must
    // still be called afterward (for error reporting).
    public Action<AVxWorker> execute;
    // Kill the thread and terminate the object. To use the object again, one
    // must call reset() again.
    public Action<AVxWorker> end;
}

internal static partial class aom_thread
{
    // State of the worker thread object
    // AVxWorkerStatus
    internal const int AVX_WORKER_STATUS_NOT_OK = 0;  // object is unusable
    internal const int AVX_WORKER_STATUS_OK = 1;      // ready to work
    internal const int AVX_WORKER_STATUS_WORKING = 2; // busy finishing the current task

    // [port] aom_set_worker_interface / aom_get_worker_interface are defined in aom_thread.c and
    // belong to its port.
}
