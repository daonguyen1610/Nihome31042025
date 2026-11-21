using NihomeBackend.Constants;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

/// <summary>One naming rule for master-data options, in the master-data list and on records.</summary>
public sealed class MasterDataNamesTests
{
    [Fact]
    public async Task Names_PreferWhatAnAdminEntered_ThenTheSeededTranslation_ThenTheName()
    {
        using var db = DbContextFactory.Create();
        MasterDataOption Option(string code, string name, string? labelKey) => new()
        {
            Category = "business_sector",
            Code = code,
            Name = name,
            LabelKey = labelKey,
            IsActive = true,
        };
        var renamed = Option("food-beverage", "Thực phẩm & Đồ uống", "masterData.business_sector.food-beverage.label");
        var seeded = Option("textile-garment", "Dệt may", "masterData.business_sector.textile-garment.label");
        var added = Option("kho-van", "Kho vận", null);
        db.MasterDataOptions.AddRange(renamed, seeded, added);
        db.Translations.AddRange(
            new Translation { Key = renamed.LabelKey!, LanguageCode = "en", Value = "Food & beverage" },
            new Translation { Key = seeded.LabelKey!, LanguageCode = "en", Value = "Textiles & garments" });
        await db.SaveChangesAsync();
        db.EntityTranslations.Add(new EntityTranslation
        {
            EntityType = EntityTypes.MasterDataOption,
            EntityId = renamed.Id,
            LanguageCode = "en",
            FieldName = "Name",
            Value = "Food & drinks",
        });
        await db.SaveChangesAsync();
        var options = new[] { renamed, seeded, added };

        var english = await MasterDataNames.LocalizeAsync(db, options, "en", default);
        var vietnamese = await MasterDataNames.LocalizeAsync(db, options, "vi", default);

        Assert.Equal("Food & drinks", english[renamed.Id]);       // what an admin entered for English
        Assert.Equal("Textiles & garments", english[seeded.Id]); // the seeded translation
        Assert.Equal("Kho vận", english[added.Id]);              // an option added later: its name
        Assert.Equal("Thực phẩm & Đồ uống", vietnamese[renamed.Id]); // Vietnamese is the source name
    }
}
