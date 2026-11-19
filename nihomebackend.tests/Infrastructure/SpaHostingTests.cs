using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Moq;
using NihomeBackend.Infrastructure;

namespace nihomebackend.tests.Infrastructure;

/// <summary>
/// After a redeploy an open tab asked for its old bundle files; IIS answered
/// them with index.html (200, text/html), the lazy page never loaded and the
/// spinner stayed until a manual reload.
/// </summary>
public sealed class SpaHostingTests
{
    [Theory]
    [InlineData("admin/work-packages/12", true)]
    [InlineData("", true)]
    [InlineData("api/quotes", false)]
    [InlineData("api", false)]
    [InlineData("assets/QuoteDetail-OLDHASH.js", false)]
    [InlineData("assets/index-OLDHASH.css", false)]
    public void OnlyPagesFallBackToIndexHtml_NeverApiOrBundleFiles(string path, bool fallsBack) =>
        Assert.Equal(fallsBack, Regex.IsMatch(path, SpaHosting.FallbackPathRegex));

    [Theory]
    [InlineData("/admin/email-templates", "index.html", "no-cache")]
    [InlineData("/", "index.html", "no-cache")]
    [InlineData("/assets/index-CooRElWV.js", "index-CooRElWV.js", "public, max-age=31536000, immutable")]
    public void IndexHtmlIsRevalidated_HashedBundlesAreCachedForGood(string path, string file, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var info = new Mock<IFileInfo>();
        info.SetupGet(item => item.Name).Returns(file);

        SpaHosting.ApplyCacheHeaders(new StaticFileResponseContext(context, info.Object));

        Assert.Equal(expected, context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public void OtherStaticFilesKeepTheServerDefault()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/images/logo.png";
        var info = new Mock<IFileInfo>();
        info.SetupGet(item => item.Name).Returns("logo.png");

        SpaHosting.ApplyCacheHeaders(new StaticFileResponseContext(context, info.Object));

        Assert.Empty(context.Response.Headers.CacheControl.ToString());
    }
}
