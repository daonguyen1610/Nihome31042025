SET NOCOUNT ON;
SET XACT_ABORT ON;

CREATE TABLE #MigrationExceptions
(
    ExceptionCode nvarchar(60) NOT NULL,
    EntityType nvarchar(40) NOT NULL,
    EntityId bigint NULL,
    RelatedEntityType nvarchar(40) NULL,
    RelatedEntityId bigint NULL,
    CurrentProjectId int NULL,
    CandidateProjectId int NULL,
    Detail nvarchar(400) NOT NULL
);

INSERT INTO #MigrationExceptions
  SELECT 'SOURCE_ORPHAN', 'DesignProject', designProject.Id, 'Contract', designProject.ContractId,
       designProject.OperationalProjectId, NULL, 'ContractId does not exist.'
  FROM design_projects designProject
  LEFT JOIN contracts contract ON contract.Id = designProject.ContractId
  WHERE designProject.ContractId IS NOT NULL AND contract.Id IS NULL
  UNION ALL
  SELECT 'SOURCE_ORPHAN', 'Quote', quote.Id, 'Opportunity', quote.OpportunityId,
       quote.OperationalProjectId, NULL, 'OpportunityId does not exist.'
  FROM quotes quote
  LEFT JOIN opportunities opportunity ON opportunity.Id = quote.OpportunityId
  WHERE opportunity.Id IS NULL
  UNION ALL
  SELECT 'SOURCE_ORPHAN', 'Contract', contract.Id, 'Opportunity', contract.OpportunityId,
       contract.OperationalProjectId, NULL, 'OpportunityId does not exist.'
  FROM contracts contract
  LEFT JOIN opportunities opportunity ON opportunity.Id = contract.OpportunityId
  WHERE contract.OpportunityId IS NOT NULL AND opportunity.Id IS NULL
  UNION ALL
  SELECT 'SOURCE_ORPHAN', 'Contract', contract.Id, 'Quote', contract.QuoteId,
       contract.OperationalProjectId, NULL, 'QuoteId does not exist.'
  FROM contracts contract
  LEFT JOIN quotes quote ON quote.Id = contract.QuoteId
  WHERE contract.QuoteId IS NOT NULL AND quote.Id IS NULL;

  INSERT INTO #MigrationExceptions
SELECT 'ORPHAN_PROJECT', 'DesignProject', source.Id, NULL, NULL,
       source.OperationalProjectId, NULL, 'OperationalProjectId does not exist.'
FROM design_projects source
LEFT JOIN operational_projects project ON project.Id = source.OperationalProjectId
WHERE source.OperationalProjectId IS NOT NULL AND project.Id IS NULL
UNION ALL
SELECT 'ORPHAN_PROJECT', 'Contract', source.Id, NULL, NULL,
       source.OperationalProjectId, NULL, 'OperationalProjectId does not exist.'
FROM contracts source
LEFT JOIN operational_projects project ON project.Id = source.OperationalProjectId
WHERE source.OperationalProjectId IS NOT NULL AND project.Id IS NULL
UNION ALL
SELECT 'ORPHAN_PROJECT', 'Opportunity', source.Id, NULL, NULL,
       source.OperationalProjectId, NULL, 'OperationalProjectId does not exist.'
FROM opportunities source
LEFT JOIN operational_projects project ON project.Id = source.OperationalProjectId
WHERE source.OperationalProjectId IS NOT NULL AND project.Id IS NULL
UNION ALL
SELECT 'ORPHAN_PROJECT', 'Quote', source.Id, NULL, NULL,
       source.OperationalProjectId, NULL, 'OperationalProjectId does not exist.'
FROM quotes source
LEFT JOIN operational_projects project ON project.Id = source.OperationalProjectId
WHERE source.OperationalProjectId IS NOT NULL AND project.Id IS NULL;

INSERT INTO #MigrationExceptions
SELECT 'CUSTOMER_MISMATCH', 'DesignProject', source.Id, 'OperationalProject', project.Id,
       source.OperationalProjectId, NULL, 'Design Project and Operational Project have different customers.'
FROM design_projects source
JOIN operational_projects project ON project.Id = source.OperationalProjectId
WHERE source.CustomerId <> project.CustomerId
UNION ALL
SELECT 'CUSTOMER_MISMATCH', 'Contract', source.Id, 'OperationalProject', project.Id,
       source.OperationalProjectId, NULL, 'Contract and Operational Project have different customers.'
FROM contracts source
JOIN operational_projects project ON project.Id = source.OperationalProjectId
WHERE source.CustomerId <> project.CustomerId
UNION ALL
SELECT 'CUSTOMER_MISMATCH', 'Opportunity', source.Id, 'OperationalProject', project.Id,
       source.OperationalProjectId, NULL, 'Opportunity and Operational Project have different customers.'
