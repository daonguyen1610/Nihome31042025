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

* Prefer manual testing for fast delivery.
* Add automated tests only when necessary.

### Manual API Test

Example:

```bash
curl -X GET http://localhost:5000/api/resource
```

---

## Quality Check

### Backend

```bash
dotnet build
```

### Frontend

```bash
npm run build
```

### Docker

```bash
docker compose up --build
```

---

## Manual Testing Rules

Always provide:

* Preconditions
* Steps
* Expected result
* Edge cases

---

## Final Response Format

Every response MUST include:

## Summary

## Files Changed

## Quality Check

## Manual Test Checklist

## Assumptions / Risks

---

## Response Style

* Be clear and concise
* Focus on practical solutions
* Highlight risks when needed
