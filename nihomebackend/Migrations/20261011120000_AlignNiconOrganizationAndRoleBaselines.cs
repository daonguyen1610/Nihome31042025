using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// Aligns the active role groups with the NICON organization chart supplied
/// on 11/10/2026 and enables one-time, administrator-preserving baselines.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261011120000_AlignNiconOrganizationAndRoleBaselines")]
public partial class AlignNiconOrganizationAndRoleBaselines : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "InitialPermissionsSeeded",
            table: "role_groups",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("""
            IF NOT EXISTS (SELECT 1 FROM role_groups WHERE Code = N'PROCUREMENT')
            BEGIN
                INSERT INTO role_groups (Code, LabelKey, SortOrder, IsActive, InitialPermissionsSeeded, CreatedAt)
                VALUES (N'PROCUREMENT', N'rbac.group.procurement', 20, 1, 0, SYSUTCDATETIME());
            END;

            UPDATE role_groups SET SortOrder = 10, LabelKey = N'rbac.group.crm', IsActive = 1 WHERE Code = N'CRM';
            UPDATE role_groups SET SortOrder = 20, LabelKey = N'rbac.group.procurement', IsActive = 1 WHERE Code = N'PROCUREMENT';
            UPDATE role_groups SET SortOrder = 30, LabelKey = N'rbac.group.construction', IsActive = 1 WHERE Code = N'CONSTRUCTION';
            UPDATE role_groups SET SortOrder = 40, LabelKey = N'rbac.group.design', IsActive = 1 WHERE Code = N'DESIGN';
            UPDATE role_groups SET SortOrder = 50, LabelKey = N'rbac.group.finance', IsActive = 1 WHERE Code = N'FINANCE';

            UPDATE roles
            SET RoleGroupId = (SELECT Id FROM role_groups WHERE Code = N'CONSTRUCTION'),
                UpdatedAt = SYSUTCDATETIME()
            WHERE Code = N'LEGAL_OFFICER';

            UPDATE roles
            SET RoleGroupId = (SELECT Id FROM role_groups WHERE Code = N'PROCUREMENT'),
                UpdatedAt = SYSUTCDATETIME()
            WHERE Code IN (N'PROCUREMENT', N'WAREHOUSE');

            UPDATE roles
            SET RoleGroupId = (SELECT Id FROM role_groups WHERE Code = N'FINANCE'),
                UpdatedAt = SYSUTCDATETIME()
            WHERE Code = N'ACCOUNTANT';

            UPDATE legacy
            SET IsActive = CASE
                WHEN EXISTS (
                    SELECT 1 FROM roles role
                    WHERE role.RoleGroupId = legacy.Id AND role.Code <> N'LEGAL_OFFICER'
                ) THEN 1 ELSE 0 END,
                UpdatedAt = SYSUTCDATETIME()
            FROM role_groups legacy
            WHERE legacy.Code = N'HR_ADMIN';

            UPDATE translations SET Value = N'Phát triển kinh doanh / CRM', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.crm' AND LanguageCode = N'vi' AND Value = N'Kinh doanh / CRM';
            UPDATE translations SET Value = N'Business Development / CRM', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.crm' AND LanguageCode = N'en' AND Value = N'Sales / CRM';
            UPDATE translations SET Value = N'业务发展 / CRM', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.crm' AND LanguageCode = N'zh' AND Value = N'销售 / CRM';
            UPDATE translations SET Value = N'事業開発 / CRM', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.crm' AND LanguageCode = N'ja' AND Value = N'営業 / CRM';

            UPDATE translations SET Value = N'Quản lý dự án – Kỹ thuật thi công', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.construction' AND LanguageCode = N'vi' AND Value = N'Quản lý thi công';
            UPDATE translations SET Value = N'Construction Management & Engineering', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.construction' AND LanguageCode = N'en' AND Value = N'Construction Management';
            UPDATE translations SET Value = N'项目管理与施工技术', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.construction' AND LanguageCode = N'zh' AND Value = N'施工管理';
            UPDATE translations SET Value = N'プロジェクト管理・施工技術', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.construction' AND LanguageCode = N'ja' AND Value = N'施工管理';

            UPDATE translations SET Value = N'Kế toán – Tài chính – Nhân sự', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.finance' AND LanguageCode = N'vi' AND Value = N'Tài chính kế toán';
            UPDATE translations SET Value = N'Accounting, Finance & Human Resources', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.finance' AND LanguageCode = N'en' AND Value = N'Finance & Accounting';
            UPDATE translations SET Value = N'会计、财务与人力资源', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.finance' AND LanguageCode = N'zh' AND Value = N'财务与会计';
            UPDATE translations SET Value = N'会計・財務・人事', UpdatedAt = SYSUTCDATETIME()
            WHERE [Key] = N'rbac.group.finance' AND LanguageCode = N'ja' AND Value = N'財務・会計';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE roles
            SET RoleGroupId = (SELECT Id FROM role_groups WHERE Code = N'HR_ADMIN'),
                UpdatedAt = SYSUTCDATETIME()
            WHERE Code = N'LEGAL_OFFICER'
              AND EXISTS (SELECT 1 FROM role_groups WHERE Code = N'HR_ADMIN');

            UPDATE roles
            SET RoleGroupId = (SELECT Id FROM role_groups WHERE Code = N'CONSTRUCTION'),
                UpdatedAt = SYSUTCDATETIME()
            WHERE Code IN (N'PROCUREMENT', N'WAREHOUSE');

            UPDATE role_groups SET IsActive = 1, UpdatedAt = SYSUTCDATETIME() WHERE Code = N'HR_ADMIN';
            DELETE FROM role_groups
            WHERE Code = N'PROCUREMENT'
              AND NOT EXISTS (SELECT 1 FROM roles WHERE RoleGroupId = role_groups.Id)
              AND NOT EXISTS (SELECT 1 FROM role_group_permissions WHERE RoleGroupId = role_groups.Id);
            """);

        migrationBuilder.DropColumn(
            name: "InitialPermissionsSeeded",
            table: "role_groups");
    }
}
