using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real ASP.NET Core app in-process for integration tests.
/// Replaces the SQL Server DbContext with an EF InMemory provider so the full
/// HTTP pipeline (routing, auth, EF queries, AutoMapper, JSON, middleware) runs
/// in-process without external infrastructure. NOTE: the InMemory provider does
/// not enforce relational constraints; for full SQL Server parity, swap to
/// Testcontainers in a follow-up.
/// </summary>
public class NihomeWebApplicationFactory : WebApplicationFactory<Program>
{
    // Unique DB name per factory instance so parallel test classes don't share state.
    private readonly string _databaseName = $"NihomeIntegrationTests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            var testSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.IntegrationTests.json");
            if (File.Exists(testSettings))
            {
                cfg.AddJsonFile(testSettings, optional: false, reloadOnChange: false);
            }
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName)
                    .AddInterceptors(new TestRowVersionInterceptor());
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            TestDataSeeder.Seed(db);
        });
    }
}

internal sealed class TestRowVersionInterceptor : SaveChangesInterceptor
{
    private static long _version;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AssignRowVersions(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AssignRowVersions(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void AssignRowVersions(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<IConcurrencyTracked>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            entry.Entity.RowVersion = BitConverter.GetBytes(Interlocked.Increment(ref _version));
        }
    }
}

internal static class ServiceCollectionExtensionsForTests
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var d in descriptors)
        {
            services.Remove(d);
        }
    }
}
