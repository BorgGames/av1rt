// Port of libaom aom_dsp/bitwriter_buffer.h

global using static Av1rt.Aom.bitwriter_buffer;

namespace Av1rt.Aom;

// [port] A struct: bitstream.c initializes it with `{ data, 0 }` and saves/restores copies
// (`saved_wb = *wb`). Functions taking `struct aom_write_bit_buffer *` take `ref`, the const
// ones take `in`.
internal partial struct aom_write_bit_buffer
{
    public Ptr<byte> bit_buffer;
    public uint bit_offset;

    // `struct aom_write_bit_buffer wb = { bit_buffer, bit_offset };`
    public aom_write_bit_buffer(Ptr<byte> bit_buffer, uint bit_offset)
    {
        this.bit_buffer = bit_buffer;
        this.bit_offset = bit_offset;
    }
}

internal static partial class bitwriter_buffer
{
    // [port] limits.h CHAR_BIT, used with the bit buffer here and in bitstream.c.
    internal const int CHAR_BIT = 8;
}
