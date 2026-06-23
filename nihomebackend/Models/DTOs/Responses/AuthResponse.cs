namespace NihomeBackend.Models.DTOs.Responses;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public int UserId { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Canonical RBAC role code for the user (e.g. <c>SUPER_ADMIN</c>,
    /// <c>ADMIN</c>, <c>USER</c>, or any custom business role). For users not
    /// yet linked to the <c>roles</c> table this falls back to the legacy
    /// enum string.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>RBAC role id. Null only for legacy users not yet backfilled.</summary>
    public int? RoleId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool OtpRequired { get; set; }

    public string? Email { get; set; }

    public string? AvatarUrl { get; set; }
}
