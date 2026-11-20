using System;
using System.Diagnostics;
using System.Runtime.Intrinsics;
using Av1rt.Aom;
using static Av1rt.Aom.intrin;
using __m128i = System.Runtime.Intrinsics.Vector128<long>;

/// <summary>
/// Micro-benchmarks of individual ported kernels. The SIMD entries mirror the native harness
/// (.oracle/kbench/kbench.c, linked against libaom) call for call, so the two outputs compare
/// per-call cost directly.
/// </summary>
internal static class KernelBench
{
    public static int Run(string[] args)
    {
        const int stride = 4096 + 160;
        var src = new byte[stride * 160];
        var refb = new byte[stride * 160];
        var dst = new byte[64 * 160];
        int bstride = cdef_block.CDEF_BSTRIDE;
        var cdefIn = new ushort[bstride * 64];
        var diff16 = new short[64 * 64];
        var coeff16 = new short[64 * 64];
        var coeff32 = new int[64 * 64];
        var qcoeff = new int[64 * 64];
        var dqcoeff = new int[64 * 64];
        var levels = new byte[enums.TX_PAD_2D];
        // Same pseudo-random inputs as kbench.c.
        uint rs = 1;
        for (int i = 0; i < src.Length; i++) { rs = rs * 1103515245 + 12345; src[i] = (byte)(rs >> 16); }
        for (int i = 0; i < refb.Length; i++) { rs = rs * 1103515245 + 12345; refb[i] = (byte)(rs >> 16); }
        for (int i = 0; i < cdefIn.Length; i++) { rs = rs * 1103515245 + 12345; cdefIn[i] = (ushort)((rs >> 16) & 255); }
        for (int i = 0; i < 64 * 64; i++)
        {
            rs = rs * 1103515245 + 12345;
            diff16[i] = (short)(((rs >> 16) & 511) - 256);
            coeff32[i] = (int)(((rs >> 8) & 4095) - 2048);
        }

        string only = args.Length > 1 ? args[1] : null;
        void T(string name, Func<uint> f) { if (only == null || name.Contains(only)) Time(name, f); }

        InterpFilterParams f8 = filter.av1_get_interp_filter_params_with_block_size(filter.EIGHTTAP_REGULAR, 8);
        InterpFilterParams f16 = filter.av1_get_interp_filter_params_with_block_size(filter.EIGHTTAP_REGULAR, 16);
        ConvolveParams cp = convolve.get_conv_params(0, 0, 8);
        Ptr<byte> S(int off) => new Ptr<byte>(src, off);
        Ptr<byte> R(int off) => new Ptr<byte>(refb, off);
        var d = new Ptr<byte>(dst);

        T("convolve_2d_sr 8x8", () => { for (int i = 0; i < 128; i++) convolve_2d_avx2.av1_convolve_2d_sr_avx2(S(stride * 8 + 8 + i * 3), stride, d, 64, 8, 8, f8, f8, 6, 10, ref cp); return 0; });
        T("convolve_2d_sr 16x16", () => { for (int i = 0; i < 128; i++) convolve_2d_avx2.av1_convolve_2d_sr_avx2(S(stride * 8 + 8 + i * 3), stride, d, 64, 16, 16, f16, f16, 6, 10, ref cp); return 0; });
        T("convolve_x_sr 16x16", () => { for (int i = 0; i < 128; i++) convolve_avx2.av1_convolve_x_sr_avx2(S(stride * 8 + 8 + i * 3), stride, d, 64, 16, 16, f16, 6, ref cp); return 0; });
        T("convolve_y_sr 16x16", () => { for (int i = 0; i < 128; i++) convolve_avx2.av1_convolve_y_sr_avx2(S(stride * 8 + 8 + i * 3), stride, d, 64, 16, 16, f16, 10); return 0; });
        T("convolve_copy 16x16", () => { for (int i = 0; i < 128; i++) aom_convolve_copy_avx2_h.aom_convolve_copy_avx2(S(i * 3), stride, d, 64, 16, 16); return 0; });
        T("subpel_var16x16 (2,6)", () => { uint s = 0, sse = 0; for (int i = 0; i < 128; i++) s += variance_impl_avx2.aom_sub_pixel_variance16x16_avx2(S(i * 3), stride, 2, 6, R(i), stride, ref sse); return s; });
        T("subpel_var16x16 (4,0)", () => { uint s = 0, sse = 0; for (int i = 0; i < 128; i++) s += variance_impl_avx2.aom_sub_pixel_variance16x16_avx2(S(i * 3), stride, 4, 0, R(i), stride, ref sse); return s; });
        T("variance16x16", () => { uint s = 0, sse = 0; for (int i = 0; i < 128; i++) s += variance_avx2.aom_variance16x16_avx2(S(i * 3), stride, R(i), stride, ref sse); return s; });
        T("sse 16x16", () => { long s = 0; for (int i = 0; i < 128; i++) s += sse_avx2.aom_sse_avx2(S(i * 3), stride, R(i), stride, 16, 16); return (uint)s; });
        T("sse 64x64", () => { long s = 0; for (int i = 0; i < 128; i++) s += sse_avx2.aom_sse_avx2(S(i * 3), stride, R(i), stride, 64, 64); return (uint)s; });
        Ptr<ushort> C(int off) => new Ptr<ushort>(cdefIn, off);
        T("cdef_filter_8_0 8x8", () => { for (int i = 0; i < 128; i++) cdef_block_avx2.cdef_filter_8_0_avx2(d, 64, C(2 * bstride + 8 + (i & 15)), 4, 2, i & 7, 5, 5, 0, 8, 8); return 0; });
        T("cdef_filter_8_1 8x8", () => { for (int i = 0; i < 128; i++) cdef_block_avx2.cdef_filter_8_1_avx2(d, 64, C(2 * bstride + 8 + (i & 15)), 4, 0, i & 7, 5, 5, 0, 8, 8); return 0; });
        T("cdef_filter_8_0 4x4", () => { for (int i = 0; i < 128; i++) cdef_block_avx2.cdef_filter_8_0_avx2(d, 64, C(2 * bstride + 8 + (i & 15)), 4, 2, i & 7, 5, 5, 0, 4, 4); return 0; });
        T("cdef_find_dir_dual", () =>
        {
            uint s = 0; int v1 = 0, v2 = 0, d1 = 0, d2 = 0;
            for (int i = 0; i < 128; i++) { cdef_block_avx2.cdef_find_dir_dual_avx2(C(i & 15), C(8 + (i & 15)), bstride, ref v1, ref v2, 0, ref d1, ref d2); s += (uint)(d1 + d2); }
            return s;
        });
        var tp = new TxfmParam { tx_type = txfm_common.DCT_DCT, tx_size = txfm_common.TX_16X16, bd = 8, tx_set_type = txfm_common.EXT_TX_SET_DCTONLY };
        T("fwd_txfm 16x16", () => { for (int i = 0; i < 128; i++) av1_fwd_txfm2d_avx2.av1_lowbd_fwd_txfm_avx2(new Ptr<short>(diff16, i & 15), new Ptr<int>(coeff32), 64, ref tp); return 0; });
        var tp8 = tp; tp8.tx_size = txfm_common.TX_8X8;
        T("fwd_txfm 8x8", () => { for (int i = 0; i < 128; i++) av1_fwd_txfm2d_avx2.av1_lowbd_fwd_txfm_avx2(new Ptr<short>(diff16, i & 15), new Ptr<int>(coeff32), 64, ref tp8); return 0; });
        T("hadamard_lp_16x16", () => { for (int i = 0; i < 128; i++) avg_intrin_avx2.aom_hadamard_lp_16x16_avx2(new Ptr<short>(diff16, i & 15), 64, new Ptr<short>(coeff16)); return 0; });
        {
            short[] zbin = { 20, 22, 22, 22, 22, 22, 22, 22 }, round = { 30, 32, 32, 32, 32, 32, 32, 32 },
                quant = { 20000, 21000, 21000, 21000, 21000, 21000, 21000, 21000 },
                qshift = { 16384, 16384, 16384, 16384, 16384, 16384, 16384, 16384 }, deq = { 40, 44, 44, 44, 44, 44, 44, 44 };
            var eob = new ushort[1];
            SCAN_ORDER so = scan.av1_scan_orders[txfm_common.TX_16X16][txfm_common.DCT_DCT];
            T("quantize_b 16x16", () =>
            {
                uint s = 0;
                for (int i = 0; i < 128; i++)
                {
                    quantize_avx2.aom_quantize_b_avx2(new Ptr<int>(coeff32, (i & 15) * 8), 256, zbin, round, quant, qshift, qcoeff, dqcoeff, deq, eob, so.scan, so.iscan);
                    s += eob[0];
                }
                return s;
            });
        }
        T("txb_init_levels 16x16", () => { for (int i = 0; i < 128; i++) encodetxb_avx2.av1_txb_init_levels_avx2(new Ptr<int>(coeff32, (i & 15) * 8), 16, 16, new Ptr<byte>(levels)); return 0; });
        {
            var nzctx = new sbyte[64 * 64];
            SCAN_ORDER so16 = scan.av1_scan_orders[txfm_common.TX_16X16][txfm_common.DCT_DCT];
            SCAN_ORDER so8 = scan.av1_scan_orders[txfm_common.TX_8X8][txfm_common.DCT_DCT];
            encodetxb_avx2.av1_txb_init_levels_avx2(new Ptr<int>(coeff32), 16, 16, new Ptr<byte>(levels));
            T("nz_map_ctx 16x16", () =>
            {
                uint s = 0;
                for (int i = 0; i < 128; i++)
                {
                    encodetxb_sse2.av1_get_nz_map_contexts_sse2(new Ptr<byte>(levels), so16.scan, 256 - (i & 63), txfm_common.TX_16X16, entropy.TX_CLASS_2D, new Ptr<sbyte>(nzctx));
                    s += (uint)nzctx[i & 255];
                }
                return s;
            });
            encodetxb_avx2.av1_txb_init_levels_avx2(new Ptr<int>(coeff32), 8, 8, new Ptr<byte>(levels));
            T("nz_map_ctx 8x8", () =>
            {
                uint s = 0;
                for (int i = 0; i < 128; i++)
                {
                    encodetxb_sse2.av1_get_nz_map_contexts_sse2(new Ptr<byte>(levels), so8.scan, 64 - (i & 31), txfm_common.TX_8X8, entropy.TX_CLASS_2D, new Ptr<sbyte>(nzctx));
                    s += (uint)nzctx[i & 63];
                }
                return s;
            });
        }

        if (only == null)
        {
            T("aom_sad32x32_c", () =>
            {
                uint s = 0;
                for (int i = 0; i < 4096; i += 32) s += sad_h.aom_sad32x32_c(S(i), stride, R(i + 1), stride);
                return s;
            });
            var fc0 = new FRAME_CONTEXT();
            var fc1 = new FRAME_CONTEXT();
            T("FRAME_CONTEXT.CopyFrom/128", () => { fc1.CopyFrom(fc0); return 0; });
            Console.WriteLine(ShiftVar(Vector128.Create(-1000, 1000, 77, -77).AsInt64(), args.Length + 1));
        }
        return 0;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static __m128i ShiftVar(__m128i v, int bit) => _mm_srai_epi32(v, bit);

    private static void Time(string name, Func<uint> f)
    {
        uint check = f();
        for (int w = 0; w < 200; w++) f();
        var sw = Stopwatch.StartNew();
        int n = 0;
        while (sw.ElapsedMilliseconds < 1000) { f(); n++; }
        double ns = sw.Elapsed.TotalMilliseconds * 1e6 / (n * 128.0);
        Console.WriteLine($"{name,-24} {ns,8:F1} ns/call  (check {check})");
    }
}
