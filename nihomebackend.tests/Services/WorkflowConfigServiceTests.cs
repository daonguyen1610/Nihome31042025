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
        SeedRoles("SUPER_ADMIN", "ADMIN", "SALES_MANAGER", "BGD");
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
