using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyTimeAndStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "surveys",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompletedByUserId",
                table: "surveys",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "surveys",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Planned");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "SurveyTime",
                table: "surveys",
                type: "time",
                nullable: true);

            // Surveys dated before this release took place before the status existed;
            // record them as surveyed so they are not all flagged as overdue.
            migrationBuilder.Sql(
                "UPDATE surveys SET Status = 'Completed', CompletedAt = SurveyDate " +
                "WHERE SurveyDate < CAST(SYSUTCDATETIME() AS date);");

            migrationBuilder.CreateIndex(
                name: "IX_surveys_CompletedByUserId",
                table: "surveys",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_surveys_Status",
                table: "surveys",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_surveys_users_CompletedByUserId",
                table: "surveys",
                column: "CompletedByUserId",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_surveys_users_CompletedByUserId",
                table: "surveys");

            migrationBuilder.DropIndex(
                name: "IX_surveys_CompletedByUserId",
                table: "surveys");

            migrationBuilder.DropIndex(
                name: "IX_surveys_Status",
                table: "surveys");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "surveys");

            migrationBuilder.DropColumn(
                name: "CompletedByUserId",
                table: "surveys");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "surveys");

            migrationBuilder.DropColumn(
                name: "SurveyTime",
                table: "surveys");
        }
    }
}
