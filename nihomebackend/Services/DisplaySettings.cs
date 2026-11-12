using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>
/// Single source of truth for the display time zone and currency applied to
/// every date, time and money value in the application. Values are persisted
/// on <see cref="SiteSettings"/> and may only be changed through the admin
/// settings screen.
/// </summary>
public static class DisplaySettings
{
    public static readonly string[] SupportedCurrencyCodes = ["VND", "USD"];

    public static string NormalizeCurrency(string? code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsSupportedCurrency(string? code) =>
        SupportedCurrencyCodes.Contains(NormalizeCurrency(code));

    public static string NormalizeCurrencyOrDefault(string? code) =>
        IsSupportedCurrency(code) ? NormalizeCurrency(code) : SiteSettings.DefaultCurrencyCode;

    /// <summary>
    /// Resolve an IANA (or Windows) time zone id. Returns the Vietnam time zone
    /// when the id is empty or unknown so display never fails.
    /// </summary>
    public static bool TryResolveTimeZone(string? id, out TimeZoneInfo zone)
    {
        zone = VietnamTimeZone;
        var value = id?.Trim();
        if (string.IsNullOrEmpty(value)) return false;
        if (TryFind(value, out zone)) return true;
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(value, out var windowsId) && TryFind(windowsId, out zone))
            return true;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(value, out var ianaId) && TryFind(ianaId, out zone))
            return true;
        zone = VietnamTimeZone;
        return false;
    }

    public static TimeZoneInfo ResolveTimeZone(string? id) =>
        TryResolveTimeZone(id, out var zone) ? zone : VietnamTimeZone;

    /// <summary>
    /// Canonical IANA id persisted for a time zone so browsers can format with
    /// it. Unknown or empty ids fall back to the Vietnam time zone.
    /// </summary>
    public static string CanonicalTimeZoneId(string? id) =>
        TryResolveTimeZone(id, out var zone) ? ToStableId(zone.Id) : SiteSettings.DefaultTimeZoneId;

    public static TimeZoneInfo VietnamTimeZone { get; } = CreateVietnamTimeZone();

    public static DateTime UtcNowIn(TimeZoneInfo zone) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);

    public static DateTime TodayIn(TimeZoneInfo zone) => UtcNowIn(zone).Date;

    public static List<TimeZoneOption> ListTimeZones()
    {
        var options = TimeZoneInfo.GetSystemTimeZones()
            .Select(zone => new TimeZoneOption(ToStableId(zone.Id), DisplayZoneLabel(zone), (int)zone.BaseUtcOffset.TotalMinutes))
            .GroupBy(option => option.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(option => option.UtcOffsetMinutes)
            .ThenBy(option => option.Id, StringComparer.Ordinal)
            .ToList();
        if (options.All(option => !string.Equals(option.Id, SiteSettings.DefaultTimeZoneId, StringComparison.OrdinalIgnoreCase)))
            options.Insert(0, new TimeZoneOption(
                SiteSettings.DefaultTimeZoneId,
                DisplayZoneLabel(VietnamTimeZone),
                (int)VietnamTimeZone.BaseUtcOffset.TotalMinutes));
        return options;
    }

    private static bool TryFind(string id, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = VietnamTimeZone;
            return false;
        }
    }

    private static string ToStableId(string systemId) =>
        TimeZoneInfo.TryConvertWindowsIdToIanaId(systemId, out var ianaId) ? ianaId : systemId;

    private static string DisplayZoneLabel(TimeZoneInfo zone)
    {
        var offset = zone.BaseUtcOffset;
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset.Duration();
        return $"{ToStableId(zone.Id)} (UTC{sign}{absolute.Hours:00}:{absolute.Minutes:00})";
    }

    private static TimeZoneInfo CreateVietnamTimeZone()
    {
        if (TryFind("Asia/Ho_Chi_Minh", out var zone)) return zone;
        if (TryFind("SE Asia Standard Time", out zone)) return zone;
        return TimeZoneInfo.CreateCustomTimeZone(
            SiteSettings.DefaultTimeZoneId,
            TimeSpan.FromHours(7),
            "Vietnam Time",
            "Vietnam Time");
    }

    public sealed record TimeZoneOption(string Id, string Label, int UtcOffsetMinutes);
}
