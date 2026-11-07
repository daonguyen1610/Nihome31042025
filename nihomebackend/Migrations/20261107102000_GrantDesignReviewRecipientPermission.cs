using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// RbacSeeder only seeds role group baselines and business role permissions once
/// (<c>InitialPermissionsSeeded</c>), so environments seeded before the design roles
/// gained <c>design.reviews.receive</c> never receive it. Grant it explicitly, and
/// only where it is missing so administrator changes are preserved.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261107102000_GrantDesignReviewRecipientPermission")]
public partial class GrantDesignReviewRecipientPermission : Migration
{
    private const string GrantGroupSql = """
        INSERT INTO role_group_permissions (RoleGroupId, PermissionId, CreatedAt)
        SELECT rg.Id, p.Id, SYSUTCDATETIME()
        FROM role_groups rg
        CROSS JOIN permissions p
        WHERE rg.Code = N'DESIGN'
          AND p.Module = N'design.reviews' AND p.Action = N'receive'
          AND NOT EXISTS (
              SELECT 1 FROM role_group_permissions existing
              WHERE existing.RoleGroupId = rg.Id AND existing.PermissionId = p.Id);
        """;

    private const string GrantRolesSql = """
        INSERT INTO role_permissions (RoleId, PermissionId, CreatedAt)
        SELECT r.Id, p.Id, SYSUTCDATETIME()
        FROM roles r
        CROSS JOIN permissions p
        WHERE r.Code IN (N'DESIGN', N'DESIGN_LEAD', N'DESIGN_MANAGER',
                         N'ARCHITECT', N'MEP_ENGINEER', N'STRUCT_ENGINEER')
          AND p.Module = N'design.reviews' AND p.Action = N'receive'
          AND NOT EXISTS (
              SELECT 1 FROM role_permissions existing
              WHERE existing.RoleId = r.Id AND existing.PermissionId = p.Id);
        """;

    private const string RevokeGroupSql = """
        DELETE existing
        FROM role_group_permissions existing
        INNER JOIN role_groups rg ON rg.Id = existing.RoleGroupId
        INNER JOIN permissions p ON p.Id = existing.PermissionId
        WHERE rg.Code = N'DESIGN'
          AND p.Module = N'design.reviews' AND p.Action = N'receive';
        """;

    private const string RevokeRolesSql = """
        DELETE existing
        FROM role_permissions existing
        INNER JOIN roles r ON r.Id = existing.RoleId
        INNER JOIN permissions p ON p.Id = existing.PermissionId
        WHERE r.Code IN (N'DESIGN', N'DESIGN_LEAD', N'DESIGN_MANAGER',
                         N'ARCHITECT', N'MEP_ENGINEER', N'STRUCT_ENGINEER')
          AND p.Module = N'design.reviews' AND p.Action = N'receive';
        """;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(GrantGroupSql);
        migrationBuilder.Sql(GrantRolesSql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(RevokeRolesSql);
        migrationBuilder.Sql(RevokeGroupSql);
    }
}
