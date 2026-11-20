// Port of libaom aom_dsp/sad.c

global using static Av1rt.Aom.sad_h;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] The class is named sad_h because sad.c defines a function named sad (PORTING 2).
// The SADMXN / SADMXN_AVG / SADMXN_SKIP / SAD_MXNX4D / SAD_MXNX3D instantiations are expanded
// by tools/gen/dsp_sizes.py for the sizes sad.c compiles with CONFIG_REALTIME_ONLY (the 4:1 and
// 1:4 sizes are !CONFIG_REALTIME_ONLY). Every _c function is followed by its aom_dsp_rtcd.h
// dispatched name as a forwarder. Besides the variants the subset executes, this includes every
// function the encoder's fn_ptr table (encoder.c: BFP / SDSFP) points at.
// [subset] CONFIG_AV1_HIGHBITDEPTH == 0: the highbd_* SAD functions are not ported.
internal static partial class sad_h
{
    /* Sum the difference between every corresponding element of the buffers. */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint sad(Ptr<byte> a, int a_stride, Ptr<byte> b,
                            int b_stride, int width, int height)
    {
        int y, x;
        uint sad = 0;

        for (y = 0; y < height; y++)
        {
            for (x = 0; x < width; x++)
            {
                sad += (uint)Math.Abs(a[x] - b[x]);
            }

            a += a_stride;
            b += b_stride;
        }
        return sad;
    }

