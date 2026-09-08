using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.Data;

/// <summary>Owns only the dedicated RFQ demo project; never attaches records to customer projects.</summary>
public static class RfqSampleDataSeeder
{
    public static void Seed(AppDbContext db)
    {
        const string code = "PJ-SAMPLE-RFQ";
        if (db.OperationalProjects.Any(x => x.Code == code) ||
            db.SeededRootDeletions.Any(x => x.ResourceType == "OperationalProject" && x.ResourceKey == code)) return;
        var owner = db.Users.FirstOrDefault(x => x.IsActive && x.RoleEntity != null && x.RoleEntity.Code == "PROCUREMENT");
        var pm = db.Users.FirstOrDefault(x => x.IsActive && x.RoleEntity != null && x.RoleEntity.Code == "PM");
        if (owner is null || pm is null) return;
        var primary = db.Vendors.SingleOrDefault(x => x.IsActive && x.VendorCode == "NCC-ELECTRIC-01");
        if (primary is null) return;
        var alternative = db.Vendors.SingleOrDefault(x => x.VendorCode == "NCC-RFQ-SAMPLE-02");
        if (alternative is not null && !alternative.IsActive) return;
        if (alternative is null)
        {
            alternative = new Vendor
            {
                VendorCode = "NCC-RFQ-SAMPLE-02",
                CompanyName = "[SAMPLE] Alternative electrical supplier",
                VendorType = VendorType.Supplier,
                IsActive = true,
                Phone = "0900000599",
                Email = "rfq.supplier.demo@example.com",
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
            };
            db.Vendors.Add(alternative);
            db.SaveChanges();
        }
        var vendors = new[] { primary, alternative };
        using var transaction = db.Database.IsRelational() ? db.Database.BeginTransaction() : null;
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Name = "[SAMPLE] RFQ factory customer",
            Type = CustomerType.Individual,
            SourceCode = "referral",
            Contacts = [new() { FullName = "RFQ demo contact", Phone = "0900000199", Email = "rfq.demo@example.com", IsPrimary = true }],
        };
        var project = new OperationalProject
        {
            Code = code,
            Name = "[SAMPLE] Factory supply comparison",
            Customer = customer,
            Status = OperationalProjectStatus.Active,
            ProjectManagerUserId = pm.Id,
            CreatedByUserId = pm.Id,
            Note = "[SAMPLE_RFQ] Dedicated procurement demonstration.",
        };
        db.OperationalProjects.Add(project);
        db.SaveChanges();
        db.OperationalProjectMembers.Add(new OperationalProjectMember
        {
            OperationalProjectId = project.Id,
            UserId = owner.Id,
            Position = "Procurement",
            StartedAt = now,
            CreatedByUserId = pm.Id,
            UpdatedByUserId = pm.Id,
        });
        var boq = new ProjectBoqRevision
        {
            OperationalProjectId = project.Id,
            RevisionNumber = 1,
            Currency = "VND",
            Status = ProjectBoqRevisionStatus.Approved,
            PreparedByUserId = owner.Id,
            SubmittedByUserId = owner.Id,
            SubmittedAt = now.AddDays(-10),
            ApprovedByUserId = pm.Id,
            ApprovedAt = now.AddDays(-9),
            CostTotal = 30000000m,
            Lines = [new() { ItemCode = "CABLE-01", Description = "Power cable", Unit = "m", ApprovedQuantity = 100, BudgetUnitPrice = 200000, Amount = 20000000 },
                new() { ItemCode = "PANEL-01", Description = "Distribution panel", Unit = "set", ApprovedQuantity = 2, BudgetUnitPrice = 5000000, Amount = 10000000, SortOrder = 1 }],
        };
        db.ProjectBoqRevisions.Add(boq);
        db.SaveChanges();
        project.FinalProjectBoqRevisionId = boq.Id;
        for (var index = 1; index <= 3; index++)
        {
            var rfq = new Rfq
            {
                Code = $"RFQ-SAMPLE-{index:D3}",
                Title = $"[SAMPLE] Electrical package {index}",
                OperationalProjectId = project.Id,
                SourceBoqRevisionId = boq.Id,
                OwnerUserId = owner.Id,
                DueAt = index == 3 ? now.AddDays(-1) : now.AddDays(14),
                IssuedAt = index == 1 ? null : now.AddDays(-7),
                Status = index == 1 ? RfqStatus.Draft : RfqStatus.Issued,
                Note = "[SAMPLE_RFQ] One package award; compare commercial terms before deciding.",
                Lines = boq.Lines.Select(x => new RfqLine
                {
                    ProjectBoqLineId = x.Id,
                    ItemCode = x.ItemCode,
                    Description = x.Description,
                    Unit = x.Unit,
                    Quantity = x.ApprovedQuantity,
                    BudgetUnitPrice = x.BudgetUnitPrice,
                }).ToList(),
                Invitations = vendors.Select(x => new RfqInvitation { VendorId = x.Id, VendorName = x.CompanyName }).ToList(),
                Events = [new() { Action = "created", ActorUserId = owner.Id, At = now.AddDays(-8) }],
            };
            if (index > 1) rfq.Events.Add(new RfqEvent { Action = "issue", ActorUserId = owner.Id, At = now.AddDays(-7) });
            db.Rfqs.Add(rfq);
            db.SaveChanges();
            if (index != 2) continue;
            for (var vendorIndex = 0; vendorIndex < 2; vendorIndex++)
            {
                var lines = rfq.Lines.Take(vendorIndex == 0 ? 2 : 1).Select(x => new RfqBidLine
                {
                    RfqLineId = x.Id,
                    UnitPrice = x.BudgetUnitPrice * (vendorIndex == 0 ? 0.95m : 0.9m),
                    Amount = x.Quantity * x.BudgetUnitPrice * (vendorIndex == 0 ? 0.95m : 0.9m),
                }).ToList();
                rfq.Bids.Add(new RfqBid
                {
                    VendorId = vendors[vendorIndex].Id,
                    Revision = 1,
                    LeadTimeDays = 14,
                    PaymentTerms = "30% advance, 70% on delivery",
                    ValidUntil = now.AddDays(30),
                    SubmittedAt = now.AddDays(-2),
                    SubmittedByUserId = owner.Id,
                    Note = vendorIndex == 0 ? "Complete scope" : "Panel not quoted",
                    Lines = lines,
                    Total = lines.Sum(x => x.Amount),
                });
                rfq.Events.Add(new RfqEvent { Action = "bid-submitted", ActorUserId = owner.Id, At = now.AddDays(-2) });
            }
            db.SaveChanges();
        }
        db.SaveChanges();
        transaction?.Commit();
    }
}
