using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260902120000_AddScopedSurveyPermissions")]
    public partial class AddScopedSurveyPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO permissions (Module, Action, DescriptionKey, IsActive, CreatedAt)
                SELECT 'crm.surveys', source.Action,
                       CONCAT('rbac.perm.crm.surveys.', source.Action),
                       1, SYSUTCDATETIME()
                FROM (VALUES ('view.all'), ('manage.all')) source(Action)
                WHERE NOT EXISTS (
                    SELECT 1 FROM permissions permission
                    WHERE permission.Module = 'crm.surveys'
                      AND permission.Action = source.Action
                );

                INSERT INTO role_permissions (RoleId, PermissionId, CreatedAt)
                SELECT role.Id, permission.Id, SYSUTCDATETIME()
                FROM roles role
                CROSS JOIN permissions permission
                WHERE permission.Module = 'crm.surveys'
                  AND (
                    (permission.Action = 'view.all' AND role.Code IN
                        ('SUPER_ADMIN', 'ADMIN', 'SALES_MANAGER', 'BGD'))
                    OR (permission.Action = 'manage.all' AND role.Code IN
                        ('SUPER_ADMIN', 'ADMIN', 'SALES_MANAGER'))
                  )
                  AND NOT EXISTS (
                    SELECT 1 FROM role_permissions existing
                    WHERE existing.RoleId = role.Id
                      AND existing.PermissionId = permission.Id
                  );

                DELETE rolePermission
                FROM role_permissions rolePermission
                INNER JOIN roles role ON role.Id = rolePermission.RoleId
                INNER JOIN permissions permission ON permission.Id = rolePermission.PermissionId
                WHERE role.Code = 'SALE'
                  AND permission.Module = 'crm.surveys'
                  AND permission.Action IN ('view.all', 'manage.all');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE rolePermission
                FROM role_permissions rolePermission
                INNER JOIN permissions permission ON permission.Id = rolePermission.PermissionId
                WHERE permission.Module = 'crm.surveys'
                  AND permission.Action IN ('view.all', 'manage.all');

                DELETE FROM permissions
                WHERE Module = 'crm.surveys'
                  AND Action IN ('view.all', 'manage.all');
                """);
        }
    }
}
