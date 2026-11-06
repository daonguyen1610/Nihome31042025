using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261106100000_AddContractUploadLimitSetting")]
public partial class AddContractUploadLimitSetting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ContractMaxFileSizeMb",
            table: "site_settings",
            type: "int",
            nullable: false,
            defaultValue: 100);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ContractMaxFileSizeMb",
            table: "site_settings");
    }
}
