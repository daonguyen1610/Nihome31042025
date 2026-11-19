using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddTenderAndCapabilityUploadLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CapabilityMaxFileSizeMb",
                table: "site_settings",
                type: "int",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "TenderMaxFileSizeMb",
                table: "site_settings",
                type: "int",
                nullable: false,
                defaultValue: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapabilityMaxFileSizeMb",
                table: "site_settings");

            migrationBuilder.DropColumn(
                name: "TenderMaxFileSizeMb",
                table: "site_settings");
        }
    }
}
