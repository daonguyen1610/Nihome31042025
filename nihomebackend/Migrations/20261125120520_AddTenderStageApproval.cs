using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddTenderStageApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "tenders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Received");

            // Existing tenders: a filed bid is at Kết quả; one still being prepared stays
            // at Triển khai lập hồ sơ thầu, or at Nộp thầu once readiness was reached, so
            // in-flight bids that were already ready to submit are not blocked.
            migrationBuilder.Sql("""
                UPDATE tenders SET Stage = CASE
                    WHEN Status IN ('Submitted', 'Won', 'Lost') OR SubmittedAt IS NOT NULL THEN 'Result'
                    WHEN EXISTS (SELECT 1 FROM tender_milestones m
                                 WHERE m.TenderId = tenders.Id AND m.Kind = 'ReadyToSubmit') THEN 'Submission'
                    ELSE 'Preparing'
                END;
                """);

            migrationBuilder.AddColumn<int>(
                name: "StageApproverUserId",
                table: "tenders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StageRequestNote",
                table: "tenders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StageRequestedAt",
                table: "tenders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StageRequestedByUserId",
                table: "tenders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenders_StageApproverUserId",
                table: "tenders",
                column: "StageApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tenders_StageRequestedByUserId",
                table: "tenders",
                column: "StageRequestedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_tenders_users_StageApproverUserId",
                table: "tenders",
                column: "StageApproverUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tenders_users_StageRequestedByUserId",
                table: "tenders",
                column: "StageRequestedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenders_users_StageApproverUserId",
                table: "tenders");

            migrationBuilder.DropForeignKey(
                name: "FK_tenders_users_StageRequestedByUserId",
                table: "tenders");

            migrationBuilder.DropIndex(
                name: "IX_tenders_StageApproverUserId",
                table: "tenders");

            migrationBuilder.DropIndex(
                name: "IX_tenders_StageRequestedByUserId",
                table: "tenders");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "tenders");

            migrationBuilder.DropColumn(
                name: "StageApproverUserId",
                table: "tenders");

            migrationBuilder.DropColumn(
                name: "StageRequestNote",
                table: "tenders");

            migrationBuilder.DropColumn(
                name: "StageRequestedAt",
                table: "tenders");

            migrationBuilder.DropColumn(
                name: "StageRequestedByUserId",
                table: "tenders");
        }
    }
}
