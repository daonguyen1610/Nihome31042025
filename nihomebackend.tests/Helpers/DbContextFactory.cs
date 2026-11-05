using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace nihomebackend.tests.Helpers;

public static class DbContextFactory
{
    public static AppDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Relational in-memory database for services that use set-based operations
    /// (ExecuteUpdate/ExecuteDelete), which the InMemory provider cannot translate.
    /// The open connection keeps the schema alive for the context's lifetime.
    /// </summary>
    public static AppDbContext CreateRelational()
    {
        // Foreign keys stay off to match the InMemory provider these tests were
        // written against; fixtures intentionally omit unrelated parent rows
        // (for example a seeded customer for a project) that the tested service
        // never reads.
        var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new SqliteAppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
