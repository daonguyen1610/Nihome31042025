using Microsoft.Extensions.DependencyInjection;
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
    public async Task Procurement_ReceivesOnlyAddressedReleasedPacket_AndAcknowledgesOnce()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (designId, drawingId, _) = await CreateShopStageProjectWithApprovedDrawingAsync();
        var contentRoot = Factory.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>().ContentRootPath;
        var filePath = Path.Combine(contentRoot, "wwwroot", "files", "design", "shop", $"ifc-{Guid.NewGuid():N}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, $"ShopDrawing:{drawingId}");
        await WithDbAsync(async db =>
        {
            var drawing = await db.ShopDrawings.SingleAsync(x => x.Id == drawingId);
            drawing.FilePath = $"/files/design/shop/{Path.GetFileName(filePath)}";
            drawing.OriginalFileName = "NICON-phat-hanh-ket-cau.pdf";
            drawing.ContentType = "application/pdf";
            await db.SaveChangesAsync();
        });
        var releaseId = await CreateFullDraftAsync(designId, drawingId);
        var context = await WithDbAsync(async db =>
        {
            var projectId = await db.DesignProjects.Where(x => x.Id == designId).Select(x => x.OperationalProjectId!.Value).SingleAsync();
            var receiverId = await db.Users.Where(x => x.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PROCUREMENT"])
                .Select(x => x.Id).SingleAsync();
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = projectId,
                UserId = receiverId,
                Position = "Cung ứng hồ sơ IFC",
                StartedAt = DateTime.UtcNow.AddDays(-1),
            });
            await db.SaveChangesAsync();
            return (ProjectId: projectId, ReceiverId: receiverId);
        });
        var added = await Client.PostAsJsonAsync($"/api/ifc-releases/{releaseId}/recipients", new
        {
            name = "Cung ứng NICON",
            recipientTypeCode = "main-contractor",
            recipientUserId = context.ReceiverId,
        });
        added.StatusCode.Should().Be(HttpStatusCode.OK);
        var internalRecipientId = (await ReadJsonAsync(added)).GetProperty("recipients").EnumerateArray()
            .Single(x => x.GetProperty("recipientUserId").ValueKind == System.Text.Json.JsonValueKind.Number).GetProperty("id").GetInt32();
        (await Client.PostAsJsonAsync($"/api/ifc-releases/{releaseId}/recipients", new
        {
            name = "Cung ứng trùng",
            recipientTypeCode = "main-contractor",
            recipientUserId = context.ReceiverId,
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var primaryEvidence = await WithDbAsync(async db =>
        {
            var review = await db.DesignReviews.Include(x => x.Documents).ThenInclude(x => x.ProjectDocument)
                .SingleAsync(x => x.DesignProjectId == designId && x.Stage == DesignReviewStage.ShopDrawing);
            var primary = review.Documents.Single(x => x.SourceRecordIdSnapshot == drawingId);
            var evidence = (ReviewId: review.Id, DocumentId: primary.ProjectDocumentId, Hash: primary.Sha256Snapshot);
            db.DesignReviewDocuments.Remove(primary);
            var supplement = new ProjectDocument
            {
                OperationalProjectId = context.ProjectId,
                Category = ProjectDocumentCategory.DesignShopDrawing,
                SourceModule = ProjectDocumentSourceModule.Design,
                SourceEntityType = nameof(ShopDrawing),
                SourceRecordId = drawingId,
                SourceSlot = "manual-upload:supplement",
                LocalPath = "/files/project-documents/NICON-thuyet-minh-ket-cau-B.pdf",
                OriginalFileName = "NICON-thuyet-minh-ket-cau-B.pdf",
                ContentType = "application/pdf",
                Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("NICON supplementary evidence B"))),
                SyncStatus = ProjectDocumentSyncStatus.Synced,
            };
            review.Documents.Add(new DesignReviewDocument
            {
                ProjectDocument = supplement,
                SourceEntityTypeSnapshot = nameof(ShopDrawing),
                SourceRecordIdSnapshot = drawingId,
                Sha256Snapshot = supplement.Sha256
            });
            await db.SaveChangesAsync();
            return evidence;
        });
        var mismatchedRelease = await Client.PostAsync($"/api/ifc-releases/{releaseId}/release", null);
        mismatchedRelease.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var mismatchError = await ReadJsonAsync(mismatchedRelease);
        mismatchError.GetProperty("code").GetString().Should().Be("IFC_PRIMARY_FILE_NOT_APPROVED");
        mismatchError.GetProperty("drawingCode").GetString().Should().NotBeNullOrWhiteSpace();
        await WithDbAsync(async db =>
        {
            (await db.IfcReleases.SingleAsync(x => x.Id == releaseId)).Status.Should().Be(IfcReleaseStatus.Draft);
            (await db.ShopDrawings.SingleAsync(x => x.Id == drawingId)).Status.Should().Be(ShopDrawingStatus.Approved);
            db.DesignReviewDocuments.Add(new DesignReviewDocument
            {
                DesignReviewId = primaryEvidence.ReviewId,
                ProjectDocumentId = primaryEvidence.DocumentId,
                SourceEntityTypeSnapshot = nameof(ShopDrawing),
                SourceRecordIdSnapshot = drawingId,
                Sha256Snapshot = primaryEvidence.Hash
            });
            await db.SaveChangesAsync();
        });
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PROCUREMENT"));
        var path = $"/api/operational-projects/{context.ProjectId}/procurement/ifc";
        (await Client.GetAsync($"{path}?page=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.GetAsync($"{path}?page={int.MaxValue}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var draftInbox = await Client.GetAsync(path);
        draftInbox.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(draftInbox)).GetProperty("items").GetArrayLength().Should().Be(0);
        (await Client.PostAsJsonAsync($"{path}/{internalRecipientId}/acknowledge", new { rowVersion = Convert.ToBase64String(new byte[8]) }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        await WithDbAsync(async db =>
        {
            var member = await db.OperationalProjectMembers.SingleAsync(x => x.OperationalProjectId == context.ProjectId && x.UserId == context.ReceiverId);
            member.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });
        (await Client.PostAsync($"/api/ifc-releases/{releaseId}/release", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WithDbAsync(async db =>
        {
            (await db.IfcReleases.SingleAsync(x => x.Id == releaseId)).Status.Should().Be(IfcReleaseStatus.Draft);
            var member = await db.OperationalProjectMembers.SingleAsync(x => x.OperationalProjectId == context.ProjectId && x.UserId == context.ReceiverId);
            member.EndedAt = null;
            var other = await db.Users.SingleAsync(x => x.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PROCUREMENT_MANAGER"]);
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = context.ProjectId,
                UserId = other.Id,
                Position = "Trưởng phòng cung ứng",
                StartedAt = DateTime.UtcNow.AddDays(-1)
            });
            await db.SaveChangesAsync();
        });
        (await Client.PostAsync($"/api/ifc-releases/{releaseId}/release", null)).EnsureSuccessStatusCode();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PROCUREMENT_MANAGER"));
        (await ReadJsonAsync(await Client.GetAsync(path))).GetProperty("items").GetArrayLength().Should().Be(0);
        (await Client.PostAsJsonAsync($"{path}/{internalRecipientId}/acknowledge", new { rowVersion = Convert.ToBase64String(new byte[8]) }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PROCUREMENT"));
        var inbox = await Client.GetAsync(path);
        inbox.StatusCode.Should().Be(HttpStatusCode.OK);
        var packet = (await ReadJsonAsync(inbox)).GetProperty("items").EnumerateArray().Single();
        packet.GetProperty("releaseId").GetInt32().Should().Be(releaseId);
        var recipientId = packet.GetProperty("recipientId").GetInt32();
        var itemId = packet.GetProperty("drawings")[0].GetProperty("itemId").GetInt32();
        var downloadPath = $"{path}/{recipientId}/items/{itemId}/content";
        var otherProjectId = await CreateOperationalProjectAsync(await FirstCustomerIdAsync());
        (await Client.GetAsync($"/api/operational-projects/{otherProjectId}/procurement/ifc/{recipientId}/items/{itemId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PROCUREMENT_MANAGER"));
        (await Client.GetAsync(downloadPath)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PROCUREMENT"));
        var file = await Client.GetAsync(downloadPath);
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        (await file.Content.ReadAsStringAsync()).Should().Be($"ShopDrawing:{drawingId}");
        await File.WriteAllTextAsync(filePath, "NICON – wrong later revision");
        var changed = await Client.GetAsync(downloadPath);
        changed.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(changed)).GetProperty("code").GetString().Should().Be("IFC_RELEASED_FILE_CHANGED");
        await File.WriteAllTextAsync(filePath, $"ShopDrawing:{drawingId}");
        (await Client.GetAsync($"{path}/{recipientId}/items/{itemId + 100000}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PostAsJsonAsync($"{path}/{recipientId}/acknowledge", new { rowVersion = Convert.ToBase64String(new byte[8]) }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        var ackGrant = await WithDbAsync(async db =>
        {
            var grant = await db.RolePermissions.SingleAsync(x => x.Role.Code == "PROCUREMENT" && x.Permission.Module == "proc.ifc" && x.Permission.Action == "acknowledge");
            var ids = (grant.RoleId, grant.PermissionId);
            db.RolePermissions.Remove(grant);
            await db.SaveChangesAsync();
            return ids;
        });
        (await Client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.PostAsJsonAsync($"{path}/{recipientId}/acknowledge", new { rowVersion = packet.GetProperty("rowVersion").GetString() }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await WithDbAsync(async db =>
        {
            (await db.IfcReleaseRecipients.SingleAsync(x => x.Id == recipientId)).AcknowledgedAt.Should().BeNull();
            db.RolePermissions.Add(new NihomeBackend.Models.Rbac.RolePermission { RoleId = ackGrant.RoleId, PermissionId = ackGrant.PermissionId });
            await db.SaveChangesAsync();
        });
        var ack = await Client.PostAsJsonAsync($"{path}/{recipientId}/acknowledge", new { rowVersion = packet.GetProperty("rowVersion").GetString() });
        ack.StatusCode.Should().Be(HttpStatusCode.OK);
        var received = await ReadJsonAsync(ack);
        received.GetProperty("acknowledgedByUserId").GetInt32().Should().Be(context.ReceiverId);
        var retry = await Client.PostAsJsonAsync($"{path}/{recipientId}/acknowledge", new { rowVersion = packet.GetProperty("rowVersion").GetString() });
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(retry)).GetProperty("acknowledgedAt").GetString().Should().Be(received.GetProperty("acknowledgedAt").GetString());
        (await Client.GetAsync($"/api/ifc-releases/{releaseId}")).StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        var viewGrant = await WithDbAsync(async db =>
        {
            var grant = await db.RolePermissions.SingleAsync(x => x.Role.Code == "PROCUREMENT" && x.Permission.Module == "proc.ifc" && x.Permission.Action == "view");
            var ids = (grant.RoleId, grant.PermissionId);
            db.RolePermissions.Remove(grant);
            await db.SaveChangesAsync();
            return ids;
        });
        (await Client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.GetAsync(downloadPath)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await WithDbAsync(async db =>
        {
            db.RolePermissions.Add(new NihomeBackend.Models.Rbac.RolePermission { RoleId = viewGrant.RoleId, PermissionId = viewGrant.PermissionId });
            var member = await db.OperationalProjectMembers.SingleAsync(x => x.OperationalProjectId == context.ProjectId && x.UserId == context.ReceiverId);
            member.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });
        (await Client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync(downloadPath)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        (await Client.PostAsJsonAsync($"/api/ifc-releases/{releaseId}/recipients/{recipientId}/acknowledge", new { }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        File.Delete(filePath);
    }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/ifc-releases?designProjectId=1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/ifc-releases?designProjectId=1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, _, _) = await CreateShopStageProjectWithApprovedDrawingAsync();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        (await Client.GetAsync($"/api/ifc-releases?designProjectId={projectId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DisciplineScopedMember_CannotReadProjectWideIfcGate()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, _, _) = await CreateShopStageProjectWithApprovedDrawingAsync();
        var release = await Client.PostAsJsonAsync("/api/ifc-releases", new
        {
            designProjectId = projectId,
            title = "Restricted IFC gate",
        });
        release.EnsureSuccessStatusCode();
        var releaseId = (await ReadJsonAsync(release)).GetProperty("id").GetInt32();
        await WithDbAsync(async db =>
        {
            var userId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["DESIGN"])
                .Select(user => user.Id)
                .SingleAsync();
            var superAdminId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.SuperAdminPhone)
                .Select(user => user.Id)
                .SingleAsync();
            var operationalProject = await db.DesignProjects
                .Where(project => project.Id == projectId)
                .Select(project => project.OperationalProject!)
                .SingleAsync();
            operationalProject.ProjectManagerUserId = superAdminId;
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = operationalProject.Id,
                UserId = userId,
                Position = "Architect",
                StartedAt = DateTime.UtcNow.AddDays(-1),
                CreatedByUserId = superAdminId,
                UpdatedByUserId = superAdminId,
                Roles =
                [
                    new OperationalProjectMemberRole
                    {
                        RoleCode = ProjectTeamRoleCode.Architect,
                        Scope = ProjectRoleScope.Discipline,
                        ScopeValue = "architecture",
                        StartedAt = DateTime.UtcNow.AddDays(-1),
                    },
                ],
            });
            await db.SaveChangesAsync();
        });

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));

        (await Client.GetAsync($"/api/ifc-releases?designProjectId={projectId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/ifc-releases/{releaseId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
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
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var proj = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Concept-stage {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
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
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    [Fact]
    public async Task DeleteItemsRecipientsAndRoot_AfterRelease_AreRejected()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, approvedId, _) = await CreateShopStageProjectWithApprovedDrawingAsync();
        var releaseId = await CreateFullDraftAsync(projectId, approvedId);
        var full = await ReadJsonAsync(await Client.GetAsync($"/api/ifc-releases/{releaseId}"));
        var itemId = full.GetProperty("items")[0].GetProperty("id").GetInt32();
        var recipientId = full.GetProperty("recipients")[0].GetProperty("id").GetInt32();
        (await Client.PostAsync($"/api/ifc-releases/{releaseId}/release", null)).EnsureSuccessStatusCode();

        (await Client.DeleteAsync($"/api/ifc-releases/{releaseId}/items/{itemId}"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.DeleteAsync($"/api/ifc-releases/{releaseId}/recipients/{recipientId}"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DeleteWithImpactAsync($"/api/ifc-releases/{releaseId}"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var preserved = await ReadJsonAsync(await Client.GetAsync($"/api/ifc-releases/{releaseId}"));
        preserved.GetProperty("status").GetString().Should().Be("Released");
        preserved.GetProperty("items").GetArrayLength().Should().Be(1);
        preserved.GetProperty("recipients").GetArrayLength().Should().Be(1);
    }

    // -------- helpers --------

    private async Task<(int ProjectId, int ApprovedShopId, int DraftingShopId)> CreateShopStageProjectWithApprovedDrawingAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var proj = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"IFC fixture {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
        });
        proj.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(proj)).GetProperty("id").GetInt32();

        await WithDbAsync(async db =>
        {
            var project = await db.DesignProjects.FindAsync(projectId);
            project!.CurrentStage = DesignProjectStage.ShopDrawing;
            await db.SaveChangesAsync();
        });

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

    private async Task<int> CreateOperationalProjectAsync(int customerId)
    {
        return await WithDbAsync<int>(async db =>
        {
            var designUserId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["DESIGN"])
                .Select(user => user.Id)
                .SingleAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-TEST-{Guid.NewGuid():N}",
                Name = "IFC release integration fixture",
                CustomerId = customerId,
                ProjectManagerUserId = designUserId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
    }

    private async Task<int> CreateFullDraftAsync(int projectId, int approvedShopId)
    {
        await CreateClientApprovedDesignReviewAsync(
            projectId, DesignReviewStage.ShopDrawing, approvedShopId);
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
