using Quant.Excel.AddIn.Diagnostics;
using Xunit;

namespace Quant.Excel.AddIn.Tests.Diagnostics;

public sealed class DiagnosticManagerFixture : IDisposable
{
    public void Dispose()
    {
        // Ensure no test in this collection leaks an enabled sink (and its background worker)
        // into other test collections that read DiagnosticManager.Current.
        DiagnosticManager.ResetForTests();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DiagnosticManagerCollection : ICollectionFixture<DiagnosticManagerFixture>
{
    public const string Name = "Diagnostic manager";
}
