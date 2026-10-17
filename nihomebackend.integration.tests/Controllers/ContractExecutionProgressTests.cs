using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public class ContractExecutionProgressTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Progress_EnforcesAuthenticationAndContractPermission()
    {
        (await Client.GetAsync("/api/contracts/1/execution-progress")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsCustomerAsync);
        (await Client.GetAsync("/api/contracts/1/execution-progress")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Progress_UsesExactContractSourcesAndDoesNotTreatHistoryAsCurrentApproval()
    {
        var user = await CreateUserAsync(["crm.contracts.view"]);
        var fixture = await SeedAsync(user.Id);
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var response = await Client.GetAsync($"/api/v1/contracts/{fixture.Id}/execution-progress");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<ContractExecutionProgressResponse>())!;
        body.Work!.PackageCount.Should().Be(13);
        body.Work.CancelledPackageCount.Should().Be(1);
        body.Work.TaskCount.Should().Be(4);
        body.Work.CompletedTaskCount.Should().Be(1);
        body.Work.VerifiedTaskCount.Should().Be(1);
        body.Work.CancelledTaskCount.Should().Be(1);
        body.Work.Sources.Should().HaveCount(10); // Counts are not limited by the source preview.
        body.Work.Sources.Should().NotContain(item => item.Code == "OTHER-CONTRACT");
        body.Qs!.DossierCount.Should().Be(6);
        body.Qs.ActiveApprovedCount.Should().Be(2); // Original + effective correction; no reversal/superseded/reversed.
        body.Qs.PendingReviewCount.Should().Be(1);
        body.Payment.Should().Be(new ContractPaymentProgress(3, 1, 1, 1000000000m, 200000000m));
        body.CalculatedAt.Kind.Should().Be(DateTimeKind.Utc);
        var raw = await ReadJsonAsync(response);
        raw.TryGetProperty("completionPercent", out _).Should().BeFalse();
        var workList = await ReadJsonAsync(await Client.GetAsync($"/api/work-packages?contractId={fixture.Id}&pageSize=1"));
        workList.GetProperty("totalCount").GetInt32().Should().Be(body.Work.PackageCount);
        (await Client.GetAsync("/api/contracts/2147483647/execution-progress")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Progress_DoesNotExposeRestrictedModuleCountsOrOtherOwnersContracts()
    {
        var owner = await CreateUserAsync(["crm.contracts.view"]);
        var fixture = await SeedAsync(owner.Id);
        await LoginAsync(owner.Phone);
        var restricted = await GetAsync(fixture.Id);
        restricted.Work.Should().BeNull();
        restricted.Qs.Should().BeNull();
        restricted.Payment.PaidCount.Should().Be(1);
        var other = await CreateUserAsync(["crm.contracts.view", "work.packages.view", "work.packages.view.all", "construction.qs.view"]);
        await LoginAsync(other.Phone);
        (await Client.GetAsync($"/api/contracts/{fixture.Id}/execution-progress")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var portfolio = await CreateUserAsync(["crm.contracts.view", "crm.contracts.view.all", "work.packages.view", "construction.qs.view"]);
        await LoginAsync(portfolio.Phone);
        var scoped = await GetAsync(fixture.Id);
        scoped.Work!.PackageCount.Should().Be(0);
        scoped.Qs.Should().BeNull(); // QS permission alone does not bypass project scope.
    }

    [Fact]
    public async Task Progress_HandlesEmptyAndDownstreamWithoutMixingDirections()
    {
        var user = await CreateUserAsync(["crm.contracts.view"]);
        var fixture = await SeedAsync(user.Id);
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var empty = await GetAsync(fixture.OtherId);
        empty.Payment.MilestoneCount.Should().Be(0);
        empty.Payment.ScheduledAmount.Should().Be(0);
        await WithDbAsync(async db =>
        {
            var contract = await db.Contracts.SingleAsync(item => item.Id == fixture.Id);
            contract.Direction = ContractDirection.Downstream;
            contract.Type = ContractType.Subcontract;
            await db.SaveChangesAsync();
        });
        var downstream = await GetAsync(fixture.Id);
        downstream.Qs!.DossierCount.Should().Be(1);
        downstream.Qs.Sources.Should().ContainSingle(item => item.Code == "DOWNSTREAM");
    }

    private Task LoginAsync(string phone) => AuthTestHelper.AuthenticateAsync(Client,
        c => AuthTestHelper.LoginAsync(c, phone, TestDataSeeder.DefaultPassword));
    private async Task<ContractExecutionProgressResponse> GetAsync(int id)
        => (await Client.GetFromJsonAsync<ContractExecutionProgressResponse>($"/api/contracts/{id}/execution-progress"))!;

    private Task<(int Id, string Phone)> CreateUserAsync(string[] grants) => WithDbAsync(async db =>
    {
        var role = new Role { Code = $"PROGRESS_{Guid.NewGuid():N}"[..30], Name = "Contract progress test", IsActive = true, InitialPermissionsSeeded = true };
        db.Roles.Add(role); await db.SaveChangesAsync();
        var ids = await db.Permissions.Where(item => grants.Contains(item.Module + "." + item.Action)).Select(item => item.Id).ToListAsync();
        ids.Count.Should().Be(grants.Length);
        db.RolePermissions.AddRange(ids.Select(id => new RolePermission { RoleId = role.Id, PermissionId = id }));
        var user = new ApplicationUser { PhoneNumber = "09" + Random.Shared.Next(10000000, 99999999), FullName = "Điều phối hợp đồng nhà máy Long Thành", Role = UserRole.USER, RoleEntityId = role.Id, IsActive = true };
        user.PasswordHash = new PasswordService().Hash(user, TestDataSeeder.DefaultPassword);
        db.Users.Add(user); await db.SaveChangesAsync(); return (user.Id, user.PhoneNumber!);
    });

    private Task<(int Id, int OtherId)> SeedAsync(int owner) => WithDbAsync(async db =>
    {
        var customer = new Customer { Name = "Chủ đầu tư Long Thành", Type = CustomerType.Company, SourceCode = "referral" };
        db.Customers.Add(customer); await db.SaveChangesAsync();
        var project = new OperationalProject { Code = $"PJ-{Guid.NewGuid():N}"[..24], Name = "Nhà máy Long Thành", CustomerId = customer.Id };
        db.OperationalProjects.Add(project); await db.SaveChangesAsync();
        Contract NewContract() => new() { ContractNumber = $"HD-{Guid.NewGuid():N}"[..28], CustomerId = customer.Id, OperationalProjectId = project.Id,
            OwnerUserId = owner, Direction = ContractDirection.Upstream, Type = ContractType.Construction, Value = 1000000000m };
        var contract = NewContract(); var other = NewContract(); db.Contracts.AddRange(contract, other); await db.SaveChangesAsync();
        var group = await db.RoleGroups.Select(item => item.Id).FirstAsync();
        var packages = Enumerable.Range(1, 13).Select(i => new DepartmentWorkPackage { Code = $"WP-{i}", Name = $"Gói công việc {i}",
            ContractId = contract.Id, OperationalProjectId = project.Id, Kind = WorkPackageKind.Project, DepartmentRoleGroupId = group,
            LeadUserId = owner, CreatedByUserId = owner, UpdatedByUserId = owner, Status = i == 13 ? WorkPackageStatus.Cancelled : WorkPackageStatus.Active }).ToList();
        db.DepartmentWorkPackages.AddRange(packages);
        db.DepartmentWorkPackages.Add(new DepartmentWorkPackage { Code = "OTHER-CONTRACT", Name = "Gói riêng cùng dự án", ContractId = other.Id,
            OperationalProjectId = project.Id, DepartmentRoleGroupId = group, LeadUserId = owner, CreatedByUserId = owner, UpdatedByUserId = owner });
        await db.SaveChangesAsync();
        foreach (var status in new[] { WorkTaskStatus.Planned, WorkTaskStatus.Completed, WorkTaskStatus.Verified, WorkTaskStatus.Cancelled })
            db.DepartmentWorkTasks.Add(new DepartmentWorkTask { WorkPackageId = packages[0].Id, Title = status.ToString(), WbsCode = status.ToString(),
                DepartmentRoleGroupId = group, AssigneeUserId = owner, Status = status, PlannedStart = new DateTime(2026, 10, 1), PlannedEnd = new DateTime(2026, 10, 31), CreatedByUserId = owner, UpdatedByUserId = owner });
        db.DepartmentWorkTasks.Add(new DepartmentWorkTask { WorkPackageId = packages[12].Id, Title = "Không tính nhóm hủy", WbsCode = "CANCELLED", DepartmentRoleGroupId = group, AssigneeUserId = owner, Status = WorkTaskStatus.Verified, CreatedByUserId = owner, UpdatedByUserId = owner });
        ConstructionQsBatch Qs(string code, ConstructionReviewStatus status, ConstructionQsRecordState state = ConstructionQsRecordState.Active, ConstructionQsAdjustmentKind kind = ConstructionQsAdjustmentKind.Original)
            => new() { Code = code, ContractId = contract.Id, OperationalProjectId = project.Id, Direction = ConstructionQsDirection.Upstream,
                Status = status, RecordState = state, AdjustmentKind = kind, IsActiveLineage = status == ConstructionReviewStatus.CmApproved,
                WbsCode = "1.1", PeriodFrom = new DateTime(2026, 10, 1), PeriodTo = new DateTime(2026, 10, 31), CreatedByUserId = owner };
        db.ConstructionQsBatches.AddRange(Qs("CURRENT", ConstructionReviewStatus.CmApproved),
            Qs("CORRECTION", ConstructionReviewStatus.CmApproved, kind: ConstructionQsAdjustmentKind.SupersedingCorrection),
            Qs("OLD", ConstructionReviewStatus.CmApproved, ConstructionQsRecordState.Superseded),
            Qs("REVERSED", ConstructionReviewStatus.CmApproved, ConstructionQsRecordState.Reversed),
            Qs("REVERSAL", ConstructionReviewStatus.CmApproved, kind: ConstructionQsAdjustmentKind.Reversal),
            Qs("PENDING", ConstructionReviewStatus.PendingCmReview));
        var wrongDirection = Qs("DOWNSTREAM", ConstructionReviewStatus.Draft); wrongDirection.Direction = ConstructionQsDirection.Downstream;
        var wrongContract = Qs("OTHER", ConstructionReviewStatus.CmApproved); wrongContract.ContractId = other.Id;
        var wrongProject = Qs("WRONG-PROJECT", ConstructionReviewStatus.Draft); wrongProject.OperationalProjectId = project.Id + 100000;
        db.ConstructionQsBatches.AddRange(wrongDirection, wrongContract, wrongProject);
        db.ContractPaymentMilestones.AddRange(new ContractPaymentMilestone { ContractId = contract.Id, Order = 1, Name = "Tạm ứng", PercentValue = 20, Status = PaymentMilestoneStatus.Paid },
            new ContractPaymentMilestone { ContractId = contract.Id, Order = 2, Name = "Giai đoạn kết cấu", PercentValue = 30, Status = PaymentMilestoneStatus.Requested },
            new ContractPaymentMilestone { ContractId = contract.Id, Order = 3, Name = "Bàn giao", PercentValue = 50, Status = PaymentMilestoneStatus.Pending });
        await db.SaveChangesAsync(); return (contract.Id, other.Id);
    });
}
