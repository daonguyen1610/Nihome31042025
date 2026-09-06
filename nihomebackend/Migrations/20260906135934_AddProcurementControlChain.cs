using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementControlChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "operational_projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FinalProjectBoqRevisionId",
                table: "operational_projects",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "material_requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SiteRequesterUserId = table.Column<int>(type: "int", nullable: false),
                    ResponsibleSiteUserId = table.Column<int>(type: "int", nullable: false),
                    AssignedProcurementUserId = table.Column<int>(type: "int", nullable: false),
                    RequiredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByUserId = table.Column<int>(type: "int", nullable: true),
                    FulfilledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_material_requests_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_material_requests_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_material_requests_users_AssignedProcurementUserId",
                        column: x => x.AssignedProcurementUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_material_requests_users_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_material_requests_users_ResponsibleSiteUserId",
                        column: x => x.ResponsibleSiteUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_material_requests_users_SiteRequesterUserId",
                        column: x => x.SiteRequesterUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "project_boq_revisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceTenderEstimateRevisionId = table.Column<int>(type: "int", nullable: true),
                    SourceContractAppendixId = table.Column<int>(type: "int", nullable: true),
                    CostTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PreparedByUserId = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SupersededAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalSelectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_boq_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_boq_revisions_contract_appendices_SourceContractAppendixId",
                        column: x => x.SourceContractAppendixId,
                        principalTable: "contract_appendices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_boq_revisions_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_boq_revisions_tender_estimate_revisions_SourceTenderEstimateRevisionId",
                        column: x => x.SourceTenderEstimateRevisionId,
                        principalTable: "tender_estimate_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_boq_revisions_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_project_boq_revisions_users_PreparedByUserId",
                        column: x => x.PreparedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_project_boq_revisions_users_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_project_boq_revisions_users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "vendor_ratings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProcurementOwnerUserId = table.Column<int>(type: "int", nullable: false),
                    QualityScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ScheduleScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CostScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    HseScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    OverallScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PreparedByUserId = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SupersedesVendorRatingId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendor_ratings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vendor_ratings_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vendor_ratings_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vendor_ratings_procurement_vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "procurement_vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vendor_ratings_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_vendor_ratings_users_PreparedByUserId",
                        column: x => x.PreparedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_vendor_ratings_users_ProcurementOwnerUserId",
                        column: x => x.ProcurementOwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_vendor_ratings_users_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_vendor_ratings_vendor_ratings_SupersedesVendorRatingId",
                        column: x => x.SupersedesVendorRatingId,
                        principalTable: "vendor_ratings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "warehouse_issues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReversalOfIssueId = table.Column<int>(type: "int", nullable: true),
                    ResponsibleSiteUserId = table.Column<int>(type: "int", nullable: false),
                    IssuedByUserId = table.Column<int>(type: "int", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedByUserId = table.Column<int>(type: "int", nullable: true),
                    WorkItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouse_issues_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_issues_users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_warehouse_issues_users_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_warehouse_issues_users_ResponsibleSiteUserId",
                        column: x => x.ResponsibleSiteUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_warehouse_issues_warehouse_issues_ReversalOfIssueId",
                        column: x => x.ReversalOfIssueId,
                        principalTable: "warehouse_issues",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "warehouse_receipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReversalOfReceiptId = table.Column<int>(type: "int", nullable: true),
                    ReceivedByUserId = table.Column<int>(type: "int", nullable: false),
                    InspectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouse_receipts_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_receipts_users_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_warehouse_receipts_users_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_warehouse_receipts_warehouse_receipts_ReversalOfReceiptId",
                        column: x => x.ReversalOfReceiptId,
                        principalTable: "warehouse_receipts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "project_boq_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectBoqRevisionId = table.Column<int>(type: "int", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApprovedQuantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    BudgetUnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_boq_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_boq_lines_project_boq_revisions_ProjectBoqRevisionId",
                        column: x => x.ProjectBoqRevisionId,
                        principalTable: "project_boq_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    ProjectBoqLineId = table.Column<int>(type: "int", nullable: false),
                    ProcurementOwnerUserId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    NegotiatedUnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_lines_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contract_lines_project_boq_lines_ProjectBoqLineId",
                        column: x => x.ProjectBoqLineId,
                        principalTable: "project_boq_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contract_lines_users_ProcurementOwnerUserId",
                        column: x => x.ProcurementOwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "material_request_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialRequestId = table.Column<int>(type: "int", nullable: false),
                    ProjectBoqLineId = table.Column<int>(type: "int", nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_request_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_material_request_lines_material_requests_MaterialRequestId",
                        column: x => x.MaterialRequestId,
                        principalTable: "material_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_material_request_lines_project_boq_lines_ProjectBoqLineId",
                        column: x => x.ProjectBoqLineId,
                        principalTable: "project_boq_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_issue_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseIssueId = table.Column<int>(type: "int", nullable: false),
                    ProjectBoqLineId = table.Column<int>(type: "int", nullable: false),
                    IssuedQuantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_issue_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouse_issue_lines_project_boq_lines_ProjectBoqLineId",
                        column: x => x.ProjectBoqLineId,
                        principalTable: "project_boq_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_issue_lines_warehouse_issues_WarehouseIssueId",
                        column: x => x.WarehouseIssueId,
                        principalTable: "warehouse_issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_receipt_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseReceiptId = table.Column<int>(type: "int", nullable: false),
                    MaterialRequestLineId = table.Column<int>(type: "int", nullable: false),
                    ContractLineId = table.Column<int>(type: "int", nullable: true),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_receipt_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouse_receipt_lines_contract_lines_ContractLineId",
                        column: x => x.ContractLineId,
                        principalTable: "contract_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_receipt_lines_material_request_lines_MaterialRequestLineId",
                        column: x => x.MaterialRequestLineId,
                        principalTable: "material_request_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_receipt_lines_warehouse_receipts_WarehouseReceiptId",
                        column: x => x.WarehouseReceiptId,
                        principalTable: "warehouse_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operational_projects_FinalProjectBoqRevisionId",
                table: "operational_projects",
                column: "FinalProjectBoqRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_lines_ContractId_ProjectBoqLineId",
                table: "contract_lines",
                columns: new[] { "ContractId", "ProjectBoqLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_lines_ProcurementOwnerUserId",
                table: "contract_lines",
                column: "ProcurementOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_lines_ProjectBoqLineId",
                table: "contract_lines",
                column: "ProjectBoqLineId");

            migrationBuilder.CreateIndex(
                name: "IX_material_request_lines_MaterialRequestId_ProjectBoqLineId",
                table: "material_request_lines",
                columns: new[] { "MaterialRequestId", "ProjectBoqLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_request_lines_ProjectBoqLineId",
                table: "material_request_lines",
                column: "ProjectBoqLineId");

            migrationBuilder.CreateIndex(
                name: "IX_material_requests_ApprovedByUserId",
                table: "material_requests",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_material_requests_AssignedProcurementUserId_FulfilledAt",
                table: "material_requests",
                columns: new[] { "AssignedProcurementUserId", "FulfilledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_material_requests_OperationalProjectId_Code",
                table: "material_requests",
                columns: new[] { "OperationalProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_requests_RejectedByUserId",
                table: "material_requests",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_material_requests_ResponsibleSiteUserId",
                table: "material_requests",
                column: "ResponsibleSiteUserId");

            migrationBuilder.CreateIndex(
                name: "IX_material_requests_SiteRequesterUserId",
                table: "material_requests",
                column: "SiteRequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_boq_lines_ProjectBoqRevisionId_ItemCode",
                table: "project_boq_lines",
                columns: new[] { "ProjectBoqRevisionId", "ItemCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_boq_lines_ProjectBoqRevisionId_SortOrder",
                table: "project_boq_lines",
                columns: new[] { "ProjectBoqRevisionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_project_boq_revisions_ApprovedByUserId",
                table: "project_boq_revisions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_boq_revisions_OperationalProjectId_RevisionNumber",
                table: "project_boq_revisions",
                columns: new[] { "OperationalProjectId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_boq_revisions_OperationalProjectId_Status_ApprovedAt",
                table: "project_boq_revisions",
                columns: new[] { "OperationalProjectId", "Status", "ApprovedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_project_boq_revisions_PreparedByUserId",
                table: "project_boq_revisions",
                column: "PreparedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_boq_revisions_RejectedByUserId",
                table: "project_boq_revisions",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_boq_revisions_SourceContractAppendixId",
                table: "project_boq_revisions",
                column: "SourceContractAppendixId");

            migrationBuilder.CreateIndex(
                name: "IX_project_boq_revisions_SourceTenderEstimateRevisionId",
                table: "project_boq_revisions",
                column: "SourceTenderEstimateRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_project_boq_revisions_SubmittedByUserId",
                table: "project_boq_revisions",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_ratings_ApprovedByUserId",
                table: "vendor_ratings",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_ratings_ContractId",
                table: "vendor_ratings",
                column: "ContractId",
                unique: true,
                filter: "[Status] = 'Approved'");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_ratings_ContractId_VersionNumber",
                table: "vendor_ratings",
                columns: new[] { "ContractId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vendor_ratings_OperationalProjectId",
                table: "vendor_ratings",
                column: "OperationalProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_ratings_PreparedByUserId",
                table: "vendor_ratings",
                column: "PreparedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_ratings_ProcurementOwnerUserId_ApprovedAt_Status",
                table: "vendor_ratings",
                columns: new[] { "ProcurementOwnerUserId", "ApprovedAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_vendor_ratings_RejectedByUserId",
                table: "vendor_ratings",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_ratings_SupersedesVendorRatingId",
                table: "vendor_ratings",
                column: "SupersedesVendorRatingId");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_ratings_VendorId",
                table: "vendor_ratings",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_issue_lines_ProjectBoqLineId",
                table: "warehouse_issue_lines",
                column: "ProjectBoqLineId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_issue_lines_WarehouseIssueId_ProjectBoqLineId",
                table: "warehouse_issue_lines",
                columns: new[] { "WarehouseIssueId", "ProjectBoqLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_issues_IssuedByUserId",
                table: "warehouse_issues",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_issues_OperationalProjectId_Code",
                table: "warehouse_issues",
                columns: new[] { "OperationalProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_issues_PostedByUserId",
                table: "warehouse_issues",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_issues_ResponsibleSiteUserId_PostedAt_Status",
                table: "warehouse_issues",
                columns: new[] { "ResponsibleSiteUserId", "PostedAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_issues_ReversalOfIssueId",
                table: "warehouse_issues",
                column: "ReversalOfIssueId",
                unique: true,
                filter: "[ReversalOfIssueId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_receipt_lines_ContractLineId",
                table: "warehouse_receipt_lines",
                column: "ContractLineId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_receipt_lines_MaterialRequestLineId",
                table: "warehouse_receipt_lines",
                column: "MaterialRequestLineId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_receipt_lines_WarehouseReceiptId_MaterialRequestLineId",
                table: "warehouse_receipt_lines",
                columns: new[] { "WarehouseReceiptId", "MaterialRequestLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_receipts_OperationalProjectId_Code",
                table: "warehouse_receipts",
                columns: new[] { "OperationalProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_receipts_PostedByUserId",
                table: "warehouse_receipts",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_receipts_ReceivedByUserId",
                table: "warehouse_receipts",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_receipts_ReversalOfReceiptId",
                table: "warehouse_receipts",
                column: "ReversalOfReceiptId",
                unique: true,
                filter: "[ReversalOfReceiptId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_operational_projects_project_boq_revisions_FinalProjectBoqRevisionId",
                table: "operational_projects",
                column: "FinalProjectBoqRevisionId",
                principalTable: "project_boq_revisions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_operational_projects_project_boq_revisions_FinalProjectBoqRevisionId",
                table: "operational_projects");

            migrationBuilder.DropTable(
                name: "vendor_ratings");

            migrationBuilder.DropTable(
                name: "warehouse_issue_lines");

            migrationBuilder.DropTable(
                name: "warehouse_receipt_lines");

            migrationBuilder.DropTable(
                name: "warehouse_issues");

            migrationBuilder.DropTable(
                name: "contract_lines");

            migrationBuilder.DropTable(
                name: "material_request_lines");

            migrationBuilder.DropTable(
                name: "warehouse_receipts");

            migrationBuilder.DropTable(
                name: "material_requests");

            migrationBuilder.DropTable(
                name: "project_boq_lines");

            migrationBuilder.DropTable(
                name: "project_boq_revisions");

            migrationBuilder.DropIndex(
                name: "IX_operational_projects_FinalProjectBoqRevisionId",
                table: "operational_projects");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "operational_projects");

            migrationBuilder.DropColumn(
                name: "FinalProjectBoqRevisionId",
                table: "operational_projects");
        }
    }
}
