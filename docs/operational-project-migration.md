# Operational Project Historical Migration

## Scope

NIH-465 reconciles historical `DesignProject`, `Contract`, `Opportunity`, and
`Quote` rows into the internal `OperationalProject` aggregate. Public website
`Project` content is explicitly outside this migration.

The migration preserves source rows and history. It updates only missing
`OperationalProjectId` values and creates an Operational Project only when no
existing project can be inherited from the same business chain. It does not
merge or delete existing Operational Projects.

## Mapping Rules

The migration uses deterministic source precedence:

1. A Design Project inherits its Contract's existing Operational Project.
2. A missing Design Project project is created as `PJ-MIG-DP-{id}`; its Contract
   then inherits that project.
3. An Opportunity inherits the single project already used by its Contracts or
   Quotes.
4. A missing Opportunity project is created as `PJ-MIG-OP-{id}`; its Quotes and
   Contracts then inherit that project.
5. A direct Contract with no project source is created as `PJ-MIG-CT-{id}`.

Every inherited project must belong to the same Customer. Multiple project
candidates, cross-customer mappings, broken foreign keys, chain mismatches, and
deterministic code collisions block the migration instead of being guessed.
The stable codes and `NOT EXISTS` guards make the data statements rerunnable.

## Pre-Deployment Rehearsal

Back up the target database and restore it under a temporary database name.
Generate the migration script, review it, and apply it to that copy before the
production maintenance window. This rehearses the exact migration instead of
maintaining a separate SQL implementation that can drift from it.

The migration itself is the validation boundary. It aborts and rolls back when
it finds broken source references, cross-customer relationships, multiple
project candidates, deterministic-code collisions, changed source counts,
unmapped rows, or post-migration chain inconsistencies. Save the migration output
and before/after source counts as deployment evidence. Resolve any reported
`THROW 51020` through `THROW 51029` condition in the source data; do not disable
the checks or edit the migration to skip affected rows.

After a successful rehearsal, verify that all historical rows are mapped:

```sql
SELECT 'DesignProject' EntityType, COUNT(*) TotalRows,
   COUNT(OperationalProjectId) MappedRows FROM design_projects
UNION ALL
SELECT 'Contract', COUNT(*), COUNT(OperationalProjectId) FROM contracts
UNION ALL
SELECT 'Opportunity', COUNT(*), COUNT(OperationalProjectId) FROM opportunities
UNION ALL
SELECT 'Quote', COUNT(*), COUNT(OperationalProjectId) FROM quotes;
```

For each row, `TotalRows` and `MappedRows` must match. Run production deployment
with application writes stopped, and take a second backup immediately after the
post-migration check passes. If writes resume, prefer a reviewed forward
correction; restoring the earlier backup would discard later transactions.

## Deployment

Generate and review the idempotent SQL script before applying it:

```bash
docker exec nihome31042025-backend dotnet ef migrations script \
  20260906150351_AddFinanceControlWorkflows \
  20260907021500_ReconcileOperationalProjectBackfill --idempotent
docker exec nihome31042025-backend dotnet ef database update
```

EF Core runs the migration in a transaction. The migration records source row
counts, performs the backfill, and aborts if source counts change, any historical
row remains unmapped, or post-migration customer and chain integrity fails.

Run the source-count query again after deployment. All four source groups must
have matching total and mapped counts.

## Rollback and Compatibility

The migration has no destructive schema operation, but its data links cannot be
removed safely after downstream modules begin using them. If deployment fails,
the migration transaction rolls back automatically. If a rollback is required
after a successful deployment, stop writes and restore the pre-deployment
database backup; do not null project IDs or delete generated projects manually.

Existing API routes and DTO fields are unchanged. Nullable project fields remain
wire-compatible for current clients; this historical migration does not impose
new runtime `NOT NULL` constraints. Runtime enforcement for every new Contract
to belong to exactly one Operational Project remains owned by NIH-466.
