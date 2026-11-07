using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// Dossier rights move from global RBAC codes to a per-member View/Create/
/// Update/Delete matrix edited by the design lead/manager. Backfills a row for
/// every active design-project member (and root members with a project-scope
/// role) using the same defaults the team service applies to new members.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261108140000_AddDesignProjectMemberPermissions")]
public partial class AddDesignProjectMemberPermissions : Migration
{
    private const string CreateTableSql = """
        IF OBJECT_ID(N'design_project_member_permissions', N'U') IS NULL
        BEGIN
            CREATE TABLE design_project_member_permissions (
                Id int IDENTITY(1,1) NOT NULL,
                MemberId int NOT NULL,
                CanView bit NOT NULL CONSTRAINT DF_dpmp_CanView DEFAULT CAST(1 AS bit),
                CanCreate bit NOT NULL CONSTRAINT DF_dpmp_CanCreate DEFAULT CAST(0 AS bit),
                CanUpdate bit NOT NULL CONSTRAINT DF_dpmp_CanUpdate DEFAULT CAST(0 AS bit),
                CanDelete bit NOT NULL CONSTRAINT DF_dpmp_CanDelete DEFAULT CAST(0 AS bit),
                CreatedAt datetime2 NOT NULL,
                CreatedByUserId int NOT NULL,
                UpdatedAt datetime2 NOT NULL,
                UpdatedByUserId int NOT NULL,
                CONSTRAINT PK_design_project_member_permissions PRIMARY KEY (Id),
                CONSTRAINT FK_design_project_member_permissions_members_MemberId
                    FOREIGN KEY (MemberId) REFERENCES operational_project_members (Id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IX_design_project_member_permissions_MemberId
                ON design_project_member_permissions (MemberId);
        END;
        """;

    private const string BackfillSql = """
        INSERT INTO design_project_member_permissions
            (MemberId, CanView, CanCreate, CanUpdate, CanDelete,
             CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId)
        SELECT
            m.Id,
            CAST(1 AS bit),
            CASE WHEN EXISTS (
                SELECT 1
                FROM operational_project_member_roles r
                WHERE r.MemberId = m.Id AND r.EndedAt IS NULL
                  AND r.RoleCode IN (N'DesignLead', N'ProjectManager')
                  AND (r.Scope = N'Project'
                       OR (r.Scope = N'Module' AND r.ScopeValue = N'Design')
                       OR r.Scope = N'Discipline')
            ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
            CASE WHEN EXISTS (
                SELECT 1
                FROM operational_project_member_roles r
                WHERE r.MemberId = m.Id AND r.EndedAt IS NULL
                  AND r.RoleCode IN (N'DesignLead', N'ProjectManager', N'Architect',
                                     N'StructuralEngineer', N'MepEngineer', N'InteriorDesigner')
                  AND (r.Scope = N'Project'
                       OR (r.Scope = N'Module' AND r.ScopeValue = N'Design')
                       OR r.Scope = N'Discipline')
            ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
            CASE WHEN EXISTS (
                SELECT 1
                FROM operational_project_member_roles r
                WHERE r.MemberId = m.Id AND r.EndedAt IS NULL
                  AND r.RoleCode IN (N'DesignLead', N'ProjectManager')
                  AND (r.Scope = N'Project'
                       OR (r.Scope = N'Module' AND r.ScopeValue = N'Design')
                       OR r.Scope = N'Discipline')
            ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
            SYSUTCDATETIME(),
            m.CreatedByUserId,
            SYSUTCDATETIME(),
            m.UpdatedByUserId
        FROM operational_project_members m
        WHERE m.EndedAt IS NULL
          AND (m.DesignProjectId IS NOT NULL
               OR EXISTS (
                   SELECT 1 FROM operational_project_member_roles r
                   WHERE r.MemberId = m.Id AND r.EndedAt IS NULL AND r.Scope = N'Project'))
          AND NOT EXISTS (
              SELECT 1 FROM design_project_member_permissions existing
              WHERE existing.MemberId = m.Id);
        """;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CreateTableSql);
        migrationBuilder.Sql(BackfillSql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'design_project_member_permissions', N'U') IS NOT NULL
                DROP TABLE design_project_member_permissions;
            """);
    }
}
