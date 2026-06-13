# AGENTS.md

## Project Context

This is a React + ASP.NET Core 8 project running with Docker Compose.

The assistant should behave like a careful senior software engineer working inside this repository.

---

## Critical Instructions

You MUST follow ALL sections in this file.

Do NOT skip any rule even if it is not explicitly mentioned in the prompt.

---

## Core Rules

* Think before editing.
* Prefer small, safe, surgical changes.
* Do not refactor unrelated code.
* Do not introduce new dependencies unless necessary.
* Follow existing project structure and conventions.
* Preserve existing behavior unless explicitly requested.
* Read nearby code before modifying.
* Do not invent requirements or APIs.

---

## Fast Delivery Rules

* Optimize for speed but keep changes safe.
* Prefer simple working solutions.
* Avoid over-engineering.
* Reuse existing code whenever possible.
* If trade-offs are made, clearly state them.

---

## Git Branching

* Always create a new branch before starting a task unless already on a task branch or instructed otherwise.
* Small documentation or instruction-only changes may stay on the current branch when the user requests a quick update.
* Branch from `main` unless instructed otherwise.
* Do not switch branches if there are uncommitted user changes that could be disrupted; ask first.

---

## Code Quality Rules

* Keep code readable and maintainable.
* Use meaningful naming.
* Keep methods small and focused.
* Avoid duplicate logic.
* Avoid deep nesting.
* Remove unused code.

---

## Clean Code Rules

* Follow SOLID principles where practical.
* Keep business logic out of controllers.
* Use clear and explicit logic.
* Handle edge cases properly.
* Avoid magic values.

---

## Design Pattern Rules

* Use patterns only when necessary.
* Prefer patterns already used in the project.
* Avoid adding unnecessary abstraction layers.

---

## React + ASP.NET Rules

* Keep frontend and backend aligned.
* Do not break API contracts.
* Update frontend if backend changes.
* Handle loading, error, and empty states.
* Update the translation and content keys in the ASP .NET DbSeeder aligned with frontend.
* Check `npm run lint` and fix the issues.
* No hardcode like the category in the React. All need to fetch from the backend API to avoid the hardcode.
* Follow the nihomeweb/CLAUDE.md for strictly developing the web UI application.

---

## Docker Development

* This project runs in Docker. Do not run `dotnet` or database commands directly on the host.
* Use `docker exec nihome31042025-backend <command>` for backend tasks.
* Run migrations inside the container: `docker exec nihome31042025-backend dotnet ef migrations add <Name>`

---

## ASP.NET Core Rules

* Keep controllers thin.
* Put logic in services.
* Use dependency injection.
* Use DTOs instead of entities.
* Use async/await for I/O.

---

## Entity Framework Rules

* Do not change schema without migrations.
* Use EF Core migrations for all changes.
* Avoid N+1 queries.
* Use AsNoTracking for read operations.

---

## EF Migration Rules

### Create Migration

```bash
dotnet ef migrations add <Name>
```

### Apply Migration

```bash
dotnet ef database update
```

### Remove Migration

```bash
dotnet ef migrations remove
```

### Generate Script

```bash
dotnet ef migrations script
```

Always review migration files before applying.

---

## ASP.NET Testing Rules

* When develop the feature, please cover the test cases properly in `nihomebackend.tests`
* Ensure the linter with `dotnet format`
* Ensure the test cases cover the feature, and follow the design pattern

---

## Test Layering — avoid duplication across layers

Each test layer has a clear, non-overlapping responsibility. Do not duplicate
coverage between layers — pick the lowest layer that can prove the behavior.

### Unit tests — `nihomebackend.tests`

* Service-level logic in isolation: validation, branching, JSON shape handling,
  cache invalidation, file-resolution helpers, etc.
* Use InMemory EF and Moq. No HTTP, no docker.
* This is where edge cases and validation matrices live.

### Integration tests — `nihomebackend.integration.tests`

* Boots the real ASP.NET pipeline via `WebApplicationFactory` (controllers,
  middleware, auth, model binding, EF).
* Owns **all** API behavior: CRUD round-trips, auth/role enforcement,
  validation 400s, contract shape, error paths.
* Fast (seconds), runs on every PR, no docker required.

### E2E tests — `nihomeweb/e2e/smoke` (Playwright)

* Scope is intentionally narrow: only what integration tests structurally
  cannot cover.
  * Real-browser rendering: SPA mounts, no JS errors, public routes resolve,
    detail pages render with seeded data.
  * Deployment-only contracts against the live `docker compose` stack:
    CORS preflight, brute-force tolerance, health endpoint, etc.
* **Do not** add API-only specs here (CRUD round-trips, auth checks,
  validation 400s). If the assertion can be made with `HttpClient` against
  `WebApplicationFactory`, it belongs in `nihomebackend.integration.tests`.
* Single Playwright project, single CI job, runs on every PR + push to main.

### Rule of thumb when adding a test

1. Pure logic / a service method? → unit test.
2. HTTP contract, validation, auth, persistence round-trip? → integration test.
3. Does the user need to actually see a page render, or does the deployed
   stack need to wire up correctly? → E2E.

If a behavior is already proven at a lower layer, do not re-assert it at a
higher layer.

## Documentation

* When maturing the features, please update the documentation in `docs/`

### Manual API Test

Example:

```bash
curl -X GET http://localhost:5000/api/resource
```

---

## Quality Check

### Backend

```bash
docker exec nihome31042025-backend dotnet build
docker exec nihome31042025-backend dotnet format --verify-no-changes
docker exec nihome31042025-backend dotnet test nihomebackend.tests/nihomebackend.tests.csproj
dotnet test nihomebackend.integration.tests/nihomebackend.integration.tests.csproj
```

### Frontend

```bash
cd nihomeweb && npm run lint && npm run build
```

### E2E (browser + deployment smoke)

Requires the full stack to be running locally:

```bash
docker compose up -d --build
cd nihomeweb && BASE_URL=http://localhost:5043 npx playwright test
```

### Docker

```bash
docker compose up --build
```

---

## Final Response Format

Every response MUST include:

## Summary

## Files Changed

## Quality Check

## Assumptions / Risks

---

## Response Style

* Be clear and concise
* Focus on practical solutions
* Highlight risks when needed

## Git commit

* When a feature or bug fix is complete, commit the work.
* Write commit messages following the 50/72 rule.
* Only add files related to the commit — do not use `git add -A` blindly.
