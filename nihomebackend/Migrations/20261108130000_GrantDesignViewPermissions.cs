using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// RbacSeeder only seeds role group baselines and business role permissions once
/// (<c>InitialPermissionsSeeded</c>), so environments seeded before the design
/// roles gained the design view codes (and <c>operations.projects.view</c>)
/// never receive them and staff hit 403 on the Design module. Grant only the
/// missing rows so administrator edits are preserved.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261108130000_GrantDesignViewPermissions")]
public partial class GrantDesignViewPermissions : Migration
{
    private const string DesignRoles = """
        N'DESIGN', N'DESIGN_MANAGER', N'DESIGN_LEAD',
        N'ARCHITECT', N'MEP_ENGINEER', N'STRUCT_ENGINEER'
        """;

    private const string PermissionFilter = """
        (p.Module = N'design.projects' AND p.Action = N'view') OR
        (p.Module = N'design.concepts' AND p.Action = N'view') OR
        (p.Module = N'design.basic' AND p.Action = N'view') OR
        (p.Module = N'design.shop' AND p.Action = N'view') OR
        (p.Module = N'design.revisions' AND p.Action = N'view') OR
        (p.Module = N'design.ifc' AND p.Action = N'view') OR
        (p.Module = N'operations.projects' AND p.Action = N'view')
        """;

    private static string GrantGroupSql => $"""
        INSERT INTO role_group_permissions (RoleGroupId, PermissionId, CreatedAt)
        SELECT rg.Id, p.Id, SYSUTCDATETIME()
        FROM role_groups rg
        CROSS JOIN permissions p
        WHERE rg.Code = N'DESIGN'
          AND ({PermissionFilter})
          AND NOT EXISTS (
              SELECT 1 FROM role_group_permissions existing
              WHERE existing.RoleGroupId = rg.Id AND existing.PermissionId = p.Id);
        """;

    private static string GrantRolesSql => $"""
        INSERT INTO role_permissions (RoleId, PermissionId, CreatedAt)
        SELECT r.Id, p.Id, SYSUTCDATETIME()
        FROM roles r
        CROSS JOIN permissions p
        WHERE r.Code IN ({DesignRoles})
          AND ({PermissionFilter})
          AND NOT EXISTS (
              SELECT 1 FROM role_permissions existing
              WHERE existing.RoleId = r.Id AND existing.PermissionId = p.Id);
        """;

    private static string RevokeGroupSql => $"""
        DELETE existing
        FROM role_group_permissions existing
        INNER JOIN role_groups rg ON rg.Id = existing.RoleGroupId
        INNER JOIN permissions p ON p.Id = existing.PermissionId
        WHERE rg.Code = N'DESIGN'
          AND ({PermissionFilter});
        """;

    private static string RevokeRolesSql => $"""
        DELETE existing
        FROM role_permissions existing
        INNER JOIN roles r ON r.Id = existing.RoleId
        INNER JOIN permissions p ON p.Id = existing.PermissionId
        WHERE r.Code IN ({DesignRoles})
          AND ({PermissionFilter});
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
