namespace NihomeBackend.Models;

public class RegistrationOtp
{
    public int Id { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string OtpCode { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? Email { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
