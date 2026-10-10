using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoleGroupId",
                table: "roles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "role_groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LabelKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "role_group_permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleGroupId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_group_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_role_group_permissions_permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_group_permissions_role_groups_RoleGroupId",
                        column: x => x.RoleGroupId,
                        principalTable: "role_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_roles_RoleGroupId",
                table: "roles",
                column: "RoleGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_role_group_permissions_PermissionId",
                table: "role_group_permissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_role_group_permissions_RoleGroupId_PermissionId",
                table: "role_group_permissions",
                columns: new[] { "RoleGroupId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_groups_Code",
                table: "role_groups",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_roles_role_groups_RoleGroupId",
                table: "roles",
                column: "RoleGroupId",
                principalTable: "role_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_roles_role_groups_RoleGroupId",
                table: "roles");

            migrationBuilder.DropTable(
                name: "role_group_permissions");

            migrationBuilder.DropTable(
                name: "role_groups");

            migrationBuilder.DropIndex(
                name: "IX_roles_RoleGroupId",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "RoleGroupId",
                table: "roles");
        }
    }
}
