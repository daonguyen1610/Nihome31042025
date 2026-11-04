using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class TranslationSeederDesignRenameTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Seed_UsesDesignDossierDefaultsAndPreservesEditedTranslation()
    {
        await WithDbAsync(async db =>
        {
            TranslationSeeder.Seed(db);
            var label = await db.Translations.SingleAsync(item =>
                item.Key == "nav.designProjects" && item.LanguageCode == "vi");
            var englishLabel = await db.Translations.SingleAsync(item =>
                item.Key == "nav.designProjects" && item.LanguageCode == "en");
            var customized = await db.Translations.SingleAsync(item =>
                item.Key == "designProjects.title" && item.LanguageCode == "vi");
            label.Value.Should().Be("Hồ Sơ Thiết Kế");
            englishLabel.Value.Should().Be("Design dossiers");
            customized.Value = "Tên thiết kế theo cấu hình riêng";
            await db.SaveChangesAsync();

            TranslationSeeder.Seed(db);

            customized.Value.Should().Be("Tên thiết kế theo cấu hình riêng");
        });
    }

    [Fact]
    public void DossierMigration_UpdatesOnlyDesignContractDefaults()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=migration-script-only;Database=NiconMigrationReview;Integrated Security=true;TrustServerCertificate=true")
            .Options);
        var script = db.GetService<IMigrator>().GenerateScript(
            "20261101040313_SyncDesignContractTranslations",
            "20261104220000_RenameDesignContractsToDossiers");

        script.Split("UPDATE translations", StringSplitOptions.None).Length.Should().Be(158);
        script.Should().Contain("WHERE [Key] = N'nav.designProjects'");
        script.Should().Contain("AND LanguageCode = N'vi'");
        script.Should().Contain("AND Value = N'Hợp Đồng Thiết Kế'");
        script.Should().Contain("SET Value = N'Hồ Sơ Thiết Kế'");
        script.Should().Contain("AND LanguageCode = N'ja'");
        script.Should().NotContain("INSERT INTO translations");
    }

    [Fact]
    public void Migration_UpdatesOnlyPreviousDefaultsForAllLanguages()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=migration-script-only;Database=NiconMigrationReview;Integrated Security=true;TrustServerCertificate=true")
            .Options);
        var script = db.GetService<IMigrator>().GenerateScript(
            "20261031180001_AddWorkTaskAttachments",
            "20261101040313_SyncDesignContractTranslations");

        script.Split("UPDATE translations", StringSplitOptions.None).Length.Should().Be(146);
        script.Should().Contain("WHERE [Key] = N'nav.designProjects'");
        script.Should().Contain("AND LanguageCode = N'vi'");
        script.Should().Contain("AND Value = N'Dự án thiết kế'");
        script.Should().Contain("SET Value = N'Hợp Đồng Thiết Kế'");
        script.Should().Contain("AND LanguageCode = N'ja'");
        script.Should().NotContain("INSERT INTO translations");
    }
}
