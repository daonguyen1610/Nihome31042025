using System.Reflection;
using System.Text.Json;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.Data;

public static class BoqCategorySeeder
{
    public static void Seed(AppDbContext db)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("nihomebackend.Data.Seeds.boq-categories.json")!;
        using var document = JsonDocument.Parse(stream);
        var fields = document.RootElement.GetProperty("fields");
        foreach (var profile in document.RootElement.GetProperty("profiles").EnumerateArray())
        {
            var code = profile.GetProperty("code").GetString()!;
            // Never overwrite configured customer categories or existing revisions on restart.
            if (db.MaterialRateCatalogs.Any(item => item.Code == code)) continue;
            var source = profile.GetProperty("configuration");
            var config = new BoqConfiguration { SourceFile = source.GetProperty("sourceFile").GetString() };
            foreach (var sheet in source.GetProperty("sheets").EnumerateArray())
            {
                var definition = JsonSerializer.Deserialize<BoqEntrySheet>(sheet.GetRawText().Replace(sheet.GetProperty("fields").GetRawText(), "[]"), BoqConfigurationRules.Json)!;
                definition.Fields = sheet.GetProperty("fields").EnumerateArray().Select(key =>
                    JsonSerializer.Deserialize<BoqEntryField>(fields.GetProperty(key.GetString()!).GetRawText(), BoqConfigurationRules.Json)!).ToList();
                config.Sheets.Add(definition);
            }
            db.MaterialRateCatalogs.Add(new MaterialRateCatalog
            {
                Code = code,
                Name = profile.GetProperty("name").GetString()!,
                CatalogType = MaterialRateCatalogType.Boq,
                Currency = "VND",
                BoqConfigurationJson = BoqConfigurationRules.Validate(config)
            });
        }
        db.SaveChanges();
    }
}
