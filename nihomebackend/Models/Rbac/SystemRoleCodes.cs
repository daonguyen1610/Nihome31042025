namespace NihomeBackend.Models.Rbac;

public static class SystemRoleCodes
{
    public const string SuperAdmin = RbacRoleCodes.SuperAdmin;
    public const string Admin = RbacRoleCodes.Admin;
    public const string User = RbacRoleCodes.User;

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SuperAdmin,
        Admin,
        User,
    };

    public static bool IsSystem(string? code) =>
        !string.IsNullOrWhiteSpace(code) && All.Contains(code.Trim());
}
