# RFQ business pipeline: scenario coverage

This is the procurement/finance submatrix of the
[project-wide scenario matrix](project-pipeline-scenario-matrix.md). It does not
define the scope of the full-project testing request.

## Review basis and scope

This matrix covers the supported customer journey from an approved project BOQ
through supplier selection, a downstream contract, material delivery/consumption,
and a manually recorded supplier payment. It is a bounded risk inventory, not a
claim that every combination of every field and state has been tested.

Initial source review baseline: `a5aa899`. Existing execution results are recorded
in [the pipeline validation report](rfq-business-pipeline-validation.md); those
historical results are not a current-run result for tests subsequently changed.

Business sources:

- [Nicon-QLVH.md](Nicon-QLVH.md), Modules 5–6: compare price, delivery and payment
  terms; enforce BOQ quantities; track site stock; contract with selected vendors.
- [Nicon-workflow.md](Nicon-workflow.md): Customer → Project → Contract context.
- [Nicon_BreakTask_v1.xlsx](Nicon_BreakTask_v1.xlsx): worksheet XML `sheet4`, rows
  64–66 identify RFQ list, create/edit and bid-selection detail; row 74 identifies
  receipt/issue forms. Workbook implementation-status labels are historical.
- [RFQ contract](rfq-bid-comparison.md): one complete winning bid, VND, immutable
  issued scope/revisions, explicit award, project access, and rounding.
- [User guide](user_guide.md): delivered finance/project-report boundaries.
- Current DTOs, controllers, services and seeded role permissions define the
  implemented transitions. A missing test is a coverage gap; an unimplemented
  financial integration is a capability boundary, not an expected test result.

The senior-business-analyst review approves this supported contract as a QA test
basis. It does not approve missing requirements or declare all product modules
complete. Functional QA must distinguish automated assertions, current execution,
historical execution, source inspection and untested external paths.

## Actors and handoffs

| Step | Actor and permission | Endpoint / observable outcome |
|---|---|---|
| BOQ create/submit | Procurement, `proc.boq.manage` | `/api/operational-projects/{projectId}/procurement/boq-revisions`; Draft → Submitted |
| BOQ decision | BGD, `proc.boq.approve` | `boq-revisions/{id}/decision`; Approved or Rejected |
| RFQ prepare/issue/quotes/evaluate | Procurement, `proc.rfqs.manage` | Same procurement base, `/rfqs`; immutable issued scope and retained quote revisions |
| Award | BGD, `proc.rfqs.award` | `rfqs/{id}/award`; one Draft Downstream contract with selected vendor/BOQ lines |
| Contract signing | Sales Manager, `crm.contracts.manage` and existing scope | `/api/contracts/{id}/transition`; Signed, subject to customer prerequisites |
| Material demand | PM, `proc.material-requests.manage` | Separate MR on the approved BOQ; submission preserves project/owner |
| MR decision | Procurement, `proc.material-requests.approve` | Approved demand; RFQ itself does not reserve this quantity |
| Receive/issue/reverse | Warehouse, `proc.warehouse.post` / `reverse` | Receipts/issues, posted stock, fulfillment and compensating reversal records |
| Prepare payable | Accountant, `finance.payments.manage` | `/api/finance/payment-references` supplies minimum eligible identities; `/payment-requests` creates Draft |
| Validate payable | Assigned Accountant, `finance.payments.manage` | UnderValidation → ReadyForApproval; evidence metadata required |
| Decide payable | BGD, `finance.payments.approve` | Approved or Rejected; assigned Accountant cannot self-approve |
| Record payment | Accountant, `finance.payments.pay` | Approved → Paid; persisted actor/date/history and finance-list readback |

Project-scoped procurement endpoints must reject unrelated projects even where
the user has the functional permission. Finance management has its existing
cross-owner downstream contract scope. Its narrow reference endpoint does not
grant general CRM list/detail access or expose prices/customer/contact fields.

## Existing lower-layer evidence

