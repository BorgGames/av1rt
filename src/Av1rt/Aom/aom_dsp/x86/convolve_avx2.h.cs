// Port of libaom aom_dsp/x86/convolve_avx2.h

// [port] The functions of av1/common/x86/convolve_avx2.c share the stem and therefore this
// class (av1/common/x86/convolve_avx2.c.cs).
global using static Av1rt.Aom.convolve_avx2;

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] Notes for the whole file:
// * `__m256i *` / `__m128i *` parameters that are arrays are (ReadOnly)Span<>, the ones that
//   point to one register are `ref` / `in`. Local register arrays are ArrN<> inline arrays
//   (plain locals, so the helpers stay inlinable and nothing is allocated).
// * The statement macros CONVOLVE_SR_* are methods taking the caller's locals they use as
//   parameters; a macro parameterised by a function (CONVOLVE_SR_HOR_FILTER_W4(fn),
//   CONVOLVE_SR_HORIZONTAL_FILTER(fn)) takes the tap count instead, constant at every call so
//   the selection folds away when inlined.
// * [subset] JNT_CONVOLVE_PROCESS_OUTPUT, JNT_CONVOLVE_HORIZONTAL_FILTER,
//   JNT_CONVOLVE_VERTICAL_FILTER_8TAP (compound prediction, never built in the subset),
//   highbd_comp_avg and highbd_convolve_rounding (high bitdepth) are not ported.
internal static partial class convolve_avx2
{
    private const MethodImplOptions AI = MethodImplOptions.AggressiveInlining;

    internal const int SECOND_32_BLK = (32);
    internal const int THIRD_32_BLK = (32 << 1);
    internal const int FOURTH_32_BLK = (SECOND_32_BLK + THIRD_32_BLK);

    // filters for 16
    internal static ReadOnlySpan<byte> filt_global_avx2 => new byte[] {
        0,  1,  1,  2,  2, 3,  3,  4,  4,  5,  5,  6,  6,  7,  7,  8,  0,  1,  1,
        2,  2,  3,  3,  4, 4,  5,  5,  6,  6,  7,  7,  8,  2,  3,  3,  4,  4,  5,
        5,  6,  6,  7,  7, 8,  8,  9,  9,  10, 2,  3,  3,  4,  4,  5,  5,  6,  6,
        7,  7,  8,  8,  9, 9,  10, 4,  5,  5,  6,  6,  7,  7,  8,  8,  9,  9,  10,
        10, 11, 11, 12, 4, 5,  5,  6,  6,  7,  7,  8,  8,  9,  9,  10, 10, 11, 11,
        12, 6,  7,  7,  8, 8,  9,  9,  10, 10, 11, 11, 12, 12, 13, 13, 14, 6,  7,
        7,  8,  8,  9,  9, 10, 10, 11, 11, 12, 12, 13, 13, 14
    };

    internal static ReadOnlySpan<byte> filt_d4_global_avx2 => new byte[] {
        0, 1, 2, 3,  1, 2, 3, 4, 2, 3, 4, 5, 3, 4, 5, 6, 0, 1, 2, 3,  1, 2,
        3, 4, 2, 3,  4, 5, 3, 4, 5, 6, 4, 5, 6, 7, 5, 6, 7, 8, 6, 7,  8, 9,
        7, 8, 9, 10, 4, 5, 6, 7, 5, 6, 7, 8, 6, 7, 8, 9, 7, 8, 9, 10,
    };

    internal static ReadOnlySpan<byte> filt4_d4_global_avx2 => new byte[] {
        2, 3, 4, 5, 3, 4, 5, 6, 4, 5, 6, 7, 5, 6, 7, 8,
        2, 3, 4, 5, 3, 4, 5, 6, 4, 5, 6, 7, 5, 6, 7, 8,
    };

    internal static ReadOnlySpan<byte> filt_center_global_avx2 => new byte[32] {
        3, 255, 4, 255, 5, 255, 6, 255, 7, 255, 8, 255, 9, 255, 10, 255,
        3, 255, 4, 255, 5, 255, 6, 255, 7, 255, 8, 255, 9, 255, 10, 255
    };

    internal static ReadOnlySpan<byte> filt1_global_sse2 => new byte[16] {
        0, 1, 1, 2,  2,  3,  3,  4,
        8, 9, 9, 10, 10, 11, 11, 12 };

    internal static ReadOnlySpan<byte> filt2_global_sse2 => new byte[16] {
        2,  3,  3,  4,  4,  5,  5,  6,
        10, 11, 11, 12, 12, 13, 13, 14 };

    internal static ReadOnlySpan<byte> filt3_global_sse2 => new byte[16] {
        0, 1, 1, 2, 8, 9, 9, 10,
        0, 0, 0, 0, 0, 0, 0, 0 };

    internal static ReadOnlySpan<byte> filt4_global_sse2 => new byte[16] {
        2, 3, 3, 4, 10, 11, 11, 12,
        0, 0, 0, 0, 0,  0,  0,  0 };

    internal static ReadOnlySpan<byte> filt5_global_sse2 => new byte[16] {
        0, 1, 1, 2, 4, 5, 5, 6,
        0, 0, 0, 0, 0, 0, 0, 0 };

    internal static ReadOnlySpan<byte> filt1_global_avx2 => new byte[32] {
        0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5,
        6, 6, 7, 7, 8, 0, 1, 1, 2, 2, 3,
        3, 4, 4, 5, 5, 6, 6, 7, 7, 8 };

    internal static ReadOnlySpan<byte> filt2_global_avx2 => new byte[32] {
        2, 3, 3, 4, 4,  5, 5, 6, 6, 7, 7,
        8, 8, 9, 9, 10, 2, 3, 3, 4, 4, 5,
        5, 6, 6, 7, 7,  8, 8, 9, 9, 10 };

    internal static ReadOnlySpan<byte> filt3_global_avx2 => new byte[32] {
        4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12,
        4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12
    };

    internal static ReadOnlySpan<byte> filt4_global_avx2 => new byte[32] {
        6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13, 14,
        6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13, 14
    };

    // CONVOLVE_SR_HOR_FILTER_W4(CONVOLVE_LOWBD): CONVOLVE_LOWBD is
    // convolve_lowbd_x_2tap_ssse3 (tap == 2) or convolve_lowbd_x_4tap_ssse3 (tap == 4).
    [MethodImpl(AI)]
    private static void CONVOLVE_SR_HOR_FILTER_W4(int tap, int im_h, Ptr<byte> src_ptr,
                                                  int src_stride,
                                                  ReadOnlySpan<__m128i> coeffs_h,
                                                  ReadOnlySpan<__m128i> filt,
                                                  __m128i round_const_h,
                                                  Ptr<short> im_block)
    {
        int i;
        for (i = 0; i < (im_h - 2); i += 2)
        {
            __m128i data =
                load_8bit_8x2_to_1_reg_sse2(src_ptr + (i * src_stride), src_stride);
            __m128i res = tap == 2 ? convolve_lowbd_x_2tap_ssse3(data, coeffs_h, filt)
                                   : convolve_lowbd_x_4tap_ssse3(data, coeffs_h, filt);
            res = _mm_srai_epi16(_mm_add_epi16(res, round_const_h), 2);
            _mm_store_si128(im_block + i * 4, res);
        }
        __m128i data_1 = _mm_loadl_epi64(src_ptr + (i * src_stride));
        __m128i res_1 = tap == 2 ? convolve_lowbd_x_2tap_ssse3(data_1, coeffs_h, filt)
                                 : convolve_lowbd_x_4tap_ssse3(data_1, coeffs_h, filt);
        res_1 = _mm_srai_epi16(_mm_add_epi16(res_1, round_const_h), 2);
        _mm_storel_epi64(im_block + i * 4, res_1);
    }

    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_HOR_FILTER_2TAP_W4(int im_h, Ptr<byte> src_ptr,
                                                        int src_stride,
                                                        ReadOnlySpan<__m128i> coeffs_h,
                                                        ReadOnlySpan<__m128i> filt,
                                                        __m128i round_const_h,
                                                        Ptr<short> im_block) =>
        CONVOLVE_SR_HOR_FILTER_W4(2, im_h, src_ptr, src_stride, coeffs_h, filt, round_const_h,
                                  im_block);

    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_HOR_FILTER_4TAP_W4(int im_h, Ptr<byte> src_ptr,
                                                        int src_stride,
                                                        ReadOnlySpan<__m128i> coeffs_h,
                                                        ReadOnlySpan<__m128i> filt,
                                                        __m128i round_const_h,
                                                        Ptr<short> im_block) =>
        CONVOLVE_SR_HOR_FILTER_W4(4, im_h, src_ptr, src_stride, coeffs_h, filt, round_const_h,
                                  im_block);

