using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AlignQuoteApprovalWorkflow : Migration
    {
        private const string OldDescription = "Chuỗi duyệt báo giá tiêu chuẩn: Sales Manager rồi Ban Giám đốc.";
        private const string NewDescription = "Người gửi chọn một Trưởng phòng Cung ứng để duyệt báo giá; người gửi không tự duyệt.";

        private const string OldStepsJson = @"[{""order"":1,""name"":""Tr\u01B0\u1EDFng ph\u00F2ng Kinh doanh duy\u1EC7t"",""approverRoleCode"":""SALES_MANAGER"",""slaHours"":24,""requireAllApprovers"":false,""conditionExpression"":null},{""order"":2,""name"":""Ban Gi\u00E1m \u0111\u1ED1c ph\u00EA duy\u1EC7t"",""approverRoleCode"":""BGD"",""slaHours"":48,""requireAllApprovers"":true,""conditionExpression"":null}]";
        private const string NewStepsJson = @"[{""order"":1,""name"":""Tr\u01B0\u1EDFng ph\u00F2ng Cung \u1EE9ng \u0111\u01B0\u1EE3c ch\u1ECDn duy\u1EC7t"",""approverRoleCode"":""PROCUREMENT_MANAGER"",""slaHours"":24,""requireAllApprovers"":false,""conditionExpression"":null}]";

        // (order, name, role, hours, everyone must approve) of each default chain.
        private const string OldSteps = @"(1, N'Trưởng phòng Kinh doanh duyệt', N'SALES_MANAGER', 24, CAST(0 AS bit)),
(2, N'Ban Giám đốc phê duyệt', N'BGD', 48, CAST(1 AS bit))";
        private const string NewSteps = @"(1, N'Trưởng phòng Cung ứng được chọn duyệt', N'PROCUREMENT_MANAGER', 24, CAST(0 AS bit))";

        // The editor's labels that are no longer used, with their seeded wording.
        private const string RetiredLabels = @"(N'workflow.field.steps', N'en', N'Steps'),
(N'workflow.field.steps', N'ja', N'ステップ数'),
(N'workflow.field.steps', N'vi', N'Số bước'),
(N'workflow.field.steps', N'zh', N'步骤数'),
(N'workflow.searchPlaceholder', N'en', N'Search by module, action or name…'),
(N'workflow.searchPlaceholder', N'ja', N'モジュール・アクション・名称で検索…'),
(N'workflow.searchPlaceholder', N'vi', N'Tìm theo module, action hoặc tên…'),
(N'workflow.searchPlaceholder', N'zh', N'按模块、操作或名称搜索…'),
(N'workflow.dialogHelp', N'en', N'Each module + action pair can have only one workflow. Approver roles must exist in RBAC.'),
(N'workflow.dialogHelp', N'ja', N'モジュールとアクションの組合せは 1 つのフローのみ。承認ロールは RBAC に存在する必要があります。'),
(N'workflow.dialogHelp', N'vi', N'Mỗi cặp module + action chỉ được có một luồng duyệt. Vai trò duyệt phải nằm trong danh sách RBAC.'),
(N'workflow.dialogHelp', N'zh', N'每个模块 + 操作组合只能配置一个流程。审批角色必须存在于 RBAC 中。'),
(N'workflow.stepFieldsRequired', N'en', N'Each step needs a name and an approver role.'),
(N'workflow.stepFieldsRequired', N'ja', N'各ステップに名称と承認ロールが必要です。'),
(N'workflow.stepFieldsRequired', N'vi', N'Mỗi bước cần có tên và vai trò duyệt.'),
(N'workflow.stepFieldsRequired', N'zh', N'每个步骤都需要名称和审批角色。'),
(N'workflow.field.description', N'en', N'Description'),
(N'workflow.field.description', N'ja', N'説明'),
(N'workflow.field.description', N'vi', N'Mô tả'),
(N'workflow.field.description', N'zh', N'描述'),
(N'workflow.field.sortOrder', N'en', N'Sort order'),
(N'workflow.field.sortOrder', N'ja', N'並び順'),
(N'workflow.field.sortOrder', N'vi', N'Thứ tự'),
(N'workflow.field.sortOrder', N'zh', N'排序'),
(N'workflow.field.stepName', N'en', N'Step name'),
(N'workflow.field.stepName', N'ja', N'ステップ名'),
(N'workflow.field.stepName', N'vi', N'Tên bước'),
(N'workflow.field.stepName', N'zh', N'步骤名称'),
(N'workflow.field.slaHours', N'en', N'SLA (hours)'),
(N'workflow.field.slaHours', N'ja', N'SLA（時間）'),
(N'workflow.field.slaHours', N'vi', N'SLA (giờ)'),
(N'workflow.field.slaHours', N'zh', N'SLA（小时）'),
(N'workflow.field.requireAll', N'en', N'Require all approvers'),
(N'workflow.field.requireAll', N'ja', N'全員承認が必要'),
(N'workflow.field.requireAll', N'vi', N'Cần tất cả duyệt'),
(N'workflow.field.requireAll', N'zh', N'需所有人审批')";

        /// <summary>
        /// True when the quote workflow's steps are exactly the given default chain: every
        /// field of every step, read as JSON, so a chain an administrator changed in any way
        /// (a step, its name, its deadline or "everyone must approve") is left alone.
        /// </summary>
        private static string StepsAre(string steps, int count) => $@"ISJSON(StepsJson) = 1
  AND (SELECT COUNT(*) FROM OPENJSON(StepsJson)) = {count}
  AND NOT EXISTS (
    SELECT [order], name, approverRoleCode, slaHours, requireAllApprovers, conditionExpression
    FROM OPENJSON(StepsJson) WITH ([order] int '$.order', name nvarchar(120) '$.name',
      approverRoleCode nvarchar(60) '$.approverRoleCode', slaHours int '$.slaHours',
      requireAllApprovers bit '$.requireAllApprovers', conditionExpression nvarchar(max) '$.conditionExpression')
    EXCEPT
    SELECT o, n, r, h, a, CAST(NULL AS nvarchar(max)) FROM (VALUES {steps}) d(o, n, r, h, a))";

        private static string MoveDefault(string fromDescription, string fromSteps, int fromCount, string toDescription, string toJson) => $@"
UPDATE workflow_configs
SET Description = N'{toDescription}', StepsJson = N'{toJson}', UpdatedAt = SYSUTCDATETIME()
WHERE Module = N'quotes' AND Action = N'approve' AND Description = N'{fromDescription}'
  AND {StepsAre(fromSteps, fromCount)};";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Quotes are approved by the Procurement Manager the sender chooses (QuoteService).
            // Correct the default chain and the template note only where an administrator has
            // not edited them; an edited row stays as the administrator left it.
            migrationBuilder.Sql(MoveDefault(OldDescription, OldSteps, 2, NewDescription, NewStepsJson) + @"
UPDATE notification_templates
SET AdminDescription = N'Trưởng phòng Cung ứng được chọn cần duyệt một báo giá vừa gửi.'
WHERE Code = N'quote.submitted-for-approval' AND AdminDescription = N'Sales Manager cần duyệt một báo giá vừa submit.';");
            // The list shows the approval chain and searches by name or approver role, and the
            // simplified editor drops its technical labels. The seeder only inserts keys, so
            // remove the retired ones here, but only where the wording is still the seeded one:
            // wording an administrator changed in /admin/translations is kept.
            migrationBuilder.Sql(@"
DELETE t FROM translations t
JOIN (VALUES " + RetiredLabels + @") v([Key], LanguageCode, Value)
  ON t.[Key] = v.[Key] AND t.LanguageCode = v.LanguageCode AND t.Value = v.Value;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO translations ([Key], LanguageCode, Value, Category, CreatedAt, UpdatedAt)
SELECT v.[Key], v.LanguageCode, v.Value, N'workflow', SYSUTCDATETIME(), SYSUTCDATETIME()
FROM (VALUES " + RetiredLabels + @") v([Key], LanguageCode, Value)
WHERE NOT EXISTS (SELECT 1 FROM translations t WHERE t.[Key] = v.[Key] AND t.LanguageCode = v.LanguageCode);");
            migrationBuilder.Sql(MoveDefault(NewDescription, NewSteps, 1, OldDescription, OldStepsJson) + @"
UPDATE notification_templates
SET AdminDescription = N'Sales Manager cần duyệt một báo giá vừa submit.'
WHERE Code = N'quote.submitted-for-approval' AND AdminDescription = N'Trưởng phòng Cung ứng được chọn cần duyệt một báo giá vừa gửi.';");
        }
    }
}
