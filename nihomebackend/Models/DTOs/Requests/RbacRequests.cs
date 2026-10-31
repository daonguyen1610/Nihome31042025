using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

// Role lifecycle through the API:
//   * POST /api/admin/rbac/roles            -> create (non-system only)
//   * PUT  /api/admin/rbac/roles/{id}       -> update labels / active flag
//   * PUT  /api/admin/rbac/roles/{id}/permissions -> matrix edit (non-system only)
//   * DELETE /api/admin/rbac/roles/{id}     -> delete (non-system, no users)
// All 3 system roles (SUPER_ADMIN, ADMIN, USER) are immune to create-time
// collisions, matrix edits, deactivation, and deletion.

public class CreateRoleRequest
{
    /// <summary>Stable identifier used in URLs, audit logs, JWT claims.
    /// UPPER_SNAKE_CASE; cannot collide with a system role code.</summary>
    [Required]
    [RegularExpression("^[A-Z][A-Z0-9_]{1,49}$",
        ErrorMessage = "Code must be UPPER_SNAKE_CASE (2-50 chars, start with A-Z).")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(150)]
    public string? LabelKey { get; set; }

    [StringLength(150)]
    public string? DescriptionKey { get; set; }

    /// <summary>Optional initial permission set; if provided, anti-escalation
    /// applies (caller must hold every requested permission).</summary>
    public List<string>? Permissions { get; set; }

    /// <summary>Optional NICON department group for organization and defaults.</summary>
    public int? RoleGroupId { get; set; }

    /// <summary>When true and Permissions is omitted, copy the selected group's
    /// current baseline. This is a one-time snapshot, not a live inheritance.</summary>
    public bool ImportGroupBaseline { get; set; }
}

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

public class SetRoleGroupRequest
{
    public int? RoleGroupId { get; set; }
}

public sealed class RoleGroupNamesRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string Vi { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 2)] public string En { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 2)] public string Zh { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 2)] public string Ja { get; set; } = string.Empty;
}

public sealed class CreateRoleGroupRequest
{
    [Required]
    [RegularExpression("^[A-Z][A-Z0-9_]{1,49}$",
        ErrorMessage = "Group code must be UPPER_SNAKE_CASE (2-50 chars, example: LEGAL_TEAM).")]
    public string Code { get; set; } = string.Empty;

    [Required] public RoleGroupNamesRequest Names { get; set; } = new();

    [Range(0, int.MaxValue)] public int SortOrder { get; set; }
}

public sealed class UpdateRoleGroupRequest
{
    [Required] public RoleGroupNamesRequest Names { get; set; } = new();

    [Range(0, int.MaxValue)] public int SortOrder { get; set; }
}
