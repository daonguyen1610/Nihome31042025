using Microsoft.AspNetCore.StaticFiles;

namespace NihomeBackend.Infrastructure;

/// <summary>
/// How the admin single-page app is served. Production is a plain dotnet
/// publish on IIS, so these rules are what keep a redeploy from stranding
/// open tabs on files that no longer exist.
/// </summary>
public static class SpaHosting
{
    /// <summary>
    /// Paths answered with index.html: every page, but never the API and never
    /// a bundle file. A missing /assets file must be a 404, not index.html,
    /// or a lazy page waits on HTML it cannot run.
    /// </summary>
    public const string FallbackPathRegex = "^(?!api($|/)|assets/).*$";

    /// <summary>
    /// index.html is revalidated on every load so a deploy is picked up at once;
    /// hashed bundle files never change, so browsers keep them for good.
    /// </summary>
    public static void ApplyCacheHeaders(StaticFileResponseContext context)
    {
        var headers = context.Context.Response.Headers;
        if (string.Equals(context.File.Name, "index.html", StringComparison.OrdinalIgnoreCase))
            headers.CacheControl = "no-cache";
        else if (context.Context.Request.Path.StartsWithSegments("/assets"))
            headers.CacheControl = "public, max-age=31536000, immutable";
    }
}
