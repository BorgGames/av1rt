// Field additions from work package f3 (var_based_part.c / partition_search.c / context_tree.c).
namespace Av1rt.Aom;

internal sealed partial class ThreadData
{
    // [port] Per-thread scratch for av1_choose_var_based_partitioning() (var_based_part.c):
    // the C stack locals and the per-superblock aom_malloc'd VP128x128, reused across calls.
    // Created on first use.
    public VarBasedPartScratch vbp_scratch;
}
