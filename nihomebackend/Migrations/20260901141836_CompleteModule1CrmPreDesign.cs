using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class CompleteModule1CrmPreDesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OperationalProjectId",
                table: "surveys",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CatalogUnitPricePerSqm",
                table: "quotes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaterialRateRevisionId",
                table: "quotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PricingEffectiveDate",
                table: "quotes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RateOverrideAt",
                table: "quotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RateOverrideByUserId",
                table: "quotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateOverrideReason",
                table: "quotes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateSource",
                table: "quotes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Override");

            migrationBuilder.AddColumn<decimal>(
                name: "CatalogUnitPricePerSqm",
                table: "quote_version_snapshots",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaterialRateRevisionId",
                table: "quote_version_snapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PricingEffectiveDate",
                table: "quote_version_snapshots",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RateOverrideAt",
                table: "quote_version_snapshots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RateOverrideByUserId",
                table: "quote_version_snapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateOverrideReason",
                table: "quote_version_snapshots",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateSource",
                table: "quote_version_snapshots",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Override");

            migrationBuilder.Sql("""
                UPDATE opportunity
                SET LostReasonCode = CASE
                        WHEN LostReasonCode IN ('price', 'competitor', 'timing', 'technical', 'other')
                            THEN LostReasonCode
                        ELSE 'other'
                    END,
                    LostNote = CASE
                        WHEN NULLIF(LTRIM(RTRIM(LostNote)), '') IS NOT NULL THEN LostNote
                        ELSE N'Dữ liệu được chuẩn hóa khi chuyển sang quy trình Module 1.'
                    END,
                    WinProbability = 0,
                    ClosedAt = COALESCE(ClosedAt, UpdatedAt, CreatedAt, SYSUTCDATETIME()),
                    WonQuoteId = NULL,
                    WonTenderId = NULL
                FROM opportunities opportunity
                WHERE opportunity.Stage = 'Lost';

                UPDATE opportunity
                SET WinProbability = 100,
                    ClosedAt = COALESCE(ClosedAt, contractEvidence.SignedAt, UpdatedAt, CreatedAt, SYSUTCDATETIME()),
                    LostReasonCode = NULL,
                    LostNote = NULL,
                    WonQuoteId = CASE
                        WHEN WonQuoteId IS NULL OR EXISTS (
                            SELECT 1 FROM quotes quote
                            WHERE quote.Id = WonQuoteId AND quote.OpportunityId = opportunity.Id
                        ) THEN WonQuoteId
                        ELSE NULL
                    END,
                    WonTenderId = CASE
                        WHEN WonTenderId IS NULL OR EXISTS (
                            SELECT 1 FROM tenders tender
                            WHERE tender.Id = WonTenderId
                              AND tender.WonOpportunityId = opportunity.Id
                              AND tender.CustomerId = opportunity.CustomerId
                        ) THEN WonTenderId
                        ELSE NULL
                    END
                FROM opportunities opportunity
                CROSS APPLY (
                    SELECT MIN(contract.SignedDate) AS SignedAt
                    FROM contracts contract
                    WHERE contract.OpportunityId = opportunity.Id
                      AND contract.CustomerId = opportunity.CustomerId
                      AND contract.SignedDate IS NOT NULL
                      AND contract.Status NOT IN ('Draft', 'Cancelled')
                ) contractEvidence
                WHERE opportunity.Stage = 'Won'
                  AND contractEvidence.SignedAt IS NOT NULL;

                UPDATE opportunity
                SET Stage = 'Negotiation',
                    WinProbability = CASE WHEN WinProbability >= 100 THEN 75 ELSE WinProbability END,
                    ClosedAt = NULL,
                    LostReasonCode = NULL,
                    LostNote = NULL,
                    WonQuoteId = NULL,
                    WonTenderId = NULL
                FROM opportunities opportunity
                WHERE opportunity.Stage = 'Won'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM contracts contract
                      WHERE contract.OpportunityId = opportunity.Id
                        AND contract.CustomerId = opportunity.CustomerId
                        AND contract.SignedDate IS NOT NULL
                        AND contract.Status NOT IN ('Draft', 'Cancelled')
                  );

                UPDATE quotes
                SET RateSource = 'Override',
                    RateOverrideReason = COALESCE(
                        NULLIF(LTRIM(RTRIM(RateOverrideReason)), ''),
                        N'Giá được chuyển đổi từ dữ liệu trước Module 1.'),
                    RateOverrideByUserId = COALESCE(RateOverrideByUserId, CreatedByUserId),
                    RateOverrideAt = COALESCE(RateOverrideAt, CreatedAt, SYSUTCDATETIME());

                UPDATE quote_version_snapshots
                SET RateSource = 'Override',
                    RateOverrideReason = COALESCE(
                        NULLIF(LTRIM(RTRIM(RateOverrideReason)), ''),
                        N'Giá được chuyển đổi từ dữ liệu trước Module 1.'),
                    RateOverrideByUserId = COALESCE(RateOverrideByUserId, CreatedByUserId),
                    RateOverrideAt = COALESCE(RateOverrideAt, CreatedAt, SYSUTCDATETIME());

                UPDATE survey
                SET OperationalProjectId = opportunity.OperationalProjectId
                FROM surveys survey
                INNER JOIN opportunities opportunity
                    ON opportunity.Id = survey.LinkedOpportunityId
                WHERE survey.OperationalProjectId IS NULL
                  AND opportunity.OperationalProjectId IS NOT NULL;

                IF EXISTS (SELECT 1 FROM surveys WHERE OperationalProjectId IS NULL)
                    THROW 51000, 'Không thể chuyển đổi phiếu khảo sát: thiếu Dự án vận hành hợp lệ.', 1;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "OperationalProjectId",
                table: "surveys",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SegmentCode",
                table: "leads",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "unclassified");

            migrationBuilder.CreateTable(
                name: "material_rate_catalogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_rate_catalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "survey_site_conditions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurveyId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NumericValue = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    UnitCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReferenceCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_site_conditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_survey_site_conditions_surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tender_estimate_revisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenderId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    VatPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    CostSubtotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    BidSubtotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    GrandBidTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SourceSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ImportedByUserId = table.Column<int>(type: "int", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByUserId = table.Column<int>(type: "int", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tender_estimate_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tender_estimate_revisions_tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "material_rate_revisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CatalogId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_rate_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_material_rate_revisions_material_rate_catalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalTable: "material_rate_catalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tender_estimate_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RevisionId = table.Column<int>(type: "int", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    BidUnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    BidAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tender_estimate_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tender_estimate_lines_tender_estimate_revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "tender_estimate_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "material_rate_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RevisionId = table.Column<int>(type: "int", nullable: false),
                    MaterialCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NormPerSqm = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitRate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    WastePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    AmountPerSqm = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_rate_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_material_rate_lines_material_rate_revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "material_rate_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_surveys_OperationalProjectId",
                table: "surveys",
                column: "OperationalProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_MaterialRateRevisionId",
                table: "quotes",
                column: "MaterialRateRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_leads_SegmentCode",
                table: "leads",
                column: "SegmentCode");

            migrationBuilder.CreateIndex(
                name: "IX_material_rate_catalogs_Code",
                table: "material_rate_catalogs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_rate_catalogs_IsActive",
                table: "material_rate_catalogs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_material_rate_lines_RevisionId_MaterialCode",
                table: "material_rate_lines",
                columns: new[] { "RevisionId", "MaterialCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_rate_revisions_CatalogId_Status_EffectiveFrom",
                table: "material_rate_revisions",
                columns: new[] { "CatalogId", "Status", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_material_rate_revisions_CatalogId_Version",
                table: "material_rate_revisions",
                columns: new[] { "CatalogId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_site_conditions_SurveyId_Category_Code",
                table: "survey_site_conditions",
                columns: new[] { "SurveyId", "Category", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tender_estimate_lines_RevisionId_ItemCode",
                table: "tender_estimate_lines",
                columns: new[] { "RevisionId", "ItemCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tender_estimate_lines_RevisionId_SortOrder",
                table: "tender_estimate_lines",
                columns: new[] { "RevisionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_tender_estimate_revisions_SourceSha256",
                table: "tender_estimate_revisions",
                column: "SourceSha256");

            migrationBuilder.CreateIndex(
                name: "IX_tender_estimate_revisions_TenderId_Status",
                table: "tender_estimate_revisions",
                columns: new[] { "TenderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_tender_estimate_revisions_TenderId_VersionNumber",
                table: "tender_estimate_revisions",
                columns: new[] { "TenderId", "VersionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_quotes_material_rate_revisions_MaterialRateRevisionId",
                table: "quotes",
                column: "MaterialRateRevisionId",
                principalTable: "material_rate_revisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_surveys_operational_projects_OperationalProjectId",
                table: "surveys",
                column: "OperationalProjectId",
                principalTable: "operational_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_quotes_material_rate_revisions_MaterialRateRevisionId",
                table: "quotes");

            migrationBuilder.DropForeignKey(
                name: "FK_surveys_operational_projects_OperationalProjectId",
                table: "surveys");

            migrationBuilder.DropTable(
                name: "material_rate_lines");

            migrationBuilder.DropTable(
                name: "survey_site_conditions");

            migrationBuilder.DropTable(
                name: "tender_estimate_lines");

            migrationBuilder.DropTable(
                name: "material_rate_revisions");

            migrationBuilder.DropTable(
                name: "tender_estimate_revisions");

            migrationBuilder.DropTable(
                name: "material_rate_catalogs");

            migrationBuilder.DropIndex(
                name: "IX_surveys_OperationalProjectId",
                table: "surveys");

            migrationBuilder.DropIndex(
                name: "IX_quotes_MaterialRateRevisionId",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "IX_leads_SegmentCode",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "OperationalProjectId",
                table: "surveys");

            migrationBuilder.DropColumn(
                name: "CatalogUnitPricePerSqm",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "MaterialRateRevisionId",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "PricingEffectiveDate",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "RateOverrideAt",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "RateOverrideByUserId",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "RateOverrideReason",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "RateSource",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "CatalogUnitPricePerSqm",
                table: "quote_version_snapshots");

            migrationBuilder.DropColumn(
                name: "MaterialRateRevisionId",
                table: "quote_version_snapshots");

            migrationBuilder.DropColumn(
                name: "PricingEffectiveDate",
                table: "quote_version_snapshots");

            migrationBuilder.DropColumn(
                name: "RateOverrideAt",
                table: "quote_version_snapshots");

            migrationBuilder.DropColumn(
                name: "RateOverrideByUserId",
                table: "quote_version_snapshots");

            migrationBuilder.DropColumn(
                name: "RateOverrideReason",
                table: "quote_version_snapshots");

            migrationBuilder.DropColumn(
                name: "RateSource",
                table: "quote_version_snapshots");

            migrationBuilder.DropColumn(
                name: "SegmentCode",
                table: "leads");
        }
    }
}
