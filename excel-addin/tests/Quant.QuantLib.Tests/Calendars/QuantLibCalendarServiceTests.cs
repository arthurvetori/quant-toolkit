using Quant.Core.Calendars;
using Quant.QuantLib.Calendars;
using Xunit;

namespace Quant.QuantLib.Tests.Calendars;

public sealed class QuantLibCalendarServiceTests
{
    [Fact]
    public void RecognizesBrazilUnitedStatesAndJointHolidays()
    {
        using var catalog = new CalendarCatalog();
        var service = new QuantLibCalendarService(catalog);

        Assert.True(service.IsHoliday(new DateOnly(2026, 4, 21), CalendarCode.BrazilSettlement));
        Assert.True(service.IsHoliday(new DateOnly(2026, 7, 3), CalendarCode.UnitedStatesSettlement));
        Assert.True(service.IsHoliday(new DateOnly(2026, 7, 3), CalendarCode.BrazilUnitedStatesSettlement));
        Assert.True(service.IsBusinessDay(new DateOnly(2026, 4, 22), CalendarCode.BrazilSettlement));
    }

    [Fact]
    public void AdjustUsesRequestedConvention()
    {
        using var catalog = new CalendarCatalog();
        var service = new QuantLibCalendarService(catalog);

        Assert.Equal(
            new DateOnly(2026, 1, 30),
            service.Adjust(new DateOnly(2026, 1, 31), CalendarCode.BrazilSettlement, BusinessDayConventionCode.ModifiedFollowing));
    }

    [Fact]
    public void BusinessDayAdvancementSupportsNegativeValues()
    {
        using var catalog = new CalendarCatalog();
        var service = new QuantLibCalendarService(catalog);

        Assert.Equal(
            new DateOnly(2026, 6, 19),
            service.AdvanceBusinessDays(new DateOnly(2026, 6, 22), -1, CalendarCode.BrazilSettlement));
    }

    [Fact]
    public void MonthAndYearAdvancementPreserveEndOfMonth()
    {
        using var catalog = new CalendarCatalog();
        var service = new QuantLibCalendarService(catalog);

        Assert.Equal(
            new DateOnly(2026, 2, 27),
            service.AdvanceMonths(new DateOnly(2026, 1, 30), 1, CalendarCode.BrazilSettlement, BusinessDayConventionCode.ModifiedFollowing));
        Assert.Equal(
            new DateOnly(2026, 2, 27),
            service.AdvanceYears(new DateOnly(2025, 2, 28), 1, CalendarCode.BrazilSettlement, BusinessDayConventionCode.ModifiedFollowing));
    }

    [Fact]
    public void BusinessDayCountHonorsEndpointInclusion()
    {
        using var catalog = new CalendarCatalog();
        var service = new QuantLibCalendarService(catalog);
        var start = new DateOnly(2026, 6, 19);
        var end = new DateOnly(2026, 6, 22);

        Assert.Equal(1, service.BusinessDaysBetween(start, end, CalendarCode.BrazilSettlement, false, true));
        Assert.Equal(2, service.BusinessDaysBetween(start, end, CalendarCode.BrazilSettlement, true, true));
    }

    [Fact]
    public void HolidayAndEndOfMonthOperationsDelegateToQuantLib()
    {
        using var catalog = new CalendarCatalog();
        var service = new QuantLibCalendarService(catalog);

        Assert.Contains(new DateOnly(2026, 4, 21), service.Holidays(
            new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 22), CalendarCode.BrazilSettlement, false));
        Assert.Equal(new DateOnly(2026, 1, 30), service.EndOfMonth(new DateOnly(2026, 1, 15), CalendarCode.BrazilSettlement));
        Assert.True(service.IsEndOfMonth(new DateOnly(2026, 1, 30), CalendarCode.BrazilSettlement));
    }

    [Fact]
    public void ReversedRangeIsRejected()
    {
        using var catalog = new CalendarCatalog();
        var service = new QuantLibCalendarService(catalog);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.BusinessDaysBetween(
            new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1), CalendarCode.BrazilSettlement, false, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Holidays(
            new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1), CalendarCode.BrazilSettlement, false));
    }
}
