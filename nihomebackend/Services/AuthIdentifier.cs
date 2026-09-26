using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>
/// Resolves a login or password-reset identifier to a user. Phone matches the
/// stored number after trim; email matches the normalized unique email.
/// </summary>
public static class AuthIdentifier
{
    public static async Task<ApplicationUser?> FindUserAsync(
        AppDbContext db,
        string? identifier,
        CancellationToken ct = default)
    {
        var trimmed = (identifier ?? string.Empty).Trim();
        if (trimmed.Length == 0) return null;

        if (trimmed.Contains('@'))
        {
            var email = EmailUniqueness.Normalize(trimmed);
            return await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        }

        var normalizedPhone = ContactValidation.NormalizePhone(trimmed);
        return await db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhone, ct);
    }
}
