using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddDriveTreeMigrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "drive_tree_migrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OldFoldersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewFoldersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedByUserId = table.Column<int>(type: "int", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drive_tree_migrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "drive_tree_migration_items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MigrationId = table.Column<int>(type: "int", nullable: false),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProjectDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    DriveFileId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FromFolderId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ToFolderId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SkipReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drive_tree_migration_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_drive_tree_migration_items_drive_tree_migrations_MigrationId",
                        column: x => x.MigrationId,
                        principalTable: "drive_tree_migrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_drive_tree_migration_items_MigrationId_Kind_OperationalProjectId_Category_DriveFileId",
                table: "drive_tree_migration_items",
                columns: new[] { "MigrationId", "Kind", "OperationalProjectId", "Category", "DriveFileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_drive_tree_migrations_Status",
                table: "drive_tree_migrations",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "drive_tree_migration_items");

            migrationBuilder.DropTable(
                name: "drive_tree_migrations");
        }
    }
}
