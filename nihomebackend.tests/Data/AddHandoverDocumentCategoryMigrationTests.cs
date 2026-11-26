using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace nihomebackend.tests.Data;

public sealed class AddHandoverDocumentCategoryMigrationTests
{
    [Fact]
    public void Upgrade_moves_handover_and_as_built_documents_and_keeps_acceptance_records()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var migrations = db.GetService<IMigrationsAssembly>();
        var id = migrations.Migrations.Keys.Single(key => key.EndsWith("_AddHandoverDocumentCategory", StringComparison.Ordinal));
        var migration = migrations.CreateMigration(migrations.Migrations[id], db.Database.ProviderName!);
        Exec("CREATE TABLE project_documents (Id INTEGER PRIMARY KEY, Category INTEGER, SourceEntityType TEXT)");
        const int acceptance = (int)ProjectDocumentCategory.ConstructionAcceptance;
        Exec($"INSERT INTO project_documents VALUES (1, {acceptance}, 'HandoverRecord'), " +
            $"(2, {acceptance}, 'AsBuiltDocument'), (3, {acceptance}, 'AcceptanceRecord')");

        Apply(migration.UpOperations);
        Assert.Equal(10L, Category(1));
        Assert.Equal(10L, Category(2));
        Assert.Equal((long)acceptance, Category(3));

        Apply(migration.DownOperations);
        Assert.Equal((long)acceptance, Category(1));
        Assert.Equal((long)acceptance, Category(2));
        Assert.Equal((long)acceptance, Category(3));

        void Exec(string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        long Category(int documentId)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT Category FROM project_documents WHERE Id = {documentId}";
            return (long)command.ExecuteScalar()!;
        }
        // Only the data statements run here: SQLite has no schema for the column operations.
        void Apply(IReadOnlyList<MigrationOperation> operations)
        {
            foreach (var operation in operations.OfType<SqlOperation>()) Exec(operation.Sql);
        }
    }
}
