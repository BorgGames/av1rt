// Port of libaom aom_dsp/entcode.h

global using static Av1rt.Aom.entcode;
/*OPT: od_ec_window must be at least 32 bits, but if you have fast arithmetic
   on a larger type, you can speed up the decoder by using it here.*/
global using od_ec_window = uint;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class entcode
{
    internal const int EC_PROB_SHIFT = 6;
    internal const int EC_MIN_PROB = 4;  // must be <= (1<<EC_PROB_SHIFT)/16

    /*The size in bits of od_ec_window.*/
    internal const int OD_EC_WINDOW_SIZE = sizeof(od_ec_window) * 8;

    /*The resolution of fractional-precision bit usage measurements, i.e.,
       3 => 1/8th bits.*/
    internal const int OD_BITRES = 3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int OD_ICDF(int x) => AOM_ICDF(x);

    // [subset] od_ec_tell_frac (entcode.c) never runs in the subset and is not ported.
}
