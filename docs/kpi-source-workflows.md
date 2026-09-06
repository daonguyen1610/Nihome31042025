# KPI Source Workflow Design

## Status and objective

This document designs the operational source workflows required by the ten KPI
definitions that currently return `MissingData`. It does not mark those metrics
as delivered. Rules labelled **Decision required** must be approved by the
Product Owner or NICON business owner before implementation.

The objective is to produce auditable business events for KPI calculation,
without treating free text, mutable current state, generic audit logs, or
inferred ownership as source evidence.

## Current constraints

- The quotation BOQ and material-rate catalog are commercial estimating
  sources. They are not an approved execution BOQ or warehouse ledger.
- `PunchItem` has an assignee and verification workflow but no root-cause or
  responsible designer attribution.
- `ContractPaymentMilestone` already has `DueDate`, `ActualPaymentDate`, and a
  status transition endpoint. It lacks responsible-accountant attribution and
  append-only transition evidence.
- Vendor master data exists, but project or contract rating does not.
- Material Request, structured receipts/issues, HSE violations, Payment
  Requests, Accounting Periods, and Accounting Corrections do not exist.
- The role catalog has `QS`, `ACCOUNTANT`, and `WAREHOUSE`, but no
  `PROCUREMENT` role. `KpiService` maps `QS` to `TENDERING`; therefore no current
  user is eligible for the seeded `PROCUREMENT` KPI definitions.

## Shared source-event contract

Every KPI source added by this design must satisfy these rules:

1. Store an explicit `OperationalProjectId`, responsible employee ID, event
   timestamp, lifecycle status, creator/updater, and row version.
2. Capture responsibility on the source event. Do not derive historical KPI
   ownership from the employee's current role or the project's current team.
3. Count only terminal business events such as `Approved`, `Confirmed`,
   `Posted`, or `Paid`. Draft, rejected, cancelled, and deleted records are not
   KPI evidence.
4. Use a half-open Vietnam-time interval: event timestamp is greater than or
   equal to period start and less than period end after conversion to UTC.
5. Make approved or posted financial and inventory records immutable. Correct
   them through a linked reversal or a new version, not an in-place edit.
6. Require `Idempotency-Key` on creates and transitions, and row version or
   `If-Match` on updates. Replayed requests return the original response;
   conflicting payloads return `409`.
7. Apply project visibility through `IProjectAccessService` in addition to the
   global permission. An inaccessible project returns `404`.
8. Write standard audit events for every transition. Evidence JSON stores the
   source entity type, record IDs, source version, responsible user, and event
   timestamp used by the calculation.
9. A period with no qualifying denominator or no approved source records stays
   `MissingData`. It is never scored as zero.
10. A locked KPI snapshot remains immutable even if source records are later
    corrected. Recalculation of locked periods requires a separately approved
    reopen workflow.

## Delivery slices

### SW-00: Procurement position eligibility

Add a distinct `PROCUREMENT` business role and map it to the `PROCUREMENT` KPI
position. Do not map `WAREHOUSE` to Procurement: receiving and issuing stock is
a control function, while vendor selection and purchase ownership are KPI
responsibilities.

Proposed grants:

| Actor | Permissions |
| --- | --- |
| Procurement | View projects and BOQ; manage assigned requests and downstream contract lines; view vendors; submit ratings; view own KPI |
| Warehouse | View approved requests; receive and issue stock; view project BOQ balance; no vendor selection or rating approval |
| PM / Site lead | Create requests; view project material transactions; confirm site responsibility |
| BGD / authorized manager | Approve BOQ revisions and vendor ratings; cross-user KPI management remains separate |

**Decision required D-01:** Confirm whether `PROCUREMENT` is a new system role,
an existing custom role, or a project-team assignment. A user with only `QS`
must remain `TENDERING` unless the business explicitly approves dual positions.
Implementation must also add the approved role to `KpiPosition` and
`KpiCalculationBackgroundService.HasKpiPosition`; adding RBAC data alone does
not make the user eligible for scheduled calculation.

