using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace NihomeBackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261105200000_ClarifyScheduleApproverGuidance")]
public partial class ClarifyScheduleApproverGuidance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Trước khi ký hợp đồng với chủ đầu tư: Trưởng phòng Kinh doanh duyệt tiến độ.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.approverCrm' AND LanguageCode = N'vi' AND Value = N'Trước khi ký hợp đồng Upstream: người duyệt thuộc nhóm CRM có quyền quản lý Cơ hội.';""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep clearer wording and later administrator edits.
    }
}
