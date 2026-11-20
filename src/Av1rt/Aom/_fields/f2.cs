// Field additions from work package f2 (av1/encoder/nonrd_pickmode.c, av1/encoder/nonrd_opt.c).

namespace Av1rt.Aom;

internal sealed partial class MACROBLOCK
{
    // [port] Per-thread scratch replacing the large C stack locals of nonrd_opt.c /
    // nonrd_pickmode.c (each thread owns one MACROBLOCK). Allocated on first use; not part of
    // the C struct, so CopyFrom does not copy it.
    private NONRD_SCRATCH nonrd_scratch_;
    internal NONRD_SCRATCH nonrd_scratch => nonrd_scratch_ ??= new NONRD_SCRATCH();
}

// [port] Reusable buffers for the stack locals of the nonrd mode search. Every user
// re-initialises what C initialises (`= { 0 }` arrays are cleared on each call); buffers that
// C leaves uninitialised are only read after being written.
internal sealed class NONRD_SCRATCH
{
    // av1_block_yrd(): DECLARE_BLOCK_YRD_BUFFERS() (tran_low_t[16 * 16] used through int16_t
    // views in low bitdepth) and uint16_t eob[1].
    internal readonly short[] yrd_dqcoeff_buf = new short[16 * 16];
    internal readonly short[] yrd_qcoeff_buf = new short[16 * 16];
    internal readonly short[] yrd_coeff_buf = new short[16 * 16];
    internal readonly ushort[] yrd_eob = new ushort[1];

    // The int16_t view of macroblock_plane::coeff written by aom_process_hadamard_lp_8x16() and
    // read back by av1_block_yrd() (see the PORT-NOTE in nonrd_opt.c.cs).
    internal readonly short[] coeff_lp = new short[MAX_SB_SQUARE];

    // av1_estimate_intra_mode() / av1_nonrd_pick_intra_mode(): `RD_STATS this_rdc` (pointed to by
    // args.rdc, a Ptr<RD_STATS>) and `struct estimate_block_intra_args args`.
    internal readonly RD_STATS[] intra_this_rdc = new RD_STATS[1];
    internal readonly estimate_block_intra_args intra_args = new();

    // model_skip_for_sb_y_large() / model_skip_for_sb_y_large_64(): the `= { 0 }` local arrays.
    internal readonly uint[] sse8x8 = new uint[256];
    internal readonly int[] sum8x8 = new int[256];
    internal readonly uint[] var8x8 = new uint[256];
    internal readonly uint[] sse16x16 = new uint[64];
    internal readonly int[] sum16x16 = new int[64];
    internal readonly uint[] var16x16 = new uint[64];

    // combined_motion_search(): int cost_list[5].
    internal readonly int[] cost_list = new int[5];

    // av1_nonrd_pick_inter_mode_sb(): InterModeSearchStateNonrd search_state,
    // PRED_BUFFER tmp_buffer[4], uint8_t pred_buf[MAX_MB_PLANE * MAX_SB_SQUARE] and
    // struct scale_factors sf_no_scale.
    internal readonly InterModeSearchStateNonrd search_state = new();
    internal readonly PRED_BUFFER[] tmp_buffer = { new(), new(), new(), new() };
    internal readonly byte[] pred_buf = new byte[MAX_MB_PLANE * MAX_SB_SQUARE];
    internal readonly scale_factors sf_no_scale = new();
}
