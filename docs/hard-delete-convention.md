# Hard Delete Convention

## Objective

Allow authorized users to permanently remove aggregate roots, including seeded
or demo records, without hiding dependent data or causing partial deletion.

## API Contract

Each supported aggregate exposes:

1. `GET /api/{resource}/{id}/deletion-impact`
2. `DELETE /api/{resource}/{id}` with `planToken`, `confirmation`, and a
  mandatory `rowVersion` when the root supports optimistic concurrency

The preview returns the root label and confirmation code, a deterministic plan
token, the total affected count, whether deletion is currently allowed, and
dependent groups classified as:

- `Delete`: aggregate-owned records removed with the root.
- `Unlink`: independent records or external resources preserved after their
  binding to the root is removed.
- `Block`: data that must be safely cleaned before the root can be deleted.

The server recomputes the impact within a serializable transaction. A changed
plan or stale row version returns `409 Conflict`; invalid confirmation or an
active blocker returns `400 Bad Request`. Rejected requests must leave the root
and all dependencies unchanged.

The deterministic plan includes every business-significant direct and nested
dependent identifier. Adding or removing a nested child after preview therefore
invalidates the submitted token.

## Execution Rules

- Controllers authorize and translate domain outcomes to HTTP responses.
- Services validate the plan, confirmation, concurrency, and blockers.
- Aggregate deletion services own dependency ordering and file staging.
- Managed files are cleaned through the project-document workflow before their
  parent project can be removed.
- A Design Project with managed files is blocked until every file has a valid
  Operational Project cleanup sidecar; standalone projects have no cleanup
  route and are therefore blocked while managed files remain.
- Google Drive folder bindings are unlinked while external folders are
  preserved; the preview must disclose this behavior.
- Independent CRM records such as opportunities, quotes, and contracts are
  unlinked rather than deleted with an Operational Project.
- Audit events are emitted only after a successful delete.
- Hard-deleted seeded roots write a durable tombstone. Seed reruns respect that
  tombstone and do not recreate records an administrator intentionally removed.

## Frontend Rules

Use the shared deletion-impact dialog to load the preview, show categorized
counts and examples, disable deletion while blockers exist, and require the
exact typed confirmation. Refresh the relevant list/detail state after success.
Do not implement aggregate deletion as parallel per-row API calls.

All display text and dependency labels are backend-seeded content translations
with Vietnamese, English, Chinese, and Japanese values.

## Verification

Use integration tests for HTTP authorization, model binding, preview accuracy,
stale plans, row-version conflicts, blockers, persistence, unlinking, and
unchanged state after rejection. Use browser tests only for dialog rendering,
typed confirmation, blocker visibility, and successful UI refresh.

## Current Rollout

The shared contract is currently implemented for Design Projects and
Operational Projects. Other root pages must be migrated separately before the
repository-wide hard-delete rollout can be considered complete.