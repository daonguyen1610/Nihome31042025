using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectDocumentUploadLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectDocumentMaxFileSizeMb",
                table: "site_settings",
                type: "int",
                nullable: false,
                defaultValue: 100);
            // The old message said "100 MiB"; its replacement names the Settings limit and is
            // seeded as a new key. The translation seeder never deletes, so the stale rows go here.
            migrationBuilder.Sql("DELETE FROM translations WHERE [Key] = N'operationalProjects.documents.validation.size';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectDocumentMaxFileSizeMb",
                table: "site_settings");
            migrationBuilder.Sql("""
                INSERT INTO translations ([Key], LanguageCode, Value, Category, CreatedAt, UpdatedAt)
                SELECT v.[Key], v.LanguageCode, v.Value, N'operationalProjects', SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM (VALUES
                    (N'operationalProjects.documents.validation.size', N'vi', N'Tệp dự án không được vượt quá 100 MiB; hãy chọn tệp nhỏ hơn.'),
                    (N'operationalProjects.documents.validation.size', N'en', N'Project files cannot exceed 100 MiB; select a smaller file.'),
                    (N'operationalProjects.documents.validation.size', N'zh', N'项目文件不得超过 100 MiB；请选择更小的文件。'),
                    (N'operationalProjects.documents.validation.size', N'ja', N'プロジェクトファイルは100 MiB以下にしてください。')
                ) AS v([Key], LanguageCode, Value)
                WHERE NOT EXISTS (SELECT 1 FROM translations t WHERE t.[Key] = v.[Key] AND t.LanguageCode = v.LanguageCode);
                """);
        }
    }
}