Paths below are relative to the repository root. `Covered` means suitable test
assertions exist at this review baseline; it is not a statement that this review
executed them. New current-run results belong in the execution section below.

| ID | Priority and measurable rule | Existing evidence | Initial assessment |
|---|---|---|---|
| SCOPE-01 | Critical: no unauthorized project reads/writes/export or cached replay after membership removal | `RfqsControllerTests.ProjectScope_IsEnforcedOnReadWriteExportReferencesAndCachedReplay` | Covered at API boundary |
| SCOPE-02 | Critical: Accountant discovers eligible Procurement-owned contract with minimum fields, without CRM access | `FinancePaymentReferencesTests.Accountant_DiscoversEligibleProcurementContract_WithoutGainingCrmAccessOrSensitiveFields`; permission theory | Covered: states, vendors, active accountants, exact JSON fields, 401/403 and CRM 404 |
| BOQ-01 | High: approved BOQ is immutable; duplicate item codes/over-limit quantities rejected | `ProcurementControllerTests.BoqLifecycle_ApprovesImmutableRevision`, `BoqUpdate_ValidatesDuplicateCodesAndPreservesRejectedInput`, `MaterialRequestCreate_RejectsExcessAllowanceWithoutPersistence` | Covered; rejection→edit→resubmit continuation is separate |
| RFQ-01 | High: invalid scope does not create RFQ | `RfqsControllerTests.InvalidDraft_IsRejectedWithoutCreatingRfq` | Covered partitions: title/length, deadline/timezone, owner, duplicate/empty/inactive vendors, wrong/unapproved/currency BOQ, missing/duplicate/wrong lines, quantity bounds/precision, note length |
| RFQ-02 | Critical: issued scope cannot mutate; stale update rejected | `DraftUpdates_RequireConcurrency_AndIssuedScopeCannotChange` | Covered at API boundary |
| RFQ-03 | High: invalid quote preserves version, bids and history | `InvalidBid_PreservesRfqAndHistory` | Covered: cross-RFQ line, uninvited vendor, negative/overprecision price, empty/duplicate lines, blank terms, lead time, validity, amount range, wrong document |
| RFQ-04 | Critical: partial/withdrawn/expired quotes cannot win; zero is explicitly quoted | `Lifecycle_PreservesRevisions_AwardsOnce_AndCreatesCorrectDownstreamContract`, `WithdrawalAndExpiredBid_CannotBeAwarded_AndZeroPricesRemainExplicit` | Covered; latest-withdrawal recovery and equal-price choices require additional scenarios |
| RFQ-05 | Critical: award rechecks owner and current approved BOQ, including newer non-VND BOQ | `Award_RevalidatesDependencies_AndPreservesStateAfterRejection` | Covered owner-left/inactive and replaced BOQ partitions |
| RFQ-06 | Critical: exactly one awarded contract and retained decision evidence on retry | `Lifecycle_PreservesRevisions_AwardsOnce_AndCreatesCorrectDownstreamContract` | Covered sequential idempotency/stale replay; real concurrent SQL is distinct |
| FILE-01 | Critical: cross-project documents rejected and linked evidence cannot be deleted | `Files_AreProjectScoped_AndLinkedEvidenceBlocksDeletion` | Covered binding, download scope, reference filtering and deletion blockers; live transport separate |
| PRICE-01 | Critical: decimal calculations and supported storage range | `nihomebackend.tests/Services/RfqPricingTests.cs` | Unit coverage of rounding/range; no browser duplicate required |
| CONTRACT-01 | High: generated draft can sign; classification/customer prerequisites preserved | RFQ lifecycle/pipeline tests; `ContractsControllerTests.Transition_DraftToSigned_Succeeds`, `Transition_CompanyContract_RequiresTaxIdAndLegalRepresentative`, `Transition_LegacyUnclassifiedContract_ReturnsBadRequestWithoutMutation` | Covered; Supplier vs Subcontractor/Both award mapping is separate |
| STOCK-01 | Critical: excess receipt/issue fails without persistence, negative stock blocked | `ProcurementControllerTests.WarehouseLifecycle_BlocksNegativeStockAndReversesSafely` | Covered at creation/posting boundaries |
| STOCK-02 | Critical: partial→full fulfillment and reversal restore status/stock | `WarehouseReceipts_TransitionMaterialRequestFulfillmentAndReversal`, `WarehouseLifecycle_BlocksNegativeStockAndReversesSafely` | Covered 40+60 delivery and reversal; RFQ-linked chain adds identity continuity only |
| STOCK-03 | High: draft edits work, posted records lock, detail and stock readbacks agree | `WarehouseDrafts_UpdateDetailAndLockAfterPosting`, `WarehouseTransactions_ListFiltersAndReturnsProjectStock` | Covered at API boundary |
| STOCK-04 | High: receipt/issue reversal retains actor/audit/KPI evidence | `ProcurementBusinessPipelineTests.ProcurementPipeline_CompletesDemandToWarehouseAndKpiEvidence` | Covered ordinary warehouse handoff; no need to duplicate every assertion in RFQ UI |
| PAY-01 | Critical: invoice must reference signed eligible matching-vendor contract; duplicate invoice rejected | `RfqProcurementPipelineTests`; `FinanceControllerTests.DuplicateSupplierInvoice_PerVendor_IsRejectedWithoutSecondRow` | Covered Draft-contract, wrong-vendor, duplicate and premature-pay rejection |
| PAY-02 | Critical: assigned Accountant validates, BGD approves, Accountant pays once; Paid cannot cancel/edit | `FinanceControllerTests.PaymentLifecycle_EnforcesSelfApprovalAndPaidImmutability`; RFQ pipeline | Covered happy path, unauthorized self-approval, history, Paid readback and retry |
| PAY-03 | High: closed periods/corrections have separate controlled lifecycle | `PeriodClose_UsesVietnamBoundaries_RequiresSequence_AndCannotReopen`, `CorrectionWorkflow_RejectsInvalidTransition_ApprovesAndReversesOnce` | Covered adjacent finance capability; not an automatic Paid ledger side effect |
| NOTIFY-01 | High: owner/PM notified once for issue/award/overdue | RFQ lifecycle; `Overdue_NotificationsAreOncePerRfq_AndClosedProjectsRejectWrites` | Covered sequential behavior; worker/mutation races are SQL scenarios |
| UI-01 | High: actual business roles can finish full supplier journey and reload Paid result | `nihomeweb/e2e/smoke/rfq-business-pipeline.spec.ts` | Existing happy-path browser journey |
| UI-02 | Medium: mobile/tablet, four languages and failed/empty lookup recovery | `admin-rfqs.spec.ts`, `admin-finance-control.spec.ts` | Existing rendering and lookup recovery; alternate terminal flows remain separate |

