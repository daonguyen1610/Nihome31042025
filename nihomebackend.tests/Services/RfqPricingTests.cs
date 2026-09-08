using System.Globalization;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class RfqPricingTests
{
    [Theory]
    [InlineData("1.25", "8.1234", "10.1543")]
    [InlineData("0.000001", "1000000", "1")]
    [InlineData("0.00005", "1", "0.0001")]
    [InlineData("0.000049", "1", "0")]
    [InlineData("100", "0", "0")]
    [InlineData("1", "999999999999.9999", "999999999999.9999")]
    public void LineAmount_UsesDecimalArithmeticAndRoundsHalfAwayFromZero(string quantity, string price, string expected)
    {
        var result = RfqService.CalculateLineAmount(decimal.Parse(quantity, CultureInfo.InvariantCulture), decimal.Parse(price, CultureInfo.InvariantCulture));
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), result);
    }

    [Fact]
    public void LineAmount_RejectsRelationalPrecisionOverflow()
    {
        Assert.Throws<ProcurementOperationException>(() => RfqService.CalculateLineAmount(1000000m, 100000000m));
    }
}
