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

    [Fact]
    public async Task CompletedPackage_ReopensWithoutRevokingVerifiedTasksOrFinanceApproval()
    {
        var seed = await SeedProjectScopeAsync();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var packageId = await SeedLifecyclePackageAsync(seed, WorkPackageStatus.Completed);
        var before = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{packageId}"));
        var reopened = await SendAsync(HttpMethod.Put, $"/api/work-packages/{packageId}", new
        {
            name = before.GetProperty("name").GetString(),
            status = "Active",
            rowVersion = before.GetProperty("rowVersion").GetString(),
        });
        reopened.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await ReadJsonAsync(reopened);
        after.GetProperty("status").GetString().Should().Be("Active");
        after.GetProperty("completedAt").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        after.GetProperty("tasks")[0].GetProperty("status").GetString().Should().Be("Verified");
        after.GetProperty("financeReview").GetProperty("status").GetString().Should().Be("Approved");
        (await SendAsync(HttpMethod.Post, $"/api/work-packages/{packageId}/tasks", new
        {
            wbsCode = "SUP-REOPEN",
            title = "Bổ sung kiểm tra vật tư sau mở lại",
            assigneeUserId = seed.DepartmentStaffId,
            plannedStart = "2026-10-23T00:00:00Z",
            plannedEnd = "2026-10-24T00:00:00Z",
        })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await WithDbAsync(db => db.DepartmentWorkPackageHistory.AnyAsync(item => item.WorkPackageId == packageId && item.Action == "Reopened"))).Should().BeTrue();
        var stale = await SendAsync(HttpMethod.Put, $"/api/work-packages/{packageId}", new
        {
            name = "Không được ghi đè",
            status = "Active",
            rowVersion = before.GetProperty("rowVersion").GetString(),
        });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CancelledPackage_CannotReopen()
    {
        var seed = await SeedProjectScopeAsync();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var id = await SeedLifecyclePackageAsync(seed, WorkPackageStatus.Cancelled);
        var package = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{id}"));
        (await SendAsync(HttpMethod.Put, $"/api/work-packages/{id}", new
        { name = "Không mở lại nhóm đã hủy", status = "Active", rowVersion = package.GetProperty("rowVersion").GetString() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DepartmentWorkPackages.Where(item => item.Id == id).Select(item => item.Status).SingleAsync())).Should().Be(WorkPackageStatus.Cancelled);
    }

    [Fact]
    public async Task HardDelete_RemovesOwnedGraphUnlinksPermitAndPreservesProject()
    {
        var seed = await SeedProjectScopeAsync();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var id = await SeedLifecyclePackageAsync(seed, WorkPackageStatus.Completed);
        var taskId = await WithDbAsync(db => db.DepartmentWorkTasks.Where(item => item.WorkPackageId == id).Select(item => item.Id).SingleAsync());
        var permitId = await WithDbAsync(async db =>
        {
            var permit = new PermitChecklistItem { OperationalProjectId = seed.ProjectId, WorkTaskId = taskId, PermitTypeCode = "building-permit" };
            db.PermitChecklistItems.Add(permit);
            db.DepartmentWorkTaskCcUsers.Add(new DepartmentWorkTaskCc { WorkTaskId = taskId, UserId = seed.DepartmentLeadId });
            await db.SaveChangesAsync();
            return permit.Id;
        });
        var package = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{id}"));
        var impactResponse = await Client.GetAsync($"/api/work-packages/{id}/deletion-impact");
        impactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var impact = await ReadJsonAsync(impactResponse);
        impact.GetProperty("canDelete").GetBoolean().Should().BeTrue();
        impact.GetProperty("items").EnumerateArray().Should().Contain(item => item.GetProperty("key").GetString() == "workPackages.permits" && item.GetProperty("action").GetString() == "Unlink");
        var delete = await SendAsync(HttpMethod.Delete, $"/api/work-packages/{id}", new
        { planToken = impact.GetProperty("planToken").GetString(), confirmation = impact.GetProperty("requiredConfirmation").GetString(), rowVersion = package.GetProperty("rowVersion").GetString() });
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WithDbAsync(db => db.DepartmentWorkPackages.AnyAsync(item => item.Id == id))).Should().BeFalse();
        (await WithDbAsync(db => db.DepartmentWorkTasks.AnyAsync(item => item.WorkPackageId == id))).Should().BeFalse();
        (await WithDbAsync(db => db.DepartmentFinanceReviews.AnyAsync(item => item.WorkPackageId == id))).Should().BeFalse();
        (await WithDbAsync(db => db.DepartmentWorkTaskCcUsers.AnyAsync(item => item.WorkTaskId == taskId))).Should().BeFalse();
        (await WithDbAsync(db => db.PermitChecklistItems.Where(item => item.Id == permitId).Select(item => item.WorkTaskId).SingleAsync())).Should().BeNull();
        (await WithDbAsync(db => db.OperationalProjects.AnyAsync(item => item.Id == seed.ProjectId))).Should().BeTrue();
    }

    [Theory]
    [InlineData("confirmation")]
    [InlineData("changed-plan")]
    [InlineData("stale-version")]
    [InlineData("missing-version")]
    [InlineData("file-blocker")]
    public async Task HardDelete_RejectionPreservesRootAndDependencies(string scenario)
    {
        var seed = await SeedProjectScopeAsync();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var id = await SeedLifecyclePackageAsync(seed, WorkPackageStatus.Completed);
        if (scenario == "file-blocker") await WithDbAsync(async db =>
        {
            db.ProjectDocuments.Add(new ProjectDocument { OperationalProjectId = seed.ProjectId, SourceEntityType = "DepartmentWorkPackage", SourceRecordId = id, OriginalFileName = "bien-ban.pdf", LocalPath = "/files/project-documents/pending.pdf" });
            await db.SaveChangesAsync();
        });
        var package = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{id}"));
        var impact = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{id}/deletion-impact"));
        if (scenario == "changed-plan") await WithDbAsync(async db =>
        {
            var taskId = await db.DepartmentWorkTasks.Where(item => item.WorkPackageId == id).Select(item => item.Id).SingleAsync();
            db.DepartmentWorkTaskCcUsers.Add(new DepartmentWorkTaskCc { WorkTaskId = taskId, UserId = seed.DepartmentLeadId });
            await db.SaveChangesAsync();
        });
        var response = await SendAsync(HttpMethod.Delete, $"/api/work-packages/{id}", new
        {
            planToken = impact.GetProperty("planToken").GetString(),
            confirmation = scenario == "confirmation" ? "WRONG" : impact.GetProperty("requiredConfirmation").GetString(),
            rowVersion = scenario == "missing-version" ? null : scenario == "stale-version" ? Convert.ToBase64String(new byte[8]) : package.GetProperty("rowVersion").GetString(),
        });
        response.StatusCode.Should().Be(scenario is "changed-plan" or "stale-version" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DepartmentWorkPackages.AnyAsync(item => item.Id == id))).Should().BeTrue();
        (await WithDbAsync(db => db.DepartmentWorkTasks.CountAsync(item => item.WorkPackageId == id))).Should().Be(1);
        (await WithDbAsync(db => db.DepartmentFinanceReviews.CountAsync(item => item.WorkPackageId == id))).Should().Be(1);
    }

    [Fact]
    public async Task HardDelete_AndReopen_RequireManagementPermission()
    {
        var seed = await SeedProjectScopeAsync();
        var id = await SeedLifecyclePackageAsync(seed, WorkPackageStatus.Completed);
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync($"/api/work-packages/{id}/deletion-impact")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Delete, $"/api/work-packages/{id}", new { planToken = new string('a', 64), confirmation = "WP", rowVersion = Convert.ToBase64String(new byte[8]) })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Put, $"/api/work-packages/{id}", new { name = "Mở lại trái quyền", status = "Active", rowVersion = Convert.ToBase64String(new byte[8]) })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.DepartmentWorkPackages.AnyAsync(item => item.Id == id))).Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HardDelete_UsedPricingEvidenceBlocksAndPreservesQuotation(bool snapshot)
    {
        var seed = await SeedProjectScopeAsync();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var id = await SeedLifecyclePackageAsync(seed, WorkPackageStatus.Completed);
        var quoteId = await WithDbAsync(async db =>
        {
            var customerId = await db.OperationalProjects.Where(item => item.Id == seed.ProjectId).Select(item => item.CustomerId).SingleAsync();
            var quote = new Quote
            {
                Code = $"QT-NICON-{id}",
                OperationalProjectId = seed.ProjectId,
                Opportunity = new Opportunity { Name = "Cung cấp vật tư công trường NICON", CustomerId = customerId, OperationalProjectId = seed.ProjectId }
            };
            var vendor = new Vendor { VendorCode = $"NCC-NICON-{id}", CompanyName = "Nhà cung cấp vật tư NICON", CreatedByUserId = seed.DepartmentStaffId };
            db.AddRange(quote, vendor);
            await db.SaveChangesAsync();
            var offer = new ProcurementInputPriceOffer
            {
                WorkPackageId = id,
                VendorId = vendor.Id,
                SupplierReference = "NICON-STEEL-REVIEW",
                ValidUntil = DateTime.UtcNow.AddDays(30),
                Status = InputPriceOfferStatus.Approved,
                CreatedByUserId = seed.DepartmentStaffId,
                UpdatedByUserId = seed.DepartmentStaffId
            };
            db.ProcurementInputPriceOffers.Add(offer);
            await db.SaveChangesAsync();
            if (snapshot) db.QuoteVersionSnapshots.Add(new QuoteVersionSnapshot { QuoteId = quote.Id, VersionNumber = 999, InputPriceOfferId = offer.Id });
            else quote.InputPriceOfferId = offer.Id;
            await db.SaveChangesAsync();
            return quote.Id;
        });
        var package = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{id}"));
        var impact = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{id}/deletion-impact"));
        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        (await SendAsync(HttpMethod.Delete, $"/api/work-packages/{id}", new
        { planToken = impact.GetProperty("planToken").GetString(), confirmation = impact.GetProperty("requiredConfirmation").GetString(), rowVersion = package.GetProperty("rowVersion").GetString() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Quotes.AnyAsync(item => item.Id == quoteId))).Should().BeTrue();
        (await WithDbAsync(db => db.ProcurementInputPriceOffers.AnyAsync(item => item.WorkPackageId == id))).Should().BeTrue();
    }

    [Theory]
    [InlineData("sent-quote")]
    [InlineData("submission")]
    [InlineData("legal-activation")]
    public async Task HardDelete_ConsumedApprovalEvidenceRemainsProtected(string kind)
    {
        var seed = await SeedProjectScopeAsync();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var id = await SeedLifecyclePackageAsync(seed, WorkPackageStatus.Completed);
        await WithDbAsync(async db =>
        {
            var package = await db.DepartmentWorkPackages.SingleAsync(item => item.Id == id);
            var customerId = await db.OperationalProjects.Where(item => item.Id == seed.ProjectId).Select(item => item.CustomerId).SingleAsync();
            if (kind == "sent-quote")
            {
                var quote = new Quote
                {
                    Code = $"QT-SENT-{id}",
                    Status = QuoteStatus.SentToCustomer,
                    OperationalProjectId = seed.ProjectId,
                    Opportunity = new Opportunity { Name = "Báo giá vật tư đã gửi", CustomerId = customerId, OperationalProjectId = seed.ProjectId }
                };
                db.Quotes.Add(quote); await db.SaveChangesAsync();
                package.SourceQuoteId = quote.Id; package.Purpose = WorkPackagePurpose.FinanceCommercialReview;
            }
            else if (kind == "submission")
            {
                var tender = new Tender
                {
                    Code = $"TD-NICON-{id}",
                    Name = "Hồ sơ nhà máy NICON",
                    CustomerId = customerId,
                    OperationalProjectId = seed.ProjectId,
                    InvitationReceivedByUserId = seed.DepartmentLeadId
                };
                db.Tenders.Add(tender); await db.SaveChangesAsync();
                package.SourceTenderId = tender.Id; package.Purpose = WorkPackagePurpose.TenderPlanning;
                db.TenderSubmissions.Add(new TenderSubmission
                {
                    TenderId = tender.Id,
                    Sequence = 1,
                    SubmittedAt = DateTime.UtcNow,
                    SubmittedByUserId = seed.DepartmentLeadId,
                    FinanceReviewWorkPackageId = id,
                    SubmissionChannel = "Customer portal"
                });
            }
            else
            {
                var contract = new Contract { ContractNumber = $"HD-NICON-{id}", CustomerId = customerId, OperationalProjectId = seed.ProjectId };
                db.Contracts.Add(contract); await db.SaveChangesAsync();
                db.ContractLegalActivations.Add(new ContractLegalActivation
                {
                    ContractId = contract.Id,
                    OperationalProjectId = seed.ProjectId,
                    WorkPackageId = id,
                    ConfigurationJson = "{}",
                    ActivatedByUserId = seed.DepartmentLeadId
                });
            }
            await db.SaveChangesAsync();
        });
        var packageResponse = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{id}"));
        var impact = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages/{id}/deletion-impact"));
        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        (await SendAsync(HttpMethod.Delete, $"/api/work-packages/{id}", new
        {
            planToken = impact.GetProperty("planToken").GetString(),
            confirmation = impact.GetProperty("requiredConfirmation").GetString(),
            rowVersion = packageResponse.GetProperty("rowVersion").GetString()
        }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DepartmentWorkPackages.AnyAsync(item => item.Id == id))).Should().BeTrue();
        (await WithDbAsync(db => db.DepartmentFinanceReviews.AnyAsync(item => item.WorkPackageId == id))).Should().BeTrue();
    }

    [Fact]
    public async Task ManagerOutsidePackageScope_CannotPreviewDeleteOrReopen()
    {
        var seed = await SeedProjectScopeAsync();
        var id = await SeedLifecyclePackageAsync(seed, WorkPackageStatus.Completed);
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync($"/api/work-packages/{id}/deletion-impact")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, $"/api/work-packages/{id}", new { planToken = new string('a', 64), confirmation = "WP", rowVersion = Convert.ToBase64String(new byte[8]) })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Put, $"/api/work-packages/{id}", new { name = "Mở lại ngoài phạm vi", status = "Active", rowVersion = Convert.ToBase64String(new byte[8]) })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await WithDbAsync(db => db.DepartmentWorkPackages.AnyAsync(item => item.Id == id))).Should().BeTrue();
    }

    private Task<int> SeedLifecyclePackageAsync((int ProjectId, int DepartmentGroupId, int DepartmentLeadId, int DepartmentStaffId, int KpiDefinitionId) seed, WorkPackageStatus status) => WithDbAsync(async db =>
    {
        var admin = await db.Users.Where(item => item.RoleEntity != null && item.RoleEntity.Code == "SUPER_ADMIN").Select(item => item.Id).SingleAsync();
        var package = new DepartmentWorkPackage
        {
            Code = $"WP-REVIEW-{Guid.NewGuid():N}",
            Name = "Rà soát giao nhận vật tư NICON",
            OperationalProjectId = seed.ProjectId,
            DepartmentRoleGroupId = seed.DepartmentGroupId,
            LeadUserId = seed.DepartmentLeadId,
            Status = status,
            CreatedByUserId = admin,
            UpdatedByUserId = admin,
            CompletedAt = status == WorkPackageStatus.Completed ? DateTime.UtcNow : null,
            FinanceReview = new DepartmentFinanceReview { Status = FinanceReviewStatus.Approved, CreatedByUserId = admin, UpdatedByUserId = admin, ReviewedByUserId = seed.DepartmentLeadId, ReviewedAt = DateTime.UtcNow },
            Tasks = [new DepartmentWorkTask { WbsCode = "SUP-01", Title = "Xác nhận giao nhận vật tư", DepartmentRoleGroupId = seed.DepartmentGroupId,
                AssigneeUserId = seed.DepartmentStaffId, ApproverUserId = seed.DepartmentLeadId, KpiDefinitionId = seed.KpiDefinitionId,
                Status = WorkTaskStatus.Verified, ProgressPercent = 100, VerifiedByUserId = seed.DepartmentLeadId, VerifiedAt = DateTime.UtcNow,
                PlannedStart = DateTime.UtcNow, PlannedEnd = DateTime.UtcNow.AddDays(1), CreatedByUserId = admin, UpdatedByUserId = admin }],
        };
        db.DepartmentWorkPackages.Add(package);
        await db.SaveChangesAsync();
        return package.Id;
    });

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
