using System.Text.RegularExpressions;

namespace NihomeBackend.Services;

/// <summary>
/// Shared phone and email format rules for every CRM contact field.
///
/// These live in one place on purpose. Before this, nothing checked the *shape*
/// of either value anywhere in the codebase: services only asked that at least
/// one of the two be present, and the DTO's <c>[EmailAddress]</c> attribute is
/// permissive enough to accept "345@434". So "ewrt" was a valid phone number.
///
/// The frontend mirrors these rules in <c>src/lib/validation.ts</c> so the user
/// hears about a bad value before the request leaves the browser. Change one and
/// change the other.
/// </summary>
public static partial class ContactValidation
{
    /// <summary>
    /// Vietnamese numbers, written with or without the country code and with the
    /// spacing people actually type. Stripped of separators first, so
    /// "0987 654 321", "0987.654.321" and "+84987654321" all pass.
    /// </summary>
    [GeneratedRegex(@"^(?:\+?84|0)\d{8,10}$")]
    private static partial Regex PhoneShape();

    /// <summary>
    /// Stricter than <c>[EmailAddress]</c>: the domain has to carry a dot and a
    /// label on each side of it, which is what rules out "345@434".
    /// </summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(?:\.[^@\s.]+)+$")]
    private static partial Regex EmailShape();

    [GeneratedRegex(@"[\s.\-()]")]
    private static partial Regex PhoneSeparators();

    /// <summary>Removes the separators people type, leaving digits and a leading +.</summary>
    public static string NormalizePhone(string phone) =>
        PhoneSeparators().Replace(phone ?? string.Empty, string.Empty);

    /// <summary>Blank counts as valid — presence is a separate rule from shape.</summary>
    public static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return true;
        return PhoneShape().IsMatch(NormalizePhone(phone.Trim()));
    }

    /// <summary>Blank counts as valid — presence is a separate rule from shape.</summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return true;
        var trimmed = email.Trim();
        return trimmed.Length <= 150 && EmailShape().IsMatch(trimmed);
    }

    /// <summary>
    /// The rule every CRM contact shares: at least one way to reach the person,
    /// and whatever was supplied has to be well formed.
    /// </summary>
    /// <returns>An error message, or null when the pair is acceptable.</returns>
    public static string? Validate(string? phone, string? email)
    {
        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
        {
            return "Cần ít nhất một trong hai: số điện thoại hoặc email.";
        }

        if (!IsValidPhone(phone))
        {
            return $"Số điện thoại '{phone}' không hợp lệ. Ví dụ hợp lệ: 0987654321 hoặc +84987654321.";
        }

        if (!IsValidEmail(email))
        {
            return $"Email '{email}' không hợp lệ.";
        }

        return null;
    }
}
