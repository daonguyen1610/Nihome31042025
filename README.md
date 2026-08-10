# Nihome31042025

Nihome is a React 18 + ASP.NET Core 8 platform backed by Entity Framework Core 8 and SQL Server 2022. The repository includes public content management, recruitment, CRM-related workflows, and construction operations such as partial acceptance, as-built dossiers, punch lists, and project handover.

## Docker development

Docker Compose is the supported development environment. Start the complete stack with:

```bash
docker compose up -d --build
```

The application is available at `http://localhost:5043`. The backend container is named `nihome31042025-backend`, and SQL Server runs in `nihome31042025-sqlserver`.

## Connect ASP.NET Core to SQL Server

The Docker Compose environment supplies the application connection string. For an external development configuration that connects to the Docker-hosted SQL Server, use:

We need to declare the `appsettings.json` like
```json
"ConnectionStrings": {
    "DefaultConnection": "Server=host.docker.internal,1433;Database=NihomeDB;User Id=sa;Password=<development-password>;TrustServerCertificate=True;"
}
```

For a tool running directly on the host, use:

We need to declare the `appsettings.json` like
```json
"ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=NihomeDB;User Id=sa;Password=<development-password>;TrustServerCertificate=True;"
}
```

## Backend commands

```bash
docker exec nihome31042025-backend dotnet build
docker exec nihome31042025-backend dotnet format --verify-no-changes
```

The Compose backend mounts only the application project, not the sibling test projects, and the current image does not install `dotnet-ef`. CI runs unit/integration tests in a full .NET 8 SDK checkout. See the developer guide for current test and migration prerequisites.

EF Core syntax from a .NET 8 SDK environment with `dotnet-ef` 8.x is:

```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

Keep database work isolated from the host SQL Server and review generated migration files before applying them.

## Swagger

Swagger is enabled only in the `Development` environment.

For the standard development setup in this repository, use:

- Swagger UI: `http://localhost:5043/swagger`
- OpenAPI JSON: `http://localhost:5043/swagger/v1/swagger.json`

Check the SQL Server database is created

```bash
docker run --platform linux/amd64 -it --rm --network container:nihome31042025-sqlserver mcr.microsoft.com/mssql-tools /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "<development-password>"

1> select name from sys.databases;
2> go

name                                                                                                                            
--------------------------------------------------------------------------------------------------------------------------------
master                                                                                                                          
tempdb                                                                                                                          
model                                                                                                                           
msdb                                                                                                                            
NihomeDB
```

### Useful SQL Commands

List all tables:

```sql
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';
GO
```

Describe a table (show columns, types, nullability):

```sql
EXEC sp_columns @table_name = 'YourTableName';
GO
```

Quick column overview:

```sql
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'YourTableName';
GO
```

Show all indexes on a table:

```sql
EXEC sp_helpindex 'YourTableName';
GO
```

## IMPORTANT

When you attempt to add new model in `Models/`
1. Adding a new Model class.
2. Adding a new Property to a Model.
3. Renaming a property.
4. Chaing data types.
5. Adding a foreign key.
6. Adding a new table.
7. Changing relationships (1 - Many, Many - Many).
8. Renaming a table.

These changes affect how EF Core expects the SQL database to look. Generate and review a migration in the Docker-based .NET 8 SDK tooling environment described in the developer guide, then apply it only after review:

```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

The `Migrations/` folder is shared schema history and must not be deleted to reset development data. For a disposable Compose database, use `docker compose down -v` and start the stack again.

## JIRA ticket
https://endava-team-nawxok20.atlassian.net/jira/software/projects/NIH/boards/3

## Documentation

- Developer setup, architecture, migrations, testing, and deployment: [`docs/application_developer.md`](docs/application_developer.md)
- Product workflows and API reference: [`docs/user_guide.md`](docs/user_guide.md)
- Roles and permission behavior: [`docs/users-rbac.md`](docs/users-rbac.md)
- Browser test operation: [`nihomeweb/e2e/README.md`](nihomeweb/e2e/README.md)

## WoW
1. Create the merge request, write clear commit message before push.
2. For the backend, need to wait for the workflow CI passed before merge.
3. Resolve all conflicts, review requests before merge.


## SQL Schema
![Nicon DB Schema](./nicon_sql_schema.png)

## For convience, run the auto-deployment.sh to auto script
```bash
$ ./auto-deployment.sh
```

Use `deployment-config/output/publish-release.zip` as the IIS hosting artifact.