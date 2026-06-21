using QL = QuantLib;

namespace Quant.QuantLib.Interop;

internal static class QuantLibDateConverter
{
    internal static QL.Date ToQuantLib(DateOnly value) =>
        new(value.Day, (QL.Month)value.Month, value.Year);

    internal static DateOnly FromQuantLib(QL.Date value) =>
        new(value.year(), (int)value.month(), value.dayOfMonth());
}
