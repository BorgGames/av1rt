// Port of libaom aom_dsp/entenc.h

global using static Av1rt.Aom.entenc;
global using od_ec_enc_window = ulong;

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

/*The entropy encoder context.*/
// [port] A class: it is only used through pointers (aom_writer.ec, &w->ec).
internal sealed partial class od_ec_enc
{
    /*Buffered output.
      This contains only the raw bits until the final call to od_ec_enc_done(),
       where all the arithmetic-coded data gets prepended to it.*/
    // [port] The allocation itself (malloc/realloc in entenc.c), so a plain array.
    public byte[] buf;
    /*The size of the buffer.*/
    public uint storage;
    /*The offset at which the next entropy-coded byte will be written.*/
    public uint offs;
    /*The low end of the current range.*/
    public od_ec_enc_window low;
    /*The number of values in the current range.*/
    public ushort rng;
    /*The number of bits of data in the current value.*/
    public short cnt;
    /*Nonzero if an error occurred.*/
    public int error;
}

/*The part of the encoder context that changes with every symbol.
  A caller that writes many symbols in a row can keep a copy of it in local
   variables, so it stays in registers: take it with od_ec_enc_get_state(),
   use the od_ec_encode_*_state() functions and put it back with
   od_ec_enc_set_state() before any other call on the encoder.*/
internal partial struct od_ec_enc_state
{
    /*The low end of the current range.*/
    public od_ec_enc_window low;
    /*The number of values in the current range.*/
    public uint rng;
    /*The number of bits of data in the current value.*/
    public int cnt;
}

internal static partial class entenc
{
    internal const int OD_MEASURE_EC_OVERHEAD = 0;

    // buf is the frame bitbuffer, offs is where carry to be added
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void propagate_carry_bwd(Ptr<byte> buf, uint offs)
    {
        ushort sum, carry = 1;
        do
        {
            sum = (ushort)(buf[(int)offs] + 1);
            buf[(int)offs--] = (byte)sum;
            carry = (ushort)(sum >> 8);
        } while (carry != 0);
    }

