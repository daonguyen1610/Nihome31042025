using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace NihomeBackend.Services;

public sealed record BoqWorkbookIssue(string Code, string? Sheet = null, string? Cell = null, string? Limit = null);

public sealed record BoqWorkbookLine(
    string ItemCode, string SheetName, int SourceRow, string? Stt, string? GroupName,
    string Description, string Unit, decimal Quantity, decimal? UnitPrice, decimal Amount);

public sealed record BoqWorkbookSheetSummary(
    string Name, int ItemCount, int SkippedCount, int MissingPriceCount, int MissingUnitCount, decimal Total);

public sealed class BoqWorkbookParseResult
{
    public const int MaxReportedIssues = 200;
    public List<BoqWorkbookIssue> Issues { get; } = [];
    public int IssueCount { get; private set; }
    public List<BoqWorkbookLine> Lines { get; } = [];
    public List<BoqWorkbookSheetSummary> Sheets { get; } = [];
    public string Sha256 { get; set; } = string.Empty;
    public bool IsValid => IssueCount == 0;

    public void Add(BoqWorkbookIssue issue)
    {
        IssueCount++;
        if (Issues.Count < MaxReportedIssues) Issues.Add(issue);
    }
}

public sealed class BoqWorkbookException(string code, BoqWorkbookParseResult? result = null, string? limit = null) : Exception($"BOQ workbook: {code}")
{
    public string Code { get; } = code;
    public BoqWorkbookParseResult? Result { get; } = result;
    public string? Limit { get; } = limit;
}

public sealed record BoqOutlineRow(int Row, string? Stt, string Description, string? Unit, string Kind);

public sealed record BoqOutlineSheet(string Name, bool IsData, int ItemCount, IReadOnlyList<BoqOutlineRow> Rows);

public static partial class ProjectBoqWorkbookParser
{
    public const long MaxFileBytes = 20 * 1024 * 1024;
    private const int MaxZipEntries = 500, MaxDescription = 500, MaxUnit = 50, MaxStt = 30;
    private const long MaxExpandedBytes = 200L * 1024 * 1024;
    private const decimal MaxNumber = 999999999999m;

