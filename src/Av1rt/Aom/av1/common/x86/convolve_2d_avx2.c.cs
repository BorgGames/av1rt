// Port of libaom av1/common/x86/convolve_2d_avx2.c

global using static Av1rt.Aom.convolve_2d_avx2;

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class convolve_2d_avx2
{
    // [port] `DECLARE_ALIGNED(32, int16_t, im_block[(MAX_SB_SIZE + MAX_FILTER_TAP) * 4])` and
    // `im_block_buf[(MAX_SB_SIZE / 8) * (MAX_SB_SIZE + MAX_FILTER_TAP) * 8]`: per-thread
    // scratch buffers (like s_im_block in convolve.c.cs). Every element read is written first
    // in the same call.
    [ThreadStatic] private static short[] s_im_block_w4;
    [ThreadStatic] private static short[] s_im_block_buf;

    private static void convolve_2d_sr_w4_avx2(
        Ptr<byte> src, int src_stride, Ptr<byte> dst, int dst_stride,
        int w, int h, InterpFilterParams filter_params_x,
        InterpFilterParams filter_params_y, int subpel_x_qn,
        int subpel_y_qn, ref ConvolveParams conv_params)
    {
        Ptr<short> im_block =
            s_im_block_w4 ??= new short[(MAX_SB_SIZE + MAX_FILTER_TAP) * 4];
        Ptr<byte> dst_ptr = dst;
        Debug.Assert(conv_params.round_0 == 3);
        Debug.Assert(conv_params.round_1 == 11);

        __m128i round_const_h = _mm_set1_epi16((short)(1 << (conv_params.round_0 - 2)));
        __m256i round_const_v =
            _mm256_set1_epi32(1 << (conv_params.round_1 - 1));

        Arr2<__m128i> filt = default, coeffs_h = default;
        Arr4<__m256i> coeffs_v = default;

        int horiz_tap = get_filter_tap(filter_params_x, subpel_x_qn);
        int vert_tap = get_filter_tap(filter_params_y, subpel_y_qn);

        Debug.Assert(horiz_tap == 2 || horiz_tap == 4);
        Debug.Assert(vert_tap == 2 || vert_tap == 4 || vert_tap == 6 || vert_tap == 8);

        if (horiz_tap == 2)
            prepare_coeffs_2t_ssse3(filter_params_x, subpel_x_qn, coeffs_h);
        else
            prepare_coeffs_4t_ssse3(filter_params_x, subpel_x_qn, coeffs_h);

        if (vert_tap == 2)
            prepare_coeffs_2t(filter_params_y, subpel_y_qn, coeffs_v);
        else if (vert_tap == 4)
            prepare_coeffs_4t(filter_params_y, subpel_y_qn, coeffs_v);
        else if (vert_tap == 6)
            prepare_coeffs_6t(filter_params_y, subpel_y_qn, coeffs_v);
        else
            prepare_coeffs(filter_params_y, subpel_y_qn, coeffs_v);

        int im_h = h + vert_tap - 1;
        int fo_vert = vert_tap / 2 - 1;
        int fo_horiz = horiz_tap / 2 - 1;
        Ptr<byte> src_ptr = src - fo_vert * src_stride - fo_horiz;

        filt[0] = _mm_load_si128(filt1_global_sse2);
        filt[1] = _mm_load_si128(filt2_global_sse2);

        if (horiz_tap == 2)
        {
            CONVOLVE_SR_HOR_FILTER_2TAP_W4(im_h, src_ptr, src_stride, coeffs_h, filt,
                                           round_const_h, im_block);
        }
        else
        {
            CONVOLVE_SR_HOR_FILTER_4TAP_W4(im_h, src_ptr, src_stride, coeffs_h, filt,
                                           round_const_h, im_block);
        }

        if (vert_tap == 2)
        {
            CONVOLVE_SR_VER_FILTER_2TAP_W4(im_block, w, h, coeffs_v, dst_ptr, dst_stride,
                                           round_const_v);
        }
        else if (vert_tap == 4)
        {
            CONVOLVE_SR_VER_FILTER_4TAP_W4(im_block, w, h, coeffs_v, dst_ptr, dst_stride,
                                           round_const_v);
        }
        else if (vert_tap == 6)
        {
            CONVOLVE_SR_VER_FILTER_6TAP_W4(im_block, w, h, coeffs_v, dst_ptr, dst_stride,
                                           round_const_v);
        }
        else
        {
            CONVOLVE_SR_VER_FILTER_8TAP_W4(im_block, w, h, coeffs_v, dst_ptr, dst_stride,
                                           round_const_v);
        }
    }

    private static void convolve_2d_sr_avx2(Ptr<byte> src, int src_stride,
                                            Ptr<byte> dst, int dst_stride, int w, int h,
                                            InterpFilterParams filter_params_x,
                                            InterpFilterParams filter_params_y,
                                            int subpel_x_qn, int subpel_y_qn,
                                            ref ConvolveParams conv_params)
    {
        Ptr<short> im_block_buf =
            s_im_block_buf ??= new short[(MAX_SB_SIZE / 8) * (MAX_SB_SIZE + MAX_FILTER_TAP) * 8];
        if (filter_params_x.taps > 8)
        {
            const int bd = 8;
            int im_stride = 8, i;
            const int strip_stride = (MAX_SB_SIZE + MAX_FILTER_TAP) * 8;
            int bits =
                FILTER_BITS * 2 - conv_params.round_0 - conv_params.round_1;
            int offset_bits = bd + 2 * FILTER_BITS - conv_params.round_0;

            Debug.Assert(conv_params.round_0 > 0);

            __m256i round_const_h12 = _mm256_set1_epi32(
                ((1 << (conv_params.round_0)) >> 1) + (1 << (bd + FILTER_BITS - 1)));
            __m128i round_shift_h12 = _mm_cvtsi32_si128(conv_params.round_0);

            __m256i sum_round_v = _mm256_set1_epi32(
                (1 << offset_bits) + ((1 << conv_params.round_1) >> 1));
            __m128i sum_shift_v = _mm_cvtsi32_si128(conv_params.round_1);

            __m256i round_const_v = _mm256_set1_epi32(
                ((1 << bits) >> 1) - (1 << (offset_bits - conv_params.round_1)) -
                ((1 << (offset_bits - conv_params.round_1)) >> 1));
            __m128i round_shift_v = _mm_cvtsi32_si128(bits);

            Arr6<__m256i> coeffs_h = default, coeffs_v = default;

            int horiz_tap = 12;
            int vert_tap = 12;

            prepare_coeffs_12taps(filter_params_x, subpel_x_qn, coeffs_h);
            prepare_coeffs_12taps(filter_params_y, subpel_y_qn, coeffs_v);

            int im_h = h + vert_tap - 1;
            int fo_vert = vert_tap / 2 - 1;
            int fo_horiz = horiz_tap / 2 - 1;
            Ptr<byte> src_ptr = src - fo_vert * src_stride - fo_horiz;

            __m256i v_zero = _mm256_setzero_si256();
            Arr12<__m256i> s = default;
            if (w <= 4)
            {
                for (i = 0; i < im_h; i += 2)
                {
                    for (int j = 0; j < w; j += 8)
                    {
                        Ptr<short> strip_im_block = im_block_buf + ((j / 8) * strip_stride);
                        __m256i data = _mm256_permute2x128_si256(
                            _mm256_castsi128_si256(
                                _mm_loadu_si128(src_ptr + (i * src_stride + j))),
                            _mm256_castsi128_si256(_mm_loadu_si128(
                                src_ptr + (i * src_stride + src_stride + j))),
                            0x20);
                        __m256i s_16lo = _mm256_unpacklo_epi8(data, v_zero);
                        __m256i s_16hi = _mm256_unpackhi_epi8(data, v_zero);
                        __m256i s_lolo = _mm256_unpacklo_epi16(s_16lo, s_16lo);
                        __m256i s_lohi = _mm256_unpackhi_epi16(s_16lo, s_16lo);

                        __m256i s_hilo = _mm256_unpacklo_epi16(s_16hi, s_16hi);
                        __m256i s_hihi = _mm256_unpackhi_epi16(s_16hi, s_16hi);

                        s[0] = _mm256_alignr_epi8(s_lohi, s_lolo, 2);
                        s[1] = _mm256_alignr_epi8(s_lohi, s_lolo, 10);
                        s[2] = _mm256_alignr_epi8(s_hilo, s_lohi, 2);
                        s[3] = _mm256_alignr_epi8(s_hilo, s_lohi, 10);
                        s[4] = _mm256_alignr_epi8(s_hihi, s_hilo, 2);
                        s[5] = _mm256_alignr_epi8(s_hihi, s_hilo, 10);

                        __m256i res_lo = convolve_12taps(s, coeffs_h);

                        __m256i res_32b_lo = _mm256_sra_epi32(
                            _mm256_add_epi32(res_lo, round_const_h12), round_shift_h12);
                        __m256i res_16b_lo = _mm256_packs_epi32(res_32b_lo, res_32b_lo);
                        __m128i res_0 = _mm256_extracti128_si256(res_16b_lo, 0);
                        __m128i res_1 = _mm256_extracti128_si256(res_16b_lo, 1);
                        if (w > 2)
                        {
                            _mm_storel_epi64(strip_im_block + (i * im_stride), res_0);
                            _mm_storel_epi64(
                                strip_im_block + (i * im_stride + im_stride), res_1);
                        }
                        else
                        {
                            uint horiz_2;
                            horiz_2 = (uint)_mm_cvtsi128_si32(res_0);
                            strip_im_block[i * im_stride] = unchecked((short)(ushort)horiz_2);
                            strip_im_block[i * im_stride + 1] = unchecked((short)(ushort)(horiz_2 >> 16));
                            horiz_2 = (uint)_mm_cvtsi128_si32(res_1);
                            strip_im_block[i * im_stride + im_stride] = unchecked((short)(ushort)horiz_2);
                            strip_im_block[i * im_stride + im_stride + 1] =
                                unchecked((short)(ushort)(horiz_2 >> 16));
                        }
                    }
                }
            }
            else
            {
                for (i = 0; i < im_h; i++)
                {
                    for (int j = 0; j < w; j += 8)
                    {
                        Ptr<short> strip_im_block = im_block_buf + ((j / 8) * strip_stride);
                        __m256i data = _mm256_permute2x128_si256(
                            _mm256_castsi128_si256(
                                _mm_loadu_si128(src_ptr + (i * src_stride + j))),
                            _mm256_castsi128_si256(_mm_loadu_si128(
                                src_ptr + (i * src_stride + j + 4))),
                            0x20);
                        __m256i s_16lo = _mm256_unpacklo_epi8(data, v_zero);
                        __m256i s_16hi = _mm256_unpackhi_epi8(data, v_zero);

                        __m256i s_lolo = _mm256_unpacklo_epi16(s_16lo, s_16lo);
                        __m256i s_lohi = _mm256_unpackhi_epi16(s_16lo, s_16lo);

                        __m256i s_hilo = _mm256_unpacklo_epi16(s_16hi, s_16hi);
                        __m256i s_hihi = _mm256_unpackhi_epi16(s_16hi, s_16hi);

                        s[0] = _mm256_alignr_epi8(s_lohi, s_lolo, 2);
                        s[1] = _mm256_alignr_epi8(s_lohi, s_lolo, 10);
                        s[2] = _mm256_alignr_epi8(s_hilo, s_lohi, 2);
                        s[3] = _mm256_alignr_epi8(s_hilo, s_lohi, 10);
                        s[4] = _mm256_alignr_epi8(s_hihi, s_hilo, 2);
                        s[5] = _mm256_alignr_epi8(s_hihi, s_hilo, 10);

                        __m256i res_lo = convolve_12taps(s, coeffs_h);

                        __m256i res_32b_lo = _mm256_sra_epi32(
                            _mm256_add_epi32(res_lo, round_const_h12), round_shift_h12);

                        __m256i res_16b_lo = _mm256_packs_epi32(res_32b_lo, res_32b_lo);
                        _mm_store_si128(strip_im_block + (i * im_stride),
                                        _mm256_extracti128_si256(
                                            _mm256_permute4x64_epi64(res_16b_lo, 0x88), 0));
                    }
                }
            }

            for (int j = 0; j < w; j += 8)
            {
                Ptr<short> im_block = im_block_buf + ((j / 8) * strip_stride);
                CONVOLVE_SR_VERTICAL_FILTER_12TAP(im_block, im_stride, w, h, j, dst, dst_stride,
                                                  s, coeffs_v, sum_round_v, sum_shift_v,
                                                  round_const_v, round_shift_v);
            }
        }
        else
        {
            int im_stride = 8, i;
            const int strip_stride = (MAX_SB_SIZE + MAX_FILTER_TAP) * 8;

            Debug.Assert(conv_params.round_0 == 3);
            Debug.Assert(conv_params.round_1 == 11);

            __m256i round_const_h =
                _mm256_set1_epi16((short)(1 << (conv_params.round_0 - 2)));
            __m256i round_const_v =
                _mm256_set1_epi32(1 << (conv_params.round_1 - 1));

            Arr4<__m256i> filt = default, coeffs_h = default, coeffs_v = default;

            int horiz_tap = get_filter_tap(filter_params_x, subpel_x_qn);
            int vert_tap = get_filter_tap(filter_params_y, subpel_y_qn);

            Debug.Assert(horiz_tap == 2 || horiz_tap == 4 || horiz_tap == 6 ||
                         horiz_tap == 8);
            Debug.Assert(vert_tap == 2 || vert_tap == 4 || vert_tap == 6 || vert_tap == 8);

            if (horiz_tap == 2)
                prepare_coeffs_2t_lowbd(filter_params_x, subpel_x_qn, coeffs_h);
            else if (horiz_tap == 4)
                prepare_coeffs_4t_lowbd(filter_params_x, subpel_x_qn, coeffs_h);
            else if (horiz_tap == 6)
                prepare_coeffs_6t_lowbd(filter_params_x, subpel_x_qn, coeffs_h);
            else
                prepare_coeffs_lowbd(filter_params_x, subpel_x_qn, coeffs_h);

            if (vert_tap == 2)
                prepare_coeffs_2t(filter_params_y, subpel_y_qn, coeffs_v);
            else if (vert_tap == 4)
                prepare_coeffs_4t(filter_params_y, subpel_y_qn, coeffs_v);
            else if (vert_tap == 6)
                prepare_coeffs_6t(filter_params_y, subpel_y_qn, coeffs_v);
            else
                prepare_coeffs(filter_params_y, subpel_y_qn, coeffs_v);

            int im_h = h + vert_tap - 1;
            int fo_vert = vert_tap / 2 - 1;
            int fo_horiz = horiz_tap / 2 - 1;
            Ptr<byte> src_ptr = src - fo_vert * src_stride - fo_horiz;

            filt[0] = _mm256_load_si256(filt1_global_avx2);
            filt[1] = _mm256_load_si256(filt2_global_avx2);
            filt[2] = _mm256_load_si256(filt3_global_avx2);
            filt[3] = _mm256_load_si256(filt4_global_avx2);

            for (i = 0; i < (im_h - 1); i += 2)
            {
                Ptr<byte> src_row0 = src_ptr + (i * src_stride);
                Ptr<byte> src_row1 = src_ptr + ((i + 1) * src_stride);
                for (int j = 0; j < w; j += 8)
                {
                    Ptr<short> strip_im_block = im_block_buf + ((j / 8) * strip_stride);
                    __m256i data =
                        _mm256_castsi128_si256(_mm_loadu_si128(src_row0 + j));
                    data = _mm256_inserti128_si256(
                        data, _mm_loadu_si128(src_row1 + j), 1);

                    __m256i res;
                    if (horiz_tap == 2)
                        res = convolve_lowbd_x_2tap(data, coeffs_h, filt);
                    else if (horiz_tap == 4)
                        res = convolve_lowbd_x_4tap(data, coeffs_h, filt);
                    else if (horiz_tap == 6)
                        res = convolve_lowbd_x_6tap(data, coeffs_h, filt);
                    else
                        res = convolve_lowbd_x(data, coeffs_h, filt);

                    res = _mm256_srai_epi16(_mm256_add_epi16(res, round_const_h), 2);
                    _mm256_store_si256(strip_im_block + (i * 8), res);
                }
            }
            {
                Ptr<byte> src_row0 = src_ptr + (i * src_stride);
                for (int j = 0; j < w; j += 8)
                {
                    Ptr<short> strip_im_block = im_block_buf + ((j / 8) * strip_stride);
                    __m256i data_1 =
                        _mm256_castsi128_si256(_mm_loadu_si128(src_row0 + j));
                    __m256i res;
                    if (horiz_tap == 2)
                        res = convolve_lowbd_x_2tap(data_1, coeffs_h, filt);
                    else if (horiz_tap == 4)
                        res = convolve_lowbd_x_4tap(data_1, coeffs_h, filt);
                    else if (horiz_tap == 6)
                        res = convolve_lowbd_x_6tap(data_1, coeffs_h, filt);
                    else
                        res = convolve_lowbd_x(data_1, coeffs_h, filt);

                    res = _mm256_srai_epi16(_mm256_add_epi16(res, round_const_h), 2);
                    _mm_store_si128(strip_im_block + (i * 8),
                                    _mm256_castsi256_si128(res));
                }
            }

            for (int j = 0; j < w; j += 8)
            {
                Ptr<short> im_block = im_block_buf + ((j / 8) * strip_stride);
                Ptr<byte> dst_ptr = dst + j;
                if (vert_tap == 2)
                {
                    CONVOLVE_SR_VERTICAL_FILTER_2TAP(im_block, im_stride, h, coeffs_v, dst_ptr,
                                                     dst_stride, round_const_v);
                }
                else if (vert_tap == 4)
                {
                    CONVOLVE_SR_VERTICAL_FILTER_4TAP(im_block, im_stride, h, coeffs_v, dst_ptr,
                                                     dst_stride, round_const_v);
                }
                else if (vert_tap == 6)
                {
                    CONVOLVE_SR_VERTICAL_FILTER_6TAP(im_block, im_stride, h, coeffs_v, dst_ptr,
                                                     dst_stride, round_const_v);
                }
                else
                {
                    CONVOLVE_SR_VERTICAL_FILTER_8TAP(im_block, im_stride, h, coeffs_v, dst_ptr,
                                                     dst_stride, round_const_v);
                }
            }
        }
    }

    internal static void av1_convolve_2d_sr_avx2(
        Ptr<byte> src, int src_stride, Ptr<byte> dst, int dst_stride,
        int w, int h, InterpFilterParams filter_params_x,
        InterpFilterParams filter_params_y, int subpel_x_qn,
        int subpel_y_qn, ref ConvolveParams conv_params)
    {
        int tap_x = get_filter_tap(filter_params_x, subpel_x_qn);
        int tap_y = get_filter_tap(filter_params_y, subpel_y_qn);

        bool use_12tap = (tap_x == 12 || tap_y == 12);
        if (w <= 4 && !use_12tap)
        {
            convolve_2d_sr_w4_avx2(src, src_stride, dst, dst_stride, w, h,
                                   filter_params_x, filter_params_y, subpel_x_qn,
                                   subpel_y_qn, ref conv_params);
        }
        else
        {
            convolve_2d_sr_avx2(src, src_stride, dst, dst_stride, w, h, filter_params_x,
                                filter_params_y, subpel_x_qn, subpel_y_qn, ref conv_params);
        }
    }
}
