using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <summary>
    /// Applies NICON's confirmed Vietnamese department name "Cung ứng" to
    /// existing translation rows. TranslationSeeder preserves existing rows,
    /// so seed changes alone would affect only new databases. Each update is
    /// guarded by the previous seed value to preserve administrator overrides.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261007093000_RenameProcurementToSupply")]
    public partial class RenameProcurementToSupply : Migration
    {
        private sealed record Rename(string Key, string PreviousValue, string Value);

        private static readonly Rename[] Renames =
        [
            new("nav.procurement", "Mua sắm", "Cung ứng"),
            new("nav.procurementControl", "Kiểm soát mua sắm", "Kiểm soát cung ứng"),
            new("procurement.title", "Kiểm soát mua sắm", "Kiểm soát cung ứng"),
            new("procurement.project.emptySelection", "Chọn một dự án để mở không gian kiểm soát mua sắm.", "Chọn một dự án để mở không gian kiểm soát cung ứng."),
            new("rbac.role.PROCUREMENT.label", "Mua hàng", "Cung ứng"),
            new("adminRbac.module.proc", "Mua sắm", "Cung ứng"),
        ];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var rename in Renames)
            {
                migrationBuilder.Sql($"""
                    UPDATE translations
                    SET Value = {Literal(rename.Value)}, UpdatedAt = SYSUTCDATETIME()
                    WHERE [Key] = {Literal(rename.Key)}
                      AND LanguageCode = N'vi'
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
                      AND LanguageCode = N'vi'
                      AND Value = {Literal(rename.Value)};
                    """);
            }
        }

        private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
    }
}
