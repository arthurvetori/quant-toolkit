using ExcelDna.Integration;
using Quant.Core.Calendars;
using Quant.Core.DayCounters;
using Quant.Excel.AddIn.Conversion;
using Quant.Excel.AddIn.Functions.DayCounters;
using Quant.Excel.AddIn.Tests.Infrastructure;
using Xunit;

namespace Quant.Excel.AddIn.Tests.Functions;

[Collection(AddInServicesCollection.Name)]
public sealed class DayCounterFunctionsTests
{
    [Fact]
    public void EveryDayCounterMatchesTheTypedService()
    {
        var start = new DateTime(2024, 2, 28);
        var end = new DateTime(2026, 6, 21);

        foreach (var code in Enum.GetValues<DayCounterCode>())
        {
            var expectedDays = AddInServices.DayCount.DayCount(
                ExcelDateConverter.ToDateOnly(start), ExcelDateConverter.ToDateOnly(end), CalendarCode.BrazilSettlement, code);
            var expectedFraction = AddInServices.DayCount.YearFraction(
                ExcelDateConverter.ToDateOnly(start), ExcelDateConverter.ToDateOnly(end), CalendarCode.BrazilSettlement, code);

            Assert.Equal(expectedDays, DayCounterFunctions.DayCount(start, end, 0, (int)code));
            Assert.Equal(expectedFraction, Assert.IsType<double>(DayCounterFunctions.YearFraction(start, end, 0, (int)code)), 12);
        }
    }

    [Fact]
    public void InvalidCodesAndRangesReturnExcelErrors()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 2);

        Assert.Equal(ExcelError.ExcelErrorValue, DayCounterFunctions.DayCount(start, end, 999, 0));
        Assert.Equal(ExcelError.ExcelErrorValue, DayCounterFunctions.DayCount(start, end, 0, 999));
        Assert.Equal(ExcelError.ExcelErrorNum, DayCounterFunctions.YearFraction(end, start, 0, 1));
    }
}
