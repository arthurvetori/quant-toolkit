using Quant.Core.Calendars;
using Quant.Core.DayCounters;

namespace Quant.Excel.AddIn.Conversion;

internal static class ExcelCodeMapper
{
    internal static CalendarCode Calendar(int value) => value switch
    {
        0 => CalendarCode.BrazilSettlement,
        1 => CalendarCode.UnitedStatesSettlement,
        2 => CalendarCode.BrazilUnitedStatesSettlement,
        _ => throw new ArgumentException("Unsupported calendar code.", nameof(value))
    };

    internal static BusinessDayConventionCode BusinessDayConvention(int value) => value switch
    {
        0 => BusinessDayConventionCode.ModifiedFollowing,
        1 => BusinessDayConventionCode.Following,
        2 => BusinessDayConventionCode.Preceding,
        3 => BusinessDayConventionCode.ModifiedPreceding,
        4 => BusinessDayConventionCode.Unadjusted,
        5 => BusinessDayConventionCode.HalfMonthModifiedFollowing,
        6 => BusinessDayConventionCode.Nearest,
        _ => throw new ArgumentException("Unsupported business-day convention code.", nameof(value))
    };

    internal static TimeUnitCode TimeUnit(int value) => value switch
    {
        0 => TimeUnitCode.Months,
        1 => TimeUnitCode.Years,
        2 => TimeUnitCode.Weeks,
        3 => TimeUnitCode.Days,
        _ => throw new ArgumentException("Unsupported time-unit code.", nameof(value))
    };

    internal static DayCounterCode DayCounter(int value) => value switch
    {
        0 => DayCounterCode.Business252,
        1 => DayCounterCode.Actual365Fixed,
        2 => DayCounterCode.Thirty360BondBasis,
        3 => DayCounterCode.Actual360,
        4 => DayCounterCode.Actual365NoLeap,
        5 => DayCounterCode.ActualActualIsda,
        6 => DayCounterCode.ActualActualAfb,
        7 => DayCounterCode.Thirty360Usa,
        8 => DayCounterCode.Thirty360European,
        9 => DayCounterCode.Thirty360Italian,
        10 => DayCounterCode.Thirty360Nasd,
        11 => DayCounterCode.OneDay,
        12 => DayCounterCode.Simple,
        _ => throw new ArgumentException("Unsupported day-counter code.", nameof(value))
    };
}
