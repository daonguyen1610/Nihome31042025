using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// The 31/10/2026 meeting made QS the owner of the project's root BOQ upload.
/// RbacSeeder seeds business role permissions only once, so environments seeded
/// earlier never give QS <c>proc.boq</c> view and manage. Grant them only where
/// missing so administrator changes are preserved.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261108100000_GrantQsProjectBoqPermissions")]
public partial class GrantQsProjectBoqPermissions : Migration
{
    private const string GrantSql = """
        INSERT INTO role_permissions (RoleId, PermissionId, CreatedAt)
        SELECT r.Id, p.Id, SYSUTCDATETIME()
        FROM roles r
        CROSS JOIN permissions p
        WHERE r.Code = N'QS'
          AND p.Module = N'proc.boq' AND p.Action IN (N'view', N'manage')
          AND NOT EXISTS (
              SELECT 1 FROM role_permissions existing
              WHERE existing.RoleId = r.Id AND existing.PermissionId = p.Id);
        """;

    private const string RevokeSql = """
        DELETE existing
        FROM role_permissions existing
        INNER JOIN roles r ON r.Id = existing.RoleId
        INNER JOIN permissions p ON p.Id = existing.PermissionId
        WHERE r.Code = N'QS'
          AND p.Module = N'proc.boq' AND p.Action IN (N'view', N'manage');
        """;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(GrantSql);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(RevokeSql);
}
