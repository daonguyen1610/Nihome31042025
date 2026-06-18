using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
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
            Password = "Secret123",
            Role = "USER",
        }));

        Assert.Equal(UserServiceError.DuplicatePhoneNumber, ex.Error);
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

    private async Task<ApplicationUser> SeedUser(
        string phone,
        string name,
        UserRole role,
        bool isActive = true,
        string? email = null)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phone,
            FullName = name,
            Email = email,
            Role = role,
            IsActive = isActive,
            PasswordHash = "hashed",
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }
}
