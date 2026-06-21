using Xunit;

namespace Quant.Excel.AddIn.Tests.Infrastructure;

public sealed class AddInServicesFixture : IDisposable
{
    public AddInServicesFixture()
    {
        AddInServices.Initialize();
    }

    public void Dispose()
    {
        AddInServices.Reset();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AddInServicesCollection : ICollectionFixture<AddInServicesFixture>
{
    public const string Name = "AddIn services";
}
