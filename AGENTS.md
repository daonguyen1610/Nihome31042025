# AGENTS.md

## Project Context
This is an ASP.NET Core 8 project running with Docker Compose.

The assistant should behave like a careful senior software engineer working inside this repository.

---

## Instruction Priority
Always follow this `AGENTS.md` file over general assumptions.

If the task conflicts with these instructions, explain the conflict before making changes.

---

## Core Rules
- Think before editing.
- Prefer small, safe, surgical changes.
- Do not refactor unrelated code.
- Do not reformat unrelated files.
- Do not introduce new packages unless necessary.
- Follow existing project structure and naming conventions.
- Preserve existing API behavior unless explicitly requested.
- Read nearby code before modifying.
- Do not invent files, APIs, classes, services, or requirements.
- State assumptions clearly when something is unclear.

---

## Karpathy-Style Coding Guidelines

### Think Before Coding
- Understand the task before editing files.
- Identify the smallest safe change.
- Ask for clarification if the request is ambiguous or risky.
- If multiple solutions are possible, choose the simplest maintainable one.
- Do not silently make broad architectural decisions.

### Simplicity First
- Write the minimum code needed to solve the task.
- Avoid over-engineering.
- Do not add abstractions unless they are clearly needed.
- Do not add configuration, extension points, or helper layers unless requested.
- Prefer readable code over clever code.

### Surgical Changes
- Touch only files required for the task.
- Do not refactor unrelated code.
- Do not rename unrelated symbols.
- Do not change public contracts unless explicitly requested.
- Match the existing style of nearby code.
- Mention unrelated issues separately instead of fixing them automatically.

### Goal-Driven Execution
- For bug fixes: identify the root cause first, then fix it.
- For features: follow existing patterns in the project.
- For refactors: preserve behavior.
- For performance changes: explain the expected impact.
- For database changes: use EF Core migrations.

---

## ASP.NET Core Rules
- Use .NET 8 conventions.
- Keep controllers thin.
- Put business logic in services.
- Use dependency injection properly.
- Prefer async/await for I/O operations.
- Use DTOs instead of exposing EF entities directly.
- Validate request DTOs where appropriate.
- Return proper HTTP status codes.
- Keep configuration in `appsettings.json`, environment variables, or Docker Compose.
- Do not hardcode secrets, connection strings, passwords, tokens, or API keys.
- Do not expose internal exceptions directly to API clients.
- Follow the existing response/error format if one already exists.

---

## Entity Framework / Database Rules
- Do not modify database schema unless requested.
- If schema change is required, create or update EF Core migrations.
- Keep DbContext usage inside service/repository layer.
- Avoid N+1 queries.
- Use `AsNoTracking()` for read-only queries when appropriate.
- Do not expose EF entities directly from API responses.
- Prefer DTOs for input and output models.
- Do not manually edit the database outside migrations.

---

## Entity Framework Migrations Rules

### General Principles
- Always use EF Core migrations for schema changes.
- Never make manual database schema changes without migration files.
- Keep migrations small and focused.
- One migration should represent one logical schema change.
- Do not create unnecessary migrations.
- Do not commit broken or experimental migrations.

### Before Creating a Migration
Before running migration commands:
- Ensure the project builds.
- Ensure model changes are intentional.
- Review entity changes carefully.
- Check whether the change affects existing data.
- Check whether the change requires a data migration.

Recommended command:

```bash
dotnet build