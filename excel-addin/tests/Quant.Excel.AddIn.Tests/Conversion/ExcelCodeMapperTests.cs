using ExcelDna.Integration;
using Quant.Core.Calendars;
using Quant.Core.DayCounters;
using Quant.Excel.AddIn.Conversion;
using Quant.Excel.AddIn.Errors;
using Xunit;

namespace Quant.Excel.AddIn.Tests.Conversion;

public sealed class ExcelCodeMapperTests
{
    [Fact]
    public void EveryPublicCodeMapsExplicitly()
    {
        Assert.Equal(CalendarCode.BrazilSettlement, ExcelCodeMapper.Calendar(0));
        Assert.Equal(CalendarCode.UnitedStatesSettlement, ExcelCodeMapper.Calendar(1));
        Assert.Equal(CalendarCode.BrazilUnitedStatesSettlement, ExcelCodeMapper.Calendar(2));

        foreach (var code in Enum.GetValues<BusinessDayConventionCode>())
        {
            Assert.Equal(code, ExcelCodeMapper.BusinessDayConvention((int)code));
        }

        foreach (var code in Enum.GetValues<TimeUnitCode>())
        {
            Assert.Equal(code, ExcelCodeMapper.TimeUnit((int)code));
        }

        foreach (var code in Enum.GetValues<DayCounterCode>())
        {
            Assert.Equal(code, ExcelCodeMapper.DayCounter((int)code));
        }
    }

    [Fact]
    public void InvalidCodesBecomeExcelValueErrors()
    {
        Assert.Equal(ExcelError.ExcelErrorValue, ExcelCall.Execute(() => ExcelCodeMapper.Calendar(999)));
        Assert.Equal(ExcelError.ExcelErrorValue, ExcelCall.Execute(() => ExcelCodeMapper.BusinessDayConvention(999)));
        Assert.Equal(ExcelError.ExcelErrorValue, ExcelCall.Execute(() => ExcelCodeMapper.TimeUnit(999)));
        Assert.Equal(ExcelError.ExcelErrorValue, ExcelCall.Execute(() => ExcelCodeMapper.DayCounter(999)));
    }

    [Fact]
    public void OutOfRangeArgumentsBecomeExcelNumberErrors()
    {
        Assert.Equal(ExcelError.ExcelErrorNum, ExcelCall.Execute(() => throw new ArgumentOutOfRangeException("value")));
    }
}
