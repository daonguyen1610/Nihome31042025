using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260902130000_SupportBoqMaterialRateCatalogs")]
    public partial class SupportBoqMaterialRateCatalogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CatalogType",
                table: "material_rate_catalogs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "InvestmentRate");

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "material_rate_lines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "MaterialCode",
                table: "material_rate_lines",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "MaterialName",
                table: "material_rate_lines",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_material_rate_catalogs_CatalogType",
                table: "material_rate_catalogs",
                column: "CatalogType");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_material_rate_catalogs_CatalogType",
                table: "material_rate_catalogs");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "material_rate_lines");

            migrationBuilder.DropColumn(
                name: "CatalogType",
                table: "material_rate_catalogs");

            migrationBuilder.AlterColumn<string>(
                name: "MaterialCode",
                table: "material_rate_lines",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60);

            migrationBuilder.AlterColumn<string>(
                name: "MaterialName",
                table: "material_rate_lines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);
        }
    }
}
