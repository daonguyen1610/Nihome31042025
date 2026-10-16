using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// Separates HR from Finance without replacing administrator-managed role
/// memberships or translations.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261016120000_SeparateHrRoleGroup")]
public partial class SeparateHrRoleGroup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF NOT EXISTS (SELECT 1 FROM role_groups WHERE Code = N'HR')
            BEGIN
                INSERT INTO role_groups
                    (Code, LabelKey, SortOrder, IsActive, InitialPermissionsSeeded, CreatedAt)
                VALUES
                    (N'HR', N'rbac.group.hr-admin', 20, 1, 0, SYSUTCDATETIME());
            END;

            UPDATE role_groups SET SortOrder = 10, IsActive = 1 WHERE Code = N'CRM';
            UPDATE role_groups SET SortOrder = 20, LabelKey = N'rbac.group.hr-admin', IsActive = 1 WHERE Code = N'HR';
            UPDATE role_groups SET SortOrder = 30, IsActive = 1 WHERE Code = N'PROCUREMENT';
            UPDATE role_groups SET SortOrder = 40, IsActive = 1 WHERE Code = N'CONSTRUCTION';
            UPDATE role_groups SET SortOrder = 50, IsActive = 1 WHERE Code = N'DESIGN';
            UPDATE role_groups SET SortOrder = 60, IsActive = 1 WHERE Code = N'FINANCE';

            -- Move only the untouched shipped mapping. An explicit admin
            -- reassignment to FINANCE must remain in effect.
            UPDATE roles
            SET RoleGroupId = (SELECT Id FROM role_groups WHERE Code = N'HR'),
                UpdatedAt = SYSUTCDATETIME()
            WHERE Code = N'HR'
              AND RoleGroupId = (SELECT Id FROM role_groups WHERE Code = N'FINANCE')
              AND InitialGroupSeeded = 1
              AND NOT EXISTS (
                  SELECT 1 FROM audit_logs
                  WHERE Action = N'rbac.role.group.update'
                    AND ResourceType = N'rbac.role'
                    AND ResourceId = N'HR'
                    AND Status = N'success'
              );

            -- Existing administrator-edited labels are preserved.
            UPDATE translations SET Value = N'Kế toán – Tài chính', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.finance' AND LanguageCode = N'vi'
              AND Value = N'Kế toán – Tài chính – Nhân sự';
            UPDATE translations SET Value = N'Accounting & Finance', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.finance' AND LanguageCode = N'en'
              AND Value = N'Accounting, Finance & Human Resources';
            UPDATE translations SET Value = N'会计与财务', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.finance' AND LanguageCode = N'zh'
              AND Value = N'会计、财务与人力资源';
            UPDATE translations SET Value = N'会計・財務', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.finance' AND LanguageCode = N'ja'
              AND Value = N'会計・財務・人事';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Preserve live role assignments on rollback. A data rollback
            -- needs a separate reviewed operation.
            UPDATE role_groups SET SortOrder = 20 WHERE Code = N'PROCUREMENT';
            UPDATE role_groups SET SortOrder = 30 WHERE Code = N'CONSTRUCTION';
            UPDATE role_groups SET SortOrder = 40 WHERE Code = N'DESIGN';
            UPDATE role_groups SET SortOrder = 50 WHERE Code = N'FINANCE';

            IF EXISTS (SELECT 1 FROM role_groups WHERE Code = N'HR')
               AND NOT EXISTS (
                   SELECT 1 FROM roles
                   WHERE RoleGroupId = (SELECT Id FROM role_groups WHERE Code = N'HR'))
               AND NOT EXISTS (
                   SELECT 1 FROM role_group_permissions
                   WHERE RoleGroupId = (SELECT Id FROM role_groups WHERE Code = N'HR'))
            BEGIN
                DELETE FROM role_groups WHERE Code = N'HR';
            END;
            """);
    }
}
