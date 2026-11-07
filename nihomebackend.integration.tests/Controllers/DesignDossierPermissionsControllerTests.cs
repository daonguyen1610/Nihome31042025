using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class DesignDossierPermissionsControllerTests(NihomeWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task ScopedViewRevocation_HidesDossierAndResourcesDespiteRootMembership()
    {
        var fixture = await CreateFixtureAsync("ARCHITECT");
        await WithDbAsync(async db =>
        {
            var root = await db.OperationalProjects.FindAsync(fixture.RootId);
            root!.ProjectManagerUserId = fixture.UserId;
            var permission = await db.DesignProjectMemberPermissions.SingleAsync(item => item.MemberId == fixture.MemberId);
            permission.CanView = false;
            permission.CanCreate = false;
            permission.CanUpdate = false;
            await db.SaveChangesAsync();
        });
        await LoginAsync("ARCHITECT");

        (await Client.GetAsync($"/api/operational-projects/{fixture.RootId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync($"/api/design-projects/{fixture.DesignId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreach (var resource in new[] { "concept-options", "basic-design-docs", "shop-drawings", "drawing-revisions", "ifc-releases" })
            (await Client.GetAsync($"/api/{resource}?designProjectId={fixture.DesignId}"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound, resource);
        var projects = await ReadJsonAsync(await Client.GetAsync("/api/design-projects?pageSize=100"));
        projects.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt32())
            .Should().NotContain(fixture.DesignId);

        await LoginAsync("SUPER_ADMIN");
        var restored = await Client.PutAsJsonAsync(PermissionsUrl(fixture), new
        {
            canView = true,
            canCreate = false,
            canUpdate = false,
            canDelete = false,
        });
        restored.StatusCode.Should().Be(HttpStatusCode.OK);
        await LoginAsync("ARCHITECT");
        (await Client.GetAsync($"/api/design-projects/{fixture.DesignId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync($"/api/concept-options?designProjectId={fixture.DesignId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MatrixGrant_AllowsCreateWithoutGlobalManage_ButDoesNotAllowUpdateOrSelfEscalation()
    {
        var fixture = await CreateFixtureAsync("WAREHOUSE");
        await LoginAsync("WAREHOUSE");
        var created = await Client.PostAsJsonAsync("/api/concept-options", new
        {
            designProjectId = fixture.DesignId,
            name = "Phương án kết cấu nhà xưởng",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var conceptId = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();
        (await Client.PutAsJsonAsync($"/api/concept-options/{conceptId}", new { name = "Unauthorized edit" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PutAsJsonAsync(PermissionsUrl(fixture), new
        {
            canView = true,
            canCreate = true,
            canUpdate = true,
            canDelete = true,
        })).IsSuccessStatusCode.Should().BeFalse();
        await WithDbAsync(async db =>
        {
            (await db.ConceptOptions.FindAsync(conceptId))!.Name.Should().Be("Phương án kết cấu nhà xưởng");
            var permission = await db.DesignProjectMemberPermissions.SingleAsync(item => item.MemberId == fixture.MemberId);
            permission.CanUpdate.Should().BeFalse();
            permission.CanDelete.Should().BeFalse();
        });
    }

    private Task LoginAsync(string role) => AuthTestHelper.AuthenticateAsync(Client,
        client => AuthTestHelper.LoginAsRoleAsync(client, role));

    private static string PermissionsUrl(Fixture fixture) =>
        $"/api/operational-projects/{fixture.RootId}/team/members/{fixture.MemberId}/design-permissions";

    private Task<Fixture> CreateFixtureAsync(string role) => WithDbAsync(async db =>
    {
        var user = await db.Users.SingleAsync(item => item.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode[role]);
        var manager = await db.Users.SingleAsync(item => item.PhoneNumber == TestDataSeeder.SuperAdminPhone);
        var customer = new Customer { Name = UniqueSlug("NICON factory owner"), Type = CustomerType.Company, SourceCode = "referral" };
        var root = new OperationalProject { Code = UniqueSlug("OP"), Name = "Dự án nhà xưởng NICON", Customer = customer };
        var design = new DesignProject
        {
            ProjectCode = UniqueSlug("DP"),
            Name = "Hồ sơ thiết kế nhà xưởng",
            Customer = customer,
            OperationalProject = root,
            ProjectManagerUserId = manager.Id,
        };
        var member = new OperationalProjectMember
        {
            OperationalProject = root,
            DesignProject = design,
            UserId = user.Id,
            Position = "Điều phối hồ sơ kết cấu",
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
            StartedAt = DateTime.UtcNow.AddDays(-1),
            Roles = [new OperationalProjectMemberRole
            {
                RoleCode = ProjectTeamRoleCode.StructuralEngineer, Scope = ProjectRoleScope.Module,
                ScopeValue = "Design", StartedAt = DateTime.UtcNow.AddDays(-1),
            }],
            DesignPermission = new DesignProjectMemberPermission
            {
                CanView = true,
                CanCreate = true,
                CreatedByUserId = manager.Id,
                UpdatedByUserId = manager.Id,
            },
        };
        db.Add(member);
        await db.SaveChangesAsync();
        return new Fixture(root.Id, design.Id, member.Id, user.Id);
    });

    private sealed record Fixture(int RootId, int DesignId, int MemberId, int UserId);
}
