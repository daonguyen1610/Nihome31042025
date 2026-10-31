using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class DesignFlowIsolationControllerTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task DesignManager_WithoutOperationalProjectView_CanManageOwnDesignTeam()
    {
        await LoginAsync("SUPER_ADMIN");
        var fixture = await WithDbAsync(async db =>
        {
            var manager = await db.Users.SingleAsync(user => user.PhoneNumber ==
                TestDataSeeder.BusinessRolePhonesByCode["DESIGN_MANAGER"]);
            var architect = await db.Users.SingleAsync(user => user.PhoneNumber ==
                TestDataSeeder.BusinessRolePhonesByCode["ARCHITECT"]);
            var customer = new Customer { Name = "NICON – Hợp đồng thiết kế xưởng", Type = CustomerType.Company, SourceCode = "referral" };
            var root = new OperationalProject { Code = $"OP-PERM-{Guid.NewGuid():N}"[..30], Name = customer.Name, Customer = customer };
            var flow = new DesignProject
            {
                OperationalProject = root, Customer = customer,
                ProjectCode = $"DP-PERM-{Guid.NewGuid():N}"[..30], Name = customer.Name,
                ProjectManagerUserId = manager.Id,
            };
            db.DesignProjects.Add(flow);
            var grant = await db.RolePermissions.Include(item => item.Permission).SingleAsync(item =>
                item.RoleId == manager.RoleEntityId && item.Permission.Module == "operations.projects" &&
                item.Permission.Action == "view");
            db.RolePermissions.Remove(grant);
            await db.SaveChangesAsync();
            return (Root: root.Id, Flow: flow.Id, Architect: architect.Id,
                GrantRole: grant.RoleId, GrantPermission: grant.PermissionId);
        });
        int? revokedManagePermissionId = null;
        try
        {
            await LoginAsync("DESIGN_MANAGER");
            var path = $"/api/operational-projects/{fixture.Root}/team";
            (await Client.GetAsync($"{path}?designProjectId={fixture.Flow}"))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = new
            {
                designProjectId = fixture.Flow, userId = fixture.Architect,
                position = "Kiến trúc sư chủ trì mặt bằng",
                startedAt = DateTime.UtcNow.AddDays(-1),
                roles = new[] { new { roleCode = "Architect", scope = "Module", scopeValue = "Design" } },
            };
            var key = $"design-team-permission-{Guid.NewGuid():N}";
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{path}/members")
            {
                Content = JsonContent.Create(payload),
            };
            request.Headers.Add("Idempotency-Key", key);
            var added = await Client.SendAsync(request);
            added.StatusCode.Should().Be(HttpStatusCode.Created, await added.Content.ReadAsStringAsync());
            revokedManagePermissionId = await WithDbAsync(async db =>
            {
                var grant = await db.RolePermissions.Include(item => item.Permission).SingleAsync(item =>
                    item.RoleId == fixture.GrantRole && item.Permission.Module == "design.projects" &&
                    item.Permission.Action == "manage");
                db.RolePermissions.Remove(grant);
                await db.SaveChangesAsync();
                return grant.PermissionId;
            });
            using var replay = new HttpRequestMessage(HttpMethod.Post, $"{path}/members")
            {
                Content = JsonContent.Create(payload),
            };
            replay.Headers.Add("Idempotency-Key", key);
            (await Client.SendAsync(replay)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await Client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            await WithDbAsync(async db =>
            {
                db.RolePermissions.Add(new NihomeBackend.Models.Rbac.RolePermission
                {
                    RoleId = fixture.GrantRole, PermissionId = fixture.GrantPermission,
                });
                if (revokedManagePermissionId.HasValue)
                    db.RolePermissions.Add(new NihomeBackend.Models.Rbac.RolePermission
                    {
                        RoleId = fixture.GrantRole, PermissionId = revokedManagePermissionId.Value,
                    });
                await db.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task DesignTeam_AllowsSeveralLeadsButOnlyOneManager()
    {
        await LoginAsync("SUPER_ADMIN");
        var fixture = await WithDbAsync(async db =>
        {
            var users = await db.Users.Where(user => new[]
            {
                TestDataSeeder.BusinessRolePhonesByCode["DESIGN_MANAGER"],
                TestDataSeeder.BusinessRolePhonesByCode["DESIGN_LEAD"],
                TestDataSeeder.BusinessRolePhonesByCode["ARCHITECT"],
                TestDataSeeder.BusinessRolePhonesByCode["DESIGN"],
            }.Contains(user.PhoneNumber)).ToDictionaryAsync(user => user.PhoneNumber);
            var customer = new Customer { Name = "NICON – Nhóm thiết kế nhà máy", Type = CustomerType.Company, SourceCode = "referral" };
            var root = new OperationalProject { Code = $"OP-LEADS-{Guid.NewGuid():N}"[..30], Name = customer.Name, Customer = customer };
            var flow = new DesignProject
            {
                OperationalProject = root, Customer = customer,
                ProjectCode = $"DP-LEADS-{Guid.NewGuid():N}"[..30], Name = customer.Name,
                ProjectManagerUserId = users[TestDataSeeder.BusinessRolePhonesByCode["DESIGN_MANAGER"]].Id,
                DesignLeadUserId = users[TestDataSeeder.BusinessRolePhonesByCode["DESIGN_LEAD"]].Id,
            };
            db.DesignProjects.Add(flow);
            await db.SaveChangesAsync();
            return (Root: root.Id, Flow: flow.Id,
                Lead: users[TestDataSeeder.BusinessRolePhonesByCode["DESIGN_LEAD"]].Id,
                Architect: users[TestDataSeeder.BusinessRolePhonesByCode["ARCHITECT"]].Id,
                Design: users[TestDataSeeder.BusinessRolePhonesByCode["DESIGN"]].Id);
        });
        async Task<HttpResponseMessage> Add(int userId, string role) => await SendAsync(
            $"/api/operational-projects/{fixture.Root}/team/members",
            new { designProjectId = fixture.Flow, userId, position = role,
                startedAt = DateTime.UtcNow.AddDays(-1),
                roles = new[] { new { roleCode = role, scope = "Module", scopeValue = "Design" } } });

        var firstLead = await Add(fixture.Lead, "DesignLead");
        firstLead.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstLeadBody = await ReadJsonAsync(firstLead);
        var secondLead = await Add(fixture.Architect, "DesignLead");
        secondLead.StatusCode.Should().Be(HttpStatusCode.Created, await secondLead.Content.ReadAsStringAsync());
        (await Add(fixture.Design, "ProjectManager")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var storedLead = await WithDbAsync(db => db.DesignProjects.AsNoTracking()
            .Where(project => project.Id == fixture.Flow).Select(project => project.DesignLeadUserId).SingleAsync());
        storedLead.Should().Be(fixture.Lead);

        var ended = await Client.PutAsJsonAsync(
            $"/api/operational-projects/{fixture.Root}/team/members/{firstLeadBody.GetProperty("id").GetInt32()}",
            new
            {
                designProjectId = fixture.Flow, userId = fixture.Lead,
                position = "DesignLead", startedAt = firstLeadBody.GetProperty("startedAt").GetString(),
                endedAt = DateTime.UtcNow,
                rowVersion = firstLeadBody.GetProperty("rowVersion").GetString(),
                roles = new[] { new { roleCode = "DesignLead", scope = "Module", scopeValue = "Design" } },
            });
        ended.StatusCode.Should().Be(HttpStatusCode.OK, await ended.Content.ReadAsStringAsync());
        var replacementLead = await WithDbAsync(db => db.DesignProjects.AsNoTracking()
            .Where(project => project.Id == fixture.Flow).Select(project => project.DesignLeadUserId).SingleAsync());
        replacementLead.Should().Be(fixture.Architect);
    }

    [Fact]
    public async Task IndependentFlows_KeepTeamsSchedulesAndDocumentWritesIsolated()
    {
        await LoginAsync("SUPER_ADMIN");
        var fixture = await WithDbAsync(async db =>
        {
            var users = await db.Users.Where(user => new[]
            {
                TestDataSeeder.BusinessRolePhonesByCode["DESIGN_MANAGER"],
                TestDataSeeder.BusinessRolePhonesByCode["DESIGN_LEAD"],
                TestDataSeeder.BusinessRolePhonesByCode["ARCHITECT"],
            }.Contains(user.PhoneNumber)).ToDictionaryAsync(user => user.PhoneNumber);
            var manager = users[TestDataSeeder.BusinessRolePhonesByCode["DESIGN_MANAGER"]];
            var lead = users[TestDataSeeder.BusinessRolePhonesByCode["DESIGN_LEAD"]];
            var architect = users[TestDataSeeder.BusinessRolePhonesByCode["ARCHITECT"]];
            var customer = new Customer { Name = "NICON – Nhà máy và văn phòng độc lập", Type = CustomerType.Company, SourceCode = "referral" };
            var root = new OperationalProject { Code = $"OP-ISOLATE-{Guid.NewGuid():N}"[..30], Name = customer.Name, Customer = customer };
            var flows = new[] { "Nhà máy", "Văn phòng" }.Select((name, index) => new DesignProject
            {
                OperationalProject = root,
                Customer = customer,
                ProjectCode = $"DP-ISOLATE-{Guid.NewGuid():N}"[..30],
                Name = $"NICON – {name}",
                ProjectManagerUserId = index == 0 ? manager.Id : null,
                DesignLeadUserId = lead.Id,
                StartDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                Deadline = new DateTime(2027, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            }).ToArray();
            db.DesignProjects.AddRange(flows);
            await db.SaveChangesAsync();
            var docs = new List<long>();
            foreach (var flow in flows)
            {
                var source = new BasicDesignDoc { DesignProjectId = flow.Id, DocumentCode = $"ARCH-{flow.Id}", Title = flow.Name, DisciplineCode = "ARCH" };
                db.BasicDesignDocs.Add(source);
                await db.SaveChangesAsync();
                var document = new ProjectDocument
                {
                    OperationalProjectId = root.Id,
                    SourceModule = ProjectDocumentSourceModule.Design,
                    SourceEntityType = nameof(BasicDesignDoc),
                    SourceRecordId = source.Id,
                    SourceType = ProjectDocumentSourceType.ManualUpload,
                    Category = ProjectDocumentCategory.DesignBasic,
                    OriginalFileName = $"nicon-{flow.Id}.pdf",
                    Sha256 = new string('a', 64),
                    SyncStatus = ProjectDocumentSyncStatus.Failed,
                    SyncAttemptCount = 3,
                };
                db.ProjectDocuments.Add(document);
                await db.SaveChangesAsync();
                docs.Add(document.Id);
            }
            return (Root: root.Id, Flows: flows.Select(flow => flow.Id).ToArray(), Architect: architect.Id, Lead: lead.Id, Documents: docs);
        });
        var teamPath = $"/api/operational-projects/{fixture.Root}/team";
        var schedulePath = $"/api/operational-projects/{fixture.Root}/design-schedule";
        var members = new List<int>();
        var approvers = new List<int>();
        var phases = new List<int>();
        var tasks = new List<int>();
        foreach (var flow in fixture.Flows)
        {
            foreach (var (user, role, destination) in new[]
            {
                (fixture.Architect, "Architect", members), (fixture.Lead, "DesignLead", approvers),
            })
            {
                var created = await SendAsync($"{teamPath}/members", new
                {
                    designProjectId = flow,
                    userId = user,
                    position = $"NICON {role}",
                    startedAt = DateTime.UtcNow.AddDays(-1),
                    roles = new[] { new { roleCode = role, scope = "Module", scopeValue = "Design" } },
                });
                created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
                destination.Add((await ReadJsonAsync(created)).GetProperty("id").GetInt32());
            }
            var initialized = await SendAsync($"{schedulePath}/initialize", new
            {
                designProjectId = flow,
                phases = new[] { new { code = "Concept", weight = 34 }, new { code = "BasicDesign", weight = 33 }, new { code = "ShopDrawing", weight = 33 } },
            });
            initialized.StatusCode.Should().Be(HttpStatusCode.OK, await initialized.Content.ReadAsStringAsync());
            phases.Add((await ReadJsonAsync(initialized)).GetProperty("phases").EnumerateArray()
                .Single(phase => phase.GetProperty("code").GetString() == "Concept").GetProperty("id").GetInt32());
        }
        members.Distinct().Should().HaveCount(2, "one person has independent memberships in two Design flows");
        for (var index = 0; index < 2; index++)
        {
            var team = await Client.GetAsync($"{teamPath}?designProjectId={fixture.Flows[index]}");
            (await ReadJsonAsync(team)).GetProperty("members").EnumerateArray().Select(member => member.GetProperty("id").GetInt32())
                .Should().BeEquivalentTo(new[] { members[index], approvers[index] });
            var created = await SendAsync($"{schedulePath}/phases/{phases[index]}/tasks", TaskPayload(members[index], approvers[index]));
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            tasks.Add((await ReadJsonAsync(created)).GetProperty("id").GetInt32());
        }
        var ambiguous = await Client.GetAsync(schedulePath);
        ambiguous.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        for (var index = 0; index < 2; index++)
        {
            var schedule = await Client.GetAsync($"{schedulePath}?designProjectId={fixture.Flows[index]}");
            (await ReadJsonAsync(schedule)).GetProperty("tasks").GetProperty("items").EnumerateArray()
                .Select(task => task.GetProperty("id").GetInt32()).Should().Equal(tasks[index]);
        }
        foreach (var invalid in new[]
        {
            TaskPayload(members[1], approvers[0], code: "WRONG-MEMBER"),
            TaskPayload(members[0], approvers[0], predecessor: tasks[1], code: "WRONG-PREDECESSOR"),
            TaskPayload(members[0], approvers[0], parent: tasks[1], code: "WRONG-PARENT"),
            TaskPayload(members[0], approvers[0], cc: members[1], code: "WRONG-CC"),
        })
            (await SendAsync($"{schedulePath}/phases/{phases[0]}/tasks", invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await LoginAsync("DESIGN_MANAGER");
        (await Client.GetAsync($"{teamPath}?designProjectId={fixture.Flows[0]}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync($"{teamPath}?designProjectId={fixture.Flows[1]}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"{schedulePath}?designProjectId={fixture.Flows[1]}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var documentsPath = $"/api/operational-projects/{fixture.Root}/documents";
        (await Client.GetAsync($"{documentsPath}?designProjectId={fixture.Flows[1]}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"{documentsPath}/{fixture.Documents[1]}/content")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PostAsync($"{documentsPath}/{fixture.Documents[0]}/retry", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.PostAsync($"{documentsPath}/{fixture.Documents[1]}/retry", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.DeleteAsync($"{documentsPath}/{fixture.Documents[1]}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await WithDbAsync(async db =>
        {
            (await db.DesignScheduleTasks.CountAsync(task => task.OperationalProjectId == fixture.Root)).Should().Be(2);
            var sibling = await db.ProjectDocuments.FindAsync(fixture.Documents[1]);
            sibling!.SyncStatus.Should().Be(ProjectDocumentSyncStatus.Failed);
            sibling.SyncAttemptCount.Should().Be(3);
            sibling.DeletedAt.Should().BeNull();
        });
    }

    private Task LoginAsync(string role) => AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));

    private async Task<HttpResponseMessage> SendAsync(string path, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload) };
        request.Headers.Add("Idempotency-Key", $"design-flow-{Guid.NewGuid():N}");
        return await Client.SendAsync(request);
    }

    private static object TaskPayload(int member, int approver, int? predecessor = null, int? parent = null, int? cc = null, string code = "ARCH-001") => new
    {
        code,
        name = "NICON – Triển khai mặt bằng",
        departmentCode = "design",
        assigneeMemberId = member,
        approverMemberId = approver,
        parentTaskId = parent,
        plannedStart = new DateOnly(2026, 11, 1),
        plannedEnd = new DateOnly(2026, 11, 2),
        status = "NotStarted",
        progressPercent = 0,
        weight = 100,
        predecessorTaskIds = predecessor.HasValue ? new[] { predecessor.Value } : Array.Empty<int>(),
        ccMemberIds = cc.HasValue ? new[] { cc.Value } : Array.Empty<int>(),
    };
}
