using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NihomeBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectScheduleEmailQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_schedule_emails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    RevisionId = table.Column<int>(type: "int", nullable: false),
                    RecipientUserId = table.Column<int>(type: "int", nullable: true),
                    ToEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_schedule_emails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_schedule_emails_project_schedule_revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "project_schedule_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_schedule_emails_OperationalProjectId",
                table: "project_schedule_emails",
                column: "OperationalProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_schedule_emails_RevisionId",
                table: "project_schedule_emails",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_project_schedule_emails_Status_NextAttemptAt_Id",
                table: "project_schedule_emails",
                columns: new[] { "Status", "NextAttemptAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_schedule_emails");
        }
    }
}
