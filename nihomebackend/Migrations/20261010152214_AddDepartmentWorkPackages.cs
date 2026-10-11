using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentWorkPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "department_work_packages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    SourceOpportunityId = table.Column<int>(type: "int", nullable: true),
                    DepartmentRoleGroupId = table.Column<int>(type: "int", nullable: false),
                    LeadUserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlannedStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlannedEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_work_packages", x => x.Id);
                    table.CheckConstraint("CK_department_work_packages_dates", "[PlannedStart] IS NULL OR [PlannedEnd] IS NULL OR [PlannedEnd] >= [PlannedStart]");
                    table.CheckConstraint("CK_department_work_packages_scope", "([Kind] = 'Project' AND [OperationalProjectId] IS NOT NULL) OR ([Kind] = 'Internal' AND [OperationalProjectId] IS NULL AND [ContractId] IS NULL AND [SourceOpportunityId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_department_work_packages_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_work_packages_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_work_packages_opportunities_SourceOpportunityId",
                        column: x => x.SourceOpportunityId,
                        principalTable: "opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_work_packages_role_groups_DepartmentRoleGroupId",
                        column: x => x.DepartmentRoleGroupId,
                        principalTable: "role_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_work_packages_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_department_work_packages_users_LeadUserId",
                        column: x => x.LeadUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "department_work_package_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkPackageId = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_work_package_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_department_work_package_history_department_work_packages_WorkPackageId",
                        column: x => x.WorkPackageId,
                        principalTable: "department_work_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_department_work_package_history_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "department_work_tasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkPackageId = table.Column<int>(type: "int", nullable: false),
                    WbsCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Resources = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AssigneeUserId = table.Column<int>(type: "int", nullable: false),
                    ApproverUserId = table.Column<int>(type: "int", nullable: true),
                    KpiDefinitionId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProgressPercent = table.Column<int>(type: "int", nullable: false),
                    PlannedStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlannedEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_work_tasks", x => x.Id);
                    table.CheckConstraint("CK_department_work_tasks_approver", "[ApproverUserId] IS NULL OR [ApproverUserId] <> [AssigneeUserId]");
                    table.CheckConstraint("CK_department_work_tasks_dates", "[PlannedEnd] >= [PlannedStart]");
                    table.CheckConstraint("CK_department_work_tasks_progress", "[ProgressPercent] >= 0 AND [ProgressPercent] <= 100");
                    table.ForeignKey(
                        name: "FK_department_work_tasks_department_work_packages_WorkPackageId",
                        column: x => x.WorkPackageId,
                        principalTable: "department_work_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_department_work_tasks_kpi_definitions_KpiDefinitionId",
                        column: x => x.KpiDefinitionId,
                        principalTable: "kpi_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_work_tasks_users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_department_work_tasks_users_AssigneeUserId",
                        column: x => x.AssigneeUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_work_tasks_users_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "department_work_task_cc_users",
                columns: table => new
                {
                    WorkTaskId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_work_task_cc_users", x => new { x.WorkTaskId, x.UserId });
                    table.ForeignKey(
                        name: "FK_department_work_task_cc_users_department_work_tasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalTable: "department_work_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_department_work_task_cc_users_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "department_work_task_dependencies",
                columns: table => new
                {
                    WorkTaskId = table.Column<int>(type: "int", nullable: false),
                    PredecessorTaskId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_work_task_dependencies", x => new { x.WorkTaskId, x.PredecessorTaskId });
                    table.CheckConstraint("CK_department_work_task_dependencies_not_self", "[WorkTaskId] <> [PredecessorTaskId]");
                    table.ForeignKey(
                        name: "FK_department_work_task_dependencies_department_work_tasks_PredecessorTaskId",
                        column: x => x.PredecessorTaskId,
                        principalTable: "department_work_tasks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_department_work_task_dependencies_department_work_tasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalTable: "department_work_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_department_work_package_history_ChangedByUserId",
                table: "department_work_package_history",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_department_work_package_history_WorkPackageId_ChangedAt",
                table: "department_work_package_history",
                columns: new[] { "WorkPackageId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_department_work_packages_Code",
                table: "department_work_packages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_department_work_packages_ContractId_Status",
                table: "department_work_packages",
                columns: new[] { "ContractId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_department_work_packages_CreatedByUserId",
                table: "department_work_packages",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_department_work_packages_DepartmentRoleGroupId",
                table: "department_work_packages",
                column: "DepartmentRoleGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_department_work_packages_Kind_DepartmentRoleGroupId_Status",
                table: "department_work_packages",
                columns: new[] { "Kind", "DepartmentRoleGroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_department_work_packages_LeadUserId",
                table: "department_work_packages",
                column: "LeadUserId");

            migrationBuilder.CreateIndex(
                name: "IX_department_work_packages_OperationalProjectId_DepartmentRoleGroupId_Status",
                table: "department_work_packages",
                columns: new[] { "OperationalProjectId", "DepartmentRoleGroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_department_work_packages_SourceOpportunityId",
                table: "department_work_packages",
                column: "SourceOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_department_work_task_cc_users_UserId",
                table: "department_work_task_cc_users",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_department_work_task_dependencies_PredecessorTaskId",
                table: "department_work_task_dependencies",
                column: "PredecessorTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_department_work_tasks_ApproverUserId",
                table: "department_work_tasks",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_department_work_tasks_AssigneeUserId_Status_PlannedEnd",
                table: "department_work_tasks",
                columns: new[] { "AssigneeUserId", "Status", "PlannedEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_department_work_tasks_KpiDefinitionId_VerifiedAt",
                table: "department_work_tasks",
                columns: new[] { "KpiDefinitionId", "VerifiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_department_work_tasks_VerifiedByUserId",
                table: "department_work_tasks",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_department_work_tasks_WorkPackageId_WbsCode",
                table: "department_work_tasks",
                columns: new[] { "WorkPackageId", "WbsCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "department_work_package_history");

            migrationBuilder.DropTable(
                name: "department_work_task_cc_users");

            migrationBuilder.DropTable(
                name: "department_work_task_dependencies");

            migrationBuilder.DropTable(
                name: "department_work_tasks");

            migrationBuilder.DropTable(
                name: "department_work_packages");
        }
    }
}
