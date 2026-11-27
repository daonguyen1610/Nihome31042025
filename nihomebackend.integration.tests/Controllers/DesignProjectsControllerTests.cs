using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Constants;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>DesignProjectsController</c> (NIH-113):
/// RBAC gating, list + get, CRUD happy paths + the auto-create hook
/// fired by <c>ContractsController</c> transitions.
/// </summary>
public class DesignProjectsControllerTests : IntegrationTestBase
{
    public DesignProjectsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/design-projects")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsWarehouse_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/design-projects")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsPm_ReturnsOk()
    {
        // PM has design.projects.view (read-only).
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));
        var res = await Client.GetAsync("/api/design-projects");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task DirectDesignAssignees_CanUseLinkedProjectWithoutOperationalMembership()
    {
        var fixture = await WithDbAsync(async db =>
        {
            var manager = await db.Users.SingleAsync(user =>
                user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["DESIGN_MANAGER"]);
            var lead = await db.Users.SingleAsync(user =>
                user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["DESIGN_LEAD"]);
            var customer = new Customer
            {
                Name = $"Chủ đầu tư phân công Chủ nhiệm {Guid.NewGuid():N}",
                Type = CustomerType.Company,
                SourceCode = "referral",
            };
            var operational = new OperationalProject
            {
                Code = $"OP-DM-{Guid.NewGuid():N}"[..30].ToUpperInvariant(),
                Name = "Dự án do PM toàn dự án quản lý",
                Customer = customer,
            };
            var design = new DesignProject
            {
                ProjectCode = $"DP-DM-{Guid.NewGuid():N}"[..30].ToUpperInvariant(),
                Name = "Hồ sơ thiết kế do Chủ nhiệm thiết kế phụ trách",
                Customer = customer,
                OperationalProject = operational,
                ProjectManagerUserId = manager.Id,
                DesignLeadUserId = lead.Id,
                CurrentStage = DesignProjectStage.Concept,
                Status = DesignProjectStatus.Active,
            };
            db.Add(design);
            await db.SaveChangesAsync();
            var basic = new BasicDesignDoc
            {
                DesignProjectId = design.Id,
                DisciplineCode = "ARCH",
                DocumentCode = "KT-BD-DIRECT",
                Title = "Hồ sơ cơ sở cho Chủ nhiệm trực tiếp",
            };
            var shop = new ShopDrawing
            {
                DesignProjectId = design.Id,
                DisciplineCode = "ARCH",
                DrawingCode = "KT-SD-DIRECT",
                Title = "Bản vẽ thi công cho Chủ trì trực tiếp",
                ConstructionItem = "Kiến trúc",
            };
            db.AddRange(basic, shop);
            db.ProjectDocuments.AddRange(
                new ProjectDocument
                {
                    OperationalProjectId = operational.Id,
                    Category = ProjectDocumentCategory.DesignBasic,
                    SourceModule = ProjectDocumentSourceModule.Design,
                    SourceType = ProjectDocumentSourceType.ExistingManagedFile,
                    LocalPath = "/files/business-documents/basic-design/direct-manager.pdf",
                    OriginalFileName = "direct-manager.pdf",
                    Sha256 = new string('a', 64),
                    SyncStatus = ProjectDocumentSyncStatus.Synced,
                },
                new ProjectDocument
                {
                    OperationalProjectId = operational.Id,
                    Category = ProjectDocumentCategory.CrmPreDesign,
                    SourceModule = ProjectDocumentSourceModule.Crm,
                    SourceType = ProjectDocumentSourceType.ExistingManagedFile,
                    LocalPath = "/files/business-documents/crm/private-commercial.pdf",
                    OriginalFileName = "private-commercial.pdf",
                    Sha256 = new string('b', 64),
                    SyncStatus = ProjectDocumentSyncStatus.Synced,
                });
            await db.SaveChangesAsync();
            return (DesignId: design.Id, OperationalId: operational.Id,
                BasicId: basic.Id, ShopId: shop.Id);
        });
        await AuthTestHelper.AuthenticateAsync(Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN_MANAGER"));

        using var list = await Client.GetAsync("/api/design-projects?pageSize=100");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(list)).GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetInt32() == fixture.DesignId);
        (await Client.GetAsync($"/api/design-projects/{fixture.DesignId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var documentsResponse = await Client.GetAsync(
            $"/api/operational-projects/{fixture.OperationalId}/documents");
        documentsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var documents = (await ReadJsonAsync(documentsResponse)).EnumerateArray().ToList();
        documents.Should().Contain(item =>
            item.GetProperty("originalFileName").GetString() == "direct-manager.pdf");
        documents.Should().NotContain(item =>
            item.GetProperty("originalFileName").GetString() == "private-commercial.pdf");

        var basicResponse = await Client.GetAsync(
            $"/api/basic-design-docs?designProjectId={fixture.DesignId}");
        basicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(basicResponse)).GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetInt32() == fixture.BasicId);
        (await Client.GetAsync($"/api/basic-design-docs/{fixture.BasicId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var shopResponse = await Client.GetAsync(
            $"/api/shop-drawings?designProjectId={fixture.DesignId}");
        shopResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(shopResponse)).GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetInt32() == fixture.ShopId);
        (await Client.GetAsync($"/api/shop-drawings/{fixture.ShopId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync($"/api/design-projects/{fixture.DesignId}/reviews"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync(
            $"/api/design-projects/{fixture.DesignId}/reviews/recipient-candidates"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await AuthTestHelper.AuthenticateAsync(Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN_LEAD"));
        var leadDocumentsResponse = await Client.GetAsync(
            $"/api/operational-projects/{fixture.OperationalId}/documents");
        leadDocumentsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var leadDocuments = await ReadJsonAsync(leadDocumentsResponse);
        leadDocuments.EnumerateArray().Should().Contain(item =>
            item.GetProperty("originalFileName").GetString() == "direct-manager.pdf");
        leadDocuments.EnumerateArray().Should().NotContain(item =>
            item.GetProperty("originalFileName").GetString() == "private-commercial.pdf");
        (await Client.GetAsync($"/api/basic-design-docs/{fixture.BasicId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync($"/api/shop-drawings/{fixture.ShopId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Pm_CannotCreate()
    {
        // PM has view but not manage.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));
        var customerId = await FirstCustomerIdAsync();
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = "PM blocked create",
            customerId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Pm_CannotPreviewDeletionImpact()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Restricted deletion preview");
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));

        var response = await Client.GetAsync($"/api/design-projects/{id}/deletion-impact");

        // Deletion is project-scoped now; a non-member is masked as 404.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await WithDbAsync(db => db.DesignProjects.AnyAsync(project => project.Id == id)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task MultipleDesignFlows_ShareOperationalProjectWithoutReplacingEachOther()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var ids = new List<int>();
        foreach (var name in new[] { "NICON - Thiết kế nhà xưởng", "NICON - Thiết kế văn phòng" })
        {
            var created = await Client.PostAsJsonAsync("/api/design-projects", new
            {
                name,
                customerId,
                operationalProjectId,
            });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            ids.Add((await ReadJsonAsync(created)).GetProperty("id").GetInt32());
        }
        ids.Distinct().Should().HaveCount(2);
        var project = await Client.GetAsync($"/api/operational-projects/{operationalProjectId}");
        project.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(project);
        body.GetProperty("designProjects").EnumerateArray().Select(item => item.GetProperty("id").GetInt32())
            .Should().BeEquivalentTo(ids);
        body.GetProperty("designProjectId").GetInt32().Should().Be(ids.Min());
        var documentIds = await WithDbAsync(async db =>
        {
            var result = new List<long>();
            foreach (var designId in ids)
            {
                var source = new BasicDesignDoc
                {
                    DesignProjectId = designId,
                    DocumentCode = $"NICON-BD-{designId}",
                    Title = "Hồ sơ cơ sở độc lập theo luồng",
                    DisciplineCode = "ARCH",
                };
                db.BasicDesignDocs.Add(source);
                await db.SaveChangesAsync();
                var document = new ProjectDocument
                {
                    OperationalProjectId = operationalProjectId,
                    Category = ProjectDocumentCategory.DesignBasic,
                    SourceModule = ProjectDocumentSourceModule.Design,
                    SourceType = ProjectDocumentSourceType.ManualUpload,
                    SourceEntityType = nameof(BasicDesignDoc),
                    SourceRecordId = source.Id,
                    OriginalFileName = $"nicon-design-{designId}.pdf",
                    LocalPath = $"/files/project-documents/{operationalProjectId}/{designId}.pdf",
                    Sha256 = new string('a', 64),
                    SyncStatus = ProjectDocumentSyncStatus.Pending,
                };
                db.ProjectDocuments.Add(document);
                await db.SaveChangesAsync();
                result.Add(document.Id);
            }
            return result;
        });
        for (var index = 0; index < ids.Count; index++)
        {
            var documents = await Client.GetAsync($"/api/operational-projects/{operationalProjectId}/documents?designProjectId={ids[index]}");
            documents.StatusCode.Should().Be(HttpStatusCode.OK);
            (await ReadJsonAsync(documents)).EnumerateArray().Select(item => item.GetProperty("id").GetInt64())
                .Should().Equal(documentIds[index]);
        }
        (await Client.GetAsync($"/api/operational-projects/{operationalProjectId}/documents?designProjectId=2147483647"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreach (var id in ids)
        {
            var design = await Client.GetAsync($"/api/design-projects/{id}");
            design.StatusCode.Should().Be(HttpStatusCode.OK);
            (await ReadJsonAsync(design)).GetProperty("operationalProjectId").GetInt32().Should().Be(operationalProjectId);
        }
    }

    [Fact]
    public async Task Create_HappyPath_AsSuperAdmin_ReturnsAutoCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"DP integ {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("projectCode").GetString().Should().StartWith("DP-");
        body.GetProperty("currentStage").GetString().Should().Be("Concept");
        body.GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task Create_WithoutProjectOrContract_IsBadRequestWithActionableMessage()
    {
        // Used to be a silent 404 that the dialog swallowed (scenario B report).
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var name = $"Thiết kế trước {Guid.NewGuid():N}";
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name,
            customerId = await FirstCustomerIdAsync(),
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(res)).GetProperty("message").GetString().Should().Contain("Dự án");
        (await WithDbAsync(db => db.DesignProjects.AnyAsync(project => project.Name == name))).Should().BeFalse();
    }

    [Fact]
    public async Task DesignFirst_DesignLeadCannotCreateEvenWhenAssignedToProject()
    {
        // Sales owns the handoff from Opportunity. Design receives the Concept
        // after assignment but cannot create a new business root itself.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var (customerId, projectId) = await ConvertLeadAsync("Nhà máy Minh Phúc giai đoạn 2");
        var (_, otherProjectId) = await ConvertLeadAsync("Kho lạnh Hậu Giang");
        var designLeadId = await WithDbAsync(db => db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["DESIGN_LEAD"])
            .Select(user => user.Id)
            .SingleAsync());

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN_LEAD"));
        (await CreateDesignAsync(customerId, projectId)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await AddTeamMemberAsync(otherProjectId, designLeadId, "Architect")).StatusCode.Should().Be(HttpStatusCode.Created);
        (await AddTeamMemberAsync(projectId, designLeadId, "DesignLead")).StatusCode.Should().Be(HttpStatusCode.Created);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN_LEAD"));
        using var notLead = await CreateDesignAsync(customerId, otherProjectId);
        notLead.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadJsonAsync(notLead)).GetProperty("message").GetString().Should().Contain("Kinh doanh");
        (await WithDbAsync(db => db.DesignProjects.AnyAsync(project => project.OperationalProjectId == otherProjectId)))
            .Should().BeFalse();

        using var denied = await CreateDesignAsync(customerId, projectId);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadJsonAsync(denied)).GetProperty("message").GetString().Should().Contain("Kinh doanh");
        (await WithDbAsync(db => db.DesignProjects.AnyAsync(project => project.OperationalProjectId == projectId)))
            .Should().BeFalse();
    }

    [Fact]
    public async Task DesignFirst_DesignLeadWithTeamManagementCannotCreateBusinessHandoff()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var projectId = await CreateOperationalProjectAsync(customerId);
        var phone = $"08{Random.Shared.Next(10_000_000, 99_999_999)}";
        var userId = await WithDbAsync(async db =>
        {
            var role = new Role
            {
                Code = $"DESIGN_CREATE_{Guid.NewGuid():N}"[..30],
                Name = "Design lead with project team management",
                InitialPermissionsSeeded = true,
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            var permissionIds = await db.Permissions
                .Where(permission =>
                    (permission.Module == "design.projects" && permission.Action == "manage") ||
                    (permission.Module == "operations.projects" && permission.Action == "manage"))
                .Select(permission => permission.Id)
                .ToListAsync();
            db.RolePermissions.AddRange(permissionIds.Select(permissionId => new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId,
            }));
            var user = new ApplicationUser
            {
                PhoneNumber = phone,
                FullName = "Design lead for permission boundary",
                Email = $"design-create-{Guid.NewGuid():N}@nihome.test",
                Role = UserRole.USER,
                RoleEntityId = role.Id,
                IsActive = true,
            };
            user.PasswordHash = new PasswordService().Hash(user, TestDataSeeder.DefaultPassword);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        });
        (await AddTeamMemberAsync(projectId, userId, "DesignLead")).StatusCode
            .Should().Be(HttpStatusCode.Created);

        await AuthTestHelper.AuthenticateAsync(Client,
            c => AuthTestHelper.LoginAsync(c, phone, TestDataSeeder.DefaultPassword));
        using var response = await CreateDesignAsync(customerId, projectId);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.DesignProjects.AnyAsync(project =>
            project.OperationalProjectId == projectId))).Should().BeFalse();
    }

    private async Task<(int CustomerId, int ProjectId)> ConvertLeadAsync(string companyName)
    {
        var lead = await SendWithKeyAsync(HttpMethod.Post, "/api/leads", new
        {
            name = $"Đại diện {companyName}",
            phone = "09" + Random.Shared.Next(10_000_000, 99_999_999),
            companyName = $"{companyName} {Guid.NewGuid():N}",
            sourceCode = "marketing",
        });
        lead.StatusCode.Should().Be(HttpStatusCode.Created, await lead.Content.ReadAsStringAsync());
        var leadId = (await ReadJsonAsync(lead)).GetProperty("id").GetInt32();
        var converted = await SendWithKeyAsync(HttpMethod.Post, $"/api/leads/{leadId}/convert", new
        {
            address = "KCN Tân Uyên, Bình Dương",
            representativeName = "Nguyễn Minh Phúc",
        });
        converted.StatusCode.Should().Be(HttpStatusCode.OK, await converted.Content.ReadAsStringAsync());
        var body = await ReadJsonAsync(converted);
        var opportunityId = body.GetProperty("convertedOpportunityId").GetInt32();
        var projectId = await WithDbAsync(db => db.Opportunities
            .Where(item => item.Id == opportunityId)
            .Select(item => item.OperationalProjectId!.Value)
            .SingleAsync());
        return (body.GetProperty("convertedCustomerId").GetInt32(), projectId);
    }

    private Task<HttpResponseMessage> CreateDesignAsync(int customerId, int operationalProjectId) =>
        Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Thiết kế ý tưởng nhà xưởng {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
        });

    private Task<HttpResponseMessage> AddTeamMemberAsync(int projectId, int userId, string roleCode) =>
        SendWithKeyAsync(HttpMethod.Post, $"/api/operational-projects/{projectId}/team/members", new
        {
            userId,
            position = roleCode == "DesignLead" ? "Chủ trì thiết kế" : "Kiến trúc sư",
            startedAt = DateTime.UtcNow.AddDays(-1),
            roles = new[] { new { roleCode, scope = "Project" } },
        });

    private async Task<HttpResponseMessage> SendWithKeyAsync(HttpMethod method, string path, object body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }

    [Fact]
    public async Task Get_UnknownId_Is404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        (await Client.GetAsync("/api/design-projects/9999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ExtraCurrentStage_DoesNotChangeServerOwnedStage()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Update round-trip");
        var operationalProjectId = await WithDbAsync<int>(db => db.DesignProjects
            .Where(project => project.Id == id)
            .Select(project => project.OperationalProjectId!.Value)
            .SingleAsync());
        var res = await Client.PutAsJsonAsync($"/api/design-projects/{id}", new
        {
            name = "Update round-trip",
            customerId,
            operationalProjectId,
            currentStage = "BasicDesign",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("currentStage").GetString().Should().Be("Concept");
    }

    [Fact]
    public async Task Delete_WithoutConfirmationBody_ReturnsClientErrorAndPreservesProject()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Missing confirmation body");

        var delete = await Client.DeleteAsync($"/api/design-projects/{id}");

        ((int)delete.StatusCode).Should().BeInRange(400, 499);
        (await WithDbAsync(db => db.DesignProjects.AnyAsync(project => project.Id == id)))
            .Should().BeTrue();
    }

    // -------- helpers --------

    [Fact]
    public async Task PurchaseOrder_CannotBeLinked_ButAnEarlierLinkStillSaves()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var purchaseOrder = await WithDbAsync<(int Id, string Number)>(async db =>
        {
            var contract = new Contract
            {
                ContractNumber = $"PO-RFQ-{Guid.NewGuid():N}"[..20],
                CustomerId = customerId,
                OperationalProjectId = operationalProjectId,
                Direction = ContractDirection.Downstream,
                Type = ContractType.Supply,
                Value = 250_000_000,
                Status = ContractStatus.Signed,
            };
            db.Contracts.Add(contract);
            await db.SaveChangesAsync();
            return (contract.Id, contract.ContractNumber);
        });

        var refused = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = "Thiết kế kho lạnh",
            customerId,
            contractId = purchaseOrder.Id,
        });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain(purchaseOrder.Number);
        (await WithDbAsync(db => db.DesignProjects.CountAsync(item => item.ContractId == purchaseOrder.Id)))
            .Should().Be(0);

        // A project linked before the rule must still be editable.
        var id = await CreateAsync(customerId, "Thiết kế kho lạnh (liên kết cũ)");
        await WithDbAsync(async db =>
        {
            var design = await db.DesignProjects.SingleAsync(item => item.Id == id);
            design.ContractId = purchaseOrder.Id;
            design.OperationalProjectId = operationalProjectId;
            await db.SaveChangesAsync();
        });
        var renamed = await Client.PutAsJsonAsync($"/api/design-projects/{id}", new
        {
            name = "Thiết kế kho lạnh — đổi tên",
            customerId,
            contractId = purchaseOrder.Id,
            operationalProjectId,
        });
        renamed.StatusCode.Should().Be(HttpStatusCode.OK, await renamed.Content.ReadAsStringAsync());
        (await ReadJsonAsync(renamed)).GetProperty("name").GetString().Should().Be("Thiết kế kho lạnh — đổi tên");
    }

    private async Task<int> CreateAsync(int customerId, string name)
    {
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name,
            customerId,
            operationalProjectId,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateOperationalProjectAsync(int customerId)
    {
        return await WithDbAsync<int>(async db =>
        {
            var project = new OperationalProject
            {
                Code = $"PJ-TEST-{Guid.NewGuid():N}",
                Name = "Design project integration fixture",
                CustomerId = customerId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
    }

    private async Task<int> FirstCustomerIdAsync()
    {
        return await WithDbAsync<int>(async db =>
        {
            var existing = await db.Customers.OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (existing != null) return existing.Id;

            var customer = new Customer
            {
                Name = "DP Test Customer " + Guid.NewGuid().ToString("N")[..6],
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
