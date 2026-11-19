using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Data;
using NihomeBackend.Extensions;

namespace nihomebackend.tests.Infrastructure;

public sealed class DataProtectionKeyRingTests
{
    // Production lost its in-memory key ring on every redeploy, so the saved
    // Google Drive Client Secret could no longer be decrypted.
    [Fact]
    public void SecretsEncryptedBeforeARestart_CanStillBeReadAfterIt()
    {
        var database = Guid.NewGuid().ToString();
        const string purpose = "Nicon.GoogleDrive.ClientSecret.v1";

        string encrypted;
        using (var before = BuildApp(database))
            encrypted = before.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector(purpose).Protect("GOCSPX-client-secret");

        using var after = BuildApp(database);
        var decrypted = after.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(purpose).Unprotect(encrypted);

        Assert.Equal("GOCSPX-client-secret", decrypted);
        using var scope = after.CreateScope();
        Assert.NotEmpty(scope.ServiceProvider.GetRequiredService<AppDbContext>().DataProtectionKeys);
    }

    private static ServiceProvider BuildApp(string database)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(database));
        services.AddNiconDataProtection(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }
}
