using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Quant.Core.Diagnostics;

namespace Quant.Infrastructure.Diagnostics;

/// <summary>
/// A bounded, non-blocking, file-backed implementation of <see cref="IDiagnosticSink"/>.
/// Producers call <see cref="TryWrite"/>, which never blocks or awaits: it either enqueues
/// the event onto an in-memory bounded channel or drops it immediately when the channel is
/// full. A single background worker drains the channel in batches and writes them to a
/// per-process log file under the supplied log directory.
/// </summary>
public sealed class AsyncFileDiagnosticSink : IDiagnosticSink, IAsyncDisposable
{
    private const int MaxBatchSize = 128;

    private readonly Channel<DiagnosticEvent> _channel;
    private readonly ConcurrentDictionary<(string Source, string Message), DateTimeOffset> _recentEvents = new();
    private readonly TimeSpan _duplicateWindow;
    private readonly string _logDirectory;
    private readonly Task? _workerTask;
    private long _droppedEvents;
    private int _stopped;

    private AsyncFileDiagnosticSink(string logDirectory, int capacity, TimeSpan duplicateWindow, bool startWorker)
    {
        _logDirectory = logDirectory;
        _duplicateWindow = duplicateWindow;
        _channel = Channel.CreateBounded<DiagnosticEvent>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
        });

        _workerTask = startWorker ? Task.Run(RunWorkerAsync) : null;
    }

    /// <summary>
    /// Creates the log directory if it does not already exist and starts a new sink.
    /// </summary>
    /// <param name="logDirectory">Directory that will receive the per-process log file.</param>
    /// <param name="capacity">Maximum number of buffered, unwritten events.</param>
    /// <param name="duplicateWindow">
    /// Window during which an equivalent (same Source and Message) event is suppressed.
    /// <see cref="TimeSpan.Zero"/> disables suppression.
    /// </param>
    /// <param name="startWorker">
    /// When <c>false</c>, the background writer is not started. This is intended for tests
    /// that only need to exercise the non-blocking producer path.
    /// </param>
    public static AsyncFileDiagnosticSink Start(
        string logDirectory,
        int capacity = 1024,
        TimeSpan? duplicateWindow = null,
        bool startWorker = true)
    {
        Directory.CreateDirectory(logDirectory);
        return new AsyncFileDiagnosticSink(logDirectory, capacity, duplicateWindow ?? TimeSpan.FromSeconds(5), startWorker);
    }

    public bool IsEnabled => _stopped == 0;

    public DiagnosticStatus Status => new(
        IsEnabled,
        IsEnabled ? "Enabled" : "Stopped",
        Interlocked.Read(ref _droppedEvents));

    public bool TryWrite(in DiagnosticEvent diagnosticEvent)
    {
        if (_stopped != 0)
        {
            return false;
        }

        if (_duplicateWindow > TimeSpan.Zero && IsDuplicate(diagnosticEvent))
        {
            return false;
        }

        if (_channel.Writer.TryWrite(diagnosticEvent))
        {
            return true;
        }

        Interlocked.Increment(ref _droppedEvents);
        return false;
    }

    private bool IsDuplicate(in DiagnosticEvent diagnosticEvent)
    {
        var key = (diagnosticEvent.Source, diagnosticEvent.Message);
        var now = diagnosticEvent.Timestamp;

        if (_recentEvents.TryGetValue(key, out var lastSeen) && now - lastSeen < _duplicateWindow)
        {
            return true;
        }

        _recentEvents[key] = now;
        return false;
    }

    private async Task RunWorkerAsync()
    {
        var reader = _channel.Reader;
        var fileName = string.Format(
            CultureInfo.InvariantCulture,
            "quant-{0:yyyyMMdd}-{1}.log",
            DateTime.UtcNow,
            Environment.ProcessId);
        var filePath = Path.Combine(_logDirectory, fileName);

        await using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = false };

        var batch = new List<DiagnosticEvent>(MaxBatchSize);

        try
        {
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                batch.Clear();
                while (batch.Count < MaxBatchSize && reader.TryRead(out var diagnosticEvent))
                {
                    batch.Add(diagnosticEvent);
                }

                if (batch.Count == 0)
                {
                    continue;
                }

                foreach (var diagnosticEvent in batch)
                {
                    await writer.WriteLineAsync(DiagnosticLineFormatter.FormatLine(diagnosticEvent)).ConfigureAwait(false);
                }

                await writer.FlushAsync().ConfigureAwait(false);
                PruneExpiredDuplicateKeys();
            }
        }
        finally
        {
            await writer.FlushAsync().ConfigureAwait(false);
        }
    }

    private void PruneExpiredDuplicateKeys()
    {
        if (_duplicateWindow <= TimeSpan.Zero)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var (key, lastSeen) in _recentEvents)
        {
            if (now - lastSeen >= _duplicateWindow)
            {
                _recentEvents.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// Stops accepting new events, completes the channel, and waits up to <paramref name="timeout"/>
    /// for the background worker to drain and flush any buffered events. Safe to call more than once.
    /// </summary>
    public async Task StopAsync(TimeSpan timeout)
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        _channel.Writer.TryComplete();

        if (_workerTask is null)
        {
            return;
        }

        var completed = await Task.WhenAny(_workerTask, Task.Delay(timeout)).ConfigureAwait(false);
        if (completed != _workerTask)
        {
            return;
        }

        // Observe any exception from the worker without throwing on a timed-out shutdown.
        await _workerTask.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
    }
}
