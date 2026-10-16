using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;

namespace NihomeBackend.Services;

public static class BoqConfigurationRules
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] Bindings = ["None", "Code", "Name", "Unit", "Quantity", "UnitPrice"];

    public static MaterialRateOperationException Error(string reason, string? context = null)
    {
        var error = new MaterialRateOperationException($"BOQ: {reason}{(context == null ? "" : $" ({context})")}", $"boqConfigured.error.{reason}");
        if (context != null) error.Data["context"] = context;
        return error;
    }

    public static BoqConfiguration? Read(string? json) => json == null ? null : JsonSerializer.Deserialize<BoqConfiguration>(json, Json);
    public static List<BoqEntryRow> ReadRows(string? json) => json == null ? [] : JsonSerializer.Deserialize<List<BoqEntryRow>>(json, Json) ?? [];
    public static string? Hash(string? json) => json == null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();

    public static string Validate(BoqConfiguration config)
    {
        if (config.Version != 1 || config.Sheets is null || config.Sheets.Count is < 1 or > 40 || config.SourceFile?.Length > 255)
            throw Error("configuration");
        var sheetKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sheet in config.Sheets)
        {
            if (sheet is null || !Key(sheet.Key, 20) || !sheetKeys.Add(sheet.Key) || sheet.SourceRegion?.Length > 300 ||
                sheet.Fields is null || sheet.Fields.Count is < 2 or > 40) throw Error("configuration");
            Names(sheet.NameVi, sheet.Name, sheet.NameZh, sheet.NameJa);
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var bindings = new HashSet<string>();
            foreach (var field in sheet.Fields)
            {
                if (field is null || !Key(field.Key, 40) || !keys.Add(field.Key) || !Bindings.Contains(field.Binding) ||
                    field.Type is not ("Text" or "Number") || field.MaxLength is < 1 or > 2000 ||
                    field.DecimalPlaces is < 0 or > 4) throw Error("configuration");
                Names(field.NameVi, field.Name, field.NameZh, field.NameJa);
                if (field.Binding == "None") continue;
                if (!bindings.Add(field.Binding) || !field.Required) throw Error("configuration");
                var numeric = field.Binding is "Quantity" or "UnitPrice";
                if (field.Type != (numeric ? "Number" : "Text") ||
                    field.Binding == "Code" && field.MaxLength > 38 ||
                    field.Binding == "Name" && field.MaxLength > 300 ||
                    field.Binding == "Unit" && field.MaxLength > 30 ||
                    field.Binding == "UnitPrice" && field.DecimalPlaces > 2) throw Error("configuration");
            }
            var required = sheet.IsPriced ? new[] { "Code", "Name", "Unit", "Quantity", "UnitPrice" } : ["Code", "Name"];
            if (required.Any(binding => !bindings.Contains(binding)) ||
                !sheet.IsPriced && bindings.Any(binding => binding is "UnitPrice" or "Quantity")) throw Error("configuration");
        }
        return JsonSerializer.Serialize(config, Json);
    }

    public static List<(BoqEntryRow Row, UpsertBoqMaterialRateLineRequest? Priced)> ValidateRows(
        BoqConfiguration config, IReadOnlyList<BoqEntryRow> rows)
    {
        if (rows.Count > 10000) throw Error("rowLimit");
        var result = new List<(BoqEntryRow, UpsertBoqMaterialRateLineRequest?)>();
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row is null) throw Error("wrongFields");
            var sheet = config.Sheets.SingleOrDefault(sheet => sheet.Key == row.SheetKey);
            if (sheet is null || row.Values is null || row.Values.Count > 40 ||
                row.Values.Keys.Any(key => sheet.Fields.All(field => field.Key != key))) throw Error("wrongFields", row.SheetKey);
            var values = new Dictionary<string, string>();
            foreach (var field in sheet.Fields)
            {
                var value = row.Values.GetValueOrDefault(field.Key)?.Trim() ?? "";
                if (field.Required && value.Length == 0) throw Error("required", $"{sheet.NameVi}/{field.NameVi}/#{result.Count + 1}");
                if (value.Length > field.MaxLength) throw Error("length", $"{sheet.NameVi}/{field.NameVi}/#{result.Count + 1}");
                if (value.Length == 0) continue;
                if (field.Type == "Number")
                {
                    // JSON/API numbers are invariant strings; Excel imports must contain numeric cells.
                    if (!Regex.IsMatch(value, @"^\d+(\.\d+)?$") ||
                        !decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) ||
                        number > 999999999999m || decimal.Round(number, field.DecimalPlaces) != number)
                        throw Error("number", $"{sheet.NameVi}/{field.NameVi}/#{result.Count + 1}");
                    value = number.ToString(CultureInfo.InvariantCulture);
                }
                values[field.Key] = value;
            }
            string Bound(string binding) => values.GetValueOrDefault(sheet.Fields.Single(field => field.Binding == binding).Key) ?? "";
            var code = Bound("Code");
            if (!Regex.IsMatch(code, @"^[A-Za-z0-9][A-Za-z0-9._-]{0,37}$")) throw Error("code", sheet.Key);
            var canonicalCode = $"{sheet.Key}.{code}";
            if (!codes.Add(canonicalCode)) throw Error("duplicate", canonicalCode);
            UpsertBoqMaterialRateLineRequest? priced = null;
            if (sheet.IsPriced)
            {
                var quantity = decimal.Parse(Bound("Quantity"), CultureInfo.InvariantCulture);
                if (quantity <= 0) throw Error("quantity", sheet.Key);
                priced = new()
                {
                    ItemCode = canonicalCode,
                    ItemName = Bound("Name"),
                    Unit = Bound("Unit"),
                    Quantity = quantity,
                    UnitPrice = decimal.Parse(Bound("UnitPrice"), CultureInfo.InvariantCulture)
                };
            }
            result.Add((new BoqEntryRow { SheetKey = sheet.Key, Values = values }, priced));
        }
        return result;
    }

    private static bool Key(string? value, int length) => value is not null && value.Length <= length && Regex.IsMatch(value, "^[a-z][a-z0-9_]*$");
    private static void Names(params string[] values)
    {
        if (values.Any(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length > 120)) throw Error("names");
    }
}
