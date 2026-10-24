using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>PermitsController</c> (NIH-137): RBAC gating,
/// lifecycle updates and the retired direct-create/delete endpoints. Legal
/// setup owns permit creation; creating a design workspace must remain
/// side-effect free.
/// </summary>
public class PermitsControllerTests : IntegrationTestBase
{
    public PermitsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/permits")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        // SALE has no permit.checklists.* bundle.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/permits")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsPm_ReturnsOkWithRiskSummary()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));
        var res = await Client.GetAsync("/api/permits");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        body.GetProperty("risk").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task Create_DesignProject_DoesNotAutoSeedPermitChecklist()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateDesignProjectAsync();

        var listRes = await Client.GetAsync($"/api/permits?designProjectId={projectId}&pageSize=100");
        listRes.EnsureSuccessStatusCode();
        var listBody = await ReadJsonAsync(listRes);
        listBody.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Patch_UpdatesStatusAndReturnsUpdatedRow()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var id = await CreatePermitRowAsync();
        (await UploadDocumentAsync(id, "SubmittedPackage", "submission.pdf")).EnsureSuccessStatusCode();

        var res = await Client.PatchAsJsonAsync($"/api/permits/{id}", new
        {
            status = "Submitted",
            submittedAt = DateTime.UtcNow,
            issuingAgency = "Sở Xây dựng — integration",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Submitted");
        body.GetProperty("issuingAgency").GetString().Should().Be("Sở Xây dựng — integration");
    }

    [Fact]
    public async Task DirectCreateAndDelete_AreRetiredForLegalPackagePermits()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateDesignProjectAsync();
        var existingId = await SeedPermitAsync(projectId);
        var created = await Client.PostAsJsonAsync("/api/permits", new
        {
            designProjectId = projectId,
            operationalProjectId = await OperationalProjectIdAsync(projectId),
            permitTypeCode = "gpxd",
            status = "Preparing",
            issuingAgency = "Integration agency",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Gone);

        (await Client.GetAsync($"/api/permits/{existingId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await UploadDocumentAsync(existingId, "SubmittedPackage", "submission.pdf")).EnsureSuccessStatusCode();
        var updated = await Client.PatchAsJsonAsync($"/api/permits/{existingId}", new
        {
            status = "Submitted",
            submittedAt = DateTime.UtcNow,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(updated)).GetProperty("status").GetString().Should().Be("Submitted");

        (await Client.DeleteAsync($"/api/permits/{existingId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Client.GetAsync($"/api/permits/{existingId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_UnknownId_Is404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var res = await Client.PatchAsJsonAsync("/api/permits/9999999", new { status = "Submitted" });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_InvalidStatus_Is400()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var id = await CreatePermitRowAsync();
        var res = await Client.PatchAsJsonAsync($"/api/permits/{id}", new { status = "NotAStatus" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Pm_CannotPatch()
    {
        // PM has permit.checklists.** (both view + manage) per rbac-defaults.
        // Use SALE for the negative case (has no permit permission at all).
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var id = await CreatePermitRowAsync();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var res = await Client.PatchAsJsonAsync($"/api/permits/{id}", new { status = "Submitted" });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.DeleteAsync($"/api/permits/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ensure_IsRetiredForKnownProject()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateDesignProjectAsync();
        var res = await Client.PostAsync($"/api/permits/design-project/{projectId}/ensure", null);
        res.StatusCode.Should().Be(HttpStatusCode.Gone);
        var body = await ReadJsonAsync(res);
        body.GetProperty("code").GetString().Should().Be("LEGAL_SETUP_REQUIRED");
    }

    [Fact]
    public async Task UploadDocument_AssignsManagedPath_AndRequiresManagePermission()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var id = await CreatePermitRowAsync();

        var uploaded = await UploadDocumentAsync(id, "SubmittedPackage", "submission.pdf");

        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(uploaded);
        body.GetProperty("submittedFilePath").GetString()
            .Should().StartWith("/files/business-documents/permits/");
        var path = body.GetProperty("submittedFilePath").GetString()!;
        var content = await Client.GetAsync($"/api/permits/{id}/documents/{Path.GetFileName(path)}/content");
        content.StatusCode.Should().Be(HttpStatusCode.OK);
        (await content.Content.ReadAsStringAsync()).Should().Be("permit document");
        (await Client.GetAsync($"/api/permits/2147483647/documents/{Path.GetFileName(path)}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await UploadDocumentAsync(id, "IssuedPermit", "issued.pdf")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Pm_ListDetailAndMutation_AreRestrictedToAccessibleProjects()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var accessibleProject = await CreateDesignProjectForPmAsync(assignToPm: true);
        var inaccessibleProject = await CreateDesignProjectForPmAsync(assignToPm: false);
        var accessiblePermit = await SeedPermitAsync(accessibleProject);
        var inaccessiblePermit = await SeedPermitAsync(inaccessibleProject);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));

        var list = await Client.GetAsync("/api/permits?pageSize=200");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = (await ReadJsonAsync(list)).GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt32());
        ids.Should().Contain(accessiblePermit).And.NotContain(inaccessiblePermit);
        (await Client.GetAsync($"/api/permits/{inaccessiblePermit}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PatchAsJsonAsync($"/api/permits/{inaccessiblePermit}", new
        {
            status = "Submitted",
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.PermitChecklistItems
            .Where(item => item.Id == inaccessiblePermit)
            .Select(item => item.Status)
            .SingleAsync())).Should().NotBe(PermitStatus.Submitted);
    }

    // -------- helpers --------

    [Fact]
    public async Task EndedLegalAssignee_CannotMutateHiddenPermit()
    {
        await AuthTestHelper.AuthenticateAsync(Client,
            c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var designProjectId = await CreateDesignProjectAsync();
        var permitId = await SeedPermitAsync(designProjectId);
        var memberId = await WithDbAsync(async db =>
        {
            var permit = await db.PermitChecklistItems.SingleAsync(x => x.Id == permitId);
            var legal = await db.Users.SingleAsync(x => x.PhoneNumber ==
                TestDataSeeder.BusinessRolePhonesByCode["LEGAL_OFFICER"]);
            var admin = await db.Users.SingleAsync(x =>
                x.PhoneNumber == TestDataSeeder.SuperAdminPhone);
            var groupId = await db.RoleGroups.Where(x => x.Code == "CONSTRUCTION")
                .Select(x => x.Id).SingleAsync();
            var member = new OperationalProjectMember
            {
                OperationalProjectId = permit.OperationalProjectId,
                UserId = legal.Id,
                Position = "NICON permit dossier officer",
                StartedAt = DateTime.UtcNow.AddDays(-7),
                CreatedByUserId = admin.Id,
                UpdatedByUserId = admin.Id,
            };
            db.OperationalProjectMembers.Add(member);
            var task = new DepartmentWorkTask
            {
                WbsCode = "L-01",
                Title = "Prepare NICON factory construction permit",
                DepartmentRoleGroupId = groupId,
                AssigneeUserId = legal.Id,
                ApproverUserId = admin.Id,
                PlannedStart = DateTime.UtcNow.AddDays(-7),
                PlannedEnd = DateTime.UtcNow.AddDays(30),
                Status = WorkTaskStatus.InProgress,
                ProgressPercent = 25,
                CreatedByUserId = admin.Id,
                UpdatedByUserId = admin.Id,
            };
            db.DepartmentWorkPackages.Add(new DepartmentWorkPackage
            {
                Code = $"LEGAL-AUDIT-{Guid.NewGuid():N}"[..35],
                Name = "NICON factory legal permitting",
                Kind = WorkPackageKind.Project,
                Purpose = WorkPackagePurpose.LegalPermitting,
                OperationalProjectId = permit.OperationalProjectId,
                DepartmentRoleGroupId = groupId,
                LeadUserId = admin.Id,
                Status = WorkPackageStatus.Active,
                PlannedStart = DateTime.UtcNow.AddDays(-7),
                PlannedEnd = DateTime.UtcNow.AddDays(30),
                CreatedByUserId = admin.Id,
                UpdatedByUserId = admin.Id,
                Tasks = [task],
            });
            permit.WorkTask = task;
            permit.OwnerUserId = legal.Id;
            permit.IssuingAgency = "Original NICON permit authority";
            await db.SaveChangesAsync();
            return member.Id;
        });

        await AuthTestHelper.AuthenticateAsync(Client,
            c => AuthTestHelper.LoginAsRoleAsync(c, "LEGAL_OFFICER"));
        (await Client.GetAsync($"/api/permits/{permitId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Simulate the persisted result of ending the project membership.
        // Global Legal permission and the historical assignee link remain.
        await WithDbAsync(async db =>
        {
            var member = await db.OperationalProjectMembers.SingleAsync(x => x.Id == memberId);
            member.EndedAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });
        (await Client.GetAsync($"/api/permits/{permitId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var response = await Client.PatchAsJsonAsync($"/api/permits/{permitId}", new
        {
            issuingAgency = "Mutation by removed NICON project member",
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Removed legal assignee must not mutate a project record hidden by GET. " +
            $"Actual PATCH status={(int)response.StatusCode}; body={body}");
        (await WithDbAsync(db => db.PermitChecklistItems.Where(x => x.Id == permitId)
            .Select(x => x.IssuingAgency).SingleAsync()))
            .Should().Be("Original NICON permit authority");
    }

    private async Task<int> CreateDesignProjectAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Permit fixture {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateDesignProjectForPmAsync(bool assignToPm)
    {
        var customerId = await FirstCustomerIdAsync();
        var pmId = await WithDbAsync(db => db.Users
            .Where(item => item.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
            .Select(item => item.Id)
            .SingleAsync());
        var operationalProjectId = await WithDbAsync<int>(async db =>
        {
            var project = new OperationalProject
            {
                Code = $"PJ-PERMIT-SCOPE-{Guid.NewGuid():N}"[..40],
                Name = "Permit scope fixture",
                CustomerId = customerId,
                ProjectManagerUserId = assignToPm ? pmId : null,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
        var response = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Permit scope {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
        });
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateOperationalProjectAsync(int customerId)
    {
        return await WithDbAsync<int>(async db =>
        {
            var project = new OperationalProject
            {
                Code = $"PJ-PERMIT-{Guid.NewGuid():N}"[..40],
                Name = "Permit integration fixture",
                CustomerId = customerId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
    }

    private async Task<HttpResponseMessage> UploadDocumentAsync(int id, string kind, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("permit document"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", fileName);
        return await Client.PostAsync($"/api/permits/{id}/documents/{kind}", content);
    }

    private async Task<int> CreatePermitRowAsync()
    {
        var projectId = await CreateDesignProjectAsync();
        return await SeedPermitAsync(projectId);
    }

    private Task<int> OperationalProjectIdAsync(int designProjectId) => WithDbAsync(db => db.DesignProjects
        .Where(item => item.Id == designProjectId)
        .Select(item => item.OperationalProjectId!.Value)
        .SingleAsync());

    private async Task<int> SeedPermitAsync(int designProjectId)
    {
        return await WithDbAsync<int>(async db =>
        {
            var operationalProjectId = await db.DesignProjects
                .Where(item => item.Id == designProjectId)
                .Select(item => item.OperationalProjectId!.Value)
                .SingleAsync();
            var permit = new PermitChecklistItem
            {
                OperationalProjectId = operationalProjectId,
                DesignProjectId = designProjectId,
                PermitTypeCode = $"gpxd-{Guid.NewGuid():N}"[..24],
                IssuingAgency = "Integration authority",
                Status = PermitStatus.Preparing,
            };
            db.PermitChecklistItems.Add(permit);
            await db.SaveChangesAsync();
            return permit.Id;
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
                Name = "Permit Test Customer " + Guid.NewGuid().ToString("N")[..6],
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
