using NihomeBackend.Data;
using NihomeBackend.Models;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

/// <summary>
/// Verifies MasterDataSeeder is idempotent and loads the shipped
/// <c>Data/Seeds/master-data/defaults.json</c> catalogue correctly.
/// </summary>
public class MasterDataSeederTests : IDisposable
{
    private readonly AppDbContext _db;

    public MasterDataSeederTests()
    {
        _db = DbContextFactory.Create();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Seed_LoadsAllRequiredCategoriesForGd2()
    {
        MasterDataSeeder.Seed(_db);

        var categories = _db.MasterDataOptions
            .Select(o => o.Category)
            .Distinct()
            .ToList();

        foreach (var expected in new[]
        {
            "customer_type", "customer_source", "customer_status",
            "lead_status",
            "opportunity_stage", "opportunity_lost_reason",
            "quote_status", "tender_status", "contract_status",
            "design_discipline",
            "permit_type", "permit_status",
        })
        {
            Assert.Contains(expected, categories);
        }
    }

    [Fact]
    public void Seed_PopulatesLabelKeyByConvention()
    {
        MasterDataSeeder.Seed(_db);

        var pccc = _db.MasterDataOptions
            .Single(o => o.Category == "permit_type" && o.Code == "pccc");

        Assert.Equal("masterData.permit_type.pccc.label", pccc.LabelKey);
    }

    [Fact]
    public void Seed_KnownEssentialEntriesArePresent()
    {
        MasterDataSeeder.Seed(_db);

        var pairs = _db.MasterDataOptions
            .Select(o => o.Category + "|" + o.Code)
            .ToHashSet();

        foreach (var pair in new[]
        {
            // Core CRM entries that quotes / contracts rely on.
            "customer_type|individual",
            "customer_type|company",
            "opportunity_stage|prospecting",
            "opportunity_stage|won",
            "opportunity_stage|lost",
            "opportunity_lost_reason|price",
            "quote_status|approved",
            "contract_status|in-progress",
            "tender_status|won",
            "permit_type|gpxd",
            "permit_status|issued",
            "design_discipline|architecture",
            "design_discipline|mep",
        })
        {
            Assert.Contains(pair, pairs);
        }
    }

    [Fact]
    public void Seed_IsIdempotent_ExistingRowsAreNotDuplicated()
    {
        MasterDataSeeder.Seed(_db);
        var afterFirst = _db.MasterDataOptions.Count();

        MasterDataSeeder.Seed(_db);
        var afterSecond = _db.MasterDataOptions.Count();

        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public void Seed_DoesNotOverwriteAdminEditedName()
    {
        MasterDataSeeder.Seed(_db);
        var row = _db.MasterDataOptions.Single(o => o.Category == "customer_type" && o.Code == "individual");
        row.Name = "Cá nhân (đã sửa)";
        row.IsActive = false;
        _db.SaveChanges();

        // Second run should not re-seed this pair — it already exists.
        MasterDataSeeder.Seed(_db);

        var stillEdited = _db.MasterDataOptions
            .Single(o => o.Category == "customer_type" && o.Code == "individual");
        Assert.Equal("Cá nhân (đã sửa)", stillEdited.Name);
        Assert.False(stillEdited.IsActive);
    }

    [Fact]
    public void Seed_AllOptionsAreActiveAndNamesPopulated()
    {
        MasterDataSeeder.Seed(_db);

        foreach (var opt in _db.MasterDataOptions)
        {
            Assert.False(string.IsNullOrWhiteSpace(opt.Category), $"missing category on id {opt.Id}");
            Assert.False(string.IsNullOrWhiteSpace(opt.Code), $"missing code on id {opt.Id}");
            Assert.False(string.IsNullOrWhiteSpace(opt.Name), $"missing name on {opt.Category}/{opt.Code}");
            Assert.True(opt.IsActive, $"expected active by default for {opt.Category}/{opt.Code}");
        }
    }
}
