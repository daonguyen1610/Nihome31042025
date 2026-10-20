using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261020170000_LinkPaymentRequestsToDownstreamQs")]
public sealed class LinkPaymentRequestsToDownstreamQs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ConstructionQsBatchId",
            table: "payment_requests",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_payment_requests_ConstructionQsBatchId",
            table: "payment_requests",
            column: "ConstructionQsBatchId");

        migrationBuilder.AddForeignKey(
            name: "FK_payment_requests_construction_qs_batches_ConstructionQsBatchId",
            table: "payment_requests",
            column: "ConstructionQsBatchId",
            principalTable: "construction_qs_batches",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_payment_requests_construction_qs_batches_ConstructionQsBatchId",
            table: "payment_requests");
        migrationBuilder.DropIndex(
            name: "IX_payment_requests_ConstructionQsBatchId",
            table: "payment_requests");
        migrationBuilder.DropColumn(
            name: "ConstructionQsBatchId",
            table: "payment_requests");
    }
}
