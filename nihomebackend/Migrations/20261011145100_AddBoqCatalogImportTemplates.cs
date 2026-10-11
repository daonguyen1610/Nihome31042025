using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddBoqCatalogImportTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImportMappingJson",
                table: "material_rate_revisions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportSourceFileName",
                table: "material_rate_revisions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportSourceHash",
                table: "material_rate_revisions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImportedAt",
                table: "material_rate_revisions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImportedByUserId",
                table: "material_rate_revisions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "boq_import_templates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    HeaderRow = table.Column<int>(type: "int", nullable: false),
                    ItemCodeColumn = table.Column<int>(type: "int", nullable: true),
                    DescriptionColumn = table.Column<int>(type: "int", nullable: false),
                    UnitColumn = table.Column<int>(type: "int", nullable: false),
                    QuantityColumn = table.Column<int>(type: "int", nullable: false),
                    UnitPriceColumn = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_boq_import_templates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_boq_import_templates_Name",
                table: "boq_import_templates",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "boq_import_templates");

            migrationBuilder.DropColumn(
                name: "ImportMappingJson",
                table: "material_rate_revisions");

            migrationBuilder.DropColumn(
                name: "ImportSourceFileName",
                table: "material_rate_revisions");

            migrationBuilder.DropColumn(
                name: "ImportSourceHash",
                table: "material_rate_revisions");

            migrationBuilder.DropColumn(
                name: "ImportedAt",
                table: "material_rate_revisions");

            migrationBuilder.DropColumn(
                name: "ImportedByUserId",
                table: "material_rate_revisions");
        }
    }
}
