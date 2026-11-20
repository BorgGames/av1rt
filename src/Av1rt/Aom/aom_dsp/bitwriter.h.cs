// Port of libaom aom_dsp/bitwriter.h

global using static Av1rt.Aom.bitwriter;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] A class: callers hold it through pointers (aom_writer *w) and bitstream.c declares it
// as a local (`aom_writer mode_bc;` becomes `aom_writer mode_bc = new();`). The embedded
// od_ec_enc is allocated with it.
internal sealed partial class aom_writer
{
    public uint pos;
    public Ptr<byte> buffer;
    public int size;
    public readonly od_ec_enc ec = new();
    public byte allow_update_cdf;
}

// [port] CONFIG_RD_DEBUG is 0, so only the cost field exists. A struct: callers declare it as a
// local and pass its address.
internal partial struct TOKEN_STATS
{
    public int cost;
}

internal static partial class bitwriter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void init_token_stats(ref TOKEN_STATS token_stats)
    {
        token_stats.cost = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_write(aom_writer w, int bit, int probability)
    {
        int p = (0x7FFFFF - (probability << 15) + probability) >> 8;
        od_ec_encode_bool_q15(w.ec, bit, (uint)p);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_write_bit(aom_writer w, int bit)
    {
        aom_write(w, bit, 128);  // aom_prob_half
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_write_literal(aom_writer w, int data, int bits)
    {
        int bit;
        for (bit = bits - 1; bit >= 0; bit--) aom_write_bit(w, 1 & (data >> bit));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_write_cdf(aom_writer w, int symb, Ptr<aom_cdf_prob> cdf, int nsymbs)
    {
        od_ec_encode_cdf_q15(w.ec, symb, cdf, nsymbs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_write_symbol(aom_writer w, int symb, Ptr<aom_cdf_prob> cdf, int nsymbs)
    {
        aom_write_cdf(w, symb, cdf, nsymbs);
        if (w.allow_update_cdf != 0) update_cdf(cdf, symb, nsymbs);
    }

    // Same as aom_write_symbol() and aom_write_bit(), but use the coder state in
    // *s. See od_ec_enc_state.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_write_symbol_state(aom_writer w, ref od_ec_enc_state s, int symb,
                                                Ptr<aom_cdf_prob> cdf, int nsymbs)
    {
        // [port] One span of the cdf and its counter for both the coder and the update.
        Span<aom_cdf_prob> c = cdf.AsSpan(0, nsymbs + 1);
        od_ec_encode_cdf_q15_state(w.ec, ref s, symb, c, nsymbs);
        if (w.allow_update_cdf != 0) update_cdf(c, symb);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_write_bit_state(aom_writer w, ref od_ec_enc_state s, int bit)
    {
        const int p = CDF_PROB_TOP / 2;
        od_ec_encode_bool_q15_state(w.ec, ref s, bit, p);
    }
}
