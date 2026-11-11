using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261112100000_SyncScheduleCopyTerminology")]
public partial class SyncScheduleCopyTerminology : Migration
{
    private sealed record Rename(string Key, string Language, string PreviousValue, string Value);
    private static readonly Rename[] Renames =
    [
        new("operationalProjects.schedule.validation.atLeastOneItem", "vi", "Tiến độ cần có ít nhất một mốc.", "Tiến độ cần có ít nhất một hạng mục."),
        new("operationalProjects.schedule.validation.atLeastOneItem", "en", "Add at least one WBS milestone to the schedule.", "Add at least one WBS item to the schedule."),
        new("operationalProjects.schedule.validation.atLeastOneItem", "zh", "进度计划至少需要一个 WBS 里程碑。", "进度计划至少需要一个 WBS 工作项。"),
        new("operationalProjects.schedule.validation.atLeastOneItem", "ja", "工程には WBS マイルストーンを1つ以上追加してください。", "工程には WBS 項目を1つ以上追加してください。"),
        new("operationalProjects.schedule.validation.tooManyItems", "vi", "Tiến độ không được có quá 500 mốc.", "Tiến độ không được có quá 500 hạng mục."),
        new("operationalProjects.schedule.validation.tooManyItems", "en", "The schedule can contain at most 500 WBS milestones.", "The schedule can contain at most 500 WBS items."),
        new("operationalProjects.schedule.validation.tooManyItems", "zh", "进度计划最多可包含 500 个 WBS 里程碑。", "进度计划最多可包含 500 个 WBS 工作项。"),
        new("operationalProjects.schedule.validation.tooManyItems", "ja", "工程に登録できる WBS マイルストーンは最大500件です。", "工程に登録できる WBS 項目は最大500件です。"),
        new("operationalProjects.schedule.validation.wbsRequired", "vi", "Mốc {index}: hãy nhập mã mốc.", "Hạng mục {index}: hãy nhập mã WBS."),
        new("operationalProjects.schedule.validation.wbsRequired", "en", "Milestone {index}: enter a WBS code.", "Item {index}: enter a WBS code."),
        new("operationalProjects.schedule.validation.wbsRequired", "zh", "里程碑 {index}：请输入 WBS 编码。", "工作项 {index}：请输入 WBS 编码。"),
        new("operationalProjects.schedule.validation.wbsRequired", "ja", "マイルストーン {index}：WBS コードを入力してください。", "項目 {index}：WBS コードを入力してください。"),
        new("operationalProjects.schedule.validation.wbsTooLong", "vi", "Mốc {index}: mã mốc tối đa 80 ký tự.", "Hạng mục {index}: mã WBS tối đa 80 ký tự."),
        new("operationalProjects.schedule.validation.wbsTooLong", "en", "Milestone {index}: the WBS code must be 80 characters or fewer.", "Item {index}: the WBS code must be 80 characters or fewer."),
        new("operationalProjects.schedule.validation.wbsTooLong", "zh", "里程碑 {index}：WBS 编码最多 80 个字符。", "工作项 {index}：WBS 编码最多 80 个字符。"),
        new("operationalProjects.schedule.validation.wbsTooLong", "ja", "マイルストーン {index}：WBS コードは80文字以内で入力してください。", "項目 {index}：WBS コードは80文字以内で入力してください。"),
        new("operationalProjects.schedule.validation.wbsDuplicate", "vi", "Mã mốc “{code}” đang bị trùng. Hãy dùng mã khác.", "Mã WBS “{code}” đang bị trùng. Hãy dùng mã khác."),
        new("operationalProjects.schedule.validation.titleRequired", "vi", "Mốc {index}: hãy nhập tên mốc / hạng mục.", "Hạng mục {index}: hãy nhập tên hạng mục."),
        new("operationalProjects.schedule.validation.titleRequired", "en", "Milestone {index}: enter a milestone or work item name.", "Item {index}: enter a name."),
        new("operationalProjects.schedule.validation.titleRequired", "zh", "里程碑 {index}：请输入里程碑或工作项名称。", "工作项 {index}：请输入名称。"),
        new("operationalProjects.schedule.validation.titleRequired", "ja", "マイルストーン {index}：名称を入力してください。", "項目 {index}：名称を入力してください。"),
        new("operationalProjects.schedule.validation.titleTooLong", "vi", "Mốc {index}: tên tối đa 300 ký tự.", "Hạng mục {index}: tên tối đa 300 ký tự."),
        new("operationalProjects.schedule.validation.titleTooLong", "en", "Milestone {index}: the name must be 300 characters or fewer.", "Item {index}: the name must be 300 characters or fewer."),
        new("operationalProjects.schedule.validation.titleTooLong", "zh", "里程碑 {index}：名称最多 300 个字符。", "工作项 {index}：名称最多 300 个字符。"),
        new("operationalProjects.schedule.validation.titleTooLong", "ja", "マイルストーン {index}：名称は300文字以内で入力してください。", "項目 {index}：名称は300文字以内で入力してください。"),
        new("operationalProjects.schedule.validation.startRequired", "vi", "Mốc {index}: hãy chọn ngày bắt đầu.", "Hạng mục {index}: hãy chọn ngày bắt đầu."),
        new("operationalProjects.schedule.validation.startRequired", "en", "Milestone {index}: choose a start date.", "Item {index}: choose a start date."),
        new("operationalProjects.schedule.validation.startRequired", "zh", "里程碑 {index}：请选择开始日期。", "工作项 {index}：请选择开始日期。"),
        new("operationalProjects.schedule.validation.startRequired", "ja", "マイルストーン {index}：開始日を選択してください。", "項目 {index}：開始日を選択してください。"),
        new("operationalProjects.schedule.validation.endRequired", "vi", "Mốc {index}: hãy chọn ngày kết thúc.", "Hạng mục {index}: hãy chọn ngày kết thúc."),
        new("operationalProjects.schedule.validation.endRequired", "en", "Milestone {index}: choose an end date.", "Item {index}: choose an end date."),
        new("operationalProjects.schedule.validation.endRequired", "zh", "里程碑 {index}：请选择结束日期。", "工作项 {index}：请选择结束日期。"),
        new("operationalProjects.schedule.validation.endRequired", "ja", "マイルストーン {index}：終了日を選択してください。", "項目 {index}：終了日を選択してください。"),
        new("operationalProjects.schedule.validation.dateOrder", "vi", "Mốc {index}: ngày kết thúc phải sau hoặc bằng ngày bắt đầu.", "Hạng mục {index}: ngày kết thúc phải sau hoặc bằng ngày bắt đầu."),
        new("operationalProjects.schedule.validation.dateOrder", "en", "Milestone {index}: the end date must be on or after the start date.", "Item {index}: the end date must be on or after the start date."),
        new("operationalProjects.schedule.validation.dateOrder", "zh", "里程碑 {index}：结束日期必须晚于或等于开始日期。", "工作项 {index}：结束日期必须晚于或等于开始日期。"),
        new("operationalProjects.schedule.validation.dateOrder", "ja", "マイルストーン {index}：終了日は開始日以降にしてください。", "項目 {index}：終了日は開始日以降にしてください。"),
        new("operationalProjects.schedule.validation.parentInvalid", "vi", "Mốc {index}: hãy chọn mốc cha trong cùng phiên bản tiến độ.", "Hạng mục {index}: hãy chọn hạng mục cha trong cùng phiên bản tiến độ."),
        new("operationalProjects.schedule.validation.parentInvalid", "en", "Milestone {index}: choose a parent milestone in this revision.", "Item {index}: choose a parent item in this revision."),
        new("operationalProjects.schedule.validation.parentInvalid", "zh", "里程碑 {index}：请选择此版本中存在的父级里程碑。", "工作项 {index}：请选择此版本中存在的父级工作项。"),
        new("operationalProjects.schedule.validation.parentInvalid", "ja", "マイルストーン {index}：この改訂内の親マイルストーンを選択してください。", "項目 {index}：この改訂内の親項目を選択してください。"),
        new("operationalProjects.schedule.validation.parentDates", "vi", "Mốc {index}: thời gian phải nằm trong ngày bắt đầu và kết thúc của mốc cha.", "Hạng mục {index}: thời gian phải nằm trong ngày bắt đầu và kết thúc của hạng mục cha."),
        new("operationalProjects.schedule.validation.parentDates", "en", "Milestone {index}: its dates must stay within the parent milestone dates.", "Item {index}: its dates must stay within the parent item dates."),
        new("operationalProjects.schedule.validation.parentDates", "zh", "里程碑 {index}：日期必须位于父级里程碑的日期范围内。", "工作项 {index}：日期必须位于父级工作项的日期范围内。"),
        new("operationalProjects.schedule.validation.parentDates", "ja", "マイルストーン {index}：日付を親マイルストーンの期間内にしてください。", "項目 {index}：日付を親項目の期間内にしてください。"),
        new("operationalProjects.schedule.validation.parentCycle", "vi", "Quan hệ mốc cha tạo thành vòng lặp. Hãy kiểm tra lại các mốc tiến độ.", "Quan hệ hạng mục cha tạo thành vòng lặp. Hãy kiểm tra lại các hạng mục tiến độ."),
        new("operationalProjects.schedule.email.noRecipients", "vi", "Không có người cần nhận email về thay đổi này.", "Chưa có công việc phòng ban, thiết kế hay thi công nào liên kết với các hạng mục thay đổi, nên hiện không có ai cần nhận email. Bạn vẫn có thể phát hành bản tiến độ này."),
        new("operationalProjects.schedule.email.noRecipients", "en", "No one needs an email about this change.", "No department, design or construction task is linked to the changed items, so no one currently needs an email. You can still publish this schedule."),
        new("operationalProjects.schedule.email.noRecipients", "zh", "此变更无需发送邮件。", "目前没有部门、设计或施工任务关联到已变更的工作项，因此暂无邮件收件人。仍可发布此进度。"),
        new("operationalProjects.schedule.email.noRecipients", "ja", "この変更についてメールの送信先はありません。", "変更された項目に部門・設計・施工のタスクが紐づいていないため、現在メール送信先はありません。この版はそのまま公開できます。"),
        new("scheduleUx.outsideFrame", "en", "{count} items fall outside the project period. Check the dates or the project frame before publishing.", "Items outside the project period: {count}. Check the dates or the project frame before publishing."),
        new("scheduleUx.masterState.Rejected", "vi", "Phiên bản bị từ chối. Xem ý kiến duyệt, sửa hạng mục rồi lưu và gửi duyệt lại.", "Bản này được trả lại để chỉnh sửa. Xem ý kiến duyệt, sửa hạng mục rồi lưu và gửi duyệt lại."),
        new("scheduleUx.masterState.Rejected", "en", "This version was rejected. Read the review comment, correct the items, save, and submit again.", "This version was returned for changes. Read the review comment, correct the items, save, and submit again."),
        new("scheduleUx.masterState.Rejected", "zh", "此版本被拒绝。请查看审批意见，修改并保存后重新提交。", "此版本已退回修改。请查看审批意见，修改并保存后重新提交。"),
        new("scheduleUx.masterState.Rejected", "ja", "却下された版です。コメントを確認し、修正・保存して再申請してください。", "この版は修正のため差し戻されました。コメントを確認し、修正・保存して再申請してください。"),
        new("scheduleUx.approverHint", "vi", "Trước hợp đồng: Trưởng phòng CRM. Sau hợp đồng: PM dự án. Hệ thống chỉ gửi yêu cầu cho người được chọn.", "Trước hợp đồng: Trưởng phòng CRM. Sau hợp đồng: PM dự án. Chọn người duyệt để gửi yêu cầu cho đúng người đó."),
        new("scheduleUx.approverHint", "en", "Before contract: CRM department head. After contract: project PM. The request goes only to the person you choose.", "Before contract: CRM department head. After contract: project PM. Choose the approver — the request goes only to that person."),
        new("scheduleUx.approverHint", "zh", "合同前：CRM部门负责人。合同后：项目经理。系统只向所选人员发送请求。", "合同前：CRM 部门负责人。合同后：项目经理。选择审批人后，请求只发送给该人员。"),
        new("scheduleUx.approverHint", "ja", "契約前：CRM部門長。契約後：プロジェクトPM。依頼は選択した本人にのみ送信されます。", "契約前：CRM 部門長。契約後：プロジェクトPM。承認者を選ぶと、依頼はその本人にのみ送信されます。"),
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
