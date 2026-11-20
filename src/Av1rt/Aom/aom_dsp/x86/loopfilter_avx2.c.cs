// Port of libaom aom_dsp/x86/loopfilter_avx2.c

global using static Av1rt.Aom.loopfilter_avx2;

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class loopfilter_avx2
{
    // DECLARE_ALIGNED(32, static const uint8_t, filt_loopfilter_avx2[32])
    private static ReadOnlySpan<byte> filt_loopfilter_avx2 => new byte[] {
        0, 128, 1, 128, 2,  128, 3,  128, 4,  128, 5,  128, 6,  128, 7,  128,
        8, 128, 9, 128, 10, 128, 11, 128, 12, 128, 13, 128, 14, 128, 15, 128
    };

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_lpf_horizontal_6_quad_avx2(Ptr<byte> s, int p,
                                                        Ptr<byte> _blimit0,
                                                        Ptr<byte> _limit0,
                                                        Ptr<byte> _thresh0)
    {
        __m128i thresh_v =
            _mm_broadcastb_epi8(_mm_cvtsi32_si128((int)_thresh0[0]));
        __m128i limit_v =
            _mm_broadcastb_epi8(_mm_cvtsi32_si128((int)_limit0[0]));
        __m128i blimit_v =
            _mm_broadcastb_epi8(_mm_cvtsi32_si128((int)_blimit0[0]));

        // [av1rt] The six lines p2..q2 (C loads each broadcast to both lanes of a 256-bit
        // register, for the widening in the flat filter: see lpf_6_quad_core).
        Span<byte> rows = (s - 3 * p).AsSpan(0, 5 * p + 16);
        __m128i p2 = _mm_loadu_si128(rows);
        __m128i p1 = _mm_loadu_si128(rows.Slice(p));
        __m128i p0 = _mm_loadu_si128(rows.Slice(2 * p));
        __m128i q0 = _mm_loadu_si128(rows.Slice(3 * p));
        __m128i q1 = _mm_loadu_si128(rows.Slice(4 * p));
        __m128i q2 = _mm_loadu_si128(rows.Slice(5 * p));

        if (!lpf_6_quad_core(p2, ref p1, ref p0, ref q0, ref q1, q2, blimit_v, limit_v,
                             thresh_v))
            return;

        // C also stores p2 and q2 on the flat path: unchanged.
        _mm_storeu_si128(rows.Slice(p), p1);
        _mm_storeu_si128(rows.Slice(2 * p), p0);
        _mm_storeu_si128(rows.Slice(3 * p), q0);
        _mm_storeu_si128(rows.Slice(4 * p), q1);
    }

    // [av1rt] aom_lpf_vertical_6_quad_sse2 (libaom has no AVX2 version) without its transpose
    // buffer: the 16 rows of 8 pixels across the edge are transposed into registers (as
    // transpose_16x8 does into the buffer), filtered by lpf_6_quad_core, and the four columns it
    // changes are written back. Same output.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_lpf_vertical_6_quad_avx2(Ptr<byte> s, int pitch,
                                                      Ptr<byte> _blimit0,
                                                      Ptr<byte> _limit0,
                                                      Ptr<byte> _thresh0)
    {
        __m128i thresh_v =
            _mm_broadcastb_epi8(_mm_cvtsi32_si128((int)_thresh0[0]));
        __m128i limit_v =
            _mm_broadcastb_epi8(_mm_cvtsi32_si128((int)_limit0[0]));
        __m128i blimit_v =
            _mm_broadcastb_epi8(_mm_cvtsi32_si128((int)_blimit0[0]));

        // Rows of columns -4..3: p3 p2 p1 p0 q0 q1 q2 q3 (a..h below; digits are rows).
        Span<byte> b = (s - 4).AsSpan(0, 15 * pitch + 8);
        // a0 a1 b0 b1 .. h0 h1, and so on for each row pair
        __m128i x0 = _mm_unpacklo_epi8(_mm_loadl_epi64(b), _mm_loadl_epi64(b.Slice(pitch)));
        __m128i x1 = _mm_unpacklo_epi8(_mm_loadl_epi64(b.Slice(2 * pitch)),
                                       _mm_loadl_epi64(b.Slice(3 * pitch)));
        __m128i x2 = _mm_unpacklo_epi8(_mm_loadl_epi64(b.Slice(4 * pitch)),
                                       _mm_loadl_epi64(b.Slice(5 * pitch)));
        __m128i x3 = _mm_unpacklo_epi8(_mm_loadl_epi64(b.Slice(6 * pitch)),
                                       _mm_loadl_epi64(b.Slice(7 * pitch)));
        __m128i x8 = _mm_unpacklo_epi8(_mm_loadl_epi64(b.Slice(8 * pitch)),
                                       _mm_loadl_epi64(b.Slice(9 * pitch)));
        __m128i x9 = _mm_unpacklo_epi8(_mm_loadl_epi64(b.Slice(10 * pitch)),
                                       _mm_loadl_epi64(b.Slice(11 * pitch)));
        __m128i x10 = _mm_unpacklo_epi8(_mm_loadl_epi64(b.Slice(12 * pitch)),
                                        _mm_loadl_epi64(b.Slice(13 * pitch)));
        __m128i x11 = _mm_unpacklo_epi8(_mm_loadl_epi64(b.Slice(14 * pitch)),
                                        _mm_loadl_epi64(b.Slice(15 * pitch)));

        // Columns a..d: a0..a3 b0..b3 c0..c3 d0..d3 (rows 0-3), then rows 4-7, 8-11, 12-15.
        __m128i lo03 = _mm_unpacklo_epi16(x0, x1), lo47 = _mm_unpacklo_epi16(x2, x3);
        __m128i lo8b = _mm_unpacklo_epi16(x8, x9), locf = _mm_unpacklo_epi16(x10, x11);
        // Columns e..h likewise.
        __m128i hi03 = _mm_unpackhi_epi16(x0, x1), hi47 = _mm_unpackhi_epi16(x2, x3);
        __m128i hi8b = _mm_unpackhi_epi16(x8, x9), hicf = _mm_unpackhi_epi16(x10, x11);

        // a0..a7 b0..b7 / c0..c7 d0..d7, and the same for rows 8-15 and for columns e..h.
        __m128i ab07 = _mm_unpacklo_epi32(lo03, lo47), cd07 = _mm_unpackhi_epi32(lo03, lo47);
        __m128i ab8f = _mm_unpacklo_epi32(lo8b, locf), cd8f = _mm_unpackhi_epi32(lo8b, locf);
        __m128i ef07 = _mm_unpacklo_epi32(hi03, hi47), gh07 = _mm_unpackhi_epi32(hi03, hi47);
        __m128i ef8f = _mm_unpacklo_epi32(hi8b, hicf), gh8f = _mm_unpackhi_epi32(hi8b, hicf);

        __m128i p2 = _mm_unpackhi_epi64(ab07, ab8f);
        __m128i p1 = _mm_unpacklo_epi64(cd07, cd8f);
        __m128i p0 = _mm_unpackhi_epi64(cd07, cd8f);
        __m128i q0 = _mm_unpacklo_epi64(ef07, ef8f);
        __m128i q1 = _mm_unpackhi_epi64(ef07, ef8f);
        __m128i q2 = _mm_unpacklo_epi64(gh07, gh8f);

        if (!lpf_6_quad_core(p2, ref p1, ref p0, ref q0, ref q1, q2, blimit_v, limit_v,
                             thresh_v))
            return;

        // Back to rows: p1 p0 q0 q1 of each row as one dword, rows 0-3 / 4-7 / 8-11 / 12-15.
        __m128i pp_lo = _mm_unpacklo_epi8(p1, p0), qq_lo = _mm_unpacklo_epi8(q0, q1);
        __m128i pp_hi = _mm_unpackhi_epi8(p1, p0), qq_hi = _mm_unpackhi_epi8(q0, q1);
        store_rows4(b.Slice(2), pitch, _mm_unpacklo_epi16(pp_lo, qq_lo));
        store_rows4(b.Slice(4 * pitch + 2), pitch, _mm_unpackhi_epi16(pp_lo, qq_lo));
        store_rows4(b.Slice(8 * pitch + 2), pitch, _mm_unpacklo_epi16(pp_hi, qq_hi));
        store_rows4(b.Slice(12 * pitch + 2), pitch, _mm_unpackhi_epi16(pp_hi, qq_hi));
    }

    // [av1rt] The four dwords of v to the starts of four rows.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void store_rows4(Span<byte> d, int pitch, __m128i v)
    {
        BinaryPrimitives.WriteInt32LittleEndian(d, _mm_cvtsi128_si32(v));
        BinaryPrimitives.WriteInt32LittleEndian(d.Slice(pitch), _mm_extract_epi32(v, 1));
        BinaryPrimitives.WriteInt32LittleEndian(d.Slice(2 * pitch), _mm_extract_epi32(v, 2));
        BinaryPrimitives.WriteInt32LittleEndian(d.Slice(3 * pitch), _mm_extract_epi32(v, 3));
    }

    // [av1rt] The filter of aom_lpf_horizontal_6_quad_avx2 on its six lines p2..q2 of 16 pixels,
    // shared with aom_lpf_vertical_6_quad_avx2. Returns false when the mask selects no pixel;
    // otherwise p1, p0, q0 and q1 are the filtered lines (the 6-tap filter keeps p2 and q2).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool lpf_6_quad_core(__m128i p2, ref __m128i p1_ref, ref __m128i p0_ref,
                                        ref __m128i q0_ref, ref __m128i q1_ref, __m128i q2,
                                        __m128i blimit_v, __m128i limit_v, __m128i thresh_v)
    {
        __m128i p1 = p1_ref, p0 = p0_ref, q0 = q0_ref, q1 = q1_ref;
        __m128i mask, flat;
        __m128i zero = _mm_setzero_si128();
        __m128i ff = _mm_cmpeq_epi8(zero, zero);

        {
            __m128i work;
            __m128i fe = _mm_set1_epi8(unchecked((sbyte)0xfe));
            __m128i abs_p1p0 =
                _mm_or_si128(_mm_subs_epu8(p1, p0), _mm_subs_epu8(p0, p1));
            __m128i abs_q1q0 =
                _mm_or_si128(_mm_subs_epu8(q1, q0), _mm_subs_epu8(q0, q1));
            __m128i abs_p0q0 =
                _mm_or_si128(_mm_subs_epu8(p0, q0), _mm_subs_epu8(q0, p0));
            __m128i abs_p1q1 =
                _mm_or_si128(_mm_subs_epu8(p1, q1), _mm_subs_epu8(q1, p1));

            flat = _mm_max_epu8(abs_p1p0, abs_q1q0);

            abs_p0q0 = _mm_adds_epu8(abs_p0q0, abs_p0q0);
            abs_p1q1 = _mm_srli_epi16(_mm_and_si128(abs_p1q1, fe), 1);
            mask = _mm_subs_epu8(_mm_adds_epu8(abs_p0q0, abs_p1q1), blimit_v);
            mask = _mm_xor_si128(_mm_cmpeq_epi8(mask, zero), ff);
            // mask |= (abs(p0 - q0) * 2 + abs(p1 - q1) / 2  > blimit) * -1;
            mask = _mm_max_epu8(flat, mask);
            // mask |= (abs(p1 - p0) > limit) * -1;
            // mask |= (abs(q1 - q0) > limit) * -1;
            work = _mm_max_epu8(
                _mm_or_si128(_mm_subs_epu8(p2, p1), _mm_subs_epu8(p1, p2)),
                _mm_or_si128(_mm_subs_epu8(q2, q1), _mm_subs_epu8(q1, q2)));
            mask = _mm_max_epu8(work, mask);
            mask = _mm_subs_epu8(mask, limit_v);
            mask = _mm_cmpeq_epi8(mask, zero);
        }

        if (0xffff == _mm_movemask_epi8(_mm_cmpeq_epi8(mask, zero))) return false;

        // loop filter
        {
            __m128i t4 = _mm_set1_epi8(4);
            __m128i t3 = _mm_set1_epi8(3);
            __m128i t80 = _mm_set1_epi8(unchecked((sbyte)0x80));
            __m128i te0 = _mm_set1_epi8(unchecked((sbyte)0xe0));
            __m128i t1f = _mm_set1_epi8(0x1f);
            __m128i t1 = _mm_set1_epi8(0x1);
            __m128i t7f = _mm_set1_epi8(0x7f);
            __m128i one = _mm_set1_epi8(1);
            __m128i hev;

            hev = _mm_subs_epu8(flat, thresh_v);
            hev = _mm_xor_si128(_mm_cmpeq_epi8(hev, zero), ff);

            __m128i ps1 = _mm_xor_si128(p1, t80);
            __m128i ps0 = _mm_xor_si128(p0, t80);
            __m128i qs0 = _mm_xor_si128(q0, t80);
            __m128i qs1 = _mm_xor_si128(q1, t80);
            __m128i filt;
            __m128i work_a;
            __m128i filter1, filter2;
            __m128i flat_p1, flat_p0, flat_q0, flat_q1;

            filt = _mm_and_si128(_mm_subs_epi8(ps1, qs1), hev);
            work_a = _mm_subs_epi8(qs0, ps0);
            filt = _mm_adds_epi8(filt, work_a);
            filt = _mm_adds_epi8(filt, work_a);
            filt = _mm_adds_epi8(filt, work_a);
            filt = _mm_and_si128(filt, mask);

            filter1 = _mm_adds_epi8(filt, t4);
            filter2 = _mm_adds_epi8(filt, t3);

            work_a = _mm_cmpgt_epi8(zero, filter1);
            filter1 = _mm_srli_epi16(filter1, 3);
            work_a = _mm_and_si128(work_a, te0);
            filter1 = _mm_and_si128(filter1, t1f);
            filter1 = _mm_or_si128(filter1, work_a);
            qs0 = _mm_xor_si128(_mm_subs_epi8(qs0, filter1), t80);

            work_a = _mm_cmpgt_epi8(zero, filter2);
            filter2 = _mm_srli_epi16(filter2, 3);
            work_a = _mm_and_si128(work_a, te0);
            filter2 = _mm_and_si128(filter2, t1f);
            filter2 = _mm_or_si128(filter2, work_a);
            ps0 = _mm_xor_si128(_mm_adds_epi8(ps0, filter2), t80);

            filt = _mm_adds_epi8(filter1, t1);
            work_a = _mm_cmpgt_epi8(zero, filt);
            filt = _mm_srli_epi16(filt, 1);
            work_a = _mm_and_si128(work_a, t80);
            filt = _mm_and_si128(filt, t7f);
            filt = _mm_or_si128(filt, work_a);
            filt = _mm_andnot_si128(hev, filt);
            ps1 = _mm_xor_si128(_mm_adds_epi8(ps1, filt), t80);
            qs1 = _mm_xor_si128(_mm_subs_epi8(qs1, filt), t80);

            __m128i work;
            work = _mm_max_epu8(
                _mm_or_si128(_mm_subs_epu8(p2, p0), _mm_subs_epu8(p0, p2)),
                _mm_or_si128(_mm_subs_epu8(q2, q0), _mm_subs_epu8(q0, q2)));
            flat = _mm_max_epu8(work, flat);
            flat = _mm_subs_epu8(flat, one);
            flat = _mm_cmpeq_epi8(flat, zero);
            flat = _mm_and_si128(flat, mask);

            if (0xffff != _mm_movemask_epi8(_mm_cmpeq_epi8(flat, zero)))
            {
                __m256i four = _mm256_set1_epi16(4);
                __m256i pixetFilter, add, res;

                // PORT-NOTE: C shuffles the lines, broadcast to both lanes, with
                // filt_loopfilter_avx2: the same 16 words as zero-extending them.
                __m256i p256_2 = _mm256_cvtepu8_epi16(p2);
                __m256i p256_1 = _mm256_cvtepu8_epi16(p1);
                __m256i p256_0 = _mm256_cvtepu8_epi16(p0);
                __m256i q256_0 = _mm256_cvtepu8_epi16(q0);
                __m256i q256_1 = _mm256_cvtepu8_epi16(q1);
                __m256i q256_2 = _mm256_cvtepu8_epi16(q2);

                pixetFilter = _mm256_slli_epi16(
                    _mm256_add_epi16(p256_2, _mm256_add_epi16(p256_1, p256_0)), 1);
                pixetFilter =
                    _mm256_add_epi16(pixetFilter, _mm256_add_epi16(p256_2, q256_0));
                pixetFilter = _mm256_add_epi16(four, pixetFilter);
                res = _mm256_srli_epi16(pixetFilter, 3);
                flat_p1 = _mm256_castsi256_si128(
                    _mm256_permute4x64_epi64(_mm256_packus_epi16(res, res), 168));
                p1 = _mm_andnot_si128(flat, ps1);
                flat_p1 = _mm_and_si128(flat, flat_p1);
                p1 = _mm_or_si128(flat_p1, p1);

                add = _mm256_add_epi16(_mm256_sub_epi16(q256_1, p256_2),
                                       _mm256_sub_epi16(q256_0, p256_2));
                pixetFilter = _mm256_add_epi16(pixetFilter, add);
                res = _mm256_srli_epi16(pixetFilter, 3);
                flat_p0 = _mm256_castsi256_si128(
                    _mm256_permute4x64_epi64(_mm256_packus_epi16(res, res), 168));
                p0 = _mm_andnot_si128(flat, ps0);
                flat_p0 = _mm_and_si128(flat, flat_p0);
                p0 = _mm_or_si128(flat_p0, p0);

                add = _mm256_add_epi16(_mm256_sub_epi16(q256_2, p256_2),
                                       _mm256_sub_epi16(q256_1, p256_1));
                pixetFilter = _mm256_add_epi16(pixetFilter, add);
                res = _mm256_srli_epi16(pixetFilter, 3);
                flat_q0 = _mm256_castsi256_si128(
                    _mm256_permute4x64_epi64(_mm256_packus_epi16(res, res), 168));
                q0 = _mm_andnot_si128(flat, qs0);
                flat_q0 = _mm_and_si128(flat, flat_q0);
                q0 = _mm_or_si128(flat_q0, q0);

                add = _mm256_add_epi16(_mm256_sub_epi16(q256_2, p256_1),
                                       _mm256_sub_epi16(q256_2, p256_0));
                pixetFilter = _mm256_add_epi16(pixetFilter, add);
                res = _mm256_srli_epi16(pixetFilter, 3);
                flat_q1 = _mm256_castsi256_si128(
                    _mm256_permute4x64_epi64(_mm256_packus_epi16(res, res), 168));
                q1 = _mm_andnot_si128(flat, qs1);
                flat_q1 = _mm_and_si128(flat, flat_q1);
                q1 = _mm_or_si128(flat_q1, q1);

            }
            else
            {
                p1 = ps1;
                p0 = ps0;
                q0 = qs0;
                q1 = qs1;
            }
        }
        p1_ref = p1;
        p0_ref = p0;
        q0_ref = q0;
        q1_ref = q1;
        return true;
    }


    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_lpf_horizontal_8_quad_avx2(Ptr<byte> s, int p,
                                                        Ptr<byte> _blimit0,
                                                        Ptr<byte> _limit0,
                                                        Ptr<byte> _thresh0)
    {
        __m256i p256_3, q256_3, p256_2, q256_2, p256_1, q256_1, p256_0, q256_0;
        __m128i p3, p2, p1, p0, q0, q1, q2, q3;
        __m128i mask, flat;

        __m128i thresh_v =
            _mm_broadcastb_epi8(_mm_cvtsi32_si128((int)_thresh0[0]));
        __m128i limit_v =
            _mm_broadcastb_epi8(_mm_cvtsi32_si128((int)_limit0[0]));
        __m128i blimit_v =
            _mm_broadcastb_epi8(_mm_cvtsi32_si128((int)_blimit0[0]));
        __m128i zero = _mm_setzero_si128();
        __m128i ff = _mm_cmpeq_epi8(zero, zero);

        p256_3 =
            _mm256_castpd_si256(_mm256_broadcast_pd(s - 4 * p));
        p256_2 =
            _mm256_castpd_si256(_mm256_broadcast_pd(s - 3 * p));
        p256_1 =
            _mm256_castpd_si256(_mm256_broadcast_pd(s - 2 * p));
        p256_0 =
            _mm256_castpd_si256(_mm256_broadcast_pd(s - 1 * p));
        q256_0 =
            _mm256_castpd_si256(_mm256_broadcast_pd(s - 0 * p));
        q256_1 =
            _mm256_castpd_si256(_mm256_broadcast_pd(s + 1 * p));
        q256_2 =
            _mm256_castpd_si256(_mm256_broadcast_pd(s + 2 * p));
        q256_3 =
            _mm256_castpd_si256(_mm256_broadcast_pd(s + 3 * p));

        p3 = _mm256_castsi256_si128(p256_3);
        p2 = _mm256_castsi256_si128(p256_2);
        p1 = _mm256_castsi256_si128(p256_1);
        p0 = _mm256_castsi256_si128(p256_0);
        q0 = _mm256_castsi256_si128(q256_0);
        q1 = _mm256_castsi256_si128(q256_1);
        q2 = _mm256_castsi256_si128(q256_2);
        q3 = _mm256_castsi256_si128(q256_3);

        {
            __m128i work;
            __m128i fe = _mm_set1_epi8(unchecked((sbyte)0xfe));
            __m128i abs_p1p0 =
                _mm_or_si128(_mm_subs_epu8(p1, p0), _mm_subs_epu8(p0, p1));
            __m128i abs_q1q0 =
                _mm_or_si128(_mm_subs_epu8(q1, q0), _mm_subs_epu8(q0, q1));
            __m128i abs_p0q0 =
                _mm_or_si128(_mm_subs_epu8(p0, q0), _mm_subs_epu8(q0, p0));
            __m128i abs_p1q1 =
                _mm_or_si128(_mm_subs_epu8(p1, q1), _mm_subs_epu8(q1, p1));

            flat = _mm_max_epu8(abs_p1p0, abs_q1q0);

            abs_p0q0 = _mm_adds_epu8(abs_p0q0, abs_p0q0);
            abs_p1q1 = _mm_srli_epi16(_mm_and_si128(abs_p1q1, fe), 1);
            mask = _mm_subs_epu8(_mm_adds_epu8(abs_p0q0, abs_p1q1), blimit_v);
            mask = _mm_xor_si128(_mm_cmpeq_epi8(mask, zero), ff);
            // mask |= (abs(p0 - q0) * 2 + abs(p1 - q1) / 2  > blimit) * -1;
            mask = _mm_max_epu8(flat, mask);
            // mask |= (abs(p1 - p0) > limit) * -1;
            // mask |= (abs(q1 - q0) > limit) * -1;
            work = _mm_max_epu8(
                _mm_or_si128(_mm_subs_epu8(p2, p1), _mm_subs_epu8(p1, p2)),
                _mm_or_si128(_mm_subs_epu8(p3, p2), _mm_subs_epu8(p2, p3)));
            mask = _mm_max_epu8(work, mask);
            work = _mm_max_epu8(
                _mm_or_si128(_mm_subs_epu8(q2, q1), _mm_subs_epu8(q1, q2)),
                _mm_or_si128(_mm_subs_epu8(q3, q2), _mm_subs_epu8(q2, q3)));
            mask = _mm_max_epu8(work, mask);
            mask = _mm_subs_epu8(mask, limit_v);
            mask = _mm_cmpeq_epi8(mask, zero);
        }

        if (0xffff == _mm_movemask_epi8(_mm_cmpeq_epi8(mask, zero))) return;

        // loop filter
        {
            __m128i t4 = _mm_set1_epi8(4);
            __m128i t3 = _mm_set1_epi8(3);
            __m128i t80 = _mm_set1_epi8(unchecked((sbyte)0x80));
            __m128i te0 = _mm_set1_epi8(unchecked((sbyte)0xe0));
            __m128i t1f = _mm_set1_epi8(0x1f);
            __m128i t1 = _mm_set1_epi8(0x1);
            __m128i t7f = _mm_set1_epi8(0x7f);
            __m128i one = _mm_set1_epi8(1);
            __m128i hev;

            hev = _mm_subs_epu8(flat, thresh_v);
            hev = _mm_xor_si128(_mm_cmpeq_epi8(hev, zero), ff);

            __m128i ps1 = _mm_xor_si128(p1, t80);
            __m128i ps0 = _mm_xor_si128(p0, t80);
            __m128i qs0 = _mm_xor_si128(q0, t80);
            __m128i qs1 = _mm_xor_si128(q1, t80);
            __m128i filt;
            __m128i work_a;
            __m128i filter1, filter2;
            __m128i flat_p2, flat_p1, flat_p0, flat_q0, flat_q1, flat_q2;

            filt = _mm_and_si128(_mm_subs_epi8(ps1, qs1), hev);
            work_a = _mm_subs_epi8(qs0, ps0);
            filt = _mm_adds_epi8(filt, work_a);
            filt = _mm_adds_epi8(filt, work_a);
            filt = _mm_adds_epi8(filt, work_a);
            filt = _mm_and_si128(filt, mask);

            filter1 = _mm_adds_epi8(filt, t4);
            filter2 = _mm_adds_epi8(filt, t3);

            work_a = _mm_cmpgt_epi8(zero, filter1);
            filter1 = _mm_srli_epi16(filter1, 3);
            work_a = _mm_and_si128(work_a, te0);
            filter1 = _mm_and_si128(filter1, t1f);
            filter1 = _mm_or_si128(filter1, work_a);
            qs0 = _mm_xor_si128(_mm_subs_epi8(qs0, filter1), t80);

            work_a = _mm_cmpgt_epi8(zero, filter2);
            filter2 = _mm_srli_epi16(filter2, 3);
            work_a = _mm_and_si128(work_a, te0);
            filter2 = _mm_and_si128(filter2, t1f);
            filter2 = _mm_or_si128(filter2, work_a);
            ps0 = _mm_xor_si128(_mm_adds_epi8(ps0, filter2), t80);

            filt = _mm_adds_epi8(filter1, t1);
            work_a = _mm_cmpgt_epi8(zero, filt);
            filt = _mm_srli_epi16(filt, 1);
            work_a = _mm_and_si128(work_a, t80);
            filt = _mm_and_si128(filt, t7f);
            filt = _mm_or_si128(filt, work_a);
            filt = _mm_andnot_si128(hev, filt);
            ps1 = _mm_xor_si128(_mm_adds_epi8(ps1, filt), t80);
            qs1 = _mm_xor_si128(_mm_subs_epi8(qs1, filt), t80);

            __m128i work;
            work = _mm_max_epu8(
                _mm_or_si128(_mm_subs_epu8(p2, p0), _mm_subs_epu8(p0, p2)),
                _mm_or_si128(_mm_subs_epu8(q2, q0), _mm_subs_epu8(q0, q2)));
            flat = _mm_max_epu8(work, flat);
            work = _mm_max_epu8(
                _mm_or_si128(_mm_subs_epu8(p3, p0), _mm_subs_epu8(p0, p3)),
                _mm_or_si128(_mm_subs_epu8(q3, q0), _mm_subs_epu8(q0, q3)));
            flat = _mm_max_epu8(work, flat);
            flat = _mm_subs_epu8(flat, one);
            flat = _mm_cmpeq_epi8(flat, zero);
            flat = _mm_and_si128(flat, mask);

            if (0xffff != _mm_movemask_epi8(_mm_cmpeq_epi8(flat, zero)))
            {
                __m256i four = _mm256_set1_epi16(4);
                __m256i pixetFilter_p2p1p0, p2p1p0, q2q1q0, pixetFilter_q2q1q0, sum_p,
                    sum_q, res_p, res_q;

                __m256i filter =
                    _mm256_load_si256(filt_loopfilter_avx2);

                p256_3 = _mm256_shuffle_epi8(p256_3, filter);
                p256_2 = _mm256_shuffle_epi8(p256_2, filter);
                p256_1 = _mm256_shuffle_epi8(p256_1, filter);
                p256_0 = _mm256_shuffle_epi8(p256_0, filter);
                q256_0 = _mm256_shuffle_epi8(q256_0, filter);
                q256_1 = _mm256_shuffle_epi8(q256_1, filter);
                q256_2 = _mm256_shuffle_epi8(q256_2, filter);
                q256_3 = _mm256_shuffle_epi8(q256_3, filter);

                p2p1p0 = _mm256_add_epi16(p256_0, _mm256_add_epi16(p256_2, p256_1));
                q2q1q0 = _mm256_add_epi16(q256_0, _mm256_add_epi16(q256_2, q256_1));

                pixetFilter_p2p1p0 =
                    _mm256_add_epi16(four, _mm256_add_epi16(p2p1p0, q2q1q0));
                pixetFilter_q2q1q0 = pixetFilter_p2p1p0;

                pixetFilter_p2p1p0 = _mm256_add_epi16(pixetFilter_p2p1p0, p256_3);
                res_p =
                    _mm256_srli_epi16(_mm256_add_epi16(pixetFilter_p2p1p0, p256_0), 3);
                flat_p0 = _mm256_castsi256_si128(
                    _mm256_permute4x64_epi64(_mm256_packus_epi16(res_p, res_p), 168));
                p0 = _mm_andnot_si128(flat, ps0);
                flat_p0 = _mm_and_si128(flat, flat_p0);
                p0 = _mm_or_si128(flat_p0, p0);

                pixetFilter_q2q1q0 = _mm256_add_epi16(pixetFilter_q2q1q0, q256_3);
                res_q =
                    _mm256_srli_epi16(_mm256_add_epi16(pixetFilter_q2q1q0, q256_0), 3);
                flat_q0 = _mm256_castsi256_si128(
                    _mm256_permute4x64_epi64(_mm256_packus_epi16(res_q, res_q), 168));
                q0 = _mm_andnot_si128(flat, qs0);
                flat_q0 = _mm_and_si128(flat, flat_q0);
                q0 = _mm_or_si128(flat_q0, q0);

                sum_p = _mm256_sub_epi16(p256_3, q256_2);
                pixetFilter_p2p1p0 = _mm256_add_epi16(pixetFilter_p2p1p0, sum_p);
                res_p =
                    _mm256_srli_epi16(_mm256_add_epi16(pixetFilter_p2p1p0, p256_1), 3);
                flat_p1 = _mm256_castsi256_si128(
                    _mm256_permute4x64_epi64(_mm256_packus_epi16(res_p, res_p), 168));
                p1 = _mm_andnot_si128(flat, ps1);
                flat_p1 = _mm_and_si128(flat, flat_p1);
                p1 = _mm_or_si128(flat_p1, p1);

                sum_q = _mm256_sub_epi16(q256_3, p256_2);
                pixetFilter_q2q1q0 = _mm256_add_epi16(pixetFilter_q2q1q0, sum_q);
                res_q =
                    _mm256_srli_epi16(_mm256_add_epi16(pixetFilter_q2q1q0, q256_1), 3);
                flat_q1 = _mm256_castsi256_si128(
                    _mm256_permute4x64_epi64(_mm256_packus_epi16(res_q, res_q), 168));
                q1 = _mm_andnot_si128(flat, qs1);
                flat_q1 = _mm_and_si128(flat, flat_q1);
                q1 = _mm_or_si128(flat_q1, q1);

                sum_p = _mm256_sub_epi16(p256_3, q256_1);
                pixetFilter_p2p1p0 = _mm256_add_epi16(pixetFilter_p2p1p0, sum_p);
                res_p =
                    _mm256_srli_epi16(_mm256_add_epi16(pixetFilter_p2p1p0, p256_2), 3);
                flat_p2 = _mm256_castsi256_si128(
                    _mm256_permute4x64_epi64(_mm256_packus_epi16(res_p, res_p), 168));
                p2 = _mm_andnot_si128(flat, p2);
                flat_p2 = _mm_and_si128(flat, flat_p2);
                p2 = _mm_or_si128(flat_p2, p2);

                sum_q = _mm256_sub_epi16(q256_3, p256_1);
                pixetFilter_q2q1q0 = _mm256_add_epi16(pixetFilter_q2q1q0, sum_q);
                res_q =
                    _mm256_srli_epi16(_mm256_add_epi16(pixetFilter_q2q1q0, q256_2), 3);
                flat_q2 = _mm256_castsi256_si128(
                    _mm256_permute4x64_epi64(_mm256_packus_epi16(res_q, res_q), 168));
                q2 = _mm_andnot_si128(flat, q2);
                flat_q2 = _mm_and_si128(flat, flat_q2);
                q2 = _mm_or_si128(flat_q2, q2);

                _mm_storeu_si128(s - 3 * p, p2);
                _mm_storeu_si128(s - 2 * p, p1);
                _mm_storeu_si128(s - 1 * p, p0);
                _mm_storeu_si128(s - 0 * p, q0);
                _mm_storeu_si128(s + 1 * p, q1);
                _mm_storeu_si128(s + 2 * p, q2);
            }
            else
            {
                _mm_storeu_si128(s - 2 * p, ps1);
                _mm_storeu_si128(s - 1 * p, ps0);
                _mm_storeu_si128(s - 0 * p, qs0);
                _mm_storeu_si128(s + 1 * p, qs1);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void trans_store_16x16_lpf_vert14(Ptr<byte> in0, int in_p,
                                                     Ptr<byte> @out, int out_p,
                                                     int is_store_avx2)
    {
        // [port] The 16 rows read and written, each as one checked window (out_p is 16, the
        // transpose buffer, whenever is_store_avx2 is set).
        Span<byte> src = in0.AsSpan(0, 15 * in_p + 16);
        Span<byte> dst = @out.AsSpan(0, 15 * out_p + 16);
        __m128i x0 = _mm_loadu_si128(src);
        __m128i x1 = _mm_loadu_si128(src.Slice(in_p * 1));
        __m128i x2 = _mm_loadu_si128(src.Slice(in_p * 2));
        __m128i x3 = _mm_loadu_si128(src.Slice(in_p * 3));
        __m128i x4 = _mm_loadu_si128(src.Slice(in_p * 4));
        __m128i x5 = _mm_loadu_si128(src.Slice(in_p * 5));
        __m128i x6 = _mm_loadu_si128(src.Slice(in_p * 6));
        __m128i x7 = _mm_loadu_si128(src.Slice(in_p * 7));

        __m256i y0 = _mm256_insertf128_si256(
            _mm256_castsi128_si256(x0), _mm_loadu_si128(src.Slice(in_p * 8)),
            0x1);
        __m256i y1 = _mm256_insertf128_si256(
            _mm256_castsi128_si256(x1), _mm_loadu_si128(src.Slice(in_p * 9)),
            0x1);
        __m256i y2 = _mm256_insertf128_si256(
            _mm256_castsi128_si256(x2), _mm_loadu_si128(src.Slice(in_p * 10)),
            0x1);
        __m256i y3 = _mm256_insertf128_si256(
            _mm256_castsi128_si256(x3), _mm_loadu_si128(src.Slice(in_p * 11)),
            0x1);
        __m256i y4 = _mm256_insertf128_si256(
            _mm256_castsi128_si256(x4), _mm_loadu_si128(src.Slice(in_p * 12)),
            0x1);
        __m256i y5 = _mm256_insertf128_si256(
            _mm256_castsi128_si256(x5), _mm_loadu_si128(src.Slice(in_p * 13)),
            0x1);
        __m256i y6 = _mm256_insertf128_si256(
            _mm256_castsi128_si256(x6), _mm_loadu_si128(src.Slice(in_p * 14)),
            0x1);
        __m256i y7 = _mm256_insertf128_si256(
            _mm256_castsi128_si256(x7), _mm_loadu_si128(src.Slice(in_p * 15)),
            0x1);

        __m256i y_s00 = _mm256_unpacklo_epi8(y0, y1);
        __m256i y_s01 = _mm256_unpackhi_epi8(y0, y1);
        __m256i y_s02 = _mm256_unpacklo_epi8(y2, y3);
        __m256i y_s03 = _mm256_unpackhi_epi8(y2, y3);
        __m256i y_s04 = _mm256_unpacklo_epi8(y4, y5);
        __m256i y_s05 = _mm256_unpackhi_epi8(y4, y5);
        __m256i y_s06 = _mm256_unpacklo_epi8(y6, y7);
        __m256i y_s07 = _mm256_unpackhi_epi8(y6, y7);

        __m256i y_s10 = _mm256_unpacklo_epi16(y_s00, y_s02);
        __m256i y_s11 = _mm256_unpackhi_epi16(y_s00, y_s02);
        __m256i y_s12 = _mm256_unpacklo_epi16(y_s01, y_s03);
        __m256i y_s13 = _mm256_unpackhi_epi16(y_s01, y_s03);
        __m256i y_s14 = _mm256_unpacklo_epi16(y_s04, y_s06);
        __m256i y_s15 = _mm256_unpackhi_epi16(y_s04, y_s06);
        __m256i y_s16 = _mm256_unpacklo_epi16(y_s05, y_s07);
        __m256i y_s17 = _mm256_unpackhi_epi16(y_s05, y_s07);

        __m256i y_s20 = _mm256_unpacklo_epi32(y_s10, y_s14);
        __m256i y_s21 = _mm256_unpackhi_epi32(y_s10, y_s14);
        __m256i y_s22 = _mm256_unpacklo_epi32(y_s11, y_s15);
        __m256i y_s23 = _mm256_unpackhi_epi32(y_s11, y_s15);
        __m256i y_s24 = _mm256_unpacklo_epi32(y_s12, y_s16);
        __m256i y_s25 = _mm256_unpackhi_epi32(y_s12, y_s16);
        __m256i y_s26 = _mm256_unpacklo_epi32(y_s13, y_s17);
        __m256i y_s27 = _mm256_unpackhi_epi32(y_s13, y_s17);

        __m256i row_s01 = _mm256_permute4x64_epi64(y_s20, 0xd8);
        __m256i row_s23 = _mm256_permute4x64_epi64(y_s21, 0xd8);
        __m256i row_s45 = _mm256_permute4x64_epi64(y_s22, 0xd8);
        __m256i row_s67 = _mm256_permute4x64_epi64(y_s23, 0xd8);
        __m256i row_s89 = _mm256_permute4x64_epi64(y_s24, 0xd8);
        __m256i row_s1011 = _mm256_permute4x64_epi64(y_s25, 0xd8);
        __m256i row_s1213 = _mm256_permute4x64_epi64(y_s26, 0xd8);
        __m256i row_s1415 = _mm256_permute4x64_epi64(y_s27, 0xd8);

        if (is_store_avx2 != 0)
        {
            _mm256_storeu_si256(dst, row_s01);
            _mm256_storeu_si256(dst.Slice(2 * out_p), row_s23);
            _mm256_storeu_si256(dst.Slice(4 * out_p), row_s45);
            _mm256_storeu_si256(dst.Slice(6 * out_p), row_s67);
            _mm256_storeu_si256(dst.Slice(8 * out_p), row_s89);
            _mm256_storeu_si256(dst.Slice(10 * out_p), row_s1011);
            _mm256_storeu_si256(dst.Slice(12 * out_p), row_s1213);
            _mm256_storeu_si256(dst.Slice(14 * out_p), row_s1415);
        }
        else
        {
            _mm_storeu_si128(dst, _mm256_castsi256_si128(row_s01));
            _mm_storeu_si128(dst.Slice(2 * out_p),
                             _mm256_castsi256_si128(row_s23));
            _mm_storeu_si128(dst.Slice(4 * out_p),
                             _mm256_castsi256_si128(row_s45));
            _mm_storeu_si128(dst.Slice(6 * out_p),
                             _mm256_castsi256_si128(row_s67));
            _mm_storeu_si128(dst.Slice(8 * out_p),
                             _mm256_castsi256_si128(row_s89));
            _mm_storeu_si128(dst.Slice(10 * out_p),
                             _mm256_castsi256_si128(row_s1011));
            _mm_storeu_si128(dst.Slice(12 * out_p),
                             _mm256_castsi256_si128(row_s1213));
            _mm_storeu_si128(dst.Slice(14 * out_p),
                             _mm256_castsi256_si128(row_s1415));
            _mm_storeu_si128(dst.Slice(1 * out_p),
                             _mm256_extracti128_si256(row_s01, 1));
            _mm_storeu_si128(dst.Slice(3 * out_p),
                             _mm256_extracti128_si256(row_s23, 1));
            _mm_storeu_si128(dst.Slice(5 * out_p),
                             _mm256_extracti128_si256(row_s45, 1));
            _mm_storeu_si128(dst.Slice(7 * out_p),
                             _mm256_extracti128_si256(row_s67, 1));
            _mm_storeu_si128(dst.Slice(9 * out_p),
                             _mm256_extracti128_si256(row_s89, 1));
            _mm_storeu_si128(dst.Slice(11 * out_p),
                             _mm256_extracti128_si256(row_s1011, 1));
            _mm_storeu_si128(dst.Slice(13 * out_p),
                             _mm256_extracti128_si256(row_s1213, 1));
            _mm_storeu_si128(dst.Slice(15 * out_p),
                             _mm256_extracti128_si256(row_s1415, 1));
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_lpf_horizontal_14_quad_avx2(Ptr<byte> s, int p,
                                                         Ptr<byte> _blimit0,
                                                         Ptr<byte> _limit0,
                                                         Ptr<byte> _thresh0)
    {
        lpf_horizontal_14_quad(s, p, _blimit0, _limit0, _thresh0);
    }

    // [av1rt] The body of aom_lpf_horizontal_14_quad_avx2. Returns whether the mask selected a
    // pixel (false: the lines are unchanged), which aom_lpf_vertical_14_quad_avx2 uses to skip
    // its transpose back.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool lpf_horizontal_14_quad(Ptr<byte> s, int p, Ptr<byte> _blimit0,
                                               Ptr<byte> _limit0, Ptr<byte> _thresh0)
    {
        __m128i mask, flat;
        __m128i zero = _mm_setzero_si128();
        __m128i ff = _mm_cmpeq_epi8(zero, zero);

        // [av1rt] C loads each line broadcast to both lanes of a 256-bit register (p256_3 ..
        // q256_3) for the flat filters. Here the lines are 128-bit loads from one window
        // (w8: lines p3..q3; w14: p6..q6 below), and the 256-bit forms are built only past the
        // early return most edges take: the lane pairs with _mm256_setr_m128i (C: blends of
        // two broadcasts) and the 16-bit lines with _mm256_cvtepu8_epi16 (C: shuffles of the
        // broadcasts with filt_loopfilter_avx2). Same values.
        Span<byte> w8 = (s - 4 * p).AsSpan(0, 7 * p + 16);
        __m128i p3 = _mm_loadu_si128(w8);
        __m128i p2 = _mm_loadu_si128(w8.Slice(p));
        __m128i p1 = _mm_loadu_si128(w8.Slice(2 * p));
        __m128i p0 = _mm_loadu_si128(w8.Slice(3 * p));
        __m128i q0 = _mm_loadu_si128(w8.Slice(4 * p));
        __m128i q1 = _mm_loadu_si128(w8.Slice(5 * p));
        __m128i q2 = _mm_loadu_si128(w8.Slice(6 * p));
        __m128i q3 = _mm_loadu_si128(w8.Slice(7 * p));

        {
            __m128i limit_v =
                _mm_broadcastb_epi8(_mm_cvtsi32_si128((int)_limit0[0]));
            __m128i blimit_v =
                _mm_broadcastb_epi8(_mm_cvtsi32_si128((int)_blimit0[0]));
            __m128i fe = _mm_set1_epi8(unchecked((sbyte)0xfe));
            __m128i abs_p1p0 =
                _mm_or_si128(_mm_subs_epu8(p1, p0), _mm_subs_epu8(p0, p1));
            __m128i abs_q1q0 =
                _mm_or_si128(_mm_subs_epu8(q1, q0), _mm_subs_epu8(q0, q1));
            __m128i abs_p0q0 =
                _mm_or_si128(_mm_subs_epu8(p0, q0), _mm_subs_epu8(q0, p0));
            __m128i abs_p1q1 =
                _mm_or_si128(_mm_subs_epu8(p1, q1), _mm_subs_epu8(q1, p1));

            flat = _mm_max_epu8(abs_p1p0, abs_q1q0);

            abs_p0q0 = _mm_adds_epu8(abs_p0q0, abs_p0q0);
            abs_p1q1 = _mm_srli_epi16(_mm_and_si128(abs_p1q1, fe), 1);
            mask = _mm_subs_epu8(_mm_adds_epu8(abs_p0q0, abs_p1q1), blimit_v);
            mask = _mm_xor_si128(_mm_cmpeq_epi8(mask, zero), ff);
            // mask |= (abs(p0 - q0) * 2 + abs(p1 - q1) / 2  > blimit) * -1;
            mask = _mm_max_epu8(flat, mask);
            // mask |= (abs(p1 - p0) > limit) * -1;
            // mask |= (abs(q1 - q0) > limit) * -1;
            __m128i work = _mm_max_epu8(
                _mm_or_si128(_mm_subs_epu8(p2, p1), _mm_subs_epu8(p1, p2)),
                _mm_or_si128(_mm_subs_epu8(p3, p2), _mm_subs_epu8(p2, p3)));
            mask = _mm_max_epu8(work, mask);
            work = _mm_max_epu8(
                _mm_or_si128(_mm_subs_epu8(q2, q1), _mm_subs_epu8(q1, q2)),
                _mm_or_si128(_mm_subs_epu8(q3, q2), _mm_subs_epu8(q2, q3)));
            mask = _mm_max_epu8(work, mask);
            mask = _mm_subs_epu8(mask, limit_v);
            mask = _mm_cmpeq_epi8(mask, zero);
        }

        if (0xffff == _mm_movemask_epi8(_mm_cmpeq_epi8(mask, zero))) return false;

        // loop filter
        {
            __m128i thresh_v =
                _mm_broadcastb_epi8(_mm_cvtsi32_si128((int)_thresh0[0]));
            __m128i one = _mm_set1_epi8(1);
            __m128i t3 = _mm_set1_epi8(3);
            __m128i t4 = _mm_add_epi8(one, t3);
            __m128i t80 = _mm_set1_epi8(unchecked((sbyte)0x80));
            __m128i te0 = _mm_set1_epi8(unchecked((sbyte)0xe0));
            __m128i t1f = _mm_set1_epi8(0x1f);
            __m128i t7f = _mm_sub_epi8(t80, one);

            __m128i hev = _mm_subs_epu8(flat, thresh_v);
            hev = _mm_xor_si128(_mm_cmpeq_epi8(hev, zero), ff);

            __m128i ps1 = _mm_xor_si128(p1, t80);
            __m128i ps0 = _mm_xor_si128(p0, t80);
            __m128i qs0 = _mm_xor_si128(q0, t80);
            __m128i qs1 = _mm_xor_si128(q1, t80);

            __m128i filt = _mm_and_si128(_mm_subs_epi8(ps1, qs1), hev);
            __m128i work_a = _mm_subs_epi8(qs0, ps0);
            filt = _mm_adds_epi8(filt, work_a);
            filt = _mm_adds_epi8(filt, work_a);
            filt = _mm_adds_epi8(filt, work_a);
            filt = _mm_and_si128(filt, mask);

            __m128i filter1 = _mm_adds_epi8(filt, t4);
            __m128i filter2 = _mm_adds_epi8(filt, t3);

            work_a = _mm_cmpgt_epi8(zero, filter1);
            filter1 = _mm_srli_epi16(filter1, 3);
            work_a = _mm_and_si128(work_a, te0);
            filter1 = _mm_and_si128(filter1, t1f);
            filter1 = _mm_or_si128(filter1, work_a);
            qs0 = _mm_xor_si128(_mm_subs_epi8(qs0, filter1), t80);

            work_a = _mm_cmpgt_epi8(zero, filter2);
            filter2 = _mm_srli_epi16(filter2, 3);
            work_a = _mm_and_si128(work_a, te0);
            filter2 = _mm_and_si128(filter2, t1f);
            filter2 = _mm_or_si128(filter2, work_a);
            ps0 = _mm_xor_si128(_mm_adds_epi8(ps0, filter2), t80);

            filt = _mm_adds_epi8(filter1, one);
            work_a = _mm_cmpgt_epi8(zero, filt);
            filt = _mm_srli_epi16(filt, 1);
            work_a = _mm_and_si128(work_a, t80);
            filt = _mm_and_si128(filt, t7f);
            filt = _mm_or_si128(filt, work_a);
            filt = _mm_andnot_si128(hev, filt);
            ps1 = _mm_xor_si128(_mm_adds_epi8(ps1, filt), t80);
            qs1 = _mm_xor_si128(_mm_subs_epi8(qs1, filt), t80);

            // Derive flat
            __m256i p0q0256 = _mm256_setr_m128i(p0, q0);
            __m256i p2q2256 = _mm256_setr_m128i(p2, q2);
            __m256i p3q3256 = _mm256_setr_m128i(p3, q3);
            __m256i ps0qs0256 =
                _mm256_insertf128_si256(_mm256_castsi128_si256(ps0), qs0, 0x1);
            __m256i ps1qs1256 =
                _mm256_insertf128_si256(_mm256_castsi128_si256(ps1), qs1, 0x1);
            __m256i work01 = _mm256_or_si256(_mm256_subs_epu8(p2q2256, p0q0256),
                                             _mm256_subs_epu8(p0q0256, p2q2256));
            __m256i work02 = _mm256_or_si256(_mm256_subs_epu8(p3q3256, p0q0256),
                                             _mm256_subs_epu8(p0q0256, p3q3256));
            __m256i max0_256 = _mm256_max_epu8(work01, work02);
            __m128i max1_256 =
                _mm_max_epu8(_mm256_castsi256_si128(max0_256),
                             _mm256_extractf128_si256(max0_256, 1));
            flat = _mm_max_epu8(max1_256, flat);
            flat = _mm_subs_epu8(flat, one);
            flat = _mm_cmpeq_epi8(flat, zero);
            flat = _mm_and_si128(flat, mask);

            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            // flat and wide flat calculations
            if (0xffff != _mm_movemask_epi8(_mm_cmpeq_epi8(flat, zero)))
            {
                __m256i flat256 =
                    _mm256_insertf128_si256(_mm256_castsi128_si256(flat), flat, 0x1);
                __m256i eight = _mm256_set1_epi16(8);
                __m256i four = _mm256_set1_epi16(4);

                Span<byte> w14 = (s - 7 * p).AsSpan(0, 13 * p + 16);
                __m128i p6 = _mm_loadu_si128(w14);
                __m128i p5 = _mm_loadu_si128(w14.Slice(p));
                __m128i p4 = _mm_loadu_si128(w14.Slice(2 * p));
                __m128i q4 = _mm_loadu_si128(w14.Slice(11 * p));
                __m128i q5 = _mm_loadu_si128(w14.Slice(12 * p));
                __m128i q6 = _mm_loadu_si128(w14.Slice(13 * p));

                // Derive flat2
                __m256i p4q4256 = _mm256_setr_m128i(p4, q4);
                __m256i p5q5256 = _mm256_setr_m128i(p5, q5);
                __m256i p6q6256 = _mm256_setr_m128i(p6, q6);
                __m256i work1 = _mm256_or_si256(_mm256_subs_epu8(p4q4256, p0q0256),
                                                _mm256_subs_epu8(p0q0256, p4q4256));
                __m256i work2 = _mm256_or_si256(_mm256_subs_epu8(p5q5256, p0q0256),
                                                _mm256_subs_epu8(p0q0256, p5q5256));
                __m256i work3 = _mm256_or_si256(_mm256_subs_epu8(p6q6256, p0q0256),
                                                _mm256_subs_epu8(p0q0256, p6q6256));
                __m256i flat2_256 = _mm256_max_epu8(work1, work2);
                flat2_256 = _mm256_max_epu8(flat2_256, work3);
                __m128i flat2 = _mm_max_epu8(_mm256_castsi256_si128(flat2_256),
                                             _mm256_extractf128_si256(flat2_256, 1));
                flat2 = _mm_subs_epu8(flat2, one);
                flat2 = _mm_cmpeq_epi8(flat2, zero);
                flat2 = _mm_and_si128(flat2, flat);  // flat2 & flat & mask

                __m256i p256_3 = _mm256_cvtepu8_epi16(p3);
                __m256i p256_2 = _mm256_cvtepu8_epi16(p2);
                __m256i p256_1 = _mm256_cvtepu8_epi16(p1);
                __m256i p256_0 = _mm256_cvtepu8_epi16(p0);
                __m256i q256_0 = _mm256_cvtepu8_epi16(q0);
                __m256i q256_1 = _mm256_cvtepu8_epi16(q1);
                __m256i q256_2 = _mm256_cvtepu8_epi16(q2);
                __m256i q256_3 = _mm256_cvtepu8_epi16(q3);

                __m256i p2p1p0 =
                    _mm256_add_epi16(p256_0, _mm256_add_epi16(p256_2, p256_1));
                __m256i q2q1q0 =
                    _mm256_add_epi16(q256_0, _mm256_add_epi16(q256_2, q256_1));

                __m256i pixetFilter_p2p1p0 =
                    _mm256_add_epi16(four, _mm256_add_epi16(p2p1p0, q2q1q0));
                __m256i pixetFilter_q2q1q0 = pixetFilter_p2p1p0;

                // Derive p0 and q0
                pixetFilter_p2p1p0 = _mm256_add_epi16(pixetFilter_p2p1p0, p256_3);
                __m256i res_p =
                    _mm256_srli_epi16(_mm256_add_epi16(pixetFilter_p2p1p0, p256_0), 3);
                pixetFilter_q2q1q0 = _mm256_add_epi16(pixetFilter_q2q1q0, q256_3);
                __m256i res_q =
                    _mm256_srli_epi16(_mm256_add_epi16(pixetFilter_q2q1q0, q256_0), 3);
                __m256i flat_p0q0 =
                    _mm256_permute4x64_epi64(_mm256_packus_epi16(res_p, res_q), 0xd8);
                p0q0256 = _mm256_andnot_si256(flat256, ps0qs0256);
                flat_p0q0 = _mm256_and_si256(flat256, flat_p0q0);
                p0q0256 = _mm256_or_si256(flat_p0q0, p0q0256);
                p0 = _mm256_castsi256_si128(p0q0256);
                q0 = _mm256_extractf128_si256(p0q0256, 1);

                // Derive p1 and q1
                __m256i sum_p = _mm256_sub_epi16(p256_3, q256_2);
                pixetFilter_p2p1p0 = _mm256_add_epi16(pixetFilter_p2p1p0, sum_p);
                res_p =
                    _mm256_srli_epi16(_mm256_add_epi16(pixetFilter_p2p1p0, p256_1), 3);
                __m256i sum_q = _mm256_sub_epi16(q256_3, p256_2);
                pixetFilter_q2q1q0 = _mm256_add_epi16(pixetFilter_q2q1q0, sum_q);
                res_q =
                    _mm256_srli_epi16(_mm256_add_epi16(pixetFilter_q2q1q0, q256_1), 3);
                __m256i flat_p1q1 =
                    _mm256_permute4x64_epi64(_mm256_packus_epi16(res_p, res_q), 0xd8);
                __m256i p1q1256 = _mm256_andnot_si256(flat256, ps1qs1256);
                flat_p1q1 = _mm256_and_si256(flat256, flat_p1q1);
                p1q1256 = _mm256_or_si256(flat_p1q1, p1q1256);
                p1 = _mm256_castsi256_si128(p1q1256);
                q1 = _mm256_extractf128_si256(p1q1256, 1);

                // Derive p2 and q2
                sum_p = _mm256_sub_epi16(p256_3, q256_1);
                pixetFilter_p2p1p0 = _mm256_add_epi16(pixetFilter_p2p1p0, sum_p);
                res_p =
                    _mm256_srli_epi16(_mm256_add_epi16(pixetFilter_p2p1p0, p256_2), 3);
                sum_q = _mm256_sub_epi16(q256_3, p256_1);
                pixetFilter_q2q1q0 = _mm256_add_epi16(pixetFilter_q2q1q0, sum_q);
                res_q =
                    _mm256_srli_epi16(_mm256_add_epi16(pixetFilter_q2q1q0, q256_2), 3);
                __m256i flat_p2q2 =
                    _mm256_permute4x64_epi64(_mm256_packus_epi16(res_p, res_q), 0xd8);
                p2q2256 = _mm256_andnot_si256(flat256, p2q2256);
                flat_p2q2 = _mm256_and_si256(flat256, flat_p2q2);
                p2q2256 = _mm256_or_si256(flat_p2q2, p2q2256);
                p2 = _mm256_castsi256_si128(p2q2256);
                q2 = _mm256_extractf128_si256(p2q2256, 1);
                if (0xffff != _mm_movemask_epi8(_mm_cmpeq_epi8(flat2, zero)))
                {
                    flat2_256 =
                        _mm256_insertf128_si256(_mm256_castsi128_si256(flat2), flat2, 0x1);
                    __m256i p256_6 = _mm256_cvtepu8_epi16(p6);
                    __m256i p256_5 = _mm256_cvtepu8_epi16(p5);
                    __m256i p256_4 = _mm256_cvtepu8_epi16(p4);
                    __m256i q256_4 = _mm256_cvtepu8_epi16(q4);
                    __m256i q256_5 = _mm256_cvtepu8_epi16(q5);
                    __m256i q256_6 = _mm256_cvtepu8_epi16(q6);

                    __m256i pixelFilter_p =
                        _mm256_add_epi16(p256_5, _mm256_add_epi16(p256_4, p256_3));
                    __m256i pixelFilter_q =
                        _mm256_add_epi16(q256_5, _mm256_add_epi16(q256_4, q256_3));

                    pixelFilter_p = _mm256_add_epi16(pixelFilter_p, p2p1p0);
                    pixelFilter_q = _mm256_add_epi16(pixelFilter_q, q2q1q0);

                    pixelFilter_p = _mm256_add_epi16(pixelFilter_p, p256_0);
                    pixelFilter_q = _mm256_add_epi16(pixelFilter_q, q256_0);
                    pixelFilter_p = _mm256_add_epi16(
                        eight, _mm256_add_epi16(pixelFilter_p, pixelFilter_q));
                    pixelFilter_q = pixelFilter_p;

                    // Derive p0 and q0
                    pixelFilter_p =
                        _mm256_add_epi16(_mm256_add_epi16(p256_6, p256_1), pixelFilter_p);
                    res_p = _mm256_srli_epi16(pixelFilter_p, 4);
                    pixelFilter_q =
                        _mm256_add_epi16(_mm256_add_epi16(q256_6, q256_1), pixelFilter_q);
                    res_q = _mm256_srli_epi16(pixelFilter_q, 4);
                    __m256i flat2_p0q0 =
                        _mm256_permute4x64_epi64(_mm256_packus_epi16(res_p, res_q), 0xd8);
                    p0q0256 = _mm256_andnot_si256(flat2_256, p0q0256);
                    flat2_p0q0 = _mm256_and_si256(flat2_256, flat2_p0q0);
                    p0q0256 = _mm256_or_si256(flat2_p0q0, p0q0256);

                    p0 = _mm256_castsi256_si128(p0q0256);
                    q0 = _mm256_extractf128_si256(p0q0256, 1);
                    _mm_storeu_si128(w8.Slice(3 * p), p0);
                    _mm_storeu_si128(w8.Slice(4 * p), q0);

                    // Derive p1 and q1
                    sum_p = _mm256_add_epi16(_mm256_sub_epi16(p256_6, q256_5),
                                             _mm256_sub_epi16(p256_2, q256_0));
                    pixelFilter_p = _mm256_add_epi16(pixelFilter_p, sum_p);
                    res_p = _mm256_srli_epi16(pixelFilter_p, 4);
                    sum_q = _mm256_add_epi16(_mm256_sub_epi16(q256_6, p256_5),
                                             _mm256_sub_epi16(q256_2, p256_0));
                    pixelFilter_q = _mm256_add_epi16(pixelFilter_q, sum_q);
                    res_q = _mm256_srli_epi16(pixelFilter_q, 4);
                    __m256i flat2_p1q1 =
                        _mm256_permute4x64_epi64(_mm256_packus_epi16(res_p, res_q), 0xd8);
                    p1q1256 = _mm256_andnot_si256(flat2_256, p1q1256);
                    flat2_p1q1 = _mm256_and_si256(flat2_256, flat2_p1q1);
                    p1q1256 = _mm256_or_si256(flat2_p1q1, p1q1256);
                    p1 = _mm256_castsi256_si128(p1q1256);
                    q1 = _mm256_extractf128_si256(p1q1256, 1);
                    _mm_storeu_si128(w8.Slice(2 * p), p1);
                    _mm_storeu_si128(w8.Slice(5 * p), q1);

                    // Derive p2 and q2
                    sum_p = _mm256_add_epi16(_mm256_sub_epi16(p256_6, q256_4),
                                             _mm256_sub_epi16(p256_3, p256_0));
                    pixelFilter_p = _mm256_add_epi16(pixelFilter_p, sum_p);
                    res_p = _mm256_srli_epi16(pixelFilter_p, 4);
                    sum_q = _mm256_add_epi16(_mm256_sub_epi16(q256_6, p256_4),
                                             _mm256_sub_epi16(q256_3, q256_0));
                    pixelFilter_q = _mm256_add_epi16(pixelFilter_q, sum_q);
                    res_q = _mm256_srli_epi16(pixelFilter_q, 4);
                    __m256i flat2_p2q2 =
                        _mm256_permute4x64_epi64(_mm256_packus_epi16(res_p, res_q), 0xd8);
                    p2q2256 = _mm256_andnot_si256(flat2_256, p2q2256);
                    flat2_p2q2 = _mm256_and_si256(flat2_256, flat2_p2q2);
                    p2q2256 = _mm256_or_si256(flat2_p2q2, p2q2256);
                    p2 = _mm256_castsi256_si128(p2q2256);
                    q2 = _mm256_extractf128_si256(p2q2256, 1);
                    _mm_storeu_si128(w8.Slice(1 * p), p2);
                    _mm_storeu_si128(w8.Slice(6 * p), q2);

                    // Derive p3 and q3
                    sum_p = _mm256_add_epi16(_mm256_sub_epi16(p256_6, q256_3),
                                             _mm256_sub_epi16(p256_4, p256_1));
                    pixelFilter_p = _mm256_add_epi16(pixelFilter_p, sum_p);
                    res_p = _mm256_srli_epi16(pixelFilter_p, 4);
                    sum_q = _mm256_add_epi16(_mm256_sub_epi16(q256_6, p256_3),
                                             _mm256_sub_epi16(q256_4, q256_1));
                    pixelFilter_q = _mm256_add_epi16(pixelFilter_q, sum_q);
                    res_q = _mm256_srli_epi16(pixelFilter_q, 4);
                    __m256i flat2_p3q3 =
                        _mm256_permute4x64_epi64(_mm256_packus_epi16(res_p, res_q), 0xd8);
                    p3q3256 = _mm256_andnot_si256(flat2_256, p3q3256);
                    flat2_p3q3 = _mm256_and_si256(flat2_256, flat2_p3q3);
                    p3q3256 = _mm256_or_si256(flat2_p3q3, p3q3256);
                    p3 = _mm256_castsi256_si128(p3q3256);
                    q3 = _mm256_extractf128_si256(p3q3256, 1);
                    _mm_storeu_si128(w8, p3);
                    _mm_storeu_si128(w8.Slice(7 * p), q3);

                    // Derive p4 and q4
                    sum_p = _mm256_add_epi16(_mm256_sub_epi16(p256_6, q256_2),
                                             _mm256_sub_epi16(p256_5, p256_2));
                    pixelFilter_p = _mm256_add_epi16(pixelFilter_p, sum_p);
                    res_p = _mm256_srli_epi16(pixelFilter_p, 4);
                    sum_q = _mm256_add_epi16(_mm256_sub_epi16(q256_6, p256_2),
                                             _mm256_sub_epi16(q256_5, q256_2));
                    pixelFilter_q = _mm256_add_epi16(pixelFilter_q, sum_q);
                    res_q = _mm256_srli_epi16(pixelFilter_q, 4);
                    __m256i flat2_p4q4 =
                        _mm256_permute4x64_epi64(_mm256_packus_epi16(res_p, res_q), 0xd8);
                    p4q4256 = _mm256_andnot_si256(flat2_256, p4q4256);
                    flat2_p4q4 = _mm256_and_si256(flat2_256, flat2_p4q4);
                    p4q4256 = _mm256_or_si256(flat2_p4q4, p4q4256);
                    _mm_storeu_si128(w14.Slice(2 * p),
                                     _mm256_castsi256_si128(p4q4256));
                    _mm_storeu_si128(w14.Slice(11 * p),
                                     _mm256_extractf128_si256(p4q4256, 1));

                    // Derive p5 and q5
                    sum_p = _mm256_add_epi16(_mm256_sub_epi16(p256_6, q256_1),
                                             _mm256_sub_epi16(p256_6, p256_3));
                    pixelFilter_p = _mm256_add_epi16(pixelFilter_p, sum_p);
                    res_p = _mm256_srli_epi16(pixelFilter_p, 4);
                    sum_q = _mm256_add_epi16(_mm256_sub_epi16(q256_6, p256_1),
                                             _mm256_sub_epi16(q256_6, q256_3));
                    pixelFilter_q = _mm256_add_epi16(pixelFilter_q, sum_q);
                    res_q = _mm256_srli_epi16(pixelFilter_q, 4);
                    __m256i flat2_p5q5 =
                        _mm256_permute4x64_epi64(_mm256_packus_epi16(res_p, res_q), 0xd8);
                    p5q5256 = _mm256_andnot_si256(flat2_256, p5q5256);
                    flat2_p5q5 = _mm256_and_si256(flat2_256, flat2_p5q5);
                    p5q5256 = _mm256_or_si256(flat2_p5q5, p5q5256);
                    _mm_storeu_si128(w14.Slice(1 * p),
                                     _mm256_castsi256_si128(p5q5256));
                    _mm_storeu_si128(w14.Slice(12 * p),
                                     _mm256_extractf128_si256(p5q5256, 1));
                }
                else
                {
                    _mm_storeu_si128(w8.Slice(1 * p), p2);
                    _mm_storeu_si128(w8.Slice(2 * p), p1);
                    _mm_storeu_si128(w8.Slice(3 * p), p0);
                    _mm_storeu_si128(w8.Slice(4 * p), q0);
                    _mm_storeu_si128(w8.Slice(5 * p), q1);
                    _mm_storeu_si128(w8.Slice(6 * p), q2);
                }
            }
            else
            {
                _mm_storeu_si128(w8.Slice(2 * p), ps1);
                _mm_storeu_si128(w8.Slice(3 * p), ps0);
                _mm_storeu_si128(w8.Slice(4 * p), qs0);
                _mm_storeu_si128(w8.Slice(5 * p), qs1);
            }
        }
        return true;
    }

    // [port] DECLARE_ALIGNED(16, unsigned char, t_dst[256]): per-thread scratch (leaf use).
    [ThreadStatic] private static byte[] s_t_dst;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_lpf_vertical_14_quad_avx2(Ptr<byte> s, int pitch,
                                                       Ptr<byte> _blimit0,
                                                       Ptr<byte> _limit0,
                                                       Ptr<byte> _thresh0)
    {
        Ptr<byte> t_dst = s_t_dst ??= new byte[256];

        // Transpose 16x16
        trans_store_16x16_lpf_vert14(s - 8, pitch, t_dst, 16, 1);

        // Loop filtering
        // [av1rt] Transposed back only when the filter changed a pixel (C always does; the
        // unchanged buffer transposes back to the same bytes).
        if (!lpf_horizontal_14_quad(t_dst + 8 * 16, 16, _blimit0, _limit0, _thresh0))
            return;

        // Transpose back
        trans_store_16x16_lpf_vert14(t_dst, 16, s - 8, pitch, 0);
    }
}
