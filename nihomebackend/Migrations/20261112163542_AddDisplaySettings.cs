using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplaySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "site_settings",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "VND");

            migrationBuilder.AddColumn<string>(
                name: "DisplayTimeZoneId",
                table: "site_settings",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Asia/Ho_Chi_Minh");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "site_settings");

            migrationBuilder.DropColumn(
                name: "DisplayTimeZoneId",
                table: "site_settings");
        }
    }
}
