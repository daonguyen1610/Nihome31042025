using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkTaskSubtasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentTaskId",
                table: "department_work_tasks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_department_work_tasks_ParentTaskId",
                table: "department_work_tasks",
                column: "ParentTaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_department_work_tasks_department_work_tasks_ParentTaskId",
                table: "department_work_tasks",
                column: "ParentTaskId",
                principalTable: "department_work_tasks",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_department_work_tasks_department_work_tasks_ParentTaskId",
                table: "department_work_tasks");

            migrationBuilder.DropIndex(
                name: "IX_department_work_tasks_ParentTaskId",
                table: "department_work_tasks");

            migrationBuilder.DropColumn(
                name: "ParentTaskId",
                table: "department_work_tasks");
        }
    }
}
