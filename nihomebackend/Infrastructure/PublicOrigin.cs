using Microsoft.AspNetCore.Http;

namespace NihomeBackend.Infrastructure;

/// <summary>
/// The address people reach NICON on, for links in emails. It is read from the
/// request being served, not from configuration: production is a plain
/// dotnet publish on IIS without environment settings, and the same build runs
/// on http, https and the local dev server.
/// </summary>
public static class PublicOrigin
{
    /// <summary>
    /// The browser page's origin when it names the host serving this request
    /// (or a loopback dev server that proxies the API); otherwise the scheme,
    /// host and application path of the request. Null outside a request.
    /// </summary>
    public static string? FromRequest(HttpRequest? request)
    {
        if (request is null || !request.Host.HasValue) return null;

        // An Origin naming another site is ignored, so a forged header cannot
        // point an email at a page outside NICON.
        if (Uri.TryCreate(request.Headers.Origin.ToString(), UriKind.Absolute, out var page) &&
            (page.Scheme == Uri.UriSchemeHttp || page.Scheme == Uri.UriSchemeHttps) &&
            (page.IsLoopback || string.Equals(page.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase)))
            return page.GetLeftPart(UriPartial.Authority);

        if (request.Scheme != Uri.UriSchemeHttp && request.Scheme != Uri.UriSchemeHttps) return null;
        return $"{request.Scheme}://{request.Host.Value}{request.PathBase.Value}".TrimEnd('/');
    }
}
