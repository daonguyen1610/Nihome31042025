using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261031180000_AddQuoteApprovalCc")]
public sealed class AddQuoteApprovalCc : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CcEmailsJson",
            table: "quote_approval_emails",
            type: "nvarchar(max)",
            maxLength: 20000,
            nullable: false,
            defaultValue: "[]");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CcEmailsJson",
            table: "quote_approval_emails");
    }
}
