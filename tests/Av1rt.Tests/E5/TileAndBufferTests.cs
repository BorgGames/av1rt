using System;
using System.Collections.Generic;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.alloccommon;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.mem;
using static Av1rt.Aom.aom_dsp_common;
using static Av1rt.Aom.tile_common;
using static Av1rt.Aom.timing;

namespace Av1rt.Tests.E5;

// Differential tests of tile_common.c, alloccommon.c (CDEF buffer sizing, av1_get_MBs) and
// timing.c against libaom.
[Trait("Category", "Native")]
public class TileAndBufferTests
{
    private static readonly (int w, int h)[] FrameSizes =
    {
        (3840, 2160), (1920, 1080), (1366, 766), (1280, 720), (352, 288), (8, 8), (1, 1),
        (4097, 64), (7680, 4320), (8192, 300), (17, 4000), (65, 33),
    };

    private static int MiCols(int w) => ((w + 7) & ~7) >> 2;
    private static int MiRows(int h) => ((h + 7) & ~7) >> 2;

    private static void CheckTiles(int mi_rows, int mi_cols, int mib_size_log2, int uniform,
                                   int log2_cols, int log2_rows, int[] colStarts, int[] rowStarts)
    {
        int ncols = colStarts == null ? 0 : colStarts.Length - 1;
        int nrows = rowStarts == null ? 0 : rowStarts.Length - 1;
        int[] eCol = new int[MAX_TILE_COLS + 1], eRow = new int[MAX_TILE_ROWS + 1];
        if (colStarts != null) Array.Copy(colStarts, eCol, colStarts.Length);
        if (rowStarts != null) Array.Copy(rowStarts, eRow, rowStarts.Length);
        int[] eParams = new int[16];
        int[] eTiles = new int[8 * MAX_TILE_COLS * MAX_TILE_ROWS];
        int n = E5Shim.aomshim_e5_tiles(mi_rows, mi_cols, mib_size_log2, uniform, log2_cols, log2_rows,
                                        ncols, nrows, eCol, eRow, eParams, eTiles, MAX_TILE_COLS * MAX_TILE_ROWS);
        Assert.True(n > 0);

        var cm = new AV1_COMMON();
        cm.seq_params = new SequenceHeader();
        cm.seq_params.mib_size_log2 = mib_size_log2;
        cm.seq_params.mib_size = 1 << mib_size_log2;
        cm.mi_params.mi_rows = mi_rows;
        cm.mi_params.mi_cols = mi_cols;
        cm.width = mi_cols * 4;
        cm.superres_upscaled_width = cm.width;

        av1_get_tile_limits(cm);
        CommonTileParams t = cm.tiles;
        t.uniform_spacing = uniform;
        if (uniform != 0)
        {
            t.log2_cols = clamp(log2_cols, t.min_log2_cols, t.max_log2_cols);
        }
        else
        {
            t.cols = ncols;
            for (int i = 0; i <= ncols; i++) t.col_start_sb[i] = colStarts[i];
        }
        av1_calculate_tile_cols(cm.seq_params, mi_rows, mi_cols, t);
        if (uniform != 0)
        {
            t.log2_rows = clamp(log2_rows, t.min_log2_rows, t.max_log2_rows);
        }
        else
        {
            t.rows = nrows;
            for (int i = 0; i <= nrows; i++) t.row_start_sb[i] = rowStarts[i];
        }
        av1_calculate_tile_rows(cm.seq_params, mi_rows, t);

        int[] aParams =
        {
            t.cols, t.rows, t.max_width_sb, t.max_height_sb, t.min_inner_width, t.log2_cols,
            t.log2_rows, t.width, t.height, t.min_log2_cols, t.min_log2_rows, t.max_log2_cols,
            t.max_log2_rows, t.min_log2, t.uniform_spacing, av1_is_min_tile_width_satisfied(cm),
        };
        Assert.Equal(eParams, aParams);
        for (int i = 0; i <= t.cols; i++) Assert.Equal(eCol[i], t.col_start_sb[i]);
        for (int i = 0; i <= t.rows; i++) Assert.Equal(eRow[i], t.row_start_sb[i]);

        int k = 0;
        for (int r = 0; r < t.rows; r++)
        {
            for (int c = 0; c < t.cols; c++)
            {
                TileInfo tile = default;
                av1_tile_init(ref tile, cm, r, c);
                int[] actual =
                {
                    tile.mi_row_start, tile.mi_row_end, tile.mi_col_start, tile.mi_col_end,
                    tile.tile_row, tile.tile_col, av1_get_sb_rows_in_tile(cm, in tile),
                    av1_get_sb_cols_in_tile(cm, in tile),
                };
                Assert.Equal(eTiles.AsSpan(8 * k, 8).ToArray(), actual);
                k++;
            }
        }
        Assert.Equal(n, k);
    }

