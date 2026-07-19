using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>DrawingRevisionsController</c> (NIH-117):
/// RBAC gating, append-only create semantics (auto-number + previous
/// flip to superseded), diff endpoint, and cross-family target
/// resolution.
/// </summary>
public class DrawingRevisionsControllerTests : IntegrationTestBase
{
    public DrawingRevisionsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/drawing-revisions")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/drawing-revisions")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var res = await Client.GetAsync("/api/drawing-revisions");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_ForShopDrawing_HappyPath_StartsAtR1()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();

        var res = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "client-request",
            note = "First revision from integration test",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("revisionNumber").GetInt32().Should().Be(1);
        body.GetProperty("isCurrent").GetBoolean().Should().BeTrue();
        body.GetProperty("targetType").GetString().Should().Be("ShopDrawing");
        body.GetProperty("targetCode").GetString().Should().StartWith("KT-SD-");
    }

    [Fact]
    public async Task Create_SecondRevision_FlipsPreviousToSuperseded()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();

        var first = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "client-request",
            note = "R1",
        });
        first.EnsureSuccessStatusCode();
        var firstId = (await ReadJsonAsync(first)).GetProperty("id").GetInt32();

        var second = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "mep-sync",
            note = "R2",
        });
        second.EnsureSuccessStatusCode();
        (await ReadJsonAsync(second)).GetProperty("revisionNumber").GetInt32().Should().Be(2);

        var reloaded = await Client.GetAsync($"/api/drawing-revisions/{firstId}");
        reloaded.EnsureSuccessStatusCode();
        (await ReadJsonAsync(reloaded)).GetProperty("isCurrent").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Create_UnknownTarget_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var res = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = 9999999,
            reasonCode = "client-request",
            note = "should fail",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_UnknownReason_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();
        var res = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "not-a-real-reason",
            note = "note",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_MissingNote_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();
        var res = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "client-request",
            note = "",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sale_CannotCreate()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var res = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "client-request",
            note = "blocked",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Diff_ReturnsMetadataChanges()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();

        var a = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "client-request",
            note = "first note",
        });
        var b = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "mep-sync",
            note = "different note",
        });
        var aId = (await ReadJsonAsync(a)).GetProperty("id").GetInt32();
        var bId = (await ReadJsonAsync(b)).GetProperty("id").GetInt32();

        var diff = await Client.GetAsync($"/api/drawing-revisions/diff?fromId={aId}&toId={bId}");
        diff.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(diff);
        body.GetProperty("changes").GetArrayLength().Should().BeGreaterOrEqualTo(1);
        body.GetProperty("from").GetProperty("id").GetInt32().Should().Be(aId);
        body.GetProperty("to").GetProperty("id").GetInt32().Should().Be(bId);
    }

    [Fact]
    public async Task Diff_AcrossDifferentTargets_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();

        // Create a second shop drawing on the same project.
        var second = await Client.PostAsJsonAsync("/api/shop-drawings", new
        {
            designProjectId = projectId,
            disciplineCode = "structure",
            constructionItem = "Cột kết cấu",
            title = "Second SD",
        });
        second.EnsureSuccessStatusCode();
        var shopId2 = (await ReadJsonAsync(second)).GetProperty("id").GetInt32();

        var a = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "client-request",
            note = "a",
        });
        var b = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId2,
            reasonCode = "client-request",
            note = "b",
        });
        var aId = (await ReadJsonAsync(a)).GetProperty("id").GetInt32();
        var bId = (await ReadJsonAsync(b)).GetProperty("id").GetInt32();

        var diff = await Client.GetAsync($"/api/drawing-revisions/diff?fromId={aId}&toId={bId}");
        diff.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -------- helpers --------

    private async Task<(int ProjectId, int ShopDrawingId)> CreateShopStageProjectWithFirstDrawingAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var proj = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Rev fixture {Guid.NewGuid():N}",
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
        opt.EnsureSuccessStatusCode();
        var optId = (await ReadJsonAsync(opt)).GetProperty("id").GetInt32();
        foreach (var s in new[] { "PendingInternalReview", "PresentedToClient", "Finalized" })
        {
            (await Client.PostAsJsonAsync($"/api/concept-options/{optId}/status", new { status = s }))
                .EnsureSuccessStatusCode();
        }

        // BasicDesign → ShopDrawing (approve 1 doc per required discipline)
        foreach (var d in new[] { "architecture", "structure", "mep" })
        {
            var docRes = await Client.PostAsJsonAsync("/api/basic-design-docs", new
            {
                designProjectId = projectId,
                disciplineCode = d,
                title = $"BD {d} {Guid.NewGuid():N}",
            });
            docRes.EnsureSuccessStatusCode();
            var docId = (await ReadJsonAsync(docRes)).GetProperty("id").GetInt32();
            (await Client.PostAsJsonAsync($"/api/basic-design-docs/{docId}/status", new { status = "SubmittedForReview" }))
                .EnsureSuccessStatusCode();
            (await Client.PostAsJsonAsync($"/api/basic-design-docs/{docId}/status", new { status = "InternallyApproved" }))
                .EnsureSuccessStatusCode();
        }
        (await Client.PostAsync($"/api/basic-design-docs/design-project/{projectId}/unlock-shop-drawing", null))
            .EnsureSuccessStatusCode();

        // First shop drawing.
        var shop = await Client.PostAsJsonAsync("/api/shop-drawings", new
        {
            designProjectId = projectId,
            disciplineCode = "architecture",
            constructionItem = "Móng cọc",
            title = $"SD arch {Guid.NewGuid():N}",
        });
        shop.EnsureSuccessStatusCode();
        var shopId = (await ReadJsonAsync(shop)).GetProperty("id").GetInt32();

        return (projectId, shopId);
    }

    private async Task<int> FirstCustomerIdAsync()
    {
        return await WithDbAsync<int>(async db =>
        {
            var existing = await db.Customers.OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (existing != null) return existing.Id;
            var customer = new Customer
            {
                Name = "Rev Test Customer " + Guid.NewGuid().ToString("N")[..6],
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
