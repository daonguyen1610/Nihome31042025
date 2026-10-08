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
                CompanyName = "Công ty TNHH Thiết bị Điện Nam Long",
                VendorType = VendorType.Supplier,
                IsActive = true,
                Phone = "0900000599",
                Email = "baogia.namlong@example.com",
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
            Name = "Công ty TNHH Cơ khí Hòa Bình",
            Type = CustomerType.Company,
            TaxId = "0100000099",
            Address = "KCN Quang Minh, huyện Mê Linh, Hà Nội",
            RepresentativeName = "Ông Nguyễn Đức Hòa",
            SourceCode = "referral",
            CreatedAt = now.AddDays(-30),
            UpdatedAt = now.AddDays(-30),
            Contacts = [new() { FullName = "Trần Văn Hòa", Position = "Trưởng phòng Mua hàng", Phone = "0900000199", Email = "muahang.hoabinh@example.com", IsPrimary = true }],
        };
        var project = new OperationalProject
        {
            Code = code,
            Name = "Nhà máy cơ khí Hòa Bình – KCN Quang Minh",
            Customer = customer,
            Status = OperationalProjectStatus.Active,
            ProjectManagerUserId = pm.Id,
            CreatedByUserId = pm.Id,
            Note = "[SAMPLE_RFQ] Dự án nhà máy cơ khí; BOQ điện cấp nguồn cho các gói hỏi giá cung ứng.",
            CreatedAt = now.AddDays(-20),
            UpdatedAt = now.AddDays(-2),
        };
        db.OperationalProjects.Add(project);
        db.SaveChanges();
        db.OperationalProjectMembers.Add(new OperationalProjectMember
        {
            OperationalProjectId = project.Id,
            UserId = owner.Id,
            Position = "Procurement",
            StartedAt = now.AddDays(-20),
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
            CreatedAt = now.AddDays(-12),
            UpdatedAt = now.AddDays(-9),
            CostTotal = 30000000m,
            Lines = [new() { ItemCode = "CABLE-01", Description = "Cáp điện lực Cu/XLPE/PVC 0,6/1 kV", Unit = "m", ApprovedQuantity = 100, BudgetUnitPrice = 200000, Amount = 20000000 },
                new() { ItemCode = "PANEL-01", Description = "Tủ phân phối điện MDB 400 A", Unit = "bộ", ApprovedQuantity = 2, BudgetUnitPrice = 5000000, Amount = 10000000, SortOrder = 1 }],
        };
        db.ProjectBoqRevisions.Add(boq);
        db.SaveChanges();
        project.FinalProjectBoqRevisionId = boq.Id;
        for (var index = 1; index <= 2; index++)
        {
            var rfq = new Rfq
            {
                Code = $"RFQ-SAMPLE-{index:D3}",
                Title = index == 1
                    ? "Dự thảo gói cáp và tủ điện nhà máy cơ khí Hòa Bình"
                    : "Tủ phân phối và cáp điện nhà máy cơ khí Hòa Bình",
                OperationalProjectId = project.Id,
                SourceBoqRevisionId = boq.Id,
                OwnerUserId = owner.Id,
                DueAt = now.AddDays(14),
                IssuedAt = index == 1 ? null : now.AddDays(-7),
                Status = index == 1 ? RfqStatus.Draft : RfqStatus.Issued,
                CreatedAt = now.AddDays(-8),
                UpdatedAt = index == 1 ? now.AddDays(-8) : now.AddDays(-2),
                Note = "[SAMPLE_RFQ] So sánh phạm vi, đơn giá, tiến độ giao hàng và điều khoản thanh toán.",
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
                    PaymentTerms = "Tạm ứng 30%, thanh toán 70% sau nghiệm thu giao hàng",
                    ValidUntil = now.AddDays(30),
                    SubmittedAt = now.AddDays(-2),
                    SubmittedByUserId = owner.Id,
                    Note = vendorIndex == 0 ? "Báo giá đủ cáp và tủ điện" : "Chỉ báo giá cáp điện, chưa có tủ phân phối",
                    Lines = lines,
                    Currency = "VND",
                    ExchangeRateToVnd = 1m,
                    Subtotal = lines.Sum(x => x.Amount),
                    TotalOriginal = lines.Sum(x => x.Amount),
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
