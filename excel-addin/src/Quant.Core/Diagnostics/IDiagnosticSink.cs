namespace Quant.Core.Diagnostics;

public interface IDiagnosticSink
{
    bool IsEnabled { get; }
    DiagnosticStatus Status { get; }
    bool TryWrite(in DiagnosticEvent diagnosticEvent);
}
