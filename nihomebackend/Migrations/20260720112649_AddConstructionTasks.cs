using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddConstructionTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "construction_tasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesignProjectId = table.Column<int>(type: "int", nullable: false),
                    TaskCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Wbs = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PlannedStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PlannedEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualStart = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    ProgressPercent = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_construction_tasks_design_projects_DesignProjectId",
                        column: x => x.DesignProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_construction_tasks_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "construction_task_dependencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    PredecessorTaskId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_task_dependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_construction_task_dependencies_construction_tasks_PredecessorTaskId",
                        column: x => x.PredecessorTaskId,
                        principalTable: "construction_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_construction_task_dependencies_construction_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "construction_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_construction_task_dependencies_PredecessorTaskId",
                table: "construction_task_dependencies",
                column: "PredecessorTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_task_dependencies_TaskId_PredecessorTaskId",
                table: "construction_task_dependencies",
                columns: new[] { "TaskId", "PredecessorTaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_construction_tasks_DesignProjectId",
                table: "construction_tasks",
                column: "DesignProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_tasks_DesignProjectId_TaskCode",
                table: "construction_tasks",
                columns: new[] { "DesignProjectId", "TaskCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_construction_tasks_OwnerUserId",
                table: "construction_tasks",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_tasks_Status",
                table: "construction_tasks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "construction_task_dependencies");

            migrationBuilder.DropTable(
                name: "construction_tasks");
        }
    }
}
