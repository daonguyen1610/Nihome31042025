using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace NihomeBackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261105193000_UpdateScheduleCustomerWording")]
public partial class UpdateScheduleCustomerWording : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Trước khi ký hợp đồng với chủ đầu tư: Trưởng phòng Kinh doanh duyệt tiến độ.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.approverCrm' AND LanguageCode = N'vi' AND Value = N'Trước khi ký hợp đồng Upstream: Trưởng phòng Kinh doanh (SALES_MANAGER) thuộc CRM là người duyệt.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Sau khi ký hợp đồng với chủ đầu tư: người quản lý dự án duyệt tiến độ.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.approverPm' AND LanguageCode = N'vi' AND Value = N'Sau khi ký hợp đồng Upstream: PM dự án là người duyệt.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Tạo phiên bản mới', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.newRevision' AND LanguageCode = N'vi' AND Value = N'Tạo revision mới';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Mã mốc', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.wbs' AND LanguageCode = N'vi' AND Value = N'Mã WBS';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Mã mốc “{code}” đang bị trùng. Hãy dùng mã khác.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.validation.wbsDuplicate' AND LanguageCode = N'vi' AND Value = N'Mã WBS “{code}” đang bị trùng. Hãy dùng mã khác.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Mốc tiến độ', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.milestone' AND LanguageCode = N'vi' AND Value = N'Mốc WBS';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Chọn mốc tiến độ đang áp dụng', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.chooseMilestone' AND LanguageCode = N'vi' AND Value = N'Chọn mốc WBS đang phát hành';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Hãy chọn mốc tiến độ đang áp dụng để ngày công việc nằm trong khung tiến độ tổng thể.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.milestoneRequired' AND LanguageCode = N'vi' AND Value = N'Bắt buộc chọn mốc WBS đang phát hành để công việc nằm trong khung tiến độ tổng thể.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Tiến độ cần có ít nhất một mốc.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.validation.atLeastOneItem' AND LanguageCode = N'vi' AND Value = N'Tiến độ cần có ít nhất một mốc WBS.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Tiến độ không được có quá 500 mốc.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.validation.tooManyItems' AND LanguageCode = N'vi' AND Value = N'Tiến độ không được có quá 500 mốc WBS.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Mốc {index}: hãy nhập mã mốc.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.validation.wbsRequired' AND LanguageCode = N'vi' AND Value = N'Mốc {index}: hãy nhập mã WBS.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Mốc {index}: mã mốc tối đa 80 ký tự.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.validation.wbsTooLong' AND LanguageCode = N'vi' AND Value = N'Mốc {index}: mã WBS tối đa 80 ký tự.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Mốc {index}: hãy chọn mốc cha trong cùng phiên bản tiến độ.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.validation.parentInvalid' AND LanguageCode = N'vi' AND Value = N'Mốc {index}: hãy chọn mốc cha có trong revision.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Quan hệ mốc cha tạo thành vòng lặp. Hãy kiểm tra lại các mốc tiến độ.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'operationalProjects.schedule.validation.parentCycle' AND LanguageCode = N'vi' AND Value = N'Quan hệ mốc cha tạo thành vòng lặp. Hãy kiểm tra lại cây WBS.';""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep clearer wording and later administrator edits.
    }
}
