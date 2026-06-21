using Quant.Core.Calendars;
using Quant.QuantLib.Interop;
using QL = QuantLib;

namespace Quant.QuantLib.Calendars;

internal sealed class CalendarCatalog : IDisposable
{
    private readonly QL.Brazil _brazil;
    private readonly QL.UnitedStates _unitedStates;
    private readonly QL.JointCalendar _joint;
    private bool _disposed;

    internal CalendarCatalog()
    {
        _brazil = new QL.Brazil(QL.Brazil.Market.Settlement);
        _unitedStates = new QL.UnitedStates(QL.UnitedStates.Market.Settlement);

        ApplyCorrections(_brazil, HolidayCorrections.BrazilAdded, HolidayCorrections.BrazilRemoved);
        ApplyCorrections(_unitedStates, HolidayCorrections.UnitedStatesAdded, HolidayCorrections.UnitedStatesRemoved);

        _joint = new QL.JointCalendar(_brazil, _unitedStates, QL.JointCalendarRule.JoinHolidays);
    }

    internal QL.Calendar Get(CalendarCode code)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return code switch
        {
            CalendarCode.BrazilSettlement => _brazil,
            CalendarCode.UnitedStatesSettlement => _unitedStates,
            CalendarCode.BrazilUnitedStatesSettlement => _joint,
            _ => throw new ArgumentOutOfRangeException(nameof(code))
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _joint.Dispose();
        _unitedStates.Dispose();
        _brazil.Dispose();
        _disposed = true;
    }

    private static void ApplyCorrections(QL.Calendar calendar, DateOnly[] added, DateOnly[] removed)
    {
        foreach (var value in added)
        {
            using var date = QuantLibDateConverter.ToQuantLib(value);
            calendar.addHoliday(date);
        }

        foreach (var value in removed)
        {
            using var date = QuantLibDateConverter.ToQuantLib(value);
            calendar.removeHoliday(date);
        }
    }
}
