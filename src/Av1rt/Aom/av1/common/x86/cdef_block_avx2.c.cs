// Port of libaom av1/common/x86/cdef_block_avx2.c

global using static Av1rt.Aom.cdef_block_avx2;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Av1rt.Aom;

// [port] `#define SIMD_FUNC(name) name##_avx2` + `#include "av1/common/cdef_block_simd.h"`:
// that instantiation (cdef_find_dir_avx2, cdef_filter_8_0_avx2 .. cdef_filter_8_3_avx2) is
// av1/common/cdef_block_simd.h.cs, the other half of this partial class.
internal static partial class cdef_block_avx2
{
    // Mask used to shuffle the elements present in 256bit register.
    // [port] The function-local `static const int shuffle_reg_256bit[8]`.
    private static ReadOnlySpan<int> shuffle_reg_256bit => new int[] {
        0x0b0a0d0c, 0x07060908, 0x03020504, 0x0f0e0100,
        0x0b0a0d0c, 0x07060908, 0x03020504, 0x0f0e0100 };

    /* partial A is a 16-bit vector of the form:
    [x8 - - x1 | x16 - - x9] and partial B has the form:
    [0  y1 - y7 | 0 y9 - y15].
    This function computes (x1^2+y1^2)*C1 + (x2^2+y2^2)*C2 + ...
    (x7^2+y2^7)*C7 + (x8^2+0^2)*C8 on each 128-bit lane. Here the C1..C8 constants
    are in const1 and const2. */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m256i fold_mul_and_sum_avx2(ref __m256i partiala,
                                                 ref __m256i partialb,
                                                 in __m256i const1,
                                                 in __m256i const2)
    {
        __m256i tmp;
        /* Reverse partial B. */
        partialb = _mm256_shuffle_epi8(
            partialb, _mm256_loadu_si256(shuffle_reg_256bit));

        /* Interleave the x and y values of identical indices and pair x8 with 0. */
        tmp = partiala;
        partiala = _mm256_unpacklo_epi16(partiala, partialb);
        partialb = _mm256_unpackhi_epi16(tmp, partialb);

        /* Square and add the corresponding x and y values. */
        partiala = _mm256_madd_epi16(partiala, partiala);
        partialb = _mm256_madd_epi16(partialb, partialb);
        /* Multiply by constant. */
        partiala = _mm256_mullo_epi32(partiala, const1);
        partialb = _mm256_mullo_epi32(partialb, const2);
        /* Sum all results. */
        partiala = _mm256_add_epi32(partiala, partialb);
        return partiala;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m256i hsum4_avx2(ref __m256i x0, ref __m256i x1, ref __m256i x2,
                                      ref __m256i x3)
    {
        __m256i t0 = _mm256_unpacklo_epi32(x0, x1);
        __m256i t1 = _mm256_unpacklo_epi32(x2, x3);
        __m256i t2 = _mm256_unpackhi_epi32(x0, x1);
        __m256i t3 = _mm256_unpackhi_epi32(x2, x3);

        x0 = _mm256_unpacklo_epi64(t0, t1);
        x1 = _mm256_unpackhi_epi64(t0, t1);
        x2 = _mm256_unpacklo_epi64(t2, t3);
        x3 = _mm256_unpackhi_epi64(t2, t3);
        return _mm256_add_epi32(_mm256_add_epi32(x0, x1),
                                _mm256_add_epi32(x2, x3));
    }

    /* Computes cost for directions 0, 5, 6 and 7. We can call this function again
    to compute the remaining directions. */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // [av1rt] lines[0..7] are the values l0..l7, and the costs are only returned: C also stores
    // them to cost_frist_8x8 / cost_second_8x8, which cdef_find_dir_dual_avx2 reads from the
    // returned vector instead (low lane: first 8x8, high lane: second).
    private static __m256i compute_directions_avx2(__m256i l0, __m256i l1, __m256i l2, __m256i l3,
                                                   __m256i l4, __m256i l5, __m256i l6, __m256i l7)
    {
        __m256i partial4a, partial4b, partial5a, partial5b, partial7a, partial7b;
        __m256i partial6;
        __m256i tmp;
        /* Partial sums for lines 0 and 1. */
        partial4a = _mm256_slli_si256(l0, 14);
        partial4b = _mm256_srli_si256(l0, 2);
        partial4a = _mm256_add_epi16(partial4a, _mm256_slli_si256(l1, 12));
        partial4b = _mm256_add_epi16(partial4b, _mm256_srli_si256(l1, 4));
        tmp = _mm256_add_epi16(l0, l1);
        partial5a = _mm256_slli_si256(tmp, 10);
        partial5b = _mm256_srli_si256(tmp, 6);
        partial7a = _mm256_slli_si256(tmp, 4);
        partial7b = _mm256_srli_si256(tmp, 12);
        partial6 = tmp;

        /* Partial sums for lines 2 and 3. */
        partial4a = _mm256_add_epi16(partial4a, _mm256_slli_si256(l2, 10));
        partial4b = _mm256_add_epi16(partial4b, _mm256_srli_si256(l2, 6));
        partial4a = _mm256_add_epi16(partial4a, _mm256_slli_si256(l3, 8));
        partial4b = _mm256_add_epi16(partial4b, _mm256_srli_si256(l3, 8));
        tmp = _mm256_add_epi16(l2, l3);
        partial5a = _mm256_add_epi16(partial5a, _mm256_slli_si256(tmp, 8));
        partial5b = _mm256_add_epi16(partial5b, _mm256_srli_si256(tmp, 8));
        partial7a = _mm256_add_epi16(partial7a, _mm256_slli_si256(tmp, 6));
        partial7b = _mm256_add_epi16(partial7b, _mm256_srli_si256(tmp, 10));
        partial6 = _mm256_add_epi16(partial6, tmp);

        /* Partial sums for lines 4 and 5. */
        partial4a = _mm256_add_epi16(partial4a, _mm256_slli_si256(l4, 6));
        partial4b = _mm256_add_epi16(partial4b, _mm256_srli_si256(l4, 10));
        partial4a = _mm256_add_epi16(partial4a, _mm256_slli_si256(l5, 4));
        partial4b = _mm256_add_epi16(partial4b, _mm256_srli_si256(l5, 12));
        tmp = _mm256_add_epi16(l4, l5);
        partial5a = _mm256_add_epi16(partial5a, _mm256_slli_si256(tmp, 6));
        partial5b = _mm256_add_epi16(partial5b, _mm256_srli_si256(tmp, 10));
        partial7a = _mm256_add_epi16(partial7a, _mm256_slli_si256(tmp, 8));
        partial7b = _mm256_add_epi16(partial7b, _mm256_srli_si256(tmp, 8));
        partial6 = _mm256_add_epi16(partial6, tmp);

        /* Partial sums for lines 6 and 7. */
        partial4a = _mm256_add_epi16(partial4a, _mm256_slli_si256(l6, 2));
        partial4b = _mm256_add_epi16(partial4b, _mm256_srli_si256(l6, 14));
        partial4a = _mm256_add_epi16(partial4a, l7);
        tmp = _mm256_add_epi16(l6, l7);
        partial5a = _mm256_add_epi16(partial5a, _mm256_slli_si256(tmp, 4));
        partial5b = _mm256_add_epi16(partial5b, _mm256_srli_si256(tmp, 12));
        partial7a = _mm256_add_epi16(partial7a, _mm256_slli_si256(tmp, 10));
        partial7b = _mm256_add_epi16(partial7b, _mm256_srli_si256(tmp, 6));
        partial6 = _mm256_add_epi16(partial6, tmp);

        __m256i const_reg_1 =
            _mm256_set_epi32(210, 280, 420, 840, 210, 280, 420, 840);
        __m256i const_reg_2 =
            _mm256_set_epi32(105, 120, 140, 168, 105, 120, 140, 168);
        __m256i const_reg_3 = _mm256_set_epi32(210, 420, 0, 0, 210, 420, 0, 0);
        __m256i const_reg_4 =
            _mm256_set_epi32(105, 105, 105, 140, 105, 105, 105, 140);

        /* Compute costs in terms of partial sums. */
        partial4a =
            fold_mul_and_sum_avx2(ref partial4a, ref partial4b, in const_reg_1, in const_reg_2);
        partial7a =
            fold_mul_and_sum_avx2(ref partial7a, ref partial7b, in const_reg_3, in const_reg_4);
        partial5a =
            fold_mul_and_sum_avx2(ref partial5a, ref partial5b, in const_reg_3, in const_reg_4);
        partial6 = _mm256_madd_epi16(partial6, partial6);
        partial6 = _mm256_mullo_epi32(partial6, _mm256_set1_epi32(105));

        partial4a = hsum4_avx2(ref partial4a, ref partial5a, ref partial6, ref partial7a);
        return partial4a;
    }

    // [av1rt] Line `off` of the two 8x8 blocks: (img >> coeff_shift) - 128, first block in the
    // low lane.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m256i load_line(ReadOnlySpan<ushort> a1, ReadOnlySpan<ushort> a2, int off,
                                     __m128i shift, __m256i c128)
    {
        __m256i l = _mm256_insertf128_si256(_mm256_castsi128_si256(_mm_loadu_si128(a1.Slice(off, 8))),
                                            _mm_loadu_si128(a2.Slice(off, 8)), 1);
        return _mm256_sub_epi16(_mm256_sra_epi16(l, shift), c128);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void cdef_find_dir_dual_avx2(Ptr<ushort> img1, Ptr<ushort> img2,
                                                 int stride, ref int var_out_1st,
                                                 ref int var_out_2nd, int coeff_shift,
                                                 ref int out_dir_1st_8x8, ref int out_dir_2nd_8x8)
    {
        // [av1rt] In registers as in C: the lines are l0..l7 (C: __m256i lines[8]), the
        // transpose is inline (array_reverse_transpose_8x8_avx2) and the costs are read from
        // dir03 / dir47 (C: cost_first_8x8[8] / cost_second_8x8[8]); best_cost / best_dir are
        // scalars. No zeroed stackalloc buffers.
        __m128i const_coeff_shift_reg = _mm_cvtsi32_si128(coeff_shift);
        __m256i const_128_reg = _mm256_set1_epi16(128);
        ReadOnlySpan<ushort> a1 = img1.AsSpan(0, 7 * stride + 8), a2 = img2.AsSpan(0, 7 * stride + 8);
        __m256i l0 = load_line(a1, a2, 0, const_coeff_shift_reg, const_128_reg);
        __m256i l1 = load_line(a1, a2, stride, const_coeff_shift_reg, const_128_reg);
        __m256i l2 = load_line(a1, a2, 2 * stride, const_coeff_shift_reg, const_128_reg);
        __m256i l3 = load_line(a1, a2, 3 * stride, const_coeff_shift_reg, const_128_reg);
        __m256i l4 = load_line(a1, a2, 4 * stride, const_coeff_shift_reg, const_128_reg);
        __m256i l5 = load_line(a1, a2, 5 * stride, const_coeff_shift_reg, const_128_reg);
        __m256i l6 = load_line(a1, a2, 6 * stride, const_coeff_shift_reg, const_128_reg);
        __m256i l7 = load_line(a1, a2, 7 * stride, const_coeff_shift_reg, const_128_reg);

        /* Compute "mostly vertical" directions. */
        __m256i dir47 = compute_directions_avx2(l0, l1, l2, l3, l4, l5, l6, l7);

        /* Transpose and reverse the order of the lines. */
        {
            __m256i tr0_0 = _mm256_unpacklo_epi16(l0, l1);
            __m256i tr0_1 = _mm256_unpacklo_epi16(l2, l3);
            __m256i tr0_2 = _mm256_unpackhi_epi16(l0, l1);
            __m256i tr0_3 = _mm256_unpackhi_epi16(l2, l3);
            __m256i tr0_4 = _mm256_unpacklo_epi16(l4, l5);
            __m256i tr0_5 = _mm256_unpacklo_epi16(l6, l7);
            __m256i tr0_6 = _mm256_unpackhi_epi16(l4, l5);
            __m256i tr0_7 = _mm256_unpackhi_epi16(l6, l7);

            __m256i tr1_0 = _mm256_unpacklo_epi32(tr0_0, tr0_1);
            __m256i tr1_1 = _mm256_unpacklo_epi32(tr0_4, tr0_5);
            __m256i tr1_2 = _mm256_unpackhi_epi32(tr0_0, tr0_1);
            __m256i tr1_3 = _mm256_unpackhi_epi32(tr0_4, tr0_5);
            __m256i tr1_4 = _mm256_unpacklo_epi32(tr0_2, tr0_3);
            __m256i tr1_5 = _mm256_unpacklo_epi32(tr0_6, tr0_7);
            __m256i tr1_6 = _mm256_unpackhi_epi32(tr0_2, tr0_3);
            __m256i tr1_7 = _mm256_unpackhi_epi32(tr0_6, tr0_7);
            l0 = _mm256_unpackhi_epi64(tr1_6, tr1_7);
            l1 = _mm256_unpacklo_epi64(tr1_6, tr1_7);
            l2 = _mm256_unpackhi_epi64(tr1_4, tr1_5);
            l3 = _mm256_unpacklo_epi64(tr1_4, tr1_5);
            l4 = _mm256_unpackhi_epi64(tr1_2, tr1_3);
            l5 = _mm256_unpacklo_epi64(tr1_2, tr1_3);
            l6 = _mm256_unpackhi_epi64(tr1_0, tr1_1);
            l7 = _mm256_unpacklo_epi64(tr1_0, tr1_1);
        }

        /* Compute "mostly horizontal" directions. */
        __m256i dir03 = compute_directions_avx2(l0, l1, l2, l3, l4, l5, l6, l7);

        __m256i max = _mm256_max_epi32(dir03, dir47);
        max =
            _mm256_max_epi32(max, _mm256_or_si256(_mm256_srli_si256(max, 8),
                                                  _mm256_slli_si256(max, 16 - (8))));
        max =
            _mm256_max_epi32(max, _mm256_or_si256(_mm256_srli_si256(max, 4),
                                                  _mm256_slli_si256(max, 16 - (4))));

        __m128i first_8x8_output = _mm256_castsi256_si128(max);
        __m128i second_8x8_output = _mm256_extractf128_si256(max, 1);
        __m128i cmpeg_res_00 =
            _mm_cmpeq_epi32(first_8x8_output, _mm256_castsi256_si128(dir47));
        __m128i cmpeg_res_01 =
            _mm_cmpeq_epi32(first_8x8_output, _mm256_castsi256_si128(dir03));
        __m128i cmpeg_res_10 =
            _mm_cmpeq_epi32(second_8x8_output, _mm256_extractf128_si256(dir47, 1));
        __m128i cmpeg_res_11 =
            _mm_cmpeq_epi32(second_8x8_output, _mm256_extractf128_si256(dir03, 1));
        __m128i t_first_8x8 = _mm_packs_epi32(cmpeg_res_01, cmpeg_res_00);
        __m128i t_second_8x8 = _mm_packs_epi32(cmpeg_res_11, cmpeg_res_10);

        int best_cost_0 = _mm_cvtsi128_si32(_mm256_castsi256_si128(max));
        int best_cost_1 = _mm_cvtsi128_si32(second_8x8_output);
        int best_dir_0 = _mm_movemask_epi8(_mm_packs_epi16(t_first_8x8, t_first_8x8));
        best_dir_0 = get_msb(best_dir_0 ^ (best_dir_0 - 1));  // Count trailing zeros
        int best_dir_1 = _mm_movemask_epi8(_mm_packs_epi16(t_second_8x8, t_second_8x8));
        best_dir_1 = get_msb(best_dir_1 ^ (best_dir_1 - 1));  // Count trailing zeros

        /* Difference between the optimal variance and the variance along the
           orthogonal direction. Again, the sum(x^2) terms cancel out. */
        // cost_first_8x8[k] / cost_second_8x8[k]: lane k & 3 of the low / high half of dir03
        // (k < 4) or dir47.
        int k0 = (best_dir_0 + 4) & 7, k1 = (best_dir_1 + 4) & 7;
        var_out_1st = best_cost_0 - (k0 < 4 ? dir03 : dir47).GetLower().AsInt32().GetElement(k0 & 3);
        var_out_2nd = best_cost_1 - (k1 < 4 ? dir03 : dir47).GetUpper().AsInt32().GetElement(k1 & 3);

        /* We'd normally divide by 840, but dividing by 1024 is close enough
        for what we're going to do with this. */
        var_out_1st >>= 10;
        var_out_2nd >>= 10;
        out_dir_1st_8x8 = best_dir_0;
        out_dir_2nd_8x8 = best_dir_1;
    }

    // [av1rt] Per row, within one checked window per side: 32 / 16 / 8 pixels per step, then
    // the rest (C: the same widths column block by column block, two rows per step). Same
    // result: dst[i * dstride + k] = src[i * sstride + k].
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void cdef_copy_rect8_8bit_to_16bit_avx2(Ptr<ushort> dst, int dstride,
                                                            Ptr<byte> src, int sstride,
                                                            int width, int height)
    {
        Debug.Assert(height % 2 == 0);
        Debug.Assert(height > 0);
        Debug.Assert(width > 0);
        ReadOnlySpan<byte> s = src.AsSpan(0, (height - 1) * sstride + width);
        Span<ushort> d = dst.AsSpan(0, (height - 1) * dstride + width);
        for (int i = 0; i < height; i++)
        {
            ReadOnlySpan<byte> sr = s.Slice(i * sstride, width);
            Span<ushort> dr = d.Slice(i * dstride, width);
            int k = 0;
            for (; k <= width - 32; k += 32)
            {
                (Vector256<ushort> lo, Vector256<ushort> hi) = Vector256.Widen(Vector256.Create(sr.Slice(k, 32)));
                lo.CopyTo(dr.Slice(k, 16));
                hi.CopyTo(dr.Slice(k + 16, 16));
            }
            if (k <= width - 16)
            {
                Vector128<byte> v = Vector128.Create(sr.Slice(k, 16));
                Vector256.Create(Vector128.WidenLower(v), Vector128.WidenUpper(v)).CopyTo(dr.Slice(k, 16));
                k += 16;
            }
            if (k <= width - 8)
            {
                Vector128.WidenLower(Vector128.CreateScalar(MemoryMarshal.Read<ulong>(sr.Slice(k, 8))).AsByte())
                    .CopyTo(dr.Slice(k, 8));
                k += 8;
            }
            for (; k < width; k++) dr[k] = sr[k];
        }
    }
}
