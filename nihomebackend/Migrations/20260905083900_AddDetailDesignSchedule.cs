using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddDetailDesignSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "design_schedule_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    DesignProjectId = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_design_schedule_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_design_schedule_history_design_projects_DesignProjectId",
                        column: x => x.DesignProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_design_schedule_history_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "design_schedule_phases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    DesignProjectId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlannedStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PlannedEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualStart = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProgressPercent = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_design_schedule_phases", x => x.Id);
                    table.CheckConstraint("CK_design_schedule_phases_planned_dates", "[PlannedEnd] >= [PlannedStart]");
                    table.CheckConstraint("CK_design_schedule_phases_progress", "[ProgressPercent] >= 0 AND [ProgressPercent] <= 100");
                    table.CheckConstraint("CK_design_schedule_phases_weight", "[Weight] >= 1 AND [Weight] <= 100");
                    table.ForeignKey(
                        name: "FK_design_schedule_phases_design_projects_DesignProjectId",
                        column: x => x.DesignProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_design_schedule_phases_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "design_schedule_tasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    DesignProjectId = table.Column<int>(type: "int", nullable: false),
                    PhaseId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DepartmentCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AssigneeMemberId = table.Column<int>(type: "int", nullable: false),
                    IsMilestone = table.Column<bool>(type: "bit", nullable: false),
                    PlannedStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PlannedEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualStart = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProgressPercent = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_design_schedule_tasks", x => x.Id);
                    table.CheckConstraint("CK_design_schedule_tasks_milestone", "[IsMilestone] = 0 OR [PlannedStart] = [PlannedEnd]");
                    table.CheckConstraint("CK_design_schedule_tasks_planned_dates", "[PlannedEnd] >= [PlannedStart]");
                    table.CheckConstraint("CK_design_schedule_tasks_progress", "[ProgressPercent] >= 0 AND [ProgressPercent] <= 100");
                    table.CheckConstraint("CK_design_schedule_tasks_weight", "[Weight] >= 1 AND [Weight] <= 100");
                    table.ForeignKey(
                        name: "FK_design_schedule_tasks_design_projects_DesignProjectId",
                        column: x => x.DesignProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_design_schedule_tasks_design_schedule_phases_PhaseId",
                        column: x => x.PhaseId,
                        principalTable: "design_schedule_phases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_design_schedule_tasks_operational_project_members_AssigneeMemberId",
                        column: x => x.AssigneeMemberId,
                        principalTable: "operational_project_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_design_schedule_tasks_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "design_schedule_task_dependencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    PredecessorTaskId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_design_schedule_task_dependencies", x => x.Id);
                    table.CheckConstraint("CK_design_schedule_task_dependencies_not_self", "[TaskId] <> [PredecessorTaskId]");
                    table.ForeignKey(
                        name: "FK_design_schedule_task_dependencies_design_schedule_tasks_PredecessorTaskId",
                        column: x => x.PredecessorTaskId,
                        principalTable: "design_schedule_tasks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_design_schedule_task_dependencies_design_schedule_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "design_schedule_tasks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_history_ChangedByUserId",
                table: "design_schedule_history",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_history_DesignProjectId",
                table: "design_schedule_history",
                column: "DesignProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_history_OperationalProjectId_ChangedAt",
                table: "design_schedule_history",
                columns: new[] { "OperationalProjectId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_phases_DesignProjectId_Code",
                table: "design_schedule_phases",
                columns: new[] { "DesignProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_phases_OperationalProjectId_Code",
                table: "design_schedule_phases",
                columns: new[] { "OperationalProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_task_dependencies_OperationalProjectId",
                table: "design_schedule_task_dependencies",
                column: "OperationalProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_task_dependencies_PredecessorTaskId",
                table: "design_schedule_task_dependencies",
                column: "PredecessorTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_task_dependencies_TaskId_PredecessorTaskId",
                table: "design_schedule_task_dependencies",
                columns: new[] { "TaskId", "PredecessorTaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_AssigneeMemberId",
                table: "design_schedule_tasks",
                column: "AssigneeMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_DesignProjectId",
                table: "design_schedule_tasks",
                column: "DesignProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_OperationalProjectId_AssigneeMemberId",
                table: "design_schedule_tasks",
                columns: new[] { "OperationalProjectId", "AssigneeMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_OperationalProjectId_Code",
                table: "design_schedule_tasks",
                columns: new[] { "OperationalProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_OperationalProjectId_DepartmentCode",
                table: "design_schedule_tasks",
                columns: new[] { "OperationalProjectId", "DepartmentCode" });

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_OperationalProjectId_PhaseId_PlannedStart_PlannedEnd",
                table: "design_schedule_tasks",
                columns: new[] { "OperationalProjectId", "PhaseId", "PlannedStart", "PlannedEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_OperationalProjectId_Status",
                table: "design_schedule_tasks",
                columns: new[] { "OperationalProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_PhaseId",
                table: "design_schedule_tasks",
                column: "PhaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "design_schedule_history");

            migrationBuilder.DropTable(
                name: "design_schedule_task_dependencies");

            migrationBuilder.DropTable(
                name: "design_schedule_tasks");

            migrationBuilder.DropTable(
                name: "design_schedule_phases");
        }
    }
}
