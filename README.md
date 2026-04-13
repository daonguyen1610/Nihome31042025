# Nihome31042025

This project is using
1. ASP.NET Core MVC(.NET 8). Version 8.0.4
2. Entity Framework Core 8.
3. SQL Server 2022

## For Docker dev
If you are using the docker, simply run

```bash
$ docker compose up -d
```

It will run the SQLServer DB, Web Application, and ASP .NET for you with hot-reload feature.

## Connect to DB with ASP .NET Core

To connect to the MySQL database. If you manage to run ASP .NET with docker.

We need to declare the `appsettings.json` like
```json
"ConnectionStrings": {
    "DefaultConnection": "Server=host.docker.internal,1433;Database=NihomeDB;User Id=sa;Password=Nihome@12092025;TrustServerCertificate=True;"
}
```

Otherwise if you are running thee ASP .NET in local dev.

We need to declare the `appsettings.json` like
```json
"ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=NihomeDB;User Id=sa;Password=Nihome@12092025;TrustServerCertificate=True;"
}
```

## How to run dotnet

```bash
cd NihomeBackend
dotnet run
```

Run with hot reload (auto refresh when you edit code).

```bash
dotnet watch run
```

Best for development because it recompiles automatically when you modify files.

Check the SQL Server database is created

```bash
docker run --platform linux/amd64 -it --rm --network container:Nihome12092025-sqlserver mcr.microsoft.com/mssql-tools /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "Nihome@12092025"

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

For SQL Server Cheat Sheet Command, please look at: [SQL Server Cheat Sheet](docs/sqlserver_cheatsheet.md).

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

These changes affect how EF Core expects the SQL Database to look. So you must run following commands:

```bash
dotnet ef migrations add <Migration Name>
dotnet ef database update
```

Migrations/ folder stores schema history. You only delete the folder if intentionally want to recreate the database from scratch.