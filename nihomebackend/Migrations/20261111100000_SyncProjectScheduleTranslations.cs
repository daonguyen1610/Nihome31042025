using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261111100000_SyncProjectScheduleTranslations")]
public partial class SyncProjectScheduleTranslations : Migration
{
    private sealed record Rename(string Key, string Language, string PreviousValue, string Value);
    private static readonly Rename[] Renames =
    [
        new("operationalProjects.schedule.approverCrm", "vi", "Trước khi ký hợp đồng với chủ đầu tư: Trưởng phòng Kinh doanh duyệt tiến độ.", "Trước khi ký hợp đồng với chủ đầu tư: Trưởng phòng CRM duyệt tiến độ."),
        new("operationalProjects.schedule.approverCrm", "en", "Before an upstream contract is signed: the CRM Sales Manager (SALES_MANAGER) is the approver.", "Before an upstream contract is signed: the CRM department head approves the schedule."),
        new("operationalProjects.schedule.approverCrm", "zh", "上游合同签署前：由 CRM 销售经理（SALES_MANAGER）审批。", "上游合同签署前：由 CRM 部门负责人审批进度。"),
        new("operationalProjects.schedule.approverCrm", "ja", "上流契約の署名前：CRM の営業マネージャー（SALES_MANAGER）が承認します。", "上流契約の署名前：CRM 部門長が工程を承認します。"),
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
