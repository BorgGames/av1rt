// Port of libaom av1/common/cdef_block.c

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class cdef_block
{
    /*
    This is Cdef_Directions (section 7.15.3) with 2 padding entries at the
    beginning and end of the table. The cdef direction range is [0, 7] and the
    first index is offset +/-2. This removes the need to constrain the first
    index to the same range using e.g., & 7.
    */
    // [port] Also read directly (index + 2) by the SIMD filters: indexing the Ptr<int[]> view
    // below takes a covariance-checked element address on every lookup.
    internal static readonly int[][] cdef_directions_padded = {
        /* Padding: cdef_directions[6] */
        new[] { 1 * CDEF_BSTRIDE + 0, 2 * CDEF_BSTRIDE + 0 },
        /* Padding: cdef_directions[7] */
        new[] { 1 * CDEF_BSTRIDE + 0, 2 * CDEF_BSTRIDE - 1 },

        /* Begin cdef_directions */
        new[] { -1 * CDEF_BSTRIDE + 1, -2 * CDEF_BSTRIDE + 2 },
        new[] { 0 * CDEF_BSTRIDE + 1, -1 * CDEF_BSTRIDE + 2 },
        new[] { 0 * CDEF_BSTRIDE + 1, 0 * CDEF_BSTRIDE + 2 },
        new[] { 0 * CDEF_BSTRIDE + 1, 1 * CDEF_BSTRIDE + 2 },
        new[] { 1 * CDEF_BSTRIDE + 1, 2 * CDEF_BSTRIDE + 2 },
        new[] { 1 * CDEF_BSTRIDE + 0, 2 * CDEF_BSTRIDE + 1 },
        new[] { 1 * CDEF_BSTRIDE + 0, 2 * CDEF_BSTRIDE + 0 },
        new[] { 1 * CDEF_BSTRIDE + 0, 2 * CDEF_BSTRIDE - 1 },
        /* End cdef_directions */

        /* Padding: cdef_directions[0] */
        new[] { -1 * CDEF_BSTRIDE + 1, -2 * CDEF_BSTRIDE + 2 },
        /* Padding: cdef_directions[1] */
        new[] { 0 * CDEF_BSTRIDE + 1, -1 * CDEF_BSTRIDE + 2 },
    };

    // [port] `const int (*const cdef_directions)[2] = cdef_directions_padded + 2`: a Ptr into the
    // row array, so cdef_directions[dir - 2] .. cdef_directions[dir + 2] index like C.
    internal static readonly Ptr<int[]> cdef_directions = new(cdef_directions_padded, 2);

    /* Instead of dividing by n between 2 and 8, we multiply by 3*5*7*8/n.
       The output is then 840 times larger, but we don't care for finding
       the max. */
    private static readonly int[] div_table = { 0, 840, 420, 280, 210, 168, 140, 120, 105 };

    /* Detect direction. 0 means 45-degree up-right, 2 is horizontal, and so on.
       The search minimizes the weighted variance along all the lines in a
       particular direction, i.e. the squared error between the input and a
       "predicted" block where each pixel is replaced by the average along a line
       in a particular direction. Since each direction have the same sum(x^2) term,
       that term is never computed. See Section 2, step 2, of:
       http://jmvalin.ca/notes/intra_paint.pdf */
    internal static int cdef_find_dir_c(Ptr<ushort> img, int stride, ref int var,
                                        int coeff_shift)
    {
        int i;
        Span<int> cost = stackalloc int[8];
        cost.Clear();
        // [port] int partial[8][15], flattened: partial[d][n] is partial[d * 15 + n].
        Span<int> partial = stackalloc int[8 * 15];
        partial.Clear();
        int best_cost = 0;
        int best_dir = 0;
        for (i = 0; i < 8; i++)
        {
            int j;
            for (j = 0; j < 8; j++)
            {
                int x;
                /* We subtract 128 here to reduce the maximum range of the squared
                   partial sums. */
                x = (img[i * stride + j] >> coeff_shift) - 128;
                partial[0 * 15 + i + j] += x;
                partial[1 * 15 + i + j / 2] += x;
                partial[2 * 15 + i] += x;
                partial[3 * 15 + 3 + i - j / 2] += x;
                partial[4 * 15 + 7 + i - j] += x;
                partial[5 * 15 + 3 - i / 2 + j] += x;
                partial[6 * 15 + j] += x;
                partial[7 * 15 + i / 2 + j] += x;
            }
        }
        for (i = 0; i < 8; i++)
        {
            cost[2] += partial[2 * 15 + i] * partial[2 * 15 + i];
            cost[6] += partial[6 * 15 + i] * partial[6 * 15 + i];
        }
        cost[2] *= div_table[8];
        cost[6] *= div_table[8];
        for (i = 0; i < 7; i++)
        {
            cost[0] += (partial[0 * 15 + i] * partial[0 * 15 + i] +
                        partial[0 * 15 + 14 - i] * partial[0 * 15 + 14 - i]) *
                       div_table[i + 1];
            cost[4] += (partial[4 * 15 + i] * partial[4 * 15 + i] +
                        partial[4 * 15 + 14 - i] * partial[4 * 15 + 14 - i]) *
                       div_table[i + 1];
        }
        cost[0] += partial[0 * 15 + 7] * partial[0 * 15 + 7] * div_table[8];
        cost[4] += partial[4 * 15 + 7] * partial[4 * 15 + 7] * div_table[8];
        for (i = 1; i < 8; i += 2)
        {
            int j;
            for (j = 0; j < 4 + 1; j++)
            {
                cost[i] += partial[i * 15 + 3 + j] * partial[i * 15 + 3 + j];
            }
            cost[i] *= div_table[8];
            for (j = 0; j < 4 - 1; j++)
            {
                cost[i] += (partial[i * 15 + j] * partial[i * 15 + j] +
                            partial[i * 15 + 10 - j] * partial[i * 15 + 10 - j]) *
                           div_table[2 * j + 2];
            }
        }
        for (i = 0; i < 8; i++)
        {
            if (cost[i] > best_cost)
            {
                best_cost = cost[i];
                best_dir = i;
            }
        }
        /* Difference between the optimal variance and the variance along the
           orthogonal direction. Again, the sum(x^2) terms cancel out. */
        var = best_cost - cost[(best_dir + 4) & 7];
        /* We'd normally divide by 840, but dividing by 1024 is close enough
           for what we're going to do with this. */
        var >>= 10;
        return best_dir;
    }

    internal static void cdef_find_dir_dual_c(Ptr<ushort> img1, Ptr<ushort> img2,
                                              int stride, ref int var1, ref int var2,
                                              int coeff_shift, ref int out1, ref int out2)
    {
        out1 = cdef_find_dir_c(img1, stride, ref var1, coeff_shift);
        out2 = cdef_find_dir_c(img2, stride, ref var2, coeff_shift);
    }

    internal static readonly int[][] cdef_pri_taps = { new[] { 4, 2 }, new[] { 3, 3 } };
    internal static readonly int[] cdef_sec_taps = { 2, 1 };

    /* Smooth in the direction detected. */
    // [port] The uint16_t *dst16 output is high-bitdepth only (cdef_filter_16_*): dropped
    // ([subset] CONFIG_AV1_HIGHBITDEPTH == 0), so dst8 is always written.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void cdef_filter_block_internal(
        Ptr<byte> dst8, int dstride, Ptr<ushort> @in,
        int pri_strength, int sec_strength, int dir, int pri_damping,
        int sec_damping, int coeff_shift, int block_width, int block_height,
        int enable_primary, int enable_secondary)
    {
        int clipping_required = B(enable_primary != 0 && enable_secondary != 0);
        int i, j, k;
        const int s = CDEF_BSTRIDE;
        int[] pri_taps = cdef_pri_taps[(pri_strength >> coeff_shift) & 1];
        int[] sec_taps = cdef_sec_taps;
        for (i = 0; i < block_height; i++)
        {
            for (j = 0; j < block_width; j++)
            {
                short sum = 0;
                short y;
                short x = (short)@in[i * s + j];
                int max = x;
                int min = x;
                for (k = 0; k < 2; k++)
                {
                    if (enable_primary != 0)
                    {
                        short p0 = (short)@in[i * s + j + cdef_directions[dir][k]];
                        short p1 = (short)@in[i * s + j - cdef_directions[dir][k]];
                        sum = (short)(sum + pri_taps[k] * constrain(p0 - x, pri_strength, pri_damping));
                        sum = (short)(sum + pri_taps[k] * constrain(p1 - x, pri_strength, pri_damping));
                        if (clipping_required != 0)
                        {
                            if (p0 != CDEF_VERY_LARGE) max = AOMMAX(p0, max);
                            if (p1 != CDEF_VERY_LARGE) max = AOMMAX(p1, max);
                            min = AOMMIN(p0, min);
                            min = AOMMIN(p1, min);
                        }
                    }
                    if (enable_secondary != 0)
                    {
                        short s0 = (short)@in[i * s + j + cdef_directions[dir + 2][k]];
                        short s1 = (short)@in[i * s + j - cdef_directions[dir + 2][k]];
                        short s2 = (short)@in[i * s + j + cdef_directions[dir - 2][k]];
                        short s3 = (short)@in[i * s + j - cdef_directions[dir - 2][k]];
                        if (clipping_required != 0)
                        {
                            if (s0 != CDEF_VERY_LARGE) max = AOMMAX(s0, max);
                            if (s1 != CDEF_VERY_LARGE) max = AOMMAX(s1, max);
                            if (s2 != CDEF_VERY_LARGE) max = AOMMAX(s2, max);
                            if (s3 != CDEF_VERY_LARGE) max = AOMMAX(s3, max);
                            min = AOMMIN(s0, min);
                            min = AOMMIN(s1, min);
                            min = AOMMIN(s2, min);
                            min = AOMMIN(s3, min);
                        }
                        sum = (short)(sum + sec_taps[k] * constrain(s0 - x, sec_strength, sec_damping));
                        sum = (short)(sum + sec_taps[k] * constrain(s1 - x, sec_strength, sec_damping));
                        sum = (short)(sum + sec_taps[k] * constrain(s2 - x, sec_strength, sec_damping));
                        sum = (short)(sum + sec_taps[k] * constrain(s3 - x, sec_strength, sec_damping));
                    }
                }
                y = (short)(x + ((8 + sum - B(sum < 0)) >> 4));
                if (clipping_required != 0)
                {
                    y = (short)clamp(y, min, max);
                }

                dst8[i * dstride + j] = (byte)y;
            }
        }
    }

    internal static void cdef_filter_8_0_c(Ptr<byte> dst8, int dstride, Ptr<ushort> @in,
                                           int pri_strength, int sec_strength, int dir,
                                           int pri_damping, int sec_damping, int coeff_shift,
                                           int block_width, int block_height)
    {
        cdef_filter_block_internal(dst8, dstride, @in, pri_strength,
                                   sec_strength, dir, pri_damping, sec_damping,
                                   coeff_shift, block_width, block_height,
                                   /*enable_primary=*/1, /*enable_secondary=*/1);
    }

    internal static void cdef_filter_8_1_c(Ptr<byte> dst8, int dstride, Ptr<ushort> @in,
                                           int pri_strength, int sec_strength, int dir,
                                           int pri_damping, int sec_damping, int coeff_shift,
                                           int block_width, int block_height)
    {
        cdef_filter_block_internal(dst8, dstride, @in, pri_strength,
                                   sec_strength, dir, pri_damping, sec_damping,
                                   coeff_shift, block_width, block_height,
                                   /*enable_primary=*/1, /*enable_secondary=*/0);
    }

    internal static void cdef_filter_8_2_c(Ptr<byte> dst8, int dstride, Ptr<ushort> @in,
                                           int pri_strength, int sec_strength, int dir,
                                           int pri_damping, int sec_damping, int coeff_shift,
                                           int block_width, int block_height)
    {
        cdef_filter_block_internal(dst8, dstride, @in, pri_strength,
                                   sec_strength, dir, pri_damping, sec_damping,
                                   coeff_shift, block_width, block_height,
                                   /*enable_primary=*/0, /*enable_secondary=*/1);
    }

    internal static void cdef_filter_8_3_c(Ptr<byte> dst8, int dstride, Ptr<ushort> @in,
                                           int pri_strength, int sec_strength, int dir,
                                           int pri_damping, int sec_damping, int coeff_shift,
                                           int block_width, int block_height)
    {
        cdef_filter_block_internal(dst8, dstride, @in, pri_strength,
                                   sec_strength, dir, pri_damping, sec_damping,
                                   coeff_shift, block_width, block_height,
                                   /*enable_primary=*/0, /*enable_secondary=*/0);
    }

    // [subset] cdef_filter_16_0_c .. cdef_filter_16_3_c: high-bitdepth output only (never
    // executed; CONFIG_AV1_HIGHBITDEPTH == 0).

    /* Compute the primary filter strength for an 8x8 block based on the
       directional variance difference. A high variance difference means
       that we have a highly directional pattern (e.g. a high contrast
       edge), so we can apply more deringing. A low variance means that we
       either have a low contrast edge, or a non-directional texture, so
       we want to be careful not to blur. */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int adjust_strength(int strength, int var)
    {
        int i = (var >> 6) != 0 ? AOMMIN(get_msb(var >> 6), 12) : 0;
        /* We use the variance of 8x8 blocks to adjust the strength. */
        return var != 0 ? (strength * (4 + i) + 8) >> 4 : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void aom_cdef_find_dir(Ptr<ushort> @in, Ptr<cdef_list> dlist,
                                          int[][] var, int cdef_count, int coeff_shift,
                                          int[][] dir)
    {
        int bi;

        // Find direction of two 8x8 blocks together.
        for (bi = 0; bi < cdef_count - 1; bi += 2)
        {
            int by = dlist[bi].by;
            int bx = dlist[bi].bx;
            int by2 = dlist[bi + 1].by;
            int bx2 = dlist[bi + 1].bx;
            int pos1 = 8 * by * CDEF_BSTRIDE + 8 * bx;
            int pos2 = 8 * by2 * CDEF_BSTRIDE + 8 * bx2;
            cdef_find_dir_dual(@in + pos1, @in + pos2, CDEF_BSTRIDE, ref var[by][bx],
                               ref var[by2][bx2], coeff_shift, ref dir[by][bx],
                               ref dir[by2][bx2]);
        }

        // Process remaining 8x8 blocks here. One 8x8 at a time.
        if ((cdef_count % 2) != 0)
        {
            int by = dlist[bi].by;
            int bx = dlist[bi].bx;
            dir[by][bx] = cdef_find_dir(@in + (8 * by * CDEF_BSTRIDE + 8 * bx),
                                        CDEF_BSTRIDE, ref var[by][bx], coeff_shift);
        }
    }

    /*
     * strength_index == 0 : enable_primary = 1, enable_secondary = 1
     * strength_index == 1 : enable_primary = 1, enable_secondary = 0
     * strength_index == 2 : enable_primary = 0, enable_secondary = 1
     * strength_index == 3 : enable_primary = 0, enable_secondary = 0
     */
    // [av1rt] The function-local `const cdef_filter_block_func cdef_filter_fn[4]` of the dst8
    // branch (cdef_filter_8_0 .. cdef_filter_8_3) is a switch in av1_cdef_filter_fb.

    // [port] `int dir[CDEF_NBLOCKS][CDEF_NBLOCKS]` / `var[..][..]` are jagged arrays; `int *dirinit`
    // is a Ptr<int> because callers pass NULL (cdef.c) or the address of a flag (pickcdef.c).
    internal static void av1_cdef_filter_fb(Ptr<byte> dst8, Ptr<ushort> dst16, int dstride,
                                            Ptr<ushort> @in, int xdec, int ydec,
                                            int[][] dir, Ptr<int> dirinit,
                                            int[][] var, int pli,
                                            Ptr<cdef_list> dlist, int cdef_count, int level,
                                            int sec_strength, int damping, int coeff_shift)
    {
        int bi;
        int bx;
        int by;
        int pri_strength = level << coeff_shift;
        sec_strength <<= coeff_shift;
        damping += coeff_shift - B(pli != AOM_PLANE_Y);
        int bw_log2 = 3 - xdec;
        int bh_log2 = 3 - ydec;
        if (!dirinit.IsNull && pri_strength == 0 && sec_strength == 0)
        {
            // [subset] The zero-strength copy into dst16 is only reached from the high-bitdepth
            // CDEF search (the 8-bit search computes the zero-strength error without filtering,
            // pickcdef.c get_filt_error); it never executed. dst16 is NULL in 8-bit.
            throw new NotSupportedException("av1_cdef_filter_fb: zero-strength dst16 copy (high bitdepth)");
        }

        if (pli == 0)
        {
            if (dirinit.IsNull || dirinit.Value == 0)
            {
                aom_cdef_find_dir(@in, dlist, var, cdef_count, coeff_shift, dir);
                if (!dirinit.IsNull) dirinit.Value = 1;
            }
        }
        if (pli == 1 && xdec != ydec)
        {
            // [subset] conv422 / conv440 direction remapping: 4:2:2 and 4:4:0 are not in the
            // subset (4:2:0 only); never executed.
            throw new NotSupportedException("av1_cdef_filter_fb: 4:2:2 / 4:4:0 chroma");
        }

        if (!dst8.IsNull)
        {
            int block_width = 8 >> xdec;
            int block_height = 8 >> ydec;
            // [av1rt] cdef_filter_fn_8[strength_index](...) as a switch: direct calls the JIT
            // can inline, instead of a delegate call per 8x8 block.
            for (bi = 0; bi < cdef_count; bi++)
            {
                by = dlist[bi].by;
                bx = dlist[bi].bx;
                int t =
                    (pli != 0 ? pri_strength : adjust_strength(pri_strength, var[by][bx]));
                int strength_index = B(sec_strength == 0) | (B(t == 0) << 1);

                Ptr<byte> d8 = dst8 + ((by << bh_log2) * dstride + (bx << bw_log2));
                Ptr<ushort> in8 = @in + ((by * CDEF_BSTRIDE << bh_log2) + (bx << bw_log2));
                int d = pri_strength != 0 ? dir[by][bx] : 0;
                switch (strength_index)
                {
                    case 0:
                        cdef_filter_8_0(d8, dstride, in8, t, sec_strength, d, damping, damping,
                                        coeff_shift, block_width, block_height);
                        break;
                    case 1:
                        cdef_filter_8_1(d8, dstride, in8, t, sec_strength, d, damping, damping,
                                        coeff_shift, block_width, block_height);
                        break;
                    case 2:
                        cdef_filter_8_2(d8, dstride, in8, t, sec_strength, d, damping, damping,
                                        coeff_shift, block_width, block_height);
                        break;
                    default:
                        cdef_filter_8_3(d8, dstride, in8, t, sec_strength, d, damping, damping,
                                        coeff_shift, block_width, block_height);
                        break;
                }
            }
        }
        else
        {
            // [subset] dst16 output (cdef_filter_16_*): high bitdepth only; never executed.
            Debug.Assert(dst16.IsNull);
            throw new NotSupportedException("av1_cdef_filter_fb: 16-bit destination (high bitdepth)");
        }
    }

    // ---- RTCD dispatch (av1_rtcd.h): AVX2 (av1/common/x86/cdef_block_avx2.c) or C ----------
    internal static int cdef_find_dir(Ptr<ushort> img, int stride, ref int var, int coeff_shift) => Simd.UseAvx2 ? cdef_find_dir_avx2(img, stride, ref var, coeff_shift) : cdef_find_dir_c(img, stride, ref var, coeff_shift);
    internal static void cdef_find_dir_dual(Ptr<ushort> img1, Ptr<ushort> img2, int stride, ref int var1, ref int var2, int coeff_shift, ref int out1, ref int out2) { if (Simd.UseAvx2) cdef_find_dir_dual_avx2(img1, img2, stride, ref var1, ref var2, coeff_shift, ref out1, ref out2); else cdef_find_dir_dual_c(img1, img2, stride, ref var1, ref var2, coeff_shift, ref out1, ref out2); }
    internal static void cdef_filter_8_0(Ptr<byte> dest, int dstride, Ptr<ushort> @in, int pri_strength, int sec_strength, int dir, int pri_damping, int sec_damping, int coeff_shift, int block_width, int block_height) { if (Simd.UseAvx2) cdef_filter_8_0_avx2(dest, dstride, @in, pri_strength, sec_strength, dir, pri_damping, sec_damping, coeff_shift, block_width, block_height); else cdef_filter_8_0_c(dest, dstride, @in, pri_strength, sec_strength, dir, pri_damping, sec_damping, coeff_shift, block_width, block_height); }
    internal static void cdef_filter_8_1(Ptr<byte> dest, int dstride, Ptr<ushort> @in, int pri_strength, int sec_strength, int dir, int pri_damping, int sec_damping, int coeff_shift, int block_width, int block_height) { if (Simd.UseAvx2) cdef_filter_8_1_avx2(dest, dstride, @in, pri_strength, sec_strength, dir, pri_damping, sec_damping, coeff_shift, block_width, block_height); else cdef_filter_8_1_c(dest, dstride, @in, pri_strength, sec_strength, dir, pri_damping, sec_damping, coeff_shift, block_width, block_height); }
    internal static void cdef_filter_8_2(Ptr<byte> dest, int dstride, Ptr<ushort> @in, int pri_strength, int sec_strength, int dir, int pri_damping, int sec_damping, int coeff_shift, int block_width, int block_height) { if (Simd.UseAvx2) cdef_filter_8_2_avx2(dest, dstride, @in, pri_strength, sec_strength, dir, pri_damping, sec_damping, coeff_shift, block_width, block_height); else cdef_filter_8_2_c(dest, dstride, @in, pri_strength, sec_strength, dir, pri_damping, sec_damping, coeff_shift, block_width, block_height); }
    internal static void cdef_filter_8_3(Ptr<byte> dest, int dstride, Ptr<ushort> @in, int pri_strength, int sec_strength, int dir, int pri_damping, int sec_damping, int coeff_shift, int block_width, int block_height) { if (Simd.UseAvx2) cdef_filter_8_3_avx2(dest, dstride, @in, pri_strength, sec_strength, dir, pri_damping, sec_damping, coeff_shift, block_width, block_height); else cdef_filter_8_3_c(dest, dstride, @in, pri_strength, sec_strength, dir, pri_damping, sec_damping, coeff_shift, block_width, block_height); }
}
