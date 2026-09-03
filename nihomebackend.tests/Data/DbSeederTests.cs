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
    public void Seed_UsesRealisticNiconSettings_AndCreatesEveryBusinessRoleUser()
    {
        DbSeeder.Seed(_db);

        var settings = _db.SiteSettings.Single();
        Assert.Equal("NICON", settings.SiteName);
        Assert.Contains("thi công trọn gói", settings.SiteDescription);

        var businessRoles = _db.Roles.Where(role => !role.IsSystem).ToList();
        Assert.NotEmpty(businessRoles);
        Assert.All(businessRoles, role => Assert.Contains(_db.Users, user =>
            user.RoleEntityId == role.Id
            && user.Email != null
            && user.Email.EndsWith("@nihome.vn")));
    }

    [Fact]
    public void Seed_BackfillsOnlyExactLegacySiteAndDemoUserValues()
    {
        var settings = CreateSettings(EmailTemplateFormatter.DefaultOtpBody);
        settings.SiteName = "NICON Custom Portal";
        settings.SiteDescription = "Căn hộ dịch vụ cao cấp - Không gian sống tiện nghi";
        _db.SiteSettings.Add(settings);
        _db.Users.Add(new ApplicationUser
        {
            PhoneNumber = "0911000003",
            FullName = "Sale Tester",
            Email = "sale.test@nihome.vn",
            Role = UserRole.USER,
            IsActive = true,
        });
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        var reloadedSettings = _db.SiteSettings.Single();
        Assert.Equal("NICON Custom Portal", reloadedSettings.SiteName);
        Assert.Contains("thi công trọn gói", reloadedSettings.SiteDescription);
        var sale = _db.Users.Single(user => user.PhoneNumber == "0911000003");
        Assert.Equal("Nguyễn Minh Anh", sale.FullName);
        Assert.Equal("minh.anh.sale@nihome.vn", sale.Email);
        Assert.Equal(_db.Roles.Single(role => role.Code == "SALE").Id, sale.RoleEntityId);
    }

    [Fact]
    public void Seed_PreservesCustomDemoUserIdentity()
    {
        _db.Users.Add(new ApplicationUser
        {
            PhoneNumber = "0911000003",
            FullName = "Tên quản trị tùy chỉnh",
            Email = "custom.owner@example.com",
            Role = UserRole.USER,
            IsActive = true,
        });
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        var sale = _db.Users.Single(user => user.PhoneNumber == "0911000003");
        Assert.Equal("Tên quản trị tùy chỉnh", sale.FullName);
        Assert.Equal("custom.owner@example.com", sale.Email);
    }

    [Fact]
    public void Seed_CreatesMissingCanonicalAdminsByPhone_DespiteExistingRolesAndEmailConflict()
    {
        _db.Users.AddRange(
            new ApplicationUser
            {
                PhoneNumber = "0900000100",
                FullName = "Existing super admin",
                Email = "superadmin@nihome.vn",
                PasswordHash = "x",
                Role = UserRole.SUPER_ADMIN,
            },
            new ApplicationUser
            {
                PhoneNumber = "0900000101",
                FullName = "Existing admin",
                Email = "existing.admin@nihome.vn",
                PasswordHash = "x",
                Role = UserRole.ADMIN,
            });
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        Assert.Contains(_db.Users, user => user.PhoneNumber == "0335240370" && user.Role == UserRole.SUPER_ADMIN);
        Assert.Contains(_db.Users, user => user.PhoneNumber == "0911111111" && user.Role == UserRole.ADMIN);
        Assert.Contains(_db.Users, user => user.PhoneNumber == "0922222222" && user.Role == UserRole.ADMIN);
        var canonical = _db.Users.Single(user => user.PhoneNumber == "0335240370");
        Assert.False(string.Equals("superadmin@nihome.vn", canonical.Email, StringComparison.OrdinalIgnoreCase));

        var canonicalIds = _db.Users
            .Where(user => user.PhoneNumber == "0335240370" || user.PhoneNumber == "0911111111" || user.PhoneNumber == "0922222222")
            .ToDictionary(user => user.PhoneNumber, user => user.Id);
        DbSeeder.Seed(_db);

        Assert.Equal(canonicalIds, _db.Users
            .Where(user => canonicalIds.Keys.Contains(user.PhoneNumber))
            .ToDictionary(user => user.PhoneNumber, user => user.Id));
    }

    [Fact]
    public void Seed_BusinessEmailConflictsUseStableFallbacksWithoutChangingExistingUsers()
    {
        var preferredSaleOwner = new ApplicationUser
        {
            PhoneNumber = "0900000200",
            FullName = "Existing sale email owner",
            Email = "MINH.ANH.SALE@NIHOME.VN",
            PasswordHash = "x",
            Role = UserRole.USER,
        };
        var preferredManagerOwner = new ApplicationUser
        {
            PhoneNumber = "0900000201",
            FullName = "Existing manager email owner",
            Email = "quoc.huy.sales@nihome.vn",
            PasswordHash = "x",
            Role = UserRole.USER,
        };
        var legacySale = new ApplicationUser
        {
            PhoneNumber = "0911000003",
            FullName = "Sale Tester",
            Email = "sale.test@nihome.vn",
            PasswordHash = "x",
            Role = UserRole.USER,
        };
        _db.Users.AddRange(preferredSaleOwner, preferredManagerOwner, legacySale);
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        Assert.Equal("seed.0911000003@nihome.vn", legacySale.Email);
        var salesManager = _db.Users.Single(user => user.PhoneNumber == "0911000010");
        Assert.Equal("seed.0911000010@nihome.vn", salesManager.Email);
        Assert.Equal("MINH.ANH.SALE@NIHOME.VN", preferredSaleOwner.Email);
        Assert.Equal("quoc.huy.sales@nihome.vn", preferredManagerOwner.Email);
        var identities = _db.Users.ToDictionary(user => user.PhoneNumber,
            user => (user.Id, user.Email));
        Assert.Equal(_db.Users.Count(), _db.Users.Select(user => user.Email).AsEnumerable()
            .Distinct(StringComparer.OrdinalIgnoreCase).Count());

        DbSeeder.Seed(_db);

        Assert.Equal(identities, _db.Users.ToDictionary(user => user.PhoneNumber,
            user => (user.Id, user.Email)));
        Assert.Equal(_db.Users.Count(), _db.Users.Select(user => user.Email).AsEnumerable()
            .Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Seed_MigratesOnlyLowestIdSiteSettingsRow()
    {
        var canonical = CreateSettings(EmailTemplateFormatter.LegacyDefaultOtpBody);
        canonical.SiteDescription = "Căn hộ dịch vụ cao cấp - Không gian sống tiện nghi";
        var custom = CreateSettings("<p>Custom {{otpCode}}</p>");
        custom.SiteName = "Secondary custom settings";
        _db.SiteSettings.AddRange(canonical, custom);
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        Assert.Equal("NICON", _db.SiteSettings.Single(settings => settings.Id == canonical.Id).SiteName);
        Assert.Equal(EmailTemplateFormatter.DefaultOtpBody,
            _db.SiteSettings.Single(settings => settings.Id == canonical.Id).OtpEmailBodyTemplate);
        Assert.Equal("Secondary custom settings",
            _db.SiteSettings.Single(settings => settings.Id == custom.Id).SiteName);
        Assert.Equal("<p>Custom {{otpCode}}</p>",
            _db.SiteSettings.Single(settings => settings.Id == custom.Id).OtpEmailBodyTemplate);
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
        Assert.All(quotes.Where(q => q.Method == QuoteMethod.UnitCost), q =>
        {
            Assert.Equal(QuoteRateSource.Override, q.RateSource);
            Assert.False(string.IsNullOrWhiteSpace(q.RateOverrideReason));
            Assert.NotNull(q.RateOverrideByUserId);
            Assert.NotNull(q.RateOverrideAt);
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
        var snapshots = _db.QuoteVersionSnapshots.ToList();
        Assert.NotEmpty(snapshots);
        var unitCostSnapshots = snapshots.Where(s => s.Method == QuoteMethod.UnitCost).ToList();
        Assert.NotEmpty(unitCostSnapshots);
        Assert.All(unitCostSnapshots, snapshot =>
        {
            Assert.Equal(QuoteRateSource.Override, snapshot.RateSource);
            Assert.False(string.IsNullOrWhiteSpace(snapshot.RateOverrideReason));
            Assert.NotNull(snapshot.RateOverrideByUserId);
            Assert.NotNull(snapshot.RateOverrideAt);
        });
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
