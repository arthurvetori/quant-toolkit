namespace Quant.Core.Diagnostics;

public sealed class NullDiagnosticSink : IDiagnosticSink
{
    public static NullDiagnosticSink Instance { get; } = new();

    private NullDiagnosticSink()
    {
    }

    public bool IsEnabled => false;

    public DiagnosticStatus Status => DiagnosticStatus.Disabled;

    public bool TryWrite(in DiagnosticEvent diagnosticEvent) => false;
}
