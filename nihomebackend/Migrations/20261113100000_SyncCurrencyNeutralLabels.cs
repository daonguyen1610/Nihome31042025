using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261113100000_SyncCurrencyNeutralLabels")]
public partial class SyncCurrencyNeutralLabels : Migration
{
    private sealed record Rename(string Key, string Language, string PreviousValue, string Value);
    private static readonly Rename[] Renames =
    [
        new("opportunities.field.estimatedValue", "vi", "Giá trị dự kiến (VND)", "Giá trị dự kiến"),
        new("opportunities.field.estimatedValue", "en", "Estimated value (VND)", "Estimated value"),
        new("opportunities.field.estimatedValue", "zh", "预估金额 (VND)", "预估金额"),
        new("opportunities.field.estimatedValue", "ja", "予想金額 (VND)", "予想金額"),
        new("quotes.field.unitPricePerSqm", "vi", "Đơn giá/m² (VND)", "Đơn giá/m²"),
        new("quotes.field.unitPricePerSqm", "en", "Unit price / m² (VND)", "Unit price / m²"),
        new("quotes.field.unitPricePerSqm", "zh", "单价/m² (VND)", "单价/m²"),
        new("quotes.field.unitPricePerSqm", "ja", "単価/m² (VND)", "単価/m²"),
        new("settings.display.currencyUsdHint", "vi", "Hai chữ số thập phân, ký hiệu $", "Số nguyên, ký hiệu $"),
        new("settings.display.currencyUsdHint", "en", "Two decimal places with the $ symbol", "Whole numbers with the $ symbol"),
        new("settings.display.currencyUsdHint", "zh", "两位小数，带 $ 符号", "整数，带 $ 符号"),
        new("settings.display.currencyUsdHint", "ja", "小数2桁と $ 記号", "整数と $ 記号"),
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var rename in Renames)
            migrationBuilder.Sql($"""
                UPDATE translations
                SET Value = {Literal(rename.Value)}, UpdatedAt = SYSUTCDATETIME()
                WHERE [Key] = {Literal(rename.Key)}
                  AND LanguageCode = {Literal(rename.Language)}
                  AND Value COLLATE Latin1_General_100_BIN2 = {Literal(rename.PreviousValue)};
                """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var rename in Renames)
            migrationBuilder.Sql($"""
                UPDATE translations
                SET Value = {Literal(rename.PreviousValue)}, UpdatedAt = SYSUTCDATETIME()
                WHERE [Key] = {Literal(rename.Key)}
                  AND LanguageCode = {Literal(rename.Language)}
                  AND Value COLLATE Latin1_General_100_BIN2 = {Literal(rename.Value)};
                """);
    }

    private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
}
