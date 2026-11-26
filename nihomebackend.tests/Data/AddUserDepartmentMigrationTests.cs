using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NihomeBackend.Data;

namespace nihomebackend.tests.Data;

public sealed class AddUserDepartmentMigrationTests
{
    private const string Id = "20261126100000_AddUserDepartment";

    [Fact]
    public void Upgrade_fills_every_department_from_the_current_role_and_downgrade_removes_the_column()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var migrations = db.GetService<IMigrationsAssembly>();
        Assert.True(migrations.Migrations.ContainsKey(Id), "Existing databases need the user department migration.");
        var migration = migrations.CreateMigration(migrations.Migrations[Id], db.Database.ProviderName!);

        Exec("CREATE TABLE roles (Id INTEGER, RoleGroupId INTEGER NULL)");
        Exec("CREATE TABLE users (Id INTEGER, RoleEntityId INTEGER NULL, DepartmentRoleGroupId INTEGER NULL)");
        Exec("INSERT INTO roles VALUES (1, 10), (2, NULL)");
        // A department role, a role without a department (BGD), and no role at all.
        Exec("INSERT INTO users VALUES (101, 1, NULL), (102, 2, NULL), (103, NULL, NULL)");

        foreach (var operation in migration.UpOperations.OfType<SqlOperation>())
            Exec(operation.Sql);

        Assert.Equal(10L, Department(101));
        Assert.Null(Department(102));
        Assert.Null(Department(103));
        Assert.Contains(migration.DownOperations.OfType<DropColumnOperation>(),
            op => op.Table == "users" && op.Name == "DepartmentRoleGroupId");

        void Exec(string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        long? Department(int userId)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT DepartmentRoleGroupId FROM users WHERE Id = {userId}";
            return command.ExecuteScalar() is long value ? value : null;
        }
    }
}
