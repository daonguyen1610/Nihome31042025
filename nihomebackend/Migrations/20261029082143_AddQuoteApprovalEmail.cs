using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteApprovalEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuoteApprovalEmailBodyTemplate",
                table: "site_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuoteApprovalEmailSubjectTemplate",
                table: "site_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalApproverUserId",
                table: "quotes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "quote_approval_emails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuoteId = table.Column<int>(type: "int", nullable: false),
                    ApproverUserId = table.Column<int>(type: "int", nullable: false),
                    ToEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_approval_emails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_approval_emails_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quotes_ApprovalApproverUserId",
                table: "quotes",
                column: "ApprovalApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_approval_emails_QuoteId_CreatedAt",
                table: "quote_approval_emails",
                columns: new[] { "QuoteId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_approval_emails_Status",
                table: "quote_approval_emails",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_quotes_users_ApprovalApproverUserId",
                table: "quotes",
                column: "ApprovalApproverUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_quotes_users_ApprovalApproverUserId",
                table: "quotes");

            migrationBuilder.DropTable(
                name: "quote_approval_emails");

            migrationBuilder.DropIndex(
                name: "IX_quotes_ApprovalApproverUserId",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "QuoteApprovalEmailBodyTemplate",
                table: "site_settings");

            migrationBuilder.DropColumn(
                name: "QuoteApprovalEmailSubjectTemplate",
                table: "site_settings");

            migrationBuilder.DropColumn(
                name: "ApprovalApproverUserId",
                table: "quotes");
        }
    }
}
