using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkTaskUploadLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkTaskMaxFileSizeMb",
                table: "site_settings",
                type: "int",
                nullable: false,
                defaultValue: 20);
            // The old hint and validation text said "20 MB"; its replacement is seeded as a new key and
            // the translation seeder never deletes, so the stale rows go here.
            migrationBuilder.Sql("DELETE FROM translations WHERE [Key] IN (N'workPackages.attachments.help', N'workPackages.attachments.validation');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkTaskMaxFileSizeMb",
                table: "site_settings");
            migrationBuilder.Sql("""
                INSERT INTO translations ([Key], LanguageCode, Value, Category, CreatedAt, UpdatedAt)
                SELECT v.[Key], v.LanguageCode, v.Value, N'work-packages', SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM (VALUES
                    (N'workPackages.attachments.help', N'vi', N'Tối đa 10 tệp mỗi lần, 20 MB/tệp. Hỗ trợ PDF, Word, Excel và ảnh JPG/PNG.'),
                    (N'workPackages.attachments.help', N'en', N'Up to 10 files at once, 20 MB each. PDF, Word, Excel, JPG and PNG are supported.'),
                    (N'workPackages.attachments.help', N'zh', N'每次最多 10 个文件，每个不超过 20 MB。支持 PDF、Word、Excel、JPG 和 PNG。'),
                    (N'workPackages.attachments.help', N'ja', N'一度に最大10ファイル、各20 MBまで。PDF、Word、Excel、JPG、PNGに対応。'),
                    (N'workPackages.attachments.validation', N'vi', N'Tệp đính kèm: chọn 1–10 tệp PDF, Word, Excel hoặc JPG/PNG; mỗi tệp không quá 20 MB, tên không quá 255 ký tự.'),
                    (N'workPackages.attachments.validation', N'en', N'Attachments: choose 1–10 PDF, Word, Excel, JPG or PNG files; each must be at most 20 MB with a name of at most 255 characters.'),
                    (N'workPackages.attachments.validation', N'zh', N'附件：请选择 1–10 个 PDF、Word、Excel、JPG 或 PNG 文件；每个不超过 20 MB，文件名不超过 255 个字符。'),
                    (N'workPackages.attachments.validation', N'ja', N'添付ファイル: PDF、Word、Excel、JPG、PNGを1～10個選択してください。各20 MB以下、名前は255文字以下です。')
                ) AS v([Key], LanguageCode, Value)
                WHERE NOT EXISTS (SELECT 1 FROM translations t WHERE t.[Key] = v.[Key] AND t.LanguageCode = v.LanguageCode);
                """);
        }
    }
}
