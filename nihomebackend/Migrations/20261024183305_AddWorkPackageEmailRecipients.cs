using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkPackageEmailRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CcEmailsJson",
                table: "work_package_approval_emails",
                type: "nvarchar(max)",
                maxLength: 20000,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "LeadEmail",
                table: "work_package_approval_emails",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            // Before recipient overrides existed, ToEmail always held the lead's
            // address. Preserve that snapshot for pending and historical emails.
            migrationBuilder.Sql("UPDATE [work_package_approval_emails] SET [LeadEmail] = [ToEmail];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CcEmailsJson",
                table: "work_package_approval_emails");

            migrationBuilder.DropColumn(
                name: "LeadEmail",
                table: "work_package_approval_emails");
        }
    }
}
