using NihomeBackend.Data;
using NihomeBackend.Models;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

public sealed class TranslationSeederTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();

    [Fact]
    public void Seed_ExistingKeyWithDifferentCase_PreservesAdminValueAndCategoryWithoutDuplicate()
    {
        _db.Translations.Add(new Translation
        {
            Key = "designProjects.team.role.projectManager",
            LanguageCode = "vi",
            Value = "Legacy value",
            Category = "legacy",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();

        TranslationSeeder.Seed(_db);

        var matches = _db.Translations
            .Where(translation =>
                translation.Key.ToLower() == "designprojects.team.role.projectmanager" &&
                translation.LanguageCode == "vi")
            .ToList();
        var translation = Assert.Single(matches);
        Assert.Equal("Legacy value", translation.Value);
        Assert.Equal("legacy", translation.Category);
    }

    [Fact]
    public void Seed_ExistingKeyBackfillsOnlyBlankCategoryAndAddsMissingLanguages()
    {
        var existing = new Translation
        {
            Key = "designProjects.team.role.projectManager",
            LanguageCode = "vi",
            Value = "Admin value",
            Category = " ",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Translations.Add(existing);
        _db.SaveChanges();

        TranslationSeeder.Seed(_db);
        var id = existing.TranslationId;

        Assert.Equal("Admin value", existing.Value);
        Assert.Equal("designProjects", existing.Category);
        Assert.Equal(4, _db.Translations.Count(translation =>
            translation.Key.ToLower() == "designprojects.team.role.projectmanager"));

        TranslationSeeder.Seed(_db);

        Assert.Equal(id, existing.TranslationId);
        Assert.Equal("Admin value", existing.Value);
        Assert.Equal(4, _db.Translations.Count(translation =>
            translation.Key.ToLower() == "designprojects.team.role.projectmanager"));
    }

    [Fact]
    public void Seed_RerunKeepsStableIdsAndCountsAcrossOverlappingResources()
    {
        TranslationSeeder.Seed(_db);
        var first = _db.Translations.ToDictionary(
            translation => translation.Key + "|" + translation.LanguageCode,
            translation => translation.TranslationId,
            StringComparer.OrdinalIgnoreCase);

        TranslationSeeder.Seed(_db);

        var second = _db.Translations.ToDictionary(
            translation => translation.Key + "|" + translation.LanguageCode,
            translation => translation.TranslationId,
            StringComparer.OrdinalIgnoreCase);
        Assert.Equal(first.Count, second.Count);
        Assert.All(first, pair => Assert.Equal(pair.Value, second[pair.Key]));
    }

    [Fact]
    public void Seed_ProjectReportPresentationKeysHaveAllSupportedLanguages()
    {
        TranslationSeeder.Seed(_db);
        var requiredKeys = new[]
        {
            "projectReports.openProject",
            "projectReports.dataQuality.summary",
            "projectReports.reason.SOURCE_NOT_CONFIGURED",
            "projectReports.reason.DESIGN_BASELINE_INCOMPLETE",
            "projectReports.reason.HISTORICAL_SNAPSHOTS_UNAVAILABLE",
            "projectReports.reason.CONSTRUCTION_WEIGHTS_UNAVAILABLE",
            "projectReports.reason.ACCEPTANCE_OUTCOME_DATA_UNAVAILABLE",
            "projectReports.reason.ACTUAL_FINANCE_LEDGER_UNAVAILABLE",
            "projectReports.reason.INVENTORY_LEDGER_UNAVAILABLE",
            "projectReports.reason.BOQ_USAGE_DATA_UNAVAILABLE",
            "projectReports.reason.VENDOR_PERFORMANCE_DATA_UNAVAILABLE",
            "projectReports.metric.historicalSCurve",
            "projectReports.metric.weightedConstructionProgress",
            "projectReports.metric.acceptanceRatios",
            "projectReports.metric.acceptanceFirstPass",
            "projectReports.metric.actualCashflow",
            "projectReports.metric.actualRevenue",
            "projectReports.metric.actualExpenditure",
            "projectReports.metric.profitAndLoss",
            "projectReports.metric.receivables",
            "projectReports.metric.inventory",
            "projectReports.metric.boqUsage",
            "projectReports.metric.vendorPerformance",
        };

        foreach (var key in requiredKeys)
        {
            var translations = _db.Translations.Where(item => item.Key == key).ToList();
            Assert.Equal(4, translations.Count);
            Assert.Equal(new[] { "en", "ja", "vi", "zh" },
                translations.Select(item => item.LanguageCode).OrderBy(code => code));
            Assert.All(translations, item => Assert.False(string.IsNullOrWhiteSpace(item.Value)));
        }
    }

    public void Dispose() => _db.Dispose();
}
