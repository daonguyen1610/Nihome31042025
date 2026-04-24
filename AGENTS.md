# AGENTS.md

## Project Context
This is a React + ASP.NET Core 8 project running with Docker Compose.

The assistant should behave like a careful senior software engineer working inside this repository.

---

## Critical Instructions
You MUST follow ALL sections in this file, including:
- Core Rules
- Karpathy-Style Coding Guidelines
- Code Quality Rules
- Clean Code Rules
- Design Pattern Rules
- React + ASP.NET Core Rules
- Entity Framework Rules
- Docker Compose Rules
- Manual Testing Rules
- Final Response Format

Do NOT skip sections even if they are not explicitly mentioned in the prompt.

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
- Optimize for fast, safe delivery.

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

## Fast Delivery Rules
- Prefer the fastest safe solution that satisfies the requirement.
- Do not block delivery with unnecessary refactoring.
- Reuse existing components, services, DTOs, utilities, and patterns.
- Avoid new dependencies unless clearly necessary.
- If a speed trade-off is made, mention it in the final response.

---

## Code Quality Rules
- Prioritize readability, maintainability, and correctness.
- Keep methods small and focused.
- Keep classes focused on one responsibility.
- Use meaningful names for classes, methods, variables, DTOs, and services.
- Avoid duplicated logic; extract helper methods only when reuse or clarity is clear.
- Avoid deeply nested conditionals; prefer early returns when readable.
- Avoid large methods, large controllers, and large service classes.
- Do not add clever or complex code when simple code is enough.
- Do not leave dead code, commented-out code, unused variables, or unused imports.
- Preserve existing public contracts unless explicitly asked to change them.

---

## Clean Code Rules
- Follow SOLID principles pragmatically.
- Apply Single Responsibility Principle especially for controllers, services, repositories, validators, and mappers.
- Keep business logic out of controllers.
- Keep infrastructure logic out of domain/business models.
- Prefer explicit, readable code over excessive abstraction.
- Avoid magic strings and magic numbers; use constants/enums when meaningful.
- Handle nulls and edge cases clearly.
- Use consistent error handling based on the existing project pattern.
- Log meaningful events and errors, but do not log sensitive data.
- Keep comments useful; explain why, not obvious what.

---

## Design Pattern Rules
- Use design patterns only when they solve a real problem in this codebase.
- Do not introduce patterns just to appear architectural.
- Prefer existing patterns already used in the project.
- If adding a pattern, explain why it is needed.

Recommended patterns for ASP.NET Core:
- Dependency Injection for services and infrastructure dependencies.
- Repository pattern only if the project already uses it or data access needs abstraction.
- Unit of Work only if multiple repositories or transactions require coordination.
- Options pattern for strongly typed configuration.
- Factory pattern when object creation has branching or complex setup.
- Strategy pattern when replacing large conditional logic with interchangeable behavior.
- Decorator pattern for cross-cutting behavior such as caching, logging, or validation.
- Mediator/CQRS only if the project already uses it or complexity justifies it.

Avoid:
- Adding Repository/Unit of Work on top of EF Core without a clear reason.
- Adding CQRS/MediatR for simple CRUD unless already used.
- Creating generic abstractions before there are multiple concrete use cases.
- Splitting code into too many layers for small features.

---

## Architecture Rules
- Keep dependencies flowing in the correct direction.
- API layer should depend on application/service layer, not the opposite.
- Domain/business logic should not depend on controllers.
- Infrastructure details should be isolated from business logic where practical.
- Keep DTOs separate from EF Core entities.
- Keep validation, mapping, persistence, and business logic separated when the feature is complex enough.
- Prefer simple vertical feature organization if the project already follows it.
- Prefer layered organization if the project already follows it.
- Do not reorganize architecture unless explicitly requested.

---

## React + ASP.NET Core Fullstack Rules
- Keep frontend and backend contracts aligned.
- If backend API changes, update the related React API client, types/models, and UI usage.
- If frontend expects a field, verify the backend response provides it.
- Use consistent naming between DTOs, API responses, and frontend models.
- Do not silently change API response shapes.
- Keep validation consistent between frontend and backend where practical.
- Handle loading, empty, error, and success states in React UI.
- Avoid duplicating business logic across frontend and backend unless necessary.

---

## React Rules
- Follow the existing React project structure.
- Reuse existing components, hooks, API clients, utilities, and styling patterns.
- Do not introduce new UI libraries unless requested.
- Keep components small and readable.
- Avoid putting too much business logic directly inside components.
- Use existing state management patterns.
- Handle API errors clearly.
- Handle null, undefined, empty arrays, and failed requests safely.
- Do not break existing routes or page behavior.
- Keep UI changes consistent with the existing design system.

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