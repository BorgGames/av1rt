using System.Runtime.InteropServices;
using Xunit;

namespace Av1rt.Tests.Shims;

internal static class SmokeShim
{
    private const string Dll = "aomshim_smoke";

    [DllImport(Dll)]
    public static extern int aomshim_smoke_get_msb(uint n);
}

public class SmokeShimTests
{
    [Fact]
    public void ShimLoadsAndCallsLibaom()
    {
        Assert.Equal(10, SmokeShim.aomshim_smoke_get_msb(1024));
    }
}
