using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceReviewQuoteVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReviewedQuoteVersion",
                table: "department_finance_reviews",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewedQuoteVersion",
                table: "department_finance_reviews");
        }
    }
}
