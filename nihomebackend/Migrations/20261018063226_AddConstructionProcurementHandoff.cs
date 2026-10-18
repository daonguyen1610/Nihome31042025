using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddConstructionProcurementHandoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProcurementContractId",
                table: "construction_resource_requests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuppliedAt",
                table: "construction_resource_requests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SuppliedByUserId",
                table: "construction_resource_requests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SupplyDocumentId",
                table: "construction_resource_requests",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "construction_resource_handoff_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConstructionResourceRequestId = table.Column<int>(type: "int", nullable: false),
                    PreviousContractId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    SupplyDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_resource_handoff_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_construction_resource_handoff_events_construction_resource_requests_ConstructionResourceRequestId",
                        column: x => x.ConstructionResourceRequestId,
                        principalTable: "construction_resource_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_construction_resource_handoff_events_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_construction_resource_requests_ProcurementContractId",
                table: "construction_resource_requests",
                column: "ProcurementContractId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_resource_requests_SuppliedByUserId",
                table: "construction_resource_requests",
                column: "SuppliedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_resource_requests_SupplyDocumentId",
                table: "construction_resource_requests",
                column: "SupplyDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_resource_handoff_events_ActorUserId",
                table: "construction_resource_handoff_events",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_resource_handoff_events_ConstructionResourceRequestId_CreatedAt",
                table: "construction_resource_handoff_events",
                columns: new[] { "ConstructionResourceRequestId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_construction_resource_requests_contracts_ProcurementContractId",
                table: "construction_resource_requests",
                column: "ProcurementContractId",
                principalTable: "contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_construction_resource_requests_project_documents_SupplyDocumentId",
                table: "construction_resource_requests",
                column: "SupplyDocumentId",
                principalTable: "project_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_construction_resource_requests_users_SuppliedByUserId",
                table: "construction_resource_requests",
                column: "SuppliedByUserId",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_construction_resource_requests_contracts_ProcurementContractId",
                table: "construction_resource_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_construction_resource_requests_project_documents_SupplyDocumentId",
                table: "construction_resource_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_construction_resource_requests_users_SuppliedByUserId",
                table: "construction_resource_requests");

            migrationBuilder.DropTable(
                name: "construction_resource_handoff_events");

            migrationBuilder.DropIndex(
                name: "IX_construction_resource_requests_ProcurementContractId",
                table: "construction_resource_requests");

            migrationBuilder.DropIndex(
                name: "IX_construction_resource_requests_SuppliedByUserId",
                table: "construction_resource_requests");

            migrationBuilder.DropIndex(
                name: "IX_construction_resource_requests_SupplyDocumentId",
                table: "construction_resource_requests");

            migrationBuilder.DropColumn(
                name: "ProcurementContractId",
                table: "construction_resource_requests");

            migrationBuilder.DropColumn(
                name: "SuppliedAt",
                table: "construction_resource_requests");

            migrationBuilder.DropColumn(
                name: "SuppliedByUserId",
                table: "construction_resource_requests");

            migrationBuilder.DropColumn(
                name: "SupplyDocumentId",
                table: "construction_resource_requests");
        }
    }
}
