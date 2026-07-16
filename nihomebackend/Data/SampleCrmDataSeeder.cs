using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.Data;

/// <summary>
/// Populates a small, deterministic set of sample rows for the CRM features
/// (Leads + Customers + primary contact + one activity each) so a freshly
/// migrated database has data to demo bulk-select, filters and detail
/// dialogs without hand-crafting curl calls. Idempotent: every insert is
/// guarded so re-running is a no-op.
/// </summary>
public static class SampleCrmDataSeeder
{
    private const string SampleTag = "[SAMPLE]";

    public static void Seed(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        // Owner defaults to the SALE test user seeded by DbSeeder, so the
        // sample data plays well with the RBAC ownership scoping tested in
        // integration + e2e specs. If SALE is missing (e.g. a partial seed),
        // fall back to the first admin.
        var owner = db.Users.FirstOrDefault(u => u.PhoneNumber == "0911000003")
                    ?? db.Users.FirstOrDefault(u => u.Role == UserRole.SUPER_ADMIN)
                    ?? db.Users.FirstOrDefault();
        if (owner is null) return;

        SeedLeads(db, owner, now);
        SeedCustomers(db, owner, now);
        SeedOpportunities(db, owner, now);
        SeedQuotes(db, owner, now);
    }

    private static void SeedLeads(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        var samples = new[]
        {
            new Lead
            {
                Name = $"{SampleTag} Nguyễn Minh Anh",
                Phone = "0900000101",
                Email = "minh.anh.sample@example.com",
                SourceCode = "marketing",
                Status = LeadStatus.New,
                Note = "Lead mẫu từ chiến dịch Facebook Ads.",
            },
            new Lead
            {
                Name = $"{SampleTag} Trần Thị Bích",
                CompanyName = "Công ty TNHH Bích Anh",
                Phone = "0900000102",
                Email = "bich.anh.sample@example.com",
                SourceCode = "referral",
                Status = LeadStatus.Contacted,
                Note = "Lead mẫu do khách hàng cũ giới thiệu.",
            },
            new Lead
            {
                Name = $"{SampleTag} Phạm Quốc Cường",
                Phone = "0900000103",
                SourceCode = "website",
                Status = LeadStatus.Interested,
                Note = "Đã liên hệ qua form website, quan tâm gói cao cấp.",
            },
            new Lead
            {
                Name = $"{SampleTag} Lê Hồng Duyên",
                Email = "duyen.sample@example.com",
                SourceCode = "event",
                Status = LeadStatus.New,
                Note = "Ghi nhận tại sự kiện triển lãm nội thất tháng 10.",
            },
            new Lead
            {
                Name = $"{SampleTag} Đỗ Thanh Ê",
                Phone = "0900000105",
                SourceCode = "cold-call",
                Status = LeadStatus.NotInterested,
                Note = "Chưa có nhu cầu, hẹn liên hệ lại quý sau.",
            },
        };

        foreach (var s in samples)
        {
            var exists = db.Leads.Any(l => l.Name == s.Name);
            if (exists) continue;

            s.OwnerUserId = owner.Id;
            s.CreatedByUserId = owner.Id;
            s.UpdatedByUserId = owner.Id;
            s.CreatedAt = now;
            s.UpdatedAt = now;

            db.Leads.Add(s);
            db.SaveChanges();

            db.LeadActivities.Add(new LeadActivity
            {
                LeadId = s.Id,
                Type = LeadActivityType.Note,
                Content = "Ghi chú mẫu khởi tạo cho lead demo.",
                CreatedByUserId = owner.Id,
                CreatedAt = now,
            });
            db.SaveChanges();
        }
    }

