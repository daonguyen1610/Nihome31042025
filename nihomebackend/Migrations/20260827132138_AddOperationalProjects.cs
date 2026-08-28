using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OperationalProjectId",
                table: "quotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OperationalProjectId",
                table: "opportunities",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OperationalProjectId",
                table: "design_projects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OperationalProjectId",
                table: "contracts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "operational_projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ProjectManagerUserId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operational_projects_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_projects_users_ProjectManagerUserId",
                        column: x => x.ProjectManagerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Preserve every existing business chain without guessing that
            // unrelated contracts or opportunities belong to one project.
            // Prefer the strongest existing aggregate (DesignProject), then
            // create one project per still-unlinked Contract or Opportunity.
            migrationBuilder.Sql("""
                INSERT INTO operational_projects
                    (Code, Name, CustomerId, ProjectManagerUserId, Status,
                     StartDate, EndDate, Note, CreatedAt, CreatedByUserId,
                     UpdatedAt, UpdatedByUserId)
                SELECT CONCAT('PJ-LEGACY-DP-', dp.Id), dp.Name, dp.CustomerId,
                       dp.ProjectManagerUserId,
                       CASE dp.Status
                           WHEN 'Active' THEN 'Active'
                           WHEN 'OnHold' THEN 'OnHold'
                           WHEN 'Completed' THEN 'Completed'
                           WHEN 'Cancelled' THEN 'Cancelled'
                           ELSE 'Planning'
                       END,
                       dp.StartDate, dp.Deadline,
                       CONCAT(N'Backfill từ Dự án thiết kế ', dp.ProjectCode, N'.'),
                       dp.CreatedAt, dp.CreatedByUserId,
                       dp.UpdatedAt, dp.UpdatedByUserId
                FROM design_projects dp;

                UPDATE dp
                SET OperationalProjectId = project.Id
                FROM design_projects dp
                INNER JOIN operational_projects project
                    ON project.Code = CONCAT('PJ-LEGACY-DP-', dp.Id);

                UPDATE contract
                SET OperationalProjectId = dp.OperationalProjectId
                FROM contracts contract
                INNER JOIN design_projects dp ON dp.ContractId = contract.Id
                WHERE contract.OperationalProjectId IS NULL;

                INSERT INTO operational_projects
                    (Code, Name, CustomerId, ProjectManagerUserId, Status,
                     StartDate, EndDate, Note, CreatedAt, CreatedByUserId,
                     UpdatedAt, UpdatedByUserId)
                SELECT CONCAT('PJ-LEGACY-CT-', contract.Id),
                       CONCAT(N'Dự án hợp đồng ', contract.ContractNumber),
                       contract.CustomerId, contract.OwnerUserId,
                       CASE contract.Status
                           WHEN 'InProgress' THEN 'Active'
                           WHEN 'OnHold' THEN 'OnHold'
                           WHEN 'Completed' THEN 'Completed'
                           WHEN 'Cancelled' THEN 'Cancelled'
                           ELSE 'Planning'
                       END,
                       contract.StartDate, contract.EndDate,
                       CONCAT(N'Backfill từ Hợp đồng ', contract.ContractNumber, N'.'),
                       contract.CreatedAt, contract.CreatedByUserId,
                       contract.UpdatedAt, contract.UpdatedByUserId
                FROM contracts contract
                WHERE contract.OperationalProjectId IS NULL;

                UPDATE contract
                SET OperationalProjectId = project.Id
                FROM contracts contract
                INNER JOIN operational_projects project
                    ON project.Code = CONCAT('PJ-LEGACY-CT-', contract.Id)
                WHERE contract.OperationalProjectId IS NULL;

                UPDATE opportunity
                SET OperationalProjectId = contract.OperationalProjectId
                FROM opportunities opportunity
                INNER JOIN contracts contract ON contract.OpportunityId = opportunity.Id
                WHERE opportunity.OperationalProjectId IS NULL;

                INSERT INTO operational_projects
                    (Code, Name, CustomerId, ProjectManagerUserId, Status,
                     StartDate, EndDate, Note, CreatedAt, CreatedByUserId,
                     UpdatedAt, UpdatedByUserId)
                SELECT CONCAT('PJ-LEGACY-OP-', opportunity.Id), opportunity.Name,
                       opportunity.CustomerId, opportunity.OwnerUserId,
                       CASE WHEN opportunity.Stage = 'Won' THEN 'Active'
                            WHEN opportunity.Stage = 'Lost' THEN 'Cancelled'
                            ELSE 'Planning' END,
                       NULL, opportunity.ExpectedCloseDate,
                       CONCAT(N'Backfill từ Cơ hội #', opportunity.Id, N'.'),
                       opportunity.CreatedAt, opportunity.CreatedByUserId,
                       opportunity.UpdatedAt, opportunity.UpdatedByUserId
                FROM opportunities opportunity
                WHERE opportunity.OperationalProjectId IS NULL;

                UPDATE opportunity
                SET OperationalProjectId = project.Id
                FROM opportunities opportunity
                INNER JOIN operational_projects project
                    ON project.Code = CONCAT('PJ-LEGACY-OP-', opportunity.Id)
                WHERE opportunity.OperationalProjectId IS NULL;

                UPDATE quote
                SET OperationalProjectId = opportunity.OperationalProjectId
                FROM quotes quote
                INNER JOIN opportunities opportunity
                    ON opportunity.Id = quote.OpportunityId
                WHERE quote.OperationalProjectId IS NULL;
                """);

            // Fresh installations receive these grants from rbac-defaults.json.
            // Existing installations have already frozen their initial role
            // grants, so add only the new feature permissions without touching
            // any administrator-customised permission.
            migrationBuilder.Sql("""
                INSERT INTO permissions (Module, Action, DescriptionKey, IsActive, CreatedAt)
                SELECT 'operations.projects', source.Action,
                       CONCAT('rbac.perm.operations.projects.', source.Action),
                       1, SYSUTCDATETIME()
                FROM (VALUES ('view'), ('view.all'), ('manage')) source(Action)
                WHERE NOT EXISTS (
                    SELECT 1 FROM permissions permission
                    WHERE permission.Module = 'operations.projects'
                      AND permission.Action = source.Action
                );

                INSERT INTO role_permissions (RoleId, PermissionId, CreatedAt)
                SELECT role.Id, permission.Id, SYSUTCDATETIME()
                FROM roles role
                CROSS JOIN permissions permission
                WHERE permission.Module = 'operations.projects'
                  AND (
                    (permission.Action = 'view' AND role.Code IN
                        ('SUPER_ADMIN','ADMIN','SALE','SALES_MANAGER','DESIGN',
                         'DESIGN_LEAD','ARCHITECT','MEP_ENGINEER','STRUCT_ENGINEER',
                         'PM','LEGAL_OFFICER','QS','ACCOUNTANT','WAREHOUSE','BGD'))
                    OR (permission.Action = 'view.all' AND role.Code IN
                        ('SUPER_ADMIN','ADMIN','SALES_MANAGER','ACCOUNTANT','BGD'))
                    OR (permission.Action = 'manage' AND role.Code IN
                        ('SUPER_ADMIN','ADMIN','SALE','SALES_MANAGER','PM'))
                  )
                  AND NOT EXISTS (
                    SELECT 1 FROM role_permissions existing
                    WHERE existing.RoleId = role.Id
                      AND existing.PermissionId = permission.Id
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_quotes_OperationalProjectId",
                table: "quotes",
                column: "OperationalProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_opportunities_OperationalProjectId",
                table: "opportunities",
                column: "OperationalProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_design_projects_OperationalProjectId",
                table: "design_projects",
                column: "OperationalProjectId",
                unique: true,
                filter: "[OperationalProjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_OperationalProjectId",
                table: "contracts",
                column: "OperationalProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_projects_Code",
                table: "operational_projects",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_projects_CustomerId",
                table: "operational_projects",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_projects_ProjectManagerUserId",
                table: "operational_projects",
                column: "ProjectManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_projects_Status",
                table: "operational_projects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_operational_projects_UpdatedAt",
                table: "operational_projects",
                column: "UpdatedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_contracts_operational_projects_OperationalProjectId",
                table: "contracts",
                column: "OperationalProjectId",
                principalTable: "operational_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_design_projects_operational_projects_OperationalProjectId",
                table: "design_projects",
                column: "OperationalProjectId",
                principalTable: "operational_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_opportunities_operational_projects_OperationalProjectId",
                table: "opportunities",
                column: "OperationalProjectId",
                principalTable: "operational_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_quotes_operational_projects_OperationalProjectId",
                table: "quotes",
                column: "OperationalProjectId",
                principalTable: "operational_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE rolePermission
                FROM role_permissions rolePermission
                INNER JOIN permissions permission
                    ON permission.Id = rolePermission.PermissionId
                WHERE permission.Module = 'operations.projects';

                DELETE FROM permissions WHERE Module = 'operations.projects';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_contracts_operational_projects_OperationalProjectId",
                table: "contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_design_projects_operational_projects_OperationalProjectId",
                table: "design_projects");

            migrationBuilder.DropForeignKey(
                name: "FK_opportunities_operational_projects_OperationalProjectId",
                table: "opportunities");

            migrationBuilder.DropForeignKey(
                name: "FK_quotes_operational_projects_OperationalProjectId",
                table: "quotes");

            migrationBuilder.DropTable(
                name: "operational_projects");

            migrationBuilder.DropIndex(
                name: "IX_quotes_OperationalProjectId",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "IX_opportunities_OperationalProjectId",
                table: "opportunities");

            migrationBuilder.DropIndex(
                name: "IX_design_projects_OperationalProjectId",
                table: "design_projects");

            migrationBuilder.DropIndex(
                name: "IX_contracts_OperationalProjectId",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "OperationalProjectId",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "OperationalProjectId",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "OperationalProjectId",
                table: "design_projects");

            migrationBuilder.DropColumn(
                name: "OperationalProjectId",
                table: "contracts");
        }
    }
}
