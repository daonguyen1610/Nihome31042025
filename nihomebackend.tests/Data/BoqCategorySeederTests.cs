using System.Text.Json;
using NihomeBackend.Data;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

public sealed class BoqCategorySeederTests
{
    [Fact]
    public void Seed_SixDistinctSourceProfiles_ValidateAndPreserveUserConfiguration()
    {
        using var db = DbContextFactory.Create();
        BoqCategorySeeder.Seed(db);
        var catalogs = db.MaterialRateCatalogs.OrderBy(item => item.Code).ToList();
        Assert.Equal(6, catalogs.Count);
        Assert.Equal(6, catalogs.Select(item => BoqConfigurationRules.Read(item.BoqConfigurationJson)!.SourceFile).Distinct().Count());
        Assert.Equal(6, catalogs.Select(item =>
        {
            var config = BoqConfigurationRules.Read(item.BoqConfigurationJson)!;
            return string.Join("|", config.Sheets.Select(sheet => $"{sheet.IsPriced}:" +
                string.Join(",", sheet.Fields.Select(field => $"{field.Key}:{field.Type}:{field.Binding}"))));
        }).Distinct().Count());
        foreach (var catalog in catalogs)
        {
            var config = BoqConfigurationRules.Read(catalog.BoqConfigurationJson)!;
            Assert.Equal(catalog.BoqConfigurationJson, BoqConfigurationRules.Validate(config));
            Assert.All(config.Sheets, sheet => Assert.All(sheet.Fields, field => Assert.All(
                new[] { field.NameVi, field.Name, field.NameZh, field.NameJa }, name => Assert.False(string.IsNullOrWhiteSpace(name)))));
        }
        catalogs[0].Name = "Nicon reviewed category";
        var original = catalogs[0].BoqConfigurationJson;
        db.SaveChanges();
        BoqCategorySeeder.Seed(db);
        Assert.Equal(6, db.MaterialRateCatalogs.Count());
        Assert.Equal("Nicon reviewed category", catalogs[0].Name);
        Assert.Equal(original, catalogs[0].BoqConfigurationJson);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("missingLanguage")]
    [InlineData("binding")]
    public void InvalidConfiguration_IsRejected(string reason)
    {
        using var db = DbContextFactory.Create();
        BoqCategorySeeder.Seed(db);
        var config = BoqConfigurationRules.Read(db.MaterialRateCatalogs.First().BoqConfigurationJson)!;
        if (reason == "duplicate") config.Sheets[0].Fields[1].Key = config.Sheets[0].Fields[0].Key;
        if (reason == "missingLanguage") config.Sheets[0].Fields[0].NameJa = "";
        if (reason == "binding") config.Sheets[0].Fields[0].Binding = "UnitPrice";
        Assert.Throws<MaterialRateOperationException>(() => BoqConfigurationRules.Validate(config));
    }
}
