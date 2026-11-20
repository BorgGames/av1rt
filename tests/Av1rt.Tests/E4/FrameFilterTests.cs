using System;
using System.Collections.Generic;
using System.Threading;
using Av1rt.Aom;
using Xunit;

#nullable enable
using static Av1rt.Aom.av1_loopfilter;
using static Av1rt.Aom.cdef;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.pickcdef;
using static Av1rt.Aom.thread_common;

namespace Av1rt.Tests.E4;

/// <summary>
/// Frame-level deblocking (av1_loopfilter.c + the single-thread driver of thread_common.c) and
/// CDEF (cdef.c + cdef_block.c) on random 8-bit 4:2:0 frames with random mode info, against
/// libaom's av1_loop_filter_frame_mt / av1_cdef_frame.
/// </summary>
public class FrameFilterTests
{
    // BLOCK_SIZE by (log2 width, log2 height) in mi units; -1 where AV1 has no such block.
    private static readonly int[,] BlockByDims =
    {
        //  h: 4    8   16   32   64  128
        { BLOCK_4X4, BLOCK_4X8, BLOCK_4X16, -1, -1, -1 },          // w 4
        { BLOCK_8X4, BLOCK_8X8, BLOCK_8X16, BLOCK_8X32, -1, -1 },  // w 8
        { BLOCK_16X4, BLOCK_16X8, BLOCK_16X16, BLOCK_16X32, BLOCK_16X64, -1 },
        { -1, BLOCK_32X8, BLOCK_32X16, BLOCK_32X32, BLOCK_32X64, -1 },
        { -1, -1, BLOCK_64X16, BLOCK_64X32, BLOCK_64X64, BLOCK_64X128 },
        { -1, -1, -1, -1, BLOCK_128X64, BLOCK_128X128 },
    };

    private sealed class ModeInfo
    {
        public int MiRows, MiCols, Sb128;
        public int[] MiBlock = Array.Empty<int>();
        public readonly List<byte> BSize = new(), TxSize = new(), Skip = new(), Mode = new(), Segment = new();
        public readonly List<sbyte> Ref0 = new();
    }

    private static void AddBlock(Random rng, ModeInfo mi, int row, int col, int wl, int hl)
    {
        if (row >= mi.MiRows || col >= mi.MiCols) return;
        int bsize = BlockByDims[wl, hl];
        Assert.True(bsize >= 0);
        int id = mi.BSize.Count;
        bool inter = rng.Next(10) < 6;
        int tx = max_txsize_rect_lookup[bsize];
        for (int depth = rng.Next(3); depth > 0; depth--) tx = sub_tx_size_map[tx];
        mi.BSize.Add((byte)bsize);
        mi.TxSize.Add((byte)tx);
        mi.Skip.Add((byte)(rng.Next(10) < 3 ? 1 : 0));
        mi.Ref0.Add((sbyte)(inter ? rng.Next(1, 8) : 0));
        mi.Mode.Add((byte)(inter ? rng.Next(13, 25) : rng.Next(0, 13)));
        mi.Segment.Add((byte)rng.Next(8));
        for (int r = row; r < Math.Min(row + (1 << hl), mi.MiRows); r++)
            for (int c = col; c < Math.Min(col + (1 << wl), mi.MiCols); c++)
                mi.MiBlock[r * mi.MiCols + c] = id;
    }

    private static void Partition(Random rng, ModeInfo mi, int row, int col, int n)
    {
        if (row >= mi.MiRows || col >= mi.MiCols) return;
        int half = n > 0 ? 1 << (n - 1) : 0;
        int choice = rng.Next(n == 0 ? 1 : n >= 2 && n <= 4 ? 6 : 4);
        if (n == 5 && choice == 0) choice = 3; // 128x128 blocks are rare: mostly split
        switch (choice)
        {
            case 0: AddBlock(rng, mi, row, col, n, n); break;
            case 1: AddBlock(rng, mi, row, col, n, n - 1); AddBlock(rng, mi, row + half, col, n, n - 1); break;
            case 2: AddBlock(rng, mi, row, col, n - 1, n); AddBlock(rng, mi, row, col + half, n - 1, n); break;
            case 3:
                Partition(rng, mi, row, col, n - 1);
                Partition(rng, mi, row, col + half, n - 1);
                Partition(rng, mi, row + half, col, n - 1);
                Partition(rng, mi, row + half, col + half, n - 1);
                break;
            case 4:
                for (int i = 0; i < 4; i++) AddBlock(rng, mi, row + i * (1 << (n - 2)), col, n, n - 2);
                break;
            default:
                for (int i = 0; i < 4; i++) AddBlock(rng, mi, row, col + i * (1 << (n - 2)), n - 2, n);
                break;
        }
    }

