using ExcelDna.Integration;
using Quant.Excel.AddIn.Conversion;
using Quant.Excel.AddIn.Errors;

namespace Quant.Excel.AddIn.Functions.DayCounters;

public static class DayCounterFunctions
{
    [ExcelFunction(Name = "bDayCount", Description = "Returns the QuantLib day count between startDate and endDate using calendarCode and dayCounterCode.", IsThreadSafe = true)]
    public static object DayCount(
        [ExcelArgument(Name = "startDate", Description = "First Excel date in the interval.")] DateTime startDate,
        [ExcelArgument(Name = "endDate", Description = "Last Excel date in the interval.")] DateTime endDate,
        [ExcelArgument(Name = "calendarCode", Description = "Calendar ID returned by bCalendars().")] int calendarCode,
        [ExcelArgument(Name = "dayCounterCode", Description = "Day-counter ID returned by bDayCounters().")] int dayCounterCode) =>
        ExcelCall.Execute(() => AddInServices.DayCount.DayCount(
            ExcelDateConverter.ToDateOnly(startDate),
            ExcelDateConverter.ToDateOnly(endDate),
            ExcelCodeMapper.Calendar(calendarCode),
            ExcelCodeMapper.DayCounter(dayCounterCode)));

    [ExcelFunction(Name = "bYearFraction", Description = "Returns the QuantLib year fraction between startDate and endDate using calendarCode and dayCounterCode.", IsThreadSafe = true)]
    public static object YearFraction(
        [ExcelArgument(Name = "startDate", Description = "First Excel date in the interval.")] DateTime startDate,
        [ExcelArgument(Name = "endDate", Description = "Last Excel date in the interval.")] DateTime endDate,
        [ExcelArgument(Name = "calendarCode", Description = "Calendar ID returned by bCalendars().")] int calendarCode,
        [ExcelArgument(Name = "dayCounterCode", Description = "Day-counter ID returned by bDayCounters().")] int dayCounterCode) =>
        ExcelCall.Execute(() => AddInServices.DayCount.YearFraction(
            ExcelDateConverter.ToDateOnly(startDate),
            ExcelDateConverter.ToDateOnly(endDate),
            ExcelCodeMapper.Calendar(calendarCode),
            ExcelCodeMapper.DayCounter(dayCounterCode)));
}
