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
        Assert.Equal(ContractReminderTemplates.RenewalBody, settings.ContractRenewalEmailBodyTemplate);
        Assert.Equal(ContractReminderTemplates.EndBody, settings.ContractEndEmailBodyTemplate);
        Assert.Equal(WorkPackageApprovalEmailTemplates.DefaultBody, settings.WorkPackageApprovalEmailBodyTemplate);

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
    public void Seed_WithoutDevelopmentMode_DoesNotCreateBusinessModuleSamples()
    {
        DbSeeder.Seed(_db);

        Assert.Empty(_db.Leads);
        Assert.Empty(_db.Customers);
        Assert.Empty(_db.Opportunities);
        Assert.Empty(_db.Quotes);
        Assert.Empty(_db.Contracts);
        Assert.Empty(_db.Vendors);
        Assert.Empty(_db.Tenders);
        Assert.Empty(_db.Surveys);
        Assert.Empty(_db.DesignProjects);
        Assert.Empty(_db.PermitChecklistItems);
        Assert.Empty(_db.KpiDefinitions);
        Assert.Empty(_db.Rfqs);
        Assert.Empty(_db.ConstructionTasks);
        Assert.Empty(_db.SiteDiaries);
        Assert.Empty(_db.PunchItems);
        Assert.Empty(_db.AcceptanceRecords);
        Assert.Empty(_db.AsBuiltDocuments);
        Assert.Empty(_db.HandoverRecords);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("legacy")]
    public void Seed_UpgradesOnlyMissingOrLegacyBusinessEmailTemplates(string? body)
    {
        var settings = CreateSettings(EmailTemplateFormatter.DefaultOtpBody);
        settings.ContractRenewalEmailBodyTemplate = body == "legacy" ? ContractReminderTemplates.LegacyRenewalBody : body;
        settings.ContractEndEmailBodyTemplate = body == "legacy" ? ContractReminderTemplates.LegacyEndBody : body;
        settings.WorkPackageApprovalEmailBodyTemplate = body == "legacy" ? WorkPackageApprovalEmailTemplates.LegacyDefaultBody : body;
        _db.SiteSettings.Add(settings);
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        Assert.Equal(ContractReminderTemplates.RenewalSubject, settings.ContractRenewalEmailSubjectTemplate);
        Assert.Equal(ContractReminderTemplates.RenewalBody, settings.ContractRenewalEmailBodyTemplate);
        Assert.Equal(ContractReminderTemplates.EndSubject, settings.ContractEndEmailSubjectTemplate);
        Assert.Equal(ContractReminderTemplates.EndBody, settings.ContractEndEmailBodyTemplate);
        Assert.Equal(WorkPackageApprovalEmailTemplates.DefaultSubject, settings.WorkPackageApprovalEmailSubjectTemplate);
        Assert.Equal(WorkPackageApprovalEmailTemplates.DefaultBody, settings.WorkPackageApprovalEmailBodyTemplate);
        var updatedAt = settings.UpdatedAt;
        DbSeeder.Seed(_db);
        Assert.Equal(updatedAt, settings.UpdatedAt);
    }

    [Fact]
    public void Seed_PreservesCustomizedBusinessEmailTemplatesAndConfirmedContent()
    {
        var settings = CreateSettings(EmailTemplateFormatter.DefaultOtpBody);
        settings.ContractRenewalEmailSubjectTemplate = "Gia hạn riêng {{contractNumber}}";
        settings.ContractRenewalEmailBodyTemplate = ContractReminderTemplates.LegacyRenewalBody.Replace("Trân trọng,", "Cảm ơn Quý khách,");
        settings.ContractEndEmailSubjectTemplate = "Thời hạn riêng {{contractNumber}}";
        settings.ContractEndEmailBodyTemplate = ContractReminderTemplates.LegacyEndBody.Replace("Trân trọng,", "Cảm ơn Quý khách,");
        settings.WorkPackageApprovalEmailSubjectTemplate = "Duyệt riêng {{workPackageCode}}";
        settings.WorkPackageApprovalEmailBodyTemplate = WorkPackageApprovalEmailTemplates.LegacyDefaultBody.Replace("#ed1c24", "#7c3aed");
        var before = (settings.ContractRenewalEmailSubjectTemplate, settings.ContractRenewalEmailBodyTemplate,
            settings.ContractEndEmailSubjectTemplate, settings.ContractEndEmailBodyTemplate,
            settings.WorkPackageApprovalEmailSubjectTemplate, settings.WorkPackageApprovalEmailBodyTemplate);
        _db.SiteSettings.Add(settings);
        var author = new ApplicationUser { FullName = "Mai Anh", Email = "mai.anh@example.test", PhoneNumber = "0901234567" };
        var lead = new ApplicationUser { FullName = "Trần An", Email = "tran.an@example.test", PhoneNumber = "0901234568" };
        var contract = new Contract
        {
            ContractNumber = "HD-ANPHU-2026-01",
            Customer = new Customer { Name = "Nhà máy An Phú", SourceCode = "marketing" },
            Owner = author,
            Direction = ContractDirection.Upstream,
            Type = ContractType.DesignAndBuild,
            Status = ContractStatus.Signed,
            SignedDate = new DateTime(2026, 10, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var package = new DepartmentWorkPackage { Code = "WP-ANPHU-MEP-01", Name = "Mua sắm thiết bị MEP An Phú", LeadUser = lead };
        _db.AddRange(author, lead, contract, package);
        _db.SaveChanges();
        var snapshot = new ContractReminder
        {
            ContractId = contract.Id,
            CreatedByUserId = author.Id,
            ToEmail = "customer@example.test",
            ReminderDate = new DateTime(2026, 11, 1),
            Subject = "Lịch đã xác nhận",
            Body = ContractReminderTemplates.LegacyRenewalBody
        };
        _db.ContractReminders.Add(snapshot);
        var approvalSnapshot = new WorkPackageApprovalEmail
        {
            WorkPackageId = package.Id,
            AuthorUserId = author.Id,
            RecipientUserId = lead.Id,
            ToEmail = lead.Email!,
            RecordType = "InputPriceOffer",
            RecordId = 1,
            Subject = "Hồ sơ đã gửi duyệt",
            Body = WorkPackageApprovalEmailTemplates.LegacyDefaultBody
        };
        _db.WorkPackageApprovalEmails.Add(approvalSnapshot);
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        Assert.Equal(before, (settings.ContractRenewalEmailSubjectTemplate, settings.ContractRenewalEmailBodyTemplate,
            settings.ContractEndEmailSubjectTemplate, settings.ContractEndEmailBodyTemplate,
            settings.WorkPackageApprovalEmailSubjectTemplate, settings.WorkPackageApprovalEmailBodyTemplate));
        Assert.Equal(ContractReminderTemplates.LegacyRenewalBody, snapshot.Body);
        Assert.Equal(WorkPackageApprovalEmailTemplates.LegacyDefaultBody, approvalSnapshot.Body);
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
