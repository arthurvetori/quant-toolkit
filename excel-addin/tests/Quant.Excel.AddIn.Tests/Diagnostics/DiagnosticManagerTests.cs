using Quant.Excel.AddIn.Diagnostics;
using Xunit;

namespace Quant.Excel.AddIn.Tests.Diagnostics;

[Collection(DiagnosticManagerCollection.Name)]
public sealed class DiagnosticManagerTests
{
    [Fact]
    public void CurrentStartsDisabled()
    {
        Assert.False(DiagnosticManager.Current.IsEnabled);
    }

    [Fact]
    public void StartEnablesTheSinkAndUsesTheDefaultLogDirectory()
    {
        try
        {
            DiagnosticManager.Start();

            Assert.True(DiagnosticManager.Current.IsEnabled);

            var expectedDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Quant",
                "Logs");

            Assert.True(Directory.Exists(expectedDirectory));
        }
        finally
        {
            DiagnosticManager.Stop(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public void StartIsIdempotentWhenAlreadyEnabled()
    {
        try
        {
            DiagnosticManager.Start();
            var first = DiagnosticManager.Current;

            DiagnosticManager.Start();
            var second = DiagnosticManager.Current;

            Assert.Same(first, second);
        }
        finally
        {
            DiagnosticManager.Stop(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public void StopIsIdempotentWhenAlreadyDisabled()
    {
        DiagnosticManager.Stop(TimeSpan.FromSeconds(2));
        DiagnosticManager.Stop(TimeSpan.FromSeconds(2));

        Assert.False(DiagnosticManager.Current.IsEnabled);
    }

    [Fact]
    public void StopDisablesAPreviouslyStartedSink()
    {
        DiagnosticManager.Start();
        Assert.True(DiagnosticManager.Current.IsEnabled);

        DiagnosticManager.Stop(TimeSpan.FromSeconds(2));

        Assert.False(DiagnosticManager.Current.IsEnabled);
    }

    [Fact]
    public void DisabledStatusMessageIsNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(DiagnosticManager.Current.Status.Message));
    }
}
