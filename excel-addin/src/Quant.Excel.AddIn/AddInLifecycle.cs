using ExcelDna.Integration;
using Quant.Excel.AddIn.Diagnostics;

namespace Quant.Excel.AddIn;

public sealed class AddInLifecycle : IExcelAddIn
{
    public void AutoOpen()
    {
        AddInServices.Initialize();
    }

    public void AutoClose()
    {
        // Stop diagnostics (bounded flush) before tearing down the QuantLib runtime: these are
        // two independent concerns (diagnostics-app vs QuantLib-runtime lifecycle), but ordering
        // matters here because in-flight calculations during shutdown should still be able to
        // submit a final diagnostic event to a sink that hasn't been swapped out yet.
        DiagnosticManager.Stop(TimeSpan.FromSeconds(2));
        AddInServices.Reset();
    }
}
