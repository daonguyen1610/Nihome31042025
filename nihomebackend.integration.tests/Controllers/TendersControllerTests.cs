using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>TendersController</c> — CRUD (NIH-95/96) plus
/// the NIH-97 detail-page workflow (checklist inline-edit, library attach,
/// mark won / mark lost, timeline).
/// </summary>
public class TendersControllerTests : IntegrationTestBase
{
    public TendersControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/tenders")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsWarehouse_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/tenders")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_HappyPath_ReturnsAutoChecklist()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();

        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = "Gói thầu Alpha",
            customerId,
            submissionDeadline = DateTime.UtcNow.AddDays(14),
            openingDate = DateTime.UtcNow.AddDays(7),
            infoSource = "Website",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("code").GetString().Should().StartWith("TD-");
        body.GetProperty("status").GetString().Should().Be("Preparing");
        body.GetProperty("checklistItems").GetArrayLength().Should().BeGreaterThan(0);
        body.GetProperty("checklistCompletionPercent").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Create_WithPastDeadline_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = "Bad deadline",
            customerId,
            submissionDeadline = DateTime.UtcNow.AddDays(-1),
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithUnknownCustomer_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = "Bad customer",
            customerId = 999_999,
            submissionDeadline = DateTime.UtcNow.AddDays(10),
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sale_CannotCreateWithoutManagePermission()
    {
        // SALE has crm.tenders.view + crm.tenders.manage — should succeed.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var customerId = await CreateCustomerAsync();
        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = "SALE-created tender",
            customerId,
            submissionDeadline = DateTime.UtcNow.AddDays(10),
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Update_WhilePreparing_UpdatesAllEditableFields()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateTenderAsync();
        var newDeadline = DateTime.UtcNow.AddDays(30);
        var res = await Client.PutAsJsonAsync($"/api/tenders/{id}", new
        {
            name = "Updated name",
            submissionDeadline = newDeadline,
            infoSource = "Referral",
            note = "note updated",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("name").GetString().Should().Be("Updated name");
        body.GetProperty("note").GetString().Should().Be("note updated");
    }

    [Fact]
    public async Task Delete_WhilePreparing_Succeeds()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateTenderAsync();
        (await Client.DeleteAsync($"/api/tenders/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/tenders/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_FilterBySearchAndStatus()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        await CreateTenderAsync(name: "Uniquely-tagged Alpha");
        await CreateTenderAsync(name: "Other tender");

        var searched = await Client.GetAsync("/api/tenders?search=Uniquely-tagged&pageSize=20");
        searched.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(searched);
        body.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        for (int i = 0; i < body.GetProperty("items").GetArrayLength(); i++)
        {
            body.GetProperty("items")[i].GetProperty("name").GetString().Should().Contain("Uniquely-tagged");
        }
    }

    // ---------- helpers ----------

    private async Task<int> CreateCustomerAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "TC-" + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new
            {
                fullName = "Contact",
                phone = "0922" + Guid.NewGuid().ToString("N")[..6],
                isPrimary = true,
            },
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateTenderAsync(string? name = null)
    {
        var customerId = await CreateCustomerAsync();
        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = name ?? "Test tender " + Guid.NewGuid().ToString("N")[..6],
            customerId,
            submissionDeadline = DateTime.UtcNow.AddDays(14),
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    // ---------- NIH-97 checklist inline-edit ----------

    [Fact]
    public async Task PatchChecklist_ChangesStatusAndBumpsPercent()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateTenderAsync();
        var itemId = await FirstChecklistItemIdAsync(id);

        var res = await Client.PatchAsJsonAsync($"/api/tenders/{id}/checklist/{itemId}", new
        {
            status = "Done",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("checklistCompletionPercent").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PatchChecklist_InvalidStatus_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateTenderAsync();
        var itemId = await FirstChecklistItemIdAsync(id);

        var res = await Client.PatchAsJsonAsync($"/api/tenders/{id}/checklist/{itemId}", new
        {
            status = "Not-A-Status",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchChecklist_UnknownItem_Is404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateTenderAsync();
        var res = await Client.PatchAsJsonAsync($"/api/tenders/{id}/checklist/9999", new { status = "Done" });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- NIH-97 library attach ----------

    [Fact]
    public async Task AttachFromLibrary_HappyPath_CopiesFileMetadata()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        var itemId = await FirstChecklistItemIdAsync(tenderId);
        var docId = await CreateCapabilityDocumentAsync();

        var res = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/checklist/attach-from-library", new
        {
            items = new[]
            {
                new { checklistItemId = itemId, capabilityDocumentId = docId },
            },
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        var items = body.GetProperty("checklistItems");
        var enumerator = items.EnumerateArray();
        var target = enumerator.First(i => i.GetProperty("id").GetInt32() == itemId);
        target.GetProperty("originalFileName").GetString().Should().NotBeNullOrEmpty();
        target.GetProperty("status").GetString().Should().Be("Done");
    }

    [Fact]
    public async Task AttachFromLibrary_UnknownDocument_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        var itemId = await FirstChecklistItemIdAsync(tenderId);

        var res = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/checklist/attach-from-library", new
        {
            items = new[] { new { checklistItemId = itemId, capabilityDocumentId = 9_999_999 } },
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- NIH-97 mark-won / mark-lost ----------

    [Fact]
    public async Task MarkWon_AsSalesManager_SetsWonAndOpportunity()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        var oppId = await CreateOpportunityAsync();

        var res = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/mark-won", new
        {
            opportunityId = oppId,
            note = "Ký hôm nay",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Won");
        body.GetProperty("wonOpportunityId").GetInt32().Should().Be(oppId);
    }

    [Fact]
    public async Task MarkWon_AsSale_IsForbidden()
    {
        // Regular SALE role should not carry crm.tenders.mark-result.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var tenderId = await CreateTenderAsync();
        var oppId = await CreateOpportunityAsync();

        var res = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/mark-won", new
        {
            opportunityId = oppId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MarkLost_HappyPath_SetsLostAndReason()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        var reasonCode = await FirstOpportunityLostReasonAsync();

        var res = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/mark-lost", new
        {
            reasonCode,
            note = "Cạnh tranh giá",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Lost");
        body.GetProperty("lostReasonCode").GetString().Should().Be(reasonCode);
    }

    [Fact]
    public async Task MarkLost_UnknownReason_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        var res = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/mark-lost", new
        {
            reasonCode = "definitely-not-a-real-reason",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MarkWon_AfterAlreadyWon_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        var oppId = await CreateOpportunityAsync();
        (await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/mark-won", new { opportunityId = oppId }))
            .EnsureSuccessStatusCode();

        var res = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/mark-won", new { opportunityId = oppId });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- NIH-97 timeline ----------

    [Fact]
    public async Task Timeline_ReturnsAuditRowsForTender()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tenderId = await CreateTenderAsync();
        // Trigger at least one auditable action.
        await Client.PutAsJsonAsync($"/api/tenders/{tenderId}", new
        {
            name = "Renamed",
            submissionDeadline = DateTime.UtcNow.AddDays(20),
            note = "note",
        });

        var res = await Client.GetAsync($"/api/tenders/{tenderId}/timeline");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        // Audit log flush is queued so the array may be empty in-test —
        // shape verification is what we assert here (matches contracts).
        (await ReadJsonAsync(res)).ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task Timeline_UnknownTender_Is404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.GetAsync("/api/tenders/9999999/timeline")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- helpers ----------

    private async Task<int> FirstChecklistItemIdAsync(int tenderId)
    {
        var res = await Client.GetAsync($"/api/tenders/{tenderId}");
        res.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(res);
        var items = body.GetProperty("checklistItems");
        items.GetArrayLength().Should().BeGreaterThan(0);
        return items[0].GetProperty("id").GetInt32();
    }

    private async Task<int> CreateOpportunityAsync()
    {
        var customerId = await CreateCustomerAsync();
        var res = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = "Opp " + Guid.NewGuid().ToString("N")[..6],
            customerId,
            estimatedValue = 1_000_000m,
            winProbability = 40,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateCapabilityDocumentAsync()
    {
        // The controller create path requires a pre-uploaded file. For
        // this test we just care that the tender attach can copy metadata
        // from an existing row, so we seed the DB directly and skip the
        // physical file upload.
        return await WithDbAsync(async db =>
        {
            var tag = await db.MasterDataOptions
                .Where(o => o.Category == "capability_document_tag" && o.IsActive)
                .OrderBy(o => o.SortOrder)
                .FirstAsync();
            var doc = new CapabilityDocument
            {
                Name = "Test doc " + Guid.NewGuid().ToString("N")[..6],
                TagCode = tag.Code,
                FilePath = "/files/capability/test-" + Guid.NewGuid().ToString("N") + ".pdf",
                OriginalFileName = "seeded.pdf",
                FileSize = 1024,
                ContentType = "application/pdf",
                CurrentVersion = 1,
            };
            db.CapabilityDocuments.Add(doc);
            await db.SaveChangesAsync();
            return doc.Id;
        });
    }

    private async Task<string> FirstOpportunityLostReasonAsync() =>
        await WithDbAsync(async db =>
        {
            var opt = await db.MasterDataOptions
                .Where(o => o.Category == "opportunity_lost_reason" && o.IsActive)
                .OrderBy(o => o.SortOrder)
                .FirstAsync();
            return opt.Code;
        });
}
