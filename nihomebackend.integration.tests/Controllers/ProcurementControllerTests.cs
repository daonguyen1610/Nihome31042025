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
        var projectId = await WithDbAsync(async db =>
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
            db.ProjectBoqRevisions.Add(new ProjectBoqRevision
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
            });
            await db.SaveChangesAsync();
            return project.Id;
        });
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsync(client, phone, TestDataSeeder.DefaultPassword));

        var list = await Client.GetAsync($"/api/operational-projects/{projectId}/procurement/material-requests");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var contextLine = (await ReadJsonAsync(list)).GetProperty("currentApprovedBoq").GetProperty("lines")[0];
        contextLine.GetProperty("itemCode").GetString().Should().Be("MR-CONTEXT");
        contextLine.GetProperty("remainingQuantity").GetDecimal().Should().Be(25m);
        contextLine.TryGetProperty("budgetUnitPrice", out _).Should().BeFalse();
        (await Client.GetAsync($"/api/operational-projects/{projectId}/procurement/boq-revisions"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
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