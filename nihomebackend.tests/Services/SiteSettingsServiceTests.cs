using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class SiteSettingsServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SiteSettingsService _sut;

    public SiteSettingsServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new SiteSettingsService(_db);
    }

    public void Dispose() => _db.Dispose();

    private void SeedSettings()
    {
        _db.SiteSettings.Add(new SiteSettings
        {
            SiteName = "Nihome",
            PrimaryEmail = "nihome@nihome.vn",
            NotificationEmail = "hr@nihome.vn",
            NewApplicationEmailSubjectTemplate = "Old subject",
            NewApplicationEmailBodyTemplate = "<p>Old body</p>",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNoSettings()
    {
        var result = await _sut.GetAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_ReturnsSettings_WhenExists()
    {
        SeedSettings();
        var result = await _sut.GetAsync();

        Assert.NotNull(result);
        Assert.Equal("Nihome", result!.SiteName);
        Assert.Equal("hr@nihome.vn", result.NotificationEmail);
    }

    [Fact]
    public async Task UpdateEmailTemplatesAsync_UpdatesAllFields()
    {
        SeedSettings();

        var result = await _sut.UpdateEmailTemplatesAsync(
            "New subject: {{candidateName}}",
            "<b>{{candidateName}}</b>",
            "new-hr@nihome.vn");

        Assert.Equal("New subject: {{candidateName}}", result.NewApplicationEmailSubjectTemplate);
        Assert.Equal("<b>{{candidateName}}</b>", result.NewApplicationEmailBodyTemplate);
        Assert.Equal("new-hr@nihome.vn", result.NotificationEmail);

        // Verify persisted
        var fromDb = _db.SiteSettings.First();
        Assert.Equal("New subject: {{candidateName}}", fromDb.NewApplicationEmailSubjectTemplate);
    }

    [Fact]
    public async Task UpdateEmailTemplatesAsync_TrimsWhitespace()
    {
        SeedSettings();

        var result = await _sut.UpdateEmailTemplatesAsync(
            "  subject  ",
            "  <p>body</p>  ",
            "  email@test.com  ");

        Assert.Equal("subject", result.NewApplicationEmailSubjectTemplate);
        Assert.Equal("<p>body</p>", result.NewApplicationEmailBodyTemplate);
        Assert.Equal("email@test.com", result.NotificationEmail);
    }

    [Fact]
    public async Task UpdateEmailTemplatesAsync_AllowsNullValues()
    {
        SeedSettings();

        var result = await _sut.UpdateEmailTemplatesAsync(null, null, null);

        Assert.Null(result.NewApplicationEmailSubjectTemplate);
        Assert.Null(result.NewApplicationEmailBodyTemplate);
        Assert.Null(result.NotificationEmail);
    }

    [Fact]
    public async Task UpdateEmailTemplatesAsync_ThrowsWhenNoSettings()
    {
        // No settings seeded
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateEmailTemplatesAsync("s", "b", "e"));

        Assert.Contains("chưa được khởi tạo", ex.Message);
    }

    [Fact]
    public async Task UpdateEmailTemplatesAsync_UpdatesTimestamp()
    {
        SeedSettings();
        var before = DateTime.UtcNow;

        await _sut.UpdateEmailTemplatesAsync("s", "b", "e@e.com");

        var settings = _db.SiteSettings.First();
        Assert.True(settings.UpdatedAt >= before);
    }
}
