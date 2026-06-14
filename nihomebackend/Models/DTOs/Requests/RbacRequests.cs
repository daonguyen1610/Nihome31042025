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
