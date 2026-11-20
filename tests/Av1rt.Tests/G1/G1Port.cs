using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.aom_encoder;
using static Av1rt.Aom.av1_cx_iface;

namespace Av1rt.Tests.G1;

// The C# side of the g1 tests: the same (id, value) configs as G1Shim, applied to the port.
// validate_config, set_encoder_config and default_extra_cfg are private to av1_cx_iface (C
// file-statics), so they are bound through reflection.
internal static class G1Port
{
    private delegate int ValidateFn(aom_codec_alg_priv_t ctx, aom_codec_enc_cfg_t cfg, in av1_extracfg extra);

    private delegate void SetEncoderConfigFn(AV1EncoderConfig oxcf, aom_codec_enc_cfg_t cfg, ref av1_extracfg extra);

    private static readonly ValidateFn validate_config = Bind<ValidateFn>("validate_config");
    private static readonly SetEncoderConfigFn set_encoder_config = Bind<SetEncoderConfigFn>("set_encoder_config");

    public static readonly av1_extracfg DefaultExtraCfg =
        (av1_extracfg)typeof(av1_cx_iface).GetField("default_extra_cfg", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);

    public delegate void InitSeqCodingToolsFn(AV1_PRIMARY ppi, AV1EncoderConfig oxcf, int disable_frame_id_numbers);

    // encoder.c: static void init_seq_coding_tools(AV1_PRIMARY *, const AV1EncoderConfig *, int).
    public static readonly InitSeqCodingToolsFn InitSeqCodingTools =
        Bind<InitSeqCodingToolsFn>("init_seq_coding_tools", typeof(encoder));

    private static T Bind<T>(string name, Type type = null) where T : Delegate
    {
        MethodInfo m = (type ?? typeof(av1_cx_iface)).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(m);
        return (T)m.CreateDelegate(typeof(T));
    }

    // ---- config ids ----

    public static long Id(string name)
    {
        int c = Array.IndexOf(G1Fields.CfgSetNames, name);
        int x = Array.IndexOf(G1Fields.ExtraSetNames, name);
        if (c >= 0 && x >= 0) throw new ArgumentException("ambiguous field " + name);
        if (c >= 0) return c;
        if (x >= 0) return 1000 + x;
        throw new ArgumentException("unknown field " + name);
    }

    public static string Name(long id) => id < 1000 ? G1Fields.CfgSetNames[id] : G1Fields.ExtraSetNames[id - 1000];

    // Kv("g_w", 640, "g_h", 480, ...) -> flat (id, value) pairs.
    public static long[] Kv(params object[] nameValue)
    {
        var kv = new long[nameValue.Length];
        for (int i = 0; i < nameValue.Length; i += 2)
        {
            kv[i] = Id((string)nameValue[i]);
            kv[i + 1] = Convert.ToInt64(nameValue[i + 1]);
        }
        return kv;
    }

    public static long[] Concat(params long[][] parts)
    {
        var all = new List<long>();
        foreach (long[] p in parts) all.AddRange(p);
        return all.ToArray();
    }

    public static string Describe(long[] kv)
    {
        var sb = new StringBuilder("{");
        for (int i = 0; i < kv.Length; i += 2) sb.Append(' ').Append(Name(kv[i])).Append('=').Append(kv[i + 1]);
        return sb.Append(" }").ToString();
    }

    // ---- port wrappers ----

    public static (aom_codec_enc_cfg_t cfg, av1_extracfg extra) MakeConfig(long[] kv)
    {
        var cfg = new aom_codec_enc_cfg_t();
        Assert.Equal(0, aom_codec_enc_config_default(aom_codec_av1_cx(), cfg, AOM_USAGE_REALTIME));
        av1_extracfg extra = DefaultExtraCfg;
        for (int i = 0; i < kv.Length; i += 2)
        {
            int id = (int)kv[i];
            if (id < 1000) G1Fields.SetCfg(cfg, id, kv[i + 1]);
            else G1Fields.SetExtra(ref extra, id - 1000, kv[i + 1]);
        }
        return (cfg, extra);
    }

    public static (int res, string detail) ValidateConfig(long[] kv)
    {
        var (cfg, extra) = MakeConfig(kv);
        var ctx = new aom_codec_alg_priv_t();
        int res = validate_config(ctx, cfg, in extra);
        return (res, ctx.err_detail);
    }

    public static AV1EncoderConfig SetEncoderConfig(long[] kv, out av1_extracfg extraAfter)
    {
        var (cfg, extra) = MakeConfig(kv);
        var oxcf = new AV1EncoderConfig();
        set_encoder_config(oxcf, cfg, ref extra);
        extraAfter = extra;
        return oxcf;
    }

    // ---- comparison ----

    public static void AssertDumpEqual(string what, string[] names, long[] expected, int expectedCount,
                                       long[] actual, int actualCount, string context)
    {
        Assert.Equal(names.Length, expectedCount);
        Assert.Equal(names.Length, actualCount);
        var diffs = new List<string>();
        for (int i = 0; i < names.Length; i++)
        {
            if (expected[i] != actual[i]) diffs.Add($"{names[i]}: libaom {expected[i]}, port {actual[i]}");
        }
        if (diffs.Count > 0)
            Assert.Fail($"{what} differs for {context}:\n  " + string.Join("\n  ", diffs));
    }
}

// A NotImplementedException thrown by a placeholder of src/Av1rt/Aom/_stubs (a function of
// another package that this branch does not port yet).
internal static class PendingStub
{
    public static bool Is(Exception e) =>
        e is NotImplementedException && e.TargetSite?.DeclaringType?.Name.StartsWith("stubs_", StringComparison.Ordinal) == true;

    public static string Name(Exception e) => $"{e.TargetSite?.DeclaringType?.Name}.{e.TargetSite?.Name}";
}
