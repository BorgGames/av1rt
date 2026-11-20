using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Av1rt.Aom;
using static Av1rt.Aom.enums;

namespace Av1rt.Tests.Frame;

// Imports of tools/oracle/shim/shim_f4.c (aomshim_f4.dll): libaom's ethread.c multi-threading
// helpers (worker counts, tile/job scheduling, row synchronization, counter accumulation).
internal static class F4Shim
{
    private const string Dll = "aomshim_f4";

    [DllImport(Dll)] public static extern int aomshim_f4_num_mt_modules();
    [DllImport(Dll)] public static extern int aomshim_f4_max_num_threads();
    [DllImport(Dll)] public static extern int aomshim_f4_compute_num_workers(int mi_rows, int mi_cols, int mib_size_log2, int tile_cols, int tile_rows, int[] col_start_sb, int[] row_start_sb, int max_threads, int row_mt, int pass, int frame_enc_workers, int[] num_mod_workers, out int weber_bsize);
    [DllImport(Dll)] public static extern void aomshim_f4_init_frame_mt(int num_workers, int[] p_num_mod_workers, int[] num_mod_workers, int[] alloc_workers);
    [DllImport(Dll)] public static extern void aomshim_f4_max_sb_rows_cols(int mi_rows, int mi_cols, int mib_size_log2, int tile_cols, int tile_rows, int[] col_start_sb, int[] row_start_sb, out int max_sb_rows, out int max_sb_cols);
    [DllImport(Dll)] public static extern void aomshim_f4_schedule(int mi_rows, int mi_cols, int mib_size_log2, int tile_cols, int tile_rows, int[] col_start_sb, int[] row_start_sb, int num_workers, int[] ops, int num_ops, int[] thread_id_to_tile_id, int[] @out, int[] tile_state);
    [DllImport(Dll)] public static extern void aomshim_f4_row_sync_write(int rows, int sync_range, int intrabc_delay, int cols, int[] ops, int num_ops, int[] @out);
    [DllImport(Dll)] public static extern int aomshim_f4_frame_counts_size();
    [DllImport(Dll)] public static extern int aomshim_f4_accumulate_frame_counts(uint[] acc, uint[] counts, int n);
    [DllImport(Dll)] public static extern void aomshim_f4_pack_bs_tile_order(ulong[] abs_sum_level, int num_tiles, ushort[] tile_idx_out);
}

// Delegates for the file-static ethread.c functions of the port (private in C#), bound by
// reflection.
internal delegate void AssignTileToThreadFn(Ptr<int> thread_id_to_tile_id, int num_tiles, int num_workers);
internal delegate int GetNextJobFn(TileDataEnc tile_data, ref int current_mi_row, int mib_size);
internal delegate void SwitchTileAndGetNextJobFn(AV1_COMMON cm, TileDataEnc[] tile_data, ref int cur_tile_id,
    ref int current_mi_row, ref int end_of_frame, int is_firstpass, int fp_block_size);
internal delegate void ComputeMaxSbRowsColsFn(AV1_COMMON cm, ref int max_sb_rows_in_tile, ref int max_sb_cols_in_tile);
internal delegate void RowMtSyncMemAllocFn(AV1EncRowMultiThreadSync row_mt_sync, AV1_COMMON cm, int rows);

internal static class EthreadPrivate
{
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;

    private static T Bind<T>(string name) where T : Delegate =>
        (typeof(ethread).GetMethod(name, Flags) ?? throw new MissingMethodException(nameof(ethread), name))
            .CreateDelegate<T>();

    public static readonly AssignTileToThreadFn assign_tile_to_thread = Bind<AssignTileToThreadFn>("assign_tile_to_thread");
    public static readonly GetNextJobFn get_next_job = Bind<GetNextJobFn>("get_next_job");
    public static readonly SwitchTileAndGetNextJobFn switch_tile_and_get_next_job =
        Bind<SwitchTileAndGetNextJobFn>("switch_tile_and_get_next_job");
    public static readonly ComputeMaxSbRowsColsFn compute_max_sb_rows_cols = Bind<ComputeMaxSbRowsColsFn>("compute_max_sb_rows_cols");
    public static readonly RowMtSyncMemAllocFn row_mt_sync_mem_alloc = Bind<RowMtSyncMemAllocFn>("row_mt_sync_mem_alloc");

