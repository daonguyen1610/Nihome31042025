using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class NotificationServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new NotificationService(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateAsync_CreatesNotificationForUser()
    {
        var user = await SeedUserAsync(UserRole.ADMIN);

        var result = await _sut.CreateAsync(user.Id, "System", "Hello", "Body", "/admin");

        Assert.Equal("System", result.Module);
        Assert.Equal(1, _db.Notifications.Count());
    }

    [Fact]
    public async Task CreateForAdminsAsync_CreatesOnlyForActiveAdmins()
    {
        var superAdmin = await SeedUserAsync(UserRole.SUPER_ADMIN);
        var admin = await SeedUserAsync(UserRole.ADMIN);
        await SeedUserAsync(UserRole.ADMIN, isActive: false);
        await SeedUserAsync(UserRole.USER);

        var count = await _sut.CreateForAdminsAsync("Contact", "New contact");

        Assert.Equal(2, count);
        Assert.Contains(_db.Notifications, n => n.UserId == superAdmin.Id);
        Assert.Contains(_db.Notifications, n => n.UserId == admin.Id);
    }

    [Fact]
    public async Task GetForUserAsync_ReturnsOnlyCurrentUserNotifications()
    {
        var user = await SeedUserAsync(UserRole.ADMIN);
        var other = await SeedUserAsync(UserRole.ADMIN);
        await _sut.CreateAsync(other.Id, "System", "Other");
        await _sut.CreateAsync(user.Id, "System", "Mine");

        var result = await _sut.GetForUserAsync(user.Id);

        Assert.Single(result);
        Assert.Equal("Mine", result[0].Title);
    }

    [Fact]
    public async Task GetForUserAsync_ReturnsRequestedPageInNewestOrder()
    {
        var user = await SeedUserAsync(UserRole.ADMIN);
        var baseDate = new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc);
        await SeedNotificationAsync(user.Id, "Oldest", baseDate);
        await SeedNotificationAsync(user.Id, "Middle", baseDate.AddMinutes(1));
        await SeedNotificationAsync(user.Id, "Newest", baseDate.AddMinutes(2));

        var result = await _sut.GetForUserAsync(user.Id, skip: 1, take: 1);

        Assert.Single(result);
        Assert.Equal("Middle", result[0].Title);
    }

    [Fact]
    public async Task GetForUserAsync_ClampsPageSize()
    {
        var user = await SeedUserAsync(UserRole.ADMIN);
        var baseDate = new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < 105; index++)
        {
            await SeedNotificationAsync(user.Id, $"Notification {index}", baseDate.AddMinutes(index));
        }

        var result = await _sut.GetForUserAsync(user.Id, skip: -10, take: 500);

        Assert.Equal(100, result.Count);
        Assert.Equal("Notification 104", result[0].Title);
    }

    [Fact]
    public async Task GetUnreadCountAsync_CountsUnreadForUser()
    {
        var user = await SeedUserAsync(UserRole.ADMIN);
        var read = await _sut.CreateAsync(user.Id, "System", "Read");
        await _sut.CreateAsync(user.Id, "System", "Unread");
        await _sut.MarkReadAsync(read.Id, user.Id);

        var count = await _sut.GetUnreadCountAsync(user.Id);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MarkReadAsync_ReturnsNull_ForWrongUser()
    {
        var user = await SeedUserAsync(UserRole.ADMIN);
        var other = await SeedUserAsync(UserRole.ADMIN);
        var notification = await _sut.CreateAsync(user.Id, "System", "Mine");

        var result = await _sut.MarkReadAsync(notification.Id, other.Id);

        Assert.Null(result);
        Assert.False(_db.Notifications.Single(n => n.Id == notification.Id).IsRead);
    }

    [Fact]
    public async Task MarkAllReadAsync_MarksOnlyCurrentUserNotifications()
    {
        var user = await SeedUserAsync(UserRole.ADMIN);
        var other = await SeedUserAsync(UserRole.ADMIN);
        await _sut.CreateAsync(user.Id, "System", "One");
        await _sut.CreateAsync(user.Id, "System", "Two");
        await _sut.CreateAsync(other.Id, "System", "Other");

        var count = await _sut.MarkAllReadAsync(user.Id);

        Assert.Equal(2, count);
        Assert.Equal(0, await _sut.GetUnreadCountAsync(user.Id));
        Assert.Equal(1, await _sut.GetUnreadCountAsync(other.Id));
    }

    [Fact]
    public async Task DeleteAsync_DeletesOnlyCurrentUserNotification()
    {
        var user = await SeedUserAsync(UserRole.ADMIN);
        var other = await SeedUserAsync(UserRole.ADMIN);
        var notification = await _sut.CreateAsync(user.Id, "System", "Mine");

        Assert.False(await _sut.DeleteAsync(notification.Id, other.Id));
        Assert.True(await _sut.DeleteAsync(notification.Id, user.Id));
        Assert.Empty(_db.Notifications);
    }

    private async Task<ApplicationUser> SeedUserAsync(UserRole role, bool isActive = true)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = Guid.NewGuid().ToString("N")[..12],
            PasswordHash = "hash",
            Role = role,
            IsActive = isActive,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task SeedNotificationAsync(int userId, string title, DateTime createdAt)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Module = "System",
            Title = title,
            CreatedAt = createdAt,
        });

        await _db.SaveChangesAsync();
    }
}
