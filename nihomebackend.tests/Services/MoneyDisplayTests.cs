using NihomeBackend.Models;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public class MoneyDisplayTests
{
    [Theory]
    [InlineData(2_500_000, "VND", "USD", 25_000, 100)]
    [InlineData(100, "USD", "VND", 25_000, 2_500_000)]
    [InlineData(2_487_500, "VND", "USD", 25_000, 100)]
    [InlineData(2_512_500, "VND", "USD", 25_000, 101)]
    [InlineData(2_500_000, "VND", "VND", 25_000, 2_500_000)]
    [InlineData(200, "USD", "USD", 25_000, 200)]
    public void Convert_UsesTheConfiguredRateAndWholeUnits(
        decimal amount,
        string from,
        string to,
        decimal rate,
        decimal expected)
    {
        Assert.Equal(expected, MoneyDisplay.Convert(amount, from, to, rate));
    }

    [Fact]
    public void Convert_TreatsAMissingRecordCurrencyAsVnd()
    {
        Assert.Equal(100m, MoneyDisplay.Convert(2_500_000m, null, "USD", 25_000m));
    }

    [Fact]
    public void Convert_FallsBackToTheDefaultRateWhenTheConfiguredRateIsInvalid()
    {
        Assert.Equal(100m, MoneyDisplay.Convert(
            2_500_000m, "VND", "USD", 0m));
        Assert.Equal(
            Math.Round(2_500_000m / SiteSettings.DefaultUsdVndRate, 0, MidpointRounding.AwayFromZero),
            MoneyDisplay.Convert(2_500_000m, "VND", "USD", 0m));
    }
}
