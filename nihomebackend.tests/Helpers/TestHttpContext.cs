using Microsoft.AspNetCore.Http;

namespace nihomebackend.tests.Helpers;

internal static class TestHttpContext
{
    /// <summary>An accessor whose current request arrived on <paramref name="origin"/>.</summary>
    public static IHttpContextAccessor ServedFrom(string origin)
    {
        var uri = new Uri(origin);
        var context = new DefaultHttpContext();
        context.Request.Scheme = uri.Scheme;
        context.Request.Host = new HostString(uri.Authority);
        return new HttpContextAccessor { HttpContext = context };
    }
}
