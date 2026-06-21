using ExcelDna.Integration;

namespace Quant.Excel.AddIn.Errors;

internal static class ExcelCall
{
    internal static object Execute(Func<object> calculation)
    {
        ArgumentNullException.ThrowIfNull(calculation);

        try
        {
            return calculation();
        }
        catch (ArgumentOutOfRangeException)
        {
            return ExcelError.ExcelErrorNum;
        }
        catch (ArgumentException)
        {
            return ExcelError.ExcelErrorValue;
        }
        catch
        {
            return ExcelError.ExcelErrorValue;
        }
    }
}