    public static readonly Comparison<PackBSTileOrder> compare_tile_order_fn =
        (Comparison<PackBSTileOrder>)(typeof(ethread).GetField("compare_tile_order_fn", Flags)
            ?? throw new MissingFieldException(nameof(ethread), "compare_tile_order_fn")).GetValue(null)!;
}

// A frame size, superblock size and explicit tile layout, as the shim's create_cpi() sets them.
internal sealed class FrameLayout
{
    public int mi_rows, mi_cols, mib_size_log2, tile_cols, tile_rows;
    public int[] col_start_sb = Array.Empty<int>(), row_start_sb = Array.Empty<int>();

    public int SbRows => (mi_rows + (1 << mib_size_log2) - 1) >> mib_size_log2;
    public int SbCols => (mi_cols + (1 << mib_size_log2) - 1) >> mib_size_log2;
    public int NumTiles => tile_cols * tile_rows;

    public static FrameLayout Random(Random rng, int max_mi_rows, int max_mi_cols, int max_tile_cols, int max_tile_rows)
    {
        var l = new FrameLayout
        {
            mib_size_log2 = rng.Next(2) == 0 ? 4 : 5,
            mi_rows = rng.Next(1, max_mi_rows + 1),
            mi_cols = rng.Next(1, max_mi_cols + 1),
        };
        l.tile_cols = rng.Next(1, Math.Min(max_tile_cols, l.SbCols) + 1);
        l.tile_rows = rng.Next(1, Math.Min(max_tile_rows, l.SbRows) + 1);
        l.col_start_sb = Starts(rng, l.SbCols, l.tile_cols);
        l.row_start_sb = Starts(rng, l.SbRows, l.tile_rows);
        return l;
    }

    // tiles + 1 strictly increasing tile starts from 0 to n_sb (random interior cuts).
    private static int[] Starts(Random rng, int n_sb, int tiles)
    {
        var cuts = new int[n_sb - 1];
        for (int i = 0; i < cuts.Length; i++) cuts[i] = i + 1;
        for (int i = cuts.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (cuts[i], cuts[j]) = (cuts[j], cuts[i]);
        }
        var starts = new int[tiles + 1];
        Array.Copy(cuts, 0, starts, 1, tiles - 1);
        Array.Sort(starts, 1, tiles - 1);
        starts[tiles] = n_sb;
        return starts;
    }

    // C# mirror of create_cpi() in shim_f4.c.
    public AV1_COMP CreateCpi()
    {
        var cpi = new AV1_COMP { ppi = new AV1_PRIMARY() };
        AV1_COMMON cm = cpi.common;
        cm.seq_params = cpi.ppi.seq_params;
        cm.seq_params.mib_size_log2 = mib_size_log2;
        cm.seq_params.mib_size = 1 << mib_size_log2;
        cm.seq_params.sb_size = mib_size_log2 == 5 ? BLOCK_128X128 : BLOCK_64X64;
        cm.mi_params.mi_rows = mi_rows;
        cm.mi_params.mi_cols = mi_cols;
        cm.tiles.cols = tile_cols;
        cm.tiles.rows = tile_rows;
        Array.Copy(col_start_sb, cm.tiles.col_start_sb, tile_cols + 1);
        Array.Copy(row_start_sb, cm.tiles.row_start_sb, tile_rows + 1);
        return cpi;
    }

    public override string ToString() =>
        $"mi {mi_rows}x{mi_cols} log2 {mib_size_log2} tiles {tile_cols}x{tile_rows} " +
        $"cols [{string.Join(",", col_start_sb)}] rows [{string.Join(",", row_start_sb)}]";
}