    public static BoqWorkbookParseResult Parse(byte[] content, BoqTemplate template)
    {
        EnsureSafe(content);
        var result = new BoqWorkbookParseResult { Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant() };
        XLWorkbook book;
        try { book = new XLWorkbook(new MemoryStream(content)); }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or FormatException or IOException or ObjectDisposedException)
        { throw new BoqWorkbookException("file"); }
        using (book)
        {
            var expected = template.Sheets.Select(sheet => sheet.Name).Concat(template.IgnoredSheets).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var extra in book.Worksheets.Where(sheet => !expected.Contains(sheet.Name)))
                result.Add(new("unknownSheet", extra.Name));
            for (var index = 0; index < template.Sheets.Count; index++)
                ReadSheet(book, template.Sheets[index], index + 1, template.AmountTolerance, result);
        }
        if (result.IsValid && result.Lines.Count == 0) result.Add(new("noRows"));
        return result;
    }

    private static void ReadSheet(XLWorkbook book, BoqTemplateSheet profile, int sheetNumber, decimal tolerance, BoqWorkbookParseResult result)
    {
        if (!book.TryGetWorksheet(profile.Name, out var sheet)) { result.Add(new("sheetMissing", profile.Name)); return; }
        var badHeader = profile.Headers.FirstOrDefault(header => Normalize(Text(sheet.Cell(header.Key))) != Normalize(header.Value));
        if (badHeader.Key != null) { result.Add(new("headerMismatch", profile.Name, badHeader.Key)); return; }

        var last = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 0;
        string? group = null;
        int items = 0, skipped = 0, missingPrices = 0, missingUnits = 0;
        decimal total = 0;
        var totalCell = profile.TotalCell;
        for (var row = profile.FirstRow; row <= last; row++)
        {
            var stt = Text(sheet.Cell($"{profile.SttColumn}{row}"));
            var description = Description(sheet, profile, row);
            var normalized = Normalize(description);
            if (IsTotal(profile, normalized, stt))
            {
                totalCell = $"{profile.AmountColumn}{row}";
                break;
            }
            var unit = Text(sheet.Cell($"{profile.UnitColumn}{row}"));
            var isItem = IsItem(sheet, profile, row, stt, unit);
            // Measurement quantities can be negative (openings deducted from a wall), so they are not read.
            if (!isItem && IsMeasurement(sheet, profile, row, last))
            {
                if (HasPrice(sheet, profile, row)) result.Add(new("sttRequired", profile.Name, $"{profile.SttColumn}{row}"));
                continue;
            }
            // Section rows repeat the column labels, so text there is not a data error.
            var quantity = Number(sheet, $"{profile.QuantityColumn}{row}", result, allowText: !isItem);
            if (!isItem)
            {
                // Without item numbers a heading may carry a count, such as 41 manholes; it has no price.
                if (quantity is > 0 && !profile.ItemsWithoutStt) result.Add(new("sttRequired", profile.Name, $"{profile.SttColumn}{row}"));
                else if (description.Length > 0 && !IsSubtotal(profile, normalized)) group = description;
                continue;
            }
            var unitPrice = UnitPrice(sheet, profile, row, result);
            var price = unitPrice.HasValue ? decimal.Round(unitPrice.Value, 4, MidpointRounding.AwayFromZero) : (decimal?)null;
            if (quantity is null or 0) { skipped++; continue; }
            var before = result.IssueCount;
            if (description.Length == 0) result.Add(new("descriptionRequired", profile.Name, $"{profile.DescriptionColumn}{row}"));
            if (description.Length > MaxDescription)
                result.Add(new("length", profile.Name, $"{profile.DescriptionColumn}{row}", MaxDescription.ToString(CultureInfo.InvariantCulture)));
            if (unit.Length > MaxUnit) result.Add(new("length", profile.Name, $"{profile.UnitColumn}{row}", MaxUnit.ToString(CultureInfo.InvariantCulture)));
            var amount = decimal.Round(quantity.Value * (price ?? 0), 4, MidpointRounding.AwayFromZero);
            if (price.HasValue)
            {
                var amountCell = $"{profile.AmountColumn}{row}";
                // Accounting layouts write "-" for a zero amount.
                var sourceAmount = Text(sheet.Cell(amountCell)) == "-" ? 0 : Number(sheet, amountCell, result);
                if (sourceAmount is null && result.IssueCount == before) result.Add(new("amountRequired", profile.Name, $"{profile.AmountColumn}{row}"));
                // Compare with the cells' full values: rounding the quantity to the stored 6 decimals can
                // move quantity × price past the tolerance (126.40802308 t × 20,000,000 per t).
                else if (sourceAmount.HasValue && Math.Abs(sourceAmount.Value -
                        Stored(sheet, $"{profile.QuantityColumn}{row}") * StoredPrice(sheet, profile, row)) > tolerance)
                    result.Add(new("amountMismatch", profile.Name, $"{profile.AmountColumn}{row}"));
                // Keep the workbook's figure (GNF rounds each amount) so the lines add up to its total.
                if (sourceAmount.HasValue) amount = decimal.Round(sourceAmount.Value, 4, MidpointRounding.AwayFromZero);
            }
            else missingPrices++;
            if (unit.Length == 0) missingUnits++;
            items++;
            total += amount;
            if (result.IssueCount != before) continue;
            result.Lines.Add(new($"S{sheetNumber:00}-R{row:0000}", profile.Name, row, Truncate(stt, MaxStt), group, description, unit, quantity.Value, price, amount));
        }
        if (totalCell == null) result.Add(new("totalRowMissing", profile.Name));
        else
        {
            var sourceTotal = Number(sheet, totalCell, result);
            var expected = profile.TotalDecimals is { } digits ? ExcelRound(total, digits) : total;
            if (sourceTotal.HasValue && Math.Abs(sourceTotal.Value - expected) > tolerance)
                result.Add(new("totalMismatch", profile.Name, totalCell));
        }
        result.Sheets.Add(new(profile.Name, items, skipped, missingPrices, missingUnits, total));
    }

    // The structure of every sheet in a template, read with the same row rules as Parse:
    // item rule (whole-number STT, or the profile's unnumbered rule), subtotal prefix = subtotal,
    // other text = group, total label ends the sheet.
    // Sheets outside the profile (cover, summary) are listed without rows.
    public static IReadOnlyList<BoqOutlineSheet> Outline(byte[] content, BoqTemplate template)
    {
        using var book = new XLWorkbook(new MemoryStream(content));
        var profiles = template.Sheets.ToDictionary(sheet => sheet.Name, StringComparer.OrdinalIgnoreCase);
        return book.Worksheets.Select(sheet => profiles.TryGetValue(sheet.Name, out var profile)
            ? OutlineSheet(sheet, profile)
            : new BoqOutlineSheet(sheet.Name, false, 0, [])).ToList();
    }

    private static BoqOutlineSheet OutlineSheet(IXLWorksheet sheet, BoqTemplateSheet profile)
    {
        var rows = new List<BoqOutlineRow>();
        var headerStt = Normalize(profile.Headers.FirstOrDefault(header => header.Key.StartsWith(profile.SttColumn, StringComparison.OrdinalIgnoreCase)).Value ?? "");
        var last = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 0;
        for (var row = profile.FirstRow; row <= last; row++)
        {
            var stt = Text(sheet.Cell($"{profile.SttColumn}{row}"));
            var description = Description(sheet, profile, row);
            var normalized = Normalize(description);
            if (IsTotal(profile, normalized, stt))
            {
                rows.Add(new(row, null, description.Length > 0 ? description : stt, null, "total"));
                break;
            }
            // Repeated column headings between sections are layout, not content.
            if (description.Length == 0 || headerStt.Length > 0 && Normalize(stt) == headerStt) continue;
            var unit = Text(sheet.Cell($"{profile.UnitColumn}{row}"));
            var isItem = IsItem(sheet, profile, row, stt, unit);
            if (!isItem && IsMeasurement(sheet, profile, row, last)) continue;
            var kind = isItem ? "item" : IsSubtotal(profile, normalized) ? "subtotal" : "group";
            rows.Add(new(row, stt.Length == 0 ? null : stt, description, kind == "item" && unit.Length > 0 ? unit : null, kind));
        }
        return new BoqOutlineSheet(sheet.Name, true, rows.Count(row => row.Kind == "item"), rows);
    }

    // Reads a numeric cell through Excel's stored result; never recalculates the workbook.
    private static decimal? Number(IXLWorksheet sheet, string address, BoqWorkbookParseResult result, bool allowText = false)
    {
        var cell = sheet.Cell(address);
        void Issue(string code) => result.Add(new(code, sheet.Name, address));
        if (cell.HasFormula && cell.CachedValue.IsBlank) { Issue("formulaNotCalculated"); return null; }
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;
        if (value.IsBlank) return null;
        if (value.IsError) { Issue("formulaError"); return null; }
        if (value.IsText)
        {
            // A formula may return "" for a row that is not filled in yet.
            if (!allowText && value.GetText().Trim().Length > 0) Issue("number");
            return null;
        }
        if (!value.IsNumber) { Issue("number"); return null; }
        var number = value.GetNumber();
        if (number < 0) { Issue("negative"); return null; }
        if (number > (double)MaxNumber) { Issue("maximum"); return null; }
        return decimal.Round((decimal)number, 6, MidpointRounding.AwayFromZero);
    }

    private static bool IsItem(IXLWorksheet sheet, BoqTemplateSheet profile, int row, string stt, string unit) =>
        !profile.ItemsWithoutStt ? IsWholeNumber(stt)
            : (HasPrice(sheet, profile, row) || Filled(sheet.Cell($"{profile.AmountColumn}{row}")))
              && (unit.Length > 0 || Filled(sheet.Cell($"{profile.QuantityColumn}{row}")));

    private static string Description(IXLWorksheet sheet, BoqTemplateSheet profile, int row)
    {
        var description = Text(sheet.Cell($"{profile.DescriptionColumn}{row}"));
        if (profile.DescriptionFirstLine) description = description.Split('\n')[0].Trim();
        var spec = profile.SpecColumn is { } column ? Text(sheet.Cell($"{column}{row}")) : "";
        return spec.Length == 0 ? description : $"{description} {spec}".Trim();
    }

    // Sum of the unit-price columns; blank when none is filled in.
    private static decimal? UnitPrice(IXLWorksheet sheet, BoqTemplateSheet profile, int row, BoqWorkbookParseResult result)
    {
        decimal? total = null;
        foreach (var column in PriceColumns(profile))
            if (Number(sheet, $"{column}{row}", result) is { } price) total = (total ?? 0) + price;
        return total;
    }

    private static IEnumerable<string> PriceColumns(BoqTemplateSheet profile) => profile.ExtraPriceColumns.Prepend(profile.UnitPriceColumn);

    private static decimal StoredPrice(IXLWorksheet sheet, BoqTemplateSheet profile, int row) =>
        PriceColumns(profile).Sum(column => Stored(sheet, $"{column}{row}"));

    // An unnumbered row with a measured value, or a label among such rows ("Bể XLNT", "Trừ ô cửa",
    // "Nhà Văn Phòng 02" above "Tầng 1", or a "Tầng 3" left without sizes at the end of a block).
    private static bool IsMeasurement(IXLWorksheet sheet, BoqTemplateSheet profile, int row, int last)
    {
        if (!profile.MeasurementRows) return false;
        var column = profile.MeasurementColumn ?? profile.QuantityColumn;
        bool Measured(int r) => Filled(sheet.Cell($"{column}{r}"));
        string Stt(int r) => Text(sheet.Cell($"{profile.SttColumn}{r}"));
        string Unit(int r) => Text(sheet.Cell($"{profile.UnitColumn}{r}"));
        bool Empty(int r) => !Measured(r) && Text(sheet.Cell($"{profile.DescriptionColumn}{r}")).Length == 0;
        bool Label(int r) => !Measured(r) && Stt(r).Length == 0 && Unit(r).Length == 0;
        if (Measured(row)) return true;
        if (Stt(row).Length == 0)
        {
            var previous = row - 1;
            while (previous >= profile.FirstRow && Empty(previous)) previous--;
            if (previous >= profile.FirstRow && Measured(previous) && !IsWholeNumber(Stt(previous)) && Unit(previous).Length == 0) return true;
        }
        for (var next = row + 1; next <= last; next++)
        {
            if (Empty(next) || Label(next)) continue;
            return Measured(next) && !IsWholeNumber(Stt(next));
        }
        return false;
    }

    private static bool HasPrice(IXLWorksheet sheet, BoqTemplateSheet profile, int row) =>
        PriceColumns(profile).Any(column => Filled(sheet.Cell($"{column}{row}")));

    private static bool Filled(IXLCell cell) => cell.HasFormula || !cell.Value.IsBlank;
    // Some sheets write the total label across the STT column (ME base "TỔNG GIÁ TRỊ TRƯỚC THUẾ").
    private static bool IsTotal(BoqTemplateSheet profile, string normalized, string stt) =>
        profile.TotalLabel.Length > 0 && (normalized == Normalize(profile.TotalLabel) || Normalize(stt) == Normalize(profile.TotalLabel));
    private static bool IsSubtotal(BoqTemplateSheet profile, string normalized) =>
        profile.SubtotalPrefix.Length > 0 && normalized.StartsWith(Normalize(profile.SubtotalPrefix), StringComparison.Ordinal);

    // Excel's ROUND, which also takes negative digits (-5 = nearest 100,000).
    private static decimal ExcelRound(decimal value, int digits)
    {
        if (digits >= 0) return decimal.Round(value, digits, MidpointRounding.AwayFromZero);
        var factor = (decimal)Math.Pow(10, -digits);
        return decimal.Round(value / factor, 0, MidpointRounding.AwayFromZero) * factor;
    }

    // The unrounded number in a cell already read by Number; a blank second price column counts as 0.
    private static decimal Stored(IXLWorksheet sheet, string address)
    {
        var cell = sheet.Cell(address);
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;
        return value.IsNumber ? (decimal)value.GetNumber() : 0;
    }

    private static bool IsWholeNumber(string text) => text.Length > 0 && text.All(char.IsDigit);
    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];

    private static string Text(IXLCell cell)
    {
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;
        return value.IsNumber ? value.GetNumber().ToString(CultureInfo.InvariantCulture)
            : value.IsText ? value.GetText().Trim() : string.Empty;
    }

    private static string Normalize(string text) => Spaces().Replace(text, "").ToUpperInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    private static void EnsureSafe(byte[] content)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(content));
            if (zip.Entries.Count > MaxZipEntries || zip.Entries.Sum(entry => entry.Length) > MaxExpandedBytes)
                throw new BoqWorkbookException("file");
            if (zip.Entries.Any(entry => entry.FullName.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase) ||
                    entry.FullName.Equals("xl/connections.xml", StringComparison.OrdinalIgnoreCase) ||
                    entry.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
                throw new BoqWorkbookException("unsafeFile");
        }
        catch (InvalidDataException) { throw new BoqWorkbookException("file"); }
    }
}
