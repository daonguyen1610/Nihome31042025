using System.Text.RegularExpressions;

namespace NihomeBackend.Models.Rbac;

/// <summary>
/// Canonical, manually-curated baseline of permission codes the application
/// always needs (e.g. `dashboard.view`, `profile.me.*`, `rbac.roles.*`).
///
/// The full effective catalog at runtime is <c>BaseCatalog ∪ Discovered</c>,
/// where <c>Discovered</c> comes from <see cref="PermissionDiscovery"/> scanning
/// <see cref="Authorization.RequirePermissionAttribute"/> usages on controllers.
/// Pages added later only need to annotate their endpoints — no edit here.
///
/// Role × permission mapping is NOT hardcoded at runtime: defaults from
/// <see cref="DefaultRolePermissionPatterns"/> are applied ONLY the first time
/// a role is seeded (tracked by <c>Role.InitialPermissionsSeeded</c>). After
/// that, ADMIN can freely edit the matrix and the seeder will not overwrite.
/// SUPER_ADMIN is the single exception — force-synced to the full catalog on
/// every boot as a lockout safety net.
///
/// Defaults support glob-style wildcards via <c>*</c> (e.g. <c>content.*.view</c>)
/// so new modules matching an existing role's pattern are auto-granted on
/// next boot without code changes.
///
/// New roles can only be introduced by adding them to
/// <see cref="DefaultBusinessRoles"/> and restarting (i.e. via DbSeeder), not
/// from the API.
/// </summary>
public static class PermissionCatalog
{
    public sealed record Entry(string Module, string Action, string DescriptionKey)
    {
        public string Code => string.Concat(Module, ".", Action);
    }

    /// <summary>
    /// Minimum permissions that must exist regardless of controller annotations.
    /// Cross-cutting concerns that don't map 1:1 to a single controller action.
    /// </summary>
    public static readonly IReadOnlyList<Entry> BaseCatalog =
    [
        new("dashboard", "view", "rbac.perm.dashboard.view"),

        new("system.audit", "view", "rbac.perm.system.audit.view"),
        new("system.notifications", "manage", "rbac.perm.system.notifications.manage"),

        new("users", "view", "rbac.perm.users.view"),
        new("users", "manage", "rbac.perm.users.manage"),
        new("rbac.roles", "view", "rbac.perm.rbac.roles.view"),
        new("rbac.roles", "manage", "rbac.perm.rbac.roles.manage"),

        new("profile.me", "view", "rbac.perm.profile.me.view"),
        new("profile.me", "update", "rbac.perm.profile.me.update"),
    ];

    /// <summary>
    /// Merges the base catalog with auto-discovered entries. Base wins on
    /// description-key conflicts; discovered entries fall back to a
    /// conventional <c>rbac.perm.{module}.{action}</c> key when none provided.
    /// </summary>
    public static IReadOnlyList<Entry> Resolve(IEnumerable<PermissionDiscovery.DiscoveredEntry>? discovered = null)
    {
        var byCode = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in BaseCatalog) byCode[e.Code] = e;
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
    /// First-seed permission patterns per role. Each pattern may contain
    /// <c>*</c> wildcards that match any single dot-segment, e.g.
    /// <c>content.*.view</c> matches every <c>content.X.view</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultRolePermissionPatterns { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [SystemRoleCodes.SuperAdmin] = ["**"],
            [SystemRoleCodes.Admin] = ["**"],
            [SystemRoleCodes.User] = ["profile.me.*"],
            ["SALE"] =
            [
                "dashboard.view",
                "contacts.*",
                "recruitment.applications.view",
                "profile.me.*",
            ],
            ["DESIGN"] =
            [
                "dashboard.view",
                "content.**",
                "processes.view",
                "profile.me.*",
            ],
            ["PM"] =
            [
                "dashboard.view",
                "content.projects.*",
                "processes.*",
                "recruitment.applications.view",
                "profile.me.*",
            ],
            ["QS"] =
            [
                "dashboard.view",
                "content.projects.view",
                "processes.view",
                "profile.me.*",
            ],
            ["ACCOUNTANT"] =
            [
                "dashboard.view",
                "contacts.view",
                "system.audit.view",
                "profile.me.*",
            ],
            ["WAREHOUSE"] =
            [
                "dashboard.view",
                "processes.view",
                "profile.me.*",
            ],
            ["BGD"] =
            [
                "dashboard.view",
                "**.view",
                "system.audit.view",
                "profile.me.*",
            ],
        };

    /// <summary>
    /// Expands wildcard patterns against the supplied effective catalog.
    /// Policy carve-out: ADMIN does NOT receive <c>users.manage</c> on first
    /// seed (only SUPER_ADMIN may manage users by default).
    /// </summary>
    public static IReadOnlyList<string> ExpandPatternsFor(string roleCode, IEnumerable<string> allCodes)
    {
        if (!DefaultRolePermissionPatterns.TryGetValue(roleCode, out var patterns))
            return Array.Empty<string>();

        var codes = allCodes.ToList();
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in patterns)
        {
            var rx = WildcardToRegex(p);
            foreach (var c in codes)
                if (rx.IsMatch(c)) matched.Add(c);
        }

        if (string.Equals(roleCode, SystemRoleCodes.Admin, StringComparison.OrdinalIgnoreCase))
            matched.Remove("users.manage");

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

    /// <summary>
    /// Business roles (non-system) seeded on first boot so the matrix editor
    /// has sensible defaults. New roles MUST be added here, not via API.
    /// </summary>
    public static IReadOnlyList<BusinessRole> DefaultBusinessRoles { get; } =
    [
        new("SALE", "Sale", "rbac.role.SALE.label", "rbac.role.SALE.description"),
        new("DESIGN", "Design", "rbac.role.DESIGN.label", "rbac.role.DESIGN.description"),
        new("PM", "Project Manager", "rbac.role.PM.label", "rbac.role.PM.description"),
        new("QS", "Quantity Surveyor", "rbac.role.QS.label", "rbac.role.QS.description"),
        new("ACCOUNTANT", "Accountant", "rbac.role.ACCOUNTANT.label", "rbac.role.ACCOUNTANT.description"),
        new("WAREHOUSE", "Warehouse", "rbac.role.WAREHOUSE.label", "rbac.role.WAREHOUSE.description"),
        new("BGD", "Board of Directors", "rbac.role.BGD.label", "rbac.role.BGD.description"),
    ];

    public sealed record BusinessRole(string Code, string Name, string LabelKey, string DescriptionKey);
}
