using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddPrecontractDepartmentControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_department_work_packages_scope",
                table: "department_work_packages");

            migrationBuilder.AddColumn<int>(
                name: "OperationalProjectId",
                table: "tenders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InputPriceOfferId",
                table: "quotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InputPriceSnapshotJson",
                table: "quotes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InputPriceOfferId",
                table: "quote_version_snapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InputPriceSnapshotJson",
                table: "quote_version_snapshots",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpstreamContractId",
                table: "project_boq_revisions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "department_work_packages",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.AddColumn<int>(
                name: "SourceQuoteId",
                table: "department_work_packages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceTenderId",
                table: "department_work_packages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "department_finance_reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkPackageId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContractConditionsReviewed = table.Column<bool>(type: "bit", nullable: false),
                    GuaranteesReviewed = table.Column<bool>(type: "bit", nullable: false),
                    PaymentTermsReviewed = table.Column<bool>(type: "bit", nullable: false),
                    FinancialDossierReviewed = table.Column<bool>(type: "bit", nullable: false),
                    Findings = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_finance_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_department_finance_reviews_department_work_packages_WorkPackageId",
                        column: x => x.WorkPackageId,
                        principalTable: "department_work_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_department_finance_reviews_users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "procurement_input_price_offers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkPackageId = table.Column<int>(type: "int", nullable: false),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    SupplierReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_procurement_input_price_offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_procurement_input_price_offers_department_work_packages_WorkPackageId",
                        column: x => x.WorkPackageId,
                        principalTable: "department_work_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_procurement_input_price_offers_procurement_vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "procurement_vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_procurement_input_price_offers_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_procurement_input_price_offers_users_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "procurement_input_price_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfferId = table.Column<int>(type: "int", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VatPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_procurement_input_price_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_procurement_input_price_lines_procurement_input_price_offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "procurement_input_price_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenders_OperationalProjectId",
                table: "tenders",
                column: "OperationalProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_InputPriceOfferId",
                table: "quotes",
                column: "InputPriceOfferId");

            migrationBuilder.CreateIndex(
                name: "IX_project_boq_revisions_UpstreamContractId",
                table: "project_boq_revisions",
                column: "UpstreamContractId");

            migrationBuilder.CreateIndex(
                name: "IX_department_work_packages_Purpose_Status",
                table: "department_work_packages",
                columns: new[] { "Purpose", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_department_work_packages_SourceQuoteId",
                table: "department_work_packages",
                column: "SourceQuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_department_work_packages_SourceTenderId",
                table: "department_work_packages",
                column: "SourceTenderId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_department_work_packages_scope",
                table: "department_work_packages",
                sql: "([Kind] = 'Project' AND [OperationalProjectId] IS NOT NULL) OR ([Kind] = 'Internal' AND [OperationalProjectId] IS NULL AND [ContractId] IS NULL AND [SourceOpportunityId] IS NULL AND [SourceQuoteId] IS NULL AND [SourceTenderId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_department_finance_reviews_ReviewedByUserId",
                table: "department_finance_reviews",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_department_finance_reviews_WorkPackageId",
                table: "department_finance_reviews",
                column: "WorkPackageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_procurement_input_price_lines_OfferId_ItemCode",
                table: "procurement_input_price_lines",
                columns: new[] { "OfferId", "ItemCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_procurement_input_price_offers_ApprovedByUserId",
                table: "procurement_input_price_offers",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_procurement_input_price_offers_RejectedByUserId",
                table: "procurement_input_price_offers",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_procurement_input_price_offers_Status_ValidUntil",
                table: "procurement_input_price_offers",
                columns: new[] { "Status", "ValidUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_procurement_input_price_offers_VendorId",
                table: "procurement_input_price_offers",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_procurement_input_price_offers_WorkPackageId_SupplierReference_VendorId",
                table: "procurement_input_price_offers",
                columns: new[] { "WorkPackageId", "SupplierReference", "VendorId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_department_work_packages_quotes_SourceQuoteId",
                table: "department_work_packages",
                column: "SourceQuoteId",
                principalTable: "quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_department_work_packages_tenders_SourceTenderId",
                table: "department_work_packages",
                column: "SourceTenderId",
                principalTable: "tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_project_boq_revisions_contracts_UpstreamContractId",
                table: "project_boq_revisions",
                column: "UpstreamContractId",
                principalTable: "contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_quotes_procurement_input_price_offers_InputPriceOfferId",
                table: "quotes",
                column: "InputPriceOfferId",
                principalTable: "procurement_input_price_offers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tenders_operational_projects_OperationalProjectId",
                table: "tenders",
                column: "OperationalProjectId",
                principalTable: "operational_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_department_work_packages_quotes_SourceQuoteId",
                table: "department_work_packages");

            migrationBuilder.DropForeignKey(
                name: "FK_department_work_packages_tenders_SourceTenderId",
                table: "department_work_packages");

            migrationBuilder.DropForeignKey(
                name: "FK_project_boq_revisions_contracts_UpstreamContractId",
                table: "project_boq_revisions");

            migrationBuilder.DropForeignKey(
                name: "FK_quotes_procurement_input_price_offers_InputPriceOfferId",
                table: "quotes");

            migrationBuilder.DropForeignKey(
                name: "FK_tenders_operational_projects_OperationalProjectId",
                table: "tenders");

            migrationBuilder.DropTable(
                name: "department_finance_reviews");

            migrationBuilder.DropTable(
                name: "procurement_input_price_lines");

            migrationBuilder.DropTable(
                name: "procurement_input_price_offers");

            migrationBuilder.DropIndex(
                name: "IX_tenders_OperationalProjectId",
                table: "tenders");

            migrationBuilder.DropIndex(
                name: "IX_quotes_InputPriceOfferId",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "IX_project_boq_revisions_UpstreamContractId",
                table: "project_boq_revisions");

            migrationBuilder.DropIndex(
                name: "IX_department_work_packages_Purpose_Status",
                table: "department_work_packages");

            migrationBuilder.DropIndex(
                name: "IX_department_work_packages_SourceQuoteId",
                table: "department_work_packages");

            migrationBuilder.DropIndex(
                name: "IX_department_work_packages_SourceTenderId",
                table: "department_work_packages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_department_work_packages_scope",
                table: "department_work_packages");

            migrationBuilder.DropColumn(
                name: "OperationalProjectId",
                table: "tenders");

            migrationBuilder.DropColumn(
                name: "InputPriceOfferId",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "InputPriceSnapshotJson",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "InputPriceOfferId",
                table: "quote_version_snapshots");

            migrationBuilder.DropColumn(
                name: "InputPriceSnapshotJson",
                table: "quote_version_snapshots");

            migrationBuilder.DropColumn(
                name: "UpstreamContractId",
                table: "project_boq_revisions");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "department_work_packages");

            migrationBuilder.DropColumn(
                name: "SourceQuoteId",
                table: "department_work_packages");

            migrationBuilder.DropColumn(
                name: "SourceTenderId",
                table: "department_work_packages");

            migrationBuilder.AddCheckConstraint(
                name: "CK_department_work_packages_scope",
                table: "department_work_packages",
                sql: "([Kind] = 'Project' AND [OperationalProjectId] IS NOT NULL) OR ([Kind] = 'Internal' AND [OperationalProjectId] IS NULL AND [ContractId] IS NULL AND [SourceOpportunityId] IS NULL)");
        }
    }
}