    private static ModeInfo RandomModeInfo(Random rng, int miRows, int miCols, int sb128)
    {
        var mi = new ModeInfo { MiRows = miRows, MiCols = miCols, Sb128 = sb128, MiBlock = new int[miRows * miCols] };
        int sbLog2 = sb128 != 0 ? 5 : 4;
        for (int r = 0; r < miRows; r += 1 << sbLog2)
            for (int c = 0; c < miCols; c += 1 << sbLog2)
                Partition(rng, mi, r, c, sbLog2);
        return mi;
    }

    // Blocky content with noise so that both the filters and their masks are exercised.
    private static byte[] RandomPlane(Random rng, int width, int height, int stride)
    {
        var p = new byte[stride * height];
        rng.NextBytes(p);
        for (int by = 0; by < height; by += 8)
            for (int bx = 0; bx < width; bx += 8)
            {
                int kind = rng.Next(4), basev = rng.Next(256), amp = kind == 0 ? 40 : kind == 1 ? 6 : 2;
                for (int y = by; y < Math.Min(by + 8, height); y++)
                    for (int x = bx; x < Math.Min(bx + 8, width); x++)
                        p[y * stride + x] = (byte)Math.Clamp(basev + rng.Next(-amp, amp + 1), 0, 255);
            }
        return p;
    }

    private static SequenceHeader Seq(int sb128) => new()
    {
        sb_size = sb128 != 0 ? BLOCK_128X128 : BLOCK_64X64,
        mib_size = sb128 != 0 ? 32 : 16,
        mib_size_log2 = sb128 != 0 ? 5 : 4,
        bit_depth = 8,
        use_highbitdepth = 0,
        monochrome = 0,
        subsampling_x = 1,
        subsampling_y = 1,
        enable_cdef = 1,
    };

    private static YV12_BUFFER_CONFIG Frame(byte[] y, int ys, byte[] u, byte[] v, int uvs, int miRows, int miCols) =>
        Frame(new[] { y, u, v }, new int[3], ys, uvs, miRows, miCols);

    private static YV12_BUFFER_CONFIG Frame(byte[][] planes, int[] origin, int ys, int uvs, int miRows, int miCols)
    {
        var f = new YV12_BUFFER_CONFIG();
        f.y_buffer = new Ptr<byte>(planes[0], origin[0]);
        f.u_buffer = new Ptr<byte>(planes[1], origin[1]);
        f.v_buffer = new Ptr<byte>(planes[2], origin[2]);
        f.y_stride = ys;
        f.uv_stride = uvs;
        f.y_crop_width = f.y_width = miCols * 4;
        f.y_crop_height = f.y_height = miRows * 4;
        f.uv_crop_width = f.uv_width = miCols * 2;
        f.uv_crop_height = f.uv_height = miRows * 2;
        return f;
    }

    private static MACROBLOCKD Xd()
    {
        var xd = new MACROBLOCKD();
        for (int p = 0; p < 3; p++) xd.plane[p].subsampling_x = xd.plane[p].subsampling_y = p > 0 ? 1 : 0;
        return xd;
    }

    // av1_setup_dst_planes (reconinter.c, not ported in this package): setup_pred_plane per plane.
    private static void SetupDstPlanes(macroblockd_plane[] planes, int bsize, YV12_BUFFER_CONFIG src,
                                       int miRow, int miCol, int planeStart, int planeEnd)
    {
        for (int i = planeStart; i < Math.Min(planeEnd, 3); ++i)
        {
            int isUv = i > 0 ? 1 : 0;
            reconinter.setup_pred_plane(ref planes[i].dst, bsize, src.buffers[i], src.crop_widths[isUv],
                                        src.crop_heights[isUv], src.strides[isUv], miRow, miCol, null,
                                        planes[i].subsampling_x, planes[i].subsampling_y);
        }
    }

    // ---- Deblocking ----------------------------------------------------------------------------

    private sealed class LfCase
    {
        public ModeInfo Mi = null!;
        public int[] Levels = null!;
        public sbyte[] RefDeltas = null!, ModeDeltas = null!;
        public int PlaneStart, PlaneEnd, Ys, Uvs;
        public byte[] Y = null!, U = null!, V = null!;
    }

