# RFQ and supplier comparison — NIH-166

## Business contract

Sources: NIH-166 and NIH-174/175/176, including their September 7 acceptance
comments; `Nicon-QLVH.md` Modules 5–6; `Nicon-workflow.md`;
`Nicon_BreakTask_v1.xlsx` (RFQ list, create/edit, and comparison detail).

The September 8 implementation request proceeds with the proposed MVP:

- Each RFQ belongs to one Operational Project and retains that project's
  Customer. A project can have many RFQs, vendors, and contracts.
- Procurement selects lines and quantities from an approved VND Project BOQ.
  Each quantity must fit its source line. RFQs do not reserve stock or consume
  Material Request quantities. MR linkage is deferred.
- Each RFQ invites one or more active suppliers/subcontractors. The vendor
  directory is shared master data; invitations and quotations belong to the RFQ.
- Lowest valid prices are highlighted, including ties. There is no weighted
  score or automatic selection. One complete quotation wins the entire RFQ;
  split awards are deferred.
- Procurement records quotes on behalf of vendors. Vendor portal and outgoing
  vendor email delivery are deferred. Bid form drafts are local until submitted.
- An explicit award creates one **Draft Downstream Supply/Subcontract contract**
  and its BOQ lines. The existing contract workflow handles signing, execution,
  and payment; an RFQ award does not sign the contract.
- VND is the supported currency because the existing contract value has no
  currency field. Unit prices and line totals use four decimal places; quantity
  uses six. Line amounts round halfway away from zero. Comparison totals sum
  those rounded amounts. Contract header value rounds the winning total to two
  decimals to match the existing contract schema; contract line prices retain
  four decimals. There is no tax, freight, discount, or currency-conversion
  calculation in this MVP; commercial qualifications belong in quote notes.

The application route is `/admin/procurement-control/rfqs`, with `projectId`
and optional `rfqId` query parameters. Customer → Project → RFQ → Contract
context remains visible. List export is CSV; detail export is a JSON evidence
bundle containing matrix lines, all quote revisions, commercial terms, files,
history, and the award comparison snapshot.

## Actors and access

| Capability | Default role grants |
|---|---|
| `proc.rfqs.view` | Procurement, PM, BGD, ADMIN, SUPER_ADMIN |
| `proc.rfqs.manage` | Procurement, ADMIN, SUPER_ADMIN |
| `proc.rfqs.export` | Procurement, BGD, ADMIN, SUPER_ADMIN |
| `proc.rfqs.award` | BGD, ADMIN, SUPER_ADMIN |

Every endpoint additionally checks existing project access rules. Missing or
out-of-scope resources return 404; unauthenticated and functionally unauthorized
requests return 401 and 403 respectively. Cached idempotency responses recheck
project scope, so removing membership also revokes replay access. The UI hides
unavailable actions; the server independently enforces them. New role grants
are added by migration without resetting unrelated role customizations.

The RFQ owner must be an active Procurement user and current project member.
Creation, issue, and award validate that relationship. Completed/cancelled
projects reject writes. Award rechecks vendor activity, bid revision/expiry,
project state, owner, and current approved BOQ inside its transaction. A newer
approved BOQ requires cancelling the old RFQ and preparing a new one.

## Lifecycle and concurrency

| State | Permitted actions |
|---|---|
| Draft | Edit scope, attach package files, issue, cancel with reason |
| Issued | Submit quote revisions until deadline, withdraw current quote with reason, start evaluation, cancel with reason |
| UnderEvaluation | Award an eligible quotation, cancel with reason |
| Awarded | View evidence and contract; close RFQ |
| Closed / Cancelled | Read/export evidence |

Issue freezes BOQ line descriptions, quantities, units, and invitations.
Starting evaluation explicitly stops further quote submissions, including when
done before the deadline. A quote revision never overwrites an earlier one.
The latest revision is current even if withdrawn; withdrawing it does not revive
an older revision. Missing price cells remain missing, while zero means a quoted
free item. Partial, expired, withdrawn, inactive-vendor, and superseded quotes
cannot be awarded. Quote validity must cover the RFQ deadline.

Mutations require `Idempotency-Key`; updates, transitions, bids, award, and file
attachment also require `rowVersion` or matching `If-Match`. Reusing a key with
another payload or submitting stale state returns 409. Root changes, quotation
history, award snapshot, and generated contract/lines commit transactionally.
Rejected business operations leave them unchanged. Audit records include actor,
project, RFQ, action, and resulting status. RFQ activity history is persisted
alongside the operation.

## Files and notifications

