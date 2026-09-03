# Module 2 — Operational Project Team and Design RACI Contract

## Business Objective

Establish one authoritative Operational Project team shared by Modules 1–8, then use that team to control Module 2 design access, responsibility, assignments, and audit history without breaking legacy Design Project data.

## Actors and RACI

| Project role | Team administration | Design delivery | Design approval | Typical RACI |
|---|---|---|---|---|
| Project Manager | Manage members, roles, reporting lines, and assignments when global project-manage permission is also granted | Coordinate | Accountable for project governance | A |
| Design Lead | Manage design-team members and assignments when global project-manage permission is also granted | Lead all three design stages | Accountable for Concept, Basic Design, Shop Drawing, and IFC gates | A/R |
| Architect | No | Architecture work | No | R |
| Structural Engineer | No | Structural work | No | R |
| MEP Engineer | No | MEP work | No | R |
| Interior Designer | No | Interior work | No | R |
| Legal Officer | No | Legal/permit consultation | No | C |
| Site Engineer | No | Construction consultation and IFC receipt | No | C/I |
| Quantity Surveyor | No | Cost consultation | No | C |
| Observer | No | Read-only project visibility | No | I |

Global RBAC remains mandatory. Project membership narrows global module permissions; it never grants a global permission the user does not already hold. `SUPER_ADMIN` and users with `operations.projects.view.all` retain an explicit administrative bypass.

## Membership Rules

- A user has at most one active membership per Operational Project.
- One membership may hold multiple active project roles and scoped roles.
- Scope is `Project`, `Module`, or `Discipline`; module and discipline scopes require a scope value.
- Module scope values use the backend project-module catalog; discipline values use active `design_discipline` master data. The API canonicalizes case and rejects unknown values, and the UI renders backend-supplied selectors.
- A member may report to another active member in the same project, but never to themselves.
- Member identity is immutable; replacing the person requires ending the old membership and creating another.
- Ending a membership is rejected while the member owns or manages active assignments or has active direct reports. Once dependencies are reassigned, ending the membership ends all active roles and prevents new assignments.
- Project Manager and Design Lead may manage the team only with global `operations.projects.manage`; global administrators may always manage it.
- Legacy Operational Project PM and Design Project PM/Design Lead fields remain available during transition and are dual-written to inferred team memberships.

## Assignment Rules

- An assignment belongs to one Operational Project and one active assignee membership.
- The optional manager must be another active member of the same project.
- The assignment manager and assignee must be different members.
- `WorkKey` is stable and unique per business work item. Multiple assignees are represented by separate rows.
- The unique KPI identity is `(OperationalProjectId, WorkKey, AssigneeMemberId)`; joins to role rows must not multiply this identity. `WorkKey` and `AssigneeMemberId` are immutable after creation, and corrections create a new assignment.
- Parallel work is represented by different `WorkKey` values sharing an optional `ParallelGroup`.
- Assignment status is `Planned`, `InProgress`, `Completed`, or `Cancelled`.
- Completed and cancelled assignments are terminal; corrections create a new assignment identity rather than rewriting history.

## API and Security Contract

- Team routes are nested under `/api/operational-projects/{projectId}/team`.
- Every team read/write resolves the authenticated user, global permission, and Operational Project membership at the API boundary.
- Every Module 2 list/detail/write/download/action is filtered or checked against Operational Project membership.
- Active team members can discover and read their Operational Project portfolio, detail, and timeline; membership alone does not authorize project update or deletion.
- Project and Design Module scopes may authorize project-wide Module 2 actions. Discipline scope filters lists, aggregates, detail, downloads, and revision diffs to matching canonical discipline codes; it never authorizes project-wide Concept or IFC gates.
- IFC release list, detail, recipient, and release actions require Project or Design Module scope; discipline membership alone returns concealed `404` responses.
- Discipline-scoped Basic Design readiness represents only the authorized disciplines and does not expose the project-wide unlock state.
- Inaccessible project resources return `404` to avoid disclosing cross-project data; missing global permission remains `403`.
- Create and update mutations support `Idempotency-Key`; successful retries replay the original response, concurrent duplicates return `409`, and failed reservations may be retried.
- Mutable membership and assignment records use row-version concurrency tokens.