    // [port] `uint8_t comp_pred[m * n]` of SADMXN_AVG: a per-thread scratch buffer of the largest
    // size (PORTING 4); the function is a leaf and aom_comp_avg_pred does not re-enter it.
    [ThreadStatic] private static byte[] s_comp_pred;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] comp_pred_buf() => s_comp_pred ??= new byte[MAX_SB_SIZE * MAX_SB_SIZE];

    // <gen:sad_sizes>
    // 128x128
    internal static uint aom_sad128x128_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 128, 128);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad128x128(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad128x128_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad128x128_c(src_ptr, src_stride, ref_ptr, ref_stride);

    internal static uint aom_sad128x128_avg_c(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred)
    {
        Ptr<byte> comp_pred = comp_pred_buf();  // uint8_t comp_pred[m * n]
        aom_comp_avg_pred(comp_pred, second_pred, 128, 128, @ref, ref_stride);
        return sad(src, src_stride, comp_pred, 128, 128, 128);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad128x128_avg(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                         int ref_stride, Ptr<byte> second_pred) =>
        Simd.UseAvx2 ? aom_sad128x128_avg_avx2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred)
                     : aom_sad128x128_avg_c(src_ptr, src_stride, ref_ptr, ref_stride, second_pred);

    internal static uint aom_sad_skip_128x128_c(Ptr<byte> src, int src_stride,
                                             Ptr<byte> @ref,
                                             int ref_stride)
    {
        return 2 * sad(src, 2 * src_stride, @ref, 2 * ref_stride, (128), (128 / 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad_skip_128x128(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                           int ref_stride) =>
        Simd.UseAvx2 ? aom_sad_skip_128x128_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad_skip_128x128_c(src_ptr, src_stride, ref_ptr, ref_stride);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad128x128x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad128x128_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad128x128x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad128x128x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad128x128x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    internal static void aom_sad_skip_128x128x4d_c(Ptr<byte> src, int src_stride,
                                                Ptr<Ptr<byte>> ref_array,
                                                int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] = 2 * sad(src, 2 * src_stride, ref_array[i],
                                   2 * ref_stride, (128), (128 / 2));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad_skip_128x128x4d(Ptr<byte> src_ptr, int src_stride,
                                              Ptr<Ptr<byte>> ref_ptr, int ref_stride,
                                              Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad_skip_128x128x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad_skip_128x128x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad128x128x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad128x128x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad128x128x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad128x128x3d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad128x128x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // 128x64
    internal static uint aom_sad128x64_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 128, 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad128x64(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad128x64_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad128x64_c(src_ptr, src_stride, ref_ptr, ref_stride);

    internal static uint aom_sad128x64_avg_c(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred)
    {
        Ptr<byte> comp_pred = comp_pred_buf();  // uint8_t comp_pred[m * n]
        aom_comp_avg_pred(comp_pred, second_pred, 128, 64, @ref, ref_stride);
        return sad(src, src_stride, comp_pred, 128, 128, 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad128x64_avg(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                         int ref_stride, Ptr<byte> second_pred) =>
        Simd.UseAvx2 ? aom_sad128x64_avg_avx2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred)
                     : aom_sad128x64_avg_c(src_ptr, src_stride, ref_ptr, ref_stride, second_pred);

    internal static uint aom_sad_skip_128x64_c(Ptr<byte> src, int src_stride,
                                             Ptr<byte> @ref,
                                             int ref_stride)
    {
        return 2 * sad(src, 2 * src_stride, @ref, 2 * ref_stride, (128), (64 / 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad_skip_128x64(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                           int ref_stride) =>
        Simd.UseAvx2 ? aom_sad_skip_128x64_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad_skip_128x64_c(src_ptr, src_stride, ref_ptr, ref_stride);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad128x64x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad128x64_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad128x64x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad128x64x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad128x64x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    internal static void aom_sad_skip_128x64x4d_c(Ptr<byte> src, int src_stride,
                                                Ptr<Ptr<byte>> ref_array,
                                                int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] = 2 * sad(src, 2 * src_stride, ref_array[i],
                                   2 * ref_stride, (128), (64 / 2));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad_skip_128x64x4d(Ptr<byte> src_ptr, int src_stride,
                                              Ptr<Ptr<byte>> ref_ptr, int ref_stride,
                                              Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad_skip_128x64x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad_skip_128x64x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad128x64x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad128x64x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad128x64x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad128x64x3d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad128x64x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // 64x128
    internal static uint aom_sad64x128_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 64, 128);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad64x128(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad64x128_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad64x128_c(src_ptr, src_stride, ref_ptr, ref_stride);

    internal static uint aom_sad64x128_avg_c(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred)
    {
        Ptr<byte> comp_pred = comp_pred_buf();  // uint8_t comp_pred[m * n]
        aom_comp_avg_pred(comp_pred, second_pred, 64, 128, @ref, ref_stride);
        return sad(src, src_stride, comp_pred, 64, 64, 128);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad64x128_avg(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                         int ref_stride, Ptr<byte> second_pred) =>
        Simd.UseAvx2 ? aom_sad64x128_avg_avx2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred)
                     : aom_sad64x128_avg_c(src_ptr, src_stride, ref_ptr, ref_stride, second_pred);

    internal static uint aom_sad_skip_64x128_c(Ptr<byte> src, int src_stride,
                                             Ptr<byte> @ref,
                                             int ref_stride)
    {
        return 2 * sad(src, 2 * src_stride, @ref, 2 * ref_stride, (64), (128 / 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad_skip_64x128(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                           int ref_stride) =>
        Simd.UseAvx2 ? aom_sad_skip_64x128_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad_skip_64x128_c(src_ptr, src_stride, ref_ptr, ref_stride);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad64x128x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad64x128_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad64x128x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad64x128x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad64x128x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    internal static void aom_sad_skip_64x128x4d_c(Ptr<byte> src, int src_stride,
                                                Ptr<Ptr<byte>> ref_array,
                                                int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] = 2 * sad(src, 2 * src_stride, ref_array[i],
                                   2 * ref_stride, (64), (128 / 2));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad_skip_64x128x4d(Ptr<byte> src_ptr, int src_stride,
                                              Ptr<Ptr<byte>> ref_ptr, int ref_stride,
                                              Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad_skip_64x128x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad_skip_64x128x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad64x128x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad64x128x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad64x128x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad64x128x3d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad64x128x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // 64x64
    internal static uint aom_sad64x64_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 64, 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad64x64(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad64x64_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad64x64_c(src_ptr, src_stride, ref_ptr, ref_stride);

    internal static uint aom_sad64x64_avg_c(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred)
    {
        Ptr<byte> comp_pred = comp_pred_buf();  // uint8_t comp_pred[m * n]
        aom_comp_avg_pred(comp_pred, second_pred, 64, 64, @ref, ref_stride);
        return sad(src, src_stride, comp_pred, 64, 64, 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad64x64_avg(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                         int ref_stride, Ptr<byte> second_pred) =>
        Simd.UseAvx2 ? aom_sad64x64_avg_avx2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred)
                     : aom_sad64x64_avg_c(src_ptr, src_stride, ref_ptr, ref_stride, second_pred);

    internal static uint aom_sad_skip_64x64_c(Ptr<byte> src, int src_stride,
                                             Ptr<byte> @ref,
                                             int ref_stride)
    {
        return 2 * sad(src, 2 * src_stride, @ref, 2 * ref_stride, (64), (64 / 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad_skip_64x64(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                           int ref_stride) =>
        Simd.UseAvx2 ? aom_sad_skip_64x64_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad_skip_64x64_c(src_ptr, src_stride, ref_ptr, ref_stride);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad64x64x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad64x64_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad64x64x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad64x64x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad64x64x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    internal static void aom_sad_skip_64x64x4d_c(Ptr<byte> src, int src_stride,
                                                Ptr<Ptr<byte>> ref_array,
                                                int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] = 2 * sad(src, 2 * src_stride, ref_array[i],
                                   2 * ref_stride, (64), (64 / 2));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad_skip_64x64x4d(Ptr<byte> src_ptr, int src_stride,
                                              Ptr<Ptr<byte>> ref_ptr, int ref_stride,
                                              Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad_skip_64x64x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad_skip_64x64x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad64x64x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad64x64x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad64x64x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad64x64x3d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad64x64x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // 64x32
    internal static uint aom_sad64x32_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 64, 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad64x32(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad64x32_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad64x32_c(src_ptr, src_stride, ref_ptr, ref_stride);

    internal static uint aom_sad64x32_avg_c(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred)
    {
        Ptr<byte> comp_pred = comp_pred_buf();  // uint8_t comp_pred[m * n]
        aom_comp_avg_pred(comp_pred, second_pred, 64, 32, @ref, ref_stride);
        return sad(src, src_stride, comp_pred, 64, 64, 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad64x32_avg(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                         int ref_stride, Ptr<byte> second_pred) =>
        Simd.UseAvx2 ? aom_sad64x32_avg_avx2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred)
                     : aom_sad64x32_avg_c(src_ptr, src_stride, ref_ptr, ref_stride, second_pred);

    internal static uint aom_sad_skip_64x32_c(Ptr<byte> src, int src_stride,
                                             Ptr<byte> @ref,
                                             int ref_stride)
    {
        return 2 * sad(src, 2 * src_stride, @ref, 2 * ref_stride, (64), (32 / 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad_skip_64x32(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                           int ref_stride) =>
        Simd.UseAvx2 ? aom_sad_skip_64x32_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad_skip_64x32_c(src_ptr, src_stride, ref_ptr, ref_stride);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad64x32x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad64x32_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad64x32x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad64x32x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad64x32x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    internal static void aom_sad_skip_64x32x4d_c(Ptr<byte> src, int src_stride,
                                                Ptr<Ptr<byte>> ref_array,
                                                int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] = 2 * sad(src, 2 * src_stride, ref_array[i],
                                   2 * ref_stride, (64), (32 / 2));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad_skip_64x32x4d(Ptr<byte> src_ptr, int src_stride,
                                              Ptr<Ptr<byte>> ref_ptr, int ref_stride,
                                              Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad_skip_64x32x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad_skip_64x32x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad64x32x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad64x32x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad64x32x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad64x32x3d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad64x32x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // 32x64
    internal static uint aom_sad32x64_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 32, 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad32x64(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad32x64_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad32x64_c(src_ptr, src_stride, ref_ptr, ref_stride);

    internal static uint aom_sad32x64_avg_c(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred)
    {
        Ptr<byte> comp_pred = comp_pred_buf();  // uint8_t comp_pred[m * n]
        aom_comp_avg_pred(comp_pred, second_pred, 32, 64, @ref, ref_stride);
        return sad(src, src_stride, comp_pred, 32, 32, 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad32x64_avg(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                         int ref_stride, Ptr<byte> second_pred) =>
        Simd.UseAvx2 ? aom_sad32x64_avg_avx2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred)
                     : aom_sad32x64_avg_c(src_ptr, src_stride, ref_ptr, ref_stride, second_pred);

    internal static uint aom_sad_skip_32x64_c(Ptr<byte> src, int src_stride,
                                             Ptr<byte> @ref,
                                             int ref_stride)
    {
        return 2 * sad(src, 2 * src_stride, @ref, 2 * ref_stride, (32), (64 / 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad_skip_32x64(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                           int ref_stride) =>
        Simd.UseAvx2 ? aom_sad_skip_32x64_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad_skip_32x64_c(src_ptr, src_stride, ref_ptr, ref_stride);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad32x64x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad32x64_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad32x64x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad32x64x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad32x64x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    internal static void aom_sad_skip_32x64x4d_c(Ptr<byte> src, int src_stride,
                                                Ptr<Ptr<byte>> ref_array,
                                                int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] = 2 * sad(src, 2 * src_stride, ref_array[i],
                                   2 * ref_stride, (32), (64 / 2));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad_skip_32x64x4d(Ptr<byte> src_ptr, int src_stride,
                                              Ptr<Ptr<byte>> ref_ptr, int ref_stride,
                                              Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad_skip_32x64x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad_skip_32x64x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad32x64x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad32x64x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad32x64x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad32x64x3d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad32x64x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // 32x32
    internal static uint aom_sad32x32_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 32, 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad32x32(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad32x32_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad32x32_c(src_ptr, src_stride, ref_ptr, ref_stride);

    internal static uint aom_sad32x32_avg_c(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred)
    {
        Ptr<byte> comp_pred = comp_pred_buf();  // uint8_t comp_pred[m * n]
        aom_comp_avg_pred(comp_pred, second_pred, 32, 32, @ref, ref_stride);
        return sad(src, src_stride, comp_pred, 32, 32, 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad32x32_avg(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                         int ref_stride, Ptr<byte> second_pred) =>
        Simd.UseAvx2 ? aom_sad32x32_avg_avx2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred)
                     : aom_sad32x32_avg_c(src_ptr, src_stride, ref_ptr, ref_stride, second_pred);

    internal static uint aom_sad_skip_32x32_c(Ptr<byte> src, int src_stride,
                                             Ptr<byte> @ref,
                                             int ref_stride)
    {
        return 2 * sad(src, 2 * src_stride, @ref, 2 * ref_stride, (32), (32 / 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad_skip_32x32(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                           int ref_stride) =>
        Simd.UseAvx2 ? aom_sad_skip_32x32_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad_skip_32x32_c(src_ptr, src_stride, ref_ptr, ref_stride);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad32x32x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad32x32_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad32x32x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad32x32x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad32x32x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    internal static void aom_sad_skip_32x32x4d_c(Ptr<byte> src, int src_stride,
                                                Ptr<Ptr<byte>> ref_array,
                                                int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] = 2 * sad(src, 2 * src_stride, ref_array[i],
                                   2 * ref_stride, (32), (32 / 2));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad_skip_32x32x4d(Ptr<byte> src_ptr, int src_stride,
                                              Ptr<Ptr<byte>> ref_ptr, int ref_stride,
                                              Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad_skip_32x32x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad_skip_32x32x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad32x32x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad32x32x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad32x32x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad32x32x3d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad32x32x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // 32x16
    internal static uint aom_sad32x16_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 32, 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad32x16(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad32x16_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad32x16_c(src_ptr, src_stride, ref_ptr, ref_stride);

    internal static uint aom_sad32x16_avg_c(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred)
    {
        Ptr<byte> comp_pred = comp_pred_buf();  // uint8_t comp_pred[m * n]
        aom_comp_avg_pred(comp_pred, second_pred, 32, 16, @ref, ref_stride);
        return sad(src, src_stride, comp_pred, 32, 32, 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad32x16_avg(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                         int ref_stride, Ptr<byte> second_pred) =>
        Simd.UseAvx2 ? aom_sad32x16_avg_avx2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred)
                     : aom_sad32x16_avg_c(src_ptr, src_stride, ref_ptr, ref_stride, second_pred);

    internal static uint aom_sad_skip_32x16_c(Ptr<byte> src, int src_stride,
                                             Ptr<byte> @ref,
                                             int ref_stride)
    {
        return 2 * sad(src, 2 * src_stride, @ref, 2 * ref_stride, (32), (16 / 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad_skip_32x16(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                           int ref_stride) =>
        Simd.UseAvx2 ? aom_sad_skip_32x16_avx2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad_skip_32x16_c(src_ptr, src_stride, ref_ptr, ref_stride);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad32x16x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad32x16_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad32x16x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad32x16x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad32x16x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    internal static void aom_sad_skip_32x16x4d_c(Ptr<byte> src, int src_stride,
                                                Ptr<Ptr<byte>> ref_array,
                                                int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] = 2 * sad(src, 2 * src_stride, ref_array[i],
                                   2 * ref_stride, (32), (16 / 2));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad_skip_32x16x4d(Ptr<byte> src_ptr, int src_stride,
                                              Ptr<Ptr<byte>> ref_ptr, int ref_stride,
                                              Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad_skip_32x16x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad_skip_32x16x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad32x16x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad32x16x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad32x16x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad32x16x3d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad32x16x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // 16x32
    internal static uint aom_sad16x32_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 16, 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad16x32(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad16x32_sse2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad16x32_c(src_ptr, src_stride, ref_ptr, ref_stride);

    internal static uint aom_sad16x32_avg_c(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred)
    {
        Ptr<byte> comp_pred = comp_pred_buf();  // uint8_t comp_pred[m * n]
        aom_comp_avg_pred(comp_pred, second_pred, 16, 32, @ref, ref_stride);
        return sad(src, src_stride, comp_pred, 16, 16, 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad16x32_avg(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                         int ref_stride, Ptr<byte> second_pred) =>
        Simd.UseAvx2 ? aom_sad16x32_avg_sse2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred)
                     : aom_sad16x32_avg_c(src_ptr, src_stride, ref_ptr, ref_stride, second_pred);

    internal static uint aom_sad_skip_16x32_c(Ptr<byte> src, int src_stride,
                                             Ptr<byte> @ref,
                                             int ref_stride)
    {
        return 2 * sad(src, 2 * src_stride, @ref, 2 * ref_stride, (16), (32 / 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad_skip_16x32(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                           int ref_stride) =>
        Simd.UseAvx2 ? aom_sad_skip_16x32_sse2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad_skip_16x32_c(src_ptr, src_stride, ref_ptr, ref_stride);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad16x32x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad16x32_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad16x32x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad16x32x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad16x32x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    internal static void aom_sad_skip_16x32x4d_c(Ptr<byte> src, int src_stride,
                                                Ptr<Ptr<byte>> ref_array,
                                                int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] = 2 * sad(src, 2 * src_stride, ref_array[i],
                                   2 * ref_stride, (16), (32 / 2));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad_skip_16x32x4d(Ptr<byte> src_ptr, int src_stride,
                                              Ptr<Ptr<byte>> ref_ptr, int ref_stride,
                                              Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad_skip_16x32x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad_skip_16x32x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad16x32x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad16x32x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad16x32x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad16x32x3d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad16x32x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // 16x16
    internal static uint aom_sad16x16_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 16, 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad16x16(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad16x16_sse2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad16x16_c(src_ptr, src_stride, ref_ptr, ref_stride);

    internal static uint aom_sad16x16_avg_c(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred)
    {
        Ptr<byte> comp_pred = comp_pred_buf();  // uint8_t comp_pred[m * n]
        aom_comp_avg_pred(comp_pred, second_pred, 16, 16, @ref, ref_stride);
        return sad(src, src_stride, comp_pred, 16, 16, 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad16x16_avg(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                         int ref_stride, Ptr<byte> second_pred) =>
        Simd.UseAvx2 ? aom_sad16x16_avg_sse2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred)
                     : aom_sad16x16_avg_c(src_ptr, src_stride, ref_ptr, ref_stride, second_pred);

    internal static uint aom_sad_skip_16x16_c(Ptr<byte> src, int src_stride,
                                             Ptr<byte> @ref,
                                             int ref_stride)
    {
        return 2 * sad(src, 2 * src_stride, @ref, 2 * ref_stride, (16), (16 / 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad_skip_16x16(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                           int ref_stride) =>
        Simd.UseAvx2 ? aom_sad_skip_16x16_sse2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad_skip_16x16_c(src_ptr, src_stride, ref_ptr, ref_stride);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad16x16x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad16x16_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad16x16x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad16x16x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad16x16x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    internal static void aom_sad_skip_16x16x4d_c(Ptr<byte> src, int src_stride,
                                                Ptr<Ptr<byte>> ref_array,
                                                int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] = 2 * sad(src, 2 * src_stride, ref_array[i],
                                   2 * ref_stride, (16), (16 / 2));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad_skip_16x16x4d(Ptr<byte> src_ptr, int src_stride,
                                              Ptr<Ptr<byte>> ref_ptr, int ref_stride,
                                              Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad_skip_16x16x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad_skip_16x16x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad16x16x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad16x16x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad16x16x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad16x16x3d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad16x16x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // 16x8
    internal static uint aom_sad16x8_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 16, 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad16x8(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad16x8_sse2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad16x8_c(src_ptr, src_stride, ref_ptr, ref_stride);

    internal static uint aom_sad16x8_avg_c(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred)
    {
        Ptr<byte> comp_pred = comp_pred_buf();  // uint8_t comp_pred[m * n]
        aom_comp_avg_pred(comp_pred, second_pred, 16, 8, @ref, ref_stride);
        return sad(src, src_stride, comp_pred, 16, 16, 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad16x8_avg(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                         int ref_stride, Ptr<byte> second_pred) =>
        Simd.UseAvx2 ? aom_sad16x8_avg_sse2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred)
                     : aom_sad16x8_avg_c(src_ptr, src_stride, ref_ptr, ref_stride, second_pred);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad16x8x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad16x8_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad16x8x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad16x8x4d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad16x8x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad16x8x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad16x8x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad16x8x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad16x8x3d_avx2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad16x8x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // 8x16
    internal static uint aom_sad8x16_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 8, 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad8x16(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad8x16_sse2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad8x16_c(src_ptr, src_stride, ref_ptr, ref_stride);

    internal static uint aom_sad8x16_avg_c(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred)
    {
        Ptr<byte> comp_pred = comp_pred_buf();  // uint8_t comp_pred[m * n]
        aom_comp_avg_pred(comp_pred, second_pred, 8, 16, @ref, ref_stride);
        return sad(src, src_stride, comp_pred, 8, 8, 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad8x16_avg(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                         int ref_stride, Ptr<byte> second_pred) =>
        Simd.UseAvx2 ? aom_sad8x16_avg_sse2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred)
                     : aom_sad8x16_avg_c(src_ptr, src_stride, ref_ptr, ref_stride, second_pred);

    internal static uint aom_sad_skip_8x16_c(Ptr<byte> src, int src_stride,
                                             Ptr<byte> @ref,
                                             int ref_stride)
    {
        return 2 * sad(src, 2 * src_stride, @ref, 2 * ref_stride, (8), (16 / 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad_skip_8x16(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                           int ref_stride) =>
        Simd.UseAvx2 ? aom_sad_skip_8x16_sse2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad_skip_8x16_c(src_ptr, src_stride, ref_ptr, ref_stride);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad8x16x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad8x16_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad8x16x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad8x16x4d_sse2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad8x16x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    internal static void aom_sad_skip_8x16x4d_c(Ptr<byte> src, int src_stride,
                                                Ptr<Ptr<byte>> ref_array,
                                                int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] = 2 * sad(src, 2 * src_stride, ref_array[i],
                                   2 * ref_stride, (8), (16 / 2));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad_skip_8x16x4d(Ptr<byte> src_ptr, int src_stride,
                                              Ptr<Ptr<byte>> ref_ptr, int ref_stride,
                                              Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad_skip_8x16x4d_sse2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad_skip_8x16x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad8x16x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad8x16x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad8x16x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array) =>
        aom_sad8x16x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);

    // 8x8
    internal static uint aom_sad8x8_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 8, 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad8x8(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad8x8_sse2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad8x8_c(src_ptr, src_stride, ref_ptr, ref_stride);

    internal static uint aom_sad8x8_avg_c(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred)
    {
        Ptr<byte> comp_pred = comp_pred_buf();  // uint8_t comp_pred[m * n]
        aom_comp_avg_pred(comp_pred, second_pred, 8, 8, @ref, ref_stride);
        return sad(src, src_stride, comp_pred, 8, 8, 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad8x8_avg(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                         int ref_stride, Ptr<byte> second_pred) =>
        Simd.UseAvx2 ? aom_sad8x8_avg_sse2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred)
                     : aom_sad8x8_avg_c(src_ptr, src_stride, ref_ptr, ref_stride, second_pred);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad8x8x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad8x8_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad8x8x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad8x8x4d_sse2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad8x8x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad8x8x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad8x8x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad8x8x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array) =>
        aom_sad8x8x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);

    // 8x4
    internal static uint aom_sad8x4_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 8, 4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad8x4(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad8x4_sse2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad8x4_c(src_ptr, src_stride, ref_ptr, ref_stride);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad8x4x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad8x4_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad8x4x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad8x4x4d_sse2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad8x4x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad8x4x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad8x4x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad8x4x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array) =>
        aom_sad8x4x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);

    // 4x8
    internal static uint aom_sad4x8_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 4, 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad4x8(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad4x8_sse2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad4x8_c(src_ptr, src_stride, ref_ptr, ref_stride);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad4x8x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad4x8_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad4x8x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad4x8x4d_sse2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad4x8x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad4x8x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad4x8x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad4x8x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array) =>
        aom_sad4x8x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);

    // 4x4
    internal static uint aom_sad4x4_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {
        return sad(src, src_stride, @ref, ref_stride, 4, 4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sad4x4(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        Simd.UseAvx2 ? aom_sad4x4_sse2(src_ptr, src_stride, ref_ptr, ref_stride)
                     : aom_sad4x4_c(src_ptr, src_stride, ref_ptr, ref_stride);

    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad4x4x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        int i;
        for (i = 0; i < 4; ++i)
        {
            sad_array[i] =
                aom_sad4x4_c(src, src_stride, ref_array[i], ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad4x4x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array)
    {
        if (Simd.UseAvx2) aom_sad4x4x4d_sse2(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
        else aom_sad4x4x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    }

    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad4x4x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {
        aom_sad4x4x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_sad4x4x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array) =>
        aom_sad4x4x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
    // </gen:sad_sizes>
}
