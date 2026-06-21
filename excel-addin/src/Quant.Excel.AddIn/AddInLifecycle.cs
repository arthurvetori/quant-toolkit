using ExcelDna.Integration;

namespace Quant.Excel.AddIn;

public sealed class AddInLifecycle : IExcelAddIn
{
    public void AutoOpen()
    {
        AddInServices.Initialize();
    }

    public void AutoClose()
    {
        AddInServices.Reset();
    }
}
