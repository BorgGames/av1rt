// Port of libaom aom_dsp/x86/sad4d_avx2.c

global using static Av1rt.Aom.sad4d_avx2;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] `const uint8_t *const ref[4]` is Ptr<Ptr<byte>>, `uint32_t res[4]` is Ptr<uint> (like the
// scalar sad.c.cs). The SADMXN_AVX2 / SAD_SKIP_MXN_AVX2 / SAD16XNX3_AVX2 / SAD16XNX4_AVX2 /
// SAD_SKIP_16XN_AVX2 instantiations are written out for the sizes compiled with
// CONFIG_REALTIME_ONLY and !CONFIG_HIGHWAY.
internal static class sad4d_avx2
{
    // AOM_FORCE_INLINE
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void aggregate_and_store_sum(Ptr<uint> res,
                                                in __m256i sum_ref0,
                                                in __m256i sum_ref1,
                                                in __m256i sum_ref2,
                                                in __m256i sum_ref3)
    {
        // In sum_ref-i the result is saved in the first 4 bytes and the other 4
        // bytes are zeroed.
        // merge sum_ref0 and sum_ref1 also sum_ref2 and sum_ref3
        // 0, 0, 1, 1
        __m256i sum_ref01 = _mm256_castps_si256(_mm256_shuffle_ps(
            _mm256_castsi256_ps(sum_ref0), _mm256_castsi256_ps(sum_ref1),
            0x88 /* _MM_SHUFFLE(2, 0, 2, 0) */));
        // 2, 2, 3, 3
        __m256i sum_ref23 = _mm256_castps_si256(_mm256_shuffle_ps(
            _mm256_castsi256_ps(sum_ref2), _mm256_castsi256_ps(sum_ref3),
            0x88 /* _MM_SHUFFLE(2, 0, 2, 0) */));

        // sum adjacent 32 bit integers
        __m256i sum_ref0123 = _mm256_hadd_epi32(sum_ref01, sum_ref23);

        // add the low 128 bit to the high 128 bit
        __m128i sum = _mm_add_epi32(_mm256_castsi256_si128(sum_ref0123),
                                    _mm256_extractf128_si256(sum_ref0123, 1));

        _mm_storeu_si128(res, sum);
    }

    // [port] Bounds checks: each row is sliced once per buffer (M bytes) and the 32-byte loads at
    // constant offsets inside it need no further checks, where loading through Ptr checks every
    // load (and was the larger part of the instructions). C's peeled first row folds into
    // zero-initialized sums (integer adds, same result). M is a constant after inlining, so the
    // M checks below fold away; widths beyond 64 take the generic loop.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m256i Sad32(ReadOnlySpan<byte> r, __m256i src_reg) =>
        _mm256_sad_epu8(_mm256_loadu_si256(r), src_reg);

    // AOM_FORCE_INLINE
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void aom_sadMxNx4d_avx2(
        int M, int N, Ptr<byte> src, int src_stride,
        Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res)
    {
        __m256i src_reg;
        __m256i sum_ref0, sum_ref1, sum_ref2, sum_ref3;
        int i, j;
        Ptr<byte> ref0 = @ref[0], ref1 = @ref[1], ref2 = @ref[2], ref3 = @ref[3];
        ReadOnlySpan<byte> sbuf = src.Array, buf0 = ref0.Array, buf1 = ref1.Array,
                           buf2 = ref2.Array, buf3 = ref3.Array;
        int so = src.Offset, o0 = ref0.Offset, o1 = ref1.Offset, o2 = ref2.Offset,
            o3 = ref3.Offset;

        sum_ref0 = _mm256_setzero_si256();
        sum_ref1 = _mm256_setzero_si256();
        sum_ref2 = _mm256_setzero_si256();
        sum_ref3 = _mm256_setzero_si256();
        for (i = 0; i < N; i++)
        {
            ReadOnlySpan<byte> s = sbuf.Slice(so, M), r0 = buf0.Slice(o0, M),
                               r1 = buf1.Slice(o1, M), r2 = buf2.Slice(o2, M),
                               r3 = buf3.Slice(o3, M);
            // sum of the absolute differences between every ref-i to src
            src_reg = _mm256_loadu_si256(s);
            sum_ref0 = _mm256_add_epi32(sum_ref0, Sad32(r0, src_reg));
            sum_ref1 = _mm256_add_epi32(sum_ref1, Sad32(r1, src_reg));
            sum_ref2 = _mm256_add_epi32(sum_ref2, Sad32(r2, src_reg));
            sum_ref3 = _mm256_add_epi32(sum_ref3, Sad32(r3, src_reg));
            if (M == 64)
            {
                src_reg = _mm256_loadu_si256(s.Slice(32));
                sum_ref0 = _mm256_add_epi32(sum_ref0, Sad32(r0.Slice(32), src_reg));
                sum_ref1 = _mm256_add_epi32(sum_ref1, Sad32(r1.Slice(32), src_reg));
                sum_ref2 = _mm256_add_epi32(sum_ref2, Sad32(r2.Slice(32), src_reg));
                sum_ref3 = _mm256_add_epi32(sum_ref3, Sad32(r3.Slice(32), src_reg));
            }
            else
            {
                for (j = 32; j < M; j += 32)
                {
                    src_reg = _mm256_loadu_si256(s.Slice(j, 32));
                    sum_ref0 = _mm256_add_epi32(sum_ref0, Sad32(r0.Slice(j, 32), src_reg));
                    sum_ref1 = _mm256_add_epi32(sum_ref1, Sad32(r1.Slice(j, 32), src_reg));
                    sum_ref2 = _mm256_add_epi32(sum_ref2, Sad32(r2.Slice(j, 32), src_reg));
                    sum_ref3 = _mm256_add_epi32(sum_ref3, Sad32(r3.Slice(j, 32), src_reg));
                }
            }
            so += src_stride;
            o0 += ref_stride;
            o1 += ref_stride;
            o2 += ref_stride;
            o3 += ref_stride;
        }

        aggregate_and_store_sum(res, in sum_ref0, in sum_ref1, in sum_ref2, in sum_ref3);
    }

    // AOM_FORCE_INLINE
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void aom_sadMxNx3d_avx2(
        int M, int N, Ptr<byte> src, int src_stride,
        Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res)
    {
        __m256i src_reg;
        __m256i sum_ref0, sum_ref1, sum_ref2;
        int i, j;
        Ptr<byte> ref0 = @ref[0], ref1 = @ref[1], ref2 = @ref[2];
        ReadOnlySpan<byte> sbuf = src.Array, buf0 = ref0.Array, buf1 = ref1.Array,
                           buf2 = ref2.Array;
        int so = src.Offset, o0 = ref0.Offset, o1 = ref1.Offset, o2 = ref2.Offset;
        __m256i zero = _mm256_setzero_si256();

        sum_ref0 = _mm256_setzero_si256();
        sum_ref2 = _mm256_setzero_si256();
        sum_ref1 = _mm256_setzero_si256();

        for (i = 0; i < N; i++)
        {
            ReadOnlySpan<byte> s = sbuf.Slice(so, M), r0 = buf0.Slice(o0, M),
                               r1 = buf1.Slice(o1, M), r2 = buf2.Slice(o2, M);
            src_reg = _mm256_loadu_si256(s);
            sum_ref0 = _mm256_add_epi32(sum_ref0, Sad32(r0, src_reg));
            sum_ref1 = _mm256_add_epi32(sum_ref1, Sad32(r1, src_reg));
            sum_ref2 = _mm256_add_epi32(sum_ref2, Sad32(r2, src_reg));
            if (M == 64)
            {
                src_reg = _mm256_loadu_si256(s.Slice(32));
                sum_ref0 = _mm256_add_epi32(sum_ref0, Sad32(r0.Slice(32), src_reg));
                sum_ref1 = _mm256_add_epi32(sum_ref1, Sad32(r1.Slice(32), src_reg));
                sum_ref2 = _mm256_add_epi32(sum_ref2, Sad32(r2.Slice(32), src_reg));
            }
            else
            {
                for (j = 32; j < M; j += 32)
                {
                    src_reg = _mm256_loadu_si256(s.Slice(j, 32));
                    sum_ref0 = _mm256_add_epi32(sum_ref0, Sad32(r0.Slice(j, 32), src_reg));
                    sum_ref1 = _mm256_add_epi32(sum_ref1, Sad32(r1.Slice(j, 32), src_reg));
                    sum_ref2 = _mm256_add_epi32(sum_ref2, Sad32(r2.Slice(j, 32), src_reg));
                }
            }
            so += src_stride;
            o0 += ref_stride;
            o1 += ref_stride;
            o2 += ref_stride;
        }
        aggregate_and_store_sum(res, in sum_ref0, in sum_ref1, in sum_ref2, in zero);
    }

    // SADMXN_AVX2(32, 16), (32, 32), (32, 64), (64, 32), (64, 64), (64, 128), (128, 64),
    // (128, 128)
    internal static void aom_sad32x16x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx4d_avx2(32, 16, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad32x16x3d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx3d_avx2(32, 16, src, src_stride, @ref, ref_stride, res);

    internal static void aom_sad32x32x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx4d_avx2(32, 32, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad32x32x3d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx3d_avx2(32, 32, src, src_stride, @ref, ref_stride, res);

    internal static void aom_sad32x64x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx4d_avx2(32, 64, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad32x64x3d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx3d_avx2(32, 64, src, src_stride, @ref, ref_stride, res);

    internal static void aom_sad64x32x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx4d_avx2(64, 32, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad64x32x3d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx3d_avx2(64, 32, src, src_stride, @ref, ref_stride, res);

    internal static void aom_sad64x64x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx4d_avx2(64, 64, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad64x64x3d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx3d_avx2(64, 64, src, src_stride, @ref, ref_stride, res);

    internal static void aom_sad64x128x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx4d_avx2(64, 128, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad64x128x3d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx3d_avx2(64, 128, src, src_stride, @ref, ref_stride, res);

    internal static void aom_sad128x64x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx4d_avx2(128, 64, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad128x64x3d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx3d_avx2(128, 64, src, src_stride, @ref, ref_stride, res);

    internal static void aom_sad128x128x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx4d_avx2(128, 128, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad128x128x3d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sadMxNx3d_avx2(128, 128, src, src_stride, @ref, ref_stride, res);

    // [port] The body of SAD_SKIP_MXN_AVX2(m, n).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SAD_SKIP_MXN_AVX2(int m, int n, Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res)
    {
        aom_sadMxNx4d_avx2(m, ((n) >> 1), src, 2 * src_stride, @ref,
                           2 * ref_stride, res);
        res[0] <<= 1;
        res[1] <<= 1;
        res[2] <<= 1;
        res[3] <<= 1;
    }

    internal static void aom_sad_skip_32x16x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        SAD_SKIP_MXN_AVX2(32, 16, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad_skip_32x32x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        SAD_SKIP_MXN_AVX2(32, 32, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad_skip_32x64x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        SAD_SKIP_MXN_AVX2(32, 64, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad_skip_64x32x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        SAD_SKIP_MXN_AVX2(64, 32, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad_skip_64x64x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        SAD_SKIP_MXN_AVX2(64, 64, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad_skip_64x128x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        SAD_SKIP_MXN_AVX2(64, 128, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad_skip_128x64x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        SAD_SKIP_MXN_AVX2(128, 64, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad_skip_128x128x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        SAD_SKIP_MXN_AVX2(128, 128, src, src_stride, @ref, ref_stride, res);

    // AOM_FORCE_INLINE
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void aom_sad16xNx3d_avx2(int N, Ptr<byte> src,
                                            int src_stride,
                                            Ptr<Ptr<byte>> @ref,
                                            int ref_stride,
                                            Ptr<uint> res)
    {
        __m256i src_reg, ref0_reg, ref1_reg, ref2_reg;
        __m256i sum_ref0, sum_ref1, sum_ref2;
        Ptr<byte> ref0, ref1, ref2;
        __m256i zero = _mm256_setzero_si256();
        System.Diagnostics.Debug.Assert(N % 2 == 0);

        ref0 = @ref[0];
        ref1 = @ref[1];
        ref2 = @ref[2];

        src_reg = yy_loadu2_128(src + src_stride, src);
        sum_ref0 = _mm256_sad_epu8(yy_loadu2_128(ref0 + ref_stride, ref0), src_reg);
        sum_ref1 = _mm256_sad_epu8(yy_loadu2_128(ref1 + ref_stride, ref1), src_reg);
        sum_ref2 = _mm256_sad_epu8(yy_loadu2_128(ref2 + ref_stride, ref2), src_reg);
        src += 2 * src_stride;
        ref0 += 2 * ref_stride;
        ref1 += 2 * ref_stride;
        ref2 += 2 * ref_stride;

        for (int i = 2; i < N; i += 2)
        {
            // load src and all refs
            src_reg = yy_loadu2_128(src + src_stride, src);
            ref0_reg = yy_loadu2_128(ref0 + ref_stride, ref0);
            ref1_reg = yy_loadu2_128(ref1 + ref_stride, ref1);
            ref2_reg = yy_loadu2_128(ref2 + ref_stride, ref2);

            // sum of the absolute differences between every ref-i to src
            ref0_reg = _mm256_sad_epu8(ref0_reg, src_reg);
            ref1_reg = _mm256_sad_epu8(ref1_reg, src_reg);
            ref2_reg = _mm256_sad_epu8(ref2_reg, src_reg);

            // sum every ref-i
            sum_ref0 = _mm256_add_epi32(sum_ref0, ref0_reg);
            sum_ref1 = _mm256_add_epi32(sum_ref1, ref1_reg);
            sum_ref2 = _mm256_add_epi32(sum_ref2, ref2_reg);

            src += 2 * src_stride;
            ref0 += 2 * ref_stride;
            ref1 += 2 * ref_stride;
            ref2 += 2 * ref_stride;
        }

        aggregate_and_store_sum(res, in sum_ref0, in sum_ref1, in sum_ref2, in zero);
    }

    // AOM_FORCE_INLINE
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void aom_sad16xNx4d_avx2(int N, Ptr<byte> src,
                                            int src_stride,
                                            Ptr<Ptr<byte>> @ref,
                                            int ref_stride,
                                            Ptr<uint> res)
    {
        __m256i src_reg, ref0_reg, ref1_reg, ref2_reg, ref3_reg;
        __m256i sum_ref0, sum_ref1, sum_ref2, sum_ref3;
        Ptr<byte> ref0, ref1, ref2, ref3;
        System.Diagnostics.Debug.Assert(N % 2 == 0);

        ref0 = @ref[0];
        ref1 = @ref[1];
        ref2 = @ref[2];
        ref3 = @ref[3];

        src_reg = yy_loadu2_128(src + src_stride, src);
        sum_ref0 = _mm256_sad_epu8(yy_loadu2_128(ref0 + ref_stride, ref0), src_reg);
        sum_ref1 = _mm256_sad_epu8(yy_loadu2_128(ref1 + ref_stride, ref1), src_reg);
        sum_ref2 = _mm256_sad_epu8(yy_loadu2_128(ref2 + ref_stride, ref2), src_reg);
        sum_ref3 = _mm256_sad_epu8(yy_loadu2_128(ref3 + ref_stride, ref3), src_reg);
        src += 2 * src_stride;
        ref0 += 2 * ref_stride;
        ref1 += 2 * ref_stride;
        ref2 += 2 * ref_stride;
        ref3 += 2 * ref_stride;
        for (int i = 2; i < N; i += 2)
        {
            // load src and all refs
            src_reg = yy_loadu2_128(src + src_stride, src);
            ref0_reg = yy_loadu2_128(ref0 + ref_stride, ref0);
            ref1_reg = yy_loadu2_128(ref1 + ref_stride, ref1);
            ref2_reg = yy_loadu2_128(ref2 + ref_stride, ref2);
            ref3_reg = yy_loadu2_128(ref3 + ref_stride, ref3);

            // sum of the absolute differences between every ref-i to src
            ref0_reg = _mm256_sad_epu8(ref0_reg, src_reg);
            ref1_reg = _mm256_sad_epu8(ref1_reg, src_reg);
            ref2_reg = _mm256_sad_epu8(ref2_reg, src_reg);
            ref3_reg = _mm256_sad_epu8(ref3_reg, src_reg);

            // sum every ref-i
            sum_ref0 = _mm256_add_epi32(sum_ref0, ref0_reg);
            sum_ref1 = _mm256_add_epi32(sum_ref1, ref1_reg);
            sum_ref2 = _mm256_add_epi32(sum_ref2, ref2_reg);
            sum_ref3 = _mm256_add_epi32(sum_ref3, ref3_reg);

            src += 2 * src_stride;
            ref0 += 2 * ref_stride;
            ref1 += 2 * ref_stride;
            ref2 += 2 * ref_stride;
            ref3 += 2 * ref_stride;
        }

        aggregate_and_store_sum(res, in sum_ref0, in sum_ref1, in sum_ref2, in sum_ref3);
    }

    // SAD16XNX4_AVX2(32), (16), (8)
    internal static void aom_sad16x32x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sad16xNx4d_avx2(32, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad16x16x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sad16xNx4d_avx2(16, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad16x8x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sad16xNx4d_avx2(8, src, src_stride, @ref, ref_stride, res);

    // SAD16XNX3_AVX2(32), (16), (8)
    internal static void aom_sad16x32x3d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sad16xNx3d_avx2(32, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad16x16x3d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sad16xNx3d_avx2(16, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad16x8x3d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        aom_sad16xNx3d_avx2(8, src, src_stride, @ref, ref_stride, res);

    // [port] The body of SAD_SKIP_16XN_AVX2(n).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SAD_SKIP_16XN_AVX2(int n, Ptr<byte> src, int src_stride,
                                           Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res)
    {
        aom_sad16xNx4d_avx2(((n) >> 1), src, 2 * src_stride, @ref, 2 * ref_stride,
                            res);
        res[0] <<= 1;
        res[1] <<= 1;
        res[2] <<= 1;
        res[3] <<= 1;
    }

    internal static void aom_sad_skip_16x32x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        SAD_SKIP_16XN_AVX2(32, src, src_stride, @ref, ref_stride, res);
    internal static void aom_sad_skip_16x16x4d_avx2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        SAD_SKIP_16XN_AVX2(16, src, src_stride, @ref, ref_stride, res);
}
