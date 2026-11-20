// Port of libaom av1/encoder/hash_motion.h
global using static Av1rt.Aom.hash_motion;

namespace Av1rt.Aom;

internal static partial class hash_motion
{
    // Block size used for force_integer_mv decisions
    internal const int FORCE_INT_MV_DECISION_BLOCK_SIZE = 8;

    // The most tables built for a frame, to keep the memory down. Frames with more
    // tiles than this let neighboring tiles share a table.
    internal const int MAX_INTRABC_HASH_TABLES = 16;

    // [subset] block_hash, IntraBCHashRegion, hash_table, IntraBCHashInfo and
    // av1_intrabc_hash_table_idx: intraBC hash search is off in the subset, and
    // MACROBLOCK::intrabc_hash_info is dropped. The executed C code that only allocates and
    // frees those buffers (encoder.c, encoder_alloc.h, ethread.c, hash_motion.c
    // av1_hash_tables_destroy) has nothing to do.
}
