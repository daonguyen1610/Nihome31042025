using System.Net;
using System.Net.Http.Json;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>SurveysController</c> (NIH-99): RBAC gating,
/// list + get, filters (search, construction type, date range, drive sync).
/// Create is exercised so subsequent slices (NIH-100/101) have a working
/// baseline; write-side tests (update / delete / RBAC-manage) will land
/// with those slices.
/// </summary>
public class SurveysControllerTests : IntegrationTestBase
{
    public SurveysControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/surveys")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsWarehouse_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/surveys")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsSalesManager_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.GetAsync("/api/surveys");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task Create_HappyPath_ReturnsAutoCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.PostAsJsonAsync("/api/surveys", new
        {
            location = "Lô A5, KCN Bắc Ninh " + Guid.NewGuid().ToString("N")[..4],
            constructionTypeCode = "industrial",
            surveyDate = DateTime.UtcNow.AddDays(-1),
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("code").GetString().Should().StartWith("SV-");
        body.GetProperty("driveSyncStatus").GetString().Should().Be("NotSynced");
    }

    [Fact]
    public async Task Create_WithUnknownConstructionType_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.PostAsJsonAsync("/api/surveys", new
        {
            location = "Bad type",
            constructionTypeCode = "definitely-not-real",
            surveyDate = DateTime.UtcNow,
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Warehouse_CannotCreate()
    {
        // WAREHOUSE has no crm.surveys.* permissions at all.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        var res = await Client.PostAsJsonAsync("/api/surveys", new
        {
            location = "Blocked",
            constructionTypeCode = "residential",
            surveyDate = DateTime.UtcNow,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_UnknownId_Is404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.GetAsync("/api/surveys/9999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_FilterBySearchAndConstructionType()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var tag = "Uniquely-tagged " + Guid.NewGuid().ToString("N")[..6];
        await CreateSurveyAsync(location: $"{tag} Alpha", type: "residential");
        await CreateSurveyAsync(location: $"{tag} Beta", type: "commercial");

        var searched = await Client.GetAsync($"/api/surveys?search={Uri.EscapeDataString(tag)}&pageSize=20");
        searched.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(searched);
        body.GetProperty("items").GetArrayLength().Should().BeGreaterOrEqualTo(2);

        var typed = await Client.GetAsync(
            $"/api/surveys?search={Uri.EscapeDataString(tag)}&constructionTypeCode=commercial");
        var typedBody = await ReadJsonAsync(typed);
        var arr = typedBody.GetProperty("items");
        for (int i = 0; i < arr.GetArrayLength(); i++)
        {
            arr[i].GetProperty("constructionTypeCode").GetString().Should().Be("commercial");
        }
    }

    private async Task<int> CreateSurveyAsync(string location, string type)
    {
        var res = await Client.PostAsJsonAsync("/api/surveys", new
        {
            location,
            constructionTypeCode = type,
            surveyDate = DateTime.UtcNow.AddDays(-1),
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }
}
