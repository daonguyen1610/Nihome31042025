using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadResponseDueDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ResponseDueDate",
                table: "leads",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseDueNotifiedAt",
                table: "leads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_leads_ResponseDueDate_ResponseDueNotifiedAt",
                table: "leads",
                columns: new[] { "ResponseDueDate", "ResponseDueNotifiedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_leads_ResponseDueDate_ResponseDueNotifiedAt",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "ResponseDueDate",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "ResponseDueNotifiedAt",
                table: "leads");
        }
    }
}
