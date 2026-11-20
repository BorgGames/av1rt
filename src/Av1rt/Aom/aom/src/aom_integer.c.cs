// Port of libaom aom/src/aom_integer.c

using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class aom_integer
{
    private const int kMaximumLeb128Size = 8;
    private const byte kLeb128ByteMask = 0x7f;  // Binary: 01111111

    // Disallow values larger than 32-bits to ensure consistent behavior on 32 and
    // 64 bit targets: value is typically used to determine buffer allocation size
    // when decoded.
    private const ulong kMaximumLeb128Value = uint.MaxValue;

    internal static int aom_uleb_size_in_bytes(ulong value)
    {
        int size = 0;
        do
        {
            ++size;
        } while ((value >>= 7) != 0);
        return size;
    }

    // [port] value and length are `uint64_t *` / `size_t *` out-parameters in C, ref here
    // (PORTING 4); the encoder always passes both, so C's NULL checks of them are always true.
    internal static int aom_uleb_decode(Ptr<byte> buffer, int available, ref ulong value,
                                        ref int length)
    {
        if (!buffer.IsNull)
        {
            value = 0;
            for (int i = 0; i < kMaximumLeb128Size && i < available; ++i)
            {
                byte decoded_byte = (byte)(buffer[i] & kLeb128ByteMask);
                value |= ((ulong)decoded_byte) << (i * 7);
                if ((buffer[i] >> 7) == 0)
                {
                    length = i + 1;

                    // Fail on values larger than 32-bits to ensure consistent behavior on
                    // 32 and 64 bit targets: value is typically used to determine buffer
                    // allocation size.
                    if (value > uint.MaxValue) return -1;

                    return 0;
                }
            }
        }

        // If we get here, either the buffer/value pointers were invalid,
        // or we ran over the available space
        return -1;
    }

    // [port] coded_size is a `size_t *` out-parameter in C, ref here (never NULL).
    internal static int aom_uleb_encode(ulong value, int available, Ptr<byte> coded_value,
                                        ref int coded_size)
    {
        int leb_size = aom_uleb_size_in_bytes(value);
        if (value > kMaximumLeb128Value || leb_size > kMaximumLeb128Size ||
            leb_size > available || coded_value.IsNull)
        {
            return -1;
        }

        for (int i = 0; i < leb_size; ++i)
        {
            byte @byte = (byte)(value & 0x7f);
            value >>= 7;

            if (value != 0) @byte |= 0x80;  // Signal that more bytes follow.

            coded_value[i] = @byte;
        }

        coded_size = leb_size;
        return 0;
    }

    // [subset] aom_uleb_encode_fixed_size: never executed (no caller in the encoder).
}
