using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteProjectBoqSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectBoqRevisionId",
                table: "quotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectBoqRevisionId",
                table: "quote_version_snapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "quote_items",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "quote_items",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "quote_items",
                type: "decimal(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "quote_items",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "ItemCode",
                table: "quote_items",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_quotes_ProjectBoqRevisionId",
                table: "quotes",
                column: "ProjectBoqRevisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_quotes_project_boq_revisions_ProjectBoqRevisionId",
                table: "quotes",
                column: "ProjectBoqRevisionId",
                principalTable: "project_boq_revisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_quotes_project_boq_revisions_ProjectBoqRevisionId",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "IX_quotes_ProjectBoqRevisionId",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "ProjectBoqRevisionId",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "ProjectBoqRevisionId",
                table: "quote_version_snapshots");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "quote_items",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "quote_items",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "quote_items",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "quote_items",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "ItemCode",
                table: "quote_items",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);
        }
    }
}
