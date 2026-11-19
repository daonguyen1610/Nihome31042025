using Microsoft.AspNetCore.Http;
using NihomeBackend.Infrastructure;

namespace nihomebackend.tests.Infrastructure;

/// <summary>
/// Links in emails use the address people reached NICON on. Production is a
/// plain dotnet publish on IIS with no configuration for it, so it is read
/// from the request being served.
/// </summary>
public sealed class PublicOriginTests
{
    private static HttpRequest Request(string scheme, string host, string? origin = null, string pathBase = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        context.Request.PathBase = pathBase;
        if (origin is not null) context.Request.Headers.Origin = origin;
        return context.Request;
    }

    [Theory]
    [InlineData("http", "niconvn.com", "http://niconvn.com")]
    [InlineData("https", "niconvn.com", "https://niconvn.com")]
    [InlineData("https", "niconvn.com:8443", "https://niconvn.com:8443")]
    public void UsesTheAddressTheRequestCameIn(string scheme, string host, string expected) =>
        Assert.Equal(expected, PublicOrigin.FromRequest(Request(scheme, host)));

    [Fact]
    public void KeepsTheApplicationPathWhenHostedBelowTheSiteRoot() =>
        Assert.Equal("https://niconvn.com/nicon",
            PublicOrigin.FromRequest(Request("https", "niconvn.com", pathBase: "/nicon")));

    [Fact]
    public void PrefersTheBrowserPageWhenTheDevServerProxiesTheApi() =>
        // Vite serves the admin on :5043 and proxies /api to the backend on :5044.
        Assert.Equal("http://localhost:5043",
            PublicOrigin.FromRequest(Request("http", "localhost:5044", "http://localhost:5043")));

    [Fact]
    public void IgnoresAnOriginHeaderNamingAnotherSite() =>
        Assert.Equal("https://niconvn.com",
            PublicOrigin.FromRequest(Request("https", "niconvn.com", "https://phishing.example")));

    [Fact]
    public void HasNoAnswerOutsideARequest() => Assert.Null(PublicOrigin.FromRequest(null));
}
