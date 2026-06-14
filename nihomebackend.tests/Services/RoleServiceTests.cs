using Microsoft.EntityFrameworkCore;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

/// <summary>
/// Security-critical: RoleService is the write surface for the runtime RBAC
/// matrix. Tests must cover every refusal branch (system-role immunity,
/// privilege escalation, unknown codes, deactivating system roles) so a
/// regression cannot silently grant excess privileges.
/// </summary>
public class RoleServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly RoleService _svc;
    private readonly PermissionService _permSvc;
    private readonly Mock<IAuditLogger> _audit = new();
    private readonly Mock<INotificationService> _notif = new();

    public RoleServiceTests()
    {
        _db = DbContextFactory.Create();
        RbacSeeder.Seed(_db);
        _permSvc = new PermissionService(_db);
        _svc = new RoleService(_db, _permSvc, _audit.Object, _notif.Object);
        _notif.Setup(n => n.CreateForAdminsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(0);
    }

    public void Dispose() => _db.Dispose();

    private ApplicationUser AddUser(string phone, UserRole legacyRole, int? roleEntityId = null, bool isActive = true)
    {
        var u = new ApplicationUser
        {
            PhoneNumber = phone,
            FullName = phone,
            PasswordHash = "x",
            Role = legacyRole,
            RoleEntityId = roleEntityId,
            IsActive = isActive,
        };
        _db.Users.Add(u);
        _db.SaveChanges();
        return u;
    }

    private int SuperAdminUserId()
    {
        var sa = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin);
        return AddUser($"09{Random.Shared.Next(10000000, 99999999)}", UserRole.SUPER_ADMIN, sa.Id).Id;
    }

    private int AdminUserId()
    {
        var admin = _db.Roles.Single(r => r.Code == SystemRoleCodes.Admin);
        return AddUser($"09{Random.Shared.Next(10000000, 99999999)}", UserRole.ADMIN, admin.Id).Id;
    }

    // ---------- Reads ----------

    [Fact]
    public async Task ListRoles_ReturnsAllSeededRoles_WithSystemRolesFirst()
    {
        var list = await _svc.ListRolesAsync();

        Assert.Contains(list, r => r.Code == SystemRoleCodes.SuperAdmin && r.IsSystem);
        Assert.Contains(list, r => r.Code == SystemRoleCodes.Admin && r.IsSystem);
        Assert.Contains(list, r => r.Code == SystemRoleCodes.User && r.IsSystem);
        // System roles ordered before business roles.
        var firstNonSystem = list.FindIndex(r => !r.IsSystem);
        if (firstNonSystem >= 0)
            Assert.All(list.Take(firstNonSystem), r => Assert.True(r.IsSystem));
    }

    [Fact]
    public async Task ListPermissions_ReturnsActiveCatalog()
    {
        var list = await _svc.ListPermissionsAsync();
        Assert.NotEmpty(list);
        Assert.All(list, p => Assert.False(string.IsNullOrWhiteSpace(p.Code)));
        Assert.Contains(list, p => p.Code == "rbac.roles.manage");
    }

    [Fact]
    public async Task GetRolePermissions_NullForUnknownRole()
    {
        var rp = await _svc.GetRolePermissionsAsync(999_999);
        Assert.Null(rp);
    }

    [Fact]
    public async Task GetRolePermissions_ReturnsRoleAndPermissionsForSuperAdmin()
    {
        var sa = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin);
        var rp = await _svc.GetRolePermissionsAsync(sa.Id);
        Assert.NotNull(rp);
        Assert.Equal(SystemRoleCodes.SuperAdmin, rp!.Role.Code);
        Assert.Equal(_db.Permissions.Count(), rp.Permissions.Count);
    }

    // ---------- UpdateRole ----------

    [Fact]
    public async Task UpdateRole_NotFound_WhenIdMissing()
    {
        var result = await _svc.UpdateRoleAsync(999_999, new UpdateRoleRequest { Name = "x" }, SuperAdminUserId());
        Assert.Equal(RoleWriteStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateRole_SuperAdminIsImmune()
    {
        var sa = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin);
        var result = await _svc.UpdateRoleAsync(sa.Id, new UpdateRoleRequest { Name = "Hacked" }, SuperAdminUserId());
        Assert.Equal(RoleWriteStatus.ForbiddenSystemRole, result.Status);
        Assert.NotEqual("Hacked", _db.Roles.AsNoTracking().Single(r => r.Id == sa.Id).Name);
    }

    [Fact]
    public async Task UpdateRole_CannotDeactivateSystemRole()
    {
        var admin = _db.Roles.Single(r => r.Code == SystemRoleCodes.Admin);
        var result = await _svc.UpdateRoleAsync(admin.Id, new UpdateRoleRequest { IsActive = false }, SuperAdminUserId());
        Assert.Equal(RoleWriteStatus.InvalidRequest, result.Status);
        Assert.True(_db.Roles.AsNoTracking().Single(r => r.Id == admin.Id).IsActive);
    }

    [Fact]
    public async Task UpdateRole_BusinessRole_AppliesLabelChangesAndEmitsAudit()
    {
        var biz = _db.Roles.First(r => !r.IsSystem);
        var actor = SuperAdminUserId();

        var result = await _svc.UpdateRoleAsync(biz.Id, new UpdateRoleRequest
        {
            Name = "Renamed",
            LabelKey = "rbac.role.x.label",
        }, actor);

        Assert.Equal(RoleWriteStatus.Success, result.Status);
        Assert.Equal("Renamed", result.Value!.Name);
        _audit.Verify(a => a.Log(It.Is<AuditEvent>(e =>
            e.ResourceType == "rbac.role" && e.ResourceId == biz.Code && e.Status == "success")), Times.Once);
        _notif.Verify(n => n.CreateForAdminsAsync("rbac", It.IsAny<string>(), biz.Code, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRole_NoChanges_DoesNotEmitAuditOrNotification()
    {
        var biz = _db.Roles.First(r => !r.IsSystem);
        var result = await _svc.UpdateRoleAsync(biz.Id, new UpdateRoleRequest(), SuperAdminUserId());
        Assert.Equal(RoleWriteStatus.Success, result.Status);
        _audit.Verify(a => a.Log(It.IsAny<AuditEvent>()), Times.Never);
        _notif.Verify(n => n.CreateForAdminsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    // ---------- UpdateRolePermissions ----------

    [Fact]
    public async Task UpdatePermissions_NotFound_WhenIdMissing()
    {
        var result = await _svc.UpdateRolePermissionsAsync(
            999_999, new UpdateRolePermissionsRequest { Permissions = ["dashboard.view"] }, SuperAdminUserId());
        Assert.Equal(RoleWriteStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdatePermissions_SuperAdminIsImmune()
    {
        var sa = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin);
        var before = _db.RolePermissions.Count(rp => rp.RoleId == sa.Id);

        var result = await _svc.UpdateRolePermissionsAsync(
            sa.Id, new UpdateRolePermissionsRequest { Permissions = ["dashboard.view"] }, SuperAdminUserId());

        Assert.Equal(RoleWriteStatus.ForbiddenSystemRole, result.Status);
        Assert.Equal(before, _db.RolePermissions.Count(rp => rp.RoleId == sa.Id));
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("USER")]
    public async Task UpdatePermissions_AnySystemRoleIsImmune_PreventsSelfLockout(string roleCode)
    {
        // ADMIN and USER matrices are sized by the seeder so an admin cannot
        // accidentally drop rbac.roles.manage (self-lockout) or strip USER's
        // profile.me.* (locks every non-admin user out of their own profile).
        var sys = _db.Roles.Single(r => r.Code == roleCode);
        var before = _db.RolePermissions.Where(rp => rp.RoleId == sys.Id)
            .Select(rp => rp.PermissionId).OrderBy(id => id).ToList();

        var result = await _svc.UpdateRolePermissionsAsync(
            sys.Id, new UpdateRolePermissionsRequest { Permissions = ["dashboard.view"] }, SuperAdminUserId());

        Assert.Equal(RoleWriteStatus.ForbiddenSystemRole, result.Status);
        var after = _db.RolePermissions.Where(rp => rp.RoleId == sys.Id)
            .Select(rp => rp.PermissionId).OrderBy(id => id).ToList();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task UpdatePermissions_RejectsUnknownCodes_AndPersistsNothing()
    {
        var biz = _db.Roles.First(r => !r.IsSystem);
        var before = _db.RolePermissions.Where(rp => rp.RoleId == biz.Id).Select(rp => rp.PermissionId).ToHashSet();

        var result = await _svc.UpdateRolePermissionsAsync(biz.Id, new UpdateRolePermissionsRequest
        {
            Permissions = ["dashboard.view", "does.not.exist", "also.bogus"],
        }, SuperAdminUserId());

        Assert.Equal(RoleWriteStatus.InvalidPermissionCodes, result.Status);
        Assert.Equal(new[] { "also.bogus", "does.not.exist" }, result.OffendingCodes);
        var after = _db.RolePermissions.Where(rp => rp.RoleId == biz.Id).Select(rp => rp.PermissionId).ToHashSet();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task UpdatePermissions_BlocksEscalationByNonSuperAdmin()
    {
        // Admin (default policy) does NOT have users.manage. They must not be
        // able to grant it to any role — including business roles they can
        // otherwise edit.
        var biz = _db.Roles.First(r => !r.IsSystem);
        var actor = AdminUserId();

        var result = await _svc.UpdateRolePermissionsAsync(biz.Id, new UpdateRolePermissionsRequest
        {
            Permissions = ["rbac.roles.view", "rbac.roles.manage", "users.manage"],
        }, actor);

        Assert.Equal(RoleWriteStatus.ForbiddenEscalation, result.Status);
        Assert.Contains("users.manage", result.OffendingCodes!);
        Assert.DoesNotContain(_db.RolePermissions.Include(rp => rp.Permission)
            .Where(rp => rp.RoleId == biz.Id)
            .Select(rp => rp.Permission.Module + "." + rp.Permission.Action),
            c => c == "users.manage");
    }

    [Fact]
    public async Task UpdatePermissions_SuperAdminCanGrantAnything()
    {
        var biz = _db.Roles.First(r => !r.IsSystem);
        var actor = SuperAdminUserId();

        var result = await _svc.UpdateRolePermissionsAsync(biz.Id, new UpdateRolePermissionsRequest
        {
            Permissions = ["dashboard.view", "users.manage"],
        }, actor);

        Assert.Equal(RoleWriteStatus.Success, result.Status);
        Assert.Contains("users.manage", result.Value!.Permissions);
    }

    [Fact]
    public async Task UpdatePermissions_ReplacesSet_AddsRemovedDiffAudited()
    {
        var biz = _db.Roles.First(r => !r.IsSystem);
        // Pre-set to a known small subset.
        var allPerms = _db.Permissions.ToList();
        var dash = allPerms.Single(p => p.Module == "dashboard" && p.Action == "view");
        var meView = allPerms.Single(p => p.Module == "profile.me" && p.Action == "view");
        _db.RolePermissions.RemoveRange(_db.RolePermissions.Where(rp => rp.RoleId == biz.Id));
        _db.RolePermissions.Add(new RolePermission { RoleId = biz.Id, PermissionId = dash.Id });
        _db.RolePermissions.Add(new RolePermission { RoleId = biz.Id, PermissionId = meView.Id });
        _db.SaveChanges();

        var result = await _svc.UpdateRolePermissionsAsync(biz.Id, new UpdateRolePermissionsRequest
        {
            // Drop profile.me.view, keep dashboard.view, add profile.me.update.
            Permissions = ["dashboard.view", "profile.me.update"],
        }, SuperAdminUserId());

        Assert.Equal(RoleWriteStatus.Success, result.Status);
        Assert.Equal(new[] { "dashboard.view", "profile.me.update" }, result.Value!.Permissions);
        _audit.Verify(a => a.Log(It.Is<AuditEvent>(e =>
            e.Action == "rbac.role.permissions.update" &&
            e.ResourceId == biz.Code &&
            e.Status == "success")), Times.Once);
    }

    [Fact]
    public async Task UpdatePermissions_NoChange_DoesNotEmitAuditOrNotification()
    {
        var biz = _db.Roles.First(r => !r.IsSystem);
        var current = _db.RolePermissions.Include(rp => rp.Permission)
            .Where(rp => rp.RoleId == biz.Id)
            .Select(rp => rp.Permission.Module + "." + rp.Permission.Action)
            .ToList();

        var result = await _svc.UpdateRolePermissionsAsync(biz.Id,
            new UpdateRolePermissionsRequest { Permissions = current }, SuperAdminUserId());

        Assert.Equal(RoleWriteStatus.Success, result.Status);
        _audit.Verify(a => a.Log(It.IsAny<AuditEvent>()), Times.Never);
        _notif.Verify(n => n.CreateForAdminsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePermissions_TrimsAndDeduplicatesInput()
    {
        var biz = _db.Roles.First(r => !r.IsSystem);
        var result = await _svc.UpdateRolePermissionsAsync(biz.Id, new UpdateRolePermissionsRequest
        {
            Permissions = ["  dashboard.view  ", "DASHBOARD.VIEW", "dashboard.view"],
        }, SuperAdminUserId());

        Assert.Equal(RoleWriteStatus.Success, result.Status);
        Assert.Equal(new[] { "dashboard.view" }, result.Value!.Permissions);
    }
}