    [Fact]
    public void UniformTileLayoutMatchesLibaom()
    {
        foreach (var (w, h) in FrameSizes)
        {
            foreach (int mib_size_log2 in new[] { 4, 5 })
            {
                for (int log2_cols = 0; log2_cols <= 6; log2_cols++)
                {
                    for (int log2_rows = 0; log2_rows <= 6; log2_rows += 2)
                    {
                        CheckTiles(MiRows(h), MiCols(w), mib_size_log2, 1, log2_cols, log2_rows, null, null);
                    }
                }
            }
        }
    }

    [Fact]
    public void NonUniformTileLayoutMatchesLibaom()
    {
        var rng = new Random(541);
        foreach (var (w, h) in FrameSizes)
        {
            foreach (int mib_size_log2 in new[] { 4, 5 })
            {
                int sb_cols = (MiCols(w) + (1 << mib_size_log2) - 1) >> mib_size_log2;
                int sb_rows = (MiRows(h) + (1 << mib_size_log2) - 1) >> mib_size_log2;
                for (int trial = 0; trial < 4; trial++)
                {
                    CheckTiles(MiRows(h), MiCols(w), mib_size_log2, 0, 0, 0,
                               RandomStarts(rng, sb_cols, MAX_TILE_COLS),
                               RandomStarts(rng, sb_rows, MAX_TILE_ROWS));
                }
            }
        }
    }

    // Strictly increasing tile start positions 0 = s[0] < ... < s[n] = sb_count.
    private static int[] RandomStarts(Random rng, int sb_count, int max_tiles)
    {
        var starts = new List<int> { 0 };
        int pos = 0;
        while (starts.Count <= max_tiles - 1)
        {
            pos += 1 + rng.Next(Math.Max(1, sb_count / 3));
            if (pos >= sb_count) break;
            starts.Add(pos);
        }
        starts.Add(sb_count);
        return starts.ToArray();
    }

    [Fact]
    public void GetMBsMatchesLibaom()
    {
        foreach (var (w, h) in FrameSizes)
        {
            Assert.Equal(E5Shim.aomshim_e5_get_MBs(w, h), av1_get_MBs(w, h));
            Assert.Equal(E5Shim.aomshim_e5_get_MBs(w + 3, h + 5), av1_get_MBs(w + 3, h + 5));
        }
    }

    [Fact]
    public void MaxLevelBitrateMatchesLibaom()
    {
        for (int profile = 0; profile < 8; profile++)
            for (int level = 0; level < 32; level++)
                for (int tier = 0; tier < 2; tier++)
                    Assert.Equal(E5Shim.aomshim_e5_max_level_bitrate(profile, level, tier),
                                 av1_max_level_bitrate(profile, level, tier));
    }

