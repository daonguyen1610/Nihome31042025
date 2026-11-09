using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261109130000_SyncCmScheduleTranslations")]
public partial class SyncCmScheduleTranslations : Migration
{
    private sealed record Rename(string Key, string Language, string PreviousValue, string Value);
    private static readonly Rename[] Renames =
    [
        new("cmSchedule.method", "vi", "Thực tế lấy từ % công việc BCH. Chỉ công việc cuối WBS được tổng hợp theo trọng số; mặc định theo số ngày lịch. Kế hoạch tuyến tính theo ngày lịch, không phải dự báo chi phí.", "Mỗi công việc lá WBS có tỷ trọng scope do CM đề xuất, tổng bằng 100% và PM duyệt. Có thể dựa trên phạm vi/khối lượng và độ phức tạp; không bắt buộc có BOQ. Kế hoạch dùng lịch làm việc lưu cùng baseline. BCH phải xác nhận actual; chưa báo cáo không được tính là 0%."),
        new("cmSchedule.method", "en", "Actuals come from site task completion. Only WBS leaves contribute, with weights initially based on calendar duration. The plan is linear by calendar day, not a cost forecast.", "Each WBS leaf has a CM-proposed scope share totaling 100%, approved by the PM. Base it on scope, quantity, or complexity; a BOQ is not required. Planned progress uses the calendar frozen with the baseline. BCH must report actuals; unreported values are not treated as 0%."),
        new("cmSchedule.method", "zh", "实际进度来自现场任务完成率。仅汇总WBS末级任务，默认权重为日历工期。计划按日历日线性计算，不代表成本预测。", "每个WBS末级任务由CM分配范围权重，合计100%，并由PM审批。可依据范围、工程量或复杂度；不要求必须有BOQ。计划按基准中冻结的工作日历计算。BCH必须报告实际进度；未报告值不会按0%处理。"),
        new("cmSchedule.method", "ja", "実績は現場作業の完了率です。WBS末端作業のみ重み付け集計し、初期値は暦日工期です。計画は暦日で線形計算し、費用予測ではありません。", "各WBS末端作業にCMがスコープ比率を割り当て、合計を100%としてPMが承認します。範囲・数量・複雑さを基準とし、BOQは必須ではありません。計画は基準に固定した稼働カレンダーで計算します。BCHが実績を報告し、未報告を0%として扱いません。"),
        new("cmSchedule.weight", "vi", "Trọng số", "Tỷ trọng scope (%)"),
        new("cmSchedule.weight", "en", "Weight", "Scope share (%)"),
        new("cmSchedule.weight", "zh", "权重", "范围权重（%）"),
        new("cmSchedule.weight", "ja", "重み", "スコープ比率（%）"),
        new("cmSchedule.periodDate", "vi", "Ngày thuộc kỳ cần xem", "Xem đến ngày"),
        new("cmSchedule.periodDate", "en", "Date within the period", "Through date"),
        new("cmSchedule.periodDate", "zh", "周期内日期", "截至日期"),
        new("cmSchedule.periodDate", "ja", "期間内の日付", "対象日"),
        new("cmSchedule.period.all", "vi", "Toàn bộ", "Ngày"),
        new("cmSchedule.period.all", "en", "All dates", "Daily"),
        new("cmSchedule.period.all", "zh", "全部日期", "按日"),
        new("cmSchedule.period.all", "ja", "全期間", "日次"),
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
