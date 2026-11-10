using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261110100000_SyncConstructionGuidanceTranslations")]
public partial class SyncConstructionGuidanceTranslations : Migration
{
    private sealed record Rename(string Key, string Language, string PreviousValue, string Value);
    private static readonly Rename[] Renames =
    [
        new("hse.search", "vi", "Tìm theo mã, vị trí, loại hoặc mô tả", "Tìm kiếm"),
        new("hse.search", "en", "Search code, location, category, or description", "Search"),
        new("hse.search", "zh", "搜索编号、位置、类别或描述", "搜索"),
        new("hse.search", "ja", "コード、場所、区分、説明を検索", "検索"),
        new("hse.hint.location", "vi", "Khu vực cụ thể để người khắc phục tìm đúng chỗ. Ví dụ: Tầng 3 – khu vực thang máy.", "Khu vực cụ thể để người khắc phục tìm đúng chỗ."),
        new("hse.hint.location", "en", "A specific area so the fix can be found. e.g. Floor 3 – lift lobby.", "A specific area so the fix can be found."),
        new("hse.hint.location", "zh", "便于整改人员找到的具体位置。例如：3层—电梯区域。", "便于整改人员找到的具体位置。"),
        new("hse.hint.location", "ja", "是正担当者が特定できる具体的な場所。例：3階・エレベーター付近。", "是正担当者が特定できる具体的な場所。"),
        new("hse.hint.category", "vi", "Nhóm vi phạm ngắn gọn. Ví dụ: Không đội mũ bảo hộ, Thiếu lan can an toàn.", "Nhóm vi phạm ngắn gọn để lọc và thống kê."),
        new("hse.hint.category", "en", "A short violation group. e.g. No hard hat, Missing guardrail.", "A short violation group, used for filtering and reporting."),
        new("hse.hint.category", "zh", "简短的违规类别。例如：未戴安全帽、缺少护栏。", "简短的违规类别，便于筛选和统计。"),
        new("hse.hint.category", "ja", "短い違反区分。例：ヘルメット未着用、手すり未設置。", "絞り込みと集計に使う短い違反区分。"),
        new("nav.constructionBchOperations", "vi", "Ban chỉ huy công trường – Vận hành", "Vận hành công trường"),
        new("nav.constructionBchOperations", "en", "Site Command Board – Operations", "Site operations"),
        new("nav.constructionBchOperations", "zh", "现场指挥部 – 运营", "现场运营"),
        new("nav.constructionBchOperations", "ja", "現場指揮本部 – 運営", "現場運営"),
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
