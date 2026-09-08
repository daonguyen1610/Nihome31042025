# Project-wide business pipeline test matrix

## Scope and evidence standard

The scope is the entire application, including all eight customer business
modules, public acquisition/content, recruitment, identity, authorization and
shared operations. [The RFQ matrix](rfq-pipeline-scenario-matrix.md) is one
submatrix; it is not a substitute for project-wide validation.

Authoritative sources are [Nicon-QLVH.md](Nicon-QLVH.md),
[Nicon-workflow.md](Nicon-workflow.md) and
[Nicon_BreakTask_v1.xlsx](Nicon_BreakTask_v1.xlsx). The
[user guide](user_guide.md) distinguishes delivered functionality from future
capabilities. Controllers, services, DTOs and seeded permissions establish the
current executable contract; tests must not invent missing links or transitions.

Initial inventory on the working tree based on `a5aa899`: 80 integration
`*Tests.cs` files, 130 unit `*Tests.cs` files and 59 Playwright `*.spec.ts` files.
These are source-file counts, not test-case counts or execution results. Theory
partitions and generated probes make source counts unsuitable for pass totals.

The named examples below identify suitable existing evidence, not an assertion
that every test in every suite passed in this review. `Existing` means inspected
test assertions exist; `Gap` means a particular joined scenario lacks evidence;
`Blocked` identifies relational/external execution not established by InMemory or
mocked tests. Final counts must come from current-run tools and match the final
working tree. Historical RFQ results remain in their validation report.

## Customer entry paths and shared identity

The workflow has multiple legitimate entry paths, not one mandatory sequence:

| Path | Business journey | Test consequence |
|---|---|---|
| A — Design & Build | Signed D&B contract → design phases → permitting/construction | Verify project/customer inheritance and design-stage readiness |
| B — Design first | Design contract → design/BOQ → later construction contract | Preserve the same project/customer across multiple contracts; do not require a construction contract prematurely |
| C — Competitive tender | Tender preparation/estimate → submission → Won → qualifying contract | Test bid prerequisites, loss/win outcomes and correct opportunity/customer linkage |
| D — Consultation/quotation | Preliminary concept/quotation → negotiation → D&B contract | Preserve preliminary-versus-approved pricing provenance and signing conditions |

Shared identity is Customer → Operational Project → one or more Contracts and
Design Projects. Procurement uses the Operational Project; construction records
often use the Design Project. A joined test must prove both identifiers remain
consistent rather than creating unrelated fixtures for every module.

## Business lifecycle inventory

Integration class names refer to `nihomebackend.integration.tests/Controllers`;
unit classes refer to `nihomebackend.tests/Services`; browser files refer to
`nihomeweb/e2e/smoke`. Names below are searchable references, not assumed results.

