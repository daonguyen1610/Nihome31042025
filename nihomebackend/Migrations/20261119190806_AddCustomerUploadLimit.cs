using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerUploadLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerMaxFileSizeMb",
                table: "site_settings",
                type: "int",
                nullable: false,
                defaultValue: 20);
            // The old hint said "tối đa 20 MB"; its replacement is seeded as a new key and
            // the translation seeder never deletes, so the stale rows go here.
            migrationBuilder.Sql("DELETE FROM translations WHERE [Key] = N'customers.document.fileHint';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerMaxFileSizeMb",
                table: "site_settings");
            migrationBuilder.Sql("""
                INSERT INTO translations ([Key], LanguageCode, Value, Category, CreatedAt, UpdatedAt)
                SELECT v.[Key], v.LanguageCode, v.Value, N'customers', SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM (VALUES
                    (N'customers.document.fileHint', N'vi', N'PDF, Word, Excel hoặc hình ảnh · tối đa 20 MB'),
                    (N'customers.document.fileHint', N'en', N'PDF, Word, Excel, or image · maximum 20 MB'),
                    (N'customers.document.fileHint', N'zh', N'PDF、Word、Excel 或图片 · 最大 20 MB'),
                    (N'customers.document.fileHint', N'ja', N'PDF、Word、Excel、画像 · 最大 20 MB')
                ) AS v([Key], LanguageCode, Value)
                WHERE NOT EXISTS (SELECT 1 FROM translations t WHERE t.[Key] = v.[Key] AND t.LanguageCode = v.LanguageCode);
                """);
        }
    }
}
