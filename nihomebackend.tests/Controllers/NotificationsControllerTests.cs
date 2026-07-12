using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Controllers;

public class NotificationsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly NotificationService _service;
    private readonly NotificationsController _sut;

    public NotificationsControllerTests()
    {
        _db = DbContextFactory.Create();
        _service = NotificationServiceTestFactory.Create(_db);
        _sut = new NotificationsController(_service);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAll_ReturnsUnauthorized_WhenUserClaimMissing()
    {
        var result = await _sut.GetAll();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyCurrentUserNotifications()
    {
        var user = await SeedUserAsync();
        var other = await SeedUserAsync();
        await _service.CreateAsync(user.Id, "System", "Mine");
        await _service.CreateAsync(other.Id, "System", "Other");
        SetUser(user.Id);

        var result = await _sut.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsAssignableFrom<List<NotificationResponse>>(ok.Value);
        Assert.Single(items);
        Assert.Equal("Mine", items[0].Title);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsCountForCurrentUser()
    {
        var user = await SeedUserAsync();
        await _service.CreateAsync(user.Id, "System", "Unread");
        SetUser(user.Id);

        var result = await _sut.GetUnreadCount();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("count = 1", ok.Value?.ToString());
    }

    [Fact]
    public async Task MarkRead_ReturnsNotFound_ForWrongUserNotification()
    {
        var user = await SeedUserAsync();
        var other = await SeedUserAsync();
        var notification = await _service.CreateAsync(other.Id, "System", "Other");
        SetUser(user.Id);

        var result = await _sut.MarkRead(notification.Id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_ForOwnNotification()
    {
        var user = await SeedUserAsync();
        var notification = await _service.CreateAsync(user.Id, "System", "Mine");
        SetUser(user.Id);

        var result = await _sut.Delete(notification.Id);

        Assert.IsType<NoContentResult>(result);
    }

    private async Task<ApplicationUser> SeedUserAsync()
    {
        var user = new ApplicationUser
        {
            PhoneNumber = Guid.NewGuid().ToString("N")[..12],
            PasswordHash = "hash",
            Role = UserRole.ADMIN,
            IsActive = true,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private void SetUser(int userId)
    {
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    "TestAuth"))
            }
        };
    }
}
