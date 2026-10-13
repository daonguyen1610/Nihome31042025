using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace NihomeBackend.IntegrationTests.Infrastructure;

public sealed class JsonLoggingPipelineTests(NihomeWebApplicationFactory factory)
    : IClassFixture<NihomeWebApplicationFactory>
{
    [Fact]
    public async Task ApiResponse_EchoesValidCorrelationId()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/logging-contract-probe");
        request.Headers.Add("X-Correlation-Id", "frontend-request-123");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("frontend-request-123", ReadCorrelationId(response.Headers));
    }

    [Fact]
    public async Task ApiResponse_ReplacesUnsafeCorrelationId()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/logging-contract-probe");
        request.Headers.Add("X-Correlation-Id", "unsafe value");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var correlationId = ReadCorrelationId(response.Headers);
        Assert.NotEqual("unsafe value", correlationId);
        Assert.Matches("^[a-f0-9]{32}$", correlationId);
    }

    private static string ReadCorrelationId(HttpResponseHeaders headers) =>
        Assert.Single(headers.GetValues("X-Correlation-Id"));
}
