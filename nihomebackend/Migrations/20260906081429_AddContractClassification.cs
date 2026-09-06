using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddContractClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "contracts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Upstream");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "contracts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Unclassified");

            migrationBuilder.AddColumn<int>(
                name: "VendorId",
                table: "contracts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contracts_Direction_Type",
                table: "contracts",
                columns: new[] { "Direction", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_contracts_VendorId",
                table: "contracts",
                column: "VendorId");

            migrationBuilder.AddForeignKey(
                name: "FK_contracts_procurement_vendors_VendorId",
                table: "contracts",
                column: "VendorId",
                principalTable: "procurement_vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contracts_procurement_vendors_VendorId",
                table: "contracts");

            migrationBuilder.DropIndex(
                name: "IX_contracts_Direction_Type",
                table: "contracts");

            migrationBuilder.DropIndex(
                name: "IX_contracts_VendorId",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "contracts");
        }
    }
}
