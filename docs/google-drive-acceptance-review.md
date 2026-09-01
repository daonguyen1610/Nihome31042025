# Google Drive Customer Acceptance Review

Review date: 1 September 2026  
Branch reviewed: `feat/nih-456-google-drive-uploads`  
Baseline commit: `f9a4697`

## Verdict

**Blocked — the current implementation does not satisfy the complete customer scope.**

The implementation provides a substantial Google Drive-backed Operational Project document catalog and compatibility sidecars for selected source modules. It does not yet provide the requested complete project hierarchy, Drive ACL synchronization, universal upload adoption, or complete in-app format support.

Passing tests in this review prove only behavior that exists. They must not be interpreted as evidence for modules or business rules that have not been implemented.

## Customer Contract

The requested outcome is:

1. Google Drive is the authoritative content store for every project-related upload.
2. Creating an Operational Project automatically provisions its complete approved folder tree.
3. The physical Drive hierarchy represents the approved Customer, Project, Contract, and document-category relationships.
4. NICON role and project scope are enforced by the API and synchronized to Drive folder permissions.
5. Authorized users can preview PDF, converted DWG, Word, Excel, and images inside NICON without downloading a personal copy.
6. Upload, read, download, delete, retry, and reconciliation are safe under invalid input, cross-project access, network failure, permission loss, and duplicate requests.
7. Existing data remains compatible until an approved and verified backfill is available.

## Acceptance Matrix

