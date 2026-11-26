using System.Net;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.IntegrationTests.Controllers;

public class GoogleDriveTreeMigrationControllerTests : IntegrationTestBase
{
    private const string Path = "/api/site-settings/google-drive/tree-migration";

    public GoogleDriveTreeMigrationControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Preview_requires_settings_view()
    {
        await AuthTestHelper.AuthenticateAsync(Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));
        (await Client.GetAsync($"{Path}/preview")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var response = await Client.GetAsync($"{Path}/preview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("blockers").EnumerateArray().Select(item => item.GetString())
            .Should().Contain("DRIVE_NOT_CONNECTED");
        body.GetProperty("latestMigration").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Run_returns_409_with_code_while_sync_is_enabled()
    {
        await SetSyncEnabledAsync(true);
        try
        {
            await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

            var response = await Client.PostAsync(Path, null);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("DRIVE_SYNC_ENABLED");
            WithDb(db => db.DriveTreeMigrations.Count()).Should().Be(0);
        }
        finally
        {
            await SetSyncEnabledAsync(false);
        }
    }

    [Fact]
    public async Task Undo_returns_409_for_a_migration_that_does_not_exist()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var response = await Client.PostAsync($"{Path}/999/undo", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(response)).GetProperty("code").GetString()
            .Should().Be("DRIVE_TREE_MIGRATION_NOT_COMPLETED");
    }

    [Fact]
    public async Task Run_and_undo_require_settings_manage()
    {
        // ADMIN holds every settings right, so a view-only role is made from SALE.
        await WithDbAsync(async db =>
        {
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = db.Roles.Single(role => role.Code == "SALE").Id,
                PermissionId = db.Permissions.Single(p => p.Module == "system.settings" && p.Action == "view").Id,
            });
            await db.SaveChangesAsync();
        });
        try
        {
            await AuthTestHelper.AuthenticateAsync(Client,
                client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));

            (await Client.GetAsync($"{Path}/preview")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await Client.PostAsync(Path, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await Client.PostAsync($"{Path}/1/undo", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            await WithDbAsync(async db =>
            {
                var saleId = db.Roles.Single(role => role.Code == "SALE").Id;
                db.RolePermissions.RemoveRange(db.RolePermissions.Where(grant =>
                    grant.RoleId == saleId && grant.Permission.Module == "system.settings").ToList());
                await db.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task Enabling_sync_during_an_unfinished_migration_is_refused_with_a_code()
    {
        var migrationId = await WithDbAsync(async db =>
        {
            var migration = new DriveTreeMigration { Status = DriveTreeMigrationStatus.Failed };
            db.DriveTreeMigrations.Add(migration);
            await db.SaveChangesAsync();
            return migration.Id;
        });
        try
        {
            await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
            var current = await ReadJsonAsync(await Client.GetAsync("/api/site-settings/google-drive/configuration"));

            var response = await Client.PutAsJsonAsync("/api/site-settings/google-drive/configuration", new
            {
                enabled = true,
                clientId = "123.apps.googleusercontent.com",
                clientSecret = "valid-secret-value",
                oAuthRedirectUri = "https://example.com/api/site-settings/google-drive/oauth/callback",
                frontendReturnUrl = "/admin/settings?tab=drive",
                rootFolderId = "1234567890root",
                instanceId = "nicon-integration",
                applicationName = "Nicon Google Drive Integration",
                folders = current.GetProperty("folders"),
                supportsAllDrives = true,
                pollIntervalSeconds = 15,
                rowVersion = current.GetProperty("rowVersion").GetString(),
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ReadJsonAsync(response)).GetProperty("code").GetString()
                .Should().Be("DRIVE_TREE_MIGRATION_UNFINISHED");
        }
        finally
        {
            await WithDbAsync(async db =>
            {
                db.DriveTreeMigrations.Remove(db.DriveTreeMigrations.Single(item => item.Id == migrationId));
                await db.SaveChangesAsync();
            });
        }
    }

    private Task SetSyncEnabledAsync(bool enabled) => WithDbAsync(async db =>
    {
        var settings = db.GoogleDriveCredentials.SingleOrDefault(item => item.Id == 1);
        if (settings is null) db.GoogleDriveCredentials.Add(settings = new GoogleDriveCredential { Id = 1 });
        settings.Enabled = enabled;
        await db.SaveChangesAsync();
    });
}
