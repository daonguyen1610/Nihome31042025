using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerActivityFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "FollowUpDate",
                table: "customer_activities",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FollowUpNotifiedAt",
                table: "customer_activities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_activities_FollowUpDate_FollowUpNotifiedAt",
                table: "customer_activities",
                columns: new[] { "FollowUpDate", "FollowUpNotifiedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_customer_activities_FollowUpDate_FollowUpNotifiedAt",
                table: "customer_activities");

            migrationBuilder.DropColumn(
                name: "FollowUpDate",
                table: "customer_activities");

            migrationBuilder.DropColumn(
                name: "FollowUpNotifiedAt",
                table: "customer_activities");
        }
    }
}