    [MethodImpl(AI)]
    internal static void sr_2d_ver_round_and_store_w4(int w, __m256i res,
                                                      Ptr<byte> dst, int dst_stride,
                                                      __m256i round_const_v)
    {
        __m256i res_round =
            _mm256_srai_epi32(_mm256_add_epi32(res, round_const_v), 11);

        __m256i res_16bit = _mm256_packs_epi32(res_round, res_round);
        __m256i res_8b = _mm256_packus_epi16(res_16bit, res_16bit);

        __m128i r0 = _mm256_castsi256_si128(res_8b);
        __m128i r1 = _mm256_extracti128_si256(res_8b, 1);

        Ptr<byte> p0 = dst;
        Ptr<byte> p1 = (dst + dst_stride);

        if (w == 4)
        {
            xx_storel_32(p0, r0);
            xx_storel_32(p1, r1);
        }
        else
        {
            Debug.Assert(w == 2);
            BinaryPrimitives.WriteUInt16LittleEndian(p0.AsSpan(2), (ushort)_mm_cvtsi128_si32(r0));
            BinaryPrimitives.WriteUInt16LittleEndian(p1.AsSpan(2), (ushort)_mm_cvtsi128_si32(r1));
        }
    }

    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_VER_FILTER_2TAP_W4(Ptr<short> im_block, int w, int h,
                                                        ReadOnlySpan<__m256i> coeffs_v,
                                                        Ptr<byte> dst_ptr, int dst_stride,
                                                        __m256i round_const_v)
    {
        int i;
        Arr2<__m128i> s = default;
        s[0] = _mm_loadl_epi64(im_block + 0 * 4);

        for (i = 0; i < h; i += 2)
        {
            Ptr<short> data = im_block + i * 4;
            s[1] = _mm_loadl_epi64(data + 1 * 4);
            __m256i src_0 = _mm256_setr_m128i(s[0], s[1]);
            s[0] = _mm_loadl_epi64(data + 2 * 4);
            __m256i src_1 = _mm256_setr_m128i(s[1], s[0]);
            __m256i ss = _mm256_unpacklo_epi16(src_0, src_1);

            __m256i res = _mm256_madd_epi16(ss, coeffs_v[0]);

            sr_2d_ver_round_and_store_w4(w, res, dst_ptr, dst_stride, round_const_v);
            dst_ptr += 2 * dst_stride;
        }
    }

    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_VER_FILTER_4TAP_W4(Ptr<short> im_block, int w, int h,
                                                        ReadOnlySpan<__m256i> coeffs_v,
                                                        Ptr<byte> dst_ptr, int dst_stride,
                                                        __m256i round_const_v)
    {
        int i;
        Arr4<__m128i> s = default;
        Arr2<__m256i> ss = default;
        s[0] = _mm_loadl_epi64(im_block + 0 * 4);
        s[1] = _mm_loadl_epi64(im_block + 1 * 4);
        s[2] = _mm_loadl_epi64(im_block + 2 * 4);

        __m256i src_0 = _mm256_setr_m128i(s[0], s[1]);
        __m256i src_1 = _mm256_setr_m128i(s[1], s[2]);

        ss[0] = _mm256_unpacklo_epi16(src_0, src_1);

        for (i = 0; i < h; i += 2)
        {
            Ptr<short> data = im_block + i * 4;
            s[3] = _mm_loadl_epi64(data + 3 * 4);
            __m256i src_2 = _mm256_setr_m128i(s[2], s[3]);
            s[2] = _mm_loadl_epi64(data + 4 * 4);
            __m256i src_3 = _mm256_setr_m128i(s[3], s[2]);
            ss[1] = _mm256_unpacklo_epi16(src_2, src_3);

            __m256i res = convolve_4tap(ss, coeffs_v);

            sr_2d_ver_round_and_store_w4(w, res, dst_ptr, dst_stride, round_const_v);
            dst_ptr += 2 * dst_stride;

            ss[0] = ss[1];
        }
    }

    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_VER_FILTER_6TAP_W4(Ptr<short> im_block, int w, int h,
                                                        ReadOnlySpan<__m256i> coeffs_v,
                                                        Ptr<byte> dst_ptr, int dst_stride,
                                                        __m256i round_const_v)
    {
        int i;
        Arr6<__m128i> s = default;
        Arr3<__m256i> ss = default;
        s[0] = _mm_loadl_epi64(im_block + 0 * 4);
        s[1] = _mm_loadl_epi64(im_block + 1 * 4);
        s[2] = _mm_loadl_epi64(im_block + 2 * 4);
        s[3] = _mm_loadl_epi64(im_block + 3 * 4);
        s[4] = _mm_loadl_epi64(im_block + 4 * 4);

        __m256i src_0 = _mm256_setr_m128i(s[0], s[1]);
        __m256i src_1 = _mm256_setr_m128i(s[1], s[2]);
        __m256i src_2 = _mm256_setr_m128i(s[2], s[3]);
        __m256i src_3 = _mm256_setr_m128i(s[3], s[4]);

        ss[0] = _mm256_unpacklo_epi16(src_0, src_1);
        ss[1] = _mm256_unpacklo_epi16(src_2, src_3);

        for (i = 0; i < h; i += 2)
        {
            Ptr<short> data = im_block + i * 4;
            s[5] = _mm_loadl_epi64(data + 5 * 4);
            __m256i src_4 = _mm256_setr_m128i(s[4], s[5]);
            s[4] = _mm_loadl_epi64(data + 6 * 4);
            __m256i src_5 = _mm256_setr_m128i(s[5], s[4]);
            ss[2] = _mm256_unpacklo_epi16(src_4, src_5);

            __m256i res = convolve_6tap(ss, coeffs_v);

            sr_2d_ver_round_and_store_w4(w, res, dst_ptr, dst_stride, round_const_v);
            dst_ptr += 2 * dst_stride;

            ss[0] = ss[1];
            ss[1] = ss[2];
        }
    }

    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_VER_FILTER_8TAP_W4(Ptr<short> im_block, int w, int h,
                                                        ReadOnlySpan<__m256i> coeffs_v,
                                                        Ptr<byte> dst_ptr, int dst_stride,
                                                        __m256i round_const_v)
    {
        int i;
        Arr8<__m128i> s = default;
        Arr4<__m256i> ss = default;
        s[0] = _mm_loadl_epi64(im_block + 0 * 4);
        s[1] = _mm_loadl_epi64(im_block + 1 * 4);
        s[2] = _mm_loadl_epi64(im_block + 2 * 4);
        s[3] = _mm_loadl_epi64(im_block + 3 * 4);
        s[4] = _mm_loadl_epi64(im_block + 4 * 4);
        s[5] = _mm_loadl_epi64(im_block + 5 * 4);
        s[6] = _mm_loadl_epi64(im_block + 6 * 4);

        __m256i src_0 = _mm256_setr_m128i(s[0], s[1]);
        __m256i src_1 = _mm256_setr_m128i(s[1], s[2]);
        __m256i src_2 = _mm256_setr_m128i(s[2], s[3]);
        __m256i src_3 = _mm256_setr_m128i(s[3], s[4]);
        __m256i src_4 = _mm256_setr_m128i(s[4], s[5]);
        __m256i src_5 = _mm256_setr_m128i(s[5], s[6]);

        ss[0] = _mm256_unpacklo_epi16(src_0, src_1);
        ss[1] = _mm256_unpacklo_epi16(src_2, src_3);
        ss[2] = _mm256_unpacklo_epi16(src_4, src_5);

        for (i = 0; i < h; i += 2)
        {
            Ptr<short> data = im_block + i * 4;
            s[7] = _mm_loadl_epi64(data + 7 * 4);
            __m256i src_6 = _mm256_setr_m128i(s[6], s[7]);
            s[6] = _mm_loadl_epi64(data + 8 * 4);
            __m256i src_7 = _mm256_setr_m128i(s[7], s[6]);
            ss[3] = _mm256_unpacklo_epi16(src_6, src_7);

            __m256i res = convolve(ss, coeffs_v);

            sr_2d_ver_round_and_store_w4(w, res, dst_ptr, dst_stride, round_const_v);
            dst_ptr += 2 * dst_stride;

            ss[0] = ss[1];
            ss[1] = ss[2];
            ss[2] = ss[3];
        }
    }

    // CONVOLVE_SR_HORIZONTAL_FILTER(CONVOLVE_LOWBD): CONVOLVE_LOWBD is convolve_lowbd_x_2tap,
    // _4tap, _6tap or convolve_lowbd_x (tap == 2, 4, 6, 8).
    [MethodImpl(AI)]
    private static void CONVOLVE_SR_HORIZONTAL_FILTER(int tap, int im_h, Ptr<byte> src_ptr,
                                                      int src_stride, int j,
                                                      ReadOnlySpan<__m256i> coeffs_h,
                                                      ReadOnlySpan<__m256i> filt,
                                                      __m256i round_const_h,
                                                      Ptr<short> im_block, int im_stride)
    {
        int i;
        for (i = 0; i < (im_h - 2); i += 2)
        {
            __m256i data = _mm256_castsi128_si256(
                _mm_loadu_si128(src_ptr + (i * src_stride) + j));
            data = _mm256_inserti128_si256(
                data,
                _mm_loadu_si128(
                    src_ptr + (i * src_stride) + j + src_stride),
                1);
            __m256i res = CONVOLVE_LOWBD(tap, data, coeffs_h, filt);
            res = _mm256_srai_epi16(_mm256_add_epi16(res, round_const_h), 2);
            _mm256_store_si256(im_block + i * im_stride, res);
        }
        __m256i data_1 = _mm256_castsi128_si256(
            _mm_loadu_si128(src_ptr + (i * src_stride) + j));
        __m256i res_1 = CONVOLVE_LOWBD(tap, data_1, coeffs_h, filt);
        res_1 = _mm256_srai_epi16(_mm256_add_epi16(res_1, round_const_h), 2);
        _mm256_store_si256(im_block + i * im_stride, res_1);
    }

    [MethodImpl(AI)]
    private static __m256i CONVOLVE_LOWBD(int tap, __m256i data, ReadOnlySpan<__m256i> coeffs,
                                          ReadOnlySpan<__m256i> filt)
    {
        if (tap == 2) return convolve_lowbd_x_2tap(data, coeffs, filt);
        if (tap == 4) return convolve_lowbd_x_4tap(data, coeffs, filt);
        if (tap == 6) return convolve_lowbd_x_6tap(data, coeffs, filt);
        return convolve_lowbd_x(data, coeffs, filt);
    }

    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_HORIZONTAL_FILTER_2TAP(int im_h, Ptr<byte> src_ptr,
                                                            int src_stride, int j,
                                                            ReadOnlySpan<__m256i> coeffs_h,
                                                            ReadOnlySpan<__m256i> filt,
                                                            __m256i round_const_h,
                                                            Ptr<short> im_block, int im_stride) =>
        CONVOLVE_SR_HORIZONTAL_FILTER(2, im_h, src_ptr, src_stride, j, coeffs_h, filt,
                                      round_const_h, im_block, im_stride);

    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_HORIZONTAL_FILTER_4TAP(int im_h, Ptr<byte> src_ptr,
                                                            int src_stride, int j,
                                                            ReadOnlySpan<__m256i> coeffs_h,
                                                            ReadOnlySpan<__m256i> filt,
                                                            __m256i round_const_h,
                                                            Ptr<short> im_block, int im_stride) =>
        CONVOLVE_SR_HORIZONTAL_FILTER(4, im_h, src_ptr, src_stride, j, coeffs_h, filt,
                                      round_const_h, im_block, im_stride);

    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_HORIZONTAL_FILTER_6TAP(int im_h, Ptr<byte> src_ptr,
                                                            int src_stride, int j,
                                                            ReadOnlySpan<__m256i> coeffs_h,
                                                            ReadOnlySpan<__m256i> filt,
                                                            __m256i round_const_h,
                                                            Ptr<short> im_block, int im_stride) =>
        CONVOLVE_SR_HORIZONTAL_FILTER(6, im_h, src_ptr, src_stride, j, coeffs_h, filt,
                                      round_const_h, im_block, im_stride);

    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_HORIZONTAL_FILTER_8TAP(int im_h, Ptr<byte> src_ptr,
                                                            int src_stride, int j,
                                                            ReadOnlySpan<__m256i> coeffs_h,
                                                            ReadOnlySpan<__m256i> filt,
                                                            __m256i round_const_h,
                                                            Ptr<short> im_block, int im_stride) =>
        CONVOLVE_SR_HORIZONTAL_FILTER(8, im_h, src_ptr, src_stride, j, coeffs_h, filt,
                                      round_const_h, im_block, im_stride);

