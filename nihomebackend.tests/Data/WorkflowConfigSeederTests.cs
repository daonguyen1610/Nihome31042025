using System.Text.Json;
using NihomeBackend.Data;
using NihomeBackend.Models.Rbac;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

/// <summary>
/// Verifies WorkflowConfigSeeder is idempotent, references only known RBAC
/// roles, and preserves admin edits made after the initial seed.
/// </summary>
public class WorkflowConfigSeederTests : IDisposable
{
    private readonly AppDbContext _db;

    public WorkflowConfigSeederTests()
    {
        _db = DbContextFactory.Create();
        SeedRoles(
            "SALES_MANAGER", "BGD", "LEGAL_OFFICER", "QS",
            "DESIGN_LEAD", "PM", "SUPER_ADMIN", "ADMIN");
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

    [Fact]
    public void Seed_LoadsShippedDefaults()
    {
        WorkflowConfigSeeder.Seed(_db);

        var pairs = _db.WorkflowConfigs
            .Select(w => w.Module + "|" + w.Action)
            .ToHashSet();

        Assert.Contains("quotes|approve", pairs);
        Assert.Contains("contracts|sign", pairs);
        Assert.Contains("tenders|submit", pairs);
    }

    [Fact]
    public void Seed_SerialisesStepsAsOrderedJson()
    {
        WorkflowConfigSeeder.Seed(_db);

        var contract = _db.WorkflowConfigs.Single(w => w.Module == "contracts" && w.Action == "sign");
        using var doc = JsonDocument.Parse(contract.StepsJson);
        var steps = doc.RootElement.EnumerateArray().ToList();

        Assert.Equal(3, steps.Count);
        Assert.Equal(1, steps[0].GetProperty("order").GetInt32());
        Assert.Equal(2, steps[1].GetProperty("order").GetInt32());
        Assert.Equal(3, steps[2].GetProperty("order").GetInt32());
        Assert.Equal("LEGAL_OFFICER", steps[0].GetProperty("approverRoleCode").GetString());
    }

    [Fact]
    public void Seed_IsIdempotent_AndDoesNotOverwriteAdminEdits()
    {
        WorkflowConfigSeeder.Seed(_db);
        var quote = _db.WorkflowConfigs.Single(w => w.Module == "quotes" && w.Action == "approve");
        quote.Name = "Custom admin rename";
        quote.IsActive = false;
        _db.SaveChanges();

        var countBefore = _db.WorkflowConfigs.Count();
        WorkflowConfigSeeder.Seed(_db);
        var countAfter = _db.WorkflowConfigs.Count();

        Assert.Equal(countBefore, countAfter);
        var reloaded = _db.WorkflowConfigs.Single(w => w.Module == "quotes" && w.Action == "approve");
        Assert.Equal("Custom admin rename", reloaded.Name);
        Assert.False(reloaded.IsActive);
    }

    [Fact]
    public void Seed_SkipsWorkflowsThatReferenceUnknownRoles()
    {
        // Drop DESIGN_LEAD so the concept-approval + IFC workflows lose their
        // only approver. The seeder should drop those steps and — because both
        // workflows end up with zero valid steps — skip the workflows.
        var designLead = _db.Roles.Single(r => r.Code == "DESIGN_LEAD");
        _db.Roles.Remove(designLead);
        _db.SaveChanges();

        WorkflowConfigSeeder.Seed(_db);

        Assert.False(_db.WorkflowConfigs.Any(w => w.Module == "design.concepts"));
        var ifc = _db.WorkflowConfigs.SingleOrDefault(w => w.Module == "design.shop");
        // design.shop has 2 steps (DESIGN_LEAD + PM); one drops out and only
        // PM remains, so the workflow is still seeded with one step.
        Assert.NotNull(ifc);
        using var doc = JsonDocument.Parse(ifc!.StepsJson);
        Assert.Single(doc.RootElement.EnumerateArray());
        Assert.Equal("PM", doc.RootElement[0].GetProperty("approverRoleCode").GetString());
    }
}
