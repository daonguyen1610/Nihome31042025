using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "surveys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ConstructionTypeCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    SurveyDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SurveyorUserId = table.Column<int>(type: "int", nullable: true),
                    LinkedProjectId = table.Column<int>(type: "int", nullable: true),
                    LinkedOpportunityId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DriveSyncStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DriveSyncError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_surveys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_surveys_opportunities_LinkedOpportunityId",
                        column: x => x.LinkedOpportunityId,
                        principalTable: "opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_surveys_projects_LinkedProjectId",
                        column: x => x.LinkedProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_surveys_users_SurveyorUserId",
                        column: x => x.SurveyorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_surveys_Code",
                table: "surveys",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_surveys_DriveSyncStatus",
                table: "surveys",
                column: "DriveSyncStatus");

            migrationBuilder.CreateIndex(
                name: "IX_surveys_LinkedOpportunityId",
                table: "surveys",
                column: "LinkedOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_surveys_LinkedProjectId",
                table: "surveys",
                column: "LinkedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_surveys_SurveyDate",
                table: "surveys",
                column: "SurveyDate");

            migrationBuilder.CreateIndex(
                name: "IX_surveys_SurveyorUserId",
                table: "surveys",
                column: "SurveyorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "surveys");
        }
    }
}
