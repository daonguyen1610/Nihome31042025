using Microsoft.AspNetCore.Http;
using NihomeBackend.Services;
using System.Text;

namespace nihomebackend.tests.Services;

public class FingerprintServiceTests
{
    private readonly FingerprintService _sut = new();

    [Fact]
    public async Task ComputeAsync_SameRequest_ProducesSameHash()
    {
        var first = await _sut.ComputeAsync(BuildRequest("POST", "/api/customers", "?source=web", "{\"name\":\"A\"}"));
        var second = await _sut.ComputeAsync(BuildRequest("POST", "/api/customers", "?source=web", "{\"name\":\"A\"}"));

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public async Task ComputeAsync_DifferentBody_ProducesDifferentHash()
    {
        var first = await _sut.ComputeAsync(BuildRequest("POST", "/api/customers", body: "{\"name\":\"A\"}"));
        var second = await _sut.ComputeAsync(BuildRequest("POST", "/api/customers", body: "{\"name\":\"B\"}"));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task ComputeAsync_DifferentOperation_ProducesDifferentHash()
    {
        var first = await _sut.ComputeAsync(BuildRequest("POST", "/api/customers", body: "{}"));
        var second = await _sut.ComputeAsync(BuildRequest("PUT", "/api/customers/1", body: "{}"));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task ComputeAsync_RestoresRequestBodyPosition()
    {
        var request = BuildRequest("POST", "/api/customers", body: "{\"name\":\"A\"}");

        await _sut.ComputeAsync(request);
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        Assert.Equal("{\"name\":\"A\"}", body);
    }

    private static HttpRequest BuildRequest(
        string method,
        string path,
        string query = "",
        string body = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        context.Request.ContentType = "application/json";
        context.Request.Headers.AcceptLanguage = "vi-VN";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context.Request;
    }
}