## Prioritized alternate/recovery additions

Add public-API tests for business state/data rules; use E2E only for actual form,
navigation, action availability and role-handoff behavior. Seed foundational
project/customer/vendor/membership data; do not seed intermediate approved states
when claiming an end-to-end pipeline. Reuse valid DTO helpers and existing tests.

| ID | Priority | Scenario and expected assertions | Target layer / initial gap |
|---|---|---|---|
| ALT-01 | High | BOQ rejected with reason; Procurement corrects and resubmits; BGD approves; RFQ uses resulting approved version | Integration continuation; missing from original full RFQ pipeline |
| ALT-02 | High | Submit revision 1 then 2; withdraw 2; revision 1 must remain superseded; fresh revision can recover before deadline; evaluation blocks further submissions | Integration; browser quote revision/reason controls where useful |
| ALT-03 | Critical | Subcontractor wins Subcontract; Both may win compatible Supply/Subcontract; mismatched type rejects with no contract/history mutation; signed result remains eligible for payable | Integration; do not invent construction acceptance linkage |
| ALT-04 | High | Rejected UnderValidation and Rejected ReadyForApproval payments require reason, retain history, cannot pay/edit or silently return to Draft | Integration terminal partitions; browser rejection form/action visibility |
| ALT-05 | Critical | Approved but unpaid request can cancel with reason; Cancelled cannot pay; Paid cannot cancel; rejected operations preserve state/version/history | Integration; browser cancellation form and reload |
| ALT-06 | High | Wrong assigned Accountant cannot validate; missing evidence cannot validate; no approval/payment side effects on either rejection | Integration; distinguish same-role ownership denial from lacking permission |
| ALT-07 | High | Split delivery on RFQ-awarded contract line reaches partial/full; reversal after consumption blocked; issue then receipt reversal restores quantities/status without deleting evidence | Integration identity continuity extension; underlying arithmetic already covered by STOCK-02 |
| ALT-08 | High | Draft/Issued/UnderEvaluation RFQ cancellation terminal; no later bid/award; Awarded→Closed retains contract/evidence; Closed cannot re-award | Integration state table; existing happy path covers close but not all cancellation sources |
| ALT-09 | High | Equal valid totals highlight all ties; selecting a higher-priced eligible bid with reason remains allowed; no automatic selection | Integration comparison semantics, not duplicated price-unit tests |
| ALT-10 | High | Dependency changes between draft/issue/award: inactive vendor, changed project status, replaced BOQ; reject atomically | Extend existing RFQ partition tests only for missing combinations |

