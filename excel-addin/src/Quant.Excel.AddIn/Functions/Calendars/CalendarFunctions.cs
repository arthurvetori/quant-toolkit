using ExcelDna.Integration;
using Quant.Excel.AddIn.Conversion;
using Quant.Excel.AddIn.Errors;

namespace Quant.Excel.AddIn.Functions.Calendars;

public static class CalendarFunctions
{
    [ExcelFunction(Name = "bIsBusinessDay", Description = "Returns TRUE when date is a business day in calendarCode.", IsThreadSafe = true)]
    public static object IsBusinessDay(
        [ExcelArgument(Name = "date", Description = "Excel date to test.")] DateTime date,
        [ExcelArgument(Name = "calendarCode", Description = "Calendar ID returned by bCalendars().")] int calendarCode) =>
        ExcelCall.Execute(() => AddInServices.Calendar.IsBusinessDay(
            ExcelDateConverter.ToDateOnly(date), ExcelCodeMapper.Calendar(calendarCode)));

    [ExcelFunction(Name = "bIsHoliday", Description = "Returns TRUE when date is a holiday or weekend in calendarCode.", IsThreadSafe = true)]
    public static object IsHoliday(
        [ExcelArgument(Name = "date", Description = "Excel date to test.")] DateTime date,
        [ExcelArgument(Name = "calendarCode", Description = "Calendar ID returned by bCalendars().")] int calendarCode) =>
        ExcelCall.Execute(() => AddInServices.Calendar.IsHoliday(
            ExcelDateConverter.ToDateOnly(date), ExcelCodeMapper.Calendar(calendarCode)));

    [ExcelFunction(Name = "bAdjustDate", Description = "Adjusts date with QuantLib using calendarCode and businessDayConvention. Defaults to Modified Following.", IsThreadSafe = true)]
    public static object AdjustDate(
        [ExcelArgument(Name = "date", Description = "Excel date to adjust.")] DateTime date,
        [ExcelArgument(Name = "calendarCode", Description = "Calendar ID returned by bCalendars().")] int calendarCode,
        [ExcelArgument(Name = "businessDayConvention", Description = "Optional convention ID returned by bBusinessDayConventions(). Default 0, Modified Following.")] int businessDayConvention = 0) =>
        ExcelCall.Execute(() => ExcelDateConverter.ToDateTime(AddInServices.Calendar.Adjust(
            ExcelDateConverter.ToDateOnly(date),
            ExcelCodeMapper.Calendar(calendarCode),
            ExcelCodeMapper.BusinessDayConvention(businessDayConvention))));

    [ExcelFunction(Name = "bAdvanceDays", Description = "Advances date by a signed number of QuantLib business days in calendarCode.", IsThreadSafe = true)]
    public static object AdvanceDays(
        [ExcelArgument(Name = "date", Description = "Starting Excel date.")] DateTime date,
        [ExcelArgument(Name = "businessDays", Description = "Signed business-day count; negative values move backward.")] int businessDays,
        [ExcelArgument(Name = "calendarCode", Description = "Calendar ID returned by bCalendars().")] int calendarCode) =>
        ExcelCall.Execute(() => ExcelDateConverter.ToDateTime(AddInServices.Calendar.AdvanceBusinessDays(
            ExcelDateConverter.ToDateOnly(date), businessDays, ExcelCodeMapper.Calendar(calendarCode))));

    [ExcelFunction(Name = "bAdvanceMonths", Description = "Advances date by months with QuantLib, preserving end-of-month relationships.", IsThreadSafe = true)]
    public static object AdvanceMonths(
        [ExcelArgument(Name = "date", Description = "Starting Excel date.")] DateTime date,
        [ExcelArgument(Name = "months", Description = "Signed number of months.")] int months,
        [ExcelArgument(Name = "calendarCode", Description = "Calendar ID returned by bCalendars().")] int calendarCode,
        [ExcelArgument(Name = "businessDayConvention", Description = "Optional convention ID. Default 0, Modified Following.")] int businessDayConvention = 0) =>
        ExcelCall.Execute(() => ExcelDateConverter.ToDateTime(AddInServices.Calendar.AdvanceMonths(
            ExcelDateConverter.ToDateOnly(date), months, ExcelCodeMapper.Calendar(calendarCode),
            ExcelCodeMapper.BusinessDayConvention(businessDayConvention))));

