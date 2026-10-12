using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeTenderReadinessRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvitationPortalUrl",
                table: "tenders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvitationReceivedByUserId",
                table: "tenders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitationSource",
                table: "tenders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "tenders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "EstimateSnapshotJson",
                table: "tender_submissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FinanceReviewSnapshotJson",
                table: "tender_submissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "LegacyUnknown",
                table: "tender_submissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlanningSnapshotJson",
                table: "tender_submissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceiptReference",
                table: "tender_submissions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionChannel",
                table: "tender_submissions",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "LegacyUnknown");

            migrationBuilder.AddColumn<int>(
                name: "CapabilityDocumentVersion",
                table: "tender_submission_items",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EvidenceId",
                table: "tender_submission_items",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotApplicableReason",
                table: "tender_submission_items",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "tender_submission_items",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentEvidenceId",
                table: "tender_checklist_items",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentKind",
                table: "tender_checklist_items",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Custom");

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "tender_checklist_items",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerifiedByUserId",
                table: "tender_checklist_items",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkTaskId",
                table: "tender_checklist_items",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tender_checklist_evidence",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChecklistItemId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CapabilityDocumentId = table.Column<int>(type: "int", nullable: true),
                    CapabilityDocumentVersion = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tender_checklist_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tender_checklist_evidence_capability_documents_CapabilityDocumentId",
                        column: x => x.CapabilityDocumentId,
                        principalTable: "capability_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tender_checklist_evidence_tender_checklist_items_ChecklistItemId",
                        column: x => x.ChecklistItemId,
                        principalTable: "tender_checklist_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tender_checklist_evidence_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tender_invitation_documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenderId = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ExternalUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tender_invitation_documents", x => x.Id);
                    table.CheckConstraint("CK_tender_invitation_documents_source", "([FilePath] IS NOT NULL AND [ExternalUrl] IS NULL) OR ([FilePath] IS NULL AND [ExternalUrl] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_tender_invitation_documents_tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tender_invitation_documents_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tender_milestones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenderId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AchievedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AchievedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tender_milestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tender_milestones_tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tender_milestones_users_AchievedByUserId",
                        column: x => x.AchievedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                UPDATE t
                SET InvitationSource = COALESCE(NULLIF(LTRIM(RTRIM(t.InfoSource)), ''), 'LegacyUnknown'),
                    InvitationReceivedByUserId = COALESCE(t.CreatedByUserId, t.UpdatedByUserId,
                        (SELECT MIN(u.Id) FROM users u))
                FROM tenders t;

                UPDATE tender_submissions
                SET SubmissionChannel = CASE WHEN NULLIF(LTRIM(RTRIM(SubmissionChannel)), '') IS NULL
                    THEN 'LegacyUnknown' ELSE SubmissionChannel END,
                    LegacyUnknown = 1,
                    PlanningSnapshotJson = CASE WHEN NULLIF(PlanningSnapshotJson, '') IS NULL THEN '[]' ELSE PlanningSnapshotJson END,
                    EstimateSnapshotJson = CASE WHEN NULLIF(EstimateSnapshotJson, '') IS NULL THEN '{}' ELSE EstimateSnapshotJson END,
                    FinanceReviewSnapshotJson = CASE WHEN NULLIF(FinanceReviewSnapshotJson, '') IS NULL THEN '{}' ELSE FinanceReviewSnapshotJson END;

                UPDATE tender_checklist_items
                SET DocumentKind = CASE LOWER(COALESCE(TemplateCode, ''))
                    WHEN 'capability' THEN 'Capability'
                    WHEN 'legal' THEN 'Legal'
                    WHEN 'bid-security' THEN 'BidSecurity'
                    WHEN 'boq' THEN 'Boq'
                    WHEN 'method-statement' THEN 'MethodStatement'
                    WHEN 'drawings' THEN 'Drawings'
                    WHEN 'schedule' THEN 'Schedule'
                    ELSE 'Custom' END;

                INSERT INTO tender_checklist_evidence
                    (ChecklistItemId, Revision, Source, FilePath, OriginalFileName, FileSize,
                     ContentType, Sha256, CapabilityDocumentId, CapabilityDocumentVersion,
                     CreatedByUserId, CreatedAt)
                SELECT i.Id, 1, 'Legacy', i.FilePath, i.OriginalFileName, 0,
                       'application/octet-stream', NULL, i.CapabilityDocumentId,
                       CASE WHEN i.CapabilityDocumentId IS NOT NULL THEN d.CurrentVersion ELSE NULL END,
                       COALESCE(i.CompletedByUserId, i.OwnerUserId, t.UpdatedByUserId,
                           t.CreatedByUserId, (SELECT MIN(u.Id) FROM users u)),
                       COALESCE(i.CompletedAt, i.UpdatedAt, i.CreatedAt)
                FROM tender_checklist_items i
                INNER JOIN tenders t ON t.Id = i.TenderId
                LEFT JOIN capability_documents d ON d.Id = i.CapabilityDocumentId
                WHERE i.FilePath IS NOT NULL AND i.OriginalFileName IS NOT NULL;

                UPDATE i
                SET CurrentEvidenceId = e.Id
                FROM tender_checklist_items i
                INNER JOIN tender_checklist_evidence e
                    ON e.ChecklistItemId = i.Id AND e.Revision = 1;

                UPDATE si
                SET EvidenceId = e.Id,
                    CapabilityDocumentVersion = e.CapabilityDocumentVersion,
                    Sha256 = e.Sha256
                FROM tender_submission_items si
                INNER JOIN tender_checklist_evidence e
                    ON e.ChecklistItemId = si.ChecklistItemId AND e.Revision = 1
                WHERE si.FilePath = e.FilePath;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "InvitationReceivedByUserId",
                table: "tenders",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InvitationSource",
                table: "tenders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenders_InvitationReceivedByUserId",
                table: "tenders",
                column: "InvitationReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_submission_items_EvidenceId",
                table: "tender_submission_items",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_checklist_items_CurrentEvidenceId",
                table: "tender_checklist_items",
                column: "CurrentEvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_checklist_items_VerifiedByUserId",
                table: "tender_checklist_items",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_checklist_items_WorkTaskId",
                table: "tender_checklist_items",
                column: "WorkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_checklist_evidence_CapabilityDocumentId",
                table: "tender_checklist_evidence",
                column: "CapabilityDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_checklist_evidence_ChecklistItemId_Revision",
                table: "tender_checklist_evidence",
                columns: new[] { "ChecklistItemId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tender_checklist_evidence_CreatedByUserId",
                table: "tender_checklist_evidence",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_invitation_documents_CreatedByUserId",
                table: "tender_invitation_documents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_invitation_documents_TenderId",
                table: "tender_invitation_documents",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_milestones_AchievedByUserId",
                table: "tender_milestones",
                column: "AchievedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_milestones_TenderId_Kind",
                table: "tender_milestones",
                columns: new[] { "TenderId", "Kind" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tender_checklist_items_department_work_tasks_WorkTaskId",
                table: "tender_checklist_items",
                column: "WorkTaskId",
                principalTable: "department_work_tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tender_checklist_items_tender_checklist_evidence_CurrentEvidenceId",
                table: "tender_checklist_items",
                column: "CurrentEvidenceId",
                principalTable: "tender_checklist_evidence",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_tender_checklist_items_users_VerifiedByUserId",
                table: "tender_checklist_items",
                column: "VerifiedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tender_submission_items_tender_checklist_evidence_EvidenceId",
                table: "tender_submission_items",
                column: "EvidenceId",
                principalTable: "tender_checklist_evidence",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tenders_users_InvitationReceivedByUserId",
                table: "tenders",
                column: "InvitationReceivedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tender_checklist_items_department_work_tasks_WorkTaskId",
                table: "tender_checklist_items");

            migrationBuilder.DropForeignKey(
                name: "FK_tender_checklist_items_tender_checklist_evidence_CurrentEvidenceId",
                table: "tender_checklist_items");

            migrationBuilder.DropForeignKey(
                name: "FK_tender_checklist_items_users_VerifiedByUserId",
                table: "tender_checklist_items");

            migrationBuilder.DropForeignKey(
                name: "FK_tender_submission_items_tender_checklist_evidence_EvidenceId",
                table: "tender_submission_items");

            migrationBuilder.DropForeignKey(
                name: "FK_tenders_users_InvitationReceivedByUserId",
                table: "tenders");

            migrationBuilder.DropTable(
                name: "tender_checklist_evidence");

            migrationBuilder.DropTable(
                name: "tender_invitation_documents");

            migrationBuilder.DropTable(
                name: "tender_milestones");

            migrationBuilder.DropIndex(
                name: "IX_tenders_InvitationReceivedByUserId",
                table: "tenders");

            migrationBuilder.DropIndex(
                name: "IX_tender_submission_items_EvidenceId",
                table: "tender_submission_items");

            migrationBuilder.DropIndex(
                name: "IX_tender_checklist_items_CurrentEvidenceId",
                table: "tender_checklist_items");

            migrationBuilder.DropIndex(
                name: "IX_tender_checklist_items_VerifiedByUserId",
                table: "tender_checklist_items");

            migrationBuilder.DropIndex(
                name: "IX_tender_checklist_items_WorkTaskId",
                table: "tender_checklist_items");

            migrationBuilder.DropColumn(
                name: "InvitationPortalUrl",
                table: "tenders");

            migrationBuilder.DropColumn(
                name: "InvitationReceivedByUserId",
                table: "tenders");

            migrationBuilder.DropColumn(
                name: "InvitationSource",
                table: "tenders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "tenders");

            migrationBuilder.DropColumn(
                name: "EstimateSnapshotJson",
                table: "tender_submissions");

            migrationBuilder.DropColumn(
                name: "FinanceReviewSnapshotJson",
                table: "tender_submissions");

            migrationBuilder.DropColumn(
                name: "LegacyUnknown",
                table: "tender_submissions");

            migrationBuilder.DropColumn(
                name: "PlanningSnapshotJson",
                table: "tender_submissions");

            migrationBuilder.DropColumn(
                name: "ReceiptReference",
                table: "tender_submissions");

            migrationBuilder.DropColumn(
                name: "SubmissionChannel",
                table: "tender_submissions");

            migrationBuilder.DropColumn(
                name: "CapabilityDocumentVersion",
                table: "tender_submission_items");

            migrationBuilder.DropColumn(
                name: "EvidenceId",
                table: "tender_submission_items");

            migrationBuilder.DropColumn(
                name: "NotApplicableReason",
                table: "tender_submission_items");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "tender_submission_items");

            migrationBuilder.DropColumn(
                name: "CurrentEvidenceId",
                table: "tender_checklist_items");

            migrationBuilder.DropColumn(
                name: "DocumentKind",
                table: "tender_checklist_items");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "tender_checklist_items");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserId",
                table: "tender_checklist_items");

            migrationBuilder.DropColumn(
                name: "WorkTaskId",
                table: "tender_checklist_items");
        }
    }
}
