using QL = QuantLib;
using Xunit;

namespace Quant.QuantLib.Tests.Interop;

public sealed class OfficialBindingSmokeTests
{
    [Fact]
    public void OfficialSwigBindingCreatesDate()
    {
        using var date = new QL.Date(20, QL.Month.June, 2026);

        Assert.Equal(20, date.dayOfMonth());
        Assert.Equal(QL.Month.June, date.month());
        Assert.Equal(2026, date.year());
    }
}
