using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task CreateLead_TurnsMessageIntoWebsiteLeadOnlyOnce()
    {
        var contactId = await WithDbAsync(async db =>
        {
            var contact = new NihomeBackend.Models.ContactMessage
            {
                Name = "Anh Quang " + Guid.NewGuid().ToString("N")[..6],
                Email = "quang.nhapho@example.com",
                Phone = "0908123456",
                Subject = "Báo giá thiết kế nhà phố 4 tầng",
                Message = "Tôi cần tư vấn thiết kế và thi công nhà phố 5x20m tại Thủ Đức.",
            };
            db.ContactMessages.Add(contact);
            await db.SaveChangesAsync();
            return contact.Id;
        });

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        (await Client.PostAsync($"/api/contacts/{contactId}/lead", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var first = await Client.PostAsync($"/api/contacts/{contactId}/lead", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        var body = await ReadJsonAsync(first);
        body.GetProperty("created").GetBoolean().Should().BeTrue();
        var leadId = body.GetProperty("leadId").GetInt32();

        var again = await ReadJsonAsync(await Client.PostAsync($"/api/contacts/{contactId}/lead", null));
        again.GetProperty("created").GetBoolean().Should().BeFalse();
        again.GetProperty("leadId").GetInt32().Should().Be(leadId);

        var lead = await WithDbAsync(db => db.Leads.AsNoTracking().SingleAsync(item => item.Id == leadId));
        lead.SourceCode.Should().Be("website");
        lead.Phone.Should().Be("0908123456");
        lead.Note.Should().Contain("Báo giá thiết kế nhà phố 4 tầng").And.Contain("Thủ Đức");
        (await ReadJsonAsync(await Client.GetAsync($"/api/contacts/{contactId}")))
            .GetProperty("leadId").GetInt32().Should().Be(leadId);
        (await Client.PostAsync("/api/contacts/999999/lead", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
