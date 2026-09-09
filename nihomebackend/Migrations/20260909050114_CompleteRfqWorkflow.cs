using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    public partial class CompleteRfqWorkflow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>("CommercialWeight", "rfqs", "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>("LeadTimeWeight", "rfqs", "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>("PriceWeight", "rfqs", "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>("VendorRatingWeight", "rfqs", "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m);

            migrationBuilder.AddColumn<string>("InvitationDeliveryError", "rfq_invitations", "nvarchar(1000)", maxLength: 1000, nullable: true);
            migrationBuilder.AddColumn<DateTime>("InvitationSentAt", "rfq_invitations", "datetime2", nullable: true);
            migrationBuilder.AddColumn<DateTime>("LastPortalAccessAt", "rfq_invitations", "datetime2", nullable: true);
            migrationBuilder.AddColumn<DateTime>("PortalTokenExpiresAt", "rfq_invitations", "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>("PortalTokenHash", "rfq_invitations", "nvarchar(64)", maxLength: 64, nullable: true);

            migrationBuilder.AddColumn<decimal>("CommercialScore", "rfq_bids", "decimal(5,2)", precision: 5, scale: 2, nullable: true);
            migrationBuilder.AddColumn<string>("Currency", "rfq_bids", "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<decimal>("DiscountAmount", "rfq_bids", "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>("DiscountPercent", "rfq_bids", "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<DateTime>("EvaluatedAt", "rfq_bids", "datetime2", nullable: true);
            migrationBuilder.AddColumn<int>("EvaluatedByUserId", "rfq_bids", "int", nullable: true);
            migrationBuilder.AddColumn<string>("EvaluationNote", "rfq_bids", "nvarchar(2000)", maxLength: 2000, nullable: true);
            migrationBuilder.AddColumn<decimal>("ExchangeRateToVnd", "rfq_bids", "decimal(20,8)", precision: 20, scale: 8, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>("FreightAmount", "rfq_bids", "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<bool>("SubmittedViaPortal", "rfq_bids", "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<decimal>("Subtotal", "rfq_bids", "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>("TotalOriginal", "rfq_bids", "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>("VatPercent", "rfq_bids", "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE [rfqs]
                SET [PriceWeight] = 50, [LeadTimeWeight] = 20,
                    [VendorRatingWeight] = 20, [CommercialWeight] = 10;

                UPDATE [rfq_bids]
                SET [Currency] = 'VND', [ExchangeRateToVnd] = 1,
                    [Subtotal] = [Total], [TotalOriginal] = [Total];
                """);

            migrationBuilder.AddCheckConstraint("CK_rfqs_scoring_weights", "rfqs",
                "[PriceWeight] + [LeadTimeWeight] + [VendorRatingWeight] + [CommercialWeight] = 100");
            migrationBuilder.AddCheckConstraint("CK_rfq_bids_exchange_rate", "rfq_bids", "[ExchangeRateToVnd] > 0");
            migrationBuilder.AddCheckConstraint("CK_rfq_bids_commercial_values", "rfq_bids",
                "[FreightAmount] >= 0 AND [DiscountPercent] >= 0 AND [DiscountPercent] <= 100 AND [DiscountAmount] >= 0 AND [VatPercent] >= 0 AND [VatPercent] <= 100");

            migrationBuilder.CreateIndex("IX_rfq_invitations_PortalTokenHash", "rfq_invitations", "PortalTokenHash",
                unique: true, filter: "[PortalTokenHash] IS NOT NULL");
            migrationBuilder.CreateIndex("IX_rfq_bids_EvaluatedByUserId", "rfq_bids", "EvaluatedByUserId");
            migrationBuilder.AddForeignKey("FK_rfq_bids_users_EvaluatedByUserId", "rfq_bids", "EvaluatedByUserId",
                "users", principalColumn: "Id");

            migrationBuilder.CreateTable(
                name: "rfq_awards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    RfqId = table.Column<int>(type: "int", nullable: false),
                    RfqBidId = table.Column<int>(type: "int", nullable: false),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRateToVnd = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    OriginalValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ValueVnd = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AwardedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AwardedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rfq_awards", x => x.Id);
                    table.ForeignKey("FK_rfq_awards_contracts_ContractId", x => x.ContractId, "contracts", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_rfq_awards_procurement_vendors_VendorId", x => x.VendorId, "procurement_vendors", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_rfq_awards_rfq_bids_RfqBidId", x => x.RfqBidId, "rfq_bids", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_rfq_awards_rfqs_RfqId", x => x.RfqId, "rfqs", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_rfq_awards_users_AwardedByUserId", x => x.AwardedByUserId, "users", "Id");
                });

            migrationBuilder.CreateTable(
                name: "rfq_award_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    RfqAwardId = table.Column<int>(type: "int", nullable: false),
                    RfqLineId = table.Column<int>(type: "int", nullable: false),
                    RfqBidLineId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AmountOriginal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AmountVnd = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rfq_award_lines", x => x.Id);
                    table.ForeignKey("FK_rfq_award_lines_rfq_awards_RfqAwardId", x => x.RfqAwardId, "rfq_awards", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_rfq_award_lines_rfq_bid_lines_RfqBidLineId", x => x.RfqBidLineId, "rfq_bid_lines", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_rfq_award_lines_rfq_lines_RfqLineId", x => x.RfqLineId, "rfq_lines", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rfq_award_material_request_allocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    RfqAwardLineId = table.Column<int>(type: "int", nullable: false),
                    MaterialRequestLineId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rfq_award_material_request_allocations", x => x.Id);
                    table.ForeignKey("FK_rfq_award_material_request_allocations_material_request_lines_MaterialRequestLineId", x => x.MaterialRequestLineId,
                        "material_request_lines", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_rfq_award_material_request_allocations_rfq_award_lines_RfqAwardLineId", x => x.RfqAwardLineId,
                        "rfq_award_lines", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_rfq_awards_AwardedByUserId", "rfq_awards", "AwardedByUserId");
            migrationBuilder.CreateIndex("IX_rfq_awards_ContractId", "rfq_awards", "ContractId", unique: true);
            migrationBuilder.CreateIndex("IX_rfq_awards_RfqBidId", "rfq_awards", "RfqBidId");
            migrationBuilder.CreateIndex("IX_rfq_awards_RfqId_VendorId", "rfq_awards", new[] { "RfqId", "VendorId" }, unique: true);
            migrationBuilder.CreateIndex("IX_rfq_awards_VendorId", "rfq_awards", "VendorId");
            migrationBuilder.CreateIndex("IX_rfq_award_lines_RfqAwardId_RfqLineId", "rfq_award_lines", new[] { "RfqAwardId", "RfqLineId" });
            migrationBuilder.CreateIndex("IX_rfq_award_lines_RfqBidLineId", "rfq_award_lines", "RfqBidLineId");
            migrationBuilder.CreateIndex("IX_rfq_award_lines_RfqLineId", "rfq_award_lines", "RfqLineId");
            migrationBuilder.CreateIndex("IX_rfq_award_material_request_allocations_MaterialRequestLineId",
                "rfq_award_material_request_allocations", "MaterialRequestLineId");
            migrationBuilder.CreateIndex("IX_rfq_award_material_request_allocations_RfqAwardLineId_MaterialRequestLineId",
                "rfq_award_material_request_allocations", new[] { "RfqAwardLineId", "MaterialRequestLineId" }, unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("rfq_award_material_request_allocations");
            migrationBuilder.DropTable("rfq_award_lines");
            migrationBuilder.DropTable("rfq_awards");
            migrationBuilder.DropForeignKey("FK_rfq_bids_users_EvaluatedByUserId", "rfq_bids");
            migrationBuilder.DropIndex("IX_rfq_bids_EvaluatedByUserId", "rfq_bids");
            migrationBuilder.DropIndex("IX_rfq_invitations_PortalTokenHash", "rfq_invitations");
            migrationBuilder.DropCheckConstraint("CK_rfqs_scoring_weights", "rfqs");
            migrationBuilder.DropCheckConstraint("CK_rfq_bids_exchange_rate", "rfq_bids");
            migrationBuilder.DropCheckConstraint("CK_rfq_bids_commercial_values", "rfq_bids");
            foreach (var column in new[] { "CommercialWeight", "LeadTimeWeight", "PriceWeight", "VendorRatingWeight" })
                migrationBuilder.DropColumn(column, "rfqs");
            foreach (var column in new[] { "InvitationDeliveryError", "InvitationSentAt", "LastPortalAccessAt", "PortalTokenExpiresAt", "PortalTokenHash" })
                migrationBuilder.DropColumn(column, "rfq_invitations");
            foreach (var column in new[] { "CommercialScore", "Currency", "DiscountAmount", "DiscountPercent", "EvaluatedAt", "EvaluatedByUserId", "EvaluationNote", "ExchangeRateToVnd", "FreightAmount", "SubmittedViaPortal", "Subtotal", "TotalOriginal", "VatPercent" })
                migrationBuilder.DropColumn(column, "rfq_bids");
        }
    }
}