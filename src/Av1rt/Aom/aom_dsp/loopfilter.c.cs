// Port of libaom aom_dsp/loopfilter.c

// [port] The stem "loopfilter" is already the name of the struct loopfilter type
// (av1/common/av1_loopfilter.h), so the class is loopfilter_h (PORTING 2). There is no
// aom_dsp/loopfilter.h: the prototypes live in aom_dsp_rtcd.h.
global using static Av1rt.Aom.loopfilter_h;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class loopfilter_h
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static sbyte signed_char_clamp(int t)
    {
        return (sbyte)clamp(t, -128, 127);
    }

    // [subset] signed_char_clamp_high: CONFIG_AV1_HIGHBITDEPTH == 0.

    // should we apply any filter at all: 11111111 yes, 00000000 no
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static sbyte filter_mask2(byte limit, byte blimit, byte p1,
                                      byte p0, byte q0, byte q1)
    {
        int mask = 0;
        mask |= B(Math.Abs(p1 - p0) > limit) * -1;
        mask |= B(Math.Abs(q1 - q0) > limit) * -1;
        mask |= B(Math.Abs(p0 - q0) * 2 + Math.Abs(p1 - q1) / 2 > blimit) * -1;
        return (sbyte)~mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static sbyte filter_mask(byte limit, byte blimit, byte p3,
                                     byte p2, byte p1, byte p0, byte q0,
                                     byte q1, byte q2, byte q3)
    {
        int mask = 0;
        mask |= B(Math.Abs(p3 - p2) > limit) * -1;
        mask |= B(Math.Abs(p2 - p1) > limit) * -1;
        mask |= B(Math.Abs(p1 - p0) > limit) * -1;
        mask |= B(Math.Abs(q1 - q0) > limit) * -1;
        mask |= B(Math.Abs(q2 - q1) > limit) * -1;
        mask |= B(Math.Abs(q3 - q2) > limit) * -1;
        mask |= B(Math.Abs(p0 - q0) * 2 + Math.Abs(p1 - q1) / 2 > blimit) * -1;
        return (sbyte)~mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static sbyte filter_mask3_chroma(byte limit, byte blimit,
                                             byte p2, byte p1, byte p0,
                                             byte q0, byte q1, byte q2)
    {
        int mask = 0;
        mask |= B(Math.Abs(p2 - p1) > limit) * -1;
        mask |= B(Math.Abs(p1 - p0) > limit) * -1;
        mask |= B(Math.Abs(q1 - q0) > limit) * -1;
        mask |= B(Math.Abs(q2 - q1) > limit) * -1;
        mask |= B(Math.Abs(p0 - q0) * 2 + Math.Abs(p1 - q1) / 2 > blimit) * -1;
        return (sbyte)~mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static sbyte flat_mask3_chroma(byte thresh, byte p2, byte p1,
                                           byte p0, byte q0, byte q1,
                                           byte q2)
    {
        int mask = 0;
        mask |= B(Math.Abs(p1 - p0) > thresh) * -1;
        mask |= B(Math.Abs(q1 - q0) > thresh) * -1;
        mask |= B(Math.Abs(p2 - p0) > thresh) * -1;
        mask |= B(Math.Abs(q2 - q0) > thresh) * -1;
        return (sbyte)~mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static sbyte flat_mask4(byte thresh, byte p3, byte p2,
                                    byte p1, byte p0, byte q0, byte q1,
                                    byte q2, byte q3)
    {
        int mask = 0;
        mask |= B(Math.Abs(p1 - p0) > thresh) * -1;
        mask |= B(Math.Abs(q1 - q0) > thresh) * -1;
        mask |= B(Math.Abs(p2 - p0) > thresh) * -1;
        mask |= B(Math.Abs(q2 - q0) > thresh) * -1;
        mask |= B(Math.Abs(p3 - p0) > thresh) * -1;
        mask |= B(Math.Abs(q3 - q0) > thresh) * -1;
        return (sbyte)~mask;
    }

    // is there high edge variance internal edge: 11111111 yes, 00000000 no
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static sbyte hev_mask(byte thresh, byte p1, byte p0,
                                  byte q0, byte q1)
    {
        int hev = 0;
        hev |= B(Math.Abs(p1 - p0) > thresh) * -1;
        hev |= B(Math.Abs(q1 - q0) > thresh) * -1;
        return (sbyte)hev;
    }

    // [port] The uint8_t *op/oq pixel pointers are `ref byte` into the frame row.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void filter4(sbyte mask, byte thresh, ref byte op1,
                                ref byte op0, ref byte oq0, ref byte oq1)
    {
        sbyte filter1, filter2;

        sbyte ps1 = (sbyte)(op1 ^ 0x80);
        sbyte ps0 = (sbyte)(op0 ^ 0x80);
        sbyte qs0 = (sbyte)(oq0 ^ 0x80);
        sbyte qs1 = (sbyte)(oq1 ^ 0x80);
        sbyte hev = hev_mask(thresh, op1, op0, oq0, oq1);

        // add outer taps if we have high edge variance
        sbyte filter = (sbyte)(signed_char_clamp(ps1 - qs1) & hev);

        // inner taps
        filter = (sbyte)(signed_char_clamp(filter + 3 * (qs0 - ps0)) & mask);

        // save bottom 3 bits so that we round one side +4 and the other +3
        // if it equals 4 we'll set to adjust by -1 to account for the fact
        // we'd round 3 the other way
        filter1 = (sbyte)(signed_char_clamp(filter + 4) >> 3);
        filter2 = (sbyte)(signed_char_clamp(filter + 3) >> 3);

        oq0 = (byte)(signed_char_clamp(qs0 - filter1) ^ 0x80);
        op0 = (byte)(signed_char_clamp(ps0 + filter2) ^ 0x80);

        // outer tap adjustments
        filter = (sbyte)(ROUND_POWER_OF_TWO(filter1, 1) & ~hev);

        oq1 = (byte)(signed_char_clamp(qs1 - filter) ^ 0x80);
        op1 = (byte)(signed_char_clamp(ps1 + filter) ^ 0x80);
    }

    internal static void aom_lpf_horizontal_4_c(Ptr<byte> s, int p /* pitch */,
                                                Ptr<byte> blimit, Ptr<byte> limit,
                                                Ptr<byte> thresh)
    {
        int i;
        int count = 4;

        // loop filter designed to work using chars so that we can make maximum use
        // of 8 bit simd instructions.
        for (i = 0; i < count; ++i)
        {
            byte p1 = s[-2 * p], p0 = s[-p];
            byte q0 = s[0 * p], q1 = s[1 * p];
            sbyte mask = filter_mask2(limit.Value, blimit.Value, p1, p0, q0, q1);
            filter4(mask, thresh.Value, ref s[-2 * p], ref s[-1 * p], ref s[0], ref s[1 * p]);
            ++s;
        }
    }

    internal static void aom_lpf_horizontal_4_dual_c(Ptr<byte> s, int p, Ptr<byte> blimit0,
                                                     Ptr<byte> limit0, Ptr<byte> thresh0,
                                                     Ptr<byte> blimit1, Ptr<byte> limit1,
                                                     Ptr<byte> thresh1)
    {
        aom_lpf_horizontal_4_c(s, p, blimit0, limit0, thresh0);
        aom_lpf_horizontal_4_c(s + 4, p, blimit1, limit1, thresh1);
    }

    // [port] Not executed in the coverage runs, but reached from filter_horz_chroma
    // (USE_QUAD, filter_length 4), a data-dependent case; trivially small.
    internal static void aom_lpf_horizontal_4_quad_c(Ptr<byte> s, int p, Ptr<byte> blimit0,
                                                     Ptr<byte> limit0,
                                                     Ptr<byte> thresh0)
    {
        aom_lpf_horizontal_4_c(s, p, blimit0, limit0, thresh0);
        aom_lpf_horizontal_4_c(s + 4, p, blimit0, limit0, thresh0);
        aom_lpf_horizontal_4_c(s + 8, p, blimit0, limit0, thresh0);
        aom_lpf_horizontal_4_c(s + 12, p, blimit0, limit0, thresh0);
    }

    internal static void aom_lpf_vertical_4_c(Ptr<byte> s, int pitch, Ptr<byte> blimit,
                                              Ptr<byte> limit, Ptr<byte> thresh)
    {
        int i;
        int count = 4;

        // loop filter designed to work using chars so that we can make maximum use
        // of 8 bit simd instructions.
        for (i = 0; i < count; ++i)
        {
            byte p1 = s[-2], p0 = s[-1];
            byte q0 = s[0], q1 = s[1];
            sbyte mask = filter_mask2(limit.Value, blimit.Value, p1, p0, q0, q1);
            filter4(mask, thresh.Value, ref s[-2], ref s[-1], ref s[0], ref s[1]);
            s += pitch;
        }
    }

    // [port] Not executed in the coverage runs, but reached from filter_vert / filter_vert_chroma
    // (USE_DUAL, filter_length 4), a data-dependent case; trivially small.
    internal static void aom_lpf_vertical_4_dual_c(Ptr<byte> s, int pitch, Ptr<byte> blimit0,
                                                   Ptr<byte> limit0, Ptr<byte> thresh0,
                                                   Ptr<byte> blimit1, Ptr<byte> limit1,
                                                   Ptr<byte> thresh1)
    {
        aom_lpf_vertical_4_c(s, pitch, blimit0, limit0, thresh0);
        aom_lpf_vertical_4_c(s + 4 * pitch, pitch, blimit1, limit1, thresh1);
    }

    // [port] Not executed in the coverage runs, but reached from filter_vert / filter_vert_chroma
    // (USE_QUAD, filter_length 4), a data-dependent case; trivially small.
    internal static void aom_lpf_vertical_4_quad_c(Ptr<byte> s, int pitch, Ptr<byte> blimit0,
                                                   Ptr<byte> limit0, Ptr<byte> thresh0)
    {
        aom_lpf_vertical_4_c(s, pitch, blimit0, limit0, thresh0);
        aom_lpf_vertical_4_c(s + 4 * pitch, pitch, blimit0, limit0, thresh0);
        aom_lpf_vertical_4_c(s + 8 * pitch, pitch, blimit0, limit0, thresh0);
        aom_lpf_vertical_4_c(s + 12 * pitch, pitch, blimit0, limit0, thresh0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void filter6(sbyte mask, byte thresh, sbyte flat,
                                ref byte op2, ref byte op1, ref byte op0,
                                ref byte oq0, ref byte oq1, ref byte oq2)
    {
        if (flat != 0 && mask != 0)
        {
            byte p2 = op2, p1 = op1, p0 = op0;
            byte q0 = oq0, q1 = oq1, q2 = oq2;

            // 5-tap filter [1, 2, 2, 2, 1]
            op1 = (byte)ROUND_POWER_OF_TWO(p2 * 3 + p1 * 2 + p0 * 2 + q0, 3);
            op0 = (byte)ROUND_POWER_OF_TWO(p2 + p1 * 2 + p0 * 2 + q0 * 2 + q1, 3);
            oq0 = (byte)ROUND_POWER_OF_TWO(p1 + p0 * 2 + q0 * 2 + q1 * 2 + q2, 3);
            oq1 = (byte)ROUND_POWER_OF_TWO(p0 + q0 * 2 + q1 * 2 + q2 * 3, 3);
        }
        else
        {
            filter4(mask, thresh, ref op1, ref op0, ref oq0, ref oq1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void filter8(sbyte mask, byte thresh, sbyte flat,
                                ref byte op3, ref byte op2, ref byte op1,
                                ref byte op0, ref byte oq0, ref byte oq1,
                                ref byte oq2, ref byte oq3)
    {
        if (flat != 0 && mask != 0)
        {
            byte p3 = op3, p2 = op2, p1 = op1, p0 = op0;
            byte q0 = oq0, q1 = oq1, q2 = oq2, q3 = oq3;

            // 7-tap filter [1, 1, 1, 2, 1, 1, 1]
            op2 = (byte)ROUND_POWER_OF_TWO(p3 + p3 + p3 + 2 * p2 + p1 + p0 + q0, 3);
            op1 = (byte)ROUND_POWER_OF_TWO(p3 + p3 + p2 + 2 * p1 + p0 + q0 + q1, 3);
            op0 = (byte)ROUND_POWER_OF_TWO(p3 + p2 + p1 + 2 * p0 + q0 + q1 + q2, 3);
            oq0 = (byte)ROUND_POWER_OF_TWO(p2 + p1 + p0 + 2 * q0 + q1 + q2 + q3, 3);
            oq1 = (byte)ROUND_POWER_OF_TWO(p1 + p0 + q0 + 2 * q1 + q2 + q3 + q3, 3);
            oq2 = (byte)ROUND_POWER_OF_TWO(p0 + q0 + q1 + 2 * q2 + q3 + q3 + q3, 3);
        }
        else
        {
            filter4(mask, thresh, ref op1, ref op0, ref oq0, ref oq1);
        }
    }

    internal static void aom_lpf_horizontal_6_c(Ptr<byte> s, int p, Ptr<byte> blimit,
                                                Ptr<byte> limit, Ptr<byte> thresh)
    {
        int i;
        int count = 4;

        // loop filter designed to work using chars so that we can make maximum use
        // of 8 bit simd instructions.
        for (i = 0; i < count; ++i)
        {
            byte p2 = s[-3 * p], p1 = s[-2 * p], p0 = s[-p];
            byte q0 = s[0 * p], q1 = s[1 * p], q2 = s[2 * p];

            sbyte mask =
                filter_mask3_chroma(limit.Value, blimit.Value, p2, p1, p0, q0, q1, q2);
            sbyte flat = flat_mask3_chroma(1, p2, p1, p0, q0, q1, q2);
            filter6(mask, thresh.Value, flat, ref s[-3 * p], ref s[-2 * p], ref s[-1 * p],
                    ref s[0], ref s[1 * p], ref s[2 * p]);
            ++s;
        }
    }

    internal static void aom_lpf_horizontal_6_dual_c(Ptr<byte> s, int p, Ptr<byte> blimit0,
                                                     Ptr<byte> limit0, Ptr<byte> thresh0,
                                                     Ptr<byte> blimit1, Ptr<byte> limit1,
                                                     Ptr<byte> thresh1)
    {
        aom_lpf_horizontal_6_c(s, p, blimit0, limit0, thresh0);
        aom_lpf_horizontal_6_c(s + 4, p, blimit1, limit1, thresh1);
    }

    internal static void aom_lpf_horizontal_6_quad_c(Ptr<byte> s, int p, Ptr<byte> blimit0,
                                                     Ptr<byte> limit0,
                                                     Ptr<byte> thresh0)
    {
        aom_lpf_horizontal_6_c(s, p, blimit0, limit0, thresh0);
        aom_lpf_horizontal_6_c(s + 4, p, blimit0, limit0, thresh0);
        aom_lpf_horizontal_6_c(s + 8, p, blimit0, limit0, thresh0);
        aom_lpf_horizontal_6_c(s + 12, p, blimit0, limit0, thresh0);
    }

    internal static void aom_lpf_horizontal_8_c(Ptr<byte> s, int p, Ptr<byte> blimit,
                                                Ptr<byte> limit, Ptr<byte> thresh)
    {
        int i;
        int count = 4;

        // loop filter designed to work using chars so that we can make maximum use
        // of 8 bit simd instructions.
        for (i = 0; i < count; ++i)
        {
            byte p3 = s[-4 * p], p2 = s[-3 * p], p1 = s[-2 * p], p0 = s[-p];
            byte q0 = s[0 * p], q1 = s[1 * p], q2 = s[2 * p], q3 = s[3 * p];

            sbyte mask =
                filter_mask(limit.Value, blimit.Value, p3, p2, p1, p0, q0, q1, q2, q3);
            sbyte flat = flat_mask4(1, p3, p2, p1, p0, q0, q1, q2, q3);
            filter8(mask, thresh.Value, flat, ref s[-4 * p], ref s[-3 * p], ref s[-2 * p],
                    ref s[-1 * p], ref s[0], ref s[1 * p], ref s[2 * p], ref s[3 * p]);
            ++s;
        }
    }

    internal static void aom_lpf_horizontal_8_dual_c(Ptr<byte> s, int p, Ptr<byte> blimit0,
                                                     Ptr<byte> limit0, Ptr<byte> thresh0,
                                                     Ptr<byte> blimit1, Ptr<byte> limit1,
                                                     Ptr<byte> thresh1)
    {
        aom_lpf_horizontal_8_c(s, p, blimit0, limit0, thresh0);
        aom_lpf_horizontal_8_c(s + 4, p, blimit1, limit1, thresh1);
    }

    internal static void aom_lpf_horizontal_8_quad_c(Ptr<byte> s, int p, Ptr<byte> blimit0,
                                                     Ptr<byte> limit0,
                                                     Ptr<byte> thresh0)
    {
        aom_lpf_horizontal_8_c(s, p, blimit0, limit0, thresh0);
        aom_lpf_horizontal_8_c(s + 4, p, blimit0, limit0, thresh0);
        aom_lpf_horizontal_8_c(s + 8, p, blimit0, limit0, thresh0);
        aom_lpf_horizontal_8_c(s + 12, p, blimit0, limit0, thresh0);
    }

    internal static void aom_lpf_vertical_6_c(Ptr<byte> s, int pitch, Ptr<byte> blimit,
                                              Ptr<byte> limit, Ptr<byte> thresh)
    {
        int i;
        int count = 4;

        for (i = 0; i < count; ++i)
        {
            byte p2 = s[-3], p1 = s[-2], p0 = s[-1];
            byte q0 = s[0], q1 = s[1], q2 = s[2];
            sbyte mask =
                filter_mask3_chroma(limit.Value, blimit.Value, p2, p1, p0, q0, q1, q2);
            sbyte flat = flat_mask3_chroma(1, p2, p1, p0, q0, q1, q2);
            filter6(mask, thresh.Value, flat, ref s[-3], ref s[-2], ref s[-1], ref s[0],
                    ref s[1], ref s[2]);
            s += pitch;
        }
    }

    internal static void aom_lpf_vertical_6_dual_c(Ptr<byte> s, int pitch, Ptr<byte> blimit0,
                                                   Ptr<byte> limit0, Ptr<byte> thresh0,
                                                   Ptr<byte> blimit1, Ptr<byte> limit1,
                                                   Ptr<byte> thresh1)
    {
        aom_lpf_vertical_6_c(s, pitch, blimit0, limit0, thresh0);
        aom_lpf_vertical_6_c(s + 4 * pitch, pitch, blimit1, limit1, thresh1);
    }

    internal static void aom_lpf_vertical_6_quad_c(Ptr<byte> s, int pitch, Ptr<byte> blimit0,
                                                   Ptr<byte> limit0, Ptr<byte> thresh0)
    {
        aom_lpf_vertical_6_c(s, pitch, blimit0, limit0, thresh0);
        aom_lpf_vertical_6_c(s + 4 * pitch, pitch, blimit0, limit0, thresh0);
        aom_lpf_vertical_6_c(s + 8 * pitch, pitch, blimit0, limit0, thresh0);
        aom_lpf_vertical_6_c(s + 12 * pitch, pitch, blimit0, limit0, thresh0);
    }

    internal static void aom_lpf_vertical_8_c(Ptr<byte> s, int pitch, Ptr<byte> blimit,
                                              Ptr<byte> limit, Ptr<byte> thresh)
    {
        int i;
        int count = 4;

        for (i = 0; i < count; ++i)
        {
            byte p3 = s[-4], p2 = s[-3], p1 = s[-2], p0 = s[-1];
            byte q0 = s[0], q1 = s[1], q2 = s[2], q3 = s[3];
            sbyte mask =
                filter_mask(limit.Value, blimit.Value, p3, p2, p1, p0, q0, q1, q2, q3);
            sbyte flat = flat_mask4(1, p3, p2, p1, p0, q0, q1, q2, q3);
            filter8(mask, thresh.Value, flat, ref s[-4], ref s[-3], ref s[-2], ref s[-1],
                    ref s[0], ref s[1], ref s[2], ref s[3]);
            s += pitch;
        }
    }

    internal static void aom_lpf_vertical_8_dual_c(Ptr<byte> s, int pitch, Ptr<byte> blimit0,
                                                   Ptr<byte> limit0, Ptr<byte> thresh0,
                                                   Ptr<byte> blimit1, Ptr<byte> limit1,
                                                   Ptr<byte> thresh1)
    {
        aom_lpf_vertical_8_c(s, pitch, blimit0, limit0, thresh0);
        aom_lpf_vertical_8_c(s + 4 * pitch, pitch, blimit1, limit1, thresh1);
    }

    internal static void aom_lpf_vertical_8_quad_c(Ptr<byte> s, int pitch, Ptr<byte> blimit0,
                                                   Ptr<byte> limit0, Ptr<byte> thresh0)
    {
        aom_lpf_vertical_8_c(s, pitch, blimit0, limit0, thresh0);
        aom_lpf_vertical_8_c(s + 4 * pitch, pitch, blimit0, limit0, thresh0);
        aom_lpf_vertical_8_c(s + 8 * pitch, pitch, blimit0, limit0, thresh0);
        aom_lpf_vertical_8_c(s + 12 * pitch, pitch, blimit0, limit0, thresh0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void filter14(sbyte mask, byte thresh, sbyte flat,
                                 sbyte flat2, ref byte op6, ref byte op5,
                                 ref byte op4, ref byte op3, ref byte op2,
                                 ref byte op1, ref byte op0, ref byte oq0,
                                 ref byte oq1, ref byte oq2, ref byte oq3,
                                 ref byte oq4, ref byte oq5, ref byte oq6)
    {
        if (flat2 != 0 && flat != 0 && mask != 0)
        {
            byte p6 = op6, p5 = op5, p4 = op4, p3 = op3, p2 = op2,
                 p1 = op1, p0 = op0;
            byte q0 = oq0, q1 = oq1, q2 = oq2, q3 = oq3, q4 = oq4,
                 q5 = oq5, q6 = oq6;

            // 13-tap filter [1, 1, 1, 1, 1, 2, 2, 2, 1, 1, 1, 1, 1]
            op5 = (byte)ROUND_POWER_OF_TWO(p6 * 7 + p5 * 2 + p4 * 2 + p3 + p2 + p1 + p0 + q0,
                                           4);
            op4 = (byte)ROUND_POWER_OF_TWO(
                p6 * 5 + p5 * 2 + p4 * 2 + p3 * 2 + p2 + p1 + p0 + q0 + q1, 4);
            op3 = (byte)ROUND_POWER_OF_TWO(
                p6 * 4 + p5 + p4 * 2 + p3 * 2 + p2 * 2 + p1 + p0 + q0 + q1 + q2, 4);
            op2 = (byte)ROUND_POWER_OF_TWO(
                p6 * 3 + p5 + p4 + p3 * 2 + p2 * 2 + p1 * 2 + p0 + q0 + q1 + q2 + q3,
                4);
            op1 = (byte)ROUND_POWER_OF_TWO(p6 * 2 + p5 + p4 + p3 + p2 * 2 + p1 * 2 + p0 * 2 +
                                               q0 + q1 + q2 + q3 + q4,
                                           4);
            op0 = (byte)ROUND_POWER_OF_TWO(p6 + p5 + p4 + p3 + p2 + p1 * 2 + p0 * 2 +
                                               q0 * 2 + q1 + q2 + q3 + q4 + q5,
                                           4);
            oq0 = (byte)ROUND_POWER_OF_TWO(p5 + p4 + p3 + p2 + p1 + p0 * 2 + q0 * 2 +
                                               q1 * 2 + q2 + q3 + q4 + q5 + q6,
                                           4);
            oq1 = (byte)ROUND_POWER_OF_TWO(p4 + p3 + p2 + p1 + p0 + q0 * 2 + q1 * 2 +
                                               q2 * 2 + q3 + q4 + q5 + q6 * 2,
                                           4);
            oq2 = (byte)ROUND_POWER_OF_TWO(
                p3 + p2 + p1 + p0 + q0 + q1 * 2 + q2 * 2 + q3 * 2 + q4 + q5 + q6 * 3,
                4);
            oq3 = (byte)ROUND_POWER_OF_TWO(
                p2 + p1 + p0 + q0 + q1 + q2 * 2 + q3 * 2 + q4 * 2 + q5 + q6 * 4, 4);
            oq4 = (byte)ROUND_POWER_OF_TWO(
                p1 + p0 + q0 + q1 + q2 + q3 * 2 + q4 * 2 + q5 * 2 + q6 * 5, 4);
            oq5 = (byte)ROUND_POWER_OF_TWO(p0 + q0 + q1 + q2 + q3 + q4 * 2 + q5 * 2 + q6 * 7,
                                           4);
        }
        else
        {
            filter8(mask, thresh, flat, ref op3, ref op2, ref op1, ref op0, ref oq0, ref oq1,
                    ref oq2, ref oq3);
        }
    }

    private static void mb_lpf_horizontal_edge_w(Ptr<byte> s, int p, Ptr<byte> blimit,
                                                 Ptr<byte> limit,
                                                 Ptr<byte> thresh, int count)
    {
        int i;
        int step = 4;

        // loop filter designed to work using chars so that we can make maximum use
        // of 8 bit simd instructions.
        for (i = 0; i < step * count; ++i)
        {
            byte p6 = s[-7 * p], p5 = s[-6 * p], p4 = s[-5 * p],
                 p3 = s[-4 * p], p2 = s[-3 * p], p1 = s[-2 * p], p0 = s[-p];
            byte q0 = s[0 * p], q1 = s[1 * p], q2 = s[2 * p], q3 = s[3 * p],
                 q4 = s[4 * p], q5 = s[5 * p], q6 = s[6 * p];
            sbyte mask =
                filter_mask(limit.Value, blimit.Value, p3, p2, p1, p0, q0, q1, q2, q3);
            sbyte flat = flat_mask4(1, p3, p2, p1, p0, q0, q1, q2, q3);
            sbyte flat2 = flat_mask4(1, p6, p5, p4, p0, q0, q4, q5, q6);

            filter14(mask, thresh.Value, flat, flat2, ref s[-7 * p], ref s[-6 * p],
                     ref s[-5 * p], ref s[-4 * p], ref s[-3 * p], ref s[-2 * p],
                     ref s[-1 * p], ref s[0], ref s[1 * p], ref s[2 * p], ref s[3 * p],
                     ref s[4 * p], ref s[5 * p], ref s[6 * p]);
            ++s;
        }
    }

    // [port] Not executed in the coverage runs, but reached from filter_horz (USE_SINGLE,
    // filter_length 14), a data-dependent case; trivially small.
    internal static void aom_lpf_horizontal_14_c(Ptr<byte> s, int p, Ptr<byte> blimit,
                                                 Ptr<byte> limit, Ptr<byte> thresh)
    {
        mb_lpf_horizontal_edge_w(s, p, blimit, limit, thresh, 1);
    }

    internal static void aom_lpf_horizontal_14_dual_c(Ptr<byte> s, int p, Ptr<byte> blimit0,
                                                      Ptr<byte> limit0, Ptr<byte> thresh0,
                                                      Ptr<byte> blimit1, Ptr<byte> limit1,
                                                      Ptr<byte> thresh1)
    {
        mb_lpf_horizontal_edge_w(s, p, blimit0, limit0, thresh0, 1);
        mb_lpf_horizontal_edge_w(s + 4, p, blimit1, limit1, thresh1, 1);
    }

    internal static void aom_lpf_horizontal_14_quad_c(Ptr<byte> s, int p, Ptr<byte> blimit0,
                                                      Ptr<byte> limit0,
                                                      Ptr<byte> thresh0)
    {
        mb_lpf_horizontal_edge_w(s, p, blimit0, limit0, thresh0, 1);
        mb_lpf_horizontal_edge_w(s + 4, p, blimit0, limit0, thresh0, 1);
        mb_lpf_horizontal_edge_w(s + 8, p, blimit0, limit0, thresh0, 1);
        mb_lpf_horizontal_edge_w(s + 12, p, blimit0, limit0, thresh0, 1);
    }

    private static void mb_lpf_vertical_edge_w(Ptr<byte> s, int p, Ptr<byte> blimit,
                                               Ptr<byte> limit, Ptr<byte> thresh,
                                               int count)
    {
        int i;

        for (i = 0; i < count; ++i)
        {
            byte p6 = s[-7], p5 = s[-6], p4 = s[-5], p3 = s[-4], p2 = s[-3],
                 p1 = s[-2], p0 = s[-1];
            byte q0 = s[0], q1 = s[1], q2 = s[2], q3 = s[3], q4 = s[4],
                 q5 = s[5], q6 = s[6];
            sbyte mask =
                filter_mask(limit.Value, blimit.Value, p3, p2, p1, p0, q0, q1, q2, q3);
            sbyte flat = flat_mask4(1, p3, p2, p1, p0, q0, q1, q2, q3);
            sbyte flat2 = flat_mask4(1, p6, p5, p4, p0, q0, q4, q5, q6);

            filter14(mask, thresh.Value, flat, flat2, ref s[-7], ref s[-6], ref s[-5],
                     ref s[-4], ref s[-3], ref s[-2], ref s[-1], ref s[0], ref s[1],
                     ref s[2], ref s[3], ref s[4], ref s[5], ref s[6]);
            s += p;
        }
    }

    // [port] Not executed in the coverage runs, but reached from filter_vert (USE_SINGLE,
    // filter_length 14), a data-dependent case; trivially small.
    internal static void aom_lpf_vertical_14_c(Ptr<byte> s, int p, Ptr<byte> blimit,
                                               Ptr<byte> limit, Ptr<byte> thresh)
    {
        mb_lpf_vertical_edge_w(s, p, blimit, limit, thresh, 4);
    }

    internal static void aom_lpf_vertical_14_dual_c(Ptr<byte> s, int pitch, Ptr<byte> blimit0,
                                                    Ptr<byte> limit0, Ptr<byte> thresh0,
                                                    Ptr<byte> blimit1, Ptr<byte> limit1,
                                                    Ptr<byte> thresh1)
    {
        mb_lpf_vertical_edge_w(s, pitch, blimit0, limit0, thresh0, 4);
        mb_lpf_vertical_edge_w(s + 4 * pitch, pitch, blimit1, limit1, thresh1, 4);
    }

    internal static void aom_lpf_vertical_14_quad_c(Ptr<byte> s, int pitch, Ptr<byte> blimit0,
                                                    Ptr<byte> limit0, Ptr<byte> thresh0)
    {
        mb_lpf_vertical_edge_w(s, pitch, blimit0, limit0, thresh0, 4);
        mb_lpf_vertical_edge_w(s + 4 * pitch, pitch, blimit0, limit0, thresh0, 4);
        mb_lpf_vertical_edge_w(s + 8 * pitch, pitch, blimit0, limit0, thresh0, 4);
        mb_lpf_vertical_edge_w(s + 12 * pitch, pitch, blimit0, limit0, thresh0, 4);
    }

    // [subset] highbd_* loop filters: CONFIG_AV1_HIGHBITDEPTH == 0.

    // ---- RTCD dispatch (aom_dsp_rtcd.h, pure-C build) --------------------------------------
    internal static void aom_lpf_horizontal_4(Ptr<byte> s, int pitch, Ptr<byte> blimit, Ptr<byte> limit, Ptr<byte> thresh) { if (Simd.UseAvx2) aom_lpf_horizontal_4_sse2(s, pitch, blimit, limit, thresh); else aom_lpf_horizontal_4_c(s, pitch, blimit, limit, thresh); }
    internal static void aom_lpf_horizontal_4_dual(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0, Ptr<byte> blimit1, Ptr<byte> limit1, Ptr<byte> thresh1) { if (Simd.UseAvx2) aom_lpf_horizontal_4_dual_sse2(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); else aom_lpf_horizontal_4_dual_c(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); }
    internal static void aom_lpf_horizontal_4_quad(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0) { if (Simd.UseAvx2) aom_lpf_horizontal_4_quad_sse2(s, pitch, blimit0, limit0, thresh0); else aom_lpf_horizontal_4_quad_c(s, pitch, blimit0, limit0, thresh0); }
    internal static void aom_lpf_horizontal_6(Ptr<byte> s, int pitch, Ptr<byte> blimit, Ptr<byte> limit, Ptr<byte> thresh) { if (Simd.UseAvx2) aom_lpf_horizontal_6_sse2(s, pitch, blimit, limit, thresh); else aom_lpf_horizontal_6_c(s, pitch, blimit, limit, thresh); }
    internal static void aom_lpf_horizontal_6_dual(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0, Ptr<byte> blimit1, Ptr<byte> limit1, Ptr<byte> thresh1) { if (Simd.UseAvx2) aom_lpf_horizontal_6_dual_sse2(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); else aom_lpf_horizontal_6_dual_c(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); }
    internal static void aom_lpf_horizontal_6_quad(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0) { if (Simd.UseAvx2) aom_lpf_horizontal_6_quad_avx2(s, pitch, blimit0, limit0, thresh0); else aom_lpf_horizontal_6_quad_c(s, pitch, blimit0, limit0, thresh0); }
    internal static void aom_lpf_horizontal_8(Ptr<byte> s, int pitch, Ptr<byte> blimit, Ptr<byte> limit, Ptr<byte> thresh) { if (Simd.UseAvx2) aom_lpf_horizontal_8_sse2(s, pitch, blimit, limit, thresh); else aom_lpf_horizontal_8_c(s, pitch, blimit, limit, thresh); }
    internal static void aom_lpf_horizontal_8_dual(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0, Ptr<byte> blimit1, Ptr<byte> limit1, Ptr<byte> thresh1) { if (Simd.UseAvx2) aom_lpf_horizontal_8_dual_sse2(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); else aom_lpf_horizontal_8_dual_c(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); }
    internal static void aom_lpf_horizontal_8_quad(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0) { if (Simd.UseAvx2) aom_lpf_horizontal_8_quad_avx2(s, pitch, blimit0, limit0, thresh0); else aom_lpf_horizontal_8_quad_c(s, pitch, blimit0, limit0, thresh0); }
    internal static void aom_lpf_horizontal_14(Ptr<byte> s, int pitch, Ptr<byte> blimit, Ptr<byte> limit, Ptr<byte> thresh) { if (Simd.UseAvx2) aom_lpf_horizontal_14_sse2(s, pitch, blimit, limit, thresh); else aom_lpf_horizontal_14_c(s, pitch, blimit, limit, thresh); }
    internal static void aom_lpf_horizontal_14_dual(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0, Ptr<byte> blimit1, Ptr<byte> limit1, Ptr<byte> thresh1) { if (Simd.UseAvx2) aom_lpf_horizontal_14_dual_sse2(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); else aom_lpf_horizontal_14_dual_c(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); }
    internal static void aom_lpf_horizontal_14_quad(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0) { if (Simd.UseAvx2) aom_lpf_horizontal_14_quad_avx2(s, pitch, blimit0, limit0, thresh0); else aom_lpf_horizontal_14_quad_c(s, pitch, blimit0, limit0, thresh0); }
    internal static void aom_lpf_vertical_4(Ptr<byte> s, int pitch, Ptr<byte> blimit, Ptr<byte> limit, Ptr<byte> thresh) { if (Simd.UseAvx2) aom_lpf_vertical_4_sse2(s, pitch, blimit, limit, thresh); else aom_lpf_vertical_4_c(s, pitch, blimit, limit, thresh); }
    internal static void aom_lpf_vertical_4_dual(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0, Ptr<byte> blimit1, Ptr<byte> limit1, Ptr<byte> thresh1) { if (Simd.UseAvx2) aom_lpf_vertical_4_dual_sse2(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); else aom_lpf_vertical_4_dual_c(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); }
    internal static void aom_lpf_vertical_4_quad(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0) { if (Simd.UseAvx2) aom_lpf_vertical_4_quad_sse2(s, pitch, blimit0, limit0, thresh0); else aom_lpf_vertical_4_quad_c(s, pitch, blimit0, limit0, thresh0); }
    internal static void aom_lpf_vertical_6(Ptr<byte> s, int pitch, Ptr<byte> blimit, Ptr<byte> limit, Ptr<byte> thresh) { if (Simd.UseAvx2) aom_lpf_vertical_6_sse2(s, pitch, blimit, limit, thresh); else aom_lpf_vertical_6_c(s, pitch, blimit, limit, thresh); }
    internal static void aom_lpf_vertical_6_dual(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0, Ptr<byte> blimit1, Ptr<byte> limit1, Ptr<byte> thresh1) { if (Simd.UseAvx2) aom_lpf_vertical_6_dual_sse2(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); else aom_lpf_vertical_6_dual_c(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); }
    internal static void aom_lpf_vertical_6_quad(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0) { if (Simd.UseAvx2) aom_lpf_vertical_6_quad_sse2(s, pitch, blimit0, limit0, thresh0); else aom_lpf_vertical_6_quad_c(s, pitch, blimit0, limit0, thresh0); }
    internal static void aom_lpf_vertical_8(Ptr<byte> s, int pitch, Ptr<byte> blimit, Ptr<byte> limit, Ptr<byte> thresh) { if (Simd.UseAvx2) aom_lpf_vertical_8_sse2(s, pitch, blimit, limit, thresh); else aom_lpf_vertical_8_c(s, pitch, blimit, limit, thresh); }
    internal static void aom_lpf_vertical_8_dual(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0, Ptr<byte> blimit1, Ptr<byte> limit1, Ptr<byte> thresh1) { if (Simd.UseAvx2) aom_lpf_vertical_8_dual_sse2(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); else aom_lpf_vertical_8_dual_c(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); }
    internal static void aom_lpf_vertical_8_quad(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0) { if (Simd.UseAvx2) aom_lpf_vertical_8_quad_sse2(s, pitch, blimit0, limit0, thresh0); else aom_lpf_vertical_8_quad_c(s, pitch, blimit0, limit0, thresh0); }
    internal static void aom_lpf_vertical_14(Ptr<byte> s, int pitch, Ptr<byte> blimit, Ptr<byte> limit, Ptr<byte> thresh) { if (Simd.UseAvx2) aom_lpf_vertical_14_sse2(s, pitch, blimit, limit, thresh); else aom_lpf_vertical_14_c(s, pitch, blimit, limit, thresh); }
    internal static void aom_lpf_vertical_14_dual(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0, Ptr<byte> blimit1, Ptr<byte> limit1, Ptr<byte> thresh1) { if (Simd.UseAvx2) aom_lpf_vertical_14_dual_sse2(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); else aom_lpf_vertical_14_dual_c(s, pitch, blimit0, limit0, thresh0, blimit1, limit1, thresh1); }
    internal static void aom_lpf_vertical_14_quad(Ptr<byte> s, int pitch, Ptr<byte> blimit0, Ptr<byte> limit0, Ptr<byte> thresh0) { if (Simd.UseAvx2) aom_lpf_vertical_14_quad_avx2(s, pitch, blimit0, limit0, thresh0); else aom_lpf_vertical_14_quad_c(s, pitch, blimit0, limit0, thresh0); }
}