## Design Lifecycle Contract

- Generic Design Project updates cannot change `CurrentStage`.
- Hard deletion is permitted only while the Design Project remains at Concept; later-stage controlled records must be preserved.
- Concept finalization is the only transition from Concept to Basic Design.
- Basic Design readiness approval is the only transition from Basic Design to Shop Drawing.
- IFC release remains the only writer that marks approved Shop Drawings as released.
- Existing stage data remains readable; migration does not rewrite historical lifecycle states.

## Compatibility and Backfill

- Existing Operational Project PMs become inferred active `ProjectManager` memberships.
- Existing Design Project Design Leads become inferred active `DesignLead` memberships.
- Existing Design Project PMs and distinct design-record owners become inferred members with traceable source metadata.
- Backfill is deterministic and idempotent; duplicate source users collapse to one membership while preserving multiple role rows.
- Legacy PM/Design Lead columns remain during the compatibility period. Their removal requires a separate approved migration.
- Runtime dual-write changes and their source references are recorded in project-team history in the same EF unit of work as the legacy project write. A no-op retry does not add history.
- An Operational Project with any team member, assignment, or responsibility-history row cannot be hard-deleted; lifecycle closure uses status cancellation so history remains immutable. Restrictive parent foreign keys enforce this rule even if a concurrent write occurs after the service pre-check.
- If a concurrent dependency wins the race after the delete pre-check, the API returns a `409` concurrency conflict and preserves both project and child records.

## Requirement Traceability

| Requirement | Implementation evidence | Verification evidence |
|---|---|---|
| M2-TEAM-01 One shared project team with multi-role membership | Operational Project member and role entities; nested team API | `ProjectTeamServiceTests` multi-role, duplicate, reporting, and lifecycle cases |
| M2-TEAM-02 Global RBAC plus scoped project authorization | `ProjectAccessService`; controller permission attributes | `ProjectAccessServiceTests`; `OperationalProjectTeamControllerTests` permission and cross-project cases |
| M2-TEAM-03 Discipline isolation | Query-level list filtering plus resource-level Basic Design, Shop Drawing, and revision access checks | Scoped list service tests; `ProjectAccessServiceTests`; `BasicDesignDocsControllerTests` list/detail isolation case |
| M2-TEAM-04 Parallel, KPI-safe assignments | Stable WorkKey, ParallelGroup, and unique project/work/member index | `ProjectTeamServiceTests` parallel and KPI identity cases |
| M2-TEAM-05 Immutable responsibility history | Transactional snapshots, restrictive parent foreign keys, and legacy synchronization history | History tests; model delete-behavior tests; team-history-only HTTP deletion rejection |
| M2-TEAM-06 Backward-compatible migration | Legacy fields retained; deterministic migration backfill and runtime dual-write | Legacy synchronization tests and migration review |
| M2-TEAM-07 Retry and concurrency safety | Idempotency reservation/replay plus row-version writes | Integration replay/conflict/concurrency tests |
| M2-TEAM-08 Controlled lifecycle preservation | Concept-only Design Project hard deletion | Unit and integration rejection tests verify unchanged downstream records |
| M2-TEAM-09 Shared-team project visibility | Active-member Operational Project list/detail/timeline reads with separate manage checks | `OperationalProjectServiceTests` read-only active-member case |

## Acceptance and Verification

- Happy path: create member with multiple roles, reporting manager, parallel assignments, update, end, and inspect history.
- Invalid data: duplicate active membership, invalid scope, self/cross-project manager, inactive user/member, invalid dates/status.
- Security: unauthenticated, missing global permission, member, Design Lead, Project Manager, administrative bypass, and cross-project access.
- Reliability: idempotent replay, key/payload conflict, concurrent duplicate, stale row version, and unchanged state after rejection.
- Compatibility: legacy projects remain readable and backfilled PM/Design Lead users receive deterministic inferred memberships.
- KPI: assignment queries return one row per `(project, workKey, member)` independent of role count.