    [MethodImpl(AI)]
    internal static void sr_2d_ver_round_and_store(__m256i res_a, __m256i res_b,
                                                   Ptr<byte> dst, int dst_stride,
                                                   __m256i round_const_v)
    {
        __m256i res_a_round =
            _mm256_srai_epi32(_mm256_add_epi32(res_a, round_const_v), 11);
        __m256i res_b_round =
            _mm256_srai_epi32(_mm256_add_epi32(res_b, round_const_v), 11);
        __m256i r16 = _mm256_packs_epi32(res_a_round, res_b_round);
        __m256i r8 = _mm256_packus_epi16(r16, r16);

        _mm_storel_epi64(dst, _mm256_castsi256_si128(r8));
        _mm_storel_epi64(dst + dst_stride,
                         _mm256_extracti128_si256(r8, 1));
    }

    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_VERTICAL_FILTER_2TAP(Ptr<short> im_block, int im_stride,
                                                          int h,
                                                          ReadOnlySpan<__m256i> coeffs_v,
                                                          Ptr<byte> dst_ptr, int dst_stride,
                                                          __m256i round_const_v)
    {
        int i;
        Arr2<__m256i> s = default;
        for (i = 0; i < h; i += 2)
        {
            Ptr<short> data = im_block + i * im_stride;
            __m256i s1 = _mm256_loadu_si256(data + 0 * im_stride);
            __m256i s2 = _mm256_loadu_si256(data + 1 * im_stride);
            s[0] = _mm256_unpacklo_epi16(s1, s2);
            s[1] = _mm256_unpackhi_epi16(s1, s2);

            __m256i res_a = _mm256_madd_epi16(s[0], coeffs_v[0]);
            __m256i res_b = _mm256_madd_epi16(s[1], coeffs_v[0]);

            sr_2d_ver_round_and_store(res_a, res_b, dst_ptr, dst_stride,
                                      round_const_v);
            dst_ptr += 2 * dst_stride;
        }
    }

    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_VERTICAL_FILTER_4TAP(Ptr<short> im_block, int im_stride,
                                                          int h,
                                                          ReadOnlySpan<__m256i> coeffs_v,
                                                          Ptr<byte> dst_ptr, int dst_stride,
                                                          __m256i round_const_v)
    {
        int i;
        Arr6<__m256i> s = default;
        Span<__m256i> sp = s;
        __m256i src_0 = _mm256_loadu_si256(im_block + 0 * im_stride);
        __m256i src_1 = _mm256_loadu_si256(im_block + 1 * im_stride);

        s[0] = _mm256_unpacklo_epi16(src_0, src_1);
        s[2] = _mm256_unpackhi_epi16(src_0, src_1);

        for (i = 0; i < h; i += 2)
        {
            Ptr<short> data = im_block + i * im_stride;
            __m256i s4 = _mm256_loadu_si256(data + 2 * im_stride);
            __m256i s5 = _mm256_loadu_si256(data + 3 * im_stride);
            s[1] = _mm256_unpacklo_epi16(s4, s5);
            s[3] = _mm256_unpackhi_epi16(s4, s5);

            __m256i res_a = convolve_4tap(sp, coeffs_v);
            __m256i res_b = convolve_4tap(sp.Slice(2), coeffs_v);

            sr_2d_ver_round_and_store(res_a, res_b, dst_ptr, dst_stride,
                                      round_const_v);
            dst_ptr += 2 * dst_stride;

            s[0] = s[1];
            s[2] = s[3];
        }
    }

    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_VERTICAL_FILTER_6TAP(Ptr<short> im_block, int im_stride,
                                                          int h,
                                                          ReadOnlySpan<__m256i> coeffs_v,
                                                          Ptr<byte> dst_ptr, int dst_stride,
                                                          __m256i round_const_v)
    {
        int i;
        __m256i src_0 = _mm256_loadu_si256(im_block + 0 * im_stride);
        __m256i src_1 = _mm256_loadu_si256(im_block + 1 * im_stride);
        __m256i src_2 = _mm256_loadu_si256(im_block + 2 * im_stride);
        __m256i src_3 = _mm256_loadu_si256(im_block + 3 * im_stride);

        Arr8<__m256i> s = default;
        Span<__m256i> sp = s;
        s[0] = _mm256_unpacklo_epi16(src_0, src_1);
        s[1] = _mm256_unpacklo_epi16(src_2, src_3);

        s[3] = _mm256_unpackhi_epi16(src_0, src_1);
        s[4] = _mm256_unpackhi_epi16(src_2, src_3);

        for (i = 0; i < h; i += 2)
        {
            Ptr<short> data = im_block + i * im_stride;

            __m256i s6 = _mm256_loadu_si256(data + 4 * im_stride);
            __m256i s7 = _mm256_loadu_si256(data + 5 * im_stride);

            s[2] = _mm256_unpacklo_epi16(s6, s7);
            s[5] = _mm256_unpackhi_epi16(s6, s7);

            __m256i res_a = convolve_6tap(sp, coeffs_v);
            __m256i res_b = convolve_6tap(sp.Slice(3), coeffs_v);

            sr_2d_ver_round_and_store(res_a, res_b, dst_ptr, dst_stride,
                                      round_const_v);
            dst_ptr += 2 * dst_stride;

            s[0] = s[1];
            s[1] = s[2];

            s[3] = s[4];
            s[4] = s[5];
        }
    }

    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_VERTICAL_FILTER_8TAP(Ptr<short> im_block, int im_stride,
                                                          int h,
                                                          ReadOnlySpan<__m256i> coeffs_v,
                                                          Ptr<byte> dst_ptr, int dst_stride,
                                                          __m256i round_const_v)
    {
        int i;
        __m256i src_0 = _mm256_loadu_si256(im_block + 0 * im_stride);
        __m256i src_1 = _mm256_loadu_si256(im_block + 1 * im_stride);
        __m256i src_2 = _mm256_loadu_si256(im_block + 2 * im_stride);
        __m256i src_3 = _mm256_loadu_si256(im_block + 3 * im_stride);
        __m256i src_4 = _mm256_loadu_si256(im_block + 4 * im_stride);
        __m256i src_5 = _mm256_loadu_si256(im_block + 5 * im_stride);

        Arr8<__m256i> s = default;
        Span<__m256i> sp = s;
        s[0] = _mm256_unpacklo_epi16(src_0, src_1);
        s[1] = _mm256_unpacklo_epi16(src_2, src_3);
        s[2] = _mm256_unpacklo_epi16(src_4, src_5);

        s[4] = _mm256_unpackhi_epi16(src_0, src_1);
        s[5] = _mm256_unpackhi_epi16(src_2, src_3);
        s[6] = _mm256_unpackhi_epi16(src_4, src_5);

        for (i = 0; i < h; i += 2)
        {
            Ptr<short> data = im_block + i * im_stride;

            __m256i s6 = _mm256_loadu_si256(data + 6 * im_stride);
            __m256i s7 = _mm256_loadu_si256(data + 7 * im_stride);

            s[3] = _mm256_unpacklo_epi16(s6, s7);
            s[7] = _mm256_unpackhi_epi16(s6, s7);

            __m256i res_a = convolve(sp, coeffs_v);
            __m256i res_b = convolve(sp.Slice(4), coeffs_v);

            sr_2d_ver_round_and_store(res_a, res_b, dst_ptr, dst_stride,
                                      round_const_v);
            dst_ptr += 2 * dst_stride;

            s[0] = s[1];
            s[1] = s[2];
            s[2] = s[3];

            s[4] = s[5];
            s[5] = s[6];
            s[6] = s[7];
        }
    }

    // [port] `s` is the caller's `__m256i s[12]`.
    [MethodImpl(AI)]
    internal static void CONVOLVE_SR_VERTICAL_FILTER_12TAP(Ptr<short> im_block, int im_stride,
                                                           int w, int h, int j,
                                                           Ptr<byte> dst, int dst_stride,
                                                           Span<__m256i> s,
                                                           ReadOnlySpan<__m256i> coeffs_v,
                                                           __m256i sum_round_v,
                                                           __m128i sum_shift_v,
                                                           __m256i round_const_v,
                                                           __m128i round_shift_v)
    {
        int i;
        __m256i src_0 = _mm256_loadu_si256(im_block + 0 * im_stride);
        __m256i src_1 = _mm256_loadu_si256(im_block + 1 * im_stride);
        __m256i src_2 = _mm256_loadu_si256(im_block + 2 * im_stride);
        __m256i src_3 = _mm256_loadu_si256(im_block + 3 * im_stride);
        __m256i src_4 = _mm256_loadu_si256(im_block + 4 * im_stride);
        __m256i src_5 = _mm256_loadu_si256(im_block + 5 * im_stride);
        __m256i src_6 = _mm256_loadu_si256(im_block + 6 * im_stride);
        __m256i src_7 = _mm256_loadu_si256(im_block + 7 * im_stride);
        __m256i src_8 = _mm256_loadu_si256(im_block + 8 * im_stride);
        __m256i src_9 = _mm256_loadu_si256(im_block + 9 * im_stride);

        s[0] = _mm256_unpacklo_epi16(src_0, src_1);
        s[1] = _mm256_unpacklo_epi16(src_2, src_3);
        s[2] = _mm256_unpacklo_epi16(src_4, src_5);
        s[3] = _mm256_unpacklo_epi16(src_6, src_7);
        s[4] = _mm256_unpacklo_epi16(src_8, src_9);

        s[6] = _mm256_unpackhi_epi16(src_0, src_1);
        s[7] = _mm256_unpackhi_epi16(src_2, src_3);
        s[8] = _mm256_unpackhi_epi16(src_4, src_5);
        s[9] = _mm256_unpackhi_epi16(src_6, src_7);
        s[10] = _mm256_unpackhi_epi16(src_8, src_9);

        for (i = 0; i < h; i += 2)
        {
            Ptr<short> data = im_block + i * im_stride;

            __m256i s6 = _mm256_loadu_si256(data + 10 * im_stride);
            __m256i s7 = _mm256_loadu_si256(data + 11 * im_stride);

            s[5] = _mm256_unpacklo_epi16(s6, s7);
            s[11] = _mm256_unpackhi_epi16(s6, s7);

            __m256i res_a = convolve_12taps(s, coeffs_v);
            __m256i res_b = convolve_12taps(s.Slice(6), coeffs_v);

            res_a =
                _mm256_sra_epi32(_mm256_add_epi32(res_a, sum_round_v), sum_shift_v);
            res_b =
                _mm256_sra_epi32(_mm256_add_epi32(res_b, sum_round_v), sum_shift_v);

            __m256i res_a_round = _mm256_sra_epi32(
                _mm256_add_epi32(res_a, round_const_v), round_shift_v);
            __m256i res_b_round = _mm256_sra_epi32(
                _mm256_add_epi32(res_b, round_const_v), round_shift_v);

            __m256i res_16bit = _mm256_packs_epi32(res_a_round, res_b_round);
            __m256i res_8b = _mm256_packus_epi16(res_16bit, res_16bit);

            __m128i res_0 = _mm256_castsi256_si128(res_8b);
            __m128i res_1 = _mm256_extracti128_si256(res_8b, 1);

            Ptr<byte> p_0 = dst + (i * dst_stride + j);
            Ptr<byte> p_1 = dst + (i * dst_stride + j + dst_stride);
            if (w - j > 4)
            {
                _mm_storel_epi64(p_0, res_0);
                _mm_storel_epi64(p_1, res_1);
            }
            else if (w == 4)
            {
                xx_storel_32(p_0, res_0);
                xx_storel_32(p_1, res_1);
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(p_0.AsSpan(2), (ushort)_mm_cvtsi128_si32(res_0));
                BinaryPrimitives.WriteUInt16LittleEndian(p_1.AsSpan(2), (ushort)_mm_cvtsi128_si32(res_1));
            }

            s[0] = s[1];
            s[1] = s[2];
            s[2] = s[3];
            s[3] = s[4];
            s[4] = s[5];

            s[6] = s[7];
            s[7] = s[8];
            s[8] = s[9];
            s[9] = s[10];
            s[10] = s[11];
        }
    }

    // [subset] JNT_CONVOLVE_PROCESS_OUTPUT, JNT_CONVOLVE_HORIZONTAL_FILTER,
    // JNT_CONVOLVE_VERTICAL_FILTER_8TAP: compound prediction only.

    [MethodImpl(AI)]
    internal static void prepare_coeffs_2t_ssse3(
        InterpFilterParams filter_params, int subpel_q4,
        Span<__m128i> coeffs /* [4] */)
    {
        Ptr<short> filter = av1_get_interp_filter_subpel_kernel(
            filter_params, subpel_q4 & SUBPEL_MASK);
        __m128i coeffs_8 = _mm_loadu_si128(filter);

        // right shift all filter co-efficients by 1 to reduce the bits required.
        // This extra right shift will be taken care of at the end while rounding
        // the result.
        // Since all filter co-efficients are even, this change will not affect the
        // end result
        Debug.Assert(_mm_test_all_zeros(_mm_and_si128(coeffs_8, _mm_set1_epi16(1)),
                                        _mm_set1_epi16(unchecked((short)0xffff))) != 0);

        __m128i coeffs_1 = _mm_srai_epi16(coeffs_8, 1);

        // coeffs 3 4 3 4 3 4 3 4
        coeffs[0] = _mm_shuffle_epi8(coeffs_1, _mm_set1_epi16(0x0806));
    }

