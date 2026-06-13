using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class ContactsControllerTests : IntegrationTestBase
{
    public ContactsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Submit_IsPublic_ReturnsCreated()
    {
        var res = await Client.PostAsJsonAsync("/api/contacts", new
        {
            name = "Visitor",
            email = "visitor@example.com",
            phone = "0900000000",
            subject = "Hello",
            message = "Please contact me",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Submit_InvalidEmail_ReturnsBadRequest()
    {
        var res = await Client.PostAsJsonAsync("/api/contacts", new
        {
            name = "Visitor",
            email = "not-an-email",
            subject = "Hello",
            message = "Body",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/contacts")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsAdmin_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        (await Client.GetAsync("/api/contacts")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_AsAdmin_Unknown_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        (await Client.GetAsync("/api/contacts/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
