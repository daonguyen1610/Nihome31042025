using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddJobApplicationStatusAppliedAtIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "job_applications",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_AppliedAt",
                table: "job_applications",
                column: "AppliedAt");

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_Status",
                table: "job_applications",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_job_applications_AppliedAt",
                table: "job_applications");

            migrationBuilder.DropIndex(
                name: "IX_job_applications_Status",
                table: "job_applications");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "job_applications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);
        }
    }
}
