using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed record BoqConfiguredPreview(string SourceHash, string ConfigurationHash, List<BoqEntryRow> Rows);

public sealed class BoqConfiguredWorkbookService(AppDbContext db, IMaterialRateService rates)
{
    public async Task<MaterialRateCatalogResponse?> ConfigureAsync(int catalogId, ConfigureBoqRequest request, int userId, CancellationToken ct)
    {
        var json = BoqConfigurationRules.Validate(request.Configuration);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var catalog = await db.MaterialRateCatalogs.SingleOrDefaultAsync(item => item.Id == catalogId, ct);
        if (catalog is null) return null;
        if (catalog.CatalogType != MaterialRateCatalogType.Boq) throw BoqConfigurationRules.Error("configuration");
        if (catalog.UpdatedAt != request.ExpectedUpdatedAt) throw new BoqConfigurationConflictException();
        if (await db.MaterialRateRevisions.AnyAsync(item => item.CatalogId == catalogId, ct)) throw BoqConfigurationRules.Error("categoryLocked");
        catalog.BoqConfigurationJson = json;
        catalog.UpdatedAt = DateTime.UtcNow;
        catalog.UpdatedByUserId = userId;
        await db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return await rates.GetCatalogAsync(catalogId, ct);
    }

    public async Task<byte[]?> TemplateAsync(int catalogId, string language, CancellationToken ct)
    {
        var catalog = await rates.GetCatalogAsync(catalogId, ct);
        if (catalog == null) return null;
        var config = catalog.BoqConfiguration ?? throw BoqConfigurationRules.Error("configuration");
        using var book = new XLWorkbook();
        var meta = book.AddWorksheet("__NICON");
        meta.Cell(1, 1).Value = catalogId;
        meta.Cell(1, 2).Value = catalog.BoqConfigurationHash!;
        meta.Cell(1, 3).Value = language;
        meta.Visibility = XLWorksheetVisibility.VeryHidden;
        foreach (var sheet in config.Sheets)
        {
            var tab = book.AddWorksheet(sheet.Key);
            for (var column = 0; column < sheet.Fields.Count; column++)
            {
                var field = sheet.Fields[column];
                tab.Cell(1, column + 1).Value = field.Key;
                tab.Cell(2, column + 1).Value = language switch { "en" => field.Name, "zh" => field.NameZh, "ja" => field.NameJa, _ => field.NameVi };
                tab.Column(column + 1).Width = field.Type == "Number" ? 18 : 30;
                tab.Range(3, column + 1, 10002, column + 1).Style.NumberFormat.Format = field.Type == "Number"
                    ? (field.DecimalPlaces == 0 ? "#,##0" : "#,##0." + new string('0', field.DecimalPlaces)) : "@";
            }
            tab.Row(1).Hide();
            tab.Row(2).Style.Font.Bold = true;
            tab.SheetView.FreezeRows(2);
        }
        using var output = new MemoryStream();
        book.SaveAs(output);
        return output.ToArray();
    }

    public async Task<BoqConfiguredPreview?> PreviewAsync(int catalogId, IFormFile? file, CancellationToken ct)
    {
        var catalog = await rates.GetCatalogAsync(catalogId, ct);
        if (catalog == null) return null;
        var config = catalog.BoqConfiguration ?? throw BoqConfigurationRules.Error("configuration");
        if (file == null || file.Length is <= 0 or > 20 * 1024 * 1024 || !Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw BoqConfigurationRules.Error("file");
        using var bytes = new MemoryStream();
        await file.CopyToAsync(bytes, ct);
        var content = bytes.ToArray();
        try
        {
            using (var zip = new ZipArchive(new MemoryStream(content)))
            {
                if (zip.Entries.Count > 500 || zip.Entries.Sum(entry => entry.Length) > 200L * 1024 * 1024 ||
                    zip.Entries.Any(entry => entry.FullName.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.Equals("xl/connections.xml", StringComparison.OrdinalIgnoreCase) || entry.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
                    throw BoqConfigurationRules.Error("file");
            }
            using var book = new XLWorkbook(new MemoryStream(content));
            if (book.Worksheets.Any(sheet => sheet.CellsUsed().Any(cell => cell.HasFormula))) throw BoqConfigurationRules.Error("formula");
            if (!book.TryGetWorksheet("__NICON", out var meta) || meta.Cell(1, 1).GetString() != catalogId.ToString(CultureInfo.InvariantCulture) ||
                meta.Cell(1, 2).GetString() != catalog.BoqConfigurationHash || book.Worksheets.Count != config.Sheets.Count + 1)
                throw BoqConfigurationRules.Error("wrongCategory");
            var language = meta.Cell(1, 3).GetString();
            if (!new[] { "vi", "en", "zh", "ja" }.Contains(language)) throw BoqConfigurationRules.Error("wrongCategory");
            var rows = new List<BoqEntryRow>();
            foreach (var sheet in config.Sheets)
            {
                if (!book.TryGetWorksheet(sheet.Key, out var tab) || (tab.LastColumnUsed()?.ColumnNumber() ?? 0) != sheet.Fields.Count)
                    throw BoqConfigurationRules.Error("wrongFields", sheet.Key);
                for (var column = 0; column < sheet.Fields.Count; column++)
                {
                    var field = sheet.Fields[column];
                    var expectedLabel = language switch { "en" => field.Name, "zh" => field.NameZh, "ja" => field.NameJa, _ => field.NameVi };
                    if (tab.Cell(1, column + 1).GetString() != field.Key || tab.Cell(2, column + 1).GetString() != expectedLabel)
                        throw BoqConfigurationRules.Error("wrongFields", sheet.Key);
                }
                var last = tab.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 2;
                if (last > 10002) throw BoqConfigurationRules.Error("rowLimit");
                for (var number = 3; number <= last; number++)
                {
                    if (tab.Row(number).Cells(1, sheet.Fields.Count).All(cell => cell.IsEmpty())) continue;
                    var row = new BoqEntryRow { SheetKey = sheet.Key };
                    for (var column = 0; column < sheet.Fields.Count; column++)
                    {
                        var field = sheet.Fields[column];
                        var cell = tab.Cell(number, column + 1);
                        if (cell.HasFormula || cell.DataType == XLDataType.Error) throw BoqConfigurationRules.Error("formula", $"{sheet.Key}!{cell.Address}");
                        if (cell.IsEmpty()) continue;
                        if (field.Type == "Number")
                        {
                            if (cell.DataType != XLDataType.Number || !cell.TryGetValue<decimal>(out var numeric))
                                throw BoqConfigurationRules.Error("number", $"{sheet.Key}!{cell.Address}");
                            row.Values[field.Key] = numeric.ToString(CultureInfo.InvariantCulture);
                        }
                        else row.Values[field.Key] = cell.GetString();
                    }
                    rows.Add(row);
                }
            }
            if (rows.Count == 0) throw BoqConfigurationRules.Error("noRows");
            var validated = BoqConfigurationRules.ValidateRows(config, rows);
            return new(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant(), catalog.BoqConfigurationHash!, validated.Select(row => row.Row).ToList());
        }
        catch (MaterialRateOperationException) { throw; }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or FormatException or IOException)
        { throw BoqConfigurationRules.Error("file"); }
    }
}
