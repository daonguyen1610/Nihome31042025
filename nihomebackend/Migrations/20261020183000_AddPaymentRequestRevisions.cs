using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261020183000_AddPaymentRequestRevisions")]
public sealed class AddPaymentRequestRevisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "RevisionNumber", table: "payment_requests", type: "int",
            nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<string>(
            name: "RevisionHistoryJson", table: "payment_requests", type: "nvarchar(max)",
            nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<int>(
            name: "RevisionCreatedByUserId", table: "payment_requests", type: "int",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM payment_requests
                WHERE RevisionNumber > 1 OR RevisionHistoryJson <> N'[]')
            BEGIN
                ;THROW 51000, 'Rollback blocked while payment revision history exists.', 1;
            END
            """);
        migrationBuilder.DropColumn(name: "RevisionCreatedByUserId", table: "payment_requests");
        migrationBuilder.DropColumn(name: "RevisionHistoryJson", table: "payment_requests");
        migrationBuilder.DropColumn(name: "RevisionNumber", table: "payment_requests");
    }
}
