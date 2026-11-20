// Port of libaom aom_mem/aom_mem.c

namespace Av1rt.Aom;

// [port] Nothing to port. aom_memalign / aom_malloc / aom_calloc / aom_free and their helpers
// (GetAllocationPaddingSize, check_size_argument_overflow, GetMallocAddressLocation,
// SetActualMallocAddress, GetActualMallocAddress) manage raw, over-allocated and aligned C heap
// blocks. Per PORTING 4 every call site allocates `new T[n]` (zeroed, element counts) and frees by
// dropping the reference, and Ptr<T> offsets stand in for aligned addresses (aom_align_addr in
// aom_mem.h.cs). The size check of check_size_argument_overflow (AOM_MAX_ALLOCABLE_MEMORY) has no
// effect in the subset: no allocation comes near 8 GiB.
internal static partial class aom_mem
{
}
