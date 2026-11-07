using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Services;

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
    public async Task EditingMember_KeepsUnchangedProjectRoleAndItsSource()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        var architectId = await UserIdAsync("ARCHITECT");
        using var createdResponse = await PostMemberAsync(projectId, architectId, "Architect", "Architect");
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var member = await ReadJsonAsync(createdResponse);
        var memberId = member.GetProperty("id").GetInt32();
        var originalRoleId = await WithDbAsync(async db =>
        {
            var role = await db.OperationalProjectMemberRoles.SingleAsync(item => item.MemberId == memberId);
            role.Source = LegacyProjectTeamSyncService.BackfillSource;
            role.SourceReference = "ExistingProjectRole";
            await db.SaveChangesAsync();
            return role.Id;
        });

        using var update = await SendJsonWithKeyAsync(
            HttpMethod.Put,
            $"/api/operational-projects/{projectId}/team/members/{memberId}",
            new
            {
                userId = architectId,
                position = "Architectural concept coordinator",
                startedAt = member.GetProperty("startedAt").GetString(),
                roles = new[] { new { roleCode = "Architect", scope = "Project" } },
                rowVersion = member.GetProperty("rowVersion").GetString(),
            },
            $"team-member-update-{Guid.NewGuid():N}");
        update.StatusCode.Should().Be(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
        var activeRoles = await WithDbAsync(db => db.OperationalProjectMemberRoles.AsNoTracking()
            .Where(item => item.MemberId == memberId && item.EndedAt == null)
            .Select(item => new { item.Id, item.Source, item.SourceReference })
            .ToListAsync());
        activeRoles.Should().ContainSingle();
        activeRoles[0].Id.Should().Be(originalRoleId);
        activeRoles[0].Source.Should().Be(LegacyProjectTeamSyncService.BackfillSource);
        activeRoles[0].SourceReference.Should().Be("ExistingProjectRole");
    }

    [Fact]
    public async Task Get_WithoutGlobalPermission_ReturnsForbidden()
    {
        var projectId = await CreateProjectAsync();
        await AuthenticateAsync("USER");

        (await Client.GetAsync($"/api/operational-projects/{projectId}/team"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.GetAsync($"/api/operational-projects/{projectId}/team/history"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task History_IncludesPreviouslyPersistedCodeChangesWithoutWritingDuplicateAuditRows()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        var otherProjectId = await CreateProjectAsync();
        var now = DateTime.UtcNow;
        await WithDbAsync(async db =>
        {
            var actor = await db.Users.FirstAsync();
            db.OperationalProjectCodeChanges.AddRange(
                new OperationalProjectCodeChange { OperationalProjectId = projectId, OldCode = "NICON-A", NewCode = "NICON-B", Reason = "Chuẩn hóa mã hồ sơ", ChangedAt = now.AddMinutes(-2), ChangedByUserId = actor.Id },
                new OperationalProjectCodeChange { OperationalProjectId = projectId, OldCode = "NICON-B", NewCode = "NICON-C", Reason = "Cập nhật mã được duyệt", ChangedAt = now, ChangedByUserId = actor.Id },
                new OperationalProjectCodeChange { OperationalProjectId = otherProjectId, OldCode = "OTHER-A", NewCode = "OTHER-B", Reason = "Other project", ChangedAt = now.AddMinutes(1), ChangedByUserId = actor.Id });
            await db.SaveChangesAsync();
        });
        var originalTeamCount = await WithDbAsync(db => db.OperationalProjectTeamHistory.CountAsync());
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await Client.GetAsync($"/api/operational-projects/{projectId}/team/history");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await ReadJsonAsync(response);
            var codes = body.EnumerateArray().Where(item => item.GetProperty("entityType").GetString() == "ProjectCode").ToList();
            codes.Should().HaveCount(2);
            codes.Select(item => item.GetProperty("newCode").GetString()).Should().Equal("NICON-C", "NICON-B");
        }
        (await WithDbAsync(db => db.OperationalProjectTeamHistory.CountAsync())).Should().Be(originalTeamCount);
    }

    [Fact]
    public async Task SamePerson_CanJoinTwoProjectsButCannotDuplicateOrReadUnassignedThirdProject()
    {
        var manager = await UserIdAsync("DESIGN_LEAD");
        var person = await UserIdAsync("SITE_COMMANDER");
        var first = await CreateProjectAsync(manager);
        var second = await CreateProjectAsync(manager);
        var third = await CreateProjectAsync(manager);
        await AuthenticateAsync("SUPER_ADMIN");
        (await PostMemberAsync(first, person, "Site command coordinator", "Observer")).StatusCode.Should().Be(HttpStatusCode.Created);
        (await PostMemberAsync(second, person, "Site command coordinator", "Observer")).StatusCode.Should().Be(HttpStatusCode.Created);
        (await PostMemberAsync(first, person, "Duplicate site assignment", "Observer")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AuthenticateAsync("SITE_COMMANDER");
        (await Client.GetAsync($"/api/operational-projects/{first}/team")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync($"/api/operational-projects/{second}/team")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync($"/api/operational-projects/{third}/team")).StatusCode.Should().Be(HttpStatusCode.NotFound);
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
    public async Task ProjectManagerCanMutateRootTeam_OtherRolesCannotMutate()
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

        var memberCount = await WithDbAsync(db => db.OperationalProjectMembers.CountAsync(member => member.OperationalProjectId == projectId));
        await AuthenticateAsync("DESIGN_LEAD");
        (await PostMemberAsync(projectId, mepId, "MEP Engineer", "MepEngineer"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AuthenticateAsync("DESIGN");
        var team = await Client.GetAsync($"/api/operational-projects/{projectId}/team");
        team.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(team)).GetProperty("canManage").GetBoolean().Should().BeFalse();
        (await PostMemberAsync(projectId, structuralId, "Structural Engineer", "StructuralEngineer"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await WithDbAsync(db => db.OperationalProjectMembers.CountAsync(member => member.OperationalProjectId == projectId)))
            .Should().Be(memberCount);
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
    public async Task AddMember_ProjectManagerRole_IsReservedForPrimaryProjectManager()
    {
        var primaryManagerId = await UserIdAsync("PM");
        var otherUserId = await UserIdAsync("ARCHITECT");
        var projectId = await CreateProjectAsync(primaryManagerId);
        await AuthenticateAsync("SUPER_ADMIN");

        var rejected = await PostMemberAsync(
            projectId,
            otherUserId,
            "Additional project manager",
            "ProjectManager");

        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.OperationalProjectMembers.AnyAsync(member =>
            member.OperationalProjectId == projectId && member.UserId == otherUserId)))
            .Should().BeFalse();
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

    [Fact]
    public async Task Reassign_ClosesOldAssignmentAndCreatesNewOneWithoutChangingKpiHistory()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        using var firstMemberResponse = await PostMemberAsync(projectId, await UserIdAsync("ARCHITECT"), "Architect", "Architect");
        using var secondMemberResponse = await PostMemberAsync(projectId, await UserIdAsync("MEP_ENGINEER"), "MEP Engineer", "MepEngineer");
        var firstMember = (await ReadJsonAsync(firstMemberResponse)).GetProperty("id").GetInt32();
        var secondMember = (await ReadJsonAsync(secondMemberResponse)).GetProperty("id").GetInt32();
        using var createdResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/team/assignments", AssignmentPayload("DES-TRANSFER", firstMember), $"create-{Guid.NewGuid():N}");
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createdResponse);
        var originalId = created.GetProperty("id").GetInt32();
        var originalIdentity = created.GetProperty("kpiIdentity").GetString();
        await AuthenticateAsync("USER");
        using var forbidden = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{projectId}/team/assignments/{originalId}",
            AssignmentPayload("DES-TRANSFER", secondMember, rowVersion: created.GetProperty("rowVersion").GetString()), $"transfer-forbidden-{Guid.NewGuid():N}");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AuthenticateAsync("SUPER_ADMIN");
        using var missingVersion = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{projectId}/team/assignments/{originalId}",
            AssignmentPayload("DES-TRANSFER", secondMember), $"transfer-missing-version-{Guid.NewGuid():N}");
        missingVersion.IsSuccessStatusCode.Should().BeFalse();
        var beforeTransfer = await ReadJsonAsync(await Client.GetAsync($"/api/operational-projects/{projectId}/team"));
        beforeTransfer.GetProperty("assignments").GetArrayLength().Should().Be(1);
        var transferPayload = AssignmentPayload("DES-TRANSFER", secondMember, rowVersion: created.GetProperty("rowVersion").GetString());
        var transferKey = $"transfer-{Guid.NewGuid():N}";
        using var transfer = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{projectId}/team/assignments/{originalId}",
            transferPayload, transferKey);
        transfer.StatusCode.Should().Be(HttpStatusCode.OK, await transfer.Content.ReadAsStringAsync());
        var result = await ReadJsonAsync(transfer);
        using var replay = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{projectId}/team/assignments/{originalId}", transferPayload, transferKey);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.Headers.GetValues("Idempotency-Replayed").Should().ContainSingle("true");
        (await ReadJsonAsync(replay)).GetProperty("id").GetInt32().Should().Be(result.GetProperty("id").GetInt32());
        result.GetProperty("id").GetInt32().Should().NotBe(originalId);
        var team = await ReadJsonAsync(await Client.GetAsync($"/api/operational-projects/{projectId}/team"));
        var assignments = team.GetProperty("assignments").EnumerateArray().ToList();
        assignments.Should().HaveCount(2);
        var old = assignments.Single(item => item.GetProperty("id").GetInt32() == originalId);
        old.GetProperty("status").GetString().Should().Be("Reassigned");
        old.GetProperty("kpiIdentity").GetString().Should().Be(originalIdentity);
        old.GetProperty("completedAt").ValueKind.Should().Be(JsonValueKind.Null);
        result.GetProperty("assigneeMemberId").GetInt32().Should().Be(secondMember);
        result.GetProperty("kpiIdentity").GetString().Should().NotBe(originalIdentity);
        using var transferBack = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{projectId}/team/assignments/{result.GetProperty("id").GetInt32()}",
            AssignmentPayload("DES-TRANSFER", firstMember, rowVersion: result.GetProperty("rowVersion").GetString()), $"transfer-back-{Guid.NewGuid():N}");
        transferBack.StatusCode.Should().Be(HttpStatusCode.OK, await transferBack.Content.ReadAsStringAsync());
        var returned = await ReadJsonAsync(transferBack);
        returned.GetProperty("previousAssignmentId").GetInt32().Should().Be(result.GetProperty("id").GetInt32());
        returned.GetProperty("kpiIdentity").GetString().Should().NotBe(originalIdentity);
    }

    [Fact]
    public async Task Assignments_AllowOneSubtaskLevelOnly()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        using var memberResponse = await PostMemberAsync(
            projectId, await UserIdAsync("ARCHITECT"), "Architect", "Architect");
        var memberId = (await ReadJsonAsync(memberResponse)).GetProperty("id").GetInt32();
        using var rootResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/team/assignments",
            AssignmentPayload("DES-SUBTASK-ROOT", memberId), $"root-{Guid.NewGuid():N}");
        rootResponse.StatusCode.Should().Be(HttpStatusCode.Created, await rootResponse.Content.ReadAsStringAsync());
        var rootId = (await ReadJsonAsync(rootResponse)).GetProperty("id").GetInt32();

        using var childResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/team/assignments",
            AssignmentPayload("DES-SUBTASK-CHILD", memberId, parentAssignmentId: rootId), $"child-{Guid.NewGuid():N}");
        childResponse.StatusCode.Should().Be(HttpStatusCode.Created, await childResponse.Content.ReadAsStringAsync());
        var child = await ReadJsonAsync(childResponse);
        child.GetProperty("parentAssignmentId").GetInt32().Should().Be(rootId);
        var childId = child.GetProperty("id").GetInt32();

        using var grandChildResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/team/assignments",
            AssignmentPayload("DES-SUBTASK-GRANDCHILD", memberId, parentAssignmentId: childId), $"grandchild-{Guid.NewGuid():N}");
        grandChildResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AssignmentAttachments_UploadListDownloadDelete_RoundTrip()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        using var memberResponse = await PostMemberAsync(
            projectId, await UserIdAsync("ARCHITECT"), "Architect", "Architect");
        var memberId = (await ReadJsonAsync(memberResponse)).GetProperty("id").GetInt32();
        using var assignmentResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/team/assignments",
            AssignmentPayload("DES-ATTACHMENT", memberId), $"attach-assignment-{Guid.NewGuid():N}");
        var assignmentId = (await ReadJsonAsync(assignmentResponse)).GetProperty("id").GetInt32();
        var baseUri = $"/api/operational-projects/{projectId}/team/assignments/{assignmentId}/attachments";
        var bytes = "attachment payload"u8.ToArray();
        using var form = new MultipartFormDataContent();
        form.Add(
            new ByteArrayContent(bytes) { Headers = { ContentType = new MediaTypeHeaderValue("application/pdf") } },
            "file",
            "spec.pdf");
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUri) { Content = form };
        request.Headers.Add("Idempotency-Key", $"attach-upload-{Guid.NewGuid():N}");
        using var upload = await Client.SendAsync(request);

        upload.StatusCode.Should().Be(HttpStatusCode.Created, await upload.Content.ReadAsStringAsync());
        var attachment = await ReadJsonAsync(upload);
        attachment.GetProperty("originalFileName").GetString().Should().Be("spec.pdf");
        var attachmentId = attachment.GetProperty("id").GetInt64();

        var list = await ReadJsonAsync(await Client.GetAsync(baseUri));
        list.EnumerateArray().Should().ContainSingle(item =>
            item.GetProperty("id").GetInt64() == attachmentId);

        using var download = await Client.GetAsync($"{baseUri}/{attachmentId}/content");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);

        (await Client.DeleteAsync($"{baseUri}/{attachmentId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"{baseUri}/{attachmentId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssignmentAttachments_CannotBeUploadedToAnotherProject()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        var otherProjectId = await CreateProjectAsync();
        using var memberResponse = await PostMemberAsync(
            projectId, await UserIdAsync("ARCHITECT"), "Architect", "Architect");
        var memberId = (await ReadJsonAsync(memberResponse)).GetProperty("id").GetInt32();
        using var assignmentResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/team/assignments",
            AssignmentPayload("DES-ATTACH-CROSS", memberId), $"attach-cross-{Guid.NewGuid():N}");
        var assignmentId = (await ReadJsonAsync(assignmentResponse)).GetProperty("id").GetInt32();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3]), "file", "spec.pdf");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/operational-projects/{otherProjectId}/team/assignments/{assignmentId}/attachments")
        {
            Content = form,
        };
        request.Headers.Add("Idempotency-Key", $"attach-cross-upload-{Guid.NewGuid():N}");

        using var upload = await Client.SendAsync(request);

        upload.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssignmentAttachments_ListAndDownload_RequireProjectScope()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var projectId = await CreateProjectAsync();
        using var memberResponse = await PostMemberAsync(
            projectId, await UserIdAsync("ARCHITECT"), "Architect", "Architect");
        var memberId = (await ReadJsonAsync(memberResponse)).GetProperty("id").GetInt32();
        using var assignmentResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/team/assignments",
            AssignmentPayload("DES-ATTACH-SCOPE", memberId), $"attach-scope-{Guid.NewGuid():N}");
        var assignmentId = (await ReadJsonAsync(assignmentResponse)).GetProperty("id").GetInt32();
        var baseUri = $"/api/operational-projects/{projectId}/team/assignments/{assignmentId}/attachments";
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3]), "file", "scope.pdf");
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUri) { Content = form };
        request.Headers.Add("Idempotency-Key", $"attach-scope-upload-{Guid.NewGuid():N}");
        using var upload = await Client.SendAsync(request);
        upload.StatusCode.Should().Be(HttpStatusCode.Created, await upload.Content.ReadAsStringAsync());
        var attachmentId = (await ReadJsonAsync(upload)).GetProperty("id").GetInt64();

        // SALE holds operations.projects.view but has no relationship to this project.
        await AuthenticateAsync("SALE");

        (await Client.GetAsync(baseUri)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"{baseUri}/{attachmentId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
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
        string? rowVersion = null,
        int? parentAssignmentId = null) => new
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
            parentAssignmentId,
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
