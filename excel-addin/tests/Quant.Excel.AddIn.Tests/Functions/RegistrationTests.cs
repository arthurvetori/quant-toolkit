using System.Reflection;
using ExcelDna.Integration;
using Quant.Excel.AddIn.Functions.Information;
using Xunit;

namespace Quant.Excel.AddIn.Tests.Functions;

public sealed class RegistrationTests
{
    [Fact]
    public void AddInExportsExactlyTheApprovedFunctions()
    {
        var expected = new[]
        {
            "bAdjustDate",
            "bAdvanceDays",
            "bAdvanceMonths",
            "bAdvanceYears",
            "bBDays",
            "bBusinessDayConventions",
            "bCalendars",
            "bDayCount",
            "bDayCounters",
            "bEndOfMonth",
            "bHolidays",
            "bIsBusinessDay",
            "bIsEndOfMonth",
            "bIsHoliday",
            "bLoggingStatus",
            "bSchedule",
            "bTimeUnits",
            "bYearFraction"
        };

        var actual = typeof(DiscoveryFunctions).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(method => method.GetCustomAttribute<ExcelFunctionAttribute>())
            .Where(attribute => attribute is not null)
            .Select(attribute => attribute!.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
