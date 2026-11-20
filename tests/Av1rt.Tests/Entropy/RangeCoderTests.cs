using System;
using Av1rt.Aom;
using Av1rt.Tests.Shims;
using Xunit;
using static Av1rt.Aom.entenc;

namespace Av1rt.Tests.Entropy;

public class RangeCoderTests
{
    [Theory]
    [InlineData(2, 1, 100)]
    [InlineData(4, 2, 5000)]
    [InlineData(8, 3, 20000)]
    [InlineData(16, 4, 100000)]
    public void OdEcMatchesLibaom(int nsyms, int seed, int count)
    {
        var rng = new Random(seed);
        // A random monotonically decreasing inverse CDF ending in 0, plus the C counter slot.
        var cuts = new int[nsyms - 1];
        for (int i = 0; i < cuts.Length; i++) cuts[i] = rng.Next(1, 32768);
        Array.Sort(cuts);
        var icdf = new ushort[nsyms + 1];
        for (int i = 0; i < nsyms - 1; i++) icdf[i] = (ushort)(32768 - cuts[i]);
        icdf[nsyms - 1] = 0;

        var symbols = new int[count];
        for (int i = 0; i < count; i++) symbols[i] = rng.Next(nsyms);

        var expected = new byte[count * 2 + 64];
        int expectedLength = SmokeShim.aomshim_smoke_ec(symbols, count, icdf, nsyms, expected, expected.Length);
        Assert.True(expectedLength > 0);

        var enc = new od_ec_enc();
        od_ec_enc_init(enc, 64);
        foreach (int s in symbols) od_ec_encode_cdf_q15(enc, s, icdf, nsyms);
        uint n = 0;
        Ptr<byte> buf = od_ec_enc_done(enc, ref n);

        Assert.Equal(expectedLength, (int)n);
        Assert.True(buf.AsSpan((int)n).SequenceEqual(expected.AsSpan(0, expectedLength)));
    }
}
