using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(NihomeBackend.Data.AppDbContext))]
[Migration("20261109120000_AddCmScheduleActualReportingAndCalendar")]
public partial class AddCmScheduleActualReportingAndCalendar : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "WeightBasisNote",
            table: "cm_schedule_baselines",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: false,
            defaultValue: "Legacy duration weighted baseline");

        migrationBuilder.AddColumn<string>(
            name: "WorkingDaysOfWeekJson",
            table: "cm_schedule_baselines",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "[0,1,2,3,4,5,6]");

        migrationBuilder.AddColumn<string>(
            name: "NonWorkingDatesJson",
            table: "cm_schedule_baselines",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<DateTime>(
            name: "ProgressReportedAt",
            table: "construction_tasks",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ProgressReportedByUserId",
            table: "construction_tasks",
            type: "int",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "WeightBasisNote", table: "cm_schedule_baselines");
        migrationBuilder.DropColumn(name: "WorkingDaysOfWeekJson", table: "cm_schedule_baselines");
        migrationBuilder.DropColumn(name: "NonWorkingDatesJson", table: "cm_schedule_baselines");
        migrationBuilder.DropColumn(name: "ProgressReportedAt", table: "construction_tasks");
        migrationBuilder.DropColumn(name: "ProgressReportedByUserId", table: "construction_tasks");
    }
}
