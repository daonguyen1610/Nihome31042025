namespace NihomeBackend.Models;

public class ApplicationUser
{
    public int Id { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Customer;

    public bool IsActive { get; set; } = true;

    public string? AvatarUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
