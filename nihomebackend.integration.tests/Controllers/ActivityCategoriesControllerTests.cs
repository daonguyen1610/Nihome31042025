using System.Net;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public class ActivityCategoriesControllerTests : IntegrationTestBase
{
    public ActivityCategoriesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        (await Client.GetAsync("/api/activity-categories")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FullRoundTrip_Create_Update_Delete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var name = $"Cat-{Guid.NewGuid():N}".Substring(0, 16);
        var created = await Client.PostAsJsonAsync("/api/activity-categories", new { name, isActive = true, sortOrder = 0 });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var updated = await Client.PutAsJsonAsync($"/api/activity-categories/{id}", new { name = name + "-v2", isActive = false, sortOrder = 2 });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.DeleteAsync($"/api/activity-categories/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.PutAsJsonAsync("/api/activity-categories/999999", new { name = "x", isActive = true, sortOrder = 0 });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LegacyTargetFields_RequireTranslationManagePermission()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var phone = $"08{Random.Shared.Next(10000000, 99999999)}";
        var setup = await WithDbAsync(async db =>
        {
            var role = new Role
            {
                Code = $"CAT_TEST_{suffix}",
                Name = $"Category test {suffix}",
                IsActive = true,
                InitialPermissionsSeeded = true,
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync();

            var categoryPermissionIds = db.Permissions
                .Where(permission => permission.Module == "content.activity-categories"
                    && (permission.Action == "view" || permission.Action == "manage"))
                .Select(permission => permission.Id)
                .ToList();
            db.RolePermissions.AddRange(categoryPermissionIds.Select(permissionId => new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId,
            }));

            var user = new ApplicationUser
            {
                PhoneNumber = phone,
                FullName = "Category-only tester",
                Email = $"category-{suffix}@nihome.test",
                Role = UserRole.USER,
                RoleEntityId = role.Id,
                IsActive = true,
            };
            user.PasswordHash = new PasswordService().Hash(user, TestDataSeeder.DefaultPassword);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return (RoleId: role.Id, UserId: user.Id);
        });

        try
        {
            await AuthTestHelper.AuthenticateAsync(
                Client,
                client => AuthTestHelper.LoginAsync(client, phone, TestDataSeeder.DefaultPassword));

            var sourceOnly = await Client.PostAsJsonAsync("/api/activity-categories", new
            {
                name = $"Source {suffix}",
                isActive = true,
                sortOrder = 0,
            });
            sourceOnly.StatusCode.Should().Be(HttpStatusCode.Created);

            var forbidden = await Client.PostAsJsonAsync("/api/activity-categories", new
            {
                name = $"Legacy {suffix}",
                nameEn = "Legacy English",
                isActive = true,
                sortOrder = 1,
            });
            forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            await WithDbAsync(async db =>
            {
                var translationPermissionId = db.Permissions
                    .Where(permission => permission.Module == "content.translations" && permission.Action == "manage")
                    .Select(permission => permission.Id)
                    .Single();
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = setup.RoleId,
                    PermissionId = translationPermissionId,
                });
                await db.SaveChangesAsync();
            });

            var allowed = await Client.PostAsJsonAsync("/api/activity-categories", new
            {
                name = $"Allowed {suffix}",
                nameEn = "Allowed English",
                isActive = true,
                sortOrder = 2,
            });
            allowed.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        finally
        {
            await WithDbAsync(async db =>
            {
                db.ActivityCategories.RemoveRange(db.ActivityCategories.Where(category =>
                    category.Name == $"Source {suffix}" || category.Name == $"Allowed {suffix}"));
                db.Users.RemoveRange(db.Users.Where(user => user.Id == setup.UserId));
                db.RolePermissions.RemoveRange(db.RolePermissions.Where(permission => permission.RoleId == setup.RoleId));
                db.Roles.RemoveRange(db.Roles.Where(role => role.Id == setup.RoleId));
                await db.SaveChangesAsync();
            });
        }
    }
}
