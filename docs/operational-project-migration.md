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

## Pre-Deployment Dry Run

Back up the target database, then run the read-only report from the repository
root. Replace the container name only when `docker compose ps` reports a
different SQL Server container.

```bash
docker compose ps
read -s SQLCMDPASSWORD
export SQLCMDPASSWORD
docker exec -i -e SQLCMDPASSWORD="$SQLCMDPASSWORD" \
   nihome31042025-sqlserver \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -d NihomeDB -b \
  -i /dev/stdin < tools/sql/operational-project-migration-report.sql
unset SQLCMDPASSWORD
```

Type the SQL Server password at the silent prompt. Do not place production
credentials in shell history or repository files.

The final result must be `ExceptionCount = 0` and
`MigrationReadiness = READY`. Save the summary, planned-operation counts, and
empty exception result as deployment evidence. Any `BLOCKED` result requires a
reviewed data correction; do not disable constraints or edit the migration to
skip the affected rows.

Run the deployment in a maintenance window with application writes stopped.
Take a second backup immediately after the post-migration report passes. If
writes resume, prefer a reviewed forward correction; restoring the earlier
backup would discard every later business transaction.

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

Run the dry-run report again after deployment. All four source groups must have
zero unmapped rows and the report must remain `READY`.

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
