# NICON Platform -- Application Developer Guide

Version 1.1

Last Updated: 24 October 2026

---

## Table of Contents

1. [Overview](#1-overview)
2. [System Requirements](#2-system-requirements)
3. [Architecture](#3-architecture)
4. [Getting Started](#4-getting-started)
5. [Configuration](#5-configuration)
6. [Database](#6-database)
7. [Backend Development](#7-backend-development)
8. [Frontend Development](#8-frontend-development)
9. [Testing](#9-testing)
10. [Deployment](#10-deployment)
11. [Troubleshooting](#11-troubleshooting)
12. [Kế hoạch hoàn thiện theo yêu cầu NICON](#12-kế-hoạch-hoàn-thiện-theo-yêu-cầu-nicon)

---

## 1. Overview

Nihome is a full-stack design-and-build operations platform. In addition to public content and recruitment, the implemented application includes authentication, dynamic RBAC, CRM, quotations/tenders/contracts, three-phase design control, permitting, construction execution, acceptance, as-built records, punch lists, and project handover. The backend uses ASP.NET Core 8 and Entity Framework Core 8; the frontend uses React 18, TypeScript, and Vite; persistence uses SQL Server 2022.

This guide covers development setup, configuration, database management, build and test procedures, and deployment.

---

## 2. System Requirements

### Software Dependencies

| Component        | Technology                        | Version   |
|------------------|-----------------------------------|-----------|
| Backend Runtime  | .NET SDK                          | 8.0       |
| Frontend Runtime | Node.js                           | 20 in CI; 22 in the development image |
| Database         | Microsoft SQL Server              | 2022      |
| Containerization | Docker and Docker Compose         | Latest    |
| PDF fonts        | Noto Sans and Noto Sans CJK       | OS package or configured path |

### Backend Packages

| Package                                            | Version |
|----------------------------------------------------|---------|
| Microsoft.EntityFrameworkCore.SqlServer             | 8.0.4   |
| Microsoft.EntityFrameworkCore.Design                | 8.0.4   |
| Microsoft.AspNetCore.Authentication.JwtBearer       | 8.0.0   |
| AutoMapper                                         | 15.1.1  |
| MailKit                                            | 4.16.0  |
| Swashbuckle.AspNetCore                             | 6.5.0   |

### Frontend Packages

| Package            | Purpose                  |
|--------------------|--------------------------|
| React 18           | UI framework             |
| TypeScript          | Type safety              |
| Vite               | Build tool               |
| Tailwind CSS       | Styling                  |
| shadcn/ui (Radix)  | UI component library     |
| React Router       | Client-side routing      |
| Redux              | State management         |
| Playwright         | Browser E2E testing      |

---

## 3. Architecture

### System Diagram

```
+-------------------+        +-------------------+        +-------------------+
|                   |  HTTP  |                   |   EF    |                   |
|  React Frontend   +------->+  ASP.NET Core 8   +-------->+  SQL Server 2022  |
|  (Vite + TS)      |  API   |  Web API          |  Core   |                   |
|                   |        |                   |        |                   |
+-------------------+        +--------+----------+        +-------------------+
                                      |
                                      | SMTP
                                      v
                              +-------+--------+
                              |  Mail Server   |
                              +----------------+
```

### Project Structure

```
Nihome31042025/
  docker-compose.yaml          -- Container orchestration
  docker-compose-e2e.yaml      -- E2E overlay with a local mail sink
  nihomebackend/               -- ASP.NET Core 8 Web API
    Controllers/               -- API endpoint controllers (thin)
    Data/                      -- Database context, migrations, seeders
      Seeds/                   -- Embedded JSON seed data files
    Models/                    -- Entity models and DTOs
    Services/                  -- Business logic layer
    Extensions/                -- Startup and middleware extensions
    Mappings/                  -- AutoMapper profiles
    Migrations/                -- EF Core migration files
    Constants/                 -- Shared constants (EntityTypes)
    Localization/              -- Localization resources
    wwwroot/                   -- Static file serving (uploaded images)
  nihomeweb/                   -- React + TypeScript frontend
    src/
      pages/                   -- Page components
      components/              -- Reusable UI components
        admin/                 -- Admin-specific components
        layout/                -- Layout components (Nav, Footer)
        ui/                    -- Base UI components (shadcn)
      services/                -- API client services
      hooks/                   -- Custom React hooks
      lib/                     -- Utility functions
      store/                   -- Redux state management
  nihomebackend.tests/         -- Backend unit tests
    Controllers/               -- Controller tests
    Services/                  -- Service tests
    Mappings/                  -- AutoMapper profile tests
    Helpers/                   -- Test helper utilities
  docs/                        -- Documentation
```

### Design Principles

- Controllers remain thin; all business logic resides in service classes.
- Dependency injection is used throughout the backend.
- DTOs are used for all API communication; entity models are never exposed directly.
- Content entities use slug-based routing for SEO-friendly URLs.
- Complex nested data (content paragraphs, gallery images, requirements) is stored as JSON columns.
- Entity translations are restricted by the metadata registry in `TranslationsController`; only registered entity fields and the `en`, `zh`, and `ja` target languages are writable.
- Structured entity translations are validated recursively against their Vietnamese source JSON shape before persistence.
- Localized categories retain direct language columns for public-query compatibility. Source-copy fallback values are not completion evidence; an `entity_translations` marker records explicit source-identical translations.
- SOLID principles are followed where practical.

---

## 4. Getting Started

### 4.1 Running with Docker Compose (Recommended)

Docker Compose provisions SQL Server, the backend API, and all dependencies in a single command.

```bash
docker compose up -d
```

Services started:

| Service          | Container Name              | Port  |
|------------------|-----------------------------|-------|
| Backend API      | nihome31042025-backend      | 5043  |
| SQL Server       | nihome31042025-sqlserver    | 1433  |

Docker builds the Vite application and the ASP.NET release binary in separate
stages, then runs the published binary from the ASP.NET runtime image. There is
no separate frontend Compose service: port `5043` serves both the SPA and API.
Rebuild the image after source changes. Port `8080` is used only when running
Vite separately on the host.
`.dockerignore` excludes both `test-results` and `TestResults` directories,
including nested .NET test output and audit harness artifacts. Keep run output
outside the build context so it neither invalidates source layers nor enters
the intermediate image's `COPY . .` step.

To stop all services:

```bash
docker compose down
```

To rebuild containers after dependency changes:

```bash
docker compose up --build
```

The backend image installs and verifies the Latin and CJK Noto fonts used by
PDF exports. Non-container deployments can override font discovery with
`NIHOME_PDF_FONT_LATIN_PATH`, `NIHOME_PDF_FONT_JA_PATH`, and
`NIHOME_PDF_FONT_ZH_PATH`. For TTC files, set the corresponding `_INDEX`
variable to the required non-negative collection index. The defaults cover the
Docker image, Windows IIS, and common macOS font locations. Backend startup
fails before serving traffic when any required language font is unavailable or
misconfigured.

To remove all volumes and start fresh:

```bash
docker compose down -v
docker compose up --build -d
```

### 4.2 Running Backend Commands

Repository backend and database commands run in Docker. The Compose backend
uses the ASP.NET runtime image and has no SDK or file watcher; source edits
require an image rebuild. Compile the application through its build stage:

```bash
docker compose build nihomeBackend
```

Run formatting, focused tests and EF tooling in a separate .NET SDK container
with the repository mounted/copied at the solution root. Include the unit and
integration projects for whole-solution format verification. Use the Compose
network for SQL tests and isolated test databases; the runtime container is
not an SDK substitute.

For source-watching development, use the standalone `docker-compose-dev.yaml`
instead of the runtime Compose file. It builds `nihomebackend/Dockerfile.dev`
with .NET SDK 8, Node 22 and PDF fonts, mounts the repository at `/src`, and
runs `dotnet watch` from `/src/nihomebackend`. Linux dependency/build caches,
managed files, uploaded/process images and file quarantine use named volumes;
frontend changes are rebuilt and served by ASP.NET. The image supplies the
tracked design/as-built sample PDFs to initialize a new managed-file volume.
Startup removes only obsolete mixed-case NuGet import files left in the
persistent backend `obj` volume by the former `NihomeBackend.csproj` name; this
prevents duplicate `MSB4011` imports without discarding the remaining cache.
Run `docker compose -f docker-compose-dev.yaml up -d --build`. The development
connection string and SQL `MSSQL_SA_PASSWORD` use the same fixed local password
as the standard Compose setup. An existing SQL volume must already use that
password; changing environment settings does not reset a persisted login. Both Compose
files use the same application/container ports and SQL/key volumes by default,
so stop the runtime stack before starting the development stack. The dev image
has a separate tag and does not replace the published runtime image.
When reusing a database with uploads, back up and copy its matching managed
files/images into the development volumes before startup. Database metadata
and file storage must belong to the same dataset. A fresh project/database must
use fresh file volumes; never expose a different dataset's uploads to cleanup.

### 4.2.1 Swagger Access

Swagger is enabled only when the backend runs in the `Development` environment.

For the standard Docker Compose development setup, use:

- Swagger UI: `http://localhost:5043/swagger`
- OpenAPI JSON: `http://localhost:5043/swagger/v1/swagger.json`
- API base path: `http://localhost:5043/api`

### 4.3 Running the Frontend Locally

```bash
cd nihomeweb
npm install
npm run dev
```

The development server starts on `http://localhost:8080`. Set `VITE_API_URL` when it must call a backend on another origin; Vite does not define an API proxy.

### 4.4 Building for Production

Backend:

```bash
docker exec nihome31042025-backend dotnet build -c Release
```

Frontend:

```bash
cd nihomeweb
npm run build
```

The frontend build output is placed in `nihomeweb/dist/` and is served as static files by the ASP.NET backend in production.

---

## 5. Configuration

### 5.1 Application Settings

Application configuration, including logging for both runtimes, is managed
through `appsettings.json` and `appsettings.Development.json` in the
`nihomebackend/` directory.

#### Database Connection

From a separate Docker container that is not attached to the Compose network:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=host.docker.internal,1433;Database=NihomeDB;User Id=sa;Password=<development-password>;TrustServerCertificate=True;"
  }
}
```

From a tool running directly on the host:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=NihomeDB;User Id=sa;Password=<development-password>;TrustServerCertificate=True;"
  }
}
```

From the backend or another service on the Compose network:

```
Server=sqlserver,1433;Database=NihomeDB;User Id=sa;Password=<development-password>;TrustServerCertificate=True;
```

#### JWT Configuration

```json
{
  "Jwt": {
    "Issuer": "nihome-api",
    "Audience": "nihome-client",
    "AccessTokenMinutes": 10080,
    "RefreshTokenDays": 30,
    "ActiveKeyId": "key2",
    "Keys": {
      "key1": "<base64-encoded-key>",
      "key2": "<base64-encoded-key>"
    }
  }
}
```

Access tokens expire after 7 days (10080 minutes). Refresh tokens expire after 30 days. Two signing keys are supported for key rotation; the `ActiveKeyId` field determines which key signs new tokens.

#### SMTP Configuration

```json
{
  "Smtp": {
    "Host": "mail9005.maychuemail.com",
    "Port": 465,
    "UseSsl": true,
    "UseStartTls": false,
    "Username": "<email-username>",
    "Password": "<email-password>",
    "FromName": "Nihome",
    "FromEmail": "noreply@nihome.vn"
  }
}
```

#### CORS Configuration

```json
{
  "Frontend": {
    "AllowedOrigins": [
      "http://localhost:8080",
      "http://127.0.0.1:8080"
    ]
  }
}
```

#### Structured JSON Logging

The `Logging` sections in `appsettings.json` and
`appsettings.Development.json` are the source of truth for backend log levels
and backend/frontend logging identity fields. ASP.NET reads them at runtime;
Vite reads the `Logging:Frontend` values from the same files at build time.

Every application log is emitted as one JSON object with this contract:

```json
{
  "logger": "nihomebackend/Services/ExampleService.cs",
  "message": "date=\"2026-10-13T08:30:00.0000000+00:00\", process_id=\"314\", Example message",
  "properties": {
    "api_name": "Example.Get",
    "app_name": "NihomeBackend",
    "correlation_id": "request-123",
    "message_type": "api_request"
  },
  "severity": "INFO",
  "thread": ".NET TP Worker"
}
```

For application `ILogger<T>` categories, `logger` is the logical repository
source path derived from the .NET category. Classes declared in a differently
named source file must be listed in `Logging:Json:SourcePathOverrides`.
Framework categories that do not have a repository source file retain their
category name. Structured message-template values are retained under
`properties` with snake-case keys in addition to the four required identity
fields. Browser code must use
`createLogger(source)` from `nihomeweb/src/lib/logger.ts`; browser process and
thread values are represented by a generated browser-session id and
`browser-main` because the browser does not expose OS process/thread ids.

The frontend adds a unique `X-Correlation-Id` to each API request. The backend
validates it, creates a safe id when it is absent or invalid, exposes the
normalized id in the response header, and places it in the logging scope for
all downstream controller and service logs. The server-generated
`HttpContext.TraceIdentifier` remains the unique request id and is not replaced
by a client correlation id. The frontend API error logger reuses the request or
response correlation id so browser and backend failures can be queried
together. Background logs use a process-scoped correlation fallback when no
request or activity exists. Deployment log collectors should parse one JSON
object per console line.

### 5.2 Docker Compose Environment Variables

The following environment variables are set in `docker-compose.yaml`:

| Variable                              | Value                          | Purpose                              |
|---------------------------------------|--------------------------------|--------------------------------------|
| `ASPNETCORE_ENVIRONMENT`              | Development                    | Runtime environment profile          |
| `ASPNETCORE_URLS`                     | http://0.0.0.0:5043            | Kestrel binding address              |
| `ConnectionStrings__DefaultConnection`| SQL Server connection string   | Database connection (container name) |

The backend image builds the frontend and publishes the ASP.NET application in
multi-stage Docker builds. Compose runs that immutable published assembly. The
health response exposes `buildSha`; CI accepts readiness only when it matches
the checked-out commit, preventing an old process from passing E2E readiness.
`docker-compose-e2e.yaml` adds the non-relaying `smtpSink` and overrides
`EmailSettings__*` for browser delivery tests.

### 5.3 Docker Compose Volumes

Named volumes preserve application keys and SQL Server data:

| Volume                  | Mount Point          | Purpose                                     |
|-------------------------|----------------------|---------------------------------------------|
| `data_protection_keys`  | `/root/.aspnet/DataProtection-Keys` | Preserve ASP.NET data-protection keys |
| `sqlserver_data`        | `/var/opt/mssql`     | Preserve SQL Server data                      |

---

## 6. Database

### 6.1 Overview

The platform uses SQL Server 2022 with Entity Framework Core 8 as the ORM. The database is named `NihomeDB`. EF Core is configured with split-query behavior and a 60-second command timeout.

### 6.2 Schema

`AppDbContext` and the model snapshot are authoritative. The grouped inventory
below prevents this guide from implying that the earlier short table was the
complete operational schema.

| Domain | Main aggregates/tables |
|---|---|
| Identity and control | users, refresh/OTP, RBAC roles/permissions, notifications/templates, audit, idempotency, hard-delete operations |
| Public content | activities/categories, news/categories, public projects/categories, services, slideshow, logos, about, processes, recruitment, contacts, settings, translations |
| CRM/pre-design | leads/activities, customers/contacts/activities/documents, opportunities/activities, quotes/items/versions/approvals/documents, material-rate catalogs/revisions/lines, tenders/checklists/estimates, surveys/media/conditions/checklists |
| Shared project | operational projects, project members/roles/assignments/history, project documents/folders, project reports/read models |
| Contracts | contracts, payment milestones/events, appendices/VOs, attachments and contract lines |
| Design/legal | design projects, concept options, basic documents, shop drawings, drawing revisions, IFC releases/items/recipients, design schedule phases/tasks/dependencies/history, permit checklist items |
| Construction | construction tasks/dependencies, site diaries, punch items, HSE violations/events, acceptance records, as-built categories/documents, handover records/history |
| Procurement | vendors/uploads, BOQ revisions/lines, material requests/lines, RFQs/lines/invitations/bids/bid lines/events/awards, receipts/issues/lines, vendor ratings, material alerts/events |
| Finance/KPI | payment requests/events/attachments, accounting periods/corrections, KPI definitions/periods/snapshots |
| Drive integration | Google Drive credential, project document catalog, project-folder bindings and synchronization state |

The coexistence of public `projects`, central `operational_projects`, and
specialized `design_projects` is intentional. Never join them by name/code or
use one as a substitute for another; follow explicit foreign keys.

### 6.3 Selected Key Indexes

- `users`: Unique index on `Phone`
- `refresh_tokens`: Unique index on `Token`
- `registration_otp`: Index on `PhoneNumber`
- `activities`, `news_articles`, `projects`, `service_items`, `slideshow_items`: Unique index on `Slug`
- `activity_categories`: Unique index on `Name`
- `translations`: Unique composite index on (`Key`, `LanguageCode`)
- `entity_translations`: Unique composite index on (`EntityType`, `EntityId`, `FieldName`, `LanguageCode`)
- `process_documents`: Index on `GroupKey`
- `handover_records`: Unique indexes on `DesignProjectId` and `HandoverCode`; index on (`Status`, `PlannedHandoverDate`)
- `handover_status_history`: Index on (`HandoverRecordId`, `ChangedAt`)

### 6.4 Entity Framework Migrations

All schema changes must go through EF Core migrations. Never modify the schema directly.

The current development image does not install `dotnet-ef`, so `docker exec ... dotnet ef` is not a working command until a pinned tool manifest or image installation is added. Generate and review migrations with a .NET 8 SDK environment that has `dotnet-ef` 8.x installed, while keeping database work containerized. The intended commands from the backend project directory are:

```bash
dotnet ef migrations add <MigrationName>
```

Apply pending migrations:

```bash
dotnet ef database update
```

Remove the last unapplied migration:

```bash
dotnet ef migrations remove
```

Generate a SQL script for review:

```bash
dotnet ef migrations script
```

List all migrations and their status:

```bash
dotnet ef migrations list
```

Always review migration files before applying them.

Do not hand-author migration metadata or the model snapshot. Generate migrations with EF Core in the provisioned Docker-based .NET 8 SDK tooling environment, review the generated migration and snapshot, and only then apply them.

The project handover schema is introduced by `AddHandoverRecords` and hardened by `AddHandoverConcurrency`. The latter adds SQL Server `rowversion` to prevent silent lost updates. Both migrations must be applied before deploying the NIH-144 application build.

### 6.5 Data Seeding

Application startup applies pending migrations and then runs the baseline seed
pipeline: users/settings, content, UI and entity translations, RBAC catalog/roles,
master data, workflows, notification templates, and deterministic business-role
users. Development-only sample CRM/design/construction data is enabled by the
startup host only in the `Development` environment; integration tests create
their own business fixtures explicitly.

For a SQL-only reset of the CRM, Design, Procurement, Finance, Legal,
Construction, and Analytics records, open `tools/sqlserver_business_data_purge.sql`
in a SQL Server query editor and run the entire script. Set `@ExpectedDatabase`
to the exact selected database name (`NihomeDB` locally or the configured
deployment database name), and set `@ExpectedSchema` to the application schema
(`dbo` locally; the observed deployment schema is `niconNew`). Keep
`@Execute = 0` for the first run to inspect the table scope, row counts, and
foreign-key dependencies. Tables in other schemas are counted but left alone.
Stop backend writes and take a verified SQL backup before rerunning with
`@Execute = 1`. The script keeps users, permissions, settings, translations,
website content, audit history, and EF migration history. Managed files and
Google Drive objects remain; clean those separately through their owning
workflows if needed. New or unclassified tables stop the script until its scope
is reviewed. Development hosts seed sample business records at startup, so keep
them stopped after a reset unless sample data is intentionally wanted.

Content behavior is entity-specific. Activities, news, and projects are slug-based backfills that preserve administrator edits. Process documents and logos are also reconciled additively: missing canonical rows are restored without deleting custom rows or overwriting administrator-managed values. Translation, RBAC, master-data, workflow, and notification files are embedded resources.

#### Process Document Seeder Identity

Canonical process rows carry an internal nullable `SeedKey` with a filtered unique index. The key is derived from the process group and its first canonical asset URL, so manifest or administrator title and sort-order changes do not alter identity. On the first startup after the migration, existing canonical rows are identified by their canonical title or seeded asset URLs and assigned that identity. Rows without a seed identity remain administrator-owned and are not deleted or repurposed.

Missing image and file metadata is backfilled only while the canonical title remains unchanged. Workflow manifests are validated before persistence; malformed definitions, invalid or duplicate step orders, and unknown approver roles fail startup rather than silently weakening an approval chain.

#### Static Asset Files for Process Documents

Physical image and file assets are stored outside the database and served as static files:

```
nihomebackend/wwwroot/process-assets/
  images/     -- JPEG/PNG images referenced by ImagesJson
  files/      -- DOC/PDF/etc files referenced by FilesJson
```

These files must be present on the server before the URLs in `processes.json` can resolve. They are not tracked in git (binary files); the authoritative source is a backup archive. In development, copy the contents of the backup to `nihomebackend/wwwroot/process-assets/`.

Translation seed files are embedded resources under `Data/Seeds/i18n/`.

#### Seeded Accounts

The backend seeders create deterministic `SUPER_ADMIN`, `ADMIN`, and selected business-role accounts for development and automated tests. Current identifiers are defined in `DbSeeder`, `BusinessRoleUserSeeder`, integration `TestDataSeeder`, and Playwright fixtures; do not duplicate credentials in operational documentation.

The startup path gates demo/sample business data to Development. Production
deployment must still rotate or disable deterministic development accounts before
the application is exposed.

### 6.6 Verifying the Database

Connect to SQL Server running in Docker:

```bash
docker run --platform linux/amd64 -it --rm \
  --network container:nihome31042025-sqlserver \
  mcr.microsoft.com/mssql-tools \
  /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "<development-password>"
```

List all databases:

```sql
SELECT name FROM sys.databases;
GO
```

List all tables:

```sql
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';
GO
```

Describe a table:

```sql
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'YourTableName';
GO
```

Show indexes on a table:

```sql
EXEC sp_helpindex 'YourTableName';
GO
```

---

### 6.7 Operational Project Historical Migration

#### Historical migration scope

NIH-465 reconciles historical `DesignProject`, `Contract`, `Opportunity`, and
`Quote` rows into the internal `OperationalProject` aggregate. Public website
`Project` content is explicitly outside this migration.

The migration preserves source rows and history. It updates only missing
`OperationalProjectId` values and creates an Operational Project only when no
existing project can be inherited from the same business chain. It does not
merge or delete existing Operational Projects.

#### Mapping Rules

The migration uses deterministic source precedence:

1. A Design Project inherits its Contract's existing Operational Project.
2. A missing Design Project project is created as `PJ-MIG-DP-{id}`; its Contract
   then inherits that project.
3. An Opportunity inherits the single project already used by its Contracts or
   Quotes.
4. A missing Opportunity project is created as `PJ-MIG-OP-{id}`; its Quotes and
   Contracts then inherit that project.
5. A direct Contract with no project source is created as `PJ-MIG-CT-{id}`.

Every inherited project must belong to the same Customer. Multiple project
candidates, cross-customer mappings, broken foreign keys, chain mismatches, and
deterministic code collisions block the migration instead of being guessed.
The stable codes and `NOT EXISTS` guards make the data statements rerunnable.

#### Pre-Deployment Rehearsal

Back up the target database and restore it under a temporary database name.
Generate the migration script, review it, and apply it to that copy before the
production maintenance window. This rehearses the exact migration instead of
maintaining a separate SQL implementation that can drift from it.

The migration itself is the validation boundary. It aborts and rolls back when
it finds broken source references, cross-customer relationships, multiple
project candidates, deterministic-code collisions, changed source counts,
unmapped rows, or post-migration chain inconsistencies. Save the migration output
and before/after source counts as deployment evidence. Resolve any reported
`THROW 51020` through `THROW 51029` condition in the source data; do not disable
the checks or edit the migration to skip affected rows.

After a successful rehearsal, verify that all historical rows are mapped:

```sql
SELECT 'DesignProject' EntityType, COUNT(*) TotalRows,
   COUNT(OperationalProjectId) MappedRows FROM design_projects
UNION ALL
SELECT 'Contract', COUNT(*), COUNT(OperationalProjectId) FROM contracts
UNION ALL
SELECT 'Opportunity', COUNT(*), COUNT(OperationalProjectId) FROM opportunities
UNION ALL
SELECT 'Quote', COUNT(*), COUNT(OperationalProjectId) FROM quotes;
```

For each row, `TotalRows` and `MappedRows` must match. Run production deployment
with application writes stopped, and take a second backup immediately after the
post-migration check passes. If writes resume, prefer a reviewed forward
correction; restoring the earlier backup would discard later transactions.

#### Deployment

Confirm the backend container with `docker compose ps`, then generate and review
the idempotent SQL script before applying it:

```bash
docker exec <backend-container> dotnet ef migrations script \
  20260906150351_AddFinanceControlWorkflows \
  20260907021500_ReconcileOperationalProjectBackfill --idempotent
docker exec <backend-container> dotnet ef database update
```

EF Core runs the migration in a transaction. The migration records source row
counts, performs the backfill, and aborts if source counts change, any historical
row remains unmapped, or post-migration customer and chain integrity fails.

Run the source-count query again after deployment. All four source groups must
have matching total and mapped counts.

#### Rollback and Compatibility

The migration has no destructive schema operation, but its data links cannot be
removed safely after downstream modules begin using them. If deployment fails,
the migration transaction rolls back automatically. If a rollback is required
after a successful deployment, stop writes and restore the pre-deployment
database backup; do not null project IDs or delete generated projects manually.

Existing API routes and DTO fields are unchanged. Nullable project fields remain
wire-compatible for current clients; this historical migration does not impose
new runtime `NOT NULL` constraints. Runtime enforcement for every new Contract
to belong to exactly one Operational Project remains owned by NIH-466.

---

## 7. Backend Development

### 7.1 Code Organization

The backend follows a layered architecture:

```
Controller (thin) --> Service (business logic) --> DbContext (data access)
```

- **Controllers**: Accept HTTP requests, validate input, delegate to services, return DTOs.
- **Services**: Contain all business logic. Each content entity has a dedicated service.
- **Models**: Divided into entities (database models) and DTOs (request/response models).
- **Mappings**: AutoMapper profiles for entity-to-DTO conversion.
- **Extensions**: Startup configuration (DI registration, CORS, middleware).

### 7.2 Services Overview

| Service                    | Purpose                                                  |
|----------------------------|----------------------------------------------------------|
| `JwtService`               | Generate JWT access tokens with user claims              |
| `RefreshTokenService`      | Manage refresh token lifecycle (create, validate, revoke)|
| `PasswordService`          | Hash and verify passwords using Identity framework       |
| `OtpService`               | Generate, verify, and manage OTP codes                   |
| `EmailService`             | Send emails via SMTP using MailKit                       |
| `EmailTemplateFormatter`   | Format email templates with placeholder substitution     |
| `TimeService`              | Centralized UTC time provider                            |
| `HostedImageService`       | Background service for image management                  |
| `OrphanedManagedFileCleanupService` | Quarantines unreferenced managed images/files after 24 hours, restores newly referenced files, and purges only after another 24-hour retention window |
| `ActivityService`          | CRUD for activities with slug lookup and language support |
| `ActivityCategoryService`  | CRUD for activity categories                             |
| `NewsService`              | CRUD for news articles with language support              |
| `ProjectService`           | CRUD for projects with slug lookup                       |
| `OperationalProjectService` | Scoped central-project CRUD, lifecycle, aggregation, code allocation, and concurrency |
| `ServiceItemService`       | CRUD for services with slug lookup                       |
| `SlideshowService`         | CRUD for slideshow items with filtering                  |
| `AboutSectionService`      | CRUD for profile/about page sections and structured data |
| `JobPositionService`       | CRUD for job positions                                   |
| `JobApplicationService`    | Submit, list, and manage job applications                |
| `ContactMessageService`    | Submit, list, and reply to contact messages              |
| `LogoService`              | CRUD for logos grouped by type                           |
| `ProcessService`           | CRUD for process documents grouped by category           |
| `SiteSettingsService`      | Get and update site settings and email templates         |
| `TranslationService`       | Manage static UI translations                            |
| `EntityTranslationService` | Manage dynamic content translations (polymorphic)        |
| `HandoverRecordService`    | Project handover scoping, validation, readiness derivation, lifecycle, and optimistic concurrency |

The orphan cleanup worker runs every six hours. It scans managed local paths
under `/images/upload`, `/images/processes`, and `/files`, while excluding the
hard-delete workflow's own quarantine. An unreferenced file must be at least
24 hours old before it is atomically moved to `.managed-file-quarantine`
outside `wwwroot`. The worker re-reads active database references immediately
before the move and again afterward; a newly referenced file is restored to
its original path. An unreferenced quarantined file is permanently purged only
after a further 24-hour retention period and another reference check. Invalid
reference data or symlink/reparse-point paths stop the operation safely rather
than risking deletion outside the managed roots.

### 7.3 Adding a New Entity

To add a new content entity:

1. Create the entity model in `Models/`.
2. Create request and response DTOs in `Models/`.
3. Add a `DbSet` in `Data/AppDbContext.cs` and configure the table in `OnModelCreating`.
4. Generate and review a migration using the Docker-based .NET 8 SDK tooling environment described in section 6.4.
5. Create a service class in `Services/`.
6. Create a controller in `Controllers/`.
7. Add AutoMapper mappings in `Mappings/AutoMapperProfile.cs`.
8. Register the service in `Extensions/ServiceCollectionExtensions.cs`.
9. Add unit tests for isolated logic and integration tests for HTTP, authorization, and persistence contracts at the appropriate test layer.

### 7.4 Conventions

- Use `async/await` for all I/O operations.
- Use `AsNoTracking()` for read-only queries.
- Return DTOs from controllers, never entity models.
- Use meaningful HTTP status codes (200, 201, 204, 400, 401, 403, 404, 409, 500).
- Validate input at the controller level.
- Keep controllers under 20 lines per action method where possible.
- JSON columns use string serialization (e.g., `ContentJson`, `SectionsJson`).

### 7.5 Procurement Vendor API

The procurement vendor slice stores supplier and subcontractor profiles in `procurement_vendors`. Vendor codes are trimmed and normalized to uppercase; vendor codes and normalized company names are unique. A vendor requires at least one valid phone number or email address. Use `IsActive` to retain a vendor for historical reporting while preventing new downstream Contracts. Detail responses include related Contracts, Operational Projects, Vendor Ratings, responsible update metadata, and the Vendor-scoped audit timeline.

Both `/api/vendors` and `/api/v1/vendors` expose the same controller:

| Method | Route | Permission | Purpose |
|--------|-------|------------|---------|
| `GET` | `/api/vendors` | `proc.vendors.view` | Search, filter, sort, and paginate vendors |
| `GET` | `/api/vendors/{id}` | `proc.vendors.view` | Read vendor details and audit metadata |
| `POST` | `/api/vendors` | `proc.vendors.manage` | Create an active vendor |
| `PUT` | `/api/vendors/{id}` | `proc.vendors.manage` | Update profile data or active status |
| `GET` | `/api/vendors/{id}/deletion-impact` | `proc.vendors.manage` | Preview Contract, Rating, Payment Request, and file impact |
| `DELETE` | `/api/vendors/{id}` | `proc.vendors.manage` | Start the confirmed durable hard-delete operation |
| `DELETE` | `/api/business-documents/vendors` | `proc.vendors.manage` | Discard an uploaded document only while no Vendor references it |

Duplicate normalized codes or company names return `409`; invalid contact or file references return `400`; missing records return `404`. Create and update operations write `vendor.create` and `vendor.update` audit events. Vendor deletion follows the repository hard-delete convention: Contracts, Vendor Ratings, Payment Requests, shared files, and unsafe files block deletion; an owned managed capability file is quarantined and purged by the durable operation. `proc.vendors.export` controls the frontend export action but does not grant API read access by itself.

Vendor capability uploads return an opaque claim token. A create or update that
uses the managed path must submit the matching token; the server claims the file
in the same database write as the Vendor. Cancelled forms discard pending tokens,
and the cleanup worker removes unclaimed uploads older than 24 hours. Claimed
uploads cannot be discarded or claimed by another Vendor.

### 7.6 Permit Checklist API

Permit creation is an explicit Operational Project workflow. An eligible
Upstream `Design`, `Construction`, or `DesignAndBuild` Contract cannot transition
from `Signed` to `InProgress` through the generic transition endpoint; it returns
`LEGAL_ACTIVATION_REQUIRED`. The PM/Admin must confirm the Legal wizard. Creating
or ensuring a `DesignProject` never auto-generates permits.

The activation transaction creates or reuses the project's single
`LegalPermitting` work package, creates one task per selected permit, records
dependencies and audit history, creates the Contract activation record, and
then changes the Contract status. Validation or concurrency failure rolls back
the entire operation. `ContractLegalActivation.ContractId` is unique for retry
protection; the package is unique by `(OperationalProjectId, LegalPermitting)`;
permit type is unique per Operational Project; and `WorkTaskId` is an optional
unique compatibility link. `DesignProjectId` remains nullable for legacy reads.

Activation/setup routes:

| Method | Route | Permission plus service scope | Purpose |
|--------|-------|-------------------------------|---------|
| `GET` | `/api/contracts/{id}/legal-activation-preview` | `crm.contracts.view`; project PM/Admin | Read-only eligibility, active permit types, existing package, and active project members |
| `POST` | `/api/contracts/{id}/activate-with-legal` | `crm.contracts.view`; project PM/Admin; idempotency key and row version | Atomically activate Contract and create/reuse Legal configuration |
| `GET` | `/api/operational-projects/{id}/legal-setup` | `permit.checklists.manage`; project PM/Admin | Preview setup for legacy/already-active projects |
| `POST` | `/api/operational-projects/{id}/legal-setup` | `permit.checklists.manage`; project PM/Admin; idempotency key | Create/reuse package and link legacy permits without changing Contract status |

Legal checklist template routes:

| Method | Route | Permission | Purpose |
|--------|-------|------------|---------|
| `GET` | `/api/legal-checklist-templates?publishedOnly=true` | `permit.templates.view` | List definitions; omit filter for all versions |
| `GET` | `/api/legal-checklist-templates/{id}` | `permit.templates.view` | Read definition, items, authors, reviewer, approval reference and row version |
| `POST` | `/api/legal-checklist-templates` | `permit.templates.manage` | Create Draft; required idempotency key |
| `PUT` | `/api/legal-checklist-templates/{id}` | `permit.templates.manage` | Edit Draft with row version |
| `POST` | `/api/legal-checklist-templates/{id}/versions` | `permit.templates.manage` | Clone a Published/Retired version into a new Draft; row version and idempotency key |
| `POST` | `/api/legal-checklist-templates/{id}/publish` | `permit.templates.publish` | Independent CM/Admin review; row version, approval reference and idempotency key |
| `POST` | `/api/legal-checklist-templates/{id}/retire` | `permit.templates.publish` | Stop new application without changing project snapshots; row version and idempotency key |

Template input includes an immutable ASCII `Code` (1–60 letters/digits/`-`/`_`,
starting with a letter/digit), required `NameVi/Name/NameZh/NameJa` (200 characters)
and `ScopeVi/Scope/ScopeZh/ScopeJa` (2,000 characters). The ordered `Items` list
contains 1–100 unique active `permit_type` codes and `IsRequired`; at least one
item must be required. Publication requires a nonblank `ApprovalReference` of
at most 1,000 characters. The creator and every editor of that version cannot
publish it. Contributor IDs are retained with the JSON definition independently
of general audit-log retention. `Draft → Published → Retired` is server-enforced; published content
is immutable. There is no template hard-delete endpoint.

Activation/setup preview now includes `Templates`, `AppliedTemplate`,
`WorkPackageRowVersion`, existing package lead/dates, and template application
actor/time. Both writes accept `TemplateId`, `TemplateRowVersion`, and
`WorkPackageRowVersion`. New packages require a Published template and matching
template version. Every setup/activation that reuses an existing package
requires its matching row version, including packages with an applied snapshot.
Stale requests return HTTP `409` without changing the dossier or activating
another contract (`CONCURRENCY_CONFLICT` for contract activation;
`crm_concurrency_conflict` from the global handler for project setup).
Confirmed schedule/lead changes record
`UpdatedByLegalSetup` history with before/after values and actor. A project with a snapshot keeps using it, including
after template retirement; another template is rejected. Required items must
already have a linked task or be included for creation/linking in the request.
Omission returns `LEGAL_REQUIRED_PERMITS_MISSING` before any write.

Setup cannot modify a Completed or Cancelled package (`LEGAL_PACKAGE_CLOSED`).
A second contract may reuse a Completed package read-only when its snapshot is
already applied, required items are linked and the request changes neither the
template, lead, dates nor task set. This records only the new contract activation;
it does not reopen or touch the legal dossier. Cancelled packages cannot be reused.
New tasks may depend on an existing configured permit from the same Operational
Project without resubmitting that permit. Unknown, unlinked and other-project
predecessors are rejected; new-task dependency cycles remain invalid.

The Legal package stores the immutable template snapshot and application audit;
permit responses expose `IsRequired`. Existing projects are not assigned a
template by migration. Applying one preserves existing package schedule/lead,
permit state, files and tasks; newly linked/created tasks must fit the existing
package schedule. Existing untemplated packages can continue to operate. The
Generic `/api/work-packages` creation rejects `LegalPermitting`, preventing a
new untemplated package from bypassing the specialist setup workflow. The
flag controls checklist inclusion, not the construction gate's stage-specific
permit requirements. Q-09/D-04 remain CM/NICON-owned decisions; demo templates
are Draft only. After a concurrency conflict, refresh keeps local draft input;
users review current data before confirming a retry.

Migration `20261022111459_AddLegalChecklistTemplates` is additive: nullable
package snapshot/source columns, a template table and `IsRequired=false` for
legacy permits. It does not backfill business classifications. Before rollback,
back up new template/snapshot data and stop application writes; `Down` removes
the new feature data/columns, retaining the original permit/task/file records.
Do not roll back after real template usage without a reviewed preservation plan.

Both `/api/permits` and `/api/v1/permits` expose the same controller:

| Method | Route | Permission | Purpose |
|--------|-------|------------|---------|
| `GET` | `/api/permits` | `permit.checklists.view` | Filter, paginate, and read the permit risk summary |
| `GET` | `/api/permits/{id}` | `permit.checklists.view` | Read one checklist item |
| `GET` | `/api/permits/{id}/history` | `permit.checklists.view` | Read authority-state/document audit history |
| `POST` | `/api/permits` | `permit.checklists.manage` | Retired with `410 LEGAL_SETUP_REQUIRED`; use Legal setup |
| `PATCH` | `/api/permits/{id}` | `permit.checklists.view` plus project actor scope | Update authority status, agency, dates, or notes; linked assignment/schedule stay task-owned |
| `POST` | `/api/permits/{id}/documents/{kind}` | `permit.checklists.view` plus project actor scope | Upload submitted or issued evidence under source module `Legal` |
| `DELETE` | `/api/permits/{id}` | `permit.checklists.manage` | Blocked with `409`; use aggregate-safe configuration/deletion workflow |
| `POST` | `/api/permits/design-project/{projectId}/ensure` | `permit.checklists.manage` | Retired with `410 LEGAL_SETUP_REQUIRED` |

Authority transitions are server locked: `Submitted`, `UnderReview`, and
`NeedMoreDocs` require a submission date and submitted-package file; `Issued`
requires an issue date and issued-permit file plus
`permit.checklists.mark-issued`. Files are catalogued in `LegalPermits` beneath
`03_Xin_phep_Phap_ly` with `ProjectDocumentSourceModule.Legal`.

The linked `DepartmentWorkTask` owns assignee, approver, plan, progress, and
dependencies. The assignee may submit `Completed` only when the permit is
`Issued` with its required evidence. Verification performs
`Completed → Verified`; a return requires a reason and performs
`Completed → InProgress` without changing the authority status.

### 7.7 Permanent Aggregate Deletion

#### Objective

Allow authorized users to permanently remove aggregate roots, including seeded
or demo records, without hiding dependent data or causing partial deletion.

#### Permanent-delete API contract

Each supported aggregate exposes:

1. `GET /api/{resource}/{id}/deletion-impact`
2. `DELETE /api/{resource}/{id}` with `planToken`, `confirmation`, and a
  mandatory `rowVersion` when the root supports optimistic concurrency

The preview returns the root label and confirmation code, a deterministic plan
token, the total affected count, whether deletion is currently allowed, and
dependent groups classified as:

- `Delete`: aggregate-owned records removed with the root.
- `Unlink`: independent records or external resources preserved after their
  binding to the root is removed.
- `Block`: data that must be safely cleaned before the root can be deleted.

The server recomputes the impact and creates the durable operation in one
serializable transaction, commits it, and only then starts processing. For
Opportunity, Quote, and Contract, `rowVersion` is mandatory: a missing or malformed
token returns `400 Bad Request`, while a stale token or changed plan returns
`409 Conflict`. Invalid confirmation or an active blocker returns
`400 Bad Request`. Rejected requests must leave the root and all dependencies
unchanged and must not emit a success/request audit.

The deterministic plan includes every business-significant direct and nested
dependent identifier. Adding or removing a nested child after preview therefore
invalidates the submitted token.

When execution includes managed local files or Nicon-owned Google Drive items,
the delete endpoint creates a durable hard-delete operation. The operation and
its items are independent records identified by a GUID; they do not hold foreign
keys to the aggregate root. Only one unresolved operation may exist for a
resource type and resource ID.

The endpoint returns:

- `204 No Content` only after external cleanup, the registered database
  finalizer, and quarantine purge have all completed.
- `202 Accepted` with the operation ID and current status when durable work is
  still pending, retrying, or requires manual action.

Clients may safely poll the operation result. A durable operation is not proof
that the root has been deleted until its status is `Completed`.

#### Execution Rules

- Controllers authorize and translate domain outcomes to HTTP responses.
- Services validate the plan, confirmation, concurrency, and blockers.
- Aggregate deletion services own dependency ordering and file staging.
- Managed files are cleaned through the project-document workflow before their
  parent project can be removed.
- Local managed files must use host-relative paths under an explicit private
  storage root. Execution moves them atomically to a same-volume hard-delete
  quarantine before any irreversible step when the parent durable operation
  owns their deletion. Project-dependent files are the exception below and are
  deleted only by their source module's manual cleanup workflow.
- Design and Operational Project deletion never removes dependent local or Drive
  files automatically. Every remaining file blocks parent deletion and includes
  a link to its owning detail or document page. The user must delete the source
  record or project document through that module's authorized workflow; its
  existing synchronization service then removes the Drive replica safely. The
  parent can be deleted only after those file dependencies are fully cleaned.
  Failed Drive-delete sidecars remain blockers and can be manually retried by
  an authorized Operational Project manager even after automatic retries are
  exhausted.
- Operational Project deletion includes the application's verified project
  folder tree in the durable plan after source-file cleanup. It inventories all
  children (including trashed items). Trashed replicas matching a deleted owned
  document's instance, replica key and generation enter the durable file plan
  before their parent folders; execution rejects replicas restored from trash.
  Empty path folders are deleted before their project root, with emptiness
  rechecked at execution. Unknown/imported/shared contents and folders with
  independent CRM files block deletion. A Drive outage
  remains visible and retryable; the database root survives until cleanup is
  complete. Stored operation items preserve the original plan across partial
  remote deletion. Design-only deletion preserves the shared Operational Project
  folder; non-migrated Survey bindings retain their existing unlink behavior.
- A migrated plan may permanently delete a Drive file or folder only when its
  metadata proves current Nicon `InstanceId` ownership, every caller-supplied
  expected app property matches, the expected parent matches when supplied,
  and Drive reports that the connected account owns and can delete the item.
  Imported, shared, mismatched, or unknown-origin items are blockers and must
  never be permanently deleted. A missing Drive item is an idempotent success.
- Independent CRM records such as opportunities, quotes, and contracts are
  unlinked rather than deleted with an Operational Project. Their local and
  Drive files are preserved; only project-bound synchronization metadata is
  removed with the deleted Operational Project.
- Tender checklist uploads under `/files/tenders` are aggregate-owned and are
  quarantined and purged with the Tender. Checklist references to Capability
  Documents are unlinked while the shared document and file survive. Any
  non-library checklist file outside `/files/tenders` blocks deletion rather
  than being silently orphaned.
- Quote documents under `/files/quotes` are aggregate-owned and are quarantined
  and purged with the Quote. Opportunities that reference the Quote are unlinked
  and preserved; any Contract reference blocks Quote deletion. A Quote
  project-document sidecar is eligible
  only when it is an exact CRM `QuoteDocument`/`file` binding to the normalized
  Quote path, has stable Nicon ownership with no conflict or active processing
  lease, and is either fully synced with complete Drive ownership metadata or
  already terminally deleted without a Drive file ID. The durable operation
  permanently deletes eligible Drive replicas using verified app properties,
  then preserves and terminalizes their sidecar records. Imported, shared,
  ambiguous, incomplete, mismatched, or unstable sidecars block deletion.
- Opportunity activities and translations are aggregate-owned. Quotes block
  Opportunity deletion; Contracts, Surveys, converted Leads, and winning
  Tenders are independent roots that are unlinked and preserved.
- Contract milestones, attachments, appendices, and files under
  `/files/contracts/` are aggregate-owned. Linked Design Projects are unlinked
  and preserved. Deletion is blocked when a path is unsafe or shared, or when
  the Contract is the last qualifying contract for a Won Opportunity. Exact
  Nicon-owned CRM sidecars for `ContractAttachment` and `ContractAppendix` use
  the same safety checks as Quote sidecars: ambiguous, conflicting, claimed,
  pending, mismatched, imported, or duplicate Drive identities block deletion.
  The durable operation verifies ownership metadata and permanently deletes
  safe synced Drive replicas before the finalizer terminalizes and preserves
  their sidecars; the finalizer never queues a background Drive delete.
- Survey checklist results and site conditions are aggregate-owned. Survey
  Media must be removed through its own managed-file workflow before the Survey
  can be deleted. An external Drive folder binding is unlinked and preserved.
  Survey management scope is `crm.surveys.manage.all`, assigned surveyor,
  survey creator, Operational Project manager, or Operational Project creator.
- Capability Document versions and files under `/files/capability/` are
  aggregate-owned. Unsafe or shared paths block deletion. Tender checklist
  references block deletion until the shared document is detached explicitly.
- Customer contacts, activities, documents, translations, and files under the
  exact `/files/customers/{customerId}/` root are aggregate-owned. Converted
  Lead and Project Document metadata links are cleared while those records are
  preserved. Opportunities, Tenders, Contracts, Design Projects, and
  Operational Projects are independent required roots and block Customer
  deletion until handled through their own authorized workflows. Undoing a
  Lead conversion always preserves its Customer and removes the project that
  conversion opened only when its deletion-impact plan lists nothing but that
  opportunity; Customers can only be
  permanently removed through this preview-and-confirm contract.
- Exactly one success audit is written by the durable operation processor in
  the same save that marks the operation `Completed`, after database
  finalization and quarantine purge. Request, validation-failure, and
  pre-completion success audits are not emitted.
- Hard-deleted seeded roots write a durable tombstone. Opportunity uses its
  exact seeded name, Contract its `HD-SAMPLE-` number, Survey its `SV-SAMPLE-`
  code, Tender its exact `TD-SAMPLE-` code, and Capability Document its managed
  path when the description begins `[SAMPLE_CAP]`. Seed reruns filter roots and
  dependents before insertion and skip Capability physical-file self-healing
  for tombstoned paths.

#### Durable Operation Lifecycle

Operations move through `Preparing`, `Ready`, `Processing`, `Completed`,
`Failed`, or `ManualActionRequired`:

1. A domain service validates authorization, confirmation, concurrency,
  blockers, and the current deterministic plan before creating the operation.
2. The processor quarantines local files, then permanently deletes only verified
  owned Drive items.
3. After external cleanup, a resource handler registered by resource type runs
  the idempotent database finalizer. Delegate-only finalizers are not allowed
  because they cannot survive application restart.
4. The processor purges quarantined local files, then atomically writes the
  idempotent completion audit and marks the operation `Completed`.

Failures before the first Drive deletion restore quarantined files and may be
retried with conservative backoff. Once a Drive deletion or database finalizer
begins, rollback is no longer safe: the operation remains in forward recovery
until remaining idempotent steps complete. Ownership mismatches and exhausted
retries move to `ManualActionRequired`; operators must resolve the blocker and
explicitly retry. Resource handlers must use the operation ID as an idempotency
key because a restart can occur after the database commit but before the item is
marked complete.

#### Frontend Rules

Use the shared deletion-impact dialog to load the preview, show categorized
counts and examples, disable deletion while blockers exist, and require the
exact typed confirmation. Refresh the relevant list/detail state after success.
Do not implement aggregate deletion as parallel per-row API calls.

Business-data blockers should include an internal resolution URL when a filtered
ADMIN list exists and internal detail links for the displayed blocker examples.
The shared dialog opens those destinations in a new tab so the user can resolve
dependencies without losing the current deletion preview. The filtered list link
is shown only when additional blockers are not represented by the detail links.
The link is a navigation convenience only; access, record scope, and available
actions on the destination page follow the existing RBAC matrix for that module.

All display text and dependency labels are backend-seeded content translations
with Vietnamese, English, Chinese, and Japanese values.

#### Verification

Use integration tests for HTTP authorization, model binding, preview accuracy,
stale plans, row-version conflicts, blockers, persistence, unlinking, and
unchanged state after rejection. Use browser tests only for dialog rendering,
typed confirmation, blocker visibility, and successful UI refresh.

#### Current Rollout

The durable operation, local quarantine, verified Drive deletion, registry, and
retry foundation is available for domain adoption. Design Project, Operational
Project, Lead, Customer, Tender, Quote, Opportunity, Contract, Survey, Vendor,
and Capability Document use the durable backend flow and shared frontend polling
dialog. Owner-scoped resources also enforce scope again during finalization.
Bulk deletion is disabled on these root pages until a server-side batch
preview-and-confirm contract is available. Other root pages must still be
migrated separately before the repository-wide hard-delete rollout is complete.

### 7.8 Customer Documents and Contract Ownership

#### Contract execution progress

`GET /api/contracts/{id}/execution-progress` (also `/api/v1/...`) requires
`crm.contracts.view` and the existing Contract owner/project/direction read
scope. Inaccessible or missing Contracts return `404`. The response deliberately
has no combined completion percentage. It contains independent `work`, `qs`,
`payment` measures and UTC `calculatedAt`.

`work` is null without `work.packages.view`; otherwise it reuses the work-package
list's owner/creator/assignee/approver/CC/project/admin visibility rules.
Only `DepartmentWorkPackage.ContractId == contract.Id` qualifies. Package counts
include cancellations, reported separately; task counts exclude cancelled
packages and split Completed, Verified and Cancelled tasks. Completed and Verified
are disjoint. Counts cover all qualifying records; only the ten most recently
updated package links are previewed. This is not a WBS-weighted progress measure.

`qs` is null without `construction.qs.view` or access to the Contract's
Operational Project. Count only dossiers matching Contract ID, Operational
Project ID and Upstream/Downstream direction. All-history count is separate from
pending CM review and current approved count. Current approved means CmApproved,
Active record state, active lineage and not a Reversal record; replaced/reversed
predecessors and reversal records do not count as effective approvals. Preview
the ten most recently updated sources; do not sum certified amounts or infer a
completion rate. QS links select the project, filter by Contract and anchor the
source dossier in the existing QS workspace. Changing projects clears that filter.
Source anchoring runs when the dossier mounts, including when the project list
loads after the QS records; regression coverage delays that project response.

`payment` uses the same Contract-visible schedule as the detail page: milestone
count, Paid/Requested counts, scheduled amount and amount of Paid milestones.
Each line uses rounded `Contract.Value * PercentValue / 100`, matching the base
Contract schedule. It is not a cash ledger, does not claim actual receipts or
disbursements, and excludes VO adjustments handled outside this schedule.

No schema/write contract changes, notifications or lifecycle transitions are
introduced. Project-only Construction Tasks/Acceptance records are not attributed
to a Contract. Null restricted-source states differ from authorized empty counts.
Sources refresh on Contract changes and manual refresh. Verification lives in
`ContractExecutionProgressTests` and `admin-contract-execution-progress.spec.ts`.

#### Contract source Lead traceability

`GET /api/contracts/{id}/source-leads` (also `/api/v1/contracts/{id}/source-leads`)
requires both `crm.contracts.view` and `crm.leads.view`. Existing Contract
owner/project/direction access still applies; inaccessible or missing Contracts
return `404`. Lead scope is checked independently: without `crm.leads.view.all`,
only Leads assigned to the caller are returned. Contract portfolio or project
access does not widen Lead access.

The response is an ordered array of `{ id, name, convertedAt }`. For Upstream
Contracts, trace `Contract.OpportunityId` to converted Leads whose
`ConvertedOpportunityId` and `ConvertedCustomerId` match and whose `ConvertedAt`
is present. Return every visible match, ordered by conversion timestamp and ID;
never pick an arbitrary first Lead or infer a source from a shared customer or
project. Downstream Contracts and direct Contracts without a source Opportunity
return an empty array. No new editable Lead field, schema, or backfill is needed.
Unconversion/unlinking is reflected on the next read; existing conversion and
deletion safeguards remain authoritative. The DTO excludes contact details and
internal notes. No email, notification or other write side effect is introduced.

The Contract information tab displays source Lead links, conversion dates,
loading/retry and a scope-aware empty state only for users with Lead view
permission. Link navigation remains protected by the existing Lead detail API.
Focused verification: `ContractSourceLeadsTests` and
`admin-contract-source-leads.spec.ts`.

Legal-representative assignments through the Customer contact upsert are
serialized per Customer aggregate. An in-process gate covers concurrent
requests handled by one application instance, while a transaction-owned SQL
Server application lock coordinates multiple instances. This keeps the
filtered unique index and the Company invariant aligned: a Company always has
exactly one legal-representative contact when two authorized actors assign
replacements concurrently.

Customer document metadata is stored in `customer_documents`; files are stored under the owning customer's dedicated web-root directory. The endpoints reuse customer owner scoping and existing CRM permissions:

| Method | Route | Permission | Purpose |
|--------|-------|------------|---------|
| `GET` | `/api/customers/{id}/documents` | `crm.customers.view` | List documents for an accessible customer |
| `GET` | `/api/customers/{id}/documents/{documentId}/content` | `crm.customers.view` | Preview or download an accessible document |
| `POST` | `/api/customers/{id}/documents` | `crm.customers.manage` | Upload PDF, Word, Excel, or image files up to 20 MB |
| `DELETE` | `/api/customers/{id}/documents/{documentId}` | `crm.customers.manage` | Delete document metadata and its managed file |

Quote document metadata is stored in `quote_documents`; physical files are stored under `nihomebackend/wwwroot/files/quotes/{quoteId}/` and use the same 20 MB and extension rules. Access follows quote owner scoping, with `crm.quotes.view.all` allowing cross-owner access. Clients retrieve bytes through the authenticated content route; direct requests to `/files/quotes/...` are blocked before static-file middleware:

| Method | Route | Permission | Purpose |
|--------|-------|------------|---------|
| `GET` | `/api/quotes/{id}/documents` | `crm.quotes.view` | List documents for an accessible quote |
| `GET` | `/api/quotes/{id}/documents/{documentId}/content` | `crm.quotes.view` | Preview or download an accessible document |
| `POST` | `/api/quotes/{id}/documents` | `crm.quotes.manage` | Upload PDF, Word, Excel, or image files up to 20 MB |
| `DELETE` | `/api/quotes/{id}/documents/{documentId}` | `crm.quotes.manage` | Delete document metadata and its managed file |

The quote creation picker uses
`GET /api/opportunities?quoteEligibleOnly=true&search=...`. Search and owner
scope are evaluated by the server before paging. The eligibility filter mirrors
quote creation rules: it excludes Lost Opportunities and Won Opportunities
that already reference a winning quote. The picker fetches at most 50 matching
rows per search and separately resolves a URL-preselected Opportunity so a
direct Create quote action remains stable beyond the first page.

Quote delivery uses `GET /api/quotes/{id}/email-preview` (`crm.quotes.send`)
to prefill the customer's primary contact email and a quote-specific subject
and body. The subject/body come from the editable quote template in
`site_settings` (`GET/PUT /api/site-settings/email-templates`, guarded by
`system.settings.view/manage`), with seeded defaults for existing instances.
The HTML template supports `customerName`, `quoteCode`,
`opportunityName`, `quoteLines`, `discountPercent`, `vatPercent`, `grandTotal`,
and `validUntil` tokens. The body must contain `quoteLines` and `grandTotal`;
template updates are limited to 200 subject and 8000 body characters.
The default uses a branded card layout. Quote HTML is restricted to basic
structural tags and safe inline styles; dynamic values are HTML-encoded.
Quote email bodies must use valid safe HTML. The send dialog previews the
rendered message in an isolated iframe, and the submitted body is validated
before SMTP delivery.

Contract renewal/end-date and work package approval defaults use the same
600px NICON card, red header and safe inline HTML styles as quote emails.
`DbSeeder` persists these defaults for new settings and upgrades each missing
or exact legacy body independently on startup. Custom subjects/bodies are
preserved; confirmed contract reminders and queued approval messages are not
rewritten. Reverting the code does not revert a body already saved in settings;
restore its previous HTML through the authorized template settings API if needed.

`GET /api/quotes/{id}/export.xlsx?lang=vi` (`crm.quotes.view`) returns the
localized quotation workbook. Supported languages are `vi`, `en`, `zh`, and
`ja`. The workbook contains typed date and numeric cells for the quote header,
BOQ or unit-cost rows, discount, VAT, and total; draft or pending quotations
are marked preliminary.
`POST /api/quotes/{id}/send` requires `toEmail`, `subject`,
`body`, `languageCode`, and the current `rowVersion`. The server validates the address and
content, permits subject/body edits only with `crm.quotes.manage`, and sends
through the configured SMTP service with the generated `.xlsx` quotation
attached before recording `SentToCustomer`.
The recipient can be changed to another valid address by an authorized sender;
the system does not prove mailbox ownership.
SMTP failure leaves the quote in `Approved`; no email is sent by a status-only
request. A durable email outbox is not yet part of this workflow, so an
external delivery followed by a concurrent status conflict still needs manual
reconciliation.

Contract renewal/end-date emails use a separate durable schedule, not the
internal payment-milestone notifications. Migration
`20261020162316_AddContractReminderEmails` adds `contract_reminders` and four
nullable template columns on `site_settings`; existing data is unchanged.
Rollback is blocked while schedules/history or edited templates exist; export
and explicitly remove that data before an intentional downgrade. Do not remove
a migration already applied to an environment.

| Method | Route | Contract |
| --- | --- | --- |
| GET | `/api/contracts/{id}/reminders` | Existing Contract view scope; newest 100 records |
| POST | `/api/contracts/{id}/reminders/preview` | Contract manage + owner/view-all; `{kind: "Renewal" or "End", reminderDate: "yyyy-MM-dd", contactId?: positive integer}` |
| POST | `/api/contracts/{id}/reminders` | Same write scope; `{previewToken}` confirms the rendered preview |
| POST | `/api/contracts/{id}/reminders/{reminderId}/cancel` | Same write scope; only Scheduled can be cancelled |
| GET/PUT | `/api/site-settings/contract-reminder-templates` | Settings view/manage; `renewalSubject`, `renewalBody`, `endSubject`, `endBody` |

Reminder dates are Vietnam calendar dates, today or later. Preview requires an
Upstream contract in Signed/InProgress/OnHold with an end date and a valid stored
customer-contact email (maximum 254 characters). The primary contact is suggested
first. Preview tokens are protected, bound to actor/contract/recipient/date and
rendered content, and expire in 15 minutes. Scheduling revalidates the context
and templates. Replaying the same accepted token returns its existing record;
only one Scheduled/Sending reminder per contract/type is allowed. A serializable
transaction covers scheduling/cancellation and claiming a due delivery.

Template subjects contain 1–200 characters on one line; HTML bodies contain
1–8,000 characters, use the same safe HTML allowlist as quotation emails, and
must contain `{{contractNumber}}` and `{{endDate}}`. Supported variables are
`{{customerName}}` (customer display name), `{{contractNumber}}` and `{{endDate}}`
(`dd/MM/yyyy`). Unknown/malformed variables are rejected. Body values are HTML
encoded server-side. Rendered subject/body limits are 200/16,000 characters.
Saved schedules retain the approved rendered content when templates change.

`ContractReminderWorker` polls every five minutes, including overdue schedules
after downtime. Before SMTP it rechecks contract eligibility, unchanged customer,
contract number/end date/contact email, scheduler account/permissions/scope, and
pending aggregate deletion. Changed context becomes NeedsReview without sending.
The worker commits Sending before SMTP, then saves Sent and a customer-care Email
entry together after SMTP acceptance. Sent means SMTP acceptance, not inbox
delivery. SMTP errors become NeedsReview/DELIVERY_UNCERTAIN; neither that state nor
Sending is automatically retried. A crash after claiming or SMTP acceptance can
leave Sending: an operator must reconcile SMTP logs before any new delivery.
Stop the sender during manual reconciliation; preserve the original snapshot,
attempt timestamps and audit evidence. Scheduled cancellation never retracts an
email already handed to SMTP. Permanent deletion discloses schedules/history,
invalidates stale plans on state changes, and blocks Sending records.
Contract direction changes also check persisted `ContractReminder.AttemptedAt`:
any non-null attempt locks direction permanently, including Sending or uncertain
delivery. The existing serializable contract update and reminder claim serialize
that boundary; no new schema is required. A rejected direction edit returns
`CONTRACT_REMINDER_DIRECTION_LOCKED` and preserves the row/version/history.

Focused validation: `ContractReminderServiceTests`, `ContractRemindersTests`,
and `e2e/smoke/contract-reminder-email.spec.ts` (live Compose SQL/SMTP, opt in with
`MAIL_SINK_URL=http://127.0.0.1:8025`; no real customer mail). Use headed slow mode
with `PLAYWRIGHT_SLOW_MO=1500` and `--headed --workers=1` for visual review.

The browser-to-SMTP delivery E2E uses an opt-in, non-relaying local mailbox.
`docker-compose-e2e.yaml` overlays `docker-compose.yaml` and adds the
`smtpSink` service, so one file starts the whole stack with the backend email
settings pointed at the mailbox:

```bash
BUILD_SHA=$(git rev-parse HEAD) docker compose \
  -f docker-compose.yaml -f docker-compose-e2e.yaml up -d --build
cd nihomeweb
BASE_URL=http://localhost:5043 MAIL_SINK_URL=http://127.0.0.1:8025 \
  npx playwright test e2e/smoke/direct-contract-from-opportunity.spec.ts \
  e2e/smoke/quote-email-delivery.spec.ts
```

The test uses a unique `example.test` recipient and verifies the captured
message and persisted quote status. Do not use production SMTP settings for
this test. When finished, restore the normal backend configuration with
`docker compose up -d --force-recreate nihomeBackend`; then stop and remove the
test-only mailbox with `docker compose -f docker-compose.yaml -f docker-compose-e2e.yaml stop smtpSink`
and `docker compose -f docker-compose.yaml -f docker-compose-e2e.yaml rm -f smtpSink`.

The CI `e2e` job sets `COMPOSE_FILE=docker-compose.yaml:docker-compose-e2e.yaml` and
`MAIL_SINK_URL=http://127.0.0.1:8025`, so `quote-email-delivery.spec.ts` runs
there instead of skipping, and no customer address is ever mailed from CI.

Each Tender records the invitation receipt time (required and earlier than the
submission deadline), source, receiving user, optional customer reference, and
an invitation portal URL or uploaded invitation documents. Every uploaded file
stores its size, media type, and SHA-256 digest. Each checklist row points to a
current evidence revision while retaining every earlier direct-upload or
capability-library revision. Users with
`crm.tenders.manage` may assign an active user whose active role also has that
permission. A changed assignment emits one `tender.checklist.assigned`
notification with a direct Tender link; an unchanged assignment is idempotent.
Internal deadlines are date-based, may not be before the current date, and may
not be after the Tender submission date. These rules are enforced by the
service as well as the form. Only the `TemplateCode=capability` row may attach
an existing capability document; every other row uses a direct upload. A row
cannot move to `Done` or `Submitted` until its required evidence is present.
`Done` records the preparer; a different user must move each required row to
`Submitted` as its independent verification. The Tender submission gate
revalidates that verification, the evidence type, and the file metadata.
Direct uploads are stored
under `wwwroot/files/tenders/`; capability-library attachments retain their
managed capability path. A capability document or retained version cannot be
deleted while either the current checklist row or its evidence history
references it. Checklist work may continue after the first `Submitted` state
for an explicit supplementary submission; terminal `Won`, `Lost`, and
`Cancelled` Tenders remain locked. The seven standard
rows include tender drawings as optional by default; users may add package-
specific rows, mark rows required by the invitation, or mark an optional row
`NotApplicable` with a reason. Standard rows cannot be deleted. Submission
creates a numbered immutable `TenderSubmission` snapshot containing the exact
evidence revisions and checksums, approved estimate content, completed planning
content, approved Finance review content, submission channel, receipt reference,
actor, and time. Adding replacement evidence resets that row's verification and
does not alter earlier snapshots. The Tender's first `SubmittedAt` is immutable.
The legacy backfill creates a `LegacyUnknown` snapshot only when a successful
`tender.transition` audit proves the historical Submitted transition and uses
that audit timestamp. It does not infer submission from `UpdatedAt`; rows
without auditable evidence remain unbackfilled instead of receiving invented
history.
If a direct upload cannot be attached because the Tender or row is missing,
terminal, persistence fails, or the physical copy is interrupted, the newly
written or partial file is removed.

#### Tender win-rate reporting

`GET /api/tenders/statistics` (also `/api/v1/tenders/statistics`) requires
`crm.tenders.view`, matching the existing cross-record Tender list scope.
`GET /api/tenders/report-customers` and `report-preparers` use the same
permission and return distinct options referenced by Tenders, including
historical preparers, without requiring user-management permission.

Optional `fromDate` and `toDate` must be supplied together as ordered ISO dates
(for example `2026-08-01` and `2026-08-31`), inclusive in Vietnam time (UTC+7).
`fromDate` must be after `0001-01-01`. Optional `customerId`/`preparerUserId`
must be positive integers. Invalid filters return `400`; omitting both dates
reports all periods. The list accepts these filters and `submittedOnly=true`
for paginated reconciliation, preserving its normal behavior when omitted.

`TenderReporting.FirstSubmissions` selects `TenderSubmission.Sequence == 1`
within the UTC half-open period. Count each Tender once, including auditable
`LegacyUnknown` submissions. Rate is `100 * Won / submitted cohort`, rounded
to two decimal places, null when empty. Pending results and cancellations after
submission remain in the denominator; cancellations before submission do not.
List status/search/pagination filters never change the report denominator.
Use current status: older cohorts may change when results arrive. KPI uses the
same submission selector for its employee/period; locked snapshots do not change.

The response has `submittedCount`, `wonCount`, `lostCount`, `pendingCount`,
`cancelledCount`, nullable percentage `winRate`, UTC `calculatedAt`, and
`missingSubmissionCount`. The latter is independent of dates, scoped by customer
and preparer: Submitted/Won/Lost records, or cancelled records with `SubmittedAt`,
lacking a first-submission event cannot be assigned to a period. Exclude and
disclose them; never invent production dates. Marked synthetic demo Tenders have
idempotent `Sample`/`LegacyUnknown` evidence; this does not backfill user records.
No schema migration is required.

Focused checks: `TenderStatisticsControllerTests` (permission, validation,
Vietnam boundaries, resubmission, current results, zero/empty, missing evidence
and drill-down), `SampleCrmDataSeederTests` (demo idempotency), and
`admin-tender-statistics.spec.ts` (browser filters, reconciliation, recovery,
localization and responsive rendering).

Every new Tender requires an existing `OperationalProjectId` belonging to the
same Customer. Tender planning reuses `DepartmentWorkPackage` with
`SourceTenderId`, `Purpose=TenderPlanning`, and one Project work package per
responsible department. Its tasks carry department, assignee, independent
approver, planned dates, predecessors, progress, concurrency and history.
Legacy rows that already have `Purpose=General` and a `SourceTenderId` remain
read/submission compatible; new Tender plans must use `TenderPlanning`. A Tender-plan package
cannot become `Completed` until every task is `Verified`; a Tender cannot become
`Submitted` until at least one non-cancelled plan package exists and every such
package is complete. This is the server-side gate for planning, dossier
preparation and internal review; the Tender checklist remains the authoritative
file/dossier checklist rather than a second planning model.
Updating a Tender plan preserves the required start/end dates, the submission
deadline and every non-cancelled task's date range. Final `Won`, `Lost` and
`Cancelled` results also lock package edits/reopening, task assignment, progress
and verification through the generic work-package APIs. Response capability
flags expose these records as read-only.
The checklist row with `TemplateCode=capability` must reference a
`CapabilityDocumentId`; direct upload is rejected and submission rechecks this
invariant. Other project-specific checklist rows require direct uploads and
reject capability-library references.

| Method | Route | Permission | Purpose |
|--------|-------|------------|---------|
| `GET` | `/api/tenders/assignee-options` | `crm.tenders.manage` | List active users whose active role can manage Tender checklist work |
| `POST` | `/api/tenders/{id}/checklist` | `crm.tenders.manage` | Add a package-specific required or optional dossier row while Preparing |
| `DELETE` | `/api/tenders/{id}/checklist/{itemId}` | `crm.tenders.manage` | Delete an empty package-specific row; standard rows cannot be deleted |
| `PATCH` | `/api/tenders/{id}/checklist/{itemId}` | `crm.tenders.manage` | Update checklist status, owner, or internal deadline while mutable |
| `POST` | `/api/tenders/{id}/checklist/{itemId}/upload` | `crm.tenders.manage` | Upload and replace the row's current file |
| `POST` | `/api/tenders/{id}/checklist/attach-from-library` | `crm.tenders.manage` | Attach an existing capability document to the selected row |
| `GET` | `/api/tenders/{id}/checklist/{itemId}/content` | `crm.tenders.view` | Preview or download the file referenced by that Tender row |
| `POST` | `/api/tenders/{id}/invitation-documents` | `crm.tenders.manage` | Upload a checksummed invitation document |
| `GET` | `/api/tenders/{id}/readiness` | `crm.tenders.view` | Return actionable planning, dossier, estimate, and Finance blockers and record first-achieved milestones |
| `POST` | `/api/tenders/{id}/submit` | `crm.tenders.submit` | Require an idempotency key, channel, concurrency token, and all readiness gates; persist the next immutable submission revision |
| `POST` | `/api/tenders/{id}/transition` (`Submitted`) | `crm.tenders.submit` | Rejected with `400`; all submission clients must use `/submit` so idempotency and concurrency are mandatory |

`POST /api/tenders/{id}/mark-won` (`crm.tenders.mark-result`) takes either
`opportunityId` (an opportunity of the tender's customer) or
`createOpportunity: true`, never both; otherwise it returns `400`. Creating
opens, in the same transaction as the Won result, an opportunity in
`Negotiation` valued at the latest approved estimate revision's
`GrandBidTotal` on the Tender's existing Operational Project. It never creates a
second project root. Contract creation remains an explicit action from the Won
result after negotiation.

Sequential codes (`PJ-`, `TD-`, `SV-`) are the highest numeric suffix in use
plus one (`SequentialCodes`), never a row count, so deleted rows do not cause
collisions. Project codes and lead-conversion projects share the per-year SQL
Server application lock in `OperationalProjectCodeAllocator`; a concurrent
tender insert that still hits the unique index retries with the next code.
`GET /api/operational-projects/code-suggestion` returns the current proposed
operational-project code for authorized creators. `POST /api/operational-projects`
accepts an optional creation-time `code`; a supplied value is normalized to
uppercase, validated against the safe 2–40 character code format and rejected
when already used. Omitting it preserves automatic allocation for integrations
and conversion flows. `PUT /api/operational-projects/{id}` deliberately has no
code field. A later change uses the dedicated
`PATCH /api/operational-projects/{id}/code` endpoint, requires
`operations.projects.rename`, a reason, and the current row version, and writes
immutable old/new-code history in the same transaction. Existing foreign keys
remain stable because modules link by `OperationalProjectId`. When Drive
bindings exist, a durable worker renames the existing project root folder
without replacing its Drive ID; status and failure details are returned in
`latestCodeChange`, and `POST .../{id}/code/drive-retry` safely requeues the
latest failed job.

`GET /api/operational-projects/{id}/team/history` combines team events with
persisted code changes, newest first, under the existing project-view check.
Code entries use `entityType=ProjectCode`, `action=Renamed`, and typed
`oldCode`, `newCode`, `reason` fields, plus the original actor and timestamp.
Entry IDs are scoped by entity type; clients must use both for identity.
Existing code changes appear without backfill or duplicate audit writes.
The shared team panel displays these entries and refreshes after a successful
code change on the Operational Project detail page.

`POST /api/contracts/{id}/link-quote` (`crm.contracts.manage`, idempotent,
concurrency-checked) attaches an `Approved`, `SentToCustomer`, or
`CustomerApproved` quote to an upstream contract that is not cancelled or
completed. The quote must match the contract's customer, opportunity, and
project, the contract must have no source quote yet, and no other live
contract may use the quote; the contract's empty opportunity is filled from
the quote. Creating a contract with a quote, or changing its quote, applies
the same approved-status rule. An `Approved` or `SentToCustomer` quote must
also be within its validity period; `CustomerApproved` is terminal. An
`Upstream` contract may instead be created from an Opportunity with an
already-agreed value and no quote. This is the second confirmed business path,
not a legacy-data exception. A won tender's approved estimate does not replace
an optional CRM customer quotation.
`Downstream` supplier/subcontractor contracts cannot reference a CRM customer
quote.
The quote deletion-impact plan blocks deletion while any contract references
the quote; deleting it must not silently detach a contract's source.
Opportunity detail includes `quoteId` on each linked contract so the Sales UI
does not offer a quote already used by a non-cancelled contract.

Contract creation derives `OwnerUserId` from the selected customer's `OwnerUserId`. An authorized explicit owner takes precedence; if the customer is unassigned, the caller is used as the fallback. Sales users cannot create or move a contract into another salesperson's customer scope. Opportunity and quote references must belong to the selected customer, and a supplied quote must belong to the supplied opportunity.

`GET /api/contracts` and its `/api/v1` alias require
`crm.contracts.view`. The list accepts `status`, `direction`, `type`,
`vendorId`, `ownerUserId`, `customerId`, `operationalProjectId`, `search`,
`signedFrom`, `signedTo`, `endFrom`, `endTo`, `valueMin`, `valueMax`, `page`, `pageSize`, `sortBy`,
and `sortDirection`. Supported sort fields are `signedDate` (default),
`endDate`, `value`, `contractNumber`, and `updatedAt`; an unknown field falls
back to signed date, and direction defaults to descending. Value filters and
the `value` sort use current value (`Value + approved VO deltas`). The response
adds portfolio totals, direction-neutral overdue/due-soon milestone counts,
and each row's next unpaid milestone and scheduled outstanding amount. List and
detail responses expose nullable `sourceTenderId`, `sourceTenderCode`, and
`sourceTenderName` fields when the linked opportunity came from a won tender;
the values are derived from a won Tender's `WonOpportunityId` rather than
duplicated on the Contract record. If legacy data links multiple won tenders to
the same opportunity, the most recently closed tender is returned.
In an
Upstream scope these amounts are receivables; in a Downstream scope they are
payment obligations. `GET /api/contracts/filter-options`
returns complete owner, customer, and Operational Project choices from the
caller's visible contract scope; it is not capped by unrelated list pagination.
`GET /api/contracts/export-data` applies the same filters, sorting, and scope in
one server query so CSV export is not assembled from a changing set of pages.
Callers without
`crm.contracts.view.all` remain owner-scoped. The Accountant role has read-only
`crm.contracts.view.upstream.all` and `crm.contracts.view.downstream.all` access
because Finance must reconcile both receivables and payables across the same
complete Operational Project portfolio exposed by
`operations.projects.view.all`. That project permission and the matching
direction permission are both required before ownership is bypassed. The
bypass applies only when the list explicitly requests `direction=Upstream` or
`direction=Downstream`, or when a read-only detail endpoint resolves to the
matching direction. An unscoped list remains owner-scoped, and neither scoped
permission grants contract mutation rights.

New contracts are always created as `Draft`; create and update reject attempts
to bypass the contract state machine. Status changes must use
`POST /api/contracts/{id}/transition`. Signed and later active states require a
signed date, the planned start cannot precede signing, and the end cannot
precede the start. Payment-schedule updates round-trip milestone IDs and patch
editable schedule fields in place, preserving finance references and milestone
history. Processed milestones and milestones referenced by payment requests
cannot be removed through schedule editing; status, accountant, request time,
and actual payment date remain controlled by the milestone-status endpoint.

The five-minute contract milestone worker emits the one-time
`crm.contract.milestone-due` notification when an unpaid Upstream collection
milestone reaches its due date. It notifies the active contract owner and
responsible accountant, stores `DueNotificationSentAt` atomically, and clears
that marker when the due date changes. Upstream-only classification metadata
omits the downstream vendor catalogue.

The canonical shared Contract registry and all master-data mutations are
available at `/admin/contracts`. Finance exposes two read-only projections of
those same Contract IDs: `/admin/finance/contracts` for Upstream receivables and
`/admin/finance/input-contracts` for Downstream payables. Selecting a Finance
row opens the canonical `/admin/contracts/{id}` record and carries a validated
`returnTo` value so users return to the same Finance filters, sort, and page.
The former Finance `/{id}` URLs redirect to that canonical record for bookmark
compatibility; they no longer render a second detail UI. Header, legal status,
attachments, and VO mutations remain governed by `crm.contracts.manage`.
Financial schedule ownership is separate: `PUT /api/contracts/{id}/milestones`
updates only milestone schedule fields, while
`PATCH /api/contracts/{id}/milestones/{milestoneId}/status` controls the
responsible accountant, request state, and actual collection/payment date.
Either `crm.contracts.manage` or `finance.payments.manage` may call those two
endpoints, but direction and Operational Project visibility are rechecked on
the server. A Finance permission therefore never elevates Contract header
access. Both Finance
views show current value, approved VO
impact, the next unpaid milestone, and
overdue/due-soon summaries with receivable/payable wording appropriate to their
direction. Server pagination and the dedicated server-side export query keep CSV
scope and filters identical to the screen without truncating at the current page
or the 100-row API limit.

Payment Request discovery and workflow are also Operational Project scoped.
`GET /api/finance/payment-references` and `GET /api/finance/payment-requests`
return only projects visible through `IProjectAccessService`; create, update,
submit, validate, decide, pay, and cancel recheck the selected Contract's actual
project before mutation. A scoped finance permission therefore requires active
project membership (or project manager/creator scope), while
`operations.projects.view.all` enables the authorized finance portfolio.
Payment creation may link one approved, active Downstream QS batch to the same
Contract and Operational Project through `ConstructionQsBatchId`. The finance
reference response lists eligible QS batches for accessible Downstream contracts.
The service rechecks the QS direction, CM approval, active lineage, Contract and
project at create/update, validation and approval. An absent QS link remains
valid for advances or other stages whose document rules are still to be agreed;
this link alone does not certify an ISO payment dossier or authorize payment.
The Finance screen uses the authorized `PUT /finance/payment-requests/{id}` for
Draft edits with the current row version and preserves attachment, milestone
and QS selections. Invoice date validation compares the date of `ReceivedAt`
in the `Asia/Ho_Chi_Minh` business timezone, including when the UTC timestamp
falls on the preceding day. A superseded QS is shown as unavailable in the editor.
`POST /finance/payment-requests/{id}/revise` requires `finance.payments.manage`,
a reason, idempotency key and current row version. It applies to submitted,
rejected or approved but unpaid requests. Submitted and approved requests can
be revised when their linked QS is no longer eligible; rejected requests can
be revised to correct their dossier. The operation snapshots the prior
revision and attachments, increments the revision number on the same request,
keeps the invoice identity, clears previous review/approval actors and returns
the current version to Draft. The new Draft must be edited and pass submission,
accounting validation and independent approval again. The pay endpoint checks
QS eligibility inside a serializable transaction and blocks payment when the
linked QS has been superseded. Historical revisions are returned read-only in
the Payment Request response; the vendor/invoice uniqueness rule remains global.
Migration `AddPaymentRequestRevisions` backfills existing requests as revision 1
and refuses rollback after revision history exists, so audit data is not dropped
silently.
For approval, the service also rejects an actor who created, submitted,
validated, or was assigned as accountant on the same Payment Request, even if
that actor has `finance.payments.approve`. This follows the separate maker,
accounting-check, and approval steps in [ISO `TQT` 5.1](<Nicon/ISO/D.Quy trình chi tiết/D.6. Quy trình thanh toán, quyết toán công trình/Quy trình thanh quyết toán.doc>)
and the distinct signature positions on [MH-M04](<Nicon/ISO/D.Quy trình chi tiết/D.7. Quy trình mua hàng, đánh giá nhà cung ứng, thầu phụ- co chinh sua/MH-M04-DE NGHI THANH TOAN.xlsx>).
It does not establish the still-open four-package document matrix or
delegated-approver policy.

#### Contract customer-care history

The customer-care slice implements `Nicon_BreakTask_v1.xlsx`, `Nicon!A62:D62`.
It is distinct from the Contract audit timeline and the Customer-wide activity
history: entries belong to exactly one Upstream Contract and are never inferred
from another Contract belonging to the same Customer.

`GET /api/contracts/{id}/care-activities?page=1&pageSize=20` requires
`crm.contracts.view` and the existing Contract read scope. It returns
`total`, `page`, `pageSize` and `items`, ordered by occurrence descending and ID
descending. Page size is clamped to 1–100. Each item includes Contract ID,
interaction type, content, UTC occurrence time, server-recorded creator and
creation time. Project and Finance read scopes do not grant write authority.

`POST /api/contracts/{id}/care-activities` requires `crm.contracts.manage`,
Contract ownership or `crm.contracts.view.all`, and an `Idempotency-Key`.
The body contains required `type` (`Call`, `Email`, `Meeting`, `Note`), required
`content` (1–4,000 characters, trimmed), and optional `occurredAt` (an ISO
timestamp normalized to UTC; defaults to server UTC now). Other Customer activity
types are rejected. Replays recheck current ownership before returning cached
data. Missing/inaccessible Contracts return 404; Downstream Contracts return
400. The Contract registry exposes an append-only Customer care tab; the
Finance projection remains read-only. Entries do not change Contract terms,
VOs, milestones or payment state, and do not schedule renewal reminders.
As confirmed by the user on 20 October 2026, Contract direction cannot change
once customer-care history exists. The Contract update endpoint enforces this
inside its serializable transaction and returns HTTP 400 with
`CONTRACT_CARE_DIRECTION_LOCKED`; rejected updates preserve the Contract and
its readable history. Adding an entry rechecks direction and owner scope in
a serializable transaction. Contracts without care history retain their
existing reclassification rules.

Migration `AddContractCareActivities` adds an indexed, Contract-owned table.
There is no historical backfill because Customer-wide notes cannot safely be
attributed to one Contract. Permanent deletion discloses and deletes these
entries in the existing aggregate transaction; adding an entry invalidates an
older deletion plan. The Customer and other Contracts' histories are preserved.
Rollback is blocked while the table contains history; export and resolve that
history through an approved data-retention plan before rollback. Sample
`HD-SAMPLE-003` demonstrates owner follow-up for the An Phú factory; other
sample Contracts retain the empty state.

Focused API coverage: `ContractCareActivitiesTests`. Seed and cleanup regression
coverage: `SampleContractSeederTests`, `BusinessRootHardDeleteHandlerTests`.
Browser coverage: `admin-contract-care.spec.ts` (requires the live Compose stack).

#### Contract continuation provenance

User-authorized policy dated 22/10/2026 supports Renewal and Expansion from an
Upstream contract with a signed date and status Signed/InProgress/OnHold/Completed.
This is an implementation decision delegated by the user, not a NICON meeting
confirmation. See the CRM contract in `Nicon-QLVH.md`.

Migration `20261021171128_AddContractContinuations` adds nullable
`SourceContractId`, `ContinuationKind` (20) and `ContinuationReason` (2000) to
Contracts and Opportunities. Foreign keys restrict source deletion. Existing
records remain unlinked; do not infer provenance from names, customer or dates.
Rollback refuses to remove populated relationships: export and explicitly
reconcile them before any intentional downgrade. Rehearse migration against a
copy before production deployment; no production execution is implied here.

Existing create routes accept optional continuation fields plus
`sourceContractRowVersion`. Source requires contracts view and owner/view-all
scope independently of opportunity permissions. An opportunity-derived contract
also requires opportunity view and owner/view-all scope. Kind is Renewal or
Expansion, reason is trimmed and required (1–2000 characters), and source is
same-customer. A quote-only create resolves the source opportunity too. Existing
quote approval, exclusivity, project and customer checks still apply. The new
contract inherits provenance from a continuation opportunity and always starts
Draft; Lost pursuits cannot generate a contract. No signed evidence, payment,
VO, QS, care or reminder records are copied. Provenance cannot be changed in
update/link-quote paths. Relations also prevent changing contract customer or
direction and prevent changing a continuation opportunity's customer.

Continuation creates require `Idempotency-Key`. Serializable transactions
protect source-version/eligibility/deletion checks and persistence. The create
idempotency guard rechecks source visibility before replay. The browser retains
the key for an unchanged payload after an uncertain request failure; changed
payloads use a new key. Creating a second pursuit intentionally uses a new key.
Closing the create dialog consumes its initiation query parameters. The source
form opens before source loading completes, exposes a retry on failure, and
supports explicit source-version refresh without replacing entered deal terms.
Validation errors are announced and focused. The reason uses a multiline field.

`GET /api/contracts/{id}/continuations` requires contracts view and owner/view-all
scope. It returns accessible source/successor contracts and successor
opportunities, with each module's permission and ownership checked separately,
newest 100 per collection, plus supported kinds. It does not disclose hidden
record names or totals. Negotiation/loss uses the existing opportunity lifecycle.
Contract deletion impact includes both directions of contract provenance and
outgoing continuation opportunities as Block. Continuation opportunity deletion
is also Block; plan fingerprints include the new edges. No related root is
cascaded or silently unlinked. The demo seeds An Phú's expansion pursuit and
independent draft `HD-SAMPLE-CONT-001` without modifying existing user records.
The unique draft contract number identifies this seeded pair on reruns; pursuit
names are neither unique keys nor evidence of seed ownership. Renaming the
seeded pursuit or creating another pursuit with the same name must not recreate
the pair or stop startup. New pairs are persisted together in one SaveChanges,
after the source's operational-project relationships have been linked.

Focused regression: `ContractContinuationsTests`, `ContractsControllerTests`,
`ContractQuoteLinkTests`, `OpportunitiesControllerTests`, and the live Compose
browser spec `contract-continuations.spec.ts`.
The browser spec covers 390/768/1440px, cancel/reopen, keyboard navigation,
source-data failure/retry on an already loaded route, stale-source recovery,
money formatting, long reasons and vi/en/zh/ja rendering. It does not establish
offline availability for uncached application bundles.

### 7.9 Operational Business Documents

Permit, procurement vendor, partial acceptance, as-built dossier, and project handover forms support local document selection in addition to their existing external URL fields. Managed files are stored under `wwwroot/files/business-documents/{area}/` with generated names. Each file is limited to 20 MB and must use `.pdf`, `.doc`, `.docx`, `.xls`, `.xlsx`, `.png`, `.jpg`, or `.jpeg`.

| Method | Route | Permission | Purpose |
|--------|-------|------------|---------|
| `POST` | `/api/business-documents/vendors` | `proc.vendors.manage` | Upload a vendor capability document |
| `POST` | `/api/business-documents/acceptance` | `construction.acceptance.manage` | Upload a partial-acceptance document |
| `POST` | `/api/business-documents/as-built` | `construction.asbuilt.manage` | Upload an as-built dossier document |
| `POST` | `/api/business-documents/handover` | `construction.handover.manage` | Upload a project-handover document |
| `POST` | `/api/permits/{id}/documents/{kind}` | `permit.checklists.manage` | Upload and assign `SubmittedPackage` or `IssuedPermit` to a permit |
| `GET` | `/api/vendors/{id}/capability-file/content` | `proc.vendors.view` | Read the managed file referenced by a persisted vendor |
| `GET` | `/api/acceptance-records/{id}/documents/{fileName}/content` | `construction.acceptance.view` | Read a referenced document within the record's project scope |
| `GET` | `/api/as-built-documents/{id}/content` | `construction.asbuilt.view` | Read the managed file referenced by a persisted dossier |
| `GET` | `/api/handover-records/{id}/documents/{fileName}/content` | `construction.handover.view` | Read a referenced document within the record's project scope |
| `GET` | `/api/permits/{id}/documents/{fileName}/content` | `permit.checklists.view` | Read a managed submitted or issued permit document |

The existing field cardinality remains authoritative: vendor and as-built records store one path, acceptance and handover records store up to 20 paths, and permits store one submitted-package path plus one issued-permit path. Managed content is never served by a generic area-and-filename endpoint. A content request must identify a persisted resource whose metadata exactly references the managed host-relative path; Acceptance and Handover additionally reuse their established caller/project visibility rules. External HTTP(S) document URLs remain supported and are never treated as managed files.

Direct static requests under `/files/quotes`, `/files/customers`, `/files/contracts`, `/files/capability`, `/files/tenders`, `/files/business-documents`, and `/files/design` return `404` before static-file middleware. The React client obtains private bytes through authenticated resource routes and Blob responses. Newly staged two-step uploads are therefore unavailable until their metadata is saved; edit forms do not offer a managed preview until the current record references that path.

Replacing or removing persisted Contract, Design, Vendor, Acceptance, As-Built, Handover, and Permit references deletes the previous managed file only after successful database persistence. Basic Design and Detail Design uploads also enforce the parent project's active stage in the backend service. The orphan-file worker reconciles cancelled or abandoned two-step uploads every six hours. It covers `/images/upload`, `/images/processes`, and all subdirectories under `/files`; hard-delete quarantine is excluded because the durable hard-delete worker owns it. An unreferenced local file older than 24 hours is first moved to the non-public `.managed-file-quarantine` directory. The worker restores it if an active reference appears and permanently purges it only after another 24 hours without a reference. Reference discovery is repeated immediately before mutation and after quarantine moves; malformed active JSON, failed reference validation, or symlink/reparse-point paths stop destructive processing. Allowed extensions and the 20 MB limit are enforced, but malware scanning and full file-signature validation remain deployment hardening work.

Customer, quote, and contract files keep their dedicated document workflows. Design-project document upload is tracked separately. Site diaries and punch lists are excluded because they do not currently expose a complete persisted document contract. Every Survey requires an Operational Project, and its media is synchronized only through that project's catalog and Drive worker.

### 7.10 Central Operational Project API

`OperationalProject` is the internal aggregate shared across the NICON modules;
it must not be confused with public portfolio content or the three-phase
`DesignProject`. The API is available at `/api/operational-projects` and
`/api/v1/operational-projects`:

| Method | Path | Permission | Purpose |
|---|---|---|---|
| `GET` | `/api/operational-projects` | `operations.projects.view` | List the caller's project scope with filters and rollup counts |
| `GET` | `/api/operational-projects/{id}` | `operations.projects.view` | Read the customer, opportunity, quote, contract, and design rollup |
| `GET` | `/api/operational-projects/{id}/timeline` | `operations.projects.view` | Read payment milestones from every Contract in the caller's project scope |
| `GET` | `/api/operational-projects/document-categories` | `operations.projects.view` | Read the server-defined upload categories and Drive paths |
| `GET` | `/api/operational-projects/{id}/documents` | `operations.projects.view` | List the caller's project document catalog |
| `GET` | `/api/operational-projects/{id}/documents/{documentId}/content` | `operations.projects.view` | Download private document content through project scope |
| `POST` | `/api/operational-projects/{id}/documents` | `operations.projects.manage` | Upload a private project document of at most 100 MiB |
| `POST` | `/api/operational-projects/{id}/documents/{documentId}/retry` | `operations.projects.manage` | Retry an eligible pending synchronization attempt |
| `POST` | `/api/operational-projects/{id}/documents/{documentId}/classify` | `operations.projects.manage` | Classify an unclassified Drive import |
| `POST` | `/api/operational-projects/{id}/documents/{documentId}/resolve-conflict` | `operations.projects.manage` | Confirm that both concurrent versions must be retained |
| `DELETE` | `/api/operational-projects/{id}/documents/{documentId}` | `operations.projects.manage` | Queue deletion of a manual upload or Drive import |
| `POST` | `/api/operational-projects` | `operations.projects.manage` | Create a planning project with a generated `PJ-YYYY-NNNN` code |
| `PUT` | `/api/operational-projects/{id}` | `operations.projects.manage` | Update metadata or perform an allowed lifecycle transition |
| `PATCH` | `/api/operational-projects/{id}/code` | `operations.projects.rename` | Change a project code with reason, row-version concurrency, history, and durable Drive rename status |
| `POST` | `/api/operational-projects/{id}/code/drive-retry` | `operations.projects.rename` | Requeue the latest failed Drive folder rename without creating a new folder |
| `DELETE` | `/api/operational-projects/{id}` | `operations.projects.manage` | Delete an empty Planning project only |

The pre-contract Design handoff is
`POST /api/opportunities/{id}/design-project` with a required design project
`name` (1–300 characters) and optional `note` (up to 4,000 characters). It requires both
`crm.opportunities.manage` and `operations.projects.manage`, applies the normal
Opportunity owner/view-all scope, rejects Lost Opportunities and Opportunities
without an Operational Project, and creates a Concept-stage Design Project with
no Contract. The unique Operational Project relationship makes retries and
concurrent clicks idempotent; the response includes `created` and the resulting
`designProject`. The Opportunity create/edit forms expose the optional
`operationalProjectId`; the Design form links a selected project through the
concurrency-protected Opportunity update before calling the handoff. Opportunity
responses expose `designProjectId` and
`designProjectCode` so the UI replaces the create action with the existing-state
action.

Operational Projects do not expose aggregate archive or restore operations.
The approved DEC-08 lifecycle decision retains the preview-and-confirm hard-delete
convention as the only aggregate removal flow; `Completed` and `Cancelled` retain
business history without hiding it as archived data. Document-level archive
states, such as the As-Built lifecycle, remain independent and do not archive the
owning Operational Project, its Contracts, audit records, or managed files.

`operations.projects.view.all` removes owner scope but does not grant mutation
permission. Update and delete requests use `rowversion`; the detail response
also emits an ETag. `AddOperationalProjects` backfills existing design,
contract, opportunity, and quote relationships before adding their foreign
keys. The operational hierarchy and user workflow are documented in
`docs/user_guide.md`. The deterministic historical reconciliation, dry-run,
deployment checks, rollback plan, and API compatibility contract are documented
in [Operational Project Historical Migration](#67-operational-project-historical-migration).

### 7.10.1 Department work-package API

`DepartmentWorkPackage` is the cross-department assignment aggregate. Project
packages require an `OperationalProjectId`. General packages can target any
active department (product-owner decision, 29 October 2026). Design and
Construction keep their specialist aggregates for specialist records; a general
package does not create a duplicate project wrapper. Their `ContractId` and
`SourceOpportunityId` are optional references. Internal packages reject all
three business-root references. Endpoints are available at `/api/work-packages`
and `/api/v1/work-packages`:

The input-price editor uses persistent, associated labels and a responsive
five-column desktop layout with stacked mobile fields. The dialog body scrolls
independently of its header/footer. Its read-only line amount uses the existing
pre-VAT quantity × unit-price display; the server remains authoritative for
persisted decimal rounding and tax totals. Numeric ranges match
`InputPriceLineRequest`; invalid values are identified next to the field.
`MoneyInput` accepts an optional formatter and decimal-comma preference for
these localized fields. Existing callers retain the default currency parsing.
`quote-pricing-responsive-live.spec.ts` covers real offer persistence and layout
at 320/390/768/1440px. Its sent-quotation rendering fixture sets only its own
test quote to `SentToCustomer` in SQL; it does not test email delivery.

| Method | Path | Permission | Purpose |
|---|---|---|---|
| `GET` | `/api/work-packages` | `work.packages.view` | List scoped project/internal packages with server filters |
| `GET` | `/api/work-packages/options` | `work.packages.view` + `manage` or `assign` | Return active department, user, and work-KPI options |
| `GET` | `/api/work-packages/{id}` | `work.packages.view` | Read package, tasks, access flags, and ETag |
| `GET` | `/api/work-packages/{id}/history` | `work.packages.view` | Read immutable package/task change snapshots |
| `POST` | `/api/work-packages` | `work.packages.manage` | Create a project or internal package |
| `PUT` | `/api/work-packages/{id}` | `work.packages.manage` | Update metadata, optional contract, or lifecycle |
| `GET` | `/api/work-packages/{id}/deletion-impact` | `work.packages.manage` + management scope | Preview deletion, unlinking and blockers |
| `DELETE` | `/api/work-packages/{id}` | Same management scope | Hard delete with code, planToken, rowVersion and idempotency key |
| `POST` | `/api/work-packages/{id}/tasks` | `work.packages.assign` | Add a department task with WBS, dependencies, CC, and optional KPI |
| `PUT` | `/api/work-packages/{id}/tasks/{taskId}` | `work.packages.assign` | Edit a non-terminal assignment |
| `PUT` | `/api/work-packages/{id}/tasks/{taskId}/progress` | `work.packages.progress` | Record monotonic assignee progress |
| `POST` | `/api/work-packages/{id}/tasks/{taskId}/verify` | `work.packages.verify` | Independently verify a completed KPI task |
| `POST` | `/api/work-packages/{id}/input-price-offers` | `work.packages.progress` | Add a supplier/subcontractor input price offer to a Procurement pricing package |
| `PUT` | `/api/work-packages/{id}/input-price-offers/{offerId}` | `work.packages.progress` | Edit a Draft/Rejected input price offer |
| `POST` | `/api/work-packages/{id}/input-price-offers/{offerId}/approval-email-preview` | `work.packages.progress` + department scope | Render the approval email and issue a 15-minute confirmation token |
| `POST` | `/api/work-packages/{id}/finance-review/approval-email-preview` | Same permission and scope | Render the finance approval email |
| `POST` | `/api/work-packages/{id}/input-price-offers/{offerId}/submit` | `work.packages.progress` | Submit an input price offer for independent approval |
| `POST` | `/api/work-packages/{id}/input-price-offers/{offerId}/decision` | `work.packages.verify` | Approve or reject a submitted input price offer |
| `PUT` | `/api/work-packages/{id}/finance-review` | `work.packages.progress` | Save the Finance – Accounting checklist (commercial or tender review) |
| `POST` | `/api/work-packages/{id}/finance-review/submit` | `work.packages.progress` | Submit the finance review for independent approval |
| `POST` | `/api/work-packages/{id}/finance-review/decision` | `work.packages.verify` | Approve or reject a submitted finance review |

Approval notification email contract (user decision, 25 October 2026): both
input-price and finance-review submissions require a server-rendered preview.
POST the corresponding `approval-email-preview` endpoint, display `toEmail`,
`subject` and sandboxed HTML `body`, then submit `previewToken` with the existing
`rowVersion` and `Idempotency-Key`. Old clients must add this preview step.
The token is bound to the submitting user, package context, record/version,
recipient list (To/CC) and rendered template; it expires after 15 minutes. Changed content
requires a new preview. A repeated submission of an already Submitted record
returns the record without enqueuing another email.

The assigned department lead must be active, independent of the record author,
have `work.packages.verify` and a valid email per `ContactValidation`; otherwise
submission is blocked without a state transition. Template editing requires
`system.settings.manage`, separate from submission permission. GET/PUT
`/api/site-settings/work-package-approval-template` uses settings view/manage.
The editor is the **Duyệt nhóm công việc** tab beside **Báo giá** and the
contract reminder tab at `/admin/email-templates`.

Supported variables: `recipientName` (lead), `workPackageCode`,
`workPackageName`, `recordType`, `recordReference` (supplier reference for
input pricing; package code for finance), and `senderName`. Use
`{{variableName}}`; the HTML must include `{{workPackageCode}}` and
`{{recordReference}}`. Subject: 1–200 characters, no newlines. HTML: 1–8,000
characters, the shared safe basic HTML/style allowlist, no unknown variables.
Rendered subjects retain the 200-character limit; bodies allow up to 16,000.
Dynamic body values are HTML-encoded on the server. Confirmed content is frozen.

Migration `20261024174814_AddWorkPackageApprovalEmails` adds nullable settings
columns and the owned `work_package_approval_emails` table. Submission, queued
email and business history commit in one transaction. The 15-second worker
claims before SMTP and checks the current recipient and submission generation.
Delivery states: Pending, Sending, Sent, NeedsReview. Package detail returns
`approvalEmails` (latest 50), allowing refresh to inspect delivery. Sending
blocks permanent deletion; other email records are disclosed as owned deletes.
An SMTP failure or context change is visible as NeedsReview. A crash after claim
can leave Sending: inspect SMTP logs before any manual recovery. There is no
automatic retry after a delivery attempt, since SMTP cannot prove exactly-once
delivery after a disconnect. Do not reset Sending/NeedsReview blindly.
Existing Submitted records are not retroactively mailed.

Recipient selection (confirmed 25 October 2026): preview POST accepts optional
`{ toEmail, ccEmails }`. An empty request defaults To to the assigned lead and
CC to an empty array. To may be edited; this changes delivery only, not the
approval actor or independent-lead eligibility. Every address is required when
present, trimmed, limited to 150 characters, checked by ContactValidation and
parsed as one mailbox. CC permits up to 20 addresses; duplicates (case-insensitive)
and addresses equal to To are removed. The server returns the normalized list
and binds it into previewToken. Submission cannot override the token recipients.
After editing To/CC, update the preview before confirming.
The editable approval-email body must include a link using
`<a href="{{workPackageUrl}}">...</a>` alongside `{{workPackageCode}}` and
`{{recordReference}}`. Preview and queued To/CC email share the rendered
`/admin/work-packages/{id}` URL. Set `Frontend:PublicBaseUrl` to the public
HTTPS origin in deployed environments; localhost HTTP is accepted for local
Compose. Without this setting, only a configured allowed frontend origin or
a loopback request origin can supply the link. Older customized templates
receive the link in memory when loaded, preserving their original content.

Migration `20261024183305_AddWorkPackageEmailRecipients` adds CcEmailsJson
(default `[]`) and LeadEmail (backfilled from historical ToEmail). The worker
rechecks the independent lead against LeadEmail, not the editable To address,
and sends one SMTP message with To and CC headers/envelope recipients. Detail
responses include ccEmails. The page shows a persistent queued confirmation and
polls delivery for up to two minutes, showing Sent/NeedsReview with a toast and
inline feedback. This does not equate SMTP acceptance with inbox delivery.
Rollback of this migration loses CC and lead snapshots: stop the worker, export
pending recipient lists and review them before deploying the old sender.

Rollback: stop the approval worker before deploying the previous assembly.
Downgrading the migration drops queued messages/delivery history and the new
custom template fields, so export this evidence first. Existing quotation and
contract reminder templates are unaffected. Never roll back by dropping volumes.

Completed packages can return to `Active` through PUT. This clears package
`CompletedAt` and records a `Reopened` history snapshot. Existing task progress,
KPI verification and approved business records remain intact; their individual
edit guards still apply. Cancelled packages remain terminal.

Package deletion recomputes its plan in a serializable transaction. It deletes
owned tasks, CC/dependencies, participants, history, input offers/lines, finance
reviews and translations. Independent permit/tender checklist records survive
with task links cleared; parent Project, Contract, Opportunity, Quote and Tender
records survive. Used input pricing (including quotation version snapshots),
sent quotations consuming commercial review, tender submissions using finance
approval/planning, contract legal activations and
remaining source files block deletion. Files must be cleaned in their owning
module; this API deletes no local or Drive content. Cleaned document metadata
survives with source entity/id cleared. Wrong code, missing/stale rowVersion,
changed plans and blockers reject without partial writes. Success returns 204.
No schema migration is required.

All writes require an idempotency key. Updates, progress, and verification also
require `rowversion` through `If-Match` or the request body. Contextual actor
rules are enforced in the service in addition to permission middleware. Task
dependencies must remain within one package and acyclic; an Active package and
completed predecessors are required before progress begins.
Internal package creation requires both `work.packages.manage` and
`work.packages.assign`, and the caller must be the selected lead in the same
role group. This prevents a view/progress-only staff role from promoting itself
to department lead.

Internal packages persist all participating role groups. Every task persists
one responsible role group and validates its assignee and optional approver
against that group. Package/task mutation and its history snapshot commit in
one database transaction; notification delivery occurs after commit and
remains retry-safe through the notification service.

The migration `AddDepartmentWorkPackages` adds package, task, dependency, CC,
and history tables. Operational Project deletion classifies owned packages as
`Delete`; Contract and Opportunity deletion classify their optional references
as `Unlink`. KPI metric `VerifiedWorkTaskCount` reads only independently
verified tasks assigned to the evaluated user in the requested period. Seeded
definitions are inactive by default so installations must configure targets and
rebalance weights before activation.

#### Pre-contract department controls (MoM §5)

The `DepartmentWorkPackage.Purpose` discriminator selects the pre-contract
business flow. Each purpose is bound to
one receiving department and one source record so the cross-department handoff is
auditable:

- `ProcurementInputPricing` requires the `PROCUREMENT` role group and a
  `SourceOpportunityId`. It carries `ProcurementInputPriceOffer` records with
  lines, vendor, supplier reference, currency and validity.
- `FinanceCommercialReview` requires the `FINANCE` role group and a
  `SourceQuoteId`; `FinanceTenderReview` requires the `FINANCE` role group and a
  `SourceTenderId`.
- `TenderPlanning` requires `SourceTenderId`, the Tender's Operational Project,
  a responsible active role group, and package dates no later than the submission
  deadline. Unlike generic project packages it may coordinate any department;
  it does not replace that department's specialist aggregate. One non-cancelled
  package per Tender and department is allowed. Every task requires an independent
  verifier and must be `Verified` before the package can complete; KPI linkage is
  optional for these planning tasks.
- All pre-contract purposes reject `ContractId`: pre-contract work must not hang off an
  executed contract.

`ProcurementInputPriceOffer` and `DepartmentFinanceReview` share one lifecycle:
`Draft → Submitted → Approved | Rejected`. Editing is limited to `Draft` and
`Rejected`; the author cannot decide their own record
(`CreatedByUserId == callerUserId` is rejected server-side), an input price offer
already referenced by a quote is immutable, and a rejected record requires a
reason. Offers and reviews are returned with `rowVersion`, and all writes require
an idempotency key plus `If-Match`/body `rowVersion`.

Server-side gates that consume these records:

- `QuoteService` send-to-customer requires an `Approved` `FinanceCommercialReview`
  package linked by `SourceQuoteId`, and the quote must carry an
  `OperationalProjectId` so the review has a project to live under. The review
  pins `ReviewedQuoteVersion` when Finance saves it, so a quote re-versioned
  after a material post-approval edit needs a fresh review before it can be sent.
  Finance review responses, including reviews nested in `GET /api/work-packages`
  and `GET /api/work-packages/{id}`, expose nullable `reviewedQuoteVersion`.
  The quotation screen filters packages by `sourceQuoteId` and uses that value
  to open a reusable review or offer a new request for a changed quotation.
- `QuoteService` resolves `inputPriceOfferId` only when the offer is `Approved`,
  still valid, and belongs to a `ProcurementInputPricing` package of the quote's
  project; the quote stores `InputPriceSnapshotJson` at link time.
- `POST /api/quotes` accepts `draftOnly: true` for the current Sales creation
  flow. The Opportunity must belong to an Operational Project, and the payload
  contains only basic quote fields and omits `method`; the persisted method
  uses an internal default until output pricing is prepared. It creates a
  zero-value Draft. On its first pricing update, `PUT /api/quotes/{id}` requires
  a selected `method` and an approved, unexpired input offer on the same
  project; normal quote validation and the existing Draft to
  PendingApproval submit gate then apply. The older priced-create contract is
  retained for existing integrations and historical tests.
- `TenderService` submission requires an `OperationalProjectId`, at least one
  completed `TenderPlanning` package, and an `Approved` `FinanceTenderReview`
  package linked by `SourceTenderId`. Legacy Tender plans stored as `General`
  remain readable by the gate, but new writes must use `TenderPlanning`.
- `ProcurementService` execution BOQ creation/update requires a same-project
  `Upstream` contract that is signed and not draft/cancelled.

The migration `AddPrecontractDepartmentControls` adds the offer, line and review
tables plus the `Purpose` discriminator; every added column is nullable except
`department_work_packages.Purpose`, which defaults to `General`.
`AddFinanceReviewQuoteVersion` adds the nullable
`department_finance_reviews.ReviewedQuoteVersion` used for the version pin.

The Module 2 schedule is exposed beneath
`/api/operational-projects/{id}/design-schedule` and remains separate from
Module 4 construction tasks. Its lifecycle, validation, weighted roll-up,
filter, concurrency, migration, and deletion contracts are documented in
[Detail Design Schedule](#716-detail-design-schedule).

The aggregate migration is incomplete by design. Construction Task, Site Diary,
Punch Item, Acceptance, As-Built, Handover and Permit still persist
`DesignProjectId`; HSE, Procurement, RFQ, Finance and project documents persist
`OperationalProjectId`. New cross-module aggregates must use
`OperationalProjectId`. Existing Design-Project-based APIs remain compatibility
surfaces and must resolve a single owning Operational Project before they emit
cross-module events. Do not add a nullable second project key without a data
backfill, uniqueness rule, conflict policy, deployment rehearsal and rollback.

The read-only timeline endpoint derives its entries from existing Contract
payment milestones and does not copy or synchronize data. Each entry identifies
its Contract, source, status, planned due date, latest update time, and amount.
`ContractPaymentMilestone.ActualPaymentDate` stores the user-confirmed business
date and is exposed as `actualDate`; the endpoint never substitutes `UpdatedAt`
or retention-managed audit data. A `Paid` write requires this date, while moving
to `Pending` or `Requested` clears it. Existing rows remain nullable because the
migration intentionally does not invent or backfill historical payment dates.
Calls are naturally idempotent, and projects outside the caller's scope return
`404` without disclosing their existence.

`ProjectDocument` is the project-scoped catalog and synchronization sidecar.
Manual catalog uploads enter private host storage and the durable sync queue.
SQL stores catalog, permission, workflow and Drive metadata. Authenticated
content requests serve the staged host file until upload succeeds, then proxy
Drive bytes after verified local-copy cleanup. OAuth credentials and raw Drive
permissions are not exposed to the browser. Source-owned files retain their
existing module storage and lifecycle as a compatibility bridge. Quote,
Contract attachment/appendix, Basic Design, Shop
Drawing, Permit, Acceptance, As-Built, Handover, and Operational
Project-linked Survey writes stage sidecars in the same database transaction as
their authoritative record and use the dedicated Survey category mapped to the
configured `SurveyMedia` folder path. Such source-owned catalog rows cannot be deleted
through the generic endpoint; users must remove or replace the file in its
source module. The Survey folder correction migration reclassifies existing managed
Survey sidecars, but no migration discovers or stages historical files that never had a sidecar.

The current implementation is a supported hybrid subset, not completion of the
expanded customer-wide Google Drive contract. Validate live upload, download,
reconciliation, ownership and cleanup against the configured environment before
claiming external transport works; metadata or mocked tests are insufficient.

Design workspace persistence is also the durable folder-initialization intent:
the Drive worker selects workspaces with missing Concept/Basic/Shop bindings,
five projects per pass, rotating its cursor past failures. After reaching the
end it wraps; a restart rediscovers missing bindings from SQL. No Drive call
occurs inside the handoff transaction and no file/contract is required. Existing
folder identities, configured paths and bindings are reused; initialization
does not migrate Survey/CRM topology. Disabled/unwritable Drive pauses work.
Failures are logged by project and missing bindings remain retryable. The
cursor is an optimization; SQL workspace/binding state determines completion.

Catalog list/download now admit active, started project members through a
shared filtered query. Category permissions follow the owning modules:
Concept/Basic/Shop use their corresponding `design.*.view`, permits use
`permit.checklists.view`, construction evidence uses Acceptance/QA-QC/QS view,
Survey uses `crm.surveys.view`, pre-design uses `crm.opportunities.view`,
Procurement uses material/resource-request view, Finance uses
`finance.payments.view`. Source-module permission is also required, except
General which relies on category permission. Unclassified files remain outside
member-only reads. Creator/PM/view-all behavior is preserved; membership does
not widen mutation authorization. List and binary download apply the same
predicate, including after assignment ends.

Internal Design recipient eligibility uses `design.reviews.receive` on an
active user/role, including custom roles; assignment to an individual review
still controls access. Defaults and four-language labels are seeded through
the normal RBAC catalog. Download UI parses bounded JSON error Blobs using
the shared asynchronous helper so integrity conflicts retain server guidance.
Pure helper regression tests run with `cd nihomeweb && npm run test:unit`.

Both .NET test projects exclude `**/TestResults/**` from default MSBuild items.
Generated `.cs` probes retained as test artifacts must not become compiled
tests or inputs to solution format verification. Keep executable regressions
under the normal tracked test directories.

New tender submissions retain versioned JSON snapshots of plan metadata and
task WBS/content/resources/actors/dates/progress/verification/CC/dependencies,
plus Finance checklist/findings/decision/actors and concurrency versions.
Snapshot fields are additive to existing submission JSON; old sparse snapshots
remain historical evidence and are not backfilled using current source data.

Relationship changes are reconciled only on an explicit update that changes the
resolved Operational Project. Linking or reassigning an Opportunity, Contract,
or Design Project stages its currently supported source files in the destination
and queues old replicas for deletion; a missing `OperationalProjectId` in those
update contracts preserves the existing relationship. Survey updates support
explicit Opportunity unlinking through `LinkedOpportunityId = null`, but every
Survey retains a required `OperationalProjectId`. Project reassignment queues
old replicas for deletion and stages media in the destination project's Survey
category. This event-driven behavior does not scan unrelated source records.

Manual catalog uploads first persist their validated bytes in private host
storage and a durable `Pending` record, including when Drive is disabled or
unavailable. They remain downloadable through the authorized content endpoint.
The worker pauses without consuming attempts until a writable Drive connection
exists. After uploading a manual file, it reads the remote content back and
compares byte count and SHA-256 before marking synchronization successful. The
fenced remote receipt is retained before readback so a failed verification can
still be followed by deletion of the remote replica. Downloads continue serving
the staged source while verification is pending. After successful verification
and synchronization, it removes the owned host copy and clears its path. Failed
cleanup is retried from the synced record; mismatched content retains the
original host file. Legacy source-managed files retain their source-module copy
and lifecycle.
Manual retry resets the bounded automatic retry budget for a failed/pending
record; processing records and protected/frozen evidence remain guarded.

The worker uses durable desired operations, bounded retries, claim tokens,
generation fencing, SQL rowversion, and per-folder reconciliation leases.
Automatic reconciliation selects up to ten due folder bindings per pass,
ordered by their oldest `UpdatedAt` touch then ID, excluding active leases.
Lease acquisition/release updates that touch even after a remote failure,
so failed folders rotate behind waiting folders instead of repeatedly taking
the first ten project slots. Successful folders retain the one-minute scan
interval. Explicit project reconciliation still visits all its bindings.
This changes scheduling only; category topology, permissions, file ownership
and the existing per-folder lease/concurrency rules remain unchanged.
Drive is authoritative for successfully synchronized manual catalog uploads
and files created directly in managed folders: reconciliation catalogs them in
their current category without downloading host copies, reflects remote edits,
and marks remotely trashed files deleted. Native Google Workspace files remain
metadata/link entries because Drive does not expose their native bytes through
the normal download operation. For legacy source-owned sidecars only, external
deletion queues restoration and concurrent remote edits preserve a separate
conflict entry until an authorized user confirms **Keep both**.

### 7.10.2 BOQ catalog workbook intake

Multi-sheet customer workbooks are normalized into Draft BOQ material-rate
revisions through `/api/material-rate-catalogs/{catalogId}/revisions/{revisionId}/boq-workbook`.
`preview` detects candidate header/column mappings without persistence. `import`
requires the same SHA-256 source hash and explicit per-sheet mappings, validates
all selected rows, and atomically replaces the revision lines. Reusable mapping
templates are listed and created at `/api/material-rate-catalogs/boq-import-templates`.
Only callers with `crm.material-rates.manage` may preview, save templates, or
import, and only BOQ revisions in `Draft` are mutable. Source filename, hash,
mapping JSON, actor, and timestamp are retained on the revision. Quotes continue
to consume only an approved effective catalog revision.

### 7.10.3 Configured BOQ category entry

Use the existing MaterialRateCatalog with CatalogType=Boq as the category.
BoqConfigurationJson stores immutable-after-first-revision sheet/field settings;
BoqRowsJson stores all typed entry values per revision. Priced entries project
into existing MaterialRateLine records in the same transaction; reference sheets
never become quote prices. Legacy catalogs have null configuration and retain
existing behavior. Six source-derived profiles are seeded only when their code
is absent; restart never overwrites user settings or creates pricing revisions.

Routes below are relative to /api/material-rate-catalogs (also /api/v1):

| Method / route | Permission | Contract |
|---|---|---|
| PUT {id}/boq-configuration | crm.material-rates.manage | configuration + expectedUpdatedAt; rejects any existing revision |
| GET {id}/configured-boq-template?language=vi | crm.material-rates.view | Localized .xlsx, hidden catalog/hash/language marker, row 1 keys, row 2 labels, data from row 3 |
| POST {id}/configured-boq-preview | crm.material-rates.manage | Multipart file; returns sourceHash/configurationHash/validated rows without persistence |
| PUT {id}/revisions/{revisionId}/configured-boq-rows | crm.material-rates.manage | rows + configurationHash + expectedUpdatedAt; Draft only |
| POST {id}/revisions/{revisionId}/configured-boq-import | crm.material-rates.manage | Multipart file + sourceHash + expectedUpdatedAt; revalidates, confirms replacement atomically |

Schema limits: version 1; 1–40 sheets; each sheet 2–40 fields; lowercase stable
keys (sheet <=20, field <=40); four nonblank names <=120 characters. Text values
are trimmed, capped by MaxLength (1–2000); core code <=38, name <=300, unit <=30.
Number values are invariant decimal strings in JSON, nonnegative, <=999999999999,
with 0–4 decimal places (unit price <=2). Quantity of priced rows is positive;
code matches letters/digits/dot/dash/underscore. A priced sheet must bind exactly
one required Code/Name/Unit/Quantity/UnitPrice; reference sheets require Code/Name
and cannot bind pricing quantity/unit price. Unknown fields/sheets are rejected.
These rules are enforced by BoqConfigurationRules and the canonical line validator
in MaterialRateService; the UI uses the same limits and shared number helpers.

Workbook upload: <=20 MB, <=500 ZIP parts, <=200 MB expanded, <=10000 entries.
Exact catalog marker/hash, sheet set and ordered field keys/localized labels
must match. Numeric entry cells must be numeric; formulas, Excel errors, macro,
external links and data connections are rejected. No formula evaluation or
external fetching is needed. Component rates/dimensions remain entry fields;
canonical totals use explicitly entered Quantity × UnitPrice. An empty workbook
is rejected. Preview is not a save; import recomputes its checksum and validates
again. A stale timestamp returns 409. SQL Server uses the existing revision
application lock for writes; configuration changes use a serializable transaction.
Legacy CSV/fixed-column/manual endpoints reject configured categories.

Migration AddConfiguredBoqEntries adds only two nullable nvarchar(max) columns;
old rows need no backfill. The schema is immutable from the first revision, so
no implicit schema migration can reinterpret existing data. Configured roots
cannot use the legacy delete path; deactivate instead. Down removes configuration
and extra/reference data, so export/backup these columns before rollback; priced
line projections remain. Rehearse on a copy before production rollout.

Regression coverage: BoqCategorySeederTests, ConfiguredBoqControllerTests,
MaterialRateServiceTests and boq-configured-entry.spec.ts. Browser tests isolate
UI writes, while integration tests own real HTTP/auth/persistence and rejection
integrity. Keep Playwright output outside the watched frontend directory.

### 7.11 BOQ Quotation Integrity

Material-rate catalogs are discriminated as `InvestmentRate` or `Boq`; existing
rows migrate to `InvestmentRate`. Unit-cost quote resolution only accepts an
Approved effective Investment-rate revision. A BOQ catalog revision stores item
code/name, unit, quantity, and unit price. Applying it copies editable quote
items while persisting `MaterialRateRevisionId` and `PricingEffectiveDate` as
provenance and `CatalogReference` as the source; the approved source revision
remains immutable while copied quote rows may be edited. BOQ catalog quantity
and unit-price inputs are limited to the Quote storage scales of four and two
decimal places respectively so persisted amounts remain reproducible. The
`NormalizeBoqCatalogReferenceSource` migration normalizes earlier BOQ catalog
rows to those scales and reconciles persisted BOQ quote and snapshot totals.
Back up the database before deployment: its `Down` operation restores source
labels but cannot restore discarded extra decimal precision or former totals.

BOQ quotations use the same server calculation on create and update: each line
amount is `quantity × unit price`, subtotal is the sum of rounded line amounts,
discount is applied before VAT, and the grand total is rounded to two decimal
places away from zero. The React create/edit preview mirrors this formula via
`src/lib/quoteTotals.ts`; the API remains authoritative.

Users with `crm.material-rates:manage` may create, update, and delete individual
lines in a Draft `Boq` revision through nested revision routes. The service
enforces trimmed field limits, case-insensitive item-code uniqueness, quantity
scale four, unit-price scale two, and four-decimal line-amount rounding away
from zero. Create appends sort order, update preserves it, and delete compacts
the remaining order. Every mutation returns the refreshed revision so React
uses server-calculated lines and totals. Investment-rate and non-Draft
revisions reject these mutations. Material Rate revision and line decimal
fields are serialized as JSON strings; clients must preserve those strings
while editing and only convert at explicit numeric boundaries so JavaScript
cannot silently alter supported decimal values.

Users with `crm.material-rates:manage` may permanently delete a Material Rate
catalog. The delete removes its revisions and lines as one aggregate. The API
returns `409 Conflict` with `materialRates.catalog.deleteBlocked` when any
current Quote or immutable Quote version snapshot references a revision, so
pricing provenance cannot be removed. Historical `Retired` revisions remain
readable, although the active management UI uses hard deletion instead of the
retire action. The `crm.material-rates:manage` permission therefore includes
irreversible catalog deletion, not only catalog editing and import. Bulk delete
uses best-effort processing; successful IDs are cleared while failed IDs remain
selected with their catalog names and localized API reasons for review or retry.

The server rejects missing rows, blank names/units, non-positive quantities,
negative prices, percentages outside 0–100, and values that cannot fit the SQL
`decimal(18,*)` columns. A scoped Sales user cannot create a quotation for
another owner's opportunity. `Idempotency-Key` replay returns the original
create response without inserting another quotation, and BOQ version snapshots
preserve the source line set after post-approval edits.

### 7.12 Project Material Request List

`GET /api/operational-projects/{projectId}/procurement/material-requests`
requires `proc.material-requests.view` and returns `404` when the caller cannot
access the Operational Project. The endpoint never returns requests from another
project. It supports `search`, `status`, `siteRequesterUserId`,
`responsibleSiteUserId`, `assignedProcurementUserId`, `requiredFrom`,
`requiredTo`, `sortBy`, `sortDirection`, `page`, and `pageSize`; page size is
limited to 100. Search covers request code, note, assigned users, and BOQ item
code or description. Reversed date ranges are rejected.

Each line includes requested quantity, net received quantity from posted and
reversed warehouse receipts, approved BOQ quantity, and remaining BOQ quantity.
Remaining quantity uses the same committed statuses as approval validation:
Approved, Partially Fulfilled, and Fulfilled. The frontend exports all filtered
pages through this endpoint, so exported data retains the selected project and
filter scope.

The response also includes `currentApprovedBoq`, a least-privilege context used
to create Material Requests. It contains the current approved revision number
and item identity, unit, approved quantity, and remaining quantity. It does not
expose budget unit prices or BOQ totals, so a caller with Material Request
permissions but without `proc.boq.view` can select valid items without gaining
access to restricted BOQ financial data or revision history.

`GET /api/operational-projects/{projectId}/procurement/material-requests/{id}`
returns one project-scoped request with lifecycle timestamps and actors, net
received quantities, BOQ allowance evidence, and least-privilege
Customer/Project/Contract context. Contract context contains identity,
classification, vendor, and status only; it does not expose contract values.
This allows an MR-only role to use the detail page without requiring BOQ or
Contract read permissions.

Material Requests follow `Draft -> Submitted -> Approved | Rejected`, with
Approved requests moving to `PartiallyFulfilled` or `Fulfilled` through posted
warehouse receipts. Drafts are editable only by their requester. Create and
update reject duplicate BOQ items, past required dates, non-project users,
non-procurement assignees, obsolete BOQ revisions, non-positive quantities, and
quantities above the remaining approved allowance. Approval repeats the
allowance check inside a serializable transaction. Non-terminal requests may be
cancelled with a reason of at least three characters.

Mutations require an `Idempotency-Key`; update and transitions also carry the
current row version and `If-Match`. Submit notifies the assigned procurement
owner, decisions notify the requester, and cancellation notifies the other
party. Audit entries use the Material Request ID and retain the Operational
Project ID as metadata. The detail UI route is
`/admin/procurement-control/projects/{projectId}/material-requests/{requestId}`.

### 7.13 Project Warehouse Transactions

Warehouse inventory remains a derived ledger under one Operational Project.
`WarehouseReceipt` records inspected material against approved Material Request
lines, while `WarehouseIssue` records allocation to a responsible site user and
optional work-item code. Posted records are immutable; corrections create an
offsetting reversal linked to the original record.

`GET /api/operational-projects/{projectId}/procurement/warehouse-transactions`
requires `proc.warehouse.view` and supports project-scoped search, receipt/issue
type, status, responsible user, occurred-date range, sorting, and pagination.
The response also returns current approved-BOQ stock rows with posted receipt,
posted issue, and on-hand quantities after reversals. Client CSV export retrieves
all pages using the same filter contract.

Receipt and issue detail endpoints expose Customer, Operational Project, and
least-privilege Contract context without contract values. Receipt lines include
their Material Request and optional Contract source; issue lines include BOQ
allowance and current stock evidence. Draft documents support idempotent `PUT`
updates with row-version/`If-Match` concurrency. `POST .../post` affects stock;
`POST .../reverse` requires a reason of at least three characters and creates an
offsetting record. Every endpoint verifies Operational Project scope, and every
mutation records the actual receipt or issue ID in the audit log.

The current warehouse workflow is online-first. The customer requirement for
offline field capture and automatic synchronization needs a shared application
shell cache, durable client queue, stable offline identifiers, conflict policy,
and sync observability. Those capabilities do not yet exist as a reusable web
platform service; do not represent ordinary browser storage as completed
offline support.

### 7.14 Project Procurement BOQ

Project procurement BOQs are revisioned operational records and are distinct
from the reusable `Boq` material-rate catalogs described above. Every revision
belongs to one Operational Project, contains case-insensitively unique material
codes, and follows `Draft -> Submitted -> Approved | Rejected`. Only Draft and
Rejected revisions are editable; editing a Rejected revision returns it to
Draft. Approved revisions remain immutable because material requests and
contract lines can use them as allowance evidence.

All endpoints require the corresponding `proc.boq` permission and verify the
caller's Operational Project scope before returning whether a revision exists:

| Method | Route | Purpose |
|--------|-------|---------|
| `GET` | `/api/operational-projects/{projectId}/procurement/boq-revisions` | Search, filter, sort, and page project revisions |
| `GET` | `/api/operational-projects/{projectId}/procurement/boq-revisions/{id}` | Read one revision and its ordered material lines |
| `GET` | `/api/operational-projects/{projectId}/procurement/boq-revisions/export` | Export the current project-scoped filter as CSV |
| `POST` | `/api/operational-projects/{projectId}/procurement/boq-revisions` | Create the next Draft revision |
| `PUT` | `/api/operational-projects/{projectId}/procurement/boq-revisions/{id}` | Replace Draft or Rejected revision content |
| `POST` | `/api/operational-projects/{projectId}/procurement/boq-revisions/{id}/submit` | Submit a revision for approval |
| `POST` | `/api/operational-projects/{projectId}/procurement/boq-revisions/{id}/decision` | Approve or reject a Submitted revision |

Mutation requests use an `Idempotency-Key`; update and transition requests also
carry the current row version and `If-Match` header. The server calculates each
line amount as `approved quantity x budget unit price`, rounded to four decimal
places away from zero, and persists the sum as the revision total. A submitted
revision notifies the Project Manager. Approval or rejection notifies the
preparer. Notification delivery is best effort and never rolls back a saved
workflow transition.

The list UI is `/admin/procurement-control`. Detail links use
`/admin/procurement-control/projects/{projectId}/boq/{boqId}` and show the
Customer, Operational Project, every related Contract, source references,
ordered material lines, and lifecycle timestamps. Edit and transition actions
are hidden unless the caller has the matching permission and the current state
allows the action.

### 7.15 Project Material Alerts

Material alerts are a persistent projection of the approved BOQ, approved
Material Request demand, and posted warehouse ledger. Users cannot create or
manually resolve an alert. The service evaluates two rules by case-insensitive
material code:

- `OverBoq`: net posted issue quantity exceeds the current approved BOQ
  allowance. The variance is `issued - BOQ allowance` and severity is Critical.
- `Shortage`: approved demand not yet issued exceeds on-hand stock. Because
  on-hand is `received - issued`, the shortage variance is equivalent to
  `approved demand - received`. It is Critical when on-hand is zero or negative,
  otherwise Warning.

An alert follows `Open -> Acknowledged -> Resolved`. The assigned procurement
user or Project Manager may acknowledge an Open alert with a 3–2,000 character
response note. Acknowledgement does not clear the discrepancy. Resolution is
automatic when source data no longer violates the rule, and a Resolved alert
reopens automatically if the violation returns. Every detection, metric update,
acknowledgement, resolution, and reopening appends a `MaterialAlertEvent`.

BOQ approval, Material Request approval/cancellation, and warehouse
post/reversal mutations evaluate alerts after the source transaction commits.
Evaluation and notification delivery are best effort so projection failures do
not roll back a valid procurement ledger mutation. Authorized users can run the
idempotent `POST .../material-alerts/evaluate` endpoint to reconcile the
projection after a transient failure.

| Method | Route | Permission and purpose |
|--------|-------|------------------------|
| `GET` | `/api/operational-projects/{projectId}/procurement/material-alerts` | `proc.material-alerts.view`; project-scoped search, filters, sorting, counts, and pagination |
| `GET` | `/api/operational-projects/{projectId}/procurement/material-alerts/{id}` | `proc.material-alerts.view`; detail, least-privilege contract context, and event history |
| `POST` | `/api/operational-projects/{projectId}/procurement/material-alerts/evaluate` | `proc.material-alerts.manage`; reconcile source data idempotently |
| `POST` | `/api/operational-projects/{projectId}/procurement/material-alerts/{id}/acknowledge` | `proc.material-alerts.manage`; acknowledge with row-version concurrency and idempotency |

PM and Procurement roles can view and manage alerts. Warehouse can view them;
BGD receives view through the existing view pattern. All endpoints also enforce
Operational Project access and return 404 outside scope. Alert responses do not
contain BOQ prices, negotiated prices, or Contract values. Detection notifies
the assignee and Project Manager without duplicate delivery when they are the
same user. Project hard-delete previews and removes alerts and their history as
owned aggregate data.

---

### 7.16 Detail Design Schedule

#### Business Objective

The detail-design schedule provides a project-scoped plan for the three design
phases without changing or reusing Module 4 construction tasks. It records
planned and actual dates, ownership, department, progress, milestones, and
Finish-to-Start predecessor relationships, then derives traceable weighted
progress for each phase and the complete design schedule.

Each task stores one active `ApproverMemberId` distinct from the assignee,
optional CC members, description, parent task, resource/subcontractor summary,
and critical-path flag. Generic task updates cannot write `Completed`; the
assignee calls `POST .../tasks/{id}/complete`, which records completion and
automatically routes the task to `Pending` approval. Only the assigned approver
can call `POST .../tasks/{id}/review`; rejection requires a reason and returns
the task to `InProgress`. Both paths enforce row-version concurrency and retain
history/notifications.

An outsourced design task additionally stores both `VendorId` and
`DownstreamContractId`; neither may be supplied alone. The server requires the
contract to belong to the same Operational Project, match the vendor, have
`Downstream` direction, use `Design` or `Subcontract` type, and not be
cancelled. The internal assignee and independent Design Lead approver remain
required, so the downstream party does not replace NICON accountability.
The task dialog loads eligible pairs from
`GET /api/operational-projects/{projectId}/design-schedule/outsource-options`;
this endpoint uses `design.schedule.manage` plus project scope and does not
require Design users to receive broad Vendor or Contract-list permissions.

`AddProjectCodeAuditAndDesignVendors` adds these nullable task links and the
versioned `operational_project_code_changes` queue/history table. Rehearse the
migration script before deployment. Rollback removes the new links and code
change history, so export that audit data first if an environment has already
used project-code changes. The same migration removes only the previously
shipped sensitive defaults from the `FINANCE` group baseline; it leaves any
other administrator-added baseline permissions unchanged. Rollback restores
those shipped defaults.

Before applying `AddDesignApprovalWorkflow` to an environment with existing
schedule tasks, assign every Design Project an active Design Lead or Design
Manager who is also an active Operational Project member and differs from each
task assignee. The migration backfills that member and deliberately aborts
instead of inventing an approver when no valid assignment exists. Rehearse this
preflight and the generated migration script against a production-like backup;
the rollback drops approval/CC data and is therefore schema-reversible but not
data-preserving for newly entered approval history.

`EnsureDesignReviewEvidenceSnapshot` is a forward-only compatibility repair
for environments that applied `AddDesignReviewWorkflow` before the immutable
source snapshot columns were introduced. It adds only missing columns/indexes,
backfills them from the linked Project Document, and aborts if any review
evidence cannot be resolved. Generate and inspect the migration script, rehearse
it against a production-like backup, and verify there are no unresolved review
evidence rows before deployment. Its `Down` method is intentionally
non-destructive; the owning `AddDesignReviewWorkflow` migration removes the
review schema during a complete rollback.

Design dossier circulation is exposed at
`/api/design-projects/{projectId}/reviews`. A Design Manager creates a revision
with designated internal recipients and Project Document IDs from the matching
Design category. Recipients can only list their assigned reviews and comment
while the review is internal. Notifications open
`/admin/design-reviews/{projectId}?reviewId={reviewId}`. This recipient workspace
loads only the scoped review API and review-specific downloads; it does not
require the full Design Project response, recipient directory or project-wide
document catalog. Legacy `?tab=reviews` links fall back to the same scoped view
when project access is denied. Other project tabs retain their permission
guard, and review access grants no additional project permission.
Review-specific content downloads verify the actual bytes against that review's
`Sha256Snapshot` before sending headers or file bytes. They buffer up to 64 KiB
in memory, spill larger files into the non-public ASP.NET temporary directory,
enforce the existing 100 MiB document limit and dispose the buffer on success,
failure or cancellation. Changed bytes return `409 Conflict` even if catalog
metadata has not yet reconciled; missing/invalid snapshot hashes, empty content
and incomplete/unverifiable downloads also fail closed. An unassigned caller
still gets `404` before storage is read. A mismatch never rewrites the snapshot,
approval state or source file. Restore the reviewed content or create the
appropriate new revision through the owning workflow. This integrity guard is
Partial protection: it does not archive or recover historical bytes, and the
historical storage/retention policy remains an open decision.
`POST .../{reviewId}/comments` accepts an optional
`projectDocumentId`; when present it must be one of that review's submitted
documents. A missing/null ID keeps the existing dossier-level comment behavior.
The response returns the ID with each comment. The nullable column and composite
foreign key in `LinkDesignReviewCommentsToDocuments` retain old comments and
prevent a file link to another review. Rehearse this migration against existing
review data before deployment; rollback drops only the new file links. The
Design Manager consolidates feedback, records submission to the client, and
records the client decision. Concept finalization,
Basic-to-Shop unlock, and IFC release require the latest matching review to be
`ClientApproved`.

Concept options additionally expose `GET/POST /api/concept-options/{id}/revisions`,
artifact and checklist-waiver `POST/DELETE` under a revision, and feedback plus
one-time response `POST` endpoints. Revision R00 is created with a new option.
The draft binds typed Project Documents owned by that option; removing a binding
does not delete the Project Document. Submission requires a synced, exact file
set and the applicable checklist, then freezes the revision. Client feedback is
attached only after client submission. New feedback requires `evidenceSource`
(1–1000 characters); its verbatim comment is preserved. A response requires
`outcome` (1–2000 characters) as well as the response text. Both are immutable
after recording. Existing rows retain null source/outcome rather than invented
evidence. Migration `AddConceptFeedbackEvidence` adds these nullable columns;
new writes enforce the required fields at the API and service boundary.
Rollback is blocked once evidence/outcome values exist. Concept evidence already
frozen or submitted and recorded acceptance files cannot be queued for deletion
through Project Documents; retain them and create a replacement revision.
A replacement revision follows a
changes-requested or rejected decision. Concept `ClientApproved` requires a
separate synced signed TK-M03 Project Document, signer and signing date.
Finalization checks that evidence on the latest revision and a filtered unique
index permits only one finalized option per Design Project. Option status is
also an optimistic concurrency token: a stale sibling update cannot discard the
winner of a competing finalization. Migration
`AddConceptDossierRevisions` backfills R00 and retains legacy review history;
rehearse it against production-like data before deployment.

Concept dossier edits, submission, client decisions, option transitions and
manual document deletion use a transaction-owned SQL Server application lock
(`concept-dossier-{optionId}`) acquired before reading the write guards. Keep new
write paths on the same lock so a stale edit cannot change a frozen file set.
Manual uploads receive distinct source slots, including Drive-only uploads
whose local path is empty; several files can belong to the same option.
Unsubmitted drafts do not supersede a submitted review for revision creation or
finalization. A draft whose revision was already submitted elsewhere remains
read-only reference history. The revision response includes `canCreateRevision`
for the latest revision. For legacy R00 shared submissions without a selected
revision, the service uses source snapshots and a changes/rejected decision (or
legacy approval without signed acceptance) to permit a fresh revision. This
does not convert historical shared approval into approval of an option: the new
revision must complete the current submission and signed TK-M03 gates.
Acceptance timestamps are returned with their UTC offset and displayed in the
browser's local calendar. These corrections do not require a schema migration.

Run `ConceptDossierConcurrencyTests` with `NICON_SQL_TEST_CONNECTION` set inside
the backend container. Each case creates and deletes its own generated
`NiconConceptTest_<guid>` database; the supplied catalog is never used for test
data or cleanup. The Concept browser spec also uses `E2E_BACKEND_CONTAINER`
(default `nihome31042025-backend`) alongside `E2E_SQL_CONTAINER` to prepare real
local image/PDF fixtures. It exercises authorized downloads through the live
Compose API without intercepting document responses; it does not prove live
Google Drive availability.

Permanent deletion of Concept, Basic Design, Shop Drawing and IFC records uses
`GET /api/<resource>/{id}/deletion-impact` followed by `DELETE` with
`ConfirmDeletionRequest`. The server recomputes the deterministic plan inside a
serializable transaction. Only `Discarded` Concept, `Rejected` Basic/Shop and
`Cancelled` IFC records are eligible. Active Project Documents, shared review
evidence and IFC references block deletion; exclusive review history is deleted
with a rejected dossier only while it has never been submitted to the client.
Any review with `ClientSubmittedAt` blocks hard delete regardless of its later
decision; corrections use the recorded client decision and a replacement
revision instead. Bulk Shop Drawing deletion is intentionally disabled
because every root requires an individual impact plan and typed confirmation.
This is the implemented safety policy, not a confirmed resolution of NICON
question Q-08. The controlling 03/10 baseline only confirms that failed-record
deletion belongs to the highest permission tier and explicitly leaves deletion
after client submission open.

#### Actors and Permissions

- A caller with `operations.projects.view` may read only an Operational Project
  visible through `IProjectAccessService.CanViewOperationalProjectAsync`.
- A caller with `design.schedule.manage` may initialize or mutate a schedule
  only when `IProjectAccessService.CanManageDesignScheduleAsync` confirms that
  the caller is the Operational or Design Project Manager, the Design Lead, or
  has an active Project-wide or Design-module Project Manager/Design Lead team
  assignment for that project. Discipline-only assignments remain read-only.
- Administrative roles that hold both `design.schedule.manage` and
  `operations.projects.view.all` may manage any existing project only when the
  active system role is `ADMIN` or `SUPER_ADMIN`. Custom roles and portfolio
  visibility by itself never enable schedule mutation or mutation controls.
- Inaccessible projects, phases, and tasks return `404` to avoid disclosing
  project existence. Missing authentication returns `401`; missing global
  permission returns `403`.

#### Lifecycle and Validation

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
A phase update is rejected when its resulting three-phase weight total would no
longer equal 100; weight redistribution requires an atomic contract and is not
performed through separate phase updates.
A milestone has `IsMilestone = true` and equal planned start and end dates.
Overdue is derived at read time when planned end is before the current UTC date
and status is not `Completed`; it is never persisted.

Task departments must be active options in the `project-department` master-data
category. The seeded options are Design, Architecture, Structural, MEP, and
Interior, with Vietnamese, English, Chinese, and Japanese labels. An assignee
must be an active user represented by a non-ended `OperationalProjectMember` in
the same project. Every predecessor must be a task in the same project;
self-dependencies and cycles are rejected before persistence.

#### Progress Policy

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

#### Design-schedule API contract

Both `/api/operational-projects/{projectId}/design-schedule` and its `/api/v1`
alias expose the same controller.

| Method | Relative route | Permission | Purpose |
|---|---|---|---|
| `GET` | `/` | `operations.projects.view` | Read phases, roll-up sources, and paged tasks |
| `POST` | `/initialize` | `design.schedule.manage` plus project leadership | Create the canonical phase baseline |
| `PUT` | `/phases/{phaseId}` | `design.schedule.manage` plus project leadership | Update phase dates, status, progress, and weight |
| `POST` | `/phases/{phaseId}/tasks` | `design.schedule.manage` plus project leadership | Create a task or milestone in a phase |
| `PUT` | `/tasks/{taskId}` | `design.schedule.manage` plus project leadership | Update a task and replace predecessor links |

Mutations require an `Idempotency-Key` containing 1 through 120 characters.
Missing, blank, or oversized keys return `400`. Reusing a key with the same
request replays the stored response; reusing it with a different request returns
`409`.
Updates require row version through the request body or `If-Match` header and
emit an ETag. Missing or malformed tokens return `400`; conflicting body and
header tokens return `400`. A stale write returns `409` and may be retried with the same
idempotency key after obtaining the current row version. Successful mutations
write both the standard audit event and a scalar schedule-history snapshot.

The task query supports `phase`, `assigneeMemberId`, `departmentCode`, `status`,
`plannedFrom`, `plannedTo`, `overdueOnly`, `page`, and `pageSize`. Date filtering
uses inclusive interval overlap: a task matches when its planned end is on or
after `plannedFrom` and its planned start is on or before `plannedTo`.

#### Compatibility and Deletion

The schedule uses dedicated `design_schedule_*` tables and does not alter,
backfill, or reinterpret `construction_tasks` or its API. Deleting a Design
Project reports schedule phases, tasks, dependencies, and history in the
deletion-impact plan. Aggregate deletion removes dependency edges explicitly;
the remaining schedule-owned rows are removed with their Design Project.

Migration `AddDetailDesignSchedule` is additive: it creates four new tables,
their foreign keys, indexes, row versions, and check constraints. It contains no
data update or migration-time initialization and must be applied through the
normal deployment gate only after backup and migration-script review.

### 7.17 Project Operational Reports

#### Purpose and scope

Project reports are operational summaries bounded by `OperationalProjectId`.
They do not calculate employee KPI scores. The API returns only projects that
the caller can access through `IProjectAccessService`.

- `GET /api/reports/projects` returns a portfolio when `projectId` is omitted.
- `GET /api/reports/projects?projectId={id}` returns one accessible project and
  returns `404` when that project does not exist or is inaccessible.
- `GET /api/reports/projects/export?format=xlsx|pdf&language=vi|en|zh|ja` uses
  the same project and date filters, renders complete filtered results on the
  server, and records an audit event with the filters and project count.
- `from` and `to` are optional inclusive `DateOnly` values. A reversed range is
  rejected with `400`.

The response includes server-generated `generatedAtUtc` and `asOfUtc` values.
Drill-downs contain only relative frontend routes and query strings.

#### Permissions

| Role | View | Export |
| --- | --- | --- |
| `SUPER_ADMIN`, `ADMIN` | Yes, through system-role wildcard defaults | Yes, through system-role wildcard defaults |
| `BGD`, `ACCOUNTANT` | Yes, explicitly seeded | Yes, explicitly seeded |
| `PM` | Yes, explicitly seeded and project-scoped | No |
| Other business roles | No default grant | No default grant |

`BGD` already has the broad `**.view` pattern, which covers
`reports.projects.view`; it also receives an explicit
`reports.projects.export` grant because export is deliberately separate.

#### Available metrics

- Project identity and manager/customer context.
- Design weighted progress using `design-schedule-weighted-v1`. The value is
  unavailable with `DESIGN_BASELINE_INCOMPLETE` until all three phase and task
  baselines satisfy the existing weight rules.
- Construction task counts by exact persisted status, overdue count, and
  overdue rows. No averaged construction percentage is calculated.
- Acceptance counts by exact persisted status and revision count, plus overdue
  count. No acceptance ratio or first-pass rate is calculated.
- Permit overdue, due-soon, and expiring counts and rows. Due soon and expiring
  use a 30-day inclusive warning window from `asOfUtc`.
- Quote totals, contract base value, approved VO delta, current contract value,
  and milestone scheduled values by status. Milestone values use the signed
  base contract value, matching existing contract semantics. They are labeled
  **Contractual schedule** and are not cash, revenue, or receivables.

#### Date filter bases

The inclusive date range applies independently to each source date rather than
the project identity: construction planned end, acceptance date, permit target
deadline or expiry, quote creation, contract signed date (or creation date when
unsigned), approved VO decision date (or update date), and milestone due date.
An in-range VO or milestone remains visible when its parent contract was signed
before the range. Design progress is current-as-of because no historical
baseline snapshots exist.

#### Explicitly unavailable metrics

The API and exports expose stable reason codes instead of synthetic zeroes:

| Metric | Reason code |
| --- | --- |
| Historical S-curve | `HISTORICAL_SNAPSHOTS_UNAVAILABLE` |
| Weighted construction progress | `CONSTRUCTION_WEIGHTS_UNAVAILABLE` |
| Acceptance ratios / first-pass | `ACCEPTANCE_OUTCOME_DATA_UNAVAILABLE` |
| Actual cashflow, revenue, expenditure, P&L, receivables | `ACTUAL_FINANCE_LEDGER_UNAVAILABLE` |
| Inventory | `INVENTORY_LEDGER_UNAVAILABLE` |
| BOQ usage | `BOQ_USAGE_DATA_UNAVAILABLE` |
| Vendor performance | `VENDOR_PERFORMANCE_DATA_UNAVAILABLE` |

A project without its design-linked construction, acceptance, or permit source
returns the affected section as `Unavailable` with `SOURCE_NOT_CONFIGURED`.
No schema migration or report snapshot table is introduced.

### 7.18 RFQ and Supplier Comparison

#### Business contract

Sources: NIH-166 and NIH-174/175/176, including their September 7 acceptance
comments; `Nicon-QLVH.md` Modules 5–6; `Nicon-workflow.md`;
`Nicon_BreakTask_v1.xlsx` (RFQ list, create/edit, and comparison detail).

The completed RFQ workflow uses the following contract:

- Each RFQ belongs to one Operational Project and retains that project's
  Customer. A project can have many RFQs, vendors, and contracts.
- Procurement selects lines and quantities from an approved VND Project BOQ.
  Each quantity must fit its source line. At award, every Supply quantity is
  manually allocated to approved, unfulfilled Material Request demand for the
  same BOQ line. Subcontract allocations do not use Material Requests. The
  transaction rejects incomplete RFQ coverage and Supply allocations above the
  remaining requested quantity.
- Each RFQ invites one or more active suppliers/subcontractors. The vendor
  directory is shared master data; invitations and quotations belong to the RFQ.
- Lowest valid VND-normalized prices are highlighted, including ties. Each RFQ
  defines price, lead-time, approved vendor-rating and manually assessed
  commercial weights totaling 100%. The score is advisory; selecting below the
  highest score requires an override reason.
- Procurement can record quotes, while vendors with valid email receive an
  expiring random-token portal link. The public portal exposes only that
  invitation's scope, provides secure downloads for RFQ package documents, and
  accepts immutable quote revisions until the deadline.
  Only a SHA-256 token hash is persisted. Failed email delivery is recorded and
  authorized users can rotate tokens and resend invitations. The existing RFQ
  owner remains the technical user FK for portal writes, while the bid read model
  and `portal-bid-submitted` event attribute the action to the invited vendor.
  `LastPortalAccessAt` is audit telemetry only; it does not extend token expiry.
- An explicit atomic batch award allocates each RFQ line and quantity across one
  or more eligible quotations. It creates one **Draft Downstream
  Supply/Subcontract contract** per selected vendor and all corresponding BOQ
  lines. Every RFQ quantity must be covered exactly; partial commits are not
  allowed. The existing contract workflow handles signing, execution, and
  payment; an RFQ award does not sign a contract.
- Award-derived contract value, customer, project, vendor and classification
  cannot be changed through the generic contract editor. Awarded lines cannot
  be edited, appended or moved through procurement endpoints, including while
  the contract is Draft. Draft coordination notes and the existing signing and
  payment workflows remain available. Ordinary contracts without an RFQ award
  retain their existing draft-editing behavior.
- Each bid records a three-letter currency and a manually entered, immutable
  exchange rate to VND. VND requires rate 1. Commercial totals apply either a
  percentage or fixed discount, then add freight, then calculate VAT. Original
  and converted totals are retained for evidence. Unit prices and line totals
  use four decimals; quantity uses six; converted contract values round to two
  decimals while contract line prices retain four decimals. Contract lines keep
  the converted base negotiated unit price; the contract header also includes
  proportional freight, discount and VAT. Reconcile that expected difference
  through the immutable award commercial snapshot, not by editing awarded lines.

The application route is `/admin/procurement-control/rfqs`, with `projectId`
and optional `rfqId` query parameters. Customer → Project → RFQ → Contract
context remains visible. List export is CSV; detail export is a JSON evidence
bundle containing matrix lines, all quote revisions, commercial terms, files,
history, and the award comparison snapshot.

#### Actors and access

| Capability | Default role grants |
|---|---|
| `proc.rfqs.view` | Procurement, PM, BGD, ADMIN, SUPER_ADMIN |
| `proc.rfqs.manage` | Procurement, ADMIN, SUPER_ADMIN |
| `proc.rfqs.export` | Procurement, BGD, ADMIN, SUPER_ADMIN |
| `proc.rfqs.award` | BGD, ADMIN, SUPER_ADMIN |

Every endpoint additionally checks existing project access rules. Missing or
out-of-scope resources return 404; unauthenticated and functionally unauthorized
requests return 401 and 403 respectively. Cached idempotency responses recheck
project scope, so removing membership also revokes replay access. The UI hides
unavailable actions; the server independently enforces them. New role grants
are added by migration without resetting unrelated role customizations.

The RFQ owner must be an active Procurement user and current project member.
Creation, issue, and award validate that relationship. Completed/cancelled
projects reject writes. Award rechecks vendor activity, bid revision/expiry,
project state, owner, and current approved BOQ inside its transaction. A newer
approved BOQ requires cancelling the old RFQ and preparing a new one.

RFQ-to-MR allocation is a purchasing reservation/coverage record, not physical
fulfillment. It does not change Material Request status; posted Warehouse
Receipts remain the only source for `PartiallyFulfilled` and `Fulfilled`.

#### Lifecycle and concurrency

| State | Permitted actions |
|---|---|
| Draft | Edit scope, attach package files, issue, cancel with reason |
| Issued | Submit quote revisions until deadline, withdraw current quote with reason, start evaluation, cancel with reason |
| UnderEvaluation | Score quotations, batch-allocate eligible lines to approved MR demand, award, cancel with reason |
| Awarded | View evidence and all generated contracts; close RFQ |
| Closed / Cancelled | Read/export evidence |

Issue freezes BOQ line descriptions, quantities, units, and invitations, creates
portal tokens, and attempts vendor email delivery after persistence commits.
Starting evaluation explicitly stops further quote submissions, including when
done before the deadline. A quote revision never overwrites an earlier one.
The latest revision is current even if withdrawn; withdrawing it does not revive
an older revision. Missing price cells remain missing, while zero means a quoted
free item. A partial current quotation can be scored and selected only for lines
it quoted; whole-package selection still requires a complete quotation. Expired,
withdrawn, inactive-vendor and superseded quotations cannot be awarded. Quote
validity must cover the RFQ deadline.

Vendor responses include the read-only `isActive` flag. An inactive vendor's
quoted amounts remain visible as historical evidence, but receive no lowest-price
highlight even when tied with an active vendor. Valid lines in an active partial
quotation still participate in line-price comparison; whole-package award
eligibility remains separate.

Mutations require `Idempotency-Key`; updates, transitions, bids, award, and file
attachment also require `rowVersion` or matching `If-Match`. Reusing a key with
another payload or submitting stale state returns 409. Root changes, quotation
history, award snapshot, and generated contract/lines commit transactionally.
Rejected business operations leave them unchanged. Audit records include actor,
project, RFQ, action, and resulting status. RFQ activity history is persisted
alongside the operation.

SQL Server mutations take update locks on the RFQ root and its owned collections
in a fixed order. Serializable isolation remains in place for dependency checks.
This prevents two RFQs from sharing an index gap while reading child history and
then deadlocking when both append records. BOQ revision and MR/receipt/issue code
allocation similarly reserve their ranges for update before reading the next
number, as does the downstream payment-request code allocator. A transaction-owned
SQL application lock excludes RFQ creation from concurrent RFQ mutations because
EF inserts new children in a different order from existing-graph reads. Creation
is serialized across projects; existing RFQ mutations share the gate and retain
their root/child locks. Ordinary detail reads retain their existing query behavior.

The joined warehouse flow also serializes its transactional writes with a
transaction-owned SQL application lock. This prevents stock scans and document
writes from taking conflicting root/child locks across projects while preserving
Serializable stock validation. Warehouse list reads explicitly use ReadCommitted
so they do not inherit a stronger isolation level from a reused connection.
The tradeoff is serialized warehouse writes across projects; the validation below
proves the tested workflow, not peak throughput or every possible database race.
RFQ creation/mutation and project MR-allocation application locks use a 15-second
timeout. A timeout returns a concurrency conflict; clients must reload current
availability and retry instead of replaying stale quantities.

#### Files and notifications

Files use the existing ProjectDocument upload validation and Google Drive
lifecycle under category `Procurement`. Uploads first become project documents;
selecting a file attaches it to a draft RFQ package or a submitted quote revision.
An abandoned form does not delete an uploaded project document: it remains
available in the project's document library. Each linked file records RFQ source
identity and package or vendor/revision slot. Cross-project, already-bound, and
pending-deletion files cannot be attached. Downloads recheck RFQ/project scope.
Linked evidence cannot be deleted through the generic document endpoint.

Google Drive must be configured to upload; failures use the existing document
service's cleanup behavior. No replacement file storage or vendor messaging
integration is introduced.

Issue and award notify the active RFQ owner and project PM through seeded in-app
notification templates. A five-minute worker marks overdue issued/evaluating
RFQs and notifies these recipients once. This marker is transactional with the
notifications and does not invent another RFQ lifecycle state. Standard
notification-template administration remains available. Issue and award
delivery is best effort after the business transaction commits; delivery failure
is logged and never reports that the persisted transition failed. The overdue
worker keeps its marker and notification in one transaction so a failed delivery
can retry on the next interval.

RFQs retain purchasing evidence through cancellation rather than exposing a
hard-delete operation. Project, invited vendor, and awarded contract deletion
previews explicitly classify RFQs as blockers. These rules also apply to demo
records. Removing the source project/vendor/contract must not erase RFQ history.

#### API and field validation

Base: `/api/operational-projects/{projectId}/procurement/rfqs`
(also available under `/api/v1`).

| Operation | Method / suffix |
|---|---|
| List / references / CSV export | GET `/`, `/references`, `/export` |
| Detail / evidence export | GET `/{id}`, `/{id}/export` |
| Create / edit | POST `/`, PUT `/{id}` |
| Lifecycle | POST `/{id}/issue`, `/evaluate`, `/cancel`, `/close` |
| Submit quote revision / withdraw | POST `/{id}/bids`, `/{id}/bids/{bidId}/withdraw` |
| Score / batch award | POST `/{id}/bids/evaluate`, `/{id}/award-batch` |
| Resend portal invitations | POST `/{id}/invitations/resend` |
| Upload / attach existing file | POST `/documents/upload`, `/{id}/documents` |
| Download RFQ file | GET `/{id}/documents/{documentId}/download` |

Public vendor portal endpoints are `GET /api/vendor-rfqs`,
`POST /api/vendor-rfqs/bids`, and
`GET /api/vendor-rfqs/documents/{documentId}/download`. The email URL stores the secret in the browser
fragment; the SPA sends it in `X-RFQ-Portal-Token`, keeping it out of server URL
logs and referrers. Tokens expire at the RFQ deadline and responses use
`Cache-Control: no-store`. Public responses omit Customer, BOQ budget, competing
quotations, users and internal history. Portal submissions require an idempotency
key and revalidate RFQ state, deadline, vendor activity and commercial fields.
The former `POST /{id}/award` whole-package endpoint returns 410 in deployed
configuration. It can be enabled only in isolated compatibility test settings;
production awards must use the batch endpoint and MR allocation contract.

`RfqRequests.cs` enforces shape, required fields, lengths, enums, and numeric
ranges. `RfqService.cs` enforces the following business relationships. Forms
mirror the editable constraints for feedback.

| Field | Rule |
|---|---|
| Code, currency, project/customer, totals | Server generated or derived; not writable in RFQ requests |
| Title | Required after trimming, maximum 200 characters |
| BOQ revision | Approved VND revision in the selected project; award requires current approved revision |
| Owner | Active Procurement member of the same project |
| Due date | Future timestamp with timezone; issue requires it still be in the future |
| Vendor IDs | 1–100 unique, active Supplier/SubContractor/Both IDs |
| BOQ lines | 1–500 unique source lines from selected revision |
| Quantity | Positive, ≤ approved quantity, ≤ 6 decimal places |
| RFQ / quotation notes | Optional, trimmed, maximum 2000 characters |
| Quote vendor | Active invited vendor of the same RFQ |
| Quote lines | 1–500 unique RFQ line IDs; omitted lines remain missing |
| Unit price | 0–999999999999.9999, ≤ 4 decimal places |
| Line / quotation total | ≤ 99999999999999.9999 VND |
| Delivery lead time | Whole days, 0–3650 |
| Payment terms | Required after trimming, maximum 1000 characters |
| Quote validity | Zoned timestamp, unexpired, on/after RFQ deadline |
| Quote file IDs | 0–20 unique, unbound, available Procurement files in same project |
| Scoring weights | Four values from 0–100 totaling exactly 100 |
| Commercial score | 0–100 with 3–2,000 character evidence note |
| Bid currency / FX | Three-letter code; positive manual rate; VND rate is 1 |
| Commercial totals | Non-negative freight and VAT; discount is percentage or amount, not both |
| Batch award | Current unwithdrawn bid line; every RFQ quantity covered exactly |
| MR allocation | Required for Supply only; approved/partially fulfilled MR, same project and BOQ line, no over-allocation |
| Contract type | Supply/Subcontract compatible with the selected vendor type |
| Award/cancel/withdraw reason | 3–2000 characters after trimming |
| Row version | Required existing 8-byte concurrency token for changes |
| List query | Search ≤ 200 chars; valid status/owner/date range; whitelisted sort; page 1–1000000; page size 1–100 |

Deadlines are stored and returned as UTC. UI date filters convert local start/end
of day to UTC. CSV cells neutralize formula prefixes.

#### Migration and demonstration

`20260908134224_AddRfqBidComparison` adds seven tables, indexes, restrictive
cross-aggregate foreign keys, and RFQ permission grants. It does not rewrite
existing business data. Review deployment backups before rollback: `Down` drops
RFQ evidence and removes this capability's permission assignments.

`20260909050114_CompleteRfqWorkflow` adds scoring, invitation delivery metadata,
commercial snapshots, split-award records and MR allocation records. Existing
RFQs are backfilled with 50/20/20/10 scoring weights; existing bids are preserved
as VND at rate 1 with their previous total copied to subtotal and original total.
Rollback drops only the extension tables and columns, so back up split-award and
portal-delivery evidence before downgrade.

RFQ demo data is enabled only for the backend `Development` environment.
Integration/E2E fixtures may also create their own isolated rows. Production
startup does not create a `PJ-SAMPLE-RFQ` project or any other business sample
rows. Production reference options come from the API, not hardcoded React
values.

### 7.19 KPI Framework and Source Evidence

#### KPI scope

The KPI platform currently implements 19 metric definitions in six scorecard
groups: Sales, Tendering, Design, Site, Procurement, and Project Accounting.
This is the delivered, source-backed subset of the target NICON 11-position
framework; it must not be described as complete coverage of all 11 positions.
Only metrics backed by structured source events are calculated. A missing
source workflow is reported as `MissingData`; it is never converted to a zero
score.

#### Platform contract

- One period exists per employee, year, and month.
- Period boundaries use `Asia/Ho_Chi_Minh` and are stored as UTC.
- Active definition weights cannot exceed 100% for a KPI position.
- A period can be locked only when every active metric is `Available` and the
  snapshot weights equal 100%.
- Snapshots freeze the definition code, version, weight, target, scoring
  direction, threshold, result, and source evidence.
- A locked period is immutable and cannot be recalculated.
- Automatic calculation runs daily at 01:00 Vietnam time. Authorized users may
  also calculate manually.
- A configured low-score threshold produces at most one notification per
  employee and period.
- `analytics.kpi.view` is own-scope. `view.all`, `manage`, and `export` grant
  cross-user viewing, definition/period management, and export respectively.

#### User interface routes

- `/admin/kpi` is the KPI evaluation workspace. It selects the employee and
  period, calculates scores, displays source evidence, exports results, and
  locks complete periods.
- `/admin/kpi/configuration` is the KPI definition workspace. It edits weights,
  targets, direction, alert thresholds, and active state, and requires
  `analytics.kpi.manage`.

The evaluation route does not render or load definition configuration. The
configuration route does not load employee dashboards or expose calculate,
export, or period-lock actions.

Both routes include an expandable, four-language usage guide. Evaluation
explains the four-step workflow, data statuses, and irreversible period lock.
Configuration explains each field and shows live readiness per role: active
weight total, missing required targets, and whether the role is ready to score.
The `RequiresTarget` flag is supplied by the backend definition contract so the
frontend does not duplicate formula rules.

#### Metric traceability

| Position | Metric | Weight | Source | Status |
| --- | --- | ---: | --- | --- |
| Sales | Lead-to-contract conversion | 40% | Lead owner, creation and conversion events | Implemented |
| Sales | New signed contract revenue | 40% | Upstream customer Contract owner, signed date and value | Implemented; target required |
| Sales | First customer interaction time | 20% | Lead creation and first non-note activity | Implemented; target required |
| Tendering | Tender win rate | 40% | Tender preparer, first submission date and current result | Implemented |
| Tendering | On-time tender preparation | 30% | Tender checklist owner/deadline/completion | Implemented |
| Tendering | Tender estimate accuracy | 30% | Approved tender estimate versus final execution BOQ | Implemented |
| Design | On-time drawing delivery | 40% | Design schedule task assignee, planned/actual end | Implemented |
| Design | First-pass drawing approval | 30% | Drawing owner, approval/release and revision count | Implemented |
| Design | Site-reported design errors | 30% | Verified Punch Items with confirmed Design root cause | Implemented; target required |
| Site | Construction progress variance | 30% | Construction task owner and planned/actual duration | Implemented; target required |
| Site | Material waste rate | 30% | Excess posted warehouse issue quantity versus final BOQ allowance | Implemented; target required |
| Site | First-pass acceptance | 20% | Acceptance creator, approval and revision count | Implemented |
| Site | HSE violation count | 20% | Confirmed HSE violations attributed to the responsible site user | Implemented; target required |
| Procurement | Purchase cost optimization | 40% | Final BOQ unit ceiling versus signed downstream Contract line cost | Implemented |
| Procurement | Delivery time | 30% | Approved Material Request to final posted Warehouse Receipt | Implemented; target required |
| Procurement | Vendor quality rating | 30% | PM-approved per-contract Vendor Rating | Implemented |
| Project Accounting | On-time receivable collection | 40% | Due-month upstream milestones attributed to an accountant | Implemented |
| Project Accounting | Partner payment processing time | 30% | Validated Payment Request to paid timestamp | Implemented; default target 72 hours |
| Project Accounting | Financial data accuracy | 30% | Approved post-close Accounting Corrections | Implemented; default target 1 correction |

#### KPI Source Evidence

KPI sources must satisfy these rules:

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

The implementation assumptions retained below describe the current code. They
must not be presented as NICON-approved business decisions when they are absent
from or conflict with the controlling 03/10 baseline. Consult the current
controllers/DTOs for actual API behavior and `docs/Nicon-QLVH.md` for the target
business contract.

| Decision | Current implementation assumption |
| --- | --- |
| D-01 | The seed defines six functional permission groups: CRM, HR, Procurement, Construction, Design, and Finance. HR is separate from Finance; Legal currently uses the Construction permission group. These are technical access baselines, not an approved six-department organization chart. The 03/10 five-department structure and Procurement/Legal placement remain Q-04. |
| D-02 | The latest approved execution BOQ is selected atomically as final when the Operational Project completes. VOs affect it only through an explicitly approved BOQ revision. |
| D-03 | Verified Punch Items with confirmed Design root cause count equally against the responsible designer; severity is evidence, not a KPI weight. The fixer is not automatically the responsible designer. |
| D-04 | Confirmed HSE violations count equally against the responsible site user. Draft, Reported, Rejected and Cancelled records and diary text do not count. |
| D-05 | Material waste is `100 * max(net posted issues - final BOQ allowance, 0) / allowance`. Missing or zero allowance must not become a synthetic score. |
| D-06 | Procurement delivery measures calendar hours from MR approval to the final posted receipt that completely fulfills the request. Partial receipts do not stop the clock. |
| D-07 | One scorecard version per completed downstream contract; quality, schedule, cost and HSE have equal weights. Procurement prepares it; the project's PM approves it. One approved rating is a valid sample. |
| D-08 | Receivable collection uses upstream milestones due in the month, attributed to the stored responsible accountant. Only full Paid status on/before the due date qualifies; unpaid overdue milestones fail. |
| D-09 | Payment processing measures calendar hours from ValidatedAt to PaidAt, attributed to the assigned accountant. Time before validation, rejected requests and incomplete documents are excluded. |
| D-10 | Each approved post-close accounting correction counts once against the original entry's responsible accountant. Reversing it preserves that count and adds no second count. |
| D-11 | Deploy complete source workflows together. Numeric targets remain administrator configuration; periods cannot lock while any active metric lacks source data or configuration. |

Sources retain explicit project, employee, actor, timestamp, status and version
identities. HSE is separate from Punch Items. Receivable evidence extends
`ContractPaymentMilestone` and its append-only events. Payables use
`PaymentRequest`; post-close corrections use `AccountingPeriod` and
`AccountingCorrection`, not generic audit rows. Procurement ratings require
independent PM approval. Do not infer historical responsibility from current
roles, teams or the user who happens to record a payment.

Source migrations include `AddPunchRootCauseAttribution`,
`AddReceivableAccountability`, `AddHseViolations`, `AddProcurementControlChain`
and `AddFinanceControlWorkflows`. Existing unattributed Punch Items and
milestones remain excluded until explicitly classified or assigned. Do not
synthesize approved events or split historical contract values into lines.
Opening-balance imports require source hash, importer, timestamp, preview,
validation and explicit confirmation. A partial historical month is not a
complete sample. KPI definitions/periods/snapshots have no user-facing hard-delete
operation; deactivation preserves definitions and locked evidence.

Validate source authorization, project scope, role separation, idempotency,
stale writes, duplicate keys, immutable approvals, reversals and unchanged state
after rejection through integration tests. Pure formulas, zero denominators,
targets, weights, precision and Vietnam-time boundaries belong in unit tests.

### 7.20 Module 4 Target Integration Contract

This section records the target contract synthesized for the expanded
Construction and Acceptance module. The 03/10 baseline confirms the CM/BCH
split, resource requests, QA/QC and QS; detailed gates, matrices and templates
remain subject to the open decisions in `docs/Nicon-QLVH.md`. It is not a claim
that every item below is customer-approved or exists in the current API.

#### Construction task drawing links

`GET /api/construction-tasks/{id}/drawings` returns `version`, `linked` and
`available`. Both this route and its `/api/v1` alias require
`construction.tasks.view` and access to the task's project. Candidates are
same-project Shop Drawings in a `Released` IFC packet with a release date;
the drawing itself must still be `Released`. No draft drawing is returned.

`PUT /api/construction-tasks/{id}/drawings` requires
`construction.tasks.manage` and accepts
`{ "version": "<GUID from GET>", "ifcReleaseItemIds": [123] }`.
The list is required, may be empty to unlink, and permits at most 200 distinct
positive IDs. Validation rejects missing, duplicate, foreign-project or
unreleased IDs with 400 without changing links. A stale version returns 409;
retrying the already-persisted desired set returns 200. EF checks the task's
concurrency token and saves the link changes and actor/timestamps atomically.
Concurrent additions that hit the unique link index and referenced records
deleted during a save also return 409; SQL can insert link rows before checking
the parent's version. Retrying the persisted selection returns 200 without
duplicating links or changing the winning version.

`ConstructionDrawingConcurrencyTests` exercises these interleavings on SQL Server
using a generated `NiconDrawingTest_<GUID>` database and a save barrier. Set
`NICON_SQL_TEST_CONNECTION` in an SDK container to a SQL Server connection with
permission to create/drop test databases, then run
`dotnet test nihomebackend.integration.tests/nihomebackend.integration.tests.csproj --filter FullyQualifiedName~ConstructionDrawingConcurrencyTests`.
The fixture replaces the supplied database name and only cleans up its generated
catalog. Without the connection variable this SQL-specific test is skipped;
InMemory tests do not establish SQL unique-index concurrency behavior.

`GET /api/construction-tasks/{id}/drawings/{itemId}/content` uses construction
view permission and project scope, and serves only a linked, released drawing
through the existing managed-file resolver. Missing/unlinked files return 404.
This does not grant access to draft design documents or design-management APIs.

Links are children of the existing compatibility `ConstructionTask`, whose
`DesignProjectId` resolves project access through its Operational Project; no
new project aggregate is introduced. An IFC item is referenced by stable ID.
The displayed R number is the last immutable drawing-revision metadata created
on or before the IFC release date (R0 when none exists); it is not a file diff
or a new versioned-file repository. Released Shop Drawing files remain locked
by the existing design workflow. Removing a link does not delete its drawing,
IFC packet or file. Project aggregate deletion includes the owned links in the
impact plan and removes them before IFC items.

Shop Drawing upload now rejects both `Released` status and membership in a
released IFC packet. Status and file path are EF concurrency tokens, so an
upload racing with IFC release cannot silently replace the issued file; the
losing write returns 409 and upload cleanup preserves the original file.

Migration `AddConstructionTaskDrawingLinks` adds the link table and a task
concurrency token. Existing tasks start with no links; no label-based backfill
is attempted. Rollback drops link data and the token, preserving tasks and
drawings; export links before rollback if they must be retained.

#### Delivered CM/BCH workflow slice

The Operational Project route family
`/api/operational-projects/{projectId}/construction` now exposes versioned CM
preparation/readiness, typed resource requests, QA/QC templates and dossiers,
and direction-explicit QS batches. Mutations require idempotency keys and use
row-version concurrency. Permissions separate BCH create actions from CM
review/start actions, and services reject author self-review. Material approval
reuses `IProcurementService` in the same relational transaction to create and
submit the existing Material Request; equipment/labour records stop at an
explicit downstream-ready state.

The readiness service verifies the signed Upstream construction contract,
released IFC, required valid permits, preparation references, locked Budget
Baseline, and any preparation flags for site handover, method statement and HSE
plan. It persists the evaluated evidence snapshot and actor. Published
preparations are superseded by a new draft rather than overwritten.

QA/QC dossiers require a published multilingual template, a same-project
released IFC packet, valid checklist JSON and active same-project Project
Documents. Each evidence document retains its explicit classification; legacy
document-ID payloads remain accepted without inferring a type from the file name.
QS validates contract direction, dates, quantity-line totals,
milestone/appendix/approved BOQ references, and governed superseding or reversal
references. Submitted records are immutable except through the documented
review/correction lifecycle.
Generic and source-managed document deletion reject QA/QC and QS evidence
links, including draft links. Creating or editing either dossier pins each
active document's row version in the same transaction as its evidence links;
a delete staged before that link commits fails concurrency rather than removing
the file or its source record. Deleted or delete-pending documents cannot be
newly attached. SQL Server regression tests cover both winning orders for
create/update and manual/source-managed deletion.

This is a partial delivery of the target below. WBS/Gantt/S-Curve and Budget
Baseline are reference-backed rather than dedicated editable aggregates; the
reproducible greater-than-5% variance alert is not implemented; equipment and
labour now support the Procurement slice below but no Finance eligibility gate;
and actual NICON ISO
template content is not seeded without an approved source pack. The legacy
Design Project construction aggregates and offline/reconnect behavior also
remain outside this migration.

#### Procurement resource handoff API — Partial

Both `/api` and `/api/v1` expose the following under
`/operational-projects/{projectId}/procurement/construction-resources`:

| Method/path | Permission | Contract |
|---|---|---|
| `GET /` | `proc.resource-requests.view` | CM-approved Equipment/Labour only; optional `type=Equipment|Labour` and `state=AwaitingContract|ContractLinked|Supplied`; project scope enforced |
| `GET /contracts` | same view permission | Minimal same-project Downstream Supply/Subcontract references in Draft/Signed/InProgress |
| `GET /documents` | `proc.resource-requests.manage` | Active same-project document IDs/names; pending deletion excluded |
| `POST /contracts` | same manage permission | VendorId, Type, Value, optional ScopeOfWork; reuses ContractService and creates only a Downstream Draft owned by the actor, using the project's customer; requires an approved equipment/labour request |
| `POST /{id}/contract` | same manage permission | ContractId and RowVersion; one contract per request, many requests per contract; relinking forbidden after supply |
| `POST /{id}/supply` | same manage permission | DocumentId and RowVersion; requires Signed/InProgress contract, records server UTC time, actor and evidence |

All POST operations require `Idempotency-Key`; link/supply accept the existing
body/If-Match concurrency convention. Positive IDs, enum values and token format
are validated; invalid transitions/references return 400, inaccessible projects
or requests return 404, missing permissions return 403, stale versions return
409. Draft Value is a non-negative decimal up to 9,007,199,254,740,991 (the web
client's safe numeric range); optional scope is trimmed, max 20,000 characters.
No supplied timestamp, actor, customer, project or direction is accepted from
the draft/supply body. Contract identity cannot move while resource links exist.

The original `HandoffStatus` remains compatible. New typed contract/supply
fields and append-only handoff events are authoritative for this slice. A single
SaveChanges writes each mutation and event within a relational serializable
transaction; failed decisions do not create supply or Finance state. The web
draft form retains its idempotency key when retrying the same payload after a
lost response. Supply confirmation advances the evidence document's existing
row version in the same transaction. A manual or managed-file deletion staged
against its previous version fails with HTTP 409, rolling back the source
metadata removal before physical cleanup. Existing references reject deletion
with HTTP 400; Drive retry remains available. Contract/project deletion impact
includes the new resource blockers. SQL concurrency regressions use
`NICON_SQL_TEST_CONNECTION` to create and clean only generated
`NiconEvidenceTest_<GUID>` databases; they never use the supplied catalog.

`GET /reports/projects` adds `cmControl` per project: separate active approved QS
directions, QA/QC and HSE counts/statuses/source links, generation timestamp and
`periodPolicy=CurrentState`. Counts are records, not physical quantities or
percent completion. These sources are not historical date-filter snapshots.
Each approved, active QS source also carries its own certified amount and
period; these are individual record facts, not an executed-value aggregate.
Source-permission denial returns Unavailable, a null count and no links. Budget
comparison remains Unavailable; no new financial formula is inferred.

Migration `20261018063226_AddConstructionProcurementHandoff` adds nullable
contract/supply fields, restrictive evidence/contract FKs and the history table.
No existing request is backfilled as supplied. Inspect/generate its SQL and apply
through the actual backend container before using the new API. A code rollback
should retain the additive schema; `Down` drops supply/history data and requires
backup and explicit operational approval once real records exist. SQL Server
application/rollback rehearsal is still required when the Docker stack is
available. Sample An Phu crane/crew requests demonstrate approved/pending work
without fabricating supplied documents, payments or ISO templates.

**Open decision / input gate:** the ISO pack was subsequently uploaded to
`docs/Nicon/ISO/` on 20 October 2026. The earlier absence of source files is
resolved; approved process/step mapping, package-specific payment exceptions,
executed-value formula and reporting period are still missing. Do not publish
production ISO templates or enable Phase 3/4 financial/baseline calculations
until CM/FA/Product confirm those inputs. No fallback document list or formula
is seeded.

#### Kế hoạch hoàn thiện theo phản hồi NICON

Kế hoạch được hợp nhất tại [mục 12](#12-kế-hoạch-hoàn-thiện-theo-yêu-cầu-nicon)
để dùng chung phụ thuộc, điều kiện nghiệm thu và migration với CRM, Thiết kế,
Drive và Tài chính. Các gói ISO/CM/BCH/FA tại đó là **Target**; các ma trận và
công thức chưa được xác nhận vẫn là **Open decision**. Phản hồi nguồn tiếp nhận
18/10/2026 và bộ ISO nhận 20/10/2026 tiếp tục là đầu vào; không coi việc lập kế
hoạch là bằng chứng đã triển khai hoặc NICON đã nghiệm thu.

#### Aggregate and ownership boundaries

Customer clarification received on 18 October 2026 is retained in
[NICON CM/ISO response](Nicon/MoMs/2026-10-18/NICON_Phan-hoi_CM_ISO.md).
Each Operational Project has its own BCH; a person may concurrently belong to
multiple project BCH teams. Membership never grants access to another project
without that project's authorization. After CM approves an equipment/labour
request, Procurement prepares the contract and supplies the site, then FA
processes payment against the applicable dossier. These are confirmed target
rules, not evidence that the downstream handoff is implemented.

The NICON ISO pack is the confirmed template source for every process and step.
The received source files are in `docs/Nicon/ISO/`; the inventory below is a
source check, not an approved mandatory-form matrix. Ask NICON about individual
missing forms and FA (chị K/Kiara) about payment-document gaps. Do not invent
mandatory documents, package-specific exceptions or payment gates from file
names alone.

- An executed Upstream D&B/construction Contract starts CM preparation on the
  existing Operational Project; it must not create a duplicate project.
- The CM preparation plan owns the WBS baseline, critical path, Baseline
  S-Curve, BCH organization, resource plan, and versioned Budget Baseline.
- Field records belong to the project and identify their WBS item, reporting
  date, author, BCH role, lifecycle status, and row version.
- QA/QC documents, Punchlist items, HSE records, resource requests, QS batches,
  and VO dossiers remain separate aggregates linked by stable IDs. Do not use
  Drive paths, labels, or free text as relational keys.
- Module 4 references Procurement suppliers/bids and Module 6 contracts,
  milestones, payment dossiers, and VOs; it does not duplicate their masters.

#### Workflow and controls

1. CM publishes a construction baseline only after WBS, milestones, BCH roles,
   resource plan, and Budget Baseline pass server validation.
2. A signed Upstream D&B/construction Contract makes the project eligible for
   CM preparation but does not authorize site start. The construction-start gate
   also requires applicable IFC releases, valid required permits, published
   schedule baseline, approved BCH/resource plan and locked Budget Baseline.
   Site-handover, method-statement and HSE-plan evidence are template-controlled
   conditions once NICON approves the ISO checklist.
3. BCH submits field evidence from mobile. Draft/offline data is not approved
   evidence and cannot trigger Procurement, payment, or KPI events.
4. Over-limit or unplanned resource requests require an independent CM level-2
   decision before Procurement. Rejection leaves BOQ commitments unchanged.
5. Upstream and Downstream QS records use distinct directions and contract
   links. Approved quantities are immutable; correction uses a superseding
   version or reversal with full audit history.
6. CM compares accepted progress evidence with the published Baseline S-Curve.
   The user confirmed on 24/10/2026 that delay is planned percentage minus
   actual percentage, in percentage points. Only a delay strictly greater
   than 5 points creates a red alert and a required explanation/recovery plan;
   planned 50 versus actual 46 is 4 points, and 50 versus 45 is not red.
   The user also requires dynamic day/month/year periods, not one fixed
   reporting cadence. Actual-progress sources, aggregation across a selected
   period and publish/unlock authority remain NICON/CM decisions. Store inputs,
   baseline version, selected period and result for reproducibility. These
   confirmed rules do not change the current Partial implementation status.
7. The creator of an MR, QA/QC dossier, QS batch, HSE violation, or VO cannot
   satisfy the independent CM review for that record. Enforce this server-side.
8. Completion/handover consumes approved acceptance, QA/QC, as-built,
   commissioning, and blocking-Punchlist state; it never trusts a client-only
   readiness flag.

The confirmed CM portfolio scope is schedule, completed quantities with separate
Upstream/Downstream acceptance, executed value versus estimate, QA/QC acceptance
dossiers and safety. Reporting periods, executed-value definitions and calculation
sources still need an agreed contract; do not combine accepted, committed and
paid values into one undocumented metric.

The target frontend separates three BCH work areas without creating three data
silos:

- **BCH Operations** owns typed resource requests and daily coordination.
- **QA/QC** owns quality dossiers, IFC inspection evidence, Punchlist, diary,
  and HSE records.
- **Site QS** owns direction-explicit Upstream and Downstream quantity batches.

All three use the same Operational Project, WBS/baseline identity, project team,
document service, audit contract, and project access service. A route split must
not duplicate project, supplier, contract, BOQ, or user masters.

Use explicit BOQ provenance. The following are not interchangeable:

| Business artifact | Current/target aggregate | May constrain MR/warehouse? |
|---|---|---|
| Preliminary quotation BOQ | `Quote`/`QuoteItem` or approved material-rate source | No |
| Tender estimate | `TenderEstimateRevision`/lines | No |
| Design quantity takeoff | Design evidence; dedicated governed aggregate remains target | No, unless promoted through an approved execution revision |
| Execution BOQ | `ProjectBoqRevision`/`ProjectBoqLine` with `Approved` status | Yes; select the applicable approved revision atomically |
| Final BOQ | Final approved execution revision after project completion and approved VO revisions | Reporting, closeout and KPI evidence |

Every import or promotion records Operational Project, source artifact/version,
file hash or equivalent provenance, importer, timestamp, preview result and the
approver. Never infer a financial/warehouse allowance merely because an Excel
file came from Module 2 or has `BOQ` in its name.

The PRD target defines BOQ consumption bands at 85% (yellow), 95% (orange), and
above 100% (red/block). Keep these configurable and server-calculated. The
calculation must define approved allowance, net committed/issued quantity,
reversals, unit conversions, decimal precision, and zero allowance. Until that
contract is approved, preserve the current hard validation that rejects a
Material Request above the remaining approved allowance; do not weaken it merely
to display the target bands.

#### Cross-module events and files

| Source event | Required downstream effect |
|---|---|
| Upstream Contract signed | Make CM preparation eligible once, idempotently |
| IFC released | Expose only the released revision to construction users |
| Resource request approved by CM | Hand off the typed request to Procurement without copying the BOQ |
| Upstream QS approved | Make evidence available to the matching receivable/payment milestone |
| Downstream QS approved | Make evidence available to the matching payable dossier |
| VO approved | Version affected budget/BOQ references; never mutate the original baseline silently |
| QA/QC document accepted | Synchronize the managed file to `04_Thi_cong_Nghiem_thu` and retain application metadata |
| Progress variance over 5% | Create one deduplicated red alert per project/baseline/period until resolved or superseded |

For Downstream payments against accepted quantities, the target eligibility
check matches the same vendor, contract, project, currency/amount basis, and
covered quantity/period:

1. a signed Downstream Contract or authorized appendix;
2. a valid posted Warehouse Receipt, net of reversals, when the payable concerns
   supplied material; and
3. an approved Downstream QS acceptance batch for performed/accepted quantity.

The exact applicability matrix for supply-only, labour-only, equipment, and
mixed subcontract packages remains a product decision. The received purchasing
procedure allows a supplier payment request before receipt against an order;
the payment procedure and `TQT-M07` also contain advance requests. Whether,
when and with which authorization/evidence these advances can enter Finance is
an open FA/CM decision. Do not apply the accepted-quantity gate to them by
default or treat missing receipt/QS as an automatic exemption. Do not invent a
dummy receipt or QS record to satisfy matching. A failed match returns actionable
differences and preserves Payment Request, ledger, notification, and audit state
apart from the allowed rejection/audit record.

External file writes use the existing durable document-operation pattern. The
database transaction records authoritative metadata and an outbox/pending
operation; a Drive failure must not falsely mark a dossier synchronized.

#### Required validation

- Unit tests: S-Curve variance, threshold boundary (`5%` is not `> 5%`), budget
  roll-up, BOQ remaining quantity, QS totals, and baseline version selection.
- Integration tests: project/contract scope, BCH-versus-CM permissions,
  separation of duties, lifecycle transitions, stale row versions,
  idempotency, rejection with unchanged downstream state, and Upstream versus
  Downstream payment linkage.
- Integration tests for Drive cover pending, retry, conflict, and failure
  states; mocked metadata alone is not evidence of successful external sync.
- Browser tests cover the mobile BCH happy/negative paths and the CM desktop
  review/alert paths, including loading, empty, error, offline/retry, and
  responsive states.

### 7.21 NICON Customer Reference Data

#### Source inventory and precedence

`docs/Nicon/` is the customer-evidence location:

- the full PRD defines the eight-module scope, cross-functional handoffs,
  eleven target positions, KPI weights, Drive tree, and UI expectations;
- the workflow DOCX is historical context and is superseded where the canonical
  `docs/Nicon-workflow.md` reflects later meeting decisions;
- `MoMs/2026-10-06/NICON_Meeting_2026-10-03_Tom-tat-yeu-cau-moi.md` is the
  controlling business baseline for the current alignment. Other files in that
  dated folder are supporting synthesis/snapshots and cannot silently close its
  open questions;
- schedule PDFs demonstrate real WBS hierarchies, durations, start/finish,
  predecessors, parallel permit/design activities, milestones, and baselines;
- `BOQ/` contains heterogeneous one-sheet and multi-sheet workbooks used to
  discover import variability;
- `ISO/` contains the customer-uploaded common document control, internal audit
  and improvement processes plus detailed customer, tender, design,
  construction, payment, purchasing, equipment and HR processes. All 170 files
  received on 20 October 2026 were opened or classified: 108 Word files, 40
  workbooks, three PDFs (two without `.pdf` suffix), two RAR archives and 17
  Office lock/temporary files. These counts describe the upload, not approved
  or usable template versions. The pack has no `D.5` folder; ask NICON whether
  one is expected before calling the process set complete.

Receipt and source mapping do not establish current ISO certification,
conformity, signed release authorization or business acceptance. The source
review does not verify signatures or certificates. Preserve customer originals;
do not rename conflicting form codes or publish completed examples as blank
production templates without the authorized NICON owner's confirmation.

The file-level ISO cross-check gives these source-to-workflow anchors:

| Source | Observed evidence | Mapping still needed |
|---|---|---|
| `D.1` customer (`KH`) | Customer list, customer information/confirmation, contract and complaint records | `KH-M01` labels both a client-list workbook here and an information-collection form in `D.2`; `KH-M02`'s process label differs from its file name. Confirm codes and pre-contract handoff |
| `D.2` tender (`DT`) | Competitive quotation and tender paths, bid work assignment, supplier quotations and contract handoff | `DT-M02` is cited as a quotation but its file is named handoff record; referenced `DT-M03` is absent. Reconcile current tender gates with the older source |
| `D.3` design (`TK`) | Preliminary, basic and technical design handoffs; `TK-M01`–`TK-M03` and appendices | Procedure also cites `TK-BM02`/`TK-BM03` or `KT-BM03` where files use `TK-M02`/`TK-M03`. Several `TK-M02` files are completed project handoffs, not blank templates. Match current design gates and released IFC rules; permit references do not settle the permit matrix |
| `D.4` construction (`TC`) | Preparation `TC-M01`, shop/material submittals `TC-M04`–`TC-M06`, diary `TC-M08`/`TC-M09`, acceptance `TC-M24`, 19 workbooks under `TC-M28` | No counterparts were identified under referenced codes `TC-M02`, `TC-M23` and `TQT-M00` in the received files/archive entries; NICON must confirm equivalents or renamed forms. `NS1-M01` is cited for forming BCH but its file is a recruitment request. `TC-M15` names two different handoffs; the procedure's `TC-M21`/`TC-M22` labels are reversed in filenames. The `TC-M24` file here is a completed acceptance record. No separate sampling, lab-result, material-certificate, method-statement or HSE-plan form was identified; confirm applicability and source with NICON |
| `D.6` payment (`TQT`) | Separate payment, subcontractor settlement and customer settlement; `TQT-M01`–`TQT-M09` and references to `MH`/`TC` forms | FA/CM must approve the four-package document matrix. No counterpart was identified under code `TC-M00`; NICON must confirm an equivalent or renamed form; this folder's `TC-M24` is a different handover form under the same code. `TC-M14`, `TC-M27` and `TC-M29` also appear in `D.4`. Conversion of `TQT-M04` exposes `=#REF!+C13` in `MAU GIA TRI PS!C20`; verify in Excel before reuse |
| `D.7` purchasing (`MH`) and `D.8` equipment (`LK`) | `MH-M01`–`MH-M08` for requests, quotations, orders and supplier review; `LK-M01`–`LK-M08` for equipment receipt, issue and maintenance | `MH-M03` contains proposal/order sheets and a completed example. `MH-M04` payment request prints `MH-BM02` inside, the same internal code as `MH-M02` quotation request. The procedure permits a supplier payment request before receipt against an order; FA/CM must resolve that case separately from accepted-quantity matching |
| `C.1` document control (`QTC-KSTLHS`) | §5.1: PTBP → ĐDCL → GĐ approval; TKISO identifies, distributes and retrieves superseded documents. §5.3: custodians and retention lists BM05/BM06. §5.4: GĐ authorizes destruction recorded in BM07 | Open decision — GĐ/ĐDCL/TKISO must confirm electronic delegation to CM/Admin, release/supersession and records retention/destruction policy. Product-level legal checklist publication does not implement or certify the full QTC workflow |
| `C.2` internal audit and `C.3` improvement | Audit schedule/findings/corrective actions and nonconformity handling, with accompanying `BM` files | Confirm current ownership, severity and closure rules; these are source processes, not delivered audit modules |
| `D.9`–`D.11` HR (`NS1`–`NS3`) | Recruitment/training, work reporting, leave and resignation records. `NS2` calls for daily/weekly/monthly reports reviewed by department heads and summarized for management; `NS2-M01` has 11 scored individual criteria | `NS1-M05`/`NS1-M06` are referenced for training but absent; `NS1-M03`/`NS1-M04` are PDFs without extensions. `NS2` does not define approval of each internal task or the software KPI calculation. Confirm current HR policy and access before mapping personal records |

The next mapping level is the **observed process step**, not a publishable
checklist. References below are to the [TC construction procedure](<Nicon/ISO/D.Quy trình chi tiết/D.4. Quy trình thi công lắp đặt/Quy trình thi công lắp đặt.doc>),
the [TQT payment procedure](<Nicon/ISO/D.Quy trình chi tiết/D.6. Quy trình thanh toán, quyết toán công trình/Quy trình thanh quyết toán.doc>)
and the [MH purchasing procedure](<Nicon/ISO/D.Quy trình chi tiết/D.7. Quy trình mua hàng, đánh giá nhà cung ứng, thầu phụ- co chinh sua/Quy trình mua hàng, đánh giá NCC.doc>).
The process documents name historical owners; they do not set current CM/FA
approval permissions.

| Source step | Files/records the procedure names | Mapping boundary to confirm |
|---|---|---|
| TC 1–4, preparation | `DT-M09`, `NS1-M01`, `TC-M02`, `TC-M01`, `TC-M04`–`TC-M06`, `TC-M25`; schedule, site survey, BCH structure, method and safety measures | No counterpart was identified under code `TC-M02`; confirm equivalents with NICON; `NS1-M01` file is a recruitment request. Identify the approved BCH, method and HSE evidence and project-type applicability before making a start gate mandatory. Current signed-contract/IFC/baseline/budget gate still governs. |
| TC 8, field operation | `TC-M07`–`TC-M13`, `TC-M15`, `TC-M18`, `TC-M20`; the text also requires approved shop drawing, method and material sample before each work stage | Map each existing diary/QA/HSE record to one source step and released IFC revision; resolve the duplicate `TC-M15` code. No ISO S-Curve formula appears here. |
| TC 10/12, acceptance | `TC-M28` internal work-stage sheets, `TC-M23` invitation, `TC-M24` acceptance and `TC-M22` defects | No counterpart was identified under code `TC-M23`; confirm equivalents with NICON; the `TC-M24` files are filled/different records. CM must select current blank forms and define which of the 19 `TC-M28` work types apply. |
| TC 11/13 and TQT 5.1–5.3, settlement | `TQT-M00`, `MH-M04`, `TQT-M01`–`TQT-M06`, `TC-M14`, `TC-M27`, `TC-M29`; signed quantity, invoice and contract evidence are described at different stages | No counterparts were identified under codes `TQT-M00` and `TC-M00`; NICON must confirm equivalents or renamed forms. Keep Upstream and Downstream separate; TQT 5.2 cites `TQT-M02` for subcontractor settlement although the received file is labeled customer payment request. Confirm code/version and three-section dossier ownership. |
| MH 1–6, purchase | `MH-M01` request, `MH-M02` quotation, `MH-M03` order, `MH-M04` payment request and receipt/invoice when goods arrive | The procedure allows payment request before receipt when the order requires it. Confirm advance authorization separately from accepted-quantity payment; current Procurement handoff supersedes the older Accounting-ordering role. |

For the four Downstream package types, the ISO supports these **discovery
candidates**, not a mandatory document matrix:

| Package | Candidate evidence found | Remaining FA/CM decision |
|---|---|---|
| Supply | `MH-M01`–`MH-M04`, order, received-goods record and invoice in MH/TQT; warehouse receipt applies to goods actually received | Required set by payment stage, approved quantity source, advance/retention and exceptions. |
| Labour | `TC-M27`, `TC-M29`, `TQT-M05`/`TQT-M06` and approved site quantity in TC/TQT | Distinguish labour team from subcontractor and identify contract, payroll/attendance or acceptance evidence actually required. |
| Equipment | `LK-M01`–`LK-M08` cover equipment handover, stock and maintenance; MH describes purchase/order | Decide purchase versus rental, supply period/usage evidence, ownership and which LK records apply; none alone proves payment eligibility. |
| Mixed subcontract | TC/TQT contain work acceptance and subcontract settlement forms, but no separate mixed-package rule | Identify line-level material/labour/equipment evidence, allocation and payment gate without requiring a fake warehouse receipt. |

The `TC` and `TQT` source procedures show effective dates in 2016 and 2020
respectively. Their role names, submission weekdays and retention periods are
historical source content, not automatically approved current product rules.
The ISO answer-by-question review is recorded in `Nicon-QLVH.md` section 12:
its historical process steps answer parts of the handoff, reporting and payment
questions, while product permissions, matrices, formulas and migration rules
remain open. In particular, the 11 individual scoring criteria in `NS2-M01`
are distinct from the PRD's 11 target KPI positions, and the 5% retention or
contract-example figures do not specify a schedule-variance alert formula.
The older construction procedure starts from a tender result/approved price,
while the current contract requires a signed Upstream contract and a separate
server start gate; the older purchasing procedure assigns ordering to Accounting,
while the later confirmed handoff assigns it to Procurement. Preserve the
current canonical workflow pending explicit reconciliation. Several files are
completed customer/project records, and some workbooks contain example sheets
and formulas; none is automatically a blank publishable template. The inner
`TC-M28` archive repeats the 19 extracted workbooks byte-for-byte; the outer
construction archive largely repeats the folder, with one `01-Dao dat.xls`
binary variant that has the same extracted cell text. The pack does not itself
resolve the four-package payment matrix, the project-specific QA/QC checklist
or current template versions. Several legacy `.xls` forms use font-dependent
Vietnamese text and formatted ranges much wider than their populated cells;
verify rendered labels and formulas in the source application before defining
an import schema. Keep the existing document workflow and snapshot/version
boundaries until the business owners confirm the mapping. Source examples:
[construction](<Nicon/ISO/D.Quy trình chi tiết/D.4. Quy trình thi công lắp đặt/Quy trình thi công lắp đặt.doc>),
[payment](<Nicon/ISO/D.Quy trình chi tiết/D.6. Quy trình thanh toán, quyết toán công trình/Quy trình thanh quyết toán.doc>),
[purchasing](<Nicon/ISO/D.Quy trình chi tiết/D.7. Quy trình mua hàng, đánh giá nhà cung ứng, thầu phụ- co chinh sua/Quy trình mua hàng, đánh giá NCC.doc>).

Source files are read-only evidence. Never rewrite, normalize, translate, or
re-export them as an implementation side effect. Canonical docs summarize the
approved contract; code and tests prove delivery status.

The PRD phrase "8 layers" is not a folder-count contract: its illustrated tree
contains six numbered top-level business folders plus three Design subfolders.
The current code exposes nine `ProjectDocumentCategory` values because Survey
and CRM Pre-Design are separate categories. Current defaults are:

| Category | Current configured path |
|---|---|
| Survey | `01_Khao_sat` |
| CRM Pre-Design | `01_CRM_PreDesign` |
| Design Concept | `02_Thiet_ke/01_So_bo_Concept` |
| Design Basic | `02_Thiet_ke/02_Co_so` |
| Design Shop Drawing | `02_Thiet_ke/03_Chi_tiet_ShopDrawing` |
| Legal | `03_Xin_phep_Phap_ly` |
| Construction/Acceptance | `04_Thi_cong_Nghiem_thu` |
| Procurement | `05_Cung_ung_Vat_tu` |
| Finance/Contracts | `06_Tai_chinh_Hop_dong` |

The business target nests Survey beneath CRM Pre-Design. Treat the current two
paths as a compatibility contract until an approved topology migration disables
sync, drains active claims, inventories remote/local bindings, handles name and
content conflicts, moves or rebinds files, verifies checksums, and provides
rollback. Changing the configuration alone does not move historical files.

#### Schedule import boundary

A schedule import must use an explicit parser profile/version and a preview
before persistence. The normalized draft model retains source file hash,
source row/task ID, task name, hierarchy/outline level, duration and unit,
planned start/finish, milestone flag, predecessor links with relationship/lag,
calendar, constraints when supported, and baseline fields. Validation rejects
missing parent/predecessor references, cycles, invalid dates/durations, duplicate
source IDs, and finish-before-start values.

Import creates a Draft schedule version. Publishing is a separate authorized
transition; it never silently replaces the active design or construction
baseline. Re-import of the same file/profile is idempotent. A changed file must
create a new version or an explicit diff, not overwrite actual progress.

#### BOQ workbook import boundary

The samples include cover/summary sheets, building and discipline sheets,
single-sheet quotations, bilingual labels, formulas, merged cells, repeated or
project-specific names, and material lists. Consequently:

1. discover all visible and hidden sheets and preserve workbook/sheet order;
2. classify sheets as ignored metadata, summary, or detail using a user-reviewed
   mapping rather than a hardcoded name list;
3. read cached formula results only when their provenance is retained and report
   missing/stale cached values instead of recalculating with a different engine;
4. normalize item code, description, unit, quantity, unit price, hierarchy and
   optional discipline only after preview;
5. validate precision, sign, required values, duplicate identity, merged-cell
   ambiguity and totals by exact sheet/cell location;
6. reconcile recognized detail totals with recognized summary totals and show
   unexplained differences;
7. persist source hash, importer, timestamp, parser/mapping version, per-sheet
   decision and row-level outcome; and
8. create a Draft revision only after explicit confirmation. Approval remains a
   separate lifecycle action and the current approved BOQ remains unchanged on
   any rejected or partial import.

File-size, sheet-count, row-count, formula, macro/external-link, and processing
time limits require explicit configuration and four-language validation
messages. Unit tests own mapping/normalization and formula-value decisions;
integration tests own upload/auth/scope/idempotency/persistence; representative
fixture workbooks derived without customer-sensitive values cover single-sheet,
multi-sheet, merged-cell, formula, duplicate, malformed, and partial-failure
cases.

---

## 8. Frontend Development

### 8.1 Technology Stack

| Technology       | Purpose                            |
|------------------|------------------------------------|
| React 18         | UI framework                       |
| TypeScript       | Type safety                        |
| Vite             | Build and dev server               |
| Tailwind CSS     | Utility-first CSS framework        |
| shadcn/ui        | Radix-based component library      |
| React Router     | Client-side routing                |
| Redux            | Authentication state management    |
| Playwright       | Browser E2E testing                |

### 8.2 API Service Modules

API calls are organized into typed modules under `src/services/`. `authApi.ts` owns authentication, `contentApi.ts` owns public content, `adminApi.ts` owns shared administration and several operational contracts, and focused modules such as `rbacApi.ts`, `crmApi.ts`, `designApi.ts`, `permitsApi.ts`, and construction service modules own their respective domains. Extend an existing domain module before introducing a parallel client.

### 8.3 Build Commands

| Command              | Purpose                            |
|----------------------|------------------------------------|
| `npm run dev`        | Start development server           |
| `npm run build`      | Production build                   |
| `npm run build:dev`  | Development build                  |
| `npm run lint`       | Run ESLint                         |
| `npm run test:e2e`   | Run Playwright browser tests       |
| `npm run preview`    | Preview production build locally   |

### 8.4 Admin CSV Export

Admin list exports are implemented on the frontend with `src/lib/exportCsv.ts` and `src/components/admin/AdminExportButton.tsx`. The helper writes UTF-8 BOM CSV output so Excel opens Vietnamese, Chinese, and Japanese text correctly without adding an `.xlsx` dependency.

Export buttons must preserve the current filters and sort order, contain the complete filtered result rather than only the visible page, and remain disabled when there are no rows. Small, unpaginated lists can export loaded rows on the frontend. A bounded, low-volume list may retrieve all matching API pages before generating the file. High-volume exports and exports that require server-side authorization or audit records must use a dedicated backend endpoint. The as-built dossier follows the backend pattern through `GET /api/as-built-documents/export` and records the export in the audit log.

The project handover list follows the same backend-export pattern through `GET /api/handover-records/export`. Its CSV includes a UTF-8 BOM, preserves active filters and sorting, records an audit event, and prefixes formula-like cell values so spreadsheet applications do not execute them.

### 8.5 Project Handover Frontend

The protected route is `/admin/construction/handover`. `HandoverRecordsPage.tsx` provides responsive list/card views, server-side filtering and pagination, CSV export, create/edit dialogs, readiness details, and lifecycle actions. Permission constants live in `src/lib/adminPermissions.ts`, and typed requests/responses live in `src/services/adminApi.ts`.

Document values are not interpolated directly into anchors. Use the shared URL helper in `src/lib/url.ts`, which accepts host-relative paths beginning with a single `/` and absolute HTTP(S) URLs. Protocol-relative URLs, dangerous schemes, and malformed values remain non-clickable.

### 8.6 Project Handover API Contract

Both `/api/handover-records` and `/api/v1/handover-records` expose the same controller:

| Method | Route | Permission | Purpose |
|--------|-------|------------|---------|
| `GET` | `/api/handover-records` | `construction.handover.view` | Filtered, sorted, paginated list and summary counts |
| `GET` | `/api/handover-records/export` | `construction.handover.view` | Complete filtered CSV export |
| `GET` | `/api/handover-records/{id}` | `construction.handover.view` | Detail, readiness, and status history |
| `POST` | `/api/handover-records` | `construction.handover.manage` | Create the project's single handover record |
| `PUT` | `/api/handover-records/{id}` | `construction.handover.manage` | Update Draft/Reopened data |
| `POST` | `/api/handover-records/{id}/status` | `construction.handover.manage` | Perform non-final lifecycle transitions |
| `POST` | `/api/handover-records/{id}/complete` | `construction.handover.complete` | Complete a ready, signed handover |
| `DELETE` | `/api/handover-records/{id}` | `construction.handover.manage` | Permanently delete a record in any status |

`view.all` controls unrestricted reads; `manage.all` independently controls unrestricted writes. A caller with only the base permission is scoped to records they created or own and projects they manage or lead. Business-rule failures return `400`, hidden/missing records return `404`, and duplicate or concurrent writes return `409` so clients can reload instead of overwriting newer data.

Readiness is derived on the server from approved partial acceptance, required approved as-built categories, unresolved punch items, commissioning, and checklist completion. Clients must display this result and must not duplicate it as an authoritative frontend calculation.

The current unique owner key is `DesignProjectId`; this is an explicit
compatibility boundary, not the target aggregate. Resolve and authorize the
owning Operational Project before cross-module access. A future migration to
`OperationalProjectId` must preserve the one-handover invariant, status history,
row versions, documents, scope rules and deep links, and must define how to
handle a construction project with no Design Project before adding the new key.

### 8.7 Procurement Vendor Frontend

The protected routes are `/admin/vendors` and `/admin/vendors/:id`. The list provides server-side search, vendor-type and active-state filters, company-name sorting, pagination, and responsive table/card views. The create/edit dialog retains entered values when the API rejects a request. The detail page shows company, contact, document, status, and audit metadata and resolves persisted document links through `src/lib/url.ts` before rendering them.

CSV export preserves the active filters and sort order. Because the vendor API currently caps pages at 100 rows and has no dedicated export endpoint, the client retrieves every matching page before passing the complete result to `src/lib/exportCsv.ts`. If vendor volume or export auditing requirements grow, replace this batching with an authorized backend export endpoint.

---

## 9. Testing

### 9.1 Backend Tests

Backend unit tests are located in `nihomebackend.tests/`.

The Compose backend mounts only `nihomebackend/`, so sibling test projects are not available through `docker exec nihome31042025-backend`. CI runs the test projects in a .NET 8 SDK checkout:

```bash
dotnet test nihomebackend.tests/nihomebackend.tests.csproj
dotnet test nihomebackend.integration.tests/nihomebackend.integration.tests.csproj
```

The test project structure:

```
nihomebackend.tests/
  Controllers/     -- Controller unit tests
  Services/        -- Service unit tests
  Mappings/        -- AutoMapper profile tests
  Helpers/         -- Test helper utilities
```

Unit tests cover isolated service logic, validation, branching, helpers, and mappings. HTTP status codes, model binding, authorization, middleware, and persistence round-trips belong in `nihomebackend.integration.tests`. Current integration tests use EF InMemory, so they do not prove SQL Server relational constraints or SQL Server-specific behavior.

### 9.2 Browser E2E Tests

Frontend behavior is validated through Playwright against the integrated Docker stack. Pure service logic and HTTP contracts belong in backend unit and integration tests respectively.

The focused `admin-navigation.spec.ts` checks client-side navigation from Roles,
browser Back/Forward and unsaved permission drafts across refetches. Verify
rendered destination content as well as the URL: a changed URL alone does not
prove navigation completed. The Roles editor combines query data with a stable
function before synchronizing drafts; do not depend on the raw `useQueries`
result array in a state-writing effect, because its identity changes per render.

```bash
docker compose up -d --build
cd nihomeweb
BASE_URL=http://localhost:5043 npx playwright test
```

CI divides the browser suite into two Playwright shards. Each shard starts an
isolated Docker Compose stack and runs with one worker because browser specs
share seeded SQL data and some deliberately exercise mutable RBAC and
configuration records. This preserves deterministic cleanup while the two
isolated databases run concurrently. Reproduce either CI shard locally with:

```bash
cd nihomeweb
CI=1 BASE_URL=http://localhost:5043 npx playwright test --shard=1/2
CI=1 BASE_URL=http://localhost:5043 npx playwright test --shard=2/2
```

Design browser flows that approve durable evidence, including Basic Design
and recipient review, require a connected Google Drive. They check the live
Drive status before creating test data and report a skip when it is not
connected; a failed status request remains a test failure. A skipped run is
not evidence of a passing Drive workflow. Run these specs against a connected
Compose stack to validate synchronization, recipient access and file downloads.

### 9.3 Manual As-Built Smoke Test

With the Docker Compose stack running, open `http://localhost:5043/login` and sign in with the development `SUPER_ADMIN` account defined by the current backend seeder or Playwright auth fixture. Then open **Admin > Construction > As-Built Records**, or navigate directly to `http://localhost:5043/admin/construction/asbuilt`.

1. Select an existing design project and confirm the summary cards and document list load without an error.
2. Create a uniquely titled **Drawing** document and confirm it appears with **Draft** status.
3. Search for the title, change the category/status filters, and select **Recently updated** sorting. Confirm the displayed rows match each selection.
4. Export the filtered list and confirm a file named `as-built-documents-YYYY-MM-DD.csv` downloads and contains the created document.
5. Open the document, submit it, and approve it. Confirm the lifecycle history shows each transition and the approved-category completeness count increases.
6. Confirm the approved document is read-only, then archive it and verify its final status.
7. Repeat the page check at mobile and tablet widths; filters and records must remain readable without horizontal page overflow.

For the authorization check, sign in as the seeded `SALE` account defined by the current business-role seeder or Playwright auth fixture and confirm the page is forbidden. If this account can access the page, inspect its assigned role before reporting a product defect: a long-lived local database may contain customized user-role assignments. Do not change the expected deny behavior or reset persistent data without reviewing those assignments.

The focused automated equivalent is:

```bash
cd nihomeweb
BASE_URL=http://localhost:5043 npx playwright test e2e/smoke/admin-asbuilt.spec.ts --grep "SUPER_ADMIN" --output=/tmp/nihome-playwright-asbuilt
```

Writing Playwright artifacts to `/tmp` prevents the backend file watcher from restarting when the frontend directory is mounted into the development container.

### 9.4 Manual Project Handover Smoke Test

With the stack running, sign in as `SUPER_ADMIN` and open `http://localhost:5043/admin/construction/handover`.

1. Confirm the list, summary, filters, sorting, pagination, and responsive card/table layouts render.
2. Create a Draft record for a project without an existing handover; add commissioning data, checklist items, a safe document URL, and a signatory.
3. Confirm readiness reflects approved partial acceptance, required as-built approvals, unresolved punch items, commissioning, and checklist completion.
4. Mark the record ready and complete it. Confirm completion is unavailable without a signatory and that the detail view records the actual date and status history.
5. Reopen the record and confirm it becomes editable; verify a stale concurrent update returns HTTP `409` and requires a reload.
6. Export CSV and confirm the file contains all filtered rows, not only the visible page.

The focused browser check is:

```bash
cd nihomeweb
BASE_URL=http://localhost:5043 npx playwright test e2e/smoke/admin-handover.spec.ts --workers=1 --output=/tmp/nihome-playwright-handover
```

### 9.5 Linting

Backend:

```bash
docker exec nihome31042025-backend dotnet format --no-restore --verify-no-changes
```

Run after dependencies have been restored in the backend container. Compose
allocates 1 GB of shared memory to this service because Roslyn's temporary
memory-mapped storage can exhaust Docker's default 64 MB and crash with an
`AccessViolationException`. After changing `shm_size`, recreate the backend with
`docker compose up -d --no-deps nihomeBackend`; restarting it does not apply the
new allocation.

Frontend:

```bash
cd nihomeweb
npm run lint
```

Translation keys:

```bash
python tools/translation_keys/check_translation_keys.py
python -m unittest discover -s tools/translation_keys -p "test_*.py"
```

The checker scans frontend/backend literal keys, dynamic key families and keys
derived from notification, master-data and RBAC seeds. It also verifies that
every seed row has Vietnamese, English, Chinese and Japanese values. Missing
keys are printed in a summary and detail table and return exit code `1`;
repository/read errors return `2`. The CI `translations (missing keys)` job
publishes the Markdown table to the GitHub Job Summary before enforcing the
exit code, so a failed check retains the complete actionable list.

### 9.6 Quality Check Summary

| Check              | Command                              |
|--------------------|--------------------------------------|
| Backend build      | `docker compose build nihomeBackend` (runtime image has no SDK) |
| Frontend build     | `npm run build`                      |
| Backend tests      | `dotnet test nihomebackend.tests/nihomebackend.tests.csproj` in CI or an SDK test environment |
| Backend integration tests | `dotnet test nihomebackend.integration.tests/nihomebackend.integration.tests.csproj` |
| Browser E2E tests  | `cd nihomeweb && BASE_URL=http://localhost:5043 npx playwright test` |
| Backend lint       | `docker exec nihome31042025-backend dotnet format --verify-no-changes` |
| Frontend lint      | `cd nihomeweb && npm run lint`       |
| Translation keys  | `python tools/translation_keys/check_translation_keys.py` |
| Docker full build  | `docker compose up --build`          |

---

### 9.7 Business Pipeline Validation

Validate all eight business modules and public/identity/shared operations.
Customer entry paths include Design & Build, design-first with later construction,
competitive tender and consultation/quotation. Preserve Customer → Operational
Project → Contract/Design Project identity through each supported handoff; do not
seed unrelated intermediate records and call that a complete pipeline.

| Area | Joined behavior to validate | Existing test entry points |
| --- | --- | --- |
| CRM | Qualification/conversion, quotation approval, tender outcomes, signing and project inheritance | `CrmBusinessPipelineTests`, `crm-business-pipeline.spec.ts` |
| Design and permits | Concept → Basic → Shop Drawing → IFC release/acknowledgment, scoped legal checklist | `DesignPermitIfcPipelineTests`, `DetailDesignScheduleControllerTests` |
| Construction | Tasks/diaries, acceptance rejection/revision, punch verification, as-built readiness, handover completion/reopen | `ConstructionHandoverPipelineTests`, `construction-handover-pipeline.spec.ts` |
| Procurement | Approved BOQ → demand approval → receipts/issues/reversals → source evidence | `ProcurementBusinessPipelineTests` |
| RFQ and finance | Tender/BOQ provenance → quote revisions/withdrawals → award → signed downstream contract → warehouse → Paid/Rejected/Cancelled invoice | `RfqProcurementPipelineTests`, `rfq-business-pipeline.spec.ts` |
| RFQ display | Active/inactive vendors, zero versus missing prices, responsive comparison and translated counts | `RfqsControllerTests`, `rfq-active-vendor-highlighting.spec.ts`, `admin-rfqs.spec.ts` |
| Reports and KPI | Created business events appear in read models; missing sources remain unavailable; frozen snapshots and supported reversals are respected | Project report, KPI and source-workflow tests |
| Shared/public | Public enquiry/application → authorized staff handling; permission revocation, file access and aggregate cleanup | Contact, recruitment, RBAC, document and hard-delete tests |

Integration classes live under `nihomebackend.integration.tests/Controllers`;
browser scenarios live under `nihomeweb/e2e/smoke`. These are starting points for
review, not a claim that every possible scenario has executable coverage.

For each workflow, test wrong role/project/owner, invalid fields, denied state
skips, required reasons, stale versions, idempotent replay and conflicting
payloads. Change dependencies after form load (membership, vendor activity, BOQ
approval or required evidence) and verify rejection leaves state, history and
bindings unchanged. Use ordinary business roles to prove handoffs. In browsers,
check error/empty/loading/retry states, retained form values, keyboard access,
mobile/tablet layout, translations and persistence after reload.

Pure logic belongs in unit tests; HTTP/auth/persistence belongs in integration;
rendering and deployed-stack wiring belong in browser E2E. API-only Playwright
requests are not proof of browser interaction. InMemory EF does not prove SQL
constraints, locking or transaction rollback. Concurrent awards, warehouse
posting/reversal and failure recovery need isolated SQL execution. Live Drive,
email and external ownership/cleanup require configured services; mocks cannot
prove transport. Record actual run results and blockers in the task/PR or test
artifacts rather than adding dated reports under `docs/`.

Do not infer unsupported behavior from conceptual workflow diagrams: automatic
exchange-rate feeds, invoice matching,
actual finance ledgers, offline synchronization, automatic permit approval and
public-contact-to-CRM conversion require their own implemented contracts and
verification. Passing a global suite proves its assertions, not exhaustive
customer-scenario coverage.

---

## 10. Deployment

### 10.1 Docker Compose Development

```bash
docker compose up --build -d
```

Services and ports:

| Service     | Port | Notes                                          |
|-------------|------|------------------------------------------------|
| Backend API | 5043 | Hot-reload enabled in development               |
| SQL Server  | 1433 | Data persisted in `sqlserver_data` Docker volume |

The backend container mounts source directories as volumes for hot-reload. Named volumes isolate `node_modules`, `bin`, `obj`, and NuGet packages to avoid host/container conflicts.

### 10.2 Production Checklist

Before deploying to production:

1. Change all default user passwords.
2. Rotate JWT signing keys and update `appsettings.json`.
3. Set `ASPNETCORE_ENVIRONMENT` to `Production`.
4. Configure CORS to allow only the production frontend domain.
5. Use a secrets manager for sensitive configuration (database credentials, SMTP credentials, JWT keys).
6. Enable HTTPS with a valid TLS certificate.
7. Review and restrict the SQL Server `sa` account; create a dedicated application user with limited permissions.
8. Configure log aggregation and monitoring.
9. Review migration scripts and account for the current startup behavior, which automatically runs `Database.Migrate()` and seeding outside `IntegrationTests`.
10. Build the release with `auto-deployment.sh`; it publishes the backend and compiled SPA into `deployment-config/output/publish-release.zip` for IIS hosting.
11. Confirm the CI publish job is gated by required build, test, E2E, and security jobs before relying on its release artifact; the current workflow does not declare those dependencies.
12. Verify PDF fonts before switching traffic. Docker builds enforce Noto Sans and Noto Sans CJK automatically. On Windows IIS, install Arial, Microsoft YaHei, and Meiryo for the application-pool account, or configure the `NIHOME_PDF_FONT_LATIN_PATH`, `NIHOME_PDF_FONT_ZH_PATH`, and `NIHOME_PDF_FONT_JA_PATH` environment variables. Set the matching `_INDEX` variable when a configured file is a TrueType collection.

The production artifact includes `web.config` for the ASP.NET Core Module and serves the compiled SPA from `wwwroot`. `NIHOMEWEB_DIST_PATH` can override the frontend distribution directory at runtime; startup fails if the configured directory does not exist. Swagger is Development-only and should not be enabled by switching production to the Development environment.

### 10.3 Google Drive credentials on IIS

Project document synchronization uses Google Drive API v3 with OAuth user authorization. All Google Drive business configuration is managed from **Settings > Google Drive** and stored in the singleton `google_drive_credentials` row. The backend encrypts the write-only client secret and OAuth refresh token with separate ASP.NET Core Data Protection purposes. Neither value is returned by the Admin API. Never put Google passwords or OAuth values in source-controlled configuration, logs, screenshots, or support messages. A missing settings row is treated as disabled, and workers perform no Drive I/O until an administrator enables a valid configuration.

#### Create the OAuth credential

1. In [Google Cloud Console](https://console.cloud.google.com/), select the Nicon project and enable **Google Drive API**.
2. Configure Google Auth Platform with an External audience. While the app is in Testing, add the Google account that owns the target Drive folder as a test user.
3. Add only the `https://www.googleapis.com/auth/drive` scope.
4. Create a **Web application** OAuth client. A Desktop client is insufficient for a deployed HTTPS callback.
5. Register the exact backend callback, for example `https://nicon.example.com/api/site-settings/google-drive/oauth/callback`, as an authorized redirect URI.
6. Keep its client ID and client secret private for entry on the Admin page. Do not create or copy a refresh token manually.
7. Copy `<FOLDER_ID>` from `https://drive.google.com/drive/folders/<FOLDER_ID>` for the root that will contain Nicon project folders.

After extracting `publish-release.zip` on IIS:

1. Set `DataProtection:KeysPath` in protected deployed configuration, or `DataProtection__KeysPath` in the application-pool environment, to a durable directory such as `D:\\NiconSecrets\\DataProtection-Keys`.
2. Grant Modify access on that directory only to the IIS application-pool identity and responsible administrators. The key ring must survive application recycle and deployment; losing it makes encrypted SQL secrets unreadable and requires entering the client secret and reconnecting.
3. Sign in with `system.settings.manage`, open **Settings > Google Drive**, enter and save the OAuth identity, exact callback URI, internal Admin return path, root folder, stable deployment instance ID, application name, all business folder paths, Drive compatibility mode, polling interval, and enabled state. Blank client-secret input preserves the stored secret; changing the client ID or secret clears the existing OAuth connection.
4. Select **Connect Google Drive**. Nicon opens Google's sign-in and consent screen in a popup and closes it after the callback. The callback validates state, PKCE, current Admin permission, and root-folder write capability before saving the connection.
5. Confirm status is `Connected`, then perform a controlled project upload. Saved settings are loaded dynamically; application recycle is not required to enable, disable, or update the integration.

To use another account, select **Disconnect** and then **Connect Google Drive**, or select **Switch Google account** to perform those steps in sequence. Nicon clears the local credential before asking Google to revoke it, then opens Google's account chooser. A failed provider revocation does not retain local Nicon access; the administrator receives a warning with instructions for manual revocation. Cancelling or failing the replacement authorization leaves Nicon disconnected. Disconnecting Nicon does not sign the user out of their broader Google browser session.

`InstanceId` must be stable and unique per deployment; it prevents one Nicon environment from claiming another environment's Drive replicas. Folder paths form the centralized registry beneath `RootFolderId` and are resolved segment by segment. Google Drive settings are intentionally not bound from `appsettings.json` or `GoogleDrive__*` environment variables. Only the Data Protection key-ring path remains deployment infrastructure because storing it in the encrypted database would create a decryption bootstrap cycle.

`FrontendReturnUrl` must remain an internal `/admin/` path, without backslashes,
fragments or control characters, including when saving disabled drafts. The
OAuth result redirect revalidates persisted legacy values and falls back to
`/admin/settings?tab=drive`. Health and redirect construction read public
configuration independently of secret decryption; an unreadable stored
credential returns `ReconnectRequired` rather than an unhandled health error.
Restore the correct Data Protection keys or re-enter the OAuth secret before
reconnecting if the key ring was lost.

The migration intentionally leaves upgraded installations disabled with empty deployment-specific fields. During a planned maintenance window, an administrator must save the former business configuration on the Admin page and reconnect before synchronization resumes. Although the existing encrypted refresh-token column is preserved, the first Client ID assignment clears that token because the application cannot prove it belongs to the newly entered OAuth client. For later topology changes with existing bindings, first save synchronization as disabled and wait for active `Processing` records to finish. Save the new root folder, deployment identity or business paths while still disabled, then re-enable in a separate save. The server rejects topology changes while processing is active or existing bindings are enabled. Nicon invalidates cached project-folder bindings so future operations resolve beneath the new topology. Existing catalog rows and Drive files are retained; the application does not move or delete historical files automatically.

Project document storage is Drive-primary after synchronization. Manual uploads
are queued in private host storage while Drive is unavailable; the worker
verifies the remote byte count and SHA-256 before removing the staged copy.
Authenticated downloads serve host or Drive content according to sync state.
Document deletion through Nicon moves its Drive replica to trash. SQL remains
authoritative for catalog, authorization, claims and workflow state.
Reconciliation catalogs unknown files without a host copy and reflects remote
changes. Source-module files retain their compatibility storage and lifecycle.
Drive sharing and permission synchronization remain disabled until IT supplies
approved group mappings.

Manual project uploads require an `Idempotency-Key`. Nicon fingerprints multipart
fields and file bytes independently from the browser-generated boundary, so a
sequential or concurrent retry of the same upload intent creates at most one SQL
catalog row and one Drive object. Reusing the key with another payload returns
`409`; the React upload form retains its key after a failed request and clears it
when the selected file, category, or Operational Project changes. Current
Operational Project scope is checked before fingerprint comparison or cached
response replay. A caller whose project access was revoked receives `404` and
cannot recover prior upload metadata by replaying or changing the payload.

The background worker skips claims while synchronization is disabled. When due
work exists, it validates that the configured root is a live writable folder
before claiming a row. Read-only, invalid-root, unavailable, or revoked-access
states leave pending rows unchanged without consuming one of their three
attempts. The connection response displays the authenticated account for
administrators to verify against the deployment setup.

Connection statuses have the following meanings:

- `Connected`: a Google account is authenticated through OAuth, the root is a live folder, and Drive reports permission to add children.
- `ReadOnly`: the folder is visible but Drive reports no permission to add children.
- `InvalidRoot`: `RootFolderId` points to a non-folder item or an item in trash.
- `ReconnectRequired`: the refresh token is missing, expired, or revoked; an administrator must reconnect once in Settings.
- `Unavailable`: authentication or folder access failed.

Each project document permits at most three automatic worker claims per retry
cycle. An authorized user can select **Retry**, including after attempt three,
to reset the attempt count and queue a new cycle after correcting the cause.
`Processing` rows reject retry until the active claim completes or is recovered.

While a legacy sidecar upload is running, a separate scoped database heartbeat renews its claim every five minutes. Losing claim ownership cancels the Drive request and SQL generation fencing rejects stale completion. If a crash or network race still leaves multiple remote files with the same Nicon replica key and generation, reconciliation preserves the SQL-bound replica and moves the extras to Drive trash.

If the application stops during an active claim, the expired lease resumes the same numbered attempt instead of consuming another one. This includes attempt three; the Drive idempotency property lets recovery reconcile a remote file that may already have been created.

Retry and deletion are rejected while a row is `Processing`. This preserves the active SQL lease and prevents an in-flight upload from creating an untracked Drive file; users can retry or delete after the worker completes or its lease is recovered.

The response includes only safe identity/folder metadata and never includes the credential path, client secret, refresh token, or private key.

For Docker development, save the OAuth configuration through **Settings >
Google Drive**, using the exact loopback callback registered with Google.
Compose persists the Data Protection key ring in the `data_protection_keys`
volume; business OAuth settings are read from the encrypted database row.

```bash
docker compose up -d --build --force-recreate nihomeBackend
```

While synchronization is disabled, the worker leaves pending rows unclaimed at
their current attempt count. When enabled, connection validation also prevents
claims until OAuth values and `RootFolderId` identify a writable folder. A
provider failure after a successful validation and claim still consumes an
attempt.

OAuth apps left in Testing can issue refresh tokens with a limited lifetime. Before production use, move the app to Production and complete any Google verification required for the Drive scope. The Google SDK automatically exchanges the stored refresh token for short-lived access tokens. A revoked refresh token cannot be silently renewed; health status becomes `ReconnectRequired` and an administrator must approve access again.

Official references: [Drive API v3](https://developers.google.com/workspace/drive/api/reference/rest/v3), [files.get](https://developers.google.com/workspace/drive/api/reference/rest/v3/files/get), [OAuth scopes](https://developers.google.com/workspace/drive/api/guides/api-specific-auth), [Shared Drives](https://developers.google.com/workspace/drive/api/guides/about-shareddrives), and [Drive API errors](https://developers.google.com/workspace/drive/api/guides/handle-errors#storageQuotaExceeded).

### 10.4 Deterministic demonstration data

Development-only sample seeders create a compact deterministic demonstration
dataset: two leads, five customers/operational projects, six opportunities,
eight quotes, six contracts, three design projects, two surveys and two tenders.
The linked examples cover pre-contract concept, quotation, contracting,
permitting, construction, acceptance, as-built and handover. The main chain is
the fictional Nhà máy may An Phú at KCN Quế Võ; a separate fictional Nhà máy
cơ khí Hòa Bình customer/project carries two RFQs, an approved electrical BOQ,
vendors and competing bids. Names are fictional but describe the work and
project context. Display names omit the `[SAMPLE]` prefix; stable codes and
internal notes identify demo-owned data.
KPI seed entries are metric definitions, not fabricated performance results.
Seeder-owned rows use stable internal markers such as `[SAMPLE_CONTRACT]`
and `[SAMPLE_DP]`;
downstream records are attached only to marker-owned sample projects rather than
arbitrary database rows.

The dataset is idempotent on a fresh Development database: rerunning sample
seeding preserves row counts and administrator-edited free text, lifecycle
values, and relationships. Reducing the manifest does not delete previously
seeded rows from an existing database; existing local demo data may therefore
contain more records than the counts above.
A constrained backfill pass populates only missing sample relationships, including
opportunity/quote links, project/customer/contract links, and PM/design-lead
assignments. Sales records are owned by the SALE demo user, project and
construction records prefer PM, design documents prefer DESIGN_LEAD then DESIGN,
and permit work prefers LEGAL_OFFICER with safe fallbacks.

When a web root is available, the seeder materializes small placeholder PDFs beneath `wwwroot/files/capability/`, `wwwroot/files/contracts/`, and `wwwroot/files/asbuilt/`; URL metadata is still seeded when no web root is supplied. These files and all named contacts, phone numbers, and email addresses in sample rows are demonstration data, not real personal or customer data, and must not be treated as production records.

---

## 11. Troubleshooting

### 11.1 Database Connection Failures

**Symptom**: The backend fails to start with a database connection error.

**Resolution**:
- Verify SQL Server is running: `docker ps | grep sqlserver`
- Check the connection string in `appsettings.json` or the Docker Compose environment variable.
- From the host use `localhost,1433`; from a Compose service use `sqlserver,1433`; from a separate Docker container use `host.docker.internal,1433` when supported by the platform.
- The current Compose health check only verifies that the SQL Server process exists; confirm an actual database connection when diagnosing readiness.

### 11.2 Migration Errors

**Symptom**: EF Core migration fails to apply.

**Resolution**:
- Review the migration file for conflicts.
- Use a .NET 8 SDK environment with `dotnet-ef` 8.x; the running development image currently lacks the tool.
- Check the current database state with `dotnet ef migrations list`.
- Remove only an unapplied last migration with `dotnet ef migrations remove`.
- Generate a review script with `dotnet ef migrations script`.

### 11.3 CORS Errors

**Symptom**: The frontend receives CORS errors when calling the API.

**Resolution**:
- Verify each entry in `Frontend:AllowedOrigins` matches the frontend origin exactly (including protocol and port).
- Restart the backend after changing CORS configuration.

### 11.4 Email Sending Failures

**Symptom**: OTP emails or contact reply emails are not delivered.

**Resolution**:
- Verify the SMTP settings in `appsettings.json`.
- Check that the SMTP server is reachable from the backend container.
- Review application logs for detailed error messages from MailKit.

### 11.5 JWT Token Issues

**Symptom**: API returns 401 Unauthorized for authenticated requests.

**Resolution**:
- Verify the access token has not expired.
- Use the refresh endpoint to obtain a new access token.
- Ensure the JWT `Issuer` and `Audience` settings match between token generation and validation.
- If signing keys were rotated, ensure the `ActiveKeyId` matches a valid key in the configuration.

The SPA restores its current user through `GET /users/me` on reload, retaining
the existing tokens. It rotates the refresh token only after an access-token
401. A transient profile/refresh failure preserves the tokens and offers retry;
an invalid session returns to login. Refresh-token rotation remains single-use.

### 11.6 Docker Volume Issues

**Symptom**: Code changes are not reflected in the running container.

**Resolution**:
- Rebuild the immutable image with `docker compose up -d --build`.
- Check `/api/system/health` and confirm `buildSha` matches the expected source revision.
- Do not remove `sqlserver_data` to refresh application code; that volume contains business data.

### 11.7 Running an old binary

**Symptom**: The health endpoint reports a different `buildSha` than the source
revision selected for E2E.

**Resolution**:
- Rebuild and recreate the backend with `BUILD_SHA=$(git rev-parse HEAD) docker compose up -d --build --force-recreate nihomeBackend`.
- Confirm the new `buildSha` before starting Playwright.

---

## 12. Kế hoạch hoàn thiện theo yêu cầu NICON

### 12.1 Phạm vi và nguyên tắc triển khai

Kế hoạch bao phủ toàn bộ 14 mục của
[baseline họp 03/10/2026](Nicon/MoMs/2026-10-06/NICON_Meeting_2026-10-03_Tom-tat-yeu-cau-moi.md),
đọc cùng [yêu cầu hiện hành](Nicon-QLVH.md),
[workflow](Nicon-workflow.md), `Nicon_BreakTask_v1.xlsx` và hiện vật khách hàng
liên quan. Giữ các xác nhận mới hơn: hai hướng HĐ Upstream ngày 16/10, BOQ cấu
hình ngày 17/10, phản hồi CM tiếp nhận 18/10 và ISO nhận 20/10. Không quay lại
quy tắc cũ đã được thay thế, cũng không suy code hiện tại thành quyết định mới.

Mọi gói trong mục này là **Target**. Chỉ một phần đã có bằng chứng thì ghi
**Partial** kèm khoảng trống trong các mục mô tả năng lực; nhánh chưa có quyết
định có thẩm quyền giữ **Open decision**. Không đánh dấu **Implemented** từ kế
hoạch, endpoint, menu hoặc test file chưa chạy. Trước từng gói, rà lại source và
test mới nhất; lỗi đã sửa ở nhánh khác phải kiểm chứng trước khi làm trùng.

Đích bàn giao là luồng nghiệp vụ đầy đủ qua persistence, API có authorization,
UI, tài liệu, seed và kiểm thử phù hợp. Ưu tiên CRM + Thiết kế; sửa quyền và bảo
toàn bằng chứng xuyên module trước để không mở rộng workflow trên dữ liệu thiếu
an toàn. Không tự mở rộng sang ERP, arbitrary Excel fidelity, full `.mpp`
import hay Drive ACL ngoài phạm vi đã được NICON xác nhận. Các đầu việc Target
trong BreakTask, gồm báo cáo tài chính/kho và chức năng chung, vẫn phải được map
và xếp đợt; chưa được duyệt thì giữ quyết định/phạm vi rõ, không âm thầm loại bỏ.

Đây là kế hoạch triển khai, chưa phải thay đổi business contract. Không tự đặt
ngày hoàn thành hoặc effort khi chưa chốt đầu vào, năng lực đội và đối chiếu
BreakTask. Sau discovery, đội phát triển và Product Owner phân bổ từng gói theo
capacity thực tế; mọi mốc phải có đầu ra và điều kiện thoát dưới đây.

Workbook được đối chiếu ở chế độ chỉ đọc: `Nicon!A1:D1` ghi lần rà source
`7813bc5d`, không chạy lại test/không xác nhận host; các đầu việc Drive/project
reports tại `'Cập nhật Demo Ngày 2506'!A17:C19` và chức năng chung tại
`'Tiến độ hiện tại'!A102:H102` cần được giữ trong traceability. Không dùng cột
estimate/status của workbook làm lịch cam kết hoặc bằng chứng nghiệm thu mới.

### 12.2 Quyết định nghiệp vụ và đầu vào bắt buộc

Ghi quyết định ngay tại [mục 12 của yêu cầu](Nicon-QLVH.md#12-các-quyết-định-bắt-buộc-còn-mở),
kèm người có thẩm quyền, ngày/nguồn, phương án được chọn, actor, lifecycle,
data/API contract, acceptance criteria và tác động dữ liệu cũ. Huỳnh Anh là đầu
mối khách hàng; Product Owner/người được NICON ủy quyền chốt rule, CM/FA/Thiết
kế xác nhận nội dung chuyên môn. Việc giao xử lý quyết định không tự có nghĩa
người đó đã chấp thuận phương án.

| Quyết định / đầu vào | Cần chốt cụ thể | Gói phụ thuộc |
|---|---|---|
| Q-01 | Giữ/chuyển/gắn nhóm tiền hợp đồng; cách phân bổ khi một dự án có nhiều HĐ; contract link ở từng purpose có được thay đổi không | ROOT-01, WORK-01, COMM-01 |
| Q-02 | Việc nội bộ có cần duyệt; metric, trọng số, kỳ khóa và điều kiện tính KPI | WORK-01, REPORT-01 |
| Q-03 / Q-04 | Ai khởi tạo giao liên phòng, ngoại lệ admin; cơ cấu năm phòng và vị trí Cung ứng/Pháp lý; bộ quyền mẫu | RBAC-01, ROOT-01, WORK-01 |
| Q-05 | Quy ước mã, normalization, uniqueness, alias và thời điểm được đổi/khóa | CRM-01; sửa stale Drive rename không cần đợi quy ước mới |
| Q-06 / Q-07 / Q-10 | Recipient chỉ bình luận hay cần đồng thuận; CC mặc định; ranh giới Lead duyệt việc và Manager chốt giai đoạn | DESIGN-01, DESIGN-02 |
| Q-08 | Cấp quyền xóa hồ sơ không đạt; hồ sơ từng trình khách; retention, bản phát hành và evidence liên quan | SAFE-02, RBAC-01, DRIVE-03; ngăn wildcard cấp nhầm quyền có thể làm trước |
| Q-09 | Permit applicability theo loại/quy mô, mandatory/exemption và template version | LEGAL-01 |
| Q-11 | Màn hình Đội ngũ/Tiến độ; WBS/calendar/baseline/import scope; mốc Project/Kinh doanh ban hành và mốc Design đề xuất | DESIGN-03; không coi có file `.mpp` là đã duyệt import contract |
| ISO / CM / FA | Map quy trình → bước → mã/phiên bản/file nguồn → form/checklist; xử lý mã thiếu/trùng; Supply/Labour/Equipment/Mixed, advance/prepayment và chứng từ áp dụng | ISO-01, CM-01, FIELD-01, FIELD-02, QS-01, FIN-01 |
| Approval / calculation | Delegation và independent review; baseline publish/unlock; nguồn actual và tổng hợp kỳ S-Curve; executed value; BOQ allowance/zero/reversal/conversion; KPI còn thiếu. Người dùng đã chốt ngày 24/10/2026: delay = planned% − actual% theo điểm phần trăm, đỏ khi >5 điểm; kỳ linh hoạt ngày/tháng/năm | CM-01, SUPPLY-01, REPORT-01 |
| Approval hiệu lực | Cancel/withdraw nhóm Finance có thu hồi approval không; nội dung/phiên bản nào thay đổi thì phải rà lại | COMM-01, TENDER-01 |
| Nguồn báo cáo / chức năng chung | Actual cashflow, cost/P&L/inventory sources, kỳ đối chiếu; phạm vi global search/saved filters và data source KPI còn thiếu | FIN-02, REPORT-01, COMMON-01 |
| Document policy | Actor × source/category × action; historical approved bytes/retention; import-to-dossier; IFC-specific upload; phạm vi scan/native export và migration Survey. Giữ sharing/ACL tắt theo quyết định hiện tại; chỉ mở lại khi có yêu cầu mới được duyệt | SAFE-01, SAFE-02, DRIVE-02, DRIVE-03 |

Các rule đã rõ được làm trước; không chờ toàn bộ bảng mới sửa quyền, evidence,
task approval bypass, tender terminal guard hoặc stale rename. Phần chưa chốt
không được publish thành template bắt buộc hay silently chọn giá trị mặc định.
Không yêu cầu gửi lại toàn bộ ISO/BOQ/MS Project đã nhận; đọc hiện vật và hỏi
đúng phần còn thiếu. Mẫu/giá trị/màu/ngày/tên khách trong evidence không tự
trở thành business rule.

### 12.3 Thứ tự, phụ thuộc và mốc bàn giao

| Đợt | Gói | Đầu ra để chuyển đợt |
|---|---|---|
| 0 — Chốt contract và tái hiện | PLAN-01, ISO-01 discovery | Traceability từng bullet, phân biệt lỗi/gap/open decision, actor/state matrix; đưa probe cần thiết vào regression đúng tầng; quyết định có owner |
| 1 — Bảo vệ luồng đang có | SAFE-01/02/03, DRIVE-01, TENDER-01 guards, QA-01 | Không còn bypass đã xác nhận về quyền, xóa evidence, approval cũ, sửa terminal plan; health/redirect/rename an toàn; các test nền liên quan xanh |
| 2 — CRM và công việc chung | RBAC-01, ROOT-01, WORK-01, CRM-01, COMMON-01 discovery | Một root dự án; role/department đúng quyết định; giao việc và notification mở đúng scope; nội bộ tách riêng; dữ liệu cũ tương thích |
| 3 — Thiết kế và Drive | DESIGN-01/02/03/04, DRIVE-02/03 | Business journey từ Opportunity đến Concept/Basic/Shop/IFC, recipient, tài liệu/phiên bản và folder-init có bằng chứng; không chỉ upload General |
| 4 — Thương mại và đấu thầu | COMM-01, BOQ-01, TENDER-01 hoàn chỉnh | Giá đầu vào → báo giá/Finance → HĐ; tender plan/dossier/submit/result có snapshot bất biến; BOQ đúng phạm vi được duyệt |
| 5 — Pháp lý và ISO áp dụng | LEGAL-01, ISO-01 publish theo quyết định | Permit/task/applicability/activation không trùng; form/checklist có nguồn và phiên bản được duyệt |
| 6 — CM, BCH và cung ứng | CM-01, FIELD-01/02, SUPPLY-01, QS-01 | Readiness gate và baseline cấu trúc; typed resource handoff, QA/QC/HSE/QS độc lập; evidence và offline/reconnect an toàn |
| 7 — Hồ sơ tài chính và báo cáo | FIN-01/02, REPORT-01, COMMON-01 hoàn chỉnh | Hai hướng hồ sơ, đủ ba phần và chứng từ áp dụng mới gửi/chi; KPI/portfolio/báo cáo tài chính-kho có nguồn và phạm vi đã chốt |
| 8 — Chuyển dữ liệu và nghiệm thu | MIG-01, RELEASE-01 | Rehearsal/rollback, docs/demo, BA → QA → NICON UAT trên bản deploy xác định; không còn High/Critical mở |

Đây là thứ tự bàn giao; không phải phải chờ hết một đợt mới discovery đợt sau.
Contract QS/Finance và template phải thiết kế thống nhất trước code liên kết.
Phần TC–KT tiền hợp đồng nằm ở COMM-01, không chờ đợt 7 hoặc bàn giao công trình.
LEGAL-01 và IFC của Design cấp bằng chứng cho CM-01; published baseline và
approved Execution BOQ cấp giới hạn cho SUPPLY-01. DRIVE và SAFE áp dụng xuyên
mọi đợt. Discovery ISO/BOQ/schedule và thiết kế read contract có thể tiến hành
khi chờ quyết định, nhưng không bật tính toán/gate chưa được duyệt.

### 12.4 Backlog tính năng và tiêu chí hoàn thành

Các đường code nêu dưới đây là điểm mở rộng cần khảo sát lại, không phải cam
kết tên endpoint/schema mới. Mỗi gói phải chốt DTO/validation/state/concurrency
và migration trước khi code; ưu tiên reuse controller/service/UI đang có.

| Gói | Thêm/cập nhật đầy đủ | Điều kiện nghiệm thu và kiểm thử chính |
|---|---|---|
| PLAN-01 — Contract và truy vết | Map từng bullet MoM và BreakTask liên quan → actor/rule/lifecycle → persistence/API/UI/seed → AC/test; giữ current subset và target tách biệt | Không bỏ mục nhỏ vì module đã có menu; mỗi gap/finding có gói xử lý, mỗi quyết định có owner; BA xác nhận test basis trước QA |
| SAFE-01 — Authorization thống nhất | `ProjectAccessService`, `ProjectDocumentService`, Design/Permits controllers và task mutation: permission + active project/source scope; sửa Design Manager upload fallback bị filter chặn và member đọc evidence bị 404 | Assigned Design upload được; QAQC/QS/Legal đọc đúng evidence; ngoài dự án/ended membership/CC không có quyền mutation bị chặn; list/count/download không leak Finance hoặc tài liệu nguồn ngoài quyền. Integration qua API, có negative source-permission cases; không cấp blanket view-all |
| SAFE-02 — Bảo toàn evidence và xóa | Dùng document/deletion services bảo vệ QA/QC/QS, review/submission và consumer tài chính; kiểm mọi generic delete, managed-source replacement/delete, remote edit/trash và aggregate deletion | Từ chối không đổi DB/file/history/job; approved dossier không âm thầm trỏ bytes mới. Chốt reject/version/archive và retention trước triển khai lưu bytes lịch sử; mọi hard delete có impact, typed code, plan token, concurrency, ownership/cleanup. SQL race và file/Drive tests, không chỉ FK/InMemory |
| SAFE-03 — Approval đúng nội dung | `DetailDesignScheduleService` và workflow consumers: API update không hoàn thành lách Pending; approval gắn revision/nội dung, không giữ Approved cho scope/người/lịch/dependency đã thay | Complete → Pending và notification; đúng một independent assigned approver; CC không approve; reject có lý do, resubmit đúng revision. Edit approved bị chặn hoặc tạo revision theo contract đã chốt; stage gate không nhận approval cũ. Unit rules + HTTP bypass/concurrency tests |
| QA-01 — Test foundation | Chuyển probe tái hiện thành regression tracked; sửa translation assertion lỗi thời, SQL fixture thiếu storage collaborator; SDK test runtime đủ fonts; live smoke thiếu credentials phải skip rõ | Các test hiện hữu đã sửa chạy pass trên final tracked source. Không thay wording staged/Pending thành synced để làm test xanh; không dùng fixture audit hoặc silent-return live test thay execution thật |
| RBAC-01 — Phòng ban và bộ quyền | Role/group editor, baseline import snapshot, editable labels qua translations, design approve/transition/read-only scope; reconcile wildcard hard-delete và default grants có kiểm soát | Role mới nhận bộ quyền phòng theo lựa chọn đã chốt; không escalation/retroactive reset; DESIGN_LEAD không vô tình được highest-tier delete. Q-04/Q-08 chi phối cơ cấu/thẩm quyền; inventory custom grants trước migration, không ghi đè role người dùng. API matrix và UI bốn tiếng |
| ROOT-01 — Dự án chung và entry points | OperationalProject là root; Kinh doanh tạo; phòng nhận workspace/gói việc sẵn, thông tin liên kết bằng stable ID; entry từ Project/Opportunity dùng chung rules | Không tạo lại customer/project theo phòng; không tự gắn việc vào HĐ đầu tiên. Nhiều HĐ theo Q-01, scope theo Q-03; Design/Construction giữ lifecycle chuyên môn. Integration cross-project/cross-contract và browser handoff |
| WORK-01 — Việc phòng ban/nội bộ | `DepartmentWorkPackageService`, UI và notifications: chủ trì giao đúng người/phòng, lịch/WBS/dependency/progress; tab nội bộ không customer/project/HĐ; KPI theo Q-02; thống nhất create/update/import | Giao trùng/stale edit/reconnect không tạo record/notification trùng; dependency và lifecycle được server enforce; kết thúc assignment chặn thao tác cũ. Notification lỗi có durable retry/dedup và deep link đúng task theo quyền hiện tại; không resend lịch sử hàng loạt chưa kiểm chứng |
| CRM-01 — CRM và mã dự án | Giữ thứ tự Project/Opportunity/Contract; Lead riêng, converted khóa; cá nhân không bắt TaxId; rename theo quy ước đã duyệt, history/unique/stable links; hai hướng Upstream được xác nhận | Validate format/length/range/normalization ở FE và server; duplicate/concurrent rename không mất liên kết. Hướng quote dùng approved/version/đúng project; direct agreed-value vẫn hợp lệ. Unit/HTTP, SQL unique/concurrency và live browser |
| DESIGN-01 — Từ Cơ hội và phân vai | Kinh doanh handoff trước HĐ vào Concept; Manager điều phối nhiều nhóm, Lead chia subtask/duyệt trực tiếp, người khác CC; sửa complete/review kiểm active assignment | Opportunity → Design idempotent; Design không tạo business root. Full role/state journey complete/reject/resubmit/approve, author không tự duyệt, người bị thu hồi scope không xử lý. Không đồng nhất Design Manager với PM toàn dự án |
| DESIGN-02 — Trình nội bộ/khách | Recipient-scoped entry point và download/comment, Manager tổng hợp → trình khách → record decision → stage gate; review evidence đúng revision/hash | Người ngoài project được chỉ định mở đúng review và file, không được đọc toàn project/dossier khác; comment không biến thành approve. Gate client acceptance/bước kế tiếp đúng contract Q-06/Q-10; test entry notification, replay/stale/negative decision, remote bytes thay đổi |
| DESIGN-03 — Tiến độ và đội ngũ | WBS hierarchy, mô tả, planned/actual dates, assignee/approver/CC, predecessor/successor, relation/lag/calendar/resource/milestone/critical path; nguồn mốc Project/Kinh doanh và Design đề xuất | Source-derived schedule fixtures giữ hierarchy/dependency/duration/critical/baseline theo import scope đã duyệt. Không flatten PDF/MPP thành checklist. Pagination đủ mọi task/member/source, không mất dữ liệu trên 100 records; UI WBS/Gantt liên kết đội ngũ, mobile/tablet. Q-11 trước import/approval provenance mới |
| DESIGN-04 — Thầu phụ, hồ sơ và IFC | Task gắn Vendor/HĐ Downstream đúng project; shortcut NCC/HĐ đã lọc; nhiều file mỗi option/stage; tab Documents tổng hợp đủ trang; upload IFC-specific theo contract; released IFC tới Construction | Manager dùng đúng supplier/RFQ/contract quyền được cấp; kết quả vào source/stage đúng. Draft/unreleased IFC không dùng thi công; không copy master data hoặc mở supplier data ngoài scope. HTTP source/vendor/contract validation và live journey Design → IFC → Construction |
| DRIVE-01 — Độ tin cậy và cấu hình | `GoogleDriveOAuthService/SettingsStore/ProjectDriveSyncService`: health khi decrypt lỗi, return URL an toàn cả disabled/callback, stale rename ordering, topology drain guard; đồng bộ docs retry | Unit và API phân biệt Disabled/Connected/ReadOnly/InvalidRoot/ReconnectRequired; cũ A→B retry không ghi đè C; stale config/Processing không mất binding. Secret write-only, PKCE/state/actor/concurrency giữ nguyên; không log token hoặc thử outage bằng cách phá kết nối người dùng |
| DRIVE-02 — Folder-init và catalog | Durable intent tạo cây Design khi handoff trước HĐ, kể cả zero file; reuse folder identity; upload mọi stage/source, checksum readback, verified host cleanup; retry/lease/fencing/import/conflict/native metadata | Drive lỗi không rollback business handoff hay báo folder đã có; Pending/Failed/retry nhìn thấy và phục hồi không trùng folder/file. Live kiểm đúng source workflow, không chỉ 9 General uploads; remote move/edit/trash/reconnect và mobile weak-network có bằng chứng. Scope scan/native export không tự mở rộng; sharing/ACL giữ tắt theo quyết định hiện tại và không là blocker nghiệm thu sync |
| DRIVE-03 — Document coverage và lịch sử | Inventory Quote/Contract/Survey/Design/Permit/Acceptance/As-built/Handover bridges; bổ sung contract còn thiếu cho Tender, Finance, diary/Punch, QA/QS ISO export; source binding cho imports theo quyết định | Source owns lifecycle, managed file phải tồn tại/đúng project/permission; upload/replacement/delete cùng metadata/durable side effect, rollback không orphan. Drive-import không tự thành approved Design evidence; legacy backfill riêng, history không tự tạo từ dữ liệu hiện tại; giữ Survey/CRM bindings đến migration duyệt |
| COMM-01 — Giá đầu vào và Finance tiền HĐ | Input offer independent lifecycle/expiry, quote snapshot; Finance đúng quote/tender content version; gate send/submit; thống nhất cancellation/withdrawal/material change và ContractId trên mọi write path | Sai purpose/project, expired/unapproved offer, self-review, stale content và bypass bị từ chối; rejected operation không side effect. Chỉ bật cancellation semantics đã chốt; input pricing/preliminary BOQ không thành Execution BOQ allowance. HTTP + live Procurement → Quote → Finance → Send |
| BOQ-01 — Giá và workbook | UnitCost/BOQ, quote version, một current approved version, totals/rounding/VAT/discount; multi-sheet/configured catalog chuẩn, preview/mapping/revision/hash; nâng hierarchy/provenance/re-import nếu được duyệt | Đọc sample single/multi-sheet/cover/bilingual/merged/formula trước import contract; import lỗi không partial write. Giữ original workbook; typed numeric/formula restrictions hiện hành không tự đổi. Khi nâng model giữ parent/sheet/row/cell/UID phù hợp quyết định; không bịa provenance cũ hoặc hứa arbitrary Excel recalculation |
| TENDER-01 — Kế hoạch tới kết quả | Full invitation → phòng lập plan → dossier/library/estimate/Finance → submit → result; khóa mọi edit/reopen/task/dependency sau terminal, dates không mất/qua deadline; full immutable submission snapshot | Snapshot tái dựng plan/tasks/actor/dates/dependencies, evidence revision/hash, estimate, Finance checklist/findings/decision/version, receipt/channel/time. Check gate và snapshot nhất quán trong transaction; source thay không đổi lần nộp cũ; resubmit thêm lần, retry không trùng. Unit/HTTP/SQL race; Won → Negotiation → HĐ đúng root |
| ISO-01 — Form/template được duyệt | Đọc ISO thực tế, map process/step/source version/field/checklist/applicability; báo riêng file thiếu/trùng mã; quản trị/publish độc lập và retention theo thẩm quyền | CM/FA/NICON chấp thuận mapping, required/optional/exemption và form UI có validation đầy đủ; raw schema/results JSON không thay form nghiệp vụ. Published version/snapshot bất biến; template mới không sửa dossier lịch sử; các required language fields đầy đủ trên mọi write path |
| LEGAL-01 — Pháp lý theo từng permit | Giữ Published-template wizard tương thích; sau Q-09 thêm applicability/tự sinh khi activation; PM phân ban đầu, chủ trì phân từng permit cho active member dù khác phòng; task/lịch/duyệt/evidence riêng | Activation atomic/idempotent, không permit/task trùng; không phụ thuộc phải tạo Design project. Permit nội bộ và kết quả cơ quan giữ lifecycle đã chốt; missing/expired/invalid evidence chặn gate. Tests applicability/legacy/mandatory/revocation/concurrency; snapshot mẫu và dữ liệu cũ giữ nguyên |
| CM-01 — Chuẩn bị và baseline | Structured WBS/Gantt/calendar/critical path/S-Curve, BCH organization/resource plan và locked Budget Baseline có version; signed Upstream D&B/construction chỉ cho chuẩn bị | Start gate kiểm released IFC, permit hợp lệ bắt buộc, published schedule, approved BCH/resource và locked budget; handover/method/HSE theo approved profile. Lưu input/version/actor; không overwrite published baseline, scope/budget changes theo VO. Tests bypass/stale evidence/version/concurrency; không tự derive baseline từ free text |
| FIELD-01 — BCH/CM và QA/QC/HSE | Tách field author và CM independent review; ISO forms cho acceptance/sample/lab/CO-CQ/material cert/diary theo applicability; project/WBS/evidence/timestamps/audit | Một người được tham gia nhiều BCH nhưng scope từng dự án độc lập; author không review hồ sơ mình. Mobile-first draft/pending/error/conflict, retry/offline không tạo trùng và không tự merge approved data. SQL/API file integrity; live BCH submit → CM review trên mobile/tablet/desktop |
| FIELD-02 — Nghiệm thu tới bàn giao | QA/QC/HSE/Punch/diary/acceptance/as-built/handover giữ record/lifecycle riêng, source document links và checklist ISO; tiếp tục existing gate/correction/reopen audit | Completion không bỏ mandatory acceptance/as-built hoặc unresolved blockers theo contract; hard delete vẫn theo aggregate policy. Test source revision/dependency/state/reconnect và browser handover journey; không coi một checklist generic là thay mọi record |
| SUPPLY-01 — Nguồn lực tới FA | Material/Equipment/Labour có WBS/quantity/unit/date/reason; Material reuse MR/applicable approved Execution BOQ, over-limit/unplanned independent CM level-2; máy/nhân công → Procurement → HĐ Downstream/supply → FA | Không suy CM Approved là đã mua/cung cấp/chi; wrong project/vendor/direction/contract/allowance và self-approval bị chặn. Retry handoff không tạo HĐ/payment trùng; source links bền. Nonmaterial không ép warehouse receipt; partial supply/cancel/amend chỉ sau rule được chốt |
| QS-01 — Hai hướng khối lượng | Upstream gắn Owner contract/receivable milestone; Downstream gắn NCC/thầu phụ/tổ đội/payable dossier; stable WBS/BOQ/VO/evidence; approved quantity immutable, correction/reversal lineage | Không suy hướng từ dấu tiền, không double-count superseded/reversed batch; downstream consumer nhận source không còn hiệu lực. HTTP/SQL direction/project/milestone/totals/concurrency; retry CM approval không sinh duplicate payment |
| FIN-01 — Dossier thanh toán hai chiều | Mở rộng tương thích PaymentRequest Downstream, thêm Upstream dossier; ba phần Công trường/CM/Kế toán có actor/evidence/status/version theo matrix duyệt; chạy song song vòng đời | Đủ ba phần và chứng từ áp dụng mới gửi khách/chi; Supply/Labour/Equipment/Mixed, advance/prepayment theo quyết định, không fake receipt. Giữ invoice uniqueness, independent approval, same-request revision khi QS superseded. API validation/authorization/file ownership; live một Upstream send và một Downstream pay |
| FIN-02 — Nguồn tài chính và báo cáo kho | Chốt với FA nguồn thực thu/thực chi/chi phí, P&L và kho theo dự án/kỳ; hoàn thiện phần Target còn thiếu bằng liên kết nguồn chuẩn, không dựng sổ kế toán hoặc tích hợp ngân hàng chưa được yêu cầu | Approved dossier/milestone không tự bằng cash received/paid. Báo cáo tách kế hoạch/certified/posted/actual theo contract đã chốt; receipt/issue/reversal và package allocation đối chiếu đúng kỳ, currency/unit. Thiếu nguồn không báo số 0 hoặc lãi giả; API source permissions, unit reconciliation và live report/export |
| REPORT-01 — CM/Finance/KPI | Portfolio progress, QS hai hướng, executed-vs-budget, QA/QC/HSE có source/period/baseline và drill-down; S-Curve và KPI theo definition/target đã duyệt | Theo xác nhận 24/10/2026: delay = planned% − actual% theo điểm phần trăm, strictly >5 điểm tạo red alert cho Site Manager và explanation/recovery; đúng 5 điểm không đỏ; kỳ linh hoạt ngày/tháng/năm. Lưu inputs/version/kỳ để tái tính. Nguồn actual/tổng hợp kỳ còn mở. Missing source hiển thị unavailable; chỉ terminal approved/posted/paid/verified source phù hợp tính KPI, không tính offline draft; unit boundary + API scope + desktop/tablet |
| COMMON-01 — Chức năng chung trong BreakTask | Kiểm kê và map master data, translations, notification/audit, exports, tìm kiếm/filter từng module; chốt phạm vi global search/saved filters còn Target và reuse query/UI hiện có | Tìm kiếm/lưu filter giữ đúng ownership/project/source permission và pagination, không trả record/count ngoài scope. Export phản ánh đúng tập/kỳ/quyền; không mất filter khi reload/back navigation. Các vị trí/KPI được canonical xác nhận có metric/source/target hoặc MissingData rõ ràng; chưa có nguồn HR/budget không tự tạo điểm |
| MIG-01 — Compatibility/backfill | Inventory IDs/versions/bindings/custom grants/legacy documents; kế hoạch additive migration cho model mới, root ID migration và Drive topology là các thay đổi riêng có decision | Dry-run counts/uniqueness/conflict/orphan/checksum, resume/checkpoint, rollback và rehearsal SQL. Không reset dữ liệu user; legacy thiếu evidence ghi rõ incomplete, không tái tạo submitted snapshot/approval/ISO completion bằng dữ liệu hiện tại |
| RELEASE-01 — Giao hàng và UAT | Cập nhật guides/RBAC/seed/translations, deploy revision xác định, smoke/UAT theo actor và scenario; bàn giao PDF quy trình kịch bản A có screenshot và hỗ trợ phản hồi NICON qua Sheet | `buildSha`/migration/version khớp; mọi High/Critical đóng, affected tests bản cuối pass; deployment/UAT/Drive NICON và việc bàn giao PDF có bằng chứng riêng. Chỉ miễn/đổi định dạng hướng dẫn khi NICON xác nhận. Gửi tài liệu/thông báo chỉ theo kênh và ủy quyền đã có, không đánh đồng local pass với host đã deploy |

Role custom hợp lệ phải tham gia workflow theo permission và assignment nghiệp
vụ tương ứng; không hardcode chỉ các role mẫu SALE/PM/DESIGN trong selection,
recipient eligibility hoặc mutation. Negative tests phải chứng minh role custom
không được nâng quyền, nhảy phòng, đọc source ngoài scope hoặc tự duyệt.

### 12.5 Kiến trúc, hợp đồng và thay đổi dữ liệu

- Giữ ASP.NET services/EF/React và durable workflow hiện có; không thêm broker,
  microservice hay dependency khi database intent/outbox đủ đáp ứng. Business
  write + intent cần atomic; worker chạy ngoài transaction dài gọi Google,
  có idempotency, generation/token fence, retry và observable failure.
- `OperationalProject` là business root. DesignProjectId của record cũ là
  compatibility boundary; giữ adapter/lookup và có migration riêng, không tạo
  aggregate liên module mới trên ID cũ hoặc đổi displayed code thành FK.
- API dùng DTO; xác định required/nullable/enum/length/format/range/normalization,
  cross-field/scope/source version, pagination và response/error contract cho
  mọi create/update/import/transition. Reuse shared contact validators; 409 cho
  stale version/plan theo convention, 403/404 theo permission/resource privacy.
  Không phát minh route hoặc state công khai trước khi duyệt hợp đồng gói đó.
- So sánh status quo và mở rộng tối thiểu: giữ module workspace và bổ sung shared
  root links thay vì viết lại mọi module thành generic tasks; giữ snapshot JSON
  có schema version nếu đủ thay vì thêm bảng không cần thiết; cần structured
  baseline/BOQ provenance/payment sections thì migration additive rõ ràng.
- Approval và submission là bằng chứng của đúng revision. Với bytes lịch sử,
  so sánh pinned remote revision và app-owned immutable copy theo khả năng giữ
  version, retention, chi phí/quota và quyền truy cập thực tế; owner chốt chính
  sách trước implementation. Chưa có quyết định thì phát hiện/chặn mismatch,
  không stream bytes mới dưới danh nghĩa evidence cũ đã duyệt.
- Document/notification retry không làm replay business transition hoặc gửi
  trùng. Mọi side effect cần event/source identity và audit; không xem log-only
  catch là đã phục hồi notification, cũng không dùng Drive queue cho mọi event.
- Form hiển thị loading/empty/error/success/conflict và responsive; mọi số nhập
  có format đọc được như `100.000`. Dùng master data API/config đã duyệt, i18n
  `/admin/translations` vi/en/zh/ja. Required multilingual entity fields được ghi
  đủ ở create/import/seed/migration/legacy auto-create, không read-time fallback.
- Bất kỳ email ứng dụng mới/thay đổi phải có template editable tại
  `/admin/email-templates`, variable contract, preview thực trước gửi, server
  render/HTML encoding/validation và send/edit permissions; thêm rendered-content
  tests, giữ template đang dùng. Notification nội bộ không tự cần email mới.

### 12.6 Ma trận kiểm thử và điều kiện nghiệm thu từng gói

| Rủi ro / hành trình | Tầng chứng minh thấp nhất | Bằng chứng bắt buộc |
|---|---|---|
| Validation, version/hash/fingerprint, calculations đã chốt, stale rename ordering | Unit | Boundary/missing/malformed/duplicate; exact thresholds; không chấp nhận approval hoặc kết quả của revision khác |
| Role × project/source × action; assignment revoked; approver/CC/author | Integration qua HTTP | Authenticated/unauthenticated, assigned/outside/ended, đúng/sai permission; list/count/content/mutation không leak; rejection giữ nguyên DB/file/history/notification |
| Rowversion, unique code/invoice, gate-snapshot race, evidence delete/approve, idempotency reservation | SQL integration cô lập | Hai thao tác cạnh tranh, một kết quả hợp lệ, stale action bị từ chối; transaction rollback và related roots không đổi; không thay bằng EF InMemory |
| BOQ/schedule/ISO import và migration | Source-derived fixture + integration | Mẫu được chấp nhận có expected mapping/totals/hierarchy/precision/evidence; lỗi không partial write; original source nguyên vẹn; rehearsal/backfill/rollback có đối chiếu |
| Lead → Opportunity/Project → Design/input price → Quote/Finance → HĐ | Integration business pipeline + browser hẹp | Handoff/source links và negative gates ở API; browser đúng role, deep link, forms và SPA trên Compose |
| Tender invitation → departmental plan/dossier/review → submission → Won → HĐ | Integration/SQL + browser hẹp | Snapshot cũ bất biến sau thay nguồn; terminal lock; duplicate/re-submit đúng contract; user thực hiện được journey |
| Design complete → Lead review → assigned internal recipients → client decision → IFC → Construction | Integration + live browser | Một approver/CC, reviewer ngoài project chỉ đọc phiên được chỉ định, đúng file revision; missing/negative/stale decision không qua gate |
| Contract activation → permit task → readiness; BCH resource/QA/QS → CM → Procurement/FA → handover | Integration/SQL + live browser | Independent review, applicability/evidence/version đúng; wrong direction/project/source và reconnect bị xử lý an toàn |
| Upstream dossier gửi khách và Downstream dossier chi | Unit package rules + integration + live browser | Đủ ba phần/chứng từ; invoice/QS/revision/advance exceptions đúng rule; render email/preview khi gửi thật; không giả receipt |
| Drive upload/source replacement/import/edit/move/trash/retry/delete | Unit/fake-provider integration và live Drive tách riêng | Checksum/parent/ownership/version/verified host cleanup; outage/expired lease/duplicate/stale plan; mock pass không tính live OAuth/Drive |
| BCH mobile và CM desktop/tablet; bốn ngôn ngữ | Live E2E Compose | 390/768/1440px phù hợp màn hình, không overflow/JS error; duplicate submit/lost response/reconnect; labels và lỗi rõ, không mất draft |

Chạy nhóm liên quan sau **final edit**, không full suite theo mặc định. Với .NET,
format các project bị ảnh hưởng trong backend/SDK container và verify solution
`dotnet format --no-restore --verify-no-changes` gồm unit/integration, rồi chạy
lại affected tests sau sửa cuối. Với frontend chạy `npm run format`,
`format:check`, lint/build theo scope; E2E phải là Compose-served application đã
ready. Test môi trường thiếu thì ghi Not Run/unverified, không lấy build/test
listing/kết quả phiên bản trước thay pass. Planning/docs-only chỉ kiểm tài liệu,
không khởi động/rebuild Docker hay chạy code suites.

Mỗi gói qua `.github/skills/senior-business-analyst/SKILL.md` rồi
`.github/skills/business-functional-qa/SKILL.md`, ưu tiên reviewer độc lập.
BA approval là test basis, không phải feature đã business-ready. Mọi critical
contract cần rõ; requirement risk chỉ Product/business owner được chấp nhận.
Không có ngoại lệ ngầm vì lịch giao hoặc test happy path đã xanh.

### 12.7 Migration, rollout, rollback và tài liệu

1. Inventory/dry-run trước thay đổi schema/grants/IDs/topology; backup dữ liệu,
   key-ring và manifest tệp/binding/checksum phù hợp, không xuất secrets vào Git.
   Đọc generated EF migration rồi rehearsal trên SQL clone độc lập; xác định
   uniqueness/conflict/legacy compatibility và đối chiếu business records.
2. Triển khai additive schema/backend có compatibility trước; backfill theo
   eligibility/checkpoint, kiểm chứng rồi mới bật UI/worker/gate tương ứng.
   Feature flag chỉ dùng khi cần rollout an toàn; không bật gate còn thiếu
   quyết định hoặc tự đánh dấu dữ liệu cũ đã đủ hồ sơ.
3. Đổi Drive root/instance/path phải có maintenance window, dừng phát sinh intent
   liên quan và drain Processing; giữ bindings hiện hữu đến bước rebind được
   duyệt. Survey topology chỉ migrate khi có kế hoạch move/rebind/reverse và
   verify historical files; lưu config mới không tương đương đã migrate dữ liệu.
4. Rollback tắt feature/worker mới và giữ immutable evidence/history; schema
   rollback chỉ khi backward-compatible và đã rehearsal. Không hứa undo được
   email đã gửi, payment posted hay file permanently deleted; những bước đó
   cần gate riêng và phương án recovery/retention được owner chấp nhận.
5. Seed/demo dùng tình huống NICON có tên có nghĩa, đúng contact/numeric rules và
   bốn ngôn ngữ; normal/empty/error/edge/legacy states không giả approval hay
   bằng chứng. Seed bổ sung idempotent, không reset free text, quyền hoặc
   translations do admin chỉnh.
6. Cập nhật `user_guide.md`, `users-rbac.md`, guide này khi behavior/API/ops đổi.
   Chỉ cập nhật `Nicon-QLVH.md`/`Nicon-workflow.md` khi requirement được xác nhận;
   không sửa customer evidence thành implementation history. Báo cáo mỗi run và
   screenshots/logs ở test artifacts/PR, không thêm report vào `docs/Nicon/`.
   Kho evidence hiện có ISO/JPG và một số tệp tạm không khớp strict allowlist
   của AGENTS; xử lý bằng quyết định tổ chức nguồn/cập nhật allowlist có thẩm
   quyền, không tự xóa hiện vật khách hàng để làm layout check xanh.
7. Task branch/commit focused từng gói, stage explicit paths, review diff/secrets,
   không sửa history. Mỗi PR nêu problem/behavior/AC/test/migration/residual risk.
   Deploy ghi revision/buildSha và migration version, smoke rồi NICON UAT theo
   actor qua đầu mối được chỉ định; host status không suy từ local branch.

### 12.8 Bao phủ toàn bộ baseline và trách nhiệm

| Mục MoM | Gói chịu trách nhiệm | Điều kiện đóng phạm vi |
|---|---|---|
| 1 — Quyền/vai trò | RBAC-01, SAFE-01/02 | Cơ cấu/baseline theo quyết định, permission/scope đúng server, không wildcard xóa nhầm |
| 2 — CRM | CRM-01, ROOT-01 | Menu, Lead conversion/lock, mã editable/unique và hai hướng HĐ đúng contract hiện hành |
| 3 — Dự án/việc chung | ROOT-01, WORK-01, REPORT-01 | Shared root, việc nội bộ, assignment/notification/KPI và multi-contract theo quyết định |
| 4 — Design trước HĐ | DESIGN-01, DRIVE-02 | Kinh doanh handoff từ Opportunity/Project, Concept không đòi HĐ và folder intent có thể phục hồi |
| 5 — Liên phòng tiền HĐ | COMM-01, SUPPLY-01 | Input pricing khác Execution BOQ, Finance commercial/tender gate đúng phiên bản, nhãn Cung ứng |
| 6 — Quote/BOQ | BOQ-01 | Hai mode/version, template/import đúng phạm vi được xác nhận; fidelity nâng cao chỉ đóng khi được duyệt và test |
| 7 — Đấu thầu | TENDER-01, COMM-01, WORK-01 | Plan theo phòng, dossier/library/review/submission/result, snapshot bất biến và terminal lock |
| 8 — Design | DESIGN-01/02/03/04, SAFE, DRIVE | Actor/approver/CC/review/client/stage/schedule/subcontractor/docs/IFC/delete đủ AC và negative cases |
| 9 — Pháp lý | LEGAL-01, ISO-01, SAFE-01 | Permit applicability/activation/task/approval/progress và revoked access được chứng minh |
| 10 — Thi công | CM-01, FIELD-01/02, SUPPLY-01, QS-01 | CM/BCH, readiness/baselines, resource/QAQC/HSE/QS/ISO và handover, không chỉ có menu |
| 11 — Tài chính | COMM-01, FIN-01/02, QS-01, REPORT-01 | Hoạt động song song, hai hướng, ba phần hồ sơ và đúng package evidence trước send/pay; báo cáo có nguồn thực tế |
| 12 — Giao hàng/Drive | DRIVE-01/02/03, RELEASE-01 | Account/config/recovery/document sources đúng, deployment/UAT/PDF hướng dẫn có bằng chứng |
| 13 — Hiện vật NICON | PLAN-01, ISO-01, BOQ-01, DESIGN-03, LEGAL-01 | Kiểm kê inputs đã nhận, chỉ rõ phần thiếu, quyết định có owner; không coi file mẫu là rule |
| 14 — Điểm chưa rõ | Bảng 12.2 và gói bị ảnh hưởng | Có xác nhận hoặc giữ Open decision; không đóng nghiệm thu bằng giả định của dev |

Đội BE/FE/Data chịu implementation/compatibility của từng gói; QA chuẩn bị dữ
liệu và thực thi theo risk, reviewer BA/Architecture kiểm contract và boundaries.
Product Owner/người được ủy quyền chịu quyết định phạm vi/rule; CM/FA/Thiết kế
xác nhận mapping chuyên môn, IT xác nhận deployment/Drive/retention. Chỉ đóng
toàn kế hoạch khi mọi AC trong phạm vi được xác nhận đã có bằng chứng, quyết
định còn mở được xử lý đúng thẩm quyền và NICON hoàn tất UAT; không dùng số test
pass hay trạng thái Connected thay cho điều kiện này.