| ID | Module / lifecycle and critical outcome | Existing integration/unit evidence | Browser evidence and remaining distinction |
|---|---|---|---|
| CRM-01 | Public contact/lead → qualified conversion → Customer + Opportunity with owner/project identity | `ContactsControllerTests.Submit_IsPublic_ReturnsCreated`; `LeadsControllerTests.Convert_TransitionsLeadAndReturnsConvertedIds`, `Convert_CompanyLeadWithoutTaxId_PreservesCustomerAndOpportunityLinks` | `crm-pipeline.spec.ts` executes APIs; `crm-workflow.spec.ts` navigates/render checks. Neither alone proves a browser lead-conversion journey |
| CRM-02 | Opportunity stages, loss reason, qualifying signed contract before Won | `OpportunitiesControllerTests.ChangeStage_ToWon_RequiresSignedContractAndSetsProbability100`, `ChangeStage_SkippingPipelineStage_IsBadRequestAndPreservesState`, `ChangeStage_FromTerminal_IsRejected` | `admin-opportunities.spec.ts`; preserve ordinary Sales ownership in a joined flow |
| CRM-03 | Approved material-rate catalog → preliminary/unit-cost or BOQ quote → approval/send → versioned edit | `MaterialRateCatalogsControllerTests.BoqLifecycle_ImportsApprovesAndReturnsTypedEffectiveRevision`; `QuotesControllerTests.Boq_CreateReopenUpdateAndApprove_PreservesTotalsAndItems`, `Submit_Approve_Send_HappyPath`, `Update_AfterApproval_SpawnsVersionTwo` | `admin-material-rates.spec.ts`, `admin-quote-pricing.spec.ts`, `admin-quote-documents.spec.ts`; quote totals/provenance belong primarily in integration/unit tests |
| CRM-04 | Tender checklist + approved estimate → submission → Won/Lost → same-customer opportunity | `TenderEstimatesControllerTests.EstimateFlow_ImportsSubmitsEnforcesApprovalPermissionAndApproves`, `TenderSubmission_RequiresCompletedChecklistAndApprovedEstimate`, `WonTransition_RequiresSubmittedTenderAndSameCustomerOpportunity`; `TendersControllerTests` | `admin-tenders.spec.ts`; joined tender → actual contract → approved operational BOQ is a separate high-value branch |
| CRM-05 | Survey evidence and capability documents → CRM/tender usage with private-file access | `SurveysControllerTests`, `CapabilityDocumentsControllerTests`, `BusinessDocumentsControllerTests` | `admin-survey-media.spec.ts`, `admin-capability-documents.spec.ts`, `admin-customer-documents.spec.ts`; live external storage is separate |
| PROJECT-01 | Operational project/team → contracts/design inheritance → visible timeline and scoped access | `OperationalProjectsControllerTests.Contract_InheritsProjectFromOpportunity`, `DesignProject_InheritsProjectFromContract`, `Timeline_AggregatesContractsAndRepeatedReadIsStable`; `OperationalProjectTeamControllerTests.AssignmentReplay_HistoryAndKpiIdentity_RemainUniqueAndImmutable` | `admin-operational-project-timeline.spec.ts`; do not confuse milestone dates with payment-request/bank dates |
| DESIGN-01 | Concept drafts/review → approved/final concept unlocks Basic Design | `ConceptOptionsControllerTests.Transition_Finalize_UnlocksProjectStage`, `Transition_Design_CanMoveButNotFinalize`; `ConceptOptionServiceTests` | Design-related page tests exist; complete concept-to-basic UI handoff requires its own evidence |
| DESIGN-02 | Required basic documents/disciplines → internal approval/readiness → Shop Drawing stage | `BasicDesignDocsControllerTests.Transition_HappyPath_ToInternallyApproved`, `UnlockShopDrawing_HappyPath_AdvancesStage`, `UnlockShopDrawing_NotReady_IsBadRequest`; discipline scope tests | `admin-basic-design.spec.ts`; source-stage seeding is not a joined concept-to-basic proof |
| DESIGN-03 | Shop drawing review + revisions + schedule → approved drawings | `ShopDrawingsControllerTests.Transition_HappyPath_ToApproved`, `Mutations_EnforceProjectStageWhileCleanupDeleteRemainsAvailableLater`; `DrawingRevisionsControllerTests`, `DetailDesignScheduleControllerTests` | `admin-shop-drawing.spec.ts`, `admin-drawing-revisions.spec.ts`, `admin-design-schedule.spec.ts` |
| DESIGN-04 | Approved drawings → IFC draft packet → atomic release → site acknowledgment | `IfcReleasesControllerTests.ReleaseAsync_HappyPath_FlipsDrawings`, `AddItems_DraftingDrawing_IsBadRequest`, `ReleaseAsync_WithoutItems_IsBadRequest`, `Acknowledge_AfterRelease_Succeeds`; `IfcReleaseServiceTests` | `admin-ifc-release.spec.ts` includes real Design Lead release UI; other cases in the same file are API probes |
| PERMIT-01 | Design-project creation seeds legal checklist → submission/correction/issued records | `PermitsControllerTests.Create_DesignProject_AutoSeedsPermitChecklist`, `Patch_UpdatesStatusAndReturnsUpdatedRow`, `Pm_CannotPatch`; `PermitChecklistServiceTests` | Legal pages/data may be covered by design routes; do not infer a mandatory IFC/construction permit gate that current services do not enforce |
| SITE-01 | Construction task dependencies/progress → site diary submit/confirm/reopen | `ConstructionTasksControllerTests.SetPredecessors_Cycle_IsBadRequest`, `Update_ProgressAndStatus_ReturnsUpdatedRow`; `SiteDiariesControllerTests.LifecycleFlow_Submit_Confirm_Reopen`, `Update_AfterSubmit_IsBadRequest` | `admin-construction-tasks.spec.ts`, `admin-site-diary.spec.ts`; joined IFC-to-site identity is separate from isolated CRUD |
| SITE-02 | Partial acceptance submit → approve or reject/revise → immutable approved evidence | `AcceptanceRecordsControllerTests.Approve_AsPm_Succeeds`, `Reject_Then_Revise_Increments_Counter`, `Update_LockedAfterApproved`, `DocumentContent_RequiresPersistedReferenceAndRecordScope` | `admin-acceptance.spec.ts` includes one browser approval journey and API-only rejection/security cases |
| SITE-03 | Punch issue → remediate → PM verification → controlled reopening; HSE confirmation/rejection/KPI eligibility | `PunchItemsControllerTests.VerifyEndpoint_AsPm_FlipsToVerified`, `Reopen_FromVerified_IncrementsCounter`, `Verify_DesignRootCause_RequiresResponsibleDesignMember`; `HseViolationsControllerTests.Lifecycle_InvalidTransitionPreservesState_AndConfirmedRowsAreImmutable`, `RejectAndCancel_RequireReason_AndDoNotQualifyForKpi` | `admin-punchlist.spec.ts`; HSE source tests do not imply full field-browser coverage |
| SITE-04 | Required as-built categories → submit/approve/archive → handover readiness | `AsBuiltDocumentsControllerTests.Approve_AsPm_Succeeds`, `Update_LockedAfterApproved`, `Archive_from_Approved_marks_ArchivedAt`; `HandoverRecordServiceTests.Ready_transition_counts_archived_required_asbuilt_documents` | `admin-asbuilt.spec.ts`; preserve approved evidence when testing handover readiness |
| SITE-05 | Acceptance + required as-built + no unresolved punches + commissioning/checklist → Ready → complete with signatory → controlled reopen | `HandoverRecordServiceTests.Ready_transition_requires_all_upstream_conditions`, `Complete_uses_dedicated_action_and_requires_signatory`, `Complete_then_reopen_clears_completion_and_increments_counter`; `HandoverRecordsControllerTests.Complete_AsDesign_IsForbidden_ButAsPmSucceedsWhenReady` | `admin-handover.spec.ts` is create/view/edit/delete coverage; not proof of completion/reopen through ordinary PM UI |
| PROC-01 | Vendor directory → approved project BOQ → MR approval → warehouse delivery/consumption/reversal | `ProcurementBusinessPipelineTests.ProcurementPipeline_CompletesDemandToWarehouseAndKpiEvidence`; `ProcurementControllerTests.WarehouseReceipts_TransitionMaterialRequestFulfillmentAndReversal`, `WarehouseLifecycle_BlocksNegativeStockAndReversesSafely`; `VendorsControllerTests` | `admin-vendors.spec.ts`, `admin-procurement-boq-list.spec.ts`, `admin-material-requests.spec.ts`, `admin-warehouse.spec.ts` |
| PROC-02 | RFQ quotations/revisions → explicit whole-package award → downstream contract → manual payable | `RfqsControllerTests`, `RfqProcurementPipelineTests`, `FinancePaymentReferencesTests`; see [RFQ submatrix](rfq-pipeline-scenario-matrix.md) | `rfq-business-pipeline.spec.ts`, `admin-rfqs.spec.ts`; this is one project-wide pipeline, not the entire project |
| FIN-01 | Upstream/downstream contract Draft→Signed→execution; commercial data/owner controls | `ContractsControllerTests.Transition_DraftToSigned_Succeeds`, `Transition_CompanyContract_RequiresTaxIdAndLegalRepresentative`, `Transition_SignedToInProgress_WithoutScan_ReturnsBadRequest`; `ContractServiceTests` | `admin-contracts.spec.ts`, `contract-money-input.spec.ts`, `contract-search-focus.spec.ts`; use normal manager role for signing |
| FIN-02 | Contract milestone planned/actual date → project timeline; approved VO → contract value change | `ContractsControllerTests.Milestone_StatusUpdate_RequiresPersistsAndClearsActualPaymentDate`, `VoWorkflow_CreateSubmitApprove_UpdatesCurrentValue`; project timeline tests | `admin-operational-project-timeline.spec.ts`; this is contractual schedule evidence, not actual cashflow |
| FIN-03 | Invoice Draft→UnderValidation→Ready→Approved→Paid, or supported rejection/cancellation | `FinanceControllerTests.PaymentLifecycle_EnforcesSelfApprovalAndPaidImmutability`, `DuplicateSupplierInvoice_PerVendor_IsRejectedWithoutSecondRow`; RFQ pipeline extensions | `admin-finance-control.spec.ts`, RFQ business journey; no direct receipt/acceptance-to-payable gate exists |
| FIN-04 | Accounting period close → adjustment submit/approve → controlled reversal | `FinanceControllerTests.PeriodClose_UsesVietnamBoundaries_RequiresSequence_AndCannotReopen`, `CorrectionWorkflow_RejectsInvalidTransition_ApprovesAndReversesOnce` | Finance page rendering is not proof of every accounting transition through browser |
| DOC-01 | Business-owned file → project catalog/private content → sync conflict/retry/delete retention | `OperationalProjectDocumentsControllerTests.SuperAdmin_UploadsListsAndDownloadsPdf_FromDriveWithoutHostCopy`, `SaleOutsideProjectScope_AllDocumentEndpointsReturnNotFoundWithoutChanges`, `Retry_ExhaustedDelete_AsInScopeProjectManager_QueuesAgain`; Google Drive adapter/OAuth unit tests | `admin-operational-project-documents.spec.ts`, `admin-google-drive-oauth.spec.ts`; injected adapters/route interception do not prove live Google transport |
| REPORT-01 | Operational records → scoped KPI evidence/project report → localized audited export | `KpiControllerTests`; `ProjectReportsControllerTests.Get_ReturnsExactAggregatesAndExplicitUnavailableStates`, `Export_RequiresExportPermissionAndProducesAuditedXlsxAndPdf`; `ProjectReportCalculationsTests`, KPI background-service tests | `admin-kpi-dashboard.spec.ts`, `admin-project-reports.spec.ts`; joined freshly-created records → actual report readback is distinct from seeded aggregates |
| IAM-01 | Login/token/role assignment → effective permissions → denial/revocation/cache behavior | `AuthControllerTests`; `RbacUserManagementFlowTests.FullFlow_CreateRole_AssignToUser_LoginAndAccessGatedEndpoints`; `UnauthorizedMutationProbeTests`, `MePermissionsControllerTests`, business-role tests | `auth-browser-flow.spec.ts`, `security.spec.ts`, `me-permissions.spec.ts`, `rbac-*.spec.ts`, `admin-rbac*.spec.ts`, users/roles/custom-role pages |
| OPS-01 | Workflow settings, notifications, audit, master data and translations support each action | `WorkflowsControllerTests.FullRoundTrip_AsAdmin_Create_Update_Delete`, `Create_WithUnknownApproverRole_ReturnsBadRequest`; notification/template, audit, master-data and translation controller tests | `admin-workflows.spec.ts`, `admin-audit-log.spec.ts`, `admin-master-data.spec.ts`, `admin-content-translations.spec.ts`; configuring workflow metadata does not certify every service uses it |
| DELETE-01 | Authorized impact preview → typed confirmation/token → transactional dependent handling → cleanup retry | Root controller deletion tests; `HardDeleteOperationsControllerTests`; `HardDeleteStateMachineTests`, `HardDeleteOperationServiceTests`, `BusinessRootHardDeletePlanServiceTests`; `CrmMultiUserConcurrencyTests` | `admin-hard-delete-dialog.spec.ts`; retain SQL/file-failure tests separately from dialog behavior |
| PUBLIC-01 | Published CMS content/routes/localization → public contact enquiry | About/activity/news/project/service/slideshow/logo/settings/category controller tests; `ContactsControllerTests` | `homepage.spec.ts`, `public-routes.spec.ts`, `public-detail-pages.spec.ts`, `deep-link-routes.spec.ts`; public contact does not imply automatic Lead creation unless explicitly implemented |
| HR-01 | Job/department/employment master data → public application → authorized processing/deletion | `JobApplicationsControllerTests.Submit_AsPublic_Then_AdminListsAndDeletes`; job-position, employment-type and recruitment-dropdown controller tests | Public detail/routes cover recruitment rendering; end-to-end CV submission/browser administrative handling needs explicit evidence |