| ID | Acceptance criterion | Status | Evidence | Required follow-up |
|---|---|---|---|---|
| AC-01 | Create the complete approved Drive tree when an Operational Project is created | **Fail** | `OperationalProjectService.CreateAsync` only commits SQL. `ProjectDriveFolderService.EnsureAsync` creates one category path lazily. Local data showed six of eight projects with zero folder bindings; the other two had only categories already used. | Add durable project-folder provisioning with retry/status and tests for all paths. Define whether project creation succeeds when Drive is temporarily unavailable. |
| AC-02 | Use the approved top-level and nested category names | **Unknown** | Configured category mapping exists for Survey, CRM/pre-design, three design stages, legal, construction/acceptance, procurement, and finance/contracts, but no authoritative customer approval of these names is recorded. | Approve exact names and then provision the complete tree, not only the first category requested. |
| AC-03 | Represent Customer → Project → Contract → Category in Drive | **Fail** | Current physical path is configured root → project → category. Customer and Contract are SQL metadata only. | Approve exact naming, movement, and rename rules for customer and contract folders before implementation. |
| AC-04 | Store Lead uploads in Drive | **Fail** | No Lead-owned document upload is staged in the project catalog. | Define Lead-to-project behavior before conversion and add storage integration. |
| AC-05 | Store Opportunity uploads in Drive | **Fail** | No direct Opportunity document upload exists. Opportunity reassignment only affects supported related records. | Define Opportunity document slots/categories and migration. |
| AC-06 | Store Quote uploads in Drive | **Partial** | Managed Quote documents stage a project sidecar only after an Operational Project can be resolved. | Define behavior before project linkage and backfill existing records. |
| AC-07 | Store Contract, attachment, and appendix uploads in Drive | **Partial** | Managed files stage sidecars when the Contract resolves to an Operational Project. No physical Contract folder exists. | Add approved contract hierarchy and pre-link behavior; backfill existing records. |
| AC-08 | Store direct Operational Project uploads in Drive | **Pass — implemented subset** | Fake-adapter HTTP tests cover upload/download/delete, idempotent replay, and authorization revalidation before replay. The review operator also recorded a configured-Drive round-trip, but no retained artifact makes that run independent release evidence. | Satisfy AC-22 and AC-23 for release evidence. |
| AC-09 | Store Concept, Basic, Shop Drawing, IFC, and related Design uploads in Drive | **Partial** | Basic Design and Shop Drawing managed files stage sidecars. Concept upload and IFC-specific staging are absent. | Implement Concept and IFC sources, then add source lifecycle and HTTP tests. |
| AC-10 | Store Survey uploads in Drive | **Partial** | Linked Survey media uses the project `Survey` category. Unlinked Survey media retains a separate legacy root workflow. | Confirm whether unlinked Survey is an accepted exception or must be migrated into the universal catalog. |
| AC-11 | Store Permit and legal uploads in Drive | **Partial** | Managed submitted and issued Permit files stage sidecars when linked to an Operational Project. | Backfill existing managed files and define pre-link behavior. |
| AC-12 | Store Construction diary, task, punch-list, and acceptance uploads in Drive | **Partial** | Managed Acceptance files stage sidecars. Site Diary, Construction Task, and Punch List attachments are not implemented. | Implement missing construction upload models and Drive staging. |
| AC-13 | Store As-built and Handover uploads in Drive | **Partial** | Managed As-built and Handover files stage sidecars when linked. | Add approved backfill and prove all legacy paths. |
| AC-14 | Store Procurement/vendor/tender/material uploads in Drive | **Fail** | Vendor, capability, and Tender checklist upload paths remain local and do not stage project sidecars. | Define project association and category placement for each source. |
| AC-15 | Synchronize NICON RBAC to Google Drive folder ACLs | **Fail — critical** | `IGoogleDriveAdapter` has no permission operations. No approved user/group mapping exists. Developer documentation explicitly disables permission synchronization. | IT/Product must approve Google Workspace group or user mappings and ownership rules. Implement least-privilege ACL reconciliation and revocation tests. |
| AC-16 | Enforce every project read/write/download/delete at the API | **Fail** | Centralized Operational Project document endpoints enforce authentication, permissions, project scope, and project/document binding. Basic Design, Shop Drawing, Permit, and As-built source APIs use module permissions without equivalent row-level Operational Project scope. | Add project-scope enforcement and integration tests to every source API. Direct Drive access remains outside NICON controls until AC-15 is implemented. |
| AC-17 | Prevent cross-project access | **Fail** | Centralized catalog isolation passes integration tests, but that narrow result does not prove the customer-wide criterion while source APIs lack equivalent project-scope enforcement. | Prove list/get/upload/download/preview/delete/retry isolation for every integrated source. |
| AC-18 | In-app PDF, Word, Excel, image, and converted-DWG viewer | **Fail** | Reusable preview supports PDF, DOCX, images, and text on selected legacy screens. The project catalog only downloads or opens Drive externally. XLS/XLSX rendering and DWG conversion are absent. | Approve conversion/provider architecture, add protected preview endpoints, and integrate the viewer into the project catalog. |
| AC-19 | Invalid file, network loss, or Drive permission loss leaves no orphan | **Fail** | Validation, bounded worker retry, claims, leases, and compensating trash after metadata-save failure exist. The absolute criterion is not met because cleanup failure has no durable orphan ledger and legacy two-step uploads can be abandoned. | Add durable cleanup/outbox handling and integration tests for provider failures at each boundary. |
| AC-20 | Retry and idempotency | **Partial** | Sidecars have stable replica keys, generations, bounded retry, claim fencing, and reconciliation idempotency. Direct project upload now requires a stable upload-intent key; sequential and concurrent replay tests prove one catalog row and one Drive object, while changed payload reuse returns `409`. Current project scope is evaluated before replay or conflict disclosure. Unsupported source modules and unapproved backfill remain outside this proof. | Apply the same verified contract as each missing source module is integrated. |
| AC-21 | Preserve existing data until migration/backfill is approved | **Partial** | Existing source storage remains authoritative while supported records use compatibility sidecars. Survey has a scoped repair migration, but no approved module-wide backfill or rollback evidence exists. | Do not remove local source files or claim universal Drive completion until module-specific backfills are reviewed and verified. |
| AC-22 | Integration test the storage abstraction | **Partial** | Fake-adapter integration coverage proves API contracts; adapter behavior has unit coverage. No automated test combines the real configured adapter with the full application pipeline. | Add an opt-in, non-silent live integration gate that uses Admin-managed encrypted settings. |
| AC-23 | Manually verify with a configured Drive account | **Unknown** | The review operator recorded a successful direct-project PDF upload, hash-matched protected download, HTTP 204 delete, and worker cleanup on 1 September 2026. No immutable sanitized artifact makes that transient execution independently reproducible. | Add a non-silent release gate and retain sanitized run metadata; repeat after each missing module is integrated. |
| AC-24 | Validate source metadata identities and project ownership | **Fail** | Direct project upload validates required source ID shape but does not prove every caller-supplied source record, customer, or contract exists and belongs to the selected project. | Resolve source metadata server-side and reject nonexistent or mismatched relationships. |
| AC-25 | Preserve hierarchy through rename, move, reassignment, contract multiplicity, and deletion | **Unknown** | Supported source reassignment queues replica movement, but the customer has not approved physical Customer/Contract identity or rename and deletion rules. | Define lifecycle rules and prove stable folder identity, movement, conflicts, and cleanup. |
| AC-26 | Reconcile Drive ACL grants, revocations, inheritance, and external drift | **Unknown** | ACL synchronization does not exist and the target Google identity/inheritance contract is undefined. | Approve the identity contract, then add least-privilege reconciliation and revocation tests. |
| AC-27 | Expose provisioning and deletion status with durable recovery | **Fail** | Category folder creation is request-driven and no project-level full-tree provisioning state or recovery workflow exists. Cleanup after some provider failures is best effort. | Add durable desired state, observable status, retry ownership, and an operator recovery path. |