FROM opportunities source
JOIN operational_projects project ON project.Id = source.OperationalProjectId
WHERE source.CustomerId <> project.CustomerId
UNION ALL
SELECT 'CUSTOMER_MISMATCH', 'Quote', quote.Id, 'OperationalProject', project.Id,
       quote.OperationalProjectId, NULL, 'Quote source customer and Operational Project have different customers.'
FROM quotes quote
JOIN opportunities opportunity ON opportunity.Id = quote.OpportunityId
JOIN operational_projects project ON project.Id = quote.OperationalProjectId
WHERE opportunity.CustomerId <> project.CustomerId;

INSERT INTO #MigrationExceptions
SELECT 'SOURCE_CUSTOMER_MISMATCH', 'DesignProject', designProject.Id, 'Contract', contract.Id,
       designProject.OperationalProjectId, contract.OperationalProjectId,
       'Design Project and Contract belong to different customers.'
FROM design_projects designProject
JOIN contracts contract ON contract.Id = designProject.ContractId
WHERE designProject.CustomerId <> contract.CustomerId
UNION ALL
SELECT 'SOURCE_CUSTOMER_MISMATCH', 'Contract', contract.Id, 'Opportunity', opportunity.Id,
       contract.OperationalProjectId, opportunity.OperationalProjectId,
       'Contract and Opportunity belong to different customers.'
FROM contracts contract
JOIN opportunities opportunity ON opportunity.Id = contract.OpportunityId
WHERE contract.CustomerId <> opportunity.CustomerId
UNION ALL
SELECT 'SOURCE_CUSTOMER_MISMATCH', 'Contract', contract.Id, 'Quote', quote.Id,
       contract.OperationalProjectId, quote.OperationalProjectId,
       'Contract and Quote source Opportunity belong to different customers.'
FROM contracts contract
JOIN quotes quote ON quote.Id = contract.QuoteId
JOIN opportunities opportunity ON opportunity.Id = quote.OpportunityId
WHERE contract.CustomerId <> opportunity.CustomerId;

INSERT INTO #MigrationExceptions
SELECT 'CHAIN_PROJECT_MISMATCH', 'DesignProject', designProject.Id, 'Contract', contract.Id,
       designProject.OperationalProjectId, contract.OperationalProjectId,
       'Design Project and Contract map to different Operational Projects.'
FROM design_projects designProject
JOIN contracts contract ON contract.Id = designProject.ContractId
WHERE designProject.OperationalProjectId IS NOT NULL
  AND contract.OperationalProjectId IS NOT NULL
  AND designProject.OperationalProjectId <> contract.OperationalProjectId
UNION ALL
SELECT 'CHAIN_PROJECT_MISMATCH', 'Quote', quote.Id, 'Opportunity', opportunity.Id,
       quote.OperationalProjectId, opportunity.OperationalProjectId,
       'Quote and Opportunity map to different Operational Projects.'
FROM quotes quote
JOIN opportunities opportunity ON opportunity.Id = quote.OpportunityId
WHERE quote.OperationalProjectId IS NOT NULL
  AND opportunity.OperationalProjectId IS NOT NULL
  AND quote.OperationalProjectId <> opportunity.OperationalProjectId
UNION ALL
SELECT 'CHAIN_PROJECT_MISMATCH', 'Contract', contract.Id, 'Opportunity', opportunity.Id,
       contract.OperationalProjectId, opportunity.OperationalProjectId,
       'Contract and Opportunity map to different Operational Projects.'
FROM contracts contract
JOIN opportunities opportunity ON opportunity.Id = contract.OpportunityId
WHERE contract.OperationalProjectId IS NOT NULL
  AND opportunity.OperationalProjectId IS NOT NULL
  AND contract.OperationalProjectId <> opportunity.OperationalProjectId
UNION ALL
SELECT 'CHAIN_PROJECT_MISMATCH', 'Contract', contract.Id, 'Quote', quote.Id,
       contract.OperationalProjectId, quote.OperationalProjectId,
       'Contract and Quote map to different Operational Projects.'
FROM contracts contract
JOIN quotes quote ON quote.Id = contract.QuoteId
WHERE contract.OperationalProjectId IS NOT NULL
  AND quote.OperationalProjectId IS NOT NULL
  AND contract.OperationalProjectId <> quote.OperationalProjectId;

WITH candidates AS
(
    SELECT contract.OpportunityId, contract.OperationalProjectId
    FROM contracts contract
    WHERE contract.OpportunityId IS NOT NULL
      AND contract.OperationalProjectId IS NOT NULL
    UNION
    SELECT quote.OpportunityId, quote.OperationalProjectId
    FROM quotes quote
    WHERE quote.OperationalProjectId IS NOT NULL
)
INSERT INTO #MigrationExceptions
SELECT 'MULTIPLE_PROJECT_CANDIDATES', 'Opportunity', candidate.OpportunityId,
       NULL, NULL, NULL, NULL,
       'Linked Contracts or Quotes map this Opportunity to multiple Operational Projects.'