    [MethodImpl(AI)]
    internal static void prepare_coeffs_4t_ssse3(
        InterpFilterParams filter_params, int subpel_q4,
        Span<__m128i> coeffs /* [4] */)
    {
        Ptr<short> filter = av1_get_interp_filter_subpel_kernel(
            filter_params, subpel_q4 & SUBPEL_MASK);
        __m128i coeffs_8 = _mm_loadu_si128(filter);

        // right shift all filter co-efficients by 1 to reduce the bits required.
        // This extra right shift will be taken care of at the end while rounding
        // the result.
        // Since all filter co-efficients are even, this change will not affect the
        // end result
        Debug.Assert(_mm_test_all_zeros(_mm_and_si128(coeffs_8, _mm_set1_epi16(1)),
                                        _mm_set1_epi16(unchecked((short)0xffff))) != 0);

        __m128i coeffs_1 = _mm_srai_epi16(coeffs_8, 1);

        // coeffs 2 3 2 3 2 3 2 3
        coeffs[0] = _mm_shuffle_epi8(coeffs_1, _mm_set1_epi16(0x0604));
        // coeffs 4 5 4 5 4 5 4 5
        coeffs[1] = _mm_shuffle_epi8(coeffs_1, _mm_set1_epi16(0x0a08));
    }

    [MethodImpl(AI)]
    internal static void prepare_coeffs_6t_ssse3(
        InterpFilterParams filter_params, int subpel_q4,
        Span<__m128i> coeffs /* [4] */)
    {
        Ptr<short> filter = av1_get_interp_filter_subpel_kernel(
            filter_params, subpel_q4 & SUBPEL_MASK);
        __m128i coeffs_8 = _mm_loadu_si128(filter);

        // right shift all filter co-efficients by 1 to reduce the bits required.
        // This extra right shift will be taken care of at the end while rounding
        // the result.
        // Since all filter co-efficients are even, this change will not affect the
        // end result
        Debug.Assert(_mm_test_all_zeros(_mm_and_si128(coeffs_8, _mm_set1_epi16(1)),
                                        _mm_set1_epi16(unchecked((short)0xffff))) != 0);

        __m128i coeffs_1 = _mm_srai_epi16(coeffs_8, 1);

        // coeffs 2 3 2 3 2 3 2 3
        coeffs[0] = _mm_shuffle_epi8(coeffs_1, _mm_set1_epi16(0x0402));
        // coeffs 4 5 4 5 4 5 4 5
        coeffs[1] = _mm_shuffle_epi8(coeffs_1, _mm_set1_epi16(0x0806));
        // coeffs 5 6 5 6 5 6 5 6
        coeffs[2] = _mm_shuffle_epi8(coeffs_1, _mm_set1_epi16(0x0c0a));
    }

    [MethodImpl(AI)]
    internal static void prepare_coeffs_ssse3(
        InterpFilterParams filter_params, int subpel_q4,
        Span<__m128i> coeffs /* [4] */)
    {
        Ptr<short> filter = av1_get_interp_filter_subpel_kernel(
            filter_params, subpel_q4 & SUBPEL_MASK);
        __m128i coeffs_8 = _mm_loadu_si128(filter);

        // right shift all filter co-efficients by 1 to reduce the bits required.
        // This extra right shift will be taken care of at the end while rounding
        // the result.
        // Since all filter co-efficients are even, this change will not affect the
        // end result
        Debug.Assert(_mm_test_all_zeros(_mm_and_si128(coeffs_8, _mm_set1_epi16(1)),
                                        _mm_set1_epi16(unchecked((short)0xffff))) != 0);

        __m128i coeffs_1 = _mm_srai_epi16(coeffs_8, 1);

        // coeffs 0 1 0 1 0 1 0 1
        coeffs[0] = _mm_shuffle_epi8(coeffs_1, _mm_set1_epi16(0x0200));
        // coeffs 2 3 2 3 2 3 2 3
        coeffs[1] = _mm_shuffle_epi8(coeffs_1, _mm_set1_epi16(0x0604));
        // coeffs 4 5 4 5 4 5 4 5
        coeffs[2] = _mm_shuffle_epi8(coeffs_1, _mm_set1_epi16(0x0a08));
        // coeffs 6 7 6 7 6 7 6 7
        coeffs[3] = _mm_shuffle_epi8(coeffs_1, _mm_set1_epi16(0x0e0c));
    }

    [MethodImpl(AI)]
    internal static void prepare_coeffs_2t_lowbd(
        InterpFilterParams filter_params, int subpel_q4,
        Span<__m256i> coeffs /* [4] */)
    {
        Ptr<short> filter = av1_get_interp_filter_subpel_kernel(
            filter_params, subpel_q4 & SUBPEL_MASK);
        __m128i coeffs_8 = _mm_loadu_si128(filter);
        __m256i filter_coeffs = _mm256_broadcastsi128_si256(coeffs_8);

        // right shift all filter co-efficients by 1 to reduce the bits required.
        // This extra right shift will be taken care of at the end while rounding
        // the result.
        // Since all filter co-efficients are even, this change will not affect the
        // end result
        Debug.Assert(_mm_test_all_zeros(_mm_and_si128(coeffs_8, _mm_set1_epi16(1)),
                                        _mm_set1_epi16(unchecked((short)0xffff))) != 0);

        __m256i coeffs_1 = _mm256_srai_epi16(filter_coeffs, 1);

        // coeffs 3 4 3 4 3 4 3 4
        coeffs[0] = _mm256_shuffle_epi8(coeffs_1, _mm256_set1_epi16(0x0806));
    }

    [MethodImpl(AI)]
    internal static void prepare_coeffs_4t_lowbd(
        InterpFilterParams filter_params, int subpel_q4,
        Span<__m256i> coeffs /* [4] */)
    {
        Ptr<short> filter = av1_get_interp_filter_subpel_kernel(
            filter_params, subpel_q4 & SUBPEL_MASK);
        __m128i coeffs_8 = _mm_loadu_si128(filter);
        __m256i filter_coeffs = _mm256_broadcastsi128_si256(coeffs_8);

        // right shift all filter co-efficients by 1 to reduce the bits required.
        // This extra right shift will be taken care of at the end while rounding
        // the result.
        // Since all filter co-efficients are even, this change will not affect the
        // end result
        Debug.Assert(_mm_test_all_zeros(_mm_and_si128(coeffs_8, _mm_set1_epi16(1)),
                                        _mm_set1_epi16(unchecked((short)0xffff))) != 0);

        __m256i coeffs_1 = _mm256_srai_epi16(filter_coeffs, 1);

        // coeffs 2 3 2 3 2 3 2 3
        coeffs[0] = _mm256_shuffle_epi8(coeffs_1, _mm256_set1_epi16(0x0604));
        // coeffs 4 5 4 5 4 5 4 5
        coeffs[1] = _mm256_shuffle_epi8(coeffs_1, _mm256_set1_epi16(0x0a08));
    }

    [MethodImpl(AI)]
    internal static void prepare_coeffs_6t_lowbd(
        InterpFilterParams filter_params, int subpel_q4,
        Span<__m256i> coeffs /* [4] */)
    {
        Ptr<short> filter = av1_get_interp_filter_subpel_kernel(
            filter_params, subpel_q4 & SUBPEL_MASK);
        __m128i coeffs_8 = _mm_loadu_si128(filter);
        __m256i filter_coeffs = _mm256_broadcastsi128_si256(coeffs_8);

        // right shift all filter co-efficients by 1 to reduce the bits required.
        // This extra right shift will be taken care of at the end while rounding
        // the result.
        // Since all filter co-efficients are even, this change will not affect the
        // end result
        Debug.Assert(_mm_test_all_zeros(_mm_and_si128(coeffs_8, _mm_set1_epi16(1)),
                                        _mm_set1_epi16(unchecked((short)0xffff))) != 0);

        __m256i coeffs_1 = _mm256_srai_epi16(filter_coeffs, 1);

        // coeffs 1 2 1 2 1 2 1 2
        coeffs[0] = _mm256_shuffle_epi8(coeffs_1, _mm256_set1_epi16(0x0402));
        // coeffs 3 4 3 4 3 4 3 4
        coeffs[1] = _mm256_shuffle_epi8(coeffs_1, _mm256_set1_epi16(0x0806));
        // coeffs 5 6 5 6 5 6 5 6
        coeffs[2] = _mm256_shuffle_epi8(coeffs_1, _mm256_set1_epi16(0x0c0a));
    }

    [MethodImpl(AI)]
    internal static void prepare_coeffs_lowbd(
        InterpFilterParams filter_params, int subpel_q4,
        Span<__m256i> coeffs /* [4] */)
    {
        Ptr<short> filter = av1_get_interp_filter_subpel_kernel(
            filter_params, subpel_q4 & SUBPEL_MASK);
        __m128i coeffs_8 = _mm_loadu_si128(filter);
        __m256i filter_coeffs = _mm256_broadcastsi128_si256(coeffs_8);

        // right shift all filter co-efficients by 1 to reduce the bits required.
        // This extra right shift will be taken care of at the end while rounding
        // the result.
        // Since all filter co-efficients are even, this change will not affect the
        // end result
        Debug.Assert(_mm_test_all_zeros(_mm_and_si128(coeffs_8, _mm_set1_epi16(1)),
                                        _mm_set1_epi16(unchecked((short)0xffff))) != 0);

        __m256i coeffs_1 = _mm256_srai_epi16(filter_coeffs, 1);

        // coeffs 0 1 0 1 0 1 0 1
        coeffs[0] = _mm256_shuffle_epi8(coeffs_1, _mm256_set1_epi16(0x0200));
        // coeffs 2 3 2 3 2 3 2 3
        coeffs[1] = _mm256_shuffle_epi8(coeffs_1, _mm256_set1_epi16(0x0604));
        // coeffs 4 5 4 5 4 5 4 5
        coeffs[2] = _mm256_shuffle_epi8(coeffs_1, _mm256_set1_epi16(0x0a08));
        // coeffs 6 7 6 7 6 7 6 7
        coeffs[3] = _mm256_shuffle_epi8(coeffs_1, _mm256_set1_epi16(0x0e0c));
    }

    [MethodImpl(AI)]
    internal static void prepare_coeffs_2t(
        InterpFilterParams filter_params, int subpel_q4,
        Span<__m256i> coeffs /* [4] */)
    {
        Ptr<short> filter = av1_get_interp_filter_subpel_kernel(
            filter_params, subpel_q4 & SUBPEL_MASK);

        // [port] C loads 8 coefficients from `filter + 1`, one past the kernel (past the end
        // of the kernel table for the last subpel position, which throws in C#). Only lanes
        // 2..3 (filter[3], filter[4]) are used, so load the kernel itself and shift it down by
        // one coefficient: the same register in every used lane.
        __m128i coeff_8 = _mm_srli_si128(_mm_loadu_si128(filter), 2);
        __m256i coeff = _mm256_broadcastsi128_si256(coeff_8);

        // coeffs 3 4 3 4 3 4 3 4
        coeffs[0] = _mm256_shuffle_epi32(coeff, 0x55);
    }

    [MethodImpl(AI)]
    internal static void prepare_coeffs_4t(
        InterpFilterParams filter_params, int subpel_q4,
        Span<__m256i> coeffs /* [4] */)
    {
        Ptr<short> filter = av1_get_interp_filter_subpel_kernel(
            filter_params, subpel_q4 & SUBPEL_MASK);

        __m128i coeff_8 = _mm_loadu_si128(filter);
        __m256i coeff = _mm256_broadcastsi128_si256(coeff_8);
        // coeffs 2 3 2 3 2 3 2 3
        coeffs[0] = _mm256_shuffle_epi32(coeff, 0x55);
        // coeffs 4 5 4 5 4 5 4 5
        coeffs[1] = _mm256_shuffle_epi32(coeff, 0xaa);
    }

