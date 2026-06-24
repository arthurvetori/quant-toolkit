using System.Diagnostics;
using Quant.Core.Diagnostics;
using Quant.Infrastructure.Diagnostics;
using Quant.Infrastructure.Tests.Support;
using Xunit;

namespace Quant.Infrastructure.Tests.Diagnostics;

public sealed class AsyncFileDiagnosticSinkTests
{
    [Fact]
    public async Task WritesAcceptedEventOnBackgroundWorker()
    {
        using var directory = new TemporaryDirectory();
        await using var sink = AsyncFileDiagnosticSink.Start(directory.Path, capacity: 16, duplicateWindow: TimeSpan.Zero);
        Assert.True(sink.TryWrite(DiagnosticEvent.Error("bSchedule", "failure", "detail")));
        await sink.StopAsync(TimeSpan.FromSeconds(2));
        Assert.Contains("bSchedule", File.ReadAllText(Assert.Single(Directory.GetFiles(directory.Path))));
    }

    [Fact]
    public async Task FullQueueNeverBlocksProducer()
    {
        using var directory = new TemporaryDirectory();
        await using var sink = AsyncFileDiagnosticSink.Start(directory.Path, capacity: 1, duplicateWindow: TimeSpan.Zero, startWorker: false);
        Assert.True(sink.TryWrite(DiagnosticEvent.Error("one", "failure", null)));
        var elapsed = Stopwatch.StartNew();
        Assert.False(sink.TryWrite(DiagnosticEvent.Error("two", "failure", null)));
        Assert.True(elapsed.Elapsed < TimeSpan.FromMilliseconds(25));
        Assert.Equal(1, sink.Status.DroppedEvents);
    }

