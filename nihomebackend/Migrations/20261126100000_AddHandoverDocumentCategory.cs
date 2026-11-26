using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddHandoverDocumentCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HandoverFolder",
                table: "google_drive_credentials",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "08_BanGiao_BaoHanh");

            // Handover = 10, ConstructionAcceptance = 6; acceptance records stay in 05/03.
            migrationBuilder.Sql(
                "UPDATE project_documents SET Category = 10 WHERE Category = 6 " +
                "AND SourceEntityType IN ('HandoverRecord', 'AsBuiltDocument')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE project_documents SET Category = 6 WHERE Category = 10 " +
                "AND SourceEntityType IN ('HandoverRecord', 'AsBuiltDocument')");

            migrationBuilder.DropColumn(
                name: "HandoverFolder",
                table: "google_drive_credentials");
        }
    }
}