Files use the existing ProjectDocument upload validation and Google Drive
lifecycle under category `Procurement`. Uploads first become project documents;
selecting a file attaches it to a draft RFQ package or a submitted quote revision.
An abandoned form does not delete an uploaded project document: it remains
available in the project's document library. Each linked file records RFQ source
identity and package or vendor/revision slot. Cross-project, already-bound, and
pending-deletion files cannot be attached. Downloads recheck RFQ/project scope.
Linked evidence cannot be deleted through the generic document endpoint.

Google Drive must be configured to upload; failures use the existing document
service's cleanup behavior. No replacement file storage or vendor messaging
integration is introduced.

Issue and award notify the active RFQ owner and project PM through seeded in-app
notification templates. A five-minute worker marks overdue issued/evaluating
RFQs and notifies these recipients once. This marker is transactional with the
notifications and does not invent another RFQ lifecycle state. Standard
notification-template administration remains available.

RFQs retain purchasing evidence through cancellation rather than exposing a
hard-delete operation. Project, invited vendor, and awarded contract deletion
previews explicitly classify RFQs as blockers. These rules also apply to demo
records. Removing the source project/vendor/contract must not erase RFQ history.

## API and field validation

Base: `/api/operational-projects/{projectId}/procurement/rfqs`
(also available under `/api/v1`).

| Operation | Method / suffix |
|---|---|
| List / references / CSV export | GET `/`, `/references`, `/export` |
| Detail / evidence export | GET `/{id}`, `/{id}/export` |
| Create / edit | POST `/`, PUT `/{id}` |
| Lifecycle | POST `/{id}/issue`, `/evaluate`, `/cancel`, `/close` |
| Submit quote revision / withdraw | POST `/{id}/bids`, `/{id}/bids/{bidId}/withdraw` |
| Award | POST `/{id}/award` |
| Upload / attach existing file | POST `/documents/upload`, `/{id}/documents` |
| Download RFQ file | GET `/{id}/documents/{documentId}/download` |

`RfqRequests.cs` enforces shape, required fields, lengths, enums, and numeric
ranges. `RfqService.cs` enforces the following business relationships. Forms
mirror the editable constraints for feedback.

| Field | Rule |
|---|---|
| Code, currency, project/customer, totals | Server generated or derived; not writable in RFQ requests |
| Title | Required after trimming, maximum 200 characters |
| BOQ revision | Approved VND revision in the selected project; award requires current approved revision |
| Owner | Active Procurement member of the same project |
| Due date | Future timestamp with timezone; issue requires it still be in the future |
| Vendor IDs | 1–100 unique, active Supplier/SubContractor/Both IDs |
| BOQ lines | 1–500 unique source lines from selected revision |
| Quantity | Positive, ≤ approved quantity, ≤ 6 decimal places |
| RFQ / quotation notes | Optional, trimmed, maximum 2000 characters |
| Quote vendor | Active invited vendor of the same RFQ |
| Quote lines | 1–500 unique RFQ line IDs; omitted lines remain missing |
| Unit price | 0–999999999999.9999, ≤ 4 decimal places |
| Line / quotation total | ≤ 99999999999999.9999 VND |
| Delivery lead time | Whole days, 0–3650 |
| Payment terms | Required after trimming, maximum 1000 characters |
| Quote validity | Zoned timestamp, unexpired, on/after RFQ deadline |
| Quote file IDs | 0–20 unique, unbound, available Procurement files in same project |
| Award bid | Current, complete, unwithdrawn, unexpired, active vendor |
| Contract type | Supply/Subcontract compatible with the selected vendor type |
| Award/cancel/withdraw reason | 3–2000 characters after trimming |
| Row version | Required existing 8-byte concurrency token for changes |
| List query | Search ≤ 200 chars; valid status/owner/date range; whitelisted sort; page 1–1000000; page size 1–100 |

Deadlines are stored and returned as UTC. UI date filters convert local start/end
of day to UTC. CSV cells neutralize formula prefixes.

## Migration and demonstration

`20260908134224_AddRfqBidComparison` adds seven tables, indexes, restrictive
cross-aggregate foreign keys, and RFQ permission grants. It does not rewrite
existing business data. Review deployment backups before rollback: `Down` drops
RFQ evidence and removes this capability's permission assignments.

`RfqSampleDataSeeder` creates only its dedicated `PJ-SAMPLE-RFQ` project and
respects project deletion tombstones. It provides a draft, an issued comparison
with complete/partial quotes, and an overdue RFQ. Re-running does not overwrite
user edits or resurrect the project. Production reference options come from
the API, not hardcoded React values.

## Acceptance traceability

