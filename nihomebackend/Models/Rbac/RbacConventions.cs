namespace NihomeBackend.Models.Rbac;

/// <summary>
/// Single source of truth for RBAC naming conventions. All code that needs
/// to build or parse permission codes MUST use these helpers — never inline
/// the separator. EF queries may use the <see cref="CodeSeparator"/> const
/// directly because the C# compiler inlines const strings.
/// </summary>
public static class RbacConventions
{
    public const string CodeSeparator = ".";

    public static string BuildCode(string module, string action) =>
        string.Concat(module, CodeSeparator, action);
}