## Highest-priority joined scenarios

Keep distinct business paths as separate tests. Building a single enormous test
that crosses every module would obscure failures and invent dependencies between
otherwise independent workflows. Lower-layer assertions should remain at their
existing level unless a cross-module identity or actor handoff adds new evidence.

| ID | Priority | Missing or incomplete joined proof | Minimum acceptance evidence |
|---|---|---|---|
| JOIN-01 | Critical | CRM acquisition → converted identities → approved quote → signed upstream contract → design project | Public API transitions preserve Customer/OperationalProject/Opportunity/Contract/DesignProject identities; retry does not duplicate conversion/design creation; terminal Won invariants remain enforced |
| JOIN-02 | Critical | Same design project traverses Concept→Basic→Shop→IFC, then site records→handover | Normal Design/Design Lead/PM roles; no intermediate-stage seeding; readiness denial before prerequisites; release flips only included drawings; final handover proves approved acceptance/categories, no open punches, checklist and signatory |
| JOIN-03 | Critical | Tender estimate approval→Won→contract/project BOQ→procurement | Approval and checklist prerequisites through endpoints; source-estimate/project/customer continuity; no incorrect reuse of another customer's opportunity or stale approved revision |
| JOIN-04 | High | Real-browser sales conversion/quotation/signing | Browser form submit/navigation/role switch, persisted resulting identifiers; current `crm-pipeline.spec.ts` is API-only and should not be labelled browser proof |
| JOIN-05 | High | Real-browser PM handover completion/reopen after actual upstream readiness | Complete and reopen dedicated actions, signatory validation, immutability, refreshed readiness/status; current handover smoke is CRUD |
| JOIN-06 | High | Created business evidence→project report/KPI/export | At least one freshly-generated accepted/paid/warehouse record reflected in the delivered read model and excluded after supported reversal, without substituting unavailable ledger metrics |
| JOIN-07 | Critical | Role/team revocation during a cross-module workflow | Removed member cannot use saved links, content URL or cached idempotency response; unaffected project members retain access; no cross-project leakage |
| JOIN-08 | Critical | Joined project deletion/cleanup after financial/design/construction history | Deterministic impact includes all new linked roots/files; rejects stale/blocking plan without any partial removal; independent principal records remain intact |
| JOIN-09 | High | Public enquiry/application→authorized staff review | Correct validation and file ownership; unauthorized list/content denied; confirm only implemented downstream actions rather than assuming auto-CRM conversion |

