using Quant.Core.Common;

namespace Quant.Excel.AddIn.Conversion;

internal static class ExcelTableConverter
{
    internal static object[,] From(IReadOnlyList<CodeDescription> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var result = new object[rows.Count, 3];

        for (var row = 0; row < rows.Count; row++)
        {
            result[row, 0] = rows[row].Id;
            result[row, 1] = rows[row].Name;
            result[row, 2] = rows[row].Description;
        }

        return result;
    }
}
