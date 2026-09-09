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

    [Fact]
    public void CommercialTotal_AppliesFreightDiscountVatAndExchangeRateInOrder()
    {
        var result = RfqService.CalculateCommercialTotals(
            subtotal: 100m, freight: 10m, discountPercent: 5m,
            discountAmount: 0m, vatPercent: 10m, exchangeRateToVnd: 25_000m);

        Assert.Equal(5m, result.DiscountAmount);
        Assert.Equal(115.5m, result.TotalOriginal);
        Assert.Equal(2_887_500m, result.TotalVnd);
    }

    [Fact]
    public void CommercialTotal_AcceptsFixedDiscount()
    {
        var result = RfqService.CalculateCommercialTotals(100m, 20m, 0m, 15m, 0m, 1m);

        Assert.Equal(15m, result.DiscountAmount);
        Assert.Equal(105m, result.TotalOriginal);
        Assert.Equal(105m, result.TotalVnd);
    }

    [Fact]
    public void CommercialTotal_RejectsTwoDiscountModes()
    {
        Assert.Throws<ProcurementOperationException>(() =>
            RfqService.CalculateCommercialTotals(100m, 0m, 5m, 5m, 0m, 1m));
    }

    [Fact]
    public void CommercialTotal_RejectsDiscountAboveTaxableAmount()
    {
        Assert.Throws<ProcurementOperationException>(() =>
            RfqService.CalculateCommercialTotals(100m, 0m, 0m, 101m, 0m, 1m));
    }
}
