# Module 2 Detail Design Schedule

## Business Objective

The detail-design schedule provides a project-scoped plan for the three design
phases without changing or reusing Module 4 construction tasks. It records
planned and actual dates, ownership, department, progress, milestones, and
Finish-to-Start predecessor relationships, then derives traceable weighted
progress for each phase and the complete design schedule.

## Actors and Permissions

- A caller with `operations.projects.view` may read only an Operational Project
  visible through `IProjectAccessService.CanViewOperationalProjectAsync`.
- A caller with `operations.projects.manage` may initialize or mutate a schedule
  only when `IProjectAccessService.CanManageTeamAsync` allows that project.
- Inaccessible projects, phases, and tasks return `404` to avoid disclosing
  project existence. Missing authentication returns `401`; missing global
  permission returns `403`.

## Lifecycle and Validation

Initialization is explicit and idempotent. It requires a Design Project with a
start date and deadline spanning at least three calendar days and exactly the
canonical `Concept`, `BasicDesign`, and `ShopDrawing` phases. Phase weights must
total 100. The service partitions the inclusive Design Project date interval
deterministically into three contiguous, non-overlapping ranges. A partial or
non-canonical existing baseline is rejected instead of being treated as
initialized; the migration does not invent schedules for existing projects.

Persisted statuses are `NotStarted`, `InProgress`, `Completed`, `OnHold`, and
`WaitingForDepartment`. Allowed transitions are:

| From | Allowed destinations |
|---|---|
| `NotStarted` | `InProgress`, `OnHold`, `WaitingForDepartment` |
| `InProgress` | `Completed`, `OnHold`, `WaitingForDepartment` |
| `OnHold` | `InProgress`, `WaitingForDepartment` |
| `WaitingForDepartment` | `InProgress`, `OnHold` |
| `Completed` | None |

Repeating the current status is allowed. `NotStarted` requires zero progress and
no actual dates. `InProgress` requires an actual start. `Completed` requires
both actual dates and 100 percent progress. An actual end is forbidden for all
other statuses. Planned and actual end dates cannot precede their corresponding
start dates. Weights range from 1 through 100 and progress from 0 through 100.
A milestone has `IsMilestone = true` and equal planned start and end dates.
Overdue is derived at read time when planned end is before the current UTC date
and status is not `Completed`; it is never persisted.

Task departments must be active options in the `project-department` master-data
category. The seeded options are Design, Architecture, Structural, MEP, and
Interior, with Vietnamese, English, Chinese, and Japanese labels. An assignee
must be an active user represented by a non-ended `OperationalProjectMember` in
the same project. Every predecessor must be a task in the same project;
self-dependencies and cycles are rejected before persistence.

## Progress Policy

The policy identifier is `design-schedule-weighted-v1`. A phase baseline is
ready only when it contains at least one task and task weights total exactly
100. Its progress is:

$$
P_{phase} = \frac{\sum_i w_i p_i}{100}
$$

The project baseline is ready only when exactly three canonical phases exist,
phase weights total 100, and every phase baseline is ready. Project progress is:

$$
P_{project} = \frac{\sum_j W_j P_j}{100}
$$

When a baseline is not ready, its rolled-up progress is `null`. Responses expose
phase IDs, task IDs, weights, source progress values, and weighted values so the
calculation is auditable. Filters affect the paged task list only; roll-up uses
the complete schedule and therefore remains stable while browsing filtered
results.

## API Contract

Both `/api/operational-projects/{projectId}/design-schedule` and its `/api/v1`
alias expose the same controller.

| Method | Relative route | Permission | Purpose |
|---|---|---|---|
| `GET` | `/` | `operations.projects.view` | Read phases, roll-up sources, and paged tasks |
| `POST` | `/initialize` | `operations.projects.manage` | Create the canonical phase baseline |
| `PUT` | `/phases/{phaseId}` | `operations.projects.manage` | Update phase dates, status, progress, and weight |
| `POST` | `/phases/{phaseId}/tasks` | `operations.projects.manage` | Create a task or milestone in a phase |
| `PUT` | `/tasks/{taskId}` | `operations.projects.manage` | Update a task and replace predecessor links |

Mutations require an `Idempotency-Key` containing 1 through 120 characters.
Missing, blank, or oversized keys return `400`. Reusing a key with the same
request replays the stored response; reusing it with a different request returns
`409`.
Updates accept row version through the existing request/`If-Match` convention
and emit an ETag. A stale write returns `409` and may be retried with the same
idempotency key after obtaining the current row version. Successful mutations
write both the standard audit event and a scalar schedule-history snapshot.

The task query supports `phase`, `assigneeMemberId`, `departmentCode`, `status`,
`plannedFrom`, `plannedTo`, `overdueOnly`, `page`, and `pageSize`. Date filtering
uses inclusive interval overlap: a task matches when its planned end is on or
after `plannedFrom` and its planned start is on or before `plannedTo`.

## Compatibility and Deletion

The schedule uses dedicated `design_schedule_*` tables and does not alter,
backfill, or reinterpret `construction_tasks` or its API. Deleting a Design
Project reports schedule phases, tasks, dependencies, and history in the
deletion-impact plan. Aggregate deletion removes dependency edges explicitly;
the remaining schedule-owned rows are removed with their Design Project.

Migration `AddDetailDesignSchedule` is additive: it creates four new tables,
their foreign keys, indexes, row versions, and check constraints. It contains no
data update or migration-time initialization and must be applied through the
normal deployment gate only after backup and migration-script review.