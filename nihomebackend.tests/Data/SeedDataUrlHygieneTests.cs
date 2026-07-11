using System.Reflection;
using System.Text.RegularExpressions;

namespace nihomebackend.tests.Data;

/// <summary>
/// Guards against reintroducing dev-host absolute URLs (`http://localhost:PORT/…`,
/// `http://127.0.0.1/…`, etc.) into any embedded seed resource. Such URLs slip
/// through code review easily and only surface as broken images in production.
/// If you legitimately need an external URL in seed data, add its host to the
/// AllowedExternalHosts set below.
/// </summary>
public class SeedDataUrlHygieneTests
{
    private static readonly Regex DevHostPattern = new(
        @"https?://(localhost|127\.0\.0\.1|0\.0\.0\.0|host\.docker\.internal)(:\d+)?/",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void EmbeddedSeedResources_DoNotContainDevHostUrls()
    {
        var asm = typeof(NihomeBackend.Data.AppDbContext).Assembly;
        var seedResources = asm.GetManifestResourceNames()
            .Where(n => n.Contains(".Seeds.", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(seedResources);

        var offenders = new List<string>();
        foreach (var resourceName in seedResources)
        {
            using var stream = asm.GetManifestResourceStream(resourceName);
            Assert.NotNull(stream);
            using var reader = new StreamReader(stream!);
            var body = reader.ReadToEnd();

            foreach (Match m in DevHostPattern.Matches(body))
            {
                offenders.Add($"{resourceName}: {m.Value}…");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Seed resources must use relative image paths, not dev-host absolute URLs:\n  " +
                string.Join("\n  ", offenders.Take(20)));
    }
}
