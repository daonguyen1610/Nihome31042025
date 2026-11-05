using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace NihomeBackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261105123000_AddSharedBusinessEmailLayout")]
public partial class AddSharedBusinessEmailLayout : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SharedBusinessEmailLayoutTemplate",
            table: "site_settings",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SharedBusinessEmailLayoutTemplate",
            table: "site_settings");
    }
}
