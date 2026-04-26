using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Controllers;

public class SiteSettingsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SiteSettingsController _sut;

    public SiteSettingsControllerTests()
    {
        _db = DbContextFactory.Create();
        var svc = new SiteSettingsService(_db);
        _sut = new SiteSettingsController(svc);
    }

    public void Dispose() => _db.Dispose();

    private void SeedSettings()
    {
        _db.SiteSettings.Add(new SiteSettings
        {
            SiteName = "Nihome",
            PrimaryEmail = "nihome@nihome.vn",
            NotificationEmail = "hr@nihome.vn",
            NewApplicationEmailSubjectTemplate = "[{{siteName}}] New",
            NewApplicationEmailBodyTemplate = "<p>Hello</p>",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task GetEmailTemplates_ReturnsNotFound_WhenNoSettings()
    {
        var result = await _sut.GetEmailTemplates();
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetEmailTemplates_ReturnsOk_WithTemplateData()
    {
        SeedSettings();

        var result = await _sut.GetEmailTemplates();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        // Verify it contains the expected properties via reflection
        var type = ok.Value!.GetType();
        Assert.Equal("[{{siteName}}] New", type.GetProperty("NewApplicationEmailSubjectTemplate")!.GetValue(ok.Value)?.ToString());
        Assert.Equal("hr@nihome.vn", type.GetProperty("NotificationEmail")!.GetValue(ok.Value)?.ToString());
    }

    [Fact]
    public async Task UpdateEmailTemplates_ReturnsOk_WithUpdatedData()
    {
        SeedSettings();

        var result = await _sut.UpdateEmailTemplates(new UpdateEmailTemplatesRequest
        {
            NewApplicationEmailSubjectTemplate = "Updated subject",
            NewApplicationEmailBodyTemplate = "<b>Updated</b>",
            NotificationEmail = "new@nihome.vn"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var type = ok.Value!.GetType();
        Assert.Equal("Updated subject", type.GetProperty("NewApplicationEmailSubjectTemplate")!.GetValue(ok.Value)?.ToString());
        Assert.Equal("new@nihome.vn", type.GetProperty("NotificationEmail")!.GetValue(ok.Value)?.ToString());
    }

    [Fact]
    public async Task UpdateEmailTemplates_ReturnsBadRequest_WhenNoSettings()
    {
        var result = await _sut.UpdateEmailTemplates(new UpdateEmailTemplatesRequest
        {
            NewApplicationEmailSubjectTemplate = "S",
            NewApplicationEmailBodyTemplate = "B",
            NotificationEmail = "e"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
