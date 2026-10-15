using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectCodeAuditAndDesignVendors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DownstreamContractId",
                table: "design_schedule_tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VendorId",
                table: "design_schedule_tasks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "operational_project_code_changes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    OldCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NewCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DriveRenameStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DriveRenameAttemptCount = table.Column<int>(type: "int", nullable: false),
                    DriveRenameError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NextDriveRenameAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DriveRenamedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_project_code_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operational_project_code_changes_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_DownstreamContractId",
                table: "design_schedule_tasks",
                column: "DownstreamContractId");

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_VendorId",
                table: "design_schedule_tasks",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_code_changes_DriveRenameStatus_NextDriveRenameAttemptAt",
                table: "operational_project_code_changes",
                columns: new[] { "DriveRenameStatus", "NextDriveRenameAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_code_changes_OperationalProjectId_ChangedAt",
                table: "operational_project_code_changes",
                columns: new[] { "OperationalProjectId", "ChangedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_design_schedule_tasks_contracts_DownstreamContractId",
                table: "design_schedule_tasks",
                column: "DownstreamContractId",
                principalTable: "contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_design_schedule_tasks_procurement_vendors_VendorId",
                table: "design_schedule_tasks",
                column: "VendorId",
                principalTable: "procurement_vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                DELETE rgp
                FROM role_group_permissions rgp
                INNER JOIN role_groups rg ON rg.Id = rgp.RoleGroupId
                INNER JOIN permissions p ON p.Id = rgp.PermissionId
                INNER JOIN (VALUES
                    (N'workflow', N'view'),
                    (N'operations.projects', N'view'),
                    (N'work.packages', N'view'),
                    (N'work.packages', N'progress'),
                    (N'analytics.kpi', N'view'),
                    (N'reports.projects', N'view'),
                    (N'crm.customers', N'view'),
                    (N'crm.contracts', N'view'),
                    (N'proc.vendors', N'view'),
                    (N'finance.payments', N'view'),
                    (N'finance.periods', N'view'),
                    (N'finance.corrections', N'view')
                ) shipped(Module, Action) ON shipped.Module = p.Module AND shipped.Action = p.Action
                WHERE rg.Code = N'FINANCE';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_design_schedule_tasks_contracts_DownstreamContractId",
                table: "design_schedule_tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_design_schedule_tasks_procurement_vendors_VendorId",
                table: "design_schedule_tasks");

            migrationBuilder.DropTable(
                name: "operational_project_code_changes");

            migrationBuilder.DropIndex(
                name: "IX_design_schedule_tasks_DownstreamContractId",
                table: "design_schedule_tasks");

            migrationBuilder.DropIndex(
                name: "IX_design_schedule_tasks_VendorId",
                table: "design_schedule_tasks");

            migrationBuilder.DropColumn(
                name: "DownstreamContractId",
                table: "design_schedule_tasks");

            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "design_schedule_tasks");

            migrationBuilder.Sql(
                """
                INSERT INTO role_group_permissions (RoleGroupId, PermissionId, CreatedAt)
                SELECT rg.Id, p.Id, SYSUTCDATETIME()
                FROM role_groups rg
                CROSS JOIN (VALUES
                    (N'workflow', N'view'),
                    (N'operations.projects', N'view'),
                    (N'work.packages', N'view'),
                    (N'work.packages', N'progress'),
                    (N'analytics.kpi', N'view'),
                    (N'reports.projects', N'view'),
                    (N'crm.customers', N'view'),
                    (N'crm.contracts', N'view'),
                    (N'proc.vendors', N'view'),
                    (N'finance.payments', N'view'),
                    (N'finance.periods', N'view'),
                    (N'finance.corrections', N'view')
                ) shipped(Module, Action)
                INNER JOIN permissions p ON p.Module = shipped.Module AND p.Action = shipped.Action
                WHERE rg.Code = N'FINANCE'
                  AND NOT EXISTS (
                      SELECT 1 FROM role_group_permissions existing
                      WHERE existing.RoleGroupId = rg.Id AND existing.PermissionId = p.Id);
                """);
        }
    }
}
