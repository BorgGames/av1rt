using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Av1rt.Aom;

namespace Av1rt.Diagnostics;

/// <summary>
/// Experiment hook: AV1RT_SF="rt_sf.skip_cdef_sb=1,lpf_sf.cdef_pick_method=3" overrides speed
/// features after libaom's speed-feature setters ran. Not for production use.
/// </summary>
internal static class SpeedFeatureOverrides
{
    private const DynamicallyAccessedMemberTypes Fields =
        DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields;

    private static readonly (string Group, string Field, string Value)[] s_overrides = Parse();

    internal static bool Any => s_overrides.Length != 0;

    private static (string, string, string)[] Parse()
    {
        string spec = Environment.GetEnvironmentVariable("AV1RT_SF");
        if (string.IsNullOrWhiteSpace(spec)) return [];
        var list = new List<(string, string, string)>();
        foreach (string item in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = item.IndexOf('='), dot = item.IndexOf('.');
            if (eq < 0 || dot < 0 || dot > eq) throw new ArgumentException($"AV1RT_SF: bad item '{item}'");
            list.Add((item[..dot], item[(dot + 1)..eq], item[(eq + 1)..]));
        }
        return list.ToArray();
    }

    internal static void Apply(SPEED_FEATURES sf)
    {
        foreach (var (group, field, value) in s_overrides)
        {
            object target = group switch
            {
                "hl_sf" => sf.hl_sf, "part_sf" => sf.part_sf, "mv_sf" => sf.mv_sf,
                "inter_sf" => sf.inter_sf, "interp_sf" => sf.interp_sf, "intra_sf" => sf.intra_sf,
                "tx_sf" => sf.tx_sf, "rd_sf" => sf.rd_sf, "winner_mode_sf" => sf.winner_mode_sf,
                "lpf_sf" => sf.lpf_sf, "rt_sf" => sf.rt_sf,
                _ => throw new ArgumentException($"AV1RT_SF: unknown group '{group}'"),
            };
            FieldInfo f = group switch
            {
                "hl_sf" => Find<HIGH_LEVEL_SPEED_FEATURES>(field), "part_sf" => Find<PARTITION_SPEED_FEATURES>(field),
                "mv_sf" => Find<MV_SPEED_FEATURES>(field), "inter_sf" => Find<INTER_MODE_SPEED_FEATURES>(field),
                "interp_sf" => Find<INTERP_FILTER_SPEED_FEATURES>(field), "intra_sf" => Find<INTRA_MODE_SPEED_FEATURES>(field),
                "tx_sf" => Find<TX_SPEED_FEATURES>(field), "rd_sf" => Find<RD_CALC_SPEED_FEATURES>(field),
                "winner_mode_sf" => Find<WINNER_MODE_SPEED_FEATURES>(field), "lpf_sf" => Find<LOOP_FILTER_SPEED_FEATURES>(field),
                _ => Find<REAL_TIME_SPEED_FEATURES>(field),
            };
            Type t = f.FieldType;
            object v = t.IsEnum ? Enum.ToObject(t, long.Parse(value, CultureInfo.InvariantCulture))
                     : t == typeof(bool) ? value is "1" or "true"
                     : Convert.ChangeType(long.Parse(value, CultureInfo.InvariantCulture), t, CultureInfo.InvariantCulture);
            f.SetValue(target, v);
        }
    }

    private static FieldInfo Find<[DynamicallyAccessedMembers(Fields)] T>(string name) =>
        typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new ArgumentException($"AV1RT_SF: {typeof(T).Name} has no field '{name}'");
}
