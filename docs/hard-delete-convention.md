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

When execution includes managed local files or Nicon-owned Google Drive items,
the delete endpoint creates a durable hard-delete operation. The operation and
its items are independent records identified by a GUID; they do not hold foreign
keys to the aggregate root. Only one unresolved operation may exist for a
resource type and resource ID.

The endpoint returns:

- `204 No Content` only after external cleanup, the registered database
  finalizer, and quarantine purge have all completed.
- `202 Accepted` with the operation ID and current status when durable work is
  still pending, retrying, or requires manual action.

Clients may safely poll the operation result. A durable operation is not proof
that the root has been deleted until its status is `Completed`.

## Execution Rules

- Controllers authorize and translate domain outcomes to HTTP responses.
- Services validate the plan, confirmation, concurrency, and blockers.
- Aggregate deletion services own dependency ordering and file staging.
- Managed files are cleaned through the project-document workflow before their
  parent project can be removed.
- Local managed files must use host-relative paths under an explicit private
  storage root. Execution moves them atomically to a same-volume hard-delete
  quarantine before any irreversible step. Missing files are successful no-ops.
- A Design Project with managed files is blocked until every file has a valid
  Operational Project cleanup sidecar; standalone projects have no cleanup
  route and are therefore blocked while managed files remain.
- Existing domain flows continue to unlink external Google Drive folder
  bindings until they are migrated to the durable operation foundation.
- A migrated plan may permanently delete a Drive file or folder only when its
  metadata proves current Nicon `InstanceId` ownership, every caller-supplied
  expected app property matches, the expected parent matches when supplied,
  and Drive reports that the connected account owns and can delete the item.
  Imported, shared, mismatched, or unknown-origin items are blockers and must
  never be permanently deleted. A missing Drive item is an idempotent success.
- Independent CRM records such as opportunities, quotes, and contracts are
  unlinked rather than deleted with an Operational Project.
- Tender checklist uploads under `/files/tenders` are aggregate-owned and are
  quarantined and purged with the Tender. Checklist references to Capability
  Documents are unlinked while the shared document and file survive. Any
  non-library checklist file outside `/files/tenders` blocks deletion rather
  than being silently orphaned.
- Quote documents under `/files/quotes` are aggregate-owned and are quarantined
  and purged with the Quote. Opportunities and Contracts that reference the
  Quote are unlinked and preserved. A Quote project-document sidecar is eligible
  only when it is an exact CRM `QuoteDocument`/`file` binding to the normalized
  Quote path, has stable Nicon ownership with no conflict or active processing
  lease, and is either fully synced with complete Drive ownership metadata or
  already terminally deleted without a Drive file ID. The durable operation
  permanently deletes eligible Drive replicas using verified app properties,
  then preserves and terminalizes their sidecar records. Imported, shared,
  ambiguous, incomplete, mismatched, or unstable sidecars block deletion.
- Customer contacts, activities, documents, translations, and files under the
  exact `/files/customers/{customerId}/` root are aggregate-owned. Converted
  Lead and Project Document metadata links are cleared while those records are
  preserved. Opportunities, Tenders, Contracts, Design Projects, and
  Operational Projects are independent required roots and block Customer
  deletion until handled through their own authorized workflows. Undoing a
  Lead conversion always preserves its Customer; Customers can only be
  permanently removed through this preview-and-confirm contract.
- Audit events are emitted only after a successful delete.
- Hard-deleted seeded roots write a durable tombstone. Seed reruns respect that
  tombstone and do not recreate records an administrator intentionally removed.

## Durable Operation Lifecycle

Operations move through `Preparing`, `Ready`, `Processing`, `Completed`,
`Failed`, or `ManualActionRequired`:

1. A domain service validates authorization, confirmation, concurrency,
  blockers, and the current deterministic plan before creating the operation.
2. The processor quarantines local files, then permanently deletes only verified
  owned Drive items.
3. After external cleanup, a resource handler registered by resource type runs
  the idempotent database finalizer. Delegate-only finalizers are not allowed
  because they cannot survive application restart.
4. The processor purges quarantined local files and marks the operation
  `Completed`.

Failures before the first Drive deletion restore quarantined files and may be
retried with conservative backoff. Once a Drive deletion or database finalizer
begins, rollback is no longer safe: the operation remains in forward recovery
until remaining idempotent steps complete. Ownership mismatches and exhausted
retries move to `ManualActionRequired`; operators must resolve the blocker and
explicitly retry. Resource handlers must use the operation ID as an idempotency
key because a restart can occur after the database commit but before the item is
marked complete.

## Frontend Rules

Use the shared deletion-impact dialog to load the preview, show categorized
counts and examples, disable deletion while blockers exist, and require the
exact typed confirmation. Refresh the relevant list/detail state after success.
Do not implement aggregate deletion as parallel per-row API calls.

Business-data blockers should include an internal resolution URL when a filtered
ADMIN list exists and internal detail links for the displayed blocker examples.
The shared dialog opens those destinations in a new tab so the user can resolve
dependencies without losing the current deletion preview. The filtered list link
is shown only when additional blockers are not represented by the detail links.
The link is a navigation convenience only; access, record scope, and available
actions on the destination page follow the existing RBAC matrix for that module.

All display text and dependency labels are backend-seeded content translations
with Vietnamese, English, Chinese, and Japanese values.

## Verification

Use integration tests for HTTP authorization, model binding, preview accuracy,
stale plans, row-version conflicts, blockers, persistence, unlinking, and
unchanged state after rejection. Use browser tests only for dialog rendering,
typed confirmation, blocker visibility, and successful UI refresh.

## Current Rollout

The durable operation, local quarantine, verified Drive deletion, registry, and
retry foundation is available for domain adoption. Design Project, Operational
Project, Lead, Customer, and Tender use the durable backend flow, owner-scoped operation
status/retry API, and the shared frontend polling dialog. Quote uses the same
durable flow for direct managed files and verified Nicon-owned Drive replicas,
while preserving terminalized project-document sidecars. Lead, Customer, Tender, and Quote
bulk deletion is disabled until a server-side
batch preview-and-confirm contract is available. Other root pages must still be
migrated separately before the repository-wide hard-delete rollout is complete.