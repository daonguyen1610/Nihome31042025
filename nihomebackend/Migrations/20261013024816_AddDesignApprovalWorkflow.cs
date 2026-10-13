using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddDesignApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "design_schedule_tasks",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotSubmitted");

            migrationBuilder.AddColumn<int>(
                name: "ApproverMemberId",
                table: "design_schedule_tasks",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE task
                SET ApproverMemberId = selected.MemberId
                FROM design_schedule_tasks AS task
                INNER JOIN design_projects AS designProject ON designProject.Id = task.DesignProjectId
                OUTER APPLY
                (
                    SELECT TOP (1) member.Id AS MemberId
                    FROM operational_project_members AS member
                    WHERE member.OperationalProjectId = task.OperationalProjectId
                      AND member.EndedAt IS NULL
                      AND member.UserId IN (designProject.DesignLeadUserId, designProject.ProjectManagerUserId)
                      AND member.UserId <> (
                          SELECT assignee.UserId
                          FROM operational_project_members AS assignee
                          WHERE assignee.Id = task.AssigneeMemberId)
                    ORDER BY CASE WHEN member.UserId = designProject.DesignLeadUserId THEN 0 ELSE 1 END,
                             member.Id
                ) AS selected;

                IF EXISTS (SELECT 1 FROM design_schedule_tasks WHERE ApproverMemberId IS NULL)
                    THROW 51000, 'Cannot backfill design task approvers. Assign an active Design Lead or Design Manager distinct from each task assignee before applying this migration.', 1;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "ApproverMemberId",
                table: "design_schedule_tasks",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "design_schedule_tasks",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCritical",
                table: "design_schedule_tasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ParentTaskId",
                table: "design_schedule_tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResourceSummary",
                table: "design_schedule_tasks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "design_schedule_tasks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "design_schedule_tasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByUserId",
                table: "design_schedule_tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "design_schedule_tasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubmittedByUserId",
                table: "design_schedule_tasks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "design_schedule_task_cc_members",
                columns: table => new
                {
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_design_schedule_task_cc_members", x => new { x.TaskId, x.MemberId });
                    table.ForeignKey(
                        name: "FK_design_schedule_task_cc_members_design_schedule_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "design_schedule_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_design_schedule_task_cc_members_operational_project_members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "operational_project_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_ApproverMemberId",
                table: "design_schedule_tasks",
                column: "ApproverMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_OperationalProjectId_ApproverMemberId_ApprovalStatus",
                table: "design_schedule_tasks",
                columns: new[] { "OperationalProjectId", "ApproverMemberId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_ParentTaskId",
                table: "design_schedule_tasks",
                column: "ParentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_task_cc_members_MemberId",
                table: "design_schedule_task_cc_members",
                column: "MemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_design_schedule_tasks_design_schedule_tasks_ParentTaskId",
                table: "design_schedule_tasks",
                column: "ParentTaskId",
                principalTable: "design_schedule_tasks",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_design_schedule_tasks_operational_project_members_ApproverMemberId",
                table: "design_schedule_tasks",
                column: "ApproverMemberId",
                principalTable: "operational_project_members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddCheckConstraint(
                name: "CK_design_schedule_tasks_approver",
                table: "design_schedule_tasks",
                sql: "[ApproverMemberId] <> [AssigneeMemberId]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_design_schedule_tasks_approver",
                table: "design_schedule_tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_design_schedule_tasks_design_schedule_tasks_ParentTaskId",
                table: "design_schedule_tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_design_schedule_tasks_operational_project_members_ApproverMemberId",
                table: "design_schedule_tasks");

            migrationBuilder.DropTable(
                name: "design_schedule_task_cc_members");

            migrationBuilder.DropIndex(
                name: "IX_design_schedule_tasks_ApproverMemberId",
                table: "design_schedule_tasks");

            migrationBuilder.DropIndex(
                name: "IX_design_schedule_tasks_OperationalProjectId_ApproverMemberId_ApprovalStatus",
                table: "design_schedule_tasks");

            migrationBuilder.DropIndex(
                name: "IX_design_schedule_tasks_ParentTaskId",
                table: "design_schedule_tasks");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "design_schedule_tasks");

            migrationBuilder.DropColumn(
                name: "ApproverMemberId",
                table: "design_schedule_tasks");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "design_schedule_tasks");

            migrationBuilder.DropColumn(
                name: "IsCritical",
                table: "design_schedule_tasks");

            migrationBuilder.DropColumn(
                name: "ParentTaskId",
                table: "design_schedule_tasks");

            migrationBuilder.DropColumn(
                name: "ResourceSummary",
                table: "design_schedule_tasks");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "design_schedule_tasks");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "design_schedule_tasks");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "design_schedule_tasks");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "design_schedule_tasks");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "design_schedule_tasks");
        }
    }
}
