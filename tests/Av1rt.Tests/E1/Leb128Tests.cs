using System;
using System.Collections.Generic;
using Xunit;
using static Av1rt.Aom.aom_integer;

namespace Av1rt.Tests.E1;

/// <summary>aom_integer.c (leb128) against libaom.</summary>
public class Leb128Tests
{
    private static IEnumerable<ulong> Values()
    {
        for (ulong v = 0; v < 70000; v++) yield return v;
        for (int b = 0; b < 64; b++)
        {
            ulong p = 1UL << b;
            yield return p - 1;
            yield return p;
            yield return p + 1;
        }
        yield return uint.MaxValue;
        yield return (ulong)uint.MaxValue + 1;
        yield return ulong.MaxValue;
        var rng = new Random(1);
        for (int i = 0; i < 20000; i++) yield return (ulong)rng.NextInt64() >> rng.Next(64);
    }

    [Fact]
    public void SizeInBytesMatchesLibaom()
    {
        foreach (ulong v in Values())
            Assert.Equal(E1Shim.aomshim_e1_uleb_size_in_bytes(v), aom_uleb_size_in_bytes(v));
    }

    [Fact]
    public void EncodeMatchesLibaom()
    {
        foreach (ulong v in Values())
        {
            foreach (int available in new[] { 0, 1, 2, 3, 4, 5, 8, 9 })
            {
                var expected = new byte[10];
                var actual = new byte[10];
                int expectedSize = -5, actualSize = -5;
                int er = E1Shim.aomshim_e1_uleb_encode(v, available, expected, ref expectedSize);
                int ar = aom_uleb_encode(v, available, actual, ref actualSize);
                Assert.Equal(er, ar);
                Assert.Equal(expectedSize, actualSize);
                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void DecodeMatchesLibaom()
    {
        var rng = new Random(2);
        for (int iter = 0; iter < 50000; iter++)
        {
            var buf = new byte[10];
            rng.NextBytes(buf);
            // Terminate after a random number of bytes (or not at all).
            int len = rng.Next(1, 11);
            for (int i = 0; i < len - 1; i++) buf[i] |= 0x80;
            if (len < 10 && rng.Next(4) != 0) buf[len - 1] &= 0x7f;
            int available = rng.Next(0, 11);

            ulong ev = 12345, av = 12345;
            int el = -9, al = -9;
            int er = E1Shim.aomshim_e1_uleb_decode(buf, available, ref ev, ref el);
            int ar = aom_uleb_decode(buf, available, ref av, ref al);
            Assert.Equal(er, ar);
            Assert.Equal(ev, av);
            Assert.Equal(el, al);
        }
    }

    [Fact]
    public void RoundTrips()
    {
        foreach (ulong v in new ulong[] { 0, 1, 127, 128, 16383, 16384, uint.MaxValue })
        {
            var buf = new byte[8];
            int size = 0;
            Assert.Equal(0, aom_uleb_encode(v, buf.Length, buf, ref size));
            ulong decoded = 0;
            int length = 0;
            Assert.Equal(0, aom_uleb_decode(buf, size, ref decoded, ref length));
            Assert.Equal(v, decoded);
            Assert.Equal(size, length);
        }
    }
}
