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

    [Fact]
    public void Seed_AddsProcessDocumentsFromSeedJson()
    {
        DbSeeder.Seed(_db);

        Assert.Equal(29, _db.ProcessDocuments.Count());
        Assert.Contains(_db.ProcessDocuments, p =>
            p.GroupKey == "dt" &&
            p.Title == "Quy trình đấu thầu" &&
            p.SortOrder == 0);
    }

    [Fact]
    public void Seed_AddsSampleQuotesLinkedToSampleOpportunities()
    {
        DbSeeder.Seed(_db);

        var quotes = _db.Quotes.ToList();
        Assert.Equal(9, quotes.Count);
        Assert.All(quotes, q =>
        {
            Assert.StartsWith("QT-", q.Code);
            Assert.True(q.GrandTotal > 0m, $"Quote {q.Code} should have positive grand total");
            Assert.NotNull(q.Note);
            Assert.StartsWith("[SAMPLE_QUOTE]", q.Note);
        });
        // Every declared QuoteStatus (bar Draft, which we intentionally have
        // two of) must be present at least once for the filter/badge demo.
        var statuses = quotes.Select(q => q.Status).ToHashSet();
        Assert.Contains(QuoteStatus.Draft, statuses);
        Assert.Contains(QuoteStatus.PendingApproval, statuses);
        Assert.Contains(QuoteStatus.Approved, statuses);
        Assert.Contains(QuoteStatus.SentToCustomer, statuses);
        Assert.Contains(QuoteStatus.CustomerApproved, statuses);
        Assert.Contains(QuoteStatus.Rejected, statuses);
        Assert.Contains(QuoteStatus.Expired, statuses);
        Assert.Contains(QuoteStatus.Cancelled, statuses);
        // A version snapshot exists so the Versions tab has V1 + V2.
        Assert.NotEmpty(_db.QuoteVersionSnapshots.ToList());
    }

    [Fact]
    public void Seed_IsIdempotentForSampleQuotes()
    {
        DbSeeder.Seed(_db);
        var firstRun = _db.Quotes.Count();
        var firstSnaps = _db.QuoteVersionSnapshots.Count();

        DbSeeder.Seed(_db);
        var secondRun = _db.Quotes.Count();
        var secondSnaps = _db.QuoteVersionSnapshots.Count();

        Assert.Equal(firstRun, secondRun);
        Assert.Equal(firstSnaps, secondSnaps);
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
