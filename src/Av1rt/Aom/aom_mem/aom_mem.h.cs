// Port of libaom aom_mem/aom_mem.h (and aom_mem/aom_mem.c)

global using static Av1rt.Aom.aom_mem;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] aom_mem.c (aom_memalign, aom_malloc, aom_calloc, aom_free and their address-storage
// helpers) has no C# counterpart: per PORTING 4, aom_malloc/aom_calloc/aom_memalign become
// `new T[n]` (zeroed, element counts instead of bytes) and aom_free(p) becomes `p = null`.
// Managed allocations either succeed or throw, so the NULL checks after them never fire.
internal static partial class aom_mem
{
    // [port] SIZE_MAX > (1ULL << 32) on the 64-bit oracle build.
    internal const ulong AOM_MAX_ALLOCABLE_MEMORY = 8589934592UL;

    // [subset] aom_memset16 is only used by high bitdepth code and is not ported.

    // aom_align_addr(addr, align) rounds a pointer up to a multiple of align.
    // [port] A Ptr<byte> has no address. Every aligned buffer in libaom comes from
    // aom_memalign(32, ...), so for align <= 32 aligning the address is the same as aligning
    // the offset into the allocation, which is what this does. Larger alignments would depend on
    // where malloc placed the block and do not occur in the subset (byte_alignment is 0).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<byte> aom_align_addr(Ptr<byte> addr, int align)
    {
        Debug.Assert(align > 0 && align <= 32 && (align & (align - 1)) == 0);
        return new Ptr<byte>(addr.Array, (addr.Offset + (align - 1)) & ~(align - 1));
    }

    // AOM_CHECK_MEM_ERROR(error_info, lval, expr): lval = expr, and an AOM_CODEC_MEM_ERROR if the
    // allocation returned NULL.
    // [port] Use as AOM_CHECK_MEM_ERROR(error_info, ref lval, new T[n]). A managed allocation never
    // returns null (it throws OutOfMemoryException), so the error path only fires if expr itself
    // yields null; it raises AomException as aom_internal_error does (PORTING 5). C's
    // assert((error_info)->setjmp) is not kept: the port uses exceptions instead of setjmp, so
    // the flag is bookkeeping only.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AOM_CHECK_MEM_ERROR<T>(aom_internal_error_info error_info, ref T lval, T expr,
        [CallerArgumentExpression(nameof(lval))] string lval_expr = "")
        where T : class
    {
        lval = expr;
        if (lval == null)
            throw new AomException(AOM_CODEC_MEM_ERROR, "Failed to allocate " + lval_expr);
    }
}
