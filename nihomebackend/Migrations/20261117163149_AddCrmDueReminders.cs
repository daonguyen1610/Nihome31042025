using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmDueReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crm_due_reminders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_due_reminders", x => x.Id);
                });

            // Keep reminders already sent before this table existed, so nobody is reminded twice.
            migrationBuilder.Sql("""
                INSERT INTO crm_due_reminders (Kind, EntityId, DueDate, SentAt)
                SELECT 'lead.response', Id, ResponseDueDate, ResponseDueNotifiedAt
                FROM leads WHERE ResponseDueDate IS NOT NULL AND ResponseDueNotifiedAt IS NOT NULL;
                INSERT INTO crm_due_reminders (Kind, EntityId, DueDate, SentAt)
                SELECT 'customer.follow-up', Id, FollowUpDate, FollowUpNotifiedAt
                FROM customer_activities WHERE FollowUpDate IS NOT NULL AND FollowUpNotifiedAt IS NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_leads_ResponseDueDate_ResponseDueNotifiedAt",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "IX_customer_activities_FollowUpDate_FollowUpNotifiedAt",
                table: "customer_activities");

            migrationBuilder.DropColumn(
                name: "ResponseDueNotifiedAt",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "FollowUpNotifiedAt",
                table: "customer_activities");

            migrationBuilder.CreateIndex(
                name: "IX_leads_ResponseDueDate",
                table: "leads",
                column: "ResponseDueDate");

            migrationBuilder.CreateIndex(
                name: "IX_customer_activities_FollowUpDate",
                table: "customer_activities",
                column: "FollowUpDate");

            migrationBuilder.CreateIndex(
                name: "IX_crm_due_reminders_Kind_EntityId_DueDate",
                table: "crm_due_reminders",
                columns: new[] { "Kind", "EntityId", "DueDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crm_due_reminders");

            migrationBuilder.DropIndex(
                name: "IX_leads_ResponseDueDate",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "IX_customer_activities_FollowUpDate",
                table: "customer_activities");

            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseDueNotifiedAt",
                table: "leads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FollowUpNotifiedAt",
                table: "customer_activities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_leads_ResponseDueDate_ResponseDueNotifiedAt",
                table: "leads",
                columns: new[] { "ResponseDueDate", "ResponseDueNotifiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_activities_FollowUpDate_FollowUpNotifiedAt",
                table: "customer_activities",
                columns: new[] { "FollowUpDate", "FollowUpNotifiedAt" });
        }
    }
}
