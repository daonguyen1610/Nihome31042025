using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// Repairs environments that recorded AddDesignReviewWorkflow before the
/// immutable evidence-snapshot columns were introduced. Fresh databases have
/// the columns already, so every operation is guarded and idempotent.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261013043000_EnsureDesignReviewEvidenceSnapshot")]
public sealed class EnsureDesignReviewEvidenceSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('design_review_documents', 'SourceEntityTypeSnapshot') IS NULL
                ALTER TABLE design_review_documents ADD SourceEntityTypeSnapshot nvarchar(100) NULL;
            IF COL_LENGTH('design_review_documents', 'SourceRecordIdSnapshot') IS NULL
                ALTER TABLE design_review_documents ADD SourceRecordIdSnapshot bigint NULL;
            IF COL_LENGTH('design_review_documents', 'Sha256Snapshot') IS NULL
                ALTER TABLE design_review_documents ADD Sha256Snapshot nvarchar(64) NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE reviewDocument
            SET SourceEntityTypeSnapshot = document.SourceEntityType,
                SourceRecordIdSnapshot = document.SourceRecordId,
                Sha256Snapshot = document.Sha256
            FROM design_review_documents AS reviewDocument
            INNER JOIN project_documents AS document
                ON document.Id = reviewDocument.ProjectDocumentId
            WHERE reviewDocument.SourceEntityTypeSnapshot IS NULL
               OR reviewDocument.SourceRecordIdSnapshot IS NULL
               OR reviewDocument.Sha256Snapshot IS NULL;

            IF EXISTS
            (
                SELECT 1
                FROM design_review_documents
                WHERE SourceEntityTypeSnapshot IS NULL
                   OR SourceRecordIdSnapshot IS NULL
                   OR Sha256Snapshot IS NULL
            )
                THROW 51001, 'Cannot backfill design review evidence snapshots. Repair source document bindings before applying this migration.', 1;

            IF EXISTS
            (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID('design_review_documents')
                  AND name = 'SourceEntityTypeSnapshot'
                  AND is_nullable = 1
            )
                ALTER TABLE design_review_documents ALTER COLUMN SourceEntityTypeSnapshot nvarchar(100) NOT NULL;
            IF EXISTS
            (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID('design_review_documents')
                  AND name = 'SourceRecordIdSnapshot'
                  AND is_nullable = 1
            )
                ALTER TABLE design_review_documents ALTER COLUMN SourceRecordIdSnapshot bigint NOT NULL;
            IF EXISTS
            (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID('design_review_documents')
                  AND name = 'Sha256Snapshot'
                  AND is_nullable = 1
            )
                ALTER TABLE design_review_documents ALTER COLUMN Sha256Snapshot nvarchar(64) NOT NULL;

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID('design_review_documents')
                  AND name = 'IX_design_review_documents_SourceEntityTypeSnapshot_SourceRecordIdSnapshot'
            )
                CREATE INDEX IX_design_review_documents_SourceEntityTypeSnapshot_SourceRecordIdSnapshot
                    ON design_review_documents (SourceEntityTypeSnapshot, SourceRecordIdSnapshot);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This compatibility repair may run after an earlier migration already
        // created the columns. Keep rollback non-destructive; rolling back
        // AddDesignReviewWorkflow still removes the owning table and columns.
        migrationBuilder.Sql("SELECT 1;");
    }
}
