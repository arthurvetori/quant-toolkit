using Xunit;

namespace Quant.Core.Tests.Architecture;

public sealed class AssemblyMarkerTests
{
    [Fact]
    public void CoreAssemblyUsesQuantName()
    {
        Assert.Equal("Quant.Core", typeof(Quant.Core.AssemblyMarker).Assembly.GetName().Name);
    }
}
