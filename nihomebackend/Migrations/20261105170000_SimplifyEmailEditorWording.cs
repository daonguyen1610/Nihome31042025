using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace NihomeBackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261105170000_SimplifyEmailEditorWording")]
public partial class SimplifyEmailEditorWording : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Nội dung email', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.editor.body' AND LanguageCode = N'vi' AND Value = N'Nội dung HTML email';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Email content', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.editor.body' AND LanguageCode = N'en' AND Value = N'Email HTML body';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'邮件内容', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.editor.body' AND LanguageCode = N'zh' AND Value = N'邮件HTML正文';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'メール本文', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.editor.body' AND LanguageCode = N'ja' AND Value = N'メールHTML本文';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Thông tin tự điền (chọn để chèn)', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.editor.tokens' AND LanguageCode = N'vi' AND Value = N'Biến có sẵn (click để chèn)';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Auto-filled details (select to insert)', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.editor.tokens' AND LanguageCode = N'en' AND Value = N'Available variables (click to insert)';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'自动填入的信息（点击插入）', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.editor.tokens' AND LanguageCode = N'zh' AND Value = N'可用变量（点击插入）';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'自動入力項目（選択して挿入）', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.editor.tokens' AND LanguageCode = N'ja' AND Value = N'利用可能な変数（クリックして挿入）';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Đường dẫn mở nhóm công việc', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.editor.token.workPackageUrl' AND LanguageCode = N'vi' AND Value = N'Link mở nhóm công việc';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Link to work package', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.editor.token.workPackageUrl' AND LanguageCode = N'en' AND Value = N'Work package link';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'作業グループへのリンク', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.editor.token.workPackageUrl' AND LanguageCode = N'ja' AND Value = N'業務グループを開くリンク';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Xem trước bằng thông tin minh họa. Khi gửi, hệ thống sẽ dùng thông tin của dự án và người nhận.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.schedule.hint' AND LanguageCode = N'vi' AND Value = N'Email thông báo duyệt/cập nhật tiến độ. Bản xem trước được server kết xuất bằng dữ liệu mẫu; khi gửi dùng dữ liệu dự án thực tế.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'The preview uses sample details. Sent emails use the actual project and recipient.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.schedule.hint' AND LanguageCode = N'en' AND Value = N'Approval and schedule-change emails. The server renders the preview with sample data; delivery uses actual project details.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'预览使用示例信息。发送时会使用实际项目和收件人信息。', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.schedule.hint' AND LanguageCode = N'zh' AND Value = N'用于进度审批和变更通知。服务器使用示例数据生成预览；发送时使用实际项目内容。';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'プレビューには例を表示します。送信時は実際の案件と受信者の情報を使います。', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.schedule.hint' AND LanguageCode = N'ja' AND Value = N'工程の承認・変更通知メールです。プレビューはサーバーがサンプルデータで生成し、送信時は実際のプロジェクト情報を使用します。';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Hãy nhờ quản trị viên kiểm tra email đã đến người nhận chưa trước khi gửi lại, để tránh gửi trùng.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.schedule.delivery.guidance' AND LanguageCode = N'vi' AND Value = N'Kiểm tra cấu hình SMTP và xác minh trạng thái gửi trước khi xử lý lại; email có thể đã đến dù máy chủ báo lỗi.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Ask an administrator to confirm whether the email arrived before sending it again to avoid duplicates.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.schedule.delivery.guidance' AND LanguageCode = N'en' AND Value = N'Check SMTP settings and confirm delivery before retrying; the message may have arrived despite the server error.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'重新发送前，请管理员确认邮件是否已送达，以免重复发送。', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.schedule.delivery.guidance' AND LanguageCode = N'zh' AND Value = N'重试前请检查 SMTP 设置并确认送达状态；即使服务器报错，邮件也可能已经送达。';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'重複送信を防ぐため、再送前に管理者へ受信状況の確認を依頼してください。', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.schedule.delivery.guidance' AND LanguageCode = N'ja' AND Value = N'再送前に SMTP 設定と配信状態を確認してください。サーバーがエラーを返してもメールが届いている場合があります。';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Nội dung email', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.quote.bodyLabel' AND LanguageCode = N'vi' AND Value = N'Nội dung HTML email';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Xem trước nội dung bên phải. Giữ thông tin hạng mục và tổng tiền trong email. Chỉ người quản lý báo giá được sửa trước khi gửi.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.quote.hint' AND LanguageCode = N'vi' AND Value = N'Có thể chỉnh HTML và xem trước bên phải. Giữ {{quoteLines}} và {{grandTotal}}; chỉ dùng HTML/CSS nội tuyến cơ bản. Người có quyền quản lý báo giá mới được sửa trước khi gửi.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Nội dung báo giá chưa đúng định dạng. Hãy kiểm tra phần nội dung và thử lại.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.quote.htmlRequired' AND LanguageCode = N'vi' AND Value = N'Nội dung mẫu báo giá phải là HTML hợp lệ và bắt đầu bằng thẻ HTML.';""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Retain the clearer wording and any subsequent administrator edits.
    }
}