    private static void SeedCustomers(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        var samples = new List<(Customer Customer, CustomerContact Contact)>
        {
            (
                new Customer
                {
                    Type = CustomerType.Individual,
                    Name = $"{SampleTag} Nguyễn Văn An",
                    SourceCode = "marketing",
                    RelationshipStatus = CustomerRelationshipStatus.Prospect,
                    Note = "Khách hàng cá nhân mẫu — kênh Marketing.",
                },
                new CustomerContact
                {
                    FullName = "Nguyễn Văn An",
                    Phone = "0900000201",
                    Email = "an.sample@example.com",
                    IsPrimary = true,
                }
            ),
            (
                new Customer
                {
                    Type = CustomerType.Individual,
                    Name = $"{SampleTag} Trần Thị Bảo",
                    SourceCode = "referral",
                    RelationshipStatus = CustomerRelationshipStatus.InProgress,
                    Note = "Khách hàng cá nhân đang trong quá trình tư vấn.",
                },
                new CustomerContact
                {
                    FullName = "Trần Thị Bảo",
                    Phone = "0900000202",
                    IsPrimary = true,
                }
            ),
            (
                new Customer
                {
                    Type = CustomerType.Company,
                    Name = $"{SampleTag} Công ty CP Xây dựng Alpha",
                    TaxId = "0100000001",
                    Address = "Số 1, Đường Alpha, Q.1, TP.HCM",
                    RepresentativeName = "Ông Nguyễn Văn Alpha",
                    SourceCode = "website",
                    RelationshipStatus = CustomerRelationshipStatus.Prospect,
                    Note = "Khách hàng doanh nghiệp mẫu — mảng B2B.",
                },
                new CustomerContact
                {
                    FullName = "Nguyễn Thị Anh",
                    Position = "Trưởng phòng Mua hàng",
                    Phone = "0900000301",
                    Email = "purchase.alpha.sample@example.com",
                    IsPrimary = true,
                }
            ),
            (
                new Customer
                {
                    Type = CustomerType.Company,
                    Name = $"{SampleTag} Công ty TNHH Beta Interior",
                    TaxId = "0100000002",
                    Address = "Số 2, Đường Beta, Q.3, TP.HCM",
                    RepresentativeName = "Bà Lê Thị Beta",
                    SourceCode = "referral",
                    RelationshipStatus = CustomerRelationshipStatus.Signed,
                    Note = "Khách hàng đã ký hợp đồng — dùng làm demo cho tab hợp đồng.",
                },
                new CustomerContact
                {
                    FullName = "Phạm Quốc Bảo",
                    Position = "Giám đốc dự án",
                    Phone = "0900000302",
                    Email = "pm.beta.sample@example.com",
                    IsPrimary = true,
                }
            ),
            (
                new Customer
                {
                    Type = CustomerType.Company,
                    Name = $"{SampleTag} Công ty CP Gamma Home",
                    TaxId = "0100000003",
                    Address = "Số 3, Đường Gamma, Q.7, TP.HCM",
                    RepresentativeName = "Ông Trần Văn Gamma",
                    SourceCode = "event",
                    RelationshipStatus = CustomerRelationshipStatus.InProgress,
                    Note = "Khách hàng gặp tại sự kiện triển lãm.",
                },
                new CustomerContact
                {
                    FullName = "Lê Hồng Cường",
                    Position = "Kế toán trưởng",
                    Phone = "0900000303",
                    IsPrimary = true,
                }
            ),
        };

        foreach (var (customer, contact) in samples)
        {
            if (db.Customers.Any(c => c.Name == customer.Name)) continue;

            customer.OwnerUserId = owner.Id;
            customer.CreatedByUserId = owner.Id;
            customer.UpdatedByUserId = owner.Id;
            customer.CreatedAt = now;
            customer.UpdatedAt = now;

            db.Customers.Add(customer);
            db.SaveChanges();

            contact.CustomerId = customer.Id;
            contact.CreatedAt = now;
            contact.UpdatedAt = now;
            db.CustomerContacts.Add(contact);

            db.CustomerActivities.Add(new CustomerActivity
            {
                CustomerId = customer.Id,
                Type = CustomerActivityType.Note,
                OccurredAt = now,
                Content = "Ghi chú mẫu khởi tạo cho khách hàng demo.",
                CreatedByUserId = owner.Id,
                CreatedAt = now,
            });

            db.SaveChanges();
        }
    }

