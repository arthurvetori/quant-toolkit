using Xunit;

namespace Quant.Excel.AddIn.Tests.Architecture;

public sealed class ProjectBoundaryTests
{
    [Fact]
    public void AssembliesUseQuantNames()
    {
        Assert.Equal("Quant.Core", typeof(Quant.Core.AssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("Quant.QuantLib", typeof(Quant.QuantLib.AssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("Quant.Infrastructure", typeof(Quant.Infrastructure.AssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("Quant.Excel.AddIn", typeof(Quant.Excel.AddIn.AssemblyMarker).Assembly.GetName().Name);
    }
}