The implementation agent should select additions from confirmed supported
contracts, execute existing global suites, and document remaining gaps. Source
inventory alone cannot prove that all critical business scenarios ran.

## Shared negative/recovery dimensions

For each joined scenario, cover the risk at its lowest effective layer:

- Wrong role and same-role wrong owner/project, including discipline-only scope.
- Unsupported state skips, rejection reasons, immutable terminal records and
  only the dedicated documented reopen/revise/reverse transitions.
- Missing/duplicate/invalid fields and server-enforced cross-record matching;
  multilingual seeded fields and exact financial/quantity precision.
- Same-key replay, changed-payload conflict and stale row version without lost
  updates, duplicate business events or duplicate downstream roots.
- Related data changing after form load: revoked membership, inactive vendor,
  changed approved BOQ, newly-open punch item or removed required evidence.
- Loading/error/empty/retry, preserved form values, keyboard access, mobile and
  tablet navigation, localization, and reload after terminal outcomes.
- Read-model and audit/notification outcomes; assertions must include unchanged
  state after rejection, not only an HTTP status or visible toast.

## Blocked execution and capability boundaries

| Area | Boundary / required evidence |
|---|---|
| Relational concurrency | InMemory EF does not prove SQL locks, unique constraints or atomic rollback. Concurrent award, warehouse post/reversal, contract creation and cleanup/fault injection require dedicated SQL execution; mark not run/blocked until executed on the final state |
| External transport | Google OAuth/Drive, email delivery and external folder retention require configured credentials and controlled external execution. Adapter mocks and metadata tests cannot replace this |
| Offline field work | Customer docs describe offline capture/synchronization. Do not claim it tested merely because desktop/mobile rendering passes; current implementation and dedicated offline conflict tests must exist |
| Actual finance | Actual cashflow/revenue/expenditure/P&L remain explicitly unavailable in project reports. Paid requests and milestone dates are not bank-ledger postings |
| Procurement matching | No automatic RFQ→MR reservation, split award, currency conversion, receipt/acceptance→payable or three-way invoice matching is implied |
| Legal/permit gates | Government issue/inspection and external signatories are business inputs. Do not invent automatic regulatory approval or new permit→construction blockers from a conceptual workflow diagram |
| Public/HR integration | Public contact and recruitment submission have their own persistence contracts; no automatic Lead, Opportunity or employment onboarding should be inferred |
| Exhaustive coverage | A global suite pass establishes the assertions actually present. It is not a proof of every possible state/field/role combination or every unimplemented requirement |

