using System.Reflection;
using System.Security.Cryptography;
using ClosedXML.Excel;

namespace NihomeBackend.Services;

public sealed record InvestmentRateTemplate(string Code, string NameKey, string File);

// The generic investment-rate (suất đầu tư) forms: one layout, default rows per building type.
public static class InvestmentRateTemplateCatalog
{
    private const string ResourcePrefix = "nihomebackend.Data.InvestmentRateTemplates.";

    public static IReadOnlyList<InvestmentRateTemplate> All { get; } =
    [
        new("SDT-NHA-XUONG", "investmentRateWorkbook.template.factory", "SDT-Nha-xuong.xlsx"),
        new("SDT-VAN-PHONG", "investmentRateWorkbook.template.office", "SDT-Van-phong.xlsx"),
        new("SDT-NOI-THAT", "investmentRateWorkbook.template.fitOut", "SDT-Hoan-thien-noi-that.xlsx"),
    ];

    public static InvestmentRateTemplate? Find(string? code) =>
        All.FirstOrDefault(template => string.Equals(template.Code, code, StringComparison.OrdinalIgnoreCase));

    public static byte[] ReadFile(InvestmentRateTemplate template)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + template.File)
            ?? throw new InvalidOperationException($"Missing investment-rate template resource {template.File}.");
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}

public sealed record InvestmentRateAreaLine(
    int SourceRow, string Name, string? Description, decimal ActualArea, decimal Factor, decimal ConvertedArea, string? Note);

public sealed record InvestmentRateGroupLine(
    int SourceRow, string Code, string Name, string? Description,
    decimal Material, decimal Labour, decimal Equipment, decimal RatePerSqm, string? Specification);

public sealed class InvestmentRateParseResult
{
    // Issues and the file hash use the BOQ report so uploads share one error contract.
    public BoqWorkbookParseResult Report { get; } = new();
    public List<InvestmentRateAreaLine> Areas { get; } = [];
    public List<InvestmentRateGroupLine> Groups { get; } = [];
    public decimal ConvertedArea => Areas.Sum(area => area.ConvertedArea);
    public decimal RatePerSqm => Groups.Sum(group => group.RatePerSqm);
    public decimal Total => Math.Round(ConvertedArea * RatePerSqm, 2, MidpointRounding.AwayFromZero);
    public bool IsValid => Report.IsValid;
}

// Reads the input cells of a filled form and computes every total itself, so a file saved
// without recalculation (or by another spreadsheet app) still gives the right numbers.
public static class InvestmentRateWorkbookParser
{
    public const string Marker = "NICON_INVESTMENT_RATE_TEMPLATE";
    public const decimal MaxFactor = 3m;
    private const decimal MaxNumber = 999999999999m;
    private const int MaxName = 500, MaxCode = 50, MaxText = 1000;

