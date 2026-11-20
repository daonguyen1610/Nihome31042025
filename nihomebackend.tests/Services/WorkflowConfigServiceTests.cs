using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class WorkflowConfigServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly WorkflowConfigService _sut;

    public WorkflowConfigServiceTests()
    {
        _db = DbContextFactory.Create();
        SeedRoles("SUPER_ADMIN", "ADMIN", "SALES_MANAGER", "BGD", "PM");
        // The quote workflow needs approvers who can view and approve quotes; PM cannot.
        GrantQuoteApproval("SUPER_ADMIN", "ADMIN", "SALES_MANAGER", "BGD");
        _sut = new WorkflowConfigService(_db, NullLogger<WorkflowConfigService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private void SeedRoles(params string[] codes)
    {
        foreach (var c in codes)
        {
            _db.Roles.Add(new Role { Code = c, Name = c });
        }
        _db.SaveChanges();
    }

    private void GrantQuoteApproval(params string[] codes)
    {
        var permissions = new[] { "view", "approve" }
            .Select(action => new Permission { Module = "crm.quotes", Action = action }).ToList();
        _db.Permissions.AddRange(permissions);
        _db.SaveChanges();
        foreach (var role in _db.Roles.Where(role => codes.Contains(role.Code)).ToList())
            foreach (var permission in permissions)
                _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Create_QuoteWorkflow_RejectsARoleThatCannotApproveQuotes()
    {
        var error = await Assert.ThrowsAsync<WorkflowConfigValidationException>(() => _sut.CreateAsync(
            Req(steps: [(1, "Trưởng phòng Kinh doanh duyệt", "SALES_MANAGER"), (2, "Quản lý dự án duyệt", "PM")])));

        Assert.Equal("QUOTE_WORKFLOW_STEPS_BLOCKED", error.Code);
        Assert.Contains("Bước 2 (PM): vai trò chưa có quyền", error.Message);
        // Other business actions are not applied to quotes, so their roles are not checked.
        var contracts = await _sut.CreateAsync(Req(module: "contracts", action: "sign",
            steps: [(1, "Quản lý dự án duyệt", "PM")]));
        Assert.Equal("contracts", contracts.Module);
    }

    [Fact]
    public async Task Create_QuoteWorkflow_RejectsAnInactiveRole()
    {
        _db.Roles.Single(role => role.Code == "BGD").IsActive = false;
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<WorkflowConfigValidationException>(() => _sut.CreateAsync(
            Req(steps: [(1, "Trưởng phòng Kinh doanh duyệt", "SALES_MANAGER"), (2, "Ban Giám đốc duyệt", "BGD")])));

        Assert.Equal("QUOTE_WORKFLOW_STEPS_BLOCKED", error.Code);
        Assert.Contains("Bước 2 (BGD): vai trò đã ngừng dùng.", error.Message);
    }

    [Fact]
    public async Task Create_QuoteWorkflow_NamesEveryBlockedStepInOrder()
    {
        _db.Roles.Single(role => role.Code == "BGD").IsActive = false;
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<WorkflowConfigValidationException>(() => _sut.CreateAsync(
            Req(steps: [(1, "Quản lý dự án duyệt", "PM"), (2, "Trưởng phòng Kinh doanh duyệt", "SALES_MANAGER"),
                (3, "Ban Giám đốc duyệt", "BGD")])));

        // Both blocked steps at once, so the administrator fixes them in one go.
        var message = error.Message;
        Assert.True(message.IndexOf("Bước 1 (PM)", StringComparison.Ordinal) <
            message.IndexOf("Bước 3 (BGD)", StringComparison.Ordinal), message);
        Assert.DoesNotContain("Bước 2", message);
    }

    [Fact]
    public async Task Get_QuoteWorkflow_FlagsAStepWhoseRoleWasDeleted()
    {
        var created = await _sut.CreateAsync(Req(steps: [(1, "Trưởng phòng Kinh doanh duyệt", "SALES_MANAGER")]));
        var row = _db.WorkflowConfigs.Single(item => item.Id == created.Id);
        row.StepsJson = row.StepsJson.Replace("SALES_MANAGER", "RETIRED_ROLE");
        await _db.SaveChangesAsync();

        var issue = Assert.Single((await _sut.GetByIdAsync(created.Id))!.StepIssues);

        Assert.Equal((1, "RoleMissing"), (issue.Order, issue.Issue));
        // Saving it while on names the step, as for any other blocked step.
        var error = await Assert.ThrowsAsync<WorkflowConfigValidationException>(() =>
            _sut.UpdateAsync(created.Id, Req(steps: [(1, "Bước cũ", "RETIRED_ROLE")])));
        Assert.Equal("QUOTE_WORKFLOW_STEPS_BLOCKED", error.Code);
        Assert.Contains("Bước 1: vai trò của bước đã bị xoá.", error.Message);
    }

    [Fact]
    public async Task Update_QuoteWorkflow_CanBeTurnedOffWhileAStepIsBlocked()
    {
        var created = await _sut.CreateAsync(Req(steps: [(1, "Ban Giám đốc duyệt", "BGD")]));
        _db.Roles.Single(role => role.Code == "BGD").IsActive = false;
        await _db.SaveChangesAsync();

        var off = Req(steps: [(1, "Ban Giám đốc duyệt", "BGD")]);
        off.IsActive = false;
        var saved = await _sut.UpdateAsync(created.Id, off);

        Assert.False(saved!.IsActive);
        // A workflow that is off is not applied, so it carries no warnings.
        Assert.Empty(saved.StepIssues);
    }

    [Fact]
    public async Task Create_QuoteWorkflow_RejectsAnEveryoneStepRoleRepeated()
    {
        var request = Req(steps: [(1, "Ban Giám đốc duyệt", "BGD"), (2, "Ban Giám đốc phê duyệt", "BGD")]);
        request.Steps[1].RequireAllApprovers = true;

        var error = await Assert.ThrowsAsync<WorkflowConfigValidationException>(() => _sut.CreateAsync(request));

        Assert.Equal("QUOTE_WORKFLOW_EVERYONE_ROLE_REPEATED", error.Code);
        // The same role in two steps where one person is chosen each time is allowed.
        request.Steps[1].RequireAllApprovers = false;
        Assert.Equal(2, (await _sut.CreateAsync(request)).Steps.Count);
    }

    [Fact]
    public async Task Get_QuoteWorkflow_NamesStepsNobodyCanApproveToday()
    {
        // As a row saved before the checks existed: a role without the grants, and a role
        // with them but nobody in it.
        _db.WorkflowConfigs.Add(new WorkflowConfig
        {
            Module = "quotes",
            Action = "approve",
            Name = "Duyệt báo giá",
            StepsJson = """[{"order":1,"name":"Quản lý dự án duyệt","approverRoleCode":"PM","slaHours":24,"requireAllApprovers":false},{"order":2,"name":"Ban Giám đốc duyệt","approverRoleCode":"BGD","slaHours":24,"requireAllApprovers":false}]""",
        });
        _db.WorkflowConfigs.Add(new WorkflowConfig
        {
            Module = "contracts",
            Action = "sign",
            Name = "Ký hợp đồng",
            StepsJson = """[{"order":1,"name":"Quản lý dự án duyệt","approverRoleCode":"PM","slaHours":24,"requireAllApprovers":false}]""",
        });
        await _db.SaveChangesAsync();

        var all = await _sut.ListAsync(includeInactive: true);

        var quote = all.Single(item => item.Module == "quotes");
        Assert.Equal([(1, "RoleCannotApprove"), (2, "NoEligibleUser")],
            quote.StepIssues.Select(issue => (issue.Order, issue.Issue)).ToList());
        // Other workflows are not applied, so they carry no issues.
        Assert.Empty(all.Single(item => item.Module == "contracts").StepIssues);
    }

    private static UpsertWorkflowConfigRequest Req(
        string module = "quotes",
        string action = "approve",
        string name = "Duyệt báo giá",
        params (int order, string name, string role)[] steps)
    {
        if (steps.Length == 0)
        {
            steps = new[] { (1, "Trưởng nhóm", "SALES_MANAGER") };
        }
        return new UpsertWorkflowConfigRequest
        {
            Module = module,
            Action = action,
            Name = name,
            IsActive = true,
            SortOrder = 0,
            Steps = steps.Select(s => new WorkflowStepRequest
            {
                Order = s.order,
                Name = s.name,
                ApproverRoleCode = s.role,
                SlaHours = 24,
                RequireAllApprovers = false,
            }).ToList(),
        };
    }

    // ---------------- Create ----------------

    [Fact]
    public async Task Create_PersistsWorkflow_WithSortedSteps()
    {
        var result = await _sut.CreateAsync(Req(
            steps: new[] { (2, "Giám đốc", "BGD"), (1, "Trưởng nhóm", "SALES_MANAGER") }));

        Assert.True(result.Id > 0);
        Assert.Equal("quotes", result.Module);
        Assert.Equal("approve", result.Action);
        Assert.Equal(2, result.Steps.Count);
        Assert.Equal(1, result.Steps[0].Order);
        Assert.Equal("SALES_MANAGER", result.Steps[0].ApproverRoleCode);
        Assert.Equal(2, result.Steps[1].Order);
    }

    [Fact]
    public async Task Create_NormalizesModuleAndActionToLowercase()
    {
        var result = await _sut.CreateAsync(Req(module: "QUOTES", action: "Approve"));
        Assert.Equal("quotes", result.Module);
        Assert.Equal("approve", result.Action);
    }

    [Fact]
    public async Task Create_DuplicateModuleActionPair_Throws()
    {
        await _sut.CreateAsync(Req());
        await Assert.ThrowsAsync<WorkflowConfigDuplicateException>(
            () => _sut.CreateAsync(Req()));
    }

    [Fact]
    public async Task Create_WithEmptySteps_Throws()
    {
        var req = Req();
        req.Steps.Clear();
        await Assert.ThrowsAsync<WorkflowConfigValidationException>(() => _sut.CreateAsync(req));
    }

    [Fact]
    public async Task Create_WithDuplicateStepOrder_Throws()
    {
        var req = Req(steps: new[] { (1, "A", "SALES_MANAGER"), (1, "B", "BGD") });
        await Assert.ThrowsAsync<WorkflowConfigValidationException>(() => _sut.CreateAsync(req));
    }

    [Fact]
    public async Task Create_WithUnknownApproverRole_Throws()
    {
        var req = Req(steps: new[] { (1, "Trưởng nhóm", "GHOST_ROLE") });
        await Assert.ThrowsAsync<WorkflowConfigValidationException>(() => _sut.CreateAsync(req));
    }

    // ---------------- Update ----------------

    [Fact]
    public async Task Update_ChangesFields_AndReserializesSteps()
    {
        var created = await _sut.CreateAsync(Req());

        var req = Req(module: "quotes", action: "approve", name: "Renamed",
            steps: new[] { (1, "Trưởng nhóm", "SALES_MANAGER"), (2, "Giám đốc", "BGD") });
        req.Description = "Updated";
        req.IsActive = false;

        var updated = await _sut.UpdateAsync(created.Id, req);

        Assert.NotNull(updated);
        Assert.Equal("Renamed", updated!.Name);
        Assert.False(updated.IsActive);
        Assert.Equal("Updated", updated.Description);
        Assert.Equal(2, updated.Steps.Count);
    }

    [Fact]
    public async Task Update_MissingId_ReturnsNull()
    {
        var result = await _sut.UpdateAsync(9999, Req());
        Assert.Null(result);
    }

    [Fact]
    public async Task Update_DuplicateModuleActionPairOnDifferentRow_Throws()
    {
        var a = await _sut.CreateAsync(Req(module: "quotes", action: "approve"));
        await _sut.CreateAsync(Req(module: "quotes", action: "sign"));

        var req = Req(module: "quotes", action: "sign");
        await Assert.ThrowsAsync<WorkflowConfigDuplicateException>(
            () => _sut.UpdateAsync(a.Id, req));
    }

    // ---------------- List / Get / Delete ----------------

    [Fact]
    public async Task ListAsync_ExcludesInactiveByDefault()
    {
        var a = await _sut.CreateAsync(Req(action: "approve"));
        var b = await _sut.CreateAsync(Req(action: "sign"));
        var reqInactive = Req(action: "sign");
        reqInactive.IsActive = false;
        await _sut.UpdateAsync(b.Id, reqInactive);

        var active = await _sut.ListAsync();
        Assert.Single(active);
        Assert.Equal(a.Id, active[0].Id);
    }

    [Fact]
    public async Task ListAsync_IncludeInactive_ReturnsAll()
    {
        await _sut.CreateAsync(Req(action: "approve"));
        var b = await _sut.CreateAsync(Req(action: "sign"));
        var reqInactive = Req(action: "sign");
        reqInactive.IsActive = false;
        await _sut.UpdateAsync(b.Id, reqInactive);

        var all = await _sut.ListAsync(includeInactive: true);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRow_AndSecondCallReturnsFalse()
    {
        var created = await _sut.CreateAsync(Req());
        Assert.True(await _sut.DeleteAsync(created.Id));
        Assert.False(await _sut.DeleteAsync(created.Id));
        Assert.Null(await _sut.GetByIdAsync(created.Id));
    }
}
