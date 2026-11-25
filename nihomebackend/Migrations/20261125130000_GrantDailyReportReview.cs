using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// Department heads review daily work reports through <c>work.daily-reports.review</c>.
/// RbacSeeder seeds business role permissions only once, so environments seeded earlier
/// would leave the head roles without it. Create the permission when missing and grant it
/// where missing, so administrator changes are kept. A fresh database gets it from the seed.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261125130000_GrantDailyReportReview")]
public partial class GrantDailyReportReview : Migration
{
    private const string Roles = "N'SALES_MANAGER', N'DESIGN_MANAGER', N'DESIGN_LEAD', N'PM', N'PROCUREMENT_MANAGER'";

    private const string GrantSql = $"""
        IF NOT EXISTS (SELECT 1 FROM permissions WHERE Module = N'work.daily-reports' AND Action = N'review')
            INSERT INTO permissions (Module, Action, DescriptionKey, IsActive, CreatedAt)
            VALUES (N'work.daily-reports', N'review', N'rbac.perm.work.daily-reports.review', 1, SYSUTCDATETIME());

        INSERT INTO role_permissions (RoleId, PermissionId, CreatedAt)
        SELECT r.Id, p.Id, SYSUTCDATETIME()
        FROM roles r
        CROSS JOIN permissions p
        WHERE r.Code IN ({Roles})
          AND p.Module = N'work.daily-reports' AND p.Action = N'review'
          AND NOT EXISTS (
              SELECT 1 FROM role_permissions existing
              WHERE existing.RoleId = r.Id AND existing.PermissionId = p.Id);
        """;

    private const string RevokeSql = $"""
        DELETE existing
        FROM role_permissions existing
        INNER JOIN roles r ON r.Id = existing.RoleId
        INNER JOIN permissions p ON p.Id = existing.PermissionId
        WHERE r.Code IN ({Roles})
          AND p.Module = N'work.daily-reports' AND p.Action = N'review';
        """;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(GrantSql);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(RevokeSql);
}
