using NihomeBackend.Data;
using NihomeBackend.Models.Rbac;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

/// <summary>
/// The role matrix at /admin/roles renders <c>rbac.perm.{code}.label</c> for
/// every catalog permission and <c>adminRbac.module.{module}</c> for the module
/// filter. A new [RequirePermission] endpoint without seed translations shows
/// raw keys there, so this pins every catalog entry to all four languages.
/// </summary>
public sealed class RbacTranslationCoverageTests : IDisposable
{
    private static readonly string[] Languages = ["en", "ja", "vi", "zh"];
    private readonly AppDbContext _db = DbContextFactory.Create();

    [Fact]
    public void Seed_EveryCatalogPermissionHasLabelAndDescriptionInAllLanguages()
    {
        var missing = RequiredKeysMissingTranslations(Catalog()
            .SelectMany(entry => new[]
            {
                $"rbac.perm.{entry.Code}.label",
                $"rbac.perm.{entry.Code}.description",
            }));

        Assert.Empty(missing);
    }

    [Fact]
    public void Seed_EveryPermissionModuleHasFilterLabelInAllLanguages()
    {
        var missing = RequiredKeysMissingTranslations(Catalog()
            .Select(entry => $"adminRbac.module.{entry.Code.Split('.')[0]}"));

        Assert.Empty(missing);
    }

    private static IReadOnlyList<PermissionCatalog.Entry> Catalog() =>
        PermissionCatalog.Resolve(RbacSeedData.Default.BaseCatalog, PermissionDiscovery.Discover());

    private List<string> RequiredKeysMissingTranslations(IEnumerable<string> keys)
    {
        TranslationSeeder.Seed(_db);
        var languagesByKey = _db.Translations
            .Where(item => item.Value != "")
            .AsEnumerable()
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.LanguageCode).ToHashSet(),
                StringComparer.OrdinalIgnoreCase);

        return keys
            .Distinct()
            .Where(key => !languagesByKey.TryGetValue(key, out var languages)
                || !Languages.All(languages.Contains))
            .Order()
            .ToList();
    }

    public void Dispose() => _db.Dispose();
}
