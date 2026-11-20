// Port of libaom aom_dsp/x86/aom_convolve_copy_avx2.c
// [subset] highbd_copy_64, highbd_copy_128 and aom_highbd_convolve_copy_avx2: high bitdepth.

global using static Av1rt.Aom.aom_convolve_copy_avx2_h;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] Class gets `_h`: the stem equals the function aom_convolve_copy_avx2.
internal static partial class aom_convolve_copy_avx2_h
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void copy_128(Ptr<byte> src, Ptr<byte> dst)
    {
        Arr4<__m256i> s = default;
        s[0] = _mm256_loadu_si256((src + 0 * 32));
        s[1] = _mm256_loadu_si256((src + 1 * 32));
        s[2] = _mm256_loadu_si256((src + 2 * 32));
        s[3] = _mm256_loadu_si256((src + 3 * 32));
        _mm256_storeu_si256((dst + 0 * 32), s[0]);
        _mm256_storeu_si256((dst + 1 * 32), s[1]);
        _mm256_storeu_si256((dst + 2 * 32), s[2]);
        _mm256_storeu_si256((dst + 3 * 32), s[3]);
    }

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
        else if (w == 8)
        {
            do
            {
                Arr2<__m128i> s = default;
                s[0] = _mm_loadl_epi64(src);
                src += src_stride;
                s[1] = _mm_loadl_epi64(src);
                src += src_stride;
                _mm_storel_epi64(dst, s[0]);
                dst += dst_stride;
                _mm_storel_epi64(dst, s[1]);
                dst += dst_stride;
                h -= 2;
            } while (h != 0);
        }
        else if (w == 16)
        {
            do
            {
                Arr2<__m128i> s = default;
                s[0] = _mm_loadu_si128(src);
                src += src_stride;
                s[1] = _mm_loadu_si128(src);
                src += src_stride;
                _mm_store_si128(dst, s[0]);
                dst += dst_stride;
                _mm_store_si128(dst, s[1]);
                dst += dst_stride;
                h -= 2;
            } while (h != 0);
        }
        else if (w == 32)
        {
            do
            {
                Arr2<__m256i> s = default;
                s[0] = _mm256_loadu_si256(src);
                src += src_stride;
                s[1] = _mm256_loadu_si256(src);
                src += src_stride;
                _mm256_storeu_si256(dst, s[0]);
                dst += dst_stride;
                _mm256_storeu_si256(dst, s[1]);
                dst += dst_stride;
                h -= 2;
            } while (h != 0);
        }
        else if (w == 64)
        {
            do
            {
                Arr4<__m256i> s = default;
                s[0] = _mm256_loadu_si256((src + 0 * 32));
                s[1] = _mm256_loadu_si256((src + 1 * 32));
                src += src_stride;
                s[2] = _mm256_loadu_si256((src + 0 * 32));
                s[3] = _mm256_loadu_si256((src + 1 * 32));
                src += src_stride;
                _mm256_storeu_si256((dst + 0 * 32), s[0]);
                _mm256_storeu_si256((dst + 1 * 32), s[1]);
                dst += dst_stride;
                _mm256_storeu_si256((dst + 0 * 32), s[2]);
                _mm256_storeu_si256((dst + 1 * 32), s[3]);
                dst += dst_stride;
                h -= 2;
            } while (h != 0);
        }
        else
        {
            do
            {
                copy_128(src, dst);
                src += src_stride;
                dst += dst_stride;
                copy_128(src, dst);
                src += src_stride;
                dst += dst_stride;
                h -= 2;
            } while (h != 0);
        }
    }
}