## Implemented Source Coverage

| Source | Drive behavior represented in source and tests |
|---|---|
| Operational Project manual document | Direct Drive upload/download/trash |
| Quote document | Compatibility sidecar when project-linked |
| Contract attachment and appendix | Compatibility sidecar when project-linked |
| Basic Design document | Compatibility sidecar when project-linked |
| Shop Drawing | Compatibility sidecar when project-linked |
| Permit submitted/issued file | Compatibility sidecar when project-linked |
| Survey media | Project sidecar when linked; legacy worker when unlinked |
| Acceptance document | Compatibility sidecar when project-linked |
| As-built document | Compatibility sidecar when project-linked |
| Handover document | Compatibility sidecar when project-linked |

The table is intentionally exhaustive for the current Drive implementation. It
does not mean every row has independent live-provider evidence. A source not
listed here must not be represented as Drive-integrated.

## Test Evidence

### Automated

The review operator recorded these executions against the reviewed working tree.
No immutable test-result artifact was retained, so the independent BA and QA
reviews treat them as supporting history rather than independent release evidence:

- Pre-remediation complete backend baseline in the application Docker image: **1,561 unit and 1,282 integration tests passed, 0 failed**.
- Focused fingerprint and Drive worker unit suite: **36 passed, 0 failed**.
- Focused Operational Project document HTTP suite: **21 passed, 0 failed**.
- Frontend lint and production build: **passed**.
- Backend build and `dotnet format --verify-no-changes`: **passed with 0 warnings and 0 errors**.
- Pre-retry-scenario headed Operational Project document Playwright baseline: **1 passed, 0 failed**.
- Failed-upload stable-key browser scenario: **implemented but not run; the current attempt failed at login with `ECONNREFUSED ::1:5043` after the Docker stack became unavailable**.
- Current complete unit rerun in the plain .NET SDK image: **1,556 passed; 6 Survey PDF tests blocked because that image lacks the required Noto fonts**.
- Current complete integration rerun: **not run because Docker Desktop's layer store returned input/output errors for both new and existing containers after the focused suites passed**.

The current focused suites contain the authorization-replay and worker failure
changes. A clean complete rerun in the application image remains required after
the Docker layer-store fault is repaired.

### Manual configured-Drive check

The following results are operator-reported and were not independently rerun.
No credential, access token, account email, Drive URL, or immutable provider
artifact is retained in this report:

