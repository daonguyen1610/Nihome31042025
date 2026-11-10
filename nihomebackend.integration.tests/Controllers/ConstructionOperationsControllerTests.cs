using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class ConstructionOperationsControllerTests : IntegrationTestBase
{
    public ConstructionOperationsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ListResources_WithoutAuthentication_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/operational-projects/1/construction/resource-requests"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResourceReferences_ProjectQsCanReadWithoutProcurementPermission()
    {
        var fixture = await CreateProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "QS"));

        using var response = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-references");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ReadJsonAsync(response);
        payload.TryGetProperty("boqLines", out _).Should().BeTrue();
        payload.TryGetProperty("siteUsers", out _).Should().BeTrue();
        payload.TryGetProperty("procurementUsers", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetPreparation_WhenNotCreated_ReturnsNoContent()
    {
        var fixture = await CreateProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));

        using var response = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/construction/preparation");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResourceRequest_CreateReplaySubmitAndCmApproval_PreservesSingleRecord()
    {
        var fixture = await CreateProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "QS"));
        var key = Guid.NewGuid().ToString("N");
        var payload = ValidEquipmentRequest();

        using var first = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests",
            payload,
            key);
        using var replay = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests",
            payload,
            key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(first);
        var replayed = await ReadJsonAsync(replay);
        replayed.GetProperty("id").GetInt32().Should().Be(created.GetProperty("id").GetInt32());

        var submitted = await TransitionAsync(fixture.ProjectId, created, "submit");
        submitted.GetProperty("status").GetString().Should().Be("PendingCmReview");

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        using var approvedResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests/{created.GetProperty("id").GetInt32()}/cm-decision",
            new
            {
                approved = true,
                reason = "Resource plan and construction schedule verified",
                rowVersion = submitted.GetProperty("rowVersion").GetString(),
            });
        approvedResponse.EnsureSuccessStatusCode();
        var approved = await ReadJsonAsync(approvedResponse);
        approved.GetProperty("status").GetString().Should().Be("CmApproved");
        approved.GetProperty("handoffStatus").GetString().Should().Be("ReadyForProcurementAndFinance");
        (await WithDbAsync(db => db.ConstructionResourceRequests.CountAsync(x => x.OperationalProjectId == fixture.ProjectId)))
            .Should().Be(1);
    }

    [Fact]
    public async Task CmDecision_WhenReviewerIsAuthor_ReturnsBadRequestAndPreservesPendingState()
    {
        var fixture = await CreateProjectAsync();
        var requestId = await WithDbAsync(async db =>
        {
            var row = new ConstructionResourceRequest
            {
                OperationalProjectId = fixture.ProjectId,
                Code = "RR-SOD-001",
                Type = ConstructionResourceType.Equipment,
                WbsCode = "WBS-01",
                NeedByDate = DateTime.UtcNow.AddDays(10),
                Quantity = 1,
                Unit = "set",
                Reason = "Tower crane for structural works",
                Status = ConstructionReviewStatus.PendingCmReview,
                CreatedByUserId = fixture.PmUserId,
            };
            db.ConstructionResourceRequests.Add(row);
            await db.SaveChangesAsync();
            return row.Id;
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        var rowVersion = await WithDbAsync(db => db.ConstructionResourceRequests
            .Where(x => x.Id == requestId)
            .Select(x => Convert.ToBase64String(x.RowVersion))
            .SingleAsync());

        using var response = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests/{requestId}/cm-decision",
            new { approved = true, reason = "Self approval is forbidden", rowVersion });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ConstructionResourceRequests
            .Where(x => x.Id == requestId)
            .Select(x => x.Status)
            .SingleAsync())).Should().Be(ConstructionReviewStatus.PendingCmReview);
    }

    [Fact]
    public async Task CmDecision_WhenReviewerEditedAndSubmittedDraft_ReturnsBadRequest()
    {
        var fixture = await CreateProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "QS"));
        using var createdResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests",
            ValidEquipmentRequest());
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createdResponse);

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        using var updateResponse = await SendAsync(
            HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests/{created.GetProperty("id").GetInt32()}",
            new
            {
                type = "Equipment",
                wbsCode = "WBS-STR-01",
                needByDate = DateTime.UtcNow.AddDays(21),
                quantity = 3m,
                unit = "set",
                reason = "CM corrected the resource quantity before submission",
                rowVersion = created.GetProperty("rowVersion").GetString(),
            });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ReadJsonAsync(updateResponse);
        var submitted = await TransitionAsync(fixture.ProjectId, updated, "submit");

        using var decisionResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests/{created.GetProperty("id").GetInt32()}/cm-decision",
            new
            {
                approved = true,
                reason = "The same CM reviewer must not approve their own correction",
                rowVersion = submitted.GetProperty("rowVersion").GetString(),
            });

        decisionResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ConstructionResourceRequests
            .Where(x => x.Id == created.GetProperty("id").GetInt32())
            .Select(x => x.Status)
            .SingleAsync())).Should().Be(ConstructionReviewStatus.PendingCmReview);
    }

    [Fact]
    public async Task DeleteResource_RequiresCurrentImpactAndTypedCode()
    {
        var fixture = await CreateProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "QS"));
        using var createdResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests",
            ValidEquipmentRequest());
        var created = await ReadJsonAsync(createdResponse);
        var requestId = created.GetProperty("id").GetInt32();
        var path = $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests/{requestId}";

        using var missingConfirmation = await SendAsync(HttpMethod.Delete, path, new { });
        missingConfirmation.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ConstructionResourceRequests.AnyAsync(x => x.Id == requestId)))
            .Should().BeTrue();

        using var impactResponse = await Client.GetAsync($"{path}/deletion-impact");
        impactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var impact = await ReadJsonAsync(impactResponse);
        using var confirmedDelete = await SendAsync(HttpMethod.Delete, path, new
        {
            planToken = impact.GetProperty("planToken").GetString(),
            confirmation = impact.GetProperty("requiredConfirmation").GetString(),
            rowVersion = created.GetProperty("rowVersion").GetString(),
        });

        confirmedDelete.StatusCode.Should().Be(HttpStatusCode.OK);
        (await WithDbAsync(db => db.ConstructionResourceRequests.AnyAsync(x => x.Id == requestId)))
            .Should().BeFalse();
    }

    [Fact]
    public async Task DeleteResource_WhenCmReviewerIsNotAuthor_ReturnsBadRequestAndPreservesDraft()
    {
        var fixture = await CreateProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "QS"));
        using var createdResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests",
            ValidEquipmentRequest());
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createdResponse);
        var requestId = created.GetProperty("id").GetInt32();

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        using var impactResponse = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests/{requestId}/deletion-impact");

        impactResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ConstructionResourceRequests.AnyAsync(x => x.Id == requestId)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task UpdateQs_WithoutConcurrencyToken_ReturnsBadRequest()
    {
        var fixture = await CreateProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "QS"));

        using var response = await SendAsync(
            HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/construction/qs-batches/999",
            new
            {
                direction = "Downstream",
                contractId = 1,
                wbsCode = "WBS-01",
                periodFrom = DateTime.UtcNow.Date,
                periodTo = DateTime.UtcNow.Date,
                certifiedAmount = 1m,
                description = "Progress certification",
                lines = new[] { new { itemCode = "QS-01", description = "Concrete", unit = "m3", quantity = 1m, unitPrice = 1m } },
                evidenceDocumentIds = Array.Empty<long>(),
            });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task QaQcDossier_TypedEvidenceRoundTripsAndMixedPayloadIsRejected()
    {
        var fixture = await CreateProjectAsync();
        var references = await WithDbAsync(async db =>
        {
            var template = new QaQcTemplate
            {
                Code = "ISO-QA-" + Guid.NewGuid().ToString("N")[..8],
                Version = 1,
                NameVi = "Hồ sơ chất lượng vật liệu",
                Name = "Material quality dossier",
                NameZh = "材料质量档案",
                NameJa = "材料品質記録",
                SchemaJson = "{}",
                IsPublished = true,
                CreatedByUserId = fixture.PmUserId,
            };
            var design = new DesignProject
            {
                OperationalProjectId = fixture.ProjectId,
                ProjectCode = "DP-QA-" + Guid.NewGuid().ToString("N")[..8],
                Name = "NICON QA/QC integration project",
            };
            db.QaQcTemplates.Add(template);
            db.DesignProjects.Add(design);
            await db.SaveChangesAsync();
            var release = new IfcRelease
            {
                DesignProjectId = design.Id,
                ReleaseNumber = "IFC-QA-01",
                Title = "Foundation inspection drawings",
                Status = IfcReleaseStatus.Released,
            };
            var document = new ProjectDocument
            {
                OperationalProjectId = fixture.ProjectId,
                Category = ProjectDocumentCategory.ConstructionAcceptance,
                LocalPath = "/files/project-documents/qa-lab-result.pdf",
                OriginalFileName = "qa-lab-result.pdf",
                Sha256 = new string('b', 64),
            };
            db.IfcReleases.Add(release);
            db.ProjectDocuments.Add(document);
            await db.SaveChangesAsync();
            return (TemplateId: template.Id, ReleaseId: release.Id, DocumentId: document.Id);
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var path = $"/api/operational-projects/{fixture.ProjectId}/construction/qa-qc/dossiers";
        using var createdResponse = await SendAsync(HttpMethod.Post, path, new
        {
            templateId = references.TemplateId,
            wbsCode = "1.2.1",
            title = "Foundation concrete laboratory test",
            ifcReleaseId = references.ReleaseId,
            checklistResultsJson = "[]",
            evidence = new[] { new { projectDocumentId = references.DocumentId, evidenceType = "LaboratoryResult" } },
        });
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createdResponse);
        created.GetProperty("evidence")[0].GetProperty("evidenceType").GetString()
            .Should().Be("LaboratoryResult");
        created.GetProperty("evidenceDocumentIds")[0].GetInt64().Should().Be(references.DocumentId);

        using var readResponse = await Client.GetAsync($"{path}/{created.GetProperty("id").GetInt32()}");
        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(readResponse)).GetProperty("evidence")[0]
            .GetProperty("evidenceType").GetString().Should().Be("LaboratoryResult");

        using var invalidResponse = await SendAsync(HttpMethod.Post, path, new
        {
            templateId = references.TemplateId,
            wbsCode = "1.2.2",
            title = "Invalid mixed evidence",
            ifcReleaseId = references.ReleaseId,
            checklistResultsJson = "[]",
            evidenceDocumentIds = new[] { references.DocumentId },
            evidence = new[] { new { projectDocumentId = references.DocumentId, evidenceType = "Sample" } },
        });
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.QaQcDossiers.CountAsync(x => x.OperationalProjectId == fixture.ProjectId)))
            .Should().Be(1);
    }

    private async Task<(int ProjectId, int PmUserId)> CreateProjectAsync() =>
        await WithDbAsync(async db =>
        {
            var pm = await db.Users.SingleAsync(x => x.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"]);
            var qs = await db.Users.SingleAsync(x => x.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["QS"]);
            var customer = new Customer
            {
                Name = "Module 4 test customer " + Guid.NewGuid().ToString("N")[..8],
                SourceCode = "referral",
                RelationshipStatus = CustomerRelationshipStatus.InProgress,
                Type = CustomerType.Company,
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-M4-{Guid.NewGuid():N}"[..30],
                Name = "NICON construction operations test",
                CustomerId = customer.Id,
                ProjectManagerUserId = pm.Id,
                CreatedByUserId = pm.Id,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = project.Id,
                UserId = qs.Id,
                Position = "Site QS",
                StartedAt = DateTime.UtcNow,
                CreatedByUserId = pm.Id,
                UpdatedByUserId = pm.Id,
            });
            await db.SaveChangesAsync();
            return (project.Id, pm.Id);
        });

    private static object ValidEquipmentRequest() => new
    {
        type = "Equipment",
        wbsCode = "WBS-STR-01",
        needByDate = DateTime.UtcNow.AddDays(14),
        quantity = 2m,
        unit = "set",
        reason = "Tower cranes required by the approved structural resource plan",
    };

    private async Task<System.Text.Json.JsonElement> TransitionAsync(
        int projectId,
        System.Text.Json.JsonElement row,
        string action)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{projectId}/construction/resource-requests/{row.GetProperty("id").GetInt32()}/{action}",
            new { rowVersion = row.GetProperty("rowVersion").GetString() });
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object payload, string? key = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(payload) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        return await Client.SendAsync(request);
    }
}
