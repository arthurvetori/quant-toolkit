using ExcelDna.Integration;
using Quant.Excel.AddIn.Diagnostics;

namespace Quant.Excel.AddIn.Functions.Information;

public static class DiagnosticsFunctions
{
    [ExcelFunction(Name = "bLoggingStatus", Description = "Returns the current Quant diagnostics status without enabling logging.", IsThreadSafe = true)]
    public static string LoggingStatus() => DiagnosticManager.Current.Status.Message;
}