- Authentication through the real API: passed.
- Admin-managed OAuth/Drive status: `Connected`.
- Direct project PDF upload: returned `Synced` and a Drive identity.
- Protected API download: SHA/content comparison matched the uploaded file.
- API delete: HTTP 204.
- Worker cleanup: deleted document no longer appeared in the active project catalog after 20 seconds.

### Not proved

- Full auto-folder provisioning at project creation.
- Customer and Contract physical folder hierarchy.
- Google Drive ACL synchronization or revocation.
- Lead, Opportunity, Concept, IFC, Site Diary, Construction Task, Punch List, or Procurement uploads to Drive.
- Historical module-wide backfill.
- XLS/XLSX preview or DWG conversion.
- Full application-pipeline live test using Admin-managed encrypted credentials.
- Source-record existence and ownership validation for caller-supplied metadata.
- Concurrent first-folder creation.
- Provider timeout after remote commit, SQL failure after upload, and subsequent cleanup failure.
- Exact file-size boundaries, multipart overflow, spoofed signatures, and the approved malware policy.
- Drive ACL drift.

## Independent Review Findings

The required clean-context reviews both returned **Blocked**. No product or
business owner has accepted the remaining requirement risks.

### Senior Business Analyst

- Critical Drive ACL synchronization and customer-wide project authorization are absent.
- Automatic hierarchy, universal source adoption, protected preview, source-wide idempotency, and durable orphan recovery are incomplete.
- Folder names, physical hierarchy lifecycle, disconnected-record behavior, Google identity mapping, viewer conversion, and backfill require owner decisions.
- The approved QA gate was not met; the customer contract is not ready to be used as a completed acceptance baseline.

### Functional QA

- Basic Design, Shop Drawing, Permit, and As-built routes require a project-scope authorization matrix and remediation.
- The project worker validates a writable Drive root before claiming due work; tests prove precheck failures preserve retry state and post-claim provider failures consume one attempt with backoff.
- Direct upload has required sequential/concurrent idempotency coverage and revalidates current project scope before replay; failed compensation still has no durable cleanup ledger.
- The existing live adapter test bypasses the Admin-configured application pipeline and returns without provider calls when its opt-in flag is absent.
- The reported full backend, frontend, Playwright, and manual Drive results support the implemented subset but are not independent release evidence without retained artifacts.

## Required Decisions Before Implementation

1. **Drive ACL identity model:** Google Workspace groups versus individual accounts; mapping from NICON roles/users to Google identities; revocation timing; Shared Drive ownership.
2. **Physical hierarchy:** whether Customer and Contract are real folder levels, how renames/moves behave, and whether one Contract can reference multiple projects.
3. **Disconnected records:** placement of Lead, Opportunity, Quote, Contract, and Survey files before an Operational Project exists.
4. **Project creation failure policy:** synchronous failure, durable provisioning queue, or project-created-with-warning when Drive is unavailable.
5. **Viewer architecture:** Google preview, Microsoft/third-party conversion, or server-generated derivatives for XLS/XLSX and DWG; data residency and licensing approval.
6. **Backfill:** source-by-source inventory, dry-run report, duplicate policy, checksum verification, rollback, and maintenance-window requirements.
7. **Capacity wording:** Google Drive is not literally unlimited; quotas and Shared Drive limits are controlled by the organization’s Google Workspace plan.

## Recommended Delivery Order

1. Approve hierarchy, disconnected-record, and ACL identity contracts.
2. Add durable full-tree provisioning with observable status and retry.
3. Extend upload idempotency to each missing source and add durable orphan cleanup.
4. Integrate missing source modules one at a time with source lifecycle tests.
5. Build and verify migration/backfill per source; keep compatibility storage until sign-off.
6. Implement ACL reconciliation and revocation after group mappings are approved.
7. Complete protected in-app preview/conversion.
8. Add a non-silent configured-Drive release gate and execute the full manual acceptance matrix.
