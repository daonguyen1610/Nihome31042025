using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace NihomeBackend.Extensions;

public static class HostExtensions
{
    public static void MigrateDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<AppDbContext>>();
        var db = services.GetRequiredService<AppDbContext>();

        try
        {
            logger.LogInformation("Starting database initialization...");

            if (db.Database.GetMigrations().Any())
            {
                db.Database.Migrate();
            }
            else
            {
                db.Database.EnsureCreated();
            }

            logger.LogInformation("Database initialization completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during database initialization");
            throw;
        }

        try
        {
            logger.LogInformation("Starting database seeding...");
            // NOTE: intentionally NOT using app.Environment.WebRootPath because
            // this project points WebRootPath at the compiled SPA (/nihomeweb/dist)
            // via a custom Kestrel setup. Seeded assets belong under the API's
            // own wwwroot, next to /files/capability/ etc.
            var webRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
            DbSeeder.Seed(db, webRoot);
            logger.LogInformation("Database seeding completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during database seeding");
            throw;
        }
    }
}