    [MethodImpl(AI)]
    internal static void prepare_coeffs_6t(
        InterpFilterParams filter_params, int subpel_q4,
        Span<__m256i> coeffs /* [4] */)
    {
        Ptr<short> filter = av1_get_interp_filter_subpel_kernel(
            filter_params, subpel_q4 & SUBPEL_MASK);

        // [port] C loads 8 coefficients from `filter + 1` (one past the kernel, see
        // prepare_coeffs_2t); lanes 0..5 (filter[1..6]) are used, the shifted kernel gives
        // the same values there.
        __m128i coeff_8 = _mm_srli_si128(_mm_loadu_si128(filter), 2);
        __m256i coeff = _mm256_broadcastsi128_si256(coeff_8);

        // coeffs 1 2 1 2 1 2 1 2
        coeffs[0] = _mm256_shuffle_epi32(coeff, 0x00);
        // coeffs 3 4 3 4 3 4 3 4
        coeffs[1] = _mm256_shuffle_epi32(coeff, 0x55);
        // coeffs 5 6 5 6 5 6 5 6
        coeffs[2] = _mm256_shuffle_epi32(coeff, 0xaa);
    }

    [MethodImpl(AI)]
    internal static void prepare_coeffs(InterpFilterParams filter_params,
                                        int subpel_q4,
                                        Span<__m256i> coeffs /* [4] */)
    {
        Ptr<short> filter = av1_get_interp_filter_subpel_kernel(
            filter_params, subpel_q4 & SUBPEL_MASK);

        __m128i coeff_8 = _mm_loadu_si128(filter);
        __m256i coeff = _mm256_broadcastsi128_si256(coeff_8);

        // coeffs 0 1 0 1 0 1 0 1
        coeffs[0] = _mm256_shuffle_epi32(coeff, 0x00);
        // coeffs 2 3 2 3 2 3 2 3
        coeffs[1] = _mm256_shuffle_epi32(coeff, 0x55);
        // coeffs 4 5 4 5 4 5 4 5
        coeffs[2] = _mm256_shuffle_epi32(coeff, 0xaa);
        // coeffs 6 7 6 7 6 7 6 7
        coeffs[3] = _mm256_shuffle_epi32(coeff, 0xff);
    }

    [MethodImpl(AI)]
    internal static void prepare_coeffs_12taps(
        InterpFilterParams filter_params, int subpel_q4,
        Span<__m256i> coeffs /* [4] */)
    {
        Ptr<short> filter = av1_get_interp_filter_subpel_kernel(
            filter_params, subpel_q4 & SUBPEL_MASK);

        __m128i coeff_8 = _mm_loadu_si128(filter);
        __m256i coeff = _mm256_broadcastsi128_si256(coeff_8);

        // coeffs 0 1 0 1 0 1 0 1
        coeffs[0] = _mm256_shuffle_epi32(coeff, 0x00);
        // coeffs 2 3 2 3 2 3 2 3
        coeffs[1] = _mm256_shuffle_epi32(coeff, 0x55);
        // coeffs 4 5 4 5 4 5 4 5
        coeffs[2] = _mm256_shuffle_epi32(coeff, 0xaa);
        // coeffs 6 7 6 7 6 7 6 7
        coeffs[3] = _mm256_shuffle_epi32(coeff, 0xff);
        // coeffs 8 9 10 11 0 0 0 0
        coeff_8 = _mm_loadl_epi64(filter + 8);
        coeff = _mm256_broadcastq_epi64(coeff_8);
        coeffs[4] = _mm256_shuffle_epi32(coeff, 0x00);  // coeffs 8 9 8 9 8 9 8 9
        coeffs[5] = _mm256_shuffle_epi32(coeff, 0x55);  // coeffs 10 11 10 11.. 10 11
    }

    [MethodImpl(AI)]
    internal static __m128i convolve_lowbd_4tap_ssse3(ReadOnlySpan<__m128i> ss,
                                                      ReadOnlySpan<__m128i> coeffs)
    {
        __m128i res_01 = _mm_maddubs_epi16(ss[0], coeffs[0]);
        __m128i res_23 = _mm_maddubs_epi16(ss[1], coeffs[1]);

        return _mm_add_epi16(res_01, res_23);
    }

    [MethodImpl(AI)]
    internal static __m128i convolve_lowbd_6tap_ssse3(ReadOnlySpan<__m128i> ss,
                                                      ReadOnlySpan<__m128i> coeffs)
    {
        __m128i res_01 = _mm_maddubs_epi16(ss[0], coeffs[0]);
        __m128i res_23 = _mm_maddubs_epi16(ss[1], coeffs[1]);
        __m128i res_45 = _mm_maddubs_epi16(ss[2], coeffs[2]);

        __m128i res = _mm_add_epi16(_mm_add_epi16(res_01, res_45), res_23);

        return res;
    }

    [MethodImpl(AI)]
    internal static __m128i convolve_lowbd_ssse3(ReadOnlySpan<__m128i> ss,
                                                 ReadOnlySpan<__m128i> coeffs)
    {
        __m128i res_01 = _mm_maddubs_epi16(ss[0], coeffs[0]);
        __m128i res_23 = _mm_maddubs_epi16(ss[1], coeffs[1]);
        __m128i res_45 = _mm_maddubs_epi16(ss[2], coeffs[2]);
        __m128i res_67 = _mm_maddubs_epi16(ss[3], coeffs[3]);

        __m128i res = _mm_add_epi16(_mm_add_epi16(res_01, res_45),
                                    _mm_add_epi16(res_23, res_67));

        return res;
    }

    [MethodImpl(AI)]
    internal static __m256i convolve_lowbd(ReadOnlySpan<__m256i> s,
                                           ReadOnlySpan<__m256i> coeffs)
    {
        __m256i res_01 = _mm256_maddubs_epi16(s[0], coeffs[0]);
        __m256i res_23 = _mm256_maddubs_epi16(s[1], coeffs[1]);
        __m256i res_45 = _mm256_maddubs_epi16(s[2], coeffs[2]);
        __m256i res_67 = _mm256_maddubs_epi16(s[3], coeffs[3]);

        // order: 0 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15
        __m256i res = _mm256_add_epi16(_mm256_add_epi16(res_01, res_45),
                                       _mm256_add_epi16(res_23, res_67));

        return res;
    }

    [MethodImpl(AI)]
    internal static __m256i convolve_lowbd_6tap(ReadOnlySpan<__m256i> s,
                                                ReadOnlySpan<__m256i> coeffs)
    {
        __m256i res_01 = _mm256_maddubs_epi16(s[0], coeffs[0]);
        __m256i res_23 = _mm256_maddubs_epi16(s[1], coeffs[1]);
        __m256i res_45 = _mm256_maddubs_epi16(s[2], coeffs[2]);

        // order: 0 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15
        __m256i res =
            _mm256_add_epi16(_mm256_add_epi16(res_01, res_45), res_23);

        return res;
    }

    [MethodImpl(AI)]
    internal static __m256i convolve_lowbd_4tap(ReadOnlySpan<__m256i> s,
                                                ReadOnlySpan<__m256i> coeffs)
    {
        __m256i res_23 = _mm256_maddubs_epi16(s[0], coeffs[0]);
        __m256i res_45 = _mm256_maddubs_epi16(s[1], coeffs[1]);

        // order: 0 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15
        __m256i res = _mm256_add_epi16(res_45, res_23);

        return res;
    }

    [MethodImpl(AI)]
    internal static __m256i convolve_6tap(ReadOnlySpan<__m256i> s,
                                          ReadOnlySpan<__m256i> coeffs)
    {
        __m256i res_0 = _mm256_madd_epi16(s[0], coeffs[0]);
        __m256i res_1 = _mm256_madd_epi16(s[1], coeffs[1]);
        __m256i res_2 = _mm256_madd_epi16(s[2], coeffs[2]);

        __m256i res = _mm256_add_epi32(_mm256_add_epi32(res_0, res_1), res_2);

        return res;
    }

    [MethodImpl(AI)]
    internal static __m256i convolve_12taps(ReadOnlySpan<__m256i> s,
                                            ReadOnlySpan<__m256i> coeffs)
    {
        __m256i res_0 = _mm256_madd_epi16(s[0], coeffs[0]);
        __m256i res_1 = _mm256_madd_epi16(s[1], coeffs[1]);
        __m256i res_2 = _mm256_madd_epi16(s[2], coeffs[2]);
        __m256i res_3 = _mm256_madd_epi16(s[3], coeffs[3]);
        __m256i res_4 = _mm256_madd_epi16(s[4], coeffs[4]);
        __m256i res_5 = _mm256_madd_epi16(s[5], coeffs[5]);

        __m256i res1 = _mm256_add_epi32(_mm256_add_epi32(res_0, res_1),
                                        _mm256_add_epi32(res_2, res_3));
        __m256i res = _mm256_add_epi32(_mm256_add_epi32(res_4, res_5), res1);

        return res;
    }

    [MethodImpl(AI)]
    internal static __m256i convolve(ReadOnlySpan<__m256i> s,
                                     ReadOnlySpan<__m256i> coeffs)
    {
        __m256i res_0 = _mm256_madd_epi16(s[0], coeffs[0]);
        __m256i res_1 = _mm256_madd_epi16(s[1], coeffs[1]);
        __m256i res_2 = _mm256_madd_epi16(s[2], coeffs[2]);
        __m256i res_3 = _mm256_madd_epi16(s[3], coeffs[3]);

        __m256i res = _mm256_add_epi32(_mm256_add_epi32(res_0, res_1),
                                       _mm256_add_epi32(res_2, res_3));

        return res;
    }

    [MethodImpl(AI)]
    internal static __m256i convolve_4tap(ReadOnlySpan<__m256i> s,
                                          ReadOnlySpan<__m256i> coeffs)
    {
        __m256i res_1 = _mm256_madd_epi16(s[0], coeffs[0]);
        __m256i res_2 = _mm256_madd_epi16(s[1], coeffs[1]);

        __m256i res = _mm256_add_epi32(res_1, res_2);
        return res;
    }

    [MethodImpl(AI)]
    internal static __m128i convolve_lowbd_x_2tap_ssse3(__m128i data,
                                                        ReadOnlySpan<__m128i> coeffs,
                                                        ReadOnlySpan<__m128i> filt)
    {
        __m128i s;
        s = _mm_shuffle_epi8(data, filt[0]);

        return _mm_maddubs_epi16(s, coeffs[0]);
    }

    [MethodImpl(AI)]
    internal static __m128i convolve_lowbd_x_4tap_ssse3(__m128i data,
                                                        ReadOnlySpan<__m128i> coeffs,
                                                        ReadOnlySpan<__m128i> filt)
    {
        Arr2<__m128i> s = default;

        s[0] = _mm_shuffle_epi8(data, filt[0]);
        s[1] = _mm_shuffle_epi8(data, filt[1]);

        return convolve_lowbd_4tap_ssse3(s, coeffs);
    }

    [MethodImpl(AI)]
    internal static __m256i convolve_lowbd_x(__m256i data,
                                             ReadOnlySpan<__m256i> coeffs,
                                             ReadOnlySpan<__m256i> filt)
    {
        Arr4<__m256i> s = default;

        s[0] = _mm256_shuffle_epi8(data, filt[0]);
        s[1] = _mm256_shuffle_epi8(data, filt[1]);
        s[2] = _mm256_shuffle_epi8(data, filt[2]);
        s[3] = _mm256_shuffle_epi8(data, filt[3]);

        return convolve_lowbd(s, coeffs);
    }