    [Fact]
    public void CdefBufferAllocationMatchesLibaom()
    {
        // (mi_rows, mi_cols, num_workers, ss_x, enable_cdef) steps run on one AV1_COMMON, so the
        // reallocation paths (size change, worker count change, cdef toggled) all run.
        int[][] steps =
        {
            new[] { MiRows(288), MiCols(352), 1, 1, 1 },
            new[] { MiRows(288), MiCols(352), 1, 1, 1 },
            new[] { MiRows(1080), MiCols(1920), 4, 1, 1 },
            new[] { MiRows(1080), MiCols(1920), 4, 1, 1 },
            new[] { MiRows(2160), MiCols(3840), 8, 1, 1 },
            new[] { MiRows(2160), MiCols(3840), 2, 1, 1 },
            new[] { MiRows(766), MiCols(1366), 2, 1, 0 },
            new[] { MiRows(766), MiCols(1366), 3, 0, 1 },
            new[] { MiRows(720), MiCols(1280), 1, 1, 1 },
        };
        int[] cfg = new int[5 * steps.Length];
        for (int s = 0; s < steps.Length; s++) Array.Copy(steps[s], 0, cfg, 5 * s, 5);
        int[] expected = new int[10 * steps.Length];
        Assert.Equal(steps.Length, E5Shim.aomshim_e5_cdef_alloc(steps.Length, cfg, expected));

        var cm = new AV1_COMMON();
        cm.seq_params = new SequenceHeader();
        var sync = new AV1CdefSync();
        AV1CdefWorkerData[] workers = null;
        for (int s = 0; s < steps.Length; s++)
        {
            int[] c = steps[s];
            cm.mi_params.mi_rows = c[0];
            cm.mi_params.mi_cols = c[1];
            cm.seq_params.subsampling_x = c[3];
            cm.seq_params.subsampling_y = c[3];
            cm.seq_params.enable_cdef = (byte)c[4];
            av1_alloc_cdef_buffers(cm, ref workers, sync, c[2], 1);
            CdefInfo ci = cm.cdef_info;
            int[] actual =
            {
                ci.allocated_srcbuf_size,
                ci.allocated_colbuf_size[0], ci.allocated_colbuf_size[1], ci.allocated_colbuf_size[2],
                ci.allocated_linebuf_size[0], ci.allocated_linebuf_size[1], ci.allocated_linebuf_size[2],
                ci.allocated_mi_rows, ci.allocated_num_workers, workers != null ? 1 : 0,
            };
            Assert.Equal(expected.AsSpan(10 * s, 10).ToArray(), actual);

            // The buffers hold the C byte sizes in uint16_t elements.
            if (c[4] != 0)
            {
                Assert.Equal(ci.allocated_srcbuf_size / 2, ci.srcbuf.Array.Length);
                for (int p = 0; p < 3; p++)
                {
                    Assert.Equal(ci.allocated_colbuf_size[p] / 2, ci.colbuf[p].Array.Length);
                    Assert.Equal(ci.allocated_linebuf_size[p] / 2, ci.linebuf[p].Array.Length);
                }
                if (c[2] > 1)
                {
                    Assert.Equal(c[2], workers.Length);
                    for (int i = 1; i < c[2]; i++)
                        Assert.Equal(ci.allocated_colbuf_size[0] / 2, workers[i].colbuf[0].Array.Length);
                }
                // The row sync exists only for multi-threaded CDEF.
                if (c[2] > 1)
                {
                    Assert.Equal(ci.allocated_mi_rows, sync.cdef_row_mt.Array.Length);
                    Assert.NotNull(sync.cdef_row_mt[0].row_mutex_);
                    Assert.NotNull(sync.cdef_row_mt[0].row_cond_);
                }
            }
        }
        av1_free_cdef_buffers(cm, ref workers, sync);
        Assert.Null(workers);
        Assert.True(sync.cdef_row_mt.IsNull);
        Assert.True(cm.cdef_info.srcbuf.IsNull);
    }

    [Fact]
    public void ContextBufferAllocation()
    {
        // av1_alloc_context_buffers through the encoder's mi callbacks (encoder_utils.h).
        var cm = new AV1_COMMON();
        CommonModeInfoParams mi = cm.mi_params;
        mi.free_mi = enc_free_mi_fn;
        mi.setup_mi = enc_setup_mi_fn;
        mi.set_mb_mi = enc_set_mb_mi_fn;
        Assert.Equal(0, av1_alloc_context_buffers(cm, 1366, 766, BLOCK_8X8));
        Assert.Equal(MiCols(1366), mi.mi_cols);
        Assert.Equal(MiRows(766), mi.mi_rows);
        Assert.Equal(mi.mi_alloc_size, mi.mi_alloc.Length);
        Assert.All(mi.mi_alloc, m => Assert.NotNull(m));
        Assert.Equal(mi.mi_grid_size, mi.mi_grid_base.Length);
        Assert.Equal(mi.mi_grid_size, mi.tx_type_map.Array.Length);
        Assert.Equal(av1_get_MBs(1366, 766), mi.MBs);

        Assert.Equal(0, av1_alloc_above_context_buffers(cm.above_contexts, 2, mi.mi_cols, 3));
        Assert.Equal(ALIGN_POWER_OF_TWO(mi.mi_cols, MAX_MIB_SIZE_LOG2), cm.above_contexts.num_mi_cols);
        Assert.Equal(cm.above_contexts.num_mi_cols, cm.above_contexts.entropy[2][1].Array.Length);

        av1_remove_common(cm);
        Assert.Null(mi.mi_alloc);
        Assert.Null(cm.above_contexts.partition);
        Assert.Null(cm.above_contexts.entropy[0]);
        Assert.Equal(0, cm.above_contexts.num_planes);
    }

    private static readonly Action<CommonModeInfoParams> enc_free_mi_fn = encoder_utils.enc_free_mi;
    private static readonly Action<CommonModeInfoParams> enc_setup_mi_fn = encoder_utils.enc_setup_mi;
    private static readonly Action<CommonModeInfoParams, int, int, int> enc_set_mb_mi_fn = encoder_utils.enc_set_mb_mi;
}
