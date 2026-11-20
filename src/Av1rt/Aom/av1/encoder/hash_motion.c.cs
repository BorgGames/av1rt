// Port of libaom av1/encoder/hash_motion.c
namespace Av1rt.Aom;

internal static partial class hash_motion
{
    // [subset] Nothing to port. hash_motion.c holds the intra block copy hash tables. In the
    // subset only av1_hash_tables_destroy (and its helper free_tile_tables) ever run, on
    // encoder teardown, to free IntraBCHashInfo buffers that were never allocated because
    // intraBC is off. IntraBCHashInfo and MACROBLOCK::intrabc_hash_info are dropped (see
    // hash_motion.h.cs), so the teardown call site (encoder_alloc.h) has nothing to free and
    // calls nothing. Every other function in the file never runs.
}
