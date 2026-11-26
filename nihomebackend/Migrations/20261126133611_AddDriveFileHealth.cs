using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddDriveFileHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MissingSince",
                table: "project_drive_folders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DriveCheckedAt",
                table: "project_documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FileCheckIntervalHours",
                table: "google_drive_credentials",
                type: "int",
                nullable: false,
                // The ADMIN setting's initial value; 0 would recheck every file on every pass.
                defaultValue: 6);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MissingSince",
                table: "project_drive_folders");

            migrationBuilder.DropColumn(
                name: "DriveCheckedAt",
                table: "project_documents");

            migrationBuilder.DropColumn(
                name: "FileCheckIntervalHours",
                table: "google_drive_credentials");
        }
    }
}
