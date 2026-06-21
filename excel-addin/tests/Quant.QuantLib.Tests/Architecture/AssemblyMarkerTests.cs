using Xunit;

namespace Quant.QuantLib.Tests.Architecture;

public sealed class AssemblyMarkerTests
{
    [Fact]
    public void QuantLibAssemblyUsesQuantName()
    {
        Assert.Equal("Quant.QuantLib", typeof(Quant.QuantLib.AssemblyMarker).Assembly.GetName().Name);
    }
}
