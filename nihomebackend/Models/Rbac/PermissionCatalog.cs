using System.Text.RegularExpressions;

namespace NihomeBackend.Models.Rbac;

/// <summary>
/// Pure helper types and utilities for the RBAC permission catalog.
///
/// This class deliberately holds NO seed data — all defaults
/// (base catalog, business roles, role × permission patterns) live in the
/// JSON seed at <c>Data/Rbac/rbac-defaults.json</c> and are loaded by
/// <see cref="Data.RbacSeedData"/>. Edit the JSON to change defaults.
///
/// Role × permission mapping is NOT hardcoded at runtime either: defaults are
/// applied ONLY the first time a role is seeded (tracked by
/// <c>Role.InitialPermissionsSeeded</c>). After that, ADMIN can freely edit
/// the matrix and the seeder will not overwrite. SUPER_ADMIN is the single
/// exception — force-synced to the full catalog on every boot as a lockout
/// safety net.
///
/// The effective catalog at runtime is <c>BaseCatalog ∪ Discovered</c>, where
/// <c>Discovered</c> comes from <see cref="PermissionDiscovery"/> scanning
/// <see cref="Authorization.RequirePermissionAttribute"/> usages. Pages added
/// later only need to annotate their endpoints — no JSON edit required for
/// the permission itself; only edit the JSON if a role pattern should change.
///
/// Patterns support <c>*</c> (one dot-segment) and <c>**</c> (any segments).
/// A role entry may declare a <c>deny</c> list that is subtracted from the
/// expanded matches (used e.g. to keep <c>users.manage</c> away from ADMIN).
/// </summary>
public static class PermissionCatalog
{
    public sealed record Entry(string Module, string Action, string DescriptionKey)
    {
        public string Code => RbacConventions.BuildCode(Module, Action);
    }

    public sealed record BusinessRole(string Code, string Name, string LabelKey, string DescriptionKey);

    /// <summary>
    /// Merges a base catalog (from JSON seed) with auto-discovered entries.
    /// Base wins on description-key conflicts; discovered entries fall back to
    /// the conventional <c>rbac.perm.{module}.{action}</c> key when none provided.
    /// </summary>
    public static IReadOnlyList<Entry> Resolve(
        IEnumerable<Entry> baseCatalog,
        IEnumerable<PermissionDiscovery.DiscoveredEntry>? discovered = null)
    {
        var byCode = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in baseCatalog) byCode[e.Code] = e;
        if (discovered != null)
        {
            foreach (var d in discovered)
            {
                if (byCode.ContainsKey(d.Code)) continue;
                var descKey = d.DescriptionKey ?? $"rbac.perm.{d.Module}.{d.Action}";
                byCode[d.Code] = new Entry(d.Module, d.Action, descKey);
            }
        }
        return byCode.Values.OrderBy(e => e.Module).ThenBy(e => e.Action).ToList();
    }

    /// <summary>
    /// Expands the given allow patterns against <paramref name="allCodes"/>,
    /// then subtracts any code matching the <paramref name="denyPatterns"/>.
    /// </summary>
    public static IReadOnlyList<string> ExpandPatterns(
        IEnumerable<string> allowPatterns,
        IEnumerable<string> allCodes,
        IEnumerable<string>? denyPatterns = null)
    {
        var codes = allCodes.ToList();
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in allowPatterns)
        {
            var rx = WildcardToRegex(p);
            foreach (var c in codes)
                if (rx.IsMatch(c)) matched.Add(c);
        }

        if (denyPatterns != null)
        {
            foreach (var p in denyPatterns)
            {
                var rx = WildcardToRegex(p);
                matched.RemoveWhere(c => rx.IsMatch(c));
            }
        }

        return matched.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ** matches across dots (any number of segments); * matches a single segment.
    private static Regex WildcardToRegex(string pattern)
    {
        const string doubleStarToken = "\u0000DS\u0000";
        var prepared = pattern.Replace("**", doubleStarToken);
        var escaped = Regex.Escape(prepared);
        escaped = escaped.Replace(doubleStarToken, ".*").Replace("\\*", "[^.]*");
        return new Regex("^" + escaped + "$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