| ID | Criterion | Primary automated evidence |
|---|---|---|
| RFQ-01 | Scope, permissions, cached replay revocation | `ProjectScope_IsEnforcedOnReadWriteExportReferencesAndCachedReplay` |
| RFQ-02 | List, filters, stable pagination, export parity | `List_FiltersSortsPaginates_AndExportUsesSameSelection` |
| RFQ-03 | Draft validation and immutable issued scope | `InvalidDraft_IsRejectedWithoutCreatingRfq`, `DraftUpdates_RequireConcurrency_AndIssuedScopeCannotChange` |
| RFQ-04 | Revision history, missing/zero prices, explicit award | `Lifecycle_PreservesRevisions_AwardsOnce_AndCreatesCorrectDownstreamContract`, `WithdrawalAndExpiredBid_CannotBeAwarded_AndZeroPricesRemainExplicit` |
| RFQ-05 | Field partitions and rejected-state integrity | `InvalidBid_PreservesRfqAndHistory` |
| RFQ-06 | Award dependency revalidation | `Award_RevalidatesDependencies_AndPreservesStateAfterRejection` |
| RFQ-07 | Document scope and retained evidence | `Files_AreProjectScoped_AndLinkedEvidenceBlocksDeletion` |
| RFQ-08 | Notifications and closed-project protection | `Overdue_NotificationsAreOncePerRfq_AndClosedProjectsRejectWrites` |
| RFQ-09 | Decimal rounding and database range | `RfqPricingTests` |
| RFQ-10 | Browser rendering and responsive interaction | `e2e/smoke/admin-rfqs.spec.ts` |

Integration tests exercise the ASP.NET pipeline with InMemory EF, which does not
prove SQL foreign keys or transaction rollback. Migration and deployed browser
validation therefore also require a SQL Server stack. Google Drive transport
is an existing integration; RFQ file isolation is tested separately from live
external Drive availability.

## Validation evidence — 8 September 2026

Validated on the task branch using an isolated SQL Server 2022 database and
ASP.NET Core 8 application on port 5044. Existing containers belonging to another
checkout were left unchanged. The migration applied successfully to the fresh
database; `dotnet ef migrations has-pending-model-changes --no-build` reported
no model drift.

| Check | Result |
|---|---|
| Backend build, `-p:SkipNihomeWebBuild=true` | Passed, no warnings/errors |
| `dotnet format Nihome31042025.sln --no-restore --verify-no-changes` | Passed |
| Backend unit suite | 2,007 passed |
| Backend integration suite | 1,846 passed |
| Focused `RfqsControllerTests` after shared worker locking | 39 passed |
| Frontend ESLint, TypeScript check, Vite production build | Passed |
| RFQ Playwright smoke | Six passed: desktop create, 390/768px comparison, vi/zh/ja rendering; English exercised by the first three |
| Translation seed validation | All 109 keys populated in vi/en/zh/ja |

Backend commands ran in `nih-166-sdk`. Frontend checks ran on the host because
the existing host-installed node modules contain platform-specific packages;
`SkipNihomeWebBuild` skips only the duplicate embedded web build. The separate
frontend production build passed. Browser command, from `nihomeweb`:

```bash
BASE_URL=http://localhost:5044 npx playwright test e2e/smoke/admin-rfqs.spec.ts
```

Additional SQL-backed failure/concurrency scenarios were executed against the
isolated database through the real HTTP API:

- **QA-RACE-01:** Concurrent BGD awards with different idempotency keys and the
  same row version initially reproduced a lock-conversion deadlock. Mutable RFQ
  reads now acquire SQL update locks before loading the aggregate; the overdue
  worker uses the same lock. Three repeated runs returned one 200 and one 409.
  Each RFQ had exactly one contract, one award event, and two notifications
  (owner and PM).
- **QA-ROLLBACK-01:** A temporary database check constraint rejected award
  notification inserts. Award failed with 500; the RFQ version, status and
  history remained unchanged, selected bid/contract/snapshot remained null,
  and no contract or award notification persisted. The injected constraint was
  removed in a finally block.
- The SQL database seeded the dedicated RFQ project and complete/partial/overdue
  examples. Desktop, mobile, and tablet screenshots were visually inspected.

A separate agent performed the Senior Business Analyst review followed by a
Functional QA source/scenario review. BA approved the business contract for QA;
all identified high-impact defects were corrected and exercised. Test execution
and screenshot inspection were performed by the implementing agent, so execution
was not independent. Live Google Drive upload/download was not exercised because
Drive credentials were not configured in the isolated stack; deployment owners
must configure and verify that existing external integration before using file
transport. RFQ document scope and retention were covered in integration tests.
