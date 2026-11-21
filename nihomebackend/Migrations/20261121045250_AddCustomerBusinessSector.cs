using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerBusinessSector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessDetail",
                table: "customers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessSectorCode",
                table: "customers",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_BusinessSectorCode",
                table: "customers",
                column: "BusinessSectorCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_customers_BusinessSectorCode",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BusinessDetail",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BusinessSectorCode",
                table: "customers");
        }
    }
}
