# Nihome Platform -- Application Developer Guide

Version 1.0

Last Updated: 26 April 2026

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

Nihome is a full-stack content management and recruitment platform. The backend is built with ASP.NET Core 8 and Entity Framework Core 8. The frontend is built with React 18, TypeScript, and Vite. The database is SQL Server 2022. All services are orchestrated through Docker Compose.

This guide covers development setup, configuration, database management, build and test procedures, and deployment.

---

## 2. System Requirements

### Software Dependencies

| Component        | Technology                        | Version   |
|------------------|-----------------------------------|-----------|
| Backend Runtime  | .NET SDK                          | 8.0       |
| Frontend Runtime | Node.js                           | 18+       |
| Database         | Microsoft SQL Server              | 2022      |
| Containerization | Docker and Docker Compose         | Latest    |

### Backend Packages

| Package                                            | Version |
|----------------------------------------------------|---------|
| Microsoft.EntityFrameworkCore.SqlServer             | 8.0.4   |
| Microsoft.EntityFrameworkCore.Design                | 8.0.4   |
| Microsoft.AspNetCore.Authentication.JwtBearer       | 8.0.0   |
| AutoMapper.Extensions.Microsoft.DependencyInjection | 12.0.1  |
| MailKit                                             | 4.15.1  |

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
| Vitest             | Unit testing             |

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

The backend runs with `dotnet watch` for hot-reload. File changes in `nihomebackend/` are automatically detected and recompiled.

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

### 4.2 Running the Backend Locally

Ensure SQL Server is accessible on `localhost:1433`.

```bash
cd nihomebackend
dotnet run
```

For development with automatic recompilation:

```bash
cd nihomebackend
dotnet watch run
```

When you run the backend directly from `nihomebackend/`, the API starts on `http://localhost:5043`.

### 4.2.1 Swagger Access

Swagger is enabled only when the backend runs in the `Development` environment.

For the standard Docker Compose development setup, use:

- Swagger UI: `http://localhost:5043/swagger`
- OpenAPI JSON: `http://localhost:5043/swagger/v1/swagger.json`
- API base path: `http://localhost:5043/api`

If you run the backend directly with `dotnet run` or `dotnet watch run`, use the same URLs:

- Swagger UI: `http://localhost:5043/swagger`
- OpenAPI JSON: `http://localhost:5043/swagger/v1/swagger.json`
- API base path: `http://localhost:5043/api`

### 4.3 Running the Frontend Locally

```bash
cd nihomeweb
npm install
npm run dev
```

The development server starts on `http://localhost:3000`.

### 4.4 Building for Production

Backend:

```bash
cd nihomebackend
dotnet build -c Release
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

When running inside Docker:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=host.docker.internal,1433;Database=NihomeDB;User Id=sa;Password=Nihome@31042025;TrustServerCertificate=True;"
  }
}
```

When running locally:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=NihomeDB;User Id=sa;Password=Nihome@31042025;TrustServerCertificate=True;"
  }
}
```

When running inside the Docker network (container-to-container):

```
Server=sqlserver,1433;Database=NihomeDB;User Id=sa;Password=Nihome@31042025;TrustServerCertificate=True;
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

#### Email (SMTP) Configuration

```json
{
  "Email": {
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
  "FrontendCors": {
    "AllowedOrigin": "http://localhost:3000"
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
| `process_documents`    | Internal process documentation entries              |
| `translations`         | Static UI translation strings (unique key + language) |
| `entity_translations`  | Dynamic content translations (polymorphic)          |

### 6.3 Key Indexes

- `users`: Unique index on `Phone`
- `refresh_tokens`: Unique index on `Token`
- `registration_otp`: Index on `PhoneNumber`
- `activities`, `news_articles`, `projects`, `service_items`, `slideshow_items`: Unique index on `Slug`
- `activity_categories`: Unique index on `Name`
- `translations`: Unique composite index on (`Key`, `LanguageCode`)
- `entity_translations`: Unique composite index on (`EntityType`, `EntityId`, `FieldName`, `LanguageCode`)
- `process_documents`: Index on `GroupKey`

### 6.4 Entity Framework Migrations

All schema changes must go through EF Core migrations. Never modify the schema directly.

Create a new migration:

```bash
cd nihomebackend
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

### 6.5 Data Seeding

On application startup, the following seeders execute in order:

1. **DbSeeder** -- Creates default users and site settings if they do not exist.
2. **ContentSeeder** -- Seeds initial activities and other content data.
3. **TranslationSeeder** -- Loads UI translation strings from embedded JSON resource files in `Data/Seeds/`.

Translation seed files are embedded resources (`*.json` files in `Data/Seeds/`).

#### Default User Accounts (Development Only)

| Role        | Phone       | Email                      | Password    |
|-------------|-------------|----------------------------|-------------|
| SUPER_ADMIN | 0335240370  | superadmin@nihome.vn       | Admin@123   |
| ADMIN       | 0335240371  | ops.admin@nihome.vn        | Admin@123   |
| ADMIN       | 0335240372  | leasing.admin@nihome.vn    | Admin@123   |

