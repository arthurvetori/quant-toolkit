using Quant.Core.Calendars;
using Quant.QuantLib.Interop;
using QL = QuantLib;

namespace Quant.QuantLib.Calendars;

public sealed class QuantLibScheduleService : IScheduleService
{
    private readonly CalendarCatalog _calendars;

    internal QuantLibScheduleService(CalendarCatalog calendars)
    {
        _calendars = calendars ?? throw new ArgumentNullException(nameof(calendars));
    }

    public IReadOnlyList<DateOnly> Generate(
        DateOnly referenceDate,
        DateOnly maturityDate,
        int interval,
        TimeUnitCode timeUnit,
        CalendarCode calendar,
        BusinessDayConventionCode convention)
    {
        if (interval <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive.");
        }

        if (referenceDate >= maturityDate)
        {
            throw new ArgumentOutOfRangeException(nameof(maturityDate), "Maturity must follow the reference date.");
        }

        using var effective = QuantLibDateConverter.ToQuantLib(referenceDate);
        using var maturity = QuantLibDateConverter.ToQuantLib(maturityDate);
        using var tenor = new QL.Period(interval, QuantLibConventionMapper.ToQuantLib(timeUnit));
        var mappedConvention = QuantLibConventionMapper.ToQuantLib(convention);
        using var schedule = new QL.Schedule(
            effective,
            maturity,
            tenor,
            _calendars.Get(calendar),
            mappedConvention,
            mappedConvention,
            QL.DateGeneration.Rule.Backward,
            true);

        var count = checked((int)schedule.size() - 1);
        var result = new DateOnly[count];

        for (var index = 0; index < count; index++)
        {
            using var date = schedule.date((uint)(index + 1));
            result[index] = QuantLibDateConverter.FromQuantLib(date);
        }

        return result;
    }
}
