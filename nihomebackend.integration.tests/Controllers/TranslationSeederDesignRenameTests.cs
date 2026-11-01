using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class TranslationSeederDesignRenameTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Seed_UpgradesOldDesignLabelButPreservesEditedTranslation()
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
            label.Value = "Dự án thiết kế";
            englishLabel.Value = "Design projects";
            customized.Value = "Tên thiết kế theo cấu hình riêng";
            await db.SaveChangesAsync();

            TranslationSeeder.Seed(db);

            label.Value.Should().Be("Hợp Đồng Thiết Kế");
            englishLabel.Value.Should().Be("Design contracts");
            customized.Value.Should().Be("Tên thiết kế theo cấu hình riêng");
        });
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
