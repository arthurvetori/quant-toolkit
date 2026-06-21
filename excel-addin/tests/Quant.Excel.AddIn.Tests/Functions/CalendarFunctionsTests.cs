using ExcelDna.Integration;
using Quant.Excel.AddIn.Functions.Calendars;
using Quant.Excel.AddIn.Tests.Infrastructure;
using Xunit;

namespace Quant.Excel.AddIn.Tests.Functions;

[Collection(AddInServicesCollection.Name)]
public sealed class CalendarFunctionsTests
{
    [Fact]
    public void IsBusinessDayReturnsBoolean()
    {
        Assert.Equal(true, CalendarFunctions.IsBusinessDay(new DateTime(2026, 4, 22), 0));
    }

    [Fact]
    public void IsHolidayReturnsBoolean()
    {
        Assert.Equal(true, CalendarFunctions.IsHoliday(new DateTime(2026, 4, 21), 0));
    }

    [Fact]
    public void AdjustDateDefaultsToModifiedFollowing()
    {
        Assert.Equal(new DateTime(2026, 1, 30), CalendarFunctions.AdjustDate(new DateTime(2026, 1, 31), 0));
    }

    [Fact]
    public void AdvanceDaysSupportsNegativeBusinessDays()
    {
        Assert.Equal(new DateTime(2026, 6, 19), CalendarFunctions.AdvanceDays(new DateTime(2026, 6, 22), -1, 0));
    }

    [Fact]
    public void AdvanceMonthsPreservesEndOfMonth()
    {
        Assert.Equal(new DateTime(2026, 2, 27), CalendarFunctions.AdvanceMonths(new DateTime(2026, 1, 30), 1, 0));
    }

    [Fact]
    public void AdvanceYearsPreservesEndOfMonth()
    {
        Assert.Equal(new DateTime(2026, 2, 27), CalendarFunctions.AdvanceYears(new DateTime(2025, 2, 28), 1, 0));
    }

    [Fact]
    public void BusinessDaysUsesDocumentedEndpointDefaults()
    {
        Assert.Equal(1, CalendarFunctions.BusinessDays(new DateTime(2026, 6, 19), new DateTime(2026, 6, 22), 0));
    }

    [Fact]
    public void HolidaysReturnsVerticalExcelDates()
    {
        var result = Assert.IsType<object[,]>(CalendarFunctions.Holidays(
            new DateTime(2026, 4, 20), new DateTime(2026, 4, 22), 0));

        Assert.Equal(1, result.GetLength(0));
        Assert.Equal(1, result.GetLength(1));
        Assert.Equal(new DateTime(2026, 4, 21), result[0, 0]);
    }

    [Fact]
    public void EndOfMonthReturnsExcelDate()
    {
        Assert.Equal(new DateTime(2026, 1, 30), CalendarFunctions.EndOfMonth(new DateTime(2026, 1, 15), 0));
    }

    [Fact]
    public void IsEndOfMonthReturnsBoolean()
    {
        Assert.Equal(true, CalendarFunctions.IsEndOfMonth(new DateTime(2026, 1, 30), 0));
    }

    [Fact]
    public void InvalidCodeAndRangeReturnNativeExcelErrors()
    {
        Assert.Equal(ExcelError.ExcelErrorValue, CalendarFunctions.IsBusinessDay(new DateTime(2026, 1, 1), 999));
        Assert.Equal(ExcelError.ExcelErrorNum, CalendarFunctions.BusinessDays(
            new DateTime(2026, 1, 2), new DateTime(2026, 1, 1), 0));
    }
}
