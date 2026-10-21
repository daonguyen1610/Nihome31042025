using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddContractContinuations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContinuationKind",
                table: "opportunities",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContinuationReason",
                table: "opportunities",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceContractId",
                table: "opportunities",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContinuationKind",
                table: "contracts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContinuationReason",
                table: "contracts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceContractId",
                table: "contracts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_opportunities_SourceContractId",
                table: "opportunities",
                column: "SourceContractId");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_SourceContractId",
                table: "contracts",
                column: "SourceContractId");

            migrationBuilder.AddForeignKey(
                name: "FK_contracts_contracts_SourceContractId",
                table: "contracts",
                column: "SourceContractId",
                principalTable: "contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_opportunities_contracts_SourceContractId",
                table: "opportunities",
                column: "SourceContractId",
                principalTable: "contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM contracts WHERE SourceContractId IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM opportunities WHERE SourceContractId IS NOT NULL)
                BEGIN
                    ;THROW 51000, 'Export and explicitly reconcile continuation provenance before rollback.', 1;
                END
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_contracts_contracts_SourceContractId",
                table: "contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_opportunities_contracts_SourceContractId",
                table: "opportunities");

            migrationBuilder.DropIndex(
                name: "IX_opportunities_SourceContractId",
                table: "opportunities");

            migrationBuilder.DropIndex(
                name: "IX_contracts_SourceContractId",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "ContinuationKind",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "ContinuationReason",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "SourceContractId",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "ContinuationKind",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "ContinuationReason",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "SourceContractId",
                table: "contracts");
        }
    }
}
