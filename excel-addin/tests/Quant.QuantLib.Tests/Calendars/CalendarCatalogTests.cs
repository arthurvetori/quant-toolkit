using Quant.Core.Calendars;
using Quant.QuantLib.Calendars;
using Quant.QuantLib.Interop;
using QL = QuantLib;
using Xunit;

namespace Quant.QuantLib.Tests.Calendars;

public sealed class CalendarCatalogTests
{
    [Fact]
    public void CalendarLookupReusesTheSameProxy()
    {
        using var catalog = new CalendarCatalog();

        Assert.Same(catalog.Get(CalendarCode.BrazilSettlement), catalog.Get(CalendarCode.BrazilSettlement));
    }

    [Fact]
    public void JointCalendarRequiresBothMarketsToBeOpen()
    {
        using var catalog = new CalendarCatalog();
        using var usOnlyHoliday = QuantLibDateConverter.ToQuantLib(new DateOnly(2026, 7, 3));

        Assert.True(catalog.Get(CalendarCode.BrazilSettlement).isBusinessDay(usOnlyHoliday));
        Assert.False(catalog.Get(CalendarCode.UnitedStatesSettlement).isBusinessDay(usOnlyHoliday));
        Assert.False(catalog.Get(CalendarCode.BrazilUnitedStatesSettlement).isBusinessDay(usOnlyHoliday));
    }

    [Fact]
    public void InitializedCalendarSupportsConcurrentReads()
    {
        using var catalog = new CalendarCatalog();
        using var date = QuantLibDateConverter.ToQuantLib(new DateOnly(2026, 7, 3));
        var calendar = catalog.Get(CalendarCode.BrazilUnitedStatesSettlement);
        var results = new bool[10_000];

        Parallel.For(0, results.Length, index => results[index] = calendar.isBusinessDay(date));

        Assert.All(results, Assert.False);
    }

    [Fact]
    public void DateAndConventionMappingsAreExplicit()
    {
        using var date = QuantLibDateConverter.ToQuantLib(new DateOnly(2026, 6, 21));

        Assert.Equal(new DateOnly(2026, 6, 21), QuantLibDateConverter.FromQuantLib(date));
        Assert.Equal(QL.BusinessDayConvention.ModifiedFollowing, QuantLibConventionMapper.ToQuantLib(BusinessDayConventionCode.ModifiedFollowing));
        Assert.Equal(QL.BusinessDayConvention.Nearest, QuantLibConventionMapper.ToQuantLib(BusinessDayConventionCode.Nearest));
        Assert.Equal(QL.TimeUnit.Months, QuantLibConventionMapper.ToQuantLib(TimeUnitCode.Months));
        Assert.Equal(QL.TimeUnit.Days, QuantLibConventionMapper.ToQuantLib(TimeUnitCode.Days));
    }

    [Fact]
    public void InvalidCalendarIsRejected()
    {
        using var catalog = new CalendarCatalog();

        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.Get((CalendarCode)999));
    }
}