    [MethodImpl(AI)]
    internal static __m256i convolve_lowbd_x_6tap(__m256i data,
                                                  ReadOnlySpan<__m256i> coeffs,
                                                  ReadOnlySpan<__m256i> filt)
    {
        Arr4<__m256i> s = default;

        s[0] = _mm256_shuffle_epi8(data, filt[0]);
        s[1] = _mm256_shuffle_epi8(data, filt[1]);
        s[2] = _mm256_shuffle_epi8(data, filt[2]);

        return convolve_lowbd_6tap(s, coeffs);
    }

    [MethodImpl(AI)]
    internal static __m256i convolve_lowbd_x_4tap(__m256i data,
                                                  ReadOnlySpan<__m256i> coeffs,
                                                  ReadOnlySpan<__m256i> filt)
    {
        Arr2<__m256i> s = default;

        s[0] = _mm256_shuffle_epi8(data, filt[0]);
        s[1] = _mm256_shuffle_epi8(data, filt[1]);

        return convolve_lowbd_4tap(s, coeffs);
    }

    [MethodImpl(AI)]
    internal static __m256i convolve_lowbd_x_2tap(__m256i data,
                                                  ReadOnlySpan<__m256i> coeffs,
                                                  ReadOnlySpan<__m256i> filt)
    {
        __m256i s;
        s = _mm256_shuffle_epi8(data, filt[0]);

        return _mm256_maddubs_epi16(s, coeffs[0]);
    }

    [MethodImpl(AI)]
    internal static void add_store_aligned_256(Ptr<CONV_BUF_TYPE> dst,
                                               in __m256i res,
                                               int do_average)
    {
        __m256i d;
        if (do_average != 0)
        {
            d = _mm256_load_si256(dst);
            d = _mm256_add_epi32(d, res);
            d = _mm256_srai_epi32(d, 1);
        }
        else
        {
            d = res;
        }
        _mm256_store_si256(dst, d);
    }

    [MethodImpl(AI)]
    internal static __m256i comp_avg(in __m256i data_ref_0,
                                     in __m256i res_unsigned,
                                     in __m256i wt,
                                     int use_dist_wtd_comp_avg)
    {
        __m256i res;
        if (use_dist_wtd_comp_avg != 0)
        {
            __m256i data_lo = _mm256_unpacklo_epi16(data_ref_0, res_unsigned);
            __m256i data_hi = _mm256_unpackhi_epi16(data_ref_0, res_unsigned);

            __m256i wt_res_lo = _mm256_madd_epi16(data_lo, wt);
            __m256i wt_res_hi = _mm256_madd_epi16(data_hi, wt);

            __m256i res_lo = _mm256_srai_epi32(wt_res_lo, DIST_PRECISION_BITS);
            __m256i res_hi = _mm256_srai_epi32(wt_res_hi, DIST_PRECISION_BITS);

            res = _mm256_packs_epi32(res_lo, res_hi);
        }
        else
        {
            __m256i wt_res = _mm256_add_epi16(data_ref_0, res_unsigned);
            res = _mm256_srai_epi16(wt_res, 1);
        }
        return res;
    }

    [MethodImpl(AI)]
    internal static __m256i convolve_rounding(in __m256i res_unsigned,
                                              in __m256i offset_const,
                                              in __m256i round_const,
                                              int round_shift)
    {
        __m256i res_signed = _mm256_sub_epi16(res_unsigned, offset_const);
        __m256i res_round = _mm256_srai_epi16(
            _mm256_add_epi16(res_signed, round_const), round_shift);
        return res_round;
    }

    // [subset] highbd_comp_avg, highbd_convolve_rounding: high bitdepth.

    [MethodImpl(AI)]
    internal static __m256i round_sr_x_avx2(__m256i data)
    {
        // we can perform the below steps:
        // data = (data + 2) >> 2
        // data = (data + 8) >> 4,
        // in the below form as well
        // data = (data + 0x22) >> 6
        __m256i value = _mm256_set1_epi16(34);
        __m256i reg = _mm256_add_epi16(data, value);
        return _mm256_srai_epi16(reg, 6);
    }

    [MethodImpl(AI)]
    internal static __m128i convolve_x_4tap_4x2_ssse3(Ptr<byte> src,
                                                      int src_stride,
                                                      ReadOnlySpan<__m128i> coeffs)
    {
        Arr2<__m128i> data = default;
        __m128i f_l0 = _mm_load_si128(filt1_global_sse2);
        __m128i f_l1 = _mm_load_si128(filt2_global_sse2);
        __m128i src_1 =
            load_8bit_8x2_to_1_reg_sse2(src, src_stride);

        data[0] = _mm_shuffle_epi8(src_1, f_l0);
        data[1] = _mm_shuffle_epi8(src_1, f_l1);
        return convolve_lowbd_4tap_ssse3(data, coeffs);
    }

    [MethodImpl(AI)]
    internal static __m128i round_sr_x_ssse3(__m128i data)
    {
        __m128i val = _mm_set1_epi16(34);
        __m128i reg = _mm_add_epi16(data, val);
        return _mm_srai_epi16(reg, 6);
    }

    [MethodImpl(AI)]
    internal static void store_8bit_4x2_sse2(__m128i reg, Ptr<byte> dst,
                                             int dst_stride)
    {
        xx_storel_32(dst, reg);
        BinaryPrimitives.WriteUInt32LittleEndian((dst + dst_stride).AsSpan(4),
            ((uint)_mm_extract_epi16(reg, 3) << 16) | (uint)_mm_extract_epi16(reg, 2));
    }

    [MethodImpl(AI)]
    internal static void pack_store_u8_4x2_sse2(__m128i reg, Ptr<byte> dst,
                                                int dst_stride)
    {
        __m128i reg_pack = _mm_packus_epi16(reg, reg);
        store_8bit_4x2_sse2(reg_pack, dst, dst_stride);
    }

    [MethodImpl(AI)]
    internal static __m128i convolve_x_4tap_2x2_ssse3(Ptr<byte> src,
                                                      int src_stride,
                                                      ReadOnlySpan<__m128i> coeffs)
    {
        Arr2<__m128i> data = default;
        __m128i f_0 = _mm_load_si128(filt3_global_sse2);
        __m128i f_1 = _mm_load_si128(filt4_global_sse2);
        __m128i reg =
            load_8bit_8x2_to_1_reg_sse2(src, src_stride);

        data[0] = _mm_shuffle_epi8(reg, f_0);
        data[1] = _mm_shuffle_epi8(reg, f_1);
        return convolve_lowbd_4tap_ssse3(data, coeffs);
    }

    [MethodImpl(AI)]
    internal static void pack_store_u8_2x2_sse2(__m128i reg, Ptr<byte> dst,
                                                int dst_stride)
    {
        __m128i data = _mm_packus_epi16(reg, reg);
        BinaryPrimitives.WriteInt16LittleEndian(dst.AsSpan(2), (short)_mm_cvtsi128_si32(data));
        BinaryPrimitives.WriteInt16LittleEndian((dst + dst_stride).AsSpan(2),
                                                (short)_mm_extract_epi16(data, 1));
    }

    [MethodImpl(AI)]
    internal static __m128i convolve_x_2tap_ssse3(in __m128i data,
                                                  ReadOnlySpan<__m128i> coeff)
    {
        return _mm_maddubs_epi16(data, coeff[0]);
    }

    [MethodImpl(AI)]
    internal static __m128i load8_x_4x2_sse4(Ptr<byte> src,
                                             int offset)
    {
        __m128i s = _mm_cvtsi32_si128(loadu_int32(src));
        return _mm_insert_epi32(s, loadu_int32(src + offset), 1);
    }

    [MethodImpl(AI)]
    internal static __m128i load_x_u8_4x2_sse4(Ptr<byte> src,
                                               int stride)
    {
        return load8_x_4x2_sse4(src, stride);
    }

    [MethodImpl(AI)]
    internal static __m128i convolve_x_2tap_2x2_ssse3(Ptr<byte> src,
                                                      int stride,
                                                      ReadOnlySpan<__m128i> coeffs)
    {
        __m128i flt = _mm_load_si128(filt5_global_sse2);
        __m128i reg = load_x_u8_4x2_sse4(src, stride);
        __m128i data = _mm_shuffle_epi8(reg, flt);
        return convolve_x_2tap_ssse3(data, coeffs);
    }

    [MethodImpl(AI)]
    internal static __m128i convolve_x_2tap_4x2_ssse3(Ptr<byte> src,
                                                      int stride,
                                                      ReadOnlySpan<__m128i> coeffs)
    {
        __m128i flt = _mm_load_si128(filt1_global_sse2);
        __m128i data =
            load_8bit_8x2_to_1_reg_sse2(src, stride);
        __m128i res = _mm_shuffle_epi8(data, flt);
        return convolve_x_2tap_ssse3(res, coeffs);
    }

    [MethodImpl(AI)]
    internal static void convolve_x_2tap_8x2_ssse3(Ptr<byte> src,
                                                   int stride,
                                                   ReadOnlySpan<__m128i> coeffs,
                                                   Span<__m128i> data)
    {
        Arr2<__m128i> res = default;
        __m128i reg_00 = _mm_loadu_si128(src);
        __m128i reg_10 = _mm_loadu_si128(src + stride);
        __m128i reg_01 = _mm_srli_si128(reg_00, 1);
        __m128i reg_11 = _mm_srli_si128(reg_10, 1);
        res[0] = _mm_unpacklo_epi8(reg_00, reg_01);
        res[1] = _mm_unpacklo_epi8(reg_10, reg_11);

        data[0] = convolve_x_2tap_ssse3(res[0], coeffs);
        data[1] = convolve_x_2tap_ssse3(res[1], coeffs);
    }

    [MethodImpl(AI)]
    internal static __m256i loadu_x_8bit_16x2_avx2(Ptr<byte> src,
                                                   int offset)
    {
        __m128i reg0 = _mm_loadu_si128(src);
        __m128i reg1 = _mm_loadu_si128(src + offset);
        return _mm256_setr_m128i(reg0, reg1);
    }

    [MethodImpl(AI)]
    internal static __m256i convolve_x_2tap_avx2(in __m256i data,
                                                 ReadOnlySpan<__m256i> coeffs)
    {
        return _mm256_maddubs_epi16(data, coeffs[0]);
    }

    [MethodImpl(AI)]
    internal static void convolve_x_2tap_16x2_avx2(Ptr<byte> src,
                                                   int stride,
                                                   ReadOnlySpan<__m256i> coeffs,
                                                   Span<__m256i> data)
    {
        __m256i reg0 = loadu_x_8bit_16x2_avx2(src, stride);
        __m256i reg1 = loadu_x_8bit_16x2_avx2(src + 1, stride);
        __m256i res0 = _mm256_unpacklo_epi8(reg0, reg1);
        __m256i res1 = _mm256_unpackhi_epi8(reg0, reg1);
        data[0] = convolve_x_2tap_avx2(res0, coeffs);
        data[1] = convolve_x_2tap_avx2(res1, coeffs);
    }

    [MethodImpl(AI)]
    internal static void store_u8_16x2_avx2(__m256i src, Ptr<byte> dst,
                                            int stride)
    {
        __m128i reg0 = _mm256_castsi256_si128(src);
        __m128i reg1 = _mm256_extracti128_si256(src, 1);
        _mm_storeu_si128(dst, reg0);
        _mm_storeu_si128(dst + stride, reg1);
    }

    [MethodImpl(AI)]
    internal static void store_u8_8x2_avx2(__m256i src, Ptr<byte> dst,
                                           int stride)
    {
        __m128i reg0 = _mm256_castsi256_si128(src);
        __m128i reg1 = _mm256_extracti128_si256(src, 1);
        _mm_storel_epi64(dst, reg0);
        _mm_storel_epi64(dst + stride, reg1);
    }

