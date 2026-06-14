namespace NihomeBackend.Authorization;

/// <summary>
/// Declarative permission requirement placed on controller actions (or whole
/// controllers). The seeder scans for this attribute at boot to auto-register
/// every (module, action) pair into the <c>permissions</c> table, and the
/// authorization filter (added in a later phase) uses it to enforce access.
///
/// Pages added later only need to annotate their endpoints — no edit to
/// PermissionCatalog is required.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : Attribute
{
    public string Module { get; }
    public string Action { get; }
    public string? DescriptionKey { get; }

    public RequirePermissionAttribute(string module, string action, string? descriptionKey = null)
    {
        if (string.IsNullOrWhiteSpace(module)) throw new ArgumentException("module is required", nameof(module));
        if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("action is required", nameof(action));

        Module = module.Trim();
        Action = action.Trim();
        DescriptionKey = string.IsNullOrWhiteSpace(descriptionKey) ? null : descriptionKey.Trim();
    }

    public string Code => string.Concat(Module, ".", Action);
}
