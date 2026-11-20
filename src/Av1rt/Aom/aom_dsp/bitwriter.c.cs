// Port of libaom aom_dsp/bitwriter.c

namespace Av1rt.Aom;

internal static partial class bitwriter
{
    internal static void aom_start_encode(aom_writer w, Ptr<byte> source, int size)
    {
        w.buffer = source;
        w.size = size;
        w.pos = 0;
        od_ec_enc_init(w.ec, 62025);
    }

    // Returns a negative number on error. Caller must check the return value and
    // handle error.
    internal static int aom_stop_encode(aom_writer w)
    {
        int nb_bits;
        uint bytes = 0;
        Ptr<byte> data;
        data = od_ec_enc_done(w.ec, ref bytes);
        if (data.IsNull || bytes > (uint)w.size)
        {
            od_ec_enc_clear(w.ec);
            return -1;
        }
        nb_bits = od_ec_enc_tell(w.ec);
        memcpy(w.buffer, data, (int)bytes);
        w.pos = bytes;
        od_ec_enc_clear(w.ec);
        return nb_bits;
    }

    internal static int aom_tell_size(aom_writer w)
    {
        int nb_bits = od_ec_enc_tell(w.ec);
        return nb_bits;
    }
}