These rows describe a bounded review backlog. A planned addition is not a passed
scenario. The final review should replace initial-gap language with exact test
names and results only after inspecting the implemented assertions and execution.

## SQL and external validation boundaries

| ID | Priority | Scenario / boundary | Current-review status |
|---|---|---|---|
| SQL-01 | Critical | Concurrent awards with different keys/same version: one winner, one conflict, one contract/event/notification set | Blocked for current review pending dedicated SQL execution; prior manual evidence is historical, not proof for new working-tree changes |
| SQL-02 | Critical | Inject failure after contract insert/notification write: transaction leaves unchanged RFQ and no partial contract/notification rows | Blocked for current review pending SQL fault-injection run; InMemory tests cannot establish transaction rollback |
| SQL-03 | Critical | Competing receipt/issue posts or reversal vs post cannot overfill demand or make stock negative; one deterministic financial effect on retries | Blocked pending concurrent relational scenario harness/execution; sequential stale-token coverage is not concurrency proof |
| EXT-01 | High | Actual Drive upload/download, credentials, retries, cleanup and external retention | Blocked without configured external credentials; metadata and ownership tests remain valid but do not prove transport |

## Explicit unimplemented or separate capabilities

- RFQ award is one complete bid in VND. Split awards, weighted/automatic vendor
  selection, currency conversion, tax/freight/discount arithmetic and vendor
  portal/outbound email are outside the delivered RFQ contract.
- RFQ does not auto-create or reserve an MR. Delivery uses a separately approved
  MR. Its optional contract-line reference is a continuity link, not three-way
  matching or an enforced cumulative purchase-order quantity ceiling.
- Supply receipt/inspection and construction acceptance are different records.
  A Subcontract can lead to a manual payable; current payable DTOs have no typed
  receipt/acceptance reference and no automatic acceptance-to-invoice gate.
- Invoice documents in these tests are metadata. They do not establish file
  existence, substantive invoice validity, an actual bank transfer or tax checks.
- Payment Rejected/Cancelled states have no documented resubmit/reopen operation.
  Tests must verify the supported terminal behavior; they must not invent one.
- Paid readback and Accountant processing-time KPI are delivered outcomes.
  Project reports explicitly mark actual cashflow/revenue/expenditure/P&L
  unavailable. No actual-ledger posting or bank reconciliation is claimed.
