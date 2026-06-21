using Quant.Core.Calendars;
using QL = QuantLib;

namespace Quant.QuantLib.Interop;

internal static class QuantLibConventionMapper
{
    internal static QL.BusinessDayConvention ToQuantLib(BusinessDayConventionCode value) => value switch
    {
        BusinessDayConventionCode.ModifiedFollowing => QL.BusinessDayConvention.ModifiedFollowing,
        BusinessDayConventionCode.Following => QL.BusinessDayConvention.Following,
        BusinessDayConventionCode.Preceding => QL.BusinessDayConvention.Preceding,
        BusinessDayConventionCode.ModifiedPreceding => QL.BusinessDayConvention.ModifiedPreceding,
        BusinessDayConventionCode.Unadjusted => QL.BusinessDayConvention.Unadjusted,
        BusinessDayConventionCode.HalfMonthModifiedFollowing => QL.BusinessDayConvention.HalfMonthModifiedFollowing,
        BusinessDayConventionCode.Nearest => QL.BusinessDayConvention.Nearest,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    internal static QL.TimeUnit ToQuantLib(TimeUnitCode value) => value switch
    {
        TimeUnitCode.Months => QL.TimeUnit.Months,
        TimeUnitCode.Years => QL.TimeUnit.Years,
        TimeUnitCode.Weeks => QL.TimeUnit.Weeks,
        TimeUnitCode.Days => QL.TimeUnit.Days,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
