# Quant Asynchronous Diagnostics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add opt-in, non-blocking, rate-limited error diagnostics controlled by Excel commands without adding file or queue work to successful worksheet calculations.

**Architecture:** `Quant.Core` defines diagnostics contracts, `Quant.Infrastructure` owns a bounded channel and one background file writer, and `Quant.Excel.AddIn` exposes start/stop commands plus a read-only status UDF. Calculation exceptions submit events only from the unexpected-error path.

**Tech Stack:** C# 12, .NET 8 `System.Threading.Channels`, ExcelDna.AddIn 1.9.0, xUnit 2.9.3.

## Global Constraints

- Complete the foundation and calendar/day-count plans first.
- Logging is disabled by default.
- Successful calculations must not create events, touch the queue, allocate log messages, or perform file I/O.
- Producers must use non-blocking `TryWrite`; a full queue drops events instead of delaying Excel.
- One background worker writes batches to `%LOCALAPPDATA%\Quant\Logs`.
- Equivalent repeated errors are rate-limited.
- Shutdown flushing is time-bounded.
- `bLoggingStart` and `bLoggingStop` are Excel commands, not worksheet functions.
- `bLoggingStatus()` is a thread-safe, read-only worksheet function.
- Do not log worksheet values or other potentially sensitive inputs.
- Every public operation and Excel registration requires tests and documentation.
- Preserve unrelated staged or working-tree changes; path-limit every commit.

---

### Task 1: Define diagnostics contracts and a zero-work disabled implementation

**Files:**
- Create: `src/Quant.Core/Diagnostics/DiagnosticEvent.cs`
- Create: `src/Quant.Core/Diagnostics/DiagnosticStatus.cs`
- Create: `src/Quant.Core/Diagnostics/IDiagnosticSink.cs`
- Create: `src/Quant.Core/Diagnostics/NullDiagnosticSink.cs`
- Create: `tests/Quant.Core.Tests/Diagnostics/NullDiagnosticSinkTests.cs`

**Interfaces:**
- Consumes: none.
- Produces: immutable diagnostic event/status types and `IDiagnosticSink` used by infrastructure and Excel.

- [ ] **Step 1: Write failing disabled-sink tests**

```csharp
using Quant.Core.Diagnostics;
using Xunit;

namespace Quant.Core.Tests.Diagnostics;

public sealed class NullDiagnosticSinkTests
{
    [Fact]
    public void DisabledSinkRejectsEventsWithoutWork()
    {
        var sink = NullDiagnosticSink.Instance;
        Assert.False(sink.IsEnabled);
        Assert.False(sink.TryWrite(DiagnosticEvent.Error("bDayCount", "Unexpected failure", "stack")));
        Assert.Equal(DiagnosticStatus.Disabled, sink.Status);
    }
}
```

- [ ] **Step 2: Run the test and confirm missing-type failures**

Run: `dotnet test tests/Quant.Core.Tests/Quant.Core.Tests.csproj -c Release --filter NullDiagnosticSinkTests`

Expected: FAIL because diagnostics contracts do not exist.

- [ ] **Step 3: Implement small immutable contracts**

```csharp
public readonly record struct DiagnosticEvent(
    DateTimeOffset Timestamp,
    string Source,
    string Message,
    string? Detail)
{
    public static DiagnosticEvent Error(string source, string message, string? detail) =>
        new(DateTimeOffset.UtcNow, source, message, detail);
}

public readonly record struct DiagnosticStatus(bool IsEnabled, string Message, long DroppedEvents)
{
    public static DiagnosticStatus Disabled { get; } = new(false, "Disabled", 0);
}

public interface IDiagnosticSink
{
    bool IsEnabled { get; }
    DiagnosticStatus Status { get; }
    bool TryWrite(in DiagnosticEvent diagnosticEvent);
}
```

`NullDiagnosticSink` is a sealed singleton whose properties return constants and whose `TryWrite` immediately returns `false`.

- [ ] **Step 4: Run tests and commit contracts**

Run: `dotnet test tests/Quant.Core.Tests/Quant.Core.Tests.csproj -c Release --filter NullDiagnosticSinkTests`

Expected: PASS.

```powershell
git add -- src/Quant.Core/Diagnostics tests/Quant.Core.Tests/Diagnostics
git commit -m "feat: define diagnostics contracts"
```

### Task 2: Implement the bounded asynchronous file sink

**Files:**
- Create: `src/Quant.Infrastructure/Diagnostics/AsyncFileDiagnosticSink.cs`
- Create: `src/Quant.Infrastructure/Diagnostics/DiagnosticLineFormatter.cs`
- Create: `tests/Quant.Infrastructure.Tests/Quant.Infrastructure.Tests.csproj`
- Create: `tests/Quant.Infrastructure.Tests/Diagnostics/AsyncFileDiagnosticSinkTests.cs`
- Create: `tests/Quant.Infrastructure.Tests/Support/TemporaryDirectory.cs`
- Modify: `Quant.sln`

