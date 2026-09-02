using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Models;
using NihomeBackend.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>QuotesController</c> (NIH-84): RBAC scoping,
/// state-machine transitions, versioning, and totals arithmetic on the
/// wire. Sales/Sales Manager identity comes from the seeded test users.
/// </summary>
public class QuotesControllerTests : IntegrationTestBase
{
    public QuotesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/quotes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsWarehouse_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/quotes")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_UnitCost_ComputesTotalsAndReturns201()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var oppId = await CreateOpportunityAsync();
        var rateCatalogId = await CreateRateCatalogAsync(8_000_000m);

        var res = await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId = oppId,
            method = "UnitCost",
            areaSqm = 50m,
            unitPricePerSqm = 8_000_000m,
            materialRateCatalogId = rateCatalogId,
            pricingEffectiveDate = "2026-09-01",
            discountPercent = 5m,
            vatPercent = 10m,
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("subtotal").GetDecimal().Should().Be(400_000_000m);
        // afterDiscount 380M ; vat 10% = 38M → grand 418M
        body.GetProperty("grandTotal").GetDecimal().Should().Be(418_000_000m);
        body.GetProperty("status").GetString().Should().Be("Draft");
        body.GetProperty("version").GetInt32().Should().Be(1);
        body.GetProperty("code").GetString().Should().StartWith("QT-");
        body.GetProperty("grandTotalInWords").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Create_AsSale_CannotAssignAnotherOwner()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var opportunityId = await CreateOpportunityAsync();
        var foreignOwnerId = await WithDbAsync(db => db.Users.AsNoTracking()
            .Where(user => user.IsActive && user.PhoneNumber != TestDataSeeder.BusinessRolePhonesByCode["SALE"])
            .Select(user => user.Id)
            .FirstAsync());

