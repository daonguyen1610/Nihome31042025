using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class DesignPermitIfcPipelineTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concept_ToPermitTracking_AndIfcReceipt_PreservesGatesAndRevisionHistory(bool needsCorrection)
    {
        // Customer/project/team are foundation data. Contract execution creates
        // the design workspace; no drawing, approval or release is seeded.
        // Permit tracking shares the project; automatic Basic-document transfer
        // is not implemented.
        var fixture = await SeedFoundationAsync();
        await LoginAsync("SALES_MANAGER");
        var contract = await PostAsync("/api/contracts", new
        {
            customerId = fixture.CustomerId,
            operationalProjectId = fixture.OperationalProjectId,
            direction = "Upstream",
            type = "DesignAndBuild",
            value = 850000000m,
            scopeOfWork = "Factory design, permits and construction delivery",
        });
        await UploadAsync($"/api/contracts/{Id(contract)}/attachments", "Signed factory design and build contract", "SignedScan");
        foreach (var status in new[] { "Signed", "InProgress" })
            contract = await PostAsync($"/api/contracts/{Id(contract)}/transition", new
            {
                newStatus = status,
                rowVersion = contract.GetProperty("rowVersion").GetString(),
            });
        var contractId = Id(contract);
        var projectId = await WithDbAsync(db => db.DesignProjects.Where(p => p.ContractId == contractId).Select(p => p.Id).SingleAsync());
        await LoginAsync("PM");
        var checklist = await GetAsync($"/api/permits?designProjectId={projectId}&pageSize=100");
        checklist.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        var permitId = Id(checklist.GetProperty("items")[0]);
        var permitCount = checklist.GetProperty("items").GetArrayLength();
        (await PostAsync($"/api/permits/design-project/{projectId}/ensure"))
            .GetProperty("items").GetArrayLength().Should().Be(permitCount);

        await LoginAsync("DESIGN");
        await RejectAsync("/api/basic-design-docs", new { designProjectId = projectId, disciplineCode = "architecture", title = "Premature basic drawing" }, HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.BasicDesignDocs.CountAsync(d => d.DesignProjectId == projectId))).Should().Be(0);
        var concept = await PostAsync("/api/concept-options", new { designProjectId = projectId, name = "Factory layout with separated loading access" });
        var alternative = await PostAsync("/api/concept-options", new { designProjectId = projectId, name = "Alternative central loading access" });
        var conceptPath = $"/api/concept-options/{Id(concept)}";
        await StatusAsync(conceptPath, "PendingInternalReview");
        await StatusAsync(conceptPath, "PresentedToClient");
        if (needsCorrection)
        {
            await StatusAsync(conceptPath, "ClientRequestedChanges");
            await StatusAsync(conceptPath, "Drafting");
            var response = await Client.PutAsJsonAsync(conceptPath, new { name = "Factory layout with revised fire access", description = "Client requested a separate emergency entrance." });
            response.EnsureSuccessStatusCode();
            await StatusAsync(conceptPath, "PendingInternalReview");
            await StatusAsync(conceptPath, "PresentedToClient");
        }
        await RejectAsync(conceptPath + "/status", new { status = "Finalized" }, HttpStatusCode.NotFound);
        (await GetAsync(conceptPath)).GetProperty("status").GetString().Should().Be("PresentedToClient");
        await AssertStageAsync(projectId, DesignProjectStage.Concept);
        await LoginAsync("DESIGN_LEAD");
        await StatusAsync(conceptPath, "Finalized");
        await AssertStageAsync(projectId, DesignProjectStage.BasicDesign);
        (await GetAsync($"/api/concept-options/{Id(alternative)}")).GetProperty("status").GetString().Should().Be("Discarded");

        var basicIds = new List<int>();
        foreach (var discipline in new[] { "architecture", "structure", "mep" })
        {
            await LoginAsync("DESIGN");
            var basic = await PostAsync("/api/basic-design-docs", new { designProjectId = projectId, disciplineCode = discipline, title = $"Factory permit {discipline}" });
            var basicId = Id(basic);
            basicIds.Add(basicId);
            var path = $"/api/basic-design-docs/{basicId}";
            await UploadAsync(path + "/upload", $"Approved basis: {discipline}");
            await StatusAsync(path, "SubmittedForReview");
            await RejectAsync(path + "/status", new { status = "InternallyApproved" }, HttpStatusCode.NotFound);
            (await WithDbAsync(db => db.BasicDesignDocs.SingleAsync(d => d.Id == basicId))).Status.Should().Be(BasicDesignDocStatus.SubmittedForReview);
            await LoginAsync("DESIGN_LEAD");
            if (needsCorrection && discipline == "architecture")
            {
                await StatusAsync(path, "InProgress");
                await LoginAsync("DESIGN");
                await PostAsync("/api/drawing-revisions", new { targetType = "BasicDesignDoc", targetId = basicId, reasonCode = "client-request", note = "Revise emergency entrance clearance for permit submission." });
                await StatusAsync(path, "SubmittedForReview");
                await LoginAsync("DESIGN_LEAD");
            }
            await StatusAsync(path, "InternallyApproved");
            if (discipline == "architecture")
            {
                await RejectAsync($"/api/basic-design-docs/design-project/{projectId}/unlock-shop-drawing", null, HttpStatusCode.BadRequest);
                await AssertStageAsync(projectId, DesignProjectStage.BasicDesign);
            }
            await StatusAsync(path, "SubmittedForPermit");
        }

        // Legal/PM upload is explicit: there is currently no automatic transfer
        // of the approved Basic file or a FK from permit to Basic document.
        await LoginAsync("PM");
        await UploadAsync($"/api/permits/{permitId}/documents/SubmittedPackage", "Factory permit submission package");
        await PatchPermitAsync(permitId, "Submitted");
        if (needsCorrection)
        {
            await PatchPermitAsync(permitId, "NeedMoreDocs");
            await UploadAsync($"/api/permits/{permitId}/documents/SubmittedPackage", "Corrected factory permit submission package");
            await PatchPermitAsync(permitId, "Submitted");
        }
        await PatchPermitAsync(permitId, "UnderReview");
        await UploadAsync($"/api/permits/{permitId}/documents/IssuedPermit", "Issued factory permit scan");
        await PatchPermitAsync(permitId, "Issued");
        var permit = await GetAsync($"/api/permits/{permitId}");
        permit.GetProperty("designProjectId").GetInt32().Should().Be(projectId);
        var fileName = Path.GetFileName(permit.GetProperty("issuedFilePath").GetString()!);
        var content = await Client.GetAsync($"/api/permits/{permitId}/documents/{fileName}/content");
        content.EnsureSuccessStatusCode();
        (await content.Content.ReadAsStringAsync()).Should().Be("Issued factory permit scan");
        await LoginAsync("SALE");
        (await Client.PatchAsJsonAsync($"/api/permits/{permitId}", new { status = "Rejected" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.PermitChecklistItems.SingleAsync(p => p.Id == permitId))).Status.Should().Be(PermitStatus.Issued);

        await LoginAsync("DESIGN_LEAD");
        foreach (var basicId in basicIds) await StatusAsync($"/api/basic-design-docs/{basicId}", "PermitApproved");
        await PostAsync($"/api/basic-design-docs/design-project/{projectId}/unlock-shop-drawing");
        await AssertStageAsync(projectId, DesignProjectStage.ShopDrawing);
        var release = await PostAsync("/api/ifc-releases", new { designProjectId = projectId, title = "Factory construction issue 01" });
        var releasePath = $"/api/ifc-releases/{Id(release)}";
        await RejectAsync(releasePath + "/release", null, HttpStatusCode.BadRequest);
        (await GetAsync(releasePath)).GetProperty("status").GetString().Should().Be("Draft");

        await LoginAsync("DESIGN");
        var shop = await PostAsync("/api/shop-drawings", new { designProjectId = projectId, disciplineCode = "architecture", constructionItem = "Factory emergency entrance", title = "Entrance reinforcement detail" });
        var shopId = Id(shop);
        var shopPath = $"/api/shop-drawings/{shopId}";
        await UploadAsync(shopPath + "/upload", "Factory construction detail PDF");
        var firstRevision = await RevisionAsync(shopId, "Initial coordinated construction detail.");
        await RejectAsync("/api/drawing-revisions", new { targetType = "ShopDrawing", targetId = shopId, reasonCode = "client-request", note = " " }, HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DrawingRevisions.CountAsync(r => r.TargetType == DrawingRevisionTargetType.ShopDrawing && r.TargetId == shopId))).Should().Be(1);
        await StatusAsync(shopPath, "InReview");
        await LoginAsync("DESIGN_LEAD");
        await RejectAsync(releasePath + "/items", new { shopDrawingIds = new[] { shopId } }, HttpStatusCode.BadRequest);
        (await GetAsync(releasePath)).GetProperty("items").GetArrayLength().Should().Be(0);
        if (needsCorrection)
        {
            await StatusAsync(shopPath, "Drafting");
            await LoginAsync("DESIGN");
            var revision = await RevisionAsync(shopId, "Resolve MEP clearance conflict before IFC issue.");
            revision.GetProperty("revisionNumber").GetInt32().Should().Be(2);
            (await GetAsync($"/api/drawing-revisions/{Id(firstRevision)}")).GetProperty("isCurrent").GetBoolean().Should().BeFalse();
            await StatusAsync(shopPath, "InReview");
            await LoginAsync("DESIGN_LEAD");
        }
        await StatusAsync(shopPath, "Approved");
        await RejectAsync(shopPath + "/status", new { status = "Released" }, HttpStatusCode.BadRequest);
        (await GetAsync(shopPath)).GetProperty("status").GetString().Should().Be("Approved");
        await PostAsync(releasePath + "/items", new { shopDrawingIds = new[] { shopId } });
        await RejectAsync(releasePath + "/release", null, HttpStatusCode.BadRequest);
        release = await PostAsync(releasePath + "/recipients", new { name = "Factory site construction team", recipientTypeCode = "main-contractor" });
        var recipientId = Id(release.GetProperty("recipients")[0]);
        await RejectAsync(releasePath + $"/recipients/{recipientId}/acknowledge", new { acknowledgementNote = "Too early" }, HttpStatusCode.BadRequest);
        (await GetAsync(releasePath)).GetProperty("recipients")[0].GetProperty("isAcknowledged").GetBoolean().Should().BeFalse();
        await LoginAsync("DESIGN");
        await RejectAsync(releasePath + "/release", null, HttpStatusCode.Forbidden);
        (await GetAsync(releasePath)).GetProperty("status").GetString().Should().Be("Draft");
        await LoginAsync("DESIGN_LEAD");
        release = await PostAsync(releasePath + "/release");
        release.GetProperty("status").GetString().Should().Be("Released");
        release.GetProperty("issuedByUserId").GetInt32().Should().Be(fixture.LeadId);
        var releaseDate = release.GetProperty("releaseDate").GetString();
        await RejectAsync(releasePath + "/release", null, HttpStatusCode.BadRequest);
        (await GetAsync(releasePath)).GetProperty("releaseDate").GetString().Should().Be(releaseDate);
        await LoginAsync("PM");
        release = await PostAsync(releasePath + $"/recipients/{recipientId}/acknowledge", new { acknowledgementNote = "Site team confirms construction issue 01 received." });
        release.GetProperty("recipients")[0].GetProperty("isAcknowledged").GetBoolean().Should().BeTrue();
        (await GetAsync(shopPath)).GetProperty("status").GetString().Should().Be("Released");
        await WithDbAsync(async db =>
        {
            (await db.ShopDrawings.SingleAsync(d => d.Id == shopId)).Status.Should().Be(ShopDrawingStatus.Released);
            var revisions = await db.DrawingRevisions.Where(r => r.TargetType == DrawingRevisionTargetType.ShopDrawing && r.TargetId == shopId).ToListAsync();
            revisions.Should().HaveCount(needsCorrection ? 2 : 1);
            revisions.Should().ContainSingle(r => r.IsCurrent);
            (await db.DesignProjects.SingleAsync(p => p.Id == projectId)).OperationalProjectId.Should().Be(fixture.OperationalProjectId);
        });
    }

    private Task LoginAsync(string role) => AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, role));
    private static int Id(JsonElement value) => value.GetProperty("id").GetInt32();
    private async Task<JsonElement> GetAsync(string path)
    {
        var response = await Client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }
    private async Task<JsonElement> PostAsync(string path, object? body = null)
    {
        var response = body is null ? await Client.PostAsync(path, null) : await Client.PostAsJsonAsync(path, body);
        response.IsSuccessStatusCode.Should().BeTrue($"POST {path}: {await response.Content.ReadAsStringAsync()}");
        return await ReadJsonAsync(response);
    }
    private async Task RejectAsync(string path, object? body, HttpStatusCode expected)
    {
        var response = body is null ? await Client.PostAsync(path, null) : await Client.PostAsJsonAsync(path, body);
        response.StatusCode.Should().Be(expected, $"POST {path}: {await response.Content.ReadAsStringAsync()}");
    }
    private Task<JsonElement> StatusAsync(string path, string status) => PostAsync(path + "/status", new { status });
    private Task<JsonElement> RevisionAsync(int shopId, string note) => PostAsync("/api/drawing-revisions", new { targetType = "ShopDrawing", targetId = shopId, reasonCode = "client-request", note });
    private async Task AssertStageAsync(int projectId, DesignProjectStage stage) =>
        (await WithDbAsync(db => db.DesignProjects.SingleAsync(p => p.Id == projectId))).CurrentStage.Should().Be(stage);
    private async Task UploadAsync(string path, string contents, string? kind = null)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(contents));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "factory-design.pdf");
        if (kind is not null) form.Add(new StringContent(kind), "kind");
        (await Client.PostAsync(path, form)).EnsureSuccessStatusCode();
    }
    private async Task PatchPermitAsync(int id, string status)
    {
        var response = await Client.PatchAsJsonAsync($"/api/permits/{id}", new { status });
        response.EnsureSuccessStatusCode();
        (await ReadJsonAsync(response)).GetProperty("status").GetString().Should().Be(status);
    }
    private async Task<(int CustomerId, int OperationalProjectId, int LeadId)> SeedFoundationAsync() =>
        await WithDbAsync(async db =>
        {
            var roleIds = new Dictionary<string, int>();
            foreach (var role in new[] { "PM", "DESIGN", "DESIGN_LEAD" })
                roleIds[role] = await db.Users.Where(u => u.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode[role]).Select(u => u.Id).SingleAsync();
            var customer = new Customer { Name = UniqueSlug("Factory design customer"), SourceCode = "referral", Type = CustomerType.Company, RelationshipStatus = CustomerRelationshipStatus.Signed, TaxId = "0312345678", Address = "15 Industrial Park Road", RepresentativeName = "Nguyen Minh An", Contacts = [new() { FullName = "Nguyen Minh An", Phone = "0912345678", Email = "an@example.com", IsPrimary = true, IsLegalRepresentative = true }] };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var operational = new OperationalProject { Code = UniqueSlug("PJ-DESIGN-PIPE"), Name = "Factory permit and construction design", CustomerId = customer.Id, ProjectManagerUserId = roleIds["PM"] };
            db.OperationalProjects.Add(operational);
            await db.SaveChangesAsync();
            foreach (var member in new[] { ("PM", ProjectTeamRoleCode.ProjectManager), ("DESIGN_LEAD", ProjectTeamRoleCode.DesignLead), ("DESIGN", ProjectTeamRoleCode.Architect) })
                db.OperationalProjectMembers.Add(new OperationalProjectMember
                {
                    OperationalProjectId = operational.Id,
                    UserId = roleIds[member.Item1],
                    Position = member.Item1,
                    StartedAt = DateTime.UtcNow.AddDays(-1),
                    CreatedByUserId = roleIds["PM"],
                    UpdatedByUserId = roleIds["PM"],
                    Roles = [new() { RoleCode = member.Item2, Scope = ProjectRoleScope.Project, StartedAt = DateTime.UtcNow.AddDays(-1) }],
                });
            await db.SaveChangesAsync();
            return (customer.Id, operational.Id, roleIds["DESIGN_LEAD"]);
        });
}