    [Fact]
    public async Task SuppressesDuplicateEventsWithinWindow()
    {
        using var directory = new TemporaryDirectory();
        await using var sink = AsyncFileDiagnosticSink.Start(directory.Path, capacity: 16, duplicateWindow: TimeSpan.FromMinutes(1));

        Assert.True(sink.TryWrite(DiagnosticEvent.Error("bDuplicate", "failure", "first")));
        Assert.False(sink.TryWrite(DiagnosticEvent.Error("bDuplicate", "failure", "second")));

        await sink.StopAsync(TimeSpan.FromSeconds(2));

        var contents = File.ReadAllText(Assert.Single(Directory.GetFiles(directory.Path)));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(contents, "bDuplicate"));
    }

    [Fact]
    public async Task AllowsRepeatedEventAfterDuplicateWindowExpires()
    {
        using var directory = new TemporaryDirectory();
        await using var sink = AsyncFileDiagnosticSink.Start(directory.Path, capacity: 16, duplicateWindow: TimeSpan.FromMilliseconds(20));

        Assert.True(sink.TryWrite(DiagnosticEvent.Error("bExpiring", "failure", "first")));
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        Assert.True(sink.TryWrite(DiagnosticEvent.Error("bExpiring", "failure", "second")));

        await sink.StopAsync(TimeSpan.FromSeconds(2));

        var contents = File.ReadAllText(Assert.Single(Directory.GetFiles(directory.Path)));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(contents, "bExpiring").Count);
    }

    [Fact]
    public async Task StopAsyncIsIdempotent()
    {
        using var directory = new TemporaryDirectory();
        await using var sink = AsyncFileDiagnosticSink.Start(directory.Path, capacity: 16, duplicateWindow: TimeSpan.Zero);
        Assert.True(sink.TryWrite(DiagnosticEvent.Error("bIdempotent", "failure", null)));

        await sink.StopAsync(TimeSpan.FromSeconds(2));
        await sink.StopAsync(TimeSpan.FromSeconds(2));

        Assert.False(sink.IsEnabled);
        Assert.False(sink.TryWrite(DiagnosticEvent.Error("bIdempotent", "failure", null)));
    }

    [Fact]
    public async Task StartCreatesLogDirectoryWhenMissing()
    {
        var parent = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"quant-tests-{Guid.NewGuid():N}");
        var nested = System.IO.Path.Combine(parent, "Logs");
        try
        {
            Assert.False(Directory.Exists(nested));
            await using var sink = AsyncFileDiagnosticSink.Start(nested, capacity: 16, duplicateWindow: TimeSpan.Zero, startWorker: false);
            Assert.True(Directory.Exists(nested));
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
    }

    [Fact]
    public async Task StopAsyncRespectsTimeoutBound()
    {
        using var directory = new TemporaryDirectory();
        await using var sink = AsyncFileDiagnosticSink.Start(directory.Path, capacity: 16, duplicateWindow: TimeSpan.Zero);
        sink.TryWrite(DiagnosticEvent.Error("bTimeout", "failure", null));

        var elapsed = Stopwatch.StartNew();
        await sink.StopAsync(TimeSpan.FromMilliseconds(50));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DisabledStatusReflectsDroppedEvents()
    {
        using var directory = new TemporaryDirectory();
        await using var sink = AsyncFileDiagnosticSink.Start(directory.Path, capacity: 1, duplicateWindow: TimeSpan.Zero, startWorker: false);
        Assert.True(sink.IsEnabled);
        Assert.Equal(0, sink.Status.DroppedEvents);

        sink.TryWrite(DiagnosticEvent.Error("a", "failure", null));
        sink.TryWrite(DiagnosticEvent.Error("b", "failure", null));
        sink.TryWrite(DiagnosticEvent.Error("c", "failure", null));

        Assert.Equal(2, sink.Status.DroppedEvents);
    }

    [Fact]
    public async Task DroppedNonDuplicateEventDoesNotPoisonDedupWindowForSubsequentWrite()
    {
        // Regression test for review finding #1: a non-duplicate event that fails to enqueue
        // (queue full) must NOT be recorded in the dedup index. If it were, a later genuinely
        // new occurrence of the same (Source, Message) would be incorrectly suppressed as a
        // duplicate of an event that was never actually written anywhere.
        //
        // startWorker:false with capacity:1 makes queue-full fully deterministic: nothing drains
        // the channel except the test's own explicit TryDrainOne call below.
        using var directory = new TemporaryDirectory();
        await using var sink = AsyncFileDiagnosticSink.Start(
            directory.Path,
            capacity: 1,
            duplicateWindow: TimeSpan.FromMinutes(1),
            startWorker: false);

        // Fill the single slot with an unrelated event so the channel is full.
        Assert.True(sink.TryWrite(DiagnosticEvent.Error("filler", "failure", null)));

        // Brand-new key "bPoison": IsDuplicate returns false (never seen before), but the
        // channel write fails because the queue is full -> dropped, not duplicate-suppressed.
        Assert.False(sink.TryWrite(DiagnosticEvent.Error("bPoison", "failure", "dropped-attempt")));
        Assert.Equal(1, sink.Status.DroppedEvents);

        // Without the fix, the dropped attempt above would have stamped the dedup index for
        // "bPoison" anyway. Assert directly that it did not.
        Assert.False(sink.TryGetRecordedDedupTimestamp("bPoison", "failure", out _));

        // Free up queue capacity (simulating the queue pressure relieving) and retry the same
        // key immediately, well within the 1-minute dedup window. Because the prior attempt was
        // never recorded, this must be accepted rather than suppressed as a duplicate.
        Assert.True(sink.TryDrainOne(out var drained));
        Assert.Equal("filler", drained.Source);

        Assert.True(sink.TryWrite(DiagnosticEvent.Error("bPoison", "failure", "should-be-accepted")));
        Assert.True(sink.TryGetRecordedDedupTimestamp("bPoison", "failure", out _));

        // A genuine duplicate of the now-accepted write, within the window, is still suppressed.
        Assert.False(sink.TryWrite(DiagnosticEvent.Error("bPoison", "failure", "true-duplicate")));
    }

    [Fact]
    public async Task StopAsyncTimeoutReturnsPromptlyAndObservesLateWorkerFault()
    {
        // Regression test for review finding #2: when StopAsync's timeout elapses before the
        // worker finishes, StopAsync must still return within the requested bound (it must not
        // itself wait any longer for the abandoned worker), and any exception the worker raises
        // afterward must be observed (via the ContinueWith(..., OnlyOnFaulted) attached in the
        // timeout branch) rather than becoming an unobserved task exception.
        //
        // We cannot directly unit-test "no unobserved task exception was raised" from inside
        // xUnit (that surfaces as a process-level TaskScheduler.UnobservedTaskException event,
        // not a normal assertion), so this test verifies the two things that *are* directly
        // observable from the public contract:
        //   1. StopAsync returns well within the timeout bound even though the worker has not
        //      finished (the worker here is artificially slow: capacity is large enough that a
        //      burst of writes followed by an immediate StopAsync(1ms) cannot possibly let the
        //      real background worker drain and flush before the timeout fires).
        //   2. The test process does not crash and the run completes cleanly even though the
        //      abandoned worker keeps running after StopAsync returns and is given time to
        //      finish flushing in the background.
        // Direct verification that the ContinueWith/OnlyOnFaulted continuation is wired was done
        // by code inspection of AsyncFileDiagnosticSink.StopAsync; this test guards the
        // observable timing contract and guards against a regression that makes StopAsync block
        // for the full worker drain on timeout (which would defeat the fix's purpose).
        using var directory = new TemporaryDirectory();
        await using var sink = AsyncFileDiagnosticSink.Start(directory.Path, capacity: 1024, duplicateWindow: TimeSpan.Zero);

        for (var i = 0; i < 200; i++)
        {
            sink.TryWrite(DiagnosticEvent.Error($"bAbandoned{i}", "failure", null));
        }

        var elapsed = Stopwatch.StartNew();
        await sink.StopAsync(TimeSpan.FromMilliseconds(1));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1), $"StopAsync should return promptly on timeout, took {elapsed.Elapsed}.");

        // Give the abandoned worker time to finish draining/flushing in the background so the
        // temp directory cleanup in TemporaryDirectory.Dispose() doesn't race an in-flight write
        // (which could otherwise manifest as a flaky IOException on Windows).
        await Task.Delay(TimeSpan.FromMilliseconds(500));
    }
}
