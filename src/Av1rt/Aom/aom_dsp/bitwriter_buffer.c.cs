// Port of libaom aom_dsp/bitwriter_buffer.c

using System.Diagnostics;

namespace Av1rt.Aom;

// [subset] aom_wb_write_unsigned_literal, aom_wb_write_uvlc and the
// primitive_quniform/subexpfin/refsubexpfin writers never run in the subset and are not ported.
internal static partial class bitwriter_buffer
{
    internal static int aom_wb_is_byte_aligned(in aom_write_bit_buffer wb)
    {
        return B(wb.bit_offset % CHAR_BIT == 0);
    }

    internal static uint aom_wb_bytes_written(in aom_write_bit_buffer wb)
    {
        return wb.bit_offset / CHAR_BIT + (uint)B(wb.bit_offset % CHAR_BIT > 0);
    }

    internal static void aom_wb_write_bit(ref aom_write_bit_buffer wb, int bit)
    {
        int off = (int)wb.bit_offset;
        int p = off / CHAR_BIT;
        int q = CHAR_BIT - 1 - off % CHAR_BIT;
        if (q == CHAR_BIT - 1)
        {
            // Zero next char and write bit
            wb.bit_buffer[p] = (byte)(bit << q);
        }
        else
        {
            wb.bit_buffer[p] &= (byte)~(1 << q);
            wb.bit_buffer[p] |= (byte)(bit << q);
        }
        wb.bit_offset = (uint)(off + 1);
    }

    private static void overwrite_bit(ref aom_write_bit_buffer wb, int bit)
    {
        // Do not zero bytes but overwrite exisiting values
        int off = (int)wb.bit_offset;
        int p = off / CHAR_BIT;
        int q = CHAR_BIT - 1 - off % CHAR_BIT;
        wb.bit_buffer[p] &= (byte)~(1 << q);
        wb.bit_buffer[p] |= (byte)(bit << q);
        wb.bit_offset = (uint)(off + 1);
    }

    internal static void aom_wb_write_literal(ref aom_write_bit_buffer wb, int data, int bits)
    {
        Debug.Assert(bits <= 31);
        int bit;
        for (bit = bits - 1; bit >= 0; bit--) aom_wb_write_bit(ref wb, (data >> bit) & 1);
    }

    internal static void aom_wb_overwrite_literal(ref aom_write_bit_buffer wb, int data, int bits)
    {
        int bit;
        for (bit = bits - 1; bit >= 0; bit--) overwrite_bit(ref wb, (data >> bit) & 1);
    }

    internal static void aom_wb_write_inv_signed_literal(ref aom_write_bit_buffer wb, int data, int bits)
    {
        aom_wb_write_literal(ref wb, data, bits + 1);
    }
}
