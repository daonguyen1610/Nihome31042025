# Nihome Platform -- Application Developer Guide

Version 1.0

Last Updated: 9 September 2026

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

The backend runs with `dotnet watch` for hot-reload and builds the Vite application through the backend project. There is no separate frontend Compose service: port `5043` serves both the SPA and API. Port `8080` is used only when running Vite separately on the host.

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

Repository backend and database commands run inside the Docker Compose environment. The backend container starts with file watching enabled, so edits under `nihomebackend/` are rebuilt automatically.

```bash
docker exec nihome31042025-backend dotnet build
```

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

Configuration is managed through `appsettings.json` and `appsettings.Development.json` in the `nihomebackend/` directory.

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

### 5.2 Docker Compose Environment Variables

The following environment variables are set in `docker-compose.yaml`:

| Variable                              | Value                          | Purpose                              |
|---------------------------------------|--------------------------------|--------------------------------------|
| `ASPNETCORE_ENVIRONMENT`              | Development                    | Runtime environment profile          |
| `ASPNETCORE_URLS`                     | http://0.0.0.0:5043            | Kestrel binding address              |
| `DOTNET_USE_POLLING_FILE_WATCHER`     | 1                              | Enables file polling for hot-reload  |
| `ConnectionStrings__DefaultConnection`| SQL Server connection string   | Database connection (container name) |

### 5.3 Docker Compose Volumes

Named volumes are used to isolate build artifacts and package caches:

| Volume                  | Mount Point          | Purpose                                     |
|-------------------------|----------------------|---------------------------------------------|
| `nihomebackend_bin`     | `/app/bin`           | Isolate compiled output from host            |
| `nihomebackend_obj`     | `/app/obj`           | Isolate build intermediates from host        |
| `nihomeweb_node_modules`| `/nihomeweb/node_modules` | Isolate npm packages from host          |
| `nihomeweb_dist`        | `/nihomeweb/dist`    | Frontend build output                        |
| `nuget_packages`        | `/root/.nuget/packages` | NuGet package cache                       |
| `sqlserver_data`        | `/var/opt/mssql`     | Persistent database storage                  |

---

## 6. Database

### 6.1 Overview

The platform uses SQL Server 2022 with Entity Framework Core 8 as the ORM. The database is named `NihomeDB`. EF Core is configured with split-query behavior and a 60-second command timeout.

### 6.2 Schema

| Table                  | Purpose                                             |
|------------------------|-----------------------------------------------------|
| `users`                | User accounts with phone-based authentication       |
| `refresh_tokens`       | JWT refresh tokens linked to users                  |
| `registration_otp`     | OTP records for registration verification           |
| `site_settings`        | Application-wide configuration (single row)         |
| `activities`           | Activity/event content entries                      |
| `activity_categories`  | Categories for grouping activities                  |
| `news_articles`        | News and article content entries                    |
| `projects`             | Project portfolio entries                           |
| `operational_projects` | Central internal project shared across the eight operational modules |
| `service_items`        | Service offering descriptions                       |
| `slideshow_items`      | Homepage slideshow slides                           |
| `job_positions`        | Open job positions for recruitment                  |
| `job_applications`     | Candidate applications (FK to job_positions, cascade delete) |
| `contact_messages`     | Messages submitted through the contact form         |
| `client_logos`         | Logos for clients, partners, and suppliers           |
| `process_documents`    | Internal process documentation entries with optional image/file asset metadata stored as JSON columns |
| `translations`         | Static UI translation strings (unique key + language) |
| `entity_translations`  | Dynamic content translations (polymorphic)          |
| `handover_records`     | One project handover aggregate per design project, including readiness inputs and SQL Server row-version concurrency |
| `handover_status_history` | Immutable project handover lifecycle history      |

### 6.3 Key Indexes

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

Outside the `IntegrationTests` environment, application startup applies pending migrations and then runs the complete seed pipeline. The order is baseline users/settings, content, UI and entity translations, RBAC catalog/roles, master data, workflows, notification templates, deterministic business-role users, and sample CRM/design/construction data.

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

The current startup path is not environment-gated and can create deterministic accounts outside Development. Production deployment must rotate or disable them and should gate demo/sample seeding before the application is exposed.

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

#### Scope

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
| `UploadedImageCleanupService` | Background cleanup of orphaned uploaded images        |
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

The permit checklist is auto-generated from active `permit_type` master data when a design project is created. Authorized operators can also manage individual rows when project-specific requirements differ from the default template. Each design-project and permit-type pair remains unique.

Both `/api/permits` and `/api/v1/permits` expose the same controller:

