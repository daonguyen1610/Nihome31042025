using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class SoftDeleteWorkPackageTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DisabledAt",
                table: "department_work_tasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisabledByUserId",
                table: "department_work_tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisabledRootTaskId",
                table: "department_work_tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusBeforeDisabled",
                table: "department_work_tasks",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_department_work_tasks_DisabledByUserId",
                table: "department_work_tasks",
                column: "DisabledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_department_work_tasks_Status_DisabledAt_DisabledRootTaskId",
                table: "department_work_tasks",
                columns: new[] { "Status", "DisabledAt", "DisabledRootTaskId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_department_work_tasks_disabled_metadata",
                table: "department_work_tasks",
                sql: "([Status] = 'Disabled' AND [DisabledAt] IS NOT NULL AND [DisabledByUserId] IS NOT NULL AND [DisabledRootTaskId] IS NOT NULL AND [StatusBeforeDisabled] IS NOT NULL) OR ([Status] <> 'Disabled' AND [DisabledAt] IS NULL AND [DisabledByUserId] IS NULL AND [DisabledRootTaskId] IS NULL AND [StatusBeforeDisabled] IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_department_work_tasks_users_DisabledByUserId",
                table: "department_work_tasks",
                column: "DisabledByUserId",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_department_work_tasks_users_DisabledByUserId",
                table: "department_work_tasks");

            migrationBuilder.DropIndex(
                name: "IX_department_work_tasks_DisabledByUserId",
                table: "department_work_tasks");

            migrationBuilder.DropIndex(
                name: "IX_department_work_tasks_Status_DisabledAt_DisabledRootTaskId",
                table: "department_work_tasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_department_work_tasks_disabled_metadata",
                table: "department_work_tasks");

            migrationBuilder.DropColumn(
                name: "DisabledAt",
                table: "department_work_tasks");

            migrationBuilder.DropColumn(
                name: "DisabledByUserId",
                table: "department_work_tasks");

            migrationBuilder.DropColumn(
                name: "DisabledRootTaskId",
                table: "department_work_tasks");

            migrationBuilder.DropColumn(
                name: "StatusBeforeDisabled",
                table: "department_work_tasks");
        }
    }
}
