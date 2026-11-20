// Port of libaom aom_dsp/x86/aom_convolve_copy_avx2.c
// [subset] highbd_copy_64, highbd_copy_128 and aom_highbd_convolve_copy_avx2: high bitdepth.

global using static Av1rt.Aom.aom_convolve_copy_avx2_h;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] Class gets `_h`: the stem equals the function aom_convolve_copy_avx2.
internal static partial class aom_convolve_copy_avx2_h
{
    // [port] copy_128 is inlined into the 128-wide case below.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_convolve_copy_avx2(Ptr<byte> src, int src_stride,
                                                Ptr<byte> dst, int dst_stride, int w, int h)
    {
        // The w == 16 case uses _mm_store_si128(), which requires its output address
        // be aligned on a 16-byte boundary.
        // [port] The alignment asserts are dropped: _mm_store_si128 is the unaligned store here
        // and managed arrays have no address to check.

        if (w == 2)
        {
            do
            {
                memcpy(dst, src, 2 * sizeof(byte));
                src += src_stride;
                dst += dst_stride;
                memcpy(dst, src, 2 * sizeof(byte));
                src += src_stride;
                dst += dst_stride;
                h -= 2;
            } while (h != 0);
        }
        else if (w == 4)
        {
            do
            {
                memcpy(dst, src, 4 * sizeof(byte));
                src += src_stride;
                dst += dst_stride;
                memcpy(dst, src, 4 * sizeof(byte));
                src += src_stride;
                dst += dst_stride;
                h -= 2;
            } while (h != 0);
        }
        else
        {
            // [port] From 8 columns on, the rows are copied through spans hoisted out of the
            // loop and sliced once per row, so the vector loads and stores inside need no
            // further bounds checks (through Ptr each one is checked). Same copies as C, one
            // row at a time instead of two.
            ReadOnlySpan<byte> sbuf = src.Array;
            Span<byte> dbuf = dst.Array;
            int so = src.Offset, d = dst.Offset;
            if (w == 8)
            {
                do
                {
                    _mm_storel_epi64(dbuf.Slice(d, 8), _mm_loadl_epi64(sbuf.Slice(so, 8)));
                    so += src_stride;
                    d += dst_stride;
                } while (--h != 0);
            }
            else if (w == 16)
            {
                do
                {
                    _mm_storeu_si128(dbuf.Slice(d, 16), _mm_loadu_si128(sbuf.Slice(so, 16)));
                    so += src_stride;
                    d += dst_stride;
                } while (--h != 0);
            }
            else if (w == 32)
            {
                do
                {
                    _mm256_storeu_si256(dbuf.Slice(d, 32), _mm256_loadu_si256(sbuf.Slice(so, 32)));
                    so += src_stride;
                    d += dst_stride;
                } while (--h != 0);
            }
            else if (w == 64)
            {
                do
                {
                    ReadOnlySpan<byte> sr = sbuf.Slice(so, 64);
                    Span<byte> dr = dbuf.Slice(d, 64);
                    __m256i s0 = _mm256_loadu_si256(sr);
                    __m256i s1 = _mm256_loadu_si256(sr.Slice(32));
                    _mm256_storeu_si256(dr, s0);
                    _mm256_storeu_si256(dr.Slice(32), s1);
                    so += src_stride;
                    d += dst_stride;
                } while (--h != 0);
            }
            else
            {
                do
                {
                    // copy_128
                    ReadOnlySpan<byte> sr = sbuf.Slice(so, 128);
                    Span<byte> dr = dbuf.Slice(d, 128);
                    __m256i s0 = _mm256_loadu_si256(sr);
                    __m256i s1 = _mm256_loadu_si256(sr.Slice(32));
                    __m256i s2 = _mm256_loadu_si256(sr.Slice(64));
                    __m256i s3 = _mm256_loadu_si256(sr.Slice(96));
                    _mm256_storeu_si256(dr, s0);
                    _mm256_storeu_si256(dr.Slice(32), s1);
                    _mm256_storeu_si256(dr.Slice(64), s2);
                    _mm256_storeu_si256(dr.Slice(96), s3);
                    so += src_stride;
                    d += dst_stride;
                } while (--h != 0);
            }
        }
    }
}