    private static LfCase RandomLfCase(Random rng, bool lumaOnly)
    {
        int miRows = 2 * rng.Next(2, 50), miCols = 2 * rng.Next(2, 50);
        var c = new LfCase { Mi = RandomModeInfo(rng, miRows, miCols, rng.Next(2)) };
        int Level() => rng.Next(5) == 0 ? 0 : rng.Next(64);
        c.Levels = new[] { Level(), Level(), Level(), Level(), rng.Next(3) == 0 ? rng.Next(8) : 0, rng.Next(2) };
        c.RefDeltas = rng.Next(2) == 0
            ? new sbyte[] { 1, 0, 0, 0, -1, 0, -1, -1 }
            : new[] { (sbyte)rng.Next(-8, 9), (sbyte)rng.Next(-8, 9), (sbyte)rng.Next(-8, 9), (sbyte)rng.Next(-8, 9),
                      (sbyte)rng.Next(-8, 9), (sbyte)rng.Next(-8, 9), (sbyte)rng.Next(-8, 9), (sbyte)rng.Next(-8, 9) };
        c.ModeDeltas = new[] { (sbyte)rng.Next(-4, 5), (sbyte)rng.Next(-4, 5) };
        (c.PlaneStart, c.PlaneEnd) = rng.Next(4) switch { 0 => (0, 1), 1 => (1, 3), _ => (0, 3) };
        if (lumaOnly) (c.PlaneStart, c.PlaneEnd) = (0, 1);
        c.Ys = miCols * 4 + rng.Next(0, 24);
        c.Uvs = miCols * 2 + rng.Next(0, 12);
        c.Y = RandomPlane(rng, miCols * 4, miRows * 4, c.Ys);
        c.U = RandomPlane(rng, miCols * 2, miRows * 2, c.Uvs);
        c.V = RandomPlane(rng, miCols * 2, miRows * 2, c.Uvs);
        return c;
    }

    private static (AV1_COMMON, MACROBLOCKD, YV12_BUFFER_CONFIG, byte[], byte[], byte[]) BuildLf(LfCase c)
    {
        ModeInfo mi = c.Mi;
        var cm = new AV1_COMMON { seq_params = Seq(mi.Sb128) };
        var blocks = new MB_MODE_INFO[mi.BSize.Count];
        for (int i = 0; i < blocks.Length; i++)
        {
            var m = new MB_MODE_INFO
            {
                bsize = mi.BSize[i], tx_size = mi.TxSize[i], skip_txfm = mi.Skip[i], mode = mi.Mode[i],
                segment_id = mi.Segment[i],
            };
            m.ref_frame[0] = mi.Ref0[i];
            m.ref_frame[1] = NONE_FRAME;
            for (int k = 0; k < m.inter_tx_size.Length; k++) m.inter_tx_size[k] = mi.TxSize[i];
            blocks[i] = m;
        }
        var grid = new MB_MODE_INFO[mi.MiRows * mi.MiCols];
        for (int i = 0; i < grid.Length; i++) grid[i] = blocks[mi.MiBlock[i]];
        cm.mi_params.mi_rows = mi.MiRows;
        cm.mi_params.mi_cols = mi.MiCols;
        cm.mi_params.mi_stride = mi.MiCols;
        cm.mi_params.mi_grid_base = grid;
        cm.lf.filter_level[0] = c.Levels[0];
        cm.lf.filter_level[1] = c.Levels[1];
        cm.lf.filter_level_u = c.Levels[2];
        cm.lf.filter_level_v = c.Levels[3];
        cm.lf.sharpness_level = c.Levels[4];
        cm.lf.mode_ref_delta_enabled = (byte)c.Levels[5];
        Array.Copy(c.RefDeltas, cm.lf.ref_deltas, 8);
        Array.Copy(c.ModeDeltas, cm.lf.mode_deltas, 2);
        av1_loop_filter_init(cm);
        byte[] y = (byte[])c.Y.Clone(), u = (byte[])c.U.Clone(), v = (byte[])c.V.Clone();
        return (cm, Xd(), Frame(y, c.Ys, u, v, c.Uvs, mi.MiRows, mi.MiCols), y, u, v);
    }

