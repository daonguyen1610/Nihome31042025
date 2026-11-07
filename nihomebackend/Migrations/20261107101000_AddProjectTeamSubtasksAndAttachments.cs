using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261107101000_AddProjectTeamSubtasksAndAttachments")]
public partial class AddProjectTeamSubtasksAndAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ParentAssignmentId",
            table: "operational_project_assignments",
            type: "int",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "operational_project_assignment_attachments",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                AssignmentId = table.Column<int>(type: "int", nullable: false),
                Path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                FileSize = table.Column<long>(type: "bigint", nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                StorageDeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_operational_project_assignment_attachments", x => x.Id);
                table.CheckConstraint(
                    "CK_operational_project_assignment_attachments_size", "[FileSize] > 0");
                table.ForeignKey(
                    name: "FK_operational_project_assignment_attachments_operational_project_assignments_AssignmentId",
                    column: x => x.AssignmentId,
                    principalTable: "operational_project_assignments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_operational_project_assignments_ParentAssignmentId",
            table: "operational_project_assignments",
            column: "ParentAssignmentId");

        migrationBuilder.CreateIndex(
            name: "IX_operational_project_assignment_attachments_AssignmentId_DeletedAt",
            table: "operational_project_assignment_attachments",
            columns: new[] { "AssignmentId", "DeletedAt" });

        migrationBuilder.AddForeignKey(
            name: "FK_operational_project_assignments_operational_project_assignments_ParentAssignmentId",
            table: "operational_project_assignments",
            column: "ParentAssignmentId",
            principalTable: "operational_project_assignments",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_operational_project_assignments_operational_project_assignments_ParentAssignmentId",
            table: "operational_project_assignments");

        migrationBuilder.DropTable(
            name: "operational_project_assignment_attachments");

        migrationBuilder.DropIndex(
            name: "IX_operational_project_assignments_ParentAssignmentId",
            table: "operational_project_assignments");

        migrationBuilder.DropColumn(
            name: "ParentAssignmentId",
            table: "operational_project_assignments");
    }
}
