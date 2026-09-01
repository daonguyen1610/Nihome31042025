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

    [Fact]
    public async Task ComputeAsync_EquivalentMultipartWithDifferentBoundary_ProducesSameHash()
    {
        var first = await BuildMultipartRequest("boundary-one", "DesignBasic", "plan.pdf", "content"u8.ToArray());
        var second = await BuildMultipartRequest("boundary-two", "DesignBasic", "plan.pdf", "content"u8.ToArray());

        Assert.Equal(await _sut.ComputeAsync(first), await _sut.ComputeAsync(second));
    }

    [Fact]
    public async Task ComputeAsync_MultipartFileOrFieldChange_ProducesDifferentHash()
    {
        var original = await BuildMultipartRequest("boundary-one", "DesignBasic", "plan.pdf", "content"u8.ToArray());
        var changedFile = await BuildMultipartRequest("boundary-two", "DesignBasic", "plan.pdf", "changed"u8.ToArray());
        var changedCategory = await BuildMultipartRequest("boundary-three", "Survey", "plan.pdf", "content"u8.ToArray());
        var originalHash = await _sut.ComputeAsync(original);

        Assert.NotEqual(originalHash, await _sut.ComputeAsync(changedFile));
        Assert.NotEqual(originalHash, await _sut.ComputeAsync(changedCategory));
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

    private static async Task<HttpRequest> BuildMultipartRequest(
        string boundary,
        string category,
        string fileName,
        byte[] bytes)
    {
        using var content = new MultipartFormDataContent(boundary);
        content.Add(new StringContent(category), "category");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", fileName);
        var body = await content.ReadAsByteArrayAsync();
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/operational-projects/1/documents";
        context.Request.ContentType = content.Headers.ContentType!.ToString();
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentLength = body.Length;
        return context.Request;
    }
}
