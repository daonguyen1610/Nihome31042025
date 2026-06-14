namespace NihomeBackend.Models.DTOs.Responses;

public class PermissionResponse
{
    public int Id { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? DescriptionKey { get; set; }
}

public class RoleResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LabelKey { get; set; }
    public string? DescriptionKey { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public int UserCount { get; set; }
    public int PermissionCount { get; set; }
}

public class RolePermissionsResponse
{
    public RoleResponse Role { get; set; } = new();
    public List<string> Permissions { get; set; } = [];
}

public class MePermissionsResponse
{
    public string Role { get; set; } = string.Empty;
    public int? RoleId { get; set; }
    public List<string> Permissions { get; set; } = [];
}
