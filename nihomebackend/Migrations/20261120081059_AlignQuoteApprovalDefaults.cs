using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AlignQuoteApprovalDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Quotes are approved only by the Procurement Manager the sender chooses (QuoteService);
            // align the untouched default chain and the template note. Rows an admin edited stay.
            migrationBuilder.Sql(@"
UPDATE workflow_configs
SET Description = N'Báo giá do Trưởng phòng Cung ứng được người gửi chọn duyệt; người gửi không tự duyệt.', StepsJson = N'[{""order"":1,""name"":""Tr\u01B0\u1EDFng ph\u00F2ng Cung \u1EE9ng duy\u1EC7t"",""approverRoleCode"":""PROCUREMENT_MANAGER"",""slaHours"":24,""requireAllApprovers"":false,""conditionExpression"":null}]', UpdatedAt = SYSUTCDATETIME()
WHERE Module = N'quotes' AND Action = N'approve' AND Description = N'Chuỗi duyệt báo giá tiêu chuẩn: Sales Manager rồi Ban Giám đốc.'
  AND StepsJson LIKE N'%""approverRoleCode"":""SALES_MANAGER""%""approverRoleCode"":""BGD""%';
UPDATE notification_templates
SET AdminDescription = N'Trưởng phòng Cung ứng được chọn cần duyệt một báo giá vừa gửi.'
WHERE Code = N'quote.submitted-for-approval' AND AdminDescription = N'Sales Manager cần duyệt một báo giá vừa submit.';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE workflow_configs
SET Description = N'Chuỗi duyệt báo giá tiêu chuẩn: Sales Manager rồi Ban Giám đốc.', StepsJson = N'[{""order"":1,""name"":""Tr\u01B0\u1EDFng ph\u00F2ng Kinh doanh duy\u1EC7t"",""approverRoleCode"":""SALES_MANAGER"",""slaHours"":24,""requireAllApprovers"":false,""conditionExpression"":null},{""order"":2,""name"":""Ban Gi\u00E1m \u0111\u1ED1c ph\u00EA duy\u1EC7t"",""approverRoleCode"":""BGD"",""slaHours"":48,""requireAllApprovers"":true,""conditionExpression"":null}]', UpdatedAt = SYSUTCDATETIME()
WHERE Module = N'quotes' AND Action = N'approve' AND Description = N'Báo giá do Trưởng phòng Cung ứng được người gửi chọn duyệt; người gửi không tự duyệt.';
UPDATE notification_templates
SET AdminDescription = N'Sales Manager cần duyệt một báo giá vừa submit.'
WHERE Code = N'quote.submitted-for-approval' AND AdminDescription = N'Trưởng phòng Cung ứng được chọn cần duyệt một báo giá vừa gửi.';");
        }
    }
}