| Method | Route | Permission | Purpose |
|--------|-------|------------|---------|
| `GET` | `/api/permits` | `permit.checklists.view` | Filter, paginate, and read the permit risk summary |
| `GET` | `/api/permits/{id}` | `permit.checklists.view` | Read one checklist item |
| `POST` | `/api/permits` | `permit.checklists.manage` | Create a project-specific checklist item |
| `PATCH` | `/api/permits/{id}` | `permit.checklists.manage` | Update status, ownership, dates, agency, or notes |
| `DELETE` | `/api/permits/{id}` | `permit.checklists.manage` | Permanently delete one checklist item |
| `POST` | `/api/permits/design-project/{projectId}/ensure` | `permit.checklists.manage` | Add missing active template items without overwriting existing rows |

Duplicate project/type pairs return `409`; invalid projects, permit types, owners, or statuses return `400`; missing rows return `404`. Create, update, and delete operations write `permit.create`, `permit.update`, and `permit.delete` audit events. Delete returns `204` and records the removed item snapshot.

### 7.7 Permanent Aggregate Deletion

#### Objective

Allow authorized users to permanently remove aggregate roots, including seeded
or demo records, without hiding dependent data or causing partial deletion.

#### API Contract

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
- Existing domain flows continue to unlink external Google Drive folder
  bindings until they are migrated to the durable operation foundation.
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
  and purged with the Quote. Opportunities and Contracts that reference the
  Quote are unlinked and preserved. A Quote project-document sidecar is eligible
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
  Lead conversion always preserves its Customer; Customers can only be
  permanently removed through this preview-and-confirm contract.
- Exactly one success audit is written by the durable operation processor in
  the same save that marks the operation `Completed`, after database
  finalization and quarantine purge. Request, validation-failure, and
  pre-completion success audits are not emitted.
- Hard-deleted seeded roots write a durable tombstone. Opportunity uses its
  `[SAMPLE]` name, Contract its `HD-SAMPLE-` number, Survey its `SV-SAMPLE-`
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

Each Tender checklist row retains one current file. Users with `crm.tenders.manage` may replace it with a direct upload or attach one existing capability document. Direct uploads are stored under `wwwroot/files/tenders/`; capability-library attachments retain their managed capability path. A capability document or retained version cannot be deleted while a Tender checklist row references its path. All checklist mutations are rejected after the Tender reaches `Won`, `Lost`, or `Cancelled`. If a direct upload cannot be attached because the Tender or row is missing, terminal, persistence fails, or the physical copy is interrupted, the newly written or partial file is removed.

| Method | Route | Permission | Purpose |
|--------|-------|------------|---------|
| `PATCH` | `/api/tenders/{id}/checklist/{itemId}` | `crm.tenders.manage` | Update checklist status, owner, or internal deadline while mutable |
| `POST` | `/api/tenders/{id}/checklist/{itemId}/upload` | `crm.tenders.manage` | Upload and replace the row's current file |
| `POST` | `/api/tenders/{id}/checklist/attach-from-library` | `crm.tenders.manage` | Attach an existing capability document to the selected row |
| `GET` | `/api/tenders/{id}/checklist/{itemId}/content` | `crm.tenders.view` | Preview or download the file referenced by that Tender row |

Contract creation derives `OwnerUserId` from the selected customer's `OwnerUserId`. An authorized explicit owner takes precedence; if the customer is unassigned, the caller is used as the fallback. Sales users cannot create or move a contract into another salesperson's customer scope. Opportunity and quote references must belong to the selected customer, and a supplied quote must belong to the supplied opportunity.

`GET /api/contracts` and its `/api/v1` alias require
`crm.contracts.view`. The list accepts `status`, `direction`, `type`,
`vendorId`, `ownerUserId`, `customerId`, `operationalProjectId`, `search`,
`signedFrom`, `signedTo`, `endFrom`, `endTo`, `valueMin`, `valueMax`, `page`, `pageSize`, `sortBy`,
and `sortDirection`. Supported sort fields are `signedDate` (default),
`endDate`, `value`, `contractNumber`, and `updatedAt`; an unknown field falls
back to signed date, and direction defaults to descending. Value filters and
the `value` sort use current value (`Value + approved VO deltas`). The response
adds portfolio totals, collection-risk counts, and each row's next unpaid
milestone and scheduled outstanding amount. `GET /api/contracts/filter-options`
returns complete owner, customer, and Operational Project choices from the
caller's visible contract scope; it is not capped by unrelated list pagination.
`GET /api/contracts/export-data` applies the same filters, sorting, and scope in
one server query so CSV export is not assembled from a changing set of pages.
Callers without
`crm.contracts.view.all` remain owner-scoped. The Accountant role has read-only
`crm.contracts.view.upstream.all` access because Finance must reconcile primary
contracts across the same complete Operational Project portfolio exposed by
`operations.projects.view.all`. Both permissions are required before the
scoped contract permission bypasses ownership, and only
when the list explicitly requests `direction=Upstream` or a read-only detail
endpoint resolves to an upstream contract. It neither exposes downstream
contracts across owners nor grants mutation permissions.

