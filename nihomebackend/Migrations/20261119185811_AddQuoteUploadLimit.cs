using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteUploadLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QuoteMaxFileSizeMb",
                table: "site_settings",
                type: "int",
                nullable: false,
                defaultValue: 20);
            // The old hint said "tối đa 20MB"; its replacement is seeded as a new key and
            // the translation seeder never deletes, so the stale rows go here.
            migrationBuilder.Sql("DELETE FROM translations WHERE [Key] = N'quotes.document.fileHint';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuoteMaxFileSizeMb",
                table: "site_settings");
            migrationBuilder.Sql("""
                INSERT INTO translations ([Key], LanguageCode, Value, Category, CreatedAt, UpdatedAt)
                SELECT v.[Key], v.LanguageCode, v.Value, N'quotes', SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM (VALUES
                    (N'quotes.document.fileHint', N'vi', N'PDF, Word, Excel hoặc ảnh; tối đa 20MB.'),
                    (N'quotes.document.fileHint', N'en', N'PDF, Word, Excel or image; maximum 20MB.'),
                    (N'quotes.document.fileHint', N'zh', N'PDF、Word、Excel 或图片；最大 20MB。'),
                    (N'quotes.document.fileHint', N'ja', N'PDF、Word、Excel、画像（最大20MB）。')
                ) AS v([Key], LanguageCode, Value)
                WHERE NOT EXISTS (SELECT 1 FROM translations t WHERE t.[Key] = v.[Key] AND t.LanguageCode = v.LanguageCode);
                """);
        }
    }
}
