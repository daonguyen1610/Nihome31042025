# Nihome Platform -- Application Developer Guide

Version 1.0

Last Updated: 12 August 2026

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
- Entity translations use a polymorphic pattern for translating any entity field.
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

Content behavior is entity-specific. Activities, news, and projects are slug-based backfills that preserve administrator edits. Process and logo seeders contain replacement behavior under some drift conditions; treat those manifests as seed-owned and review the seeder before changing manifest counts. Translation, RBAC, master-data, workflow, and notification files are embedded resources.

#### Process Document Seeder Guard

The process document seeder uses a two-condition guard to decide whether to re-seed:

```
if (count matches AND (no asset data in seed OR DB rows already have assets)) → skip
```

This means the seeder re-runs automatically after a migration that adds `ImagesJson`/`FilesJson` columns even when the row count has not changed. After the re-seed, subsequent restarts skip again because the DB rows now have asset data.

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

The procurement vendor slice stores supplier and subcontractor profiles in `procurement_vendors`. Vendor codes are trimmed, normalized to uppercase, and protected by a unique database index. Use `IsActive` to retain a vendor for historical reporting while preventing new use. `DELETE` permanently removes obsolete, duplicate, or test records and should only be used when historical retention is not required.

Both `/api/vendors` and `/api/v1/vendors` expose the same controller:

| Method | Route | Permission | Purpose |
|--------|-------|------------|---------|
| `GET` | `/api/vendors` | `proc.vendors.view` | Search, filter, sort, and paginate vendors |
| `GET` | `/api/vendors/{id}` | `proc.vendors.view` | Read vendor details and audit metadata |
| `POST` | `/api/vendors` | `proc.vendors.manage` | Create an active vendor |
| `PUT` | `/api/vendors/{id}` | `proc.vendors.manage` | Update profile data or active status |
| `DELETE` | `/api/vendors/{id}` | `proc.vendors.manage` | Permanently delete a vendor |

Duplicate normalized codes return `409`; invalid request data returns `400`; missing records return `404`. Create, update, and delete operations write `vendor.create`, `vendor.update`, and `vendor.delete` audit events. Delete returns `204` and records the removed vendor snapshot in the audit event. `proc.vendors.export` controls the frontend export action but does not grant API read access by itself.

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
| `DELETE` | `/api/handover-records/{id}` | `construction.handover.manage` | Delete Draft/Cancelled records |

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

The production artifact includes `web.config` for the ASP.NET Core Module and serves the compiled SPA from `wwwroot`. `NIHOMEWEB_DIST_PATH` can override the frontend distribution directory at runtime; startup fails if the configured directory does not exist. Swagger is Development-only and should not be enabled by switching production to the Development environment.

### 10.1 Deterministic demonstration data

`DbSeeder` creates a deterministic demonstration dataset covering the CRM funnel, all contract statuses, design stages, permitting, construction, acceptance, as-built, and handover workflows. Seeder-owned rows use stable markers such as `[SAMPLE]`, `[SAMPLE_CONTRACT]`, and `[SAMPLE_DP]`; downstream records are attached only to marker-owned sample projects rather than arbitrary database rows.

The dataset is idempotent: rerunning startup seeding preserves row counts and administrator-edited free text and values. A constrained repair pass updates only sample-marker relationship fields needed for referential coherence, including opportunity/quote links, project/customer/contract links, and PM/design-lead assignments. Sales records are owned by the SALE demo user, project and construction records prefer PM, design documents prefer DESIGN_LEAD then DESIGN, and permit work prefers LEGAL_OFFICER with safe fallbacks.

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
