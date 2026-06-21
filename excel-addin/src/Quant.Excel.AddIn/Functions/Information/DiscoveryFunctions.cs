using ExcelDna.Integration;
using Quant.Core.Common;
using Quant.Excel.AddIn.Conversion;

namespace Quant.Excel.AddIn.Functions.Information;

public static class DiscoveryFunctions
{
    [ExcelFunction(Name = "bCalendars", Description = FunctionDescriptions.Calendars, IsThreadSafe = true)]
    public static object[,] Calendars() => ExcelTableConverter.From(CodeCatalog.Calendars);

    [ExcelFunction(Name = "bDayCounters", Description = FunctionDescriptions.DayCounters, IsThreadSafe = true)]
    public static object[,] DayCounters() => ExcelTableConverter.From(CodeCatalog.DayCounters);

    [ExcelFunction(Name = "bBusinessDayConventions", Description = FunctionDescriptions.BusinessDayConventions, IsThreadSafe = true)]
    public static object[,] BusinessDayConventions() => ExcelTableConverter.From(CodeCatalog.BusinessDayConventions);

    [ExcelFunction(Name = "bTimeUnits", Description = FunctionDescriptions.TimeUnits, IsThreadSafe = true)]
    public static object[,] TimeUnits() => ExcelTableConverter.From(CodeCatalog.TimeUnits);
}