### SW-01: Approved execution BOQ

Create a project-owned, versioned execution baseline rather than reusing mutable
Quote rows.

Proposed entities:

| Entity | Required data |
| --- | --- |
| `ProjectBoqRevision` | Project, revision number, currency, status, optional source Tender Estimate revision, cost total, prepared/approved actors and timestamps, row version |
| `ProjectBoqLine` | Revision, stable item code, description, unit, approved quantity, approved unit-cost ceiling, amount, optional construction work item/material catalog link |

Lifecycle: `Draft -> Submitted -> Approved` or `Rejected`; a rejected revision
may return to Draft. Approval supersedes the previous approved revision from
its effective timestamp. Approved revisions and lines are immutable.

API surface:

| Method | Route | Permission |
| --- | --- | --- |
| `GET` | `/api/operational-projects/{projectId}/boq-revisions` | `proc.boq.view` plus project access |
| `POST` | `/api/operational-projects/{projectId}/boq-revisions` | `proc.boq.manage` |
| `PUT` | `/api/operational-projects/{projectId}/boq-revisions/{id}` | `proc.boq.manage` |
| `POST` | `/api/operational-projects/{projectId}/boq-revisions/{id}/submit` | `proc.boq.manage` |
| `POST` | `/api/operational-projects/{projectId}/boq-revisions/{id}/decision` | `proc.boq.approve` |

This source unlocks tender estimate accuracy and supplies the budget quantity
and unit-cost ceiling for procurement and material metrics.

**Decision required D-02:** Define which approved execution revision is the
"final actual BOQ" for Tender KPI: first approved construction baseline, last
approved revision before completion, or the final approved revision at project
completion. Also approve whether approved Variation Orders change that baseline.

### SW-02: Design-error attribution on Punch Items

Extend `PunchItem`; do not create a second defect aggregate.

Proposed fields:

- `RootCauseCode`: `Design`, `Construction`, `Material`, `ClientChange`, or
  `Other`, stored as an approved master-data code.
- `ResponsibleDesignUserId`: required only when root cause is `Design`; the user
  must be an active design member of the same project.
- `RootCauseConfirmedByUserId` and `RootCauseConfirmedAt`.
- `RootCauseNote`, required for `Other` and recommended for `Design`.

Only a verifier with `construction.punch.classify` may confirm or change root
cause. The assignee remains the person fixing the item and must not be reused as
the responsible designer. Once a Punch Item is `Verified`, attribution changes
require reopening it and create an audit entry.

KPI source event: a verified Punch Item with confirmed `Design` root cause,
bucketed by `VerifiedAt` and attributed to `ResponsibleDesignUserId`.

Proposed raw value: count of qualifying items. Scoring remains
`LowerIsBetter` against the configured target.

**Decision required D-03:** Confirm whether severity weights the metric or all
confirmed design errors count equally. The source document specifies a count,
so equal counting is the safest default.

### SW-03: HSE violation workflow

Create a separate HSE aggregate. A Punch Item tracks a deliverable defect and
does not contain the regulatory, penalty, and remediation data required for an
HSE event.

Proposed `HseViolation` data:

- Project, code, occurred timestamp, location, violation category, severity,
  description, regulatory reference, and evidence documents.
- `ResponsibleSiteUserId`, remediation owner, deadline, remediation note.
- Reporter, confirmer, confirmed timestamp, closer, and closed timestamp.
- Optional penalty reference and amount; do not expose payroll-sensitive data
  to users with only project view permission.

Lifecycle: `Draft -> Reported -> Confirmed -> Remediated -> Closed`, with
`Reported -> Rejected` and `Confirmed -> Cancelled` restricted to an authorized
manager with a reason. Confirmed records are immutable except through a formal
correction event.

API root: `/api/operational-projects/{projectId}/hse-violations` with
`construction.hse.view`, `manage`, `confirm`, and `close` actions. The reporting
form must be mobile-first and preserve an idempotent offline client identifier
for later synchronization.

