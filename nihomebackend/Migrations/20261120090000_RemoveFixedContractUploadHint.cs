using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// contracts.documents.uploadHint said "up to 20 MB" although the contract limit is set in
/// Settings. Its replacement is seeded as a new key; the translation seeder never deletes,
/// so the old rows are removed here.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261120090000_RemoveFixedContractUploadHint")]
public partial class RemoveFixedContractUploadHint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DELETE FROM translations WHERE [Key] = N'contracts.documents.uploadHint';");

    // Restores the old label for a rolled-back frontend that still reads it.
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO translations ([Key], LanguageCode, Value, Category, CreatedAt, UpdatedAt)
        SELECT v.[Key], v.LanguageCode, v.Value, N'contracts', SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM (VALUES
            (N'contracts.documents.uploadHint', N'vi', N'Kéo thả hoặc chọn nhiều tệp cùng lúc; tối đa 20 MB mỗi tệp.'),
            (N'contracts.documents.uploadHint', N'en', N'Drag and drop or choose several files at once; up to 20 MB each.'),
            (N'contracts.documents.uploadHint', N'zh', N'可拖放或一次选择多个文件；每个最大 20 MB。'),
            (N'contracts.documents.uploadHint', N'ja', N'複数ファイルをまとめてドロップまたは選択できます（1ファイル最大20 MB）。')
        ) AS v([Key], LanguageCode, Value)
        WHERE NOT EXISTS (SELECT 1 FROM translations t WHERE t.[Key] = v.[Key] AND t.LanguageCode = v.LanguageCode);
        """);
}
