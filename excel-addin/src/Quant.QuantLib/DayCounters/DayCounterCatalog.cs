using Quant.Core.Calendars;
using Quant.Core.DayCounters;
using Quant.QuantLib.Calendars;
using QL = QuantLib;

namespace Quant.QuantLib.DayCounters;

internal sealed class DayCounterCatalog : IDisposable
{
    private readonly QL.Business252 _business252Brazil;
    private readonly QL.Business252 _business252UnitedStates;
    private readonly QL.Business252 _business252Joint;
    private readonly QL.Actual365Fixed _actual365Fixed = new();
    private readonly QL.Thirty360 _thirty360BondBasis = new(QL.Thirty360.Convention.BondBasis);
    private readonly QL.Actual360 _actual360 = new();
    private readonly QL.Actual365Fixed _actual365NoLeap = new(QL.Actual365Fixed.Convention.NoLeap);
    private readonly QL.ActualActual _actualActualIsda = new(QL.ActualActual.Convention.ISDA);
    private readonly QL.ActualActual _actualActualAfb = new(QL.ActualActual.Convention.AFB);
    private readonly QL.Thirty360 _thirty360Usa = new(QL.Thirty360.Convention.USA);
    private readonly QL.Thirty360 _thirty360European = new(QL.Thirty360.Convention.European);
    private readonly QL.Thirty360 _thirty360Italian = new(QL.Thirty360.Convention.Italian);
    private readonly QL.Thirty360 _thirty360Nasd = new(QL.Thirty360.Convention.NASD);
    private readonly QL.OneDayCounter _oneDay = new();
    private readonly QL.SimpleDayCounter _simple = new();
    private bool _disposed;

    internal DayCounterCatalog(CalendarCatalog calendars)
    {
        ArgumentNullException.ThrowIfNull(calendars);

        _business252Brazil = new QL.Business252(calendars.Get(CalendarCode.BrazilSettlement));
        _business252UnitedStates = new QL.Business252(calendars.Get(CalendarCode.UnitedStatesSettlement));
        _business252Joint = new QL.Business252(calendars.Get(CalendarCode.BrazilUnitedStatesSettlement));
    }

    internal QL.DayCounter Get(DayCounterCode dayCounter, CalendarCode calendar)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return dayCounter switch
        {
            DayCounterCode.Business252 => GetBusiness252(calendar),
            DayCounterCode.Actual365Fixed => _actual365Fixed,
            DayCounterCode.Thirty360BondBasis => _thirty360BondBasis,
            DayCounterCode.Actual360 => _actual360,
            DayCounterCode.Actual365NoLeap => _actual365NoLeap,
            DayCounterCode.ActualActualIsda => _actualActualIsda,
            DayCounterCode.ActualActualAfb => _actualActualAfb,
            DayCounterCode.Thirty360Usa => _thirty360Usa,
            DayCounterCode.Thirty360European => _thirty360European,
            DayCounterCode.Thirty360Italian => _thirty360Italian,
            DayCounterCode.Thirty360Nasd => _thirty360Nasd,
            DayCounterCode.OneDay => _oneDay,
            DayCounterCode.Simple => _simple,
            _ => throw new ArgumentOutOfRangeException(nameof(dayCounter))
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _simple.Dispose();
        _oneDay.Dispose();
        _thirty360Nasd.Dispose();
        _thirty360Italian.Dispose();
        _thirty360European.Dispose();
        _thirty360Usa.Dispose();
        _actualActualAfb.Dispose();
        _actualActualIsda.Dispose();
        _actual365NoLeap.Dispose();
        _actual360.Dispose();
        _thirty360BondBasis.Dispose();
        _actual365Fixed.Dispose();
        _business252Joint.Dispose();
        _business252UnitedStates.Dispose();
        _business252Brazil.Dispose();
        _disposed = true;
    }

    private QL.Business252 GetBusiness252(CalendarCode calendar) => calendar switch
    {
        CalendarCode.BrazilSettlement => _business252Brazil,
        CalendarCode.UnitedStatesSettlement => _business252UnitedStates,
        CalendarCode.BrazilUnitedStatesSettlement => _business252Joint,
        _ => throw new ArgumentOutOfRangeException(nameof(calendar))
    };
}