KPI source event: confirmed violations attributed to `ResponsibleSiteUserId`,
bucketed by `ConfirmedAt`. Raw value is the count; scoring is
`LowerIsBetter` against the configured target.

**Decision required D-04:** Confirm whether rejected/cancelled records and
severity affect the KPI. The source document says confirmed, penalized events;
therefore drafts and unconfirmed diary text cannot count.

### SW-04: Material and procurement transaction chain

This slice must be delivered after SW-01. It provides one traceable chain from
approved demand to negotiated cost, receipt, and site issue.

Proposed aggregates:

| Aggregate | Key contract |
| --- | --- |
| `MaterialRequest` and lines | Project, BOQ line, requested quantity/date, site requester, responsible site user, assigned procurement user; `Draft -> Submitted -> Approved/Rejected -> PartiallyFulfilled -> Fulfilled/Cancelled` |
| Downstream `ContractLine` | Contract, BOQ line, quantity, negotiated unit price, procurement owner snapshot; allowed only for compatible downstream contract types |
| `WarehouseReceipt` and lines | Project, request line, downstream contract line, received quantity, inspected timestamp, received by; `Draft -> Posted/Reversed` |
| `WarehouseIssue` and lines | Project, BOQ line, issued quantity, work item, responsible site user, issued timestamp; `Draft -> Posted/Reversed` |

Posted receipt and issue quantities update inventory in the same transaction.
Stock may not become negative. A reversal references the original row and uses
the opposite quantity; posted records are never edited or deleted. Request
approval checks remaining approved BOQ quantity and requires an override reason
plus elevated permission when demand exceeds the ceiling.

Suggested permission modules are `proc.material-requests`, `proc.contract-lines`,
and `proc.warehouse`, each with explicit view/manage/approve/post actions.

This chain supports three KPI definitions:

| KPI | Proposed source and raw value |
| --- | --- |
| Procurement cost optimization | Signed downstream contract lines attributed to their procurement owner. Proposed raw percentage is `100 * sum((BOQ unit ceiling - negotiated unit price) * quantity) / sum(BOQ unit ceiling * quantity)`; negative values expose overruns rather than being clamped in source data. |
| Procurement delivery time | Fulfilled approved requests attributed to the assigned procurement user. Raw value is average elapsed hours from `ApprovedAt` to the final posted receipt that satisfies the approved quantity. |
| Site material waste | Posted issue quantity attributed to the responsible site user and compared with the effective approved BOQ quantity for the same lines. |

**Decision required D-05:** The source document describes
`issued quantity / BOQ maximum`, while the metric name says waste. Approve either
utilization percentage or excess-waste percentage
`max(issued - allowance, 0) / allowance`. They produce materially different
scores.

**Decision required D-06:** Confirm whether procurement delivery time uses
calendar hours or working hours, and whether partial receipts stop the clock.
The proposed default uses calendar hours and stops only when the request is
fully received.

### SW-05: Vendor rating

Create `VendorRating` as a project and downstream-contract scorecard. It stores
the Vendor, Contract, evaluation period, quality/schedule/cost/HSE component
scores, component weights, overall score, comments, procurement owner snapshot,
submitter, approver, timestamps, and row version.

Lifecycle: `Draft -> Submitted -> Approved` or `Rejected`. Approved ratings are
immutable; a correction creates a superseding version. Enforce one current
rating per contract and evaluation period.

API root: `/api/operational-projects/{projectId}/vendor-ratings` with
`proc.vendor-ratings.view`, `manage`, and `approve`. Procurement may prepare a
rating but must not approve its own rating. PM or an authorized manager supplies
the independent approval.

KPI source event: approved ratings bucketed by `ApprovedAt` and attributed to
the captured procurement owner. Raw value is the average approved overall
score, with the source component scores retained in evidence.

**Decision required D-07:** Approve rating cadence, component weights, minimum
sample size, and approver. The source only requires a periodic average and does
not define these controls.

### SW-06: Receivable ownership and collection evidence

