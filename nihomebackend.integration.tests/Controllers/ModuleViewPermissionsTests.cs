using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// NICON feedback row 18: a role holding only a module-wide view permission
/// reads that module's pages but every write is still refused server-side.
/// </summary>
public sealed class ModuleViewPermissionsTests : IntegrationTestBase
{
    public ModuleViewPermissionsTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Theory]
    [InlineData("finance", "/api/finance/periods", "POST", "/api/finance/periods")]
    [InlineData("permit", "/api/legal-checklist-templates", "PUT", "/api/legal-checklist-templates/1")]
    [InlineData("proc", "/api/vendors", "POST", "/api/vendors")]
    [InlineData("construction", "/api/asbuilt-categories", "POST", "/api/asbuilt-categories")]
    public async Task ModuleView_ReadsModuleButCannotWrite(string module, string readUrl, string writeMethod, string writeUrl)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var phone = $"07{Random.Shared.Next(10000000, 99999999)}";
        await WithDbAsync(async db =>
        {
            var role = new Role { Code = $"VIEW_{suffix}", Name = $"Module viewer {suffix}", IsActive = true, InitialPermissionsSeeded = true };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            var permission = await db.Permissions.SingleAsync(p => p.Module == module && p.Action == "view");
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            var user = new ApplicationUser
            {
                PhoneNumber = phone,
                FullName = $"Module viewer {module}",
                Email = $"module-view-{suffix}@nihome.test",
                Role = UserRole.USER,
                RoleEntityId = role.Id,
                IsActive = true,
            };
            user.PasswordHash = new PasswordService().Hash(user, TestDataSeeder.DefaultPassword);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsync(client, phone, TestDataSeeder.DefaultPassword));

        (await Client.GetAsync(readUrl)).StatusCode.Should().Be(HttpStatusCode.OK);
        var write = new HttpRequestMessage(new HttpMethod(writeMethod), writeUrl) { Content = JsonContent.Create(new { }) };
        (await Client.SendAsync(write)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
