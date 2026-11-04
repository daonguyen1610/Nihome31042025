using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261105090000_AddGoogleDriveDeletionPolicies")]
public sealed class AddGoogleDriveDeletionPolicies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "DeleteDriveFilesOnWebDelete",
            table: "google_drive_credentials",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "DeleteWebRecordsOnDriveDelete",
            table: "google_drive_credentials",
            type: "bit",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DeleteDriveFilesOnWebDelete",
            table: "google_drive_credentials");

        migrationBuilder.DropColumn(
            name: "DeleteWebRecordsOnDriveDelete",
            table: "google_drive_credentials");
    }
}
