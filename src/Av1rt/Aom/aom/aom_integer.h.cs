// Port of libaom aom/aom_integer.h

global using static Av1rt.Aom.aom_integer;

namespace Av1rt.Aom;

// [port] The fixed-width integer types map to the C# primitives (PORTING 3.1). The functions
// declared here (aom_uleb_size_in_bytes, aom_uleb_decode, aom_uleb_encode,
// aom_uleb_encode_fixed_size) are defined in aom/src/aom_integer.c and belong to its port,
// which adds them to this class.
internal static partial class aom_integer
{
}
