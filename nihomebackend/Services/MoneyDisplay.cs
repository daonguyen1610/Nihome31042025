using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>
/// Converts a stored amount from the record's currency to the currency the
/// system is currently displaying. Records keep their own currency; only
/// aggregate totals, filters and ordering need the conversion on the server.
/// </summary>
public static class MoneyDisplay
{
    public static decimal Convert(decimal amount, string? fromCurrency, string toCurrency, decimal usdVndRate)
    {
        var from = DisplaySettings.NormalizeCurrencyOrDefault(fromCurrency);
        var to = DisplaySettings.NormalizeCurrencyOrDefault(toCurrency);
        if (from == to) return amount;

        var rate = usdVndRate > 0 ? usdVndRate : SiteSettings.DefaultUsdVndRate;
        var converted = to == "USD" ? amount / rate : amount * rate;
        return Math.Round(converted, 0, MidpointRounding.AwayFromZero);
    }
}
