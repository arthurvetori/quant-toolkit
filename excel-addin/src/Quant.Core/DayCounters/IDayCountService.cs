using Quant.Core.Calendars;

namespace Quant.Core.DayCounters;

public interface IDayCountService
{
    int DayCount(DateOnly startDate, DateOnly endDate, CalendarCode calendar, DayCounterCode dayCounter);
    double YearFraction(DateOnly startDate, DateOnly endDate, CalendarCode calendar, DayCounterCode dayCounter);
}
