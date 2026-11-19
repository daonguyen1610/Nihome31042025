using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// The whole Procurement department sees every NICON quote (19/11/2026 decision); only the
/// Procurement head approves. RbacSeeder seeds business role permissions only once, so
/// environments seeded earlier never give PROCUREMENT <c>crm.quotes.view</c> and
/// <c>crm.quotes.view.all</c>. Grant them only where missing so administrator changes are kept.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261119180000_GrantProcurementQuoteVisibility")]
public partial class GrantProcurementQuoteVisibility : Migration
{
    private const string GrantSql = """
        INSERT INTO role_permissions (RoleId, PermissionId, CreatedAt)
        SELECT r.Id, p.Id, SYSUTCDATETIME()
        FROM roles r
        CROSS JOIN permissions p
        WHERE r.Code = N'PROCUREMENT'
          AND p.Module = N'crm.quotes' AND p.Action IN (N'view', N'view.all')
          AND NOT EXISTS (
              SELECT 1 FROM role_permissions existing
              WHERE existing.RoleId = r.Id AND existing.PermissionId = p.Id);
        """;

    // PROCUREMENT_MANAGER already had both grants by default and is left alone, so an
    // administrator's removal there is not undone.
    private const string RevokeSql = """
        DELETE existing
        FROM role_permissions existing
        INNER JOIN roles r ON r.Id = existing.RoleId
        INNER JOIN permissions p ON p.Id = existing.PermissionId
        WHERE r.Code = N'PROCUREMENT'
          AND p.Module = N'crm.quotes' AND p.Action IN (N'view', N'view.all');
        """;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(GrantSql);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(RevokeSql);
}