Extend `ContractPaymentMilestone` instead of introducing a duplicate receivable
entity in the first slice:

- Add `ResponsibleAccountantUserId`, assigned before the milestone becomes
  `Requested`.
- Add `RequestedAt` and preserve the existing `DueDate` and
  `ActualPaymentDate`.
- Add append-only `ContractPaymentMilestoneEvent` rows for status, actor,
  timestamp, and note.
- Stop replacing milestone history after a Contract is active; mutate each
  milestone through its transition endpoint with row-version protection.

Only upstream Contract milestones are eligible. Proposed denominator is all
milestones on non-cancelled Contracts whose `DueDate` falls in the KPI month
and have a responsible accountant. Numerator is those paid on or before
`DueDate`. The responsible accountant receives the result even if another user
records the payment.

**Decision required D-08:** Confirm denominator behavior for unpaid milestones
whose due date has passed, partial payments, reassignment during a period, and
payments received before the due month.

### SW-07: Partner Payment Request

Create a payable workflow linked to a downstream Contract and Vendor.

Proposed `PaymentRequest` data includes request code, supplier invoice number,
invoice date/amount, received timestamp, validated timestamp, assigned
accountant, Contract, optional milestone, attachments, approval actor/time,
paid actor/time, rejection reason, and row version. Supplier invoice number is
unique per Vendor.

Lifecycle:

`Draft -> UnderValidation -> ReadyForApproval -> Approved -> Paid`

`UnderValidation` or `ReadyForApproval` may become `Rejected`; `Approved` may be
cancelled only by an elevated role before payment, with a reason. A paid request
is immutable and corrections use a linked reversal.

API root: `/api/finance/payment-requests` with `finance.payments.view`,
`manage`, `approve`, and `pay`. The assigned accountant cannot approve their own
request; payment confirmation is a separate permission.

KPI source event: paid requests attributed to the assigned accountant and
bucketed by `PaidAt`. Proposed raw value is average elapsed hours from
`ValidatedAt` to `PaidAt`.

**Decision required D-09:** Confirm whether the clock ends at approval or bank
payment, and whether weekends, holidays, rejected requests, and requests waiting
for missing vendor documents are excluded.

### SW-08: Accounting period and correction log

Generic `AuditLog` is not a valid accounting source because it has no accounting
period, original entry, reason code, approval, or reversal semantics.

Proposed entities:

| Entity | Required data |
| --- | --- |
| `AccountingPeriod` | Year, month, Vietnam-time UTC boundaries, status, closed/reopened actor, timestamp, reason, row version; unique by year and month |
| `AccountingCorrection` | Project, accounting period, source entity type/ID, reason code, original and corrected values, responsible accountant, recorder, approver, approval timestamp, optional reversal link |

Period lifecycle is `Open -> Closing -> Closed`; reopening requires an elevated
permission and reason. Correction lifecycle is
`Draft -> Submitted -> Approved/Rejected -> Reversed`. Approved and reversed
records are immutable.

API roots are `/api/finance/periods` and `/api/finance/corrections`, using
`finance.periods.view/close/reopen` and
`finance.corrections.view/manage/approve/reverse`.

KPI source event: approved post-close corrections attributed to the responsible
accountant and bucketed by `ApprovedAt`. Raw value is count; scoring is
`LowerIsBetter` against the configured target.

**Decision required D-10:** Define which correction reasons count, whether a
reversal counts again, and whether attribution belongs to the original entry
owner or the employee who records the correction. The proposed default assigns
the correction to the original entry owner.

## Metric contract summary

