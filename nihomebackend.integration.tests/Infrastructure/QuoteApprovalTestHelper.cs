using System.Net.Http.Json;
using System.Text.Json;

namespace NihomeBackend.IntegrationTests.Infrastructure;

internal static class QuoteApprovalTestHelper
{
    public static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, int quoteId, string rowVersion)
    {
        using var candidates = await client.GetAsync($"/api/quotes/{quoteId}/approval-candidates");
        candidates.EnsureSuccessStatusCode();
        using var candidateBody = JsonDocument.Parse(await candidates.Content.ReadAsStringAsync());
        var approverId = candidateBody.RootElement.EnumerateArray()
            .Single(candidate => candidate.GetProperty("roleCode").GetString() == "SUPER_ADMIN")
            .GetProperty("id").GetInt32();
        using var preview = await client.PostAsJsonAsync($"/api/quotes/{quoteId}/approval-email-preview", new { approverUserId = approverId, rowVersion });
        preview.EnsureSuccessStatusCode();
        using var previewBody = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        return await client.PostAsJsonAsync($"/api/quotes/{quoteId}/submit", new
        {
            approverUserId = approverId,
            rowVersion,
            previewToken = previewBody.RootElement.GetProperty("previewToken").GetString(),
        });
    }

    public static async Task<HttpResponseMessage> ApproveAsync(HttpClient client, int quoteId, string rowVersion)
    {
        var prior = client.DefaultRequestHeaders.Authorization;
        try
        {
            await AuthTestHelper.AuthenticateAsync(client, AuthTestHelper.LoginAsSuperAdminAsync);
            return await client.PostAsJsonAsync($"/api/quotes/{quoteId}/approve", new { rowVersion });
        }
        finally { client.DefaultRequestHeaders.Authorization = prior; }
    }
}
