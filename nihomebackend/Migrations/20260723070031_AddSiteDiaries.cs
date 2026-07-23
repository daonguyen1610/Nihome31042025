using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteDiaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "site_diaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesignProjectId = table.Column<int>(type: "int", nullable: false),
                    DiaryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WeatherCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    WeatherNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    HeadcountLabor = table.Column<int>(type: "int", nullable: false),
                    HeadcountEngineers = table.Column<int>(type: "int", nullable: false),
                    HeadcountSupervisors = table.Column<int>(type: "int", nullable: false),
                    HeadcountSubcontractors = table.Column<int>(type: "int", nullable: false),
                    MachinesSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MaterialsReceived = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    WorkPerformed = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Incidents = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_diaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_site_diaries_design_projects_DesignProjectId",
                        column: x => x.DesignProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_site_diaries_users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_site_diaries_users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_site_diaries_ConfirmedByUserId",
                table: "site_diaries",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_site_diaries_DesignProjectId",
                table: "site_diaries",
                column: "DesignProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_site_diaries_DesignProjectId_DiaryDate",
                table: "site_diaries",
                columns: new[] { "DesignProjectId", "DiaryDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_site_diaries_Status",
                table: "site_diaries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_site_diaries_SubmittedByUserId",
                table: "site_diaries",
                column: "SubmittedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site_diaries");
        }
    }
}
