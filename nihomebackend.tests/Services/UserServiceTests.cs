using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class UserServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new UserService(_db, new PasswordService(), new NoOpNotificationService());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetListAsync_FiltersBySearchAndRole()
    {
        await SeedUser("0900000001", "Alice Admin", UserRole.ADMIN, email: "alice@nicon.vn");
        await SeedUser("0900000002", "Regular User", UserRole.USER, email: "user@nicon.vn");

        var result = await _sut.GetListAsync(0, 20, "alice", "ADMIN");

        Assert.Single(result.Items);
        Assert.Equal("Alice Admin", result.Items[0].FullName);
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task CreateAsync_ThrowsDuplicatePhoneNumber_WhenPhoneExists()
    {
        await SeedUser("0900000001", "Existing", UserRole.ADMIN);

        var ex = await Assert.ThrowsAsync<UserServiceException>(() => _sut.CreateAsync(new CreateUserRequest
        {
            PhoneNumber = "0900000001",
            FullName = "Duplicate",
            Email = "newdup@nicon.vn",
            Password = "Secret123",
            Role = "USER",
        }));

        Assert.Equal(UserServiceError.DuplicatePhoneNumber, ex.Error);
    }

    [Fact]
    public async Task CreateAsync_ThrowsDuplicateEmail_WhenEmailExists_CaseInsensitive()
    {
        await SeedUser("0900000020", "Existing", UserRole.USER, email: "shared@nicon.vn");

        var ex = await Assert.ThrowsAsync<UserServiceException>(() => _sut.CreateAsync(new CreateUserRequest
        {
            PhoneNumber = "0900000021",
            FullName = "Other",
            Email = "SHARED@Nicon.VN",
            Password = "Secret123",
            Role = "USER",
        }));

        Assert.Equal(UserServiceError.DuplicateEmail, ex.Error);
    }

    [Fact]
    public async Task CreateAsync_ThrowsDuplicateEmail_WhenEmailMissing()
    {
        var ex = await Assert.ThrowsAsync<UserServiceException>(() => _sut.CreateAsync(new CreateUserRequest
        {
            PhoneNumber = "0900000022",
            FullName = "No Email",
            Email = "   ",
            Password = "Secret123",
            Role = "USER",
        }));

        Assert.Equal(UserServiceError.DuplicateEmail, ex.Error);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsDuplicateEmail_WhenEmailBelongsToAnotherUser()
    {
        await SeedUser("0900000030", "Owner", UserRole.USER, email: "owner@nicon.vn");
        var target = await SeedUser("0900000031", "Target", UserRole.USER, email: "target@nicon.vn");

        var ex = await Assert.ThrowsAsync<UserServiceException>(() => _sut.UpdateAsync(
            target.Id,
            new UpdateUserRequest { Email = "owner@nicon.vn" },
            currentUserId: 999));

        Assert.Equal(UserServiceError.DuplicateEmail, ex.Error);
    }

    [Fact]
    public async Task CreateAsync_HashesPasswordAndReturnsDetail()
    {
        var result = await _sut.CreateAsync(new CreateUserRequest
        {
            PhoneNumber = "0900000003",
            FullName = "Created User",
            Email = "created@nicon.vn",
            Password = "Secret123",
            Role = "ADMIN",
        });

        var user = _db.Users.Single(u => u.Id == result.Id);
        Assert.Equal("ADMIN", result.Role);
        Assert.NotEqual("Secret123", user.PasswordHash);
        Assert.True(new PasswordService().Verify(user, "Secret123"));
    }

    [Fact]
    public async Task UpdateAsync_ThrowsInvalidRole_WhenRoleIsUnknown()
    {
        var user = await SeedUser("0900000004", "Target", UserRole.USER);

        var ex = await Assert.ThrowsAsync<UserServiceException>(() => _sut.UpdateAsync(
            user.Id,
            new UpdateUserRequest { Role = "OWNER" },
            currentUserId: 999));

        Assert.Equal(UserServiceError.InvalidRole, ex.Error);
    }

    [Fact]
    public async Task UpdateAsync_PreventsSelfRoleChange()
    {
        var user = await SeedUser("0900000005", "Self Admin", UserRole.ADMIN);

        var ex = await Assert.ThrowsAsync<UserServiceException>(() => _sut.UpdateAsync(
            user.Id,
            new UpdateUserRequest { Role = "USER" },
            currentUserId: user.Id));

        Assert.Equal(UserServiceError.SelfActionNotAllowed, ex.Error);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesUser()
    {
        var actor = await SeedUser("0900000006", "Actor", UserRole.SUPER_ADMIN);
        var target = await SeedUser("0900000007", "Target", UserRole.USER);

        var deleted = await _sut.DeleteAsync(target.Id, actor.Id);

        Assert.True(deleted);
        Assert.False(_db.Users.Find(target.Id)!.IsActive);
    }

    [Fact]
    public async Task DeleteAsync_PreventsDeletingLastActiveSuperAdmin()
    {
        var superAdmin = await SeedUser("0900000008", "Only Super Admin", UserRole.SUPER_ADMIN);

        var ex = await Assert.ThrowsAsync<UserServiceException>(() => _sut.DeleteAsync(superAdmin.Id, currentUserId: 999));

        Assert.Equal(UserServiceError.LastSuperAdmin, ex.Error);
    }

    [Fact]
    public async Task GetRoleCatalogAsync_ReturnsAllSystemRolesWithCounts()
    {
        await SeedUser("0900000009", "Super Admin", UserRole.SUPER_ADMIN);
        await SeedUser("0900000010", "Admin", UserRole.ADMIN);
        await SeedUser("0900000011", "User", UserRole.USER);

        var result = await _sut.GetRoleCatalogAsync();

        Assert.Equal(3, result.Roles.Count);
        Assert.Contains(result.Roles, r => r.Role == "SUPER_ADMIN" && r.UserCount == 1);
        Assert.Contains(result.Roles, r => r.Role == "ADMIN" && r.UserCount == 1);
        Assert.Contains(result.Roles, r => r.Role == "USER" && r.UserCount == 1);
        Assert.NotEmpty(result.PermissionMatrix);
    }

    // -------------------- RBAC role assignment --------------------

    [Fact]
    public async Task CreateAsync_WithSystemRoleCode_SetsRoleEntityIdAndMirrorEnum()
    {
        var adminRole = await SeedRole(SystemRoleCodes.Admin, "Quản trị viên", isSystem: true);

        var result = await _sut.CreateAsync(new CreateUserRequest
        {
            PhoneNumber = "0900001001",
            FullName = "Sys Admin",
            Email = "sysadmin@nicon.vn",
            Password = "Secret123",
            Role = SystemRoleCodes.Admin,
        });

        var user = _db.Users.Single(u => u.Id == result.Id);
        Assert.Equal(adminRole.Id, user.RoleEntityId);
        Assert.Equal(UserRole.ADMIN, user.Role);
        Assert.Equal(SystemRoleCodes.Admin, result.Role);
        Assert.Equal(adminRole.Id, result.RoleId);
        Assert.Equal("Quản trị viên", result.RoleName);
    }

    [Fact]
    public async Task CreateAsync_WithCustomRoleCode_SetsRoleEntityIdAndMirrorsAsUser()
    {
        var pmRole = await SeedRole("PROJECT_MANAGER", "Project Manager", isSystem: false);

        var result = await _sut.CreateAsync(new CreateUserRequest
        {
            PhoneNumber = "0900001002",
            FullName = "Custom Role User",
            Email = "pm@nicon.vn",
            Password = "Secret123",
            Role = "PROJECT_MANAGER",
        });

        var user = _db.Users.Single(u => u.Id == result.Id);
        Assert.Equal(pmRole.Id, user.RoleEntityId);
        // Mirror enum defaults to USER for custom roles so legacy queries
        // (NotificationService admin filter, JWT role claim) don't grant
        // accidental elevated access.
        Assert.Equal(UserRole.USER, user.Role);
        Assert.Equal("PROJECT_MANAGER", result.Role);
        Assert.Equal(pmRole.Id, result.RoleId);
        Assert.Equal("Project Manager", result.RoleName);
    }

    [Fact]
    public async Task CreateAsync_WithInactiveCustomRole_ThrowsInvalidRole()
    {
        await SeedRole("OPERATOR", "Operator", isSystem: false, isActive: false);

        var ex = await Assert.ThrowsAsync<UserServiceException>(() => _sut.CreateAsync(new CreateUserRequest
        {
            PhoneNumber = "0900001003",
            FullName = "Inactive Role User",
            Email = "op@nicon.vn",
            Password = "Secret123",
            Role = "OPERATOR",
        }));

        Assert.Equal(UserServiceError.InvalidRole, ex.Error);
    }

    [Fact]
    public async Task UpdateAsync_FromSystemToCustomRole_SwapsRoleEntityIdAndMirrors()
    {
        var adminRole = await SeedRole(SystemRoleCodes.Admin, "Admin");
        var pmRole = await SeedRole("PROJECT_MANAGER", "Project Manager", isSystem: false);
        var target = await SeedUser("0900001004", "Target", UserRole.ADMIN, roleEntityId: adminRole.Id);

        var result = await _sut.UpdateAsync(
            target.Id,
            new UpdateUserRequest { Role = "PROJECT_MANAGER" },
            currentUserId: 999);

        Assert.NotNull(result);
        var reloaded = _db.Users.AsNoTracking().Single(u => u.Id == target.Id);
        Assert.Equal(pmRole.Id, reloaded.RoleEntityId);
        Assert.Equal(UserRole.USER, reloaded.Role);
        Assert.Equal("PROJECT_MANAGER", result!.Role);
        Assert.Equal(pmRole.Id, result.RoleId);
    }

    [Fact]
    public async Task GetListAsync_FiltersByCustomRoleCode_MatchesRbacFkOnly()
    {
        var pmRole = await SeedRole("PROJECT_MANAGER", "Project Manager", isSystem: false);
        await SeedUser("0900001010", "PM User", UserRole.USER, roleEntityId: pmRole.Id);
        await SeedUser("0900001011", "Regular User", UserRole.USER); // no RoleEntityId

        var result = await _sut.GetListAsync(0, 20, search: null, role: "PROJECT_MANAGER");

        Assert.Equal(1, result.Total);
        Assert.Equal("PM User", result.Items[0].FullName);
        Assert.Equal(pmRole.Id, result.Items[0].RoleId);
        Assert.Equal("PROJECT_MANAGER", result.Items[0].Role);
    }

    [Fact]
    public async Task GetListAsync_FiltersBySystemRoleCode_MatchesBothRbacFkAndLegacyEnum()
    {
        var adminRole = await SeedRole(SystemRoleCodes.Admin, "Admin");
        await SeedUser("0900001020", "Backfilled Admin", UserRole.ADMIN, roleEntityId: adminRole.Id);
        await SeedUser("0900001021", "Legacy Admin", UserRole.ADMIN); // no RoleEntityId yet

        var result = await _sut.GetListAsync(0, 20, search: null, role: "ADMIN");

        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task DeleteAsync_CountsLastSuperAdminAcrossLegacyAndRbacFk()
    {
        var saRole = await SeedRole(SystemRoleCodes.SuperAdmin, "Super Admin");
        // One via RBAC FK, one via legacy enum only — together they form the quorum.
        await SeedUser("0900001030", "Backfilled SA", UserRole.SUPER_ADMIN, roleEntityId: saRole.Id);
        var legacySa = await SeedUser("0900001031", "Legacy SA", UserRole.SUPER_ADMIN);

        // Deleting the legacy one is allowed because the backfilled one remains.
        var ok = await _sut.DeleteAsync(legacySa.Id, currentUserId: 999);
        Assert.True(ok);
    }

    private async Task<Role> SeedRole(string code, string name, bool isSystem = true, bool isActive = true)
    {
        var role = new Role
        {
            Code = code,
            Name = name,
            IsSystem = isSystem,
            IsActive = isActive,
        };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    private async Task<ApplicationUser> SeedUser(
        string phone,
        string name,
        UserRole role,
        bool isActive = true,
        string? email = null,
        int? roleEntityId = null)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phone,
            FullName = name,
            Email = email ?? $"seed-{phone}@test.com",
            Role = role,
            RoleEntityId = roleEntityId,
            IsActive = isActive,
            PasswordHash = "hashed",
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }
}
