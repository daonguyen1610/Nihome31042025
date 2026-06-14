using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.Rbac;

public class Role
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? LabelKey { get; set; }

    [MaxLength(150)]
    public string? DescriptionKey { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Marks whether RbacSeeder has already populated the initial permission
    /// set for this role. Once true, subsequent reboots will never re-seed —
    /// admin edits (including emptying the role) are preserved. SUPER_ADMIN
    /// is excluded from this rule and always force-synced.
    /// </summary>
    public bool InitialPermissionsSeeded { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
