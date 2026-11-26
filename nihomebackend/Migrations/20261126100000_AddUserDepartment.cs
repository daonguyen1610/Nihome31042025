using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartmentRoleGroupId",
                table: "users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_DepartmentRoleGroupId",
                table: "users",
                column: "DepartmentRoleGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_users_role_groups_DepartmentRoleGroupId",
                table: "users",
                column: "DepartmentRoleGroupId",
                principalTable: "role_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Existing people take the department of their current role, so nothing changes for them.
            migrationBuilder.Sql("""
                UPDATE users
                SET DepartmentRoleGroupId = (SELECT r.RoleGroupId FROM roles r WHERE r.Id = users.RoleEntityId)
                WHERE EXISTS (SELECT 1 FROM roles r WHERE r.Id = users.RoleEntityId AND r.RoleGroupId IS NOT NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_role_groups_DepartmentRoleGroupId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_DepartmentRoleGroupId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DepartmentRoleGroupId",
                table: "users");
        }
    }
}
