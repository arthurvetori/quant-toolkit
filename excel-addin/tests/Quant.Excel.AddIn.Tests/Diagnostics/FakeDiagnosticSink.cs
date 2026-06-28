using System.Collections.Concurrent;
using Quant.Core.Diagnostics;

namespace Quant.Excel.AddIn.Tests.Diagnostics;

/// <summary>
/// In-memory <see cref="IDiagnosticSink"/> used to assert exactly which events the Excel error
/// boundary submits, without touching the file system or a background worker.
/// </summary>
internal sealed class FakeDiagnosticSink : IDiagnosticSink
{
    private readonly ConcurrentQueue<DiagnosticEvent> _events = new();

    public bool IsEnabled { get; set; } = true;

    public DiagnosticStatus Status => new(IsEnabled, "Fake", 0);

    public IReadOnlyCollection<DiagnosticEvent> Events => _events;

    public bool TryWrite(in DiagnosticEvent diagnosticEvent)
    {
        if (!IsEnabled)
        {
            return false;
        }

        _events.Enqueue(diagnosticEvent);
        return true;
    }
}
