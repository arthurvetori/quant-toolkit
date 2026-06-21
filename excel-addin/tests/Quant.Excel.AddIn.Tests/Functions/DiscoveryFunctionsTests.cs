using Quant.Core.Common;
using Quant.Excel.AddIn.Functions.Information;
using Xunit;

namespace Quant.Excel.AddIn.Tests.Functions;

public sealed class DiscoveryFunctionsTests
{
    [Theory]
    [MemberData(nameof(DiscoveryTables))]
    public void DiscoveryFunctionsReturnIdNameAndDescriptionColumns(object[,] result, IReadOnlyList<CodeDescription> source)
    {
        Assert.Equal(source.Count, result.GetLength(0));
        Assert.Equal(3, result.GetLength(1));

        for (var row = 0; row < source.Count; row++)
        {
            Assert.Equal(source[row].Id, result[row, 0]);
            Assert.Equal(source[row].Name, result[row, 1]);
            Assert.Equal(source[row].Description, result[row, 2]);
        }
    }

    public static TheoryData<object[,], IReadOnlyList<CodeDescription>> DiscoveryTables => new()
    {
        { DiscoveryFunctions.Calendars(), CodeCatalog.Calendars },
        { DiscoveryFunctions.DayCounters(), CodeCatalog.DayCounters },
        { DiscoveryFunctions.BusinessDayConventions(), CodeCatalog.BusinessDayConventions },
        { DiscoveryFunctions.TimeUnits(), CodeCatalog.TimeUnits }
    };
}
