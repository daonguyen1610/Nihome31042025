using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class LegalChecklistTemplatesControllerTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task LegalAuthors_CmPublishes_AndVersionHistoryRemainsImmutable()
    {
        await Login("LEGAL_OFFICER");
        var request = Request();
        var created = await Post("/api/legal-checklist-templates", request);
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var draft = await ReadJsonAsync(created);
        var id = draft.GetProperty("id").GetInt32();
        var token = draft.GetProperty("rowVersion").GetString()!;
        var forbidden = await Post($"/api/legal-checklist-templates/{id}/publish", new { rowVersion = token, approvalReference = "CM approved" });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await Login("PM");
        var noEvidence = await Post($"/api/legal-checklist-templates/{id}/publish", new { rowVersion = token });
        noEvidence.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var published = await Post($"/api/legal-checklist-templates/{id}/publish", new { rowVersion = token, approvalReference = "CM/NICON test approval" });
        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
        var current = await ReadJsonAsync(published);
        current.GetProperty("status").GetString().Should().Be("Published");
        current.GetProperty("publishedByUserId").GetInt32().Should().NotBe(current.GetProperty("createdByUserId").GetInt32());
        var version = current.GetProperty("rowVersion").GetString()!;

        await Login("LEGAL_OFFICER");
        request.RowVersion = version;
        var immutable = await Client.PutAsJsonAsync($"/api/legal-checklist-templates/{id}", request);
        immutable.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var newVersion = await Post($"/api/legal-checklist-templates/{id}/versions", new { rowVersion = version });
        newVersion.StatusCode.Should().Be(HttpStatusCode.OK, await newVersion.Content.ReadAsStringAsync());
        (await ReadJsonAsync(newVersion)).GetProperty("version").GetInt32().Should().Be(2);

        await Login("PM");
        (await Post($"/api/legal-checklist-templates/{id}/retire", new { rowVersion = version })).EnsureSuccessStatusCode();
        var retired = await ReadJsonAsync(await Client.GetAsync($"/api/legal-checklist-templates/{id}"));
        retired.GetProperty("status").GetString().Should().Be("Retired");
        retired.GetProperty("items").GetRawText().Should().Be(current.GetProperty("items").GetRawText());
        await WithDbAsync(async db => (await db.AuditLogs.CountAsync(x => x.ResourceType == "LegalChecklistTemplate" && x.ResourceId == request.Code + ":1")).Should().Be(3));
    }

    [Fact]
    public async Task SelfApproval_StaleEdits_AndInvalidCatalogueAreRejected()
    {
        await Login("SUPER_ADMIN");
        var request = Request();
        var created = await Post("/api/legal-checklist-templates", request);
        created.EnsureSuccessStatusCode();
        var draft = await ReadJsonAsync(created);
        var id = draft.GetProperty("id").GetInt32();
        var token = draft.GetProperty("rowVersion").GetString()!;
        var self = await Post($"/api/legal-checklist-templates/{id}/publish", new { rowVersion = token, approvalReference = "CM/NICON test" });
        self.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(self)).GetProperty("code").GetString().Should().Be("LEGAL_TEMPLATE_SELF_APPROVAL");
        request.RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        (await Client.PutAsJsonAsync($"/api/legal-checklist-templates/{id}", request)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var invalid = Request(); invalid.Items[0].PermitTypeCode = "not-a-permit";
        (await Post("/api/legal-checklist-templates", invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var blank = Request(); blank.NameZh = " ";
        (await Post("/api/legal-checklist-templates", blank)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await Login("USER");
        (await Client.GetAsync("/api/legal-checklist-templates")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Post("/api/legal-checklist-templates", Request())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private Task Login(string role) => AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));
    private async Task<HttpResponseMessage> Post(string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await Client.SendAsync(request);
    }
    private static SaveLegalChecklistTemplateRequest Request() => new()
    {
        Code = $"LEGAL-{Guid.NewGuid():N}".ToUpperInvariant(),
        NameVi = "Pháp lý nhà máy",
        Name = "Factory permits",
        NameZh = "工厂许可",
        NameJa = "工場許可",
        ScopeVi = "Mẫu kiểm thử",
        Scope = "Test fixture",
        ScopeZh = "测试模板",
        ScopeJa = "テスト用",
        Items = [new() { PermitTypeCode = "gpxd", IsRequired = true }],
    };
}
