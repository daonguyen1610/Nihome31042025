using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AllowAssignmentHandover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_operational_project_assignments_OperationalProjectId_WorkKey_AssigneeMemberId",
                table: "operational_project_assignments");

            migrationBuilder.AddColumn<int>(
                name: "PreviousAssignmentId",
                table: "operational_project_assignments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_assignments_OperationalProjectId_WorkKey_AssigneeMemberId",
                table: "operational_project_assignments",
                columns: new[] { "OperationalProjectId", "WorkKey", "AssigneeMemberId" },
                unique: true,
                filter: "[Status] <> 'Reassigned'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_operational_project_assignments_OperationalProjectId_WorkKey_AssigneeMemberId",
                table: "operational_project_assignments");

            migrationBuilder.DropColumn(
                name: "PreviousAssignmentId",
                table: "operational_project_assignments");

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_assignments_OperationalProjectId_WorkKey_AssigneeMemberId",
                table: "operational_project_assignments",
                columns: new[] { "OperationalProjectId", "WorkKey", "AssigneeMemberId" },
                unique: true);
        }
    }
}
