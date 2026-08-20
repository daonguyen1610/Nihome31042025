using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class HardenIdempotencyReplay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResponseHeadersJson",
                table: "idempotency_records",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("DELETE FROM idempotency_records");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResponseHeadersJson",
                table: "idempotency_records");
        }
    }
}
