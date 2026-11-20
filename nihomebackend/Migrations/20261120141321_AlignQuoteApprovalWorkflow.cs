using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AlignQuoteApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Quotes are approved by the Procurement Manager the sender chooses (QuoteService).
            // Correct the default chain and the template note where an administrator has not
            // edited them; an edited row stays as the administrator left it.
            migrationBuilder.Sql(@"
UPDATE workflow_configs
SET Description = N'Người gửi chọn một Trưởng phòng Cung ứng để duyệt báo giá; người gửi không tự duyệt.', StepsJson = N'[{""order"":1,""name"":""Tr\u01B0\u1EDFng ph\u00F2ng Cung \u1EE9ng \u0111\u01B0\u1EE3c ch\u1ECDn duy\u1EC7t"",""approverRoleCode"":""PROCUREMENT_MANAGER"",""slaHours"":24,""requireAllApprovers"":false,""conditionExpression"":null}]', UpdatedAt = SYSUTCDATETIME()
WHERE Module = N'quotes' AND Action = N'approve' AND Description = N'Chuỗi duyệt báo giá tiêu chuẩn: Sales Manager rồi Ban Giám đốc.'
  AND StepsJson LIKE N'%""approverRoleCode"":""SALES_MANAGER""%""approverRoleCode"":""BGD""%';
UPDATE notification_templates
SET AdminDescription = N'Trưởng phòng Cung ứng được chọn cần duyệt một báo giá vừa gửi.'
WHERE Code = N'quote.submitted-for-approval' AND AdminDescription = N'Sales Manager cần duyệt một báo giá vừa submit.';");
            // The workflow list shows the approval chain instead of a step count, so the
            // label is gone; the seeder only inserts keys, so remove its rows here.
            // The list searches by workflow name or approver role now, not module or action,
            // so the search hint has a new key and the old wording goes.
            migrationBuilder.Sql("DELETE FROM translations WHERE [Key] IN (N'workflow.field.steps', N'workflow.searchPlaceholder');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO translations ([Key], LanguageCode, Value, Category, CreatedAt, UpdatedAt)
SELECT v.[Key], v.LanguageCode, v.Value, v.Category, v.CreatedAt, v.UpdatedAt FROM (VALUES
(N'workflow.field.steps', N'en', N'Steps', N'workflow', SYSUTCDATETIME(), SYSUTCDATETIME()),
(N'workflow.field.steps', N'ja', N'ステップ数', N'workflow', SYSUTCDATETIME(), SYSUTCDATETIME()),
(N'workflow.field.steps', N'vi', N'Số bước', N'workflow', SYSUTCDATETIME(), SYSUTCDATETIME()),
(N'workflow.field.steps', N'zh', N'步骤数', N'workflow', SYSUTCDATETIME(), SYSUTCDATETIME()),
(N'workflow.searchPlaceholder', N'en', N'Search by module, action or name…', N'workflow', SYSUTCDATETIME(), SYSUTCDATETIME()),
(N'workflow.searchPlaceholder', N'ja', N'モジュール・アクション・名称で検索…', N'workflow', SYSUTCDATETIME(), SYSUTCDATETIME()),
(N'workflow.searchPlaceholder', N'vi', N'Tìm theo module, action hoặc tên…', N'workflow', SYSUTCDATETIME(), SYSUTCDATETIME()),
(N'workflow.searchPlaceholder', N'zh', N'按模块、操作或名称搜索…', N'workflow', SYSUTCDATETIME(), SYSUTCDATETIME())
) v([Key], LanguageCode, Value, Category, CreatedAt, UpdatedAt)
WHERE NOT EXISTS (SELECT 1 FROM translations t WHERE t.[Key] = v.[Key] AND t.LanguageCode = v.LanguageCode);
UPDATE workflow_configs
SET Description = N'Chuỗi duyệt báo giá tiêu chuẩn: Sales Manager rồi Ban Giám đốc.', StepsJson = N'[{""order"":1,""name"":""Tr\u01B0\u1EDFng ph\u00F2ng Kinh doanh duy\u1EC7t"",""approverRoleCode"":""SALES_MANAGER"",""slaHours"":24,""requireAllApprovers"":false,""conditionExpression"":null},{""order"":2,""name"":""Ban Gi\u00E1m \u0111\u1ED1c ph\u00EA duy\u1EC7t"",""approverRoleCode"":""BGD"",""slaHours"":48,""requireAllApprovers"":true,""conditionExpression"":null}]', UpdatedAt = SYSUTCDATETIME()
WHERE Module = N'quotes' AND Action = N'approve' AND Description = N'Người gửi chọn một Trưởng phòng Cung ứng để duyệt báo giá; người gửi không tự duyệt.';
UPDATE notification_templates
SET AdminDescription = N'Sales Manager cần duyệt một báo giá vừa submit.'
WHERE Code = N'quote.submitted-for-approval' AND AdminDescription = N'Trưởng phòng Cung ứng được chọn cần duyệt một báo giá vừa gửi.';");
        }
    }
}
