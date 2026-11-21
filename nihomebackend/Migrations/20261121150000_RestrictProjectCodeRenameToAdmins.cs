using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// NICON (Góp ý lập trình v2, answer 2): only system administrators change a project code.
/// The shipped defaults gave <c>operations.projects.rename</c> to SALES_MANAGER (through
/// <c>operations.projects.**</c>) and BGD; RbacSeeder seeds business roles only once, so
/// existing databases keep those grants until this removes them. SUPER_ADMIN and ADMIN keep it.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261121150000_RestrictProjectCodeRenameToAdmins")]
public partial class RestrictProjectCodeRenameToAdmins : Migration
{
    private const string RevokeSql = """
        DELETE existing
        FROM role_permissions existing
        INNER JOIN roles r ON r.Id = existing.RoleId
        INNER JOIN permissions p ON p.Id = existing.PermissionId
        WHERE r.Code IN (N'SALES_MANAGER', N'BGD')
          AND p.Module = N'operations.projects' AND p.Action = N'rename';
        """;

    private const string GrantSql = """
        INSERT INTO role_permissions (RoleId, PermissionId, CreatedAt)
        SELECT r.Id, p.Id, SYSUTCDATETIME()
        FROM roles r
        CROSS JOIN permissions p
        WHERE r.Code IN (N'SALES_MANAGER', N'BGD')
          AND p.Module = N'operations.projects' AND p.Action = N'rename'
          AND NOT EXISTS (
              SELECT 1 FROM role_permissions existing
              WHERE existing.RoleId = r.Id AND existing.PermissionId = p.Id);
        """;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(RevokeSql);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(GrantSql);
}