## Final independent BA and QA review

**Executed validation: 1,885 integration tests, 2,007 unit tests and 215 browser
tests passed; one live-Drive browser test was skipped.** The final RFQ-only
rerun passed all ten partitions after the final provenance and deterministic
event assertions. Whole-solution format verification exited zero. Frontend lint,
app typecheck, changed-E2E lint/typecheck and production build passed.

**BA verdict: Approved for QA for the bounded implemented pipeline contract.**
Actors, lifecycle gates and persisted identities in the new tests align with the
existing supported workflows. No new critical/high business defect was found.
This approval does not accept unimplemented capabilities or imply exhaustive
requirement coverage.

**QA verdict: executed assertions pass; comprehensive project-wide business
completion remains unproved.** The listed joined coverage gaps and external
checks have not all run, so the skill's unconditional project-wide Pass gate is
not met. The review does not represent product-owner acceptance of those risks.

### Expanded coverage review during the current task

The independent reviewer inspected these assertions and read the final execution
logs. Executing agents ran the suites; this reviewer did not edit tests or
production.

| Addition | Inspected coverage and reported result | Limits |
|---|---|---|
| `CrmBusinessPipelineTests.LeadConversion_QuoteDecision_ContractAndOpportunityClosure_PreserveCommercialContext` | Three integration partitions passed: Won, RevisedWon, Lost. Lead conversion/replay, shared project, BOQ quote approval/version totals, customer decision, signed scan prerequisite, InProgress auto-design creation, terminal opportunity and identity checks | Uses Sales Manager for the commercial chain with explicit Sale approval denial; it is not every Sales ownership/portfolio combination |
| `DesignPermitIfcPipelineTests.Concept_ToPermitTracking_AndIfcReceipt_PreservesGatesAndRevisionHistory` | Two integration partitions and 70 adjacent cases reported passed. Signed/executing contract creates design; Concept Final unlocks Basic; required disciplines reviewed; explicit permit submission/correction/issue; Shop review/revisions, IFC packet/recipient prerequisites, release and acknowledgment | Permit files and Basic status are explicitly updated, not automatically synchronized. Separate foundation from the CRM and construction tests |
| `ConstructionHandoverPipelineTests.SiteExecution_ToClientHandover_EnforcesReadinessAndRecoversQualityIssues` | Two integration partitions reported passed. Task/diary, acceptance rejection/revision, punch repair/verification/reopening, required as-built upload/approval, readiness invalidation and PM completion with immutable history | Seeds Design Project foundation; no IFC-to-site same-record continuation. The recovery branch now also reopens the completed handover, rejects immediate recompletion, returns through readiness and completes again with one record and six history entries |
| `RfqProcurementPipelineTests.ApprovedScope_ToAward_AndInvoiceOutcome_PreservesBusinessHandoffs` | Ten current integration partitions; Supplier/Both/Subcontractor, compatible Supply/Subcontract, Paid/RejectedUnderValidation/RejectedReadyForApproval/Cancelled plus three unawarded RFQ cancellation stages, tender-sourced BOQ, both tied bids, assignment denial, quote withdrawal recovery, split delivery/reversal and finance reference handoff. Full integration passed 1,885; final assertion-only focused RFQ rerun passed 10 | Detailed remaining partitions are in the RFQ submatrix |
| `crm-business-pipeline.spec.ts` | New real-browser lead conversion → negotiation → reasoned Lost decision source inspected | Reported passed; Won/revised-Won browser continuation not added |
| `construction-handover-pipeline.spec.ts` | New ordinary PM/Design site-to-handover browser source inspected | Reported passed; completed-handover reopen remains integration evidence |
| `rfq-business-pipeline.spec.ts` | Three Paid/Rejected/Cancelled browser partitions reported passed in isolation | Final combined run passed all three outcomes without retries; an earlier SQL 3980 remains an unexplained intermittent observation |

