using NihomeBackend.Data;
using NihomeBackend.Models;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

public sealed class TranslationSeederTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();

    [Fact]
    public void Seed_ExistingKeyWithDifferentCase_UpdatesWithoutDuplicate()
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
        Assert.Equal("Quản lý dự án", translation.Value);
        Assert.Equal("designProjects", translation.Category);
    }

    public void Dispose() => _db.Dispose();
}
