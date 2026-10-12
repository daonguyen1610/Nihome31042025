using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class BackfillLegacyTenderSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "FinanceReviewWorkPackageId",
                table: "tender_submissions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "ApprovedEstimateRevisionId",
                table: "tender_submissions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // Only an actual successful Submitted transition is reliable enough
            // to establish a legacy submission time. Never infer it from UpdatedAt.
            // Terminal tenders were immutable after submission, so their current
            // dossier is the best available legacy snapshot; LegacyUnknown makes
            // the evidence limitation explicit.
            migrationBuilder.Sql(
                """
                ;WITH FirstSubmissionAudit AS
                (
                    SELECT TRY_CONVERT(int, a.ResourceId) AS TenderId,
                           a.AuditId,
                           a.CreatedAt,
                           a.ActorUserId,
                           ROW_NUMBER() OVER (
                               PARTITION BY TRY_CONVERT(int, a.ResourceId)
                               ORDER BY a.CreatedAt, a.Id) AS SequenceNumber
                    FROM audit_logs a
                    WHERE a.Action = 'tender.transition'
                      AND a.ResourceType = 'Tender'
                      AND a.Status = 'success'
                      AND ISJSON(a.NewValueJson) = 1
                      AND JSON_VALUE(a.NewValueJson, '$.Status') = 'Submitted'
                      AND TRY_CONVERT(int, a.ResourceId) IS NOT NULL
                )
                UPDATE t
                SET SubmittedAt = a.CreatedAt
                FROM tenders t
                INNER JOIN FirstSubmissionAudit a
                    ON a.TenderId = t.Id AND a.SequenceNumber = 1
                WHERE t.SubmittedAt IS NULL;

                ;WITH FirstSubmissionAudit AS
                (
                    SELECT TRY_CONVERT(int, a.ResourceId) AS TenderId,
                           a.AuditId,
                           a.CreatedAt,
                           a.ActorUserId,
                           ROW_NUMBER() OVER (
                               PARTITION BY TRY_CONVERT(int, a.ResourceId)
                               ORDER BY a.CreatedAt, a.Id) AS SequenceNumber
                    FROM audit_logs a
                    WHERE a.Action = 'tender.transition'
                      AND a.ResourceType = 'Tender'
                      AND a.Status = 'success'
                      AND ISJSON(a.NewValueJson) = 1
                      AND JSON_VALUE(a.NewValueJson, '$.Status') = 'Submitted'
                      AND TRY_CONVERT(int, a.ResourceId) IS NOT NULL
                )
                INSERT INTO tender_submissions
                    (TenderId, Sequence, SubmittedAt, SubmittedByUserId,
                     ApprovedEstimateRevisionId, FinanceReviewWorkPackageId,
                     SubmissionChannel, ReceiptReference, LegacyUnknown,
                     PlanningSnapshotJson, EstimateSnapshotJson, FinanceReviewSnapshotJson)
                SELECT t.Id, 1, a.CreatedAt,
                       COALESCE(a.ActorUserId, t.CreatedByUserId, t.UpdatedByUserId,
                           (SELECT MIN(u.Id) FROM users u)),
                       estimate.Id, finance.Id, 'LegacyUnknown', NULL, 1,
                       CONCAT('{"legacyAuditId":"', a.AuditId, '"}'), '{}', '{}'
                FROM tenders t
                INNER JOIN FirstSubmissionAudit a
                    ON a.TenderId = t.Id AND a.SequenceNumber = 1
                OUTER APPLY
                (
                    SELECT TOP (1) r.Id
                    FROM tender_estimate_revisions r
                    WHERE r.TenderId = t.Id AND r.Status = 'Approved'
                    ORDER BY r.Id DESC
                ) estimate
                OUTER APPLY
                (
                    SELECT TOP (1) p.Id
                    FROM department_work_packages p
                    INNER JOIN department_finance_reviews f ON f.WorkPackageId = p.Id
                    WHERE p.SourceTenderId = t.Id
                      AND p.Purpose = 'FinanceTenderReview'
                      AND f.Status = 'Approved'
                    ORDER BY p.Id DESC
                ) finance
                WHERE t.Status IN ('Submitted', 'Won', 'Lost')
                  AND NOT EXISTS (
                      SELECT 1 FROM tender_submissions existing
                      WHERE existing.TenderId = t.Id)
                  AND COALESCE(a.ActorUserId, t.CreatedByUserId, t.UpdatedByUserId,
                      (SELECT MIN(u.Id) FROM users u)) IS NOT NULL;

                INSERT INTO tender_submission_items
                    (TenderSubmissionId, ChecklistItemId, EvidenceId, TemplateCode,
                     Title, IsRequired, Status, FilePath, OriginalFileName,
                     CapabilityDocumentId, CapabilityDocumentVersion, Sha256,
                     NotApplicableReason)
                SELECT s.Id, i.Id, i.CurrentEvidenceId, i.TemplateCode,
                       i.Title, i.IsRequired, i.Status,
                       COALESCE(e.FilePath, i.FilePath),
                       COALESCE(e.OriginalFileName, i.OriginalFileName),
                       COALESCE(e.CapabilityDocumentId, i.CapabilityDocumentId),
                       e.CapabilityDocumentVersion, e.Sha256, i.NotApplicableReason
                FROM tender_submissions s
                INNER JOIN tender_checklist_items i ON i.TenderId = s.TenderId
                LEFT JOIN tender_checklist_evidence e ON e.Id = i.CurrentEvidenceId
                WHERE s.LegacyUnknown = 1
                  AND JSON_VALUE(s.PlanningSnapshotJson, '$.legacyAuditId') IS NOT NULL
                  AND (i.IsRequired = 1 OR i.Status IN ('Submitted', 'NotApplicable'))
                  AND NOT EXISTS (
                      SELECT 1 FROM tender_submission_items existing
                      WHERE existing.TenderSubmissionId = s.Id
                        AND existing.ChecklistItemId = i.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE t
                SET SubmittedAt = NULL
                FROM tenders t
                INNER JOIN tender_submissions s ON s.TenderId = t.Id
                WHERE s.LegacyUnknown = 1
                  AND JSON_VALUE(s.PlanningSnapshotJson, '$.legacyAuditId') IS NOT NULL
                  AND t.SubmittedAt = s.SubmittedAt;

                DELETE FROM tender_submissions
                WHERE LegacyUnknown = 1
                  AND JSON_VALUE(PlanningSnapshotJson, '$.legacyAuditId') IS NOT NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "FinanceReviewWorkPackageId",
                table: "tender_submissions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ApprovedEstimateRevisionId",
                table: "tender_submissions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
