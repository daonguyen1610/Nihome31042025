using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

/// <summary>
/// Centralizes case-insensitive email lookup and the "Email already
/// registered" rule that every email-writing path must respect.
/// </summary>
public static class EmailUniqueness
{
    /// <summary>Trim + lower-invariant so storage and lookup match.</summary>
    public static string Normalize(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    public static async Task<bool> IsTakenAsync(
        AppDbContext db,
        string email,
        int? excludeUserId = null,
        CancellationToken ct = default)
    {
        var normalized = Normalize(email);
        if (string.IsNullOrEmpty(normalized)) return false;

        return await db.Users
            .AsNoTracking()
            .AnyAsync(u =>
                u.Email == normalized &&
                (excludeUserId == null || u.Id != excludeUserId), ct);
    }
}

public sealed class EmailAlreadyRegisteredException(string email)
    : InvalidOperationException($"Email already registered: {email}")
{
    public string Email { get; } = email;
}
