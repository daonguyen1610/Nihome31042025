using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <summary>
    /// 14/11/2026: the CRM menu entry for investment-rate catalogs and the BOQ template library is
    /// renamed "Mẫu báo giá" (quote templates). TranslationSeeder only inserts missing keys, so the
    /// label is updated here, only while it still holds the earlier seed value, leaving
    /// administrator edits alone.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261114120000_SyncQuoteTemplatesNavLabel")]
    public partial class SyncQuoteTemplatesNavLabel : Migration
    {
        private sealed record Rename(string Key, string Language, string PreviousValue, string Value);

        private static readonly Rename[] Renames =
        [
            new("nav.materialRates", "en", "Material norms & rates", "Quote templates"),
            new("nav.materialRates", "ja", "材料基準・単価", "見積テンプレート"),
            new("nav.materialRates", "vi", "Định mức & đơn giá vật liệu", "Mẫu báo giá"),
            new("nav.materialRates", "zh", "材料定额与单价", "报价模板"),
        ];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var rename in Renames)
            {
                migrationBuilder.Sql($"""
                    UPDATE translations
                    SET Value = {Literal(rename.Value)}, UpdatedAt = SYSUTCDATETIME()
                    WHERE [Key] = {Literal(rename.Key)}
                      AND LanguageCode = {Literal(rename.Language)}
                      AND Value = {Literal(rename.PreviousValue)};
                    """);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var rename in Renames)
            {
                migrationBuilder.Sql($"""
                    UPDATE translations
                    SET Value = {Literal(rename.PreviousValue)}, UpdatedAt = SYSUTCDATETIME()
                    WHERE [Key] = {Literal(rename.Key)}
                      AND LanguageCode = {Literal(rename.Language)}
                      AND Value = {Literal(rename.Value)};
                    """);
            }
        }

        private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
    }
}