    private static (byte[], byte[], byte[]) LibaomLf(LfCase c)
    {
        ModeInfo mi = c.Mi;
        byte[] y = (byte[])c.Y.Clone(), u = (byte[])c.U.Clone(), v = (byte[])c.V.Clone();
        E4Shim.aomshim_e4_lf_frame(mi.MiRows, mi.MiCols, mi.Sb128, mi.MiBlock, mi.BSize.Count, mi.BSize.ToArray(),
                                   mi.TxSize.ToArray(), mi.Skip.ToArray(), mi.Ref0.ToArray(), mi.Mode.ToArray(),
                                   mi.Segment.ToArray(), c.Levels, c.RefDeltas, c.ModeDeltas, y, c.Ys, u, v, c.Uvs,
                                   c.PlaneStart, c.PlaneEnd);
        return (y, u, v);
    }

    // av1_loop_filter_frame_mt with one worker (loop_filter_rows, lpf_opt_level 2), with the
    // stubbed av1_setup_dst_planes replaced by SetupDstPlanes.
    private static void LoopFilterFrame(AV1_COMMON cm, MACROBLOCKD xd, YV12_BUFFER_CONFIG frame,
                                        int planeStart, int planeEnd)
    {
        const int lpf_opt_level = 2;
        var planes_to_lf = new int[3];
        if (check_planes_to_loop_filter(cm.lf, planes_to_lf, planeStart, planeEnd) == 0) return;
        av1_loop_filter_frame_init(cm, planeStart, planeEnd);
        var params_buf = new AV1_DEBLOCKING_PARAMETERS[MAX_MIB_SIZE];
        var tx_buf = new int[MAX_MIB_SIZE];
        for (int mi_row = 0; mi_row < cm.mi_params.mi_rows; mi_row += MAX_MIB_SIZE)
            for (int plane = 0; plane < 3; ++plane)
            {
                if (skip_loop_filter_plane(planes_to_lf, plane, lpf_opt_level)) continue;
                bool joint = plane > 0;
                for (int dir = 0; dir < 2; ++dir)
                    for (int mi_col = 0; mi_col < cm.mi_params.mi_cols; mi_col += MAX_MIB_SIZE)
                    {
                        SetupDstPlanes(xd.plane, cm.seq_params.sb_size, frame, mi_row, mi_col, plane,
                                       plane + (joint ? 2 : 1));
                        if (plane == 0 && dir == 0)
                            av1_filter_block_plane_vert_opt(cm, xd, xd.plane[0], (uint)mi_row, (uint)mi_col,
                                                            params_buf, tx_buf, MAX_MIB_SIZE_LOG2);
                        else if (plane == 0)
                            av1_filter_block_plane_horz_opt(cm, xd, xd.plane[0], (uint)mi_row, (uint)mi_col,
                                                            params_buf, tx_buf, MAX_MIB_SIZE_LOG2);
                        else if (dir == 0)
                            av1_filter_block_plane_vert_opt_chroma(cm, xd, new Ptr<macroblockd_plane>(xd.plane, plane),
                                                                   (uint)mi_row, (uint)mi_col, params_buf, tx_buf,
                                                                   plane, joint, MAX_MIB_SIZE_LOG2);
                        else
                            av1_filter_block_plane_horz_opt_chroma(cm, xd, new Ptr<macroblockd_plane>(xd.plane, plane),
                                                                   (uint)mi_row, (uint)mi_col, params_buf, tx_buf,
                                                                   plane, joint, MAX_MIB_SIZE_LOG2);
                    }
            }
    }

    private static void AssertPlanes((byte[] y, byte[] u, byte[] v) expected, byte[] y, byte[] u, byte[] v, string what)
    {
        Assert.True(expected.y.AsSpan().SequenceEqual(y), $"{what}: Y differs");
        Assert.True(expected.u.AsSpan().SequenceEqual(u), $"{what}: U differs");
        Assert.True(expected.v.AsSpan().SequenceEqual(v), $"{what}: V differs");
    }

    private static void DeblockFrames(int seed, int iterations, bool lumaOnly, bool throughDriver)
    {
        var rng = new Random(seed);
        for (int iter = 0; iter < iterations; iter++)
        {
            LfCase c = RandomLfCase(rng, lumaOnly);
            var expected = LibaomLf(c);
            var (cm, xd, frame, y, u, v) = BuildLf(c);
            if (throughDriver) av1_loop_filter_frame_mt(frame, cm, xd, c.PlaneStart, c.PlaneEnd, 0, null!, 1, null!, 2);
            else LoopFilterFrame(cm, xd, frame, c.PlaneStart, c.PlaneEnd);
            AssertPlanes(expected, y, u, v, $"iteration {iter}");
        }
    }

