namespace Quant.Core.Calendars;

public interface ICalendarService
{
    bool IsBusinessDay(DateOnly date, CalendarCode calendar);
    bool IsHoliday(DateOnly date, CalendarCode calendar);
    DateOnly Adjust(DateOnly date, CalendarCode calendar, BusinessDayConventionCode convention);
    DateOnly AdvanceBusinessDays(DateOnly date, int businessDays, CalendarCode calendar);
    DateOnly AdvanceMonths(DateOnly date, int months, CalendarCode calendar, BusinessDayConventionCode convention);
    DateOnly AdvanceYears(DateOnly date, int years, CalendarCode calendar, BusinessDayConventionCode convention);
    int BusinessDaysBetween(DateOnly startDate, DateOnly endDate, CalendarCode calendar, bool includeStart, bool includeEnd);
    IReadOnlyList<DateOnly> Holidays(DateOnly startDate, DateOnly endDate, CalendarCode calendar, bool includeWeekends);
    DateOnly EndOfMonth(DateOnly date, CalendarCode calendar);
    bool IsEndOfMonth(DateOnly date, CalendarCode calendar);
}
