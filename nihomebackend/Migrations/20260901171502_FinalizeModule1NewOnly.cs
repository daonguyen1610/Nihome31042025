using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeModule1NewOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

                IF COL_LENGTH('quotes', 'RateSource') IS NOT NULL
                BEGIN
                    UPDATE quotes
                    SET RateSource = 'Override',
                        RateOverrideReason = COALESCE(
                            NULLIF(LTRIM(RTRIM(RateOverrideReason)), ''),
                            N'Giá được chuyển đổi từ dữ liệu trước Module 1.'),
                        RateOverrideByUserId = COALESCE(RateOverrideByUserId, CreatedByUserId),
                        RateOverrideAt = COALESCE(RateOverrideAt, CreatedAt, SYSUTCDATETIME());

                    DECLARE @quoteDefault sysname;
                    SELECT @quoteDefault = defaultConstraint.name
                    FROM sys.default_constraints defaultConstraint
                    INNER JOIN sys.columns columnDefinition
                        ON columnDefinition.object_id = defaultConstraint.parent_object_id
                       AND columnDefinition.column_id = defaultConstraint.parent_column_id
                    WHERE defaultConstraint.parent_object_id = OBJECT_ID('quotes')
                      AND columnDefinition.name = 'RateSource';
                    IF @quoteDefault IS NOT NULL
                    BEGIN
                        DECLARE @dropQuoteDefault nvarchar(max) =
                            N'ALTER TABLE quotes DROP CONSTRAINT ' + QUOTENAME(@quoteDefault);
                        EXEC sp_executesql @dropQuoteDefault;
                    END;
                    ALTER TABLE quotes ADD CONSTRAINT DF_quotes_RateSource
                        DEFAULT N'Override' FOR RateSource;
                END;

                IF COL_LENGTH('quote_version_snapshots', 'RateSource') IS NOT NULL
                BEGIN
                    UPDATE quote_version_snapshots
                    SET RateSource = 'Override',
                        RateOverrideReason = COALESCE(
                            NULLIF(LTRIM(RTRIM(RateOverrideReason)), ''),
                            N'Giá được chuyển đổi từ dữ liệu trước Module 1.'),
                        RateOverrideByUserId = COALESCE(RateOverrideByUserId, CreatedByUserId),
                        RateOverrideAt = COALESCE(RateOverrideAt, CreatedAt, SYSUTCDATETIME());

                    DECLARE @snapshotDefault sysname;
                    SELECT @snapshotDefault = defaultConstraint.name
                    FROM sys.default_constraints defaultConstraint
                    INNER JOIN sys.columns columnDefinition
                        ON columnDefinition.object_id = defaultConstraint.parent_object_id
                       AND columnDefinition.column_id = defaultConstraint.parent_column_id
                    WHERE defaultConstraint.parent_object_id = OBJECT_ID('quote_version_snapshots')
                      AND columnDefinition.name = 'RateSource';
                    IF @snapshotDefault IS NOT NULL
                    BEGIN
                        DECLARE @dropSnapshotDefault nvarchar(max) =
                            N'ALTER TABLE quote_version_snapshots DROP CONSTRAINT ' + QUOTENAME(@snapshotDefault);
                        EXEC sp_executesql @dropSnapshotDefault;
                    END;
                    ALTER TABLE quote_version_snapshots ADD CONSTRAINT DF_quote_version_snapshots_RateSource
                        DEFAULT N'Override' FOR RateSource;
                END;

                IF EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE object_id = OBJECT_ID('surveys')
                      AND name = 'OperationalProjectId'
                      AND is_nullable = 1)
                BEGIN
                    UPDATE survey
                    SET OperationalProjectId = opportunity.OperationalProjectId
                    FROM surveys survey
                    INNER JOIN opportunities opportunity
                        ON opportunity.Id = survey.LinkedOpportunityId
                    WHERE survey.OperationalProjectId IS NULL
                      AND opportunity.OperationalProjectId IS NOT NULL;

                    INSERT INTO customers
                        (Type, Name, SourceCode, RelationshipStatus, OwnerUserId,
                         Note, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId)
                    SELECT 'Individual',
                           CONCAT(N'Khách hàng khảo sát legacy ', survey.Code),
                           'other', 'Prospect', NULL,
                           CONCAT(N'MIGRATION:CompleteModule1:Survey:', survey.Id),
                           survey.CreatedAt, survey.CreatedByUserId,
                           survey.UpdatedAt, survey.UpdatedByUserId
                    FROM surveys survey
                    WHERE survey.OperationalProjectId IS NULL
                      AND NOT EXISTS (
                          SELECT 1
                          FROM operational_projects project
                          WHERE project.Code = CONCAT('PJ-LEGACY-SV-', survey.Id)
                      )
                      AND NOT EXISTS (
                          SELECT 1
                          FROM customers customer
                          WHERE customer.Note = CONCAT(N'MIGRATION:CompleteModule1:Survey:', survey.Id)
                      );

                    INSERT INTO operational_projects
                        (Code, Name, CustomerId, ProjectManagerUserId, Status,
                         StartDate, EndDate, Note, CreatedAt, CreatedByUserId,
                         UpdatedAt, UpdatedByUserId)
                    SELECT CONCAT('PJ-LEGACY-SV-', survey.Id),
                           CONCAT(N'Dự án khảo sát ', survey.Code),
                           legacyCustomer.Id, NULL, 'Planning',
                           survey.SurveyDate, NULL,
                              CONCAT(N'MIGRATION:CompleteModule1:Survey:', survey.Id,
                                  N'; Backfill từ phiếu khảo sát legacy ', survey.Code, N'.'),
                           survey.CreatedAt, survey.CreatedByUserId,
                           survey.UpdatedAt, survey.UpdatedByUserId
                    FROM surveys survey
                    CROSS APPLY (
                        SELECT TOP (1) customer.Id
                        FROM customers customer
                        WHERE customer.Note = CONCAT(N'MIGRATION:CompleteModule1:Survey:', survey.Id)
                        ORDER BY customer.Id
                    ) legacyCustomer
                    WHERE survey.OperationalProjectId IS NULL
                      AND NOT EXISTS (
                          SELECT 1
                          FROM operational_projects project
                          WHERE project.Code = CONCAT('PJ-LEGACY-SV-', survey.Id)
                      );

                    UPDATE survey
                    SET OperationalProjectId = project.Id
                    FROM surveys survey
                    INNER JOIN operational_projects project
                        ON project.Code = CONCAT('PJ-LEGACY-SV-', survey.Id)
                       AND project.Note LIKE CONCAT(
                           N'MIGRATION:CompleteModule1:Survey:', survey.Id, N';%')
                    WHERE survey.OperationalProjectId IS NULL;

                    IF EXISTS (SELECT 1 FROM surveys WHERE OperationalProjectId IS NULL)
                        THROW 51000, 'Không thể chuyển đổi phiếu khảo sát: thiếu Dự án vận hành hợp lệ.', 1;

                    IF EXISTS (
                        SELECT 1 FROM sys.foreign_keys
                        WHERE parent_object_id = OBJECT_ID('surveys')
                          AND name = 'FK_surveys_operational_projects_OperationalProjectId')
                        ALTER TABLE surveys DROP CONSTRAINT
                            FK_surveys_operational_projects_OperationalProjectId;
                    IF EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE object_id = OBJECT_ID('surveys')
                          AND name = 'IX_surveys_OperationalProjectId')
                        DROP INDEX IX_surveys_OperationalProjectId ON surveys;
                    ALTER TABLE surveys ALTER COLUMN OperationalProjectId int NOT NULL;
                    CREATE INDEX IX_surveys_OperationalProjectId
                        ON surveys(OperationalProjectId);
                    ALTER TABLE surveys ADD CONSTRAINT
                        FK_surveys_operational_projects_OperationalProjectId
                        FOREIGN KEY (OperationalProjectId)
                        REFERENCES operational_projects(Id);
                END;

                IF COL_LENGTH('opportunities', 'StageSemanticsVersion') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE object_id = OBJECT_ID('opportunities')
                          AND name = 'IX_opportunities_StageSemanticsVersion')
                        DROP INDEX IX_opportunities_StageSemanticsVersion ON opportunities;

                    DECLARE @semanticsDefault sysname;
                    SELECT @semanticsDefault = defaultConstraint.name
                    FROM sys.default_constraints defaultConstraint
                    INNER JOIN sys.columns columnDefinition
                        ON columnDefinition.object_id = defaultConstraint.parent_object_id
                       AND columnDefinition.column_id = defaultConstraint.parent_column_id
                    WHERE defaultConstraint.parent_object_id = OBJECT_ID('opportunities')
                      AND columnDefinition.name = 'StageSemanticsVersion';
                    IF @semanticsDefault IS NOT NULL
                    BEGIN
                        DECLARE @dropSemanticsDefault nvarchar(max) =
                            N'ALTER TABLE opportunities DROP CONSTRAINT ' + QUOTENAME(@semanticsDefault);
                        EXEC sp_executesql @dropSemanticsDefault;
                    END;

                    ALTER TABLE opportunities DROP COLUMN
                        StageReconciledAt,
                        StageReconciledByUserId,
                        StageReconciledNote,
                        StageSemanticsVersion;
                END;

                                DELETE rolePermission
                                FROM role_permissions rolePermission
                                INNER JOIN permissions permission
                                        ON permission.Id = rolePermission.PermissionId
                                WHERE permission.Module = 'crm.opportunities'
                                    AND permission.Action = 'reconcile';

                                DELETE FROM permissions
                                WHERE Module = 'crm.opportunities'
                                    AND Action = 'reconcile';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