    public static InvestmentRateParseResult Parse(byte[] content, InvestmentRateTemplate template)
    {
        ProjectBoqWorkbookParser.EnsureSafe(content);
        var result = new InvestmentRateParseResult();
        result.Report.Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        XLWorkbook workbook;
        try { workbook = new XLWorkbook(new MemoryStream(content)); }
        catch (Exception error) when (error is not OutOfMemoryException) { throw new BoqWorkbookException("file"); }
        using (workbook)
        {
            var metadata = workbook.Worksheets.FirstOrDefault(sheet => sheet.Name == "_NICON");
            if (metadata?.Cell("A1").GetString() != Marker || metadata.Cell("A2").GetString() != "1")
                throw new BoqWorkbookException("notInvestmentRateForm");
            if (!string.Equals(metadata.Cell("A3").GetString(), template.Code, StringComparison.OrdinalIgnoreCase))
                throw new BoqWorkbookException("wrongInvestmentRateTemplate");
            var sheetName = metadata.Cell("A4").GetString();
            if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var sheet))
            {
                result.Report.Add(new("sheetMissing", sheetName));
                return result;
            }
            var areaRows = TableRows(workbook, sheet, "SDT_AREA_HEADER", "SDT_AREA_TOTAL", result);
            var groupRows = TableRows(workbook, sheet, "SDT_RATE_HEADER", "SDT_RATE_TOTAL", result);
            if (areaRows is { } areas) ReadAreas(sheet, areas, result);
            if (groupRows is { } groups) ReadGroups(sheet, groups, result);
            if (result.IsValid && result.Groups.Count == 0) result.Report.Add(new("noInvestmentRateGroups", sheet.Name));
        }
        return result;
    }

    // The rows between a table's header and its total line; both are workbook names so inserted rows still count.
    private static (int First, int Last)? TableRows(
        XLWorkbook workbook, IXLWorksheet sheet, string headerName, string totalName, InvestmentRateParseResult result)
    {
        var header = NamedRow(workbook, sheet, headerName);
        var total = NamedRow(workbook, sheet, totalName);
        if (header is null || total is null || total <= header)
        {
            result.Report.Add(new("totalRowMissing", sheet.Name));
            return null;
        }
        return (header.Value + 1, total.Value - 1);
    }

    private static int? NamedRow(XLWorkbook workbook, IXLWorksheet sheet, string name)
    {
        if (!workbook.DefinedNames.TryGetValue(name, out var definedName)) return null;
        var range = definedName.Ranges.FirstOrDefault();
        return range is not null && range.Worksheet.Name == sheet.Name ? range.RangeAddress.FirstAddress.RowNumber : null;
    }

    private static void ReadAreas(IXLWorksheet sheet, (int First, int Last) rows, InvestmentRateParseResult result)
    {
        for (var row = rows.First; row <= rows.Last; row++)
        {
            var name = Text(sheet, "B", row);
            var description = Text(sheet, "C", row);
            var actual = Number(sheet, "E", row, result);
            var factor = Number(sheet, "F", row, result);
            if (name is null && description is null && actual is null) continue;
            if (name is null) { result.Report.Add(new("descriptionRequired", sheet.Name, $"B{row}")); continue; }
            CheckLength(sheet, $"B{row}", name, MaxName, result);
            CheckLength(sheet, $"C{row}", description, MaxName, result);
            var note = Text(sheet, "H", row);
            CheckLength(sheet, $"H{row}", note, MaxText, result);
            if (factor is null or > MaxFactor)
            {
                result.Report.Add(new("factorRange", sheet.Name, $"F{row}"));
                continue;
            }
            var area = actual ?? 0m;
            result.Areas.Add(new(row, name, description, area, factor.Value, area * factor.Value, note));
        }
    }

    private static void ReadGroups(IXLWorksheet sheet, (int First, int Last) rows, InvestmentRateParseResult result)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var row = rows.First; row <= rows.Last; row++)
        {
            var name = Text(sheet, "B", row);
            var code = Text(sheet, "D", row);
            var material = Number(sheet, "E", row, result);
            var labour = Number(sheet, "F", row, result);
            var equipment = Number(sheet, "G", row, result);
            if (name is null && code is null && material is null && labour is null && equipment is null) continue;
            if (code is null) { result.Report.Add(new("codeRequired", sheet.Name, $"D{row}")); continue; }
            if (name is null) { result.Report.Add(new("descriptionRequired", sheet.Name, $"B{row}")); continue; }
            if (!codes.Add(code)) { result.Report.Add(new("duplicateCode", sheet.Name, $"D{row}")); continue; }
            CheckLength(sheet, $"D{row}", code, MaxCode, result);
            CheckLength(sheet, $"B{row}", name, MaxName, result);
            var description = Text(sheet, "C", row);
            CheckLength(sheet, $"C{row}", description, MaxName, result);
            var specification = Text(sheet, "K", row);
            CheckLength(sheet, $"K{row}", specification, MaxText, result);
            var rate = (material ?? 0m) + (labour ?? 0m) + (equipment ?? 0m);
            result.Groups.Add(new(row, code, name, description, material ?? 0m, labour ?? 0m, equipment ?? 0m, rate, specification));
        }
    }

    private static string? Text(IXLWorksheet sheet, string column, int row)
    {
        var value = sheet.Cell($"{column}{row}").Value;
        var text = value.IsBlank ? null : value.ToString(System.Globalization.CultureInfo.InvariantCulture).Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    // A blank cell is null; text, errors, negatives and huge numbers are reported against the cell.
    private static decimal? Number(IXLWorksheet sheet, string column, int row, InvestmentRateParseResult result)
    {
        var cell = $"{column}{row}";
        var value = sheet.Cell(cell).Value;
        if (value.IsBlank || value.IsText && string.IsNullOrWhiteSpace(value.GetText())) return null;
        if (value.IsError) { result.Report.Add(new("formulaError", sheet.Name, cell)); return null; }
        if (!value.IsNumber) { result.Report.Add(new("number", sheet.Name, cell)); return null; }
        var number = (decimal)value.GetNumber();
        if (number < 0) { result.Report.Add(new("negative", sheet.Name, cell)); return null; }
        if (number > MaxNumber) { result.Report.Add(new("maximum", sheet.Name, cell)); return null; }
        return number;
    }

    private static void CheckLength(IXLWorksheet sheet, string cell, string? value, int limit, InvestmentRateParseResult result)
    {
        if (value?.Length > limit) result.Report.Add(new("length", sheet.Name, cell, limit.ToString()));
    }
}
