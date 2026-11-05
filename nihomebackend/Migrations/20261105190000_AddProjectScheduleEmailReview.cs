using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace NihomeBackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261105190000_AddProjectScheduleEmailReview")]
public partial class AddProjectScheduleEmailReview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ReviewedAt", table: "project_schedule_emails", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "ReviewedByUserId", table: "project_schedule_emails", type: "int", nullable: true);
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Người quản lý dự án kiểm tra với người nhận, sau đó chọn Đã nhận hoặc Gửi lại trong dự án.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.schedule.delivery.guidance' AND LanguageCode = N'vi' AND Value = N'Hãy nhờ quản trị viên kiểm tra email đã đến người nhận chưa trước khi gửi lại, để tránh gửi trùng.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'The project manager checks with the recipient, then marks Received or Send again in the project.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.schedule.delivery.guidance' AND LanguageCode = N'en' AND Value = N'Ask an administrator to confirm whether the email arrived before sending it again to avoid duplicates.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'项目经理向收件人确认后，在项目中选择已收到或重新发送。', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.schedule.delivery.guidance' AND LanguageCode = N'zh' AND Value = N'重新发送前，请管理员确认邮件是否已送达，以免重复发送。';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'プロジェクト管理者が受信者に確認し、プロジェクト内で受信済みまたは再送を選びます。', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.schedule.delivery.guidance' AND LanguageCode = N'ja' AND Value = N'重複送信を防ぐため、再送前に管理者へ受信状況の確認を依頼してください。';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Email #{{emailId}} của bản tiến độ #{{revisionId}}, dự án #{{projectId}} chưa gửi được sau 5 lần. Hãy mở dự án, kiểm tra với người nhận rồi chọn Đã nhận hoặc Gửi lại.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'notification.project.schedule.email-needs-review.body' AND LanguageCode = N'vi' AND Value LIKE N'%SMTP%';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Email #{{emailId}} for schedule version #{{revisionId}} in project #{{projectId}} could not be sent after 5 attempts. Open the project, check with the recipient, then mark Received or Send again.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'notification.project.schedule.email-needs-review.body' AND LanguageCode = N'en' AND Value LIKE N'%SMTP%';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'项目 #{{projectId}} 的进度版本 #{{revisionId}} 的邮件 #{{emailId}} 尝试发送五次仍未成功。请打开项目，向收件人确认后选择已收到或重新发送。', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'notification.project.schedule.email-needs-review.body' AND LanguageCode = N'zh' AND Value LIKE N'%SMTP%';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'プロジェクト #{{projectId}} の工程版 #{{revisionId}} のメール #{{emailId}} は5回送信できませんでした。プロジェクトを開き、受信者に確認してから受信済みまたは再送を選んでください。', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'notification.project.schedule.email-needs-review.body' AND LanguageCode = N'ja' AND Value LIKE N'%SMTP%';""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep updated guidance and any administrator edits.
        migrationBuilder.DropColumn(name: "ReviewedAt", table: "project_schedule_emails");
        migrationBuilder.DropColumn(name: "ReviewedByUserId", table: "project_schedule_emails");
    }
}
