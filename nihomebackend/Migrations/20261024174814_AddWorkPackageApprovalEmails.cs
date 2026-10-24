using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkPackageApprovalEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkPackageApprovalEmailBodyTemplate",
                table: "site_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkPackageApprovalEmailSubjectTemplate",
                table: "site_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "work_package_approval_emails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkPackageId = table.Column<int>(type: "int", nullable: false),
                    RecordType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RecordId = table.Column<int>(type: "int", nullable: false),
                    AuthorUserId = table.Column<int>(type: "int", nullable: false),
                    RecipientUserId = table.Column<int>(type: "int", nullable: false),
                    ToEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    PreviewHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_package_approval_emails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_package_approval_emails_department_work_packages_WorkPackageId",
                        column: x => x.WorkPackageId,
                        principalTable: "department_work_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_package_approval_emails_PreviewHash",
                table: "work_package_approval_emails",
                column: "PreviewHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_package_approval_emails_Status_CreatedAt",
                table: "work_package_approval_emails",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_work_package_approval_emails_WorkPackageId",
                table: "work_package_approval_emails",
                column: "WorkPackageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_package_approval_emails");

            migrationBuilder.DropColumn(
                name: "WorkPackageApprovalEmailBodyTemplate",
                table: "site_settings");

            migrationBuilder.DropColumn(
                name: "WorkPackageApprovalEmailSubjectTemplate",
                table: "site_settings");
        }
    }
}
