using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261107120000_AddAssignmentComments")]
public partial class AddAssignmentComments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "operational_project_assignment_comments",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                AssignmentId = table.Column<int>(type: "int", nullable: false),
                Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<int>(type: "int", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_operational_project_assignment_comments", x => x.Id);
                table.ForeignKey(
                    name: "FK_operational_project_assignment_comments_operational_project_assignments_AssignmentId",
                    column: x => x.AssignmentId,
                    principalTable: "operational_project_assignments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_operational_project_assignment_comments_users_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_operational_project_assignment_comments_AssignmentId_CreatedAt",
            table: "operational_project_assignment_comments",
            columns: new[] { "AssignmentId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_operational_project_assignment_comments_CreatedByUserId",
            table: "operational_project_assignment_comments",
            column: "CreatedByUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "operational_project_assignment_comments");
    }
}
