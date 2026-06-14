using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Data;

/// <summary>
/// Loads the RBAC defaults bundle (base catalog, business roles, role ×
/// permission patterns) from <c>Data/Rbac/rbac-defaults.json</c>. The file is
/// shipped as an embedded resource so the runtime image is self-contained.
/// Result is cached per-assembly load.
/// </summary>
public static class RbacSeedData
{
    private static readonly Lazy<Bundle> _default = new(() => LoadFromAssembly(typeof(RbacSeedData).Assembly));

    public static Bundle Default => _default.Value;

    /// <summary>Test hook: load a bundle from an arbitrary assembly.</summary>
    public static Bundle LoadFromAssembly(Assembly assembly)
    {
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(".rbac-defaults.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "rbac-defaults.json embedded resource not found in assembly " + assembly.FullName);

        using var stream = assembly.GetManifestResourceStream(resource)!;
        var raw = JsonSerializer.Deserialize<Raw>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("rbac-defaults.json is empty");

        var baseCatalog = (raw.BaseCatalog ?? [])
            .Select(e => new PermissionCatalog.Entry(
                e.Module,
                e.Action,
                $"rbac.perm.{e.Module}.{e.Action}"))
            .ToList();

        var businessRoles = (raw.BusinessRoles ?? [])
            .Select(r => new PermissionCatalog.BusinessRole(
                r.Code,
                r.Name,
                $"rbac.role.{r.Code}.label",
                $"rbac.role.{r.Code}.description"))
            .ToList();

        var rolePermissions = (raw.RolePermissions ?? new())
            .ToDictionary(
                kv => kv.Key,
                kv => new RolePermissionDefaults(
                    kv.Value?.Patterns ?? [],
                    kv.Value?.Deny ?? []),
                StringComparer.OrdinalIgnoreCase);

        return new Bundle(baseCatalog, businessRoles, rolePermissions);
    }

    public sealed record Bundle(
        IReadOnlyList<PermissionCatalog.Entry> BaseCatalog,
        IReadOnlyList<PermissionCatalog.BusinessRole> BusinessRoles,
        IReadOnlyDictionary<string, RolePermissionDefaults> RolePermissions);

    public sealed record RolePermissionDefaults(IReadOnlyList<string> Patterns, IReadOnlyList<string> Deny);

    private sealed class Raw
    {
        [JsonPropertyName("baseCatalog")] public List<RawEntry>? BaseCatalog { get; set; }
        [JsonPropertyName("businessRoles")] public List<RawRole>? BusinessRoles { get; set; }
        [JsonPropertyName("rolePermissions")] public Dictionary<string, RawRolePerms>? RolePermissions { get; set; }
    }

    private sealed class RawEntry
    {
        public string Module { get; set; } = "";
        public string Action { get; set; } = "";
    }

    private sealed class RawRole
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private sealed class RawRolePerms
    {
        public List<string>? Patterns { get; set; }
        public List<string>? Deny { get; set; }
    }
}
