using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class DepartmentWorkPackagesControllerTests(NihomeWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task List_WithoutAuthentication_IsUnauthorized()
    {
        (await Client.GetAsync("/api/work-packages")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync("/api/work-packages/departments")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Departments_WithoutViewPermission_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsCustomerAsync);
        (await Client.GetAsync("/api/work-packages/departments")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StaffRole_CanViewButCannotLoadAssignmentOptionsOrCreateInternalPackage()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ARCHITECT"));

        (await Client.GetAsync("/api/work-packages")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync("/api/work-packages/options")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var departmentsResponse = await Client.GetAsync("/api/work-packages/departments");
        departmentsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var departments = await ReadJsonAsync(departmentsResponse);
        departments.EnumerateArray().Should().NotBeEmpty();
        foreach (var department in departments.EnumerateArray())
        {
            department.TryGetProperty("labelKey", out _).Should().BeTrue();
            department.TryGetProperty("users", out _).Should().BeFalse();
            department.TryGetProperty("vendors", out _).Should().BeFalse();
            department.TryGetProperty("baselinePermissions", out _).Should().BeFalse();
        }
        var create = await SendAsync(HttpMethod.Post, "/api/work-packages", new
        {
            kind = "Internal",
            name = "Nhân viên tự tạo nhóm nội bộ",
            departmentRoleGroupId = 1,
            leadUserId = 1,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProjectPackage_FollowsDepartmentAssignmentProgressAndIndependentKpiVerification()
    {
        var seed = await SeedProjectScopeAsync();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));

        var packageResponse = await SendAsync(HttpMethod.Post, "/api/work-packages", new
        {
            kind = "Project",
            name = "Chuẩn bị vật tư thi công",
            operationalProjectId = seed.ProjectId,
            departmentRoleGroupId = seed.DepartmentGroupId,
            leadUserId = seed.DepartmentLeadId,
            plannedStart = "2026-10-12T00:00:00Z",
            plannedEnd = "2026-10-30T00:00:00Z",
        });
        packageResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var package = await ReadJsonAsync(packageResponse);
        var packageId = package.GetProperty("id").GetInt32();

        var taskResponse = await SendAsync(HttpMethod.Post, $"/api/work-packages/{packageId}/tasks", new
        {
            wbsCode = "SUP-01",
            title = "Lập kế hoạch cung ứng vật tư",
            assigneeUserId = seed.DepartmentStaffId,
            approverUserId = seed.DepartmentLeadId,
            kpiDefinitionId = seed.KpiDefinitionId,
            plannedStart = "2026-10-12T00:00:00Z",
            plannedEnd = "2026-10-18T00:00:00Z",
            ccUserIds = Array.Empty<int>(),
            predecessorTaskIds = Array.Empty<int>(),
        });
        taskResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var task = await ReadJsonAsync(taskResponse);
        var taskId = task.GetProperty("id").GetInt32();
        package = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{packageId}"));

        var activated = await SendAsync(HttpMethod.Put, $"/api/work-packages/{packageId}", new
        {
            name = package.GetProperty("name").GetString(),
            status = "Active",
            plannedStart = package.GetProperty("plannedStart").GetString(),
            plannedEnd = package.GetProperty("plannedEnd").GetString(),
            rowVersion = package.GetProperty("rowVersion").GetString(),
        }, package.GetProperty("rowVersion").GetString());
        activated.StatusCode.Should().Be(HttpStatusCode.OK);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        var completed = await SendAsync(HttpMethod.Put, $"/api/work-packages/{packageId}/tasks/{taskId}/progress", new
        {
            status = "Completed",
            progressPercent = 100,
            rowVersion = task.GetProperty("rowVersion").GetString(),
        }, task.GetProperty("rowVersion").GetString());
        completed.StatusCode.Should().Be(HttpStatusCode.OK);
        var completedTask = await ReadJsonAsync(completed);

        var selfVerify = await SendAsync(HttpMethod.Post, $"/api/work-packages/{packageId}/tasks/{taskId}/verify", new
        {
            confirm = true,
            rowVersion = completedTask.GetProperty("rowVersion").GetString(),
        }, completedTask.GetProperty("rowVersion").GetString());
        selfVerify.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PROCUREMENT"));
        var verified = await SendAsync(HttpMethod.Post, $"/api/work-packages/{packageId}/tasks/{taskId}/verify", new
        {
            confirm = true,
            rowVersion = completedTask.GetProperty("rowVersion").GetString(),
        }, completedTask.GetProperty("rowVersion").GetString());
        verified.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(verified)).GetProperty("status").GetString().Should().Be("Verified");

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var noKpiResponse = await SendAsync(HttpMethod.Post, $"/api/work-packages/{packageId}/tasks", new
        {
            wbsCode = "SUP-02",
            title = "Kiểm tra biên bản giao vật tư",
            assigneeUserId = seed.DepartmentStaffId,
            approverUserId = seed.DepartmentLeadId,
            plannedStart = "2026-10-19T00:00:00Z",
            plannedEnd = "2026-10-20T00:00:00Z",
            ccUserIds = Array.Empty<int>(),
            predecessorTaskIds = Array.Empty<int>(),
        });
        noKpiResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var noKpiTask = await ReadJsonAsync(noKpiResponse);
        var noKpiTaskId = noKpiTask.GetProperty("id").GetInt32();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        var noKpiCompleted = await SendAsync(HttpMethod.Put, $"/api/work-packages/{packageId}/tasks/{noKpiTaskId}/progress", new
        {
            status = "Completed",
            progressPercent = 100,
            rowVersion = noKpiTask.GetProperty("rowVersion").GetString(),
        }, noKpiTask.GetProperty("rowVersion").GetString());
        noKpiCompleted.StatusCode.Should().Be(HttpStatusCode.OK);
        var completedNoKpiTask = await ReadJsonAsync(noKpiCompleted);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        package = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{packageId}"));
        var prematureClosure = await SendAsync(HttpMethod.Put, $"/api/work-packages/{packageId}", new
        {
            name = package.GetProperty("name").GetString(),
            status = "Completed",
            plannedStart = package.GetProperty("plannedStart").GetString(),
            plannedEnd = package.GetProperty("plannedEnd").GetString(),
            rowVersion = package.GetProperty("rowVersion").GetString(),
        }, package.GetProperty("rowVersion").GetString());
        prematureClosure.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PROCUREMENT"));
        var noKpiVerified = await SendAsync(HttpMethod.Post, $"/api/work-packages/{packageId}/tasks/{noKpiTaskId}/verify", new
        {
            confirm = true,
            rowVersion = completedNoKpiTask.GetProperty("rowVersion").GetString(),
        }, completedNoKpiTask.GetProperty("rowVersion").GetString());
        noKpiVerified.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(noKpiVerified)).GetProperty("status").GetString().Should().Be("Verified");

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        package = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{packageId}"));
        var closed = await SendAsync(HttpMethod.Put, $"/api/work-packages/{packageId}", new
        {
            name = package.GetProperty("name").GetString(),
            status = "Completed",
            plannedStart = package.GetProperty("plannedStart").GetString(),
            plannedEnd = package.GetProperty("plannedEnd").GetString(),
            rowVersion = package.GetProperty("rowVersion").GetString(),
        }, package.GetProperty("rowVersion").GetString());
        closed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TenderPlan_TaskWithoutKpi_FollowsIndependentVerificationAndCompletionPipeline()
    {
        var seed = await SeedProjectScopeAsync();
        var tenderId = await WithDbAsync(async db =>
        {
            var project = await db.OperationalProjects.SingleAsync(item => item.Id == seed.ProjectId);
            var adminId = project.ProjectManagerUserId!.Value;
            var tender = new Tender
            {
                Code = $"TD-WP-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
                Name = "Gói thầu nhà máy NICON có kế hoạch liên phòng",
                CustomerId = project.CustomerId,
                OperationalProjectId = project.Id,
                PreparerUserId = adminId,
                SubmissionDeadline = DateTime.UtcNow.Date.AddDays(30),
                InvitationReceivedAt = DateTime.UtcNow.Date.AddDays(-1),
                InvitationSource = "Email khách hàng",
                InvitationReceivedByUserId = adminId,
                CreatedByUserId = adminId,
                UpdatedByUserId = adminId,
            };
            db.Tenders.Add(tender);
            await db.SaveChangesAsync();
            return tender.Id;
        });
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));

        var packageResponse = await SendAsync(HttpMethod.Post, "/api/work-packages", new
        {
            kind = "Project",
            purpose = "TenderPlanning",
            name = "Chuẩn bị hồ sơ pháp lý dự thầu",
            operationalProjectId = seed.ProjectId,
            sourceTenderId = tenderId,
            departmentRoleGroupId = seed.DepartmentGroupId,
            leadUserId = seed.DepartmentLeadId,
            plannedStart = DateTime.UtcNow.Date,
            plannedEnd = DateTime.UtcNow.Date.AddDays(14),
        });
        packageResponse.StatusCode.Should().Be(HttpStatusCode.Created, await packageResponse.Content.ReadAsStringAsync());
        var package = await ReadJsonAsync(packageResponse);
        var packageId = package.GetProperty("id").GetInt32();

        var taskResponse = await SendAsync(HttpMethod.Post, $"/api/work-packages/{packageId}/tasks", new
        {
            wbsCode = "TD-PL-01",
            title = "Rà soát hồ sơ pháp lý theo HSMT",
            assigneeUserId = seed.DepartmentStaffId,
            approverUserId = seed.DepartmentLeadId,
            plannedStart = DateTime.UtcNow.Date,
            plannedEnd = DateTime.UtcNow.Date.AddDays(7),
            ccUserIds = Array.Empty<int>(),
            predecessorTaskIds = Array.Empty<int>(),
        });
        taskResponse.StatusCode.Should().Be(HttpStatusCode.Created, await taskResponse.Content.ReadAsStringAsync());
        var task = await ReadJsonAsync(taskResponse);
        var taskId = task.GetProperty("id").GetInt32();
        package = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{packageId}"));

        var activated = await SendAsync(HttpMethod.Put, $"/api/work-packages/{packageId}", new
        {
            name = package.GetProperty("name").GetString(),
            status = "Active",
            plannedStart = package.GetProperty("plannedStart").GetString(),
            plannedEnd = package.GetProperty("plannedEnd").GetString(),
            rowVersion = package.GetProperty("rowVersion").GetString(),
        }, package.GetProperty("rowVersion").GetString());
        activated.StatusCode.Should().Be(HttpStatusCode.OK);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        var completed = await SendAsync(HttpMethod.Put, $"/api/work-packages/{packageId}/tasks/{taskId}/progress", new
        {
            status = "Completed",
            progressPercent = 100,
            rowVersion = task.GetProperty("rowVersion").GetString(),
        }, task.GetProperty("rowVersion").GetString());
        completed.StatusCode.Should().Be(HttpStatusCode.OK);
        var completedTask = await ReadJsonAsync(completed);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PROCUREMENT"));
        var verified = await SendAsync(HttpMethod.Post, $"/api/work-packages/{packageId}/tasks/{taskId}/verify", new
        {
            confirm = true,
            rowVersion = completedTask.GetProperty("rowVersion").GetString(),
        }, completedTask.GetProperty("rowVersion").GetString());
        verified.StatusCode.Should().Be(HttpStatusCode.OK, await verified.Content.ReadAsStringAsync());

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        package = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{packageId}"));
        var closed = await SendAsync(HttpMethod.Put, $"/api/work-packages/{packageId}", new
        {
            name = package.GetProperty("name").GetString(),
            status = "Completed",
            plannedStart = package.GetProperty("plannedStart").GetString(),
            plannedEnd = package.GetProperty("plannedEnd").GetString(),
            rowVersion = package.GetProperty("rowVersion").GetString(),
        }, package.GetProperty("rowVersion").GetString());

        closed.StatusCode.Should().Be(HttpStatusCode.OK, await closed.Content.ReadAsStringAsync());
        (await ReadJsonAsync(closed)).GetProperty("status").GetString().Should().Be("Completed");
    }

    [Fact]
    public async Task InternalPackage_RejectsProjectReference()
    {
        var seed = await SeedProjectScopeAsync();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));

        var response = await SendAsync(HttpMethod.Post, "/api/work-packages", new
        {
            kind = "Internal",
            name = "Đào tạo nội bộ",
            operationalProjectId = seed.ProjectId,
            departmentRoleGroupId = seed.DepartmentGroupId,
            leadUserId = seed.DepartmentLeadId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DepartmentWorkPackages.AnyAsync(item => item.Name == "Đào tạo nội bộ")))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Administrator_CannotLinkPackageToOpportunityFromAnotherProject()
    {
        var seed = await SeedProjectScopeAsync();
        var opportunityId = await WithDbAsync(async db =>
        {
            var adminId = await db.Users.Where(item => item.RoleEntity != null && item.RoleEntity.Code == "SUPER_ADMIN")
                .Select(item => item.Id).SingleAsync();
            var customer = new Customer
            {
                Name = $"NICON Other Project {Guid.NewGuid():N}",
                Type = CustomerType.Company,
                SourceCode = "referral",
            };
            var otherProject = new OperationalProject
            {
                Code = $"WP-OTHER-{Guid.NewGuid():N}"[..24].ToUpperInvariant(),
                Name = "Dự án khác",
                Customer = customer,
                ProjectManagerUserId = adminId,
            };
            var opportunity = new Opportunity
            {
                Name = "Cơ hội thuộc dự án khác",
                Customer = customer,
                OperationalProject = otherProject,
                OwnerUserId = adminId,
                Stage = OpportunityStage.Qualification,
            };
            db.Add(opportunity);
            await db.SaveChangesAsync();
            return opportunity.Id;
        });
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));

        var response = await SendAsync(HttpMethod.Post, "/api/work-packages", new
        {
            kind = "Project",
            name = "Nhóm việc sai dự án",
            operationalProjectId = seed.ProjectId,
            sourceOpportunityId = opportunityId,
            departmentRoleGroupId = seed.DepartmentGroupId,
            leadUserId = seed.DepartmentLeadId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DepartmentWorkPackages.AnyAsync(item => item.Name == "Nhóm việc sai dự án")))
            .Should().BeFalse();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body, string? etag = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        if (!string.IsNullOrWhiteSpace(etag)) request.Headers.TryAddWithoutValidation("If-Match", $"\"{etag}\"");
        return await Client.SendAsync(request);
    }

    private Task<(int ProjectId, int DepartmentGroupId, int DepartmentLeadId, int DepartmentStaffId, int KpiDefinitionId)> SeedProjectScopeAsync() =>
        WithDbAsync(async db =>
        {
            var departmentGroupId = await db.RoleGroups.Where(item => item.Code == "PROCUREMENT").Select(item => item.Id).SingleAsync();
            var departmentLeadId = await db.Users.Where(item => item.RoleEntity != null && item.RoleEntity.Code == "PROCUREMENT")
                .Select(item => item.Id).SingleAsync();
            var departmentStaffId = await db.Users.Where(item => item.RoleEntity != null && item.RoleEntity.Code == "WAREHOUSE")
                .Select(item => item.Id).SingleAsync();
            var adminId = await db.Users.Where(item => item.RoleEntity != null && item.RoleEntity.Code == "SUPER_ADMIN")
                .Select(item => item.Id).SingleAsync();
            var customer = new Customer
            {
                Name = $"NICON Work Package Client {Guid.NewGuid():N}",
                Type = CustomerType.Company,
                SourceCode = "referral",
            };
            var project = new OperationalProject
            {
                Code = $"WP-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
                Name = "Nhà máy sản xuất linh kiện",
                Customer = customer,
                ProjectManagerUserId = adminId,
            };
            var definition = new KpiDefinition
            {
                Code = $"WAREHOUSE-WORK-{Guid.NewGuid():N}"[..30],
                RoleCode = "WAREHOUSE",
                NameKey = "kpi.definition.WAREHOUSE_VERIFIED_WORK",
                SourceModule = "WORK",
                MetricCode = "VerifiedWorkTaskCount",
                Weight = .1m,
                TargetValue = 1m,
                IsActive = true,
                CreatedByUserId = adminId,
                UpdatedByUserId = adminId,
            };
            db.AddRange(project, definition);
            await db.SaveChangesAsync();
            return (project.Id, departmentGroupId, departmentLeadId, departmentStaffId, definition.Id);
        });
}