JOIN-01 now has joined API evidence. JOIN-02 is substantially covered by two
connected-domain pipelines but not by one persisted design/IFC/site journey.
JOIN-04 has a passing actual browser Lost branch. JOIN-05 has a passing completion
browser; completed-handover reopen is now in the new joined integration test as
well as existing unit tests, but not this browser journey. JOIN-03 now has tender
estimate approval/checklist/Won → sourced BOQ → RFQ → signed downstream contract
→ warehouse/payable coverage. It does not create a tender-linked upstream contract
and now explicitly verifies persisted BOQ source estimate/project identifiers;
the final focused rerun passed.

### Remaining coverage and observed risks

- Critical/high joined proof remains partial for tender Won → upstream commercial
  contract, the same persisted IFC → construction/handover journey, fresh
  cross-module report/KPI/export evidence, mid-workflow membership revocation,
  and aggregate deletion/failure recovery. Existing lower-layer suites remain
  evidence; these are coverage gaps, not demonstrated product defects.
- CRM browser covers Lost, with Won/revised-Won covered by integration.
  Completed-handover reopen/recomplete is integration coverage, not browser.
- **Medium, reported manual UI finding:** at 390px the filtered handover list/Total
  shows one record while Ready and HandedOver cards each show four. Their scope
  is inconsistent or insufficiently explained. Root visually observed it; this
  reviewer did not reproduce it. No incorrect persisted handover state is shown.
  Track a separate scoped-count/UI clarification follow-up.