**Interfaces:**
- Consumes: `IDiagnosticSink`, `DiagnosticEvent`, and `DiagnosticStatus` from Task 1.
- Produces: `AsyncFileDiagnosticSink.Start(logDirectory)`, non-blocking `TryWrite`, and `StopAsync(timeout)`.

The test-only directory helper is fully owned by the test project:

```csharp
internal sealed class TemporaryDirectory : IDisposable
{
    internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"quant-tests-{Guid.NewGuid():N}");
    internal TemporaryDirectory() => Directory.CreateDirectory(Path);
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
```

- [ ] **Step 1: Write failing infrastructure tests**

```csharp
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
```

Also test duplicate suppression, idempotent stop, directory creation, and bounded shutdown.

- [ ] **Step 2: Run tests and verify missing implementation failures**

Run: `dotnet test tests/Quant.Infrastructure.Tests/Quant.Infrastructure.Tests.csproj -c Release`

Expected: FAIL because the sink and test project do not exist.

- [ ] **Step 3: Implement a bounded channel and non-blocking producer**

Create the channel with:

```csharp
Channel.CreateBounded<DiagnosticEvent>(new BoundedChannelOptions(capacity)
{
    SingleReader = true,
    SingleWriter = false,
    FullMode = BoundedChannelFullMode.Wait,
    AllowSynchronousContinuations = false
});
```

`TryWrite` checks the enabled flag, suppresses an equivalent `(Source, Message)` within the configured window, calls only `_channel.Writer.TryWrite`, and increments an `Interlocked` dropped counter when the `Wait`-mode channel rejects a full-queue write. It never calls `WriteAsync` or waits. The worker removes expired duplicate-suppression keys after each batch so the rate-limit index cannot grow without bound.

- [ ] **Step 4: Implement one batching file worker**

The worker reads up to 128 events, formats one invariant line per event, and performs one asynchronous `StreamWriter.FlushAsync` per batch. Filenames use UTC date and process ID: `quant-YYYYMMDD-{pid}.log`. Escape CR/LF from fields so each event occupies one line. `StopAsync` completes the writer and waits no longer than the supplied timeout.

- [ ] **Step 5: Run tests and commit infrastructure**

Run: `dotnet test tests/Quant.Infrastructure.Tests/Quant.Infrastructure.Tests.csproj -c Release`

Expected: PASS, including the non-blocking overflow test.

```powershell
git add -- src/Quant.Infrastructure/Diagnostics tests/Quant.Infrastructure.Tests Quant.sln
git commit -m "feat: add bounded asynchronous diagnostics"
```

### Task 3: Compose diagnostics with Excel errors and lifecycle

**Files:**
- Create: `src/Quant.Excel.AddIn/Commands/DiagnosticsCommands.cs`
- Create: `src/Quant.Excel.AddIn/Functions/Information/DiagnosticsFunctions.cs`
- Create: `src/Quant.Excel.AddIn/Diagnostics/DiagnosticManager.cs`
- Modify: `src/Quant.Excel.AddIn/Errors/ExcelCall.cs`
- Modify: `src/Quant.Excel.AddIn/AddInLifecycle.cs`
- Modify: `src/Quant.Excel.AddIn/AddInServices.cs`
- Create: `tests/Quant.Excel.AddIn.Tests/Diagnostics/DiagnosticManagerTests.cs`
- Create: `tests/Quant.Excel.AddIn.Tests/Functions/DiagnosticsFunctionsTests.cs`
- Modify: `tests/Quant.Excel.AddIn.Tests/Functions/DescriptionTests.cs`

**Interfaces:**
- Consumes: `AsyncFileDiagnosticSink` and the existing Excel error boundary.
- Produces: `bLoggingStart`, `bLoggingStop`, `bLoggingStatus()`, and unexpected-exception submission.

- [ ] **Step 1: Write failing command, status, and exception-path tests**

Tests must assert start/stop idempotence, default `%LOCALAPPDATA%\Quant\Logs`, status text, registration types (`ExcelCommand` for start/stop and `ExcelFunction` for status), nonempty descriptions, no event for successful calls, no event for expected argument errors, and one sanitized event for an unexpected exception.

- [ ] **Step 2: Run tests and verify failures**

Run: `dotnet test tests/Quant.Excel.AddIn.Tests/Quant.Excel.AddIn.Tests.csproj -c Release --filter "DiagnosticManagerTests|DiagnosticsFunctionsTests|DescriptionTests"`

Expected: FAIL because the Excel diagnostics layer does not exist.

