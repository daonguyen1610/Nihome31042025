using System.Xml.Linq;
using NihomeBackend.Services;

namespace NihomeBackend.Tests.Deployment;

/// <summary>
/// Production runs in IIS in-process (plain <c>dotnet publish</c> on Plesk). IIS request
/// filtering rejects bodies above 30,000,000 bytes (~28.6 MB) before the app sees them, so
/// the published web.config must raise <c>maxAllowedContentLength</c> or every upload above
/// that size fails whatever the Settings limits allow.
/// </summary>
public class IisWebConfigTests
{
    private static XDocument LoadProjectWebConfig()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nihomebackend", "web.config")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return XDocument.Load(Path.Combine(directory!.FullName, "nihomebackend", "web.config"));
    }

    [Fact]
    public void WebConfig_RaisesIisRequestLimit_AboveEveryUploadBody()
    {
        var config = LoadProjectWebConfig();

        var limit = config.Descendants("requestLimits").Single().Attribute("maxAllowedContentLength");
        Assert.NotNull(limit);
        var bytes = ulong.Parse(limit!.Value);
        // IIS stores it as uint: this is the most it accepts (~4 GB).
        Assert.Equal(uint.MaxValue, bytes);
        Assert.True(bytes >= (ulong)ProjectDocumentStorageService.MultipartBodyLengthLimit);
    }

    [Fact]
    public void WebConfig_KeepsTheInProcessAspNetCoreHost()
    {
        var host = LoadProjectWebConfig().Descendants("aspNetCore").Single();

        Assert.Equal("inprocess", host.Attribute("hostingModel")?.Value);
        Assert.Equal(@".\nihomebackend.dll", host.Attribute("arguments")?.Value);
    }
}
