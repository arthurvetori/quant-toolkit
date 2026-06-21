using Quant.Core.Calendars;
using Quant.Core.Common;
using Quant.Core.DayCounters;
using Xunit;

namespace Quant.Core.Tests.Common;

public sealed class CodeCatalogTests
{
    [Theory]
    [InlineData(CalendarCode.BrazilSettlement, 0)]
    [InlineData(CalendarCode.UnitedStatesSettlement, 1)]
    [InlineData(CalendarCode.BrazilUnitedStatesSettlement, 2)]
    public void CalendarIdsAreStable(CalendarCode code, int expected) => Assert.Equal(expected, (int)code);

    [Theory]
    [InlineData(BusinessDayConventionCode.ModifiedFollowing, 0)]
    [InlineData(BusinessDayConventionCode.Following, 1)]
    [InlineData(BusinessDayConventionCode.Preceding, 2)]
    [InlineData(BusinessDayConventionCode.ModifiedPreceding, 3)]
    [InlineData(BusinessDayConventionCode.Unadjusted, 4)]
    [InlineData(BusinessDayConventionCode.HalfMonthModifiedFollowing, 5)]
    [InlineData(BusinessDayConventionCode.Nearest, 6)]
    public void BusinessDayConventionIdsAreStable(BusinessDayConventionCode code, int expected) => Assert.Equal(expected, (int)code);

    [Theory]
    [InlineData(TimeUnitCode.Months, 0)]
    [InlineData(TimeUnitCode.Years, 1)]
    [InlineData(TimeUnitCode.Weeks, 2)]
    [InlineData(TimeUnitCode.Days, 3)]
    public void TimeUnitIdsAreStable(TimeUnitCode code, int expected) => Assert.Equal(expected, (int)code);

    [Theory]
    [InlineData(DayCounterCode.Business252, 0)]
    [InlineData(DayCounterCode.Actual365Fixed, 1)]
    [InlineData(DayCounterCode.Thirty360BondBasis, 2)]
    [InlineData(DayCounterCode.Actual360, 3)]
    [InlineData(DayCounterCode.Actual365NoLeap, 4)]
    [InlineData(DayCounterCode.ActualActualIsda, 5)]
    [InlineData(DayCounterCode.ActualActualAfb, 6)]
    [InlineData(DayCounterCode.Thirty360Usa, 7)]
    [InlineData(DayCounterCode.Thirty360European, 8)]
    [InlineData(DayCounterCode.Thirty360Italian, 9)]
    [InlineData(DayCounterCode.Thirty360Nasd, 10)]
    [InlineData(DayCounterCode.OneDay, 11)]
    [InlineData(DayCounterCode.Simple, 12)]
    public void DayCounterIdsAreStable(DayCounterCode code, int expected) => Assert.Equal(expected, (int)code);

    [Fact]
    public void DiscoveryRowsHaveUniqueIdsAndDescriptions()
    {
        Assert.Equal(CodeCatalog.Calendars.Count, CodeCatalog.Calendars.Select(x => x.Id).Distinct().Count());
        Assert.Equal(CodeCatalog.DayCounters.Count, CodeCatalog.DayCounters.Select(x => x.Id).Distinct().Count());
        Assert.Equal(CodeCatalog.BusinessDayConventions.Count, CodeCatalog.BusinessDayConventions.Select(x => x.Id).Distinct().Count());
        Assert.Equal(CodeCatalog.TimeUnits.Count, CodeCatalog.TimeUnits.Select(x => x.Id).Distinct().Count());

        var rows = CodeCatalog.Calendars
            .Concat(CodeCatalog.DayCounters)
            .Concat(CodeCatalog.BusinessDayConventions)
            .Concat(CodeCatalog.TimeUnits);

        Assert.All(rows, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Name));
            Assert.False(string.IsNullOrWhiteSpace(row.Description));
        });
    }
}