    [Fact]
    public void DeblockFrameLumaMatchesLibaom() => DeblockFrames(4241, 60, lumaOnly: true, throughDriver: false);

    // The chroma planes need get_plane_block_size (av1_ss_size_lookup).
    [FactUnlessStub("av1_ss_size_lookup")]
    public void DeblockFrameMatchesLibaom() => DeblockFrames(4242, 60, lumaOnly: false, throughDriver: false);

    [FactUnlessStub("av1_setup_dst_planes", "av1_ss_size_lookup")]
    public void DeblockFrameThroughDriverMatchesLibaom() => DeblockFrames(4243, 30, lumaOnly: false, throughDriver: true);

    // The row-synchronised multi-threaded deblocking path (loop_filter_frame_mt_init, then worker
    // threads that take jobs with get_lf_job_info and run av1_thread_loop_filter_rows with the
    // AV1LfSync, as libaom's loop_filter_row_worker does): the horizontal jobs wait in
    // sync_read() for the vertical ones (sync_write()).
    [FactUnlessStub("av1_setup_dst_planes", "av1_ss_size_lookup")]
    public void DeblockFrameRowSyncedThreadsMatchLibaom()
    {
        var rng = new Random(4244);
        var lf_sync = new AV1LfSync();
        for (int iter = 0; iter < 30; iter++)
        {
            LfCase c = RandomLfCase(rng, lumaOnly: false);
            (c.PlaneStart, c.PlaneEnd) = (0, 3);
            var expected = LibaomLf(c);
            var (cm, _, frame, y, u, v) = BuildLf(c);
            cm.width = c.Mi.MiCols * 4;
            var planes_to_lf = new int[3];
            if (check_planes_to_loop_filter(cm.lf, planes_to_lf, 0, 3) != 0)
            {
                av1_loop_filter_frame_init(cm, 0, 3);
                int numWorkers = 2 + rng.Next(3);
                loop_filter_frame_mt_init(cm, 0, cm.mi_params.mi_rows, planes_to_lf, numWorkers, lf_sync, 2,
                                          MAX_MIB_SIZE_LOG2);
                var threads = new Thread[numWorkers];
                Exception? failure = null;
                for (int w = 0; w < numWorkers; w++)
                {
                    threads[w] = new Thread(() =>
                    {
                        try
                        {
                            MACROBLOCKD xd = Xd(); // per-worker planes (LFWorkerData)
                            var params_buf = new AV1_DEBLOCKING_PARAMETERS[MAX_MIB_SIZE];
                            var tx_buf = new int[MAX_MIB_SIZE];
                            Ptr<AV1LfMTInfo> job;
                            while (!(job = get_lf_job_info(lf_sync)).IsNull)
                                av1_thread_loop_filter_rows(frame, cm, xd.plane, xd, job.Value.mi_row, job.Value.plane,
                                                            job.Value.dir, 2, lf_sync, xd.error_info, params_buf,
                                                            tx_buf, MAX_MIB_SIZE_LOG2);
                        }
                        catch (Exception e) { failure = e; }
                    });
                    threads[w].Start();
                }
                foreach (Thread t in threads) Assert.True(t.Join(TimeSpan.FromSeconds(60)), "deadlock");
                if (failure != null) throw failure;
            }
            AssertPlanes(expected, y, u, v, $"iteration {iter}");
        }
        av1_loop_filter_dealloc(lf_sync);
    }

    // ---- CDEF ----------------------------------------------------------------------------------

    private sealed class CdefCase
    {
        public int MiRows, MiCols, Ys, Uvs;
        public byte[] Skip = null!;
        public sbyte[] Strength = null!;
        public int[] Info = null!, Strengths = null!, UvStrengths = null!;
        public byte[] Y = null!, U = null!, V = null!;
    }

