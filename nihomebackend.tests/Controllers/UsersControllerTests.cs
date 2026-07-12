using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Controllers;

public class UsersControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UsersController _sut;
    private readonly NotificationService _notificationSvc;

    public UsersControllerTests()
    {
        _db = DbContextFactory.Create();
        _notificationSvc = NotificationServiceTestFactory.Create(_db);
        var service = new UserService(_db, new PasswordService(), _notificationSvc);
        var idempotency = new IdempotencyService(_db, Mock.Of<ILogger<IdempotencyService>>());
        var fingerprint = new FingerprintService();
        _sut = new UsersController(service, idempotency, fingerprint)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = BuildUserPrincipal(100),
                },
            },
        };
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAll_ReturnsOk_WithUserList()
    {
        await SeedUser("0910000001", "Admin", UserRole.ADMIN);

        var result = await _sut.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<UserListResponse>(ok.Value);
        Assert.Single(response.Items);
    }

    [Fact]
    public async Task Create_Throws_DuplicatePhoneNumber_WhenPhoneExists()
    {
        await SeedUser("0910000002", "Existing", UserRole.USER);

        // Domain exception bubbles up — the global exception handler turns it
        // into a 409 at the HTTP boundary.
        var ex = await Assert.ThrowsAsync<UserServiceException>(() => _sut.Create(new CreateUserRequest
        {
            PhoneNumber = "0910000002",
            FullName = "Duplicate",
            Email = "dup@example.com",
            Password = "Secret123",
            Role = "USER",
        }, idempotencyKey: null, CancellationToken.None));

        Assert.Equal(UserServiceError.DuplicatePhoneNumber, ex.Error);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WhenRequestIsValid()
    {
        var result = await _sut.Create(new CreateUserRequest
        {
            PhoneNumber = "0910000003",
            FullName = "Created User",
            Email = "created@example.com",
            Password = "Secret123",
            Role = "ADMIN",
        }, idempotencyKey: null, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<UserDetailResponse>(created.Value);
        Assert.Equal("ADMIN", response.Role);
    }

    [Fact]
    public async Task Create_Throws_DuplicateEmail_WhenEmailAlreadyUsed()
    {
        await SeedUser("0910009001", "Existing", UserRole.USER, email: "shared@example.com");

        var ex = await Assert.ThrowsAsync<UserServiceException>(() => _sut.Create(new CreateUserRequest
        {
            PhoneNumber = "0910009002",
            FullName = "Another",
            Email = "SHARED@Example.com",
            Password = "Secret123",
            Role = "USER",
        }, idempotencyKey: null, CancellationToken.None));

        Assert.Equal(UserServiceError.DuplicateEmail, ex.Error);
    }

    [Fact]
    public async Task Update_Throws_SelfActionNotAllowed_WhenChangingOwnRole()
    {
        var current = await SeedUser("0910000004", "Current", UserRole.SUPER_ADMIN);
        _sut.ControllerContext.HttpContext.User = BuildUserPrincipal(current.Id);

        var ex = await Assert.ThrowsAsync<UserServiceException>(() => _sut.Update(
            current.Id,
            new UpdateUserRequest { Role = "ADMIN" },
            idempotencyKey: null,
            CancellationToken.None));

        Assert.Equal(UserServiceError.SelfActionNotAllowed, ex.Error);
    }

    [Fact]
    public async Task Update_Throws_DuplicateEmail_WhenEmailTakenByAnotherUser()
    {
        await SeedUser("0910009101", "Taker", UserRole.USER, email: "unique@example.com");
        var target = await SeedUser("0910009102", "Target", UserRole.USER, email: "target@example.com");

        var ex = await Assert.ThrowsAsync<UserServiceException>(() => _sut.Update(
            target.Id,
            new UpdateUserRequest { Email = "unique@example.com" },
            idempotencyKey: null,
            CancellationToken.None));

        Assert.Equal(UserServiceError.DuplicateEmail, ex.Error);
    }

    [Fact]
    public async Task Create_StoresIdempotencyRecord_WhenKeyProvided()
    {
        const string key = "create-key-1";

        var result = await _sut.Create(new CreateUserRequest
        {
            PhoneNumber = "0910009201",
            FullName = "Once",
            Email = "once@example.com",
            Password = "Secret123",
            Role = "USER",
        }, key, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result.Result);

        var record = _db.IdempotencyRecords.Single(r => r.Key == key);
        Assert.Equal("users.admin.create", record.Scope);
        Assert.Equal(201, record.StatusCode);
    }

    [Fact]
    public async Task Delete_ReturnsNoContentAndSoftDeletes()
    {
        var actor = await SeedUser("0910000005", "Actor", UserRole.SUPER_ADMIN);
        var target = await SeedUser("0910000006", "Target", UserRole.USER);
        _sut.ControllerContext.HttpContext.User = BuildUserPrincipal(actor.Id);

        var result = await _sut.Delete(target.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.False(_db.Users.Find(target.Id)!.IsActive);
    }

    [Fact]
    public async Task GetRoles_ReturnsRoleCatalog()
    {
        await SeedUser("0910000007", "Admin", UserRole.ADMIN);

        var result = await _sut.GetRoles();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RoleCatalogResponse>(ok.Value);
        Assert.Equal(3, response.Roles.Count);
    }

    [Fact]
    public async Task Create_SendsAdminNotification()
    {
        var admin = await SeedUser("0910000090", "Admin", UserRole.ADMIN);

        await _sut.Create(new CreateUserRequest
        {
            PhoneNumber = "0910000099",
            FullName = "New Person",
            Email = "new@example.com",
            Password = "Secret123",
            Role = "USER",
        }, idempotencyKey: null, CancellationToken.None);

        Assert.Equal(1, _db.Notifications.Count());
        var notification = _db.Notifications.Single();
        Assert.Equal(admin.Id, notification.UserId);
        Assert.Equal("User", notification.Module);
        Assert.Contains("/admin/users", notification.LinkUrl);
    }

    [Fact]
    public async Task ToggleActive_SendsStatusNotificationToUser()
    {
        var actor = await SeedUser("0910000097", "Actor", UserRole.SUPER_ADMIN);
        var target = await SeedUser("0910000098", "Target", UserRole.USER);
        _sut.ControllerContext.HttpContext.User = BuildUserPrincipal(actor.Id);

        await _sut.ToggleActive(target.Id);

        Assert.Equal(1, _db.Notifications.Count());
        var notification = _db.Notifications.Single();
        Assert.Equal(target.Id, notification.UserId);
        Assert.Equal("User", notification.Module);
    }

    private async Task<ApplicationUser> SeedUser(
        string phone,
        string name,
        UserRole role,
        string? email = null)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phone,
            FullName = name,
            Email = email ?? $"seed-{phone}@test.com",
            Role = role,
            PasswordHash = "hashed",
            IsActive = true,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private static ClaimsPrincipal BuildUserPrincipal(int userId) => new(
        new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, "SUPER_ADMIN"),
            ],
            "Test"));
}