        var response = await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId,
            ownerUserId = foreignOwnerId,
            method = "Boq",
            items = new[] { new { name = "Concrete", unit = "m3", quantity = 1m, unitPrice = 100m } },
            discountPercent = 0m,
            vatPercent = 10m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Quotes.AnyAsync(item => item.OpportunityId == opportunityId)))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Boq_CreateReopenUpdateAndApprove_PreservesTotalsAndItems()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var oppId = await CreateOpportunityAsync();
        var create = await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId = oppId,
            method = "Boq",
            items = new[]
            {
                new { itemCode = "BOQ-01", name = "Concrete", unit = "m3", quantity = 2.5m, unitPrice = 100m },
                new { itemCode = "BOQ-02", name = "Steel", unit = "kg", quantity = 3m, unitPrice = 50m },
            },
            discountPercent = 10m,
            vatPercent = 8m,
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(create);
        created.GetProperty("subtotal").GetDecimal().Should().Be(400m);
        created.GetProperty("grandTotal").GetDecimal().Should().Be(388.8m);
        created.GetProperty("items").GetArrayLength().Should().Be(2);

        var quoteId = created.GetProperty("id").GetInt32();
        var reopened = await Client.GetAsync($"/api/quotes/{quoteId}");
        reopened.EnsureSuccessStatusCode();
        var reopenedBody = await ReadJsonAsync(reopened);
        reopenedBody.GetProperty("items")[0].GetProperty("itemCode").GetString().Should().Be("BOQ-01");

        var update = await Client.PutAsJsonAsync($"/api/quotes/{quoteId}", new
        {
            rowVersion = reopenedBody.GetProperty("rowVersion").GetString(),
            items = new[]
            {
                new { itemCode = "BOQ-01", name = "Concrete", unit = "m3", quantity = 4m, unitPrice = 100m },
            },
            discountPercent = 0m,
            vatPercent = 10m,
        });
        update.EnsureSuccessStatusCode();
        var updated = await ReadJsonAsync(update);
        updated.GetProperty("subtotal").GetDecimal().Should().Be(400m);
        updated.GetProperty("grandTotal").GetDecimal().Should().Be(440m);
        updated.GetProperty("items").GetArrayLength().Should().Be(1);

        var submit = await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/submit", new
        {
            rowVersion = updated.GetProperty("rowVersion").GetString(),
        });
        submit.EnsureSuccessStatusCode();
        var submitted = await ReadJsonAsync(submit);
        var approve = await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/approve", new
        {
            rowVersion = submitted.GetProperty("rowVersion").GetString(),
        });
        approve.EnsureSuccessStatusCode();
        (await ReadJsonAsync(approve)).GetProperty("status").GetString().Should().Be("Approved");
    }

    [Fact]
    public async Task Boq_CreateWithCatalogRevision_ReturnsCatalogProvenance()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var opportunityId = await CreateOpportunityAsync();
        var catalog = await CreateBoqRateCatalogAsync();

        var response = await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId,
            method = "Boq",
            materialRateCatalogId = catalog.CatalogId,
            pricingEffectiveDate = "2026-09-01",
            items = new[]
            {
                new { itemCode = "BOQ-CAT-01", name = "Concrete", unit = "m3", quantity = 2.5m, unitPrice = 160m },
            },
            discountPercent = 0m,
            vatPercent = 10m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(response);
        body.GetProperty("method").GetString().Should().Be("Boq");
        body.GetProperty("materialRateCatalogId").GetInt32().Should().Be(catalog.CatalogId);
        body.GetProperty("materialRateRevisionId").GetInt32().Should().Be(catalog.RevisionId);
        body.GetProperty("materialRateCatalogCode").GetString().Should().Be(catalog.Code);
        body.GetProperty("materialRateCatalogName").GetString().Should().Be(catalog.Name);
        body.GetProperty("materialRateRevisionVersion").GetInt32().Should().Be(1);
        body.GetProperty("pricingEffectiveDate").GetString().Should().Be("2026-09-01");
        body.GetProperty("rateSource").GetString().Should().Be("Catalog");
        body.GetProperty("rateOverrideReason").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        body.GetProperty("subtotal").GetDecimal().Should().Be(400m);
        body.GetProperty("grandTotal").GetDecimal().Should().Be(440m);
        body.GetProperty("items")[0].GetProperty("itemCode").GetString().Should().Be("BOQ-CAT-01");
    }

    [Fact]
    public async Task Boq_InvalidOrOverflowingRows_AreRejected()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var oppId = await CreateOpportunityAsync();

        (await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId = oppId,
            method = "Boq",
            items = Array.Empty<object>(),
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId = oppId,
            method = "Boq",
            items = new[]
            {
                new { name = "Overflow", unit = "item", quantity = 99_999_999_999_999m, unitPrice = 99_999_999_999_999m },
            },
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Boq_CreateRetryWithSameKey_DoesNotDuplicateQuote()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var oppId = await CreateOpportunityAsync();
        var payload = new
        {
            opportunityId = oppId,
            method = "Boq",
            items = new[] { new { name = "Wall", unit = "m2", quantity = 10m, unitPrice = 20m } },
            discountPercent = 0m,
            vatPercent = 8m,
        };
        var key = $"nih-450-{Guid.NewGuid():N}";
        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/quotes")
        {
            Content = JsonContent.Create(payload),
        };
        firstRequest.Headers.Add("Idempotency-Key", key);
        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/quotes")
        {
            Content = JsonContent.Create(payload),
        };
        replayRequest.Headers.Add("Idempotency-Key", key);

        var first = await Client.SendAsync(firstRequest);
        var replay = await Client.SendAsync(replayRequest);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstId = (await ReadJsonAsync(first)).GetProperty("id").GetInt32();
        var replayId = (await ReadJsonAsync(replay)).GetProperty("id").GetInt32();
        replayId.Should().Be(firstId);
        (await WithDbAsync(db => db.Quotes.CountAsync(quote => quote.OpportunityId == oppId)))
            .Should().Be(1);
    }

    [Fact]
    public async Task Sale_CannotCreateBoqForAnotherOwnersOpportunity()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var oppId = await CreateOpportunityAsync();
        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var response = await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId = oppId,
            method = "Boq",
            items = new[] { new { name = "Wall", unit = "m2", quantity = 10m, unitPrice = 20m } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_UnknownOpportunity_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId = 999_999,
            method = "UnitCost",
            areaSqm = 10m,
            unitPricePerSqm = 1_000_000m,
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ForWonOpportunity_LinksWinningQuoteAndRejectsSecondQuote()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var oppId = await CreateOpportunityAsync();
        var rateCatalogId = await CreateRateCatalogAsync(8_000_000m);
        await WithDbAsync(async db =>
        {
            var opportunity = await db.Opportunities.SingleAsync(item => item.Id == oppId);
            opportunity.Stage = OpportunityStage.Won;
            opportunity.ClosedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });

        var payload = new
        {
            opportunityId = oppId,
            method = "UnitCost",
            areaSqm = 50m,
            unitPricePerSqm = 8_000_000m,
            materialRateCatalogId = rateCatalogId,
            pricingEffectiveDate = "2026-09-01",
            discountPercent = 0m,
            vatPercent = 8m,
        };
        var created = await Client.PostAsJsonAsync("/api/quotes", payload);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var quoteId = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var opportunity = await ReadJsonAsync(await Client.GetAsync($"/api/opportunities/{oppId}"));
        opportunity.GetProperty("wonQuoteId").GetInt32().Should().Be(quoteId);

        (await Client.PostAsJsonAsync("/api/quotes", payload))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Submit_Approve_Send_HappyPath()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();

        (await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/submit", new { }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/approve", new { }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var sendRes = await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/send", new { });
        sendRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(sendRes);
        body.GetProperty("status").GetString().Should().Be("SentToCustomer");
    }

    [Fact]
    public async Task Approve_WithoutApprovePermission_IsForbidden()
    {
        // SALE has manage but not approve — SALES_MANAGER approves.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var quoteId = await CreateQuoteAsync();
        (await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/submit", new { }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var res = await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/approve", new { });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_AfterApproval_SpawnsVersionTwo()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();
        (await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/submit", new { }))
            .EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/approve", new { }))
            .EnsureSuccessStatusCode();

        var updateRes = await Client.PutAsJsonAsync($"/api/quotes/{quoteId}", new
        {
            areaSqm = 200m,
            unitPricePerSqm = 10_000_000m,
            rateOverrideReason = "Điều chỉnh đơn giá cho phiên bản báo giá mới.",
            discountPercent = 0m,
            vatPercent = 8m,
        });
        updateRes.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(updateRes);
        body.GetProperty("version").GetInt32().Should().Be(2);
        body.GetProperty("status").GetString().Should().Be("Draft");

        var versionsRes = await Client.GetAsync($"/api/quotes/{quoteId}/versions");
        versionsRes.EnsureSuccessStatusCode();
        var versionsBody = await ReadJsonAsync(versionsRes);
        versionsBody.GetProperty("versions").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Versions_EnforcesAuthenticationPermissionAndOwnerScope()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var quoteId = await CreateQuoteAsync();
        (await Client.GetAsync($"/api/quotes/{quoteId}/versions"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        Client.DefaultRequestHeaders.Authorization = null;
        (await Client.GetAsync($"/api/quotes/{quoteId}/versions"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync($"/api/quotes/{quoteId}/versions"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.GetAsync($"/api/quotes/{quoteId}/versions"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var managerQuoteId = await CreateQuoteAsync();
        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync($"/api/quotes/{managerQuoteId}/versions"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Sale_CannotSeeAnotherSalesQuote()
    {
        // SALES_MANAGER creates the quote → owned by that user.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync($"/api/quotes/{quoteId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_SubmittedQuote_RemovesAggregateAndPreservesOpportunity()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();
        var quote = await ReadJsonAsync(await Client.GetAsync($"/api/quotes/{quoteId}"));
        var opportunityId = quote.GetProperty("opportunityId").GetInt32();
        (await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/submit", new { })).EnsureSuccessStatusCode();

        (await Client.DeleteAsync($"/api/quotes/{quoteId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Client.GetAsync($"/api/quotes/{quoteId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/opportunities/{opportunityId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_MissingQuote_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.DeleteAsync("/api/quotes/9999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithoutManagePermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.DeleteAsync("/api/quotes/9999999")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_OtherOwnersQuote_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();

        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        (await Client.DeleteAsync($"/api/quotes/{quoteId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Documents_UploadListDelete_RoundTrips()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var quoteId = await CreateQuoteAsync();

        var upload = await UploadDocumentAsync(quoteId, "proposal.pdf", "Customer proposal");

        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var uploaded = await ReadJsonAsync(upload);
        uploaded.GetProperty("originalFileName").GetString().Should().Be("proposal.pdf");
        uploaded.GetProperty("label").GetString().Should().Be("Customer proposal");
        var filePath = uploaded.GetProperty("filePath").GetString();
        filePath.Should().StartWith($"/files/quotes/{quoteId}/");
        (await Client.GetAsync(filePath)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await Client.GetAsync($"/api/quotes/{quoteId}/documents");
        list.EnsureSuccessStatusCode();
        var listed = await ReadJsonAsync(list);
        listed.GetArrayLength().Should().Be(1);

        var documentId = uploaded.GetProperty("id").GetInt32();
        var content = await Client.GetAsync($"/api/quotes/{quoteId}/documents/{documentId}/content");
        content.StatusCode.Should().Be(HttpStatusCode.OK);
        (await content.Content.ReadAsStringAsync()).Should().Be("document");

        Client.DefaultRequestHeaders.Authorization = null;
        (await Client.GetAsync($"/api/quotes/{quoteId}/documents/{documentId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        (await Client.DeleteAsync($"/api/quotes/{quoteId}/documents/{documentId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadJsonAsync(await Client.GetAsync($"/api/quotes/{quoteId}/documents")))
            .GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Documents_UnsupportedExtension_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var quoteId = await CreateQuoteAsync();

        (await UploadDocumentAsync(quoteId, "proposal.exe", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Documents_OtherOwnersQuote_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();

        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        (await Client.GetAsync($"/api/quotes/{quoteId}/documents"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await UploadDocumentAsync(quoteId, "proposal.pdf", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Documents_WithoutManagePermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));

        (await UploadDocumentAsync(9999999, "proposal.pdf", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnitCost_ExposesProvenanceAndRejectsUnauthorizedOverride()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var opportunityId = await CreateOpportunityAsync();
        var catalogId = await CreateRateCatalogAsync(7_500_000m);
        var denied = await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId,
            method = "UnitCost",
            areaSqm = 20m,
            unitPricePerSqm = 7_000_000m,
            materialRateCatalogId = catalogId,
            pricingEffectiveDate = "2026-09-01",
            rateOverrideReason = "Điều chỉnh theo phạm vi thi công thực tế.",
        });
        denied.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var created = await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId,
            method = "UnitCost",
            areaSqm = 20m,
            materialRateCatalogId = catalogId,
            pricingEffectiveDate = "2026-09-01",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(created);
        body.GetProperty("rateSource").GetString().Should().Be("Catalog");
        body.GetProperty("catalogUnitPricePerSqm").GetDecimal().Should().Be(7_500_000m);
        body.GetProperty("materialRateRevisionId").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UnitCost_RejectsDateWithoutApprovedEffectiveRevision()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var opportunityId = await CreateOpportunityAsync();
        var catalogId = await CreateRateCatalogAsync(7_500_000m);

        var response = await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId,
            method = "UnitCost",
            areaSqm = 20m,
            materialRateCatalogId = catalogId,
            pricingEffectiveDate = "2025-12-31",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString()
            .Should().Contain("đã duyệt có hiệu lực");
    }

    [Fact]
    public async Task UnitCost_SalesManagerCanOverrideWithVietnameseReason()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var opportunityId = await CreateOpportunityAsync();
        var catalogId = await CreateRateCatalogAsync(7_500_000m);

        var response = await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId,
            method = "UnitCost",
            areaSqm = 20m,
            unitPricePerSqm = 7_000_000m,
            materialRateCatalogId = catalogId,
            pricingEffectiveDate = "2026-09-01",
            rateOverrideReason = "Điều chỉnh theo phạm vi thi công thực tế.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(response);
        body.GetProperty("rateSource").GetString().Should().Be("Override");
        body.GetProperty("unitPricePerSqm").GetDecimal().Should().Be(7_000_000m);
        body.GetProperty("catalogUnitPricePerSqm").GetDecimal().Should().Be(7_500_000m);
        body.GetProperty("rateOverrideByUserId").GetInt32().Should().BeGreaterThan(0);
        body.GetProperty("rateOverrideAt").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ExportPdf_ReturnsPreliminaryPdfAndRequiresAuthentication()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();
        using (var scope = Factory.Services.CreateScope())
        {
            var translations = scope.ServiceProvider.GetRequiredService<TranslationService>();
            await UpsertEnglishPdfTranslationAsync(translations, "quotes.pdf.title", "PRELIMINARY QUOTATION");
            await UpsertEnglishPdfTranslationAsync(translations, "quotes.pdf.preliminaryWatermark", "PRELIMINARY");
            await UpsertEnglishPdfTranslationAsync(translations, "quotes.pdf.customerOpportunity", "CUSTOMER / OPPORTUNITY");
            await UpsertEnglishPdfTranslationAsync(translations, "quotes.pdf.area", "Area");
            await UpsertEnglishPdfTranslationAsync(translations, "quotes.pdf.catalogRate", "Catalog rate/m²");
            await UpsertEnglishPdfTranslationAsync(translations, "quotes.pdf.appliedRate", "Applied rate/m²");
            await UpsertEnglishPdfTranslationAsync(translations, "quotes.pdf.discount", "Discount");
            await UpsertEnglishPdfTranslationAsync(translations, "quotes.pdf.grandTotal", "GRAND TOTAL");
        }
        var response = await Client.GetAsync($"/api/quotes/{quoteId}/export.pdf?lang=en");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes[..5].Should().Equal("%PDF-"u8.ToArray());
        using var document = PdfDocument.Open(bytes);
        var text = string.Join('\n', document.GetPages()
            .Select(page => ContentOrderTextExtractor.GetText(page)));
        text.Should().Contain("PRELIMINARY");
        text.Should().Contain("PRELIMINARY QUOTATION");
        text.Should().Contain("CUSTOMER / OPPORTUNITY");
        text.Should().Contain("Area");
        text.Should().Contain("Catalog rate/m²");
        text.Should().Contain("Applied rate/m²");
        text.Should().Contain("Discount");
        text.Should().Contain("VAT");
        text.Should().Contain("GRAND TOTAL");

        Client.DefaultRequestHeaders.Authorization = null;
        (await Client.GetAsync($"/api/quotes/{quoteId}/export.pdf"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync($"/api/quotes/{quoteId}/export.pdf"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("vi", "BÁO GIÁ SƠ BỘ", "SƠ BỘ")]
    [InlineData("en", "PRELIMINARY QUOTATION", "PRELIMINARY")]
    [InlineData("zh", "初步报价单", "初步")]
    [InlineData("ja", "概算見積書", "概算")]
    public async Task ExportPdf_SupportsLocalizedUnicodeText(string language, string title, string watermark)
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();
        using (var scope = Factory.Services.CreateScope())
        {
            var translations = scope.ServiceProvider.GetRequiredService<TranslationService>();
            await translations.UpsertPairAsync(
                "quotes.pdf.title",
                language == "vi" ? title : "BÁO GIÁ SƠ BỘ",
                language == "vi"
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string> { [language] = title },
                "quotes");
            await translations.UpsertPairAsync(
                "quotes.pdf.preliminaryWatermark",
                language == "vi" ? watermark : "SƠ BỘ",
                language == "vi"
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string> { [language] = watermark },
                "quotes");
        }

        var response = await Client.GetAsync($"/api/quotes/{quoteId}/export.pdf?lang={language}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync());
        var text = string.Join('\n', document.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)));
        text.Should().Contain(title).And.Contain(watermark);
    }

    [Fact]
    public async Task ExportPdf_RejectsUnsupportedLanguage()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();

        var response = await Client.GetAsync($"/api/quotes/{quoteId}/export.pdf?lang=fr");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString()
            .Should().Contain("vi, en, zh hoặc ja");
    }

    private static Task UpsertEnglishPdfTranslationAsync(
        TranslationService translations,
        string key,
        string value) => translations.UpsertPairAsync(
            key,
            value,
            new Dictionary<string, string> { ["en"] = value },
            "quotes");

    // ---------- helpers ----------

    private async Task<HttpResponseMessage> UploadDocumentAsync(int quoteId, string fileName, string? label)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("document"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);
        if (label is not null) content.Add(new StringContent(label), "label");
        return await Client.PostAsync($"/api/quotes/{quoteId}/documents", content);
    }

    private async Task<int> CreateCustomerAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Q-Customer " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new
            {
                fullName = "Contact",
                phone = "0922" + Random.Shared.Next(100000, 999999),
                isPrimary = true,
            },
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateOpportunityAsync()
    {
        var customerId = await CreateCustomerAsync();
        var res = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = "Q-Deal " + Guid.NewGuid().ToString("N")[..6],
            customerId,
            estimatedValue = 500_000_000m,
            winProbability = 40,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateQuoteAsync()
    {
        var oppId = await CreateOpportunityAsync();
        var rateCatalogId = await CreateRateCatalogAsync(5_000_000m);
        var res = await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId = oppId,
            method = "UnitCost",
            areaSqm = 100m,
            unitPricePerSqm = 5_000_000m,
            materialRateCatalogId = rateCatalogId,
            pricingEffectiveDate = "2026-09-01",
            discountPercent = 0m,
            vatPercent = 8m,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private Task<int> CreateRateCatalogAsync(decimal amountPerSqm) => WithDbAsync(async db =>
    {
        var catalog = new MaterialRateCatalog
        {
            Code = "RATE-" + Guid.NewGuid().ToString("N")[..10],
            Name = "Integration rate",
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };
        catalog.Revisions.Add(new MaterialRateRevision
        {
            Version = 1,
            Status = MaterialRateRevisionStatus.Approved,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
            Lines =
            [
                new MaterialRateLine
                {
                    MaterialCode = "PACKAGE",
                    MaterialName = "Standard package",
                    Unit = "m2",
                    AmountPerSqm = amountPerSqm,
                },
            ],
        });
        db.MaterialRateCatalogs.Add(catalog);
        await db.SaveChangesAsync();
        return catalog.Id;
    });

    private Task<(int CatalogId, int RevisionId, string Code, string Name)> CreateBoqRateCatalogAsync() =>
        WithDbAsync(async db =>
        {
            var catalog = new MaterialRateCatalog
            {
                CatalogType = MaterialRateCatalogType.Boq,
                Code = "BOQ-RATE-" + Guid.NewGuid().ToString("N")[..8],
                Name = "Integration BOQ rate",
                CreatedByUserId = 1,
                UpdatedByUserId = 1,
                Revisions =
                [
                    new MaterialRateRevision
                    {
                        Version = 1,
                        Status = MaterialRateRevisionStatus.Approved,
                        EffectiveFrom = new DateOnly(2026, 1, 1),
                        CreatedByUserId = 1,
                        UpdatedByUserId = 1,
                    },
                ],
            };
            db.MaterialRateCatalogs.Add(catalog);
            await db.SaveChangesAsync();
            return (catalog.Id, catalog.Revisions[0].Id, catalog.Code, catalog.Name);
        });
}
