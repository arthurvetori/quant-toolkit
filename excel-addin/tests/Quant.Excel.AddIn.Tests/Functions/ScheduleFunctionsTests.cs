using ExcelDna.Integration;
using Quant.Excel.AddIn.Functions.Calendars;
using Quant.Excel.AddIn.Tests.Infrastructure;
using Xunit;

namespace Quant.Excel.AddIn.Tests.Functions;

[Collection(AddInServicesCollection.Name)]
public sealed class ScheduleFunctionsTests
{
    [Fact]
    public void AlignedScheduleSpillsVerticallyWithoutReferenceDate()
    {
        var result = Assert.IsType<object[,]>(ScheduleFunctions.Schedule(
            new DateTime(2025, 1, 15), new DateTime(2026, 1, 15), 6, 0, 0, 4));

        Assert.Equal(2, result.GetLength(0));
        Assert.Equal(1, result.GetLength(1));
        Assert.Equal(new DateTime(2025, 7, 15), result[0, 0]);
        Assert.Equal(new DateTime(2026, 1, 15), result[1, 0]);
    }

    [Fact]
    public void BackwardScheduleCreatesShortFrontStub()
    {
        var result = Assert.IsType<object[,]>(ScheduleFunctions.Schedule(
            new DateTime(2025, 2, 1), new DateTime(2026, 1, 15), 6, 0, 0, 4));

        Assert.Equal(new DateTime(2025, 7, 15), result[0, 0]);
        Assert.Equal(new DateTime(2026, 1, 15), result[result.GetLength(0) - 1, 0]);
    }

    [Fact]
    public void DefaultConventionPreservesEndOfMonth()
    {
        var result = Assert.IsType<object[,]>(ScheduleFunctions.Schedule(
            new DateTime(2025, 2, 28), new DateTime(2026, 2, 28), 6, 0, 0));

        Assert.Equal(new DateTime(2026, 2, 27), result[result.GetLength(0) - 1, 0]);
        Assert.True(result.Cast<object>().Cast<DateTime>().SequenceEqual(
            result.Cast<object>().Cast<DateTime>().OrderBy(date => date)));
    }

    [Fact]
    public void InvalidInputsReturnNativeExcelErrors()
    {
        var reference = new DateTime(2025, 1, 1);
        var maturity = new DateTime(2026, 1, 1);

        Assert.Equal(ExcelError.ExcelErrorNum, ScheduleFunctions.Schedule(reference, maturity, 0, 0, 0));
        Assert.Equal(ExcelError.ExcelErrorNum, ScheduleFunctions.Schedule(maturity, reference, 6, 0, 0));
        Assert.Equal(ExcelError.ExcelErrorValue, ScheduleFunctions.Schedule(reference, maturity, 6, 999, 0));
        Assert.Equal(ExcelError.ExcelErrorValue, ScheduleFunctions.Schedule(reference, maturity, 6, 0, 999));
        Assert.Equal(ExcelError.ExcelErrorValue, ScheduleFunctions.Schedule(reference, maturity, 6, 0, 0, 999));
    }
}
