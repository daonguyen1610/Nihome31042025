using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddDesignDocumentFileMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "shop_drawings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "shop_drawings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "shop_drawings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "shop_drawings",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "basic_design_docs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "basic_design_docs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "basic_design_docs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "basic_design_docs",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "shop_drawings");

            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "shop_drawings");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "shop_drawings");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "shop_drawings");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "basic_design_docs");

            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "basic_design_docs");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "basic_design_docs");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "basic_design_docs");
        }
    }
}