    [ExcelFunction(Name = "bAdvanceYears", Description = "Advances date by years with QuantLib, preserving end-of-month relationships.", IsThreadSafe = true)]
    public static object AdvanceYears(
        [ExcelArgument(Name = "date", Description = "Starting Excel date.")] DateTime date,
        [ExcelArgument(Name = "years", Description = "Signed number of years.")] int years,
        [ExcelArgument(Name = "calendarCode", Description = "Calendar ID returned by bCalendars().")] int calendarCode,
        [ExcelArgument(Name = "businessDayConvention", Description = "Optional convention ID. Default 0, Modified Following.")] int businessDayConvention = 0) =>
        ExcelCall.Execute(() => ExcelDateConverter.ToDateTime(AddInServices.Calendar.AdvanceYears(
            ExcelDateConverter.ToDateOnly(date), years, ExcelCodeMapper.Calendar(calendarCode),
            ExcelCodeMapper.BusinessDayConvention(businessDayConvention))));

    [ExcelFunction(Name = "bBDays", Description = "Returns the QuantLib business-day count between startDate and endDate. By default startDate is excluded and endDate is included.", IsThreadSafe = true)]
    public static object BusinessDays(
        [ExcelArgument(Name = "startDate", Description = "First Excel date in the interval.")] DateTime startDate,
        [ExcelArgument(Name = "endDate", Description = "Last Excel date in the interval.")] DateTime endDate,
        [ExcelArgument(Name = "calendarCode", Description = "Calendar ID returned by bCalendars().")] int calendarCode,
        [ExcelArgument(Name = "includeStart", Description = "Optional; include startDate. Default FALSE.")] bool includeStart = false,
        [ExcelArgument(Name = "includeEnd", Description = "Optional; include endDate. Default TRUE.")] bool includeEnd = true) =>
        ExcelCall.Execute(() => AddInServices.Calendar.BusinessDaysBetween(
            ExcelDateConverter.ToDateOnly(startDate), ExcelDateConverter.ToDateOnly(endDate),
            ExcelCodeMapper.Calendar(calendarCode), includeStart, includeEnd));

    [ExcelFunction(Name = "bHolidays", Description = "Returns a vertical list of QuantLib holidays between startDate and endDate.", IsThreadSafe = true)]
    public static object Holidays(
        [ExcelArgument(Name = "startDate", Description = "First Excel date in the inclusive range.")] DateTime startDate,
        [ExcelArgument(Name = "endDate", Description = "Last Excel date in the inclusive range.")] DateTime endDate,
        [ExcelArgument(Name = "calendarCode", Description = "Calendar ID returned by bCalendars().")] int calendarCode,
        [ExcelArgument(Name = "includeWeekends", Description = "Optional; include weekend dates. Default FALSE.")] bool includeWeekends = false) =>
        ExcelCall.Execute(() => ExcelTableConverter.FromDates(AddInServices.Calendar.Holidays(
            ExcelDateConverter.ToDateOnly(startDate), ExcelDateConverter.ToDateOnly(endDate),
            ExcelCodeMapper.Calendar(calendarCode), includeWeekends)));

    [ExcelFunction(Name = "bEndOfMonth", Description = "Returns the last business day of date's month in calendarCode.", IsThreadSafe = true)]
    public static object EndOfMonth(
        [ExcelArgument(Name = "date", Description = "Excel date whose month is used.")] DateTime date,
        [ExcelArgument(Name = "calendarCode", Description = "Calendar ID returned by bCalendars().")] int calendarCode) =>
        ExcelCall.Execute(() => ExcelDateConverter.ToDateTime(AddInServices.Calendar.EndOfMonth(
            ExcelDateConverter.ToDateOnly(date), ExcelCodeMapper.Calendar(calendarCode))));

    [ExcelFunction(Name = "bIsEndOfMonth", Description = "Returns TRUE when date is the last business day of its month in calendarCode.", IsThreadSafe = true)]
    public static object IsEndOfMonth(
        [ExcelArgument(Name = "date", Description = "Excel date to test.")] DateTime date,
        [ExcelArgument(Name = "calendarCode", Description = "Calendar ID returned by bCalendars().")] int calendarCode) =>
        ExcelCall.Execute(() => AddInServices.Calendar.IsEndOfMonth(
            ExcelDateConverter.ToDateOnly(date), ExcelCodeMapper.Calendar(calendarCode)));
}
