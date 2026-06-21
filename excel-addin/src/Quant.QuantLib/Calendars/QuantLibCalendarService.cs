using Quant.Core.Calendars;
using Quant.QuantLib.Interop;
using QL = QuantLib;

namespace Quant.QuantLib.Calendars;

public sealed class QuantLibCalendarService : ICalendarService
{
    private readonly CalendarCatalog _calendars;

    internal QuantLibCalendarService(CalendarCatalog calendars)
    {
        _calendars = calendars ?? throw new ArgumentNullException(nameof(calendars));
    }

    public bool IsBusinessDay(DateOnly date, CalendarCode calendar)
    {
        using var input = QuantLibDateConverter.ToQuantLib(date);
        return _calendars.Get(calendar).isBusinessDay(input);
    }

    public bool IsHoliday(DateOnly date, CalendarCode calendar)
    {
        using var input = QuantLibDateConverter.ToQuantLib(date);
        return _calendars.Get(calendar).isHoliday(input);
    }

    public DateOnly Adjust(DateOnly date, CalendarCode calendar, BusinessDayConventionCode convention)
    {
        using var input = QuantLibDateConverter.ToQuantLib(date);
        using var output = _calendars.Get(calendar).adjust(input, QuantLibConventionMapper.ToQuantLib(convention));
        return QuantLibDateConverter.FromQuantLib(output);
    }

    public DateOnly AdvanceBusinessDays(DateOnly date, int businessDays, CalendarCode calendar)
    {
        using var input = QuantLibDateConverter.ToQuantLib(date);
        using var output = _calendars.Get(calendar).advance(input, businessDays, QL.TimeUnit.Days);
        return QuantLibDateConverter.FromQuantLib(output);
    }

    public DateOnly AdvanceMonths(DateOnly date, int months, CalendarCode calendar, BusinessDayConventionCode convention)
    {
        using var input = QuantLibDateConverter.ToQuantLib(date);
        using var output = _calendars.Get(calendar).advance(
            input, months, QL.TimeUnit.Months, QuantLibConventionMapper.ToQuantLib(convention), true);
        return QuantLibDateConverter.FromQuantLib(output);
    }

    public DateOnly AdvanceYears(DateOnly date, int years, CalendarCode calendar, BusinessDayConventionCode convention)
    {
        using var input = QuantLibDateConverter.ToQuantLib(date);
        using var output = _calendars.Get(calendar).advance(
            input, years, QL.TimeUnit.Years, QuantLibConventionMapper.ToQuantLib(convention), true);
        return QuantLibDateConverter.FromQuantLib(output);
    }

    public int BusinessDaysBetween(
        DateOnly startDate,
        DateOnly endDate,
        CalendarCode calendar,
        bool includeStart,
        bool includeEnd)
    {
        ValidateRange(startDate, endDate);
        using var start = QuantLibDateConverter.ToQuantLib(startDate);
        using var end = QuantLibDateConverter.ToQuantLib(endDate);
        return _calendars.Get(calendar).businessDaysBetween(start, end, includeStart, includeEnd);
    }

    public IReadOnlyList<DateOnly> Holidays(
        DateOnly startDate,
        DateOnly endDate,
        CalendarCode calendar,
        bool includeWeekends)
    {
        ValidateRange(startDate, endDate);
        using var start = QuantLibDateConverter.ToQuantLib(startDate);
        using var end = QuantLibDateConverter.ToQuantLib(endDate);
        using var holidays = _calendars.Get(calendar).holidayList(start, end, includeWeekends);
        var result = new DateOnly[holidays.Count];

        for (var index = 0; index < holidays.Count; index++)
        {
            using var holiday = holidays[index];
            result[index] = QuantLibDateConverter.FromQuantLib(holiday);
        }

        return result;
    }

    public DateOnly EndOfMonth(DateOnly date, CalendarCode calendar)
    {
        using var input = QuantLibDateConverter.ToQuantLib(date);
        using var output = _calendars.Get(calendar).endOfMonth(input);
        return QuantLibDateConverter.FromQuantLib(output);
    }

    public bool IsEndOfMonth(DateOnly date, CalendarCode calendar)
    {
        using var input = QuantLibDateConverter.ToQuantLib(date);
        return _calendars.Get(calendar).isEndOfMonth(input);
    }

    private static void ValidateRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentOutOfRangeException(nameof(endDate), "End date cannot precede start date.");
        }
    }
}
