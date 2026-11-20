// Port of libaom aom_ports/aom_once.h

global using static Av1rt.Aom.aom_once_h;

using System;
using System.Collections.Generic;

namespace Av1rt.Aom;

// [port] The class is named aom_once_h because its member aom_once may not share the class name.
internal static partial class aom_once_h
{
    // [port] C (Windows, CONFIG_MULTITHREAD) keeps one `static INIT_ONCE aom_init_once` per
    // translation unit that includes this header, and every such unit passes a single function
    // (av1_rtcd.c, aom_dsp_rtcd.c, aom_scale_rtcd.c, reconintra.c: their own file-static
    // setup function). Keying the once-flag by the delegate gives the same behaviour. The lock
    // also makes concurrent callers wait until func has completed, like InitOnceBeginInitialize.
    private static readonly object s_lock = new();
    private static readonly HashSet<Action> s_done = new();

    internal static void aom_once(Action func)
    {
        lock (s_lock)
        {
            if (s_done.Contains(func)) return;
            func();
            s_done.Add(func);
        }
    }
}
