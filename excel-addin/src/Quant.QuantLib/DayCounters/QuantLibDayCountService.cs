using Quant.Core.Calendars;
using Quant.Core.DayCounters;
using Quant.QuantLib.Interop;

namespace Quant.QuantLib.DayCounters;

public sealed class QuantLibDayCountService : IDayCountService
{
    private readonly DayCounterCatalog _dayCounters;

    internal QuantLibDayCountService(DayCounterCatalog dayCounters)
    {
        _dayCounters = dayCounters ?? throw new ArgumentNullException(nameof(dayCounters));
    }

    public int DayCount(DateOnly startDate, DateOnly endDate, CalendarCode calendar, DayCounterCode dayCounter)
    {
        ValidateRange(startDate, endDate);
        using var start = QuantLibDateConverter.ToQuantLib(startDate);
        using var end = QuantLibDateConverter.ToQuantLib(endDate);
        return _dayCounters.Get(dayCounter, calendar).dayCount(start, end);
    }

    public double YearFraction(DateOnly startDate, DateOnly endDate, CalendarCode calendar, DayCounterCode dayCounter)
    {
        ValidateRange(startDate, endDate);
        using var start = QuantLibDateConverter.ToQuantLib(startDate);
        using var end = QuantLibDateConverter.ToQuantLib(endDate);
        return _dayCounters.Get(dayCounter, calendar).yearFraction(start, end);
    }

    private static void ValidateRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentOutOfRangeException(nameof(endDate), "End date cannot precede start date.");
        }
    }
}
