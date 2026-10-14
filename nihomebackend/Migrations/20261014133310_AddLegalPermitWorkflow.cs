using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261014133310_AddLegalPermitWorkflow")]
public partial class AddLegalPermitWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_permit_checklist_items_design_projects_DesignProjectId",
            table: "permit_checklist_items");
        migrationBuilder.DropIndex(
            name: "IX_permit_checklist_items_DesignProjectId_PermitTypeCode",
            table: "permit_checklist_items");

        migrationBuilder.AlterColumn<int>(
            name: "DesignProjectId", table: "permit_checklist_items", type: "int", nullable: true,
            oldClrType: typeof(int), oldType: "int");
        migrationBuilder.AddColumn<int>(
            name: "OperationalProjectId", table: "permit_checklist_items", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "WorkTaskId", table: "permit_checklist_items", type: "int", nullable: true);

        migrationBuilder.Sql("""
            UPDATE permit
            SET OperationalProjectId = designProject.OperationalProjectId
            FROM permit_checklist_items AS permit
            INNER JOIN design_projects AS designProject ON designProject.Id = permit.DesignProjectId
            WHERE permit.OperationalProjectId IS NULL
              AND designProject.OperationalProjectId IS NOT NULL;

            IF EXISTS (SELECT 1 FROM permit_checklist_items WHERE OperationalProjectId IS NULL)
                THROW 51001, 'LEGAL_PERMIT_OPERATIONAL_PROJECT_ORPHAN: Resolve permit rows without an OperationalProject before applying this migration.', 1;

            IF EXISTS (
                SELECT 1 FROM permit_checklist_items
                GROUP BY OperationalProjectId, PermitTypeCode
                HAVING COUNT(*) > 1
            )
                THROW 51002, 'LEGAL_PERMIT_TYPE_CONFLICT: Resolve duplicate permit types within an OperationalProject before applying this migration.', 1;
            """);

        migrationBuilder.AlterColumn<int>(
            name: "OperationalProjectId", table: "permit_checklist_items", type: "int", nullable: false,
            oldClrType: typeof(int), oldType: "int", oldNullable: true);

        migrationBuilder.CreateTable(
            name: "contract_legal_activations",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ContractId = table.Column<int>(type: "int", nullable: false),
                OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                WorkPackageId = table.Column<int>(type: "int", nullable: false),
                ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ActivatedByUserId = table.Column<int>(type: "int", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_contract_legal_activations", item => item.Id);
                table.ForeignKey("FK_contract_legal_activations_contracts_ContractId", item => item.ContractId,
                    "contracts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_contract_legal_activations_department_work_packages_WorkPackageId", item => item.WorkPackageId,
                    "department_work_packages", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_contract_legal_activations_operational_projects_OperationalProjectId", item => item.OperationalProjectId,
                    "operational_projects", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_contract_legal_activations_users_ActivatedByUserId", item => item.ActivatedByUserId,
                    "users", "Id");
            });

        migrationBuilder.CreateTable(
            name: "permit_checklist_history",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                PermitChecklistItemId = table.Column<int>(type: "int", nullable: false),
                FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ChangedByUserId = table.Column<int>(type: "int", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_permit_checklist_history", item => item.Id);
                table.ForeignKey("FK_permit_checklist_history_permit_checklist_items_PermitChecklistItemId",
                    item => item.PermitChecklistItemId, "permit_checklist_items", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_permit_checklist_history_users_ChangedByUserId",
                    item => item.ChangedByUserId, "users", "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_permit_checklist_items_OperationalProjectId_PermitTypeCode",
            table: "permit_checklist_items", columns: new[] { "OperationalProjectId", "PermitTypeCode" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_permit_checklist_items_WorkTaskId", table: "permit_checklist_items", column: "WorkTaskId",
            unique: true, filter: "[WorkTaskId] IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "IX_department_work_packages_OperationalProjectId_Purpose",
            table: "department_work_packages", columns: new[] { "OperationalProjectId", "Purpose" }, unique: true,
            filter: "[OperationalProjectId] IS NOT NULL AND [Purpose] = 'LegalPermitting'");
        migrationBuilder.CreateIndex(
            name: "IX_contract_legal_activations_ActivatedByUserId", table: "contract_legal_activations", column: "ActivatedByUserId");
        migrationBuilder.CreateIndex(
            name: "IX_contract_legal_activations_ContractId", table: "contract_legal_activations", column: "ContractId", unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_contract_legal_activations_OperationalProjectId_ActivatedAt", table: "contract_legal_activations",
            columns: new[] { "OperationalProjectId", "ActivatedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_contract_legal_activations_WorkPackageId", table: "contract_legal_activations", column: "WorkPackageId");
        migrationBuilder.CreateIndex(
            name: "IX_permit_checklist_history_ChangedByUserId", table: "permit_checklist_history", column: "ChangedByUserId");
        migrationBuilder.CreateIndex(
            name: "IX_permit_checklist_history_PermitChecklistItemId_ChangedAt", table: "permit_checklist_history",
            columns: new[] { "PermitChecklistItemId", "ChangedAt" });

        migrationBuilder.AddForeignKey(
            name: "FK_permit_checklist_items_department_work_tasks_WorkTaskId", table: "permit_checklist_items",
            column: "WorkTaskId", principalTable: "department_work_tasks", principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
        migrationBuilder.AddForeignKey(
            name: "FK_permit_checklist_items_design_projects_DesignProjectId", table: "permit_checklist_items",
            column: "DesignProjectId", principalTable: "design_projects", principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
        migrationBuilder.AddForeignKey(
            name: "FK_permit_checklist_items_operational_projects_OperationalProjectId", table: "permit_checklist_items",
            column: "OperationalProjectId", principalTable: "operational_projects", principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_permit_checklist_items_department_work_tasks_WorkTaskId", "permit_checklist_items");
        migrationBuilder.DropForeignKey("FK_permit_checklist_items_design_projects_DesignProjectId", "permit_checklist_items");
        migrationBuilder.DropForeignKey("FK_permit_checklist_items_operational_projects_OperationalProjectId", "permit_checklist_items");
        migrationBuilder.DropTable("contract_legal_activations");
        migrationBuilder.DropTable("permit_checklist_history");
        migrationBuilder.DropIndex("IX_permit_checklist_items_OperationalProjectId_PermitTypeCode", "permit_checklist_items");
        migrationBuilder.DropIndex("IX_permit_checklist_items_WorkTaskId", "permit_checklist_items");
        migrationBuilder.DropIndex("IX_department_work_packages_OperationalProjectId_Purpose", "department_work_packages");

        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM permit_checklist_items WHERE DesignProjectId IS NULL)
                THROW 51003, 'LEGAL_PERMIT_ROLLBACK_BLOCKED: Remediate permits without a legacy DesignProject mapping before rollback.', 1;
            """);

        migrationBuilder.DropColumn("OperationalProjectId", "permit_checklist_items");
        migrationBuilder.DropColumn("WorkTaskId", "permit_checklist_items");
        migrationBuilder.AlterColumn<int>(
            name: "DesignProjectId", table: "permit_checklist_items", type: "int", nullable: false,
            oldClrType: typeof(int), oldType: "int", oldNullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_permit_checklist_items_DesignProjectId_PermitTypeCode", table: "permit_checklist_items",
            columns: new[] { "DesignProjectId", "PermitTypeCode" }, unique: true);
        migrationBuilder.AddForeignKey(
            name: "FK_permit_checklist_items_design_projects_DesignProjectId", table: "permit_checklist_items",
            column: "DesignProjectId", principalTable: "design_projects", principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
