using ExcelDna.Integration;
using Quant.Excel.AddIn.Conversion;
using Quant.Excel.AddIn.Errors;

namespace Quant.Excel.AddIn.Functions.Calendars;

public static class ScheduleFunctions
{
    [ExcelFunction(Name = "bSchedule", Description = "Returns a chronological vertical schedule generated backward with a short front stub, excluding referenceDate and including maturityDate while preserving end-of-month relationships.", IsThreadSafe = true)]
    public static object Schedule(
        [ExcelArgument(Name = "referenceDate", Description = "Schedule start date; excluded from the returned dates.")] DateTime referenceDate,
        [ExcelArgument(Name = "maturityDate", Description = "Schedule maturity date; included in the returned dates.")] DateTime maturityDate,
        [ExcelArgument(Name = "interval", Description = "Positive number of time units between schedule dates.")] int interval,
        [ExcelArgument(Name = "timeUnit", Description = "Time-unit ID returned by bTimeUnits().")] int timeUnit,
        [ExcelArgument(Name = "calendarCode", Description = "Calendar ID returned by bCalendars().")] int calendarCode,
        [ExcelArgument(Name = "businessDayConvention", Description = "Optional convention ID returned by bBusinessDayConventions(). Default 0, Modified Following.")] int businessDayConvention = 0) =>
        ExcelCall.Execute(() => ExcelTableConverter.FromDates(AddInServices.Schedule.Generate(
            ExcelDateConverter.ToDateOnly(referenceDate),
            ExcelDateConverter.ToDateOnly(maturityDate),
            interval,
            ExcelCodeMapper.TimeUnit(timeUnit),
            ExcelCodeMapper.Calendar(calendarCode),
            ExcelCodeMapper.BusinessDayConvention(businessDayConvention))));
}
