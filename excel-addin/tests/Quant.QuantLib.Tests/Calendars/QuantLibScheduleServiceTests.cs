using Quant.Core.Calendars;
using Quant.QuantLib.Calendars;
using Xunit;

namespace Quant.QuantLib.Tests.Calendars;

public sealed class QuantLibScheduleServiceTests
{
    [Fact]
    public void AlignedScheduleExcludesReferenceAndIncludesMaturity()
    {
        using var calendars = new CalendarCatalog();
        var service = new QuantLibScheduleService(calendars);

        var result = service.Generate(
            new DateOnly(2025, 1, 15),
            new DateOnly(2026, 1, 15),
            6,
            TimeUnitCode.Months,
            CalendarCode.BrazilSettlement,
            BusinessDayConventionCode.Unadjusted);

        Assert.Equal([new DateOnly(2025, 7, 15), new DateOnly(2026, 1, 15)], result);
        Assert.DoesNotContain(new DateOnly(2025, 1, 15), result);
        Assert.True(result.SequenceEqual(result.OrderBy(date => date)));
    }

    [Fact]
    public void BackwardScheduleProducesShortFrontStub()
    {
        using var calendars = new CalendarCatalog();
        var service = new QuantLibScheduleService(calendars);

        var result = service.Generate(
            new DateOnly(2025, 2, 1),
            new DateOnly(2026, 1, 15),
            6,
            TimeUnitCode.Months,
            CalendarCode.BrazilSettlement,
            BusinessDayConventionCode.Unadjusted);

        Assert.Equal(new DateOnly(2025, 7, 15), result[0]);
        Assert.Equal(new DateOnly(2026, 1, 15), result[^1]);
    }

    [Fact]
    public void EndOfMonthRelationshipIsPreserved()
    {
        using var calendars = new CalendarCatalog();
        var service = new QuantLibScheduleService(calendars);
        var calendar = new QuantLibCalendarService(calendars);

        var result = service.Generate(
            new DateOnly(2025, 2, 28),
            new DateOnly(2026, 2, 28),
            6,
            TimeUnitCode.Months,
            CalendarCode.BrazilSettlement,
            BusinessDayConventionCode.ModifiedFollowing);

        Assert.All(result, date => Assert.True(calendar.IsEndOfMonth(date, CalendarCode.BrazilSettlement)));
        Assert.Equal(new DateOnly(2026, 2, 27), result[^1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonpositiveIntervalIsRejected(int interval)
    {
        using var calendars = new CalendarCatalog();
        var service = new QuantLibScheduleService(calendars);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Generate(
            new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1), interval, TimeUnitCode.Months,
            CalendarCode.BrazilSettlement, BusinessDayConventionCode.ModifiedFollowing));
    }

    [Fact]
    public void ReferenceMustPrecedeMaturity()
    {
        using var calendars = new CalendarCatalog();
        var service = new QuantLibScheduleService(calendars);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Generate(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), 6, TimeUnitCode.Months,
            CalendarCode.BrazilSettlement, BusinessDayConventionCode.ModifiedFollowing));
    }
}
