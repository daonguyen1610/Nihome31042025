using System.Reflection;
using NihomeBackend.Authorization;

namespace NihomeBackend.Models.Rbac;

/// <summary>
/// Discovers permission codes from <see cref="RequirePermissionAttribute"/>
/// usages across one or more assemblies. Run at seed time so every new
/// endpoint annotated with the attribute is auto-registered in the
/// <c>permissions</c> table — no manual edit to <see cref="PermissionCatalog"/>
/// required.
/// </summary>
public static class PermissionDiscovery
{
    public sealed record DiscoveredEntry(string Module, string Action, string? DescriptionKey)
    {
        public string Code => string.Concat(Module, ".", Action);
    }

    /// <summary>
    /// Scans the provided assemblies for <see cref="RequirePermissionAttribute"/>
    /// usages on types and methods. Duplicates are collapsed; the description
    /// key from the first occurrence wins.
    /// </summary>
    public static IReadOnlyList<DiscoveredEntry> Discover(IEnumerable<Assembly>? assemblies = null)
    {
        var asms = assemblies?.ToList() ?? [typeof(PermissionDiscovery).Assembly];
        var seen = new Dictionary<string, DiscoveredEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var asm in asms)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }

            foreach (var t in types)
            {
                CollectFrom(t.GetCustomAttributes<RequirePermissionAttribute>(inherit: true), seen);
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    CollectFrom(m.GetCustomAttributes<RequirePermissionAttribute>(inherit: true), seen);
                }
            }
        }

        return seen.Values.OrderBy(e => e.Module).ThenBy(e => e.Action).ToList();
    }

    private static void CollectFrom(IEnumerable<RequirePermissionAttribute> attrs, IDictionary<string, DiscoveredEntry> sink)
    {
        foreach (var a in attrs)
        {
            if (sink.ContainsKey(a.Code)) continue;
            sink[a.Code] = new DiscoveredEntry(a.Module, a.Action, a.DescriptionKey);
        }
    }
}
