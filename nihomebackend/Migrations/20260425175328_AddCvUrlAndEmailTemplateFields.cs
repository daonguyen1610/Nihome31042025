using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCvUrlAndEmailTemplateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NewApplicationEmailBodyTemplate",
                table: "site_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewApplicationEmailSubjectTemplate",
                table: "site_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotificationEmail",
                table: "site_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvUrl",
                table: "job_applications",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewApplicationEmailBodyTemplate",
                table: "site_settings");

            migrationBuilder.DropColumn(
                name: "NewApplicationEmailSubjectTemplate",
                table: "site_settings");

            migrationBuilder.DropColumn(
                name: "NotificationEmail",
                table: "site_settings");

            migrationBuilder.DropColumn(
                name: "CvUrl",
                table: "job_applications");
        }
    }
}
