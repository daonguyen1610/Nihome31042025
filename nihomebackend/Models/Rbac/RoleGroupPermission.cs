namespace NihomeBackend.Models.Rbac;

public class RoleGroupPermission
{
    public int Id { get; set; }
    public int RoleGroupId { get; set; }
    public int PermissionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public RoleGroup RoleGroup { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
