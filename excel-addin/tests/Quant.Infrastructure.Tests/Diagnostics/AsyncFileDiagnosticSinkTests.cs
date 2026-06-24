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
}
