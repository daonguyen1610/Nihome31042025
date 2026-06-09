using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectCategoryId",
                table: "projects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActivityCategoryId",
                table: "activities",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "project_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_categories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_projects_ProjectCategoryId",
                table: "projects",
                column: "ProjectCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_activities_ActivityCategoryId",
                table: "activities",
                column: "ActivityCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_project_categories_Name",
                table: "project_categories",
                column: "Name",
                unique: true);

            // Backfill: ensure a ProjectCategory row exists for every distinct projects.Category value.
            migrationBuilder.Sql(@"
                INSERT INTO project_categories (Name, IsActive, SortOrder, CreatedAt, UpdatedAt)
                SELECT DISTINCT LTRIM(RTRIM(p.Category)), 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM projects p
                WHERE p.Category IS NOT NULL
                  AND LTRIM(RTRIM(p.Category)) <> ''
                  AND NOT EXISTS (
                    SELECT 1 FROM project_categories c
                    WHERE LOWER(c.Name) = LOWER(LTRIM(RTRIM(p.Category)))
                  );");

            // Backfill: ensure an ActivityCategory row exists for every distinct activities.Category value.
            migrationBuilder.Sql(@"
                INSERT INTO activity_categories (Name, IsActive, SortOrder, CreatedAt, UpdatedAt)
                SELECT DISTINCT LTRIM(RTRIM(a.Category)), 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM activities a
                WHERE a.Category IS NOT NULL
                  AND LTRIM(RTRIM(a.Category)) <> ''
                  AND NOT EXISTS (
                    SELECT 1 FROM activity_categories c
                    WHERE LOWER(c.Name) = LOWER(LTRIM(RTRIM(a.Category)))
                  );");

            // Backfill FK columns by matching the denormalized name string.
            migrationBuilder.Sql(@"
                UPDATE a SET a.ActivityCategoryId = c.Id
                FROM activities a
                INNER JOIN activity_categories c
                  ON LOWER(LTRIM(RTRIM(a.Category))) = LOWER(c.Name)
                WHERE a.ActivityCategoryId IS NULL;");

            migrationBuilder.Sql(@"
                UPDATE p SET p.ProjectCategoryId = c.Id
                FROM projects p
                INNER JOIN project_categories c
                  ON LOWER(LTRIM(RTRIM(p.Category))) = LOWER(c.Name)
                WHERE p.ProjectCategoryId IS NULL
                  AND p.Category IS NOT NULL
                  AND LTRIM(RTRIM(p.Category)) <> '';");

            migrationBuilder.AddForeignKey(
                name: "FK_activities_activity_categories_ActivityCategoryId",
                table: "activities",
                column: "ActivityCategoryId",
                principalTable: "activity_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_projects_project_categories_ProjectCategoryId",
                table: "projects",
                column: "ProjectCategoryId",
                principalTable: "project_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_activities_activity_categories_ActivityCategoryId",
                table: "activities");

            migrationBuilder.DropForeignKey(
                name: "FK_projects_project_categories_ProjectCategoryId",
                table: "projects");

            migrationBuilder.DropTable(
                name: "project_categories");

            migrationBuilder.DropIndex(
                name: "IX_projects_ProjectCategoryId",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_activities_ActivityCategoryId",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "ProjectCategoryId",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ActivityCategoryId",
                table: "activities");
        }
    }
}