The shared frontend list is available at `/admin/contracts`. The Finance entry
`/admin/finance/contracts` fixes the same list and create form to
`direction=Upstream`. Its URL retains filters, sort, and page while users inspect
a detail. The Finance view shows current value, approved VO impact, next
collection, and overdue/due-soon summary; secondary filters are collapsible for
tablet/mobile use. Both surfaces use server pagination and the dedicated
server-side export query, so CSV cannot silently truncate at the current page or
the 100-row API limit.

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

Replacing or removing persisted Contract, Design, Vendor, Acceptance, As-Built, Handover, and Permit references deletes the previous managed file only after successful database persistence. Basic Design and Detail Design uploads also enforce the parent project's active stage in the backend service. Uploading before saving a two-step form can still leave an inaccessible, unreferenced physical file when the user cancels, because staged-file expiry/reconciliation is not yet implemented. Allowed extensions and the 20 MB limit are enforced, but malware scanning and full file-signature validation remain deployment hardening work.

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
| `DELETE` | `/api/operational-projects/{id}` | `operations.projects.manage` | Delete an empty Planning project only |

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

The Module 2 schedule is exposed beneath
`/api/operational-projects/{id}/design-schedule` and remains separate from
Module 4 construction tasks. Its lifecycle, validation, weighted roll-up,
filter, concurrency, migration, and deletion contracts are documented in
[Detail Design Schedule](#716-detail-design-schedule).

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
Manual catalog uploads stream directly into the configured Google Drive
category folder; SQL stores catalog, permission, workflow, and Drive metadata,
and no duplicate file is written under the application web root. Authenticated
content requests proxy the Drive bytes so OAuth credentials and raw Drive
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

Relationship changes are reconciled only on an explicit update that changes the
resolved Operational Project. Linking or reassigning an Opportunity, Contract,
or Design Project stages its currently supported source files in the destination
and queues old replicas for deletion; a missing `OperationalProjectId` in those
update contracts preserves the existing relationship. Survey updates support
explicit Opportunity unlinking through `LinkedOpportunityId = null`, but every
Survey retains a required `OperationalProjectId`. Project reassignment queues
old replicas for deletion and stages media in the destination project's Survey
category. This event-driven behavior does not scan unrelated source records.

The worker uses durable desired operations, bounded retries, claim tokens,
generation fencing, SQL rowversion, and per-folder reconciliation leases for
legacy source-owned sidecars. Drive is authoritative for manual catalog uploads
and files created directly in managed folders: reconciliation catalogs them in
their current category without downloading host copies, reflects remote edits,
and marks remotely trashed files deleted. Native Google Workspace files remain
metadata/link entries because Drive does not expose their native bytes through
the normal download operation. For legacy source-owned sidecars only, external
deletion queues restoration and concurrent remote edits preserve a separate
conflict entry until an authorized user confirms **Keep both**.

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

#### API Contract

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

`RfqSampleDataSeeder` creates only its dedicated `PJ-SAMPLE-RFQ` project and
respects project deletion tombstones. It provides a draft, an issued comparison
with complete/partial quotes, and an overdue RFQ. Re-running does not overwrite
user edits or resurrect the project. Production reference options come from
the API, not hardcoded React values.

### 7.19 KPI Framework and Source Evidence

#### Scope

The KPI platform implements the approved NICON framework from the NIH-447
customer attachment and `docs/Nicon_BreakTask_v1.xlsx`. It calculates only
metrics backed by structured source events. A missing source workflow is
reported as `MissingData`; it is never converted to a zero score.

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
| Tendering | Tender win rate | 40% | Tender preparer, close date and result | Implemented |
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

The approved source decisions are retained below. Earlier design proposals and
pre-delivery inventories are not an alternative API specification; consult the
current controllers/DTOs and the module contracts in this guide.

| Decision | Approved contract |
| --- | --- |
| D-01 | `PROCUREMENT` is a distinct business role and KPI position; `QS` remains Tendering and `WAREHOUSE` remains a control role. Scheduled KPI eligibility must include Procurement. |
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

```bash
docker compose up -d --build
cd nihomeweb
BASE_URL=http://localhost:5043 npx playwright test
```

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
docker exec nihome31042025-backend dotnet format --verify-no-changes
```

Frontend:

```bash
cd nihomeweb
npm run lint
```

### 9.6 Quality Check Summary

| Check              | Command                              |
|--------------------|--------------------------------------|
| Backend build      | `docker exec nihome31042025-backend dotnet build` |
| Frontend build     | `npm run build`                      |
| Backend tests      | `dotnet test nihomebackend.tests/nihomebackend.tests.csproj` in CI or an SDK test environment |
| Backend integration tests | `dotnet test nihomebackend.integration.tests/nihomebackend.integration.tests.csproj` |
| Browser E2E tests  | `cd nihomeweb && BASE_URL=http://localhost:5043 npx playwright test` |
| Backend lint       | `docker exec nihome31042025-backend dotnet format --verify-no-changes` |
| Frontend lint      | `cd nihomeweb && npm run lint`       |
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

The migration intentionally leaves upgraded installations disabled with empty deployment-specific fields. During a planned maintenance window, an administrator must save the former business configuration on the Admin page and reconnect before synchronization resumes. Although the existing encrypted refresh-token column is preserved, the first Client ID assignment clears that token because the application cannot prove it belongs to the newly entered OAuth client. For later topology changes, first disable synchronization and wait for active `Processing` records to finish, then change the root folder, deployment identity, or business folder paths and re-enable. Nicon invalidates cached project-folder bindings so future operations resolve beneath the new topology. Existing catalog rows and Drive files are retained; the application does not move or delete historical files automatically.

Project document storage is Drive-primary. Nicon uploads manual project files directly to their configured Drive category, proxies authenticated downloads, and moves deleted files to Drive trash rather than permanently erasing them. SQL remains authoritative for catalog metadata, application authorization, claims, and workflow state, while Drive is authoritative for file content. Reconciliation catalogs unknown files without a host copy and reflects remote changes. Existing source-module files use a compatibility sidecar until those modules migrate their own storage contracts. Drive sharing and permission synchronization are intentionally disabled until IT supplies approved group mappings.

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

Each project document permits at most three worker claims. During backoff after a failed claim, an authorized user can select **Retry** to make the next remaining claim eligible immediately. After the third failed claim the row is terminal; correct the Drive configuration before replacing or restaging the source file.

While a legacy sidecar upload is running, a separate scoped database heartbeat renews its claim every five minutes. Losing claim ownership cancels the Drive request and SQL generation fencing rejects stale completion. If a crash or network race still leaves multiple remote files with the same Nicon replica key and generation, reconciliation preserves the SQL-bound replica and moves the extras to Drive trash.

If the application stops during an active claim, the expired lease resumes the same numbered attempt instead of consuming another one. This includes attempt three; the Drive idempotency property lets recovery reconcile a remote file that may already have been created.

Retry and deletion are rejected while a row is `Processing`. This preserves the active SQL lease and prevents an in-flight upload from creating an untracked Drive file; users can retry or delete after the worker completes or its lease is recovered.

The response includes only safe identity/folder metadata and never includes the credential path, client secret, refresh token, or private key.

For Docker development, provide client ID, client secret, localhost callback URI, root folder, and instance ID through local ignored configuration or environment variables, then recreate the backend. Compose persists the Data Protection key ring in the `data_protection_keys` volume.

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

### 10.1 Deterministic demonstration data

`DbSeeder` creates a deterministic demonstration dataset covering the CRM funnel, all contract statuses, design stages, permitting, construction, acceptance, as-built, and handover workflows. Seeder-owned rows use stable markers such as `[SAMPLE]`, `[SAMPLE_CONTRACT]`, and `[SAMPLE_DP]`; downstream records are attached only to marker-owned sample projects rather than arbitrary database rows.

The dataset is idempotent: rerunning startup seeding preserves row counts and administrator-edited free text, lifecycle values, and relationships. A constrained backfill pass populates only missing sample relationships, including opportunity/quote links, project/customer/contract links, and PM/design-lead assignments. Sales records are owned by the SALE demo user, project and construction records prefer PM, design documents prefer DESIGN_LEAD then DESIGN, and permit work prefers LEGAL_OFFICER with safe fallbacks.

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

### 11.6 Docker Volume Issues

**Symptom**: Code changes are not reflected in the running container.

**Resolution**:
- Verify that volumes are correctly mounted in `docker-compose.yaml`.
- For persistent issues, remove named volumes and rebuild: `docker compose down -v && docker compose up --build`
- The `bin`, `obj`, and `node_modules` directories use named volumes to avoid host/container conflicts.

### 11.7 Hot-Reload Not Working

**Symptom**: Backend does not recompile after file changes.

**Resolution**:
- Confirm `DOTNET_USE_POLLING_FILE_WATCHER` is set to `1` in the Docker Compose environment.
- Confirm the backend is started with `dotnet watch run`.
- Restart the container: `docker compose restart nihomeBackend`

---
