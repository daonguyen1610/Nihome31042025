using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleItemLinksAndBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "BaselineEnd",
                table: "project_schedule_items",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BaselineStart",
                table: "project_schedule_items",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMilestone",
                table: "project_schedule_items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PredecessorsJson",
                table: "project_schedule_items",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaselineEnd",
                table: "project_schedule_items");

            migrationBuilder.DropColumn(
                name: "BaselineStart",
                table: "project_schedule_items");

            migrationBuilder.DropColumn(
                name: "IsMilestone",
                table: "project_schedule_items");

            migrationBuilder.DropColumn(
                name: "PredecessorsJson",
                table: "project_schedule_items");
        }
    }
}
