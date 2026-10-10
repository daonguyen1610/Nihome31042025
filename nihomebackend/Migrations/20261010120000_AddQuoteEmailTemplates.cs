using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261010120000_AddQuoteEmailTemplates")]
public partial class AddQuoteEmailTemplates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "QuoteEmailSubjectTemplate",
            table: "site_settings",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "QuoteEmailBodyTemplate",
            table: "site_settings",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "QuoteEmailSubjectTemplate", table: "site_settings");
        migrationBuilder.DropColumn(name: "QuoteEmailBodyTemplate", table: "site_settings");
    }
}
