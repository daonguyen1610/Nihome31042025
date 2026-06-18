using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        _notificationSvc = new NotificationService(_db);
        var service = new UserService(_db, new PasswordService(), _notificationSvc);
        _sut = new UsersController(service)
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
    public async Task Create_ReturnsConflict_WhenPhoneExists()
    {
        await SeedUser("0910000002", "Existing", UserRole.USER);

        var result = await _sut.Create(new CreateUserRequest
        {
            PhoneNumber = "0910000002",
            FullName = "Duplicate",
            Password = "Secret123",
            Role = "USER",
        });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WhenRequestIsValid()
    {
        var result = await _sut.Create(new CreateUserRequest
        {
            PhoneNumber = "0910000003",
            FullName = "Created User",
            Password = "Secret123",
            Role = "ADMIN",
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<UserDetailResponse>(created.Value);
        Assert.Equal("ADMIN", response.Role);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenChangingOwnRole()
    {
        var current = await SeedUser("0910000004", "Current", UserRole.SUPER_ADMIN);
        _sut.ControllerContext.HttpContext.User = BuildUserPrincipal(current.Id);

        var result = await _sut.Update(current.Id, new UpdateUserRequest { Role = "ADMIN" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
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
    public async Task Create_SendsWelcomeNotificationToNewUser()
    {
        var result = await _sut.Create(new CreateUserRequest
        {
            PhoneNumber = "0910000099",
            FullName = "New Person",
            Password = "Secret123",
            Role = "USER",
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var user = Assert.IsType<UserDetailResponse>(created.Value);

        Assert.Equal(1, _db.Notifications.Count());
        var notification = _db.Notifications.Single();
        Assert.Equal(user.Id, notification.UserId);
        Assert.Equal("User", notification.Module);
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

    private async Task<ApplicationUser> SeedUser(string phone, string name, UserRole role)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phone,
            FullName = name,
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
