using System.ComponentModel.DataAnnotations;
using NihomeBackend.Models.Rbac;

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

    // RBAC FK — when set, takes precedence over the legacy enum for permission
    // resolution. Nullable so existing seed/integration flows that only set the
    // enum continue to work; PermissionService falls back to the system role
    // whose Code matches the enum value.
    public int? RoleEntityId { get; set; }
    public Role? RoleEntity { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<UserDocument> Documents { get; set; } = new List<UserDocument>();
}
