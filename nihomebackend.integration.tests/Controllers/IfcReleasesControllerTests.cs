using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>IfcReleasesController</c> (NIH-118):
/// RBAC gating (view / manage / stricter release), CRUD lifecycle,
/// item + recipient management, atomic release action that flips
/// bundled shop drawings to Released, and post-release ack tracking.
/// </summary>
public class IfcReleasesControllerTests : IntegrationTestBase
{
    public IfcReleasesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/ifc-releases")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/ifc-releases")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        (await Client.GetAsync("/api/ifc-releases")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_HappyPath_AllocatesNumberAsDraft()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, _, _) = await CreateShopStageProjectWithApprovedDrawingAsync();

        var res = await Client.PostAsJsonAsync("/api/ifc-releases", new
        {
            designProjectId = projectId,
            title = $"E2E release {Guid.NewGuid():N}",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Draft");
        body.GetProperty("releaseNumber").GetString().Should().StartWith("IFC-");
    }

    [Fact]
    public async Task Create_ProjectNotAtShopStage_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var proj = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Concept-stage {Guid.NewGuid():N}",
            customerId,
        });
        var projectId = (await ReadJsonAsync(proj)).GetProperty("id").GetInt32();

        var res = await Client.PostAsJsonAsync("/api/ifc-releases", new
        {
            designProjectId = projectId,
            title = "should fail",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddItems_DraftingDrawing_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, approvedId, draftingId) = await CreateShopStageProjectWithApprovedDrawingAsync();
        var release = await Client.PostAsJsonAsync("/api/ifc-releases", new
        {
            designProjectId = projectId,
            title = "R",
        });
        var releaseId = (await ReadJsonAsync(release)).GetProperty("id").GetInt32();

        // Drafting drawing rejected.
        var bad = await Client.PostAsJsonAsync($"/api/ifc-releases/{releaseId}/items", new
        {
            shopDrawingIds = new[] { draftingId },
        });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Approved drawing accepted.
        var good = await Client.PostAsJsonAsync($"/api/ifc-releases/{releaseId}/items", new
        {
            shopDrawingIds = new[] { approvedId },
        });
        good.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReleaseAsync_RequiresReleasePermission()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, approvedId, _) = await CreateShopStageProjectWithApprovedDrawingAsync();
        var releaseId = await CreateFullDraftAsync(projectId, approvedId);

        // DESIGN role has design.ifc.view + manage but NOT release (only Design Lead + BGD do).
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var res = await Client.PostAsync($"/api/ifc-releases/{releaseId}/release", null);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReleaseAsync_HappyPath_FlipsDrawings()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, approvedId, _) = await CreateShopStageProjectWithApprovedDrawingAsync();
        var releaseId = await CreateFullDraftAsync(projectId, approvedId);

        var res = await Client.PostAsync($"/api/ifc-releases/{releaseId}/release", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Released");

        var drawingRes = await Client.GetAsync($"/api/shop-drawings/{approvedId}");
        drawingRes.EnsureSuccessStatusCode();
        (await ReadJsonAsync(drawingRes)).GetProperty("status").GetString().Should().Be("Released");
    }

    [Fact]
    public async Task ReleaseAsync_WithoutItems_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, _, _) = await CreateShopStageProjectWithApprovedDrawingAsync();
        var draft = await Client.PostAsJsonAsync("/api/ifc-releases", new
        {
            designProjectId = projectId,
            title = "empty draft",
        });
        var releaseId = (await ReadJsonAsync(draft)).GetProperty("id").GetInt32();
        (await Client.PostAsJsonAsync($"/api/ifc-releases/{releaseId}/recipients", new
        {
            name = "ABC",
            recipientTypeCode = "main-contractor",
        })).EnsureSuccessStatusCode();

        (await Client.PostAsync($"/api/ifc-releases/{releaseId}/release", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_AfterRelease_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, approvedId, _) = await CreateShopStageProjectWithApprovedDrawingAsync();
        var releaseId = await CreateFullDraftAsync(projectId, approvedId);
        (await Client.PostAsync($"/api/ifc-releases/{releaseId}/release", null)).EnsureSuccessStatusCode();

        var res = await Client.PutAsJsonAsync($"/api/ifc-releases/{releaseId}", new
        {
            title = "renamed",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Acknowledge_AfterRelease_Succeeds()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, approvedId, _) = await CreateShopStageProjectWithApprovedDrawingAsync();
        var releaseId = await CreateFullDraftAsync(projectId, approvedId);
        var full = await Client.GetAsync($"/api/ifc-releases/{releaseId}");
        var body = await ReadJsonAsync(full);
        var recipientId = body.GetProperty("recipients")[0].GetProperty("id").GetInt32();
        (await Client.PostAsync($"/api/ifc-releases/{releaseId}/release", null)).EnsureSuccessStatusCode();

        var ackRes = await Client.PostAsJsonAsync(
            $"/api/ifc-releases/{releaseId}/recipients/{recipientId}/acknowledge",
            new { acknowledgementNote = "email confirmed" });
        ackRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var reloaded = await ReadJsonAsync(ackRes);
        var recipient = reloaded.GetProperty("recipients").EnumerateArray()
            .First(r => r.GetProperty("id").GetInt32() == recipientId);
        recipient.GetProperty("isAcknowledged").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_Draft_MovesToCancelled()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, approvedId, _) = await CreateShopStageProjectWithApprovedDrawingAsync();
        var releaseId = await CreateFullDraftAsync(projectId, approvedId);
        var res = await Client.PostAsync($"/api/ifc-releases/{releaseId}/cancel", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(res)).GetProperty("status").GetString().Should().Be("Cancelled");
    }

    // -------- helpers --------

    private async Task<(int ProjectId, int ApprovedShopId, int DraftingShopId)> CreateShopStageProjectWithApprovedDrawingAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var proj = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"IFC fixture {Guid.NewGuid():N}",
            customerId,
        });
        proj.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(proj)).GetProperty("id").GetInt32();

        // Concept → BasicDesign
        var opt = await Client.PostAsJsonAsync("/api/concept-options", new
        {
            designProjectId = projectId,
            name = "AutoFinalized",
        });
        var optId = (await ReadJsonAsync(opt)).GetProperty("id").GetInt32();
        foreach (var s in new[] { "PendingInternalReview", "PresentedToClient", "Finalized" })
        {
            (await Client.PostAsJsonAsync($"/api/concept-options/{optId}/status", new { status = s }))
                .EnsureSuccessStatusCode();
        }
        // BasicDesign → ShopDrawing
        foreach (var d in new[] { "architecture", "structure", "mep" })
        {
            var docRes = await Client.PostAsJsonAsync("/api/basic-design-docs", new
            {
                designProjectId = projectId,
                disciplineCode = d,
                title = $"BD {d} {Guid.NewGuid():N}",
            });
            var docId = (await ReadJsonAsync(docRes)).GetProperty("id").GetInt32();
            (await Client.PostAsJsonAsync($"/api/basic-design-docs/{docId}/status", new { status = "SubmittedForReview" }))
                .EnsureSuccessStatusCode();
            (await Client.PostAsJsonAsync($"/api/basic-design-docs/{docId}/status", new { status = "InternallyApproved" }))
                .EnsureSuccessStatusCode();
        }
        (await Client.PostAsync($"/api/basic-design-docs/design-project/{projectId}/unlock-shop-drawing", null))
            .EnsureSuccessStatusCode();

        // Approved shop drawing.
        var approvedShop = await Client.PostAsJsonAsync("/api/shop-drawings", new
        {
            designProjectId = projectId,
            disciplineCode = "architecture",
            constructionItem = "Móng cọc",
            title = $"approved SD {Guid.NewGuid():N}",
        });
        var approvedId = (await ReadJsonAsync(approvedShop)).GetProperty("id").GetInt32();
        (await Client.PostAsJsonAsync($"/api/shop-drawings/{approvedId}/status", new { status = "InReview" }))
            .EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"/api/shop-drawings/{approvedId}/status", new { status = "Approved" }))
            .EnsureSuccessStatusCode();

        // Drafting shop drawing (still Drafting — used to prove the guard).
        var draftingShop = await Client.PostAsJsonAsync("/api/shop-drawings", new
        {
            designProjectId = projectId,
            disciplineCode = "structure",
            constructionItem = "Cột kết cấu",
            title = $"drafting SD {Guid.NewGuid():N}",
        });
        var draftingId = (await ReadJsonAsync(draftingShop)).GetProperty("id").GetInt32();

        return (projectId, approvedId, draftingId);
    }

    private async Task<int> CreateFullDraftAsync(int projectId, int approvedShopId)
    {
        var draft = await Client.PostAsJsonAsync("/api/ifc-releases", new
        {
            designProjectId = projectId,
            title = $"E2E full draft {Guid.NewGuid():N}",
        });
        var releaseId = (await ReadJsonAsync(draft)).GetProperty("id").GetInt32();
        (await Client.PostAsJsonAsync($"/api/ifc-releases/{releaseId}/items", new
        {
            shopDrawingIds = new[] { approvedShopId },
        })).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"/api/ifc-releases/{releaseId}/recipients", new
        {
            name = "ABC Corp",
            recipientTypeCode = "main-contractor",
        })).EnsureSuccessStatusCode();
        return releaseId;
    }

    private async Task<int> FirstCustomerIdAsync()
    {
        return await WithDbAsync<int>(async db =>
        {
            var existing = await db.Customers.OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (existing != null) return existing.Id;
            var customer = new Customer
            {
                Name = "IFC Test Customer " + Guid.NewGuid().ToString("N")[..6],
                SourceCode = "referral",
                RelationshipStatus = CustomerRelationshipStatus.InProgress,
                Type = CustomerType.Company,
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            return customer.Id;
        });
    }
}
