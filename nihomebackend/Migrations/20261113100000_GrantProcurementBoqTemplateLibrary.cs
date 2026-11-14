using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// Procurement downloads BOQ forms from the material-rate BOQ library (13/11/2026 decision).
/// RbacSeeder seeds business role permissions only once, so environments seeded earlier
/// never give Procurement roles <c>crm.material-rates.view</c>. Grant it only where missing
/// so administrator changes are preserved.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261113100000_GrantProcurementBoqTemplateLibrary")]
public partial class GrantProcurementBoqTemplateLibrary : Migration
{
    private const string GrantSql = """
        INSERT INTO role_permissions (RoleId, PermissionId, CreatedAt)
        SELECT r.Id, p.Id, SYSUTCDATETIME()
        FROM roles r
        CROSS JOIN permissions p
        WHERE r.Code IN (N'PROCUREMENT', N'PROCUREMENT_MANAGER')
          AND p.Module = N'crm.material-rates' AND p.Action = N'view'
          AND NOT EXISTS (
              SELECT 1 FROM role_permissions existing
              WHERE existing.RoleId = r.Id AND existing.PermissionId = p.Id);
        """;

    private const string RevokeSql = """
        DELETE existing
        FROM role_permissions existing
        INNER JOIN roles r ON r.Id = existing.RoleId
        INNER JOIN permissions p ON p.Id = existing.PermissionId
        WHERE r.Code IN (N'PROCUREMENT', N'PROCUREMENT_MANAGER')
          AND p.Module = N'crm.material-rates' AND p.Action = N'view';
        """;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(GrantSql);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(RevokeSql);
}