    [MethodImpl(AI)]
    internal static void pack_store_16x2_avx2(__m256i data0,
                                              __m256i data1, Ptr<byte> dst,
                                              int stride)
    {
        __m256i res = _mm256_packus_epi16(data0, data1);
        store_u8_16x2_avx2(res, dst, stride);
    }

    [MethodImpl(AI)]
    internal static void pack_store_8x2_avx2(__m256i data, Ptr<byte> dst,
                                             int stride)
    {
        __m256i res = _mm256_packus_epi16(data, data);
        store_u8_8x2_avx2(res, dst, stride);
    }

    [MethodImpl(AI)]
    internal static void round_pack_store_16x2_avx2(ReadOnlySpan<__m256i> data,
                                                    Ptr<byte> dst,
                                                    int dst_stride)
    {
        Arr2<__m256i> reg = default;

        reg[0] = round_sr_x_avx2(data[0]);
        reg[1] = round_sr_x_avx2(data[1]);
        pack_store_16x2_avx2(reg[0], reg[1], dst, dst_stride);
    }

    [MethodImpl(AI)]
    internal static void convolve_x_2tap_32_avx2(Ptr<byte> src,
                                                 ReadOnlySpan<__m256i> coeffs,
                                                 Span<__m256i> data)
    {
        __m256i res0 = _mm256_loadu_si256(src);
        __m256i res1 = _mm256_loadu_si256(src + 1);
        __m256i reg0 = _mm256_unpacklo_epi8(res0, res1);
        __m256i reg1 = _mm256_unpackhi_epi8(res0, res1);

        data[0] = convolve_x_2tap_avx2(reg0, coeffs);
        data[1] = convolve_x_2tap_avx2(reg1, coeffs);
    }

    [MethodImpl(AI)]
    internal static void pack_store_32_avx2(__m256i data0, __m256i data1,
                                            Ptr<byte> dst)
    {
        __m256i reg = _mm256_packus_epi16(data0, data1);
        _mm256_storeu_si256(dst, reg);
    }

    [MethodImpl(AI)]
    internal static void round_pack_store_32_avx2(ReadOnlySpan<__m256i> data,
                                                  Ptr<byte> dst)
    {
        Arr2<__m256i> reg = default;

        reg[0] = round_sr_x_avx2(data[0]);
        reg[1] = round_sr_x_avx2(data[1]);
        pack_store_32_avx2(reg[0], reg[1], dst);
    }

    [MethodImpl(AI)]
    internal static void convolve_round_2tap_32_avx2(Ptr<byte> src,
                                                     ReadOnlySpan<__m256i> coeffs,
                                                     Ptr<byte> dst)
    {
        Arr2<__m256i> data = default;

        convolve_x_2tap_32_avx2(src, coeffs, data);
        round_pack_store_32_avx2(data, dst);
    }

    [MethodImpl(AI)]
    internal static void load_avg_store_2tap_32_avx2(Ptr<byte> src,
                                                     Ptr<byte> dst)
    {
        __m256i res0 = _mm256_loadu_si256(src);
        __m256i res1 = _mm256_loadu_si256(src + 1);
        __m256i data = _mm256_avg_epu8(res0, res1);
        _mm256_storeu_si256(dst, data);
    }

    [MethodImpl(AI)]
    internal static __m256i load_convolve_8tap_8x2_avx2(Ptr<byte> src,
                                                        int stride,
                                                        ReadOnlySpan<__m256i> coeffs,
                                                        ReadOnlySpan<__m256i> flt)
    {
        __m256i res = loadu_x_8bit_16x2_avx2(src, stride);
        return convolve_lowbd_x(res, coeffs, flt);
    }

    [MethodImpl(AI)]
    internal static void load_convolve_8tap_16x2_avx2(Ptr<byte> src,
                                                      int src_stride,
                                                      ReadOnlySpan<__m256i> coeffs,
                                                      ReadOnlySpan<__m256i> flt,
                                                      Span<__m256i> reg)
    {
        reg[0] = load_convolve_8tap_8x2_avx2(src + 0, src_stride, coeffs, flt);
        reg[1] = load_convolve_8tap_8x2_avx2(src + 8, src_stride, coeffs, flt);
    }

    [MethodImpl(AI)]
    internal static void load_convolve_8tap_32_avx2(Ptr<byte> src,
                                                    ReadOnlySpan<__m256i> coeffs,
                                                    ReadOnlySpan<__m256i> filt,
                                                    Span<__m256i> data)
    {
        __m256i reg_0 = _mm256_loadu_si256(src);
        __m256i reg_8 = _mm256_loadu_si256(src + 8);

        data[0] = convolve_lowbd_x(reg_0, coeffs, filt);
        data[1] = convolve_lowbd_x(reg_8, coeffs, filt);
    }

    [MethodImpl(AI)]
    internal static void load_convolve_round_8tap_32_avx2(Ptr<byte> src,
                                                          ReadOnlySpan<__m256i> coeffs,
                                                          ReadOnlySpan<__m256i> filt,
                                                          Ptr<byte> dst)
    {
        Arr2<__m256i> data = default;

        load_convolve_8tap_32_avx2(src, coeffs, filt, data);
        round_pack_store_32_avx2(data, dst);
    }

    [MethodImpl(AI)]
    internal static void load_convolve_6tap_32_avx2(Ptr<byte> src,
                                                    ReadOnlySpan<__m256i> coeffs,
                                                    ReadOnlySpan<__m256i> filt,
                                                    Span<__m256i> data)
    {
        __m256i reg0 = _mm256_loadu_si256(src);
        __m256i reg1 = _mm256_loadu_si256(src + 8);

        data[0] = convolve_lowbd_x_6tap(reg0, coeffs, filt);
        data[1] = convolve_lowbd_x_6tap(reg1, coeffs, filt);
    }

    [MethodImpl(AI)]
    internal static void convolve_sr_store_6tap_32_avx2(Ptr<byte> src,
                                                        ReadOnlySpan<__m256i> coeffs,
                                                        ReadOnlySpan<__m256i> filt,
                                                        Ptr<byte> dst)
    {
        Arr2<__m256i> data = default;

        load_convolve_6tap_32_avx2(src, coeffs, filt, data);
        round_pack_store_32_avx2(data, dst);
    }

    [MethodImpl(AI)]
    internal static __m256i load_convolve_6tap_8x2_avx2(Ptr<byte> src,
                                                        int stride,
                                                        ReadOnlySpan<__m256i> coeffs,
                                                        ReadOnlySpan<__m256i> filt)
    {
        __m256i data = loadu_x_8bit_16x2_avx2(src, stride);
        return convolve_lowbd_x_6tap(data, coeffs, filt);
    }

    [MethodImpl(AI)]
    internal static void load_convolve_6tap_16x2_avx2(Ptr<byte> src,
                                                      int src_stride,
                                                      ReadOnlySpan<__m256i> coeffs,
                                                      ReadOnlySpan<__m256i> filt,
                                                      Span<__m256i> data)
    {
        data[0] = load_convolve_6tap_8x2_avx2(src + 0, src_stride, coeffs, filt);
        data[1] = load_convolve_6tap_8x2_avx2(src + 8, src_stride, coeffs, filt);
    }

    [MethodImpl(AI)]
    internal static __m128i round_sr_y_ssse3(__m128i data)
    {
        __m128i value = _mm_set1_epi16(32);
        __m128i reg = _mm_add_epi16(data, value);
        return _mm_srai_epi16(reg, FILTER_BITS - 1);
    }

    [MethodImpl(AI)]
    internal static __m256i round_sr_y_avx2(__m256i data)
    {
        __m256i value = _mm256_set1_epi16(32);
        __m256i reg = _mm256_add_epi16(data, value);
        return _mm256_srai_epi16(reg, FILTER_BITS - 1);
    }

    [MethodImpl(AI)]
    internal static void round_pack_store_y_8x2_avx2(__m256i res,
                                                     Ptr<byte> dst,
                                                     int dst_stride)
    {
        __m256i r;

        r = round_sr_y_avx2(res);
        pack_store_8x2_avx2(r, dst, dst_stride);
    }

    [MethodImpl(AI)]
    internal static void round_pack_store_y_16x2_avx2(ReadOnlySpan<__m256i> res,
                                                      Ptr<byte> dst,
                                                      int dst_stride)
    {
        Arr2<__m256i> r = default;

        r[0] = round_sr_y_avx2(res[0]);
        r[1] = round_sr_y_avx2(res[1]);
        pack_store_16x2_avx2(r[0], r[1], dst, dst_stride);
    }

    [MethodImpl(AI)]
    internal static void round_pack_store_y_32_avx2(ReadOnlySpan<__m256i> res,
                                                    Ptr<byte> dst)
    {
        Arr2<__m256i> r = default;

        r[0] = round_sr_y_avx2(res[0]);
        r[1] = round_sr_y_avx2(res[1]);
        pack_store_32_avx2(r[0], r[1], dst);
    }

