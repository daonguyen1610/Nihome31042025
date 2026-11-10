using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(NihomeBackend.Data.AppDbContext))]
[Migration("20261110090000_TrackConstructionReviewEditors")]
public partial class TrackConstructionReviewEditors : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddAndBackfill(migrationBuilder, "construction_resource_requests");
        AddAndBackfill(migrationBuilder, "qa_qc_dossiers");
        AddAndBackfill(migrationBuilder, "construction_qs_batches");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LastEditedByUserId", table: "construction_resource_requests");
        migrationBuilder.DropColumn(name: "LastEditedByUserId", table: "qa_qc_dossiers");
        migrationBuilder.DropColumn(name: "LastEditedByUserId", table: "construction_qs_batches");
    }

    private static void AddAndBackfill(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<int>(
            name: "LastEditedByUserId",
            table: table,
            type: "int",
            nullable: false,
            defaultValue: 0);
        migrationBuilder.Sql($"UPDATE [{table}] SET [LastEditedByUserId] = [CreatedByUserId]");
    }
}
