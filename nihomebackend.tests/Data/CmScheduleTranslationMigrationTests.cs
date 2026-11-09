using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NihomeBackend.Data;

namespace nihomebackend.tests.Data;

public sealed class CmScheduleTranslationMigrationTests
{
    [Fact]
    public void Upgrade_updates_previous_defaults_in_all_languages_and_preserves_admin_edits()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        connection.CreateFunction("SYSUTCDATETIME", () => DateTime.UtcNow.ToString("O"));
        connection.CreateCollation("Latin1_General_100_BIN2", string.CompareOrdinal);
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var migrations = db.GetService<IMigrationsAssembly>();
        const string id = "20261109130000_SyncCmScheduleTranslations";
        Assert.True(migrations.Migrations.ContainsKey(id), "Existing databases need the CM wording upgrade migration.");
        var migration = migrations.CreateMigration(migrations.Migrations[id], db.Database.ProviderName!);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE translations ([Key] TEXT, LanguageCode TEXT, Value TEXT, UpdatedAt TEXT)";
            command.ExecuteNonQuery();
        }
        var assembly = typeof(TranslationSeeder).Assembly;
        using var stream = assembly.GetManifestResourceStream("nihomebackend.Data.Seeds.i18n.cm-schedule.json")!;
        using var seeds = JsonDocument.Parse(stream);
        var changed = seeds.RootElement.EnumerateArray().Where(x => x.TryGetProperty("old", out _)).ToList();
        Assert.Equal(4, changed.Count);
        foreach (var entry in changed)
            foreach (var language in new[] { "vi", "en", "zh", "ja" })
            {
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO translations ([Key], LanguageCode, Value) VALUES ($key, $language, $value)";
                command.Parameters.AddWithValue("$key", entry.GetProperty("key").GetString());
                command.Parameters.AddWithValue("$language", language);
                command.Parameters.AddWithValue("$value", entry.GetProperty("old").GetProperty(language).GetString());
                command.ExecuteNonQuery();
            }
        Apply(migration.UpOperations);
        AssertValues(false);
        Apply(migration.DownOperations);
        AssertValues(true);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE translations SET Value = 'Admin wording'";
            command.ExecuteNonQuery();
        }
        Apply(migration.UpOperations);
        Apply(migration.DownOperations);
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM translations WHERE Value <> 'Admin wording'";
        Assert.Equal(0L, check.ExecuteScalar());
        check.CommandText = "UPDATE translations SET Value = 'WEIGHT' WHERE [Key] = 'cmSchedule.weight' AND LanguageCode = 'en'";
        check.ExecuteNonQuery();
        Apply(migration.UpOperations);
        Apply(migration.DownOperations);
        check.CommandText = "SELECT Value FROM translations WHERE [Key] = 'cmSchedule.weight' AND LanguageCode = 'en'";
        Assert.Equal("WEIGHT", check.ExecuteScalar());

        void Apply(IReadOnlyList<MigrationOperation> operations)
        {
            foreach (var operation in operations.Cast<SqlOperation>())
            {
                using var command = connection.CreateCommand();
                // SQLite accepts the same guarded UPDATE; remove SQL Server's
                // Unicode literal prefix. Live Compose also executes the SQL.
                command.CommandText = operation.Sql.Replace("N'", "'");
                command.ExecuteNonQuery();
            }
        }
        void AssertValues(bool previous)
        {
            foreach (var entry in changed)
                foreach (var language in new[] { "vi", "en", "zh", "ja" })
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT Value FROM translations WHERE [Key] = $key AND LanguageCode = $language";
                    command.Parameters.AddWithValue("$key", entry.GetProperty("key").GetString());
                    command.Parameters.AddWithValue("$language", language);
                    Assert.Equal((previous ? entry.GetProperty("old") : entry).GetProperty(language).GetString(), command.ExecuteScalar());
                }
        }
    }
}
