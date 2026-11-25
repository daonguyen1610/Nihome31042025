using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddContractReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReviewStepIndex",
                table: "contracts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewStepsJson",
                table: "contracts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewSubmittedByUserId",
                table: "contracts",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Existing statuses keep their meaning; only the two new ones are unknown to older code.
            migrationBuilder.Sql("UPDATE contracts SET Status = 'Draft' WHERE Status IN ('UnderReview', 'Approved');");

            migrationBuilder.DropColumn(
                name: "ReviewStepIndex",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "ReviewStepsJson",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "ReviewSubmittedByUserId",
                table: "contracts");
        }
    }
}
