using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.Rbac;

/// <summary>
/// NICON department-level role grouping. Groups are developer-defined; their
/// baseline permission sets and role memberships are managed at runtime.
/// </summary>
public class RoleGroup
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string LabelKey { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Role> Roles { get; set; } = new List<Role>();
    public ICollection<RoleGroupPermission> BaselinePermissions { get; set; } = new List<RoleGroupPermission>();
}
