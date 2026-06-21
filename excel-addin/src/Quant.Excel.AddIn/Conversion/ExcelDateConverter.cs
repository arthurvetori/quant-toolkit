namespace Quant.Excel.AddIn.Conversion;

internal static class ExcelDateConverter
{
    internal static DateOnly ToDateOnly(DateTime value) => DateOnly.FromDateTime(value);

    internal static DateTime ToDateTime(DateOnly value) => value.ToDateTime(TimeOnly.MinValue);
}
