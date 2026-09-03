using System.Text.Json;
using NihomeBackend.Data;
using NihomeBackend.Models;
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
    public void Seed_MatchesWorkflowAndRoleCodesCaseInsensitively()
    {
        var salesManager = _db.Roles.Single(role => role.Code == "SALES_MANAGER");
        salesManager.Code = "sales_manager";
        _db.WorkflowConfigs.Add(new WorkflowConfig
        {
            Module = "QUOTES",
            Action = "APPROVE",
            Name = "Admin case-preserved workflow",
            StepsJson = "[{\"order\":1,\"name\":\"Custom\",\"approverRoleCode\":\"sales_manager\"}]",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();

        WorkflowConfigSeeder.Seed(_db);

        var matches = _db.WorkflowConfigs.Where(workflow =>
            workflow.Module.ToLower() == "quotes" && workflow.Action.ToLower() == "approve").ToList();
        var workflow = Assert.Single(matches);
        Assert.Equal("Admin case-preserved workflow", workflow.Name);
        Assert.False(workflow.IsActive);
    }

    [Fact]
    public void Seed_RepairsOnlyBlankNameAndEmptyStepsOnExistingDefault()
    {
        _db.WorkflowConfigs.Add(new WorkflowConfig
        {
            Module = "quotes",
            Action = "approve",
            Name = " ",
            Description = "Admin description",
            StepsJson = "[]",
            IsActive = false,
            SortOrder = 77,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();

        WorkflowConfigSeeder.Seed(_db);

        var workflow = _db.WorkflowConfigs.Single(item => item.Module == "quotes" && item.Action == "approve");
        Assert.False(string.IsNullOrWhiteSpace(workflow.Name));
        Assert.NotEqual("[]", workflow.StepsJson);
        Assert.Equal("Admin description", workflow.Description);
        Assert.False(workflow.IsActive);
        Assert.Equal(77, workflow.SortOrder);
    }

    [Fact]
    public void Seed_RepairsMalformedStepsOnExistingDefault()
    {
        _db.WorkflowConfigs.Add(new WorkflowConfig
        {
            Module = "quotes",
            Action = "approve",
            Name = "Admin workflow name",
            Description = "Admin description",
            StepsJson = "{not-json",
            IsActive = false,
            SortOrder = 77,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();

        WorkflowConfigSeeder.Seed(_db);

        var workflow = _db.WorkflowConfigs.Single(item => item.Module == "quotes" && item.Action == "approve");
        using var document = JsonDocument.Parse(workflow.StepsJson);
        Assert.True(document.RootElement.GetArrayLength() > 0);
        Assert.Equal("Admin workflow name", workflow.Name);
        Assert.Equal("Admin description", workflow.Description);
        Assert.False(workflow.IsActive);
        Assert.Equal(77, workflow.SortOrder);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{not-json")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[{}]")]
    [InlineData("[{\"approverRoleCode\":\"ADMIN\",\"order\":1}]")]
    [InlineData("[{\"name\":\"Approve\",\"order\":1}]")]
    [InlineData("[{\"name\":\"Approve\",\"approverRoleCode\":\"ADMIN\"}]")]
    [InlineData("[{\"name\":\"Approve\",\"approverRoleCode\":\"ADMIN\",\"order\":0}]")]
    [InlineData("[{\"name\":\"Approve\",\"approverRoleCode\":\"ADMIN\",\"order\":-1}]")]
    [InlineData("[{\"name\":\"Approve\",\"approverRoleCode\":\"UNKNOWN_ROLE\",\"order\":1}]")]
    [InlineData("[{\"name\":\"One\",\"approverRoleCode\":\"ADMIN\",\"order\":1},{\"name\":\"Two\",\"approverRoleCode\":\"BGD\",\"order\":1}]")]
    [InlineData("[{\"name\":\"Approve\",\"approverRoleCode\":\"ADMIN\",\"order\":1},{}]")]
    public void Seed_RepairsStructurallyUnusableSteps(string stepsJson)
    {
        _db.WorkflowConfigs.Add(new WorkflowConfig
        {
            Module = "quotes",
            Action = "approve",
            Name = "Admin workflow name",
            StepsJson = stepsJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();

        WorkflowConfigSeeder.Seed(_db);

        var workflow = _db.WorkflowConfigs.Single(item => item.Module == "quotes" && item.Action == "approve");
        using var document = JsonDocument.Parse(workflow.StepsJson);
        var steps = document.RootElement.EnumerateArray().ToList();
        Assert.NotEmpty(steps);
        Assert.All(steps, step =>
        {
            Assert.False(string.IsNullOrWhiteSpace(step.GetProperty("name").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(step.GetProperty("approverRoleCode").GetString()));
            Assert.True(step.GetProperty("order").GetInt32() > 0);
        });
    }

    [Fact]
    public void Seed_PreservesValidCustomSteps()
    {
        const string customSteps = "[{\"order\":42,\"name\":\"Custom approval\",\"approverRoleCode\":\"ADMIN\",\"customField\":true}]";
        _db.WorkflowConfigs.Add(new WorkflowConfig
        {
            Module = "quotes",
            Action = "approve",
            Name = "Admin workflow name",
            StepsJson = customSteps,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();

        WorkflowConfigSeeder.Seed(_db);

        Assert.Equal(customSteps, _db.WorkflowConfigs.Single(item =>
            item.Module == "quotes" && item.Action == "approve").StepsJson);
    }

    [Fact]
    public void Seed_RejectsDuplicateWorkflowDefinitionsBeforeInsertion()
    {
        const string json = """
            { "workflows": [
              { "module": "quotes", "action": "approve", "name": "One", "steps": [
                { "name": "Approve", "approverRoleCode": "ADMIN", "order": 1 }
              ] },
              { "module": "QUOTES", "action": "APPROVE", "name": "Two", "steps": [
                { "name": "Approve", "approverRoleCode": "ADMIN", "order": 1 }
              ] }
            ] }
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        var error = Assert.Throws<InvalidDataException>(() => WorkflowConfigSeeder.Seed(_db, stream));

        Assert.Contains("quotes|approve", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_db.WorkflowConfigs);
    }

        [Theory]
        [InlineData(0, 2)]
        [InlineData(-1, 2)]
        [InlineData(1, 1)]
        public void Seed_RejectsInvalidIncomingStepOrders(int firstOrder, int secondOrder)
        {
                var json = $$"""
                        { "workflows": [
                            { "module": "quotes", "action": "approve", "name": "Approve quote", "steps": [
                                { "name": "One", "approverRoleCode": "ADMIN", "order": {{firstOrder}} },
                                { "name": "Two", "approverRoleCode": "BGD", "order": {{secondOrder}} }
                            ] }
                        ] }
                        """;
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

                var error = Assert.Throws<InvalidDataException>(() => WorkflowConfigSeeder.Seed(_db, stream));

                Assert.Contains("quotes|approve", error.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Empty(_db.WorkflowConfigs);
        }

            [Theory]
            [InlineData("{ \"module\": \"quotes\", \"action\": \"approve\", \"steps\": [] }")]
            [InlineData("{ \"module\": \"quotes\", \"action\": \"approve\", \"name\": \"Approve\", \"steps\": {} }")]
            [InlineData("{ \"module\": \"quotes\", \"action\": \"approve\", \"name\": \"Approve\", \"steps\": [] }")]
            [InlineData("{ \"module\": \"quotes\", \"action\": \"approve\", \"name\": \"Approve\", \"steps\": [{ \"name\": \"One\", \"approverRoleCode\": \"ADMIN\" }] }")]
            [InlineData("{ \"module\": \"quotes\", \"action\": \"approve\", \"name\": \"Approve\", \"steps\": [{ \"name\": \"One\", \"approverRoleCode\": \"ADMIN\", \"order\": \"first\" }] }")]
            [InlineData("{ \"module\": \"quotes\", \"action\": \"approve\", \"name\": \"Approve\", \"steps\": [{ \"name\": \"One\", \"approverRoleCode\": \"UNKNOWN\", \"order\": 1 }] }")]
            public void Seed_RejectsMalformedIncomingWorkflowWithoutPersistence(string workflowJson)
            {
                var json = $"{{ \"workflows\": [{workflowJson}] }}";
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

                Assert.Throws<InvalidDataException>(() => WorkflowConfigSeeder.Seed(_db, stream));

                Assert.Empty(_db.WorkflowConfigs);
            }

            [Theory]
            [InlineData("{}")]
            [InlineData("{ \"workflows\": {} }")]
            [InlineData("{ \"workflows\": [] }")]
            public void Seed_RejectsInvalidWorkflowManifestRoot(string json)
            {
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

                Assert.Throws<InvalidDataException>(() => WorkflowConfigSeeder.Seed(_db, stream));

                Assert.Empty(_db.WorkflowConfigs);
            }

            [Fact]
            public void Seed_InvalidLaterWorkflowDoesNotPersistEarlierValidWorkflow()
            {
                const string json = """
                    { "workflows": [
                      { "module": "quotes", "action": "approve", "name": "Approve quote", "steps": [
                        { "name": "Approve", "approverRoleCode": "ADMIN", "order": 1 }
                      ] },
                      { "module": "contracts", "action": "sign", "name": "Sign contract", "steps": [] }
                    ] }
                    """;
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

                Assert.Throws<InvalidDataException>(() => WorkflowConfigSeeder.Seed(_db, stream));

                Assert.Empty(_db.WorkflowConfigs);
            }

    [Fact]
            public void Seed_RejectsWorkflowsThatReferenceUnknownRoles()
    {
        var designLead = _db.Roles.Single(r => r.Code == "DESIGN_LEAD");
        _db.Roles.Remove(designLead);
        _db.SaveChanges();

                var error = Assert.Throws<InvalidDataException>(() => WorkflowConfigSeeder.Seed(_db));

                Assert.Contains("DESIGN_LEAD", error.Message);
                Assert.Empty(_db.WorkflowConfigs);
    }
}
