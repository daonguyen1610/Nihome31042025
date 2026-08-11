using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.Data;

public static class SampleVendorDataSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Vendors.Any()) return;
        var owners = db.Users.Where(u => u.IsActive).OrderBy(u => u.Id).Take(3).ToList();
        if (owners.Count == 0) return;
        while (owners.Count < 3) owners.Add(owners[0]);
        var now = DateTime.UtcNow;
        var vendors = new[]
        {
            Create("NCC-001", "Công ty Vật liệu Nihome", VendorType.Supplier, "construction-materials", owners[0], true, now),
            Create("NTP-001", "Công ty Thi công Thành Công", VendorType.SubContractor, "civil-works", owners[1], true, now),
            Create("NCC-002", "Công ty Cơ điện Bốn Mùa", VendorType.Both, "mep", owners[2], false, now),
        };
        db.Vendors.AddRange(vendors);
        db.SaveChanges();

        var project = db.DesignProjects.OrderBy(p => p.Id).FirstOrDefault();
        if (project is null) return;
        db.VendorEvaluations.Add(new VendorEvaluation
        {
            VendorId = vendors[0].Id,
            ProjectId = project.Id,
            ScoreQuality = 8,
            ScoreSchedule = 7,
            ScoreCost = 8,
            ScoreSafety = 9,
            Comment = "Dữ liệu đánh giá mẫu phục vụ QA.",
            EvaluatedByUserId = owners[0].Id,
            EvaluatedAt = now,
            UpdatedByUserId = owners[0].Id,
            UpdatedAt = now,
        });
        db.SaveChanges();
    }

    private static Vendor Create(string code, string name, VendorType type, string group, ApplicationUser owner, bool active, DateTime now) => new()
    {
        VendorCode = code,
        CompanyName = name,
        NormalizedCompanyName = VendorService.NormalizeCompanyName(name),
        VendorType = type,
        TaxCode = $"QA-{code}",
        Phone = "0900000000",
        Email = $"{code.ToLowerInvariant()}@nihome.test",
        Address = "Thành phố Hồ Chí Minh",
        ContactPerson = "QA Contact",
        LicenseNo = $"LICENSE-{code}",
        ServiceGroupCode = group,
        OwnerUserId = owner.Id,
        IsActive = active,
        CreatedAt = now,
        CreatedByUserId = owner.Id,
        UpdatedAt = now,
        UpdatedByUserId = owner.Id,
    };
}
