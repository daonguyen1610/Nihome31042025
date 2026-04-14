namespace NihomeBackend.Models.DTOs.Responses;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public int UserId { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool OtpRequired { get; set; }

    public string? Email { get; set; }

    public string? AvatarUrl { get; set; }
}
