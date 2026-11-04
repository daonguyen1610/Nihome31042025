using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261104220000_RenameDesignContractsToDossiers")]
public sealed class RenameDesignContractsToDossiers : Migration
{
    private sealed record Rename(string Key, string Language, string PreviousValue, string Value);

    private static readonly Rename[] AdditionalRenames =
    [
        new("designProjects.team.validation.managerAlreadyAssigned", "vi", "Hợp Đồng Thiết Kế đã có Chủ Nhiệm Thiết Kế. Hãy sửa phân công hiện tại trước.", "Hồ Sơ Thiết Kế đã có Chủ Nhiệm Thiết Kế. Hãy sửa phân công hiện tại trước."),
        new("designProjects.team.validation.managerAlreadyAssigned", "en", "This design contract already has a Design Manager. Edit the current assignment first.", "This design dossier already has a Design Manager. Edit the current assignment first."),
        new("designProjects.team.validation.managerAlreadyAssigned", "zh", "该设计合同已有设计负责人。请先修改现有分配。", "该设计档案已有设计负责人。请先修改现有分配。"),
        new("designProjects.team.validation.managerAlreadyAssigned", "ja", "この設計契約には設計責任者がいます。現在の割り当てを先に編集してください。", "この設計図書には設計責任者がいます。現在の割り当てを先に編集してください。"),
        new("designProjects.team.ownerPickerHint", "vi", "Chỉ chọn thành viên đang hiệu lực trong đội ngũ Hợp Đồng Thiết Kế.", "Chỉ chọn thành viên đang hiệu lực trong đội ngũ Hồ Sơ Thiết Kế."),
        new("designProjects.team.ownerPickerHint", "en", "Choose an active member of this design contract team.", "Choose an active member of this design dossier team."),
        new("designProjects.team.ownerPickerHint", "zh", "请选择该设计合同团队中的有效成员。", "请选择该设计档案团队中的有效成员。"),
        new("designProjects.team.ownerPickerHint", "ja", "この設計契約チームの有効なメンバーを選択してください。", "この設計図書チームの有効なメンバーを選択してください。"),
        new("designProjects.team.validation.ownerActive", "vi", "Người phụ trách phải là thành viên đang hiệu lực trong đội ngũ Hợp Đồng Thiết Kế. Hãy thêm người tại tab Đội ngũ hoặc chọn thành viên khác.", "Người phụ trách phải là thành viên đang hiệu lực trong đội ngũ Hồ Sơ Thiết Kế. Hãy thêm người tại tab Đội ngũ hoặc chọn thành viên khác."),
        new("designProjects.team.validation.ownerActive", "en", "The owner must be an active member of this design contract team. Add them in Team or choose someone else.", "The owner must be an active member of this design dossier team. Add them in Team or choose someone else."),
        new("designProjects.team.validation.ownerActive", "zh", "负责人必须是该设计合同团队的有效成员。请在团队中添加该成员或选择其他人。", "负责人必须是该设计档案团队的有效成员。请在团队中添加该成员或选择其他人。"),
        new("designProjects.team.validation.ownerActive", "ja", "担当者はこの設計契約チームの有効なメンバーである必要があります。チームで追加するか、別の人を選択してください。", "担当者はこの設計図書チームの有効なメンバーである必要があります。チームで追加するか、別の人を選択してください。"),
    ];

    private static readonly Rename[] Renames = SyncDesignContractTranslations.Renames
        .Select(item => new Rename(item.Key, item.Language, item.Value, ToDossier(item.Value)))
        .Where(item => item.PreviousValue != item.Value)
        .Concat(AdditionalRenames)
        .ToArray();

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

    private static string ToDossier(string value) => value
        .Replace("Hợp Đồng Thiết Kế", "Hồ Sơ Thiết Kế", StringComparison.Ordinal)
        .Replace("Hợp đồng thiết kế", "Hồ sơ thiết kế", StringComparison.Ordinal)
        .Replace("hợp đồng thiết kế", "hồ sơ thiết kế", StringComparison.Ordinal)
        .Replace("Design contracts", "Design dossiers", StringComparison.Ordinal)
        .Replace("Design contract", "Design dossier", StringComparison.Ordinal)
        .Replace("design contracts", "design dossiers", StringComparison.Ordinal)
        .Replace("design contract", "design dossier", StringComparison.Ordinal)
        .Replace("设计合同", "设计档案", StringComparison.Ordinal)
        .Replace("設計契約", "設計図書", StringComparison.Ordinal);

    private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
}
