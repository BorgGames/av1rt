// Port of libaom aom_ports/bitops.h

global using static Av1rt.Aom.bitops;

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class bitops
{
    // get_msb:
    // Returns (int)floor(log2(n)). n must be > 0.
    // These versions of get_msb() are only valid when n != 0 because all
    // of the optimized versions are undefined when n == 0:
    // [port] The oracle (MSVC) uses _BitScanReverse; BitOperations.Log2 is the same for n != 0.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_msb(uint n)
    {
        Debug.Assert(n != 0);
        return BitOperations.Log2(n);
    }

    // [port] C callers pass int arguments to get_msb(unsigned int) and rely on the implicit
    // conversion; this overload performs that same conversion so they need no cast.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_msb(int n) => get_msb((uint)n);

    // [subset] aom_ceil_log2 and aom_clzll never run in the subset (palette and other tools that use
    // them are off) and are not ported.
}