FROM candidates candidate
JOIN opportunities opportunity ON opportunity.Id = candidate.OpportunityId
WHERE opportunity.OperationalProjectId IS NULL
GROUP BY candidate.OpportunityId
HAVING COUNT(DISTINCT candidate.OperationalProjectId) > 1;

INSERT INTO #MigrationExceptions
SELECT 'MIGRATION_CODE_COLLISION', 'DesignProject', source.Id, 'OperationalProject', project.Id,
       NULL, project.Id, 'PJ-MIG-DP code is owned by a different customer.'
FROM design_projects source
JOIN operational_projects project ON project.Code = CONCAT('PJ-MIG-DP-', source.Id)
WHERE source.OperationalProjectId IS NULL AND project.CustomerId <> source.CustomerId
UNION ALL
SELECT 'MIGRATION_CODE_COLLISION', 'Opportunity', source.Id, 'OperationalProject', project.Id,
       NULL, project.Id, 'PJ-MIG-OP code is owned by a different customer.'
FROM opportunities source
JOIN operational_projects project ON project.Code = CONCAT('PJ-MIG-OP-', source.Id)
WHERE source.OperationalProjectId IS NULL AND project.CustomerId <> source.CustomerId
UNION ALL
SELECT 'MIGRATION_CODE_COLLISION', 'Contract', source.Id, 'OperationalProject', project.Id,
       NULL, project.Id, 'PJ-MIG-CT code is owned by a different customer.'
FROM contracts source
JOIN operational_projects project ON project.Code = CONCAT('PJ-MIG-CT-', source.Id)
WHERE source.OperationalProjectId IS NULL AND project.CustomerId <> source.CustomerId;

SELECT EntityType, TotalRows, MappedRows, TotalRows - MappedRows UnmappedRows
FROM
(
    SELECT 'DesignProject' EntityType, COUNT(*) TotalRows,
           COUNT(OperationalProjectId) MappedRows FROM design_projects
    UNION ALL
    SELECT 'Contract', COUNT(*), COUNT(OperationalProjectId) FROM contracts
    UNION ALL
    SELECT 'Opportunity', COUNT(*), COUNT(OperationalProjectId) FROM opportunities
    UNION ALL
    SELECT 'Quote', COUNT(*), COUNT(OperationalProjectId) FROM quotes
) summary
ORDER BY EntityType;

SELECT PlannedOperation, COUNT(*) PlannedRows
FROM
(
    SELECT 'CREATE_FROM_DESIGN_PROJECT' PlannedOperation, source.Id
    FROM design_projects source
    LEFT JOIN contracts contract ON contract.Id = source.ContractId
    WHERE source.OperationalProjectId IS NULL
      AND contract.OperationalProjectId IS NULL
    UNION ALL
    SELECT 'CREATE_FROM_OPPORTUNITY', source.Id
    FROM opportunities source
    WHERE source.OperationalProjectId IS NULL
      AND NOT EXISTS (
          SELECT 1 FROM contracts contract
          WHERE contract.OpportunityId = source.Id
            AND contract.OperationalProjectId IS NOT NULL)
      AND NOT EXISTS (
          SELECT 1 FROM quotes quote
          WHERE quote.OpportunityId = source.Id
            AND quote.OperationalProjectId IS NOT NULL)
    UNION ALL
    SELECT 'CREATE_FROM_DIRECT_CONTRACT', source.Id
    FROM contracts source
    WHERE source.OperationalProjectId IS NULL
      AND source.OpportunityId IS NULL
      AND source.QuoteId IS NULL
      AND NOT EXISTS (
          SELECT 1 FROM design_projects designProject
          WHERE designProject.ContractId = source.Id
            AND designProject.OperationalProjectId IS NOT NULL)
) planned
GROUP BY PlannedOperation
ORDER BY PlannedOperation;

SELECT ExceptionCode, EntityType, EntityId, RelatedEntityType, RelatedEntityId,
       CurrentProjectId, CandidateProjectId, Detail
FROM #MigrationExceptions
ORDER BY ExceptionCode, EntityType, EntityId;

SELECT ExceptionCode, COUNT(*) ExceptionRows
FROM #MigrationExceptions
GROUP BY ExceptionCode
ORDER BY ExceptionCode;

DECLARE @ExceptionCount int = (SELECT COUNT(*) FROM #MigrationExceptions);
SELECT @ExceptionCount ExceptionCount,
       CASE WHEN @ExceptionCount = 0 THEN 'READY' ELSE 'BLOCKED' END MigrationReadiness;

IF @ExceptionCount > 0
    THROW 51019,
        'Operational Project migration dry-run found blocking exceptions. Resolve the reported rows before deployment.',
        1;
