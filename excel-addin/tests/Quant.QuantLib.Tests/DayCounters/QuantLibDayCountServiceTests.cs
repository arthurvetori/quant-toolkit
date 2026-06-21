using Quant.Core.Calendars;
using Quant.Core.DayCounters;
using Quant.QuantLib.Calendars;
using Quant.QuantLib.DayCounters;
using Quant.QuantLib.Interop;
using Xunit;

namespace Quant.QuantLib.Tests.DayCounters;

public sealed class QuantLibDayCountServiceTests
{
    [Fact]
    public void EveryCounterMatchesDirectQuantLibCalls()
    {
        using var calendars = new CalendarCatalog();
        using var counters = new DayCounterCatalog(calendars);
        var service = new QuantLibDayCountService(counters);
        var startDate = new DateOnly(2024, 2, 28);
        var endDate = new DateOnly(2026, 6, 21);
        using var start = QuantLibDateConverter.ToQuantLib(startDate);
        using var end = QuantLibDateConverter.ToQuantLib(endDate);

        foreach (var code in Enum.GetValues<DayCounterCode>())
        {
            var direct = counters.Get(code, CalendarCode.BrazilSettlement);

            Assert.Equal(direct.dayCount(start, end), service.DayCount(startDate, endDate, CalendarCode.BrazilSettlement, code));
            Assert.Equal(direct.yearFraction(start, end), service.YearFraction(startDate, endDate, CalendarCode.BrazilSettlement, code), 12);
        }
    }

    [Fact]
    public void ReversedRangeIsRejected()
    {
        using var calendars = new CalendarCatalog();
        using var counters = new DayCounterCatalog(calendars);
        var service = new QuantLibDayCountService(counters);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.DayCount(
            new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1), CalendarCode.BrazilSettlement, DayCounterCode.Actual365Fixed));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.YearFraction(
            new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1), CalendarCode.BrazilSettlement, DayCounterCode.Actual365Fixed));
    }
}
