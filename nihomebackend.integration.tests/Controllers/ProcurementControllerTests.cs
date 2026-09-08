using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public class ProcurementControllerTests : IntegrationTestBase
{
    public ProcurementControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task BoqLifecycle_ApprovesImmutableRevision()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();

        var created = await SendAsync(HttpMethod.Post, $"/api/operational-projects/{projectId}/procurement/boq-revisions", new
        {
            currency = "VND",
            lines = new[] { new { itemCode = "MAT-001", description = "Concrete", unit = "m3", approvedQuantity = 10m, budgetUnitPrice = 1_000m } },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdJson = await ReadJsonAsync(created);
        var id = createdJson.GetProperty("id").GetInt32();

        var invalidDecision = await SendAsync(HttpMethod.Post, $"/api/operational-projects/{projectId}/procurement/boq-revisions/{id}/decision", new
        {
            approved = true,
            rowVersion = createdJson.GetProperty("rowVersion").GetString(),
        });
        invalidDecision.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ProjectBoqRevisions.AsNoTracking().SingleAsync(item => item.Id == id)))
            .Status.Should().Be(ProjectBoqRevisionStatus.Draft);

        var submitted = await SendAsync(HttpMethod.Post, $"/api/operational-projects/{projectId}/procurement/boq-revisions/{id}/submit", new
        {
            rowVersion = createdJson.GetProperty("rowVersion").GetString(),
        });
        submitted.EnsureSuccessStatusCode();
        var submittedJson = await ReadJsonAsync(submitted);
        var projectManagerUserId = await WithDbAsync(db => db.OperationalProjects.AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => item.ProjectManagerUserId!.Value)
            .SingleAsync());
        (await WithDbAsync(db => db.Notifications.AsNoTracking().AnyAsync(item =>
            item.UserId == projectManagerUserId &&
            item.TemplateCode == "procurement.boq.submitted" &&
            item.RefEntityType == "ProjectBoqRevision" &&
            item.RefEntityId == id))).Should().BeTrue();

        var shortRejection = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/procurement/boq-revisions/{id}/decision",
            new
            {
                approved = false,
                reason = "No",
                rowVersion = submittedJson.GetProperty("rowVersion").GetString(),
            });
        shortRejection.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ProjectBoqRevisions.AsNoTracking().SingleAsync(item => item.Id == id)))
            .Status.Should().Be(ProjectBoqRevisionStatus.Submitted);

        var approved = await SendAsync(HttpMethod.Post, $"/api/operational-projects/{projectId}/procurement/boq-revisions/{id}/decision", new
        {
            approved = true,
            rowVersion = submittedJson.GetProperty("rowVersion").GetString(),
        });
        approved.EnsureSuccessStatusCode();
        var approvedJson = await ReadJsonAsync(approved);
        approvedJson.GetProperty("status").GetString().Should().Be("Approved");
        approvedJson.GetProperty("costTotal").GetDecimal().Should().Be(10_000m);

        var updateApproved = await SendAsync(HttpMethod.Put, $"/api/operational-projects/{projectId}/procurement/boq-revisions/{id}", new
        {
            currency = "VND",
            rowVersion = approvedJson.GetProperty("rowVersion").GetString(),
            lines = new[] { new { itemCode = "MAT-001", description = "Changed", unit = "m3", approvedQuantity = 20m, budgetUnitPrice = 1_000m } },
        });
        updateApproved.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ProjectBoqRevisions.AsNoTracking().SingleAsync(item => item.Id == id)))
            .CostTotal.Should().Be(10_000m);
    }

    [Fact]
    public async Task BoqList_FiltersSortsPaginatesAndExportsWithinProject()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var otherProjectId = await CreateProjectAsync();

        async Task CreateRevisionAsync(int targetProjectId, string code, decimal quantity, decimal unitPrice)
        {
            var response = await SendAsync(HttpMethod.Post,
                $"/api/operational-projects/{targetProjectId}/procurement/boq-revisions",
                new
                {
                    currency = "VND",
                    lines = new[] { new { itemCode = code, description = $"Description {code}", unit = "m2", approvedQuantity = quantity, budgetUnitPrice = unitPrice } },
                });
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        }

        await CreateRevisionAsync(projectId, "MAT-LOW", 2, 100);
        await CreateRevisionAsync(projectId, "MAT-HIGH", 3, 500);
        await CreateRevisionAsync(otherProjectId, "MAT-OTHER-PROJECT", 9, 999);

        var firstPage = await Client.GetAsync(
            $"/api/operational-projects/{projectId}/procurement/boq-revisions?status=Draft&search=MAT&sortBy=total&sortDirection=desc&page=1&pageSize=1");

        firstPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadJsonAsync(firstPage);
        page.GetProperty("total").GetInt32().Should().Be(2);
        page.GetProperty("page").GetInt32().Should().Be(1);
        page.GetProperty("pageSize").GetInt32().Should().Be(1);
        var item = page.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("operationalProjectId").GetInt32().Should().Be(projectId);
        item.GetProperty("costTotal").GetDecimal().Should().Be(1_500m);
        item.GetProperty("lineCount").GetInt32().Should().Be(1);

        var noMatch = await ReadJsonAsync(await Client.GetAsync(
            $"/api/operational-projects/{projectId}/procurement/boq-revisions?search=MAT-OTHER-PROJECT"));
        noMatch.GetProperty("total").GetInt32().Should().Be(0);

        var export = await Client.GetAsync(
            $"/api/operational-projects/{projectId}/procurement/boq-revisions/export?search=MAT&sortBy=total&sortDirection=desc");
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        export.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = await export.Content.ReadAsStringAsync();
        csv.Should().Contain("MAT-HIGH").And.Contain("MAT-LOW").And.NotContain("MAT-OTHER-PROJECT");
    }

    [Fact]
    public async Task BoqList_ProjectOutsideCallerScope_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PROCUREMENT"));

        var response = await Client.GetAsync(
            $"/api/operational-projects/{projectId}/procurement/boq-revisions");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BoqDetail_ReturnsLinesAndRejectsCrossProjectLookup()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var otherProjectId = await CreateProjectAsync();
        var created = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/procurement/boq-revisions",
            new
            {
                currency = "VND",
                lines = new[]
                {
                    new { itemCode = "MAT-DETAIL", description = "Detail material", unit = "m2", approvedQuantity = 4m, budgetUnitPrice = 250m },
                },
            });
        created.EnsureSuccessStatusCode();
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var detail = await Client.GetAsync(
            $"/api/operational-projects/{projectId}/procurement/boq-revisions/{id}");

        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonAsync(detail);
        json.GetProperty("operationalProjectId").GetInt32().Should().Be(projectId);
        json.GetProperty("status").GetString().Should().Be("Draft");
        json.GetProperty("costTotal").GetDecimal().Should().Be(1_000m);
        json.GetProperty("updatedAt").GetDateTime().Should().NotBe(default);
        json.GetProperty("rowVersion").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("lines").EnumerateArray().Single()
            .GetProperty("itemCode").GetString().Should().Be("MAT-DETAIL");

        (await Client.GetAsync(
            $"/api/operational-projects/{otherProjectId}/procurement/boq-revisions/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BoqUpdate_ValidatesDuplicateCodesAndPreservesRejectedInput()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var created = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/procurement/boq-revisions",
            new
            {
                currency = "VND",
                lines = new[]
                {
                    new { itemCode = "MAT-ORIGINAL", description = "Original material", unit = "m2", approvedQuantity = 2m, budgetUnitPrice = 100m },
                },
            });
        created.EnsureSuccessStatusCode();
        var createdJson = await ReadJsonAsync(created);
        var id = createdJson.GetProperty("id").GetInt32();
        var rowVersion = createdJson.GetProperty("rowVersion").GetString();

        var duplicate = await SendAsync(HttpMethod.Put,
            $"/api/operational-projects/{projectId}/procurement/boq-revisions/{id}",
            new
            {
                currency = "VND",
                rowVersion,
                lines = new[]
                {
                    new { itemCode = "MAT-DUP", description = "First", unit = "m2", approvedQuantity = 2m, budgetUnitPrice = 100m },
                    new { itemCode = "mat-dup", description = "Second", unit = "m2", approvedQuantity = 3m, budgetUnitPrice = 200m },
                },
            });

        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var unchanged = await WithDbAsync(db => db.ProjectBoqRevisions.AsNoTracking()
            .Include(item => item.Lines).SingleAsync(item => item.Id == id));
        unchanged.Lines.Should().ContainSingle().Which.ItemCode.Should().Be("MAT-ORIGINAL");
        unchanged.CostTotal.Should().Be(200m);

        var updated = await SendAsync(HttpMethod.Put,
            $"/api/operational-projects/{projectId}/procurement/boq-revisions/{id}",
            new
            {
                currency = "USD",
                rowVersion,
                lines = new[]
                {
                    new { itemCode = "MAT-UPDATED", description = "Updated material", unit = "item", approvedQuantity = 4m, budgetUnitPrice = 300m },
                },
            });

        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        var updatedJson = await ReadJsonAsync(updated);
        updatedJson.GetProperty("currency").GetString().Should().Be("USD");
        updatedJson.GetProperty("costTotal").GetDecimal().Should().Be(1_200m);
        updatedJson.GetProperty("lines").EnumerateArray().Single()
            .GetProperty("itemCode").GetString().Should().Be("MAT-UPDATED");
    }

    [Fact]
    public async Task Workspace_WithoutAuthentication_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/operational-projects/1/procurement"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Workspace_WithBoqOnlyPermission_DoesNotExposeMaterialRequests()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var phone = $"07{Random.Shared.Next(10000000, 99999999)}";
        var projectId = await WithDbAsync(async db =>
        {
            var role = new Role
            {
                Code = $"BOQ_ONLY_{suffix}",
                Name = $"BOQ only {suffix}",
                IsActive = true,
                InitialPermissionsSeeded = true,
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            var boqViewPermissionId = await db.Permissions
                .Where(permission => permission.Module == "proc.boq" && permission.Action == "view")
                .Select(permission => permission.Id).SingleAsync();
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = boqViewPermissionId });
            var user = new ApplicationUser
            {
                PhoneNumber = phone,
                FullName = "BOQ only tester",
                Email = $"boq-only-{suffix}@nihome.test",
                Role = UserRole.USER,
                RoleEntityId = role.Id,
                IsActive = true,
            };
            user.PasswordHash = new PasswordService().Hash(user, TestDataSeeder.DefaultPassword);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            var customer = new Customer { Type = CustomerType.Company, Name = "BOQ permission customer", SourceCode = "referral" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-BOQ-{suffix}",
                Name = "BOQ Permission Project",
                CustomerId = customer.Id,
                ProjectManagerUserId = user.Id,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            var procurementUserId = await db.Users
                .Where(item => item.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PROCUREMENT"])
                .Select(item => item.Id).SingleAsync();
            db.MaterialRequests.Add(NewMaterialRequest(project.Id, $"MR-BOQ-{suffix}", MaterialRequestStatus.Draft,
                user.Id, procurementUserId, DateTime.UtcNow.AddDays(2)));
            await db.SaveChangesAsync();
            return project.Id;
        });
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsync(client, phone, TestDataSeeder.DefaultPassword));

        var response = await Client.GetAsync($"/api/operational-projects/{projectId}/procurement");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(response)).GetProperty("materialRequests").GetArrayLength().Should().Be(0);
        (await Client.GetAsync($"/api/operational-projects/{projectId}/procurement/material-requests"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MaterialRequests_List_FiltersPaginatesAndStaysWithinProject()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        var projectId = await CreateProjectAsync();
        var otherProjectId = await CreateProjectAsync();
        var procurementUserId = await WithDbAsync(db => db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PROCUREMENT"])
            .Select(user => user.Id).SingleAsync());
        var siteUserId = await WithDbAsync(db => db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
            .Select(user => user.Id).SingleAsync());

        await WithDbAsync(async db =>
        {
            db.MaterialRequests.AddRange(
                NewMaterialRequest(projectId, "MR-MATCH-01", MaterialRequestStatus.Submitted,
                    siteUserId, procurementUserId, new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc)),
                NewMaterialRequest(projectId, "MR-MATCH-02", MaterialRequestStatus.Submitted,
                    siteUserId, procurementUserId, new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc)),
                NewMaterialRequest(projectId, "MR-DRAFT-01", MaterialRequestStatus.Draft,
                    siteUserId, procurementUserId, new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc)),
                NewMaterialRequest(otherProjectId, "MR-MATCH-OTHER", MaterialRequestStatus.Submitted,
                    siteUserId, procurementUserId, new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc)));
            await db.SaveChangesAsync();
        });

        var response = await Client.GetAsync(
            $"/api/operational-projects/{projectId}/procurement/material-requests" +
            $"?search=MATCH&status=Submitted&assignedProcurementUserId={procurementUserId}" +
            "&requiredFrom=2026-09-10&requiredTo=2026-09-11&sortBy=requiredAt&sortDirection=desc&page=1&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("total").GetInt32().Should().Be(2);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(1);
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("code").GetString().Should().Be("MR-MATCH-02");
        items.EnumerateArray().Should().NotContain(item =>
            item.GetProperty("operationalProjectId").GetInt32() == otherProjectId);

        var ascending = await ReadJsonAsync(await Client.GetAsync(
            $"/api/operational-projects/{projectId}/procurement/material-requests" +
            "?search=MATCH&status=Submitted&sortBy=requiredAt&sortDirection=asc&page=1&pageSize=1"));
        ascending.GetProperty("items")[0].GetProperty("code").GetString().Should().Be("MR-MATCH-01");
    }

    [Fact]
    public async Task MaterialRequests_List_WithoutPermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN"));

        (await Client.GetAsync("/api/operational-projects/1/procurement/material-requests"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MaterialRequests_List_OutsideProjectScope_ReturnsNotFound()
    {
        var projectId = await CreateProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "WAREHOUSE"));

        (await Client.GetAsync($"/api/operational-projects/{projectId}/procurement/material-requests"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MaterialRequests_List_WithReversedDateRange_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        var projectId = await CreateProjectAsync();

        (await Client.GetAsync($"/api/operational-projects/{projectId}/procurement/material-requests?requiredFrom=2026-09-12&requiredTo=2026-09-10"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MaterialRequestDetail_ReturnsLifecycleAndRejectsCrossProjectLookup()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        var projectId = await CreateProjectAsync();
        var otherProjectId = await CreateProjectAsync();
        var requestId = await WithDbAsync(async db =>
        {
            var siteUserId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
                .Select(user => user.Id).SingleAsync();
            var procurementUserId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PROCUREMENT"])
                .Select(user => user.Id).SingleAsync();
            var revision = new ProjectBoqRevision
            {
                OperationalProjectId = projectId,
                RevisionNumber = 1,
                Status = ProjectBoqRevisionStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                PreparedByUserId = siteUserId,
                Lines = [new ProjectBoqLine { ItemCode = "MAT-DETAIL", Description = "Detail material", Unit = "kg", ApprovedQuantity = 100m, BudgetUnitPrice = 10m, Amount = 1_000m }],
            };
            db.ProjectBoqRevisions.Add(revision);
            var customerId = await db.OperationalProjects.Where(item => item.Id == projectId)
                .Select(item => item.CustomerId).SingleAsync();
            db.Contracts.AddRange(
                new Contract
                {
                    ContractNumber = $"HD-MR-{Guid.NewGuid():N}"[..24],
                    CustomerId = customerId,
                    OperationalProjectId = projectId,
                    Direction = ContractDirection.Upstream,
                    Type = ContractType.DesignAndBuild,
                    Status = ContractStatus.InProgress,
                    Value = 1_000_000m,
                },
                new Contract
                {
                    ContractNumber = $"PO-MR-{Guid.NewGuid():N}"[..24],
                    CustomerId = customerId,
                    OperationalProjectId = projectId,
                    Direction = ContractDirection.Downstream,
                    Type = ContractType.Supply,
                    Status = ContractStatus.Draft,
                    Value = 500_000m,
                });
            await db.SaveChangesAsync();
            var request = NewMaterialRequest(projectId, "MR-DETAIL", MaterialRequestStatus.Submitted,
                siteUserId, procurementUserId, DateTime.UtcNow.AddDays(2));
            request.SubmittedAt = DateTime.UtcNow;
            request.Lines.Add(new MaterialRequestLine { ProjectBoqLineId = revision.Lines.Single().Id, RequestedQuantity = 25m });
            db.MaterialRequests.Add(request);
            await db.SaveChangesAsync();
            return request.Id;
        });

        var detail = await Client.GetAsync(
            $"/api/operational-projects/{projectId}/procurement/material-requests/{requestId}");

        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonAsync(detail);
        json.GetProperty("code").GetString().Should().Be("MR-DETAIL");
        json.GetProperty("submittedAt").GetDateTime().Should().NotBe(default);
        json.GetProperty("submittedByName").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("createdAt").GetDateTime().Should().NotBe(default);
        json.GetProperty("updatedAt").GetDateTime().Should().NotBe(default);
        json.GetProperty("operationalProjectCode").GetString().Should().StartWith("PJ-PROC-");
        json.GetProperty("customerName").GetString().Should().Be("Procurement Customer");
        json.GetProperty("contracts").GetArrayLength().Should().Be(2);
        json.GetProperty("contracts")[0].TryGetProperty("value", out _).Should().BeFalse();
        json.GetProperty("lines")[0].GetProperty("boqApprovedQuantity").GetDecimal().Should().Be(100m);
        json.GetProperty("lines")[0].GetProperty("boqRemainingQuantity").GetDecimal().Should().Be(100m);

        (await Client.GetAsync(
            $"/api/operational-projects/{otherProjectId}/procurement/material-requests/{requestId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MaterialRequestLifecycle_UpdatesNotifiesAndLocksApprovedRequest()
    {
        var fixture = await CreateMaterialRequestProjectAsync(100m);
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));

        var created = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-requests",
            new
            {
                responsibleSiteUserId = fixture.ProjectManagerUserId,
                assignedProcurementUserId = fixture.ProcurementUserId,
                requiredAt = DateTime.UtcNow.AddDays(5),
                note = "Foundation materials",
                lines = new[] { new { projectBoqLineId = fixture.BoqLineId, requestedQuantity = 20m } },
            });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var createdJson = await ReadJsonAsync(created);
        var requestId = createdJson.GetProperty("id").GetInt32();

        var updated = await SendAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-requests/{requestId}",
            new
            {
                responsibleSiteUserId = fixture.ProjectManagerUserId,
                assignedProcurementUserId = fixture.ProcurementUserId,
                requiredAt = DateTime.UtcNow.AddDays(6),
                note = "Updated foundation materials",
                rowVersion = createdJson.GetProperty("rowVersion").GetString(),
                lines = new[] { new { projectBoqLineId = fixture.BoqLineId, requestedQuantity = 30m } },
            });
        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        var updatedJson = await ReadJsonAsync(updated);
        updatedJson.GetProperty("lines")[0].GetProperty("requestedQuantity").GetDecimal().Should().Be(30m);

        var submitted = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-requests/{requestId}/submit",
            new { rowVersion = updatedJson.GetProperty("rowVersion").GetString() });
        submitted.StatusCode.Should().Be(HttpStatusCode.OK);
        var submittedJson = await ReadJsonAsync(submitted);
        submittedJson.GetProperty("status").GetString().Should().Be("Submitted");
        (await WithDbAsync(db => db.Notifications.AsNoTracking().AnyAsync(item =>
            item.UserId == fixture.ProcurementUserId &&
            item.TemplateCode == "procurement.material-request.submitted" &&
            item.RefEntityId == requestId))).Should().BeTrue();

        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PROCUREMENT"));
        var approved = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-requests/{requestId}/decision",
            new { approved = true, rowVersion = submittedJson.GetProperty("rowVersion").GetString() });
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        var approvedJson = await ReadJsonAsync(approved);
        approvedJson.GetProperty("status").GetString().Should().Be("Approved");
        (await WithDbAsync(db => db.Notifications.AsNoTracking().AnyAsync(item =>
            item.UserId == fixture.ProjectManagerUserId &&
            item.TemplateCode == "procurement.material-request.approved" &&
            item.RefEntityId == requestId))).Should().BeTrue();

        var updateApproved = await SendAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-requests/{requestId}",
            new
            {
                responsibleSiteUserId = fixture.ProjectManagerUserId,
                assignedProcurementUserId = fixture.ProcurementUserId,
                requiredAt = DateTime.UtcNow.AddDays(7),
                rowVersion = approvedJson.GetProperty("rowVersion").GetString(),
                lines = new[] { new { projectBoqLineId = fixture.BoqLineId, requestedQuantity = 40m } },
            });
        updateApproved.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.MaterialRequests.AsNoTracking().SingleAsync(item => item.Id == requestId)))
            .Status.Should().Be(MaterialRequestStatus.Approved);
    }

    [Fact]
    public async Task MaterialRequestCreate_RejectsExcessAllowanceWithoutPersistence()
    {
        var fixture = await CreateMaterialRequestProjectAsync(10m);
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));

        var response = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-requests",
            new
            {
                responsibleSiteUserId = fixture.ProjectManagerUserId,
                assignedProcurementUserId = fixture.ProcurementUserId,
                requiredAt = DateTime.UtcNow.AddDays(2),
                lines = new[] { new { projectBoqLineId = fixture.BoqLineId, requestedQuantity = 11m } },
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.MaterialRequests.CountAsync(item => item.OperationalProjectId == fixture.ProjectId)))
            .Should().Be(0);
    }

    [Fact]
    public async Task MaterialRequestCancel_RequiresReasonAndNotifiesAssignedOwner()
    {
        var fixture = await CreateMaterialRequestProjectAsync(50m);
        var requestId = await WithDbAsync(async db =>
        {
            var request = NewMaterialRequest(fixture.ProjectId, "MR-CANCEL", MaterialRequestStatus.Draft,
                fixture.ProjectManagerUserId, fixture.ProcurementUserId, DateTime.UtcNow.AddDays(2));
            request.Lines.Add(new MaterialRequestLine { ProjectBoqLineId = fixture.BoqLineId, RequestedQuantity = 5m });
            db.MaterialRequests.Add(request);
            await db.SaveChangesAsync();
            return request.Id;
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        var detail = await ReadJsonAsync(await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-requests/{requestId}"));

        var invalid = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-requests/{requestId}/cancel",
            new { reason = "No", rowVersion = detail.GetProperty("rowVersion").GetString() });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.MaterialRequests.AsNoTracking().SingleAsync(item => item.Id == requestId)))
            .Status.Should().Be(MaterialRequestStatus.Draft);

        var cancelled = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-requests/{requestId}/cancel",
            new { reason = "Scope changed", rowVersion = detail.GetProperty("rowVersion").GetString() });
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK, await cancelled.Content.ReadAsStringAsync());
        (await ReadJsonAsync(cancelled)).GetProperty("status").GetString().Should().Be("Cancelled");
        (await WithDbAsync(db => db.Notifications.AsNoTracking().AnyAsync(item =>
            item.UserId == fixture.ProcurementUserId &&
            item.TemplateCode == "procurement.material-request.cancelled" &&
            item.RefEntityId == requestId))).Should().BeTrue();
    }

    [Fact]
    public async Task MaterialRequests_List_ReturnsPostedReceiptAndRemainingBoqQuantities()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        var projectId = await CreateProjectAsync();
        var userId = await WithDbAsync(db => db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
            .Select(user => user.Id).SingleAsync());
        var procurementUserId = await WithDbAsync(db => db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PROCUREMENT"])
            .Select(user => user.Id).SingleAsync());
        await WithDbAsync(async db =>
        {
            var revision = new ProjectBoqRevision
            {
                OperationalProjectId = projectId,
                RevisionNumber = 1,
                Status = ProjectBoqRevisionStatus.Approved,
                PreparedByUserId = userId,
                Lines =
                [
                    new ProjectBoqLine
                    {
                        ItemCode = "MAT-QTY",
                        Description = "Quantity test material",
                        Unit = "kg",
                        ApprovedQuantity = 100m,
                        BudgetUnitPrice = 10m,
                        Amount = 1_000m,
                    },
                ],
            };
            db.ProjectBoqRevisions.Add(revision);
            await db.SaveChangesAsync();
            var boqLineId = revision.Lines.Single().Id;
            var target = NewMaterialRequest(projectId, "MR-QTY-01", MaterialRequestStatus.PartiallyFulfilled,
                userId, procurementUserId, DateTime.UtcNow.AddDays(2));
            target.Lines.Add(new MaterialRequestLine { ProjectBoqLineId = boqLineId, RequestedQuantity = 40m });
            var other = NewMaterialRequest(projectId, "MR-QTY-02", MaterialRequestStatus.Approved,
                userId, procurementUserId, DateTime.UtcNow.AddDays(3));
            other.Lines.Add(new MaterialRequestLine { ProjectBoqLineId = boqLineId, RequestedQuantity = 20m });
            var draft = NewMaterialRequest(projectId, "MR-QTY-DRAFT", MaterialRequestStatus.Draft,
                userId, procurementUserId, DateTime.UtcNow.AddDays(4));
            draft.Lines.Add(new MaterialRequestLine { ProjectBoqLineId = boqLineId, RequestedQuantity = 30m });
            var rejected = NewMaterialRequest(projectId, "MR-QTY-REJECTED", MaterialRequestStatus.Rejected,
                userId, procurementUserId, DateTime.UtcNow.AddDays(5));
            rejected.Lines.Add(new MaterialRequestLine { ProjectBoqLineId = boqLineId, RequestedQuantity = 10m });
            db.MaterialRequests.AddRange(target, other, draft, rejected);
            await db.SaveChangesAsync();
            var postedReceipt = new WarehouseReceipt
            {
                OperationalProjectId = projectId,
                Code = "GRN-QTY-01",
                Status = WarehouseLedgerStatus.Reversed,
                ReceivedByUserId = userId,
                InspectedAt = DateTime.UtcNow,
                PostedAt = DateTime.UtcNow,
                PostedByUserId = userId,
                Lines = [new WarehouseReceiptLine { MaterialRequestLineId = target.Lines.Single().Id, ReceivedQuantity = 15m }],
            };
            db.WarehouseReceipts.Add(postedReceipt);
            await db.SaveChangesAsync();
            db.WarehouseReceipts.Add(new WarehouseReceipt
            {
                OperationalProjectId = projectId,
                Code = "GRN-QTY-REVERSAL",
                Status = WarehouseLedgerStatus.Posted,
                ReversalOfReceiptId = postedReceipt.Id,
                ReceivedByUserId = userId,
                InspectedAt = DateTime.UtcNow,
                PostedAt = DateTime.UtcNow,
                PostedByUserId = userId,
                ReversalReason = "Receipt entered in error",
                Lines = [new WarehouseReceiptLine { MaterialRequestLineId = target.Lines.Single().Id, ReceivedQuantity = 15m }],
            });
            await db.SaveChangesAsync();
        });

        var response = await Client.GetAsync(
            $"/api/operational-projects/{projectId}/procurement/material-requests?search=MR-QTY-01");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        var line = body.GetProperty("items")[0].GetProperty("lines")[0];
        line.GetProperty("requestedQuantity").GetDecimal().Should().Be(40m);
        line.GetProperty("receivedQuantity").GetDecimal().Should().Be(0m);
        line.GetProperty("boqApprovedQuantity").GetDecimal().Should().Be(100m);
        line.GetProperty("boqRemainingQuantity").GetDecimal().Should().Be(40m);
        var boqContextLine = body.GetProperty("currentApprovedBoq").GetProperty("lines")[0];
        boqContextLine.GetProperty("itemCode").GetString().Should().Be("MAT-QTY");
        boqContextLine.GetProperty("approvedQuantity").GetDecimal().Should().Be(100m);
        boqContextLine.GetProperty("remainingQuantity").GetDecimal().Should().Be(40m);
        boqContextLine.TryGetProperty("budgetUnitPrice", out _).Should().BeFalse();
    }

    [Fact]
    public async Task MaterialRequests_List_WithMrOnlyPermission_ReturnsBoqContextWithoutBoqAccess()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var phone = $"06{Random.Shared.Next(10000000, 99999999)}";
        var fixture = await WithDbAsync(async db =>
        {
            var role = new Role
            {
                Code = $"MR_ONLY_{suffix}",
                Name = $"MR only {suffix}",
                IsActive = true,
                InitialPermissionsSeeded = true,
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            var permissionIds = await db.Permissions
                .Where(permission => permission.Module == "proc.material-requests" &&
                    (permission.Action == "view" || permission.Action == "manage"))
                .Select(permission => permission.Id)
                .ToListAsync();
            db.RolePermissions.AddRange(permissionIds.Select(permissionId =>
                new RolePermission { RoleId = role.Id, PermissionId = permissionId }));
            var user = new ApplicationUser
            {
                PhoneNumber = phone,
                FullName = "MR only tester",
                Email = $"mr-only-{suffix}@nihome.test",
                Role = UserRole.USER,
                RoleEntityId = role.Id,
                IsActive = true,
            };
            user.PasswordHash = new PasswordService().Hash(user, TestDataSeeder.DefaultPassword);
            db.Users.Add(user);
            var customer = new Customer { Type = CustomerType.Company, Name = "MR permission customer", SourceCode = "referral" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-MR-{suffix}",
                Name = "MR Permission Project",
                CustomerId = customer.Id,
                ProjectManagerUserId = user.Id,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            var revision = new ProjectBoqRevision
            {
                OperationalProjectId = project.Id,
                RevisionNumber = 1,
                Status = ProjectBoqRevisionStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                PreparedByUserId = user.Id,
                Lines = [new ProjectBoqLine
                {
                    ItemCode = "MR-CONTEXT",
                    Description = "Context material",
                    Unit = "item",
                    ApprovedQuantity = 25m,
                    BudgetUnitPrice = 999m,
                    Amount = 24_975m,
                }],
            };
            db.ProjectBoqRevisions.Add(revision);
            await db.SaveChangesAsync();
            var request = NewMaterialRequest(project.Id, $"MR-{suffix}", MaterialRequestStatus.Draft,
                user.Id, user.Id, DateTime.UtcNow.AddDays(2));
            request.Lines.Add(new MaterialRequestLine
            {
                ProjectBoqLineId = revision.Lines.Single().Id,
                RequestedQuantity = 5m,
            });
            db.MaterialRequests.Add(request);
            await db.SaveChangesAsync();
            return (ProjectId: project.Id, RequestId: request.Id);
        });
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsync(client, phone, TestDataSeeder.DefaultPassword));

        var list = await Client.GetAsync($"/api/operational-projects/{fixture.ProjectId}/procurement/material-requests");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var contextLine = (await ReadJsonAsync(list)).GetProperty("currentApprovedBoq").GetProperty("lines")[0];
        contextLine.GetProperty("itemCode").GetString().Should().Be("MR-CONTEXT");
        contextLine.GetProperty("remainingQuantity").GetDecimal().Should().Be(25m);
        contextLine.TryGetProperty("budgetUnitPrice", out _).Should().BeFalse();
        var detail = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-requests/{fixture.RequestId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailJson = await ReadJsonAsync(detail);
        detailJson.GetProperty("operationalProjectCode").GetString().Should().StartWith("PJ-MR-");
        detailJson.TryGetProperty("costTotal", out _).Should().BeFalse();
        (await Client.GetAsync($"/api/operational-projects/{fixture.ProjectId}/procurement/boq-revisions"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WarehouseTransactions_ListFiltersAndReturnsProjectStock()
    {
        var fixture = await CreateWarehouseProjectAsync();
        var other = await CreateWarehouseProjectAsync();
        await WithDbAsync(async db =>
        {
            db.WarehouseReceipts.AddRange(
                new WarehouseReceipt
                {
                    OperationalProjectId = fixture.ProjectId,
                    Code = "WR-MATCH-01",
                    Status = WarehouseLedgerStatus.Posted,
                    ReceivedByUserId = fixture.WarehouseUserId,
                    InspectedAt = new DateTime(2026, 9, 5, 8, 0, 0, DateTimeKind.Utc),
                    PostedAt = new DateTime(2026, 9, 5, 9, 0, 0, DateTimeKind.Utc),
                    PostedByUserId = fixture.WarehouseUserId,
                    Lines = [new WarehouseReceiptLine { MaterialRequestLineId = fixture.MaterialRequestLineId, ReceivedQuantity = 60m }],
                },
                new WarehouseReceipt
                {
                    OperationalProjectId = other.ProjectId,
                    Code = "WR-MATCH-OTHER",
                    Status = WarehouseLedgerStatus.Posted,
                    ReceivedByUserId = other.WarehouseUserId,
                    InspectedAt = new DateTime(2026, 9, 6, 8, 0, 0, DateTimeKind.Utc),
                    PostedAt = new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Utc),
                    PostedByUserId = other.WarehouseUserId,
                    Lines = [new WarehouseReceiptLine { MaterialRequestLineId = other.MaterialRequestLineId, ReceivedQuantity = 90m }],
                });
            db.WarehouseIssues.Add(new WarehouseIssue
            {
                OperationalProjectId = fixture.ProjectId,
                Code = "WI-MATCH-01",
                Status = WarehouseLedgerStatus.Posted,
                ResponsibleSiteUserId = fixture.ProjectManagerUserId,
                IssuedByUserId = fixture.WarehouseUserId,
                IssuedAt = new DateTime(2026, 9, 7, 8, 0, 0, DateTimeKind.Utc),
                PostedAt = new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc),
                PostedByUserId = fixture.WarehouseUserId,
                WorkItemCode = "FOUNDATION-A",
                Lines = [new WarehouseIssueLine { ProjectBoqLineId = fixture.BoqLineId, IssuedQuantity = 20m }],
            });
            await db.SaveChangesAsync();
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "WAREHOUSE"));

        var response = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/warehouse-transactions" +
            "?search=MATCH&status=Posted&sortBy=occurredAt&sortDirection=desc&page=1&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonAsync(response);
        json.GetProperty("total").GetInt32().Should().Be(2);
        json.GetProperty("items")[0].GetProperty("code").GetString().Should().Be("WI-MATCH-01");
        json.GetProperty("items")[0].GetProperty("operationalProjectId").GetInt32().Should().Be(fixture.ProjectId);
        var stock = json.GetProperty("stock").EnumerateArray().Single(item => item.GetProperty("itemCode").GetString() == "MAT-MR");
        stock.GetProperty("receivedQuantity").GetDecimal().Should().Be(60m);
        stock.GetProperty("issuedQuantity").GetDecimal().Should().Be(20m);
        stock.GetProperty("onHandQuantity").GetDecimal().Should().Be(40m);
        json.ToString().Should().NotContain("WR-MATCH-OTHER");

        var receiptsOnly = await ReadJsonAsync(await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/warehouse-transactions?type=receipt&search=MATCH"));
        receiptsOnly.GetProperty("total").GetInt32().Should().Be(1);
        receiptsOnly.GetProperty("items")[0].GetProperty("type").GetString().Should().Be("Receipt");
    }

    [Fact]
    public async Task WarehouseDrafts_UpdateDetailAndLockAfterPosting()
    {
        var fixture = await CreateWarehouseProjectAsync();
        var otherProjectId = await CreateProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "WAREHOUSE"));

        var created = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts",
            new
            {
                inspectedAt = DateTime.UtcNow,
                receivedByUserId = fixture.WarehouseUserId,
                lines = new[] { new { materialRequestLineId = fixture.MaterialRequestLineId, receivedQuantity = 10m } },
            });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var createdJson = await ReadJsonAsync(created);
        var receiptId = createdJson.GetProperty("id").GetInt32();

        var updated = await SendAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts/{receiptId}",
            new
            {
                inspectedAt = DateTime.UtcNow.AddMinutes(-1),
                receivedByUserId = fixture.WarehouseUserId,
                rowVersion = createdJson.GetProperty("rowVersion").GetString(),
                lines = new[] { new { materialRequestLineId = fixture.MaterialRequestLineId, receivedQuantity = 15m } },
            });
        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        var updatedJson = await ReadJsonAsync(updated);
        updatedJson.GetProperty("lines")[0].GetProperty("receivedQuantity").GetDecimal().Should().Be(15m);

        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var otherUserId = await WithDbAsync(db => db.Users.Where(user => user.PhoneNumber == "0335240370")
            .Select(user => user.Id).SingleAsync());
        var nonOwnerUpdate = await SendAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts/{receiptId}",
            new
            {
                inspectedAt = DateTime.UtcNow.AddMinutes(-1),
                receivedByUserId = otherUserId,
                rowVersion = updatedJson.GetProperty("rowVersion").GetString(),
                lines = new[] { new { materialRequestLineId = fixture.MaterialRequestLineId, receivedQuantity = 20m } },
            });
        nonOwnerUpdate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.WarehouseReceiptLines.AsNoTracking()
            .SingleAsync(item => item.WarehouseReceiptId == receiptId))).ReceivedQuantity.Should().Be(15m);

        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "WAREHOUSE"));

        var detail = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts/{receiptId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailJson = await ReadJsonAsync(detail);
        detailJson.GetProperty("operationalProjectCode").GetString().Should().StartWith("PJ-MR-");
        detailJson.GetProperty("customerName").GetString().Should().Be("MR Lifecycle Customer");
        detailJson.GetProperty("receivedByName").GetString().Should().NotBeNullOrWhiteSpace();
        detailJson.GetProperty("lines")[0].GetProperty("materialRequestCode").GetString().Should().Be("MR-WAREHOUSE");
        detailJson.GetProperty("lines")[0].GetProperty("requestedQuantity").GetDecimal().Should().Be(100m);
        (await Client.GetAsync($"/api/operational-projects/{otherProjectId}/procurement/receipts/{receiptId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var posted = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts/{receiptId}/post",
            new { rowVersion = updatedJson.GetProperty("rowVersion").GetString() });
        posted.StatusCode.Should().Be(HttpStatusCode.OK, await posted.Content.ReadAsStringAsync());
        var postedJson = await ReadJsonAsync(posted);
        postedJson.GetProperty("status").GetString().Should().Be("Posted");

        var rejectedUpdate = await SendAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts/{receiptId}",
            new
            {
                inspectedAt = DateTime.UtcNow.AddHours(2),
                receivedByUserId = fixture.WarehouseUserId,
                rowVersion = postedJson.GetProperty("rowVersion").GetString(),
                lines = new[] { new { materialRequestLineId = fixture.MaterialRequestLineId, receivedQuantity = 25m } },
            });
        rejectedUpdate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.WarehouseReceiptLines.AsNoTracking()
            .SingleAsync(item => item.WarehouseReceiptId == receiptId))).ReceivedQuantity.Should().Be(15m);
    }

    [Fact]
    public async Task WarehouseLifecycle_BlocksNegativeStockAndReversesSafely()
    {
        var fixture = await CreateWarehouseProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "WAREHOUSE"));

        var futureReceipt = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts",
            new
            {
                inspectedAt = DateTime.UtcNow.AddHours(1),
                receivedByUserId = fixture.WarehouseUserId,
                lines = new[] { new { materialRequestLineId = fixture.MaterialRequestLineId, receivedQuantity = 10m } },
            });
        futureReceipt.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var excessiveReceipt = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts",
            new
            {
                inspectedAt = DateTime.UtcNow,
                receivedByUserId = fixture.WarehouseUserId,
                lines = new[] { new { materialRequestLineId = fixture.MaterialRequestLineId, receivedQuantity = 101m } },
            });
        excessiveReceipt.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.WarehouseReceipts.CountAsync(item => item.OperationalProjectId == fixture.ProjectId)))
            .Should().Be(0);

        async Task<System.Text.Json.JsonElement> CreateAndPostReceipt(decimal quantity)
        {
            var created = await SendAsync(HttpMethod.Post,
                $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts",
                new
                {
                    inspectedAt = DateTime.UtcNow,
                    receivedByUserId = fixture.WarehouseUserId,
                    lines = new[] { new { materialRequestLineId = fixture.MaterialRequestLineId, receivedQuantity = quantity } },
                });
            var draft = await ReadJsonAsync(created);
            var posted = await SendAsync(HttpMethod.Post,
                $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts/{draft.GetProperty("id").GetInt32()}/post",
                new { rowVersion = draft.GetProperty("rowVersion").GetString() });
            posted.StatusCode.Should().Be(HttpStatusCode.OK, await posted.Content.ReadAsStringAsync());
            return await ReadJsonAsync(posted);
        }

        var receipt = await CreateAndPostReceipt(60m);
        (await WithDbAsync(db => db.MaterialRequests.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.MaterialRequestId))).Status.Should().Be(MaterialRequestStatus.PartiallyFulfilled);
        var excessiveIssue = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/issues",
            new
            {
                issuedAt = DateTime.UtcNow,
                responsibleSiteUserId = fixture.ProjectManagerUserId,
                issuedByUserId = fixture.WarehouseUserId,
                workItemCode = "CREW-EXCESS",
                lines = new[] { new { projectBoqLineId = fixture.BoqLineId, issuedQuantity = 61m } },
            });
        excessiveIssue.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.WarehouseIssues.CountAsync(item => item.OperationalProjectId == fixture.ProjectId)))
            .Should().Be(0);
        var issueDraftResponse = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/issues",
            new
            {
                issuedAt = DateTime.UtcNow,
                responsibleSiteUserId = fixture.ProjectManagerUserId,
                issuedByUserId = fixture.WarehouseUserId,
                workItemCode = "CREW-A",
                lines = new[] { new { projectBoqLineId = fixture.BoqLineId, issuedQuantity = 25m } },
            });
        issueDraftResponse.StatusCode.Should().Be(HttpStatusCode.Created, await issueDraftResponse.Content.ReadAsStringAsync());
        var issueDraft = await ReadJsonAsync(issueDraftResponse);
        var issueDetail = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/issues/{issueDraft.GetProperty("id").GetInt32()}");
        issueDetail.StatusCode.Should().Be(HttpStatusCode.OK);
        var issueDetailJson = await ReadJsonAsync(issueDetail);
        issueDetailJson.GetProperty("operationalProjectCode").GetString().Should().StartWith("PJ-MR-");
        issueDetailJson.GetProperty("lines")[0].GetProperty("stockOnHand").GetDecimal().Should().Be(60m);
        var issueUpdatedResponse = await SendAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/issues/{issueDraft.GetProperty("id").GetInt32()}",
            new
            {
                issuedAt = DateTime.UtcNow.AddMinutes(-1),
                responsibleSiteUserId = fixture.ProjectManagerUserId,
                issuedByUserId = fixture.WarehouseUserId,
                workItemCode = "CREW-B",
                rowVersion = issueDraft.GetProperty("rowVersion").GetString(),
                lines = new[] { new { projectBoqLineId = fixture.BoqLineId, issuedQuantity = 20m } },
            });
        issueUpdatedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await issueUpdatedResponse.Content.ReadAsStringAsync());
        var issueUpdated = await ReadJsonAsync(issueUpdatedResponse);
        issueUpdated.GetProperty("workItemCode").GetString().Should().Be("CREW-B");
        issueUpdated.GetProperty("lines")[0].GetProperty("issuedQuantity").GetDecimal().Should().Be(20m);
        var issuePostedResponse = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/issues/{issueDraft.GetProperty("id").GetInt32()}/post",
            new { rowVersion = issueUpdated.GetProperty("rowVersion").GetString() });
        issuePostedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await issuePostedResponse.Content.ReadAsStringAsync());
        var issuePosted = await ReadJsonAsync(issuePostedResponse);

        var blockedReceiptReverse = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts/{receipt.GetProperty("id").GetInt32()}/reverse",
            new { reason = "Receipt entered in error", rowVersion = receipt.GetProperty("rowVersion").GetString() });
        blockedReceiptReverse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.WarehouseReceipts.AsNoTracking()
            .SingleAsync(item => item.Id == receipt.GetProperty("id").GetInt32()))).Status.Should().Be(WarehouseLedgerStatus.Posted);

        var reversedIssue = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/issues/{issuePosted.GetProperty("id").GetInt32()}/reverse",
            new { reason = "Wrong allocation", rowVersion = issuePosted.GetProperty("rowVersion").GetString() });
        reversedIssue.StatusCode.Should().Be(HttpStatusCode.OK, await reversedIssue.Content.ReadAsStringAsync());
        (await WithDbAsync(db => db.MaterialRequests.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.MaterialRequestId))).Status.Should().Be(MaterialRequestStatus.PartiallyFulfilled);
        var reversedIssueJson = await ReadJsonAsync(reversedIssue);
        var originalIssueDetail = await ReadJsonAsync(await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/issues/{issuePosted.GetProperty("id").GetInt32()}"));
        originalIssueDetail.GetProperty("reversalTransactionId").GetInt32()
            .Should().Be(reversedIssueJson.GetProperty("id").GetInt32());
        originalIssueDetail.GetProperty("reversalReason").GetString().Should().Be("Wrong allocation");
        var reversedReceipt = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts/{receipt.GetProperty("id").GetInt32()}/reverse",
            new { reason = "Receipt entered in error", rowVersion = receipt.GetProperty("rowVersion").GetString() });
        reversedReceipt.StatusCode.Should().Be(HttpStatusCode.OK, await reversedReceipt.Content.ReadAsStringAsync());
        var reversedReceiptJson = await ReadJsonAsync(reversedReceipt);
        var originalReceiptDetail = await ReadJsonAsync(await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts/{receipt.GetProperty("id").GetInt32()}"));
        originalReceiptDetail.GetProperty("reversalTransactionId").GetInt32()
            .Should().Be(reversedReceiptJson.GetProperty("id").GetInt32());
        originalReceiptDetail.GetProperty("reversalReason").GetString().Should().Be("Receipt entered in error");

        var list = await ReadJsonAsync(await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/warehouse-transactions"));
        var stock = list.GetProperty("stock").EnumerateArray().Single(item => item.GetProperty("itemCode").GetString() == "MAT-MR");
        stock.GetProperty("onHandQuantity").GetDecimal().Should().Be(0m);
        (await WithDbAsync(db => db.MaterialRequests.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.MaterialRequestId))).Status.Should().Be(MaterialRequestStatus.Approved);
    }

    [Fact]
    public async Task WarehouseReceipts_TransitionMaterialRequestFulfillmentAndReversal()
    {
        var fixture = await CreateWarehouseProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "WAREHOUSE"));

        async Task<System.Text.Json.JsonElement> CreateAndPostAsync(decimal quantity)
        {
            var created = await SendAsync(HttpMethod.Post,
                $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts",
                new
                {
                    inspectedAt = DateTime.UtcNow,
                    receivedByUserId = fixture.WarehouseUserId,
                    lines = new[] { new { materialRequestLineId = fixture.MaterialRequestLineId, receivedQuantity = quantity } },
                });
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var draft = await ReadJsonAsync(created);
            var posted = await SendAsync(HttpMethod.Post,
                $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts/{draft.GetProperty("id").GetInt32()}/post",
                new { rowVersion = draft.GetProperty("rowVersion").GetString() });
            posted.StatusCode.Should().Be(HttpStatusCode.OK, await posted.Content.ReadAsStringAsync());
            return await ReadJsonAsync(posted);
        }

        async Task ReverseAsync(System.Text.Json.JsonElement receipt, string reason)
        {
            var reversed = await SendAsync(HttpMethod.Post,
                $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts/{receipt.GetProperty("id").GetInt32()}/reverse",
                new { reason, rowVersion = receipt.GetProperty("rowVersion").GetString() });
            reversed.StatusCode.Should().Be(HttpStatusCode.OK, await reversed.Content.ReadAsStringAsync());
        }

        var first = await CreateAndPostAsync(40m);
        (await WithDbAsync(db => db.MaterialRequests.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.MaterialRequestId))).Status.Should().Be(MaterialRequestStatus.PartiallyFulfilled);

        var second = await CreateAndPostAsync(60m);
        var fulfilled = await WithDbAsync(db => db.MaterialRequests.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.MaterialRequestId));
        fulfilled.Status.Should().Be(MaterialRequestStatus.Fulfilled);
        fulfilled.FulfilledAt.Should().NotBeNull();

        await ReverseAsync(second, "Second delivery was duplicated");
        var partial = await WithDbAsync(db => db.MaterialRequests.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.MaterialRequestId));
        partial.Status.Should().Be(MaterialRequestStatus.PartiallyFulfilled);
        partial.FulfilledAt.Should().BeNull();

        await ReverseAsync(first, "First delivery was duplicated");
        (await WithDbAsync(db => db.MaterialRequests.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.MaterialRequestId))).Status.Should().Be(MaterialRequestStatus.Approved);
    }

    [Fact]
    public async Task VendorRating_ComponentAboveOneHundred_IsRejectedByApiContract()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();

        var response = await SendAsync(HttpMethod.Post, $"/api/operational-projects/{projectId}/procurement/vendor-ratings", new
        {
            contractId = 1,
            qualityScore = 101m,
            scheduleScore = 80m,
            costScore = 80m,
            hseScore = 80m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.VendorRatings.CountAsync())).Should().Be(0);
    }

    private async Task<int> CreateProjectAsync() => await WithDbAsync(async db =>
    {
        var pmUserId = await db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
            .Select(user => user.Id).SingleAsync();
        var customer = new Customer { Type = CustomerType.Company, Name = "Procurement Customer", SourceCode = "referral" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var project = new OperationalProject
        {
            Code = $"PJ-PROC-{Guid.NewGuid():N}"[..30],
            Name = "Procurement Test Project",
            CustomerId = customer.Id,
            ProjectManagerUserId = pmUserId,
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    });

    private async Task<(int ProjectId, int ProjectManagerUserId, int ProcurementUserId, int BoqLineId)>
        CreateMaterialRequestProjectAsync(decimal approvedQuantity) => await WithDbAsync(async db =>
        {
            var projectManagerUserId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
                .Select(user => user.Id).SingleAsync();
            var procurementUserId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PROCUREMENT"])
                .Select(user => user.Id).SingleAsync();
            var customer = new Customer { Type = CustomerType.Company, Name = "MR Lifecycle Customer", SourceCode = "referral" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-MR-{Guid.NewGuid():N}"[..30],
                Name = "MR Lifecycle Project",
                CustomerId = customer.Id,
                ProjectManagerUserId = projectManagerUserId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = project.Id,
                UserId = procurementUserId,
                Position = "Procurement",
                StartedAt = DateTime.UtcNow,
                CreatedByUserId = projectManagerUserId,
                UpdatedByUserId = projectManagerUserId,
            });
            var revision = new ProjectBoqRevision
            {
                OperationalProjectId = project.Id,
                RevisionNumber = 1,
                Status = ProjectBoqRevisionStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                PreparedByUserId = projectManagerUserId,
                Lines = [new ProjectBoqLine
                {
                    ItemCode = "MAT-MR",
                    Description = "Lifecycle material",
                    Unit = "item",
                    ApprovedQuantity = approvedQuantity,
                    BudgetUnitPrice = 100m,
                    Amount = approvedQuantity * 100m,
                }],
            };
            db.ProjectBoqRevisions.Add(revision);
            await db.SaveChangesAsync();
            return (project.Id, projectManagerUserId, procurementUserId, revision.Lines.Single().Id);
        });

    private async Task<(int ProjectId, int ProjectManagerUserId, int WarehouseUserId, int BoqLineId,
        int MaterialRequestId, int MaterialRequestLineId)> CreateWarehouseProjectAsync()
    {
        var fixture = await CreateMaterialRequestProjectAsync(100m);
        return await WithDbAsync(async db =>
        {
            var warehouseUserId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["WAREHOUSE"])
                .Select(user => user.Id).SingleAsync();
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = fixture.ProjectId,
                UserId = warehouseUserId,
                Position = "WAREHOUSE",
                StartedAt = DateTime.UtcNow,
                CreatedByUserId = fixture.ProjectManagerUserId,
                UpdatedByUserId = fixture.ProjectManagerUserId,
            });
            var request = NewMaterialRequest(fixture.ProjectId, "MR-WAREHOUSE", MaterialRequestStatus.Approved,
                fixture.ProjectManagerUserId, fixture.ProcurementUserId, DateTime.UtcNow.AddDays(2));
            request.ApprovedAt = DateTime.UtcNow;
            request.Lines.Add(new MaterialRequestLine
            {
                ProjectBoqLineId = fixture.BoqLineId,
                RequestedQuantity = 100m,
            });
            db.MaterialRequests.Add(request);
            await db.SaveChangesAsync();
            return (fixture.ProjectId, fixture.ProjectManagerUserId, warehouseUserId, fixture.BoqLineId,
                request.Id, request.Lines.Single().Id);
        });
    }

    private static MaterialRequest NewMaterialRequest(
        int projectId,
        string code,
        MaterialRequestStatus status,
        int siteUserId,
        int procurementUserId,
        DateTime requiredAt) => new()
        {
            OperationalProjectId = projectId,
            Code = code,
            Status = status,
            SiteRequesterUserId = siteUserId,
            ResponsibleSiteUserId = siteUserId,
            AssignedProcurementUserId = procurementUserId,
            RequiredAt = requiredAt,
        };

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }
}