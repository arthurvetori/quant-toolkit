using System.Runtime.CompilerServices;
using ExcelDna.Integration;
using Quant.Core.Diagnostics;
using Quant.Excel.AddIn.Diagnostics;

namespace Quant.Excel.AddIn.Errors;

internal static class ExcelCall
{
    internal static object Execute(Func<object> calculation, [CallerMemberName] string functionName = "")
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
        catch (Exception exception)
        {
            var sink = DiagnosticManager.Current;
            if (sink.IsEnabled)
            {
                sink.TryWrite(DiagnosticEvent.Error(functionName, $"{exception.GetType().Name}: {exception.Message}", exception.StackTrace));
            }

            return ExcelError.ExcelErrorValue;
        }
    }
}
