using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

/// <summary>
/// Security-critical: PermissionService is what every authorization check will
/// ultimately consult. Tests must cover every fail-closed branch so a regression
/// cannot accidentally grant privileges.
/// </summary>
public class PermissionServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PermissionService _svc;

    public PermissionServiceTests()
    {
        _db = DbContextFactory.Create();
        RbacSeeder.Seed(_db);
        _svc = new PermissionService(_db);
    }

    public void Dispose() => _db.Dispose();

    private ApplicationUser AddUser(string phone, UserRole legacyRole, int? roleEntityId = null, bool isActive = true)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phone,
            FullName = phone,
            PasswordHash = "x",
            Role = legacyRole,
            RoleEntityId = roleEntityId,
            IsActive = isActive,
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task SuperAdmin_ResolvesToFullCatalog()
    {
        var sa = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin);
        var user = AddUser("0900000001", UserRole.SUPER_ADMIN, sa.Id);

        var perms = await _svc.GetForUserAsync(user.Id);

        Assert.Equal(_db.Permissions.Count(), perms.Count);
        Assert.Contains("users.manage", perms);
    }

    [Fact]
    public async Task Admin_DoesNotIncludeUsersManageByDefaultPolicy()
    {
        var admin = _db.Roles.Single(r => r.Code == SystemRoleCodes.Admin);
        var user = AddUser("0900000002", UserRole.ADMIN, admin.Id);

        var perms = await _svc.GetForUserAsync(user.Id);

        Assert.Contains("rbac.roles.manage", perms);
        Assert.DoesNotContain("users.manage", perms);
    }

    [Fact]
    public async Task User_OnlyGetsProfileMePermissions()
    {
        var userRole = _db.Roles.Single(r => r.Code == SystemRoleCodes.User);
        var user = AddUser("0900000003", UserRole.USER, userRole.Id);

        var perms = await _svc.GetForUserAsync(user.Id);

        Assert.Equal(new HashSet<string> { "profile.me.view", "profile.me.update" }, perms);
    }

    [Fact]
    public async Task InactiveUser_ResolvesToEmpty_EvenIfRoleIsSuperAdmin()
    {
        var sa = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin);
        var user = AddUser("0900000004", UserRole.SUPER_ADMIN, sa.Id, isActive: false);

        var perms = await _svc.GetForUserAsync(user.Id);

        Assert.Empty(perms);
    }

    [Fact]
    public async Task InactiveRole_ResolvesToEmpty_AndDoesNotFallBackToEnum()
    {
        // Security guarantee: if admin deactivates a role, the user must NOT
        // silently inherit privileges via the legacy enum fallback.
        var admin = _db.Roles.Single(r => r.Code == SystemRoleCodes.Admin);
        admin.IsActive = false;
        _db.SaveChanges();

        var user = AddUser("0900000005", UserRole.ADMIN, admin.Id);

        var perms = await _svc.GetForUserAsync(user.Id);

        Assert.Empty(perms);
    }

    [Fact]
    public async Task NoRoleEntityId_FallsBackToActiveSystemRoleMatchingEnum()
    {
        var user = AddUser("0900000006", UserRole.ADMIN, roleEntityId: null);

        var perms = await _svc.GetForUserAsync(user.Id);

        Assert.NotEmpty(perms);
        Assert.Contains("rbac.roles.view", perms);
        Assert.DoesNotContain("users.manage", perms);
    }

    [Fact]
    public async Task NoRoleEntityId_AndEnumIsCustom_ResolvesToEmpty()
    {
        // A user could be stored with RoleEntityId=null and an enum value that
        // does not map to any SYSTEM role (e.g. corrupted legacy data).
        // Must fail closed.
        var user = new ApplicationUser
        {
            PhoneNumber = "0900000007",
            FullName = "x",
            PasswordHash = "x",
            // Force a non-system role via reflection-free trick: set enum to USER
            // then null the FK and rename the user's role at runtime. We can
            // simulate "unmapped" by setting RoleEntityId to null and changing
            // the role to a value that's not in SystemRoleCodes — but the enum
            // only has USER/ADMIN/SUPER_ADMIN, all of which are system. So this
            // tests the realistic path: NO FK + legacy enum still works
            // (covered above). Here we make the system role inactive so the
            // lookup yields null.
            Role = UserRole.USER,
            RoleEntityId = null,
            IsActive = true,
        };
        _db.Users.Add(user);
        _db.SaveChanges();

        var userRole = _db.Roles.Single(r => r.Code == SystemRoleCodes.User);
        userRole.IsActive = false;
        _db.SaveChanges();

        var perms = await _svc.GetForUserAsync(user.Id);

        Assert.Empty(perms);
    }

    [Fact]
    public async Task UnknownUserId_ResolvesToEmpty()
    {
        var perms = await _svc.GetForUserAsync(999_999);
        Assert.Empty(perms);
    }

    [Fact]
    public async Task ZeroOrNegativeUserId_ResolvesToEmpty()
    {
        Assert.Empty(await _svc.GetForUserAsync(0));
        Assert.Empty(await _svc.GetForUserAsync(-1));
    }

    [Fact]
    public async Task InactivePermissionRow_IsExcluded()
    {
        // Deactivating a permission catalog entry must immediately revoke it
        // for every role that had it via role_permissions.
        var sa = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin);
        var perm = _db.Permissions.Single(p => p.Module == "users" && p.Action == "manage");
        perm.IsActive = false;
        _db.SaveChanges();

        var user = AddUser("0900000008", UserRole.SUPER_ADMIN, sa.Id);

        var perms = await _svc.GetForUserAsync(user.Id);

        Assert.DoesNotContain("users.manage", perms);
        Assert.Contains("dashboard.view", perms);
    }

    [Fact]
    public async Task HasAsync_ReturnsTrue_WhenCodeInSet()
    {
        var sa = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin);
        var user = AddUser("0900000009", UserRole.SUPER_ADMIN, sa.Id);

        Assert.True(await _svc.HasAsync(user.Id, "users.manage"));
    }

    [Fact]
    public async Task HasAsync_ReturnsFalse_ForUnknownCode()
    {
        var sa = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin);
        var user = AddUser("0900000010", UserRole.SUPER_ADMIN, sa.Id);

        Assert.False(await _svc.HasAsync(user.Id, "does.not.exist"));
    }

    [Fact]
    public async Task HasAsync_FailsClosed_OnEmptyOrWhitespaceCode()
    {
        var sa = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin);
        var user = AddUser("0900000011", UserRole.SUPER_ADMIN, sa.Id);

        Assert.False(await _svc.HasAsync(user.Id, ""));
        Assert.False(await _svc.HasAsync(user.Id, "   "));
        Assert.False(await _svc.HasAsync(user.Id, null!));
    }

    [Fact]
    public async Task RoleEntityIdTakesPrecedenceOverLegacyEnum()
    {
        // Bob's enum says USER but he has been promoted to ADMIN by setting
        // RoleEntityId. The new FK wins — he must NOT be capped to USER scope.
        var admin = _db.Roles.Single(r => r.Code == SystemRoleCodes.Admin);
        var user = AddUser("0900000012", UserRole.USER, admin.Id);

        var perms = await _svc.GetForUserAsync(user.Id);

        Assert.Contains("rbac.roles.manage", perms);
    }

    [Fact]
    public async Task AdminEditOfMatrix_IsReflectedImmediately()
    {
        // Strip rbac.roles.manage from ADMIN at runtime and verify the next
        // call reflects the change (no stale caching).
        var admin = _db.Roles.Single(r => r.Code == SystemRoleCodes.Admin);
        var user = AddUser("0900000013", UserRole.ADMIN, admin.Id);
        Assert.Contains("rbac.roles.manage", await _svc.GetForUserAsync(user.Id));

        var perm = _db.Permissions.Single(p => p.Module == "rbac.roles" && p.Action == "manage");
        var row = _db.RolePermissions.Single(rp => rp.RoleId == admin.Id && rp.PermissionId == perm.Id);
        _db.RolePermissions.Remove(row);
        _db.SaveChanges();

        Assert.DoesNotContain("rbac.roles.manage", await _svc.GetForUserAsync(user.Id));
    }
}
