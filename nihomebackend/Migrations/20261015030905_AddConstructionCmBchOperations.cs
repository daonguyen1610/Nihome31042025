using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddConstructionCmBchOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "construction_preparations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SupersedesPreparationId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    WbsBaselineReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ScheduleBaselineReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SCurveBaselineReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BchOrganizationReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResourcePlanReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BudgetBaselineReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BudgetBaselineLocked = table.Column<bool>(type: "bit", nullable: false),
                    SiteHandoverRequired = table.Column<bool>(type: "bit", nullable: false),
                    SiteHandoverComplete = table.Column<bool>(type: "bit", nullable: false),
                    MethodStatementRequired = table.Column<bool>(type: "bit", nullable: false),
                    MethodStatementApproved = table.Column<bool>(type: "bit", nullable: false),
                    HsePlanRequired = table.Column<bool>(type: "bit", nullable: false),
                    HsePlanApproved = table.Column<bool>(type: "bit", nullable: false),
                    ReadinessSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedByUserId = table.Column<int>(type: "int", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_preparations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_construction_preparations_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "construction_qs_batches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    WbsCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProjectBoqRevisionId = table.Column<int>(type: "int", nullable: true),
                    ContractAppendixId = table.Column<int>(type: "int", nullable: true),
                    ContractPaymentMilestoneId = table.Column<int>(type: "int", nullable: true),
                    CertifiedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CmReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    CmReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CmDecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SupersedesBatchId = table.Column<int>(type: "int", nullable: true),
                    ReversesBatchId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_qs_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_construction_qs_batches_contract_appendices_ContractAppendixId",
                        column: x => x.ContractAppendixId,
                        principalTable: "contract_appendices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_construction_qs_batches_contract_payment_milestones_ContractPaymentMilestoneId",
                        column: x => x.ContractPaymentMilestoneId,
                        principalTable: "contract_payment_milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_construction_qs_batches_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_construction_qs_batches_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_construction_qs_batches_project_boq_revisions_ProjectBoqRevisionId",
                        column: x => x.ProjectBoqRevisionId,
                        principalTable: "project_boq_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "construction_resource_requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    WbsCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NeedByDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ProjectBoqLineId = table.Column<int>(type: "int", nullable: true),
                    ResponsibleSiteUserId = table.Column<int>(type: "int", nullable: true),
                    AssignedProcurementUserId = table.Column<int>(type: "int", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CmReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CmReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    CmDecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HandoffEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HandoffEntityId = table.Column<long>(type: "bigint", nullable: true),
                    HandoffStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_resource_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_construction_resource_requests_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "qa_qc_templates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    NameVi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameZh = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameJa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qa_qc_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "construction_qs_batch_evidence",
                columns: table => new
                {
                    ConstructionQsBatchId = table.Column<int>(type: "int", nullable: false),
                    ProjectDocumentId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_qs_batch_evidence", x => new { x.ConstructionQsBatchId, x.ProjectDocumentId });
                    table.ForeignKey(
                        name: "FK_construction_qs_batch_evidence_construction_qs_batches_ConstructionQsBatchId",
                        column: x => x.ConstructionQsBatchId,
                        principalTable: "construction_qs_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_construction_qs_batch_evidence_project_documents_ProjectDocumentId",
                        column: x => x.ProjectDocumentId,
                        principalTable: "project_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "construction_qs_batch_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConstructionQsBatchId = table.Column<int>(type: "int", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_qs_batch_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_construction_qs_batch_lines_construction_qs_batches_ConstructionQsBatchId",
                        column: x => x.ConstructionQsBatchId,
                        principalTable: "construction_qs_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qa_qc_dossiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    QaQcTemplateId = table.Column<int>(type: "int", nullable: false),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    TemplateSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IfcReleaseId = table.Column<int>(type: "int", nullable: true),
                    ChecklistResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WbsCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CmReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    CmReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CmDecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qa_qc_dossiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qa_qc_dossiers_ifc_releases_IfcReleaseId",
                        column: x => x.IfcReleaseId,
                        principalTable: "ifc_releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_qa_qc_dossiers_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_qa_qc_dossiers_qa_qc_templates_QaQcTemplateId",
                        column: x => x.QaQcTemplateId,
                        principalTable: "qa_qc_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "qa_qc_dossier_evidence",
                columns: table => new
                {
                    QaQcDossierId = table.Column<int>(type: "int", nullable: false),
                    ProjectDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    EvidenceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qa_qc_dossier_evidence", x => new { x.QaQcDossierId, x.ProjectDocumentId });
                    table.ForeignKey(
                        name: "FK_qa_qc_dossier_evidence_project_documents_ProjectDocumentId",
                        column: x => x.ProjectDocumentId,
                        principalTable: "project_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_qa_qc_dossier_evidence_qa_qc_dossiers_QaQcDossierId",
                        column: x => x.QaQcDossierId,
                        principalTable: "qa_qc_dossiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_construction_preparations_OperationalProjectId_Version",
                table: "construction_preparations",
                columns: new[] { "OperationalProjectId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_construction_qs_batch_evidence_ProjectDocumentId",
                table: "construction_qs_batch_evidence",
                column: "ProjectDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_qs_batch_lines_ConstructionQsBatchId",
                table: "construction_qs_batch_lines",
                column: "ConstructionQsBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_qs_batches_ContractAppendixId",
                table: "construction_qs_batches",
                column: "ContractAppendixId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_qs_batches_ContractId",
                table: "construction_qs_batches",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_qs_batches_ContractPaymentMilestoneId",
                table: "construction_qs_batches",
                column: "ContractPaymentMilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_qs_batches_OperationalProjectId_Code",
                table: "construction_qs_batches",
                columns: new[] { "OperationalProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_construction_qs_batches_ProjectBoqRevisionId",
                table: "construction_qs_batches",
                column: "ProjectBoqRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_resource_requests_OperationalProjectId_Code",
                table: "construction_resource_requests",
                columns: new[] { "OperationalProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_construction_resource_requests_OperationalProjectId_Status",
                table: "construction_resource_requests",
                columns: new[] { "OperationalProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_qa_qc_dossier_evidence_ProjectDocumentId",
                table: "qa_qc_dossier_evidence",
                column: "ProjectDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_qa_qc_dossiers_IfcReleaseId",
                table: "qa_qc_dossiers",
                column: "IfcReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_qa_qc_dossiers_OperationalProjectId_Code",
                table: "qa_qc_dossiers",
                columns: new[] { "OperationalProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qa_qc_dossiers_QaQcTemplateId",
                table: "qa_qc_dossiers",
                column: "QaQcTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_qa_qc_templates_Code_Version",
                table: "qa_qc_templates",
                columns: new[] { "Code", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "construction_preparations");

            migrationBuilder.DropTable(
                name: "construction_qs_batch_evidence");

            migrationBuilder.DropTable(
                name: "construction_qs_batch_lines");

            migrationBuilder.DropTable(
                name: "construction_resource_requests");

            migrationBuilder.DropTable(
                name: "qa_qc_dossier_evidence");

            migrationBuilder.DropTable(
                name: "construction_qs_batches");

            migrationBuilder.DropTable(
                name: "qa_qc_dossiers");

            migrationBuilder.DropTable(
                name: "qa_qc_templates");
        }
    }
}
