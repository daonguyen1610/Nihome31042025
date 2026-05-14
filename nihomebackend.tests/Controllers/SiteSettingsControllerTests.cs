using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
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
            EnableOtpForRegistration = true,
            EnableOtpForForgotPassword = false,
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
    public async Task GetOtpSettings_ReturnsDefaults_WhenNoSettings()
    {
        var result = await _sut.GetOtpSettings();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var value = Assert.IsType<OtpSettingsResponse>(ok.Value);
        Assert.True(value.EnableOtpForRegistration);
        Assert.True(value.EnableOtpForForgotPassword);
    }

    [Fact]
    public async Task GetOtpSettings_ReturnsOk_WithOtpFlags()
    {
        SeedSettings();

        var result = await _sut.GetOtpSettings();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var value = Assert.IsType<OtpSettingsResponse>(ok.Value);
        Assert.True(value.EnableOtpForRegistration);
        Assert.False(value.EnableOtpForForgotPassword);
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
    public async Task UpdateOtpSettings_ReturnsOk_WithUpdatedFlags()
    {
        SeedSettings();

        var result = await _sut.UpdateOtpSettings(new UpdateOtpSettingsRequest
        {
            EnableOtpForRegistration = false,
            EnableOtpForForgotPassword = true
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var value = Assert.IsType<OtpSettingsResponse>(ok.Value);
        Assert.False(value.EnableOtpForRegistration);
        Assert.True(value.EnableOtpForForgotPassword);
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

    [Fact]
    public async Task UpdateOtpSettings_ReturnsBadRequest_WhenNoSettings()
    {
        var result = await _sut.UpdateOtpSettings(new UpdateOtpSettingsRequest
        {
            EnableOtpForRegistration = true,
            EnableOtpForForgotPassword = true
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