    private static void SeedOpportunities(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        var sampleCustomers = db.Customers
            .Where(c => c.Name.StartsWith(SampleTag))
            .OrderBy(c => c.Id)
            .ToList();
        if (sampleCustomers.Count == 0) return;

        var samples = new (string Name, decimal Value, int Probability, OpportunityStage Stage, int CloseDaysFromNow)[]
        {
            ($"{SampleTag} Nhà máy Alpha - Giai đoạn 1", 4_500_000_000m, 60, OpportunityStage.Qualification, 45),
            ($"{SampleTag} Beta Interior - Showroom Q3", 2_100_000_000m, 35, OpportunityStage.Prospecting, 90),
            ($"{SampleTag} Gamma Home - Cải tạo văn phòng", 1_250_000_000m, 75, OpportunityStage.Proposal, 30),
            ($"{SampleTag} Beta Interior - Nhà xưởng phụ", 3_800_000_000m, 55, OpportunityStage.Negotiation, 60),
            ($"{SampleTag} Alpha - Mở rộng kho", 800_000_000m, 100, OpportunityStage.Won, -10),
            ($"{SampleTag} Trần Thị Bảo - Nhà phố", 350_000_000m, 0, OpportunityStage.Lost, -20),
        };

        for (var i = 0; i < samples.Length; i++)
        {
            var (name, value, probability, stage, closeDays) = samples[i];
            if (db.Opportunities.Any(o => o.Name == name)) continue;

            var customer = sampleCustomers[i % sampleCustomers.Count];
            var closeDate = now.AddDays(closeDays);

            var op = new Opportunity
            {
                Name = name,
                CustomerId = customer.Id,
                OwnerUserId = owner.Id,
                EstimatedValue = value,
                WinProbability = probability,
                ExpectedCloseDate = closeDate,
                Stage = stage,
                ClosedAt = stage is OpportunityStage.Won or OpportunityStage.Lost ? closeDate : null,
                LostReasonCode = stage == OpportunityStage.Lost ? "price" : null,
                LostNote = stage == OpportunityStage.Lost ? "Khách hàng cân nhắc lại vì ngân sách." : null,
                Note = "Cơ hội mẫu — demo pipeline & stage transition.",
                CreatedAt = now,
                UpdatedAt = now,
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
            };
            db.Opportunities.Add(op);
            db.SaveChanges();

            db.OpportunityActivities.Add(new OpportunityActivity
            {
                OpportunityId = op.Id,
                Type = OpportunityActivityType.Note,
                OccurredAt = now,
                Content = "Ghi chú mẫu khởi tạo cho cơ hội demo.",
                CreatedByUserId = owner.Id,
                CreatedAt = now,
            });
            db.SaveChanges();
        }
    }

