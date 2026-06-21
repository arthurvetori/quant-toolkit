using Quant.Core.Calendars;
using Quant.Core.DayCounters;
using Quant.QuantLib;

namespace Quant.Excel.AddIn;

internal static class AddInServices
{
    private static readonly object Sync = new();
    private static QuantLibRuntime? _runtime;

    internal static ICalendarService Calendar => Runtime.Calendar;

    internal static IDayCountService DayCount => Runtime.DayCount;

    internal static IScheduleService Schedule => Runtime.Schedule;

    internal static void Initialize()
    {
        lock (Sync)
        {
            _runtime ??= new QuantLibRuntime();
        }
    }

    internal static void Reset()
    {
        QuantLibRuntime? runtime;

        lock (Sync)
        {
            runtime = _runtime;
            _runtime = null;
        }

        runtime?.Dispose();
    }

    private static QuantLibRuntime Runtime =>
        Volatile.Read(ref _runtime) ?? throw new InvalidOperationException("The Quant add-in is not initialized.");
}