- [ ] **Step 3: Implement atomic start/stop management**

`DiagnosticManager` starts with `NullDiagnosticSink.Instance`. Use a lock only for rare start/stop operations and publish the active `IDiagnosticSink` with `Volatile.Write`. Reading `Current` uses `Volatile.Read`; calculations do not take a lock.

```csharp
internal static IDiagnosticSink Current => Volatile.Read(ref _current);

internal static void Start()
{
    lock (Gate)
    {
        if (_current.IsEnabled) return;
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Quant", "Logs");
        Volatile.Write(ref _current, AsyncFileDiagnosticSink.Start(directory));
    }
}
```

- [ ] **Step 4: Register commands and status function**

```csharp
[ExcelCommand(Name = "bLoggingStart", Description = "Starts non-blocking Quant error diagnostics for this Excel session.")]
public static void StartLogging() => DiagnosticManager.Start();

[ExcelCommand(Name = "bLoggingStop", Description = "Stops Quant diagnostics and performs a bounded flush.")]
public static void StopLogging() => DiagnosticManager.Stop(TimeSpan.FromSeconds(2));

[ExcelFunction(Name = "bLoggingStatus", Description = "Returns the current Quant diagnostics status without enabling logging.", IsThreadSafe = true)]
public static string LoggingStatus() => DiagnosticManager.Current.Status.Message;
```

- [ ] **Step 5: Connect only unexpected Excel errors**

Keep `ArgumentException` and `ArgumentOutOfRangeException` mappings unchanged. In the final catch only, create a sanitized event containing function name, exception type/message, and stack trace; do not include argument values.

```csharp
catch (Exception exception)
{
    var sink = DiagnosticManager.Current;
    if (sink.IsEnabled)
    {
        sink.TryWrite(DiagnosticEvent.Error(functionName, $"{exception.GetType().Name}: {exception.Message}", exception.StackTrace));
    }
    return ExcelError.ExcelErrorValue;
}
```

The success path never reads the sink. `AutoClose` stops diagnostics before disposing the QuantLib runtime.

- [ ] **Step 6: Run tests and commit Excel integration**

Run: `dotnet test tests/Quant.Excel.AddIn.Tests/Quant.Excel.AddIn.Tests.csproj -c Release --filter "DiagnosticManagerTests|DiagnosticsFunctionsTests|DescriptionTests"`

Expected: PASS.

```powershell
git add -- src/Quant.Excel.AddIn/Commands src/Quant.Excel.AddIn/Diagnostics src/Quant.Excel.AddIn/Functions/Information/DiagnosticsFunctions.cs src/Quant.Excel.AddIn/Errors/ExcelCall.cs src/Quant.Excel.AddIn/AddInLifecycle.cs src/Quant.Excel.AddIn/AddInServices.cs tests/Quant.Excel.AddIn.Tests
git commit -m "feat: expose opt-in asynchronous diagnostics"
```

### Task 4: Document and verify diagnostics under load

**Files:**
- Create: `docs/functions/diagnostics.md`
- Create: `docs/performance/diagnostics.md`
- Modify: `README.md`
- Modify: `eng/verify-package.ps1`

**Interfaces:**
- Consumes: all prior diagnostics tasks.
- Produces: operational guidance and an automated load/packaging gate.

- [ ] **Step 1: Add a deterministic load verification**

Extend verification to enqueue 100,000 synthetic errors into a capacity-256 sink while the worker is paused. Assert the producer loop completes without an await or blocking call, dropped count is positive, and shutdown remains within its configured timeout.

- [ ] **Step 2: Run the full suite and package check**

Run:

```powershell
dotnet test Quant.sln -c Release
.\eng\verify-package.ps1
```

Expected: all tests PASS; the XLL exports `bLoggingStatus` and registers `bLoggingStart`/`bLoggingStop` as commands; the packed x64 add-in still contains `NQuantLibc.dll`.

- [ ] **Step 3: Write operational and performance documentation**

Document how to start, inspect, and stop diagnostics; the log path; sanitization; dropped-event behavior; duplicate window; bounded shutdown; and the explicit trade-off that asynchronous logging minimizes but cannot eliminate error-path overhead. State that successful calculations produce no events.

- [ ] **Step 4: Commit verification and documentation**

```powershell
git add -- docs/functions/diagnostics.md docs/performance/diagnostics.md README.md eng/verify-package.ps1
git commit -m "docs: add diagnostics operations guide"
```

## Plan Verification

Run the commands in Task 4. In Excel x64, invoke `bLoggingStart` from the macro dialog, confirm `bLoggingStatus()` reports enabled, provoke one unexpected test-only error in a development build, invoke `bLoggingStop`, and verify one sanitized log entry was written without worksheet values.
