using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.Rbac;

public class Permission
{
    public int Id { get; set; }

    [Required, MaxLength(60)]
    public string Module { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? DescriptionKey { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Code => string.Concat(Module, ".", Action);

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
