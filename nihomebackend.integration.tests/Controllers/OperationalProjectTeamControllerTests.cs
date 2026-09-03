using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class OperationalProjectTeamControllerTests : IntegrationTestBase
{
    public OperationalProjectTeamControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Get_AuthenticatedManager_ReturnsProjectTeam()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();

        var response = await Client.GetAsync($"/api/operational-projects/{projectId}/team");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("operationalProjectId").GetInt32().Should().Be(projectId);
        body.GetProperty("canManage").GetBoolean().Should().BeTrue();
        body.GetProperty("roleDefinitions").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Get_WithoutGlobalPermission_ReturnsForbidden()
    {
        var projectId = await CreateProjectAsync();
        await AuthenticateAsync("USER");

        (await Client.GetAsync($"/api/operational-projects/{projectId}/team"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_NonmemberCrossProject_ReturnsNotFound()
    {
        var designLeadId = await UserIdAsync("DESIGN_LEAD");
        var projectId = await CreateProjectAsync(designLeadId);
        await AuthenticateAsync("PM");

        (await Client.GetAsync($"/api/operational-projects/{projectId}/team"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProjectManagerWithGlobalManageCanMutate_DesignLeadWithoutItIsForbidden()
    {
        var projectManagerId = await UserIdAsync("PM");
        var designLeadId = await UserIdAsync("DESIGN_LEAD");
        var observerId = await UserIdAsync("DESIGN");
        var architectId = await UserIdAsync("ARCHITECT");
        var mepId = await UserIdAsync("MEP_ENGINEER");
        var structuralId = await UserIdAsync("STRUCT_ENGINEER");
        var projectId = await CreateProjectAsync(projectManagerId);
        await SeedMemberAsync(projectId, designLeadId, ProjectTeamRoleCode.DesignLead);
        await SeedMemberAsync(projectId, observerId, ProjectTeamRoleCode.Observer);

        await AuthenticateAsync("PM");
        (await PostMemberAsync(projectId, architectId, "Architect", "Architect"))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        await AuthenticateAsync("DESIGN_LEAD");
        (await PostMemberAsync(projectId, mepId, "MEP Engineer", "MepEngineer"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthenticateAsync("DESIGN");
        var team = await Client.GetAsync($"/api/operational-projects/{projectId}/team");
        team.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(team)).GetProperty("canManage").GetBoolean().Should().BeFalse();
        (await PostMemberAsync(projectId, structuralId, "Structural Engineer", "StructuralEngineer"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddMember_IdempotentReplayAndKeyConflict_DoNotDuplicate()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        var userId = await UserIdAsync("ARCHITECT");
        var key = $"team-member-{Guid.NewGuid():N}";
        var payload = MemberPayload(userId, "Project Architect", "Architect");

        using var first = await SendJsonWithKeyAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{projectId}/team/members",
            payload,
            key);
        using var replay = await SendJsonWithKeyAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{projectId}/team/members",
            payload,
            key);
        using var conflict = await SendJsonWithKeyAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{projectId}/team/members",
            MemberPayload(userId, "Different Position", "Architect"),
            key);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.Headers.GetValues("Idempotency-Replayed").Should().ContainSingle("true");
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.OperationalProjectMembers.CountAsync(member =>
            member.OperationalProjectId == projectId && member.UserId == userId))).Should().Be(1);
    }

    [Fact]
    public async Task UpdateAssignment_WithStaleRowVersionAndIdempotencyKey_ReturnsConflictThenAllowsRetry()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        var userId = await UserIdAsync("ARCHITECT");
        using var memberResponse = await PostMemberAsync(projectId, userId, "Architect", "Architect");
        var memberId = (await ReadJsonAsync(memberResponse)).GetProperty("id").GetInt32();
        using var createdResponse = await SendJsonWithKeyAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{projectId}/team/assignments",
            AssignmentPayload("DES-STALE", memberId),
            $"team-assignment-{Guid.NewGuid():N}");
        var created = await ReadJsonAsync(createdResponse);
        var assignmentId = created.GetProperty("id").GetInt32();
        var staleRowVersion = created.GetProperty("rowVersion").GetString();
        await WithDbAsync(async db =>
        {
            var assignment = await db.OperationalProjectAssignments.FindAsync(assignmentId);
            assignment!.Title = "Competing update";
            await db.SaveChangesAsync();
        });
        var updateKey = $"team-assignment-update-{Guid.NewGuid():N}";
        using var staleUpdate = await SendJsonWithKeyAsync(
            HttpMethod.Put,
            $"/api/operational-projects/{projectId}/team/assignments/{assignmentId}",
            AssignmentPayload("DES-STALE", memberId, rowVersion: staleRowVersion),
            updateKey);

        var staleBody = await staleUpdate.Content.ReadAsStringAsync();
        staleUpdate.StatusCode.Should().Be(HttpStatusCode.Conflict, staleBody);
        (await ReadJsonAsync(staleUpdate)).GetProperty("code").GetString()
            .Should().Be("crm_concurrency_conflict");
        var team = await ReadJsonAsync(await Client.GetAsync($"/api/operational-projects/{projectId}/team"));
        team.GetProperty("assignments")[0].GetProperty("title").GetString().Should().Be("Competing update");
        var currentRowVersion = team.GetProperty("assignments")[0].GetProperty("rowVersion").GetString();

        using var retry = await SendJsonWithKeyAsync(
            HttpMethod.Put,
            $"/api/operational-projects/{projectId}/team/assignments/{assignmentId}",
            AssignmentPayload("DES-STALE", memberId, rowVersion: currentRowVersion),
            updateKey);

        retry.StatusCode.Should().Be(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
        (await WithDbAsync(db => db.IdempotencyRecords.CountAsync(record => record.Key == updateKey)))
            .Should().Be(1);
    }

    [Fact]
    public async Task AssignmentReplay_HistoryAndKpiIdentity_RemainUniqueAndImmutable()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        var userId = await UserIdAsync("ARCHITECT");
        using var memberResponse = await PostMemberAsync(projectId, userId, "Architect", "Architect");
        var member = await ReadJsonAsync(memberResponse);
        var memberId = member.GetProperty("id").GetInt32();
        var key = $"team-assignment-{Guid.NewGuid():N}";
        var payload = AssignmentPayload("DES-ARCH-01", memberId);

        using var createdResponse = await SendJsonWithKeyAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{projectId}/team/assignments",
            payload,
            key);
        var created = await ReadJsonAsync(createdResponse);
        using var replay = await SendJsonWithKeyAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{projectId}/team/assignments",
            payload,
            key);
        var assignmentId = created.GetProperty("id").GetInt32();
        var originalKpiIdentity = created.GetProperty("kpiIdentity").GetString();
        var originalSnapshot = await WithDbAsync(db => db.OperationalProjectTeamHistory
            .Where(item => item.OperationalProjectId == projectId && item.EntityType == "Assignment")
            .Select(item => item.SnapshotJson)
            .SingleAsync());

        using var updatedResponse = await SendJsonWithKeyAsync(
            HttpMethod.Put,
            $"/api/operational-projects/{projectId}/team/assignments/{assignmentId}",
            AssignmentPayload(
                "DES-ARCH-01",
                memberId,
                status: "InProgress",
                rowVersion: created.GetProperty("rowVersion").GetString()),
            $"team-assignment-update-{Guid.NewGuid():N}");

        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.Headers.GetValues("Idempotency-Replayed").Should().ContainSingle("true");
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(updatedResponse)).GetProperty("kpiIdentity").GetString()
            .Should().Be(originalKpiIdentity);
        var team = await ReadJsonAsync(await Client.GetAsync($"/api/operational-projects/{projectId}/team"));
        team.GetProperty("assignments").GetArrayLength().Should().Be(1);
        team.GetProperty("assignments")[0].GetProperty("kpiIdentity").GetString()
            .Should().Be(originalKpiIdentity);
        var history = await ReadJsonAsync(await Client.GetAsync(
            $"/api/operational-projects/{projectId}/team/history"));
        history.EnumerateArray().Count(item => item.GetProperty("entityType").GetString() == "Assignment")
            .Should().Be(2);
        history.EnumerateArray().Should().Contain(item =>
            item.GetProperty("snapshotJson").GetString() == originalSnapshot);
    }

    private Task AuthenticateAsync(string roleCode) => AuthTestHelper.AuthenticateAsync(
        Client,
        client => AuthTestHelper.LoginAsRoleAsync(client, roleCode));

    private async Task<int> CreateProjectAsync(int? projectManagerUserId = null)
    {
        return await WithDbAsync<int>(async db =>
        {
            var customer = new Customer
            {
                Name = $"Team customer {Guid.NewGuid():N}",
                Type = CustomerType.Company,
                SourceCode = "referral",
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-TEAM-{Guid.NewGuid():N}"[..30],
                Name = "Operational project team integration fixture",
                CustomerId = customer.Id,
                ProjectManagerUserId = projectManagerUserId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
    }

    private async Task<int> UserIdAsync(string roleCode)
    {
        var phone = roleCode == "USER"
            ? TestDataSeeder.CustomerPhone
            : TestDataSeeder.BusinessRolePhonesByCode[roleCode];
        return await WithDbAsync(db => db.Users
            .Where(user => user.PhoneNumber == phone)
            .Select(user => user.Id)
            .SingleAsync());
    }

    private async Task SeedMemberAsync(int projectId, int userId, ProjectTeamRoleCode roleCode)
    {
        await WithDbAsync(async db =>
        {
            var member = new OperationalProjectMember
            {
                OperationalProjectId = projectId,
                UserId = userId,
                Position = roleCode.ToString(),
                StartedAt = DateTime.UtcNow.AddDays(-1),
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
            };
            member.Roles.Add(new OperationalProjectMemberRole
            {
                RoleCode = roleCode,
                Scope = ProjectRoleScope.Project,
                StartedAt = member.StartedAt,
            });
            db.OperationalProjectMembers.Add(member);
            await db.SaveChangesAsync();
        });
    }

    private Task<HttpResponseMessage> PostMemberAsync(
        int projectId,
        int userId,
        string position,
        string roleCode) => SendJsonWithKeyAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{projectId}/team/members",
            MemberPayload(userId, position, roleCode),
            $"team-member-{Guid.NewGuid():N}");

    private static object MemberPayload(
        int userId,
        string position,
        string roleCode,
        string? rowVersion = null) => new
        {
            userId,
            position,
            startedAt = DateTime.UtcNow.AddDays(-1),
            roles = new[] { new { roleCode, scope = "Project" } },
            rowVersion,
        };

    private static object AssignmentPayload(
        string workKey,
        int memberId,
        string status = "Planned",
        string? rowVersion = null) => new
        {
            workKey,
            title = "Architectural design package",
            module = "Design",
            discipline = "Architecture",
            parallelGroup = "DESIGN-SPRINT-1",
            assigneeMemberId = memberId,
            status,
            plannedStart = DateTime.UtcNow,
            plannedEnd = DateTime.UtcNow.AddDays(7),
            rowVersion,
        };

    private async Task<HttpResponseMessage> SendJsonWithKeyAsync(
        HttpMethod method,
        string uri,
        object payload,
        string key)
    {
        using var request = new HttpRequestMessage(method, uri)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("Idempotency-Key", key);
        return await Client.SendAsync(request);
    }
}
