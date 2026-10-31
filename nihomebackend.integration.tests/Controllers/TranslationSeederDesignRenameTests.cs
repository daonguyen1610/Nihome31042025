using Microsoft.EntityFrameworkCore;
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
}
