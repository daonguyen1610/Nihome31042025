using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTemplatesAndExtendNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "notifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefEntityId",
                table: "notifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefEntityType",
                table: "notifications",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateCode",
                table: "notifications",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notification_templates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TitleKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BodyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AdminDescription = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_templates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_templates_Code",
                table: "notification_templates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_templates_Module",
                table: "notification_templates",
                column: "Module");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_templates");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "RefEntityId",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "RefEntityType",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "TemplateCode",
                table: "notifications");
        }
    }
}
