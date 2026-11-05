using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace NihomeBackend.Migrations;

/// <summary>Remove the model length limit from the existing nvarchar(max) outbox body.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261105122000_AllowFullProjectScheduleEmailDiff")]
public partial class AllowFullProjectScheduleEmailDiff : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Body",
            table: "project_schedule_emails",
            type: "nvarchar(max)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldMaxLength: 16000);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Body",
            table: "project_schedule_emails",
            type: "nvarchar(max)",
            maxLength: 16000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");
    }
}
