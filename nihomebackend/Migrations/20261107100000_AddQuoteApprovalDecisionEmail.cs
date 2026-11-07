using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261107100000_AddQuoteApprovalDecisionEmail")]
public partial class AddQuoteApprovalDecisionEmail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Kind",
            table: "quote_approval_emails",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Request");

        migrationBuilder.AddColumn<string>(
            name: "Decision",
            table: "quote_approval_emails",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "QuoteApprovalDecisionEmailSubjectTemplate",
            table: "site_settings",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "QuoteApprovalDecisionEmailBodyTemplate",
            table: "site_settings",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.DropIndex(
            name: "IX_quote_approval_emails_Status",
            table: "quote_approval_emails");

        migrationBuilder.CreateIndex(
            name: "IX_quote_approval_emails_Status_Kind",
            table: "quote_approval_emails",
            columns: new[] { "Status", "Kind" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_quote_approval_emails_Status_Kind",
            table: "quote_approval_emails");

        migrationBuilder.CreateIndex(
            name: "IX_quote_approval_emails_Status",
            table: "quote_approval_emails",
            column: "Status");

        migrationBuilder.DropColumn(
            name: "QuoteApprovalDecisionEmailBodyTemplate",
            table: "site_settings");

        migrationBuilder.DropColumn(
            name: "QuoteApprovalDecisionEmailSubjectTemplate",
            table: "site_settings");

        migrationBuilder.DropColumn(
            name: "Decision",
            table: "quote_approval_emails");

        migrationBuilder.DropColumn(
            name: "Kind",
            table: "quote_approval_emails");
    }
}
