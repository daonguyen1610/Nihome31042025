using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmRecordCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "quotes",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "VND");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "opportunities",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "VND");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "contracts",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "VND");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "contracts");
        }
    }
}
