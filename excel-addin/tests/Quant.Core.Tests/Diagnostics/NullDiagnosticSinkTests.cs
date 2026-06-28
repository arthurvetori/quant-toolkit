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
