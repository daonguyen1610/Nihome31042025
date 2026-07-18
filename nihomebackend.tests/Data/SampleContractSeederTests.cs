using NihomeBackend.Data;
using NihomeBackend.Models;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

/// <summary>
/// Verifies the sample-contract branch of <see cref="SampleCrmDataSeeder"/>:
/// seeds sample customers first (required FK), runs the top-level seeder and
/// asserts every ContractStatus branch has coverage and re-runs are no-ops.
/// </summary>
public class SampleContractSeederTests : IDisposable
{
    private readonly AppDbContext _db;

    public SampleContractSeederTests()
    {
        _db = DbContextFactory.Create();
        // The seeder resolves an owner via the SALE test user's phone; when
        // absent it falls back to any SUPER_ADMIN row. Seed the minimum.
        _db.Users.Add(new ApplicationUser
        {
            PhoneNumber = "0335240370",
            FullName = "Super Admin",
            Email = "superadmin@example.com",
            Role = UserRole.SUPER_ADMIN,
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Seed_InsertsSampleContractsCoveringMultipleStatuses()
    {
        SampleCrmDataSeeder.Seed(_db);

        var contracts = _db.Contracts.ToList();
        Assert.NotEmpty(contracts);

        // The seed list drives one row per status entry we authored — assert
        // the flagship statuses are covered so filters + badge have data.
        var statuses = contracts.Select(c => c.Status).Distinct().ToList();
        Assert.Contains(ContractStatus.Draft, statuses);
        Assert.Contains(ContractStatus.Signed, statuses);
        Assert.Contains(ContractStatus.InProgress, statuses);
        Assert.Contains(ContractStatus.OnHold, statuses);
    }

    [Fact]
    public void Seed_GeneratesUniqueContractNumbers()
    {
        SampleCrmDataSeeder.Seed(_db);

        var numbers = _db.Contracts.Select(c => c.ContractNumber).ToList();
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.All(numbers, n => Assert.StartsWith("HD-", n));
    }

    [Fact]
    public void Seed_IsIdempotent_AndPreservesAdminEdits()
    {
        SampleCrmDataSeeder.Seed(_db);
        var edited = _db.Contracts.First();
        var originalName = edited.ContractNumber;
        edited.Note = "[SAMPLE_CONTRACT] Custom admin note override";
        edited.Value = 999_999_999m;
        _db.SaveChanges();

        var countBefore = _db.Contracts.Count();
        SampleCrmDataSeeder.Seed(_db);
        var countAfter = _db.Contracts.Count();

        Assert.Equal(countBefore, countAfter);
        var reloaded = _db.Contracts.Single(c => c.ContractNumber == originalName);
        Assert.Equal(999_999_999m, reloaded.Value);
    }
}