- The initial global run had fixture-container RBAC failures, a missing RFQ
  permission-matrix entry and an arbitrary-contract fixture assumption. Corrected
  fixtures/matrix passed 35 RBAC and four contract checks, then the full browser
  rerun passed. These initial failures are superseded by current evidence.
- One initial RFQ receipt produced SQL error 3980 during a split query. All three
  final combined RFQ outcomes passed without retries; root cause remains unproved.
  Retain the observation as intermittent operational risk, not a proven fix or
  evidence of corruption.
- The sole skip is `admin-survey-media.spec.ts:382`, live Drive connection/upload.
  `GOOGLE_DRIVE_LIVE_E2E` is absent because protected OAuth credentials and
  RootFolderId are unavailable. Live transport, SQL race/fault scenarios and the
  unimplemented boundaries above retain their explicit limitations.

### Final execution evidence

| Check | Result | Evidence |
|---|---|---|
| Full integration | 1,885 passed, zero failed/skipped, 1m17s | `/tmp/nihome-project-integration-final.log`; precedes final test-only RFQ assertions |
| Final RFQ integration | 10 passed, zero failed/skipped, 5s | `/tmp/nihome-project-rfq-final.log`; closes that source difference |
| Full unit | 2,007 passed, zero failed/skipped, 52s | `/tmp/nihome-project-unit-final.log` |
| Full browser | 215 passed, one external skip, zero failed/not-run, 5.9m | `/tmp/nihome-project-e2e-final.log` |
| Backend format | Exit zero | Executing agent reported final whole-solution verification |
| Frontend | Lint, app typecheck, changed-E2E lint/typecheck and build passed | Executing agent results; `/tmp/nihome-project-web-build.log` |

### Reproduction and latest verification

Commands below were supplied by the executing agent and refer to this isolated
validation stack. Run backend commands from repository root; the SDK container
uses `/workspace` mounted to this repository. Substitute only an independently
validated stack/container/database for another environment.

```bash
docker exec nih-166-sdk dotnet test nihomebackend.integration.tests/nihomebackend.integration.tests.csproj --no-restore -p:SkipNihomeWebBuild=true
docker exec nih-166-sdk dotnet test nihomebackend.tests/nihomebackend.tests.csproj --no-restore -p:SkipNihomeWebBuild=true
docker exec nih-166-sdk dotnet test nihomebackend.integration.tests/nihomebackend.integration.tests.csproj --no-restore -p:SkipNihomeWebBuild=true --filter FullyQualifiedName~RfqProcurementPipelineTests
docker exec nih-166-sdk dotnet format Nihome31042025.sln --no-restore --verify-no-changes
```

From `nihomeweb`:

```bash
BASE_URL=http://localhost:5044 E2E_SQL_CONTAINER=nih-166-sql E2E_SQL_DATABASE=Nih166Validation npx playwright test --workers=2
npm run lint
npx tsc --noEmit --project tsconfig.app.json
npm run build
```
