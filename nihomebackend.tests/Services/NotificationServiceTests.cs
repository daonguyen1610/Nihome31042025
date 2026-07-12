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
        _sut = NotificationServiceTestFactory.Create(_db);
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

    // -------------- NIH-381: NotifyFromTemplateAsync + channel routing --------------

    [Fact]
    public async Task NotifyFromTemplateAsync_ReturnsNull_WhenTemplateMissing()
    {
        var user = await SeedUserAsync(UserRole.ADMIN);

        var result = await _sut.NotifyFromTemplateAsync(user.Id, "nope");

        Assert.Null(result);
        Assert.Empty(_db.Notifications);
    }

    [Fact]
    public async Task NotifyFromTemplateAsync_ReturnsNull_WhenTemplateInactive()
    {
        var user = await SeedUserAsync(UserRole.ADMIN);
        await SeedTemplateAsync("quote.approved", channel: NotificationChannel.InApp, isActive: false);

        var result = await _sut.NotifyFromTemplateAsync(user.Id, "quote.approved");

        Assert.Null(result);
        Assert.Empty(_db.Notifications);
    }

    [Fact]
    public async Task NotifyFromTemplateAsync_ReturnsNull_WhenUserInactive()
    {
        var user = await SeedUserAsync(UserRole.ADMIN, isActive: false);
        await SeedTemplateAsync("lead.assigned", channel: NotificationChannel.InApp);

        var result = await _sut.NotifyFromTemplateAsync(user.Id, "lead.assigned");

        Assert.Null(result);
        Assert.Empty(_db.Notifications);
    }

    [Fact]
    public async Task NotifyFromTemplateAsync_InApp_RendersTitle_AndPersistsRow()
    {
        var user = await SeedUserAsync(UserRole.USER);
        await SeedTemplateAsync("lead.assigned");
        SeedTranslation("notification.lead.assigned.title", "vi", "Bạn có Lead mới: {{leadName}}");
        SeedTranslation("notification.lead.assigned.body", "vi", "Lead {{leadName}} từ {{leadSource}}.");
        await _db.SaveChangesAsync();

        var data = new Dictionary<string, string>
        {
            ["leadName"] = "Acme JSC",
            ["leadSource"] = "Sự kiện",
        };
        var result = await _sut.NotifyFromTemplateAsync(user.Id, "lead.assigned", data,
            refEntityType: "Lead", refEntityId: 42);

        Assert.NotNull(result);
        Assert.Equal("Bạn có Lead mới: Acme JSC", result!.Title);
        Assert.Equal("Lead Acme JSC từ Sự kiện.", result.Body);

        var row = _db.Notifications.Single();
        Assert.Equal(user.Id, row.UserId);
        Assert.Equal("lead.assigned", row.TemplateCode);
        Assert.Equal("Lead", row.RefEntityType);
        Assert.Equal(42, row.RefEntityId);
        Assert.Equal("crm.leads", row.Module);
    }

    [Fact]
    public async Task NotifyFromTemplateAsync_UsesRequestedLanguage()
    {
        var user = await SeedUserAsync(UserRole.USER);
        await SeedTemplateAsync("lead.assigned");
        SeedTranslation("notification.lead.assigned.title", "vi", "VI title");
        SeedTranslation("notification.lead.assigned.title", "en", "EN title");
        SeedTranslation("notification.lead.assigned.body", "vi", "VI body");
        SeedTranslation("notification.lead.assigned.body", "en", "EN body");
        await _db.SaveChangesAsync();

        var enResult = await _sut.NotifyFromTemplateAsync(user.Id, "lead.assigned", languageCode: "en");
        var viResult = await _sut.NotifyFromTemplateAsync(user.Id, "lead.assigned", languageCode: "vi");

        Assert.Equal("EN title", enResult!.Title);
        Assert.Equal("VI title", viResult!.Title);
    }

    [Fact]
    public async Task NotifyFromTemplateAsync_FallsBackToKey_WhenTranslationMissing()
    {
        var user = await SeedUserAsync(UserRole.USER);
        await SeedTemplateAsync("lead.assigned");
        // No translation seeded — should surface the raw key so the miss is visible.
        var result = await _sut.NotifyFromTemplateAsync(user.Id, "lead.assigned");

        Assert.Equal("notification.lead.assigned.title", result!.Title);
    }

    [Fact]
    public async Task NotifyFromTemplateAsync_LeavesUnknownPlaceholderInPlace()
    {
        var user = await SeedUserAsync(UserRole.USER);
        await SeedTemplateAsync("lead.assigned");
        SeedTranslation("notification.lead.assigned.title", "vi", "Hi {{missing}}");
        await _db.SaveChangesAsync();

        var result = await _sut.NotifyFromTemplateAsync(user.Id, "lead.assigned",
            new Dictionary<string, string> { ["other"] = "x" });

        Assert.Equal("Hi {{missing}}", result!.Title);
    }

    [Fact]
    public async Task NotifyFromTemplateAsync_EmailChannel_DoesNotCreateInAppRow_ButSendsEmail()
    {
        var email = new CapturingEmailService();
        var sut = NotificationServiceTestFactory.Create(_db, email);

        var user = await SeedUserAsync(UserRole.USER, email: "sales@nihome.test");
        await SeedTemplateAsync("permit.expiring-soon", channel: NotificationChannel.Email);
        SeedTranslation("notification.permit.expiring-soon.title", "vi", "Permit expiring");
        SeedTranslation("notification.permit.expiring-soon.body", "vi", "Body");
        await _db.SaveChangesAsync();

        var result = await sut.NotifyFromTemplateAsync(user.Id, "permit.expiring-soon");

        Assert.Null(result); // no in-app row for Email-only
        Assert.Empty(_db.Notifications);
        Assert.Single(email.Sent);
        Assert.Equal("sales@nihome.test", email.Sent[0].To);
        Assert.Equal("Permit expiring", email.Sent[0].Subject);
    }

    [Fact]
    public async Task NotifyFromTemplateAsync_BothChannel_CreatesRowAndSendsEmail()
    {
        var email = new CapturingEmailService();
        var sut = NotificationServiceTestFactory.Create(_db, email);

        var user = await SeedUserAsync(UserRole.USER, email: "sales@nihome.test");
        await SeedTemplateAsync("quote.submitted-for-approval", channel: NotificationChannel.Both);
        SeedTranslation("notification.quote.submitted-for-approval.title", "vi", "Cần duyệt");
        SeedTranslation("notification.quote.submitted-for-approval.body", "vi", "Chi tiết");
        await _db.SaveChangesAsync();

        var result = await sut.NotifyFromTemplateAsync(user.Id, "quote.submitted-for-approval");

        Assert.NotNull(result);
        Assert.Single(_db.Notifications);
        Assert.Single(email.Sent);
    }

    [Fact]
    public async Task NotifyFromTemplateAsync_EmailFailure_DoesNotRollBackInAppRow()
    {
        var email = new CapturingEmailService { ThrowOnSend = true };
        var sut = NotificationServiceTestFactory.Create(_db, email);

        var user = await SeedUserAsync(UserRole.USER, email: "sales@nihome.test");
        await SeedTemplateAsync("quote.submitted-for-approval", channel: NotificationChannel.Both);
        SeedTranslation("notification.quote.submitted-for-approval.title", "vi", "T");
        SeedTranslation("notification.quote.submitted-for-approval.body", "vi", "B");
        await _db.SaveChangesAsync();

        var result = await sut.NotifyFromTemplateAsync(user.Id, "quote.submitted-for-approval");

        // In-app row must still be persisted so the user sees the notification
        // even if SMTP is down.
        Assert.NotNull(result);
        Assert.Single(_db.Notifications);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task NotifyFromTemplateAsync_EmailChannel_SkipsUserWithoutEmail()
    {
        var email = new CapturingEmailService();
        var sut = NotificationServiceTestFactory.Create(_db, email);

        var user = await SeedUserAsync(UserRole.USER, email: "");
        await SeedTemplateAsync("permit.expiring-soon", channel: NotificationChannel.Email);
        SeedTranslation("notification.permit.expiring-soon.title", "vi", "T");
        await _db.SaveChangesAsync();

        var result = await sut.NotifyFromTemplateAsync(user.Id, "permit.expiring-soon");

        Assert.Null(result);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task NotifyManyFromTemplateAsync_CountsOnlyInAppRows()
    {
        var u1 = await SeedUserAsync(UserRole.USER);
        var u2 = await SeedUserAsync(UserRole.USER);
        await SeedTemplateAsync("design.revision.created", channel: NotificationChannel.InApp);
        SeedTranslation("notification.design.revision.created.title", "vi", "R");
        SeedTranslation("notification.design.revision.created.body", "vi", "B");
        await _db.SaveChangesAsync();

        var count = await _sut.NotifyManyFromTemplateAsync(new[] { u1.Id, u2.Id }, "design.revision.created");

        Assert.Equal(2, count);
        Assert.Equal(2, _db.Notifications.Count());
    }

    [Fact]
    public async Task UpdateTemplateAsync_UpdatesChannelAndActiveFlag()
    {
        var template = await SeedTemplateAsync("quote.approved", channel: NotificationChannel.InApp);

        var updated = await _sut.UpdateTemplateAsync("quote.approved", NotificationChannel.Both, isActive: false);

        Assert.NotNull(updated);
        Assert.Equal(NotificationChannel.Both, updated!.Channel);
        Assert.False(updated.IsActive);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateTemplateAsync_ReturnsNull_ForUnknownCode()
    {
        var result = await _sut.UpdateTemplateAsync("nope", NotificationChannel.Both, true);
        Assert.Null(result);
    }

    private async Task<ApplicationUser> SeedUserAsync(UserRole role, bool isActive = true, string? email = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            PhoneNumber = suffix,
            PasswordHash = "hash",
            Role = role,
            IsActive = isActive,
            Email = email ?? $"user-{suffix}@nihome.test",
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<NotificationTemplate> SeedTemplateAsync(
        string code,
        NotificationChannel channel = NotificationChannel.InApp,
        bool isActive = true)
    {
        var module = code.Split('.', 2)[0] switch
        {
            "lead" => "crm.leads",
            "quote" => "crm.quotes",
            "contract" => "crm.contracts",
            "design" => "design.revisions",
            "permit" => "permit.checklists",
            _ => "system",
        };
        var t = new NotificationTemplate
        {
            Code = code,
            Module = module,
            TitleKey = $"notification.{code}.title",
            BodyKey = $"notification.{code}.body",
            Channel = channel,
            IsActive = isActive,
        };
        _db.NotificationTemplates.Add(t);
        await _db.SaveChangesAsync();
        return t;
    }

    private void SeedTranslation(string key, string lang, string value)
    {
        _db.Translations.Add(new Translation
        {
            Key = key,
            LanguageCode = lang,
            Value = value,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
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
