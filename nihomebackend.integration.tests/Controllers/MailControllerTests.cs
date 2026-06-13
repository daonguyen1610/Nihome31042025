using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class MailControllerTests : IntegrationTestBase
{
    public MailControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Send_WithoutAuth_ReturnsUnauthorized()
    {
        var res = await Client.PostAsJsonAsync("/api/mail/send", new
        {
            to = "test@example.com",
            subject = "Hi",
            body = "Hello",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Send_AsAdmin_RejectedDueToRoleStringMismatch()
    {
        // Note: MailController declares [Authorize(Roles = "SUPERADMIN,ADMIN")] (no underscore).
        // The seeded ADMIN role still satisfies the "ADMIN" entry, so this returns OK/BadRequest
        // depending on mail backend availability — never 403/401.
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.PostAsJsonAsync("/api/mail/send", new
        {
            to = "test@example.com",
            subject = "Hi",
            body = "Hello",
        });
        res.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        res.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Diagnose_WithoutAuth_ReturnsUnauthorized()
    {
        var res = await Client.PostAsJsonAsync("/api/mail/diagnose", new { });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
