using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleDesignFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_design_projects_OperationalProjectId",
                table: "design_projects");

            migrationBuilder.CreateIndex(
                name: "IX_design_projects_OperationalProjectId",
                table: "design_projects",
                column: "OperationalProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM design_projects
                    WHERE OperationalProjectId IS NOT NULL
                    GROUP BY OperationalProjectId HAVING COUNT(*) > 1)
                    THROW 51000, 'Cannot restore one Design flow per project while multiple flows exist. Reconcile their project bindings before rollback; no flow will be deleted automatically.', 1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_design_projects_OperationalProjectId",
                table: "design_projects");

            migrationBuilder.CreateIndex(
                name: "IX_design_projects_OperationalProjectId",
                table: "design_projects",
                column: "OperationalProjectId",
                unique: true,
                filter: "[OperationalProjectId] IS NOT NULL");
        }
    }
}
