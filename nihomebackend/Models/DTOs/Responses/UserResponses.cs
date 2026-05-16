namespace NihomeBackend.Models.DTOs.Responses;

public class UserListItemResponse
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
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