    [MethodImpl(AI)]
    internal static void round_pack_store_y_32x2_avx2(ReadOnlySpan<__m256i> res,
                                                      Ptr<byte> dst,
                                                      int dst_stride)
    {
        round_pack_store_y_32_avx2(res, dst);
        round_pack_store_y_32_avx2(res.Slice(2), dst + dst_stride);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_2tap_2x2_ssse3(Ptr<byte> data,
                                                   int stride,
                                                   ReadOnlySpan<__m128i> coeffs,
                                                   Span<__m128i> d, ref __m128i res)
    {
        d[1] = _mm_cvtsi32_si128(loadu_int16(data + 1 * stride));
        __m128i src_01a = _mm_unpacklo_epi16(d[0], d[1]);
        d[0] = _mm_cvtsi32_si128(loadu_int16(data + 2 * stride));
        __m128i src_12a = _mm_unpacklo_epi16(d[1], d[0]);

        __m128i s = _mm_unpacklo_epi8(src_01a, src_12a);

        res = _mm_maddubs_epi16(s, coeffs[0]);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_4tap_2x2_ssse3(Ptr<byte> data,
                                                   int stride,
                                                   ReadOnlySpan<__m128i> coeffs,
                                                   Span<__m128i> d, Span<__m128i> s,
                                                   ref __m128i res)
    {
        d[3] = _mm_cvtsi32_si128(loadu_int16(data + 3 * stride));
        __m128i src_23a = _mm_unpacklo_epi16(d[2], d[3]);
        d[2] = _mm_cvtsi32_si128(loadu_int16(data + 4 * stride));
        __m128i src_34a = _mm_unpacklo_epi16(d[3], d[2]);

        s[1] = _mm_unpacklo_epi8(src_23a, src_34a);

        res = convolve_lowbd_4tap_ssse3(s, coeffs);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_6tap_2x2_ssse3(Ptr<byte> data,
                                                   int stride,
                                                   ReadOnlySpan<__m128i> coeffs,
                                                   Span<__m128i> d, Span<__m128i> s,
                                                   ref __m128i res)
    {
        d[5] = _mm_cvtsi32_si128(loadu_int16(data + 5 * stride));
        __m128i src_45a = _mm_unpacklo_epi16(d[4], d[5]);
        d[4] = _mm_cvtsi32_si128(loadu_int16(data + 6 * stride));
        __m128i src_56a = _mm_unpacklo_epi16(d[5], d[4]);

        s[2] = _mm_unpacklo_epi8(src_45a, src_56a);

        res = convolve_lowbd_6tap_ssse3(s, coeffs);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_8tap_2x2_ssse3(Ptr<byte> data,
                                                   int stride,
                                                   ReadOnlySpan<__m128i> coeffs,
                                                   Span<__m128i> d, Span<__m128i> s,
                                                   ref __m128i res)
    {
        d[7] = _mm_cvtsi32_si128(loadu_int16(data + 7 * stride));
        __m128i src_67a = _mm_unpacklo_epi16(d[6], d[7]);
        d[6] = _mm_cvtsi32_si128(loadu_int16(data + 8 * stride));
        __m128i src_78a = _mm_unpacklo_epi16(d[7], d[6]);

        s[3] = _mm_unpacklo_epi8(src_67a, src_78a);

        res = convolve_lowbd_ssse3(s, coeffs);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_2tap_4x2_ssse3(Ptr<byte> data,
                                                   int stride,
                                                   ReadOnlySpan<__m128i> coeffs,
                                                   Span<__m128i> d, ref __m128i res)
    {
        d[1] = _mm_cvtsi32_si128(loadu_int32(data + 1 * stride));
        __m128i src_01a = _mm_unpacklo_epi32(d[0], d[1]);
        d[0] = _mm_cvtsi32_si128(loadu_int32(data + 2 * stride));
        __m128i src_12a = _mm_unpacklo_epi32(d[1], d[0]);

        __m128i s = _mm_unpacklo_epi8(src_01a, src_12a);

        res = _mm_maddubs_epi16(s, coeffs[0]);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_4tap_4x2_ssse3(Ptr<byte> data,
                                                   int stride,
                                                   ReadOnlySpan<__m128i> coeffs,
                                                   Span<__m128i> d, Span<__m128i> s,
                                                   ref __m128i res)
    {
        d[3] = _mm_cvtsi32_si128(loadu_int32(data + 3 * stride));
        __m128i src_23a = _mm_unpacklo_epi32(d[2], d[3]);
        d[2] = _mm_cvtsi32_si128(loadu_int32(data + 4 * stride));
        __m128i src_34a = _mm_unpacklo_epi32(d[3], d[2]);

        s[1] = _mm_unpacklo_epi8(src_23a, src_34a);

        res = convolve_lowbd_4tap_ssse3(s, coeffs);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_6tap_4x2_ssse3(Ptr<byte> data,
                                                   int stride,
                                                   ReadOnlySpan<__m128i> coeffs,
                                                   Span<__m128i> d, Span<__m128i> s,
                                                   ref __m128i res)
    {
        d[5] = _mm_cvtsi32_si128(loadu_int32(data + 5 * stride));
        __m128i src_45a = _mm_unpacklo_epi32(d[4], d[5]);
        d[4] = _mm_cvtsi32_si128(loadu_int32(data + 6 * stride));
        __m128i src_56a = _mm_unpacklo_epi32(d[5], d[4]);

        s[2] = _mm_unpacklo_epi8(src_45a, src_56a);

        res = convolve_lowbd_6tap_ssse3(s, coeffs);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_8tap_4x2_ssse3(Ptr<byte> data,
                                                   int stride,
                                                   ReadOnlySpan<__m128i> coeffs,
                                                   Span<__m128i> d, Span<__m128i> s,
                                                   ref __m128i res)
    {
        d[7] = _mm_cvtsi32_si128(loadu_int32(data + 7 * stride));
        __m128i src_67a = _mm_unpacklo_epi32(d[6], d[7]);
        d[6] = _mm_cvtsi32_si128(loadu_int32(data + 8 * stride));
        __m128i src_78a = _mm_unpacklo_epi32(d[7], d[6]);

        s[3] = _mm_unpacklo_epi8(src_67a, src_78a);

        res = convolve_lowbd_ssse3(s, coeffs);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_2tap_8x2_avx2(Ptr<byte> data,
                                                  int stride,
                                                  ReadOnlySpan<__m256i> coeffs, Span<__m128i> d,
                                                  ref __m256i res)
    {
        d[1] = _mm_loadu_si128(data + 1 * stride);
        __m256i src_01a = _mm256_setr_m128i(d[0], d[1]);
        d[0] = _mm_loadu_si128(data + 2 * stride);
        __m256i src_12a = _mm256_setr_m128i(d[1], d[0]);

        __m256i s = _mm256_unpacklo_epi8(src_01a, src_12a);

        res = _mm256_maddubs_epi16(s, coeffs[0]);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_4tap_8x2_avx2(Ptr<byte> data,
                                                  int stride,
                                                  ReadOnlySpan<__m256i> coeffs,
                                                  Span<__m128i> d, Span<__m256i> s,
                                                  ref __m256i res)
    {
        d[3] = _mm_loadu_si128(data + 3 * stride);
        __m256i src_23a = _mm256_setr_m128i(d[2], d[3]);
        d[2] = _mm_loadu_si128(data + 4 * stride);
        __m256i src_34a = _mm256_setr_m128i(d[3], d[2]);

        s[1] = _mm256_unpacklo_epi8(src_23a, src_34a);

        res = convolve_lowbd_4tap(s, coeffs);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_6tap_8x2_avx2(Ptr<byte> data,
                                                  int stride,
                                                  ReadOnlySpan<__m256i> coeffs,
                                                  Span<__m128i> d, Span<__m256i> s,
                                                  ref __m256i res)
    {
        d[5] = _mm_loadu_si128(data + 5 * stride);
        __m256i src_45a = _mm256_setr_m128i(d[4], d[5]);
        d[4] = _mm_loadu_si128(data + 6 * stride);
        __m256i src_56a = _mm256_setr_m128i(d[5], d[4]);

        s[2] = _mm256_unpacklo_epi8(src_45a, src_56a);

        res = convolve_lowbd_6tap(s, coeffs);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_8tap_8x2_avx2(Ptr<byte> data,
                                                  int stride,
                                                  ReadOnlySpan<__m256i> coeffs,
                                                  Span<__m128i> d, Span<__m256i> s,
                                                  ref __m256i res)
    {
        d[7] = _mm_loadu_si128(data + 7 * stride);
        __m256i src_67a = _mm256_setr_m128i(d[6], d[7]);
        d[6] = _mm_loadu_si128(data + 8 * stride);
        __m256i src_78a = _mm256_setr_m128i(d[7], d[6]);

        s[3] = _mm256_unpacklo_epi8(src_67a, src_78a);

        res = convolve_lowbd(s, coeffs);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_2tap_16x2_avx2(Ptr<byte> data,
                                                   int stride,
                                                   ReadOnlySpan<__m256i> coeffs,
                                                   Span<__m128i> d, Span<__m256i> res)
    {
        d[1] = _mm_loadu_si128(data + 1 * stride);
        __m256i src_01a = _mm256_setr_m128i(d[0], d[1]);
        d[0] = _mm_loadu_si128(data + 2 * stride);
        __m256i src_12a = _mm256_setr_m128i(d[1], d[0]);

        __m256i s0 = _mm256_unpacklo_epi8(src_01a, src_12a);
        __m256i s1 = _mm256_unpackhi_epi8(src_01a, src_12a);

        res[0] = _mm256_maddubs_epi16(s0, coeffs[0]);
        res[1] = _mm256_maddubs_epi16(s1, coeffs[0]);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_4tap_16x2_avx2(Ptr<byte> data,
                                                   int stride,
                                                   ReadOnlySpan<__m256i> coeffs,
                                                   Span<__m128i> d, Span<__m256i> s,
                                                   Span<__m256i> res)
    {
        d[3] = _mm_loadu_si128(data + 3 * stride);
        __m256i src_23a = _mm256_setr_m128i(d[2], d[3]);
        d[2] = _mm_loadu_si128(data + 4 * stride);
        __m256i src_34a = _mm256_setr_m128i(d[3], d[2]);

        s[1] = _mm256_unpacklo_epi8(src_23a, src_34a);
        s[3] = _mm256_unpackhi_epi8(src_23a, src_34a);

        res[0] = convolve_lowbd_4tap(s, coeffs);
        res[1] = convolve_lowbd_4tap(s.Slice(2), coeffs);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_6tap_16x2_avx2(Ptr<byte> data,
                                                   int stride,
                                                   ReadOnlySpan<__m256i> coeffs,
                                                   Span<__m128i> d, Span<__m256i> s,
                                                   Span<__m256i> res)
    {
        d[5] = _mm_loadu_si128(data + 5 * stride);
        __m256i src_45a = _mm256_setr_m128i(d[4], d[5]);
        d[4] = _mm_loadu_si128(data + 6 * stride);
        __m256i src_56a = _mm256_setr_m128i(d[5], d[4]);

        s[2] = _mm256_unpacklo_epi8(src_45a, src_56a);
        s[5] = _mm256_unpackhi_epi8(src_45a, src_56a);

        res[0] = convolve_lowbd_6tap(s, coeffs);
        res[1] = convolve_lowbd_6tap(s.Slice(3), coeffs);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_8tap_16x2_avx2(Ptr<byte> data,
                                                   int stride,
                                                   ReadOnlySpan<__m256i> coeffs,
                                                   Span<__m128i> d, Span<__m256i> s,
                                                   Span<__m256i> res)
    {
        d[7] = _mm_loadu_si128(data + 7 * stride);
        __m256i src_67a = _mm256_setr_m128i(d[6], d[7]);
        d[6] = _mm_loadu_si128(data + 8 * stride);
        __m256i src_78a = _mm256_setr_m128i(d[7], d[6]);

        s[3] = _mm256_unpacklo_epi8(src_67a, src_78a);
        s[7] = _mm256_unpackhi_epi8(src_67a, src_78a);

        res[0] = convolve_lowbd(s, coeffs);
        res[1] = convolve_lowbd(s.Slice(4), coeffs);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_2tap_32x2_avx2(Ptr<byte> data,
                                                   int stride,
                                                   ReadOnlySpan<__m256i> coeffs,
                                                   Span<__m256i> d, Span<__m256i> res)
    {
        d[1] = _mm256_loadu_si256(data + 1 * stride);
        __m256i s00 = _mm256_unpacklo_epi8(d[0], d[1]);
        __m256i s01 = _mm256_unpackhi_epi8(d[0], d[1]);
        d[0] = _mm256_loadu_si256(data + 2 * stride);
        __m256i s10 = _mm256_unpacklo_epi8(d[1], d[0]);
        __m256i s11 = _mm256_unpackhi_epi8(d[1], d[0]);

        res[0] = _mm256_maddubs_epi16(s00, coeffs[0]);
        res[1] = _mm256_maddubs_epi16(s01, coeffs[0]);
        res[2] = _mm256_maddubs_epi16(s10, coeffs[0]);
        res[3] = _mm256_maddubs_epi16(s11, coeffs[0]);
    }

    [MethodImpl(AI)]
    internal static void convolve_y_4tap_32x2_avx2(Ptr<byte> data,
                                                   int stride,
                                                   ReadOnlySpan<__m256i> coeffs,
                                                   Span<__m256i> d, Span<__m256i> s1,
                                                   Span<__m256i> s2, Span<__m256i> res)
    {
        d[3] = _mm256_loadu_si256(data + 3 * stride);
        s1[1] = _mm256_unpacklo_epi8(d[2], d[3]);
        s1[3] = _mm256_unpackhi_epi8(d[2], d[3]);
        d[2] = _mm256_loadu_si256(data + 4 * stride);
        s2[1] = _mm256_unpacklo_epi8(d[3], d[2]);
        s2[3] = _mm256_unpackhi_epi8(d[3], d[2]);

        res[0] = convolve_lowbd_4tap(s1, coeffs);
        res[1] = convolve_lowbd_4tap(s1.Slice(2), coeffs);
        res[2] = convolve_lowbd_4tap(s2, coeffs);
        res[3] = convolve_lowbd_4tap(s2.Slice(2), coeffs);
    }
}
