using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

// Note: roles cannot be created from the API. New roles are introduced only
// through DbSeeder / PermissionCatalog.DefaultBusinessRoles. The matrix editor
// only allows updating labels, the active flag, and the role -> permissions
// mapping.

public class UpdateRoleRequest
{
    [StringLength(100, MinimumLength = 2)]
    public string? Name { get; set; }

    [StringLength(150)]
    public string? LabelKey { get; set; }

    [StringLength(150)]
    public string? DescriptionKey { get; set; }

    public bool? IsActive { get; set; }
}

public class UpdateRolePermissionsRequest
{
    [Required]
    public List<string> Permissions { get; set; } = [];
}
