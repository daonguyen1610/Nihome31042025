using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

public class DbSeederTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Seed_ReplacesLegacyDefaultOtpBodyTemplate()
    {
        _db.SiteSettings.Add(CreateSettings(EmailTemplateFormatter.LegacyDefaultOtpBody));
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        var settings = _db.SiteSettings.Single();
        Assert.Equal(EmailTemplateFormatter.DefaultOtpBody, settings.OtpEmailBodyTemplate);
    }

    [Fact]
    public void Seed_DoesNotReplaceCustomOtpBodyTemplate()
    {
        const string customBody = "<p style='color:#7c3aed'>Custom OTP {{otpCode}}</p>";
        _db.SiteSettings.Add(CreateSettings(customBody));
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        var settings = _db.SiteSettings.Single();
        Assert.Equal(customBody, settings.OtpEmailBodyTemplate);
    }

    private static SiteSettings CreateSettings(string otpEmailBodyTemplate)
    {
        var now = DateTime.UtcNow;
        return new SiteSettings
        {
            SiteName = "Nihome",
            PrimaryEmail = "nihome@nihome.vn",
            EnableOtpForRegistration = true,
            EnableOtpForForgotPassword = true,
            OtpEmailSubjectTemplate = EmailTemplateFormatter.DefaultOtpSubject,
            OtpEmailBodyTemplate = otpEmailBodyTemplate,
            NewApplicationEmailSubjectTemplate = EmailTemplateFormatter.DefaultNewApplicationSubject,
            NewApplicationEmailBodyTemplate = EmailTemplateFormatter.DefaultNewApplicationBody,
            NotificationEmail = "nihome@nihome.vn",
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
