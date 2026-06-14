namespace NihomeBackend.Models.Rbac;

public static class SystemRoleCodes
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string Admin = "ADMIN";
    public const string User = "USER";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SuperAdmin,
        Admin,
        User,
    };

    public static bool IsSystem(string? code) =>
        !string.IsNullOrWhiteSpace(code) && All.Contains(code.Trim());
}
