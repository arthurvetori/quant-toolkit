using Quant.Core.Calendars;
using Quant.Core.DayCounters;
using Quant.QuantLib.Calendars;
using Quant.QuantLib.DayCounters;
using Xunit;

namespace Quant.QuantLib.Tests.DayCounters;

public sealed class DayCounterCatalogTests
{
    [Fact]
    public void Business252IsCachedPerCalendar()
    {
        using var calendars = new CalendarCatalog();
        using var counters = new DayCounterCatalog(calendars);

        var brazil = counters.Get(DayCounterCode.Business252, CalendarCode.BrazilSettlement);

        Assert.Same(brazil, counters.Get(DayCounterCode.Business252, CalendarCode.BrazilSettlement));
        Assert.NotSame(brazil, counters.Get(DayCounterCode.Business252, CalendarCode.UnitedStatesSettlement));
    }

    [Fact]
    public void EveryDayCounterIsCached()
    {
        using var calendars = new CalendarCatalog();
        using var counters = new DayCounterCatalog(calendars);

        foreach (var code in Enum.GetValues<DayCounterCode>())
        {
            Assert.Same(
                counters.Get(code, CalendarCode.BrazilSettlement),
                counters.Get(code, CalendarCode.BrazilSettlement));
        }
    }

    [Fact]
    public void InvalidDayCounterIsRejected()
    {
        using var calendars = new CalendarCatalog();
        using var counters = new DayCounterCatalog(calendars);

        Assert.Throws<ArgumentOutOfRangeException>(() => counters.Get((DayCounterCode)999, CalendarCode.BrazilSettlement));
    }
}
