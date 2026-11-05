using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace nihomebackend.tests.Services;

public sealed class ProjectScheduleEmailMigrationTests
{
    [Fact]
    public void FullDiffMigrationIsRegisteredForTheProductionContext()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).Options);

        Assert.Contains("20261105122000_AllowFullProjectScheduleEmailDiff", db.Database.GetMigrations());
    }
}
