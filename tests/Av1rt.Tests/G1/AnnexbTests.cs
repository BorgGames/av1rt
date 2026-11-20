using System;
using System.Collections.Generic;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.encoder;

namespace Av1rt.Tests.G1;

/// <summary>av1_convert_sect5obus_to_annexb (encoder.c) against libaom.</summary>
[Trait("Category", "Native")]
public class AnnexbTests
{
    // A section 5 temporal unit: OBUs with obu_has_size_field, random extension headers,
    // payload sizes and (possibly non-minimal) leb128 size encodings.
    private static List<byte> RandomObus(Random rng, out bool truncatedLeb)
    {
        var data = new List<byte>();
        int obus = 1 + rng.Next(6);
        truncatedLeb = false;
        for (int o = 0; o < obus; o++)
        {
            bool ext = rng.Next(3) == 0;
            int type = 1 + rng.Next(8);
            data.Add((byte)((type << 3) | (ext ? 0x4 : 0) | 0x2 | rng.Next(2)));
            if (ext) data.Add((byte)rng.Next(256));

            if (o == obus - 1 && rng.Next(12) == 0)
            {
                // Size field without a terminating byte: aom_uleb_decode fails.
                truncatedLeb = true;
                int n = 1 + rng.Next(3);
                for (int i = 0; i < n; i++) data.Add(0x80);
                break;
            }

            int payload = rng.Next(5) switch
            {
                0 => rng.Next(4),
                1 => 120 + rng.Next(16),
                2 => 16370 + rng.Next(30),
                _ => rng.Next(400),
            };
            int pad = rng.Next(4) == 0 ? rng.Next(4) : 0;
            ulong value = (ulong)payload;
            int minimal = 1;
            while ((value >> (7 * minimal)) != 0) minimal++;
            int len = Math.Min(minimal + pad, 8);
            for (int i = 0; i < len; i++)
            {
                byte b = (byte)((value >> (7 * i)) & 0x7f);
                if (i < len - 1) b |= 0x80;
                data.Add(b);
            }
            for (int i = 0; i < payload; i++) data.Add((byte)rng.Next(256));
        }
        return data;
    }

    [Fact]
    public void RandomTemporalUnitsMatchLibaom()
    {
        var rng = new Random(77);
        int ok = 0, errors = 0;
        for (int iter = 0; iter < 4000; iter++)
        {
            List<byte> obus = RandomObus(rng, out _);
            int frameSize = obus.Count;
            int slack = rng.Next(3) == 0 ? 0 : rng.Next(24);
            int bufferSize = frameSize + slack;
            var expected = new byte[bufferSize];
            obus.CopyTo(expected);
            for (int i = frameSize; i < bufferSize; i++) expected[i] = (byte)rng.Next(256);
            var actual = (byte[])expected.Clone();

            int expectedSize = frameSize;
            int eRes = G1Shim.aomshim_g1_convert_sect5obus_to_annexb(expected, bufferSize, ref expectedSize);
            int actualSize = frameSize;
            int aRes = av1_convert_sect5obus_to_annexb(actual, bufferSize, ref actualSize);

            Assert.Equal(eRes, aRes);
            Assert.Equal(expectedSize, actualSize);
            Assert.Equal(expected, actual);
            if (eRes == 0) ok++;
            else errors++;
        }
        Assert.True(ok > 1000 && errors > 100, $"ok {ok}, errors {errors}");
    }

    [Fact]
    public void EmptyFrameIsUnchanged()
    {
        var buf = new byte[4];
        int size = 0;
        Assert.Equal(0, av1_convert_sect5obus_to_annexb(buf, buf.Length, ref size));
        Assert.Equal(0, size);
        int cSize = 0;
        Assert.Equal(0, G1Shim.aomshim_g1_convert_sect5obus_to_annexb(new byte[4], 4, ref cSize));
        Assert.Equal(0, cSize);
    }
}