| KPI code | Qualifying timestamp | Employee attribution | Raw type | Dependency |
| --- | --- | --- | --- | --- |
| `TENDER_ESTIMATE_ACCURACY` | Approved final execution BOQ timestamp | Tender preparer snapshot | Accuracy percentage | SW-01, D-02 |
| `DESIGN_SITE_ERRORS` | Punch `VerifiedAt` | Responsible designer | Count | SW-02, D-03 |
| `SITE_MATERIAL_WASTE` | Warehouse issue posted timestamp | Responsible site user | Percentage | SW-01, SW-04, D-05 |
| `SITE_HSE` | Violation confirmed timestamp | Responsible site user | Count | SW-03, D-04 |
| `PROCUREMENT_COST` | Downstream contract signed timestamp | Procurement owner | Savings percentage | SW-00, SW-01, SW-04 |
| `PROCUREMENT_ON_TIME` | Request fully received timestamp | Assigned procurement user | Average hours | SW-00, SW-04, D-06 |
| `PROCUREMENT_VENDOR_RATING` | Rating approved timestamp | Procurement owner snapshot | Average score | SW-00, SW-05, D-07 |
| `ACCOUNTING_COLLECTION` | Milestone due date | Responsible accountant | On-time percentage | SW-06, D-08 |
| `ACCOUNTING_PAYMENT_SPEED` | Payment `PaidAt` | Assigned accountant | Average hours | SW-07, D-09 |
| `ACCOUNTING_ACCURACY` | Correction approved timestamp | Responsible accountant | Count | SW-08, D-10 |

## Proposed calculation contracts

These formulas are implementation proposals, not approved business decisions.
They define the missing details that must be accepted or replaced at the
corresponding decision gate.

### Direct percentage metrics

Direct percentage metrics produce a score from zero through 100 without
`TargetValue`.

Tender estimate accuracy for each qualifying project is proposed as:

$$
Accuracy = \max\left(0, 100 -
\frac{|ExecutionBoqValue - TenderBoqValue|}{TenderBoqValue} \times 100\right)
$$

The employee raw value and score are the average project accuracy. A zero
tender BOQ is invalid source data. D-02 must choose `CostSubtotal`,
`GrandBidTotal`, or another matching amount basis on both revisions.

Procurement cost optimization is proposed as:

$$
SavingsPercent = 100 \times
\frac{\sum((BudgetUnitPrice - ActualUnitPrice) \times Quantity)}
{\sum(BudgetUnitPrice \times Quantity)}
$$

The raw value may be negative to disclose an overrun. The score is clamped to
zero through 100 only after aggregation. If the business wants performance
against a savings target instead, retain the raw percentage and score it
through `TargetValue`; this choice must be approved before implementation.

Vendor Rating raw value and score are the average approved overall score.
Receivable collection raw value and score are:

$$
CollectionRate = 100 \times
\frac{MilestonesPaidOnOrBeforeDueDate}{MilestonesDueInPeriod}
$$

A zero denominator returns `MissingData`.

### Target-scored metrics

The following raw values use the existing `AgainstTarget` behavior and require
a positive `TargetValue` before they can become `Available`:

| KPI | Raw value | Direction |
| --- | --- | --- |
| Design site errors | Confirmed design-error count | Lower is better |
| Site HSE | Confirmed violation count | Lower is better |
| Procurement delivery | Average approved-to-fully-received hours | Lower is better |
| Partner payment speed | Average validated-to-paid hours | Lower is better |
| Accounting accuracy | Approved post-close correction count | Lower is better |

Material waste scoring remains undefined until D-05 selects utilization or
excess-waste semantics. It must not be implemented by choosing whichever
formula fits the current `LowerIsBetter` seed.

All elapsed-time proposals use calendar hours. Working-time calendars require
an approved holiday/calendar source and cannot be emulated by subtracting
weekends in KPI code.

## Position activation policy

KPI activation is atomic per position. A production position is lock-ready only
when its active definitions have approved sources, can all return `Available`,
and total exactly 100 percent. Activating a partial position either exposes
permanent `MissingData` or makes the period impossible to lock.

The current foundation deliberately exposes unavailable definitions as
`MissingData`; therefore Tendering, Design, Site, Procurement, and Project
Accounting are not business-ready for period lock. Each rollout must choose one
of these explicit strategies:

1. keep the complete approved position visible and non-lockable until every
  source is delivered; or
