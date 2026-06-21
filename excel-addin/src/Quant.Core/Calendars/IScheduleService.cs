namespace Quant.Core.Calendars;

public interface IScheduleService
{
    IReadOnlyList<DateOnly> Generate(
        DateOnly referenceDate,
        DateOnly maturityDate,
        int interval,
        TimeUnitCode timeUnit,
        CalendarCode calendar,
        BusinessDayConventionCode convention);
}