These credentials are for development and staging only. Change all default passwords before deploying to production.

### 6.6 Verifying the Database

Connect to SQL Server running in Docker:

```bash
docker run --platform linux/amd64 -it --rm \
  --network container:nihome31042025-sqlserver \
  mcr.microsoft.com/mssql-tools \
  /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "Nihome@31042025"
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

### 7.3 Adding a New Entity

To add a new content entity:

1. Create the entity model in `Models/`.
2. Create request and response DTOs in `Models/`.
3. Add a `DbSet` in `Data/AppDbContext.cs` and configure the table in `OnModelCreating`.
4. Create a migration: `dotnet ef migrations add Add<EntityName>Table`.
5. Create a service class in `Services/`.
6. Create a controller in `Controllers/`.
7. Add AutoMapper mappings in `Mappings/AutoMapperProfile.cs`.
8. Register the service in `Extensions/ServiceCollectionExtensions.cs`.
9. Write unit tests in `nihomebackend.tests/`.

### 7.4 Conventions

- Use `async/await` for all I/O operations.
- Use `AsNoTracking()` for read-only queries.
- Return DTOs from controllers, never entity models.
- Use meaningful HTTP status codes (200, 201, 400, 404, 500).
- Validate input at the controller level.
- Keep controllers under 20 lines per action method where possible.
- JSON columns use string serialization (e.g., `ContentJson`, `SectionsJson`).

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
| Vitest           | Unit testing framework             |

### 8.2 API Service Modules

API calls are organized into three service modules:

- `authApi.ts` -- Authentication endpoints (login, register, refresh, logout, forgot password)
- `contentApi.ts` -- Public content endpoints (activities, news, projects, services, slideshow)
- `adminApi.ts` -- Admin management endpoints (CRUD operations, settings, translations)

### 8.3 Build Commands

| Command              | Purpose                            |
|----------------------|------------------------------------|
| `npm run dev`        | Start development server           |
| `npm run build`      | Production build                   |
| `npm run build:dev`  | Development build                  |
| `npm run lint`       | Run ESLint                         |
| `npm run test`       | Run tests once                     |
| `npm run test:watch` | Run tests in watch mode            |
| `npm run preview`    | Preview production build locally   |

---

## 9. Testing

### 9.1 Backend Tests

Backend unit tests are located in `nihomebackend.tests/`.

Run all tests:

```bash
cd nihomebackend.tests
dotnet test
```

The test project structure:

```
nihomebackend.tests/
  Controllers/     -- Controller unit tests
  Services/        -- Service unit tests
  Mappings/        -- AutoMapper profile tests
  Helpers/         -- Test helper utilities
```

When adding a new feature, write corresponding tests for:

- Controller action methods (input validation, response codes, service delegation)
- Service methods (business logic, edge cases, error handling)
- AutoMapper mappings (entity-to-DTO conversion correctness)

### 9.2 Frontend Tests

Frontend tests use Vitest.

```bash
cd nihomeweb
npm run test
```

Watch mode for development:

```bash
cd nihomeweb
npm run test:watch
```

### 9.3 Linting

Backend:

```bash
cd nihomebackend
dotnet format
```

Frontend:

```bash
cd nihomeweb
npm run lint
```

### 9.4 Quality Check Summary

| Check              | Command                              |
|--------------------|--------------------------------------|
| Backend build      | `dotnet build`                       |
| Frontend build     | `npm run build`                      |
| Backend tests      | `cd nihomebackend.tests && dotnet test` |
| Frontend tests     | `cd nihomeweb && npm run test`       |
| Backend lint       | `cd nihomebackend && dotnet format`  |
| Frontend lint      | `cd nihomeweb && npm run lint`       |
| Docker full build  | `docker compose up --build`          |

---

## 10. Deployment

### 10.1 Docker Compose (Development and Staging)

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
9. Run `dotnet ef database update` against the production database after reviewing the migration script.
10. Build the frontend with `npm run build` and deploy the `dist/` output.

---

## 11. Troubleshooting

### 11.1 Database Connection Failures

**Symptom**: The backend fails to start with a database connection error.

**Resolution**:
- Verify SQL Server is running: `docker ps | grep sqlserver`
- Check the connection string in `appsettings.json` or the Docker Compose environment variable.
- When running locally, use `localhost,1433`. When running inside Docker, use `sqlserver,1433` or `host.docker.internal,1433`.
- Ensure the SQL Server health check has passed before starting the backend.

### 11.2 Migration Errors

**Symptom**: EF Core migration fails to apply.

**Resolution**:
- Review the migration file for conflicts.
- Check the current database state: `dotnet ef migrations list`
- If the last migration is problematic and has not been applied, remove it: `dotnet ef migrations remove`
- Generate a SQL script for manual review: `dotnet ef migrations script`

### 11.3 CORS Errors

**Symptom**: The frontend receives CORS errors when calling the API.

**Resolution**:
- Verify the `FrontendCors:AllowedOrigin` setting matches the frontend URL exactly (including protocol and port).
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
