using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Av1rt.Aom;
using Xunit;

namespace Av1rt.Tests.E1;

/// <summary>
/// Maps a C# <see cref="FRAME_CONTEXT"/> to the memory image of the C struct: every CDF array
/// in declaration order (all aom_cdf_prob, so no padding between them), then the int
/// `initialized` at the next 4-byte boundary. Reflection walks the fields in metadata
/// (= declaration) order; the struct size is checked against sizeof(FRAME_CONTEXT) from the shim.
/// </summary>
internal sealed class FrameContextImage
{
    /// <summary>One innermost CDF row of the struct and its C field path.</summary>
    internal readonly record struct Row(string Path, ushort[] Cdf);

    public readonly List<Row> Rows = new();
    public readonly int InitializedOffset;
    public readonly int Size;
    private readonly FRAME_CONTEXT _fc;

    public FrameContextImage(FRAME_CONTEXT fc)
    {
        _fc = fc;
        Walk(fc, "fc", Rows);
        int cdfBytes = Rows.Sum(r => r.Cdf.Length) * 2;
        InitializedOffset = (cdfBytes + 3) & ~3;
        Size = InitializedOffset + 4;
        Assert.Equal(E1Shim.aomshim_e1_frame_context_size(), Size);
    }

    private static void Walk(object o, string path, List<Row> rows)
    {
        foreach (FieldInfo f in o.GetType()
                     .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .OrderBy(f => f.MetadataToken))
        {
            if (f.FieldType == typeof(int)) continue;  // FRAME_CONTEXT.initialized, handled apart
            AddValue(f.GetValue(o), path + "." + f.Name, rows);
        }
    }

    private static void AddValue(object v, string path, List<Row> rows)
    {
        switch (v)
        {
            case ushort[] cdf:
                rows.Add(new Row(path, cdf));
                break;
            case Array a:
                for (int i = 0; i < a.Length; i++) AddValue(a.GetValue(i), $"{path}[{i}]", rows);
                break;
            default:
                Walk(v, path, rows);  // nmv_context, nmv_component, segmentation_probs
                break;
        }
    }

    public byte[] ToBytes()
    {
        var b = new byte[Size];
        int pos = 0;
        foreach (Row r in Rows)
            foreach (ushort x in r.Cdf)
            {
                BitConverter.TryWriteBytes(b.AsSpan(pos, 2), x);
                pos += 2;
            }
        BitConverter.TryWriteBytes(b.AsSpan(InitializedOffset, 4), _fc.initialized);
        return b;
    }

    public void FromBytes(byte[] b)
    {
        int pos = 0;
        foreach (Row r in Rows)
            for (int i = 0; i < r.Cdf.Length; i++)
            {
                r.Cdf[i] = BitConverter.ToUInt16(b, pos);
                pos += 2;
            }
        _fc.initialized = BitConverter.ToInt32(b, InitializedOffset);
    }

    /// <summary>Asserts that a C image equals this context, naming the first differing CDF.</summary>
    public void AssertEqualTo(byte[] c)
    {
        Assert.Equal(Size, c.Length);
        int pos = 0;
        foreach (Row r in Rows)
        {
            for (int i = 0; i < r.Cdf.Length; i++)
            {
                ushort expected = BitConverter.ToUInt16(c, pos + 2 * i);
                if (expected != r.Cdf[i])
                    Assert.Fail($"{r.Path}[{i}]: libaom {expected}, port {r.Cdf[i]}");
            }
            pos += 2 * r.Cdf.Length;
        }
        Assert.Equal(BitConverter.ToInt32(c, InitializedOffset), _fc.initialized);
    }

    /// <summary>A C FRAME_CONTEXT image whose CDF part and `initialized` are zero.</summary>
    public byte[] NewCImage() => new byte[Size];
}
