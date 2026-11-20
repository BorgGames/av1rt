using System.Runtime.InteropServices;
using System.Text;

namespace Av1rt.Tests.G1;

// libaom wrappers from tools/oracle/shim/shim_g1.c. A config is a list of (id, value) pairs
// applied on top of the realtime defaults: id < 1000 is an aom_codec_enc_cfg_t field
// (G1Fields.CfgSetNames), id >= 1000 an av1_extracfg field (G1Fields.ExtraSetNames).
internal static class G1Shim
{
    private const string Dll = "aomshim_g1";

    [DllImport(Dll)]
    public static extern int aomshim_g1_counts(int[] output);

    [DllImport(Dll)]
    public static extern int aomshim_g1_config_default(uint usage, long[] output);

    [DllImport(Dll)]
    private static extern int aomshim_g1_validate_config(long[] kv, int n, byte[] detail, int cap, out int hasDetail);

    [DllImport(Dll)]
    public static extern int aomshim_g1_set_encoder_config(long[] kv, int n, long[] output, long[] tiles);

    [DllImport(Dll)]
    public static extern int aomshim_g1_get_encoder_config(long[] kv, int n, long[] output);

    [DllImport(Dll)]
    public static extern int aomshim_g1_seq_params(long[] kv, int n, long[] output);

    [DllImport(Dll)]
    private static extern int aomshim_g1_enc_init(long[] kv, int n, byte[] detail, int cap, out int hasDetail);

    [DllImport(Dll)]
    public static extern int aomshim_g1_convert_sect5obus_to_annexb(byte[] buffer, int bufferSize, ref int frameSize);

    public static (int res, string detail) ValidateConfig(long[] kv)
    {
        var buf = new byte[512];
        int res = aomshim_g1_validate_config(kv, kv.Length / 2, buf, buf.Length, out int has);
        return (res, has != 0 ? Decode(buf) : null);
    }

    public static (int res, string detail) EncInit(long[] kv)
    {
        var buf = new byte[512];
        int res = aomshim_g1_enc_init(kv, kv.Length / 2, buf, buf.Length, out int has);
        return (res, has != 0 ? Decode(buf) : null);
    }

    private static string Decode(byte[] buf)
    {
        int len = System.Array.IndexOf(buf, (byte)0);
        return Encoding.ASCII.GetString(buf, 0, len < 0 ? buf.Length : len);
    }
}
