using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Models;
using NihomeBackend.Services;
using NihomeBackend.Services.GoogleDrive;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>SurveysController</c> (NIH-99): RBAC gating,
/// list + get, filters (search, construction type, date range, drive sync).
/// Create is exercised so subsequent slices (NIH-100/101) have a working
/// baseline; write-side tests (update / delete / RBAC-manage) will land
/// with those slices.
/// </summary>
public class SurveysControllerTests : IntegrationTestBase
{
    public SurveysControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/surveys")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsWarehouse_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/surveys")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsSalesManager_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.GetAsync("/api/surveys");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task Sales_ScopeHidesAnotherProjectAcrossSurveyEndpoints_UntilAssignedAsSurveyor()
    {
        var context = await WithDbAsync(async db =>
        {
            var salesUser = await db.Users.SingleAsync(user =>
                user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["SALE"]);
            var projectManager = await db.Users.SingleAsync(user =>
                user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"]);
            var customer = new Customer
            {
                Name = $"Scoped survey customer {Guid.NewGuid():N}",
                Type = CustomerType.Company,
                OwnerUserId = projectManager.Id,
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new OperationalProject
            {
                Code = $"OP-{Guid.NewGuid():N}",
                Name = "Private customer project",
                CustomerId = customer.Id,
                ProjectManagerUserId = projectManager.Id,
                CreatedByUserId = projectManager.Id,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            var survey = new Survey
            {
                Code = $"SV-SCOPE-{Guid.NewGuid():N}"[..30],
                Location = "Private customer site",
                SurveyDate = DateTime.UtcNow.AddDays(-1),
                OperationalProjectId = project.Id,
                CreatedByUserId = projectManager.Id,
            };
            db.Surveys.Add(survey);
            await db.SaveChangesAsync();
            return (SurveyId: survey.Id, ProjectId: project.Id, SalesUserId: salesUser.Id);
        });

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));

        var list = await ReadJsonAsync(await Client.GetAsync("/api/surveys?pageSize=100"));
        list.GetProperty("items").EnumerateArray().Should()
            .NotContain(item => item.GetProperty("id").GetInt32() == context.SurveyId);
        (await Client.GetAsync($"/api/surveys/{context.SurveyId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/surveys/{context.SurveyId}/timeline")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/surveys/{context.SurveyId}/export.pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var conditionResponse = await Client.PutAsJsonAsync($"/api/surveys/{context.SurveyId}/conditions", new
        {
            conditions = new[]
            {
                new { category = "RightOfWay", code = "access-width", statusCode = "Unknown", unitCode = "m" },
            },
        });
        conditionResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using (var csv = new MultipartFormDataContent())
        {
            csv.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(
                "Category,Code,Status,Value,Unit,InfrastructureTypeCode,Note\nRightOfWay,access-width,Unknown,,m,,")),
                "file", "survey-conditions.csv");
            (await Client.PostAsync($"/api/surveys/{context.SurveyId}/conditions/import", csv))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        using (var media = new MultipartFormDataContent())
        {
            media.Add(new ByteArrayContent([1, 2, 3]), "file", "private.jpg");
            (await Client.PostAsync($"/api/surveys/{context.SurveyId}/media", media))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        (await Client.GetAsync($"/api/surveys/{context.SurveyId}/media/999/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.DeleteAsync($"/api/surveys/{context.SurveyId}/media/999"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PostAsync($"/api/surveys/{context.SurveyId}/media/999/retry-sync", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PutAsJsonAsync($"/api/surveys/{context.SurveyId}/checklist/999", new
        {
            status = "NeedsAttention",
            note = "Must remain private",
        })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/surveys/{context.SurveyId}/sync-log"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var updateResponse = await Client.PutAsJsonAsync($"/api/surveys/{context.SurveyId}", new
        {
            location = "Must remain private",
            surveyDate = DateTime.UtcNow,
            operationalProjectId = context.ProjectId,
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DeleteSurveyAsync(context.SurveyId, new string('a', 64), "HIDDEN"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await WithDbAsync(async db =>
        {
            var survey = await db.Surveys.SingleAsync(item => item.Id == context.SurveyId);
            survey.SurveyorUserId = context.SalesUserId;
            await db.SaveChangesAsync();
        });

        (await Client.GetAsync($"/api/surveys/{context.SurveyId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var assignedUpdate = await Client.PutAsJsonAsync($"/api/surveys/{context.SurveyId}", new
        {
            location = "Assigned surveyor update",
            surveyDate = DateTime.UtcNow.AddDays(-1),
            surveyorUserId = context.SalesUserId,
            operationalProjectId = context.ProjectId,
        });
        assignedUpdate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(assignedUpdate)).GetProperty("location").GetString()
            .Should().Be("Assigned surveyor update");
    }

    [Fact]
    public async Task Create_HappyPath_ReturnsAutoCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var operationalProjectId = await CreateOperationalProjectAsync();
        var res = await Client.PostAsJsonAsync("/api/surveys", new
        {
            location = "Lô A5, KCN Bắc Ninh " + Guid.NewGuid().ToString("N")[..4],
            constructionTypeCode = "industrial",
            surveyDate = DateTime.UtcNow.AddDays(-1),
            operationalProjectId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("code").GetString().Should().StartWith("SV-");
        body.GetProperty("driveSyncStatus").GetString().Should().Be("NotSynced");
        body.GetProperty("operationalProjectId").GetInt32().Should().Be(operationalProjectId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task Create_WithoutValidOperationalProject_IsBadRequest(int? operationalProjectId)
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.PostAsJsonAsync("/api/surveys", new
        {
            location = "Invalid project routing",
            constructionTypeCode = "industrial",
            surveyDate = DateTime.UtcNow.AddDays(-1),
            operationalProjectId,
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithMismatchedOpportunityProject_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var context = await WithDbAsync(async db =>
        {
            var customer = new Customer { Name = $"Survey mismatch {Guid.NewGuid():N}", Type = CustomerType.Company };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var opportunityProject = new OperationalProject
            {
                Code = $"OP-{Guid.NewGuid():N}",
                Name = "Opportunity project",
                CustomerId = customer.Id,
            };
            var suppliedProject = new OperationalProject
            {
                Code = $"OP-{Guid.NewGuid():N}",
                Name = "Supplied project",
                CustomerId = customer.Id,
            };
            db.OperationalProjects.AddRange(opportunityProject, suppliedProject);
            await db.SaveChangesAsync();
            var opportunity = new Opportunity
            {
                Name = "Survey mismatch opportunity",
                CustomerId = customer.Id,
                OperationalProjectId = opportunityProject.Id,
            };
            db.Opportunities.Add(opportunity);
            await db.SaveChangesAsync();
            return (OpportunityId: opportunity.Id, ProjectId: suppliedProject.Id);
        });

        var response = await Client.PostAsJsonAsync("/api/surveys", new
        {
            location = "Mismatched routing",
            surveyDate = DateTime.UtcNow,
            linkedOpportunityId = context.OpportunityId,
            operationalProjectId = context.ProjectId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString().Should().Contain("không khớp");
    }

    [Fact]
    public async Task Create_WithUnknownConstructionType_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.PostAsJsonAsync("/api/surveys", new
        {
            location = "Bad type",
            constructionTypeCode = "definitely-not-real",
            surveyDate = DateTime.UtcNow,
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Warehouse_CannotCreate()
    {
        // WAREHOUSE has no crm.surveys.* permissions at all.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        var res = await Client.PostAsJsonAsync("/api/surveys", new
        {
            location = "Blocked",
            constructionTypeCode = "residential",
            surveyDate = DateTime.UtcNow,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_UnknownId_Is404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.GetAsync("/api/surveys/9999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_FilterBySearchAndConstructionType()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tag = "Uniquely-tagged " + Guid.NewGuid().ToString("N")[..6];
        await CreateSurveyAsync(location: $"{tag} Alpha", type: "residential");
        await CreateSurveyAsync(location: $"{tag} Beta", type: "commercial");

        var searched = await Client.GetAsync($"/api/surveys?search={Uri.EscapeDataString(tag)}&pageSize=20");
        searched.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(searched);
        body.GetProperty("items").GetArrayLength().Should().BeGreaterOrEqualTo(2);

        var typed = await Client.GetAsync(
            $"/api/surveys?search={Uri.EscapeDataString(tag)}&constructionTypeCode=commercial");
        var typedBody = await ReadJsonAsync(typed);
        var arr = typedBody.GetProperty("items");
        for (int i = 0; i < arr.GetArrayLength(); i++)
        {
            arr[i].GetProperty("constructionTypeCode").GetString().Should().Be("commercial");
        }
    }

    // ---------- NIH-100 update / delete ----------

    [Fact]
    public async Task Update_HappyPath_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateSurveyAsync("Old location " + Guid.NewGuid().ToString("N")[..4], "residential");
        var operationalProjectId = await GetOperationalProjectIdAsync(id);

        var res = await Client.PutAsJsonAsync($"/api/surveys/{id}", new
        {
            location = "Updated location",
            constructionTypeCode = "commercial",
            surveyDate = DateTime.UtcNow.AddDays(-2),
            operationalProjectId,
            note = "Ghi chú",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("location").GetString().Should().Be("Updated location");
        body.GetProperty("constructionTypeCode").GetString().Should().Be("commercial");
        body.GetProperty("note").GetString().Should().Be("Ghi chú");
    }

    [Fact]
    public async Task Update_UnknownId_Is404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var operationalProjectId = await CreateOperationalProjectAsync();
        var res = await Client.PutAsJsonAsync("/api/surveys/9999999", new
        {
            location = "x",
            constructionTypeCode = "residential",
            surveyDate = DateTime.UtcNow,
            operationalProjectId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_InvalidConstructionType_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateSurveyAsync("Bad update", "residential");
        var operationalProjectId = await GetOperationalProjectIdAsync(id);
        var res = await Client.PutAsJsonAsync($"/api/surveys/{id}", new
        {
            location = "Bad update",
            constructionTypeCode = "definitely-not-real",
            surveyDate = DateTime.UtcNow,
            operationalProjectId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Warehouse_CannotUpdate()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateSurveyAsync("SM created", "residential");

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        var res = await Client.PutAsJsonAsync($"/api/surveys/{id}", new
        {
            location = "should be blocked",
            constructionTypeCode = "residential",
            surveyDate = DateTime.UtcNow,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_NotSynced_WithPreviewConfirmationSucceeds()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateSurveyAsync("Delete me", "residential");
        var impact = await GetDeletionImpactAsync(id);
        impact.GetProperty("resourceType").GetString().Should().Be("Survey");
        impact.GetProperty("requiredConfirmation").GetString()
            .Should().Be((await ReadJsonAsync(await Client.GetAsync($"/api/surveys/{id}"))).GetProperty("code").GetString());

        (await ConfirmDeleteAsync(id, impact)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/surveys/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletionImpact_EnforcesAuthorizationPermissionAndOwnerScope()
    {
        (await Client.GetAsync("/api/surveys/1/deletion-impact"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/surveys/1/deletion-impact"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateSurveyAsync("Manager owned", "residential");

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync($"/api/surveys/{id}/deletion-impact"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DeleteSurveyAsync(id, new string('a', 64), "HIDDEN"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await WithDbAsync(db => db.Surveys.AnyAsync(item => item.Id == id))).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_RejectsMissingOrInvalidConfirmationAndStalePlanWithoutMutation()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateSurveyAsync("Rejected delete", "residential");
        var impact = await GetDeletionImpactAsync(id);
        var token = impact.GetProperty("planToken").GetString()!;
        var confirmation = impact.GetProperty("requiredConfirmation").GetString()!;

        (await DeleteSurveyAsync(id, token, null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DeleteSurveyAsync(id, token, $"{confirmation} ")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DeleteSurveyAsync(id, new string('a', 64), confirmation)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await WithDbAsync(db => db.Surveys.AnyAsync(item => item.Id == id))).Should().BeTrue();
        (await WithDbAsync(db => db.HardDeleteOperations.AnyAsync(item =>
            item.ResourceType == "Survey" && item.ResourceId == id.ToString()))).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_MissingSurvey_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await DeleteSurveyAsync(9999999, new string('a', 64), "SV-MISSING"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithoutManagePermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await DeleteSurveyAsync(9999999, new string('a', 64), "SV-MISSING"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- NIH-101 timeline ----------

    [Fact]
    public async Task Timeline_ReturnsArrayForKnownSurvey()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateSurveyAsync("Timeline probe", "residential");
        var operationalProjectId = await GetOperationalProjectIdAsync(id);
        // Fire an auditable action so the timeline has content queued.
        await Client.PutAsJsonAsync($"/api/surveys/{id}", new
        {
            location = "Timeline probe (updated)",
            surveyDate = DateTime.UtcNow.AddDays(-1),
            operationalProjectId,
        });

        var res = await Client.GetAsync($"/api/surveys/{id}/timeline");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        // Audit log flush is queued in the background so the array may be
        // empty in-test — shape verification is what we assert here,
        // matching the tenders / contracts timeline pattern.
        (await ReadJsonAsync(res)).ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task Timeline_UnknownSurvey_Is404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.GetAsync("/api/surveys/9999999/timeline")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<int> CreateSurveyAsync(string location, string type)
    {
        var operationalProjectId = await CreateOperationalProjectAsync();
        var res = await Client.PostAsJsonAsync("/api/surveys", new
        {
            location,
            constructionTypeCode = type,
            surveyDate = DateTime.UtcNow.AddDays(-1),
            operationalProjectId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private Task<int> GetOperationalProjectIdAsync(int surveyId) => WithDbAsync(db =>
        db.Surveys.AsNoTracking()
            .Where(survey => survey.Id == surveyId)
            .Select(survey => survey.OperationalProjectId)
            .SingleAsync());

    private Task<int> CreateOperationalProjectAsync() => WithDbAsync(async db =>
    {
        var customer = new Customer { Name = $"Survey customer {Guid.NewGuid():N}", Type = CustomerType.Company };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var project = new OperationalProject
        {
            Code = $"OP-{Guid.NewGuid():N}",
            Name = "Survey project",
            CustomerId = customer.Id,
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    });

    private async Task<System.Text.Json.JsonElement> GetDeletionImpactAsync(int surveyId)
    {
        var response = await Client.GetAsync($"/api/surveys/{surveyId}/deletion-impact");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadJsonAsync(response);
    }

    private Task<HttpResponseMessage> ConfirmDeleteAsync(int surveyId, System.Text.Json.JsonElement impact) =>
        DeleteSurveyAsync(
            surveyId,
            impact.GetProperty("planToken").GetString(),
            impact.GetProperty("requiredConfirmation").GetString());

    private async Task<HttpResponseMessage> DeleteSurveyAsync(int surveyId, string? planToken, string? confirmation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/surveys/{surveyId}")
        {
            Content = JsonContent.Create(new { planToken, confirmation }),
        };
        return await Client.SendAsync(request);
    }
}

public class SurveyMediaControllerTests : IntegrationTestBase, IAsyncLifetime
{
    private static readonly byte[] JpegBytes = [0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10, 0xff, 0xd9];
    private readonly HashSet<int> createdSurveyIds = [];

    public SurveyMediaControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Upload_WithoutAuthenticationOrPermission_IsRejected()
    {
        using (var anonymousForm = CreateUploadForm("anonymous.jpg", JpegBytes))
        {
            var anonymousResponse = await Client.PostAsync("/api/surveys/9999999/media", anonymousForm);
            anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        using var forbiddenForm = CreateUploadForm("forbidden.jpg", JpegBytes);
        var forbiddenResponse = await Client.PostAsync("/api/surveys/9999999/media", forbiddenForm);
        forbiddenResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DriveConnection_WhenOAuthIsUnavailable_ReportsPushOnlyWithoutExposingSecrets()
    {
        await AuthenticateAsSalesManagerAsync();

        var response = await Client.GetAsync("/api/surveys/drive-connection");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await ReadJsonAsync(response);
        status.GetProperty("status").GetString().Should().Be("Unavailable");
        status.GetProperty("syncMode").GetString().Should().Be("PushOnly");
        status.ToString().Should().NotContain("ClientSecret")
            .And.NotContain("refresh_token")
            .And.NotContain("client_secret");
    }

    [Fact]
    public async Task Upload_Jpg_RemainsPending_AndIsAvailableThroughParentBoundReadEndpoints()
    {
        await AuthenticateAsSalesManagerAsync();
        var surveyId = await CreateSurveyAsync("Media detail");
        var otherSurveyId = await CreateSurveyAsync("Wrong media parent");
        const string note = "Ảnh hiện trạng móng";
        const decimal latitude = 10.7769m;
        const decimal longitude = 106.7009m;

        using var form = CreateUploadForm("foundation.jpg", JpegBytes, note, latitude, longitude);
        var uploadResponse = await Client.PostAsync($"/api/surveys/{surveyId}/media", form);

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var uploaded = await ReadJsonAsync(uploadResponse);
        var mediaId = uploaded.GetProperty("id").GetInt64();
        uploaded.GetProperty("originalFileName").GetString().Should().Be("foundation.jpg");
        uploaded.GetProperty("contentType").GetString().Should().Be("image/jpeg");
        uploaded.GetProperty("note").GetString().Should().Be(note);
        uploaded.GetProperty("latitude").GetDecimal().Should().Be(latitude);
        uploaded.GetProperty("longitude").GetDecimal().Should().Be(longitude);
        uploaded.GetProperty("syncStatus").GetString().Should().Be("Pending");
        uploaded.GetProperty("syncAttemptCount").GetInt32().Should().Be(0);
        uploaded.GetProperty("contentUrl").GetString().Should()
            .Be($"/api/surveys/{surveyId}/media/{mediaId}/content");

        var persisted = await WithDbAsync(db => db.SurveyMedia.AsNoTracking().SingleAsync(media => media.Id == mediaId));
        persisted.SyncStatus.Should().Be(SurveyMediaSyncStatus.Pending);
        persisted.SyncAttemptCount.Should().Be(0);
        persisted.DriveFileId.Should().BeNull();
        var staged = await WithDbAsync(db => db.ProjectDocuments.AsNoTracking()
            .SingleAsync(document => document.SourceModule == ProjectDocumentSourceModule.Survey &&
                document.SourceRecordId == mediaId));
        staged.Category.Should().Be(ProjectDocumentCategory.Survey);

        var detailResponse = await Client.GetAsync($"/api/surveys/{surveyId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await ReadJsonAsync(detailResponse);
        detail.GetProperty("media").EnumerateArray().Should()
            .ContainSingle(media => media.GetProperty("id").GetInt64() == mediaId);
        detail.GetProperty("checklistResults").GetArrayLength().Should().BeGreaterThan(0);

        var contentResponse = await Client.GetAsync($"/api/surveys/{surveyId}/media/{mediaId}/content");
        contentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        contentResponse.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        (await contentResponse.Content.ReadAsByteArrayAsync()).Should().Equal(JpegBytes);
        (await Client.GetAsync($"/api/surveys/{otherSurveyId}/media/{mediaId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var syncLogResponse = await Client.GetAsync($"/api/surveys/{surveyId}/sync-log");
        syncLogResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var syncEntry = (await ReadJsonAsync(syncLogResponse)).EnumerateArray().Single();
        syncEntry.GetProperty("mediaId").GetInt64().Should().Be(mediaId);
        syncEntry.GetProperty("fileName").GetString().Should().Be("foundation.jpg");
        syncEntry.GetProperty("status").GetString().Should().Be("Pending");
        syncEntry.GetProperty("attemptCount").GetInt32().Should().Be(0);
        syncEntry.GetProperty("maxAttempts").GetInt32().Should().Be(3);

    }

    [Fact]
    public async Task ExportPdf_ValidSurvey_ReturnsLocalizedPdfWithMediaNote()
    {
        await AuthenticateAsSalesManagerAsync();
        var surveyId = await CreateSurveyAsync("PDF export");
        const string note = "Ảnh hiện trạng móng";
        using (var form = CreateUploadForm("foundation.jpg", JpegBytes, note))
        {
            (await Client.PostAsync($"/api/surveys/{surveyId}/media", form))
                .StatusCode.Should().Be(HttpStatusCode.Created);
        }
        await WithDbAsync(async db =>
        {
            await UpsertTranslationAsync(db, "surveys.pdf.title", "en", "SURVEY REPORT");
            await UpsertTranslationAsync(
                db, "masterData.survey_checklist_default.geology.label", "en", "Geology");
            await db.SaveChangesAsync();
        });

        var response = await Client.GetAsync($"/api/surveys/{surveyId}/export.pdf?lang=en");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be($"survey-{surveyId}.pdf");
        var pdf = await response.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        using var pdfDocument = PdfDocument.Open(pdf);
        var pdfText = string.Join(
            '\n',
            pdfDocument.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)));
        pdfText.Should().Contain("SURVEY REPORT");
        pdfText.Should().Contain("Geology");
        pdfText.Should().Contain(note);
    }

    [Fact]
    public async Task Conditions_TemplateAtomicImportJsonReplaceAndPdf_AllWork()
    {
        await AuthenticateAsSalesManagerAsync();
        var template = await Client.GetAsync("/api/surveys/conditions/template.csv");
        template.StatusCode.Should().Be(HttpStatusCode.OK);
        var templateText = await template.Content.ReadAsStringAsync();
        templateText.Should().Contain("RightOfWay,access-width,Unknown,,m")
            .And.Contain("Elevation,site-elevation,Unknown,,m")
            .And.Contain("Infrastructure,electricity");

        var surveyId = await CreateSurveyAsync("Structured conditions");
        await WithDbAsync(async db =>
        {
            db.SurveySiteConditions.Add(new SurveySiteCondition
            {
                SurveyId = surveyId,
                Category = SurveySiteConditionCategory.RightOfWay,
                Code = "access-width",
                Status = SurveySiteConditionStatus.Available,
                NumericValue = 4,
                UnitCode = "m",
            });
            await db.SaveChangesAsync();
        });
        using (var invalidForm = CreateCsvForm(ValidConditionsCsv().Replace(",6.5,m,", ",6.5,yard,")))
        {
            var invalid = await Client.PostAsync($"/api/surveys/{surveyId}/conditions/import", invalidForm);
            invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        var preserved = await WithDbAsync(db => db.SurveySiteConditions.AsNoTracking()
            .SingleAsync(condition => condition.SurveyId == surveyId));
        preserved.NumericValue.Should().Be(4);

        using (var validForm = CreateCsvForm(ValidConditionsCsv()))
        {
            var valid = await Client.PostAsync($"/api/surveys/{surveyId}/conditions/import", validForm);
            valid.StatusCode.Should().Be(HttpStatusCode.OK);
            (await ReadJsonAsync(valid)).GetProperty("conditions").GetArrayLength().Should().Be(3);
        }
        var jsonReplace = await Client.PutAsJsonAsync($"/api/surveys/{surveyId}/conditions", new
        {
            conditions = new object[]
            {
                new { category = "RightOfWay", code = "access-width", statusCode = "Available", numericValue = 7.2m, unitCode = "m" },
                new { category = "Elevation", code = "site-elevation", statusCode = "Available", numericValue = 2.1m, unitCode = "m", description = "Finished floor benchmark" },
                new { category = "Infrastructure", code = "electricity", statusCode = "Available", referenceCode = "electricity", description = "Grid at boundary" },
            },
        });
        jsonReplace.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await ReadJsonAsync(await Client.GetAsync($"/api/surveys/{surveyId}"));
        detail.GetProperty("siteConditions").GetArrayLength().Should().Be(3);
        using (var scope = Factory.Services.CreateScope())
        {
            var translations = scope.ServiceProvider.GetRequiredService<TranslationService>();
            await translations.UpsertPairAsync(
                "surveys.pdf.conditions",
                "ĐIỀU KIỆN HIỆN TRƯỜNG",
                new Dictionary<string, string> { ["en"] = "SITE CONDITIONS" },
                "surveys");
        }
        var pdfResponse = await Client.GetAsync($"/api/surveys/{surveyId}/export.pdf?lang=en");
        pdfResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var pdf = PdfDocument.Open(await pdfResponse.Content.ReadAsByteArrayAsync());
        var pdfText = string.Join('\n', pdf.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)));
        pdfText.Should().Contain("SITE CONDITIONS").And.Contain("access-width").And.Contain("7.2 m");
    }

    [Fact]
    public async Task ExportPdf_WithoutAuthenticationOrPermission_IsRejected()
    {
        (await Client.GetAsync("/api/surveys/9999999/export.pdf"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/surveys/9999999/export.pdf"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExportPdf_UnknownSurvey_ReturnsNotFound()
    {
        await AuthenticateAsSalesManagerAsync();

        (await Client.GetAsync("/api/surveys/9999999/export.pdf"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExportPdf_UnsupportedLanguage_ReturnsActionableBadRequest()
    {
        await AuthenticateAsSalesManagerAsync();
        var surveyId = await CreateSurveyAsync("Invalid PDF language");

        var response = await Client.GetAsync($"/api/surveys/{surveyId}/export.pdf?lang=fr");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString()
            .Should().Contain("vi, en, zh hoặc ja");
    }

    [Fact]
    public async Task Upload_UnsupportedExtension_ReturnsBadRequestWithMessage()
    {
        await AuthenticateAsSalesManagerAsync();
        var surveyId = await CreateSurveyAsync("Unsupported media");
        using var form = CreateUploadForm("malware.exe", [0x4d, 0x5a]);

        var response = await Client.PostAsync($"/api/surveys/{surveyId}/media", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var message = (await ReadJsonAsync(response)).GetProperty("message").GetString();
        message.Should().Contain("không được hỗ trợ").And.Contain("JPG");
        (await WithDbAsync(db => db.SurveyMedia.CountAsync(media => media.SurveyId == surveyId))).Should().Be(0);
    }

    [Fact]
    public async Task Retry_PendingAndFailedMedia_RequeuesUntilThirdAttemptIsTerminal()
    {
        await AuthenticateAsSalesManagerAsync();
        var surveyId = await CreateSurveyAsync("Retry media");
        var mediaId = await UploadJpegAsync(surveyId, "retry.jpg");

        var pendingResponse = await Client.PostAsync(
            $"/api/surveys/{surveyId}/media/{mediaId}/retry-sync", null);
        pendingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(pendingResponse)).GetProperty("syncStatus").GetString().Should().Be("Pending");

        await SetSyncFailureAsync(mediaId, 2, "Drive unavailable");
        var failedResponse = await Client.PostAsync(
            $"/api/surveys/{surveyId}/media/{mediaId}/retry-sync", null);
        failedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retried = await ReadJsonAsync(failedResponse);
        retried.GetProperty("syncStatus").GetString().Should().Be("Pending");
        retried.GetProperty("syncAttemptCount").GetInt32().Should().Be(2);
        retried.GetProperty("syncError").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        await SetSyncFailureAsync(mediaId, 3, "Final Drive failure");
        var terminalResponse = await Client.PostAsync(
            $"/api/surveys/{surveyId}/media/{mediaId}/retry-sync", null);
        terminalResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(terminalResponse)).GetProperty("message").GetString().Should().Contain("3 lần");

        var terminal = await WithDbAsync(db => db.SurveyMedia.AsNoTracking().SingleAsync(media => media.Id == mediaId));
        terminal.SyncStatus.Should().Be(SurveyMediaSyncStatus.Failed);
        terminal.SyncAttemptCount.Should().Be(3);
        terminal.SyncError.Should().Be("Final Drive failure");
    }

    [Fact]
    public async Task Checklist_UpdateSucceeds_AndWrongSurveyParentReturnsNotFound()
    {
        await AuthenticateAsSalesManagerAsync();
        var surveyId = await CreateSurveyAsync("Checklist media");
        var otherSurveyId = await CreateSurveyAsync("Wrong checklist parent");
        var resultId = await WithDbAsync(db => db.SurveyChecklistResults.AsNoTracking()
            .Where(result => result.SurveyId == surveyId)
            .OrderBy(result => result.SortOrder)
            .Select(result => result.Id)
            .FirstAsync());

        var updateResponse = await Client.PutAsJsonAsync($"/api/surveys/{surveyId}/checklist/{resultId}", new
        {
            status = "NeedsAttention",
            note = "Cần đo lại cao độ",
            sortOrder = 42,
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ReadJsonAsync(updateResponse);
        updated.GetProperty("status").GetString().Should().Be("NeedsAttention");
        updated.GetProperty("note").GetString().Should().Be("Cần đo lại cao độ");
        updated.GetProperty("sortOrder").GetInt32().Should().Be(42);

        var wrongParentResponse = await Client.PutAsJsonAsync(
            $"/api/surveys/{otherSurveyId}/checklist/{resultId}", new
            {
                status = "Failed",
                note = "Must not be applied",
                sortOrder = 1,
            });
        wrongParentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var persisted = await WithDbAsync(db => db.SurveyChecklistResults.AsNoTracking()
            .SingleAsync(result => result.Id == resultId));
        persisted.Status.Should().Be(SurveyChecklistStatus.NeedsAttention);
        persisted.Note.Should().Be("Cần đo lại cao độ");
        persisted.SortOrder.Should().Be(42);
    }

    [Fact]
    public async Task Delete_SurveyWithMediaIsRejected_ThenDeletingMediaAllowsSurveyDeletion()
    {
        await AuthenticateAsSalesManagerAsync();
        var surveyId = await CreateSurveyAsync("Delete guarded media");
        var mediaId = await UploadJpegAsync(surveyId, "delete.jpg");
        var storedPath = await WithDbAsync(db => db.SurveyMedia.AsNoTracking()
            .Where(media => media.Id == mediaId)
            .Select(media => media.RelativePath)
            .SingleAsync());

        var impact = await ReadJsonAsync(await Client.GetAsync($"/api/surveys/{surveyId}/deletion-impact"));
        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "survey.media" &&
            item.GetProperty("action").GetString() == "Block");

        var guardedDelete = await ConfirmSurveyDeleteAsync(surveyId, impact);
        guardedDelete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Surveys.AnyAsync(survey => survey.Id == surveyId))).Should().BeTrue();
        (await WithDbAsync(db => db.SurveyMedia.AnyAsync(media => media.Id == mediaId))).Should().BeTrue();
        File.Exists(ResolveStoredPath(storedPath)).Should().BeTrue();

        (await Client.DeleteAsync($"/api/surveys/{surveyId}/media/{mediaId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        File.Exists(ResolveStoredPath(storedPath)).Should().BeFalse();
        var clearedImpact = await ReadJsonAsync(await Client.GetAsync($"/api/surveys/{surveyId}/deletion-impact"));
        (await ConfirmSurveyDeleteAsync(surveyId, clearedImpact))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/surveys/{surveyId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ProcessingMedia_ReturnsConflictAndPreservesClaim()
    {
        await AuthenticateAsSalesManagerAsync();
        var surveyId = await CreateSurveyAsync("Delete processing media");
        var mediaId = await UploadJpegAsync(surveyId, "processing.jpg");
        var claimToken = Guid.NewGuid();
        var claimExpiresAt = DateTime.UtcNow.AddMinutes(15);
        await WithDbAsync(async db =>
        {
            var media = await db.SurveyMedia.SingleAsync(item => item.Id == mediaId);
            media.SyncStatus = SurveyMediaSyncStatus.Processing;
            media.SyncAttemptCount = 1;
            media.ClaimToken = claimToken;
            media.ClaimExpiresAt = claimExpiresAt;
            await db.SaveChangesAsync();
        });

        var response = await Client.DeleteAsync($"/api/surveys/{surveyId}/media/{mediaId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(response)).GetProperty("message").GetString().Should().Contain("đang được đồng bộ");
        var persisted = await WithDbAsync(db => db.SurveyMedia.AsNoTracking().SingleAsync(item => item.Id == mediaId));
        persisted.SyncStatus.Should().Be(SurveyMediaSyncStatus.Processing);
        persisted.SyncAttemptCount.Should().Be(1);
        persisted.ClaimToken.Should().Be(claimToken);
        persisted.ClaimExpiresAt.Should().Be(claimExpiresAt);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (createdSurveyIds.Count == 0) return;

        await WithDbAsync(async db =>
        {
            db.SurveyMedia.RemoveRange(db.SurveyMedia.Where(media => createdSurveyIds.Contains(media.SurveyId)));
            db.SurveyChecklistResults.RemoveRange(
                db.SurveyChecklistResults.Where(result => createdSurveyIds.Contains(result.SurveyId)));
            db.SurveySiteConditions.RemoveRange(
                db.SurveySiteConditions.Where(condition => createdSurveyIds.Contains(condition.SurveyId)));
            db.Surveys.RemoveRange(db.Surveys.Where(survey => createdSurveyIds.Contains(survey.Id)));
            await db.SaveChangesAsync();
        });

        var environment = Factory.Services.GetRequiredService<IWebHostEnvironment>();
        foreach (var surveyId in createdSurveyIds)
        {
            var directory = Path.Combine(
                environment.ContentRootPath, "wwwroot", "files", "survey-media", surveyId.ToString());
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private Task AuthenticateAsSalesManagerAsync() =>
        AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));

    private async Task<int> CreateSurveyAsync(string prefix)
    {
        var operationalProjectId = await CreateOperationalProjectAsync();
        var response = await Client.PostAsJsonAsync("/api/surveys", new
        {
            location = $"{prefix} {Guid.NewGuid():N}"[..Math.Min(60, prefix.Length + 33)],
            constructionTypeCode = "industrial",
            surveyDate = DateTime.UtcNow.AddDays(-1),
            operationalProjectId,
        });
        response.EnsureSuccessStatusCode();
        var surveyId = (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
        createdSurveyIds.Add(surveyId);
        return surveyId;
    }

    private async Task<long> UploadJpegAsync(int surveyId, string fileName)
    {
        using var form = CreateUploadForm(fileName, JpegBytes);
        var response = await Client.PostAsync($"/api/surveys/{surveyId}/media", form);
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("id").GetInt64();
    }

    private async Task<HttpResponseMessage> ConfirmSurveyDeleteAsync(
        int surveyId,
        System.Text.Json.JsonElement impact)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/surveys/{surveyId}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
            }),
        };
        return await Client.SendAsync(request);
    }

    private async Task SetSyncFailureAsync(long mediaId, int attemptCount, string error)
    {
        await WithDbAsync(async db =>
        {
            var media = await db.SurveyMedia.SingleAsync(item => item.Id == mediaId);
            media.SyncStatus = SurveyMediaSyncStatus.Failed;
            media.SyncAttemptCount = attemptCount;
            media.SyncError = error;
            media.LastSyncAttemptAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });
    }

    private string ResolveStoredPath(string relativePath)
    {
        var environment = Factory.Services.GetRequiredService<IWebHostEnvironment>();
        return Path.Combine(
            environment.ContentRootPath,
            "wwwroot",
            relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    }

    private static MultipartFormDataContent CreateUploadForm(
        string fileName,
        byte[] bytes,
        string? note = null,
        decimal? latitude = null,
        decimal? longitude = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        if (note is not null) form.Add(new StringContent(note), "note");
        if (latitude.HasValue) form.Add(new StringContent(latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)), "latitude");
        if (longitude.HasValue) form.Add(new StringContent(longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)), "longitude");
        return form;
    }

    private static MultipartFormDataContent CreateCsvForm(string csv)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "conditions.csv");
        return form;
    }

    private static string ValidConditionsCsv() =>
        "Category,Code,StatusCode,NumericValue,UnitCode,ReferenceCode,Description,Note\r\n" +
        "RightOfWay,access-width,Available,6.5,m,,Truck access,\r\n" +
        "Elevation,site-elevation,NeedsInvestigation,,m,,Survey benchmark required,\r\n" +
        "Infrastructure,electricity,Available,,,electricity,Grid at boundary,\r\n";

    private Task<int> CreateOperationalProjectAsync() => WithDbAsync(async db =>
    {
        var customer = new Customer { Name = $"Survey media customer {Guid.NewGuid():N}", Type = CustomerType.Company };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var project = new OperationalProject
        {
            Code = $"OP-{Guid.NewGuid():N}",
            Name = "Survey media project",
            CustomerId = customer.Id,
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    });

    private static async Task UpsertTranslationAsync(
        NihomeBackend.Data.AppDbContext db,
        string key,
        string language,
        string value)
    {
        var translation = await db.Translations.SingleOrDefaultAsync(item =>
            item.Key == key && item.LanguageCode == language);
        if (translation is null)
        {
            db.Translations.Add(new Translation
            {
                Key = key,
                LanguageCode = language,
                Value = value,
                Category = "surveys",
            });
            return;
        }

        translation.Value = value;
    }
}
