using System.Reflection;
using ExcelDna.Integration;
using Quant.Excel.AddIn.Commands;
using Quant.Excel.AddIn.Diagnostics;
using Quant.Excel.AddIn.Errors;
using Quant.Excel.AddIn.Functions.Information;
using Xunit;

namespace Quant.Excel.AddIn.Tests.Diagnostics;

[Collection(DiagnosticManagerCollection.Name)]
public sealed class DiagnosticsFunctionsTests
{
    [Fact]
    public void StartLoggingIsRegisteredAsAnExcelCommand()
    {
        var method = typeof(DiagnosticsCommands).GetMethod(nameof(DiagnosticsCommands.StartLogging));
        var attribute = method!.GetCustomAttribute<ExcelCommandAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("bLoggingStart", attribute!.Name);
        Assert.False(string.IsNullOrWhiteSpace(attribute.Description));
        Assert.Null(method!.GetCustomAttribute<ExcelFunctionAttribute>());
    }

    [Fact]
    public void StopLoggingIsRegisteredAsAnExcelCommand()
    {
        var method = typeof(DiagnosticsCommands).GetMethod(nameof(DiagnosticsCommands.StopLogging));
        var attribute = method!.GetCustomAttribute<ExcelCommandAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("bLoggingStop", attribute!.Name);
        Assert.False(string.IsNullOrWhiteSpace(attribute.Description));
        Assert.Null(method!.GetCustomAttribute<ExcelFunctionAttribute>());
    }

    [Fact]
    public void LoggingStatusIsRegisteredAsAThreadSafeExcelFunction()
    {
        var method = typeof(DiagnosticsFunctions).GetMethod(nameof(DiagnosticsFunctions.LoggingStatus));
        var attribute = method!.GetCustomAttribute<ExcelFunctionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("bLoggingStatus", attribute!.Name);
        Assert.True(attribute!.IsThreadSafe);
        Assert.False(string.IsNullOrWhiteSpace(attribute.Description));
        Assert.Null(method!.GetCustomAttribute<ExcelCommandAttribute>());
        Assert.Empty(method!.GetParameters());
    }

    [Fact]
    public void LoggingStatusReflectsDisabledByDefault()
    {
        Assert.Equal(DiagnosticManager.Current.Status.Message, DiagnosticsFunctions.LoggingStatus());
    }

    [Fact]
    public void StartLoggingThenStopLoggingRoundTripsCleanly()
    {
        DiagnosticsCommands.StartLogging();
        try
        {
            Assert.True(DiagnosticManager.Current.IsEnabled);
        }
        finally
        {
            DiagnosticsCommands.StopLogging();
        }

        Assert.False(DiagnosticManager.Current.IsEnabled);
    }

    [Fact]
    public void SuccessfulCalculationsNeverTouchTheSink()
    {
        var sink = new ThrowIfReadDiagnosticSink();
        DiagnosticManager.SetSinkForTests(sink);

        try
        {
            var result = ExcelCall.Execute(() => (object)42);

            Assert.Equal(42, result);
            Assert.False(sink.WasRead);
        }
        finally
        {
            DiagnosticManager.ResetForTests();
        }
    }

    [Fact]
    public void ExpectedArgumentExceptionDoesNotWriteAnEvent()
    {
        var sink = new FakeDiagnosticSink();
        DiagnosticManager.SetSinkForTests(sink);

        try
        {
            var result = ExcelCall.Execute(() => throw new ArgumentException("bad input"));

            Assert.Equal(ExcelError.ExcelErrorValue, result);
            Assert.Empty(sink.Events);
        }
        finally
        {
            DiagnosticManager.ResetForTests();
        }
    }

    [Fact]
    public void ExpectedArgumentOutOfRangeExceptionDoesNotWriteAnEvent()
    {
        var sink = new FakeDiagnosticSink();
        DiagnosticManager.SetSinkForTests(sink);

        try
        {
            var result = ExcelCall.Execute(() => throw new ArgumentOutOfRangeException("input"));

            Assert.Equal(ExcelError.ExcelErrorNum, result);
            Assert.Empty(sink.Events);
        }
        finally
        {
            DiagnosticManager.ResetForTests();
        }
    }

    [Fact]
    public void UnexpectedExceptionWritesExactlyOneSanitizedEvent()
    {
        var sink = new FakeDiagnosticSink();
        DiagnosticManager.SetSinkForTests(sink);

        try
        {
            var result = SampleCaller.Invoke();

            Assert.Equal(ExcelError.ExcelErrorValue, result);
            Assert.Single(sink.Events);

            var diagnosticEvent = sink.Events.Single();

            // Only function name, exception type/message, and stack trace travel into the
            // sanitized event. There is no worksheet argument value in scope at this call site at
            // all (ExcelCall.Execute never receives or forwards the caller's argument values), so
            // the event is structurally limited to those three sanitized fields.
            Assert.Equal(nameof(SampleCaller.Invoke), diagnosticEvent.Source);
            Assert.Contains(nameof(InvalidOperationException), diagnosticEvent.Message);
            Assert.Contains("boom", diagnosticEvent.Message);
            Assert.NotNull(diagnosticEvent.Detail);
        }
        finally
        {
            DiagnosticManager.ResetForTests();
        }
    }

    [Fact]
    public void UnexpectedExceptionIsNotSubmittedWhenDiagnosticsAreDisabled()
    {
        var sink = new FakeDiagnosticSink { IsEnabled = false };
        DiagnosticManager.SetSinkForTests(sink);

        try
        {
            var result = SampleCaller.Invoke();

            Assert.Equal(ExcelError.ExcelErrorValue, result);
            Assert.Empty(sink.Events);
        }
        finally
        {
            DiagnosticManager.ResetForTests();
        }
    }

    private static class SampleCaller
    {
        internal static object Invoke() =>
            ExcelCall.Execute(() => throw new InvalidOperationException("boom"));
    }

    private sealed class ThrowIfReadDiagnosticSink : Quant.Core.Diagnostics.IDiagnosticSink
    {
        public bool WasRead { get; private set; }

        public bool IsEnabled
        {
            get
            {
                WasRead = true;
                return true;
            }
        }

        public Quant.Core.Diagnostics.DiagnosticStatus Status
        {
            get
            {
                WasRead = true;
                return new Quant.Core.Diagnostics.DiagnosticStatus(true, "Should not be read", 0);
            }
        }

        public bool TryWrite(in Quant.Core.Diagnostics.DiagnosticEvent diagnosticEvent)
        {
            WasRead = true;
            return true;
        }
    }
}
