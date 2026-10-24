using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class WorkPackageApprovalEmailsTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Submit_PersistsPreviewAndHistoryOnce_AndMakesDeliveryVisible(bool finance)
    {
        var seed = await SeedAsync(finance);
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var preview = await Preview(seed);
        var submit = await Post(seed.Path + "/submit", new { rowVersion = seed.Version, previewToken = preview.GetProperty("previewToken").GetString() });
        submit.StatusCode.Should().Be(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        var record = await ReadJsonAsync(submit);
        record.GetProperty("status").GetString().Should().Be("Submitted");
        var repeat = await Post(seed.Path + "/submit", new { rowVersion = seed.Version, previewToken = preview.GetProperty("previewToken").GetString() });
        repeat.StatusCode.Should().Be(HttpStatusCode.OK);
        await WithDbAsync(async db =>
        {
            var queued = await db.WorkPackageApprovalEmails.Where(item => item.WorkPackageId == seed.PackageId).ToListAsync();
            queued.Should().ContainSingle();
            queued[0].Subject.Should().Be(preview.GetProperty("subject").GetString());
            queued[0].Body.Should().Be(preview.GetProperty("body").GetString());
            queued[0].ToEmail.Should().Be(preview.GetProperty("toEmail").GetString());
            (await db.DepartmentWorkPackageHistory.CountAsync(item => item.WorkPackageId == seed.PackageId && item.Action == "Submitted")).Should().Be(1);
        });
        var detail = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{seed.PackageId}"));
        detail.GetProperty("approvalEmails").GetArrayLength().Should().Be(1);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("forged")]
    [InlineData("changed-template")]
    [InlineData("stale-record")]
    public async Task Submit_RejectsUnconfirmedOrStaleContent_WithoutChangingState(string kind)
    {
        var seed = await SeedAsync(false);
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var preview = await Preview(seed);
        if (kind == "changed-template") await WithDbAsync(async db =>
        {
            var settings = await db.SiteSettings.FirstAsync();
            settings.WorkPackageApprovalEmailSubjectTemplate = "Changed {{workPackageCode}}";
            await db.SaveChangesAsync();
        });
        if (kind == "stale-record") await WithDbAsync(async db =>
        {
            var offer = await db.ProcurementInputPriceOffers.SingleAsync(item => item.WorkPackageId == seed.PackageId);
            offer.SupplierReference = "BG-UPDATED";
            await db.SaveChangesAsync();
        });
        var response = await Post(seed.Path + "/submit", new
        {
            rowVersion = seed.Version,
            previewToken = kind == "missing" ? null : kind == "forged" ? "invalid" : preview.GetProperty("previewToken").GetString(),
        });
        response.StatusCode.Should().Be(kind == "stale-record" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest);
        await WithDbAsync(async db =>
        {
            (await db.ProcurementInputPriceOffers.SingleAsync(item => item.WorkPackageId == seed.PackageId)).Status.Should().Be(InputPriceOfferStatus.Draft);
            (await db.WorkPackageApprovalEmails.CountAsync(item => item.WorkPackageId == seed.PackageId)).Should().Be(0);
            (await db.DepartmentWorkPackageHistory.CountAsync(item => item.WorkPackageId == seed.PackageId && item.Action == "Submitted")).Should().Be(0);
        });
    }

    [Theory]
    [InlineData("self")]
    [InlineData("inactive")]
    [InlineData("email")]
    public async Task InvalidApprover_BlocksPreviewAndSubmit(string kind)
    {
        var seed = await SeedAsync(false);
        await WithDbAsync(async db =>
        {
            var package = await db.DepartmentWorkPackages.Include(item => item.LeadUser).SingleAsync(item => item.Id == seed.PackageId);
            if (kind == "self") package.LeadUserId = package.CreatedByUserId;
            if (kind == "inactive") package.LeadUser.IsActive = false;
            if (kind == "email") package.LeadUser.Email = "invalid";
            await db.SaveChangesAsync();
        });
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        (await Client.PostAsync(seed.Path + "/approval-email-preview", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Post(seed.Path + "/submit", new { rowVersion = seed.Version })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ApprovalPermissions_KeepTemplateManagementSeparateFromSubmission()
    {
        var seed = await SeedAsync(false);
        (await Client.PostAsync(seed.Path + "/approval-email-preview", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsCustomerAsync);
        (await Client.PostAsync(seed.Path + "/approval-email-preview", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.GetAsync("/api/site-settings/work-package-approval-template")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "WAREHOUSE"));
        (await Client.PostAsync(seed.Path + "/approval-email-preview", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.PutAsJsonAsync("/api/site-settings/work-package-approval-template", new { subject = "Change", body = "<p>Change</p>" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TemplateUpdate_ValidatesHtmlTokensAndPersistsCustomContent()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var path = "/api/site-settings/work-package-approval-template";
        (await Client.PutAsJsonAsync(path, new { subject = "Invalid\nHeader", body = "<script>bad</script>" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = new { subject = "Review {{workPackageCode}}", body = "<p>{{recipientName}}: {{workPackageCode}} / {{recordReference}}</p>" };
        (await Client.PutAsJsonAsync(path, content)).StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await ReadJsonAsync(await Client.GetAsync(path));
        saved.GetProperty("subject").GetString().Should().Be(content.subject);
        saved.GetProperty("body").GetString().Should().Be(content.body);
    }

    [Fact]
    public async Task DeletionImpact_DisclosesEmails_AndBlocksInFlightDelivery()
    {
        var seed = await SeedAsync(false);
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var preview = await Preview(seed);
        (await Post(seed.Path + "/submit", new { rowVersion = seed.Version, previewToken = preview.GetProperty("previewToken").GetString() })).EnsureSuccessStatusCode();
        await WithDbAsync(async db =>
        {
            var email = await db.WorkPackageApprovalEmails.SingleAsync(item => item.WorkPackageId == seed.PackageId);
            email.Status = "Sending";
            await db.SaveChangesAsync();
        });
        var impact = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{seed.PackageId}/deletion-impact"));
        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        impact.GetProperty("items").EnumerateArray().Should().Contain(item => item.GetProperty("key").GetString() == "workPackages.sendingEmails" && item.GetProperty("action").GetString() == "Block");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomRecipients_AreConfirmedAndPersisted_ForBothDossiers(bool finance)
    {
        var seed = await SeedAsync(finance);
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var initial = await Preview(seed);
        initial.GetProperty("ccEmails").GetArrayLength().Should().Be(0);
        var custom = await Client.PostAsJsonAsync(seed.Path + "/approval-email-preview", new
        {
            toEmail = " delegate@example.test ",
            ccEmails = new[] { "cm@example.test", "CM@example.test", "delegate@example.test", "qs@example.test" },
        });
        custom.EnsureSuccessStatusCode();
        var preview = await ReadJsonAsync(custom);
        preview.GetProperty("toEmail").GetString().Should().Be("delegate@example.test");
        preview.GetProperty("ccEmails").GetArrayLength().Should().Be(2);
        var response = await Post(seed.Path + "/submit", new
        {
            rowVersion = seed.Version,
            previewToken = preview.GetProperty("previewToken").GetString(),
            // Submission cannot override recipients bound into the confirmed token.
            toEmail = "unconfirmed@example.test",
            ccEmails = new[] { "unconfirmed@example.test" },
        });
        response.EnsureSuccessStatusCode();
        var detail = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{seed.PackageId}"));
        detail.GetProperty("approvalEmails")[0].GetProperty("toEmail").GetString().Should().Be("delegate@example.test");
        detail.GetProperty("approvalEmails")[0].GetProperty("ccEmails").GetArrayLength().Should().Be(2);
        await WithDbAsync(async db =>
        {
            var queued = await db.WorkPackageApprovalEmails.SingleAsync(item => item.WorkPackageId == seed.PackageId);
            queued.LeadEmail.Should().Be(initial.GetProperty("toEmail").GetString());
            queued.CcEmailsJson.Should().NotContain("unconfirmed");
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("a@example.test,b@example.test")]
    public async Task Preview_RejectsInvalidRecipients_WithoutEnqueuing(string address)
    {
        var seed = await SeedAsync(false);
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        (await Client.PostAsJsonAsync(seed.Path + "/approval-email-preview", new { toEmail = address })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync(seed.Path + "/approval-email-preview", new { ccEmails = new[] { address } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync(seed.Path + "/approval-email-preview", new { ccEmails = Enumerable.Repeat("cc@example.test", 21) })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WithDbAsync(async db => (await db.WorkPackageApprovalEmails.CountAsync(item => item.WorkPackageId == seed.PackageId)).Should().Be(0));
    }

    private async Task<JsonElement> Preview(Seed seed)
    {
        var response = await Client.PostAsync(seed.Path + "/approval-email-preview", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await ReadJsonAsync(response);
    }

    private async Task<HttpResponseMessage> Post(string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await Client.SendAsync(request);
    }

    private sealed record Seed(int PackageId, string Path, string Version);
    private Task<Seed> SeedAsync(bool finance) => WithDbAsync<Seed>(async db =>
    {
        var admin = await db.Users.SingleAsync(item => item.PhoneNumber == TestDataSeeder.SuperAdminPhone);
        var department = await db.RoleGroups.SingleAsync(item => item.Code == (finance ? "FINANCE" : "PROCUREMENT"));
        // A dedicated recipient keeps negative tests from changing shared role accounts.
        var leadRole = await db.Roles.SingleAsync(item => item.Code == (finance ? "ACCOUNTANT" : "PROCUREMENT"));
        var lead = new ApplicationUser { Email = $"review-{Guid.NewGuid():N}@example.test", FullName = "Trưởng phòng An Phú", PhoneNumber = "0901234567", RoleEntityId = leadRole.Id, Role = UserRole.USER };
        var customer = new Customer { Name = "Nhà máy An Phú", SourceCode = "referral" };
        var project = new OperationalProject { Code = $"APP-{Guid.NewGuid():N}"[..25], Name = "Cải tạo MEP An Phú", Customer = customer, ProjectManagerUserId = admin.Id };
        var package = new DepartmentWorkPackage
        {
            Code = $"WP-{Guid.NewGuid():N}"[..25],
            Name = "Rà soát báo giá An Phú",
            Kind = WorkPackageKind.Project,
            Purpose = finance ? WorkPackagePurpose.FinanceCommercialReview : WorkPackagePurpose.ProcurementInputPricing,
            DepartmentRoleGroupId = department.Id,
            LeadUser = lead,
            CreatedByUserId = admin.Id,
            UpdatedByUserId = admin.Id,
            OperationalProject = project
        };
        db.Add(package);
        if (!await db.SiteSettings.AnyAsync()) db.SiteSettings.Add(new SiteSettings());
        await db.SaveChangesAsync();
        if (finance)
        {
            var review = new DepartmentFinanceReview
            {
                WorkPackageId = package.Id,
                CreatedByUserId = admin.Id,
                UpdatedByUserId = admin.Id,
                ContractConditionsReviewed = true,
                GuaranteesReviewed = true,
                PaymentTermsReviewed = true
            };
            db.Add(review); await db.SaveChangesAsync();
            return new(package.Id, $"/api/work-packages/{package.Id}/finance-review", Convert.ToBase64String(review.RowVersion));
        }
        var vendor = new Vendor { VendorCode = $"MEP-{Guid.NewGuid():N}"[..25], CompanyName = "Công ty Cơ điện An Phú", IsActive = true };
        var offer = new ProcurementInputPriceOffer
        {
            WorkPackageId = package.Id,
            Vendor = vendor,
            CreatedByUserId = admin.Id,
            UpdatedByUserId = admin.Id,
            SupplierReference = "BG-MEP-AN-PHU-01",
            ValidUntil = DateTime.UtcNow.AddDays(30),
            Lines = [new() { ItemCode = "MEP-01", Description = "Cáp điện", Unit = "m", Quantity = 100, UnitPrice = 50000, Amount = 5000000 }]
        };
        db.Add(offer); await db.SaveChangesAsync();
        return new(package.Id, $"/api/work-packages/{package.Id}/input-price-offers/{offer.Id}", Convert.ToBase64String(offer.RowVersion));
    });
}
