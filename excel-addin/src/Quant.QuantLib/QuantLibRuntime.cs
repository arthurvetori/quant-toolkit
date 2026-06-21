using Quant.Core.Calendars;
using Quant.Core.DayCounters;
using Quant.QuantLib.Calendars;
using Quant.QuantLib.DayCounters;

namespace Quant.QuantLib;

public sealed class QuantLibRuntime : IDisposable
{
    private bool _disposed;

    public QuantLibRuntime()
    {
        Calendars = new CalendarCatalog();
        DayCounters = new DayCounterCatalog(Calendars);
        Calendar = new QuantLibCalendarService(Calendars);
        DayCount = new QuantLibDayCountService(DayCounters);
        Schedule = new QuantLibScheduleService(Calendars);
    }

    public ICalendarService Calendar { get; }

    public IDayCountService DayCount { get; }

    public IScheduleService Schedule { get; }

    internal CalendarCatalog Calendars { get; }

    internal DayCounterCatalog DayCounters { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DayCounters.Dispose();
        Calendars.Dispose();
        _disposed = true;
    }
}