    private static void SeedQuotes(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        var sampleOpps = db.Opportunities
            .Where(o => o.Name.StartsWith(SampleTag))
            .OrderBy(o => o.Id)
            .ToList();
        if (sampleOpps.Count == 0) return;

        // Deterministic per-opportunity samples covering every non-terminal
        // state so the admin UI has something to demo (filters, workflow
        // buttons, versioning, expiring badge) without hand-crafting quotes.
        // Method/status/items tuned so each shape gets exercised.
        var seeds = new (int OppIndex, QuoteMethod Method, QuoteStatus Status, int ValidDays, string Note)[]
        {
            (0, QuoteMethod.UnitCost, QuoteStatus.Draft,           30, "Báo giá mẫu — nháp, suất đầu tư."),
            (1, QuoteMethod.Boq,      QuoteStatus.PendingApproval, 30, "Báo giá mẫu — chờ duyệt nội bộ, BOQ."),
            (2, QuoteMethod.UnitCost, QuoteStatus.Approved,        30, "Báo giá mẫu — đã duyệt, sẵn gửi khách."),
            (3, QuoteMethod.Boq,      QuoteStatus.SentToCustomer,   2, "Báo giá mẫu — đã gửi khách, sắp hết hạn."),
        };

        var year = now.Year;
        // Compute the next sequential code once so we can assign quotes
        // deterministically without racing GenerateCodeAsync in the service.
        var nextSeq = 1 + (db.Quotes.AsNoTracking()
            .Where(q => q.Code.StartsWith($"QT-{year}-"))
            .Select(q => q.Code)
            .AsEnumerable()
            .Select(c => int.TryParse(c.AsSpan($"QT-{year}-".Length), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max());

        for (var i = 0; i < seeds.Length; i++)
        {
            var (oppIndex, method, status, validDays, note) = seeds[i];
            if (oppIndex >= sampleOpps.Count) continue;

            var opp = sampleOpps[oppIndex];
            // Idempotency: skip if any quote already exists for this
            // opportunity — a partial re-seed would otherwise stack duplicates.
            if (db.Quotes.Any(q => q.OpportunityId == opp.Id)) continue;

            var code = $"QT-{year}-{nextSeq++:D4}";
            var validUntil = now.AddDays(validDays);
            var quote = new Quote
            {
                Code = code,
                OpportunityId = opp.Id,
                OwnerUserId = opp.OwnerUserId,
                Method = method,
                Version = 1,
                DiscountPercent = i == 3 ? 5m : 0m,
                VatPercent = 8m,
                ValidUntil = validUntil,
                Note = note,
                Status = status,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
            };

            if (method == QuoteMethod.UnitCost)
            {
                quote.AreaSqm = 120m + oppIndex * 40m;
                quote.UnitPricePerSqm = 8_000_000m + oppIndex * 1_500_000m;
                quote.PackageDescription = "Gói mẫu bao gồm thi công phần thô + hoàn thiện cơ bản.";
            }
            else
            {
                quote.Items = new List<QuoteItem>
                {
                    NewItem("BOQ-001", "Bê tông móng M300", "m3", 25m, 1_450_000m, 1),
                    NewItem("BOQ-002", "Cốt thép D16",       "kg", 1_200m, 24_500m, 2),
                    NewItem("BOQ-003", "Ván khuôn thép",     "m2", 180m,   85_000m, 3),
                    NewItem("BOQ-004", "Sơn Dulux nội thất", "m2", 350m,   75_000m, 4),
                };
            }

            RecomputeTotals(quote);

            // Workflow timestamps + approval log match the seeded status.
            switch (status)
            {
                case QuoteStatus.PendingApproval:
                    quote.SubmittedAt = now;
                    quote.SubmittedByUserId = owner.Id;
                    break;
                case QuoteStatus.Approved:
                    quote.SubmittedAt = now.AddMinutes(-30);
                    quote.SubmittedByUserId = owner.Id;
                    quote.ApprovedAt = now.AddMinutes(-10);
                    quote.ApprovedByUserId = owner.Id;
                    break;
                case QuoteStatus.SentToCustomer:
                    quote.SubmittedAt = now.AddHours(-2);
                    quote.SubmittedByUserId = owner.Id;
                    quote.ApprovedAt = now.AddHours(-1);
                    quote.ApprovedByUserId = owner.Id;
                    quote.SentAt = now.AddMinutes(-15);
                    quote.SentByUserId = owner.Id;
                    break;
            }

            db.Quotes.Add(quote);
            db.SaveChanges();

            db.QuoteApprovalLogs.Add(new QuoteApprovalLog
            {
                QuoteId = quote.Id,
                Action = QuoteWorkflowAction.Create,
                FromStatus = null,
                ToStatus = QuoteStatus.Draft,
                ByUserId = owner.Id,
                Note = "Seed mẫu.",
                CreatedAt = now,
            });
            if (status >= QuoteStatus.PendingApproval && status <= QuoteStatus.SentToCustomer)
            {
                AppendLog(db, quote.Id, QuoteWorkflowAction.Submit, QuoteStatus.Draft, QuoteStatus.PendingApproval, owner.Id, quote.SubmittedAt ?? now);
            }
            if (status >= QuoteStatus.Approved && status <= QuoteStatus.SentToCustomer)
            {
                AppendLog(db, quote.Id, QuoteWorkflowAction.Approve, QuoteStatus.PendingApproval, QuoteStatus.Approved, owner.Id, quote.ApprovedAt ?? now);
            }
            if (status == QuoteStatus.SentToCustomer)
            {
                AppendLog(db, quote.Id, QuoteWorkflowAction.Send, QuoteStatus.Approved, QuoteStatus.SentToCustomer, owner.Id, quote.SentAt ?? now);
            }
            db.SaveChanges();
        }
    }

    private static QuoteItem NewItem(string code, string name, string unit, decimal qty, decimal price, int sort) => new()
    {
        ItemCode = code,
        Name = name,
        Unit = unit,
        Quantity = qty,
        UnitPrice = price,
        Amount = decimal.Round(qty * price, 2, MidpointRounding.AwayFromZero),
        SortOrder = sort,
    };

    private static void RecomputeTotals(Quote q)
    {
        var subtotal = q.Method == QuoteMethod.Boq
            ? q.Items.Sum(i => i.Amount)
            : decimal.Round((q.AreaSqm ?? 0m) * (q.UnitPricePerSqm ?? 0m), 2, MidpointRounding.AwayFromZero);
        var afterDiscount = subtotal * (1 - q.DiscountPercent / 100m);
        var vat = afterDiscount * (q.VatPercent / 100m);
        q.Subtotal = decimal.Round(subtotal, 2, MidpointRounding.AwayFromZero);
        q.GrandTotal = decimal.Round(afterDiscount + vat, 2, MidpointRounding.AwayFromZero);
    }

    private static void AppendLog(AppDbContext db, int quoteId, QuoteWorkflowAction action,
        QuoteStatus from, QuoteStatus to, int userId, DateTime at)
    {
        db.QuoteApprovalLogs.Add(new QuoteApprovalLog
        {
            QuoteId = quoteId,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            ByUserId = userId,
            CreatedAt = at,
        });
    }
}
