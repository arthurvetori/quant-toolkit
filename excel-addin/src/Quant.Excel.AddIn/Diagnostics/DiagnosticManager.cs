using Quant.Core.Diagnostics;
using Quant.Infrastructure.Diagnostics;

namespace Quant.Excel.AddIn.Diagnostics;

/// <summary>
/// Owns the single, process-wide opt-in diagnostics sink used by the Excel add-in. Starts
/// disabled (<see cref="NullDiagnosticSink"/>) so calculations never pay any diagnostics cost
/// unless a user explicitly runs <c>bLoggingStart</c>. Start/stop are rare, lock-guarded
/// operations; <see cref="Current"/> is read on every error path and therefore never takes a
/// lock, publishing/observing the active sink with <see cref="Volatile"/>.
/// </summary>
internal static class DiagnosticManager
{
    private static readonly object Gate = new();
    private static IDiagnosticSink _current = NullDiagnosticSink.Instance;

    internal static IDiagnosticSink Current => Volatile.Read(ref _current);

    /// <summary>
    /// Starts diagnostics if not already enabled. Idempotent: a second call while already
    /// enabled is a no-op.
    /// </summary>
    internal static void Start()
    {
        lock (Gate)
        {
            if (_current.IsEnabled)
            {
                return;
            }

            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Quant",
                "Logs");

            Volatile.Write(ref _current, AsyncFileDiagnosticSink.Start(directory));
        }
    }

    /// <summary>
    /// Stops diagnostics, if enabled, performing a bounded flush within <paramref name="timeout"/>.
    /// Idempotent: a call while already disabled is a no-op.
    /// </summary>
    internal static void Stop(TimeSpan timeout)
    {
        IDiagnosticSink previous;

        lock (Gate)
        {
            if (!_current.IsEnabled)
            {
                return;
            }

            previous = _current;
            Volatile.Write(ref _current, NullDiagnosticSink.Instance);
        }

        if (previous is AsyncFileDiagnosticSink fileSink)
        {
            // Bounded wait: StopAsync never blocks longer than the supplied timeout, so this
            // synchronous call is bounded by that same timeout.
            fileSink.StopAsync(timeout).GetAwaiter().GetResult();
        }
        else if (previous is IAsyncDisposable disposable)
        {
            disposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Test-only hook to inject an arbitrary sink (e.g. an in-memory fake) without touching the
    /// file system or starting a background worker. Not part of the public contract.
    /// </summary>
    internal static void SetSinkForTests(IDiagnosticSink sink)
    {
        lock (Gate)
        {
            Volatile.Write(ref _current, sink);
        }
    }

    /// <summary>
    /// Test-only hook that forcibly resets diagnostics back to the disabled state without
    /// performing any flush, so tests do not leak a started sink (and its background worker)
    /// into later tests. Not part of the public contract.
    /// </summary>
    internal static void ResetForTests()
    {
        lock (Gate)
        {
            Volatile.Write(ref _current, NullDiagnosticSink.Instance);
        }
    }
}