2. publish a separately approved temporary 100-percent definition version for
  that position, then supersede it atomically when the remaining sources ship.

Do not silently deactivate one metric and leave weights below 100 percent.
**Decision required D-11:** Approve the rollout strategy per position and the
effective month of each definition version.

## Migration and legacy-data policy

- Add schemas through separate migrations per source slice. Do not combine all
  workflows into one migration.
- Do not synthesize approved source events from free-text diaries, generic
  audit rows, Quote rows, or current team assignments.
- Existing Punch Items and milestones receive null attribution and remain
  excluded until an authorized user classifies or assigns them.
- Existing Contracts may receive line items only through a reviewed import or
  explicit edit workflow. Aggregate Contract value is not silently split.
- Imported BOQ, stock, or finance opening balances require source file hash,
  importer, timestamp, preview, validation report, and explicit confirmation.
- Do not infer historical responsibility from the current project team or a
  generic audit-log creator. Historical periods stay `MissingData` when an
  approved attribution source is unavailable.
- A source workflow deployed mid-month becomes eligible only from its approved
  effective month. Do not partially backfill a month and present it as a full
  sample.
- Extend hard-delete impact plans for every new project-owned aggregate.
  Posted financial and inventory records should normally block hard delete or
  require an approved reversal first.
- KPI definitions, periods, and snapshots have no user-facing hard-delete
  operation. Deactivation preserves definitions; locked snapshots are retained
  for audit and continue to reference frozen definition metadata.

## Test strategy

| Layer | Required proof |
| --- | --- |
| Unit | Formula boundaries, zero denominator, target direction, weighted averages, quantity and money precision, timezone conversion, valid transitions |
| Integration | Authentication, project scope, role separation, idempotency, stale row version, duplicate source keys, approval immutability, reversal behavior, unchanged state after rejection, KPI evidence IDs |
| Migration | Unique/check constraints, delete behavior, no invented legacy events, snapshot alignment, rollback script review |
| E2E | Mobile field reporting for Punch/HSE/MR; desktop approval, receipt, rating, payment, correction, KPI recalculation and locked-period behavior |

For each metric, integration tests must include one qualifying record, one
boundary record exactly at the next period start, one rejected/cancelled record,
one record attributed to another employee, and the no-data case.

## Delivery order and gates

| Order | Slice | Metrics unlocked | Entry gate |
| ---: | --- | --- | --- |
| 0 | SW-00 Procurement eligibility | None directly | D-01 approved |
| 1 | SW-02 Punch attribution | Design site errors | D-03 approved |
| 2 | SW-06 Receivable attribution | Collection rate | D-08 approved |
| 3 | SW-03 HSE violations | HSE count | D-04 approved |
| 4 | SW-01 Execution BOQ | Tender accuracy foundation | D-02 approved |
| 5 | SW-04 Material/procurement chain | Procurement cost/delivery and site waste | SW-01 plus D-05/D-06 |
| 6 | SW-05 Vendor rating | Vendor rating | SW-00 plus D-07 |
| 7 | SW-07 Payment Request | Partner payment speed | D-09 approved |
| 8 | SW-08 Period close/correction | Accounting accuracy | D-10 approved |

A slice is complete only when its backend contract, migration, four-language
translations, frontend states, focused tests, documentation, hard-delete impact,
and KPI calculator branch are delivered together. Until then, its metric must
continue returning explicit `MissingData` and the affected KPI period cannot be
locked.

## Cross-cutting decision gate

Before enabling any of the ten metrics, approve how late source events interact
with a locked KPI period. The current KPI service has no reopen operation.
Safe default: a locked snapshot never changes; a correction is visible in the
source workflow and affects only an explicitly reopened or later period. Adding
reopen requires authorization, reason, audit, notification, optimistic
concurrency, and preservation of the previous locked snapshot version.

**Decision required D-12:** Approve whether KPI periods may be reopened. If not,
late approved events never alter a locked score. If yes, define who may reopen,
the allowed age, employee notification, and how prior snapshot versions remain
visible.