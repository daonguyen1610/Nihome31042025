using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models;

public class ApplicationUser
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? FullName { get; set; }

    [MaxLength(150)]
    public string? Email { get; set; }

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; } = UserRole.USER;

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<UserDocument> Documents { get; set; } = new List<UserDocument>();
}