- Vendor rating, signed-scan execution, VO approval, accounting-period closure
  and correction each have separate existing workflows/tests. Extending the
  RFQ payment journey does not certify every branch of those capabilities.

## Final execution and independent review

The independent reviewer inspected the final source and execution logs. The
implementing agents executed tests; this reviewer edited documentation only.
Final RFQ integration: **10 passed, zero failed/skipped**, five seconds. Full
integration: **1,885 passed** before final RFQ assertion-only edits, which the
focused rerun verified. Full browser: **215 passed, one protected live-Drive
skip**, including all three RFQ Paid/Rejected/Cancelled outcomes without retries.
Full unit: **2,007 passed**. Backend format and frontend quality checks passed.
Exact commands and project-wide evidence are in the project matrix.

### Current task additions reviewed

The reviewer inspected
`RfqProcurementPipelineTests.ApprovedScope_ToAward_AndInvoiceOutcome_PreservesBusinessHandoffs`.
The current source contains ten integration partitions, all passed in the final
focused rerun after the final test-only assertions:

| Vendor / contract | Invoice outcomes |
|---|---|
| Supplier / Supply | Paid, RejectedUnderValidation, RejectedReadyForApproval, Cancelled; RFQ cancellation from Draft, Issued and Evaluation |
| SubContractor / Subcontract | Paid |
| Both / Subcontract | Paid, with equal-price alternative |
| Both / Supply | Paid, with an explicitly selected non-lowest bid |

| Gap ID | Current disposition after assertion review |
|---|---|
| ALT-01 | Still open: the new pipeline submits and approves BOQ directly; no rejected BOQ correction/resubmission branch |
| ALT-02 | Withdrawal/revision recovery covered: old revision remains noncurrent/ineligible after latest withdrawal, third revision recovers. A direct API submission after evaluation is not asserted by this addition |
| ALT-03 | Covered compatible Subcontractor/Both contracts and wrong-type rejection, then signing and payable; no invented construction-acceptance link |
| ALT-04 | Covered both rejection source states, required reason and unpayable terminal behavior |
| ALT-05 | Covered approved cancellation, required reason, unpayable terminal state and Paid cancellation rejection |
| ALT-06 | Partially closed: a permission-bearing ADMIN cannot validate the assigned Accountant’s request; state, event count and validation timestamp remain unchanged. Missing-evidence validation remains a distinct partition |
| ALT-07 | Covered RFQ-linked partial delivery, reversal restoration, full delivery/consumption and blocked reversal of consumed stock; existing warehouse tests continue to own general arithmetic |
| ALT-08 | Awarded→Closed and cancellation from Draft/Issued/Evaluation are covered, including blank-reason rejection, replay without duplicate event, terminal issue rejection and no contract/MR creation |
| ALT-09 | Non-lowest explicit selection and both equal-price bids being highlighted are explicitly asserted |
| ALT-10 | Existing owner/BOQ partitions retained; no new vendor-inactive-at-award partition inspected |

The Supplier/Paid partition calls `PrepareWonTenderEstimateAsync`: Sales Manager
creates a same-project/customer opportunity and tender, imports/submits an
estimate, rejects unauthorized Sale approval, approves it and completes the
checklist. Procurement sourcing before Won is rejected without a BOQ row;
mark-Won then permits the sourced BOQ and downstream RFQ/warehouse/payment chain.
Persisted BOQ source estimate and project identifiers are explicitly asserted.
This does not create the tender-linked upstream commercial contract.

**BA:** the bounded implemented contract is approved as a QA basis; no new
critical/high business defect was found in this independent source review.
**QA:** executed assertions pass, with no claim of exhaustive business coverage.
The remaining alternate partitions above, SQL races/fault injection and live
transport keep separate unproved status. The one skipped browser requires
protected Drive OAuth credentials and RootFolderId. An initial SQL 3980 receipt
failure did not recur in the final combined run; its cause remains unexplained.
The broader project gaps and reported handover-card scope issue remain in the
project matrix.