    private static CdefCase RandomCdefCase(Random rng)
    {
        int miRows = 2 * rng.Next(2, 50), miCols = 2 * rng.Next(2, 50);
        var c = new CdefCase { MiRows = miRows, MiCols = miCols };
        c.Skip = new byte[miRows * miCols];
        int skipPct = rng.Next(4) switch { 0 => 0, 1 => 90, _ => 40 };
        for (int i = 0; i < c.Skip.Length; i++) c.Skip[i] = (byte)(rng.Next(100) < skipPct ? 1 : 0);
        int bits = rng.Next(4);
        c.Info = new[] { 3 + rng.Next(4), 1 << bits, bits };
        c.Strength = new sbyte[miRows * miCols];
        for (int i = 0; i < c.Strength.Length; i++)
            c.Strength[i] = (sbyte)(rng.Next(12) == 0 ? -1 : rng.Next(1 << bits));
        c.Strengths = new int[8];
        c.UvStrengths = new int[8];
        for (int i = 0; i < 8; i++)
        {
            c.Strengths[i] = rng.Next(5) == 0 ? 0 : rng.Next(64);
            c.UvStrengths[i] = rng.Next(4) == 0 ? 0 : rng.Next(64);
        }
        int aligned = (miCols * 4 + 15) & ~15;
        c.Ys = aligned + rng.Next(0, 3) * 8;
        c.Uvs = aligned / 2 + rng.Next(0, 3) * 8;
        c.Y = RandomPlane(rng, c.Ys, miRows * 4, c.Ys);
        c.U = RandomPlane(rng, c.Uvs, miRows * 2, c.Uvs);
        c.V = RandomPlane(rng, c.Uvs, miRows * 2, c.Uvs);
        return c;
    }

    private static (AV1_COMMON, MACROBLOCKD, YV12_BUFFER_CONFIG, byte[], byte[], byte[]) BuildCdef(CdefCase c)
    {
        var cm = new AV1_COMMON { seq_params = Seq(0) };
        var grid = new MB_MODE_INFO[c.MiRows * c.MiCols];
        for (int i = 0; i < grid.Length; i++)
            grid[i] = new MB_MODE_INFO { bsize = BLOCK_64X64, skip_txfm = c.Skip[i], cdef_strength = c.Strength[i] };
        cm.mi_params.mi_rows = c.MiRows;
        cm.mi_params.mi_cols = c.MiCols;
        cm.mi_params.mi_stride = c.MiCols;
        cm.mi_params.mi_grid_base = grid;
        CdefInfo ci = cm.cdef_info;
        ci.cdef_damping = c.Info[0];
        ci.nb_cdef_strengths = c.Info[1];
        ci.cdef_bits = c.Info[2];
        Array.Copy(c.Strengths, ci.cdef_strengths, 8);
        Array.Copy(c.UvStrengths, ci.cdef_uv_strengths, 8);
        // Buffer sizes of av1_alloc_cdef_buffers (alloccommon.c) with one worker.
        int luma_stride = (c.MiCols * 4 + 15) & ~15;
        ci.srcbuf = new ushort[Aom.cdef_block.CDEF_INBUF_SIZE];
        for (int p = 0; p < 3; p++)
        {
            int shift = p > 0 ? 1 : 0;
            ci.linebuf[p] = new ushort[3 * (2 << 1) * (luma_stride >> shift)];
            ci.colbuf[p] = new ushort[(64 << (2 - shift)) * 2 * 2 * 8];
        }
        byte[] y = (byte[])c.Y.Clone(), u = (byte[])c.U.Clone(), v = (byte[])c.V.Clone();
        return (cm, Xd(), Frame(y, c.Ys, u, v, c.Uvs, c.MiRows, c.MiCols), y, u, v);
    }

    private static (byte[], byte[], byte[]) LibaomCdef(CdefCase c)
    {
        byte[] y = (byte[])c.Y.Clone(), u = (byte[])c.U.Clone(), v = (byte[])c.V.Clone();
        E4Shim.aomshim_e4_cdef_frame(c.MiRows, c.MiCols, c.Skip, c.Strength, c.Info, c.Strengths, c.UvStrengths,
                                     y, c.Ys, u, v, c.Uvs);
        return (y, u, v);
    }

