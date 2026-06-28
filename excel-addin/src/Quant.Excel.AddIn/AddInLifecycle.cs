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
        // matters here. DiagnosticManager.Stop() swaps the active sink to NullDiagnosticSink
        // immediately, so any error that occurs during or after this call is intentionally not
        // logged. Stopping diagnostics first (and flushing within its bounded timeout) avoids a
        // slow flush blocking Excel teardown, and avoids attempting to log against a runtime
        // that is itself mid-disposal.
        DiagnosticManager.Stop(TimeSpan.FromSeconds(2));
        AddInServices.Reset();
    }
}
