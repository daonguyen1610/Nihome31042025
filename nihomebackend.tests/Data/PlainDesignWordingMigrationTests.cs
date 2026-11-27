using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NihomeBackend.Data;
using nihomebackend.Migrations;

namespace nihomebackend.tests.Data;

public sealed class PlainDesignWordingMigrationTests
{
    [Fact]
    public void Upgrade_rewrites_untouched_seed_text_and_keeps_admin_edits()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        connection.CreateFunction("SYSUTCDATETIME", () => DateTime.UtcNow.ToString("O"));
        connection.CreateCollation("Latin1_General_100_BIN2", string.CompareOrdinal);
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var migrations = db.GetService<IMigrationsAssembly>();
        const string id = "20261127032442_PlainDesignWording";
        var migration = migrations.CreateMigration(migrations.Migrations[id], db.Database.ProviderName!);
        Exec("CREATE TABLE translations ([Key] TEXT, LanguageCode TEXT, Value TEXT, UpdatedAt TEXT)");

        var (editedKey, editedLang, editedOld, _) = PlainDesignWording.Changes[0];
        foreach (var (key, lang, oldValue, _) in PlainDesignWording.Changes)
            Insert(key, lang, key == editedKey && lang == editedLang ? editedOld.ToUpperInvariant() : oldValue);

        Apply(migration.UpOperations);
        foreach (var (key, lang, _, newValue) in PlainDesignWording.Changes.Skip(1))
            Assert.Equal(newValue, Value(key, lang));
        Assert.Equal(editedOld.ToUpperInvariant(), Value(editedKey, editedLang));

        Apply(migration.DownOperations);
        foreach (var (key, lang, oldValue, _) in PlainDesignWording.Changes.Skip(1))
            Assert.Equal(oldValue, Value(key, lang));

        void Exec(string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        void Insert(string key, string lang, string value)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO translations ([Key], LanguageCode, Value) VALUES ($k, $l, $v)";
            command.Parameters.AddWithValue("$k", key);
            command.Parameters.AddWithValue("$l", lang);
            command.Parameters.AddWithValue("$v", value);
            command.ExecuteNonQuery();
        }
        string? Value(string key, string lang)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM translations WHERE [Key] = $k AND LanguageCode = $l";
            command.Parameters.AddWithValue("$k", key);
            command.Parameters.AddWithValue("$l", lang);
            return command.ExecuteScalar() as string;
        }
        // SQLite runs the same guarded UPDATE once SQL Server's N'' prefix is removed.
        void Apply(IReadOnlyList<MigrationOperation> operations)
        {
            foreach (var operation in operations.Cast<SqlOperation>())
                Exec(operation.Sql.Replace("N'", "'"));
        }
    }
}
