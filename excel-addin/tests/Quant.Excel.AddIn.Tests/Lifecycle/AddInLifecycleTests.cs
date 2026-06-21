using ExcelDna.Integration;
using Xunit;

namespace Quant.Excel.AddIn.Tests.Lifecycle;

public sealed class AddInLifecycleTests
{
    [Fact]
    public void LifecycleImplementsExcelDnaContract()
    {
        var lifecycle = new AddInLifecycle();

        Assert.IsAssignableFrom<IExcelAddIn>(lifecycle);
        lifecycle.AutoOpen();
        lifecycle.AutoClose();
    }
}