    [Fact]
    public void CdefFrameMatchesLibaom()
    {
        var rng = new Random(5151);
        for (int iter = 0; iter < 60; iter++)
        {
            CdefCase c = RandomCdefCase(rng);
            var expected = LibaomCdef(c);
            var (cm, xd, frame, y, u, v) = BuildCdef(c);
            // av1_cdef_frame with the stubbed av1_setup_dst_planes replaced by SetupDstPlanes.
            int nvfb = (cm.mi_params.mi_rows + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;
            SetupDstPlanes(xd.plane, cm.seq_params.sb_size, frame, 0, 0, 0, 3);
            for (int fbr = 0; fbr < nvfb; fbr++)
                av1_cdef_fb_row(cm, xd, cm.cdef_info.linebuf, cm.cdef_info.colbuf, cm.cdef_info.srcbuf, fbr,
                                av1_cdef_init_fb_row, null!, xd.error_info);
            AssertPlanes(expected, y, u, v, $"iteration {iter}");
        }
    }

    [FactUnlessStub("av1_setup_dst_planes")]
    public void CdefFrameThroughDriverMatchesLibaom()
    {
        var rng = new Random(5152);
        for (int iter = 0; iter < 30; iter++)
        {
            CdefCase c = RandomCdefCase(rng);
            var expected = LibaomCdef(c);
            var (cm, xd, frame, y, u, v) = BuildCdef(c);
            av1_cdef_frame(frame, cm, xd, av1_cdef_init_fb_row);
            AssertPlanes(expected, y, u, v, $"iteration {iter}");
        }
    }
    // av1_cdef_frame_mt with 2..5 workers (worker 0 on this thread), row synchronisation via
    // av1_cdef_init_fb_row_mt, against libaom's single-threaded av1_cdef_frame.
    [FactUnlessStub("av1_setup_dst_planes", "aom_get_worker_interface")]
    public void CdefFrameMultiThreadedMatchesLibaom()
    {
        var rng = new Random(5153);
        var wi = (AVxWorkerInterface)StubProbe.Call("aom_get_worker_interface")!;
        for (int iter = 0; iter < 30; iter++)
        {
            CdefCase c = RandomCdefCase(rng);
            var expected = LibaomCdef(c);
            var (cm, xd, frame, y, u, v) = BuildCdef(c);
            int numWorkers = 2 + rng.Next(4);
            int nvfb = (c.MiRows + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;
            int luma_stride = (c.MiCols * 4 + 15) & ~15;
            cm.cur_frame = new RefCntBuffer { buf = frame };
            cm.cdef_info.allocated_num_workers = numWorkers;
            for (int p = 0; p < 3; p++)
                cm.cdef_info.linebuf[p] = new ushort[nvfb * (2 << 1) * (luma_stride >> (p > 0 ? 1 : 0))];

            var cdef_sync = new AV1CdefSync();
            aom_pthread.pthread_mutex_init(ref cdef_sync.mutex_, null!);
            var rowSync = new AV1CdefRowSync[nvfb];
            for (int r = 0; r < nvfb; r++)
            {
                aom_pthread.pthread_mutex_init(ref rowSync[r].row_mutex_, null!);
                aom_pthread.pthread_cond_init(ref rowSync[r].row_cond_, null!);
            }
            cdef_sync.cdef_row_mt = rowSync;

            var cdef_worker = new AV1CdefWorkerData[numWorkers];
            var workers = new AVxWorker[numWorkers];
            for (int i = 0; i < numWorkers; i++)
            {
                cdef_worker[i] = new AV1CdefWorkerData();
                if (i > 0)
                {
                    cdef_worker[i].srcbuf = new ushort[Aom.cdef_block.CDEF_INBUF_SIZE];
                    for (int p = 0; p < 3; p++)
                        cdef_worker[i].colbuf[p] = new ushort[(64 << (2 - (p > 0 ? 1 : 0))) * 2 * 2 * 8];
                }
                workers[i] = new AVxWorker();
                wi.init(workers[i]);
                if (i > 0) Assert.True(wi.reset(workers[i]) != 0);
            }
            try
            {
                av1_cdef_frame_mt(cm, xd, cdef_worker, workers, cdef_sync, numWorkers, av1_cdef_init_fb_row_mt, 0);
            }
            finally
            {
                for (int i = 1; i < numWorkers; i++) wi.end(workers[i]);
            }
            AssertPlanes(expected, y, u, v, $"iteration {iter} ({numWorkers} workers)");
        }
    }

    // ---- CDEF search (pickcdef.c) --------------------------------------------------------------

    // av1_cdef_search with CDEF_FAST_SEARCH_LVL5 (speed 11 below the PICK_FROM_Q resolution
    // threshold) or CDEF_FULL_SEARCH, single-threaded MSE pass.
    [FactUnlessStub("aom_sse", "av1_setup_dst_planes", "av1_ss_size_lookup")]
    public void CdefSearchMatchesLibaom()
    {
        var rng = new Random(606);
        for (int iter = 0; iter < 40; iter++)
        {
            int miRows = 2 * rng.Next(2, 40), miCols = 2 * rng.Next(2, 40);
            int sb128 = rng.Next(3) == 0 ? 1 : 0;
            int pick = rng.Next(4) == 0 ? 0 /* CDEF_FULL_SEARCH */ : 5 /* CDEF_FAST_SEARCH_LVL5 */;
            int qindex = rng.Next(256), rdmult = rng.Next(50, 50000);
            var skip = new byte[miRows * miCols];
            int skipPct = rng.Next(4) switch { 0 => 0, 1 => 90, _ => 40 };
            for (int i = 0; i < skip.Length; i++) skip[i] = (byte)(rng.Next(100) < skipPct ? 1 : 0);
            // Frames with a border: when a 128x128 block straddles the bottom / right edge, the
            // search reads CDEF_VBORDER rows / CDEF_HBORDER columns beyond the frame (libaom reads
            // its frame border there).
            const int border = 16;
            int ys = miCols * 4 + 2 * border + rng.Next(0, 3) * 8, uvs = miCols * 2 + 2 * border + rng.Next(0, 3) * 8;
            int[] origin = { border * ys + border, border * uvs + border, border * uvs + border };
            byte[][] src =
            {
                RandomPlane(rng, ys, miRows * 4 + 2 * border, ys), RandomPlane(rng, uvs, miRows * 2 + 2 * border, uvs),
                RandomPlane(rng, uvs, miRows * 2 + 2 * border, uvs),
            };
            var rec = new byte[3][];
            int amp = new[] { 0, 2, 6, 20 }[rng.Next(4)];
            for (int p = 0; p < 3; p++)
            {
                rec[p] = (byte[])src[p].Clone();
                for (int i = 0; i < rec[p].Length; i++) rec[p][i] = (byte)Math.Clamp(rec[p][i] + rng.Next(-amp, amp + 1), 0, 255);
            }

            var expected = new int[35];
            var expectedStrength = new sbyte[miRows * miCols];
            E4Shim.CdefSearch(miRows, miCols, sb128, skip, qindex, pick, rdmult, src, rec, origin, ys, uvs, expected,
                              expectedStrength);

            var cpi = new AV1_COMP { ppi = new AV1_PRIMARY() };
            AV1_COMMON cm = cpi.common;
            cm.seq_params = Seq(sb128);
            cm.quant_params.base_qindex = qindex;
            var grid = new MB_MODE_INFO[miRows * miCols];
            for (int i = 0; i < grid.Length; i++)
                grid[i] = new MB_MODE_INFO { bsize = sb128 != 0 ? BLOCK_128X128 : BLOCK_64X64, skip_txfm = skip[i] };
            cm.mi_params.mi_rows = miRows;
            cm.mi_params.mi_cols = miCols;
            cm.mi_params.mi_stride = miCols;
            cm.mi_params.mi_grid_base = grid;
            cm.cur_frame = new RefCntBuffer { buf = Frame(rec, origin, ys, uvs, miRows, miCols) };
            cpi.source = Frame(src, origin, ys, uvs, miRows, miCols);
            for (int p = 0; p < 3; p++)
                cpi.td.mb.e_mbd.plane[p].subsampling_x = cpi.td.mb.e_mbd.plane[p].subsampling_y = p > 0 ? 1 : 0;
            cpi.td.mb.rdmult = rdmult;
            cpi.oxcf.tool_cfg.cdef_control = CDEF_ALL;
            cpi.sf.lpf_sf.cdef_pick_method = pick;
            cpi.mt_info.num_mod_workers[6 /* MOD_CDEF_SEARCH */] = 1;
            av1_cdef_search(cpi);

            CdefInfo ci = cm.cdef_info;
            var actual = new int[35];
            actual[0] = ci.cdef_bits;
            actual[1] = ci.nb_cdef_strengths;
            actual[2] = ci.cdef_damping;
            for (int i = 0; i < 16; i++)
            {
                actual[3 + i] = ci.cdef_strengths[i];
                actual[19 + i] = ci.cdef_uv_strengths[i];
            }
            Assert.True(expected.AsSpan().SequenceEqual(actual),
                        $"iteration {iter}: [{string.Join(",", actual)}] vs libaom [{string.Join(",", expected)}]");
            for (int i = 0; i < grid.Length; i++)
                Assert.True(expectedStrength[i] == grid[i].cdef_strength, $"iteration {iter}: cdef_strength of mi {i}");
        }
    }
}