    // Convert to big-endian byte order and write data to buffer adding the
    // carry-bit
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void write_enc_data_to_out_buf(Ptr<byte> @out, uint offs, ulong output, ulong carry,
                                                   ref uint enc_offs, byte num_bytes_ready)
    {
        // [port] C: reg = HToBE64(output << ...); memcpy(&out[offs], &reg, 8). Byte swapping and
        // storing in host order is a big-endian store of the unswapped value.
        BinaryPrimitives.WriteUInt64BigEndian(@out.AsSpan((int)offs, 8),
                                              output << ((8 - num_bytes_ready) << 3));
        // Propagate carry backwards if exists
        if (carry != 0)
        {
            Debug.Assert(offs > 0);
            propagate_carry_bwd(@out, offs - 1);
        }
        enc_offs = offs + num_bytes_ready;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static od_ec_enc_state od_ec_enc_get_state(od_ec_enc enc)
    {
        od_ec_enc_state st;
        st.low = enc.low;
        st.rng = enc.rng;
        st.cnt = enc.cnt;
        return st;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void od_ec_enc_set_state(od_ec_enc enc, in od_ec_enc_state st)
    {
        enc.low = st.low;
        enc.rng = (ushort)st.rng;
        enc.cnt = (short)st.cnt;
    }

    /*Takes updated low and range values, renormalizes them so that
       32768 <= rng < 65536 (flushing bytes from low to the output buffer if
       necessary), and stores them back in the state.
      low: The new value of low.
      rng: The new value of the range.*/
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void od_ec_enc_normalize_state(od_ec_enc enc, ref od_ec_enc_state st,
                                                   od_ec_enc_window low, uint rng)
    {
        Debug.Assert(rng > 0 && rng <= 65535U);
        /*The number of leading zeros in the 16-bit binary representation of rng.
          get_msb(rng) is at most 15, so the xor is 15 - get_msb(rng).*/
        int d = 15 ^ get_msb(rng);
        int s = st.cnt + d;
        /*The limit is explained in od_ec_enc_flush().*/
        if (s >= 40)
        {
            low = od_ec_enc_flush(enc, low, st.cnt, s);
            /*The flush leaves s - 8 * ((s >> 3) + 1) bits.*/
            s = (s & 7) - 8;
        }
        st.low = low << d;
        st.rng = rng << d;
        st.cnt = s;
    }

    /*Encode a single binary value.
      val: The value to encode (0 or 1).
      f: The probability that the val is one, scaled by 32768.*/
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void od_ec_encode_bool_q15_state(od_ec_enc enc, ref od_ec_enc_state st, int val, uint f)
    {
        od_ec_enc_window l;
        uint r;
        uint v;
        Debug.Assert(0 < f);
        Debug.Assert(f < 32768U);
        l = st.low;
        r = st.rng;
        Debug.Assert(32768U <= r);
        v = ((r >> 8) * (uint)(f >> EC_PROB_SHIFT) >> (7 - EC_PROB_SHIFT));
        v += EC_MIN_PROB;
        if (val != 0) l += r - v;
        r = val != 0 ? v : r - v;
        od_ec_enc_normalize_state(enc, ref st, l, r);
    }

    /*Encodes a symbol given a cumulative distribution function (CDF) table in Q15,
       like od_ec_encode_cdf_q15().
      No branch on s, because coefficient levels are hard to predict.
      s: The index of the symbol to encode.
      icdf: 32768 minus the CDF, such that symbol s falls in the range
             [s > 0 ? (32768 - icdf[s - 1]) : 0, 32768 - icdf[s]).
            The values must be monotonically decreasing, and icdf[nsyms - 1] must
             be 0.
      nsyms: The number of symbols in the alphabet.
             This should be at most 16.*/
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void od_ec_encode_cdf_q15_state(od_ec_enc enc, ref od_ec_enc_state st, int s,
                                                    Ptr<ushort> icdf, int nsyms)
    {
        od_ec_encode_cdf_q15_state(enc, ref st, s, icdf.AsSpan(0, nsyms), nsyms);
    }

    // [port] icdf as a span of at least nsyms entries (callers pass the whole cdf, counter
    // included, and share the span with update_cdf).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void od_ec_encode_cdf_q15_state(od_ec_enc enc, ref od_ec_enc_state st, int s,
                                                    ReadOnlySpan<ushort> icdf, int nsyms)
    {
        uint r = st.rng;
        Debug.Assert(s >= 0);
        Debug.Assert(s < nsyms);
        Debug.Assert(icdf[nsyms - 1] == OD_ICDF(CDF_PROB_TOP));
        Debug.Assert(32768U <= r);
        Debug.Assert(7 - EC_PROB_SHIFT >= 0);
        int N = nsyms - 1;
        /*icdf[s - 1] for s > 0, and a valid entry to read for s == 0.*/
        uint fl = icdf[s - B(s > 0)];
        uint fh = icdf[s];
        uint r8 = r >> 8;
        uint pu = r8 * (uint)(fl >> EC_PROB_SHIFT) >> (7 - EC_PROB_SHIFT);
        uint pv = r8 * (uint)(fh >> EC_PROB_SHIFT) >> (7 - EC_PROB_SHIFT);
        /*s spans [u, v) below rng, with u = pu + EC_MIN_PROB * (N - s + 1) and
           v = pv + EC_MIN_PROB * (N - s). um is u without their common term. For
           s == 0 (or fl == CDF_PROB_TOP) u is rng, so low stays as it is. Doing
           the select first keeps the chain from rng to the new rng short.*/
        uint rk = r - (uint)(EC_MIN_PROB * (N - s));
        uint um = (s > 0 && fl < CDF_PROB_TOP) ? pu + EC_MIN_PROB : rk;
        od_ec_enc_normalize_state(enc, ref st, st.low + (rk - um), um - pv);
    }
}
