using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkPackageBoqSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceWorkPackageId",
                table: "project_boq_revisions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_boq_revisions_SourceWorkPackageId",
                table: "project_boq_revisions",
                column: "SourceWorkPackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_project_boq_revisions_department_work_packages_SourceWorkPackageId",
                table: "project_boq_revisions",
                column: "SourceWorkPackageId",
                principalTable: "department_work_packages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_project_boq_revisions_department_work_packages_SourceWorkPackageId",
                table: "project_boq_revisions");

            migrationBuilder.DropIndex(
                name: "IX_project_boq_revisions_SourceWorkPackageId",
                table: "project_boq_revisions");

            migrationBuilder.DropColumn(
                name: "SourceWorkPackageId",
                table: "project_boq_revisions");
        }
    }
}
