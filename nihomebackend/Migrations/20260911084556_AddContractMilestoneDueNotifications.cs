using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddContractMilestoneDueNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueNotificationSentAt",
                table: "contract_payment_milestones",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_payment_milestones_DueNotificationSentAt_DueDate",
                table: "contract_payment_milestones",
                columns: new[] { "DueNotificationSentAt", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contract_payment_milestones_DueNotificationSentAt_DueDate",
                table: "contract_payment_milestones");

            migrationBuilder.DropColumn(
                name: "DueNotificationSentAt",
                table: "contract_payment_milestones");
        }
    }
}
