using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260907021500_ReconcileOperationalProjectBackfill")]
    public partial class ReconcileOperationalProjectBackfill : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @DesignProjectCount int = (SELECT COUNT(*) FROM design_projects);
                DECLARE @ContractCount int = (SELECT COUNT(*) FROM contracts);
                DECLARE @OpportunityCount int = (SELECT COUNT(*) FROM opportunities);
                DECLARE @QuoteCount int = (SELECT COUNT(*) FROM quotes);
                DECLARE @OperationalProjectCount int = (SELECT COUNT(*) FROM operational_projects);

                IF EXISTS (
                  SELECT 1
                  FROM design_projects designProject
                  LEFT JOIN contracts contract ON contract.Id = designProject.ContractId
                  WHERE designProject.ContractId IS NOT NULL AND contract.Id IS NULL
                  UNION ALL
                  SELECT 1
                  FROM quotes quote
                  LEFT JOIN opportunities opportunity ON opportunity.Id = quote.OpportunityId
                  WHERE opportunity.Id IS NULL
                  UNION ALL
                  SELECT 1
                  FROM contracts contract
                  LEFT JOIN opportunities opportunity ON opportunity.Id = contract.OpportunityId
                  WHERE contract.OpportunityId IS NOT NULL AND opportunity.Id IS NULL
                  UNION ALL
                  SELECT 1
                  FROM contracts contract
                  LEFT JOIN quotes quote ON quote.Id = contract.QuoteId
                  WHERE contract.QuoteId IS NOT NULL AND quote.Id IS NULL
                )
                  THROW 51029,
                    'Operational Project migration blocked: a source relationship references a missing record.',
                    1;

                IF EXISTS (
                    SELECT 1
                    FROM design_projects item
                    JOIN operational_projects project ON project.Id = item.OperationalProjectId
                    WHERE item.CustomerId <> project.CustomerId
                    UNION ALL
                    SELECT 1
                    FROM contracts item
                    JOIN operational_projects project ON project.Id = item.OperationalProjectId
                    WHERE item.CustomerId <> project.CustomerId
                    UNION ALL
                    SELECT 1
                    FROM opportunities item
                    JOIN operational_projects project ON project.Id = item.OperationalProjectId
                    WHERE item.CustomerId <> project.CustomerId
                    UNION ALL
                    SELECT 1
                    FROM quotes item
                    JOIN opportunities opportunity ON opportunity.Id = item.OpportunityId
                    JOIN operational_projects project ON project.Id = item.OperationalProjectId
                    WHERE opportunity.CustomerId <> project.CustomerId
                )
                    THROW 51020,
                        'Operational Project migration blocked: a linked record belongs to another customer.',
                        1;

                    IF EXISTS (
                      SELECT 1
                      FROM design_projects designProject
                      JOIN contracts contract ON contract.Id = designProject.ContractId
                      WHERE designProject.CustomerId <> contract.CustomerId
                      UNION ALL
                      SELECT 1
                      FROM contracts contract
                      JOIN opportunities opportunity ON opportunity.Id = contract.OpportunityId
                      WHERE contract.CustomerId <> opportunity.CustomerId
                      UNION ALL
                      SELECT 1
                      FROM contracts contract
                      JOIN quotes quote ON quote.Id = contract.QuoteId
                      JOIN opportunities opportunity ON opportunity.Id = quote.OpportunityId
                      WHERE contract.CustomerId <> opportunity.CustomerId
                    )
                      THROW 51028,
                        'Operational Project migration blocked: linked source records belong to different customers.',
                        1;

                IF EXISTS (
                    SELECT 1
                    FROM design_projects designProject
                    JOIN contracts contract ON contract.Id = designProject.ContractId
                    WHERE designProject.OperationalProjectId IS NOT NULL
                      AND contract.OperationalProjectId IS NOT NULL
                      AND designProject.OperationalProjectId <> contract.OperationalProjectId
                    UNION ALL
                    SELECT 1
                    FROM quotes quote
                    JOIN opportunities opportunity ON opportunity.Id = quote.OpportunityId
                    WHERE quote.OperationalProjectId IS NOT NULL
                      AND opportunity.OperationalProjectId IS NOT NULL
                      AND quote.OperationalProjectId <> opportunity.OperationalProjectId
                    UNION ALL
                    SELECT 1
                    FROM contracts contract
                    JOIN opportunities opportunity ON opportunity.Id = contract.OpportunityId
                    WHERE contract.OperationalProjectId IS NOT NULL
                      AND opportunity.OperationalProjectId IS NOT NULL
                      AND contract.OperationalProjectId <> opportunity.OperationalProjectId
                    UNION ALL
                    SELECT 1
                    FROM contracts contract
                    JOIN quotes quote ON quote.Id = contract.QuoteId
                    WHERE contract.OperationalProjectId IS NOT NULL
                      AND quote.OperationalProjectId IS NOT NULL
                      AND contract.OperationalProjectId <> quote.OperationalProjectId
                )
                    THROW 51021,
                        'Operational Project migration blocked: a business chain maps to multiple projects.',
                        1;

                IF EXISTS (
                    SELECT contract.OpportunityId
                    FROM contracts contract
                    WHERE contract.OpportunityId IS NOT NULL
                      AND contract.OperationalProjectId IS NOT NULL
                    GROUP BY contract.OpportunityId
                    HAVING COUNT(DISTINCT contract.OperationalProjectId) > 1
                )
                    THROW 51022,
                        'Operational Project migration blocked: contracts for one opportunity map to multiple projects.',
                        1;

                IF EXISTS (
                    SELECT 1
                    FROM design_projects source
                    JOIN operational_projects project
                      ON project.Code = CONCAT('PJ-MIG-DP-', source.Id)
                    WHERE source.OperationalProjectId IS NULL
                      AND project.CustomerId <> source.CustomerId
                    UNION ALL
                    SELECT 1
                    FROM opportunities source
                    JOIN operational_projects project
                      ON project.Code = CONCAT('PJ-MIG-OP-', source.Id)
                    WHERE source.OperationalProjectId IS NULL
                      AND project.CustomerId <> source.CustomerId
                    UNION ALL
                    SELECT 1
                    FROM contracts source
                    JOIN operational_projects project
                      ON project.Code = CONCAT('PJ-MIG-CT-', source.Id)
                    WHERE source.OperationalProjectId IS NULL
                      AND project.CustomerId <> source.CustomerId
                )
                    THROW 51023,
                        'Operational Project migration blocked: a deterministic migration code is already owned by another customer.',
                        1;

                UPDATE designProject
                SET OperationalProjectId = contract.OperationalProjectId
                FROM design_projects designProject
                JOIN contracts contract ON contract.Id = designProject.ContractId
                WHERE designProject.OperationalProjectId IS NULL
                  AND contract.OperationalProjectId IS NOT NULL;

                INSERT INTO operational_projects
                    (Code, Name, CustomerId, ProjectManagerUserId, Status,
                     StartDate, EndDate, Note, CreatedAt, CreatedByUserId,
                     UpdatedAt, UpdatedByUserId)
                SELECT CONCAT('PJ-MIG-DP-', source.Id), source.Name,
                       source.CustomerId, source.ProjectManagerUserId,
                       CASE source.Status
                           WHEN 'Active' THEN 'Active'
                           WHEN 'OnHold' THEN 'OnHold'
                           WHEN 'Completed' THEN 'Completed'
                           WHEN 'Cancelled' THEN 'Cancelled'
                           ELSE 'Planning'
                       END,
                       source.StartDate, source.Deadline,
                       CONCAT(N'MIGRATION:NIH-465:DesignProject:', source.Id),
                       source.CreatedAt, source.CreatedByUserId,
                       source.UpdatedAt, source.UpdatedByUserId
                FROM design_projects source
                WHERE source.OperationalProjectId IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM operational_projects project
                      WHERE project.Code = CONCAT('PJ-MIG-DP-', source.Id));

                UPDATE source
                SET OperationalProjectId = project.Id
                FROM design_projects source
                JOIN operational_projects project
                  ON project.Code = CONCAT('PJ-MIG-DP-', source.Id)
                WHERE source.OperationalProjectId IS NULL;

                UPDATE contract
                SET OperationalProjectId = designProject.OperationalProjectId
                FROM contracts contract
                JOIN design_projects designProject ON designProject.ContractId = contract.Id
                WHERE contract.OperationalProjectId IS NULL;

                IF EXISTS (
                    SELECT candidate.OpportunityId
                    FROM (
                        SELECT contract.OpportunityId, contract.OperationalProjectId
                        FROM contracts contract
                        WHERE contract.OpportunityId IS NOT NULL
                          AND contract.OperationalProjectId IS NOT NULL
                        UNION
                        SELECT quote.OpportunityId, quote.OperationalProjectId
                        FROM quotes quote
                        WHERE quote.OperationalProjectId IS NOT NULL
                    ) candidate
                    JOIN opportunities opportunity ON opportunity.Id = candidate.OpportunityId
                    WHERE opportunity.OperationalProjectId IS NULL
                    GROUP BY candidate.OpportunityId
                    HAVING COUNT(DISTINCT candidate.OperationalProjectId) > 1
                )
                    THROW 51024,
                        'Operational Project migration blocked: an unlinked opportunity has multiple project candidates.',
                        1;

                UPDATE opportunity
                SET OperationalProjectId = candidate.OperationalProjectId
                FROM opportunities opportunity
                -- Preflight 51024 guarantees zero or one distinct candidate.
                CROSS APPLY (
                    SELECT MIN(projectId) OperationalProjectId
                    FROM (
                        SELECT contract.OperationalProjectId projectId
                        FROM contracts contract
                        WHERE contract.OpportunityId = opportunity.Id
                          AND contract.OperationalProjectId IS NOT NULL
                        UNION
                        SELECT quote.OperationalProjectId
                        FROM quotes quote
                        WHERE quote.OpportunityId = opportunity.Id
                          AND quote.OperationalProjectId IS NOT NULL
                    ) projects
                ) candidate
                WHERE opportunity.OperationalProjectId IS NULL
                  AND candidate.OperationalProjectId IS NOT NULL;

                INSERT INTO operational_projects
                    (Code, Name, CustomerId, ProjectManagerUserId, Status,
                     StartDate, EndDate, Note, CreatedAt, CreatedByUserId,
                     UpdatedAt, UpdatedByUserId)
                SELECT CONCAT('PJ-MIG-OP-', source.Id), source.Name,
                       source.CustomerId, source.OwnerUserId,
                       CASE WHEN source.Stage = 'Won' THEN 'Active'
                            WHEN source.Stage = 'Lost' THEN 'Cancelled'
                            ELSE 'Planning' END,
                       NULL, source.ExpectedCloseDate,
                       CONCAT(N'MIGRATION:NIH-465:Opportunity:', source.Id),
                       source.CreatedAt, source.CreatedByUserId,
                       source.UpdatedAt, source.UpdatedByUserId
                FROM opportunities source
                WHERE source.OperationalProjectId IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM operational_projects project
                      WHERE project.Code = CONCAT('PJ-MIG-OP-', source.Id));

                UPDATE source
                SET OperationalProjectId = project.Id
                FROM opportunities source
                JOIN operational_projects project
                  ON project.Code = CONCAT('PJ-MIG-OP-', source.Id)
                WHERE source.OperationalProjectId IS NULL;

                UPDATE quote
                SET OperationalProjectId = opportunity.OperationalProjectId
                FROM quotes quote
                JOIN opportunities opportunity ON opportunity.Id = quote.OpportunityId
                WHERE quote.OperationalProjectId IS NULL;

                UPDATE contract
                SET OperationalProjectId = opportunity.OperationalProjectId
                FROM contracts contract
                JOIN opportunities opportunity ON opportunity.Id = contract.OpportunityId
                WHERE contract.OperationalProjectId IS NULL;

                UPDATE contract
                SET OperationalProjectId = quote.OperationalProjectId
                FROM contracts contract
                JOIN quotes quote ON quote.Id = contract.QuoteId
                WHERE contract.OperationalProjectId IS NULL;

                INSERT INTO operational_projects
                    (Code, Name, CustomerId, ProjectManagerUserId, Status,
                     StartDate, EndDate, Note, CreatedAt, CreatedByUserId,
                     UpdatedAt, UpdatedByUserId)
                SELECT CONCAT('PJ-MIG-CT-', source.Id),
                       CONCAT(N'Dự án hợp đồng ', source.ContractNumber),
                       source.CustomerId, source.OwnerUserId,
                       CASE source.Status
                           WHEN 'InProgress' THEN 'Active'
                           WHEN 'OnHold' THEN 'OnHold'
                           WHEN 'Completed' THEN 'Completed'
                           WHEN 'Cancelled' THEN 'Cancelled'
                           ELSE 'Planning'
                       END,
                       source.StartDate, source.EndDate,
                       CONCAT(N'MIGRATION:NIH-465:Contract:', source.Id),
                       source.CreatedAt, source.CreatedByUserId,
                       source.UpdatedAt, source.UpdatedByUserId
                FROM contracts source
                WHERE source.OperationalProjectId IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM operational_projects project
                      WHERE project.Code = CONCAT('PJ-MIG-CT-', source.Id));

                UPDATE source
                SET OperationalProjectId = project.Id
                FROM contracts source
                JOIN operational_projects project
                  ON project.Code = CONCAT('PJ-MIG-CT-', source.Id)
                WHERE source.OperationalProjectId IS NULL;

                IF (SELECT COUNT(*) FROM design_projects) <> @DesignProjectCount
                   OR (SELECT COUNT(*) FROM contracts) <> @ContractCount
                   OR (SELECT COUNT(*) FROM opportunities) <> @OpportunityCount
                   OR (SELECT COUNT(*) FROM quotes) <> @QuoteCount
                   OR (SELECT COUNT(*) FROM operational_projects) < @OperationalProjectCount
                    THROW 51025,
                        'Operational Project migration blocked: source row counts changed unexpectedly.',
                        1;

                IF EXISTS (
                    SELECT 1 FROM design_projects WHERE OperationalProjectId IS NULL
                    UNION ALL
                    SELECT 1 FROM contracts WHERE OperationalProjectId IS NULL
                    UNION ALL
                    SELECT 1 FROM opportunities WHERE OperationalProjectId IS NULL
                    UNION ALL
                    SELECT 1 FROM quotes WHERE OperationalProjectId IS NULL
                )
                    THROW 51026,
                        'Operational Project migration blocked: one or more historical records remain unmapped.',
                        1;

                IF EXISTS (
                    SELECT 1
                    FROM design_projects item
                    JOIN operational_projects project ON project.Id = item.OperationalProjectId
                    WHERE item.CustomerId <> project.CustomerId
                    UNION ALL
                    SELECT 1
                    FROM contracts item
                    JOIN operational_projects project ON project.Id = item.OperationalProjectId
                    WHERE item.CustomerId <> project.CustomerId
                    UNION ALL
                    SELECT 1
                    FROM opportunities item
                    JOIN operational_projects project ON project.Id = item.OperationalProjectId
                    WHERE item.CustomerId <> project.CustomerId
                    UNION ALL
                    SELECT 1
                    FROM quotes item
                    JOIN opportunities opportunity ON opportunity.Id = item.OpportunityId
                    JOIN operational_projects project ON project.Id = item.OperationalProjectId
                    WHERE opportunity.CustomerId <> project.CustomerId
                    UNION ALL
                    SELECT 1
                    FROM design_projects designProject
                    JOIN contracts contract ON contract.Id = designProject.ContractId
                    WHERE designProject.OperationalProjectId <> contract.OperationalProjectId
                    UNION ALL
                    SELECT 1
                    FROM contracts contract
                    JOIN opportunities opportunity ON opportunity.Id = contract.OpportunityId
                    WHERE contract.OperationalProjectId <> opportunity.OperationalProjectId
                    UNION ALL
                    SELECT 1
                    FROM contracts contract
                    JOIN quotes quote ON quote.Id = contract.QuoteId
                    WHERE contract.OperationalProjectId <> quote.OperationalProjectId
                    UNION ALL
                    SELECT 1
                    FROM quotes quote
                    JOIN opportunities opportunity ON opportunity.Id = quote.OpportunityId
                    WHERE quote.OperationalProjectId <> opportunity.OperationalProjectId
                )
                    THROW 51027,
                        'Operational Project migration blocked: post-migration integrity validation failed.',
                        1;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Historical relationships cannot be removed safely after later modules use them.
        }
    }
}
