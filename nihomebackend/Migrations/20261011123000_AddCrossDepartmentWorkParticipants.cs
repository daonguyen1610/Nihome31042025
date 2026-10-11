using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// Records every department participating in an internal initiative and makes
/// the responsible department explicit on each work task.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261011123000_AddCrossDepartmentWorkParticipants")]
public partial class AddCrossDepartmentWorkParticipants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DepartmentRoleGroupId",
            table: "department_work_tasks",
            type: "int",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE task
            SET DepartmentRoleGroupId = package.DepartmentRoleGroupId
            FROM department_work_tasks task
            INNER JOIN department_work_packages package ON package.Id = task.WorkPackageId;
            """);

        migrationBuilder.AlterColumn<int>(
            name: "DepartmentRoleGroupId",
            table: "department_work_tasks",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.CreateTable(
            name: "department_work_package_participant_groups",
            columns: table => new
            {
                WorkPackageId = table.Column<int>(type: "int", nullable: false),
                RoleGroupId = table.Column<int>(type: "int", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_department_work_package_participant_groups",
                    x => new { x.WorkPackageId, x.RoleGroupId });
                table.ForeignKey(
                    name: "FK_department_work_package_participant_groups_department_work_packages_WorkPackageId",
                    column: x => x.WorkPackageId,
                    principalTable: "department_work_packages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_department_work_package_participant_groups_role_groups_RoleGroupId",
                    column: x => x.RoleGroupId,
                    principalTable: "role_groups",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.Sql("""
            INSERT INTO department_work_package_participant_groups (WorkPackageId, RoleGroupId)
            SELECT Id, DepartmentRoleGroupId
            FROM department_work_packages;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_department_work_tasks_WorkPackageId_DepartmentRoleGroupId_Status",
            table: "department_work_tasks",
            columns: new[] { "WorkPackageId", "DepartmentRoleGroupId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_department_work_tasks_DepartmentRoleGroupId",
            table: "department_work_tasks",
            column: "DepartmentRoleGroupId");

        migrationBuilder.CreateIndex(
            name: "IX_department_work_package_participant_groups_RoleGroupId",
            table: "department_work_package_participant_groups",
            column: "RoleGroupId");

        migrationBuilder.AddForeignKey(
            name: "FK_department_work_tasks_role_groups_DepartmentRoleGroupId",
            table: "department_work_tasks",
            column: "DepartmentRoleGroupId",
            principalTable: "role_groups",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "department_work_package_participant_groups");

        migrationBuilder.DropForeignKey(
            name: "FK_department_work_tasks_role_groups_DepartmentRoleGroupId",
            table: "department_work_tasks");

        migrationBuilder.DropIndex(
            name: "IX_department_work_tasks_WorkPackageId_DepartmentRoleGroupId_Status",
            table: "department_work_tasks");

        migrationBuilder.DropIndex(
            name: "IX_department_work_tasks_DepartmentRoleGroupId",
            table: "department_work_tasks");

        migrationBuilder.DropColumn(
            name: "DepartmentRoleGroupId",
            table: "department_work_tasks");
    }
}
