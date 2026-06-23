namespace NihomeBackend.Models.DTOs.Responses;

public class UserListItemResponse
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }

    /// <summary>
    /// Canonical RBAC role code (e.g. <c>SUPER_ADMIN</c>, <c>ADMIN</c>, <c>USER</c>,
    /// or any custom business role like <c>PROJECT_MANAGER</c>). For legacy users
    /// not yet linked to the <c>roles</c> table this falls back to the enum string.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>RBAC role id. Null only for legacy users not yet backfilled.</summary>
    public int? RoleId { get; set; }

    /// <summary>Human-readable role name from the <c>roles</c> table. Null for legacy users.</summary>
    public string? RoleName { get; set; }

    public bool IsActive { get; set; }
    public string? AvatarUrl { get; set; }
}

public class UserDetailResponse : UserListItemResponse
{
    public int RefreshTokenCount { get; set; }
}

public class UserListResponse
{
    public List<UserListItemResponse> Items { get; set; } = [];
    public int Total { get; set; }
}

public class RoleMetadataResponse
{
    public string Role { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
    public string DescriptionKey { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public bool IsSystemRole { get; set; } = true;
}

public class PermissionMatrixRowResponse
{
    public string ModuleKey { get; set; } = string.Empty;
    public Dictionary<string, bool> AccessByRole { get; set; } = new();
}

public class RoleCatalogResponse
{
    public List<RoleMetadataResponse> Roles { get; set; } = [];
    public List<PermissionMatrixRowResponse> PermissionMatrix { get; set; } = [];
}
