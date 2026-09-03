using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using nihomebackend.Migrations;

namespace NihomeBackend.IntegrationTests.Infrastructure;

public class SeededRootDeletionMigrationTests
{
    [Fact]
    public void Up_BackfillSkipsOccupiedAndDuplicateOperationalProjectLinks()
    {
        var migration = new AddSeededRootDeletionTombstones();
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(AddSeededRootDeletionTombstones)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [migrationBuilder]);

        var sql = migrationBuilder.Operations.OfType<SqlOperation>().Single().Sql;

        sql.Should().Contain("NOT EXISTS");
        sql.Should().Contain("occupied.OperationalProjectId = contracts.OperationalProjectId");
        sql.Should().Contain("earlier_contract.OperationalProjectId = contracts.OperationalProjectId");
        sql.Should().Contain("earlier.Id < design_projects.Id");
    }
}