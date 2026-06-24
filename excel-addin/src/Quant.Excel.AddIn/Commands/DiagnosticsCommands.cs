using ExcelDna.Integration;
using Quant.Excel.AddIn.Diagnostics;

namespace Quant.Excel.AddIn.Commands;

public static class DiagnosticsCommands
{
    [ExcelCommand(Name = "bLoggingStart", Description = "Starts non-blocking Quant error diagnostics for this Excel session.")]
    public static void StartLogging() => DiagnosticManager.Start();

    [ExcelCommand(Name = "bLoggingStop", Description = "Stops Quant diagnostics and performs a bounded flush.")]
    public static void StopLogging() => DiagnosticManager.Stop(TimeSpan.FromSeconds(2));
}
